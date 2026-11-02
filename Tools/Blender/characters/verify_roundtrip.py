# Author : Baptiste Vu Liem
"""Round-trip check: import an exported character FBX and a clip FBX into an empty scene, drive the
character with the clip and render a frame. Prints bone/mesh/clip statistics.

Usage: blender -b --factory-startup --python Tools/Blender/characters/verify_roundtrip.py -- Chr_Agent Walk_F [out.png]
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chr_common as C  # noqa: E402

args = C.argv_after_dashes()
char = args[0] if args else "Chr_Agent"
clip = args[1] if len(args) > 1 else "Walk_F"
out = args[2] if len(args) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(C.CHAR_DIR, char, char + ".fbx"))
char_objs = list(bpy.context.selected_objects)
arm = next(o for o in char_objs if o.type == "ARMATURE")
meshes = [o for o in char_objs if o.type == "MESH"]
print("[rt] character", char, "armature", arm.name, "bones", len(arm.data.bones))
for m in meshes:
    tris = sum(len(p.vertices) - 2 for p in m.data.polygons)
    groups = {g.name for g in m.vertex_groups}
    print("[rt]  mesh", m.name, "tris", tris, "materials", [s.material.name for s in m.material_slots],
          "skinned", any(md.type == "ARMATURE" for md in m.modifiers), "groups", len(groups),
          "unknown groups", sorted(groups - {b.name for b in arm.data.bones})[:5])

bpy.ops.import_scene.fbx(filepath=os.path.join(C.ANIM_DIR, clip + ".fbx"))
anim_objs = list(bpy.context.selected_objects)
anim_arm = next(o for o in anim_objs if o.type == "ARMATURE")
act = anim_arm.animation_data.action
names_a = {b.name for b in anim_arm.data.bones}
names_c = {b.name for b in arm.data.bones}
print("[rt] clip", clip, "action", act.name, "frames", tuple(act.frame_range), "bones match", names_a == names_c)
# compare rest poses (head positions) between the clip skeleton and the character skeleton
worst = max(names_a & names_c, key=lambda n: ((anim_arm.matrix_world @ anim_arm.data.bones[n].head_local) - (arm.matrix_world @ arm.data.bones[n].head_local)).length)
diff = ((anim_arm.matrix_world @ anim_arm.data.bones[worst].head_local) - (arm.matrix_world @ arm.data.bones[worst].head_local)).length
print("[rt] object scales: character", tuple(round(x, 3) for x in arm.matrix_world.to_scale()), "clip", tuple(round(x, 3) for x in anim_arm.matrix_world.to_scale()))
print("[rt] max world rest head offset clip vs character: %.3f m (%s)" % (diff, worst))
for n in ("hand_r", "foot_l", "pelvis"):
    print("[rt]  rest", n, "clip", tuple(round(x, 3) for x in anim_arm.matrix_world @ anim_arm.data.bones[n].head_local),
          "character", tuple(round(x, 3) for x in arm.matrix_world @ arm.data.bones[n].head_local))
arm.animation_data_create()
arm.animation_data.action = act
if hasattr(arm.animation_data, "action_slot") and len(getattr(act, "slots", [])):
    arm.animation_data.action_slot = act.slots[0]
for o in anim_objs:
    bpy.data.objects.remove(o, do_unlink=True)
sc = bpy.context.scene
sc.frame_start, sc.frame_end = int(act.frame_range[0]), int(act.frame_range[1])
mid = int(sum(act.frame_range) / 2)
sc.frame_set(mid)
if out:
    cam = C.setup_preview_scene(res=(600, 800))
    C.aim_camera(cam, (0, 0, 0.95), 4.0, 30, 1.2, lens=50)
    C.render_to(out)
    print("[rt] rendered", out)
