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

if wanted("weapons"):
    # Falloff ranges, pellets, spreads, guard engage ranges, minigun damage, the sniper's smoke trail; per-weapon headshot
    # kills keep their default (on).
    hitmen_content.build_smoke_trail_material()
    for name in hitmen_content.WEAPON_JUICE:
        data = load("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name)
        if not data:
            continue
        rng = data.get_editor_property("range")
        values = dict(falloff_start_range=rng * 0.5, falloff_end_range=rng)
        values.update(hitmen_content.WEAPON_BALANCE.get(name, {}))
        for key, value in values.items():
            data.set_editor_property(key, value)
        if name == "Sniper":
            data.set_editor_property("tracer_style", hitmen_content.weapon_juice("Sniper")["tracer_style"])
        save(data)
    log("weapons: balance, falloff, pellets, sniper smoke")

if wanted("sniper_glint"):
    # Red star at every sniper's scope (the guard Blueprint carries it; only snipers show it).
    glint = hitmen_content.build_glint_material()
    for bp_path in ("/Game/Hitmen/Characters/NPC/BP_NPC_Guard", "/Game/Hitmen/Characters/NPC/BP_NPC_Sniper"):
        bp = load(bp_path)
        if bp:
            unreal.get_default_object(bp.generated_class()).set_editor_property("glint_material", glint)
            unreal.BlueprintEditorLibrary.compile_blueprint(bp)
            save(bp)
    log("sniper_glint: M_Hitmen_Glint")

if wanted("mounted_gun"):
    # The shield plate sits low so the barrel shows in the first-person view.
    mounted = load("/Game/Hitmen/World/Vehicles/BP_MountedGun")
    if mounted:
        d = unreal.get_default_object(mounted.generated_class())
        hitmen_content.set_mesh(d.get_editor_property("shield"), hitmen_content.SHAPES["cube"], hitmen_content.MOUNTED_GUN_SHIELD[1],
                                load("/Game/Hitmen/Materials/Instances/MI_Metal"), hitmen_content.MOUNTED_GUN_SHIELD[0])
        unreal.BlueprintEditorLibrary.compile_blueprint(mounted)
        save(mounted)
    log("mounted_gun: shield lowered")

if wanted("bodies"):
    # The hollow body container (its walls come from its size in C++), the mounted gun's impact puff.
    W = "/Game/Hitmen/World/Interactables"
    mi = {name: load("/Game/Hitmen/Materials/Instances/" + name) for name in ("MI_Green", "MI_Dark")}
    hitmen_content.build_body_container(W, mi)
    mounted = load("/Game/Hitmen/World/Vehicles/BP_MountedGun")
    if mounted:
        unreal.get_default_object(mounted.generated_class()).set_editor_property("impact_fx", load(hitmen_content.TOON + "NS_Gunshot_Hit"))
        unreal.BlueprintEditorLibrary.compile_blueprint(mounted)
        save(mounted)
    log("bodies: body container, mounted gun impacts")

if wanted("vip"):
    # The dead VIP: PP_Markers (yellow body / ghost, gold retrieval points), the target-eliminated sound, extraction
    # models (helicopter, car, crate) for retrieval points.
    hitmen_content.build_markers_pp()
    sound_set = load("/Game/Hitmen/Data/DA_SoundSet")
    if sound_set:
        sounds = dict(sound_set.get_editor_property("sounds"))
        volumes = dict(sound_set.get_editor_property("volumes"))
        sounds[unreal.HitmenSound.TARGET_ELIMINATED] = load("/Game/HeartBeats/Action/Cue/Slowdown3_Cue")
        volumes[unreal.HitmenSound.TARGET_ELIMINATED] = 0.9
        sound_set.set_editor_property("sounds", sounds)
        sound_set.set_editor_property("volumes", volumes)
        save(sound_set)
    mi = {n: load("/Game/Hitmen/Materials/Instances/" + n) for n in ("MI_Dark", "MI_Metal", "MI_Glass", "MI_Red", "MI_Wood")}
    made = hitmen_content.build_retrieval_variants("/Game/Hitmen/World/Spawning", mi)
    log("vip: PP_Markers, sound, retrieval variants", ", ".join(sorted(made)))

if wanted("ai"):
    # Louder unsilenced gunshots, three guards answer a shot, guards per weapon, NPC names.
    for name, values in hitmen_content.WEAPON_BALANCE.items():
        data = load("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name)
        if data and "noise_radius" in values:
            data.set_editor_property("noise_radius", values["noise_radius"])
            data.set_editor_property("impact_noise_radius", values.get("impact_noise_radius", data.get_editor_property("impact_noise_radius")))
            save(data)
    for asset in EAL.list_assets("/Game/Hitmen/Data/Rules", recursive=True, include_folder=False):
        rules = EAL.load_asset(asset)
        if isinstance(rules, unreal.HitmenMatchRules):
            hitmen_content.set_gunshot_responders(rules)
            save(rules)
    made = hitmen_content.build_guard_loadouts()
    log("ai: gunshot radii and responders, guards", ", ".join(sorted(made)))

if wanted("gym_ai"):
    # The gym's AI pen, one of each kind: static sentry, stationary, lone patroller, a group of 4 on one route,
    # keycard guard, sniper, VIP candidate (replaces the pen's NPCs, routes, groups and labels; walls, pillar, tower,
    # lockers, grass and the camera stay).
    import hitmen_levels
    importlib.reload(hitmen_levels)
    les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    les.load_level("/Game/Hitmen/Maps/L_Gym")
    x0, x1, y0, y1 = hitmen_levels.GYM_AI_PEN
    removed = 0
    for actor in eas.get_all_level_actors():
        loc = actor.get_actor_location()
        if not (x0 - 50 <= loc.x <= x1 + 50 and y0 - 50 <= loc.y <= y1 + 50):
            continue
        is_label = isinstance(actor, unreal.TextRenderActor) and loc.z > 60
        is_ai = isinstance(actor, (unreal.HitmenNPC, unreal.HitmenPatrolRoute, unreal.HitmenGuardGroup, unreal.HitmenDistraction))
        if is_label or is_ai:
            eas.destroy_actor(actor)
            removed += 1
    hitmen_levels.gym_ai_pen("Gym/AI")
    les.save_current_level()
    log("gym_ai: removed", removed, "placed the new pen")

if wanted("map_layers"):
    # Marker kinds get a map layer (cameras on the NPC layer, now in the pause map legend when that layer is on); the
    # interactable kinds and their Blueprints (Interactables layer, off by default on both maps).
    hitmen_content.build_map_markers()
    log("map_layers: marker kinds and interactable markers")

if wanted("sightroom"):
    # PP_Sightroom fades highlights per pixel (object distance to you, wave front, depth test): rebuilt.
    hitmen_content.build_sightroom_pp()
    log("sightroom: PP_Sightroom rebuilt")

if wanted("watchers"):
    # Watcher keys (E up / A down, 1 / 2 / 3 views, N / B overlays), the spectator pawn's scan and focus overlay, players
    # outlined for watchers in PP_Markers (stencil 7).
    actions, imc, spectator_imc = hitmen_content.build_input()
    pc_bp = load("/Game/Hitmen/Core/BP_PlayerController")
    if pc_bp:
        d = unreal.get_default_object(pc_bp.generated_class())
        hitmen_content.set_props(d, spectator_mapping_context=spectator_imc, spectator_mode_action=actions["IA_SpectatorMode"],
                                 **hitmen_content.watcher_actions(actions))
        unreal.BlueprintEditorLibrary.compile_blueprint(pc_bp)
        save(pc_bp)
    spectator_bp = load("/Game/Hitmen/Core/BP_SpectatorPawn")
    if spectator_bp:
        hitmen_content.set_spectator_pawn(spectator_bp)
        save(spectator_bp)
    hitmen_content.build_markers_pp()
    log("watchers: input, spectator pawn, PP_Markers")

if wanted("ld_fixtures"):
    # Tile (PM_Tile) for floors, the light switch, the weapon chest, keycard doors 2 and 3; their map markers.
    physical = {name: load("/Game/Hitmen/Physics/" + name) for name in set(hitmen_content.MATERIAL_SURFACES.values())}
    color = load("/Game/Hitmen/Materials/Master/M_Hitmen_Color")
    made = hitmen_content.palette_instance("MI_Tile", (0.74, 0.72, 0.68), color, "/Game/Hitmen/Materials/Instances", physical)
    mi = {name: load("/Game/Hitmen/Materials/Instances/" + name) for name in ("MI_White", "MI_Dark", "MI_Blue", "MI_Red", "MI_Yellow")}
    made.update(hitmen_content.build_ld_fixtures(mi))
    hitmen_content.build_map_markers()
    log("ld_fixtures:", ", ".join(sorted(made)))

print("\n".join(LOG))
