# Author : Baptiste Vu Liem
"""Cast generator entry point (headless Blender 5.2).

  blender -b --factory-startup --python Tools/Blender/characters/cast/cast_build.py -- Chr_Rook [Chr_Rook_F ...] [--render] [--face] [--beauty]
  blender -b --factory-startup --python Tools/Blender/characters/cast/cast_build.py -- all --render

Builds each character from cast_specs.CAST (SDF parts -> meshes, buried faces culled against every
other opaque part, looks assigned), saves Tools/Blender/out/cast/work/<Name>.blend and renders the
design-sheet views into Tools/Blender/out/cast/renders/ (sheet.py assembles them).
"""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
import numpy as np  # noqa: E402

import assemble as A  # noqa: E402
import blmesh  # noqa: E402
import body  # noqa: E402
import garments as G  # noqa: E402
import garments2 as G2  # noqa: E402
import inspect  # noqa: E402
import hair as H  # noqa: E402
import head  # noqa: E402
import materials as M  # noqa: E402
import render as R  # noqa: E402
from cast_specs import CAST  # noqa: E402
from cast_specs_cast import ORDER  # noqa: E402  (merges the rest of the cast into CAST)

PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT = os.path.join(PROJECT, "Tools", "Blender", "out", "cast")
TRANSPARENT = {"Lens"}
log = blmesh.log


def look_material(char, slot, look, spec):
    name = "%s_%s" % (char, slot)
    kind = look["kind"]
    if kind == "fabric":
        return M.get(name, M.fabric, look["colour"], look["ph"], look["scale"], look["rough"], look["sheen"], look["bump"],
                     look["contrast"], coat=look.get("coat", 0.0), metal=look.get("metal", 0.0))
    if kind == "camo":
        return M.get(name, M.fabric, look["colours"][0], look["ph"], 3.0, look["rough"], 0.3, 0.25, 0.45,
                     pattern=M.camo(look["colours"], look["cscale"], look["camo"], look["seed"]))
    if kind == "pattern":
        pk, a, b, size = look["pattern"]
        pat = M.checks(a, b, size) if pk == "checks" else M.stripes(a, b, size)
        return M.get(name, M.fabric, look["colour"], look["ph"], look.get("scale", 3.0), look.get("rough", 0.8), look.get("sheen", 0.3),
                     look.get("bump", 0.25), look.get("contrast", 0.5), pattern=pat)
    if kind == "plain":
        return M.get(name, M.plain, look["colour"], look["rough"], look["metal"], look["coat"], look["noise"], look["bump"],
                     look["scale"], look["sheen"])
    if kind == "glass":
        return M.get(name, M.glass, look["colour"])
    raise KeyError(kind)


def make_parts(name, spec, head_only=False):
    B = body.BodyShape(spec["body"])
    f = head.spec(**spec["face"])
    parts = []
    hp = head.head_part(f)
    parts.append(hp)
    hair_p = None
    if spec.get("hair"):
        hair_p = H.STYLES[spec["hair"]](f, **spec.get("hair_kw", {}))
        parts.append(hair_p)
    parts += head.lid_parts(f)
    parts.append(head.brow_part(f, hp))
    if spec.get("beard"):
        parts += H.BEARDS[spec["beard"]](f, hp)
    if head_only:
        if any(g[0] == "shades_on_head" for g in spec["outfit"]):
            kw = next(g[1] for g in spec["outfit"] if g[0] == "shades_on_head")
            parts += G.shades_on_head(B, hair_p if hair_p is not None else hp, **kw)
        return B, f, parts
    parts.append(B.skin_part())
    outfit = spec["outfit"]
    if not any(g[0] == "gloves" for g in outfit):
        parts += G.bare_hands(B)
    built = {}
    for gname, kw in outfit:
        fn = getattr(G, gname, None) or getattr(G2, gname)
        params = inspect.signature(fn).parameters
        kw = dict(kw)
        ref = kw.pop("ref", None)
        if gname == "shades_on_head":
            new = fn(B, hair_p if hair_p is not None else hp, **kw)
        elif "f" in params:
            new = fn(B, f, **kw)
        elif "shirt_part" in params or "ref_part" in params:
            key = "shirt_part" if "shirt_part" in params else "ref_part"
            kw[key] = built.get(ref or "shirt", [None])[0]
            new = fn(B, **kw)
        else:
            new = fn(B, **kw)
        built[gname] = new
        parts += new
    return B, f, parts


def build(name, coll=None, head_only=False):
    spec = CAST[name]
    t0 = time.time()
    B, f, parts = make_parts(name, spec, head_only)
    coll = coll or A.collection(name)
    # 1) mesh everything
    meshes = {}
    for p in parts:
        t = time.time()
        meshes[p.name] = p.mesh()
        log("mesh %-22s %7d verts %5.1fs" % (p.name, len(meshes[p.name][0]), time.time() - t))
    # 2) cull faces buried in any other opaque part
    bounds = {p.name: p.bounds() for p in parts}
    obs = {}
    for p in parts:
        P, faces, N = meshes[p.name]
        if len(P) == 0:
            continue
        lo, hi = P.min(0), P.max(0)
        occ = [o for o in parts if o is not p and o.material not in TRANSPARENT
               and np.all(bounds[o.name][0] <= hi) and np.all(bounds[o.name][1] >= lo)]
        if occ:
            inside = blmesh.inside_mask(P, occ, 0.0015)
            masks = [~np.all(inside[fc], axis=1) for fc in faces]
            P, faces, N = blmesh.compact(P, faces, N, masks)
        if len(P) == 0:
            continue
        ob = blmesh.new_object(p.name, P, faces, N, coll)
        ob["cast_material"] = p.material
        classify = p.meta.get("classify")
        if classify is not None:
            me = ob.data
            nf = len(me.polygons)
            cen = np.empty(nf * 3, np.float32)
            me.polygons.foreach_get("center", cen)
            nrm = np.empty(nf * 3, np.float32)
            me.polygons.foreach_get("normal", nrm)
            me.polygons.foreach_set("material_index", np.asarray(classify(cen.reshape(-1, 3), nrm.reshape(-1, 3)), np.int32))
        obs[p.name] = (ob, p)
    # 3) looks
    skin = M.get(name + "_Skin", M.skin, f["skin"])
    lash = M.get(name + "_Lashes", M.plain, (0.05, 0.035, 0.03), 0.6)
    hp_meta = parts[1].meta if spec.get("hair") else {}
    hair_m = M.get(name + "_Hair", M.hair, spec.get("hair_colour", (0.1, 0.07, 0.05)), hp_meta.get("rough", 0.48),
                   flow=hp_meta.get("flow", (1, 1, 1)), sheen=0.18 if hp_meta.get("rough", 0.48) < 0.7 else 0.05)
    brow_m = M.get(name + "_Brows", M.hair, spec.get("brow_colour", spec.get("hair_colour", (0.1, 0.07, 0.05))), 0.62, sheen=0.06, roots=(1.60, 1.70))
    looks = spec["looks"]
    for pname, (ob, p) in obs.items():
        mat = p.material
        if mat == "Skin":
            A.assign(ob, skin)
            co = A.verts(ob)
            if pname == "Head":
                A.set_colours(ob, head.skin_colours(co, f))
            elif pname.startswith("Lid"):
                A.set_colours(ob, np.ones((len(co), 3)) * np.array([0.97, 0.88, 0.85]))
            else:
                A.set_colours(ob, np.ones((len(co), 3)))
        elif mat == "Lashes":
            A.assign(ob, lash)
        elif mat == "Hair":
            A.assign(ob, hair_m)
        elif mat == "Brows":
            A.assign(ob, brow_m)
        else:
            A.assign(ob, look_material(name, mat, looks[mat], spec))
            for extra in p.meta.get("slots", []):
                ob.data.materials.append(look_material(name, extra, looks[extra], spec))
    # eyes
    eye_m = M.get(name + "_Eyes", M.eye, f["iris"])
    for c in head.eye_centres(f):
        e = A.eyeball("Eye_" + ("l" if c[0] > 0 else "r"), c, f["eye_r"], side=1 if c[0] > 0 else -1, coll=coll)
        A.assign(e, eye_m)
    log("BUILT", name, "in %.1fs" % (time.time() - t0), "objects", len(coll.objects))
    return B, f, coll


def silhouette(renders):
    """Grey 1.80 m scale figure (average base, arms down) for the sheets."""
    import skeleton
    skeleton.ARM_ANGLE = 80.0
    B = body.BodyShape("average")
    f = head.spec()
    coll = A.collection("Silhouette")
    parts = [head.head_part(f), B.skin_part(0.004)] + G.bare_hands(B)
    grey = M.get("SilGrey", M.plain, (0.55, 0.56, 0.58), 0.8)
    for p in parts:
        ob = blmesh.build_part(p, collection=coll)
        if ob is not None:
            A.assign(ob, grey)
    R.ortho_view("front")
    R.render(os.path.join(renders, "Silhouette_front.png"))
    skeleton.ARM_ANGLE = 45.0


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--silhouette" in args:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        R.setup()
        silhouette(os.path.join(OUT, "renders"))
        return
    names = [a for a in args if not a.startswith("--")]
    if not names or names == ["all"]:
        names = list(ORDER)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    renders = os.path.join(OUT, "renders")
    work = os.path.join(OUT, "work")
    os.makedirs(work, exist_ok=True)
    R.setup()
    for n in names:
        for c in list(bpy.data.collections):
            for o in list(c.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(c)
        try:
            B, f, coll = build(n, head_only="--headonly" in args)
        except Exception:
            import traceback
            traceback.print_exc()
            print("[cast] FAILED", n, flush=True)
            continue
        if "--headonly" in args:
            for yaw, tag in ((0, "hf"), (32, "h3"), (90, "hs")):
                R.persp_view(yaw, target=(0, -0.02, 1.645), dist=1.10, height=1.665, lens=85)
                R.render(os.path.join(renders, "%s_%s.png" % (n, tag)), 760, 760)
            continue
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(work, n + ".blend"))
        if "--render" in args:
            t = time.time()
            R.turnaround(renders, n)
            log("rendered", n, "%.1fs" % (time.time() - t))
        if "--face" in args:
            R.persp_view(18, target=(0, -0.02, 1.63), dist=1.25, height=1.67, lens=85)
            R.render(os.path.join(renders, n + "_face.png"), 900, 900)
        if "--expr" in args:
            import faces
            objs = {o.name: o for o in coll.objects if o.name in ("Head", "Brows", "Beard") or o.name.startswith(("LidUp", "LidLow", "Lash"))}
            eyes = [o for o in coll.objects if o.name.startswith("Eye_")]
            normal_eye = eyes[0].data.materials[0] if eyes else None
            dead_eye = M.get(n + "_EyesDead", M.eye, f["iris"], dead=True)
            for kind in faces.EXPRESSIONS:
                faces.apply(objs, f, kind)
                for e in eyes:
                    e.data.materials[0] = dead_eye if kind == "Dead" else normal_eye
                R.persp_view(14, target=(0, -0.04, 1.585), dist=1.05, height=1.60, lens=85)
                R.render(os.path.join(renders, "%s_expr_%s.png" % (n, kind)), 560, 560)
            faces.apply(objs, f, "Neutral")
            for e in eyes:
                e.data.materials[0] = normal_eye
        if "--beauty" in args:
            R.persp_view(28, target=(0, 0, 0.93), dist=4.35, height=1.22, lens=50)
            R.render(os.path.join(renders, n + "_beauty.png"), 1000, 1400)
    print("[cast] DONE", flush=True)


if __name__ == "__main__":
    main()
