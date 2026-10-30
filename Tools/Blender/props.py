# Author : Baptiste Vu Liem
# Goobyr FPS hero props, modelled in code with bpy: a chunky toy sedan and the four common guns.
# Colours are written as vertex colours ("Col"), which the Goobyr/Toon shader multiplies.
# Each prop is exported as FBX into Assets/_Game/Art/Resources/Models/ (loaded by name at runtime:
# Car, W_Pistol, W_SilencedPistol, W_Smg, W_Shotgun, W_Sniper). Muzzle empties mark the barrel end.
# Run from Blender (Scripting tab) or through MCP: set OUT to the project's Models folder first.
import bpy
import bmesh
import math
import os

OUT = globals().get("OUT", r"D:\_Project\_GamesUnity\_Isart\Goobyr FPS\Assets\_Game\Art\Resources\Models")


# Work in a scene of our own: the user's open scene is never touched.
if "GoobyrProps" not in bpy.data.scenes:
    bpy.data.scenes.new("GoobyrProps")
PROPS_SCENE = bpy.data.scenes["GoobyrProps"]
bpy.context.window.scene = PROPS_SCENE


def clear():
    for obj in list(PROPS_SCENE.objects):
        mesh = obj.data if obj.type == "MESH" else None
        bpy.data.objects.remove(obj, do_unlink=True)
        if mesh is not None and mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def paint(obj, rgb):
    mesh = obj.data
    if not mesh.color_attributes:
        mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")
    layer = mesh.color_attributes["Col"]
    for item in layer.data:
        item.color = (rgb[0], rgb[1], rgb[2], 1.0)


def box(name, loc, size, rgb, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    paint(obj, rgb)
    return obj


def cyl(name, loc, radius, depth, rgb, verts=10, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    paint(obj, rgb)
    return obj


def join(name, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    # Flat shading: the low-poly look.
    for poly in obj.data.polygons:
        poly.use_smooth = False
    return obj


def muzzle(parent, loc):
    bpy.ops.object.empty_add(location=loc)
    empty = bpy.context.active_object
    empty.name = "Muzzle"
    empty.parent = parent
    return empty


def export(name, root):
    # Props face Blender +Y, which lands on Unity -Z: WeaponModels / MapKit turn them around.
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children:
        child.select_set(True)
    os.makedirs(OUT, exist_ok=True)
    # Unity axes: -Z forward in Blender becomes +Z in Unity with these settings.
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE", colors_type="SRGB")


METAL = (0.22, 0.23, 0.27)
DARK = (0.12, 0.12, 0.14)
WOOD = (0.55, 0.35, 0.2)


def car():
    clear()
    body = box("Body", (0, 0, 0.75), (1.9, 4.3, 0.8), (0.85, 0.3, 0.3), 0.12)
    cabin = box("Cabin", (0, -0.2, 1.45), (1.65, 2.2, 0.65), (0.7, 0.85, 0.95), 0.1)
    bumper_f = box("BumperF", (0, 2.2, 0.5), (1.95, 0.2, 0.3), DARK)
    bumper_b = box("BumperB", (0, -2.2, 0.5), (1.95, 0.2, 0.3), DARK)
    lights = [box("Light", (x, 2.16, 0.8), (0.35, 0.08, 0.18), (1.0, 0.95, 0.7)) for x in (-0.65, 0.65)]
    tails = [box("Tail", (x, -2.16, 0.8), (0.35, 0.08, 0.15), (0.9, 0.15, 0.15)) for x in (-0.65, 0.65)]
    wheels = []
    for x in (-0.95, 0.95):
        for y in (-1.4, 1.4):
            wheels.append(cyl("Wheel", (x, y, 0.35), 0.36, 0.28, DARK, 10, (0, math.pi / 2, 0)))
            wheels.append(cyl("Hub", (x * 1.08, y, 0.35), 0.16, 0.06, (0.8, 0.8, 0.82), 8, (0, math.pi / 2, 0)))
    obj = join("Car", [body, cabin, bumper_f, bumper_b] + lights + tails + wheels)
    export("Car", obj)


def pistol(name, silenced):
    clear()
    parts = [box("Slide", (0, 0.05, 0.05), (0.06, 0.26, 0.07), METAL, 0.01),
             box("Grip", (0, -0.04, -0.04), (0.055, 0.07, 0.14), (0.35, 0.75, 0.95) if silenced else (0.9, 0.6, 0.3), 0.01),
             box("Trigger", (0, 0.01, -0.02), (0.02, 0.04, 0.04), DARK)]
    end = 0.2
    if silenced:
        parts.append(cyl("Silencer", (0, 0.27, 0.05), 0.026, 0.18, DARK, 10, (math.pi / 2, 0, 0)))
        end = 0.37
    obj = join(name, parts)
    muzzle(obj, (0, end, 0.05))
    export(name, obj)


def smg():
    clear()
    parts = [box("Body", (0, 0.08, 0.04), (0.08, 0.38, 0.11), METAL, 0.012),
             box("Mag", (0, 0.12, -0.09), (0.05, 0.06, 0.18), (0.95, 0.85, 0.3)),
             box("Grip", (0, -0.04, -0.06), (0.055, 0.06, 0.12), DARK),
             box("Stock", (0, -0.17, 0.03), (0.04, 0.12, 0.06), DARK),
             cyl("Barrel", (0, 0.32, 0.05), 0.018, 0.1, DARK, 8, (math.pi / 2, 0, 0))]
    obj = join("W_Smg", parts)
    muzzle(obj, (0, 0.38, 0.05))
    export("W_Smg", obj)


def shotgun():
    clear()
    parts = [box("Stock", (0, -0.13, 0.0), (0.07, 0.26, 0.1), WOOD, 0.015),
             box("Body", (0, 0.12, 0.03), (0.08, 0.24, 0.09), METAL, 0.01),
             cyl("Barrel", (0, 0.42, 0.05), 0.025, 0.36, DARK, 10, (math.pi / 2, 0, 0)),
             cyl("Pump", (0, 0.36, 0.0), 0.032, 0.14, (0.85, 0.35, 0.3), 10, (math.pi / 2, 0, 0))]
    obj = join("W_Shotgun", parts)
    muzzle(obj, (0, 0.6, 0.05))
    export("W_Shotgun", obj)


def sniper():
    clear()
    parts = [box("Stock", (0, -0.16, 0.0), (0.07, 0.3, 0.12), (0.45, 0.8, 0.45), 0.015),
             box("Body", (0, 0.14, 0.03), (0.07, 0.32, 0.08), METAL, 0.01),
             cyl("Barrel", (0, 0.55, 0.04), 0.017, 0.5, DARK, 8, (math.pi / 2, 0, 0)),
             cyl("Scope", (0, 0.12, 0.12), 0.03, 0.26, DARK, 10, (math.pi / 2, 0, 0)),
             cyl("Lens", (0, 0.255, 0.12), 0.026, 0.01, (0.4, 0.8, 1.0), 10, (math.pi / 2, 0, 0)),
             box("Bipod", (0, 0.4, -0.05), (0.1, 0.02, 0.1), DARK)]
    obj = join("W_Sniper", parts)
    muzzle(obj, (0, 0.8, 0.04))
    export("W_Sniper", obj)


car()
pistol("W_Pistol", False)
pistol("W_SilencedPistol", True)
smg()
shotgun()
sniper()
print("exported to", OUT)
