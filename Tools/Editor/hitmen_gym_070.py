"""0.7.0 gym layout pass (run in the open editor, not in PIE; idempotent: every step checks what is already there).

    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_gym_070.py

- Zipline: the start tower is 5.5 m deep (room in front of the post) and the landing platform is 5 x 12 m.
- Tower access: the ladder moves to the back of the ladder block; a marked path on the right runs under a stair of
  five 40 cm steps from the block top to the tower top.
- Mounted gun stands on its own tripod (pedestal removed); the radio gets the TV and the alarm clock beside it;
  a body container near the guards.
Then bake the navmesh (Tools/Editor/ue_bake_nav.sh) and run hitmen_check_reach.py.
"""
import unreal

EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
LES = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
CUBE = unreal.load_asset("/Engine/BasicShapes/Cube")
GRID = unreal.load_asset("/Game/LevelPrototyping/Materials/MI_PrototypeGrid_Gray_02")
MARK = unreal.load_asset("/Game/Hitmen/Materials/Instances/MI_Gold")


def actors():
    return EAS.get_all_level_actors()


def by_label(label):
    for a in actors():
        if a.get_actor_label() == label:
            return a
    return None


def box(label, center, extent, material=GRID, folder="Gym/Parkour"):
    """A cube static mesh actor filling center +- extent (cm); reused if the label exists."""
    a = by_label(label)
    if not a:
        a = EAS.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(*center))
        a.set_actor_label(label)
    comp = a.static_mesh_component
    comp.set_static_mesh(CUBE)
    comp.set_material(0, material)
    a.set_actor_location(unreal.Vector(*center), False, False)
    a.set_actor_rotation(unreal.Rotator(roll=0, pitch=0, yaw=0), False)
    a.set_actor_scale3d(unreal.Vector(extent[0] / 50.0, extent[1] / 50.0, extent[2] / 50.0))
    a.set_folder_path(folder)
    return a


def set_box(actor, x0, x1, y0, y1, z0, z1):
    """Resize a 100 cm cube actor to the given world box."""
    actor.set_actor_location(unreal.Vector((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), False, False)
    actor.set_actor_scale3d(unreal.Vector((x1 - x0) / 100.0, (y1 - y0) / 100.0, (z1 - z0) / 100.0))


def set_label_text(label, text):
    a = by_label(label)
    if a:
        a.text_render.set_editor_property("text", text)


def spawn_bp(path, label, location, yaw=0.0, folder="Gym/Fixtures"):
    a = by_label(label)
    if not a:
        cls = unreal.load_asset(path).generated_class()
        a = EAS.spawn_actor_from_class(cls, unreal.Vector(*location), unreal.Rotator(roll=0, pitch=0, yaw=yaw))
        a.set_actor_label(label)
    a.set_actor_location_and_rotation(unreal.Vector(*location), unreal.Rotator(roll=0, pitch=0, yaw=yaw), False, False)
    a.set_folder_path(folder)
    return a


# ---- Zipline start: the tower top runs 2.5 m further toward the cable (y 150..700) ----
tower = by_label("StaticMeshActor27")
if tower:
    set_box(tower, -3150, -2850, 150, 700, 0, 600)

# ---- Zipline landing: 5 x 12 m (x -3650..-3150, y 2100..3300), top at 3 m ----
landing = by_label("StaticMeshActor36")
if landing:
    set_box(landing, -3650, -3150, 2100, 3300, 0, 300)

# ---- Ladder behind the ladder block (block y 50..550): back face, facing +Y ----
ladder = by_label("BP_Ladder")
if ladder:
    ladder.set_actor_location_and_rotation(unreal.Vector(-2400, 575, 0), unreal.Rotator(roll=0, pitch=0, yaw=90), False, False)
set_label_text("Label_LADDER", "LADDER  behind (path on the right)")

# ---- Stair from the block top (4 m) to the tower top (6 m), over the path: five 40 cm steps ----
old_bridge = by_label("StaticMeshActor28")
if old_bridge:
    EAS.destroy_actor(old_bridge)
for i in range(5):
    x1 = -2650 - 40 * i
    x0 = x1 - 40
    top = 400 + 40 * (i + 1)
    box("Gym_TowerStep%d" % i, ((x0 + x1) / 2, 300, (360 + top) / 2), (20, 80, (top - 360) / 2))

# ---- The path on the right: a gold strip from the front, under the stair, then behind the block to the ladder ----
box("Gym_PathRight", (-2750, 200, 1), (80, 500, 1), MARK, "Gym/Movement")
box("Gym_PathBehind", (-2510, 640, 1), (160, 60, 1), MARK, "Gym/Movement")

# ---- Mounted gun: no pedestal, the tripod stands on the floor ----
pedestal = by_label("StaticMeshActor72")
if pedestal:
    EAS.destroy_actor(pedestal)
gun = by_label("BP_MountedGun")
if gun:
    loc = gun.get_actor_location()
    gun.set_actor_location(unreal.Vector(loc.x, loc.y, 0), False, False)
set_label_text("Label_MOUNTED GUN  F", "MOUNTED GUN  F (tripod, place anywhere)")

# ---- Distractions: radio, TV and alarm clock side by side, each on its plinth ----
plinth = by_label("StaticMeshActor61")
if plinth:
    for label, y in (("Gym_PlinthTV", -1650), ("Gym_PlinthAlarm", -2150)):
        p = by_label(label)
        if not p:
            p = EAS.duplicate_actor(plinth)
            p.set_actor_label(label)
        p.set_actor_location(unreal.Vector(1800, y, 40), False, False)
spawn_bp("/Game/Hitmen/World/Interactables/Distraction/BP_Distraction_TV", "BP_Distraction_TV", (1800, -1650, 80))
spawn_bp("/Game/Hitmen/World/Interactables/Distraction/BP_Distraction_Alarm", "BP_Distraction_Alarm", (1800, -2150, 80))
set_label_text("Label_RADIO  noise", "DISTRACTIONS  F on / off (radio, TV, alarm)")

# ---- Body container near the guards (carry a body with F, hide it inside) ----
spawn_bp("/Game/Hitmen/World/Interactables/Container/BP_BodyContainer", "BP_BodyContainer", (300, 1100, 0), 0.0)
label = by_label("Label_BODY CONTAINER")
if not label:
    label = EAS.spawn_actor_from_class(unreal.TextRenderActor, unreal.Vector(300, 1100, 230), unreal.Rotator(roll=0, pitch=0, yaw=-90))
    label.set_actor_label("Label_BODY CONTAINER")
label.set_actor_location(unreal.Vector(300, 1100, 230), False, False)
label.text_render.set_editor_property("text", "BODY CONTAINER  F with a body")
label.text_render.set_editor_property("horizontal_alignment", unreal.HorizTextAligment.EHTA_CENTER)
label.text_render.set_editor_property("world_size", 26.0)
label.set_folder_path("Labels")

LES.save_current_level()
print("gym 0.7.0 layout done")
