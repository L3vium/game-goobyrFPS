"""0.9.0 content patch for an existing project: the builders never recreate existing assets, so 0.9.0 values that
must reach assets that already exist are applied here. Idempotent; run in the open editor (PIE stopped):
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_090.py
Every step is safe to re-run. To run some steps only:
    python Tools/Editor/ue_remote.py "import unreal; PATCH_STEPS = ['surfaces']; exec(open(unreal.Paths.project_dir() + 'Tools/Editor/hitmen_patch_090.py').read())"
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
CHARACTERS = ("/Game/Hitmen/Characters/Player/BP_PlayerCharacter", "/Game/Hitmen/Characters/NPC/BP_NPC_Guard",
              "/Game/Hitmen/Characters/NPC/BP_NPC_Sniper", "/Game/Hitmen/Characters/NPC/BP_NPC_VIPCandidate")


def wanted(step):
    return not STEPS or step in STEPS


def load(asset_path):
    return EAL.load_asset(asset_path) if EAL.does_asset_exist(asset_path) else None


def save(asset):
    if asset:
        EAL.save_loaded_asset(asset, only_if_is_dirty=False)


def each_character_cdo(fn):
    for bp_path in CHARACTERS:
        bp = load(bp_path)
        if not bp:
            log("WARN missing", bp_path)
            continue
        fn(unreal.get_default_object(bp.generated_class()))
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        save(bp)


LOG.clear()

if wanted("weapon_light"):
    # The muzzle light only lives with a flash (code fix) and is half as strong as 0.8.0.
    for name in hitmen_content.WEAPON_JUICE:
        data = load("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name)
        if data:
            data.set_editor_property("muzzle_light_intensity", hitmen_content.WEAPON_JUICE[name][3])
            save(data)
    log("weapon_light: intensities halved")

if wanted("surfaces"):
    # One surface table in DA_SoundSet for every character (+ Tile); the characters' own maps are emptied.
    physical = hitmen_content.build_physical_materials()
    log("surfaces: physical materials", ", ".join(sorted(physical)))
    sound_set = load("/Game/Hitmen/Data/DA_SoundSet")
    if sound_set:
        hitmen_content.set_surface_table(sound_set)
        save(sound_set)
        log("surfaces: DA_SoundSet table,", len(sound_set.get_editor_property("surfaces")), "rows")
    each_character_cdo(hitmen_content.set_footstep_sounds)

if wanted("traversal_fx"):
    # Ladder puffs up and down (both off by default in Project Settings -> Hitmen -> Juice, like slides and steps).
    dust = load(hitmen_content.TRAVERSAL_DUST)
    each_character_cdo(lambda c: hitmen_content.set_traversal_fx(c, dust))
    log("traversal_fx: ladder descend slot")

print("\n".join(LOG))
