# Author : Baptiste Vu Liem
"""The game's plants and rocks: faceted trunks and blobs, flat colours (headless Blender 5.2).

  blender -b --factory-startup --python Tools/Blender/lowpoly/veg_build.py

Writes Tools/Blender/out/lowpoly/<Veg_Name>/<Veg_Name>.fbx and _flat.json (slot -> colour), one mesh
named <Veg_Name>_LOD0. Copy them over Assets/_Game/Art/PBR/Models/<Veg_Name>/ under the editor lock,
then Goobyr/Build/Flat Materials remaps the slots. Same names as the models they replace, so every map
recipe keeps working.
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(PROJECT, "Tools", "Blender", "out", "lowpoly")


def V(*a):
    return np.array(a, dtype=np.float64)


class Mesh:
    def __init__(self):
        self.verts, self.faces, self.slots = [], [], []

    def add(self, verts, faces, slot):
        base = len(self.verts)
        self.verts.extend([tuple(v) for v in verts])
        for f in faces:
            self.faces.append(tuple(i + base for i in f))
            self.slots.append(slot)


def frame(axis):
    axis = axis / np.linalg.norm(axis)
    side = V(1, 0, 0) if abs(axis[0]) < 0.9 else V(0, 1, 0)
    u = side - axis * np.dot(side, axis)
    u /= np.linalg.norm(u)
    return u, np.cross(axis, u)


def chain(m, points, radii, slot, sides=6):
    points = [np.asarray(p, dtype=np.float64) for p in points]
    verts, faces = [], []
    for i, p in enumerate(points):
        a = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        u, v = frame(a)
        for k in range(sides):
            t = (k + 0.5) / sides * math.tau
            verts.append(p + (u * math.cos(t) + v * math.sin(t)) * radii[i])
    for i in range(len(points) - 1):
        for k in range(sides):
            a, b = i * sides + k, i * sides + (k + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range(sides)))
    faces.append(tuple(range((len(points) - 1) * sides, len(points) * sides)))
    m.add(verts, faces, slot)


def blob(m, centre, radii, slot, seed=1, rough=0.08, subdivisions=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    rng = np.random.default_rng(seed)
    verts = []
    for v in bm.verts:
        p = np.array(v.co, dtype=np.float64) * (1.0 + rng.uniform(-rough, rough))
        verts.append(np.asarray(centre) + p * np.asarray(radii))
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    m.add(verts, faces, slot)


def frond(m, root, yaw, length, width, droop, slot, rise=0.25, segments=4):
    """A long flat leaf: arching out and down, with a little thickness so it shades on both faces."""
    d = V(math.cos(yaw), math.sin(yaw), 0)
    side = V(-d[1], d[0], 0)
    verts, faces = [], []
    for i in range(segments + 1):
        t = i / segments
        w = width * math.sin(math.pi * min(0.98, t * 0.92 + 0.08))
        p = np.asarray(root) + d * length * t + V(0, 0, length * (rise * t - droop * t * t))
        verts += [p - side * w, p + side * w, p + V(0, 0, 0.02 * length)]
    for i in range(segments):
        a = i * 3
        faces += [(a, a + 2, a + 5, a + 3), (a + 2, a + 1, a + 4, a + 5), (a + 1, a, a + 3, a + 4)]
    m.add(verts, faces, slot)


def cone_stack(m, h, base, levels, slots, start=0.18):
    for i in range(levels):
        t0 = start + (1 - start) * i / levels
        t1 = min(1.0, t0 + (1 - start) / levels * 1.45)
        r = base * (1 - i / levels * 0.72)
        slot = slots[0] if isinstance(slots, str) else slots[min(i, len(slots) - 1)]
        chain(m, [V(0, 0, h * t0), V(0, 0, h * t0 + 0.01), V(0, 0, h * t1)], [r, r, 0.02], slot, sides=7)


# ---------------------------------------------------------------- plants


def ceiba(m):
    chain(m, [V(0, 0, -0.5), V(0, 0, 1.5), V(0, 0, 9), V(0.3, 0, 13)], [1.15, 0.95, 0.7, 0.5], "Trunk", sides=7)
    for i in range(4):
        a = i * math.tau / 4 + 0.4
        chain(m, [V(0, 0, 2.6), V(math.cos(a) * 1.6, math.sin(a) * 1.6, 0.6), V(math.cos(a) * 2.0, math.sin(a) * 2.0, -0.5)], [0.42, 0.34, 0.25], "Trunk", sides=4)
    for x, y, z, r, s in ((0, 0, 15, 7, 1), (5, 2, 13, 4.5, 2), (-5, -2, 13.5, 4.8, 3), (1, -5, 13, 4.2, 4), (-2, 5, 13, 4.4, 5)):
        blob(m, V(x, y, z), (r, r, r * 0.68), "Leaf", s)


def round_tree(m, h, r, trunk, blobs=3, seed=1):
    chain(m, [V(0, 0, -0.3), V(0, 0, h * 0.5), V(0.15, 0.05, h * 0.85)], [trunk, trunk * 0.8, trunk * 0.55], "Trunk", sides=6)
    blob(m, V(0, 0, h), (r, r, r * 0.8), "Leaf", seed)
    rng = np.random.default_rng(seed + 5)
    for i in range(blobs - 1):
        a = rng.uniform(0, math.tau)
        rr = r * rng.uniform(0.5, 0.72)
        blob(m, V(math.cos(a) * r * 0.8, math.sin(a) * r * 0.8, h - r * rng.uniform(0.15, 0.5)), (rr, rr, rr * 0.8), "Leaf2" if i % 2 else "Leaf", seed + i + 2)


def acacia(m):
    chain(m, [V(0, 0, -0.3), V(0, 0, 2.2), V(0.5, 0.2, 4.2)], [0.32, 0.26, 0.16], "Trunk", sides=6)
    chain(m, [V(0, 0, 2.2), V(-1.3, -0.3, 4.0)], [0.18, 0.1], "Trunk", sides=5)
    blob(m, V(0.9, 0.3, 4.7), (3.0, 2.8, 0.75), "Leaf", 2)
    blob(m, V(-1.6, -0.5, 4.4), (1.9, 1.8, 0.55), "Leaf2", 3)


def palm(m, h, lean, fronds, frond_len, seed=1):
    pts = [V(lean * (t ** 2), 0, h * t) for t in (0, 0.25, 0.5, 0.75, 1.0)]
    pts[0] = pts[0] - V(0, 0, 0.3)
    chain(m, pts, [0.26, 0.21, 0.18, 0.16, 0.14], "Trunk", sides=6)
    top = pts[-1]
    for i in range(fronds):
        frond(m, top, i * math.tau / fronds + seed, frond_len, frond_len * 0.13, 0.75, "Leaf" if i % 2 else "Leaf2")
    blob(m, top, (0.28, 0.28, 0.24), "Trunk2", seed, subdivisions=1)


def fir(m, h, base, snowy=False):
    chain(m, [V(0, 0, -0.3), V(0, 0, h * 0.3)], [base * 0.13, base * 0.1], "Trunk", sides=5)
    levels = 4 if h > 8 else 3
    slots = ["Leaf", "Leaf", "Snow", "Snow"] if snowy else ["Leaf", "Leaf2", "Leaf", "Leaf2"]
    cone_stack(m, h, base, levels, slots)


def birch(m):
    chain(m, [V(0, 0, -0.3), V(0, 0, 4), V(0.2, 0, 8)], [0.18, 0.14, 0.08], "Trunk", sides=6)
    blob(m, V(0.1, 0, 8), (2.2, 2.2, 2.7), "Leaf", 4)
    blob(m, V(-0.9, 0.5, 6), (1.4, 1.4, 1.5), "Leaf2", 5)


def dead(m):
    chain(m, [V(0, 0, -0.3), V(0, 0, 2.5), V(0.3, 0, 5.5)], [0.26, 0.2, 0.07], "Trunk", sides=5)
    chain(m, [V(0, 0, 2.5), V(-1.5, 0.3, 4.3)], [0.13, 0.04], "Trunk", sides=4)
    chain(m, [V(0.1, 0, 3.4), V(1.3, -0.6, 4.8)], [0.1, 0.03], "Trunk", sides=4)


def bush(m, r, slots, seed=1, count=3):
    rng = np.random.default_rng(seed)
    blob(m, V(0, 0, r * 0.62), (r, r, r * 0.78), slots[0], seed)
    for i in range(count - 1):
        a = rng.uniform(0, math.tau)
        rr = r * rng.uniform(0.5, 0.75)
        blob(m, V(math.cos(a) * r * 0.75, math.sin(a) * r * 0.75, rr * 0.6), (rr, rr, rr * 0.8), slots[(i + 1) % len(slots)], seed + i + 3)


def ground_fronds(m, count, length, width, slot, slot2, droop=0.55, rise=0.75, seed=1):
    rng = np.random.default_rng(seed)
    for i in range(count):
        frond(m, V(0, 0, 0.05), i * math.tau / count + rng.uniform(-0.2, 0.2), length * rng.uniform(0.8, 1.1), width, droop, slot if i % 2 else slot2, rise=rise, segments=3)


def banana(m):
    chain(m, [V(0, 0, -0.2), V(0, 0, 2.6)], [0.17, 0.11], "Trunk", sides=6)
    for i in range(7):
        frond(m, V(0, 0, 2.5), i * math.tau / 7, 2.2, 0.36, 0.7, "Leaf" if i % 2 else "Leaf2", rise=0.55)


def tree_fern(m):
    chain(m, [V(0, 0, -0.2), V(0, 0, 3.2)], [0.13, 0.1], "Trunk", sides=6)
    for i in range(10):
        frond(m, V(0, 0, 3.1), i * math.tau / 10, 2.1, 0.22, 0.8, "Leaf" if i % 2 else "Leaf2", rise=0.45)


def tuft(m, blades, height, width, slot, slot2, seed=1):
    """A clump of fat leaves: the low-poly grass."""
    rng = np.random.default_rng(seed)
    for i in range(blades):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0, width * 1.4)
        root = V(math.cos(a) * r, math.sin(a) * r, 0)
        h = height * rng.uniform(0.7, 1.05)
        lean = rng.uniform(0.04, 0.2) * h
        d = V(math.cos(a), math.sin(a), 0)
        side = V(-d[1], d[0], 0)
        wid = width * rng.uniform(0.55, 0.85)
        pts = [root - side * wid, root + side * wid, root + side * wid * 0.85 + d * lean * 0.3 + V(0, 0, h * 0.6), root + d * lean + V(0, 0, h), root - side * wid * 0.85 + d * lean * 0.3 + V(0, 0, h * 0.6)]
        m.add(pts, [(0, 1, 2, 3, 4)], slot if i % 2 else slot2)


def log(m):
    chain(m, [V(-2, 0, 0.32), V(0, 0.05, 0.34), V(2, 0, 0.3)], [0.34, 0.32, 0.28], "Trunk", sides=7)


def rock(m, radii, seed):
    blob(m, V(0, 0, radii[2] * 0.55), radii, "Rock", seed, rough=0.24, subdivisions=1)


JUNGLE = dict(Trunk=(0.5, 0.33, 0.24), Trunk2=(0.42, 0.3, 0.2), Leaf=(0.42, 0.68, 0.38), Leaf2=(0.34, 0.6, 0.36))
DRY = dict(Trunk=(0.56, 0.42, 0.3), Trunk2=(0.5, 0.38, 0.26), Leaf=(0.66, 0.68, 0.34), Leaf2=(0.58, 0.62, 0.32))
PALM = dict(Trunk=(0.6, 0.47, 0.34), Trunk2=(0.45, 0.33, 0.22), Leaf=(0.46, 0.7, 0.34), Leaf2=(0.38, 0.62, 0.3))
SNOW = dict(Trunk=(0.4, 0.32, 0.28), Leaf=(0.26, 0.46, 0.42), Leaf2=(0.32, 0.54, 0.46), Snow=(0.9, 0.95, 0.97))
BIRCH = dict(Trunk=(0.9, 0.9, 0.84), Leaf=(0.86, 0.72, 0.34), Leaf2=(0.8, 0.62, 0.28))
ROCK = dict(Rock=(0.62, 0.6, 0.58))
LOG = dict(Trunk=(0.46, 0.34, 0.25))
G_JUNGLE = dict(A=(0.44, 0.72, 0.38), B=(0.52, 0.78, 0.4))
G_DRY = dict(A=(0.78, 0.7, 0.42), B=(0.72, 0.62, 0.36))
G_ALPINE = dict(A=(0.58, 0.72, 0.5), B=(0.74, 0.82, 0.68))


def bx(fn, *a, **k):
    return lambda m: fn(m, *a, **k)


PLANTS = {
    "Veg_Ceiba": (ceiba, JUNGLE),
    "Veg_JungleTree_A": (bx(round_tree, 9.5, 4.0, 0.42, 3, 1), JUNGLE),
    "Veg_JungleTree_B": (bx(round_tree, 12.5, 4.8, 0.5, 4, 7), JUNGLE),
    "Veg_Acacia": (acacia, DRY),
    "Veg_Birch": (birch, BIRCH),
    "Veg_Banana": (banana, JUNGLE),
    "Veg_TreeFern": (tree_fern, JUNGLE),
    "Veg_CoconutPalm": (bx(palm, 10, 1.6, 9, 3.6, 1), PALM),
    "Veg_DatePalm": (bx(palm, 8, 0.5, 11, 3.2, 2), PALM),
    "Veg_RoyalPalm": (bx(palm, 13, 0.2, 10, 3.8, 3), PALM),
    "Veg_Fir_Large": (bx(fir, 14, 3.4), SNOW),
    "Veg_Fir_Medium": (bx(fir, 9, 2.5), SNOW),
    "Veg_Fir_Small": (bx(fir, 5, 1.6), SNOW),
    "Veg_Fir_Snowy_Large": (bx(fir, 14, 3.4, True), SNOW),
    "Veg_Fir_Snowy_Medium": (bx(fir, 9, 2.5, True), SNOW),
    "Veg_DeadTree": (dead, LOG),
    "Veg_JungleBush_A": (bx(bush, 1.1, ["Leaf", "Leaf2"], 1), JUNGLE),
    "Veg_JungleBush_B": (bx(bush, 1.5, ["Leaf2", "Leaf"], 4, 4), JUNGLE),
    "Veg_DesertShrub_A": (bx(bush, 0.7, ["Leaf", "Leaf2"], 2), DRY),
    "Veg_DesertShrub_B": (bx(bush, 1.0, ["Leaf2", "Leaf"], 5, 4), DRY),
    "Veg_SnowBush": (bx(bush, 1.0, ["Snow", "Leaf"], 6), SNOW),
    "Veg_Fern": (bx(ground_fronds, 7, 0.9, 0.12, "Leaf", "Leaf2"), JUNGLE),
    "Veg_Grass_Jungle": (bx(tuft, 6, 0.42, 0.075, "A", "B", 1), G_JUNGLE),
    "Veg_Grass_Dry": (bx(tuft, 6, 0.38, 0.07, "A", "B", 2), G_DRY),
    "Veg_Grass_Alpine": (bx(tuft, 6, 0.34, 0.07, "A", "B", 3), G_ALPINE),
    "Veg_TallGrass_Jungle": (bx(tuft, 11, 1.45, 0.17, "A", "B", 4), G_JUNGLE),
    "Veg_TallGrass_Dry": (bx(tuft, 11, 1.35, 0.16, "A", "B", 5), G_DRY),
    "Veg_TallGrass_Alpine": (bx(tuft, 11, 1.25, 0.16, "A", "B", 6), G_ALPINE),
    "Veg_Log": (log, LOG),
    "Veg_Rock_A": (bx(rock, (1.0, 0.8, 0.7), 1), ROCK),
    "Veg_Rock_B": (bx(rock, (1.5, 1.2, 1.0), 2), ROCK),
    "Veg_Rock_C": (bx(rock, (0.7, 0.7, 0.45), 3), ROCK),
}


def export_mesh(name, m, colours):
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    me = bpy.data.meshes.new(name + "_LOD0")
    me.from_pydata(m.verts, [], m.faces)
    me.update()
    slots = sorted(set(m.slots))
    for slot in slots:
        me.materials.append(bpy.data.materials.new("%s_%s" % (name, slot)))
    me.polygons.foreach_set("material_index", [slots.index(s) for s in m.slots])
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name + "_LOD0", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    out_dir = os.path.join(OUT, name)
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, name + ".fbx"), use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0, mesh_smooth_type="FACE", use_tspace=False,
                             path_mode="STRIP", embed_textures=False, bake_anim=False)
    json.dump({"slots": {slot: colours[slot] for slot in slots}}, open(os.path.join(out_dir, name + "_flat.json"), "w"), indent=1)
    print("[veg]", name, len(m.verts), "verts", len(m.faces), "faces", flush=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name, (fn, colours) in PLANTS.items():
        m = Mesh()
        fn(m)
        export_mesh(name, m, colours)
    print("[veg] DONE", flush=True)


if __name__ == "__main__":
    main()
