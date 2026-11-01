# Author : Baptiste Vu Liem
"""Shared helpers for the realistic character pipeline (MPFB humans -> Unity FBX).

Imported by chr_build.py, anim_build.py and preview.py (all run headless in Blender 5.2).
Conventions (see docs/Credits.md and the report in the session):
  * armature object is always called "Armature", bones use the MPFB "game_engine" names
    (UE-mannequin style: Root, pelvis, spine_01..03, neck_01, head, clavicle_l, upperarm_l ...)
  * rest pose = shared A-pose (arms straight 45 deg below horizontal, palms down/in, fingers
    straight, legs straight), identical on every character, the arms and every clip; metres, Z-up in
    Blender, exported Y-up / -Z forward for Unity.
  * character faces -Y in Blender (MakeHuman default), +X is the character's left.
"""
import math
import os
import sys
import tempfile

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CHAR_DIR = os.path.join(PROJECT, "Assets", "_Game", "Art", "Characters")
ANIM_DIR = os.path.join(CHAR_DIR, "Animations")
WORK_DIR = os.environ.get("GOOBYR_CHAR_WORK", os.path.join(tempfile.gettempdir(), "goobyr_chars_work"))
os.makedirs(WORK_DIR, exist_ok=True)

ARMATURE_NAME = "Armature"

FINGERS = ["thumb", "index", "middle", "ring", "pinky"]


def argv_after_dashes():
    if "--" in sys.argv:
        return sys.argv[sys.argv.index("--") + 1:]
    return []


def clean_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images, bpy.data.actions,
                 bpy.data.cameras, bpy.data.lights):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)


def select_only(obj):
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


# ---------------------------------------------------------------------------------------------
# T-pose
# ---------------------------------------------------------------------------------------------

def _rotate_pose_bone_world(arm, pb, q):
    """Rotate pose bone pb by world quaternion q around its head (armature space == world here)."""
    m = pb.matrix.copy()
    head = m.to_translation()
    rot = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head)
    pb.matrix = rot @ m
    bpy.context.view_layer.update()


def _align_bone(arm, name, direction):
    pb = arm.pose.bones.get(name)
    if pb is None:
        return
    y = (pb.matrix.to_3x3() @ Vector((0, 1, 0))).normalized()
    q = y.rotation_difference(Vector(direction).normalized())
    _rotate_pose_bone_world(arm, pb, q)


REST_ARM_ANGLE = 45.0  # degrees below horizontal: shared A-pose reference of every exported FBX


def pose_tpose(arm, arm_angle=None):
    """Pose the MPFB game_engine rig into the shared reference pose (not applied):
    arms straight at REST_ARM_ANGLE below horizontal in the frontal plane (0 = T-pose), palms down/in,
    fingers straight, legs straight down. Native clavicles are kept (best shoulder deformation)."""
    ang = math.radians(REST_ARM_ANGLE if arm_angle is None else arm_angle)
    select_only(arm)
    bpy.ops.object.mode_set(mode="POSE")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for side, sx in (("l", 1.0), ("r", -1.0)):
        Ry = Matrix.Rotation(sx * ang, 3, "Y")
        if ang == 0.0:
            pb = arm.pose.bones["clavicle_" + side]
            y = (pb.matrix.to_3x3() @ Vector((0, 1, 0))).normalized()
            _align_bone(arm, "clavicle_" + side, Vector((y.x, y.y, 0.0)))
        axis = Ry @ Vector((sx, 0, 0))
        for b in ("upperarm_", "lowerarm_", "hand_"):
            _align_bone(arm, b + side, axis)
        # palm: make the knuckle line (index -> pinky) point backwards (+Y) by rolling the hand
        hand = arm.pose.bones["hand_" + side]
        a = arm.pose.bones["index_01_" + side].head
        b = arm.pose.bones["pinky_01_" + side].head
        knuckles = (b - a)
        k = (knuckles - axis * knuckles.dot(axis)).normalized()
        want = Vector((0, 1, 0))
        rot = k.angle(want)
        sign = 1.0 if axis.dot(k.cross(want)) > 0 else -1.0
        _rotate_pose_bone_world(arm, hand, Quaternion(axis, sign * rot))
        for f in ("index", "middle", "ring", "pinky"):
            for i in ("01", "02", "03"):
                _align_bone(arm, "%s_%s_%s" % (f, i, side), axis)
        tdir = Ry @ Vector((sx * 0.70, -0.62, -0.35)).normalized()
        t1 = Ry @ Vector((sx * 0.55, -0.62, -0.45)).normalized()
        for i in ("01", "02", "03"):
            _align_bone(arm, "thumb_%s_%s" % (i, side), tdir if i != "01" else t1)
        # legs: straight down, very slightly apart; feet forward
        _align_bone(arm, "thigh_" + side, (sx * 0.035, 0, -1))
        _align_bone(arm, "calf_" + side, (sx * 0.035, 0.0, -1))
    bpy.ops.object.mode_set(mode="OBJECT")


def apply_pose_as_rest(arm, meshes):
    """Bake the current pose into meshes and armature rest (meshes must have no shape keys)."""
    for m in meshes:
        select_only(m)
        if m.data.shape_keys:
            bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
        for mod in list(m.modifiers):
            if mod.type == "ARMATURE":
                bpy.ops.object.modifier_apply(modifier=mod.name)
    select_only(arm)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for m in meshes:
        mod = m.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
        # keep armature first
        select_only(m)
        while m.modifiers[0] != mod:
            bpy.ops.object.modifier_move_up(modifier=mod.name)


# ---------------------------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------------------------

def export_fbx(path, objects, bake_anim=False, action=None, fps=30):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    scene = bpy.context.scene
    scene.render.fps = fps
    kwargs = dict(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        global_scale=1.0,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        armature_nodetype="NULL",
        mesh_smooth_type="FACE",
        use_tspace=True,
        use_mesh_modifiers=True,
        path_mode="STRIP",
        embed_textures=False,
        bake_anim=bake_anim,
    )
    if bake_anim:
        kwargs.update(
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=False,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0,
        )
    bpy.ops.export_scene.fbx(**kwargs)


# ---------------------------------------------------------------------------------------------
# Preview rendering
# ---------------------------------------------------------------------------------------------

def setup_preview_scene(res=(720, 960)):
    scene = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = eng
            break
        except TypeError:
            continue
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.42, 0.45, 0.5, 1)
    bg.inputs[1].default_value = 0.35

    def light(name, kind, loc, rot, energy, size=1.0):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        if kind == "AREA":
            ld.size = size
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        lo.rotation_euler = [math.radians(a) for a in rot]
        scene.collection.objects.link(lo)
        return lo

    light("Key", "AREA", (-2.2, -3.0, 3.0), (55, 0, -35), 260, 2.5)
    light("Fill", "AREA", (3.0, -2.5, 1.6), (70, 0, 50), 90, 3.0)
    light("Rim", "AREA", (0.5, 3.5, 2.8), (-60, 0, 175), 220, 2.0)
    # ground
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
    g = bpy.context.object
    g.name = "PreviewGround"
    gm = bpy.data.materials.new("PreviewGround")
    gm.use_nodes = True
    p = next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (0.25, 0.25, 0.25, 1)
    p.inputs["Roughness"].default_value = 0.9
    g.data.materials.append(gm)
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def aim_camera(cam, target, distance, yaw_deg, height, lens=50):
    cam.data.lens = lens
    yaw = math.radians(yaw_deg)
    # yaw 0 = in front of the character (character faces -Y)
    pos = Vector((math.sin(yaw) * distance, -math.cos(yaw) * distance, height))
    cam.location = Vector(target) + pos - Vector((0, 0, target[2])) + Vector((0, 0, 0))
    cam.location.z = height
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def render_to(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
