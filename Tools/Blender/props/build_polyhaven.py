# Author : Baptiste Vu Liem
"""Game-ready conversions of Poly Haven (CC0) models into the props convention.

    python Tools/polyhaven.py model <id>        (sources -> Assets/_Game/Art/PBR/Models/<id>/)
    blender -b --factory-startup --python Tools/Blender/props/build_polyhaven.py -- [Prop_Generator ...]

Each entry imports the Poly Haven FBX, keeps the wanted objects, decimates to the budget, re-pivots,
renames material slots <Name>_<part> and rewrites the textures as <slot>_diff/_nor_gl/_arm
(ARM composed from Poly Haven's separate roughness / metal maps, AO = 1).
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pbr_lib as L  # noqa: E402


# ------------------------------------------------------------------ import helpers
def import_ph(ph_id):
    path = os.path.join(L.MODELS, ph_id, ph_id + ".fbx")
    bpy.ops.import_scene.fbx(filepath=path)
    objs = list(bpy.context.scene.objects)
    for o in objs:  # flatten hierarchy (drop rigs), keep world transforms
        if o.parent:
            mw = o.matrix_world.copy()
            o.parent = None
            o.matrix_world = mw
    for o in objs:
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in meshes:  # bake transforms into the mesh
        if o.data.users > 1:
            o.data = o.data.copy()
        o.data.transform(o.matrix_world)
        o.matrix_world = Matrix.Identity(4)
        for m in list(o.modifiers):
            o.modifiers.remove(m)
        while len(o.data.uv_layers) > 1:
            o.data.uv_layers.remove(o.data.uv_layers[-1])
        if o.data.uv_layers:
            o.data.uv_layers[0].name = "UVMap"
    return meshes


def upstream_image(sock, depth=0):
    if sock is None or not sock.is_linked or depth > 6:
        return None, None
    node = sock.links[0].from_node
    if node.type == "TEX_IMAGE":
        return node.image, sock.links[0].from_socket.name
    for inp in node.inputs:
        img, ch = upstream_image(inp, depth + 1)
        if img is not None:
            return img, ch
    return None, None


def _arr(img, w, h):
    if img is None:
        return None
    if tuple(img.size) != (w, h):
        img = img.copy()
        img.scale(w, h)
    return L.img_array(img)


def find_file(folder, prefix, kinds):
    for fn in sorted(os.listdir(folder)):
        low = fn.lower()
        if low.startswith(prefix.lower() + "_"):
            rest = low[len(prefix) + 1:]
            for k in kinds:
                if rest.startswith(k + "_"):
                    return os.path.join(folder, fn)
    return None


GLASS_TINT = {}


def convert_material(mat, slot, src_folder, out_folder, ph_id):
    """Writes <slot>_diff/_nor_gl/_arm beside the FBX from the Poly Haven maps of `mat`."""
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if mat.node_tree else None
    imgs = {}
    if bsdf:
        imgs["diff"], _ = upstream_image(bsdf.inputs["Base Color"])
        imgs["rough"], _ = upstream_image(bsdf.inputs["Roughness"])
        imgs["metal"], _ = upstream_image(bsdf.inputs["Metallic"])
        imgs["nor"], _ = upstream_image(bsdf.inputs["Normal"])
        imgs["alpha"], achan = upstream_image(bsdf.inputs["Alpha"])
    else:
        achan = None
    # fall back on Poly Haven file naming (<material>_<map>_<res>.<ext>)
    prefixes = [mat.name, ph_id]
    for key, kinds in (("diff", ("diff", "basecolor", "col")), ("rough", ("rough", "roughness")),
                       ("metal", ("metal", "metallic")), ("nor", ("nor_gl",)), ("alpha", ("alpha", "opacity"))):
        if imgs.get(key) is None:
            for pre in prefixes:
                f = find_file(src_folder, pre, kinds)
                if f:
                    imgs[key] = bpy.data.images.load(f, check_existing=True)
                    break
    for k in ("rough", "metal", "nor", "alpha"):
        if imgs.get(k) is not None:
            imgs[k].colorspace_settings.is_data = True
    base = imgs.get("diff")
    if base is not None:
        w, h = base.size
    else:
        w = h = 64
    d = _arr(base, w, h)
    if d is None:
        col = bsdf.inputs["Base Color"].default_value if bsdf else (0.5, 0.5, 0.5, 1)
        d = np.ones((h, w, 4), np.float32) * np.array([c ** (1 / 2.2) for c in col[:3]] + [1.0], np.float32)
    a = _arr(imgs.get("alpha"), w, h)
    glass = "glass" in slot.lower() or "window" in slot.lower()
    if slot in GLASS_TINT:
        d = np.ones((h, w, 4), np.float32) * np.array([c ** (1 / 2.2) for c in GLASS_TINT[slot]] + [1.0], np.float32)
    if a is not None and not glass:
        rgba = d.copy()
        rgba[..., 3] = a[..., 3] if (achan == "Alpha" and imgs.get("alpha") is base) else a[..., 0]
        L.save_array(rgba, os.path.join(out_folder, slot + "_diff.png"))
        jpg = os.path.join(out_folder, slot + "_diff.jpg")
        if os.path.exists(jpg):
            os.remove(jpg)
    else:
        L.save_array(d[..., :3], os.path.join(out_folder, slot + "_diff.jpg"))
    n = _arr(imgs.get("nor"), w, h)
    if n is None:
        n = np.ones((h, w, 3), np.float32) * np.array([0.5, 0.5, 1.0], np.float32)
    L.save_array(n[..., :3], os.path.join(out_folder, slot + "_nor_gl.jpg"), True, 95)
    r = _arr(imgs.get("rough"), w, h)
    m = _arr(imgs.get("metal"), w, h)
    rv = r[..., 0] if r is not None else np.full((h, w), bsdf.inputs["Roughness"].default_value if bsdf else 0.5,
                                                 np.float32)
    mv = m[..., 0] if m is not None else np.full((h, w), bsdf.inputs["Metallic"].default_value if bsdf else 0.0,
                                                 np.float32)
    if glass:
        rv = np.minimum(rv, 0.1)
    arm = np.stack([np.ones((h, w), np.float32), rv, mv], 2)
    L.save_array(arm, os.path.join(out_folder, slot + "_arm.jpg"), True)
    return L._export_material(slot, out_folder, glass, 0.3)


def clear_custom_normals(ob):
    L.set_active(ob)
    try:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    except Exception:
        pass


def join_objs(objs, name):
    L.set_active(objs[0])
    for o in objs[1:]:
        o.select_set(True)
    if len(objs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def bounds_of(ob):
    co = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    return co.min(0), co.max(0), co


def decimate(ob, max_tris):
    t = L.tris(ob)
    if t <= max_tris:
        return t
    m = ob.modifiers.new("dec", "DECIMATE")
    m.ratio = max_tris / t * 0.97
    m.use_collapse_triangulate = True
    m.use_symmetry = False
    L.apply_mods(ob)
    clear_custom_normals(ob)
    ob.data.shade_smooth()
    ob.data.set_sharp_from_angle(angle=math.radians(50))
    return L.tris(ob)


def slot_name(name, ph_id, mat_name, rename):
    if mat_name in rename:
        return f"{name}_{rename[mat_name]}"
    suf = mat_name
    for pre in (ph_id + "_", ph_id):
        if suf.startswith(pre):
            suf = suf[len(pre):]
    suf = suf.strip("_") or "body"
    return f"{name}_{suf}"


def convert(name, ph_id, category, keep=None, drop=(), max_tris=20000, rot_z=0.0, pivot="bottom",
            rename=None, view="prop", children=None, empties=None, extra=None, per_obj_max=None, scale=1.0,
            notes=""):
    """keep: object-name substrings to keep (None = all); drop: substrings to remove.
    children: {ChildName: substring} objects kept as separate child meshes (doors...).
    extra(objs) -> list of extra objects to add (already textured), may adjust things."""
    folder = os.path.join(L.MODELS, name)
    os.makedirs(folder, exist_ok=True)
    src = os.path.join(L.MODELS, ph_id)
    objs = import_ph(ph_id)
    for o in list(objs):
        nm = o.name
        if (keep is not None and not any(k in nm for k in keep)) or any(d in nm for d in drop):
            bpy.data.objects.remove(o)
            objs.remove(o)
    for o in objs:
        lim = (per_obj_max or {}).get(next((k for k in (per_obj_max or {}) if k in o.name), None))
        if lim:
            decimate(o, lim)
        if rot_z or scale != 1.0:
            o.data.transform(Matrix.Rotation(math.radians(rot_z), 4, "Z") @ Matrix.Diagonal((scale,) * 3 + (1,)))
    extra_objs = extra(objs) if extra else []
    child_objs = {}
    for cname, sub in (children or {}).items():
        sel = [o for o in objs if sub in o.name]
        for o in sel:
            objs.remove(o)
        if sel:
            child_objs[cname] = join_objs(sel, cname)
    ob = join_objs(objs + extra_objs, name)
    total = L.tris(ob) + sum(L.tris(c) for c in child_objs.values())
    if total > max_tris:
        decimate(ob, max_tris - sum(L.tris(c) for c in child_objs.values()))
    # pivot
    allo = [ob] + list(child_objs.values())
    mn, mx = L.world_bounds(allo)
    if callable(pivot):
        pv = Vector(pivot(ob, mn, mx))
    elif pivot == "center":
        pv = (mn + mx) / 2
    elif pivot == "top":
        pv = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mx.z))
    else:
        pv = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    for o in allo:
        o.data.transform(Matrix.Translation(-pv))
    for cname, c in child_objs.items():
        cmn, cmx, _ = bounds_of(c)
        cp = Vector(((cmn[0] + cmx[0]) / 2, (cmn[1] + cmx[1]) / 2, cmn[2]))
        c.data.transform(Matrix.Translation(-cp))
        c.parent = ob
        c.location = cp
    # materials
    rename = rename or {}
    done = {}
    for o in allo:
        for i, m in enumerate(o.data.materials):
            if m is None:
                continue
            if m.name.startswith(name + "_") and m.name in bpy.data.materials and m.get("final"):
                continue
            key = m.name
            if key not in done:
                slot = slot_name(name, ph_id, key, rename)
                done[key] = convert_material(m, slot, src, folder, ph_id)
                done[key]["final"] = True
            o.data.materials[i] = done[key]
    for o in allo:
        L.merge_slots(o)
    for en, loc in (empties or {}).items():
        L.add_empty(en, ob, Vector(loc) - pv if not callable(loc) else loc(pv))
    L.export_fbx(ob, name, folder)
    entry = L.write_part(name, category, allo, f"Poly Haven '{ph_id}' (polyhaven.com/a/{ph_id})", L.CC0_PH, notes)
    L.preview(allo, name, view)
    return entry


# ------------------------------------------------------------------ special cases
def sniper():
    """bolt_action_rifle_7_62 + a modern variable scope (procedural, baked) on rings."""
    import build_weapons as W  # noqa: F401  (spec helpers)

    def extra(objs):
        rifle = next(o for o in objs if o.name == "bolt_action_rifle_7_62")
        _, _, co = bounds_of(rifle)
        # barrel along X: find muzzle end (thinnest z extent within 4 cm of an end)
        xs = co[:, 0]
        lo, hi = xs.min(), xs.max()
        zr_lo = np.ptp(co[xs < lo + 0.04][:, 2])
        zr_hi = np.ptp(co[xs > hi - 0.04][:, 2])
        muzzle_neg = zr_lo < zr_hi  # muzzle at -X
        # rotate so the barrel points -Y
        rz = 90 if muzzle_neg else -90
        R = Matrix.Rotation(math.radians(rz), 4, "Z")
        for o in objs:
            o.data.transform(R)
        _, _, co = bounds_of(rifle)
        ys = co[:, 1]
        tip = co[ys < ys.min() + 0.01]
        bore = Vector((float(tip[:, 0].mean()), float(ys.min()), float(tip[:, 2].mean())))
        trig = next(o for o in objs if "trigger" in o.name)
        tmn, tmx, _ = bounds_of(trig)
        trig_c = Vector(((tmn + tmx) / 2).tolist())
        # receiver top near the old scope position
        sc_y = trig_c.y - 0.12
        band = co[(np.abs(co[:, 1] - sc_y) < 0.08) & (np.abs(co[:, 0]) < 0.02)]
        top = float(band[:, 2].max()) if len(band) else bore.z + 0.03
        axis_z = top + 0.045
        P = []
        cy = sc_y + 0.01
        prof = [(0.0, 0.0), (0.021, 0.0), (0.0215, 0.004), (0.0215, 0.03), (0.0205, 0.034), (0.0135, 0.075),
                (0.0127, 0.08), (0.0127, 0.24), (0.0145, 0.25), (0.025, 0.30), (0.026, 0.305), (0.026, 0.355),
                (0.0245, 0.36), (0.0, 0.36)]
        # eyepiece at +Y (rear), objective at -Y (front)
        P.append(L.lathe(prof, 40, (0, cy + 0.17, axis_z), (90, 0, 0), mat="scope"))
        P.append(L.lathe([(0.0, 0.0), (0.0225, 0.0), (0.0225, 0.001), (0.0, 0.001)], 32,
                         (0, cy - 0.19, axis_z), (90, 0, 0), mat="lens"))
        P.append(L.lathe([(0.0, 0.0), (0.0185, 0.0), (0.0185, 0.001), (0.0, 0.001)], 32,
                         (0, cy + 0.172, axis_z), (-90, 0, 0), mat="lens"))
        P.append(L.lathe([(0.0225, 0.0), (0.0228, 0.002), (0.0228, 0.022), (0.0225, 0.024)], 40,
                         (0, cy + 0.15, axis_z), (90, 0, 0), mat="rubber"))  # zoom ring
        # turrets
        P.append(L.cyl(0.016, 0.012, (0, cy + 0.0, axis_z + 0.012), mat="scope", n=24, bv=0.001))
        P.append(L.cyl(0.0125, 0.016, (0, cy + 0.0, axis_z + 0.025), mat="knurl", n=32, bv=0.001))
        P.append(L.cyl(0.0125, 0.016, (0.025, cy, axis_z), (0, 90, 0), mat="knurl", n=32, bv=0.001))
        P.append(L.cyl(0.016, 0.012, (0.012, cy, axis_z), (0, 90, 0), mat="scope", n=24, bv=0.001))
        P.append(L.cyl(0.01, 0.014, (-0.02, cy, axis_z), (0, 90, 0), mat="knurl", n=24, bv=0.001))
        # rings + bases
        for ry in (cy - 0.06, cy + 0.07):
            P.append(L.lathe([(0.0127, 0.0), (0.0165, 0.0), (0.0165, 0.016), (0.0127, 0.016)], 32,
                             (0, ry - 0.008, axis_z), (-90, 0, 0), mat="mount"))
            P.append(L.box((0.026, 0.016, axis_z - top - 0.008), (0, ry, (axis_z + top) / 2 - 0.006),
                           mat="mount", bv=0.002))
            P.append(L.box((0.03, 0.024, 0.006), (0, ry, top + 0.001), mat="mount", bv=0.0015))
            P.append(L.cyl(0.0035, 0.034, (0, ry, axis_z - 0.0165), (0, 90, 0), mat="mount", n=12, bv=0.0005))
        P.append(L.text("3-12x44", 0.004, (0.0128, cy + 0.12, axis_z), (90, 0, 90), mat="ink2"))
        specs = {"scope": W.S("gunmetal", color=(0.03, 0.03, 0.032), rough=(0.35, 0.5), scratch=0.08),
                 "knurl": W.S("polymer", grain=1.0), "rubber": W.S("rubber"),
                 "mount": W.S("blued"), "lens": L.spec(None, (0.05, 0.08, 0.12), glass=True, rough=(0.02, 0.02),
                                                     alpha=0.7),
                 "ink2": L.spec(None, (0.7, 0.7, 0.7), rough=(0.5, 0.5))}
        scope = L.join(P, "W_Sniper_scope")
        L.finish_normals(scope)
        L.bake_asset(scope, "W_Sniper", specs, 1024)
        for m in scope.data.materials:
            m["final"] = True
        sniper.grip = trig_c + Vector((0, 0.07, -0.04))
        sniper.muzzle = bore
        return [scope]

    def pv(ob, mn, mx):
        return sniper.grip

    return convert("W_Sniper", "bolt_action_rifle_7_62", "weapon", drop=("bullet", "_scope"), max_tris=15000,
                   per_obj_max={"_wrap": 900, "_bolt_a": 1000},
                   pivot=pv, view="weapon", extra=extra,
                   empties={"Grip": lambda p: Vector((0, 0, 0)), "Muzzle": lambda p: sniper.muzzle - p},
                   rename={"bolt_action_rifle_7_62": "rifle", "bolt_action_rifle_7_62_accesories": "accessories"},
                   notes="Poly Haven rifle + procedural 3-12x44 scope (W_Sniper_body textures)")


def wrench():
    def extra(objs):
        # long axis Z -> lay along Y with the jaw (widest in X) towards -Y
        allco = np.concatenate([bounds_of(o)[2] for o in objs])
        z = allco[:, 2]
        top_w = np.ptp(allco[z > z.max() - 0.04][:, 0])
        bot_w = np.ptp(allco[z < z.min() + 0.04][:, 0])
        head_up = top_w > bot_w
        R = Matrix.Rotation(math.radians(90 if head_up else -90), 4, "X")
        for o in objs:
            o.data.transform(R)
        allco = np.concatenate([bounds_of(o)[2] for o in objs])
        wrench.grip = Vector((float(allco[:, 0].mean()), float(allco[:, 1].max() - 0.06), float(allco[:, 2].mean())))
        wrench.tip = Vector((float(allco[:, 0].mean()), float(allco[:, 1].min()), float(allco[:, 2].mean())))
        return []
    return convert("W_Wrench", "adjustable_wrench", "throwable", max_tris=8000, view="weapon", extra=extra,
                   pivot=lambda ob, mn, mx: wrench.grip,
                   empties={"Grip": lambda p: Vector((0, 0, 0)), "Muzzle": lambda p: wrench.tip - p})


def fence():
    def extra(objs):
        dbl = next(o for o in objs if o.name == "modular_chainlink_fence_double")
        sgl = next(o for o in objs if o.name == "modular_chainlink_fence")
        post = next(o for o in objs if o.name == "modular_chainlink_fence_post")
        out = []
        x = 0.0
        pieces = []
        for src in (dbl, sgl):
            mn, mx, _ = bounds_of(src)
            o = src.copy()
            o.data = src.data.copy()
            bpy.context.scene.collection.objects.link(o)
            o.data.transform(Matrix.Translation(Vector((x - mn[0], -(mn[1] + mx[1]) / 2, -mn[2]))))
            pieces.append(o)
            x += mx[0] - mn[0]
        pm, px, _ = bounds_of(post)
        for k, px_ in enumerate((0.0, x - (bounds_of(sgl)[1][0] - bounds_of(sgl)[0][0]), x)):
            o = post.copy()
            o.data = post.data.copy()
            bpy.context.scene.collection.objects.link(o)
            o.data.transform(Matrix.Translation(Vector((px_ - (pm[0] + px[0]) / 2, -(pm[1] + px[1]) / 2, -pm[2]))))
            pieces.append(o)
        for o in pieces:
            o.data.transform(Matrix.Diagonal((3.0 / x, 1, 1, 1)))
        for o in (dbl, sgl, post):
            objs.remove(o)
            bpy.data.objects.remove(o)
        for o in list(objs):
            objs.remove(o)
            bpy.data.objects.remove(o)
        return pieces
    return convert("Prop_ChainlinkFence", "modular_chainlink_fence", "architecture",
                   max_tris=12000,
                   extra=extra, rename={"modular_chainlink_fence_wire": "wire_card",
                                        "modular_chainlink_fence_posts": "posts"},
                   notes="3.0 m run: double + single panels between three posts; wire slot is alpha-tested")


def fire_escape():
    """One storey of the modular fire escape: platform + railing at 4 m, stairs down to the ground."""
    def extra(objs):
        get = {o.name: o for o in objs}
        plat = get["modular_fire_escape_platform_middle"]
        rail = get["modular_fire_escape_railing_middle"]
        stairs = get["modular_fire_escape_stairs"]
        pmn, pmx, _ = bounds_of(plat)
        smn, smx, _ = bounds_of(stairs)
        stairs.data.transform(Matrix.Translation(Vector((0, 0, pmx[2] - smx[2]))))
        rmn, rmx, _ = bounds_of(rail)
        rail.data.transform(Matrix.Translation(Vector(((pmn[0] + pmx[0]) / 2 - (rmn[0] + rmx[0]) / 2,
                                                       (pmn[1] + pmx[1]) / 2 - (rmn[1] + rmx[1]) / 2,
                                                       pmx[2] - 0.03 - rmn[2]))))
        return []
    return convert("Prop_FireEscape", "modular_fire_escape", "architecture",
                   keep=("platform_middle", "railing_middle", "_stairs"), max_tris=12000, rot_z=180,
                   rename={"modular_fire_escape_01": "frame", "modular_fire_escape_02": "grating"},
                   extra=extra, notes="one storey: platform+railing on top, stairs down to the ground")


BUILDERS = {
    "W_Sniper": sniper,
    "W_Wrench": wrench,
    "W_Rock": lambda: convert("W_Rock", "stone_01", "throwable", keep=("LOD2",), max_tris=8000, pivot="center",
                              rename={"stone_01": "stone"},
                              empties={"Grip": lambda p: Vector((0, 0, 0)), "Muzzle": lambda p: Vector((0, -0.07, 0))}),
    "W_Bottle": lambda: convert("W_Bottle", "wine_bottles_01", "throwable", keep=("bordeaux",), max_tris=8000,
                                pivot="center", rename={"wine_bottles_01_glass_orange": "bottle_glass",
                                                        "wine_bottles_01_ftero_vinea_02": "label",
                                                        "wine_bottles_01_seal_cork": "seal",
                                                        "wine_bottles_01_wine_red": "wine"},
                                empties={"Grip": lambda p: Vector((0, 0, 0.1)), "Muzzle": lambda p: Vector((0, 0, -0.15))}),
    "Prop_SecurityCamera": lambda: convert("Prop_SecurityCamera", "security_camera_01", "system", max_tris=14000,
                                           rename={"security_camera_01": "body",
                                                   "security_camera_01_glass": "lens_glass"}),
    "Prop_BreakerBox": lambda: convert("Prop_BreakerBox", "power_box_01", "system", max_tris=14000,
                                       per_obj_max={"_box": 12000}, children={"Door": "_door"},
                                       rename={"power_box_01": "body"}),
    "Prop_Generator": lambda: convert("Prop_Generator", "portable_generator", "system", max_tris=20000,
                                      rename={"portable_generator": "body", "portable_generator_glass": "gauge_glass"}),
    "Prop_Jerrycan": lambda: convert("Prop_Jerrycan", "metal_jerrycan", "prop", max_tris=8000,
                                     rename={"metal_jerrycan": "body"}),
    "Prop_PropaneTank": lambda: convert("Prop_PropaneTank", "propane_tank", "prop", max_tris=8000,
                                        rename={"propane_tank": "body"}),
    "Prop_ExplosiveBarrel": lambda: convert("Prop_ExplosiveBarrel", "Barrel_01", "prop", max_tris=8000,
                                            rename={"Barrel_01": "body"}),
    "Prop_Crate_Wood": lambda: convert("Prop_Crate_Wood", "wooden_crate_02", "prop", max_tris=8000, rot_z=90,
                                       rename={"wooden_crate_02": "body"}),
    "Prop_Crate_Military": lambda: convert("Prop_Crate_Military", "wooden_military_crate", "prop", max_tris=12000,
                                           per_obj_max={"wooden_military_crate_lid": 2500,
                                                        "wooden_military_crate": 8000},
                                           rename={"wooden_military_crate": "body"}),
    "Prop_Chandelier": lambda: convert("Prop_Chandelier", "Chandelier_01", "prop", max_tris=20000, pivot="top",
                                       rename={"basecolor_classic_chandelier_1": "body",
                                               "lamps_classic_chandelier_1": "shades"}),
    "Prop_StreetLamp": lambda: convert("Prop_StreetLamp", "street_lamp_01", "architecture", max_tris=20000,
                                       rename={"street_lamp_01": "body", "street_lamp_01_glass": "lamp_glass",
                                               "street_lamp_01_bulb": "bulb"}),
    "Prop_Barrier_Concrete": lambda: convert("Prop_Barrier_Concrete", "concrete_road_barrier", "architecture",
                                             keep=("LOD2",), max_tris=8000,
                                             rename={"concrete_road_barrier": "concrete"}),
    "Prop_ChainlinkFence": fence,
    "Prop_FireEscape": fire_escape,
    "Furn_Sofa": lambda: convert("Furn_Sofa", "sofa_02", "furniture", rename={"sofa_02": "leather"}),
    "Furn_Armchair": lambda: convert("Furn_Armchair", "modern_arm_chair_01", "furniture",
                                     rename={"modern_arm_chair_01_legs": "wood",
                                             "modern_arm_chair_01_pillow": "leather"}),
    "Furn_DiningChair": lambda: convert("Furn_DiningChair", "dining_chair_02", "furniture", max_tris=10000,
                                        rename={"dining_chair_02": "body"}),
    "Furn_Bed": lambda: convert("Furn_Bed", "GothicBed_01", "furniture", rename={"GothicBed_01": "body"}),
    "Furn_Desk": lambda: convert("Furn_Desk", "metal_office_desk", "furniture",
                                 rename={"metal_office_desk": "body"}),
    "Furn_Bookshelf": lambda: convert("Furn_Bookshelf", "wooden_bookshelf_worn", "furniture",
                                      rename={"wooden_bookshelf_worn": "wood"}),
    "Furn_Cabinet": lambda: convert("Furn_Cabinet", "GothicCabinet_01", "furniture",
                                    rename={"GothicCabinet_01": "wood"}),
    "Furn_CoffeeTable": lambda: convert("Furn_CoffeeTable", "modern_coffee_table_01", "furniture",
                                        rename={"modern_coffee_table_01": "body"}),
    "Furn_Shelves_Metal": lambda: convert("Furn_Shelves_Metal", "steel_frame_shelves_01", "furniture",
                                          rename={"steel_frame_shelves_01": "body"}),
    "Furn_PottedPlant": lambda: convert("Furn_PottedPlant", "potted_plant_02", "furniture", max_tris=20000,
                                        per_obj_max={"_dirt": 2500},
                                        rename={"potted_plant_02_pot": "pot", "potted_plant_02_leaves": "leaf"}),
    "Furn_Stove": lambda: convert("Furn_Stove", "electric_stove", "furniture",
                                  rename={"electric_stove": "body", "electric_stove_glass": "oven_glass"}),
}

GLASS_TINT["W_Bottle_bottle_glass"] = (0.1, 0.16, 0.06)
GLASS_TINT["W_Bottle_wine"] = (0.09, 0.005, 0.015)

if __name__ == "__main__":
    L.run(BUILDERS, "prop")
