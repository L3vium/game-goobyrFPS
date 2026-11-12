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

if wanted("fp_motion"):
    # Procedural first-person motion per weapon (hip / aim deltas, equip, kick, scope).
    for name in hitmen_content.FP_MOTION:
        data = load(WEAPONS + "DA_Weapon_" + name)
        if data:
            data.set_editor_property("fp_motion", hitmen_content.fp_motion(name))
            save(data)
    log("fp motion: weapons", len(hitmen_content.FP_MOTION))

if wanted("vfx"):
    # Toon effects only (no Flipbook comic sheets, no blood): muzzle flash + light + glow, toon impacts, tracers,
    # per-weapon gun voices; flash bursts on explosions; smoke grenade Niagara; jetpack smoke; traversal juice.
    TOON = hitmen_content.TOON
    mats = hitmen_content.build_glow_materials()
    for name in hitmen_content.WEAPON_JUICE:
        data = load(WEAPONS + "DA_Weapon_" + name)
        if not data:
            continue
        for field, value in hitmen_content.weapon_juice(name).items():
            data.set_editor_property(field, value)
        muzzle = hitmen_content.weapon_muzzle_offset(data)
        if muzzle:
            data.set_editor_property("muzzle_offset", muzzle)
        save(data)

    def bp_defaults(bp_path, **props):
        bp = load(bp_path)
        if not bp:
            log("WARN missing", bp_path)
            return None
        default = unreal.get_default_object(bp.generated_class())
        for field, value in props.items():
            try:
                default.set_editor_property(field, value)
            except Exception as error:
                log("WARN", bp_path, field, error)
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        save(bp)
        return default

    vanish = load(TOON + "NS_Pickup_Cast")
    for bp_name in ("BP_Weapon_Hitscan", "BP_Weapon_Projectile", "BP_Weapon_Melee", "BP_Weapon_Shotgun"):
        bp_defaults("/Game/Hitmen/Weapons/Blueprints/" + bp_name, thrown_vanish_fx=vanish)
    proj = "/Game/Hitmen/Weapons/Projectiles/"
    bp_defaults(proj + "BP_Projectile_Flashbang", explosion_effect=None, explosion_flash_radius=650.0,
                explosion_flash_color=unreal.LinearColor(1.0, 1.0, 1.0, 1.0), explosion_flash_duration=0.45, explosion_light_intensity=400000.0)
    for name in ("BP_Projectile_Rocket", "BP_Projectile_Grenade", "BP_Projectile_C4", "BP_Projectile_TankShell"):
        bp_defaults(proj + name, explosion_flash_radius=320.0, explosion_flash_color=unreal.LinearColor(1.0, 0.55, 0.2, 1.0),
                    explosion_flash_duration=0.25, explosion_light_intensity=150000.0)
    smoke = load("/Game/Hitmen/Abilities/Actors/BP_SmokeCloud")
    if smoke:
        default = unreal.get_default_object(smoke.generated_class())
        effect = default.get_editor_property("effect")
        effect.set_editor_property("asset", load("/Game/Vefects/Smoke_Bombs_VFX/Skills/Loop/NS_SmokeBomb_Classic_L"))
        default.set_editor_property("effect_time_scale", 1.8)
        unreal.BlueprintEditorLibrary.compile_blueprint(smoke)
        save(smoke)
    bp_defaults("/Game/Hitmen/Abilities/Move/Jetpack/BP_Ability_Jetpack", thrust_fx=load("/Game/Vefects/Free_Fire/Shared/Particles/NS_Fire_Small_Smoke"))
    bp_defaults("/Game/Hitmen/Characters/Player/BP_PlayerCharacter", climb_fx=load(TOON + "NS_Gunshot_Hit"), zipline_fx=load(TOON + "NS_Gunshot_Cast"))
    bp_defaults("/Game/Hitmen/World/Vehicles/BP_Tank", muzzle_blast_fx=load(TOON + "NS_Explosion_Fire_Omni"), hull_dust_fx=load(TOON + "NS_Gunshot_Hit"))
    bp_defaults("/Game/Hitmen/World/Interactables/Security/BP_Turret", muzzle_fx=load(TOON + "NS_Gunshot_Cast"), impact_fx=load(TOON + "NS_Gunshot_Hit"))
    log("vfx: toon weapons, flashes, smoke, jetpack, traversal, tank, turret")

if wanted("abilities"):
    # Stone that rests and gets picked up, thrown wind grenade, activation effects, display names.
    TOON = hitmen_content.TOON
    SOUND_NOTES = "/Game/Vefects/Stylized_VFX/Skills/MagicAttacks/Sound/NS_SoundMagic_Hit"
    proj_folder = "/Game/Hitmen/Weapons/Projectiles/"
    stone = load(proj_folder + "BP_Projectile_Stone")
    if stone and not isinstance(unreal.get_default_object(stone.generated_class()), unreal.HitmenStone):
        unreal.BlueprintEditorLibrary.reparent_blueprint(stone, unreal.HitmenStone)
    if stone:
        d = unreal.get_default_object(stone.generated_class())
        d.set_editor_property("noise_fx", load(SOUND_NOTES))
        d.set_editor_property("land_sound", load(hitmen_content.DRY_SOUND))
        unreal.BlueprintEditorLibrary.compile_blueprint(stone)
        save(stone)
    wind = hitmen_content.make_blueprint("BP_Projectile_Wind", "/Game/Hitmen/Weapons/Projectiles", unreal.HitmenProjectile_Wind)
    if wind:
        d = hitmen_content.cdo(wind)
        hitmen_content.set_mesh(hitmen_content.component(d, "mesh"), hitmen_content.SHAPES["sphere"], (0.12, 0.12, 0.12),
                                load("/Game/Hitmen/Materials/Instances/MI_Blue") or None)
        d.set_editor_property("explosion_effect", load(TOON + "NS_Explosion_Ice_Omni"))
        d.set_editor_property("explosion_effect_scale", 1.4)
        unreal.BlueprintEditorLibrary.compile_blueprint(wind)
        save(wind)
    ab = "/Game/Hitmen/Abilities/"
    def ability_bp(rel, **props):
        bp = load(ab + rel)
        if not bp:
            log("WARN missing", rel)
            return
        d = unreal.get_default_object(bp.generated_class())
        for field, value in props.items():
            try:
                d.set_editor_property(field, value)
            except Exception as error:
                log("WARN", rel, field, error)
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        save(bp)
    ability_bp("Manipulation/WindMine/BP_Ability_WindMine", projectile_class=wind.generated_class() if wind else None)
    ability_bp("Manipulation/Lighter/BP_Ability_Lighter", activated_fx=load("/Game/Vefects/Free_Fire/Shared/Particles/NS_Fire_Small"),
               activated_fx_scale=0.7, activated_fx_duration=1.4)
    ability_bp("Manipulation/RemoteDistraction/BP_Ability_RemoteDistraction", activated_fx=load(SOUND_NOTES), activated_fx_scale=0.5)
    ability_bp("Manipulation/Charm/BP_Ability_Charm", activated_fx=load(TOON + "NS_Buff_Cast"), activated_fx_scale=0.6)
    ability_bp("Manipulation/MultiKill/BP_Ability_MultiKill", activated_fx=load(TOON + "NS_Debuff_Cast"), activated_fx_scale=0.4)
    ability_bp("Move/Transposition/BP_Ability_Transposition", activated_fx=load(TOON + "NS_Dash"), activated_fx_scale=0.6)
    ability_bp("Move/Blink/BP_Ability_Blink", activated_fx=load(TOON + "NS_Dash"), activated_fx_scale=0.6)
    data = load("/Game/Hitmen/Data/Abilities/DA_Ability_WindMine")
    if data:
        data.set_editor_property("display_name", unreal.Text("Wind grenade"))
        data.set_editor_property("description", unreal.Text("Thrown: blows everyone around away, you too."))
        save(data)
    data = load("/Game/Hitmen/Data/Abilities/DA_Ability_Trap")
    if data:
        data.set_editor_property("description", unreal.Text("Catches anyone who steps in, you too. Mash the move keys to break free."))
        save(data)
    log("abilities: stone, wind grenade, activation effects")

if wanted("bodies"):
    # The body container (hide carried bodies, 2 by default).
    mi = {name: load("/Game/Hitmen/Materials/Instances/" + name) for name in ("MI_Green", "MI_Dark")}
    hitmen_content.build_body_container("/Game/Hitmen/World/Interactables", mi)
    log("bodies: BP_BodyContainer")

if wanted("fixtures"):
    # Cameras (yellow zone projected by the light, beam, breakable), monitors (frozen frame), breaker lever, dark
    # bulbs, distraction variants, mounted gun without a pedestal.
    TOON = hitmen_content.TOON
    mats_folder = "/Game/Hitmen/Materials/Master"
    projection = hitmen_content.build_camera_projection(mats_folder)
    hitmen_content.build_screen_material(mats_folder)
    W = "/Game/Hitmen/World/Interactables/"

    def bp_set(bp_path, **props):
        bp = load(bp_path)
        if not bp:
            log("WARN missing", bp_path)
            return None
        d = unreal.get_default_object(bp.generated_class())
        for field, value in props.items():
            try:
                d.set_editor_property(field, value)
            except Exception as error:
                log("WARN", bp_path, field, error)
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        save(bp)
        return d

    ring = hitmen_content.build_camera_ring()
    bp_set(W + "Security/BP_SecurityCamera", projection_material=None, show_floor_ring=True, beam_scattering=25.0, view_light_intensity=9000.0,
           broken_fx=load(TOON + "NS_Lightning_Hit"), suspicious_color=unreal.LinearColor(1.0, 0.78, 0.1, 0.4))
    breaker = load(W + "Power/BP_CircuitBreaker")
    if breaker:
        d = unreal.get_default_object(breaker.generated_class())
        hitmen_content.set_mesh(d.get_editor_property("lever"), hitmen_content.SHAPES["cube"], (0.06, 0.06, 0.32),
                                load("/Game/Hitmen/Materials/Instances/MI_Red"), (14, 0, 150))
        d.set_editor_property("switch_fx", load(TOON + "NS_Lightning_Hit"))
        unreal.BlueprintEditorLibrary.compile_blueprint(breaker)
        save(breaker)
    bp_set(W + "Power/BP_LightFixture", off_material=load("/Game/Hitmen/Materials/Instances/MI_Dark"))
    hitmen_content.build_distractions(W + "Distraction")
    mounted = load("/Game/Hitmen/World/Vehicles/BP_MountedGun")
    if mounted:
        d = unreal.get_default_object(mounted.generated_class())
        hitmen_content.set_mesh(d.get_editor_property("tripod"), hitmen_content.SHAPES["cone"], (0.9, 0.9, 1.2),
                                load("/Game/Hitmen/Materials/Instances/MI_Metal"), (0, 0, 60))
        unreal.BlueprintEditorLibrary.compile_blueprint(mounted)
        save(mounted)
    log("fixtures: cameras, monitors, breaker, lamps, distractions, mounted gun")

if wanted("sound"):
    # Attenuation per sound space (steps, gunshots, impacts, world) and the space of each sound-set entry.
    hitmen_content.build_sound_set()
    log("sound: set rebuilt with attenuations")

print("\n".join(LOG))
