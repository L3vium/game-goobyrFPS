# Author : Baptiste Vu Liem
"""The game's characters: low-poly, flat-coloured, long arms and big soft ears (headless Blender 5.2).

  blender -b --factory-startup --python Tools/Blender/characters/wobbly/wobbly_build.py -- all
  blender -b --factory-startup --python Tools/Blender/characters/wobbly/wobbly_build.py -- Chr_Rook Chr_Guard_Snow

Same 53-bone skeleton and axes as the clips (cast/skeleton.py, ref_axes.json), plus ear_l and ear_r
under the head, so every animation plays as it is. Writes to Tools/Blender/out/wobbly/<Name>/:
<Name>.fbx and <Name>_flat.json (slot -> colour, read by Goobyr/Build/Characters). Copy them over
Assets/_Game/Art/Characters/<Name>/ under the editor lock.
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
CAST = os.path.join(os.path.dirname(HERE), "cast")
sys.path.insert(0, CAST)
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT = os.path.join(PROJECT, "Tools", "Blender", "out", "wobbly")

import skeleton  # noqa: E402

REF_AXES = json.load(open(os.path.join(CAST, "ref_axes.json")))
# The shared glove hand's bone layout (cast/glove.py), without importing its mesh code's dependencies.
import glove  # noqa: E402


# Long, dangling arms: the same clips play, the hands just hang lower (Humanoid retargets by rotation).
ARM_SCALE = 1.32


def V(*a):
    return np.array(a, dtype=np.float64)


class Mesh:
    """Parts accumulated into one skinned mesh: vertices with bone weights, faces with a slot."""

    def __init__(self):
        self.verts, self.weights, self.faces, self.slots = [], [], [], []

    def add(self, verts, faces, slot, weights):
        base = len(self.verts)
        self.verts.extend([tuple(v) for v in verts])
        self.weights.extend(weights)
        for f in faces:
            self.faces.append(tuple(i + base for i in f))
            self.slots.append(slot)


def frame(axis):
    axis = axis / np.linalg.norm(axis)
    side = V(1, 0, 0) if abs(axis[0]) < 0.9 else V(0, 1, 0)
    u = side - axis * np.dot(side, axis)
    u /= np.linalg.norm(u)
    return u, np.cross(axis, u)


def chain(m, points, radii, weights, slot, sides=6, phase=0.5):
    """A faceted tube through points; radii are (rx, ry) or r per point, weights a {bone: w} per point."""
    points = [np.asarray(p, dtype=np.float64) for p in points]
    verts, faces, w = [], [], []
    for i, p in enumerate(points):
        a = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        u, v = frame(a)
        r = radii[i] if isinstance(radii[i], tuple) else (radii[i], radii[i])
        for k in range(sides):
            t = (k + phase) / sides * math.tau
            verts.append(p + u * math.cos(t) * r[0] + v * math.sin(t) * r[1])
            w.append(weights[i])
    for i in range(len(points) - 1):
        for k in range(sides):
            a, b = i * sides + k, i * sides + (k + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range(sides)))
    faces.append(tuple(range((len(points) - 1) * sides, len(points) * sides)))
    m.add(verts, faces, slot, w)


def box(m, centre, size, slot, bone, turn=0.0, taper=1.0):
    """A box, optionally turned about Z and narrowing toward its top."""
    c, s = math.cos(turn), math.sin(turn)
    verts = []
    for z in (-1, 1):
        k = taper if z > 0 else 1.0
        for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            px, py = x * size[0] / 2 * k, y * size[1] / 2 * k
            verts.append(np.asarray(centre) + V(px * c - py * s, px * s + py * c, z * size[2] / 2))
    faces = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    m.add(verts, faces, slot, [bone if isinstance(bone, dict) else {bone: 1.0}] * 8)


def blob(m, centre, radii, slot, bone, subdivisions=2, clip_below=None, seed=0):
    """A faceted ball (icosphere), squashed to radii, optionally cut flat under a height."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    rng = np.random.default_rng(seed)
    verts = []
    for v in bm.verts:
        p = np.array(v.co, dtype=np.float64) * (1.0 + (rng.uniform(-0.035, 0.035) if seed else 0.0))
        p = np.asarray(centre) + p * np.asarray(radii)
        if clip_below is not None:
            p[2] = max(p[2], clip_below)
        verts.append(p)
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    m.add(verts, faces, slot, [bone if isinstance(bone, dict) else {bone: 1.0}] * len(verts))


def ear(m, sign, slot, size):
    """A big soft leaf of an ear on its own bone: narrow where it joins the head, wide and round at the
    tip, a little lifted, with a darker hollow on its front."""
    root = V(sign * 0.150, 0.0, 1.625)
    bone = "ear_" + ("l" if sign > 0 else "r")
    l, h = 0.27 * size, 0.135 * size
    outline = [(0.0, -0.20), (0.18, -0.34), (0.42, -0.62), (0.68, -0.72), (0.90, -0.52), (1.0, -0.06),
               (0.92, 0.42), (0.70, 0.74), (0.42, 0.70), (0.18, 0.40), (0.0, 0.22)]
    thick = 0.012

    def at(u, t):
        # Lifted along its length and curling back at the tip.
        return root + V(sign * u * l, 0.05 * u * u, t * h + 0.09 * u)

    front = [at(u, t) + V(0, -thick, 0) for u, t in outline]
    back = [at(u, t) + V(0, thick, 0) for u, t in outline]
    n = len(outline)
    faces = [tuple(range(n)), tuple(range(n, 2 * n))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    m.add(front + back, faces, slot, [{bone: 1.0}] * (2 * n))

    # The hollow: a smaller leaf just in front.
    inner = [at(0.14 + u * 0.72, t * 0.68) + V(0, -thick - 0.004, 0) for u, t in outline[1:-1]]
    m.add(inner, [tuple(range(len(inner)))], "EarIn", [{bone: 1.0}] * len(inner))


def body(m, spec, s):
    """Torso, legs, arms, hands, feet, head: everyone's shapes, in their colours."""
    j = s.j
    heavy = spec.get("build", 1.0)
    female = spec.get("female", False)
    chest = (0.17 * heavy, 0.115 * heavy) if not female else (0.15, 0.105)
    hip = (0.155 * heavy, 0.115 * heavy) if not female else (0.165, 0.115)

    # Torso: jacket or shirt, from the hips to the base of the neck.
    chain(m, [j["pelvis"] + V(0, 0, -0.085), j["pelvis"], j["spine_01"], j["spine_02"], j["spine_03"], V(0, 0.015, 1.33), V(0, 0.012, 1.425)],
          [(hip[0] * 0.9, hip[1] * 0.9), hip, (hip[0] * 0.96, hip[1]), (chest[0] * 0.92, chest[1]), chest, (chest[0] * 1.02, chest[1] * 0.98), (0.08, 0.07)],
          [{"pelvis": 1.0}, {"pelvis": 1.0}, {"spine_01": 1.0}, {"spine_02": 1.0}, {"spine_03": 1.0}, {"spine_03": 1.0}, {"neck_01": 0.5, "spine_03": 0.5}],
          "Top", sides=8)
    # Neck.
    chain(m, [V(0, 0.012, 1.40), j["neck"], V(0, 0.005, 1.53)], [0.055, 0.052, 0.055], [{"spine_03": 0.5, "neck_01": 0.5}, {"neck_01": 1.0}, {"head": 1.0}], "Skin", sides=6)

    for side, sx in (("l", 1.0), ("r", -1.0)):
        # Arms: one sleeve, folding at the elbow.
        sh, el, wr = j["shoulder_" + side], j["elbow_" + side], j["wrist_" + side]
        ua, la = "upperarm_" + side, "lowerarm_" + side
        cuff = (spec.get("short_sleeves") and 0.45) or 1.0
        chain(m, [sh + V(-sx * 0.02, 0, 0.02), sh, (sh + el) / 2, el, el + (wr - el) * 0.5 * cuff if cuff < 1 else (el + wr) / 2, wr if cuff == 1 else el + (wr - el) * 0.5 * cuff + (wr - el) * 0.01],
              [0.04, 0.062, 0.055, 0.048, 0.045, 0.04],
              [{"clavicle_" + side: 1.0}, {ua: 1.0}, {ua: 1.0}, {ua: 0.5, la: 0.5}, {la: 1.0}, {la: 1.0}], "Top", sides=6)
        if cuff < 1:
            chain(m, [el, wr], [0.04, 0.036], [{la: 1.0}, {la: 1.0}], "Skin", sides=6)
        # A mitten hand: palm and thumb, bare or gloved.
        o, u, v, w = s.hand_frame(side)
        hand = "hand_" + side
        hand_slot = spec.get("hands", "Skin")
        blob(m, o + u * 0.075, (0.085, 0.088, 0.07), hand_slot, hand, subdivisions=1)
        chain(m, [o + u * 0.04 + v * 0.04, o + u * 0.11 + v * 0.1], [0.027, 0.022], [{hand: 1.0}, {hand: 1.0}], hand_slot, sides=5)

        # Legs: one trouser, folding at the knee.
        hp, kn, an = j["hip_" + side], j["knee_" + side], j["ankle_" + side]
        th, ca = "thigh_" + side, "calf_" + side
        chain(m, [hp + V(0, 0, 0.04), hp, (hp + kn) / 2, kn, (kn + an) / 2, an + V(0, 0, 0.02)],
              [0.08, 0.085 * heavy, 0.072 * heavy, 0.058, 0.055, 0.048],
              [{"pelvis": 1.0}, {th: 1.0}, {th: 1.0}, {th: 0.5, ca: 0.5}, {ca: 1.0}, {ca: 1.0}], "Bottom", sides=6)

        # Shoes: a heel and a toe, each on its own bone.
        ball = j["ball_" + side]
        foot, toe = "foot_" + side, "ball_" + side
        box(m, V(an[0], an[1] - 0.03, 0.05), (0.098, 0.15, 0.10), "Shoes", foot, taper=0.86)
        box(m, V(ball[0], ball[1] - 0.025, 0.036), (0.104, 0.15, 0.072), "Shoes", toe, taper=0.8)

    # The head: a tall egg, a nose, small eyes set wide, and the ears.
    blob(m, V(0, 0.0, 1.615), (0.165, 0.165, 0.195), "Skin", "head", subdivisions=2, seed=3)
    box(m, V(0, -0.168, 1.585), (0.04, 0.05, 0.05), "Skin", "head", taper=0.7)
    for sign in (1, -1):
        blob(m, V(sign * 0.07, -0.142, 1.645), (0.034, 0.016, 0.042), "Eyes", "head", subdivisions=1)
        blob(m, V(sign * 0.07, -0.156, 1.642), (0.014, 0.008, 0.018), "Pupil", "head", subdivisions=1)
        box(m, V(sign * 0.072, -0.148, 1.70), (0.066, 0.012, 0.012), "Hair", "head")
        ear(m, sign, spec.get("ear_slot", "Skin"), spec.get("ears", 1.0))


def headwear(m, kind):
    if kind == "none":
        return
    if kind == "cap":
        blob(m, V(0, 0.005, 1.70), (0.172, 0.172, 0.125), "Hat", "head", subdivisions=2, clip_below=1.685)
        box(m, V(0, -0.20, 1.69), (0.17, 0.12, 0.015), "Hat", "head")
    elif kind == "helmet":
        blob(m, V(0, 0.005, 1.68), (0.19, 0.19, 0.16), "Hat", "head", subdivisions=2, clip_below=1.645)
    elif kind == "beanie":
        blob(m, V(0, 0.005, 1.69), (0.178, 0.178, 0.155), "Hat", "head", subdivisions=2, clip_below=1.67)
        blob(m, V(0, 0.005, 1.845), (0.04, 0.04, 0.04), "Hat2", "head", subdivisions=1)
    elif kind == "keffiyeh":
        blob(m, V(0, 0.02, 1.67), (0.182, 0.182, 0.17), "Hat", "head", subdivisions=2, clip_below=1.60)
        box(m, V(0, 0.15, 1.52), (0.24, 0.05, 0.22), "Hat", "head", taper=1.2)
    elif kind == "fedora":
        blob(m, V(0, 0.005, 1.74), (0.145, 0.145, 0.10), "Hat", "head", subdivisions=2, clip_below=1.71)
        chain(m, [V(0, 0.005, 1.70), V(0, 0.005, 1.722)], [0.26, 0.25], [{"head": 1.0}, {"head": 1.0}], "Hat", sides=10)
    elif kind == "chef":
        blob(m, V(0, 0.005, 1.84), (0.15, 0.15, 0.11), "Hat", "head", subdivisions=2)
        chain(m, [V(0, 0.005, 1.70), V(0, 0.005, 1.80)], [0.135, 0.13], [{"head": 1.0}, {"head": 1.0}], "Hat", sides=8)


def hair(m, kind):
    if kind == "short":
        blob(m, V(0, 0.015, 1.68), (0.172, 0.17, 0.145), "Hair", "head", subdivisions=2, clip_below=1.665, seed=5)
    elif kind == "bun":
        blob(m, V(0, 0.015, 1.68), (0.172, 0.17, 0.145), "Hair", "head", subdivisions=2, clip_below=1.665, seed=5)
        blob(m, V(0, 0.13, 1.79), (0.065, 0.065, 0.065), "Hair", "head", subdivisions=1)
    elif kind == "long":
        blob(m, V(0, 0.02, 1.67), (0.178, 0.178, 0.16), "Hair", "head", subdivisions=2, clip_below=1.60, seed=5)
        box(m, V(0, 0.13, 1.46), (0.27, 0.07, 0.30), {"spine_03": 0.5, "head": 0.5}, "Hair" and {"spine_03": 0.5, "head": 0.5}) if False else None
        box(m, V(0, 0.13, 1.46), (0.27, 0.07, 0.30), "Hair", {"spine_03": 0.5, "head": 0.5}, taper=1.15)


def extras(m, kinds, s):
    j = s.j
    for kind in kinds:
        if kind == "tie":
            # Shirt front and the team's tie: the agents' signature.
            box(m, V(0, -0.112, 1.325), (0.09, 0.012, 0.15), "Shirt", "spine_03", taper=1.45)
            box(m, V(0, -0.122, 1.28), (0.034, 0.012, 0.21), "Team", "spine_03", taper=0.7)
        elif kind == "vest":
            box(m, V(0, -0.008, 1.24), (0.355, 0.255, 0.30), "Gear", "spine_03", taper=0.96)
            box(m, V(-0.075, -0.135, 1.20), (0.06, 0.03, 0.09), "Gear2", "spine_03")
            box(m, V(0.075, -0.135, 1.20), (0.06, 0.03, 0.09), "Gear2", "spine_03")
        elif kind == "belt":
            chain(m, [j["pelvis"] + V(0, 0, -0.02), j["pelvis"] + V(0, 0, 0.03)], [(0.165, 0.125), (0.165, 0.125)], [{"pelvis": 1.0}, {"pelvis": 1.0}], "Gear2", sides=8)
        elif kind == "shades":
            box(m, V(0, -0.158, 1.648), (0.20, 0.012, 0.042), "Gear2", "head")
        elif kind == "apron":
            box(m, V(0, -0.135, 0.92), (0.26, 0.018, 0.56), "Shirt", {"pelvis": 0.7, "spine_01": 0.3})
        elif kind == "lapels":
            box(m, V(0, -0.116, 1.31), (0.11, 0.012, 0.17), "Shirt", "spine_03", taper=1.3)
        elif kind == "collar":
            chain(m, [V(0, 0.01, 1.42), V(0, 0.01, 1.47)], [0.09, 0.085], [{"spine_03": 1.0}, {"neck_01": 1.0}], "Gear", sides=8)
        elif kind == "skirt":
            chain(m, [V(0, 0, 0.94), V(0, 0, 0.60)], [(0.165, 0.12), (0.22, 0.17)], [{"pelvis": 1.0}, {"pelvis": 1.0}], "Top", sides=8)


# ---------------------------------------------------------------- the cast

BLACK = (0.13, 0.13, 0.17)
SHOE = (0.15, 0.13, 0.13)
WHITE = (0.92, 0.92, 0.9)
EYE = (0.97, 0.97, 0.95)
PUPIL = (0.06, 0.06, 0.09)
TEAM = (1.0, 0.45, 0.2)

SKINS = [(0.98, 0.72, 0.56), (0.86, 0.58, 0.42), (0.62, 0.40, 0.28), (1.0, 0.78, 0.64)]


def look(top, bottom, skin=SKINS[0], shoes=SHOE, hair_col=(0.2, 0.15, 0.12), **more):
    c = {"Top": top, "Bottom": bottom, "Skin": skin, "Shoes": shoes, "Hair": hair_col, "Eyes": EYE, "Pupil": PUPIL,
         "Team": TEAM, "Shirt": WHITE, "Hat": BLACK, "Hat2": WHITE, "Gear": BLACK, "Gear2": (0.24, 0.22, 0.2),
         "EarIn": (skin[0] * 0.86, skin[1] * 0.62, skin[2] * 0.62)}
    c.update(more)
    return c


SPECS = {
    # Agents: a dark suit, a tie in the team's colour.
    "Chr_Rook": dict(colours=look((0.17, 0.18, 0.24), (0.14, 0.15, 0.2)), hair="none", hat="none", extras=["tie"], hands="Skin", ears=1.1),
    "Chr_Rook_F": dict(colours=look((0.17, 0.18, 0.24), (0.14, 0.15, 0.2), skin=SKINS[1], hair_col=(0.25, 0.13, 0.1)), female=True, hair="bun", hat="none", extras=["tie"], ears=1.0),
    # Guards dress for the map's climate.
    "Chr_Guard_Jungle": dict(colours=look((0.43, 0.5, 0.3), (0.34, 0.37, 0.25), skin=SKINS[1], Hat=(0.34, 0.4, 0.23), Gear=(0.27, 0.3, 0.2), Gear2=(0.16, 0.16, 0.13)), hair="none", hat="cap", extras=["vest", "belt"], ears=0.95),
    "Chr_Guard_Desert": dict(colours=look((0.8, 0.68, 0.48), (0.68, 0.56, 0.38), skin=SKINS[2], Hat=(0.92, 0.88, 0.76), Gear=(0.52, 0.42, 0.3), Gear2=(0.3, 0.24, 0.18)), hair="none", hat="keffiyeh", extras=["vest", "belt"], ears=0.8),
    "Chr_Guard_Snow": dict(colours=look((0.72, 0.8, 0.88), (0.5, 0.56, 0.66), skin=SKINS[3], Hat=(0.32, 0.38, 0.52), Hat2=(0.92, 0.94, 0.96), Gear=(0.38, 0.44, 0.56), Gear2=(0.2, 0.22, 0.28)), hair="none", hat="beanie", extras=["vest", "belt"], ears=0.9),
    "Chr_Bodyguard": dict(colours=look((0.1, 0.1, 0.13), (0.1, 0.1, 0.13), skin=SKINS[2], Gear2=(0.05, 0.05, 0.07)), build=1.18, hair="short", hat="none", extras=["lapels", "shades"], ears=0.85),
    # Targets stand out.
    "Chr_VIP_M": dict(colours=look((0.82, 0.3, 0.32), (0.92, 0.88, 0.8), hair_col=(0.8, 0.78, 0.74), Hat=(0.94, 0.9, 0.8), Gear2=(0.12, 0.1, 0.1)), build=1.1, hair="short", hat="fedora", extras=["lapels", "shades"], ears=1.0),
    "Chr_VIP_F": dict(colours=look((0.52, 0.28, 0.6), (0.52, 0.28, 0.6), skin=SKINS[3], hair_col=(0.8, 0.62, 0.3), Gear2=(0.1, 0.08, 0.12)), female=True, hair="long", hat="none", extras=["skirt", "shades"], ears=0.9),
    # The rest of the old cast, kept so every reference still finds a body.
    "Chr_Base_Slim": dict(colours=look((0.45, 0.62, 0.72), (0.3, 0.32, 0.42)), build=0.92, hair="short", hat="none", extras=[], short_sleeves=True),
    "Chr_Base_Average": dict(colours=look((0.84, 0.6, 0.34), (0.32, 0.34, 0.44), skin=SKINS[1]), hair="short", hat="none", extras=[], short_sleeves=True),
    "Chr_Base_Heavy": dict(colours=look((0.56, 0.72, 0.5), (0.4, 0.34, 0.3), skin=SKINS[2]), build=1.25, hair="short", hat="none", extras=[], short_sleeves=True),
    "Chr_Guest_M": dict(colours=look((0.88, 0.5, 0.42), (0.86, 0.82, 0.7)), hair="short", hat="none", extras=[], short_sleeves=True),
    "Chr_Guest_F": dict(colours=look((0.44, 0.7, 0.72), (0.44, 0.7, 0.72), skin=SKINS[1]), female=True, hair="long", hat="none", extras=["skirt"], short_sleeves=True),
    "Chr_Waiter": dict(colours=look((0.92, 0.92, 0.9), BLACK, Team=(0.1, 0.1, 0.13)), hair="short", hat="none", extras=["tie"]),
    "Chr_Cook": dict(colours=look((0.94, 0.94, 0.92), (0.3, 0.32, 0.36), Hat=(0.96, 0.96, 0.94)), hair="none", hat="chef", extras=["apron"]),
    "Chr_Maid": dict(colours=look((0.2, 0.2, 0.28), (0.2, 0.2, 0.28), skin=SKINS[2]), female=True, hair="bun", hat="none", extras=["apron", "skirt"]),
    "Chr_Mechanic": dict(colours=look((0.3, 0.42, 0.6), (0.3, 0.42, 0.6), Hat=(0.8, 0.3, 0.2)), hair="none", hat="cap", extras=["belt"]),
}
ORDER = list(SPECS)


def build_mesh(name, spec, s):
    m = Mesh()
    body(m, spec, s)
    headwear(m, spec.get("hat", "none"))
    hair(m, spec.get("hair", "none"))
    extras(m, spec.get("extras", []), s)
    return m


def make_armature(s, coll):
    bones = s.bones(glove.LAYOUT)
    ad = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", ad)
    coll.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for n in skeleton.BONE_NAMES:
        eb = ad.edit_bones.new(n)
        h, t = bones[n]
        eb.head, eb.tail = Vector(h), Vector(t)
        eb.use_deform = n != "Root"
    for n in skeleton.BONE_NAMES:
        p = skeleton.PARENT[n]
        if p:
            ad.edit_bones[n].parent = ad.edit_bones[p]
            ad.edit_bones[n].use_connect = False
        ad.edit_bones[n].align_roll(Vector(REF_AXES[n]))
    # The ears: not part of the humanoid, free to flop (HumanWobble finds them by name).
    for side, sign in (("l", 1.0), ("r", -1.0)):
        eb = ad.edit_bones.new("ear_" + side)
        eb.head = Vector((sign * 0.15, 0.0, 1.625))
        eb.tail = Vector((sign * 0.34, 0.0, 1.66))
        eb.parent = ad.edit_bones["head"]
        eb.use_deform = True
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def export(name):
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    spec = SPECS[name]
    s = skeleton.Build(shoulder_x=0.20 if not spec.get("female") else 0.18, hip_x=0.09, female=spec.get("female", False), arm_scale=ARM_SCALE)
    coll = bpy.context.scene.collection
    arm = make_armature(s, coll)
    m = build_mesh(name, spec, s)

    me = bpy.data.meshes.new(name + "_LOD0")
    me.from_pydata(m.verts, [], m.faces)
    me.update()
    slots = sorted(set(m.slots))
    for slot in slots:
        mat = bpy.data.materials.new("%s_%s" % (name, slot))
        me.materials.append(mat)
    idx = {slot: i for i, slot in enumerate(slots)}
    me.polygons.foreach_set("material_index", [idx[slot] for slot in m.slots])
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name + "_LOD0", me)
    coll.objects.link(ob)
    for i, w in enumerate(m.weights):
        for bone, value in w.items():
            g = ob.vertex_groups.get(bone) or ob.vertex_groups.new(name=bone)
            g.add([i], value, "REPLACE")
    ob.parent = arm
    ob.modifiers.new("Armature", "ARMATURE").object = arm

    out_dir = os.path.join(OUT, name)
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, name + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0, add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=True, armature_nodetype="NULL",
                             mesh_smooth_type="FACE", use_tspace=False, use_mesh_modifiers=False, path_mode="STRIP", embed_textures=False, bake_anim=False)
    json.dump({"slots": {slot: spec["colours"][slot] for slot in slots}}, open(os.path.join(out_dir, name + "_flat.json"), "w"), indent=1)
    print("[wobbly]", name, len(m.verts), "verts", len(m.faces), "faces", flush=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["all"]
    names = ORDER if "all" in args else args
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for n in names:
        export(n)
    print("[wobbly] DONE", flush=True)


if __name__ == "__main__":
    main()
