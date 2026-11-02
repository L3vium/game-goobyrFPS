# Author : Baptiste Vu Liem
"""Contact sheets of animation clips (Eevee), rendered on the reference character."""
import math
import os

import bpy
import numpy as np

import chr_common as C

_SCENE_READY = {"cam": None}
CELL = (300, 400)


def _ensure_scene():
    if _SCENE_READY["cam"] is None:
        cam = C.setup_preview_scene(res=CELL)
        bpy.context.scene.render.resolution_percentage = 100
        try:
            bpy.context.scene.eevee.taa_render_samples = 16
        except Exception:
            pass
        lod1 = bpy.data.objects.get("Chr_Agent_LOD1")
        if lod1:
            lod1.hide_render = True
        _SCENE_READY["cam"] = cam
    return _SCENE_READY["cam"]


def _read_png(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)


# reference geometry the clips are authored against (min corner, max corner), Blender metres
PROPS = {
    "Mantle": [((-0.6, -1.6, 0.0), (0.6, -0.40, 1.10))],
    "Hang_Idle": [((-1.0, -1.2, 0.0), (1.0, -0.12, 2.10))],
    "Shimmy_L": [((-1.0, -1.2, 0.0), (1.0, -0.12, 2.10))],
    "Shimmy_R": [((-1.0, -1.2, 0.0), (1.0, -0.12, 2.10))],
    "Hang_ClimbUp": [((-1.0, -1.6, 0.0), (1.0, -0.12, 2.10))],
    "Lean_Wall": [((-1.0, 0.08, 0.0), (1.0, 0.4, 2.2))],
    "Sit_Chair": [((-0.25, -0.20, 0.0), (0.25, 0.30, 0.45))],
}


def _add_props(name):
    objs = []
    for (a, b) in PROPS.get(name, []):
        bpy.ops.mesh.primitive_cube_add(size=1, location=[(a[i] + b[i]) / 2 for i in range(3)])
        o = bpy.context.object
        o.scale = [b[i] - a[i] for i in range(3)]
        objs.append(o)
    return objs


def contact_sheet(arm, clip, out_dir, count=8, yaws=(35, 100)):
    cam = _ensure_scene()
    props = _add_props(clip.name)
    n = len(clip.frames)
    idx = sorted(set(int(round(i * (n - 1) / max(count - 1, 1))) for i in range(count)))
    tmp = os.path.join(C.WORK_DIR, "_cs")
    os.makedirs(tmp, exist_ok=True)
    rows = []
    for yaw in yaws:
        row = []
        for i in idx:
            bpy.context.scene.frame_set(i)
            hips = arm.pose.bones["pelvis"].head  # armature space == world
            tgt = (hips.x, hips.y, hips.z - 0.08)
            dist = 4.6
            yr = math.radians(yaw)
            cam.data.lens = 50
            cam.location = (tgt[0] + math.sin(yr) * dist, tgt[1] - math.cos(yr) * dist, tgt[2] + 0.3)
            from mathutils import Vector
            d = Vector(tgt) - cam.location
            cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
            p = os.path.join(tmp, "f_%d_%d.png" % (yaw, i))
            C.render_to(p)
            row.append(_read_png(p))
        rows.append(np.concatenate(row, axis=1))
    for o in props:
        bpy.data.objects.remove(o, do_unlink=True)
    sheet = np.concatenate(rows[::-1], axis=0)  # images are bottom-up
    h, w = sheet.shape[:2]
    img = bpy.data.images.new("sheet", w, h, alpha=True)
    img.pixels.foreach_set(sheet.ravel())
    out = os.path.join(out_dir, "anim_%s.png" % clip.name)
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("[anim] contact sheet", out, "frames", idx, flush=True)
    return out
