"""0.5.0 content patch for an existing project: the builders never recreate existing assets, so 0.5.0 values
that must reach assets that already exist are applied here. Idempotent; run in the open editor (PIE stopped):
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_050.py
Every step is safe to re-run. To run some steps only, set STEPS below (empty = all)."""
import os
import sys

import unreal
sys.path.append(os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir()), "Tools", "Editor"))

import hitmen_content
from hitmen_common import log, LOG

STEPS = []
EAL = unreal.EditorAssetLibrary


def wanted(step):
    return not STEPS or step in STEPS


def clear_bp_defaults(bp_path, fields):
    bp = EAL.load_asset(bp_path) if EAL.does_asset_exist(bp_path) else None
    if not bp:
        log("WARN missing", bp_path)
        return
    default = unreal.get_default_object(bp.generated_class())
    for field in fields:
        default.set_editor_property(field, None)
    unreal.BlueprintEditorLibrary.compile_blueprint(bp)
    EAL.save_loaded_asset(bp, only_if_is_dirty=False)


if wanted("sounds"):
    # UI confirm (team change) is a soft tick instead of a machine start-up; no thunder at match start.
    hitmen_content.build_sound_set()
    log("sound set rebuilt")

if wanted("weapon_vfx"):
    # Guns carry no VFX at all (muzzle flash, impact puffs, dust, blood, back blast), nor the turret or arrows.
    for asset in EAL.list_assets("/Game/Hitmen/Data/Weapons", recursive=False):
        data = EAL.load_asset(asset)
        if isinstance(data, unreal.HitmenWeaponData):
            for field in ("muzzle_fx", "impact_fx", "surface_impact_fx", "flesh_impact_fx", "back_blast_fx"):
                data.set_editor_property(field, None)
            EAL.save_loaded_asset(data, only_if_is_dirty=False)
    clear_bp_defaults("/Game/Hitmen/World/Interactables/Security/BP_Turret", ("muzzle_fx", "impact_fx"))
    clear_bp_defaults("/Game/Hitmen/Weapons/Projectiles/BP_Projectile_Arrow", ("impact_effect",))
    log("weapon VFX cleared")

if wanted("noise"):
    # Hearing: a gunshot is heard close to the shooter only; the bullet impact is what draws NPCs.
    report = {"SMG": (600.0, 500.0), "AssaultRifle": (700.0, 600.0), "BurstRifle": (700.0, 600.0), "Shotgun": (800.0, 600.0),
              "Minigun": (900.0, 700.0), "Sniper": (800.0, 700.0), "RocketLauncher": (1200.0, 0.0), "GrenadeLauncher": (1000.0, 0.0),
              "BlackHole": (500.0, 0.0), "Pistol": (0.0, 300.0)}
    for name, (radius, impact) in report.items():
        data = EAL.load_asset("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name)
        data.set_editor_property("noise_radius", radius)
        data.set_editor_property("impact_noise_radius", impact)
        EAL.save_loaded_asset(data, only_if_is_dirty=False)
    for bp_path, field, value in (("/Game/Hitmen/Weapons/Projectiles/BP_Projectile_Rocket", "explosion_noise_radius", 1500.0),
                                  ("/Game/Hitmen/Weapons/Projectiles/BP_Projectile_Grenade", "explosion_noise_radius", 1500.0),
                                  ("/Game/Hitmen/Weapons/Projectiles/BP_Projectile_TankShell", "explosion_noise_radius", 1500.0),
                                  ("/Game/Hitmen/World/Vehicles/BP_Tank", "cannon_noise_radius", 1500.0)):
        bp = EAL.load_asset(bp_path) if EAL.does_asset_exist(bp_path) else None
        if not bp:
            log("WARN missing", bp_path)
            continue
        unreal.get_default_object(bp.generated_class()).set_editor_property(field, value)
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        EAL.save_loaded_asset(bp, only_if_is_dirty=False)
    log("noise radii patched")

if wanted("openings"):
    # Doors fill the 110 x 230 openings; windows open 80 degrees (105 cut into the wall).
    for bp_path in ("/Game/Hitmen/World/Interactables/Door/BP_Door", "/Game/Hitmen/World/Interactables/Door/BP_Door_Keycard1"):
        bp = EAL.load_asset(bp_path) if EAL.does_asset_exist(bp_path) else None
        if not bp:
            log("WARN missing", bp_path)
            continue
        mesh = unreal.get_default_object(bp.generated_class()).get_editor_property("mesh")
        mesh.set_editor_property("relative_scale3d", unreal.Vector(1.1, 0.1, 2.3))
        mesh.set_editor_property("relative_location", unreal.Vector(55.0, 0.0, 115.0))
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        EAL.save_loaded_asset(bp, only_if_is_dirty=False)
    window = EAL.load_asset("/Game/Hitmen/World/Interactables/Window/BP_Window")
    if window:
        unreal.get_default_object(window.generated_class()).set_editor_property("shutter_open_angle", 80.0)
        unreal.BlueprintEditorLibrary.compile_blueprint(window)
        EAL.save_loaded_asset(window, only_if_is_dirty=False)
    log("doors and windows patched")

if wanted("arms"):
    # First-person arms: on the right, aiming stays low right, fists higher (punches cross the view).
    player = EAL.load_asset("/Game/Hitmen/Characters/BP_PlayerCharacter") if EAL.does_asset_exist("/Game/Hitmen/Characters/BP_PlayerCharacter") else None
    if not player:
        for asset in EAL.list_assets("/Game/Hitmen/Characters", recursive=True):
            if asset.split(".")[-1] == "BP_PlayerCharacter":
                player = EAL.load_asset(asset)
    if player:
        default = unreal.get_default_object(player.generated_class())
        default.set_editor_property("arms_hip_offset", unreal.Vector(8.0, 20.0, -166.0))
        default.set_editor_property("arms_aim_offset", unreal.Vector(4.0, 11.0, -161.0))
        default.set_editor_property("arms_unarmed_offset", unreal.Vector(20.0, 6.0, -150.0))
        unreal.BlueprintEditorLibrary.compile_blueprint(player)
        EAL.save_loaded_asset(player, only_if_is_dirty=False)
        log("player arms offsets patched")
    else:
        log("WARN BP_PlayerCharacter not found")

if wanted("vehicles"):
    from hitmen_common import set_mesh, SHAPES

    def find(name, root="/Game/Hitmen"):
        for asset in EAL.list_assets(root, recursive=True):
            if asset.split(".")[-1] == name:
                return EAL.load_asset(asset)
        return None

    dark = find("MI_Dark")
    laser = find("MI_Laser")
    # Mounted gun: the barrel cylinder lies along the gun's forward axis (it pointed straight up).
    mounted = find("BP_MountedGun")
    if mounted:
        set_mesh(unreal.get_default_object(mounted.generated_class()).get_editor_property("mesh"), SHAPES["cylinder"], (0.12, 0.12, 1.4), dark, (70, 0, 0), (-90, 0, 0))
        unreal.BlueprintEditorLibrary.compile_blueprint(mounted)
        EAL.save_loaded_asset(mounted, only_if_is_dirty=False)
    # Tank: a real cannon barrel; the shell is a glowing straight slug.
    tank = find("BP_Tank")
    if tank:
        tank_cdo = unreal.get_default_object(tank.generated_class())
        set_mesh(tank_cdo.get_editor_property("barrel"), SHAPES["cylinder"], (0.32, 0.32, 3.4), dark)
        tank_cdo.set_editor_property("shell_speed", 20000.0)
        unreal.BlueprintEditorLibrary.compile_blueprint(tank)
        EAL.save_loaded_asset(tank, only_if_is_dirty=False)
    shell = find("BP_Projectile_TankShell")
    if shell:
        shell_cdo = unreal.get_default_object(shell.generated_class())
        set_mesh(shell_cdo.get_editor_property("mesh"), SHAPES["sphere"], (0.35, 0.35, 0.35), laser)
        for field, value in (("explosion_radius", 1200.0), ("explosion_damage", 260.0), ("explosion_effect_scale", 3.5),
                             ("explosion_shake_intensity", 6.0)):
            shell_cdo.set_editor_property(field, value)
        trail = EAL.load_asset("/Game/Vefects/Essential_Trails_VFX/Shared/Particles/NS_Trail_Simple_02")
        if trail:
            shell_cdo.set_editor_property("trail_effect", trail)
        movement = shell_cdo.get_editor_property("movement")
        movement.set_editor_property("projectile_gravity_scale", 0.0)
        movement.set_editor_property("rotation_follows_velocity", True)
        unreal.BlueprintEditorLibrary.compile_blueprint(shell)
        EAL.save_loaded_asset(shell, only_if_is_dirty=False)
    log("vehicles patched")

if wanted("explosions"):
    # 3D toon explosions (Anime_Stylized_VFX) instead of the 2D flipbook sheets.
    toon = "/Game/Vefects/Anime_Stylized_VFX/VFX/Particles/"
    bomb = EAL.load_asset(toon + "NS_Bomb_Explosion")
    ground = EAL.load_asset(toon + "NS_Explosion_Fire_Ground")
    ice = EAL.load_asset(toon + "NS_Explosion_Ice_Omni")
    for name, effect in (("BP_Projectile_Rocket", bomb), ("BP_Projectile_TankShell", bomb), ("BP_Projectile_C4", bomb),
                         ("BP_Projectile_Grenade", ground), ("BP_Projectile_Flashbang", ice)):
        bp = EAL.load_asset("/Game/Hitmen/Weapons/Projectiles/" + name)
        if not bp:
            log("WARN missing", name)
            continue
        unreal.get_default_object(bp.generated_class()).set_editor_property("explosion_effect", effect)
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        EAL.save_loaded_asset(bp, only_if_is_dirty=False)
    barrel = EAL.load_asset("/Game/Hitmen/World/Interactables/Barrel/BP_ExplosiveBarrel")
    if barrel:
        barrel_cdo = unreal.get_default_object(barrel.generated_class())
        barrel_cdo.set_editor_property("explosion_effect", ground)
        barrel_cdo.set_editor_property("explosion_effect_scale", 1.6)
        unreal.BlueprintEditorLibrary.compile_blueprint(barrel)
        EAL.save_loaded_asset(barrel, only_if_is_dirty=False)
    log("explosion effects patched")

if wanted("exposure"):
    # One fixed exposure on every match map: no eye adaptation when arriving or moving between light and shade.
    import hitmen_levels
    les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    current = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world().get_path_name().split(".")[0]
    for map_name in ("L_Gym", "L_LDTest", "L_Empty_01", "L_Empty_02"):
        map_path = "/Game/Hitmen/Maps/" + map_name
        if not EAL.does_asset_exist(map_path):
            continue
        les.load_level(map_path)
        hitmen_levels.match_exposure()
        les.save_current_level()
        log("fixed exposure:", map_name)
    les.load_level(current)

if wanted("difficulty"):
    hitmen_content.build_rules()
    log("match rules (difficulty presets) rebuilt")

print("\n".join(LOG))
