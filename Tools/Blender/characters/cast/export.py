# Author : Baptiste Vu Liem
"""Step 2 of the cast: game-ready characters from the SDF parts (headless Blender 5.2).

  blender -b --factory-startup --python Tools/Blender/characters/cast/export.py -- Chr_Rook [more...] [--preview]

Per character: build the parts (cast_build.build), decimate every part to its share of the triangle
budget, group the parts by material slot, unwrap each slot and bake it from the high-resolution parts
(albedo, OpenGL normal, roughness, ambient occlusion -> <Chr>_<Slot>_diff / _nor_gl / _arm), build the
shared 53-bone skeleton (UE-mannequin names, rolls copied from Chr_Agent), weight by capsule distance
(the glove module's own weights on the hands, rigid heads), split the face into the head mesh, make
_LOD1 (40 %) and _LOD2 (15 %), add the expression shape keys last, export
Tools/Blender/out/cast/export/<Chr>/<Chr>.fbx + textures (copied into Assets under the editor lock).
"""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import assemble as A  # noqa: E402
import cast_build  # noqa: E402
import faces  # noqa: E402
import glove  # noqa: E402
import head as H  # noqa: E402
import materials as M  # noqa: E402
import render as R  # noqa: E402
import skeleton  # noqa: E402
from cast_specs import CAST  # noqa: E402

OUT = os.path.join(cast_build.OUT, "export")
HERO_TRIS, NPC_TRIS = 30000, 20000
LOD_RATIOS = (("_LOD1", 0.40), ("_LOD2", 0.15))
FINAL = {"Skin": "Body"}
HEAD_PARTS = set(faces.FACE_PARTS) | {"Eye_l", "Eye_r"}
RIGID_HEAD = ("Hair", "Hat", "Shades", "Brows", "Beard", "Lid", "Lash", "Eye_", "Earring")
BOOST = {"Head": 3.6, "Hand": 1.6, "Glove": 1.6, "Hair": 1.25, "Brows": 3.5, "Beard": 1.3, "Lid": 2.4, "Lash": 2.0}
RES = {"Body": 1024, "Jacket": 1024, "Trousers": 1024, "Hair": 512, "Shirt": 1024, "Suit": 1024, "Dress": 1024,
       "Parka": 1024, "Tee": 512, "Scarf": 512, "Glove": 512, "Boots": 512, "Shoes": 512, "Rib": 512, "Vest": 512,
       "Apron": 512, "Waistcoat": 512, "Hat": 512, "Lining": 256}
PLAIN = {"plain", "glass"}
REF_AXES = json.load(open(os.path.join(HERE, "ref_axes.json")))
log = cast_build.log


# ------------------------------------------------------------------------------------- helpers

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def area(ob):
    return sum(p.area for p in ob.data.polygons)


def ctx(ob, sel=None):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in (sel or [ob]):
        o.select_set(True)
    bpy.context.view_layer.objects.active = ob


def dup(ob, name, coll):
    n = ob.copy()
    n.data = ob.data.copy()
    n.name = n.data.name = name
    for m in list(n.modifiers):
        n.modifiers.remove(m)
    coll.objects.link(n)
    return n


def clear_custom_normals(ob):
    ctx(ob)
    try:
        with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
    except Exception:
        pass
    ob.data.shade_smooth()


def decimate(ob, ratio):
    if ratio >= 0.999:
        return
    m = ob.modifiers.new("Dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = max(ratio, 0.001)
    m.use_collapse_triangulate = True
    ctx(ob)
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=m.name)


def verts(ob):
    co = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3).astype(np.float64)


def int_attr(ob, name, values):
    a = ob.data.attributes.get(name) or ob.data.attributes.new(name, "INT", "POINT")
    a.data.foreach_set("value", np.asarray(values, np.int32))


def read_int(ob, name):
    a = ob.data.attributes[name]
    out = np.empty(len(ob.data.vertices), np.int32)
    a.data.foreach_get("value", out)
    return out


def join(objs, name):
    objs = [o for o in objs if o is not None]
    ctx(objs[0], objs)
    with bpy.context.temp_override(object=objs[0], active_object=objs[0], selected_objects=objs, selected_editable_objects=objs):
        bpy.ops.object.join()
    o = objs[0]
    o.name = o.data.name = name
    return o


# ------------------------------------------------------------------------------------- 1. low-res parts

def low_parts(hi, total, coll):
    """Decimated copy of every hi-res part (eyes rebuilt lighter), budget by visible area."""
    weights = {}
    for name, ob in hi.items():
        if name.startswith("Eye_"):
            continue
        b = next((v for k, v in BOOST.items() if name.startswith(k)), 1.0)
        weights[name] = (area(ob) ** 0.8) * b
    s = sum(weights.values())
    lows = {}
    for name, w in weights.items():
        ob = hi[name]
        target = max(90, int(total * 0.95 * w / s))
        lo = dup(ob, name, coll)
        clear_custom_normals(lo)
        decimate(lo, target / max(tris(lo), 1))
        lo["cast_part"] = name
        lows[name] = lo
    return lows


# ------------------------------------------------------------------------------------- 2. slots, UVs, bakes

def slot_of(mat_name, char):
    if mat_name.startswith("look_"):
        mat_name = mat_name[5:]
    s = mat_name[len(char) + 1:] if mat_name.startswith(char + "_") else mat_name
    return FINAL.get(s, s)


def split_by_slot(lows, char, coll):
    """{slot: [low objects]} (multi-material parts are separated per material)."""
    groups = {}
    for name, lo in list(lows.items()):
        mats = [s.material for s in lo.material_slots if s.material]
        if len(mats) > 1:
            ctx(lo)
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.mesh.separate(type="MATERIAL")
            bpy.ops.object.mode_set(mode="OBJECT")
            pieces = [o for o in coll.objects if o.name.startswith(name) and o.type == "MESH" and o.get("cast_part") == name]
        else:
            pieces = [lo]
        for p in pieces:
            used = {p.data.polygons[i].material_index for i in range(len(p.data.polygons))}
            mi = min(used) if used else 0
            mat = p.material_slots[mi].material
            slot = slot_of(mat.name, char)
            p["cast_slot"] = slot
            groups.setdefault(slot, []).append(p)
    return groups


def unwrap(ob):
    ctx(ob)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=0.004, area_weight=0.0)
    try:
        bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
    except TypeError:
        bpy.ops.uv.pack_islands(margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")


def new_image(name, res, non_color, fill=(0.5, 0.5, 0.5, 1.0)):
    img = bpy.data.images.get(name)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=False, float_buffer=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.generated_color = fill
    return img


def image_np(img):
    a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def save_np(arr, path, non_color=False, fmt="JPEG"):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False, float_buffer=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    rgba = np.ones((h, w, 4), np.float32)
    rgba[..., :arr.shape[2]] = arr[..., :min(arr.shape[2], 4)]
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = fmt
    sc = bpy.context.scene
    sc.render.image_settings.quality = 92
    img.save()
    bpy.data.images.remove(img)


def bake_setup():
    sc = bpy.context.scene
    R._cycles_gpu(sc, 16)
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = 0.006
    sc.render.bake.max_ray_distance = 0.03
    sc.render.bake.margin = 16
    sc.render.bake.target = "IMAGE_TEXTURES"


def push_pull(a, valid):
    """Fills the invalid pixels of a (H x W x C) from the valid ones: averaged down a 2x pyramid and
    pulled back up (holes take their surroundings' colour, smoothly)."""
    h, w = valid.shape
    if valid.all() or not valid.any():
        return a if valid.any() else np.broadcast_to(a.reshape(-1, a.shape[-1]).mean(0), a.shape).copy()
    H, W = h + h % 2, w + w % 2
    ap = np.zeros((H, W, a.shape[2]), np.float64)
    vp = np.zeros((H, W), np.float64)
    ap[:h, :w] = a * valid[..., None]
    vp[:h, :w] = valid
    sums = ap.reshape(H // 2, 2, W // 2, 2, -1).sum((1, 3))
    n = vp.reshape(H // 2, 2, W // 2, 2).sum((1, 3))
    coarse = push_pull(sums / np.maximum(n, 1e-9)[..., None], n > 0)
    up = np.repeat(np.repeat(coarse, 2, 0), 2, 1)[:h, :w]
    return np.where(valid[..., None], a, up)


def fill_misses(out):
    """Texels whose bake rays found nothing (a decimated garment poking through the layer over it,
    where the high-res faces were culled) come back pure black: they take their neighbours' values."""
    miss = (out["DIFFUSE"][..., :3] <= 0.0).all(-1)
    if not miss.any():
        return out
    for kind in out:
        out[kind] = push_pull(out[kind].astype(np.float64), ~miss).astype(np.float32)
    return out


def bake_slot(slot_ob, sources, slot, res):
    """Bakes into four images on the slot object (sources selected, slot object active)."""
    mat = bpy.data.materials.new("Bake_" + slot)
    mat.use_nodes = True
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    nt.nodes.active = tex
    slot_ob.data.materials.clear()
    slot_ob.data.materials.append(mat)
    out = {}
    sc = bpy.context.scene
    for kind, non_color, samples in (("DIFFUSE", False, 1), ("NORMAL", True, 1), ("ROUGHNESS", True, 1), ("AO", True, 24)):
        img = new_image("%s_%s" % (slot, kind), res, non_color, (0.5, 0.5, 1.0, 1.0) if kind == "NORMAL" else (0.5, 0.5, 0.5, 1.0))
        tex.image = img
        sc.cycles.samples = samples
        ctx(slot_ob, sources + [slot_ob])
        kw = dict(type=kind, use_selected_to_active=True, cage_extrusion=0.006, max_ray_distance=0.03, margin=16, margin_type="EXTEND")
        if kind == "DIFFUSE":
            kw["pass_filter"] = {"COLOR"}
        if kind == "NORMAL":
            kw.update(normal_space="TANGENT", normal_r="POS_X", normal_g="POS_Y", normal_b="POS_Z")
        bpy.ops.object.bake(**kw)
        out[kind] = image_np(img)
    return fill_misses(out)


def eye_texture(iris, res=256, dead=False):
    """Planar eyeball texture matching materials.eye (UV radius 0.5 = the whole eyeball)."""
    y, x = np.mgrid[0:res, 0:res]
    u, v = (x + 0.5) / res - 0.5, (y + 0.5) / res - 0.5
    d = np.sqrt(u * u + v * v)
    lin = lambda c: np.array([(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c])
    iris = np.array(iris)
    stops = [(0.0, (0.01, 0.01, 0.012)), (0.112, (0.01, 0.01, 0.012)), (0.128, iris * 0.55), (0.17, np.minimum(iris * 1.35, 1)),
             (0.235, iris), (0.258, (0.03, 0.025, 0.02)), (0.282, (0.95, 0.93, 0.91)), (0.46, (0.93, 0.88, 0.86)), (1.0, (0.85, 0.72, 0.70))]
    col = np.zeros((res, res, 3))
    for i in range(3):
        col[..., i] = np.interp(d, [s[0] for s in stops], [s[1][i] for s in stops])
    if dead:
        bar = np.minimum(np.abs(u - v), np.abs(u + v))
        col[(d < 0.27)] = (0.95, 0.93, 0.91)
        col[(bar < 0.045) & (d < 0.25)] = (0.02, 0.02, 0.02)
    return col  # sRGB values


# ------------------------------------------------------------------------------------- 3. skeleton and weights

def make_armature(B, coll):
    bones = B.sk.bones(glove.LAYOUT)
    ad = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", ad)
    coll.objects.link(arm)
    ctx(arm)
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
    bpy.ops.object.mode_set(mode="OBJECT")
    ad.display_type = "STICK"
    return arm, bones


BODY_BONES = {"pelvis": 0.115, "spine_01": 0.115, "spine_02": 0.125, "spine_03": 0.125, "neck_01": 0.055, "head": 0.09,
              "clavicle_l": 0.045, "clavicle_r": 0.045, "upperarm_l": 0.050, "upperarm_r": 0.050, "lowerarm_l": 0.040,
              "lowerarm_r": 0.040, "hand_l": 0.030, "hand_r": 0.030, "thigh_l": 0.072, "thigh_r": 0.072, "calf_l": 0.050,
              "calf_r": 0.050, "foot_l": 0.040, "foot_r": 0.040, "ball_l": 0.030, "ball_r": 0.030}


def _seg_dist(P, a, b):
    ab = b - a
    t = np.clip(((P - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)


def neighbours():
    nb = {n: {n} for n in BODY_BONES}
    for n in BODY_BONES:
        p = skeleton.PARENT.get(n)
        if p in nb:
            nb[n].add(p)
            nb[p].add(n)
    for side in ("l", "r"):          # the shoulder blends over the chest, the hip over the belly
        nb["upperarm_" + side] |= {"spine_03"}
        nb["thigh_" + side] |= {"spine_01"}
    nb["neck_01"] |= {"clavicle_l", "clavicle_r"}
    return nb


def capsule_weights(P, bones, allowed=None, sigma=0.022):
    names = [n for n in BODY_BONES if (allowed is None or n in allowed)]
    D = np.stack([np.maximum(_seg_dist(P, np.asarray(bones[n][0]), np.asarray(bones[n][1])) - BODY_BONES[n], 0.0) for n in names], 1)
    near = np.argmin(D, axis=1)
    nb = neighbours()
    mask = np.zeros_like(D, dtype=bool)
    idx = {n: i for i, n in enumerate(names)}
    for i, n in enumerate(names):
        cols = [idx[m] for m in nb[n] if m in idx]
        rows = near == i
        for c in cols:
            mask[rows, c] = True
    dmin = D[np.arange(len(P)), near]
    W = np.exp(-(D - dmin[:, None]) / sigma) * mask
    W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
    return names, W


def smooth(ob, names, W, iters=6, factor=0.5):
    me = ob.data
    e = np.empty(len(me.edges) * 2, np.int32)
    me.edges.foreach_get("vertices", e)
    e = e.reshape(-1, 2)
    nv = len(me.vertices)
    deg = np.bincount(e.ravel(), minlength=nv).astype(np.float32)[:, None]
    for _ in range(iters):
        acc = np.zeros_like(W)
        np.add.at(acc, e[:, 0], W[e[:, 1]])
        np.add.at(acc, e[:, 1], W[e[:, 0]])
        avg = np.where(deg > 0, acc / np.maximum(deg, 1), W)
        W = W * (1 - factor) + avg * factor
    return W


def write_groups(ob, names, W, limit=4):
    W = np.asarray(W, dtype=np.float64)
    if W.shape[1] > limit:
        thr = -np.sort(-W, axis=1)[:, limit - 1:limit]
        W = np.where(W >= thr, W, 0.0)
    W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
    for i, n in enumerate(names):
        col = W[:, i]
        nz = np.nonzero(col > 0.004)[0]
        if not len(nz):
            continue
        g = ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)
        for w in np.unique(np.round(col[nz], 3)):
            sel = nz[np.round(col[nz], 3) == w]
            g.add(sel.tolist(), float(w), "REPLACE")


def weight_mesh(ob, part_names, B, bones):
    """All bone weights of a joined mesh, by part rules."""
    P = verts(ob)
    pid = read_int(ob, "cast_part")
    part_of = np.array(part_names)[pid]
    all_names = [n for n in skeleton.BONE_NAMES if n != "Root"]
    col = {n: i for i, n in enumerate(all_names)}
    W = np.zeros((len(P), len(all_names)))
    generic = np.ones(len(P), bool)
    # hands and gloves: the glove module's weights
    for s in ("l", "r"):
        m = np.array([p in ("Hand_" + s, "Glove_" + s) for p in part_of])
        if m.any():
            fr = B.frame(s)
            for bone, w in glove.hand_weights(P[m], fr, s).items():
                W[m, col[bone]] = w
            generic &= ~m
    # head parts: rigid on the head, the head mesh's neck blends into neck_01
    rigid = np.array([p.startswith(RIGID_HEAD) for p in part_of])
    W[rigid, col["head"]] = 1.0
    generic &= ~rigid
    m = part_of == "Head"
    if m.any():
        t = np.clip((P[m, 2] - 1.470) / 0.06, 0, 1)
        W[m, col["head"]] = t
        W[m, col["neck_01"]] = 1 - t
        generic &= ~m
    m = np.array([p.startswith("Earpiece") for p in part_of])
    if m.any():
        t = np.clip((P[m, 2] - 1.48) / 0.06, 0, 1)
        W[m, col["head"]] = t
        W[m, col["neck_01"]] = 1 - t
        generic &= ~m
    # shoes and boots: leg bones of their side only
    for s, sx in (("l", 1), ("r", -1)):
        m = np.array([p.startswith(("Boots", "Shoes")) for p in part_of]) & (P[:, 0] * sx > 0)
        if m.any():
            names, w = capsule_weights(P[m], bones, {"calf_" + s, "foot_" + s, "ball_" + s}, 0.015)
            for i, n in enumerate(names):
                W[m, col[n]] = w[:, i]
            generic &= ~m
    m = np.array([p == "Armband" for p in part_of])
    if m.any():
        W[m, col["upperarm_l"]] = 1.0
        generic &= ~m
    if generic.any():
        names, w = capsule_weights(P[generic], bones)
        for i, n in enumerate(names):
            W[generic, col[n]] = w[:, i]
    W = smooth(ob, all_names, W, iters=5, factor=0.45)
    # re-pin the rigid sets after smoothing (smoothing only softens the generic seams)
    W[rigid] = 0.0
    W[rigid, col["head"]] = 1.0
    write_groups(ob, all_names, W)


# ------------------------------------------------------------------------------------- 4. main

def export(name, preview=False):
    t0 = time.time()
    spec = CAST[name]
    for c in list(bpy.data.collections):
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(c)
    B, f, coll = cast_build.build(name)
    hi = {o.name: o for o in coll.objects if o.type == "MESH"}
    for o in hi.values():
        o.hide_render = False
    for m in bpy.data.materials:
        if not m.name.startswith("look_"):
            m.name = "look_" + m.name
    lowc = A.collection(name + "_game")
    total = HERO_TRIS if name.startswith("Chr_Rook") else NPC_TRIS
    lows = low_parts(hi, total, lowc)
    # game eyeballs: lighter spheres, procedural texture (no bake)
    eye_lo = []
    for c in H.eye_centres(f):
        side = "l" if c[0] > 0 else "r"
        e = A.eyeball("Eye_" + side, c, f["eye_r"], side=1 if c[0] > 0 else -1, coll=lowc, segments=(20, 14))
        e["cast_part"] = "Eye_" + side
        eye_lo.append(e)
    part_names = sorted(list(lows.keys()) + [e["cast_part"] for e in eye_lo])
    pidx = {n: i for i, n in enumerate(part_names)}
    for n, lo in lows.items():
        int_attr(lo, "cast_part", np.full(len(lo.data.vertices), pidx[n]))
    for e in eye_lo:
        int_attr(e, "cast_part", np.full(len(e.data.vertices), pidx[e["cast_part"]]))
    log("low parts", len(lows), "tris", sum(tris(o) for o in lows.values()), "%.0fs" % (time.time() - t0))

    out_dir = os.path.join(OUT, name)
    os.makedirs(out_dir, exist_ok=True)
    groups = split_by_slot(lows, name, lowc)
    for o in lowc.objects:          # the low meshes never occlude or shade the bakes
        for flag in ("visible_diffuse", "visible_glossy", "visible_shadow", "visible_transmission", "visible_volume_scatter"):
            if hasattr(o, flag):
                setattr(o, flag, False)
    bake_setup()
    looks = spec["looks"]
    slot_objs = []
    for slot, objs in groups.items():
        src_parts = [o.get("cast_part") for o in objs]
        so = join([o for o in objs], "%s_%s" % (name, slot)) if len(objs) > 1 else objs[0]
        so.name = so.data.name = "%s_%s" % (name, slot)
        unwrap(so)
        look = looks.get({"Body": "Skin"}.get(slot, slot))
        res = RES.get(slot, 256)
        prefix = os.path.join(out_dir, "%s_%s" % (name, slot))
        if look is not None and look.get("kind") in PLAIN:
            c = np.array(look["colour"])
            save_np(np.broadcast_to(c, (32, 32, 3)).copy(), prefix + "_diff.jpg", fmt="JPEG")
            save_np(np.broadcast_to((0.5, 0.5, 1.0), (32, 32, 3)).copy(), prefix + "_nor_gl.png", True, "PNG")
            save_np(np.broadcast_to((1.0, look.get("rough", 0.5), look.get("metal", 0.0)), (32, 32, 3)).copy(), prefix + "_arm.jpg", True, "JPEG")
        else:
            sources = [hi[p] for p in src_parts if p in hi]
            sources = list({s.name: s for s in sources}.values())
            t = time.time()
            img = bake_slot(so, sources, slot, res)
            metal = (look or {}).get("metal", 0.0)
            diff = img["DIFFUSE"][..., :3]
            if slot in ("Team", "Lining"):
                # team slots keep only the fabric's light and shade; Unity tints them with the team colour
                lum = diff @ np.array([0.2126, 0.7152, 0.0722])
                lum = np.clip(lum / max(float(np.median(lum)), 1e-3) * 0.9, 0, 1)
                diff = np.repeat(lum[..., None], 3, axis=2)
            save_np(diff, prefix + "_diff.jpg", fmt="JPEG")
            save_np(img["NORMAL"][..., :3], prefix + "_nor_gl.png", True, "PNG")
            ao = img["AO"][..., 0]
            # skin: only a whisper of baked AO (lids, brows and hair shells darken the face otherwise)
            ao = ao * 0.25 + 0.75 if slot == "Body" else ao * 0.5 + 0.5
            arm = np.stack([np.clip(ao, 0, 1), img["ROUGHNESS"][..., 0],
                            np.full(diff.shape[:2], metal)], -1)
            save_np(arm, prefix + "_arm.jpg", True, "JPEG")
            log("baked", slot, res, "%.1fs" % (time.time() - t))
        mat = bpy.data.materials.new("%s_%s" % (name, slot))
        so.data.materials.clear()
        so.data.materials.append(mat)
        so["cast_slot"] = slot
        slot_objs.append(so)
    # eyes: texture from the iris colour; the shared dead-eyes texture
    eyes = join(eye_lo, "%s_Eyes" % name) if len(eye_lo) > 1 else eye_lo[0]
    save_np(eye_texture(f["iris"]), os.path.join(out_dir, "%s_Eyes_diff.jpg" % name))
    save_np(np.broadcast_to((0.5, 0.5, 1.0), (32, 32, 3)).copy(), os.path.join(out_dir, "%s_Eyes_nor_gl.png" % name), True, "PNG")
    save_np(np.broadcast_to((1.0, 0.1, 0.0), (32, 32, 3)).copy(), os.path.join(out_dir, "%s_Eyes_arm.jpg" % name), True, "JPEG")
    save_np(eye_texture(f["iris"], dead=True), os.path.join(OUT, "EyesDead.jpg"))
    em = bpy.data.materials.new("%s_Eyes" % name)
    eyes.data.materials.clear()
    eyes.data.materials.append(em)
    slot_objs.append(eyes)
    log("bakes done %.0fs" % (time.time() - t0))

    # join, split the face off, rig
    slot_names = [o.get("cast_slot") for o in slot_objs]
    allm = join(slot_objs, name + "_LOD0")
    pid = read_int(allm, "cast_part")
    is_head = np.isin(np.array(part_names)[pid], list(HEAD_PARTS))
    ctx(allm)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    allm.data.vertices.foreach_set("select", is_head)
    pv = np.empty(len(allm.data.loops), np.int32)
    allm.data.loops.foreach_get("vertex_index", pv)
    starts = np.empty(len(allm.data.polygons), np.int32)
    allm.data.polygons.foreach_get("loop_start", starts)
    allm.data.polygons.foreach_set("select", is_head[pv[starts]])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    head_ob = next(o for o in lowc.objects if o.name.startswith(name + "_LOD0") and o != allm)
    head_ob.name = head_ob.data.name = name + "_Head_LOD0"
    arm, bones = make_armature(B, lowc)
    for ob in (allm, head_ob):
        weight_mesh(ob, part_names, B, bones)
        ob.parent = arm
        ob.modifiers.new("Armature", "ARMATURE").object = arm
        ob.data.shade_smooth()
    # LODs (before the shape keys)
    lods = []
    for src in (allm, head_ob):
        for suffix, ratio in LOD_RATIOS:
            lo = dup(src, src.name.replace("_LOD0", suffix), lowc)
            lo.parent = arm
            decimate(lo, ratio)
            lo.modifiers.new("Armature", "ARMATURE").object = arm
            lods.append(lo)
    # expression shape keys on the head mesh (last)
    P = verts(head_ob)
    part_of = np.array(part_names)[read_int(head_ob, "cast_part")]
    head_ob.shape_key_add(name="Basis", from_mix=False)
    for k in faces.SHAPE_KEYS:
        kb = head_ob.shape_key_add(name=k, from_mix=False)
        kb.data.foreach_set("co", faces.deform(P, part_of, f, k).astype(np.float32).ravel())
        kb.value = 0.0
    for ob in [allm, head_ob] + lods:
        for a in ("cast_part",):
            if a in ob.data.attributes:
                ob.data.attributes.remove(ob.data.attributes[a])
        for ca in list(ob.data.color_attributes):
            ob.data.color_attributes.remove(ca)
    stats = {o.name: tris(o) for o in [allm, head_ob] + lods}
    log("RESULT", name, json.dumps(stats), "%.0fs" % (time.time() - t0))
    for o in list(coll.objects):
        o.hide_render = True
        o.hide_viewport = True
    fbx = os.path.join(out_dir, name + ".fbx")
    export_fbx(fbx, [arm, allm, head_ob] + lods)
    json.dump({"tris": stats, "parts": part_names, "slots": slot_names},
              open(os.path.join(out_dir, name + "_export.json"), "w"), indent=1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + "_game.blend"))
    if preview:
        preview_render(name, out_dir, [allm, head_ob], lods)
    return fbx


def export_fbx(path, objects):
    ctx(objects[0], objects)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0, add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=True, armature_nodetype="NULL",
                             mesh_smooth_type="FACE", use_tspace=True, use_mesh_modifiers=False, path_mode="STRIP", embed_textures=False,
                             bake_anim=False)


def preview_render(name, out_dir, meshes, lods):
    """The exported LOD0 with its baked textures, front and face (Eevee)."""
    for lo in lods:
        lo.hide_render = True
    for ob in meshes:
        for s in ob.material_slots:
            m = s.material
            if m is None:
                continue
            base = os.path.join(out_dir, m.name)
            b = M.NB(m)
            uv = b.n("ShaderNodeUVMap")
            for suffix, sock, nc in (("_diff.jpg", "Base Color", False),):
                t = b.n("ShaderNodeTexImage")
                t.image = bpy.data.images.load(base + suffix, check_existing=True)
                b.link(uv.outputs[0], t.inputs[0])
                b.link(t.outputs[0], b.bsdf.inputs[sock])
            if os.path.exists(base + "_nor_gl.png"):
                t = b.n("ShaderNodeTexImage")
                t.image = bpy.data.images.load(base + "_nor_gl.png", check_existing=True)
                t.image.colorspace_settings.name = "Non-Color"
                b.link(uv.outputs[0], t.inputs[0])
                nm = b.n("ShaderNodeNormalMap")
                b.link(t.outputs[0], nm.inputs["Color"])
                b.link(nm.outputs[0], b.bsdf.inputs["Normal"])
            if os.path.exists(base + "_arm.jpg"):
                t = b.n("ShaderNodeTexImage")
                t.image = bpy.data.images.load(base + "_arm.jpg", check_existing=True)
                t.image.colorspace_settings.name = "Non-Color"
                b.link(uv.outputs[0], t.inputs[0])
                sep = b.n("ShaderNodeSeparateColor")
                b.link(t.outputs[0], sep.inputs[0])
                b.link(sep.outputs[1], b.bsdf.inputs["Roughness"])
                b.link(sep.outputs[2], b.bsdf.inputs["Metallic"])
    R.setup()
    renders = os.path.join(cast_build.OUT, "renders")
    R.turnaround(renders, name + "_game", views=("front",))
    R.persp_view(18, target=(0, -0.02, 1.63), dist=1.25, height=1.67, lens=85)
    R.render(os.path.join(renders, name + "_game_face.png"), 900, 900)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [a for a in args if not a.startswith("--")]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for n in names:
        try:
            export(n, preview="--preview" in args)
        except Exception:
            import traceback
            traceback.print_exc()
            print("[cast] EXPORT FAILED", n, flush=True)
    print("[cast] EXPORT DONE", flush=True)


if __name__ == "__main__":
    main()
