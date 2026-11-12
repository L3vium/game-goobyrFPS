"""0.7.0 content patch for an existing project: the builders never recreate existing assets, so 0.7.0 values that
must reach assets that already exist are applied here. Idempotent; run in the open editor (PIE stopped):
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_070.py
Every step is safe to re-run. To run some steps only:
    python Tools/Editor/ue_remote.py "PATCH_STEPS = ['rules']; exec(open(r'Tools/Editor/hitmen_patch_070.py').read())"
"""
import importlib
import os
import sys

import unreal
HERE = os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir()), "Tools", "Editor")
if HERE not in sys.path:
    sys.path.append(HERE)

import hitmen_common
import hitmen_content
importlib.reload(hitmen_common)
importlib.reload(hitmen_content)
from hitmen_common import log, LOG

STEPS = globals().get("PATCH_STEPS", [])
EAL = unreal.EditorAssetLibrary
WEAPONS = "/Game/Hitmen/Data/Weapons/"


def wanted(step):
    return not STEPS or step in STEPS


def load(asset_path):
    return EAL.load_asset(asset_path) if EAL.does_asset_exist(asset_path) else None


def save(asset):
    if asset:
        EAL.save_loaded_asset(asset, only_if_is_dirty=False)


def open_map(name):
    """Loads /Game/Hitmen/Maps/<name> in the editor (PIE must be stopped). Returns the editor world."""
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).load_level("/Game/Hitmen/Maps/" + name)
    return unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()


def save_map():
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()


def actors():
    return unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors()


LOG.clear()

if wanted("rules"):
    # Guards hit harder (players have 150 HP now).
    rules = load("/Game/Hitmen/Data/Rules/DA_MatchRules_Default")
    if rules:
        rules.set_editor_property("difficulty_presets", hitmen_content.difficulty_presets())
        save(rules)
        log("rules: difficulty presets")

if wanted("minigun"):
    # Fires at once at 450 rpm and ramps to 1100 in 1.2 s (was: 0.8 s of silence, then 1500).
    data = load(WEAPONS + "DA_Weapon_Minigun")
    if data:
        for field, value in (("rounds_per_minute", 1100.0), ("spin_up_time", 1.2), ("spin_up_start_rpm", 450.0), ("spin_down_grace", 0.4)):
            data.set_editor_property(field, value)
        save(data)
        log("minigun: ramp")

if wanted("burst_rifle"):
    # The burst rifle is gone: catalog, gym pedestal (pickup, block, label), data asset.
    burst = load(WEAPONS + "DA_Weapon_BurstRifle")
    catalog = load("/Game/Hitmen/Data/DA_Catalog")
    if burst and catalog:
        for field in ("weapons", "loot_weapons"):
            catalog.set_editor_property(field, [w for w in catalog.get_editor_property(field) if w != burst])
        save(catalog)
    if burst and EAL.does_asset_exist("/Game/Hitmen/Maps/L_Gym"):
        open_map("L_Gym")
        spots = []
        for actor in actors():
            if isinstance(actor, unreal.HitmenWeaponPickup) and actor.get_editor_property("weapon") == burst:
                spots.append(actor.get_actor_location())
                actor.destroy_actor()
        for actor in actors():
            loc = actor.get_actor_location()
            near = any(abs(loc.x - p.x) < 80 and abs(loc.y - p.y) < 80 for p in spots)
            if near and (isinstance(actor, unreal.TextRenderActor) or isinstance(actor, unreal.StaticMeshActor)):
                actor.destroy_actor()
        save_map()
        log("burst rifle: gym pedestals removed", len(spots))
    if burst:
        EAL.delete_asset(WEAPONS + "DA_Weapon_BurstRifle")
        icon = "/Game/Hitmen/UI/Icons/Weapons/T_Icon_Weapon_BurstRifle"
        if EAL.does_asset_exist(icon):
            EAL.delete_asset(icon)
        log("burst rifle: deleted")

if wanted("sound"):
    # Attenuation per sound space (steps, gunshots, impacts, world) and the space of each sound-set entry.
    hitmen_content.build_sound_set()
    log("sound: set rebuilt with attenuations")

print("\n".join(LOG))
