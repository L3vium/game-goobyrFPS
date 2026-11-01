# Author : Baptiste Vu Liem
"""Shared toolkit of the realistic prop pipeline (weapons, systemic props, furniture, vehicles).

Every asset is built in a fresh scene from parts (bevelled boxes, lathed profiles, extruded
silhouettes, swept tubes...). Each part names a material *spec* (Poly Haven texture + tint +
roughness/metal + edge wear). Two texturing modes:

  * "bake" (default): parts are joined, unwrapped once (smart project + pack) and Cycles bakes the
    procedural/triplanar look (Poly Haven textures box-projected in object space, bevel-node rounded
    edges, edge wear, AO) into one unique texture set per asset: <Name>_body_diff/_nor_gl/_arm.
  * "tile": big architectural pieces keep world-scaled tiling UVs; each material gets its own
    tinted copy of the Poly Haven maps (<Name>_<part>_diff/_nor_gl/_arm).

Slots whose spec has glass=True (name ends with _glass) get a tiny flat texture set and are left
out of the bake. Output: Assets/_Game/Art/PBR/Models/<Name>/<Name>.fbx (+ textures), an Eevee
preview PNG, and a manifest fragment (Tools/Blender/props/manifest_parts/<Name>.json) merged by
merge_manifest() into Assets/_Game/Art/PBR/Models/props_manifest.json.

Conventions: metres, Blender Z up, props face -Y (Unity +Z) with the pivot at the bottom centre;
weapons point the barrel along -Y with the pivot at the grip. FBX export: axis_forward=-Z, axis_up=Y,
baked space transform.
"""
import bpy
import bmesh
import json
import math
import os
import sys
import time
import traceback

import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PBR = os.path.join(ROOT, "Assets", "_Game", "Art", "PBR")
MODELS = os.path.join(PBR, "Models")
TEXTURES = os.path.join(PBR, "Textures")
PARTS_DIR = os.path.join(HERE, "manifest_parts")
PREVIEW_DIR = os.environ.get("GOOBYR_PREVIEW_DIR") or os.path.join(
    os.environ.get("TEMP", HERE), "goobyr_props_previews")
CC0_PH = "CC0 (Poly Haven)"


# ------------------------------------------------------------------ args / scene
def cli_names():
    """Asset names passed after '--' (empty = build everything)."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [a for a in argv if not a.startswith("-")]
    flags = {a for a in argv if a.startswith("-")}
    return names, flags


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    return sc


def gpu_cycles(sc, samples=16):
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for t in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = t
                prefs.get_devices()
                ok = False
                for d in prefs.devices:
                    d.use = d.type == t
                    ok = ok or d.use
                if ok:
                    sc.cycles.device = "GPU"
                    break
            except TypeError:
                continue
    except Exception:
        pass
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False


def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def set_active(ob):
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)


# ------------------------------------------------------------------ transforms
def rot_matrix(rot):
    rx, ry, rz = (math.radians(a) for a in rot)
    return (Matrix.Rotation(rz, 4, "Z") @ Matrix.Rotation(ry, 4, "Y") @ Matrix.Rotation(rx, 4, "X"))


def place(ob, loc=(0, 0, 0), rot=(0, 0, 0), scale=None):
    m = Matrix.Translation(Vector(loc)) @ rot_matrix(rot)
    if scale is not None:
        s = scale if isinstance(scale, (tuple, list)) else (scale, scale, scale)
        m = m @ Matrix.Diagonal((s[0], s[1], s[2], 1.0))
    ob.data.transform(m)
    ob.data.update()
    return ob


# ------------------------------------------------------------------ part creation
def _mesh_obj(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = link(bpy.data.objects.new(name, me))
    ob["part"] = mat
    return ob


def apply_mods(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    old = ob.data
    ob.modifiers.clear()
    ob.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return ob


def bevel(ob, width=0.003, seg=2, angle=35.0, apply=True):
    if width <= 0:
        return ob
    m = ob.modifiers.new("bevel", "BEVEL")
    m.width = width
    m.segments = seg
    m.limit_method = "ANGLE"
    m.angle_limit = math.radians(angle)
    m.use_clamp_overlap = True
    m.miter_outer = "MITER_ARC"
    if apply:
        apply_mods(ob)
    return ob


def subsurf(ob, levels=2, apply=True):
    m = ob.modifiers.new("subd", "SUBSURF")
    m.levels = levels
    m.render_levels = levels
    if apply:
        apply_mods(ob)
    return ob


def box(size, loc=(0, 0, 0), rot=(0, 0, 0), mat="body", bv=0.003, seg=2, name="box"):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    ob = _mesh_obj(name, bm, mat)
    bevel(ob, min(bv, min(size) * 0.45), seg)
    return place(ob, loc, rot)


def cyl(r, h, loc=(0, 0, 0), rot=(0, 0, 0), mat="body", n=24, bv=0.002, r2=None, seg=2, name="cyl",
        caps=True):
    """Cylinder (or cone with r2) along local Z, centred."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=n, radius1=r,
                          radius2=r if r2 is None else r2, depth=h)
    ob = _mesh_obj(name, bm, mat)
    bevel(ob, min(bv, h * 0.4, r * 0.4), seg)
    return place(ob, loc, rot)


def sphere(r, loc=(0, 0, 0), rot=(0, 0, 0), mat="body", seg=16, rings=10, scale=None, name="sph"):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    ob = _mesh_obj(name, bm, mat)
    return place(ob, loc, rot, scale)


def lathe(profile, n=32, loc=(0, 0, 0), rot=(0, 0, 0), mat="body", name="lathe", smooth_profile=0):
    """Surface of revolution around local Z. profile: [(radius, z), ...] from bottom to top.
    A radius of 0 at either end closes the surface with a pole."""
    pts = list(profile)
    for _ in range(smooth_profile):
        out = [pts[0]]
        for a, b in zip(pts[:-1], pts[1:]):
            out.append((a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25))
            out.append((a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75))
        out.append(pts[-1])
        pts = out
    bm = bmesh.new()
    rings = []
    for r, z in pts:
        if r < 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n), z))
                          for i in range(n)])
    for a, b in zip(rings[:-1], rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(n):
            j = (i + 1) % n
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[i], b[0], a[j]))
            else:
                bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _mesh_obj(name, bm, mat)
    return place(ob, loc, rot)


def extrude(points, thickness, loc=(0, 0, 0), rot=(0, 0, 0), mat="body", bv=0.002, seg=2, name="ext",
            plane="YZ", angle=35.0):
    """Extrude a 2D outline. plane 'YZ': points are (y, z), thickness along X (side silhouettes).
    plane 'XY': points are (x, y), thickness along Z. plane 'XZ': (x, z), thickness along Y."""
    bm = bmesh.new()
    vs = []
    for u, v in points:
        if plane == "YZ":
            co = (-thickness / 2, u, v)
        elif plane == "XY":
            co = (u, v, -thickness / 2)
        else:
            co = (u, -thickness / 2, v)
        vs.append(bm.verts.new(co))
    f = bm.faces.new(vs)
    d = {"YZ": (thickness, 0, 0), "XY": (0, 0, thickness), "XZ": (0, thickness, 0)}[plane]
    res = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=Vector(d), verts=[e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _mesh_obj(name, bm, mat)
    bevel(ob, min(bv, thickness * 0.4), seg, angle)
    return place(ob, loc, rot)


def sweep(points, radius, mat="body", n=8, smooth=True, name="sweep", caps=True, resolution=6):
    """Tube along a 3D path (bezier with auto handles when smooth)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = max(1, n // 4 - 1)
    cu.use_fill_caps = caps
    cu.resolution_u = resolution
    if smooth:
        sp = cu.splines.new("BEZIER")
        sp.bezier_points.add(len(points) - 1)
        for bp, p in zip(sp.bezier_points, points):
            bp.co = p
            bp.handle_left_type = bp.handle_right_type = "AUTO"
    else:
        sp = cu.splines.new("POLY")
        sp.points.add(len(points) - 1)
        for pp, p in zip(sp.points, points):
            pp.co = (p[0], p[1], p[2], 1.0)
    tmp = link(bpy.data.objects.new(name + "_c", cu))
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    ob = link(bpy.data.objects.new(name, me))
    ob["part"] = mat
    return ob


def dup(ob, loc=(0, 0, 0), rot=(0, 0, 0), mirror_x=False, scale=None):
    """Copy of a part, transformed (in its current space) by rot then loc."""
    me = ob.data.copy()
    o = link(bpy.data.objects.new(ob.name + "_d", me))
    o["part"] = ob["part"]
    if mirror_x:
        me.transform(Matrix.Diagonal((-1, 1, 1, 1)))
        me.flip_normals()
    place(o, loc, rot, scale)
    return o


def boolean(target, cutter, op="DIFFERENCE", keep=False):
    m = target.modifiers.new("bool", "BOOLEAN")
    m.object = cutter
    m.operation = op
    m.solver = "EXACT"
    apply_mods(target)
    if not keep:
        bpy.data.objects.remove(cutter)
    return target


def cutter_box(size, loc=(0, 0, 0), rot=(0, 0, 0)):
    return box(size, loc, rot, mat="_cut", bv=0)


def text(body, size, loc=(0, 0, 0), rot=(0, 0, 0), mat="ink", align="CENTER", depth=0.0, spacing=1.0,
         bold=False, name="txt"):
    """Flat mesh lettering in the XY plane (reads along +X, faces +Z) - labels and markings."""
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = body
    cu.size = size
    cu.align_x = align
    cu.align_y = "CENTER"
    cu.extrude = depth
    cu.resolution_u = 2
    cu.space_character = spacing
    if bold:
        cu.offset = size * 0.02
    tmp = link(bpy.data.objects.new(name + "_c", cu))
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    ob = link(bpy.data.objects.new(name, me))
    ob["part"] = mat
    return place(ob, loc, rot)


def wrap_text(ob, radius, z=0.0, angle=0.0, lift=0.0002):
    """Wrap an XY-plane text (centred on x=0, reading along +X, its v along +Y) around a vertical
    cylinder of `radius` centred on the Z axis, facing -Y rotated by `angle` degrees, at height z."""
    me = ob.data
    co = np.empty(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    r = radius + lift + co[:, 2]
    th = co[:, 0] / radius + math.radians(angle)
    out = np.stack([r * np.sin(th), -r * np.cos(th), co[:, 1] + z], 1)
    me.vertices.foreach_set("co", out.ravel())
    me.update()
    return ob


def taper_x(ob, fn):
    """Scale vertex X by fn(y, z) (e.g. stocks thinner at the wrist)."""
    me = ob.data
    co = np.empty(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    co[:, 0] *= np.array([fn(y, z) for y, z in co[:, 1:]], np.float32)
    me.vertices.foreach_set("co", co.ravel())
    me.update()
    return ob


def join(parts, name):
    parts = [p for p in parts if p is not None]
    set_active(parts[0])
    for p in parts[1:]:
        p.select_set(True)
    # keep part names as material placeholders
    for p in parts:
        key = p.get("part", "body")
        mat = bpy.data.materials.get("__" + key) or bpy.data.materials.new("__" + key)
        p.data.materials.clear()
        p.data.materials.append(mat)
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


# ------------------------------------------------------------------ normals / stats
def finish_normals(ob, angle=35.0, weighted=True):
    me = ob.data
    me.shade_smooth()
    me.set_sharp_from_angle(angle=math.radians(angle))
    t = ob.modifiers.new("tri", "TRIANGULATE")
    t.quad_method = "BEAUTY"
    t.ngon_method = "BEAUTY"
    apply_mods(ob)
    if weighted:
        m = ob.modifiers.new("wn", "WEIGHTED_NORMAL")
        m.keep_sharp = True
        m.weight = 50
        apply_mods(ob)
    ob.data.name = ob.name


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def decimate_to(ob, max_tris):
    t = tris(ob)
    if t <= max_tris:
        return t
    m = ob.modifiers.new("dec", "DECIMATE")
    m.ratio = max_tris / t * 0.98
    m.use_collapse_triangulate = True
    apply_mods(ob)
    return tris(ob)


def world_bounds(objs):
    bpy.context.view_layer.update()
    mins, maxs = [], []
    for o in objs:
        if o.type != "MESH" or not len(o.data.vertices):
            continue
        co = np.empty(len(o.data.vertices) * 3, np.float32)
        o.data.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        mw = np.array(o.matrix_world, np.float32)
        co = co @ mw[:3, :3].T + mw[:3, 3]
        mins.append(co.min(0))
        maxs.append(co.max(0))
    return Vector(np.min(mins, 0).tolist()), Vector(np.max(maxs, 0).tolist())


# ------------------------------------------------------------------ textures
_img_cache = {}
_mean_cache = {}


def tex_path(tex_id, kind):
    for res in ("1k", "2k"):
        p = os.path.join(TEXTURES, tex_id, f"{tex_id}_{kind}_{res}.jpg")
        if os.path.exists(p):
            return p
    return None


def load_image(path, data=False):
    key = (path, data)
    if key in _img_cache:
        try:
            if _img_cache[key].name in bpy.data.images:
                return _img_cache[key]
        except ReferenceError:
            pass
    img = bpy.data.images.load(path, check_existing=True)
    if data:
        img.colorspace_settings.is_data = True
    _img_cache[key] = img
    return img


def img_array(img):
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def mean_lum(tex_id):
    if tex_id in _mean_cache:
        return _mean_cache[tex_id]
    p = tex_path(tex_id, "diff")
    a = np.power(img_array(load_image(p))[..., :3], 2.2)
    v = float((a[..., 0] * 0.3 + a[..., 1] * 0.59 + a[..., 2] * 0.11).mean())
    _mean_cache[tex_id] = max(v, 0.02)
    return _mean_cache[tex_id]


def save_array(arr, path, data=False, quality=92):
    """arr: HxWx3/4 float 0..1 (already in the file's encoding). Saves JPG or PNG by extension."""
    h, w = arr.shape[:2]
    if arr.shape[2] == 3:
        arr = np.concatenate([arr, np.ones((h, w, 1), np.float32)], axis=2)
    name = os.path.basename(path)
    has_alpha = path.lower().endswith(".png") and bool((arr[..., 3] < 0.999).any())
    img = bpy.data.images.new(name, w, h, alpha=has_alpha)
    if has_alpha:
        img.alpha_mode = "STRAIGHT"
    if data:
        img.colorspace_settings.is_data = True
    img.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "JPEG" if path.lower().endswith(".jpg") else "PNG"
    sc = bpy.context.scene
    sc.render.image_settings.quality = quality
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(filepath=path, quality=quality)
    bpy.data.images.remove(img)


# ------------------------------------------------------------------ material specs
def spec(tex=None, color=(1, 1, 1), tile=0.5, mode="mul", var=0.6, rough=(0.3, 0.6), metal=0.0,
         bump=0.3, wear=0.0, wear_color=(0.55, 0.55, 0.55), noise=0.08, glass=False, alpha=1.0,
         bevel_r=0.0015, grain=0.0, rough_tex=True, scratch=0.0):
    """Material description.
    tex: Poly Haven texture id (Textures/<id>), box-projected in object space, one repeat per `tile` m.
    mode: 'mul' (texture x colour), 'tint' (texture luminance drives a variation of `color`), 'none'.
    rough: (lo, hi) remap of the texture roughness (or of noise when no texture). metal: 0..1.
    wear: edge-wear amount (bare metal `wear_color` on sharp edges). grain: fine noise bump (plastic)."""
    return dict(tex=tex, color=tuple(color), tile=tile, mode=mode, var=var, rough=rough, metal=metal,
                bump=bump, wear=wear, wear_color=tuple(wear_color), noise=noise, glass=glass, alpha=alpha,
                bevel_r=bevel_r, grain=grain, rough_tex=rough_tex, scratch=scratch)


# Library of commonly used looks (tweak per asset with dict(LIB["x"], color=...)).
LIB = {
    "gunmetal": spec("green_metal_rust", (0.05, 0.052, 0.056), 0.5, "tint", 0.12, (0.3, 0.5), 1.0, 0.03,
                     wear=0.5, wear_color=(0.3, 0.3, 0.31), scratch=0.12),
    "blued": spec("green_metal_rust", (0.06, 0.065, 0.075), 0.5, "tint", 0.12, (0.22, 0.4), 1.0, 0.03,
                  wear=0.45, wear_color=(0.35, 0.35, 0.37), scratch=0.12),
    "polymer": spec(None, (0.022, 0.022, 0.024), 0.2, "none", 0.0, (0.6, 0.78), 0.0, 0.0, noise=0.06,
                    grain=0.25, wear=0.25, wear_color=(0.07, 0.07, 0.07), scratch=0.2),
    "rubber": spec(None, (0.025, 0.025, 0.025), 0.15, "none", 0.0, (0.8, 0.95), 0.0, 0.0, noise=0.08, grain=0.35),
    "steel": spec(None, (0.55, 0.55, 0.56), 0.4, "none", 0.0, (0.25, 0.45), 1.0, 0.0, noise=0.08,
                  scratch=0.4, wear_color=(0.7, 0.7, 0.7)),
    "brushed": spec(None, (0.68, 0.68, 0.69), 0.5, "none", 0.0, (0.2, 0.32), 1.0, 0.0, noise=0.05,
                    scratch=0.6, wear_color=(0.8, 0.8, 0.8)),
    "alu": spec(None, (0.78, 0.79, 0.8), 0.6, "none", 0.0, (0.3, 0.45), 1.0, 0.0, noise=0.05, scratch=0.3,
                wear_color=(0.9, 0.9, 0.9)),
    "galvanized": spec("corrugated_iron_02", (0.6, 0.61, 0.6), 1.2, "tint", 0.6, (0.35, 0.65), 1.0, 0.0),
    "chrome": spec(None, (0.9, 0.9, 0.9), 0.5, "none", 0.0, (0.05, 0.12), 1.0, 0.0, noise=0.02),
    "brass": spec(None, (0.78, 0.6, 0.3), 0.3, "none", 0.0, (0.2, 0.4), 1.0, 0.0, noise=0.1, scratch=0.3,
                  wear_color=(0.9, 0.75, 0.45)),
    "copper": spec(None, (0.8, 0.45, 0.3), 0.3, "none", 0.0, (0.25, 0.45), 1.0, 0.0, noise=0.1, scratch=0.3),
    "paint_red": spec("green_metal_rust", (0.42, 0.035, 0.025), 0.6, "tint", 0.5, (0.35, 0.6), 0.0, 0.15,
                      wear=0.5, wear_color=(0.35, 0.33, 0.32), scratch=0.2),
    "paint_olive": spec("green_metal_rust", (0.13, 0.15, 0.08), 0.6, "tint", 0.5, (0.45, 0.7), 0.0, 0.15,
                        wear=0.4, wear_color=(0.3, 0.29, 0.27), scratch=0.15),
    "paint_grey": spec("green_metal_rust", (0.32, 0.33, 0.34), 0.6, "tint", 0.4, (0.35, 0.55), 0.0, 0.1,
                       wear=0.3, wear_color=(0.5, 0.5, 0.5), scratch=0.15),
    "paint_white": spec("green_metal_rust", (0.72, 0.72, 0.7), 0.6, "tint", 0.25, (0.3, 0.5), 0.0, 0.1,
                        wear=0.15, wear_color=(0.5, 0.5, 0.5)),
    "paint_black": spec("green_metal_rust", (0.025, 0.025, 0.027), 0.5, "tint", 0.4, (0.3, 0.55), 0.0, 0.1,
                        wear=0.3, wear_color=(0.45, 0.45, 0.45), scratch=0.2),
    "plastic_white": spec(None, (0.8, 0.8, 0.78), 0.3, "none", 0.0, (0.35, 0.5), 0.0, 0.0, noise=0.03,
                          grain=0.08),
    "plastic_black": spec(None, (0.03, 0.03, 0.032), 0.3, "none", 0.0, (0.35, 0.5), 0.0, 0.0, noise=0.04,
                          grain=0.1, scratch=0.15, wear_color=(0.1, 0.1, 0.1)),
    "plastic_grey": spec(None, (0.25, 0.25, 0.26), 0.3, "none", 0.0, (0.4, 0.55), 0.0, 0.0, noise=0.04,
                         grain=0.12),
    "ceramic": spec(None, (0.86, 0.86, 0.84), 0.3, "none", 0.0, (0.04, 0.1), 0.0, 0.0, noise=0.015),
    "walnut": spec("walnut_veneer", (0.75, 0.6, 0.5), 0.8, "mul", 0, (0.35, 0.55), 0.0, 0.15),
    "oak": spec("oak_veneer_01", (1, 1, 1), 0.8, "mul", 0, (0.4, 0.6), 0.0, 0.15),
    "darkwood": spec("dark_wood", (1, 1, 1), 1.0, "mul", 0, (0.35, 0.6), 0.0, 0.2),
    "rawwood": spec("wooden_planks", (1, 1, 1), 1.2, "mul", 0, (0.6, 0.85), 0.0, 0.5),
    "roughwood": spec("rough_wood", (1, 1, 1), 1.0, "mul", 0, (0.6, 0.9), 0.0, 0.5),
    "plywood": spec("plywood", (1, 1, 1), 1.0, "mul", 0, (0.5, 0.75), 0.0, 0.2),
    "leather_black": spec("fabric_leather_02", (0.05, 0.045, 0.04), 0.4, "tint", 0.6, (0.35, 0.6), 0.0, 0.5),
    "leather_brown": spec("brown_leather", (1, 1, 1), 0.5, "mul", 0, (0.35, 0.6), 0.0, 0.5),
    "fabric_grey": spec("wool_boucle", (0.32, 0.32, 0.33), 0.25, "tint", 0.5, (0.85, 1.0), 0.0, 0.5),
    "fabric_beige": spec("rough_linen", (0.62, 0.57, 0.48), 0.3, "tint", 0.5, (0.85, 1.0), 0.0, 0.4),
    "velvet_green": spec("velour_velvet", (0.03, 0.22, 0.08), 0.3, "tint", 0.4, (0.85, 1.0), 0.0, 0.3),
    "hessian": spec("hessian_230", (1, 1, 1), 0.3, "mul", 0, (0.85, 1.0), 0.0, 0.6),
    "marble": spec("marble_01", (1, 1, 1), 1.2, "mul", 0, (0.08, 0.2), 0.0, 0.05),
    "concrete": spec("concrete_floor_painted", (0.8, 0.8, 0.78), 1.5, "mul", 0, (0.75, 0.95), 0.0, 0.6),
    "rust": spec("rust_coarse_01", (1, 1, 1), 0.5, "mul", 0, (0.7, 0.95), 0.3, 0.5),
    "paper": spec("rough_linen", (0.82, 0.8, 0.74), 0.08, "tint", 0.15, (0.6, 0.75), 0.0, 0.1),
    "ink": spec(None, (0.02, 0.02, 0.02), 0.1, "none", 0.0, (0.4, 0.5), 0.0, 0.0, noise=0.02),
    "glass": spec(None, (0.82, 0.88, 0.9), glass=True, rough=(0.04, 0.04), alpha=0.25),
    "glass_dark": spec(None, (0.08, 0.1, 0.11), glass=True, rough=(0.03, 0.03), alpha=0.6),
    "glass_green": spec(None, (0.12, 0.35, 0.15), glass=True, rough=(0.05, 0.05), alpha=0.55),
    "glass_amber": spec(None, (0.45, 0.22, 0.05), glass=True, rough=(0.05, 0.05), alpha=0.5),
    "screen": spec(None, (0.01, 0.012, 0.015), 0.2, "none", 0.0, (0.05, 0.08), 0.0, 0.0, noise=0.0),
}


def _n(nodes, t, x=0, y=0, **kw):
    n = nodes.new(t)
    n.location = (x, y)
    for k, v in kw.items():
        setattr(n, k, v)
    return n


def build_bake_material(name, s):
    """Cycles node tree evaluating spec s in object space (no UV needed)."""
    mat = bpy.data.materials.new(name)
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = _n(nodes, "ShaderNodeOutputMaterial", 900, 0)
    bsdf = _n(nodes, "ShaderNodeBsdfPrincipled", 600, 0)
    emit = _n(nodes, "ShaderNodeEmission", 600, -400)
    emitc = _n(nodes, "ShaderNodeEmission", 600, -550)
    links.new(bsdf.outputs[0], out.inputs["Surface"])
    tc = _n(nodes, "ShaderNodeTexCoord", -1400, 0)
    mapn = _n(nodes, "ShaderNodeMapping", -1200, 0)
    mapn.inputs["Scale"].default_value = (1 / s["tile"],) * 3
    links.new(tc.outputs["Object"], mapn.inputs["Vector"])
    color = None
    rough_src = None
    height = None
    if s["tex"]:
        diff = _n(nodes, "ShaderNodeTexImage", -1000, 200)
        diff.image = load_image(tex_path(s["tex"], "diff"))
        diff.projection = "BOX"
        diff.projection_blend = 0.25
        links.new(mapn.outputs[0], diff.inputs[0])
        arm = _n(nodes, "ShaderNodeTexImage", -1000, -150)
        arm.image = load_image(tex_path(s["tex"], "arm"), data=True)
        arm.projection = "BOX"
        arm.projection_blend = 0.25
        links.new(mapn.outputs[0], arm.inputs[0])
        sep = _n(nodes, "ShaderNodeSeparateColor", -750, -150)
        links.new(arm.outputs[0], sep.inputs[0])
        rough_src = sep.outputs[1]
        bw = _n(nodes, "ShaderNodeRGBToBW", -750, 300)
        links.new(diff.outputs[0], bw.inputs[0])
        height = bw.outputs[0]
        if s["mode"] == "mul":
            mix = _n(nodes, "ShaderNodeMix", -500, 250, data_type="RGBA", blend_type="MULTIPLY")
            mix.inputs[0].default_value = 1.0
            links.new(diff.outputs[0], mix.inputs[6])
            mix.inputs[7].default_value = (*s["color"], 1)
            color = mix.outputs[2]
        elif s["mode"] == "tint":
            div = _n(nodes, "ShaderNodeMath", -600, 350, operation="DIVIDE")
            links.new(bw.outputs[0], div.inputs[0])
            div.inputs[1].default_value = mean_lum(s["tex"])
            pw = _n(nodes, "ShaderNodeMath", -450, 350, operation="POWER")
            links.new(div.outputs[0], pw.inputs[0])
            pw.inputs[1].default_value = s["var"]
            mix = _n(nodes, "ShaderNodeMix", -300, 250, data_type="RGBA", blend_type="MULTIPLY")
            mix.inputs[0].default_value = 1.0
            mix.inputs[6].default_value = (*s["color"], 1)
            links.new(pw.outputs[0], mix.inputs[7])
            color = mix.outputs[2]
        else:
            color = diff.outputs[0]
    # procedural macro noise (colour + roughness variation)
    noise = _n(nodes, "ShaderNodeTexNoise", -1000, -500)
    noise.inputs["Scale"].default_value = 6.0
    noise.inputs["Detail"].default_value = 6.0
    links.new(tc.outputs["Object"], noise.inputs["Vector"])
    nval = noise.outputs["Fac"]
    if color is None:
        mr = _n(nodes, "ShaderNodeMapRange", -600, 300)
        links.new(nval, mr.inputs[0])
        mr.inputs[1].default_value = 0.3
        mr.inputs[2].default_value = 0.7
        mr.inputs[3].default_value = 1 - s["noise"] * 2
        mr.inputs[4].default_value = 1 + s["noise"] * 2
        mix = _n(nodes, "ShaderNodeMix", -300, 250, data_type="RGBA", blend_type="MULTIPLY")
        mix.inputs[0].default_value = 1.0
        mix.inputs[6].default_value = (*s["color"], 1)
        links.new(mr.outputs[0], mix.inputs[7])
        color = mix.outputs[2]
    if rough_src is None or not s["rough_tex"]:
        rough_src = nval
    rr = _n(nodes, "ShaderNodeMapRange", -300, -150)
    links.new(rough_src, rr.inputs[0])
    if rough_src is nval:
        rr.inputs[1].default_value, rr.inputs[2].default_value = 0.3, 0.7
    rr.inputs[3].default_value, rr.inputs[4].default_value = s["rough"]
    rough = rr.outputs[0]
    metal_val = None
    # edge wear
    bev = _n(nodes, "ShaderNodeBevel", -600, -700)
    bev.samples = 8
    bev.inputs["Radius"].default_value = s["bevel_r"]
    normal_src = bev.outputs[0]
    if s["wear"] > 0:
        geo = _n(nodes, "ShaderNodeNewGeometry", -800, -900)
        dot = _n(nodes, "ShaderNodeVectorMath", -400, -800, operation="DOT_PRODUCT")
        links.new(bev.outputs[0], dot.inputs[0])
        links.new(geo.outputs["Normal"], dot.inputs[1])
        edge = _n(nodes, "ShaderNodeMapRange", -250, -800)
        links.new(dot.outputs["Value"], edge.inputs[0])
        edge.inputs[1].default_value = 0.995
        edge.inputs[2].default_value = 0.93
        wn = _n(nodes, "ShaderNodeTexNoise", -400, -1000)
        wn.inputs["Scale"].default_value = 60.0
        wn.inputs["Detail"].default_value = 8.0
        links.new(tc.outputs["Object"], wn.inputs["Vector"])
        wm = _n(nodes, "ShaderNodeMapRange", -250, -1000)
        links.new(wn.outputs["Fac"], wm.inputs[0])
        wm.inputs[1].default_value = 0.35
        wm.inputs[2].default_value = 0.65
        mul = _n(nodes, "ShaderNodeMath", -100, -900, operation="MULTIPLY")
        links.new(edge.outputs[0], mul.inputs[0])
        links.new(wm.outputs[0], mul.inputs[1])
        amt = _n(nodes, "ShaderNodeMath", 50, -900, operation="MULTIPLY", use_clamp=True)
        links.new(mul.outputs[0], amt.inputs[0])
        amt.inputs[1].default_value = s["wear"] * 1.5
        cm = _n(nodes, "ShaderNodeMix", 250, 250, data_type="RGBA")
        links.new(amt.outputs[0], cm.inputs[0])
        links.new(color, cm.inputs[6])
        cm.inputs[7].default_value = (*s["wear_color"], 1)
        color = cm.outputs[2]
        rm = _n(nodes, "ShaderNodeMix", 250, -150, data_type="FLOAT")
        links.new(amt.outputs[0], rm.inputs[0])
        links.new(rough, rm.inputs[2])
        rm.inputs[3].default_value = 0.3
        rough = rm.outputs[0]
        mm = _n(nodes, "ShaderNodeMix", 250, -300, data_type="FLOAT")
        links.new(amt.outputs[0], mm.inputs[0])
        mm.inputs[2].default_value = s["metal"]
        mm.inputs[3].default_value = 1.0 if s["wear_color"][0] > 0.2 else s["metal"]
        metal_val = mm.outputs[0]
    if s.get("scratch", 0) > 0:
        # fine random scratches: thin voronoi cell edges on a stretched, rotated mapping
        sm = _n(nodes, "ShaderNodeMapping", -1000, -1300)
        sm.inputs["Rotation"].default_value = (0.4, 0.9, 0.3)
        sm.inputs["Scale"].default_value = (90.0, 9.0, 90.0)
        links.new(tc.outputs["Object"], sm.inputs["Vector"])
        vor = _n(nodes, "ShaderNodeTexVoronoi", -800, -1300, feature="DISTANCE_TO_EDGE")
        links.new(sm.outputs[0], vor.inputs["Vector"])
        th = _n(nodes, "ShaderNodeMapRange", -600, -1300)
        links.new(vor.outputs["Distance"], th.inputs[0])
        th.inputs[1].default_value, th.inputs[2].default_value = 0.0, 0.035
        th.inputs[3].default_value, th.inputs[4].default_value = 1.0, 0.0
        sn = _n(nodes, "ShaderNodeTexNoise", -800, -1500)
        sn.inputs["Scale"].default_value = 14.0
        links.new(tc.outputs["Object"], sn.inputs["Vector"])
        sk = _n(nodes, "ShaderNodeMapRange", -600, -1500)
        links.new(sn.outputs["Fac"], sk.inputs[0])
        sk.inputs[1].default_value, sk.inputs[2].default_value = 0.45, 0.65
        smul = _n(nodes, "ShaderNodeMath", -400, -1400, operation="MULTIPLY")
        links.new(th.outputs[0], smul.inputs[0])
        links.new(sk.outputs[0], smul.inputs[1])
        samt = _n(nodes, "ShaderNodeMath", -250, -1400, operation="MULTIPLY", use_clamp=True)
        links.new(smul.outputs[0], samt.inputs[0])
        samt.inputs[1].default_value = s["scratch"]
        sc_c = _n(nodes, "ShaderNodeMix", 350, 250, data_type="RGBA")
        links.new(samt.outputs[0], sc_c.inputs[0])
        links.new(color, sc_c.inputs[6])
        sc_c.inputs[7].default_value = (*s["wear_color"], 1)
        color = sc_c.outputs[2]
        sc_r = _n(nodes, "ShaderNodeMix", 350, -150, data_type="FLOAT")
        links.new(samt.outputs[0], sc_r.inputs[0])
        links.new(rough, sc_r.inputs[2])
        sc_r.inputs[3].default_value = max(s["rough"][0] - 0.1, 0.15)
        rough = sc_r.outputs[0]
    # bump: texture luminance + fine grain
    nrm = normal_src
    if height is not None and s["bump"] > 0:
        b = _n(nodes, "ShaderNodeBump", 300, -550)
        b.inputs["Strength"].default_value = s["bump"]
        b.inputs["Distance"].default_value = 0.002 * s["tile"]
        links.new(height, b.inputs["Height"])
        links.new(nrm, b.inputs["Normal"])
        nrm = b.outputs[0]
    if s["grain"] > 0:
        gn = _n(nodes, "ShaderNodeTexNoise", 100, -1200)
        gn.inputs["Scale"].default_value = 900.0
        gn.inputs["Detail"].default_value = 2.0
        links.new(tc.outputs["Object"], gn.inputs["Vector"])
        b2 = _n(nodes, "ShaderNodeBump", 450, -700)
        b2.inputs["Strength"].default_value = s["grain"]
        b2.inputs["Distance"].default_value = 0.0004
        links.new(gn.outputs["Fac"], b2.inputs["Height"])
        links.new(nrm, b2.inputs["Normal"])
        nrm = b2.outputs[0]
    links.new(color, bsdf.inputs["Base Color"])
    links.new(color, emitc.inputs["Color"])
    links.new(rough, bsdf.inputs["Roughness"])
    if metal_val is not None:
        links.new(metal_val, bsdf.inputs["Metallic"])
        links.new(metal_val, emit.inputs["Color"])
    else:
        bsdf.inputs["Metallic"].default_value = s["metal"]
        emit.inputs["Color"].default_value = (s["metal"],) * 3 + (1,)
    links.new(nrm, bsdf.inputs["Normal"])
    mat["bsdf"] = bsdf.name
    mat["emit"] = emit.name
    mat["emitc"] = emitc.name
    mat["out"] = out.name
    return mat


# ------------------------------------------------------------------ UVs
def smart_uv(ob, layer="UVMap", margin=0.004, angle=60.0):
    me = ob.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name=layer)
    me.uv_layers.active = uv
    set_active(ob)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin, area_weight=0.0,
                             scale_to_bounds=False)
    try:
        bpy.ops.uv.pack_islands(rotate=True, margin=margin, shape_method="CONCAVE")
    except TypeError:
        bpy.ops.uv.pack_islands(margin=margin)
    bpy.ops.object.mode_set(mode="OBJECT")
    return uv


def world_scale_uv(ob, tile):
    """Rescale the (packed) UVs so one UV unit = `tile` metres."""
    me = ob.data
    uv = me.uv_layers.active.data
    a3 = auv = 0.0
    for p in me.polygons:
        a3 += p.area
        pts = [uv[i].uv for i in p.loop_indices]
        s = 0.0
        for i in range(1, len(pts) - 1):
            s += abs((pts[i] - pts[0]).cross(pts[i + 1] - pts[0])) * 0.5
        auv += s
    if auv <= 0:
        return
    k = math.sqrt(a3 / auv) / tile
    arr = np.empty(len(uv) * 2, np.float32)
    uv.foreach_get("uv", arr)
    uv.foreach_set("uv", arr * k)


# ------------------------------------------------------------------ export materials
def _export_material(slot, folder, glass=False, alpha=1.0):
    mat = bpy.data.materials.get(slot) or bpy.data.materials.new(slot)
    nt = mat.node_tree
    nt.nodes.clear()
    out = _n(nt.nodes, "ShaderNodeOutputMaterial", 600, 0)
    bsdf = _n(nt.nodes, "ShaderNodeBsdfPrincipled", 300, 0)
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    d = _n(nt.nodes, "ShaderNodeTexImage", -300, 300)
    dp = os.path.join(folder, slot + "_diff.jpg")
    if not os.path.exists(dp):
        dp = os.path.join(folder, slot + "_diff.png")
    d.image = bpy.data.images.load(dp)
    nt.links.new(d.outputs[0], bsdf.inputs["Base Color"])
    if dp.endswith(".png"):
        nt.links.new(d.outputs["Alpha"], bsdf.inputs["Alpha"])
        try:
            mat.surface_render_method = "DITHERED"
        except Exception:
            pass
    a = _n(nt.nodes, "ShaderNodeTexImage", -300, 0)
    a.image = bpy.data.images.load(os.path.join(folder, slot + "_arm.jpg"))
    a.image.colorspace_settings.is_data = True
    sep = _n(nt.nodes, "ShaderNodeSeparateColor", 0, 0)
    nt.links.new(a.outputs[0], sep.inputs[0])
    nt.links.new(sep.outputs[1], bsdf.inputs["Roughness"])
    nt.links.new(sep.outputs[2], bsdf.inputs["Metallic"])
    n = _n(nt.nodes, "ShaderNodeTexImage", -300, -300)
    n.image = bpy.data.images.load(os.path.join(folder, slot + "_nor_gl.jpg"))
    n.image.colorspace_settings.is_data = True
    nm = _n(nt.nodes, "ShaderNodeNormalMap", 0, -300)
    nt.links.new(n.outputs[0], nm.inputs["Color"])
    nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    if glass:
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Transmission Weight"].default_value = 0.0
        try:
            mat.surface_render_method = "BLENDED"
        except Exception:
            pass
    return mat


def write_flat_set(slot, folder, color, rough, metal=0.0, size=64):
    c = np.ones((size, size, 3), np.float32) * np.array(color, np.float32) ** (1 / 2.2)
    save_array(c, os.path.join(folder, slot + "_diff.jpg"))
    n = np.ones((size, size, 3), np.float32) * np.array([0.5, 0.5, 1.0], np.float32)
    save_array(n, os.path.join(folder, slot + "_nor_gl.jpg"), data=True)
    a = np.ones((size, size, 3), np.float32) * np.array([1.0, rough, metal], np.float32)
    save_array(a, os.path.join(folder, slot + "_arm.jpg"), data=True)


# ------------------------------------------------------------------ bake
def _bake(ob, kind, img, mats, samples, **kw):
    for m in mats:
        nt = m.node_tree
        node = nt.nodes.get("__bake") or _n(nt.nodes, "ShaderNodeTexImage", 900, 400)
        node.name = "__bake"
        node.image = img
        nt.nodes.active = node
    bpy.context.scene.cycles.samples = samples
    set_active(ob)
    try:
        bpy.ops.object.bake(type=kind, margin=8, use_clear=True, **kw)
    except RuntimeError as e:  # GPU busy / context lost: fall back to the CPU
        print("BAKE GPU FAILED, retrying on CPU:", e)
        bpy.context.scene.cycles.device = "CPU"
        bpy.ops.object.bake(type=kind, margin=8, use_clear=True, **kw)


def bake_asset(ob, name, specs, size=1024, ao_strength=0.55, folder=None):
    """ob: joined mesh whose placeholder materials are '__<part>'. Returns final slot list."""
    folder = folder or os.path.join(MODELS, name)
    os.makedirs(folder, exist_ok=True)
    sc = bpy.context.scene
    gpu_cycles(sc)
    me = ob.data
    smart_uv(ob)
    # map placeholder slots -> bake materials / glass slots
    body_slot = f"{name}_body"
    baked, slot_names = [], []
    for i, m in enumerate(me.materials):
        key = m.name[2:]
        s = specs[key]
        if s["glass"]:
            slot = f"{name}_{key}" if key.endswith("glass") else f"{name}_{key}_glass"
            slot_names.append((i, slot, s))
            bm_ = bpy.data.materials.new("__tmp_" + key)
            me.materials[i] = bm_
        else:
            bm_ = build_bake_material("__bake_" + key, s)
            me.materials[i] = bm_
            baked.append(bm_)
            slot_names.append((i, body_slot, s))
    if baked:
        imgs = {}
        for kind in ("diff", "nor", "rough", "metal", "ao"):
            imgs[kind] = bpy.data.images.new(f"{name}_{kind}", size, size, alpha=False)
            if kind != "diff":
                imgs[kind].colorspace_settings.is_data = True
        # glass faces bake as neutral; give the placeholder a principled so bake succeeds
        for i, slot, s in slot_names:
            if s["glass"]:
                gm = me.materials[i]
                baked.append(gm)
        _bake(ob, "NORMAL", imgs["nor"], baked, 4, normal_space="TANGENT")
        _bake(ob, "ROUGHNESS", imgs["rough"], baked, 1)
        _bake(ob, "AO", imgs["ao"], baked, 48)
        for key, img in (("emitc", imgs["diff"]), ("emit", imgs["metal"])):  # albedo + metallic via emission
            for m in baked:
                if key in m:
                    nt = m.node_tree
                    nt.links.new(nt.nodes[m[key]].outputs[0], nt.nodes[m["out"]].inputs["Surface"])
            _bake(ob, "EMIT", img, baked, 1)
        diff = img_array(imgs["diff"])[..., :3]
        ao = img_array(imgs["ao"])[..., 0:1]
        ao_soft = 1.0 - ao_strength * (1.0 - ao)
        save_array(diff * ao_soft, os.path.join(folder, body_slot + "_diff.jpg"))
        save_array(img_array(imgs["nor"])[..., :3], os.path.join(folder, body_slot + "_nor_gl.jpg"), True, 95)
        arm = np.concatenate([ao, img_array(imgs["rough"])[..., 0:1], img_array(imgs["metal"])[..., 0:1]], 2)
        save_array(arm, os.path.join(folder, body_slot + "_arm.jpg"), True)
    # final materials, collapse duplicate slots
    final = {}
    for i, slot, s in slot_names:
        if slot not in final:
            if s["glass"]:
                write_flat_set(slot, folder, s["color"], s["rough"][0])
                final[slot] = _export_material(slot, folder, True, s["alpha"])
            else:
                final[slot] = _export_material(slot, folder)
        me.materials[i] = final[slot]
    merge_slots(ob)
    return list(final.keys())


def assign_part(ob, faces_fn, part):
    """Move the faces selected by faces_fn(center, normal) to placeholder material '__<part>'."""
    me = ob.data
    mat = bpy.data.materials.get("__" + part) or bpy.data.materials.new("__" + part)
    if mat.name not in [m.name for m in me.materials]:
        me.materials.append(mat)
    idx = [m.name for m in me.materials].index(mat.name)
    for p in me.polygons:
        if faces_fn(p.center, p.normal):
            p.material_index = idx


def merge_slots(ob):
    me = ob.data
    names = [m.name for m in me.materials]
    uniq = []
    for n in names:
        if n not in uniq:
            uniq.append(n)
    remap = [uniq.index(n) for n in names]
    idx = np.empty(len(me.polygons), np.int32)
    me.polygons.foreach_get("material_index", idx)
    idx = np.array(remap, np.int32)[idx]
    mats = [bpy.data.materials[n] for n in uniq]
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    me.polygons.foreach_set("material_index", idx)
    me.update()


def tile_asset(ob, name, specs, folder=None, res=1024):
    """Tiling mode: per-material processed copies of the Poly Haven maps and world-scaled UVs."""
    folder = folder or os.path.join(MODELS, name)
    os.makedirs(folder, exist_ok=True)
    me = ob.data
    smart_uv(ob, margin=0.002, angle=50.0)
    # per material scale: split by material tile size (use the dominant tile of each face)
    uvl = me.uv_layers.active.data
    keys = [m.name[2:] for m in me.materials]
    # world-scale per material group
    for i, key in enumerate(keys):
        s = specs[key]
        faces = [p for p in me.polygons if p.material_index == i]
        a3 = auv = 0.0
        for p in faces:
            a3 += p.area
            pts = [uvl[j].uv for j in p.loop_indices]
            for k in range(1, len(pts) - 1):
                auv += abs((pts[k] - pts[0]).cross(pts[k + 1] - pts[0])) * 0.5
        if auv <= 0:
            continue
        f = math.sqrt(a3 / auv) / s["tile"]
        for p in faces:
            for j in p.loop_indices:
                uvl[j].uv = uvl[j].uv * f
    final = []
    for i, key in enumerate(keys):
        s = specs[key]
        slot = f"{name}_{key}"
        if s["glass"] and "glass" not in slot:
            slot += "_glass"
        if s["glass"] or not s["tex"]:
            write_flat_set(slot, folder, s["color"], (s["rough"][0] + s["rough"][1]) / 2, s["metal"])
        else:
            d = img_array(load_image(tex_path(s["tex"], "diff")))[..., :3]
            if s["mode"] == "tint":
                dl = np.power(d, 2.2)
                lum = (dl[..., 0] * 0.3 + dl[..., 1] * 0.59 + dl[..., 2] * 0.11)[..., None]
                lin = np.array(s["color"], np.float32)
                d = (lin * np.power(lum / mean_lum(s["tex"]), s["var"])) ** (1 / 2.2)
            elif s["mode"] == "mul":
                d = d * np.array(s["color"], np.float32) ** (1 / 2.2)
            save_array(d, os.path.join(folder, slot + "_diff.jpg"))
            nimg = img_array(load_image(tex_path(s["tex"], "nor_gl"), data=True))[..., :3]
            nimg[..., :2] = (nimg[..., :2] - 0.5) * max(s["bump"] * 2.0, 0.2) + 0.5
            save_array(nimg, os.path.join(folder, slot + "_nor_gl.jpg"), True, 95)
            a = img_array(load_image(tex_path(s["tex"], "arm"), data=True))[..., :3].copy()
            lo, hi = s["rough"]
            a[..., 1] = lo + (hi - lo) * a[..., 1]
            a[..., 2] = s["metal"]
            save_array(a, os.path.join(folder, slot + "_arm.jpg"), True)
        me.materials[i] = _export_material(slot, folder, s["glass"], s["alpha"])
        final.append(slot)
    merge_slots(ob)
    return sorted(set(final))


# ------------------------------------------------------------------ export / preview / manifest
def add_empty(name, parent, loc):
    e = link(bpy.data.objects.new(name, None))
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.03
    e.parent = parent
    e.location = loc
    return e


def export_fbx(root, name, folder=None):
    folder = folder or os.path.join(MODELS, name)
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".fbx")
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    stack = [root]
    while stack:
        o = stack.pop()
        o.select_set(True)
        stack += list(o.children)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=True,
                             add_leaf_bones=False, path_mode="STRIP", embed_textures=False,
                             use_custom_props=False, bake_anim=False)
    return path


def write_part(name, category, objs, source, licence, notes=""):
    mn, mx = world_bounds(objs)
    size = mx - mn
    t = sum(tris(o) for o in objs if o.type == "MESH")
    entry = dict(name=name, category=category,
                 size_m=[round(size.x, 3), round(size.z, 3), round(size.y, 3)],
                 tris=t, source=source, licence=licence)
    if notes:
        entry["notes"] = notes
    os.makedirs(PARTS_DIR, exist_ok=True)
    with open(os.path.join(PARTS_DIR, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(entry, f, indent=1)
    return entry


def merge_manifest():
    out = []
    for fn in sorted(os.listdir(PARTS_DIR)):
        if fn.endswith(".json"):
            with open(os.path.join(PARTS_DIR, fn), encoding="utf-8") as f:
                out.append(json.load(f))
    order = ["weapon", "throwable", "system", "architecture", "prop", "furniture", "vehicle"]
    out.sort(key=lambda e: (order.index(e["category"]) if e["category"] in order else 99, e["name"]))
    path = os.path.join(MODELS, "props_manifest.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    return path


def preview(objs, name, view="prop", res=640):
    """Eevee studio render of the exported materials."""
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = 32
        sc.eevee.use_raytracing = True
    except Exception:
        pass
    sc.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
    world = bpy.data.worlds.new("studio")
    sc.world = world
    wn = world.node_tree.nodes
    bg = next(n for n in wn if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.42, 0.44, 0.47, 1)
    bg.inputs["Strength"].default_value = 0.25
    mn, mx = world_bounds([o for o in objs if o.type == "MESH"])
    c = (mn + mx) / 2
    r = max((mx - mn).length / 2, 0.01)
    # floor
    if view != "weapon":
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=r * 8)
        fl = _mesh_obj("__floor", bm, "floor")
        fl.location = (c.x, c.y, mn.z)
        fm = bpy.data.materials.new("__floor")
        b = next(n for n in fm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = (0.18, 0.18, 0.19, 1)
        b.inputs["Roughness"].default_value = 0.8
        fl.data.materials.append(fm)
    cam_d = bpy.data.cameras.new("cam")
    cam_d.lens = 70
    cam = link(bpy.data.objects.new("cam", cam_d))
    sc.camera = cam
    if view == "weapon":
        d = Vector((1.0, -0.25, 0.22)).normalized()
    elif view == "top":
        d = Vector((-0.6, -1.0, 1.1)).normalized()
    else:
        d = Vector((-0.75, -1.0, 0.55)).normalized()
    fov = 2 * math.atan(18 / cam_d.lens)
    dist = r / math.sin(fov / 2) * 1.05
    cam.location = c + d * dist
    print("PREVIEW", name, "r", r, "dist", dist, "c", tuple(c))
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam_d.clip_end = dist * 10
    cam_d.clip_start = dist * 0.01
    for i, (vec, en, size) in enumerate((((-1.0, -1.2, 1.4), 65, 1.5), ((1.4, -0.4, 0.6), 20, 2.0),
                                          ((0.3, 1.5, 1.2), 55, 1.0))):
        ld = bpy.data.lights.new(f"l{i}", "AREA")
        ld.size = r * 2.5 * size
        ld.energy = en * (r * 2) ** 2
        lo = link(bpy.data.objects.new(f"l{i}", ld))
        v = Vector(vec).normalized()
        lo.location = c + v * r * 4
        lo.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    sc.render.filepath = os.path.join(PREVIEW_DIR, name + ".png")
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    return sc.render.filepath


# ------------------------------------------------------------------ driver
def run(builders, default_category):
    """builders: {Name: (fn, category or None)}; fn() -> dict(objs, root, specs, ...) handled inside."""
    names, flags = cli_names()
    todo = names or list(builders.keys())
    report = []
    for nm in todo:
        if nm not in builders:
            print("UNKNOWN", nm)
            continue
        t0 = time.time()
        try:
            reset()
            entry = builders[nm]()
            report.append((nm, entry.get("tris"), round(time.time() - t0, 1)))
            print(f"BUILT {nm}: {entry.get('tris')} tris, {entry.get('size_m')} m, {time.time() - t0:.1f}s")
        except Exception:
            traceback.print_exc()
            report.append((nm, "FAILED", 0))
            print("FAILED", nm)
    merge_manifest()
    print("REPORT", report)


def finalize(name, parts, specs, category, source="Procedural (Blender Python)", licence="Original, project-owned",
             mode="bake", size=1024, view="prop", empties=None, children=None, max_tris=None, smooth_angle=35.0,
             pivot=None, notes="", post=None):
    """Join parts, normals, texture (bake/tile), empties, export, preview, manifest.
    children: {ChildName: (list_of_parts, pivot_xyz)} -> separate meshes parented to the root
    (e.g. wardrobe doors). empties: {Name: (x,y,z)}."""
    folder = os.path.join(MODELS, name)
    groups = [(name, parts, None)]
    for cname, (cparts, cpivot) in (children or {}).items():
        groups.append((cname, cparts, cpivot))
    # tag faces with group index then join everything for a shared atlas
    allp = []
    for gi, (_, gparts, _) in enumerate(groups):
        for p in gparts:
            if p is None:
                continue
            attr = p.data.attributes.new("grp", "INT", "FACE")
            attr.data.foreach_set("value", [gi] * len(p.data.polygons))
            allp.append(p)
    ob = join(allp, name)
    if pivot is not None:
        ob.data.transform(Matrix.Translation(-Vector(pivot)))
    if post:
        post(ob)
    if max_tris:
        decimate_to(ob, max_tris)
    finish_normals(ob, smooth_angle)
    if mode == "bake":
        bake_asset(ob, name, specs, size, folder=folder)
    else:
        tile_asset(ob, name, specs, folder=folder)
    # split children out
    objs = [ob]
    if children:
        gvals = np.empty(len(ob.data.polygons), np.int32)
        ob.data.attributes["grp"].data.foreach_get("value", gvals)
        for gi in range(1, len(groups)):
            cname, _, cpivot = groups[gi]
            set_active(ob)
            bpy.ops.object.mode_set(mode="EDIT")
            bm = bmesh.from_edit_mesh(ob.data)
            lay = bm.faces.layers.int.get("grp")
            for f in bm.faces:
                f.select_set(f[lay] == gi)
            bmesh.update_edit_mesh(ob.data)
            bpy.ops.mesh.separate(type="SELECTED")
            bpy.ops.object.mode_set(mode="OBJECT")
            child = [o for o in bpy.context.selected_objects if o is not ob][0]
            child.name = cname
            child.data.name = cname
            pv = Vector(cpivot) - (Vector(pivot) if pivot else Vector())
            child.data.transform(Matrix.Translation(-pv))
            child.parent = ob
            child.location = pv
            objs.append(child)
            # drop unused material slots
            used = sorted(set(p.material_index for p in child.data.polygons))
            if len(used) < len(child.data.materials):
                mats = [child.data.materials[i] for i in used]
                idx = np.empty(len(child.data.polygons), np.int32)
                child.data.polygons.foreach_get("material_index", idx)
                remap = {u: k for k, u in enumerate(used)}
                idx = np.array([remap[i] for i in idx], np.int32)
                child.data.materials.clear()
                for m in mats:
                    child.data.materials.append(m)
                child.data.polygons.foreach_set("material_index", idx)
    for o in objs:
        if "grp" in o.data.attributes:
            o.data.attributes.remove(o.data.attributes["grp"])
    for en, loc in (empties or {}).items():
        add_empty(en, ob, loc)
    export_fbx(ob, name, folder)
    entry = write_part(name, category, objs, source, licence, notes)
    preview(objs, name, view)
    return entry
