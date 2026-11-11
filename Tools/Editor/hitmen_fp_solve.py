"""First-person weapon hold solver (PIE running, player alive with first-person arms).
For every weapon: equips it, reads the visible mesh's barrel (longest bounds axis, toward the side the mesh
extends to from its grip pivot) and top (second longest), and computes the FirstPersonAttachOffset rotation
that points the barrel along the camera with the top up. (0.4.0 used the world-space bounds centre and
saved a roll that laid the guns on their side; this one works in the mesh's own frame.)
solve() applies it live and returns {weapon: (pitch, yaw, roll)}; save(results) writes the data assets
(PIE stopped). Run:
    python Tools/Editor/ue_remote.py "exec(open(r'<project>/Tools/Editor/hitmen_fp_solve.py').read()); print(solve())"
"""
import unreal

WEAPONS = ("Pistol", "SMG", "AssaultRifle", "BurstRifle", "Shotgun", "Minigun", "Sniper", "Bow",
           "RocketLauncher", "GrenadeLauncher", "BlackHole", "Sword")
SOCKET = "HandGrip_R"


def _world():
    return unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()


def _axes(comp):
    return [comp.get_forward_vector(), comp.get_right_vector(), comp.get_up_vector()]


def _mesh_of(comp):
    if isinstance(comp, unreal.StaticMeshComponent):
        return comp.get_editor_property("static_mesh")
    if isinstance(comp, unreal.SkeletalMeshComponent):
        return comp.get_editor_property("skeletal_mesh_asset")
    return None


def _equip(inv, data):
    inv.give_weapon(data, -1, True)
    for index, slot in enumerate(inv.get_slots()):
        if slot.get_editor_property("weapon") == data:
            inv.select_slot(index)
            break
    return inv.get_active_weapon()


def _mesh_frame(comp):
    """Barrel (+X') and top (+Z') of the mesh in its own local space: the longest bounds axis points to the
    side the mesh extends to from its pivot (the grip), the second longest is the height, signed the same way."""
    bounds = _mesh_of(comp).get_bounds()
    ext = [bounds.box_extent.x, bounds.box_extent.y, bounds.box_extent.z]
    org = [bounds.origin.x, bounds.origin.y, bounds.origin.z]
    order = sorted(range(3), key=lambda i: -ext[i])
    unit = [unreal.Vector(1, 0, 0), unreal.Vector(0, 1, 0), unreal.Vector(0, 0, 1)]
    barrel = unit[order[0]] * (1.0 if org[order[0]] >= 0.0 else -1.0)
    top = unit[order[1]] * (1.0 if org[order[1]] >= 0.0 else -1.0)
    return unreal.MathLibrary.make_rot_from_xz(barrel, top).quaternion()


def solve(names=WEAPONS, apply=True):
    """Computes, for each weapon, the FirstPersonAttachOffset rotation (relative to the hand socket) that puts the
    mesh barrel along the camera and its top up. Pure math from the current pose: no bounds-centre guess."""
    world = _world()
    pc = unreal.GameplayStatics.get_player_controller(world, 0)
    pc.set_control_rotation(unreal.Rotator(pitch=0.0, yaw=pc.get_control_rotation().yaw, roll=0.0))
    pawn = pc.get_controlled_pawn()
    inv = pawn.get_component_by_class(unreal.HitmenInventoryComponent)
    arms = pawn.get_first_person_mesh()
    results = {}
    for name in names:
        data = unreal.load_asset("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name)
        weapon = _equip(inv, data)
        if not weapon:
            print("no weapon", name)
            continue
        comps = [c for c in weapon.get_components_by_class(unreal.MeshComponent) if c.is_visible() and _mesh_of(c)]
        if not comps:
            print("no visible mesh", name)
            continue
        comp = comps[0]
        cam = pc.player_camera_manager.get_camera_rotation()
        q_cam = unreal.Rotator(pitch=0.0, yaw=cam.yaw, roll=0.0).quaternion()
        q_mesh_want = q_cam.multiply(_mesh_frame(comp).inversed())
        root = weapon.root_component
        q_root = root.get_world_rotation().quaternion()
        q_mesh_in_root = q_root.inversed().multiply(comp.get_world_rotation().quaternion())
        q_root_want = q_mesh_want.multiply(q_mesh_in_root.inversed())
        q_socket = arms.get_socket_rotation(SOCKET).quaternion()
        relative = q_socket.inversed().multiply(q_root_want).rotator()
        if apply:
            root.set_relative_rotation(relative, False, False)
        results[name] = (round(relative.pitch, 1), round(relative.yaw, 1), round(relative.roll, 1))
    return results


def save(results):
    """Writes the solved rotations into each weapon's FirstPersonAttachOffset (translation / scale kept)."""
    for name, (pitch, yaw, roll) in results.items():
        data = unreal.load_asset("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name)
        offset = data.get_editor_property("first_person_attach_offset")
        offset.rotation = unreal.Rotator(pitch=pitch, yaw=yaw, roll=roll).quaternion()
        data.set_editor_property("first_person_attach_offset", offset)
        unreal.EditorAssetLibrary.save_loaded_asset(data)
        print("saved", name, pitch, yaw, roll)
