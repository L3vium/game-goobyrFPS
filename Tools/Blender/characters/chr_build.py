# Author : Baptiste Vu Liem
"""Build realistic rigged characters (MPFB / MakeHuman) and export them as Unity-ready FBX.

Usage (headless, re-runnable):
  blender -b --python Tools/Blender/characters/chr_build.py -- Chr_Agent [Chr_Guard_Snow ...] [--preview] [--no-export]
  blender -b --python Tools/Blender/characters/chr_build.py -- all --preview

Output: Assets/_Game/Art/Characters/<Name>/<Name>.fbx (+ textures), work .blend in WORK_DIR for the
animation builder, previews in $GOOBYR_PREVIEW_DIR (if set).
Requires the MPFB extension and MakeHuman asset packs (fetch_assets.py).
"""
import math
import os
import random
import re
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chr_common as C  # noqa: E402
import chr_textures as T  # noqa: E402
from chr_specs import CHARACTERS  # noqa: E402

from bl_ext.blender_org.mpfb.services.humanservice import HumanService  # noqa: E402
from bl_ext.blender_org.mpfb.services.targetservice import TargetService  # noqa: E402
from bl_ext.blender_org.mpfb.services.locationservice import LocationService  # noqa: E402

TRI_BUDGET = 24500
LOD1_RATIO = 0.4

MH_DATA = LocationService.get_user_data()
MPFB_DATA = LocationService.get_mpfb_data()

PART_ROUGH = {"Body": 0.52, "Eyes": 0.08, "Brows": 0.85, "Lashes": 0.85, "Hair": 0.62}
ALPHA_PARTS = {"Brows", "Lashes", "Hair"}
TEX_SIZE = {"Body": 2048, "Eyes": 512, "Brows": 512, "Lashes": 512, "Hair": 1024}


def log(*a):
    print("[chr]", *a, flush=True)


# ------------------------------------------------------------------------------------------ MPFB

def asset_path(sub, name, ext):
    return "%s/%s.%s" % (name, name, ext)


def create_human(spec):
    hi = HumanService._create_default_human_info_dict()
    ph = dict(spec["phenotype"])
    hi["phenotype"].update(ph)
    hi["phenotype"]["race"] = dict(spec["race"])
    hi["rig"] = "game_engine"
    hi["eyes"] = asset_path("eyes", "high-poly", "mhclo")
    hi["eyebrows"] = asset_path("eyebrows", spec["eyebrows"], "mhclo") if spec.get("eyebrows") else ""
    hi["eyelashes"] = asset_path("eyelashes", spec["eyelashes"], "mhclo") if spec.get("eyelashes") else ""
    hi["hair"] = asset_path("hair", spec["hair"], "mhclo") if spec.get("hair") else ""
    hi["proxy"] = ""
    hi["clothes"] = [asset_path("clothes", c["asset"], "mhclo") for c in spec["clothes"]]
    hi["skin_mhmat"] = spec["skin"]
    hi["skin_material_type"] = "MAKESKIN"
    hi["clothes_material_type"] = "MAKESKIN"
    hi["eyes_material_type"] = "MAKESKIN"
    targets = [dict(target=t, value=v) for t, v in spec.get("targets", [])]
    rnd = random.Random(spec.get("face_seed", 0))
    face_pool = ["nose-scale-horiz-incr", "nose-scale-horiz-decr", "nose-point-width-incr", "nose-volume-incr",
                 "nose-volume-decr", "nose-trans-up", "nose-trans-down", "chin-width-incr", "chin-width-decr",
                 "chin-height-incr", "chin-height-decr", "mouth-scale-horiz-incr", "mouth-scale-horiz-decr",
                 "mouth-lowerlip-volume-incr", "mouth-upperlip-volume-decr", "cheek-bones-incr", "cheek-bones-decr",
                 "head-age-incr", "forehead-scale-vert-incr", "forehead-scale-vert-decr", "ears-scale-decr",
                 "eyebrows-trans-down", "eyebrows-angle-down"]
    for t in rnd.sample(face_pool, 6):
        targets.append(dict(target=t, value=round(rnd.uniform(0.15, 0.45), 2)))
    hi["targets"] = targets
    st = HumanService.get_default_deserialization_settings()
    st["subdiv_levels"] = 0
    bm = HumanService.deserialize_from_dict(hi, st)
    return bm


def classify_meshes(spec, arm):
    """Return list of (object, part, cfg) for every mesh parented to the rig."""
    out = []
    clothes = {c["asset"].lower(): c for c in spec["clothes"]}
    for o in arm.children:
        if o.type != "MESH":
            continue
        imgs = []
        for s in o.material_slots:
            if s.material and s.material.node_tree:
                imgs += [n.image.filepath for n in s.material.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
        path = " ".join(imgs).replace("/", "\\").lower()
        part, cfg = None, {}
        m = re.search(r"\\clothes\\([^\\]+)\\", path)
        if "\\skins\\" in path or o.name.lower() in ("human", spec["name"].lower() + ".body"):
            part = "Body"
        elif "\\eyes\\" in path:
            part = "Eyes"
        elif "\\eyebrows\\" in path:
            part = "Brows"
        elif "\\eyelashes\\" in path:
            part = "Lashes"
        elif "\\hair\\" in path:
            part = "Hair"
        elif "\\teeth\\" in path or "\\tongue\\" in path:
            part = None
        elif m and m.group(1) in clothes:
            cfg = clothes[m.group(1)]
            part = cfg["part"]
        log("mesh", o.name, "->", part)
        out.append((o, part, cfg))
    return out


def read_asset_credits(spec):
    rows = []
    for c in spec["clothes"]:
        p = os.path.join(MH_DATA, "clothes", c["asset"], c["asset"] + ".mhclo")
        author, lic = "MakeHuman team", "CC0"
        if os.path.isfile(p):
            with open(p, encoding="utf-8", errors="ignore") as f:
                for line in f.readlines()[:12]:
                    if line.lower().startswith("# author"):
                        author = line.split(" ", 2)[2].strip()
                    if line.lower().startswith("# license"):
                        lic = line.split(" ", 2)[2].strip()
        rows.append((c["asset"], author, lic))
    return rows


# ------------------------------------------------------------------------------------------ geometry

def delete_vertices(obj, predicate_groups, keep_group=None):
    """Delete vertices that belong (w>0.5) to any group in predicate_groups, or not in keep_group."""
    gi = {g.name: g.index for g in obj.vertex_groups}
    dels = set(gi[g] for g in predicate_groups if g in gi)
    keep = gi.get(keep_group) if keep_group else None
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    dl = bm.verts.layers.deform.active
    kill = []
    for v in bm.verts:
        d = v[dl]
        if any(d.get(i, 0.0) > 0.5 for i in dels):
            kill.append(v)
        elif keep is not None and d.get(keep, 0.0) < 0.5:
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return len(kill)


def delete_faces_by_uv(obj, pred):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    kill = []
    for f in bm.faces:
        u = sum(l[uv].uv.x for l in f.loops) / len(f.loops)
        v = sum(l[uv].uv.y for l in f.loops) / len(f.loops)
        if pred(u, v):
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    log("deleted faces by uv", obj.name, len(kill))


def smooth_weights(obj, arm, factor=0.5, repeat=6):
    """Laplacian smoothing of the bone weights over the mesh edges (numpy), renormalised."""
    bones = {b.name for b in arm.data.bones}
    groups = [g for g in obj.vertex_groups if g.name in bones]
    if not groups:
        return
    gidx = {g.index: i for i, g in enumerate(groups)}
    nv = len(obj.data.vertices)
    W = np.zeros((nv, len(groups)), np.float32)
    for v in obj.data.vertices:
        for g in v.groups:
            if g.group in gidx:
                W[v.index, gidx[g.group]] = g.weight
    e = np.empty(len(obj.data.edges) * 2, np.int32)
    obj.data.edges.foreach_get("vertices", e)
    e = e.reshape(-1, 2)
    deg = np.bincount(e.ravel(), minlength=nv).astype(np.float32)[:, None]
    for _ in range(repeat):
        acc = np.zeros_like(W)
        np.add.at(acc, e[:, 0], W[e[:, 1]])
        np.add.at(acc, e[:, 1], W[e[:, 0]])
        avg = np.where(deg > 0, acc / np.maximum(deg, 1), W)
        W = W * (1 - factor) + avg * factor
    W /= np.maximum(W.sum(1, keepdims=True), 1e-6)
    for i, g in enumerate(groups):
        col = W[:, i]
        nz = np.nonzero(col > 0.002)[0]
        z = np.nonzero(col <= 0.002)[0]
        if len(z):
            g.remove(z.tolist())
        for vi in nz.tolist():
            g.add([vi], float(col[vi]), "REPLACE")


EXPOSED_BONES = ("head", "neck_01", "hand_", "thumb_", "index_", "middle_", "ring_", "pinky_")


def hide_covered_skin(body, cloths, reach=0.12):
    """Delete body vertices whose outward normal ray hits a garment within `reach` metres."""
    import bmesh
    from mathutils.bvhtree import BVHTree
    trees = []
    for c in cloths:
        bmc = bmesh.new()
        bmc.from_mesh(c.data)
        bmc.transform(c.matrix_world)
        trees.append(BVHTree.FromBMesh(bmc))
        bmc.free()
    names = {g.index: g.name for g in body.vertex_groups}
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.normal_update()
    dl = bm.verts.layers.deform.active
    mw = body.matrix_world
    kill = []
    for v in bm.verts:
        d = v[dl]
        if d:
            top = max(d.items(), key=lambda kv: kv[1])[0]
            if names.get(top, "").startswith(EXPOSED_BONES):
                continue
        co = mw @ v.co
        n = (mw.to_3x3() @ v.normal).normalized()
        o = co - n * 0.002
        if any(t.ray_cast(o, n, reach)[0] is not None for t in trees):
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(body.data)
    bm.free()
    log("covered skin removed:", len(kill), "verts")


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def decimate(obj, ratio, protect=None):
    """Collapse-decimate obj. protect: {vertex_group_name: strength 0..1} areas kept denser."""
    if ratio >= 0.999:
        return
    dec_group = None
    if protect:
        gi = {g.name: g.index for g in obj.vertex_groups}
        dec_group = obj.vertex_groups.new(name="_decimate")
        for v in obj.data.vertices:
            w = 1.0
            for g in v.groups:
                for name, k in protect.items():
                    if gi.get(name) == g.group:
                        w = min(w, 1.0 - k * g.weight)
            dec_group.add([v.index], max(w, 0.0), "REPLACE")
    C.select_only(obj)
    arm_mods = [m for m in obj.modifiers if m.type == "ARMATURE"]
    arm_obj = arm_mods[0].object if arm_mods else None
    for m in list(obj.modifiers):
        obj.modifiers.remove(m)
    d = obj.modifiers.new("Decimate", "DECIMATE")
    d.decimate_type = "COLLAPSE"
    d.ratio = ratio
    d.use_collapse_triangulate = False
    if dec_group is not None:
        d.vertex_group = dec_group.name
        d.vertex_group_factor = 1.0
    bpy.ops.object.modifier_apply(modifier=d.name)
    if dec_group is not None:
        obj.vertex_groups.remove(obj.vertex_groups[dec_group.name])
    if arm_obj:
        a = obj.modifiers.new("Armature", "ARMATURE")
        a.object = arm_obj


def clean_weights(obj, arm, limit=4):
    bones = {b.name for b in arm.data.bones}
    for g in list(obj.vertex_groups):
        if g.name not in bones:
            obj.vertex_groups.remove(g)
    C.select_only(obj)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=limit)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.001)


def make_earpiece(arm, body, eyes):
    """Covert security earpiece: bud in the right ear + coiled wire down to the collar (procedural)."""
    mw = body.matrix_world
    ez = sum((mw @ v.co).z for v in eyes.data.vertices) / len(eyes.data.vertices)
    head_gi = body.vertex_groups["head"].index
    cand = []
    for v in body.data.vertices:
        co = mw @ v.co
        if co.x < 0 and abs(co.z - (ez - 0.025)) < 0.012 and any(g.group == head_gi and g.weight > 0.8 for g in v.groups):
            cand.append(co)
    rim = min(cand, key=lambda c: c.x)
    bud = rim + Vector((0.010, -0.004, 0.0))
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.0065, location=bud)
    b = bpy.context.object
    b.scale = (0.8, 1.0, 1.0)
    neck = arm.data.bones["neck_01"].head_local
    pts = [bud + Vector((-0.002, 0.004, -0.004)), bud + Vector((0.0, 0.016, -0.03)),
           Vector((bud.x + 0.012, neck.y + 0.045, neck.z + 0.04)), Vector((bud.x + 0.02, neck.y + 0.06, neck.z - 0.03))]
    cu = bpy.data.curves.new("EarWire", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 0.0016
    cu.bevel_resolution = 1
    sp = cu.splines.new("NURBS")
    sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts):
        p.co = (c.x, c.y, c.z, 1)
    sp.use_endpoint_u = True
    sp.order_u = 3
    sp.resolution_u = 6
    w = bpy.data.objects.new("EarWire", cu)
    bpy.context.scene.collection.objects.link(w)
    C.select_only(w)
    bpy.ops.object.convert(target="MESH")
    w = bpy.context.object
    C.select_only(b)
    w.select_set(True)
    bpy.ops.object.join()
    ep = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if not ep.data.uv_layers:
        ep.data.uv_layers.new(name="UVMap")
    gh = ep.vertex_groups.new(name="head")
    gn = ep.vertex_groups.new(name="neck_01")
    for v in ep.data.vertices:
        t = max(0.0, min(1.0, (bud.z - 0.03 - v.co.z) / 0.06))
        gh.add([v.index], 1.0 - t, "REPLACE")
        if t > 0:
            gn.add([v.index], t, "REPLACE")
    ep.parent = arm
    ep.modifiers.new("Armature", "ARMATURE").object = arm
    ep.name = "Earpiece"
    log("earpiece at", tuple(round(x, 3) for x in bud))
    return ep


# ------------------------------------------------------------------------------------------ materials

def material_images(obj):
    found = {}
    for s in obj.material_slots:
        if not (s.material and s.material.node_tree):
            continue
        for n in s.material.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                lab = (n.label or n.name).lower()
                p = bpy.path.abspath(n.image.filepath)
                if "diffuse" in lab:
                    found["diff"] = p
                elif "normal" in lab:
                    found["norm"] = p
                elif "bump" in lab:
                    found["bump"] = p
    # look for siblings that MakeSkin did not wire
    if "diff" in found:
        d = os.path.dirname(found["diff"])
        for f in os.listdir(d):
            fl = f.lower()
            if "norm" not in found and re.search(r"(norm|nrm|nrml|normal)\.(png|jpg)$", fl):
                found["norm"] = os.path.join(d, f)
    return found


def build_textures(spec, name, part, cfg, obj, out_dir):
    slot = "%s_%s" % (name, part)
    src = material_images(obj)
    size = cfg.get("tex", TEX_SIZE.get(part, 2048))
    alpha = part in ALPHA_PARTS
    if part == "Eyes":
        src["diff"] = os.path.join(MH_DATA, "eyes", "materials", spec.get("eyes", "brown") + "_eye.png")
    if "flat" in cfg:
        diff = np.ones((size, size, 4), np.float32)
        diff[..., :3] = cfg["flat"]
    else:
        diff = T.load_rgba(src["diff"]) if "diff" in src else np.full((size, size, 4), 0.5, np.float32)
    h, w = diff.shape[:2]
    if max(h, w) > size:
        diff = T.resize(diff, (size, size))
    rgb, a = diff[..., :3].copy(), diff[..., 3].copy()
    # ---- recolour
    if "fill" in cfg:
        rgb = T.fill_boxes(rgb, cfg["fill"][0], cfg["fill"][1])
    if "keys" in cfg:
        rgb = T.recolour_keys(rgb, cfg["keys"], sigma=cfg.get("sigma", 0.16))
    if "tint" in cfg:
        rgb = T.tint(rgb, cfg["tint"], cfg.get("contrast", 1.0))
    for boxes, colour, contrast in cfg.get("box_tint", []):
        m = T.box_mask(rgb.shape[0], rgb.shape[1], boxes)[..., None]
        rgb = T.tint(rgb, colour, contrast) * m + rgb * (1 - m)
    if "camo" in cfg:
        cm = cfg["camo"]
        mask = T.box_mask(rgb.shape[0], rgb.shape[1], cm["boxes"]) if "boxes" in cm else None
        rgb = T.apply_camo(rgb, cm["colours"], scale=int(cm.get("scale", 40) * rgb.shape[1] / 1024),
                           seed=cm.get("seed", 3), kind=cm.get("kind", "woodland"), mask=mask)
    if part == "Hair" and spec.get("hair_colour"):
        rgb = T.tint(rgb, spec["hair_colour"], 1.1)
    if part == "Brows" and spec.get("brow_colour"):
        rgb = T.tint(rgb, spec["brow_colour"], 1.0)
    if part == "Lashes":
        rgb = T.tint(rgb, (0.05, 0.04, 0.035), 1.0)
    if part == "Body" and spec.get("skin_tint"):
        rgb = np.clip(rgb * np.array(spec["skin_tint"], np.float32), 0, 1)
    # ---- normal
    if "norm" in src and part not in ("Body",) and not cfg.get("no_src_normal"):
        nrm = T.load_rgba(src["norm"])[..., :3]
        if max(nrm.shape[:2]) > size:
            nrm = T.resize(nrm, (size, size))
        if cfg.get("flip_green"):
            nrm[..., 1] = 1.0 - nrm[..., 1]
    elif "bump" in src:
        b = T.load_rgba(src["bump"])
        b = T.resize(b, (rgb.shape[1], rgb.shape[0]))
        nrm = T.height_to_normal(T.lum(b[..., :3]), 6.0 * rgb.shape[1] / 1024.0)
    else:
        strength = {"Body": 1.6, "Eyes": 0.0, "Hair": 1.0}.get(part, 2.5)
        nrm = T.detail_normal_from_albedo(rgb, strength=strength, radius=4) if strength > 0 else \
            np.concatenate([np.full(rgb.shape[:2] + (2,), 0.5, np.float32), np.ones(rgb.shape[:2] + (1,), np.float32)], -1)
    # ---- ARM (AO from albedo cavity, roughness constant + albedo variation, metallic)
    l = T.lum(rgb)
    cav = np.clip(1.0 - np.maximum(T.blur(l, 6) - l, 0) * 2.5, 0.55, 1.0)
    rough = np.clip(cfg.get("rough", PART_ROUGH.get(part, 0.75)) + (0.5 - l) * 0.08, 0.04, 1.0)
    metal = np.full_like(l, cfg.get("metal", 0.0))
    arm = np.stack([cav, rough, metal], axis=-1)
    ext = ".png" if alpha else ".jpg"
    paths = dict(diff=os.path.join(out_dir, slot + "_diff" + ext),
                 nor=os.path.join(out_dir, slot + "_nor_gl.jpg"),
                 arm=os.path.join(out_dir, slot + "_arm.jpg"))
    T.save_rgba(np.concatenate([rgb, a[..., None] if alpha else np.ones_like(a)[..., None]], -1), paths["diff"])
    T.save_rgba(nrm.astype(np.float32), paths["nor"])
    T.save_rgba(arm.astype(np.float32), paths["arm"])
    return slot, paths, alpha


def make_material(slot, paths, alpha):
    mat = bpy.data.materials.get(slot) or bpy.data.materials.new(slot)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])

    def tex(p, non_color):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(p, check_existing=True)
        if non_color:
            n.image.colorspace_settings.name = "Non-Color"
        return n

    d = tex(paths["diff"], False)
    nt.links.new(d.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha:
        nt.links.new(d.outputs["Alpha"], bsdf.inputs["Alpha"])
        try:
            mat.surface_render_method = "DITHERED"
        except Exception:
            mat.blend_method = "HASHED"
    nn = tex(paths["nor"], True)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    a = tex(paths["arm"], True)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(a.outputs["Color"], sep.inputs[0])
    nt.links.new(sep.outputs[1], bsdf.inputs["Roughness"])
    nt.links.new(sep.outputs[2], bsdf.inputs["Metallic"])
    if "Subsurface Weight" in bsdf.inputs and slot.endswith("_Body"):
        bsdf.inputs["Subsurface Weight"].default_value = 0.15
        bsdf.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.2)
        bsdf.inputs["Subsurface Scale"].default_value = 0.01
    return mat


# ------------------------------------------------------------------------------------------ main build

def build(name, export=True, preview_dir=None):
    if name == ARMS_NAME:
        spec = dict(CHARACTERS[ARMS_SOURCE])
        spec["arms_only"] = True
    else:
        spec = dict(CHARACTERS[name])
    spec["name"] = name
    log("=== building", name)
    C.clean_scene()
    bm = create_human(spec)
    arm = bm.parent
    TargetService.bake_targets(bm)
    parts = classify_meshes(spec, arm)

    # drop teeth/tongue/unknown meshes
    for o, part, cfg in list(parts):
        if part is None:
            bpy.data.objects.remove(o, do_unlink=True)
    parts = [p for p in parts if p[1] is not None]

    # eyes: drop the transparent cornea shell (its UVs sit on the black disc in the corner)
    for o, part, cfg in parts:
        if part == "Eyes":
            delete_faces_by_uv(o, lambda u, v: u > 0.82 and v < 0.18)

    # body: remove helper geometry and the skin hidden under clothes
    delete_groups = [g.name for g in bm.vertex_groups if g.name.startswith("Delete.")]
    n = delete_vertices(bm, delete_groups, keep_group="body")
    log("body verts deleted", n)
    for o, part, cfg in parts:
        for m in list(o.modifiers):
            if m.type == "MASK":
                o.modifiers.remove(m)

    # push inner layers (e.g. a waistband under an untucked shirt) slightly inwards
    height = max((bm.matrix_world @ v.co).z for v in bm.data.vertices)
    for o, part, cfg in parts:
        if "shrink" in cfg:
            z0, z1, d = cfg["shrink"]
            for v in o.data.vertices:
                if z0 * height <= v.co.z <= z1 * height:
                    v.co -= v.normal * d

    # delete inner-layer geometry fully covered by an outer garment (e.g. a waistband under a polo)
    for o, part, cfg in parts:
        if "hide_under" in cfg:
            outer_name, margin = cfg["hide_under"]
            outer = next(x for x, p2, c2 in parts if p2 == outer_name)
            hem = min(v.co.z for v in outer.data.vertices)
            import bmesh
            bmx = bmesh.new()
            bmx.from_mesh(o.data)
            kill = [v for v in bmx.verts if v.co.z > hem + margin]
            bmesh.ops.delete(bmx, geom=kill, context="VERTS")
            bmx.to_mesh(o.data)
            bmx.free()
            log("hid", len(kill), "verts of", part, "above", round(hem + margin, 3))

    # rest pose -> shared A-pose reference
    meshes = [p[0] for p in parts]
    C.pose_tpose(arm)
    C.apply_pose_as_rest(arm, meshes)

    # clothing: smooth the skin weights (softer shoulder/hip transitions, no creases on the jacket)
    for o, part, cfg in parts:
        if part not in ("Body", "Eyes", "Brows", "Lashes", "Hair") and cfg.get("smooth_weights", True):
            smooth_weights(o, arm, factor=0.5, repeat=cfg.get("smooth_repeat", 6))

    # skin fully covered by clothing never shows: delete it (no poke-through when the cloth is decimated)
    body_obj = next((o for o, p, c in parts if p == "Body"), None)
    cloth_objs = [o for o, p, c in parts if p not in ("Body", "Eyes", "Brows", "Lashes", "Hair", "Earpiece")]
    if body_obj and cloth_objs:
        hide_covered_skin(body_obj, cloth_objs)

    if spec.get("arms_only"):
        parts, meshes = keep_arms(parts)

    if spec.get("earpiece") and not spec.get("arms_only"):
        eyes = next(o for o, p, c in parts if p == "Eyes")
        ep = make_earpiece(arm, bm, eyes)
        parts.append((ep, "Earpiece", dict(flat=(0.07, 0.065, 0.06), rough=0.3, tex=64)))
        meshes.append(ep)

    # budget
    fixed = {"Body", "Eyes", "Brows", "Lashes", "Earpiece"}
    tris = {o.name: tri_count(o) for o in meshes}
    total = sum(tris.values())
    fixed_tris = sum(tris[o.name] for o, p, c in parts if p in fixed)
    flex_tris = total - fixed_tris
    log("tris before", total, "fixed", fixed_tris, "flex", flex_tris)
    if total > TRI_BUDGET and not spec.get("arms_only"):
        body = next(o for o, p, c in parts if p == "Body")
        bt = tri_count(body)
        if bt > 9000:
            decimate(body, 9000.0 / bt, protect={"head": 0.8, "neck_01": 0.3})
            fixed_tris -= bt - tri_count(body)
        ratio = max(0.2, (TRI_BUDGET - fixed_tris) / max(flex_tris, 1))
        for o, part, cfg in parts:
            if part not in fixed:
                decimate(o, ratio * cfg.get("decimate_bias", 1.0))
        total = sum(tri_count(o) for o in meshes)
        if total > TRI_BUDGET:
            body = next(o for o, p, c in parts if p == "Body")
            bt = tri_count(body)
            decimate(body, max(0.4, (bt - (total - TRI_BUDGET)) / bt))
    log("tris after", sum(tri_count(o) for o in meshes))

    # textures + materials
    out_dir = os.path.join(C.CHAR_DIR, name)
    os.makedirs(out_dir, exist_ok=True)
    mats = {}
    for o, part, cfg in parts:
        key = part
        if key not in mats:
            slot, paths, alpha = build_textures(spec, name, part, cfg, o, out_dir)
            mats[key] = make_material(slot, paths, alpha)
        o.data.materials.clear()
        o.data.materials.append(mats[key])

    # join into one LOD0 mesh
    for o in meshes:
        clean_weights(o, arm)
    C.select_only(meshes[0])
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = next(o for o, p, c in parts if p == "Body")
    bpy.ops.object.join()
    lod0 = bpy.context.object
    lod0.name = lod0.data.name = name if spec.get("arms_only") else name + "_LOD0"
    for m in list(lod0.modifiers):
        if m.type != "ARMATURE":
            lod0.modifiers.remove(m)
    if not any(m.type == "ARMATURE" for m in lod0.modifiers):
        lod0.modifiers.new("Armature", "ARMATURE").object = arm
    # merge material slots with identical materials
    C.select_only(lod0)
    bpy.ops.object.material_slot_remove_unused()
    bpy.ops.object.shade_smooth()

    if spec.get("arms_only"):
        arm.name = arm.data.name = C.ARMATURE_NAME
        for b in arm.pose.bones:
            b.rotation_mode = "QUATERNION"
        log("RESULT", name, "tris", tri_count(lod0), "materials", [s.material.name for s in lod0.material_slots])
        if export:
            C.export_fbx(os.path.join(out_dir, name + ".fbx"), [arm, lod0])
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.WORK_DIR, name + ".blend"))
        if preview_dir:
            render_arms_preview(name, arm, lod0, preview_dir)
        return arm, lod0, None

    # LOD1
    lod1 = lod0.copy()
    lod1.data = lod0.data.copy()
    lod1.name = lod1.data.name = name + "_LOD1"
    bpy.context.scene.collection.objects.link(lod1)
    decimate(lod1, LOD1_RATIO)
    clean_weights(lod1, arm)
    lod1.parent = arm

    arm.name = C.ARMATURE_NAME
    arm.data.name = C.ARMATURE_NAME
    arm.data.display_type = "STICK"
    for b in arm.pose.bones:
        b.rotation_mode = "QUATERNION"

    log("RESULT", name, "LOD0 tris", tri_count(lod0), "LOD1 tris", tri_count(lod1),
        "materials", [s.material.name for s in lod0.material_slots])
    for asset, author, lic in read_asset_credits(spec):
        log("CREDIT", name, asset, "|", author, "|", lic)

    if export:
        C.export_fbx(os.path.join(out_dir, name + ".fbx"), [arm, lod0, lod1])
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.WORK_DIR, name + ".blend"))
    if preview_dir:
        render_previews(name, lod0, lod1, preview_dir)
    return arm, lod0, lod1


ARMS_NAME = "Chr_Arms"
ARMS_SOURCE = "Chr_Agent"
ARM_PREFIX = ("upperarm_", "lowerarm_", "hand_", "thumb_", "index_", "middle_", "ring_", "pinky_")


def keep_arms(parts):
    """First-person arms: keep only skin/suit vertices driven by the arm bones (clavicle excluded)."""
    kept = []
    for o, part, cfg in parts:
        if part in ("Eyes", "Brows", "Lashes", "Hair", "Shoes", "Boots"):
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        arm_idx = {g.index for g in o.vertex_groups if g.name.startswith(ARM_PREFIX)}
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(o.data)
        dl = bm.verts.layers.deform.active
        kill = [v for v in bm.verts if sum(w for i, w in v[dl].items() if i in arm_idx) < 0.5]
        bmesh.ops.delete(bm, geom=kill, context="VERTS")
        bm.to_mesh(o.data)
        bm.free()
        if len(o.data.vertices) == 0:
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        kept.append((o, part, cfg))
    return kept, [p[0] for p in kept]


def render_arms_preview(name, arm, mesh, preview_dir):
    cam = C.setup_preview_scene()
    C.aim_camera(cam, (0, 0, 1.3), 1.6, 20, 1.5, lens=40)
    C.render_to(os.path.join(preview_dir, name + "_front.png"))
    C.aim_camera(cam, (0, 0, 1.3), 1.6, 160, 1.5, lens=40)
    C.render_to(os.path.join(preview_dir, name + "_back.png"))


def render_previews(name, lod0, lod1, preview_dir):
    lod1.hide_render = True
    cam = C.setup_preview_scene()
    h = max(v.co.z for v in lod0.data.vertices)
    C.aim_camera(cam, (0, 0, h * 0.55), 4.2, 0, h * 0.6, lens=50)
    C.render_to(os.path.join(preview_dir, name + "_front.png"))
    C.aim_camera(cam, (0, 0, h * 0.55), 4.2, 90, h * 0.6, lens=50)
    C.render_to(os.path.join(preview_dir, name + "_side.png"))
    C.aim_camera(cam, (0, 0, h * 0.91), 1.1, 25, h * 0.93, lens=60)
    C.render_to(os.path.join(preview_dir, name + "_face.png"))
    C.aim_camera(cam, (0, 0, h * 0.55), 4.2, 180, h * 0.6, lens=50)
    C.render_to(os.path.join(preview_dir, name + "_back.png"))


def main():
    args = C.argv_after_dashes()
    names = [a for a in args if not a.startswith("--")]
    if not names or names == ["all"]:
        names = list(CHARACTERS.keys()) + [ARMS_NAME]
    preview_dir = os.environ.get("GOOBYR_PREVIEW_DIR") if "--preview" in args else None
    for n in names:
        build(n, export="--no-export" not in args, preview_dir=preview_dir)


if __name__ == "__main__":
    main()
