"""First-person weapon hold solver (PIE running, player alive with first-person arms).
For every weapon: equips it, finds the visible mesh's long axis (barrel / blade) and its height axis,
signed away from the hand (the longer side of a gun is the barrel, the grip hangs below), and computes
the FirstPersonAttachOffset rotation that points the barrel along the camera with the top up.
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


def solve(names=WEAPONS):
    world = _world()
    pc = unreal.GameplayStatics.get_player_controller(world, 0)
    pawn = pc.get_controlled_pawn()
    inv = pawn.get_component_by_class(unreal.HitmenInventoryComponent)
    arms = pawn.get_first_person_mesh()
    cam_rot = pc.player_camera_manager.get_camera_rotation()
    cam_fwd = unreal.MathLibrary.get_forward_vector(cam_rot)
    cam_up = unreal.MathLibrary.get_up_vector(cam_rot)
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
        bounds = _mesh_of(comp).get_bounds()
        ext = [bounds.box_extent.x, bounds.box_extent.y, bounds.box_extent.z]
        order = sorted(range(3), key=lambda i: -ext[i])
        long_axis, up_axis = order[0], order[1]
        axes = _axes(comp)
        hand = arms.get_socket_location(SOCKET)
        center = comp.get_world_transform().transform_location(bounds.origin)
        to_center = center - hand
        barrel = axes[long_axis] * (1.0 if to_center.dot(axes[long_axis]) >= 0.0 else -1.0)
        top = axes[up_axis] * (1.0 if to_center.dot(axes[up_axis]) >= 0.0 else -1.0)
        now = unreal.MathLibrary.make_rot_from_xz(barrel, top).quaternion()
        want = unreal.MathLibrary.make_rot_from_xz(cam_fwd, cam_up).quaternion()
        delta = want.multiply(now.inversed())
        new_world = delta.multiply(weapon.get_actor_rotation().quaternion())
        socket_rot = arms.get_socket_rotation(SOCKET).quaternion()
        relative = socket_rot.inversed().multiply(new_world).rotator()
        weapon.root_component.set_relative_rotation(relative, False, False)
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
