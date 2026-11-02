# Author : Baptiste Vu Liem
"""The first-person arms rig and its placeholder glove and sleeve.

Generic rig (not Humanoid), rooted at the camera (the eye, origin of the scene):

    root (the eye) - spine_03 - clavicle_l/r - upperarm - lowerarm - hand - fingers
                                                       \\- lowerarm_twist_01 / _02
    hand_r - weapon_r : the socket, a frame equal to the held weapon's Grip frame (the model is
                        parented there at identity in Unity); hand_l - prop_l : a magazine in the
                        support hand during reloads.

Shoulders 0.29 m below and 0.13 m behind the eye, 0.36 m apart; upper arm 0.30, forearm 0.265.
Two twist bones carry 40 % and 75 % of the hand's roll down the forearm, so a palm-up support hand
twists the sleeve evenly instead of wringing the wrist.

The mesh is a placeholder with the cast track's glove spec (thumb, separate index, the middle/ring/
pinky fused into one paddle, each still on its own bone chain) under an olive bomber sleeve ending
in a ribbed knit cuff. Parts are voxel-unioned, baked to one PBR atlas (pbr_lib) and weighted from
capsule distances. When the cast module exists, build_mesh() takes its parts instead.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

import fp_common as F
from fp_common import L

# The cast track's shared glove / forearm module (Tools/Blender/characters/cast/glove.py): the hand
# layout, the glove, the bomber sleeve and the rib cuff of the hero. GOOBYR_FP_PLACEHOLDER=1 builds
# the placeholder hand of this file instead.
CAST_DIR = os.path.join(F.ROOT, "Tools", "Blender", "characters", "cast")
CG = None
if os.environ.get("GOOBYR_FP_PLACEHOLDER") != "1" and os.path.exists(os.path.join(CAST_DIR, "glove.py")):
    sys.path.insert(0, CAST_DIR)
    try:
        import glove as CG  # noqa: E402
    except Exception as e:  # the module is optional: the placeholder still builds
        print("CAST GLOVE UNAVAILABLE:", e)
        CG = None
if CG is not None:
    # first person: the cast's glove shape at a near-real size (its 1.10 'readable' hand, a hand-width
    # from the eye, crowds the view and overshoots the triggers); bones and mesh both read these
    CG.SCALE = float(os.environ.get("GOOBYR_FP_HAND_SCALE", "0.97"))
    CG.LAYOUT = {k: ([tuple(c * CG.SCALE for c in p) for p in v] if isinstance(v, list)
                     else tuple(c * CG.SCALE for c in v)) for k, v in CG.HAND.items()}

SIDES = ("r", "l")
FINGERS = ("index", "middle", "ring", "pinky")
PADDLE = ("middle", "ring", "pinky")

EYE = Vector((0.0, 0.0, 0.0))
UPPER_ARM = 0.30
FOREARM = 0.265
HAND = 0.088
TWIST = {"lowerarm_twist_01": 0.40, "lowerarm_twist_02": 0.75}

# Finger layout in the hand frame (f along the fingers, t towards the thumb side, p towards the palm):
# knuckle (MCP) position, phalanx lengths, glove radius at the base and the tip.
FINGER_SPEC = {
    "index": dict(mcp=(0.090, 0.0365, 0.0), lengths=(0.046, 0.027, 0.024), r=(0.0122, 0.0104), splay=6.0),
    "middle": dict(mcp=(0.095, 0.0085, 0.0), lengths=(0.050, 0.031, 0.025), r=(0.0122, 0.0108), splay=0.0),
    "ring": dict(mcp=(0.090, -0.0135, 0.0), lengths=(0.047, 0.030, 0.024), r=(0.0118, 0.0104), splay=-1.5),
    "pinky": dict(mcp=(0.081, -0.0335, 0.0015), lengths=(0.038, 0.023, 0.021), r=(0.0107, 0.0094), splay=-3.5),
}
THUMB_SPEC = dict(cmc=(0.008, 0.024, 0.015), dir=(0.80, 0.40, 0.45), lengths=(0.042, 0.033, 0.028),
                  r=(0.0155, 0.0120))
if CG is not None:
    # the cast hand's finger radii (base, tip), scaled like its layout
    for _n in FINGERS:
        FINGER_SPEC[_n]["r"] = (CG.FINGER_R[_n][0] * CG.SCALE, CG.FINGER_R[_n][-1] * CG.SCALE)
    THUMB_SPEC["r"] = (CG.FINGER_R["thumb"][0] * CG.SCALE, CG.FINGER_R["thumb"][-1] * CG.SCALE)


def cast_frame(side):
    """The cast module's hand frame for this rig's rest hand (u fingers, v thumb side, w palm)."""
    f, t, p = hand_frame(side)
    return CG.frame_from_arm(np.array(wrist(side)), np.array(f), np.array(t), side)


def side_x(side):
    """Sign of a side on Blender X (the right of the screen is -X)."""
    return -1.0 if side == "r" else 1.0


def shoulder(side):
    return Vector((side_x(side) * 0.18, 0.13, -0.29))


def arm_dir(side):
    """Rest direction of the whole arm: forward, a little down and in."""
    return Vector((-side_x(side) * 0.09, -1.0, -0.35)).normalized()


def hand_frame(side):
    """Rest hand axes (F fingers, T thumb side, P palm normal): palms facing in, thumbs up."""
    f = arm_dir(side)
    inward = Vector((-side_x(side), 0.0, 0.0))
    p = (inward - f * inward.dot(f)).normalized()
    t = f.cross(p) if side == "r" else p.cross(f)
    return f, t, p


def hand_point(side, ftp):
    """A point given in hand coordinates (f, t, p) in the rest pose."""
    f, t, p = hand_frame(side)
    return wrist(side) + f * ftp[0] + t * ftp[1] + p * ftp[2]


def elbow(side):
    return shoulder(side) + arm_dir(side) * UPPER_ARM


def wrist(side):
    return elbow(side) + arm_dir(side) * FOREARM


def finger_dirs(side, name):
    f, t, p = hand_frame(side)
    s = FINGER_SPEC[name]
    a = math.radians(s["splay"])
    return (f * math.cos(a) + t * math.sin(a)).normalized(), p


def thumb_axes(side):
    f, t, p = hand_frame(side)
    d = THUMB_SPEC["dir"]
    td = (f * d[0] + t * d[1] + p * d[2]).normalized()
    # the thumb curls across the palm, towards the little finger
    curl = p * 0.55 - t * 0.85
    curl = (curl - td * curl.dot(td)).normalized()
    return td, curl


def rest_bones():
    """name -> (head, tail, z_axis, parent) of the rest skeleton."""
    B = {}
    B["root"] = (EYE, EYE + Vector((0, -0.08, 0)), Vector((0, 0, 1)), None)
    B["spine_03"] = (Vector((0, 0.16, -0.46)), Vector((0, 0.14, -0.24)), Vector((0, -1, 0)), "root")
    for s in SIDES:
        sx = side_x(s)
        sh, el, wr = shoulder(s), elbow(s), wrist(s)
        f, t, p = hand_frame(s)
        B["clavicle_" + s] = (Vector((sx * 0.025, 0.05, -0.235)), sh, Vector((0, 0, 1)), "spine_03")
        B["upperarm_" + s] = (sh, el, p, "clavicle_" + s)
        B["lowerarm_" + s] = (el, wr, p, "upperarm_" + s)
        for tw, frac in TWIST.items():
            h = el + f * FOREARM * frac
            B[tw + "_" + s] = (h, h + f * 0.05, p, "lowerarm_" + s)
        if CG is not None:
            # the cast's shared hand: its layout places the hand and the 15 finger bones
            segs = CG.bone_segments(cast_frame(s), s)
            h, tl = segs["hand_" + s]
            B["hand_" + s] = (Vector(h), Vector(tl), p, "lowerarm_" + s)
            for name in FINGERS + ("thumb",):
                parent = "hand_" + s
                for i in range(3):
                    bn = f"{name}_0{i + 1}_{s}"
                    h, tl = segs[bn]
                    z = p
                    if name == "thumb":  # the thumb curls across the palm, towards the little finger
                        d = (Vector(tl) - Vector(h)).normalized()
                        z = p * 0.55 - t * 0.85
                        z = (z - d * z.dot(d)).normalized()
                    B[bn] = (Vector(h), Vector(tl), z, parent)
                    parent = bn
            continue
        B["hand_" + s] = (wr, wr + f * HAND, p, "lowerarm_" + s)
        for name in FINGERS:
            spec = FINGER_SPEC[name]
            d, pz = finger_dirs(s, name)
            head = hand_point(s, spec["mcp"])
            parent = "hand_" + s
            for i, ln in enumerate(spec["lengths"]):
                bn = f"{name}_0{i + 1}_{s}"
                B[bn] = (head, head + d * ln, pz, parent)
                parent = bn
                head = head + d * ln
        td, curl = thumb_axes(s)
        head = hand_point(s, THUMB_SPEC["cmc"])
        parent = "hand_" + s
        for i, ln in enumerate(THUMB_SPEC["lengths"]):
            bn = f"thumb_0{i + 1}_{s}"
            B[bn] = (head, head + td * ln, curl, parent)
            parent = bn
            head = head + td * ln
    # sockets: re-keyed in every pose, rest values are just sensible defaults
    B["weapon_r"] = (hand_point("r", (0.07, 0.0, 0.035)), hand_point("r", (0.07, 0.0, 0.035)) + Vector((0, 0, 0.04)),
                     Vector((0, -1, 0)), "hand_r")
    B["prop_l"] = (hand_point("l", (0.07, 0.0, 0.03)), hand_point("l", (0.07, 0.0, 0.03)) + Vector((0, 0, 0.04)),
                   Vector((0, -1, 0)), "hand_l")
    return B


DEFORM = None  # filled by build_armature: bones that carry skin


def build_armature():
    """Creates the armature object 'FP_Rig' (at the origin) in its rest pose."""
    global DEFORM
    arm_data = bpy.data.armatures.new("FP_Rig")
    arm = bpy.data.objects.new("FP_Rig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    L.set_active(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    rest = rest_bones()
    for name, (head, tail, z, parent) in rest.items():
        eb = arm_data.edit_bones.new(name)
        eb.head = head
        eb.tail = tail
        eb.align_roll(z)
    for name, (_, _, _, parent) in rest.items():
        if parent:
            arm_data.edit_bones[name].parent = arm_data.edit_bones[parent]
            arm_data.edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    DEFORM = [n for n in rest if n not in ("root", "spine_03", "weapon_r", "prop_l")]
    for b in arm_data.bones:
        b.use_deform = b.name in DEFORM
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    arm.show_in_front = True
    return arm


# ------------------------------------------------------------------ mesh primitives
def _bm_obj(name, bm, part):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob["part"] = part
    return ob


def capsule(bm, a, b, ra, rb, seg=20, rings=8):
    """A tapered capsule from a (radius ra) to b (radius rb) into the bmesh: two spheres and a cone."""
    a, b = Vector(a), Vector(b)
    for c, r in ((a, ra), (b, rb)):
        bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r,
                                  matrix=Matrix.Translation(c))
    d = b - a
    n = d.length
    if n < 1e-5:
        return
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    m = Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=ra, radius2=rb, depth=n, matrix=m)


def ellipsoid(bm, c, axes, radii, seg=24, rings=12):
    """Ellipsoid centred on c with its radii along the three given axes."""
    m = Matrix((axes[0] * radii[0], axes[1] * radii[1], axes[2] * radii[2])).transposed().to_4x4()
    m.translation = c
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0, matrix=m)


def loft(bm, centres, axes_f, half_a, half_b, ax_a, ax_b, seg=32, caps=True, n=2.0):
    """A closed tube of elliptical rings (half widths half_a along ax_a, half_b along ax_b); n > 2
    squares the section off into a rounded box (superellipse)."""
    rings = []
    for i, c in enumerate(centres):
        ring = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            cx, sy = math.cos(th), math.sin(th)
            cx = math.copysign(abs(cx) ** (2.0 / n), cx)
            sy = math.copysign(abs(sy) ** (2.0 / n), sy)
            ring.append(bm.verts.new(c + ax_a * (half_a[i] * cx) + ax_b * (half_b[i] * sy)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(seg):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % seg], rings[i + 1][(k + 1) % seg], rings[i + 1][k]))
    if caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    return rings


# ------------------------------------------------------------------ glove
def build_glove(side):
    """The leather glove of one hand, voxel-unioned from its parts (rest pose)."""
    f, t, p = hand_frame(side)
    wr = wrist(side)
    bm = bmesh.new()

    def hp(ftp):
        return hand_point(side, ftp)

    # gauntlet under the knit cuff, flaring a little at its end
    cs = [wr + f * x for x in (-0.052, -0.035, -0.015, 0.004)]
    loft(bm, cs, f, [0.0300, 0.0290, 0.0290, 0.0320], [0.0232, 0.0222, 0.0220, 0.0205], t, p, seg=36)
    # palm: a rounded-box loft from the wrist to the knuckles, full on the palm side
    xs = (-0.004, 0.014, 0.032, 0.050, 0.068, 0.084, 0.095, 0.101)
    cs = [hp((x, 0.004, 0.002)) for x in xs]
    loft(bm, cs, f, [0.037, 0.042, 0.045, 0.047, 0.048, 0.0475, 0.045, 0.040],
         [0.019, 0.021, 0.021, 0.020, 0.018, 0.0162, 0.0138, 0.0095], t, p, seg=40, n=2.7)
    # thenar (the thumb grows out of it) and hypothenar pads, faint knuckles on the back
    ellipsoid(bm, hp((0.030, 0.026, 0.012)), (f, t, p), (0.036, 0.024, 0.017))
    ellipsoid(bm, hp((0.045, -0.030, 0.008)), (f, t, p), (0.036, 0.015, 0.012))
    for name in FINGERS:
        m = FINGER_SPEC[name]["mcp"]
        ellipsoid(bm, hp((m[0] - 0.004, m[1], -0.006)), (f, t, p), (0.012, 0.011, 0.009), seg=16, rings=8)
    # fingers: the index on its own, the paddle from three overlapping chains bridged by a slab
    # (one fused mitt with faint grooves between the fingers)
    for name in FINGERS:
        spec = FINGER_SPEC[name]
        d, _ = finger_dirs(side, name)
        head = hp(spec["mcp"])
        total = sum(spec["lengths"])
        r0, r1 = spec["r"]
        pos = 0.0
        for i, ln in enumerate(spec["lengths"]):
            a, b = head + d * pos, head + d * (pos + ln)
            ra, rb = r0 + (r1 - r0) * pos / total, r0 + (r1 - r0) * (pos + ln) / total
            capsule(bm, a, b, ra, rb)
            pos += ln
    slab = [hp((x, -0.0125, 0.0)) for x in (0.082, 0.11, 0.14, 0.168)]
    loft(bm, slab, f, [0.021, 0.021, 0.0195, 0.0165], [0.0098, 0.0094, 0.0090, 0.0085], t, p, seg=28, n=2.4)
    # thumb
    td, _ = thumb_axes(side)
    head = hp(THUMB_SPEC["cmc"])
    r0, r1 = THUMB_SPEC["r"]
    total = sum(THUMB_SPEC["lengths"])
    pos = 0.0
    for ln in THUMB_SPEC["lengths"]:
        capsule(bm, head + td * pos, head + td * (pos + ln), r0 + (r1 - r0) * pos / total,
                r0 + (r1 - r0) * (pos + ln) / total)
        pos += ln
    # wrist strap with a press stud on the back of the hand
    strap = [wr + f * x for x in (-0.030, -0.018)]
    loft(bm, strap, f, [0.0315, 0.0315], [0.0245, 0.0245], t, p, seg=36)
    bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=0.0055, radius2=0.0045, depth=0.006,
                          matrix=Matrix.Translation(wr + f * -0.024 - p * 0.0255)
                          @ Vector((0, 0, 1)).rotation_difference(-p).to_matrix().to_4x4())
    ob = _bm_obj("glove_" + side, bm, "glove")
    # union: voxel remesh, relax, decimate
    m = ob.modifiers.new("vox", "REMESH")
    m.mode = "VOXEL"
    m.voxel_size = 0.0011
    m.adaptivity = 0.0
    m.use_smooth_shade = True
    L.apply_mods(ob)
    sm = ob.modifiers.new("relax", "CORRECTIVE_SMOOTH")
    sm.iterations = 12
    sm.smooth_type = "LENGTH_WEIGHTED"
    sm.use_only_smooth = True
    L.apply_mods(ob)
    dec = ob.modifiers.new("dec", "DECIMATE")
    dec.ratio = min(1.0, 7000.0 / max(1, len(ob.data.polygons)))
    L.apply_mods(ob)
    ob.data.shade_smooth()
    return ob


# ------------------------------------------------------------------ sleeve and cuff
SLEEVE_END = -0.058  # along the forearm from the wrist: where the sleeve meets the cuff
CUFF = (-0.074, -0.008)


def build_sleeve(side):
    """Olive bomber sleeve: a puffy elliptical tube from the shoulder to the cuff, a few soft folds."""
    f, t, p = hand_frame(side)
    sh = shoulder(side)
    length = UPPER_ARM + FOREARM + SLEEVE_END
    rings, seg = 62, 40
    rng = np.random.default_rng(7 if side == "r" else 11)
    phase = rng.uniform(0, 6.28, 6)
    bm = bmesh.new()
    rows = []
    for i in range(rings):
        s = i / (rings - 1)
        d = -0.03 + (length + 0.03) * s
        c = sh + f * d
        # radius profile: shoulder, upper arm, elbow, forearm, end gathered into the cuff
        prof = np.interp(d, [-0.03, 0.06, 0.24, 0.31, 0.40, 0.47, length - 0.02, length],
                         [0.062, 0.058, 0.055, 0.051, 0.047, 0.0435, 0.0405, 0.0385])
        fold = 0.0
        for k, (x0, w, a) in enumerate(((0.29, 0.03, 0.0035), (0.36, 0.022, 0.0028), (0.42, 0.02, 0.0024),
                                        (0.47, 0.016, 0.002), (0.20, 0.03, 0.002))):
            fold += a * math.exp(-((d - x0) / w) ** 2)
        row = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            wob = 0.0012 * math.sin(3 * th + phase[0] + d * 20) + 0.0008 * math.sin(5 * th + phase[1] + d * 33)
            # folds pinch on the inside of the elbow more than on the outside
            ra = (prof + fold * (0.6 + 0.4 * math.cos(th)) + wob) * 1.04
            rb = (prof + fold * (0.6 + 0.4 * math.cos(th)) + wob) * 0.94
            row.append(bm.verts.new(c + t * (ra * math.cos(th)) + p * (rb * math.sin(th))))
        rows.append(row)
    for i in range(rings - 1):
        for k in range(seg):
            bm.faces.new((rows[i][k], rows[i][(k + 1) % seg], rows[i + 1][(k + 1) % seg], rows[i + 1][k]))
    # a turned-in hem at the end so the edge has thickness
    inner = [bm.verts.new(v.co - (v.co - (sh + f * length)).normalized() * 0.004 + f * -0.008) for v in rows[-1]]
    for k in range(seg):
        bm.faces.new((rows[-1][k], rows[-1][(k + 1) % seg], inner[(k + 1) % seg], inner[k]))
    # the rings follow the hand frame, which is mirrored (left-handed) on the right arm: face outwards
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _bm_obj("sleeve_" + side, bm, "sleeve")
    ob.data.shade_smooth()
    return ob


def build_cuff(side):
    """Knit rib cuff at the wrist: ribbed outer wall, inner wall, rolled edge."""
    f, t, p = hand_frame(side)
    wr = wrist(side)
    seg, ribs = 96, 32
    bm = bmesh.new()
    xs = np.linspace(CUFF[0], CUFF[1], 9)
    outer, inner = [], []
    for x in xs:
        c = wr + f * x
        bulge = 0.0012 * math.sin(math.pi * (x - CUFF[0]) / (CUFF[1] - CUFF[0]))
        row = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            rib = 0.0011 * (0.5 + 0.5 * math.cos(ribs * th))
            ra, rb = 0.0368 + bulge + rib, 0.0302 + bulge + rib
            row.append(bm.verts.new(c + t * (ra * math.cos(th)) + p * (rb * math.sin(th))))
        outer.append(row)
        row = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            row.append(bm.verts.new(c + t * (0.0322 * math.cos(th)) + p * (0.0256 * math.sin(th))))
        inner.append(row)

    def strip(a, b):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    for i in range(len(xs) - 1):
        strip(outer[i], outer[i + 1])
        strip(inner[i + 1], inner[i])
    # rolled edges at both ends
    for row_o, row_i, sgn in ((outer[-1], inner[-1], 1.0), (outer[0], inner[0], -1.0)):
        c = wr + f * (xs[-1] if sgn > 0 else xs[0])
        mid = []
        for k in range(seg):
            a, b = row_o[k].co, row_i[k].co
            mid.append(bm.verts.new((a + b) / 2 + f * (0.0028 * sgn)))
        if sgn > 0:
            strip(row_o, mid)
            strip(mid, row_i)
        else:
            strip(mid, row_o)
            strip(row_i, mid)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _bm_obj("cuff_" + side, bm, "cuff")
    ob.data.shade_smooth()
    return ob


SPECS = {
    # Rook's look (cast_specs.ROOK_LOOKS, sRGB there, linear here, lifted a little for the darker
    # first-person light): dark brown leather gloves, a soft sheen worn lighter on the knuckles
    "glove": L.spec("brown_leather", (0.045, 0.021, 0.012), 0.12, "tint", 0.55, (0.34, 0.55), 0.0, 0.55,
                    wear=0.25, wear_color=(0.12, 0.075, 0.045), noise=0.06, bevel_r=0.002),
    # olive MA-1 nylon of the bomber
    "sleeve": L.spec("rough_linen", (0.06, 0.072, 0.024), 0.18, "tint", 0.35, (0.45, 0.62), 0.0, 0.35, noise=0.05),
    # knit rib, a darker olive: no texture (the library's wool reads as a plaid at this size), a fine grain
    "cuff": L.spec(None, (0.028, 0.034, 0.013), 0.08, "none", 0.0, (0.82, 0.95), 0.0, 0.0, noise=0.10, grain=0.35),
}


CAST_PARTS = {"Glove": "glove", "Jacket": "sleeve", "Rib": "cuff", "Team": "cuff"}
# triangle budget per cast part (the SDF meshes come out at 1-2.5 mm voxels: ~370k vertices for both arms)
CAST_TRIS = {"Glove": 12000, "Jacket": 5000, "Rib": 2500, "Team": 800}


def decimate(ob, tris):
    """Collapse-decimates a part to about `tris` triangles, keeping its vertex groups."""
    n = len(ob.data.polygons)
    if n <= tris:
        return
    m = ob.modifiers.new("dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = tris / n
    m.use_collapse_triangulate = True
    L.set_active(ob)
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier=m.name)


def build_cast_mesh(arm, bake=True, size=2048):
    """The cast module's hero arm (glove, MA-1 sleeve, rib cuff) on this rig's rest pose, decimated,
    baked with this file's PBR specs (by its material slots), its weights spread down the twist bones."""
    parts = []
    for s in SIDES:
        for ob in CG.make_fp_arm(s, np.array(shoulder(s)), np.array(elbow(s)), cast_frame(s), team=False):
            mat = str(ob.get("cast_material", "Jacket"))
            n = len(ob.data.vertices)
            decimate(ob, CAST_TRIS.get(mat, 4000))
            print("CAST PART %s (%s) %d -> %d verts" % (ob.name, mat, n, len(ob.data.vertices)))
            ob["part"] = CAST_PARTS.get(mat, "sleeve")
            ob.data.polygons.foreach_set("material_index", [0] * len(ob.data.polygons))
            parts.append(ob)
    ob = L.join(parts, F.ASSET)
    if bake:
        L.bake_asset(ob, F.ASSET, SPECS, size, ao_strength=0.5, folder=F.OUT)
    twist_weights(ob)
    ob.parent = arm
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    ob.data.shade_smooth()
    return ob


def twist_weights(ob):
    """The cast's forearm weight ('lowerarm') shared down the forearm: lowerarm at the elbow, the two
    twist bones at 40 % and 75 % of the way to the wrist, so a turned hand twists the sleeve evenly."""
    groups = {g.name: g.index for g in ob.vertex_groups}
    for s in SIDES:
        lo = "lowerarm_" + s
        if lo not in groups:
            continue
        for tw in TWIST:
            if tw + "_" + s not in groups:
                groups[tw + "_" + s] = ob.vertex_groups.new(name=tw + "_" + s).index
        el = np.array(elbow(s))
        f = np.array(arm_dir(s))
        knots = [0.0, TWIST["lowerarm_twist_01"], TWIST["lowerarm_twist_02"]]
        names = [lo, "lowerarm_twist_01_" + s, "lowerarm_twist_02_" + s]
        for v in ob.data.vertices:
            w_lo = next((g.weight for g in v.groups if g.group == groups[lo]), 0.0)
            if w_lo <= 0.0:
                continue
            u = min(max(float((np.array(v.co) - el) @ f) / FOREARM, 0.0), 1.0)
            shares = []
            for i, k in enumerate(knots):
                lo_k = knots[i - 1] if i > 0 else -1.0
                hi_k = knots[i + 1] if i + 1 < len(knots) else 2.0
                tent = (u - lo_k) / (k - lo_k) if u <= k else (hi_k - u) / (hi_k - k)
                if i == 0 and u <= 0.0:
                    tent = 1.0
                if i == len(knots) - 1 and u >= k:
                    tent = 1.0
                shares.append(min(max(tent, 0.0), 1.0))
            total = sum(shares) or 1.0
            for nm, sh in zip(names, shares):
                wt = w_lo * sh / total
                if wt > 0.002:
                    ob.vertex_groups[groups[nm]].add([v.index], wt, "REPLACE")
                elif nm != lo:
                    pass
            if shares[0] / total < 0.002:
                ob.vertex_groups[groups[lo]].remove([v.index])
            else:
                ob.vertex_groups[groups[lo]].add([v.index], w_lo * shares[0] / total, "REPLACE")
    print("TWIST WEIGHTS", F.ASSET, len(ob.data.vertices), "verts")


def build_mesh(arm, bake=True, size=2048):
    """Builds, bakes and weights the arms mesh 'FP_Arms' parented to the armature (the cast module's
    hero arm when available, else this file's placeholder)."""
    if CG is not None:
        return build_cast_mesh(arm, bake, size)
    parts = []
    for s in SIDES:
        parts += [build_glove(s), build_sleeve(s), build_cuff(s)]
    ob = L.join(parts, F.ASSET)
    if bake:
        L.bake_asset(ob, F.ASSET, SPECS, size, ao_strength=0.5, folder=F.OUT)
    skin(ob, arm)
    return ob


# ------------------------------------------------------------------ weights
def _seg_dist(pts, a, b):
    ab = b - a
    t = np.clip(((pts - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
    return np.linalg.norm(pts - (a + t[:, None] * ab), axis=1), t


def skin(ob, arm):
    """Linear-blend weights from capsule distances, per region:
    sleeve/cuff by position down the arm (upper arm, elbow blend, forearm ramp over the twist bones),
    glove by the nearest finger or thumb segment (soft at the joints, the paddle shared laterally
    between its three chains), the palm and gauntlet on the hand."""
    rest = rest_bones()
    me = ob.data
    n = len(me.vertices)
    co = np.empty(n * 3, np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3).astype(np.float64)
    W = {}  # bone -> weight array

    def add(bone, w):
        W.setdefault(bone, np.zeros(n))
        W[bone] += w

    for s in SIDES:
        sx = side_x(s)
        mine = (co[:, 0] * sx) > -0.02  # this side of the body
        sh = np.array(shoulder(s))
        f = np.array(hand_frame(s)[0])
        d = (co - sh) @ f  # distance down the arm
        L_ua, L_fa = UPPER_ARM, FOREARM
        # arm part: everything up to the wrist, by position along the arm
        arm_w = mine.astype(float)
        u = (d - L_ua) / L_fa  # 0 at the elbow, 1 at the wrist
        elbow_t = np.clip((d - (L_ua - 0.045)) / 0.08, 0, 1)
        elbow_t = elbow_t * elbow_t * (3 - 2 * elbow_t)
        clav = np.clip((0.035 - d) / 0.06, 0, 1) * 0.5
        add("clavicle_" + s, arm_w * clav)
        add("upperarm_" + s, arm_w * (1 - elbow_t) * (1 - clav))
        # forearm ramp: lowerarm (u 0) -> twist_01 (0.40) -> twist_02 (0.75) -> hand (1.0)
        knots = [0.0, TWIST["lowerarm_twist_01"], TWIST["lowerarm_twist_02"], 1.0]
        names = ["lowerarm_" + s, "lowerarm_twist_01_" + s, "lowerarm_twist_02_" + s, "hand_" + s]
        uc = np.clip(u, 0, 1)
        for i, nm in enumerate(names):
            lo = knots[i - 1] if i > 0 else -1.0
            hi = knots[i + 1] if i < len(knots) - 1 else 2.0
            k = knots[i]
            tent = np.where(uc <= k, np.clip((uc - lo) / max(k - lo, 1e-6), 0, 1), np.clip((hi - uc) / max(hi - k, 1e-6), 0, 1))
            if i == 0:
                tent = np.where(uc <= 0, 1.0, tent)
            add(nm, arm_w * elbow_t * tent)
        # the glove (beyond the gauntlet) is re-weighted below from the finger capsules
        glove = mine & (d > L_ua + L_fa + 0.004)
        if not glove.any():
            continue
        gi = np.where(glove)[0]
        for bone in list(W.keys()):
            W[bone][gi] = 0.0
        pts = co[gi]
        hand_f = (pts - np.array(wrist(s))) @ f
        cands = {}
        for name in FINGERS:
            for i in range(3):
                bn = f"{name}_0{i + 1}_{s}"
                h, tl, _, _ = rest[bn]
                dist, _ = _seg_dist(pts, np.array(h), np.array(tl))
                cands[bn] = dist - FINGER_SPEC[name]["r"][0]
        for i in range(3):
            bn = f"thumb_0{i + 1}_{s}"
            h, tl, _, _ = rest[bn]
            dist, _ = _seg_dist(pts, np.array(h), np.array(tl))
            cands[bn] = dist - THUMB_SPEC["r"][0] * (1.25 if i == 0 else 1.0)
        h, tl, _, _ = rest["hand_" + s]
        dist, _ = _seg_dist(pts, np.array(h), np.array(tl))
        cands["hand_" + s] = dist - 0.026
        # fingers only take what lies past their knuckle (minus a soft band), the thumb its own pad
        for name in FINGERS:
            mcp_f = FINGER_SPEC[name]["mcp"][0]
            gate = np.clip((hand_f - (mcp_f - 0.016)) / 0.012, 0, 1)
            for i in range(3):
                bn = f"{name}_0{i + 1}_{s}"
                cands[bn] = np.where(gate > 0, cands[bn] + (1 - gate) * 0.02, 1.0)
        names = list(cands.keys())
        D = np.stack([cands[k] for k in names], 1)
        sigma = 0.0035
        dmin = D.min(1, keepdims=True)
        E = np.exp(-(D - dmin) / sigma)
        E[(D - dmin) > 4 * sigma] = 0.0
        E /= E.sum(1, keepdims=True)
        for j, nm in enumerate(names):
            add(nm, np.zeros(n))
            W[nm][gi] += E[:, j]
        # the gauntlet/wrist zone blends the hand with the last twist bone
        wz = mine & (d > L_ua + L_fa - 0.06) & (d <= L_ua + L_fa + 0.004)
        if wz.any():
            wi = np.where(wz)[0]
            k = np.clip((d[wi] - (L_ua + L_fa - 0.06)) / 0.064, 0, 1)
            for bone in list(W.keys()):
                W[bone][wi] *= (1 - k)
            add("hand_" + s, np.zeros(n))
            W["hand_" + s][wi] += k
    # keep 4 strongest, normalise, write groups
    names = list(W.keys())
    M = np.stack([W[k] for k in names], 1)
    order = np.argsort(-M, 1)
    keep = np.zeros_like(M, bool)
    np.put_along_axis(keep, order[:, :4], True, 1)
    M = np.where(keep, M, 0.0)
    M[M < 0.01] = 0.0
    sums = M.sum(1, keepdims=True)
    sums[sums == 0] = 1.0
    M /= sums
    for j, nm in enumerate(names):
        vg = ob.vertex_groups.get(nm) or ob.vertex_groups.new(name=nm)
        idx = np.nonzero(M[:, j])[0]
        for i in idx:
            vg.add([int(i)], float(M[i, j]), "REPLACE")
    ob.parent = arm
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    unweighted = int((M.sum(1) == 0).sum())
    print("SKIN", F.ASSET, n, "verts,", len(names), "bones, unweighted:", unweighted)
    return ob
