"""0.8.0 content patch for an existing project: the builders never recreate existing assets, so 0.8.0 values that
must reach assets that already exist are applied here. Idempotent; run in the open editor (PIE stopped):
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_080.py
Every step is safe to re-run. To run some steps only:
    python Tools/Editor/ue_remote.py "PATCH_STEPS = ['footsteps']; exec(open(r'Tools/Editor/hitmen_patch_080.py').read())"
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


LOG.clear()

if wanted("footsteps"):
    # Kenney steps (five takes per surface, random without repeats) and landing thumps for the player and every NPC.
    for bp_path in CHARACTERS:
        bp = load(bp_path)
        if not bp:
            log("WARN missing", bp_path)
            continue
        hitmen_content.set_footstep_sounds(unreal.get_default_object(bp.generated_class()))
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        save(bp)
    sound_set = load("/Game/Hitmen/Data/DA_SoundSet")
    if sound_set:
        sounds = dict(sound_set.get_editor_property("sounds"))
        S = unreal.HitmenSound
        for event, name in ((S.LANDING, "SW_Land_Soft_00"), (S.LANDING_HARD, "SW_Land_Heavy_00")):
            wave = load("/Game/Hitmen/Audio/SFX/Footsteps/" + name)
            if wave:
                sounds[event] = wave
        sound_set.set_editor_property("sounds", sounds)
        save(sound_set)
    log("footsteps: Kenney variations and landings")

if wanted("surfaces"):
    # Rocks and mountains sound like stone (they had no physical material: concrete).
    inst = "/Game/Hitmen/Materials/Instances/"
    for name, surface in hitmen_content.MATERIAL_SURFACES.items():
        pm = load("/Game/Hitmen/Physics/" + surface)
        for mi_name in (name, "MI_Grid_" + name[3:]):
            mi = load(inst + mi_name)
            if mi and pm and mi.get_editor_property("phys_material") != pm:
                mi.set_editor_property("phys_material", pm)
                save(mi)
                log("surfaces:", mi_name, "->", surface)

if wanted("aim"):
    # Faster aim: the sniper's raise / wobble / zoom sequence takes ~0.2 s instead of ~0.8 s.
    data = load("/Game/Hitmen/Data/Weapons/DA_Weapon_Sniper")
    if data:
        data.set_editor_property("fp_motion", hitmen_content.fp_motion("Sniper"))
        save(data)
        log("aim: sniper scope sequence")

print("\n".join(LOG))
