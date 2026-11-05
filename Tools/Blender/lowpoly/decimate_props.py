# Author : Baptiste Vu Liem
"""Brings the scanned furniture and props down to faceted low-poly (headless Blender 5.2).

  blender -b --factory-startup --python Tools/Blender/lowpoly/decimate_props.py [-- Name Name ...]

Reads Assets/_Game/Art/PBR/Models/<Furn_|Prop_>*/<Name>.fbx and writes the same model, same object and
material names, to Tools/Blender/out/lowpoly/<Name>/<Name>.fbx. Copy them over under the editor lock.
Their materials come from Goobyr/Build/Flat Materials (each keeps the texture's average colour).
"""
import glob
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
MODELS = os.path.join(PROJECT, "Assets", "_Game", "Art", "PBR", "Models")
OUT = os.path.join(PROJECT, "Tools", "Blender", "out", "lowpoly")
TARGET = {"Furn_": 700, "Prop_": 450}


def tris(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)


def run(path):
    name = os.path.splitext(os.path.basename(path))[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_image_search=False, ignore_leaf_bones=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    target = TARGET.get(name[:5], 500)
    total = sum(tris(o) for o in meshes if "_LOD" not in o.name or o.name.endswith("_LOD0")) or 1
    before = sum(tris(o) for o in meshes)
    for ob in meshes:
        share = target / total
        if ob.name.endswith("_LOD1"):
            share *= 0.6
        elif ob.name.endswith("_LOD2"):
            share *= 0.35
        if share >= 1.0 or tris(ob) < 60:
            continue
        bpy.context.view_layer.objects.active = ob
        # Scanned shells are full of doubles and open seams: weld first, or the collapse tears them.
        mod = ob.modifiers.new("Weld", "WELD")
        mod.merge_threshold = 0.002
        dec = ob.modifiers.new("Decimate", "DECIMATE")
        dec.decimate_type = "COLLAPSE"
        dec.ratio = max(0.01, share)
        dec.use_collapse_triangulate = True
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.modifier_apply(modifier=dec.name)
    after = sum(tris(o) for o in meshes)
    out_dir = os.path.join(OUT, name)
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, name + ".fbx"), use_selection=True, axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", global_scale=1.0, mesh_smooth_type="FACE", use_tspace=False, path_mode="STRIP",
                             embed_textures=False, bake_anim=False, add_leaf_bones=False, object_types={"MESH", "EMPTY"})
    print("[props]", name, before, "->", after, flush=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    paths = []
    for prefix in TARGET:
        paths += glob.glob(os.path.join(MODELS, prefix + "*", prefix + "*.fbx"))
    for path in sorted(paths):
        if args and os.path.splitext(os.path.basename(path))[0] not in args:
            continue
        try:
            run(path)
        except Exception as e:
            print("[props] FAILED", path, e, flush=True)
    print("[props] DONE", flush=True)


if __name__ == "__main__":
    main()
