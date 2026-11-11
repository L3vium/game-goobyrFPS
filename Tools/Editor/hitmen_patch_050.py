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

if wanted("difficulty"):
    hitmen_content.build_rules()
    log("match rules (difficulty presets) rebuilt")

print("\n".join(LOG))
