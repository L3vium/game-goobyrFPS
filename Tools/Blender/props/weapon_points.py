# Author : Baptiste Vu Liem
"""First-person points of the exported weapons (Sight, ForeGrip, MagWell, see build_weapons.py).

    blender -b --factory-startup --python Tools/Blender/props/weapon_points.py -- --add W_Sniper
    blender -b --factory-startup --python Tools/Blender/props/weapon_points.py -- --list [folder]

--add   W_Sniper's Poly Haven source is no longer kept in Assets, so its points (the same rules as
        build_polyhaven.sniper) are added to the current export by an FBX round trip: import, measure
        on the mesh, add the empties, export with the props settings (into GOOBYR_PROPS_OUT if set).
--list  prints every W_* model's empties in Blender and Unity coordinates (Unity = (-x, z, -y)).
"""
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pbr_lib as L  # noqa: E402
from build_polyhaven import sniper_points  # noqa: E402

WEAPONS = ("W_Pistol", "W_SilencedPistol", "W_Smg", "W_Shotgun", "W_Sniper", "W_RocketLauncher", "W_Flamethrower")


def import_model(path):
    L.reset()
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_custom_normals=True, use_image_search=False)
    roots = [o for o in bpy.context.scene.objects if o.parent is None]
    return roots[0] if roots else None


def local_vertices(ob, material_substring=None):
    """Vertices of a mesh in its own space, optionally only those of faces using a material."""
    me = ob.data
    if material_substring is None:
        co = np.empty(len(me.vertices) * 3, np.float32)
        me.vertices.foreach_get("co", co)
        return co.reshape(-1, 3)
    idx = [i for i, m in enumerate(me.materials) if m is not None and material_substring in m.name]
    verts = set()
    for p in me.polygons:
        if p.material_index in idx:
            verts.update(p.vertices)
    return np.array([tuple(me.vertices[v].co) for v in sorted(verts)], np.float32)


def add_sniper(out_folder):
    path = os.path.join(L.MODELS, "W_Sniper", "W_Sniper.fbx")
    root = import_model(path)
    names = {o.name for o in root.children}
    for n in ("Sight", "ForeGrip", "MagWell"):
        if n in names:
            bpy.data.objects.remove(bpy.data.objects[n])
    muzzle = next(o for o in root.children if o.name.startswith("Muzzle"))
    bore = Vector(muzzle.location)
    # the pivot is 7 cm behind and 4 cm under the trigger (build_polyhaven.sniper)
    trig_c = Vector((0.0, -0.07, 0.04))
    lens = local_vertices(root, "lens")
    rear = lens[lens[:, 1] > lens[:, 1].max() - 0.003]
    axis = Vector((0.0, float(rear[:, 1].max()) - 0.003, float((rear[:, 2].min() + rear[:, 2].max()) * 0.5)))
    rifle = local_vertices(root, "rifle")
    fore, mag = sniper_points(rifle, trig_c, bore)
    for n, p in (("Sight", axis), ("ForeGrip", fore), ("MagWell", mag)):
        L.add_empty(n, root, p)
        print("POINT", n, tuple(round(v, 4) for v in p))
    L.export_fbx(root, "W_Sniper", os.path.join(out_folder, "W_Sniper"))
    print("EXPORTED", os.path.join(out_folder, "W_Sniper", "W_Sniper.fbx"))


def list_points(folder):
    for name in WEAPONS:
        path = os.path.join(folder, name, name + ".fbx")
        if not os.path.exists(path):
            print("MISSING", path)
            continue
        root = import_model(path)
        mn, mx = L.world_bounds([root] + list(root.children))
        print(f"{name}: bounds {tuple(round(v, 3) for v in mn)} .. {tuple(round(v, 3) for v in mx)}")
        for c in sorted(root.children, key=lambda o: o.name):
            p = c.location
            r = tuple(round(v * 57.29578, 1) for v in c.rotation_euler)
            print(f"   {c.name:<10} {c.type:<6} blender ({p.x:+.4f}, {p.y:+.4f}, {p.z:+.4f}) rot {r}"
                  f"   unity ({-p.x:+.4f}, {p.z:+.4f}, {-p.y:+.4f})")


MARKS = {"Grip": (0.9, 0.9, 0.9), "Muzzle": (1.0, 0.5, 0.0), "Sight": (0.0, 1.0, 0.2), "ForeGrip": (0.1, 0.4, 1.0),
         "MagWell": (1.0, 0.1, 0.8), "Mag": (1.0, 1.0, 0.0)}


def preview_points(folder, out_dir):
    """Side view (from the gun's right) of each weapon with a coloured ball on every point and the
    sight line drawn along the barrel: Grip white, Muzzle orange, Sight green, ForeGrip blue,
    MagWell magenta."""
    import math
    for name in WEAPONS:
        path = os.path.join(folder, name, name + ".fbx")
        if not os.path.exists(path):
            continue
        root = import_model(path)
        objs = [root] + list(root.children)
        mn, mx = L.world_bounds(objs)
        for c in list(root.children):
            col = MARKS.get(c.name.split(".")[0])
            if col is None or c.type != "EMPTY":
                continue
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.006, location=c.matrix_world.translation)
            s = bpy.context.object
            m = bpy.data.materials.new("mark_" + c.name)
            b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            b.inputs["Base Color"].default_value = col + (1,)
            b.inputs["Emission Color"].default_value = col + (1,)
            b.inputs["Emission Strength"].default_value = 3.0
            s.data.materials.append(m)
            if c.name.startswith("Sight"):
                p = c.matrix_world.translation
                bpy.ops.mesh.primitive_cylinder_add(radius=0.0008, depth=1.4, location=(p.x, p.y - 0.5, p.z),
                                                   rotation=(math.radians(90), 0, 0))
                bpy.context.object.data.materials.append(m)
        sc = bpy.context.scene
        try:
            sc.render.engine = "BLENDER_EEVEE"
        except TypeError:
            sc.render.engine = "BLENDER_EEVEE_NEXT"
        sc.render.resolution_x, sc.render.resolution_y = 1600, 700
        w = bpy.data.worlds.new("w")
        sc.world = w
        bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
        bg.inputs["Color"].default_value = (0.55, 0.57, 0.6, 1)
        bg.inputs["Strength"].default_value = 1.0
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        sc.collection.objects.link(cam)
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = max(mx.y - mn.y, (mx.z - mn.z) * 1600 / 700) * 1.1
        c = (mn + mx) / 2
        cam.location = (mn.x - 1.0, c.y, c.z)
        cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
        sc.camera = cam
        sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
        sun.data.energy = 3.0
        sun.rotation_euler = (math.radians(40), math.radians(-30), math.radians(-60))
        sc.collection.objects.link(sun)
        os.makedirs(out_dir, exist_ok=True)
        sc.render.filepath = os.path.join(out_dir, name + "_points.png")
        bpy.ops.render.render(write_still=True)
        print("PREVIEW", sc.render.filepath)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--add" in args:
        add_sniper(L.OUT_MODELS)
    if "--list" in args:
        i = args.index("--list")
        list_points(args[i + 1] if i + 1 < len(args) and not args[i + 1].startswith("-") else L.OUT_MODELS)
    if "--preview" in args:
        preview_points(L.OUT_MODELS, L.PREVIEW_DIR)
