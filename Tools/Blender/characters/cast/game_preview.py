# Author : Baptiste Vu Liem
"""Renders an exported cast FBX (LOD0, baked textures) close on the face from both three-quarter sides.

    blender -b --python Tools/Blender/characters/cast/game_preview.py -- Chr_Waiter [Chr_Rook ...] [--shape=Dead]

Out: Tools/Blender/out/cast/renders/<Chr>_game_q_l.png / _q_r.png. Checks the bake (smudges, seams) without Unity.
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import cast_build  # noqa: E402
import export  # noqa: E402
import render as R  # noqa: E402


def preview(name, shape=None):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    out_dir = os.path.join(cast_build.OUT, "export", name)
    bpy.ops.import_scene.fbx(filepath=os.path.join(out_dir, name + ".fbx"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and ("_LOD" not in o.name or o.name.endswith("_LOD0"))]
    lods = [o for o in bpy.context.scene.objects if o.type == "MESH" and ("_LOD1" in o.name or "_LOD2" in o.name)]
    for o in lods:
        o.hide_render = True
    for o in meshes:
        for s in o.material_slots:
            if s.material is not None:
                s.material.name = s.material.name.split(".")[0]
    if shape:
        for o in meshes:
            keys = o.data.shape_keys
            if keys is not None and shape in keys.key_blocks:
                keys.key_blocks[shape].value = 1.0
    export.preview_render(name, out_dir, meshes, lods)
    renders = os.path.join(cast_build.OUT, "renders")
    tag = "_" + shape if shape else ""
    for side, ang in (("l", 35), ("r", -35)):
        R.persp_view(ang, target=(0, -0.02, 1.60), dist=0.9, height=1.62, lens=85)
        R.render(os.path.join(renders, "%s_game_q_%s%s.png" % (name, side, tag)), 800, 800)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for n in [a for a in args if not a.startswith("--")]:
        shape = next((a.split("=", 1)[1] for a in args if a.startswith("--shape=")), None)
        preview(n, shape)
    print("[cast] PREVIEW DONE", flush=True)


if __name__ == "__main__":
    main()
