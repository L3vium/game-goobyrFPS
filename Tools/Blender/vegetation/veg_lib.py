# Author : Baptiste Vu Liem
"""Shared Blender helpers of the realistic vegetation builders.

MeshBuilder (per-vertex position / normal / uv / colour), tapered tubes with parallel-transport frames,
alpha-tested cards and folded strips mapped on atlas cells, materials, LOD objects, billboard (impostor)
baking, FBX export and Eevee preview renders.

Vertex colour convention (exported in the FBX, usable by a wind / foliage shader):
  R = ambient occlusion (canopy interior darker), G = per-branch random phase, B = wind weight
  (0 at the trunk base -> 1 on leaf tips), A = 1.
"""
import json
import math
import os
import random
import shutil

import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

UP = Vector((0, 0, 1))


# ------------------------------------------------------------------ context
class Ctx:
    src = ""      # Poly Haven cache
    work = ""     # prep_textures output (atlas/, tex/)
    out = ""      # Assets/_Game/Art/PBR/Models
    preview = ""  # preview png folder ('' = none)
    bake = True


def atlas(name):
    with open(os.path.join(Ctx.work, "atlas", name + ".json")) as f:
        d = json.load(f)
    return d["cells"]


# ------------------------------------------------------------------ mesh builder
class MeshBuilder:
    def __init__(self):
        self.v, self.n, self.uv, self.col = [], [], [], []
        self.faces, self.mats = [], []

    def vert(self, co, n, uv, col=(1, 0, 0, 1)):
        self.v.append((co[0], co[1], co[2]))
        self.n.append((n[0], n[1], n[2]))
        self.uv.append((uv[0], uv[1]))
        self.col.append(col)
        return len(self.v) - 1

    def face(self, idx, mat):
        self.faces.append(tuple(idx))
        self.mats.append(mat)

    def tris(self):
        return sum(len(f) - 2 for f in self.faces)

    def to_object(self, name, materials):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.faces)
        for m in materials:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", self.mats)
        loops = np.array([i for f in self.faces for i in f], np.int64)
        uvl = me.uv_layers.new(name="UVMap")
        uv = np.array(self.uv, np.float32)[loops]
        uvl.data.foreach_set("uv", uv.ravel())
        ca = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
        col = np.array(self.col, np.float32)[loops]
        ca.data.foreach_set("color", col.ravel())
        me.polygons.foreach_set("use_smooth", [True] * len(self.faces))
        nrm = np.array(self.n, np.float32)
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-6)
        me.normals_split_custom_set_from_vertices([tuple(x) for x in nrm])
        me.update()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


# ------------------------------------------------------------------ tubes
def frames(path):
    n = len(path)
    tans = []
    for i in range(n):
        a = path[max(i - 1, 0)]
        b = path[min(i + 1, n - 1)]
        tans.append((b - a).normalized())
    t0 = tans[0]
    ref = Vector((1, 0, 0)) if abs(t0.x) < 0.9 else Vector((0, 1, 0))
    N = t0.cross(ref).normalized()
    out = []
    prev = t0
    for t in tans:
        q = prev.rotation_difference(t)
        N = (q @ N)
        N = (N - t * N.dot(t)).normalized()
        out.append((t, N, t.cross(N)))
        prev = t
    return out


def tube(mb, path, radii, sides, mat, tile=1.0, col_fn=None, radial_fn=None, v0=0.0, u_rep=None, close_tip=True, seam_rot=0.0):
    """Tapered tube along `path`. UV: u around (u_rep repeats), v along length (metres / tile).
    radial_fn(i, angle) -> radius multiplier (buttresses, gnarls). Returns the final v."""
    fr = frames(path)
    if u_rep is None:
        u_rep = max(1, round(2 * math.pi * radii[0] / tile))
    v = v0
    rings = []
    for i, p in enumerate(path):
        if i > 0:
            v += (path[i] - path[i - 1]).length * u_rep / max(2 * math.pi * max(radii[0], 1e-3), 1e-3)
        t, N, B = fr[i]
        ring = []
        for j in range(sides + 1):
            a = 2 * math.pi * j / sides + seam_rot
            d = N * math.cos(a) + B * math.sin(a)
            r = radii[i] * (radial_fn(i, a) if radial_fn else 1.0)
            co = p + d * r
            col = col_fn(i / max(len(path) - 1, 1), co) if col_fn else (1, 0, 0, 1)
            ring.append(mb.vert(co, d, (u_rep * j / sides, v), col))
        rings.append(ring)
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for j in range(sides):
            mb.face((a[j], a[j + 1], b[j + 1], b[j]), mat)
    if close_tip and radii[-1] > 0.002:
        t = fr[-1][0]
        c = mb.vert(path[-1] + t * radii[-1] * 0.6, t, (0.5 * u_rep, v + radii[-1] / tile),
                    col_fn(1.0, path[-1]) if col_fn else (1, 0, 0, 1))
        last = rings[-1]
        for j in range(sides):
            mb.face((last[j], last[j + 1], c), mat)
    return v


# ------------------------------------------------------------------ cards
def cell_uv(cell, s, t):
    """(s across 0..1, t along 0..1) -> uv inside the atlas cell."""
    u0, v0, u1, v1 = cell["rect"]
    return (u0 + (u1 - u0) * s, v0 + (v1 - v0) * t)


def strip(mb, path, sides_vec, widths, cell, mat, fold=0.0, normal_fn=None, col_fn=None, s_range=(0.0, 1.0)):
    """Card following a polyline: rows along `path`, three columns (left / mid-rib / right).
    fold > 0 lifts the mid rib along the card normal (V-fold, fraction of the half width)."""
    n = len(path)
    rows = []
    for i in range(n):
        t = i / (n - 1)
        side = sides_vec[i]
        fwd = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        nrm = fwd.cross(side).normalized()
        w = widths[i]
        row = []
        for k, s in enumerate((0.0, 0.5, 1.0)):
            off = (s - 0.5) * w
            co = path[i] + side * off + (nrm * fold * w * 0.5 if k == 1 else Vector())
            nn = normal_fn(co, nrm) if normal_fn else nrm
            ss = s_range[0] + (s_range[1] - s_range[0]) * s
            row.append(mb.vert(co, nn, cell_uv(cell, ss, t), col_fn(t, co) if col_fn else (1, 0.5, 1, 1)))
        rows.append(row)
    for i in range(n - 1):
        a, b = rows[i], rows[i + 1]
        mb.face((a[0], a[1], b[1], b[0]), mat)
        mb.face((a[1], a[2], b[2], b[1]), mat)


def card(mb, base, axis, roll_normal, length, cell, mat, segs=1, droop=0.0, normal_fn=None, col_fn=None, width_mul=1.0, curl=0.0):
    """Flat (segs=1) or bending card, base at the bottom centre of the cell, growing along `axis`.
    roll_normal: wished card normal (projected perpendicular to axis). Two triangles per segment."""
    axis = axis.normalized()
    nrm = (roll_normal - axis * roll_normal.dot(axis))
    if nrm.length < 1e-4:
        nrm = axis.orthogonal()
    nrm.normalize()
    side = axis.cross(nrm).normalized()
    width = length * cell["aspect"] * width_mul
    pts = []
    p = Vector(base)
    d = axis.copy()
    seg_len = length / segs
    for i in range(segs + 1):
        pts.append(p.copy())
        d = (d + Vector((0, 0, -droop / segs)) + nrm * (curl / segs)).normalized()
        p = p + d * seg_len
    rows = []
    for i, q in enumerate(pts):
        t = i / segs
        row = []
        for s in (0.0, 1.0):
            co = q + side * (s - 0.5) * width
            nn = normal_fn(co, nrm) if normal_fn else nrm
            row.append(mb.vert(co, nn, cell_uv(cell, s, t), col_fn(t, co) if col_fn else (1, 0.5, 1, 1)))
        rows.append(row)
    for i in range(segs):
        a, b = rows[i], rows[i + 1]
        mb.face((a[0], a[1], b[1], b[0]), mat)


def canopy_normal(center, k=0.6, up=0.25):
    """Leaf normal = face normal (flipped outward) blended with the direction from the canopy centre."""
    def fn(co, nrm):
        out = (Vector(co) - center)
        out = out.normalized() if out.length > 1e-4 else UP
        f = nrm if nrm.dot(out) >= 0 else -nrm
        n = f * (1 - k) + out * k + UP * up
        return n.normalized()
    return fn


# ------------------------------------------------------------------ materials
def _img(path, non_color=False):
    if not os.path.exists(path):
        return None
    im = bpy.data.images.load(path, check_existing=True)
    if non_color:
        im.colorspace_settings.name = "Non-Color"
    return im


def make_material(slot, folder, cutout):
    """Principled material named after the slot, wired to <slot>_diff/_nor_gl/_arm in `folder`."""
    m = bpy.data.materials.get(slot) or bpy.data.materials.new(slot)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
    diff = None
    for ext in ("png", "jpg"):
        diff = _img(os.path.join(folder, f"{slot}_diff.{ext}"))
        if diff:
            break
    if diff:
        tx = nt.nodes.new("ShaderNodeTexImage")
        tx.image = diff
        nt.links.new(tx.outputs["Color"], bsdf.inputs["Base Color"])
        if cutout:
            gt = nt.nodes.new("ShaderNodeMath")
            gt.operation = "GREATER_THAN"
            gt.inputs[1].default_value = 0.5
            nt.links.new(tx.outputs["Alpha"], gt.inputs[0])
            nt.links.new(gt.outputs[0], bsdf.inputs["Alpha"])
    nor = _img(os.path.join(folder, f"{slot}_nor_gl.jpg"), True) or _img(os.path.join(folder, f"{slot}_nor_gl.png"), True)
    if nor:
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = nor
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    arm = _img(os.path.join(folder, f"{slot}_arm.jpg"), True)
    if arm:
        ta = nt.nodes.new("ShaderNodeTexImage")
        ta.image = arm
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(ta.outputs["Color"], sep.inputs[0])
        nt.links.new(sep.outputs[1], bsdf.inputs["Roughness"])
    if cutout:
        # thin leaves: some light through (preview only; Unity uses its own foliage settings)
        try:
            bsdf.inputs["Transmission Weight"].default_value = 0.0
            bsdf.inputs["Subsurface Weight"].default_value = 0.0
        except KeyError:
            pass
        bsdf.inputs["Specular IOR Level"].default_value = 0.35
    m["cutout"] = bool(cutout)
    return m


def install_textures(asset, part, src_prefix, folder):
    """Copy <work>/<src_prefix>_{diff,nor_gl,arm}.* to <folder>/<asset>_<part>_*.*; returns the slot name."""
    slot = f"{asset}_{part}"
    os.makedirs(folder, exist_ok=True)
    for m in ("diff", "nor_gl", "arm"):
        for ext in ("png", "jpg"):
            s = f"{src_prefix}_{m}.{ext}"
            if os.path.exists(s):
                shutil.copyfile(s, os.path.join(folder, f"{slot}_{m}.{ext}"))
                break
    return slot


# ------------------------------------------------------------------ scene helpers
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for c in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.objects):
        for x in list(c):
            c.remove(x)


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def bounds(ob):
    vs = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", vs)
    vs = vs.reshape(-1, 3)
    return vs.min(0), vs.max(0)


def export_fbx(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL", apply_unit_scale=True, use_mesh_modifiers=True,
                             mesh_smooth_type="OFF", use_tspace=True, path_mode="STRIP", embed_textures=False,
                             bake_anim=False, add_leaf_bones=False, colors_type="LINEAR")


# ------------------------------------------------------------------ billboard (LOD2)
def _emit_material(src, mode):
    """Unlit copy of a material: 'albedo' = base colour x vertex AO, 'normal' = camera-space normal."""
    m = bpy.data.materials.new(src.name + "_" + mode)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    tex = next((n for n in src.node_tree.nodes if n.type == "TEX_IMAGE" and n.image and n.image.colorspace_settings.name != "Non-Color"), None)
    tx = None
    if tex:
        tx = nt.nodes.new("ShaderNodeTexImage")
        tx.image = tex.image
        tx.interpolation = "Closest" if mode == "alpha" else "Linear"
    if mode == "albedo":
        if tx:
            vc = nt.nodes.new("ShaderNodeVertexColor")
            vc.layer_name = "Col"
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            nt.links.new(vc.outputs["Color"], sep.inputs[0])
            mul = nt.nodes.new("ShaderNodeMix")
            mul.data_type = "RGBA"
            mul.blend_type = "MULTIPLY"
            mul.inputs["Factor"].default_value = 1.0
            ao = nt.nodes.new("ShaderNodeMath")
            ao.operation = "MULTIPLY_ADD"
            ao.inputs[1].default_value = 0.6
            ao.inputs[2].default_value = 0.4
            nt.links.new(sep.outputs[0], ao.inputs[0])
            comb = nt.nodes.new("ShaderNodeCombineColor")
            for i in range(3):
                nt.links.new(ao.outputs[0], comb.inputs[i])
            nt.links.new(tx.outputs["Color"], mul.inputs["A"])
            nt.links.new(comb.outputs[0], mul.inputs["B"])
            nt.links.new(mul.outputs["Result"], emit.inputs["Color"])
    else:
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        vt = nt.nodes.new("ShaderNodeVectorTransform")
        vt.vector_type = "NORMAL"
        vt.convert_from = "WORLD"
        vt.convert_to = "CAMERA"
        nt.links.new(geo.outputs["Normal"], vt.inputs[0])
        # Blender camera space looks down -Z: flip so the normal toward the viewer is +Z, keep X right / Y up
        flip = nt.nodes.new("ShaderNodeVectorMath")
        flip.operation = "MULTIPLY"
        flip.inputs[1].default_value = (1, 1, -1)
        nt.links.new(vt.outputs[0], flip.inputs[0])
        mad = nt.nodes.new("ShaderNodeVectorMath")
        mad.operation = "MULTIPLY_ADD"
        mad.inputs[1].default_value = (0.5, 0.5, 0.5)
        mad.inputs[2].default_value = (0.5, 0.5, 0.5)
        nt.links.new(flip.outputs[0], mad.inputs[0])
        nt.links.new(mad.outputs[0], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    if tx and src.get("cutout"):
        gt = nt.nodes.new("ShaderNodeMath")
        gt.operation = "GREATER_THAN"
        gt.inputs[1].default_value = 0.5
        nt.links.new(tx.outputs["Alpha"], gt.inputs[0])
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        mix = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(gt.outputs[0], mix.inputs[0])
        nt.links.new(tr.outputs[0], mix.inputs[1])
        nt.links.new(emit.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs["Surface"])
    else:
        nt.links.new(emit.outputs[0], out.inputs["Surface"])
    return m


def _render_to_array(path, w, h):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)


def _dilate(rgb, alpha, iters=24):
    """Edge padding: push opaque colours outward into the transparent area."""
    filled = alpha > 0.5
    out = rgb.copy()
    for _ in range(iters):
        if filled.all():
            break
        acc = np.zeros_like(out)
        cnt = np.zeros(filled.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            sf = np.roll(np.roll(filled, dy, 0), dx, 1)
            so = np.roll(np.roll(out, dy, 0), dx, 1)
            acc += so * sf[..., None]
            cnt += sf
        new = (~filled) & (cnt > 0)
        out[new] = acc[new] / cnt[new][..., None]
        filled = filled | new
    if not filled.all():
        out[~filled] = out[filled].mean(0) if filled.any() else 0.5
    return out


def bake_billboard(asset, ob, folder, tmp, view_px=512):  # noqa: C901
    """Renders front (-Y) and side (+X) orthographic views of `ob` (albedo and camera normal) into
    <asset>_card_diff.png / _nor_gl.png / _arm.jpg and returns (slot, quad layout)."""
    sc = bpy.context.scene
    lo, hi = bounds(ob)
    H = float(hi[2]) * 1.02
    Wx = float(max(abs(lo[0]), abs(hi[0]))) * 2.04
    Wy = float(max(abs(lo[1]), abs(hi[1]))) * 2.04
    W = max(Wx, Wy)
    ratio = W / max(H, 1e-3)
    if ratio < 0.75:
        vw, vh = view_px, view_px * 2      # tall: conifers, palms
    elif ratio > 1.5:
        vw, vh = view_px * 2, view_px      # wide: umbrella crowns, bushes
    else:
        vw, vh = view_px, view_px
    aspect = vw / vh
    Hr = max(H, W / aspect)
    Wr = Hr * aspect
    zlo = min(0.0, float(lo[2]))
    Hr = max(Hr, H - zlo)
    Wr = Hr * aspect
    # scene setup
    prev_engine = sc.render.engine
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.image_settings.color_depth = "8"
    sc.eevee.taa_render_samples = 16
    sc.render.filter_size = 0.6
    world = bpy.data.worlds.new("bb") if not sc.world else sc.world
    sc.world = world
    others = [o for o in sc.objects if o is not ob]
    hidden = {o: o.hide_render for o in others}
    for o in others:
        o.hide_render = True
    cam = bpy.data.objects.new("bb_cam", bpy.data.cameras.new("bb_cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(Wr, Hr)
    cam.data.clip_end = 1000
    sc.camera = cam
    orig = [s.material for s in ob.material_slots]
    views = [((0, -200, zlo + Hr / 2), (math.pi / 2, 0, 0)), ((200, 0, zlo + Hr / 2), (math.pi / 2, 0, math.pi / 2))]
    res = {}
    for mode in ("albedo", "normal"):
        mats = [_emit_material(m, mode) for m in orig]
        for s, m in zip(ob.material_slots, mats):
            s.material = m
        try:
            sc.view_settings.view_transform = "Standard"
        except TypeError:
            pass
        if mode == "normal":
            for vt in ("Raw", "Standard"):
                try:
                    sc.view_settings.view_transform = vt
                    break
                except TypeError:
                    continue
        sc.view_settings.look = "None"
        sc.view_settings.exposure = 0
        sc.view_settings.gamma = 1
        imgs = []
        for i, (loc, rot) in enumerate(views):
            cam.location = loc
            cam.rotation_euler = rot
            imgs.append(_render_to_array(os.path.join(tmp, f"{asset}_bb_{mode}_{i}.png"), vw, vh))
        res[mode] = np.concatenate(imgs, axis=1)  # side by side (rows bottom-up in Blender pixels)
        for s, m in zip(ob.material_slots, orig):
            s.material = m
        for m in mats:
            bpy.data.materials.remove(m)
    for o, h in hidden.items():
        o.hide_render = h
    bpy.data.objects.remove(cam)
    sc.render.film_transparent = False
    sc.render.filter_size = 1.5
    sc.render.engine = prev_engine
    sc.view_settings.view_transform = "Standard"
    alb, nrm = res["albedo"], res["normal"]
    a = alb[..., 3]
    # 'Raw' already gives encoded data; 'Standard' would have applied sRGB -> decode
    if sc.view_settings.view_transform != "Raw":
        pass
    rgb = _dilate(alb[..., :3], a)
    nn = _dilate(nrm[..., :3], a)
    vec = nn * 2 - 1
    vec /= np.maximum(np.linalg.norm(vec, axis=2, keepdims=True), 1e-5)
    vec[..., 2] = np.abs(vec[..., 2])
    nn = vec * 0.5 + 0.5
    slot = f"{asset}_card"
    h, w = a.shape
    _save_image(np.dstack([rgb, a]), os.path.join(folder, f"{slot}_diff.png"), True)
    _save_image(np.dstack([nn, np.ones_like(a)]), os.path.join(folder, f"{slot}_nor_gl.png"), False)
    arm = np.dstack([np.ones_like(a), np.full_like(a, 0.85), np.zeros_like(a), np.ones_like(a)])
    _save_image(arm, os.path.join(folder, f"{slot}_arm.jpg"), False, "JPEG")
    return slot, (Wr, Hr, zlo)


def _save_image(px, path, alpha, fmt="PNG"):
    h, w, _ = px.shape
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=alpha)
    im.pixels.foreach_set(px.astype(np.float32).ravel())
    im.filepath_raw = path
    im.file_format = fmt
    im.save()
    bpy.data.images.remove(im)


def billboard_mesh(name, mat, layout):
    """Two crossed quads: front (XZ plane, left half of the atlas) and side (YZ plane, right half)."""
    Wr, Hr, zlo = layout
    mb = MeshBuilder()
    w = Wr / 2
    for k in range(2):
        if k == 0:
            corners = [Vector((-w, 0, zlo)), Vector((w, 0, zlo)), Vector((w, 0, zlo + Hr)), Vector((-w, 0, zlo + Hr))]
            nrm = Vector((0, -1, 0))
        else:
            corners = [Vector((0, -w, zlo)), Vector((0, w, zlo)), Vector((0, w, zlo + Hr)), Vector((0, -w, zlo + Hr))]
            nrm = Vector((1, 0, 0))
        u0 = 0.5 * k
        uvs = [(u0, 0), (u0 + 0.5, 0), (u0 + 0.5, 1), (u0, 1)]
        idx = []
        for c, uv in zip(corners, uvs):
            n = (nrm * 0.4 + (c - Vector((0, 0, zlo + Hr * 0.55))).normalized() * 0.6).normalized()
            idx.append(mb.vert(c, n, uv, (1, 0.5, min(1, max(0, (c.z - zlo) / Hr)), 1)))
        mb.face(idx, 0)
    return mb.to_object(name, [mat])


# ------------------------------------------------------------------ preview
def preview(asset, objs, path, hdri=None):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.taa_render_samples = 48
    try:
        sc.eevee.use_shadows = True
    except AttributeError:
        pass
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Medium High Contrast" if "AgX - Medium High Contrast" in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["look"].enum_items] else "None"
    world = bpy.data.worlds.new("pv")
    sc.world = world
    world.use_nodes = True
    nt = world.node_tree
    bg = next(n for n in nt.nodes if n.type == "BACKGROUND")
    if hdri and os.path.exists(hdri):
        env = nt.nodes.new("ShaderNodeTexEnvironment")
        env.image = bpy.data.images.load(hdri, check_existing=True)
        nt.links.new(env.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = 0.8
    else:
        bg.inputs["Color"].default_value = (0.5, 0.65, 0.9, 1)
    sun = bpy.data.objects.new("pv_sun", bpy.data.lights.new("pv_sun", "SUN"))
    sun.data.energy = 4.0
    sun.data.angle = math.radians(2)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    sc.collection.objects.link(sun)
    # lay out LODs side by side
    spans = []
    for o in objs:
        lo, hi = bounds(o)
        spans.append((lo, hi))
    H = max(float(h[2]) for _, h in spans)
    Wm = max(float(max(h[0] - l[0], h[1] - l[1])) for l, h in spans)
    gap = Wm * 1.1
    for i, o in enumerate(objs):
        o.location = ((i - (len(objs) - 1) / 2) * gap, 0, 0)
    ground = bpy.data.objects.new("pv_ground", bpy.data.meshes.new("pv_ground"))
    s = gap * len(objs) * 3
    ground.data.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    gm = bpy.data.materials.new("pv_ground")
    gm.use_nodes = True
    gb = next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    gb.inputs["Base Color"].default_value = (0.09, 0.075, 0.055, 1)
    gb.inputs["Roughness"].default_value = 1
    ground.data.materials.append(gm)
    sc.collection.objects.link(ground)
    cam = bpy.data.objects.new("pv_cam", bpy.data.cameras.new("pv_cam"))
    sc.collection.objects.link(cam)
    cam.data.lens = 40
    total_w = gap * len(objs)
    rx, ry = 1500, 800
    sc.render.resolution_x, sc.render.resolution_y = rx, ry
    th = math.tan(cam.data.angle / 2)
    tv = th * ry / rx
    pitch = 0.12
    dist = max(total_w * 0.52 / th, H * 0.58 / tv)
    target = Vector((0, 0, H * 0.48))
    d = Vector((0, -1, pitch)).normalized()
    cam.location = target + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = path
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    bpy.ops.render.render(write_still=True)
    # eye-level shot of LOD0 (player height 1.7 m)
    for o in objs:
        o.location = (0, 0, 0)
        o.hide_render = o is not objs[0]
    lo, hi = spans[0]
    w0 = float(max(hi[0] - lo[0], hi[1] - lo[1]))
    sc.render.resolution_x, sc.render.resolution_y = 900, 900
    eye = 1.7 if H > 3 else max(0.4, H * 0.8)
    cam.data.lens = 22 if H > 3 else 35
    look = Vector((0, 0, H * 0.5))
    th = math.tan(cam.data.angle / 2)
    D = max(H * 0.55 / th, w0 * 0.55 / th, 1.0) * 1.05
    cam.location = Vector((D * 0.25, -D, eye))
    cam.rotation_euler = (look - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path.replace(".png", "_eye.png")
    bpy.ops.render.render(write_still=True)
    for o in objs:
        o.hide_render = False
    for o in (sun, ground, cam):
        bpy.data.objects.remove(o)
