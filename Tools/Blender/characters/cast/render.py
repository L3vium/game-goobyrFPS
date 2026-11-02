# Author : Baptiste Vu Liem
"""Design-sheet renders of the cast: neutral studio light, orthographic front / side / back, Eevee.

Each view is a transparent PNG at a fixed scale (PX_PER_M pixels per metre, feet on the bottom
margin), so sheet.py can draw the 1.80 m / 1.60 m guides and the grey scale figure exactly.
"""
import math
import os

import bpy
from mathutils import Vector

PX_PER_M = 400
VIEW_H = 2.20          # metres shown vertically
FOOT_MARGIN = 0.06     # metres below the feet


def _set(obj, attr, value):
    try:
        setattr(obj, attr, value)
    except (AttributeError, TypeError):
        pass


def _cycles_gpu(sc, samples):
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
    except Exception as e:
        print("[cast] no GPU for Cycles:", e)
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    _set(sc.cycles, "denoiser", "OPENIMAGEDENOISE")
    sc.cycles.max_bounces = 6
    sc.cycles.transparent_max_bounces = 8
    sc.render.use_persistent_data = True


def setup(width_m=1.9, transparent=True, samples=64, engine="EEVEE"):
    sc = bpy.context.scene
    if engine == "CYCLES":
        _cycles_gpu(sc, samples)
    else:
        for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
            try:
                sc.render.engine = eng
                break
            except TypeError:
                continue
    sc.render.resolution_x = int(round(width_m * PX_PER_M))
    sc.render.resolution_y = int(round(VIEW_H * PX_PER_M))
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    vt = [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items]
    sc.view_settings.view_transform = "AgX" if "AgX" in vt else "Filmic"
    looks = [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["look"].enum_items]
    for want in ("AgX - Punchy", "AgX - Medium High Contrast", "Medium High Contrast"):
        if want in looks:
            sc.view_settings.look = want
            break
    sc.view_settings.exposure = -0.55
    ee = sc.eevee
    _set(ee, "taa_render_samples", samples)
    _set(ee, "use_raytracing", True)
    _set(ee, "ray_tracing_method", "SCREEN")
    _set(ee, "use_shadows", True)
    _set(ee, "shadow_ray_count", 2)
    _set(ee, "shadow_step_count", 8)
    _set(ee, "use_gtao", True)
    _set(ee, "gtao_distance", 0.25)
    _set(ee, "fast_gi_distance", 0.4)
    _set(ee, "horizon_quality", 0.75)
    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.36, 0.37, 0.39, 1)
    bg.inputs[1].default_value = 0.45
    for o in [o for o in bpy.data.objects if o.get("cast_rig")]:
        bpy.data.objects.remove(o, do_unlink=True)

    def light(name, loc, target, energy, size, colour=(1, 1, 1)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy = energy
        ld.size = size
        ld.color = colour
        _set(ld, "use_shadow", True)
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        d = Vector(target) - Vector(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        lo["cast_rig"] = True
        sc.collection.objects.link(lo)
        return lo

    light("Key", (-2.4, -3.2, 3.4), (0, 0, 1.1), 700, 2.2, (1.0, 0.96, 0.91))
    light("Fill", (3.2, -2.6, 1.7), (0, 0, 1.0), 200, 3.0, (0.90, 0.94, 1.0))
    light("Rim", (1.2, 3.4, 3.0), (0, 0, 1.3), 650, 2.0, (1.0, 0.98, 0.95))
    light("Top", (0.0, -0.5, 4.2), (0, 0, 1.0), 160, 3.0)
    # ground (shadow receiver only, invisible to the camera through the transparent film)
    if "CastGround" not in bpy.data.objects:
        me = bpy.data.meshes.new("CastGround")
        r = 3.0
        me.from_pydata([(-r, -r, 0), (r, -r, 0), (r, r, 0), (-r, r, 0)], [], [(0, 1, 2, 3)])
        g = bpy.data.objects.new("CastGround", me)
        g["cast_rig"] = True
        sc.collection.objects.link(g)
        m = bpy.data.materials.new("CastGround")
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        p.inputs["Base Color"].default_value = (0.30, 0.30, 0.31, 1)
        p.inputs["Roughness"].default_value = 0.9
        me.materials.append(m)
        _set(g, "is_shadow_catcher", True)
        g.visible_camera = False


def camera(name="CastCam"):
    sc = bpy.context.scene
    cam = bpy.data.objects.get(name)
    if cam is None:
        cd = bpy.data.cameras.new(name)
        cam = bpy.data.objects.new(name, cd)
        cam["cast_rig"] = True
        sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def ortho_view(view, cx=0.0, cy=0.0):
    cam = camera()
    cam.data.type = "ORTHO"
    cam.data.sensor_fit = "VERTICAL"
    cam.data.ortho_scale = VIEW_H
    cam.data.clip_start = 0.1
    cam.data.clip_end = 30.0
    zc = VIEW_H * 0.5 - FOOT_MARGIN
    d = 8.0
    if view == "front":
        cam.location = (cx, cy - d, zc)
        cam.rotation_euler = (math.radians(90), 0, 0)
    elif view == "back":
        cam.location = (cx, cy + d, zc)
        cam.rotation_euler = (math.radians(90), 0, math.radians(180))
    elif view == "side":       # the character's left profile (faces image left)
        cam.location = (cx + d, cy, zc)
        cam.rotation_euler = (math.radians(90), 0, math.radians(90))
    elif view == "side_r":
        cam.location = (cx - d, cy, zc)
        cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
    return cam


def persp_view(yaw_deg, target=(0, 0, 1.0), dist=4.2, height=1.35, lens=60):
    cam = camera()
    cam.data.type = "PERSP"
    cam.data.lens = lens
    cam.data.sensor_fit = "VERTICAL"
    cam.data.sensor_width = 36
    cam.data.sensor_height = 24
    y = math.radians(yaw_deg)
    cam.location = (target[0] + math.sin(y) * dist, target[1] - math.cos(y) * dist, height)
    d = Vector(target) - Vector(cam.location)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return cam


def render(path, width_px=None, height_px=None):
    sc = bpy.context.scene
    if width_px:
        sc.render.resolution_x = width_px
    if height_px:
        sc.render.resolution_y = height_px
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def turnaround(outdir, name, views=("front", "side", "back"), width_m=1.9, offset=(0.0, 0.0)):
    paths = []
    sc = bpy.context.scene
    sc.render.resolution_x = int(round(width_m * PX_PER_M))
    sc.render.resolution_y = int(round(VIEW_H * PX_PER_M))
    for v in views:
        ortho_view(v, *offset)
        paths.append(render(os.path.join(outdir, "%s_%s.png" % (name, v))))
    return paths
