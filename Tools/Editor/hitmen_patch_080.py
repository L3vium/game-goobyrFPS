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


# Gym title: over the middle of the hall, above the zone titles (520 cm), facing the spawn (south-west).
GYM_TITLE_XY = (0.0, 0.0)
GYM_TITLE_YAW = -126.0
GYM_TITLES = (("Label_HITMEN GYM", 1150.0, 320.0), ("Label_Esc: debug panel - w", 1050.0, 70.0))


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

if wanted("bodyik"):
    # Procedural body layer (crouch / slide pose, hands on ledges, punches with closed fists) in both ABPs.
    fist = load(hitmen_content.ANIMS + "/Unarmed/Attack/MM_Attack_01")
    for abp_path in ("/Game/Hitmen/Characters/Shared/Anims/ABP_HitmenCharacter", "/Game/Hitmen/Characters/Player/Anims/ABP_FirstPersonArms"):
        abp = load(abp_path)
        if abp and unreal.HitmenAnimGraphTools.add_body_ik_node(abp, fist, 0.3, True):
            save(abp)
            log("bodyik:", abp_path)
        else:
            log("WARN bodyik failed", abp_path)

if wanted("traversal_fx"):
    # Dust where the hands land on a ledge, along slides and on ladders (it was the gunshot puff); a slide scrape.
    dust = load(hitmen_content.TRAVERSAL_DUST)
    for bp_path in CHARACTERS:
        bp = load(bp_path)
        if bp and dust:
            hitmen_content.set_traversal_fx(unreal.get_default_object(bp.generated_class()), dust)
            unreal.BlueprintEditorLibrary.compile_blueprint(bp)
            save(bp)
    sound_set = load("/Game/Hitmen/Data/DA_SoundSet")
    scrape = load(hitmen_content.SLIDE_SOUND)
    if sound_set and scrape:
        sounds = dict(sound_set.get_editor_property("sounds"))
        volumes = dict(sound_set.get_editor_property("volumes"))
        sounds[unreal.HitmenSound.SLIDE] = scrape
        volumes[unreal.HitmenSound.SLIDE] = 0.45
        sound_set.set_editor_property("sounds", sounds)
        sound_set.set_editor_property("volumes", volumes)
        save(sound_set)
    log("traversal_fx: dust and slide sound")

if wanted("sightroom"):
    # Hitman Instinct: the wave sweeps far, usable objects are depth-tested (never through walls).
    hitmen_content.build_sightroom_pp()
    log("sightroom: PP_Sightroom rebuilt")

if wanted("weapon_juice"):
    # Muzzle lights ~100x weaker (they were 50-375x a room lamp), sniper trail + impact burst.
    for name in hitmen_content.WEAPON_JUICE:
        data = load("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name)
        if data:
            for key, value in hitmen_content.weapon_juice(name).items():
                data.set_editor_property(key, value)
            save(data)
    log("weapon_juice: lights, trails, bursts")

if wanted("map_cameras"):
    # Security cameras stay on the minimap only: off the pause map and its legend.
    camera = load("/Game/Hitmen/Data/MapMarkers/DA_MapMarker_Camera")
    if camera:
        camera.set_editor_property("show_on_pause_map", False)
        camera.set_editor_property("show_in_legend", False)
        save(camera)
        log("map_cameras: pause map and legend off")

if wanted("gym_title"):
    # The gym title floated like a sign on the south wall: now high over the middle of the hall, readable both ways.
    les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    les.load_level("/Game/Hitmen/Maps/L_Gym")
    labels = {a.get_actor_label(): a for a in eas.get_all_level_actors()}
    placed = 0
    for label, z, size in GYM_TITLES:
        actor = labels.get(label)
        if not actor:
            log("WARN gym_title missing", label)
            continue
        actor.set_actor_location_and_rotation(unreal.Vector(GYM_TITLE_XY[0], GYM_TITLE_XY[1], z), unreal.Rotator(roll=0.0, pitch=0.0, yaw=GYM_TITLE_YAW), False, True)
        actor.text_render.set_editor_property("world_size", size)
        # One face only: a text render draws from both sides, two back to back read as a mirrored mess.
        if labels.get(label + " (back)"):
            eas.destroy_actor(labels[label + " (back)"])
        placed += 1
    les.save_current_level()
    log("gym_title: placed", placed)

print("\n".join(LOG))
