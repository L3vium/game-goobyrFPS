"""0.11.0 content patch for an existing project: the builders never recreate existing assets, so 0.11.0 values that
must reach assets that already exist (and the hand-edited maps L_Gym / L_LDTest) are applied here. Idempotent; run in
the open editor (PIE stopped):
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_0110.py
Every step is safe to re-run. To run some steps only:
    python Tools/Editor/ue_remote.py "import unreal; PATCH_STEPS = ['zipline']; exec(open(unreal.Paths.project_dir() + 'Tools/Editor/hitmen_patch_0110.py').read())"
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
AT = unreal.AssetToolsHelpers.get_asset_tools()
PROJECT = unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir())
SYNTH_FOLDER = "/Game/Hitmen/Audio/SFX/Synth"
MAPS = ("/Game/Hitmen/Maps/L_Gym", "/Game/Hitmen/Maps/L_LDTest")


def wanted(step):
    return not STEPS or step in STEPS


def load(asset_path):
    return EAL.load_asset(asset_path) if EAL.does_asset_exist(asset_path) else None


def save(asset):
    if asset:
        EAL.save_loaded_asset(asset, only_if_is_dirty=False)


def import_synth(names):
    """Tools/Audio/Synth/<name>.wav (Tools/Audio/hitmen_synth_sfx.py) into Audio/SFX/Synth."""
    source = os.path.join(PROJECT, "Tools", "Audio", "Synth")
    if not EAL.does_directory_exist(SYNTH_FOLDER):
        EAL.make_directory(SYNTH_FOLDER)
    tasks = []
    for name in names:
        wav = os.path.join(source, name + ".wav")
        if not os.path.isfile(wav):
            log("WARN missing", wav, "(run Tools/Audio/hitmen_synth_sfx.py)")
            continue
        task = unreal.AssetImportTask()
        task.set_editor_property("filename", wav)
        task.set_editor_property("destination_path", SYNTH_FOLDER)
        task.set_editor_property("destination_name", name)
        task.set_editor_property("automated", True)
        task.set_editor_property("replace_existing", True)
        task.set_editor_property("save", True)
        tasks.append(task)
    if tasks:
        AT.import_asset_tasks(tasks)
    return len(tasks)


LOG.clear()

if wanted("zipline"):
    # The ride: a clamp as the trolley takes the cable, then one steady cable sound for the whole ride (a seamless loop).
    imported = import_synth(["SW_ZiplineEnter", "SW_ZiplineLoop"])
    loop = load(SYNTH_FOLDER + "/SW_ZiplineLoop")
    if loop:
        loop.set_editor_property("looping", True)
        save(loop)
    S = unreal.HitmenSound
    sound_set = load("/Game/Hitmen/Data/DA_SoundSet")
    if sound_set:
        sounds = dict(sound_set.get_editor_property("sounds"))
        volumes = dict(sound_set.get_editor_property("volumes"))
        spaces = dict(sound_set.get_editor_property("spaces"))
        for event, name, volume in ((S.ZIPLINE_ENTER, "SW_ZiplineEnter", 0.7), (S.ZIPLINE_LOOP, "SW_ZiplineLoop", 0.4)):
            asset = load(SYNTH_FOLDER + "/" + name)
            if asset:
                sounds[event] = asset
                volumes[event] = volume
                spaces[event] = unreal.HitmenSoundSpace.WORLD
        sound_set.set_editor_property("sounds", sounds)
        sound_set.set_editor_property("volumes", volumes)
        sound_set.set_editor_property("spaces", spaces)
        save(sound_set)
    log("zipline:", imported, "waves imported, enter + loop in DA_SoundSet")

if wanted("voice"):
    # Proximity chat falloff for bodies and flying watchers (Project Settings > Hitmen > Watchers > VoiceAttenuation).
    asset = hitmen_content.build_voice_attenuation()
    log("voice:", "SA_Hitmen_Voice" if asset else "FAILED")

if wanted("holster"):
    # Pistols fit a hip holster: guards carry them there while calm (and they can be pickpocketed).
    pistol = load("/Game/Hitmen/Data/Weapons/DA_Weapon_Pistol")
    if pistol:
        pistol.set_editor_property("can_holster", True)
        save(pistol)
    log("holster: DA_Weapon_Pistol can_holster", bool(pistol))

if wanted("carry"):
    # Guards on duty (stationary posts) keep their gun in their hands; patrols keep a pistol holstered until alerted, and
    # so do keycard carriers (both hips to pickpocket).
    les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for map_path in MAPS:
        if not EAL.does_asset_exist(map_path):
            continue
        les.load_level(map_path)
        changed = 0
        for actor in eas.get_all_level_actors():
            if not isinstance(actor, unreal.HitmenNPC):
                continue
            stationary = actor.get_editor_property("behavior") == unreal.HitmenNPCBehavior.STATIONARY
            carrier = actor.get_editor_property("carried_keycard") >= 0
            in_hands = stationary and not carrier
            wanted_carry = unreal.HitmenNPCWeaponCarry.IN_HANDS if in_hands else unreal.HitmenNPCWeaponCarry.HOLSTERED
            if actor.get_editor_property("weapon_carry") != wanted_carry:
                actor.set_editor_property("weapon_carry", wanted_carry)
                changed += 1
        les.save_current_level()
        log("carry:", map_path.split("/")[-1], changed, "NPCs set")

print("\n".join(LOG))
