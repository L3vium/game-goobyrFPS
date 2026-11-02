# Author : Baptiste Vu Liem
"""Shared helpers of the first-person arms pipeline (fp_rig, fp_solve, fp_build).

Space: the scene is the camera's space. The eye is the origin, the view looks along Blender -Y with
+Z up, so the right of the screen is -X. Unity sees the same point at (-x, z, -y) (FBX export with
axis_forward=-Z, axis_up=Y and Unity's handedness flip), and a rotation R as C.R.C^-1.
Metres everywhere.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PROPS = os.path.join(HERE, "..", "props")
sys.path.insert(0, os.path.abspath(PROPS))
import pbr_lib as L  # noqa: E402

ASSET = "FP_Arms"
ASSET_DIR_REL = "Assets/_Game/Art/FirstPerson/" + ASSET
ASSET_DIR = os.path.join(ROOT, *ASSET_DIR_REL.split("/"))
OUT = os.environ.get("GOOBYR_FP_OUT") or os.path.join(ROOT, "Tools", "Blender", "out", "fp", ASSET)
PREVIEW = os.environ.get("GOOBYR_FP_PREVIEW") or os.path.join(OUT, "previews")
# Weapons are read from the staging export when it exists (it carries the new points), else Assets.
WEAPON_DIRS = [p for p in (os.environ.get("GOOBYR_PROPS_OUT"), L.MODELS) if p]

FPS = 60
# vertical field of view of the Unity overlay camera that draws the arms (CameraRig.VIEW_FOV)
VIEW_FOV = float(os.environ.get("GOOBYR_FP_FOV", "48"))

# Blender -> Unity basis change (a reflection): Unity = C . Blender
C3 = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))


def u2b(v):
    """Unity camera space (x right, y up, z forward) -> Blender scene."""
    return Vector((-v[0], -v[2], v[1]))


def b2u(v):
    return (-v[0], v[2], -v[1])


def rot_b2u(m3):
    """Blender rotation (3x3) -> Unity quaternion (x, y, z, w)."""
    mu = C3 @ m3.to_3x3() @ C3.transposed()
    q = mu.to_quaternion()
    return (q.x, q.y, q.z, q.w)


def euler_u(pitch, yaw, roll):
    """A rotation given the Unity way (degrees: pitch about x - positive tips the front down -,
    yaw about y - positive turns right -, roll about z - positive tips the top to the left), as a
    Blender 3x3 matrix. Unity's Euler order is Z, X, Y (roll first, yaw last)."""
    def ax(axis_u, deg):
        # a rotation about a Unity axis = rotation about the mapped Blender axis by the opposite angle
        # (the basis change is a reflection)
        return Matrix.Rotation(math.radians(-deg), 3, u2b(axis_u).normalized())
    return ax((0, 1, 0), yaw) @ ax((1, 0, 0), pitch) @ ax((0, 0, 1), roll)


def frame(rot3, loc):
    m = rot3.to_4x4()
    m.translation = Vector(loc)
    return m


def look_frame(forward, up):
    """3x3 matrix whose columns are the weapon convention axes for a forward (-Y local) and up (+Z)."""
    f = Vector(forward).normalized()
    u = (Vector(up) - f * Vector(up).dot(f)).normalized()
    y = -f
    x = y.cross(u)
    return Matrix((x, y, u)).transposed()


def smoothstep(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0) if b != a else (1.0 if x >= a else 0.0)
    return t * t * (3 - 2 * t)


def write_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=1)


# ------------------------------------------------------------------ scene / render
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.render.fps = FPS
    return sc


def fp_camera(aspect=16 / 9, name="FPCam"):
    """The Unity overlay camera: at the eye, looking down -Y, 60 degrees vertical."""
    sc = bpy.context.scene
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        sc.collection.objects.link(cam)
    cam.location = (0, 0, 0)
    cam.rotation_euler = (math.radians(90), 0, math.radians(180))
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(VIEW_FOV)
    cam.data.clip_start = 0.02
    cam.data.clip_end = 20
    sc.camera = cam
    sc.render.resolution_y = 720
    sc.render.resolution_x = int(round(720 * aspect))
    return cam


def studio(strength=1.0, ground=False):
    """Soft outdoor-ish light: sky, a warm key from above right, a cool fill."""
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    names = [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items]
    sc.view_settings.view_transform = "AgX" if "AgX" in names else "Filmic"
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    w = bpy.data.worlds.new("fp_world")
    sc.world = w
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.42, 0.5, 0.62, 1)
    bg.inputs["Strength"].default_value = 0.7 * strength
    for name, energy, rot, col in (("key", 3.2, (50, 12, 160), (1.0, 0.93, 0.82)),
                                   ("fill", 0.8, (65, -20, -40), (0.75, 0.82, 1.0))):
        ld = bpy.data.lights.new(name, "SUN")
        ld.energy = energy * strength
        ld.color = col
        ld.angle = math.radians(3)
        lo = bpy.data.objects.new(name, ld)
        lo.rotation_euler = tuple(math.radians(a) for a in rot)
        sc.collection.objects.link(lo)
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -1.6))
        g = bpy.context.object
        gm = bpy.data.materials.new("ground")
        b = next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = (0.2, 0.22, 0.18, 1)
        g.data.materials.append(gm)


def render(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)
    return path


# ------------------------------------------------------------------ weapons
def weapon_fbx(name):
    for d in WEAPON_DIRS:
        p = os.path.join(d, name, name + ".fbx")
        if os.path.exists(p):
            return p
    return None


def import_weapon(name):
    """Imports a W_* model as exported (its Grip at the origin, barrel -Y). Returns (root, points)
    where points maps empty names (Grip, Muzzle, Sight, ForeGrip, MagWell) to 4x4 local matrices,
    and the "Mag" child mesh if any."""
    path = weapon_fbx(name)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_custom_normals=True)
    new = [o for o in bpy.data.objects if o not in before]
    root = next(o for o in new if o.parent is None)
    root.name = "WPN_" + name
    points = {}
    for o in new:
        base = o.name.split(".")[0]
        if o.type == "EMPTY":
            points[base] = o.matrix_local.copy()
    mag = next((o for o in new if o.type == "MESH" and o.name.split(".")[0] == "Mag"), None)
    return root, points, mag
