"""Hitmen level builder: every map is generated from engine shapes + the checker material.
Re-running rebuilds the maps from scratch (hand edits to these maps are overwritten)."""
import math
import os
import random
import unreal
from hitmen_common import *

LES = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
UES = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)

GRID = "/Game/LevelPrototyping/Materials/MI_PrototypeGrid_Gray"
GRID_DARK = "/Game/LevelPrototyping/Materials/MI_PrototypeGrid_TopDark"
GRID_ALT = "/Game/LevelPrototyping/Materials/MI_PrototypeGrid_Gray_02"


def mi(name):
    """Level geometry uses the checker twin of each colour (MI_Red -> MI_Grid_Red)."""
    return path("Materials", "Instances", "MI_Grid_" + name[3:] if name.startswith("MI_") else name)


def bp(rel):
    asset = load(path(rel))
    if asset is None:
        log("WARN missing blueprint", rel)
        return None
    return asset.generated_class()


# ---------------------------------------------------------------------------------------------
# Spawning helpers
# ---------------------------------------------------------------------------------------------

def new_map(name):
    asset_path = path("Maps", name)
    if EAL.does_asset_exist(asset_path):
        # Rebuild in place: clear every generated actor (keep world settings / default brush).
        LES.load_level(asset_path)
        for actor in EAS.get_all_level_actors():
            if not isinstance(actor, unreal.WorldSettings) and type(actor) is not unreal.Brush:
                EAS.destroy_actor(actor)
    else:
        LES.new_level(asset_path)
    log("map", name)


def spawn(cls, loc, rot=(0, 0, 0), label=None, folder=None):
    if cls is None:
        return None
    actor = EAS.spawn_actor_from_class(cls, unreal.Vector(*loc), unreal.Rotator(pitch=rot[0], yaw=rot[1], roll=rot[2]))
    if actor:
        if label:
            actor.set_actor_label(label)
        if folder:
            actor.set_folder_path(folder)
    return actor


def box(center, size, material=GRID, yaw=0.0, label=None, folder="Geometry", hidden=False, shape="cube"):
    """Axis box of `size` (cm) centred at `center`, rotated by yaw."""
    actor = spawn(unreal.StaticMeshActor, center, (0, yaw, 0), label, folder)
    comp = actor.static_mesh_component
    comp.set_static_mesh(load(SHAPES[shape]))
    actor.set_actor_scale3d(unreal.Vector(size[0] / 100.0, size[1] / 100.0, size[2] / 100.0))
    mat = load(material) if isinstance(material, str) else material
    if mat:
        comp.set_material(0, mat)
    if hidden:
        actor.set_actor_hidden_in_game(True)
    return actor


def floor(center, size, material=GRID, folder="Geometry"):
    return box((center[0], center[1], center[2] - 10), (size[0], size[1], 20), material, folder=folder)


def text(loc, string, size=90, yaw=-90.0, color=(1, 1, 1), folder="Labels"):
    actor = spawn(unreal.TextRenderActor, loc, (0, yaw, 0), "Label_" + string[:20], folder)
    comp = actor.text_render
    comp.set_text(string)
    comp.set_editor_property("world_size", size)
    comp.set_editor_property("horizontal_alignment", unreal.HorizTextAligment.EHTA_CENTER)
    comp.set_editor_property("text_render_color", unreal.Color(r=int(color[0] * 255), g=int(color[1] * 255), b=int(color[2] * 255), a=255))
    return actor


def wall(start, end, z0, height, thickness=25, openings=(), material=GRID, folder="Geometry"):
    """Wall from start to end (XY) with openings [(distance from start, width, sill, top)]."""
    sx, sy = start
    ex, ey = end
    length = math.hypot(ex - sx, ey - sy)
    if length < 1:
        return
    dx, dy = (ex - sx) / length, (ey - sy) / length
    yaw = math.degrees(math.atan2(dy, dx))

    def segment(t0, t1, zb, zt):
        if t1 - t0 < 1 or zt - zb < 1:
            return
        mid = (t0 + t1) / 2.0
        box((sx + dx * mid, sy + dy * mid, (zb + zt) / 2.0), (t1 - t0, thickness, zt - zb), material, yaw, folder=folder)

    cursor = 0.0
    for center, width, sill, top in sorted(openings):
        a, b = center - width / 2.0, center + width / 2.0
        segment(cursor, a, z0, z0 + height)
        segment(a, b, z0, z0 + sill)
        segment(a, b, z0 + top, z0 + height)
        cursor = b
    segment(cursor, length, z0, z0 + height)


def stairs(start, direction_yaw, rise, run, width=200, steps=None, material=GRID, folder="Geometry"):
    steps = steps or max(4, int(rise / 20))
    step_rise = rise / steps
    step_run = run / steps
    rad = math.radians(direction_yaw)
    dx, dy = math.cos(rad), math.sin(rad)
    for i in range(steps):
        h = step_rise * (i + 1)
        cx = start[0] + dx * step_run * (i + 0.5)
        cy = start[1] + dy * step_run * (i + 0.5)
        box((cx, cy, start[2] + h / 2.0), (step_run, width, h), material, direction_yaw, folder=folder)


# Match maps: one fixed exposure (no eye adaptation: arriving on the map, entering a room or leaving a cave never
# makes the image drift). Rooms are lit by light fixtures for it. EV100, ExtendDefaultLuminanceRange on.
MATCH_EV100 = -0.3


def match_exposure(ev100=MATCH_EV100):
    """Unbound post process volume with min = max exposure on a match map."""
    volume = None
    for actor in unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors():
        if isinstance(actor, unreal.PostProcessVolume) and actor.get_actor_label() == "MatchExposure":
            volume = actor
    if volume is None:
        volume = spawn(unreal.PostProcessVolume, (0, 0, 500), label="MatchExposure", folder="Lighting")
    volume.set_editor_property("unbound", True)
    settings = volume.get_editor_property("settings")
    settings.set_editor_property("override_auto_exposure_method", True)
    settings.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_HISTOGRAM)
    settings.set_editor_property("override_auto_exposure_min_brightness", True)
    settings.set_editor_property("auto_exposure_min_brightness", ev100)
    settings.set_editor_property("override_auto_exposure_max_brightness", True)
    settings.set_editor_property("auto_exposure_max_brightness", ev100)
    settings.set_editor_property("override_auto_exposure_bias", True)
    settings.set_editor_property("auto_exposure_bias", 0.0)
    volume.set_editor_property("settings", settings)
    return volume


def environment(sun_pitch=-40.0, sun_yaw=30.0, fog=0.01, intensity=8.0, fixed_exposure=True):
    if fixed_exposure:
        match_exposure()
    sun = spawn(unreal.DirectionalLight, (0, 0, 2000), (sun_pitch, sun_yaw, 0), "Sun", "Lighting")
    light = sun.light_component
    light.set_editor_property("intensity", intensity)
    light.set_editor_property("atmosphere_sun_light", True)
    light.set_mobility(unreal.ComponentMobility.MOVABLE)
    spawn(unreal.SkyAtmosphere, (0, 0, 0), label="SkyAtmosphere", folder="Lighting")
    sky = spawn(unreal.SkyLight, (0, 0, 500), label="SkyLight", folder="Lighting")
    sky.light_component.set_editor_property("real_time_capture", True)
    sky.light_component.set_mobility(unreal.ComponentMobility.MOVABLE)
    height_fog = spawn(unreal.ExponentialHeightFog, (0, 0, 0), label="Fog", folder="Lighting")
    height_fog.component.set_editor_property("fog_density", fog)
    spawn(unreal.VolumetricCloud, (0, 0, 0), label="Clouds", folder="Lighting")


def nav_bounds(center, extent):
    volume = spawn(unreal.NavMeshBoundsVolume, center, label="NavMeshBounds", folder="Gameplay")
    if volume:
        volume.set_actor_scale3d(unreal.Vector(extent[0] / 100.0, extent[1] / 100.0, extent[2] / 100.0))
    return volume


def game_mode(name):
    world = UES.get_editor_world()
    settings = world.get_world_settings()
    settings.set_editor_property("default_game_mode", bp("Core/" + name))


def save_map():
    LES.save_current_level()


def set_mesh_height(actor, height, comp_name="mesh"):
    comp = actor.get_editor_property(comp_name)
    if comp:
        scale = comp.get_editor_property("relative_scale3d")
        comp.set_editor_property("relative_scale3d", unreal.Vector(scale.x, scale.y, height / 100.0))
        comp.set_editor_property("relative_location", unreal.Vector(5, 0, height / 2.0))


def ladder(loc, yaw, height, gutter=False, folder="Gameplay/Traversal"):
    actor = spawn(bp("World/Interactables/Ladder/" + ("BP_Gutter" if gutter else "BP_Ladder")), loc, (0, yaw, 0), None, folder)
    if actor:
        actor.set_editor_property("height", float(height))
        set_mesh_height(actor, height)
    return actor


def patrol(points, waits=None, folder="AI", curved=False):
    route = spawn(bp("AI/BP_PatrolRoute"), points[0], label="PatrolRoute", folder=folder)
    entries = []
    for i, p in enumerate(points):
        e = unreal.HitmenPatrolPoint()
        e.set_editor_property("location", unreal.Vector(p[0] - points[0][0], p[1] - points[0][1], p[2] - points[0][2]))
        e.set_editor_property("wait_time", (waits or [3.0] * len(points))[i])
        entries.append(e)
    route.set_editor_property("points", entries)
    if curved:
        try:
            route.set_editor_property("curved", True)
        except Exception as error:
            log("WARN curved patrol:", error)
    return route


def npc(kind, loc, yaw=0, route=None, keycard=None, folder="AI", stationary=False, sentry=False):
    """Place an NPC. stationary = stands at its post facing yaw; sentry = stationary, sight only,
    never leaves its post."""
    cls = {"guard": "Characters/NPC/BP_NPC_Guard", "sniper": "Characters/NPC/BP_NPC_Sniper",
           "vip": "Characters/NPC/BP_NPC_VIPCandidate"}[kind]
    actor = spawn(bp(cls), (loc[0], loc[1], loc[2] + 100), (0, yaw, 0), None, folder)
    if actor and route:
        actor.set_editor_property("patrol_route", route)
    if actor and keycard is not None:
        actor.set_editor_property("carried_keycard", keycard)
    if actor and (stationary or sentry):
        actor.set_editor_property("behavior", unreal.HitmenNPCBehavior.STATIONARY)
    if actor and sentry:
        actor.set_editor_property("reacts_to_noises", False)
        actor.set_editor_property("hold_position", True)
    return actor


def team_starts(groups, folder="Gameplay/Starts"):
    for group, (x, y, yaw) in enumerate(groups):
        for i in range(4):
            offset_x = (i % 2) * 150 - 75
            offset_y = (i // 2) * 150 - 75
            start = spawn(bp("World/Spawning/BP_TeamStart"), (x + offset_x, y + offset_y, 120), (0, yaw, 0), None, folder)
            if start:
                start.set_editor_property("spawn_group", group)


def weapon_pickup(loc, weapon, folder="Gameplay/Loot"):
    actor = spawn(bp("Weapons/Pickups/BP_WeaponPickup"), loc, folder=folder)
    data = load(path("Data", "Weapons", "DA_Weapon_" + weapon))
    if actor and data:
        actor.set_editor_property("weapon", data)
    return actor


def ability_pickup(loc, ability, folder="Gameplay/Loot"):
    actor = spawn(bp("Abilities/Pickups/BP_AbilityPickup"), loc, folder=folder)
    data = load(path("Data", "Abilities", "DA_Ability_" + ability))
    if actor and data:
        actor.set_editor_property("ability", data)
    return actor


# ---------------------------------------------------------------------------------------------
# Front end: one scene for the title (museum), the room (briefing room) and "Your agent" (stage).
# The HUD blends between CameraActors tagged Shot_<Phase>; no level ever loads between screens.
# ---------------------------------------------------------------------------------------------

def shot(name, loc, rot, fov=70.0):
    camera = spawn(unreal.CameraActor, loc, rot, name, "Cameras")
    camera.camera_component.set_editor_property("field_of_view", fov)
    camera.camera_component.set_editor_property("constrain_aspect_ratio", False)
    camera.tags = [unreal.Name(name)]
    return camera


# ---- Front end lighting: one fixed exposure for the whole scene (no eye adaptation while the
# camera flies between the hall, the briefing room and the stage); every room is lit for it. ----
FRONTEND_EV100 = 5.0
FRONTEND_MUSEUM_SPOT = 3500.0
FRONTEND_MUSEUM_FILL = 2600.0
FRONTEND_KEY = 6000.0
FRONTEND_BRIEFING_LAMP = 5000.0
FRONTEND_BRIEFING_FILL = 2800.0
FRONTEND_STAGE_KEY = 2200.0
FRONTEND_STAGE_RIM = 2600.0
FRONTEND_STAGE = (5000, -600)
# Camera of "Your agent": the agent sits on the left of the frame (loadout on the right).
FRONTEND_AGENT_SHOT = ((5000 - 480, -600 + 170, 125), (-3, 0, 0))


def front_end_exposure(volume=None):
    """Unbound post process volume with min = max exposure (EV100): nothing adapts."""
    if volume is None:
        volume = spawn(unreal.PostProcessVolume, (2500, 0, 300), label="FrontEndExposure", folder="Lighting")
    volume.set_editor_property("unbound", True)
    settings = volume.get_editor_property("settings")
    settings.set_editor_property("override_auto_exposure_method", True)
    settings.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_HISTOGRAM)
    settings.set_editor_property("override_auto_exposure_min_brightness", True)
    settings.set_editor_property("auto_exposure_min_brightness", FRONTEND_EV100)
    settings.set_editor_property("override_auto_exposure_max_brightness", True)
    settings.set_editor_property("auto_exposure_max_brightness", FRONTEND_EV100)
    settings.set_editor_property("override_auto_exposure_bias", True)
    settings.set_editor_property("auto_exposure_bias", 0.0)
    settings.set_editor_property("override_local_exposure_highlight_contrast_scale", True)
    settings.set_editor_property("local_exposure_highlight_contrast_scale", 1.0)
    settings.set_editor_property("override_local_exposure_shadow_contrast_scale", True)
    settings.set_editor_property("local_exposure_shadow_contrast_scale", 1.0)
    volume.set_editor_property("settings", settings)
    return volume


def front_end_tune_lights(actors):
    """Light levels for the fixed exposure; also adds the briefing fills and frames the agent."""
    labels = {a.get_actor_label(): a for a in actors}
    for actor in actors:
        loc = actor.get_actor_location()
        if isinstance(actor, unreal.SpotLight):
            comp = actor.spot_light_component
            label = actor.get_actor_label()
            if label == "KeyLight":
                comp.set_editor_property("intensity", FRONTEND_KEY)
            elif label == "StageKey":
                comp.set_editor_property("intensity", FRONTEND_STAGE_KEY)
            elif label == "StageRim":
                comp.set_editor_property("intensity", FRONTEND_STAGE_RIM)
            elif loc.z > 800 and loc.x < 3000:
                comp.set_editor_property("intensity", FRONTEND_MUSEUM_SPOT)
            elif 3000 < loc.x < 5600 and loc.z > 550:
                comp.set_editor_property("intensity", FRONTEND_BRIEFING_LAMP)
                comp.set_editor_property("outer_cone_angle", 50.0)
        elif isinstance(actor, unreal.PointLight) and loc.x < 3000 and not actor.get_actor_label().startswith("BriefingFill"):
            actor.point_light_component.set_editor_property("intensity", FRONTEND_MUSEUM_FILL)
    # Briefing room fills: the room reads at the same exposure as the hall.
    # (kept off the agent stage at y -600 so the stage stays lit by its own key and rim)
    for index, (x, y) in enumerate(((3400, -150), (4300, -150), (4300, 1100), (3400, 700), (4300, 700), (5200, 700))):
        name = "BriefingFill_%d" % index
        fill = labels.get(name) or spawn(unreal.PointLight, (x, y, 500), label=name, folder="Lighting")
        fill.set_actor_location(unreal.Vector(x, y, 500), False, False)
        fill.point_light_component.set_editor_property("intensity", FRONTEND_BRIEFING_FILL)
        fill.point_light_component.set_editor_property("attenuation_radius", 1400.0)
        fill.point_light_component.set_editor_property("light_color", unreal.Color(r=215, g=225, b=255, a=255))
    camera = labels.get("Shot_Customization")
    if camera:
        loc, rot = FRONTEND_AGENT_SHOT
        camera.set_actor_location(unreal.Vector(*loc), False, False)
        camera.set_actor_rotation(unreal.Rotator(pitch=rot[0], yaw=rot[1], roll=rot[2]), False)


# Windows: the opening spans the window's local Y, so in a wall running along X they are turned 90.

def build_front_end():
    new_map("L_FrontEnd")
    game_mode("BP_GM_Lobby")
    environment(sun_pitch=-60, intensity=0.0, fog=0.02, fixed_exposure=False)

    # ---- Museum hall (title) ----
    floor((1200, 0, 0), (3600, 2400, 0), mi("MI_White"))
    box((1200, 0, 900), (3600, 2400, 20), mi("MI_Dark"), folder="Museum")
    for side in (-1, 1):
        wall((-600, side * 1200), (3000, side * 1200), 0, 900, 40, material=mi("MI_Stone"), folder="Museum")
        for i in range(6):
            x = -200 + i * 600
            box((x, side * 1050, 400), (90, 90, 800), mi("MI_White"), folder="Museum", shape="cylinder")
            if i % 2 == 0:
                box((x + 300, side * 1170, 360), (260, 20, 180), mi("MI_Gold"), folder="Museum")
                box((x + 300, side * 1160, 360), (220, 10, 140), mi(["MI_Red", "MI_Blue", "MI_Green"][i // 2 % 3]), folder="Museum")
    wall((-600, -1200), (-600, 1200), 0, 900, 40, material=mi("MI_Stone"), folder="Museum")
    # The far wall opens onto the briefing room (the camera flies through it).
    wall((3000, -1200), (3000, 1200), 0, 900, 40, openings=[(1200, 700, 0, 520)], material=mi("MI_Stone"), folder="Museum")
    for x, y, shape, mat in ((900, -500, "sphere", "MI_White"), (1700, 450, "cone", "MI_Gold"), (2300, -500, "cube", "MI_Stone")):
        box((x, y, 60), (120, 120, 120), mi("MI_Dark"), folder="Museum")
        box((x, y, 180), (90, 90, 120), mi(mat), folder="Museum", shape=shape)
    for x in (300, 1200, 2100):
        spot = spawn(unreal.SpotLight, (x, 0, 850), (-90, 0, 0), None, "Lighting")
        spot.spot_light_component.set_editor_property("intensity", 6000.0)
        spot.spot_light_component.set_editor_property("outer_cone_angle", 40.0)
    for x in (-200, 500, 1200, 1900, 2600):
        for y in (-700, 700):
            fill = spawn(unreal.PointLight, (x, y, 650), label=None, folder="Lighting")
            fill.point_light_component.set_editor_property("intensity", 4000.0)
            fill.point_light_component.set_editor_property("attenuation_radius", 1600.0)
            fill.point_light_component.set_editor_property("light_color", unreal.Color(r=255, g=214, b=170, a=255))
    key = spawn(unreal.SpotLight, (-150, 60, 330), (-30, 25, 0), "KeyLight", "Lighting")
    key.spot_light_component.set_editor_property("intensity", 12000.0)
    key.spot_light_component.set_editor_property("outer_cone_angle", 30.0)
    manny = spawn(unreal.SkeletalMeshActor, (120, 150, 0), (0, 115, 0), "Assassin", "Museum")
    comp = manny.skeletal_mesh_component
    comp.set_skeletal_mesh_asset(load("/Game/Characters/Mannequins/Meshes/SKM_Manny_Simple"))
    comp.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
    anim = unreal.SingleAnimationPlayData()
    anim.set_editor_property("anim_to_play", load("/Game/Characters/Mannequins/Anims/Unarmed/MM_Idle"))
    anim.set_editor_property("saved_looping", True)
    anim.set_editor_property("saved_playing", True)
    comp.set_editor_property("animation_data", anim)
    title_cam = shot("Shot_Title", (-380, -60, 165), (-4, 6, 0))
    title_cam.set_editor_property("auto_activate_for_player", unreal.AutoReceiveInput.PLAYER0)
    spawn(unreal.AmbientSound, (1200, 0, 300), label="MuseumAmbience (assign a sound)", folder="Audio")

    # ---- Briefing room (lobby) ----
    floor((4300, 0, 0), (2600, 2400, 0), mi("MI_Dark"))
    box((4300, 0, 620), (2600, 2400, 20), mi("MI_Dark"), folder="Briefing")
    for side in (-1, 1):
        wall((3000, side * 1200), (5600, side * 1200), 0, 620, 40, material=mi("MI_Stone"), folder="Briefing")
    wall((5600, -1200), (5600, 1200), 0, 620, 40, material=mi("MI_Stone"), folder="Briefing")
    # Map table: dark slab with a lit schematic top, chairs around.
    box((4000, 350, 40), (1000, 560, 80), mi("MI_Dark"), folder="Briefing/Table")
    box((4000, 350, 84), (940, 500, 6), mi("MI_Dark"), folder="Briefing/Table")
    box((4000, 350, 88), (700, 360, 2), mi("MI_Blue"), folder="Briefing/Table")
    for i in range(4):
        box((3650 + i * 230, 350 + 400, 45), (60, 60, 90), mi("MI_Stone"), folder="Briefing/Table")
        box((3650 + i * 230, 350 - 400, 45), (60, 60, 90), mi("MI_Stone"), folder="Briefing/Table")
    # Screens on the side wall.
    for i, mat in enumerate(("MI_Red", "MI_Blue", "MI_White")):
        box((3600 + i * 600, 1170, 330), (480, 16, 270), mi("MI_Dark"), folder="Briefing/Screens")
        box((3600 + i * 600, 1160, 330), (440, 6, 230), mi(mat), folder="Briefing/Screens")
    for x in (3500, 4500):
        lamp = spawn(unreal.SpotLight, (x, 350, 600), (-90, 0, 0), None, "Lighting")
        lamp.spot_light_component.set_editor_property("intensity", 2500.0)
        lamp.spot_light_component.set_editor_property("outer_cone_angle", 35.0)
        lamp.spot_light_component.set_editor_property("light_color", unreal.Color(r=200, g=220, b=255, a=255))
    shot("Shot_Lobby", (3250, -650, 430), (-28, 40, 0))

    # ---- Agent stage (customization): the agent stands on the left third of the frame ----
    sx, sy = 5000, -600
    box((sx, sy, 5), (320, 320, 10), mi("MI_Stone"), folder="Stage", shape="cylinder")
    spawn(bp("Characters/Player/BP_AgentPreview"), (sx, sy, 10), (0, 72, 0), "AgentPreview", "Stage")
    stage_key = spawn(unreal.SpotLight, (sx - 250, sy + 150, 420), (-55, -30, 0), "StageKey", "Lighting")
    stage_key.spot_light_component.set_editor_property("intensity", 6000.0)
    stage_key.spot_light_component.set_editor_property("outer_cone_angle", 28.0)
    stage_rim = spawn(unreal.SpotLight, (sx + 260, sy - 200, 380), (-45, 145, 0), "StageRim", "Lighting")
    stage_rim.spot_light_component.set_editor_property("intensity", 9000.0)
    stage_rim.spot_light_component.set_editor_property("outer_cone_angle", 30.0)
    stage_rim.spot_light_component.set_editor_property("light_color", unreal.Color(r=255, g=90, b=80, a=255))
    shot("Shot_Customization", *FRONTEND_AGENT_SHOT)
    front_end_exposure()
    front_end_tune_lights(EAS.get_all_level_actors())
    save_map()


def respread_ldtest_spawns(count=16, half=8200):
    """Many spawn points spread over open ground (the match picks one cluster per team at random).
    Idempotent: replaces every team start. Also clears leftover civilian placements."""
    LES.load_level(path("Maps", "L_LDTest"))
    world = UES.get_editor_world()
    for actor in EAS.get_all_level_actors():
        name = actor.get_class().get_name()
        if "TeamStart" in name or "Civilian" in name or "Civilian" in actor.get_actor_label():
            EAS.destroy_actor(actor)
    candidates = []
    step = 700
    for gx in range(-half, half + 1, step):
        for gy in range(-half, half + 1, step):
            hit = unreal.SystemLibrary.line_trace_single(world, unreal.Vector(gx, gy, 4000), unreal.Vector(gx, gy, -200),
                                                         unreal.TraceTypeQuery.TRACE_TYPE_QUERY1, False, [], unreal.DrawDebugTrace.NONE, True)
            if not hit:
                continue
            impact = hit.to_tuple()[4]
            if impact.z > 40:          # roofs, crates: outdoors on the ground only
                continue
            blocked = unreal.SystemLibrary.sphere_trace_single(world, unreal.Vector(gx, gy, impact.z + 140), unreal.Vector(gx, gy, impact.z + 141), 120.0,
                                                               unreal.TraceTypeQuery.TRACE_TYPE_QUERY1, False, [], unreal.DrawDebugTrace.NONE, True)
            if blocked:
                continue
            candidates.append((gx, gy, impact.z))
    # Farthest-point selection from a fixed seed so re-runs give the same layout.
    chosen = []
    if candidates:
        chosen.append(min(candidates, key=lambda c: (c[0] + half) ** 2 + (c[1] + half) ** 2))
    while candidates and len(chosen) < count:
        best = max(candidates, key=lambda c: min((c[0] - o[0]) ** 2 + (c[1] - o[1]) ** 2 for o in chosen))
        chosen.append(best)
    for x, y, z in chosen:
        yaw = math.degrees(math.atan2(-y, -x))   # face the map centre
        spawn(bp("World/Spawning/BP_TeamStart"), (x, y, z + 120), (0, yaw, 0), None, "Gameplay/Starts")
    log("LDTest spawns", len(chosen), "from", len(candidates), "candidates")
    save_map()


def build_empty(name):
    new_map(name)
    game_mode("BP_GM_Match")
    floor((0, 0, 0), (20000, 20000, 0), GRID)
    environment()
    nav_bounds((0, 0, 0), (10000, 10000, 2000))
    team_starts([(-6000, -6000, 45), (6000, 6000, -135), (-6000, 6000, -45), (6000, -6000, 135)])
    for x, y in ((0, 0), (5000, -5000), (-5000, 5000)):
        spawn(bp("World/Spawning/BP_RetrievalPoint"), (x, y, 0), folder="Gameplay/Retrieval")
    save_map()


# ---------------------------------------------------------------------------------------------
# Debug gym: a compact, walled showcase ("mini LD") — every move, weapon, ability, fixture and
# AI behaviour packed along short loops, each station labelled just above itself.
# ---------------------------------------------------------------------------------------------

GYM_HALF = 3700          # hall is 74 x 74 m
GYM_WALL_H = 900
LABEL_GAP = 60           # labels float this far above the top of what they describe


def label(x, y, top, string, size=36, color=(1, 1, 1), yaw=-90.0, folder="Labels"):
    """A label right above a station (top = height of the thing it names)."""
    return text((x, y, top + LABEL_GAP), string, size, yaw, color, folder)


def zone_title(x, y, string, folder="Labels"):
    return text((x, y, 520), string, 110, color=(1, 0.78, 0.1), folder=folder)


def block(x, y, w, d, h, material=GRID_DARK, folder="Gym/Movement", z0=0.0):
    return box((x, y, z0 + h / 2.0), (w, d, h), material, folder=folder)


def build_gym():
    new_map("L_Gym")
    game_mode("BP_GM_Gym")
    H = GYM_HALF
    floor((0, 0, 0), (2 * H, 2 * H, 0), GRID)
    # Perimeter: tall walls, so the view never runs to an endless floor.
    for start, end in (((-H, -H), (H, -H)), ((H, -H), (H, H)), ((H, H), (-H, H)), ((-H, H), (-H, -H))):
        wall(start, end, 0, GYM_WALL_H, 60, material=GRID_ALT, folder="Gym/Hall")
    environment(sun_pitch=-55, intensity=6.0, fog=0.004)
    nav_bounds((0, 0, 0), (H + 200, H + 200, 1500))
    # Spawn: south-west, facing north toward the movement course.
    team_starts([(-2300, -3150, 90)])
    text((-1300, -3450, 260), "HITMEN GYM", 120)
    text((-1300, -3450, 190), "Esc: debug panel - weapons, abilities, keycards, god mode", 34)

    # =========================== A. MOVEMENT (west, a loop going north) ===========================
    X = -2400
    zone_title(X, -3000, "MOVEMENT")
    G = "Gym/Movement"
    # Metrics: what Space does at each height.
    block(X, -2550, 420, 60, 50, folder=G)
    label(X, -2550, 50, "STEP  50 cm  (walk / jump)")
    block(X, -2150, 420, 60, 120, folder=G)
    label(X, -2150, 120, "VAULT  120 cm  (Space)")
    block(X, -1650, 420, 320, 250, folder=G)
    label(X, -1650, 250, "LEDGE GRAB  250 cm  (Space: climb on top)")
    stairs((X + 260, -1500, 0), 270, 250, 450, width=160, folder=G)
    block(X, -1000, 420, 25, 250, folder=G)
    label(X, -1000, 250, "CLIMB OVER  thin 250 cm wall")
    # Window in a wall: F to open, Space to vault through.
    wall((X - 350, -500), (X + 350, -500), 0, 320, 25, [(350, 120, 100, 220)], GRID_DARK, G)
    spawn(bp("World/Interactables/Window/BP_Window"), (X, -500, 0), (0, 90, 0), folder=G)
    label(X, -500, 320, "WINDOW  F open  -  Space vault")
    # Ladder and gutter up to a platform; zipline down from it to the east.
    block(X, 300, 500, 500, 400, folder=G)
    ladder((X, 25, 0), -90, 400)
    label(X - 120, 60, 400, "LADDER")
    block(X - 600, 300, 300, 300, 600, folder=G)
    ladder((X - 600, 125, 0), -90, 600, gutter=True)
    label(X - 600, 140, 600, "GUTTER")
    block(X - 375, 300, 150, 120, 400, folder=G, z0=0)  # bridge from the gutter top down to the platform
    # Jump pad onto a high ledge; teleporter back to the start.
    spawn(bp("World/Interactables/JumpPad/BP_JumpPad"), (X, 1100, 0), folder=G)
    label(X, 1100, 20, "JUMP PAD")
    block(X, 1600, 500, 400, 500, folder=G)
    tp_a = spawn(bp("World/Interactables/Teleporter/BP_Teleporter"), (X, 1600, 500), (0, 0, 0), folder=G)
    tp_b = spawn(bp("World/Interactables/Teleporter/BP_Teleporter"), (X + 600, -3050, 0), (0, 90, 0), folder=G)
    if tp_a and tp_b:
        tp_a.set_editor_property("destination", tp_b)
        tp_b.set_editor_property("destination", tp_a)
    label(X, 1600, 520, "TELEPORTER  (back to start)")
    # Tall grass and a locker to break line of sight.
    spawn(bp("World/Interactables/Grass/BP_TallGrass"), (-3400, 1500, 55), folder=G)
    label(-3400, 1500, 110, "TALL GRASS  crouch inside")
    spawn(bp("World/Interactables/Locker/BP_Locker"), (X - 700, 2200, 0), (0, 0, 0), folder=G)
    label(X - 700, 2200, 215, "LOCKER  F hide")

    # ======================= PARKOUR RUN (west wall, then north to the AI pen) =======================
    # Rooftops from the spawn to the gutter tower, a zipline north, then over to an overlook above the AI pen.
    # Gaps and heights follow the metrics: jump apex ~1 m, vault / mantle 120 cm, ledge grab 250 cm.
    G = "Gym/Parkour"
    RX = -3400
    zone_title(RX, -3500, "PARKOUR RUN")
    run = [
        # (x, y, width, depth, height, label)
        (RX, -3200, 400, 300, 120, "1 VAULT"),
        (RX, -2650, 400, 500, 250, "2 JUMP + MANTLE"),
        (RX, -1900, 400, 600, 250, "3 GAP  2 m"),
        (RX, -1200, 400, 500, 370, "4 JUMP + GRAB"),
        (RX, 100, 400, 400, 370, "6 LEDGE GRAB  onto the tower"),
    ]
    for (bx, by, bw, bd, bh, text_) in run:
        block(bx, by, bw, bd, bh, GRID_ALT, G)
        label(bx, by, bh, text_, 30)
    block(RX, -525, 60, 850, 370, GRID_ALT, G)  # beam from the 4th roof to the 6th
    label(RX, -525, 370, "5 BEAM", 30)
    # Zipline from the gutter tower (600) north to a roof by the west wall, then steps over to the north roofs.
    zip_start = (X - 600, 420, 600 + 260)
    nw = (RX, 2650, 400, 500, 300)
    zip_line = spawn(bp("World/Interactables/Zipline/BP_Zipline"), zip_start, folder=G)
    if zip_line:
        zip_line.set_editor_property("end_point", unreal.Vector(nw[0] - zip_start[0], nw[1] - nw[3] / 2 + 80 - zip_start[1], nw[4] + 260 - zip_start[2]))
    label(X - 600, 420, 860, "7 ZIPLINE  F at the post", 30)
    block(nw[0], nw[1], nw[2], nw[3], nw[4], GRID_ALT, G)
    block(-2900, 2650, 300, 300, 400, GRID_ALT, G)
    label(-2900, 2650, 400, "8 STEP UP", 30)
    # North roofs: one comes from the jump pad ledge, the other leads to the overlook; a ladder on its back.
    block(X, 2250, 400, 500, 500, GRID_ALT, G)
    block(X, 3000, 400, 600, 500, GRID_ALT, G)
    ladder((X, 3315, 0), 90, 500)
    block(-2000, 3000, 400, 80, 500, GRID_ALT, G)
    label(-2000, 3000, 500, "9 PLANK", 30)
    block(-1650, 3000, 300, 400, 500, GRID_ALT, G)
    label(-1650, 3000, 500, "10 OVERLOOK  drop into the AI pen", 30)

    # =========================== B. ARMOURY (centre-south) ===========================
    G = "Gym/Armoury"
    zone_title(0, -3000, "ARMOURY")
    weapons = ["Pistol", "SMG", "AssaultRifle", "Shotgun", "Minigun", "Sniper", "Bow", "RocketLauncher", "GrenadeLauncher", "BlackHole", "Sword"]
    for i, name in enumerate(weapons):
        x = -1000 + (i % 6) * 400
        y = -2600 if i < 6 else -2100
        block(x, y, 140, 140, 90, mi("MI_Dark"), G)
        weapon_pickup((x, y, 140), name, G)
        label(x, y, 200, name.upper(), 26)
    # Containers: fixed contents and random contents.
    fixed = spawn(bp("World/Interactables/Container/BP_Container"), (-600, -1500, 0), (0, -90, 0), folder=G)
    if fixed:
        entries = []
        for weapon, item in (("Sniper", None), ("Shotgun", None), (None, "SmokeGrenade"), (None, "Flashbang")):
            e = unreal.HitmenContainerEntry()
            if weapon:
                e.set_editor_property("weapon", load(path("Data", "Weapons", "DA_Weapon_" + weapon)))
            if item:
                e.set_editor_property("item", load(path("Data", "Abilities", "DA_Ability_" + item)))
            entries.append(e)
        fixed.set_editor_property("contents", entries)
    label(-600, -1500, 160, "CONTAINER  fixed contents")
    random_box = spawn(bp("World/Interactables/Container/BP_Container_Random"), (200, -1500, 0), (0, -90, 0), folder=G)
    label(200, -1500, 160, "CONTAINER  random contents")
    # Loot points restricted to a list (one picked at random when play starts).
    loot = spawn(bp("World/Spawning/BP_LootSpawn_Weapon"), (900, -1500, 40), folder=G)
    if loot:
        loot.set_editor_property("weapon_overrides", [load(path("Data", "Weapons", "DA_Weapon_" + w)) for w in ("SMG", "AssaultRifle", "Shotgun")])
    label(900, -1500, 120, "LOOT POINT  SMG / rifle / shotgun", 26)
    # Targets: barrels and a breakable wall.
    for i in range(3):
        spawn(bp("World/Interactables/Barrel/BP_ExplosiveBarrel"), (-900 + i * 250, -800, 0), folder=G)
    label(-650, -800, 100, "EXPLOSIVE BARRELS")
    spawn(bp("World/Interactables/Breakable/BP_BreakableWall"), (300, -800, 0), folder=G)
    label(300, -800, 300, "BREAKABLE WALL  explosions")
    # Abilities: 3 rows of 7 pickups.
    abilities = ["Blink", "DoubleJump", "Jetpack", "Dash", "Hook", "Transposition", "Pill", "Invisibility", "Armor", "C4",
                 "Stone", "RemoteDistraction", "SleepDart", "Charm", "Flashbang", "SmokeGrenade", "Lighter", "MultiKill", "Trap", "WindMine", "PoisonVial"]
    zone_title(0, -250, "ABILITIES  E move / A manipulation")
    for i, name in enumerate(abilities):
        x = -1050 + (i % 7) * 350
        y = 0 + (i // 7) * 330
        ability_pickup((x, y, 0), name, "Gym/Abilities")
        label(x, y, 70, name.upper(), 22)

    # =========================== C. FIXTURES (east) ===========================
    G = "Gym/Fixtures"
    X = 2400
    zone_title(X, -3000, "FIXTURES")
    # Doors: plain and keycard (keycard on a pedestal next to it).
    wall((X - 900, -2600), (X - 300, -2600), 0, 300, 25, [(300, 110, 0, 230)], GRID_DARK, G)
    spawn(bp("World/Interactables/Door/BP_Door"), (X - 655, -2600, 0), folder=G)
    label(X - 600, -2600, 300, "DOOR  F")
    wall((X + 100, -2600), (X + 700, -2600), 0, 300, 25, [(300, 110, 0, 230)], GRID_DARK, G)
    spawn(bp("World/Interactables/Door/BP_Door_Keycard1"), (X + 345, -2600, 0), folder=G)
    block(X + 900, -2800, 100, 100, 90, mi("MI_Dark"), G)
    card = spawn(bp("World/Interactables/Keycard/BP_KeycardPickup"), (X + 900, -2800, 90), folder=G)
    if card:
        card.set_editor_property("keycard_id", 1)
    label(X + 400, -2600, 300, "KEYCARD 1 DOOR  (card on the pedestal)")
    # Distractions.
    block(X - 600, -1900, 100, 100, 80, mi("MI_Dark"), G)
    spawn(bp("World/Interactables/Distraction/BP_Distraction_Radio"), (X - 600, -1900, 80), folder=G)
    label(X - 600, -1900, 120, "RADIO  noise")
    block(X + 300, -1840, 300, 20, 260, GRID_DARK, G)  # cover from the camera
    # Security: camera on a pole watching the path, station with its feed.
    block(X + 800, -1150, 50, 50, 420, GRID_DARK, G)
    camera = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (X + 800, -1190, 400), (0, -150, 0), folder=G)
    console = spawn(bp("World/Interactables/Security/BP_ControlConsole"), (X - 500, -1150, 0), (0, -90, 0), folder=G)
    if camera and console:
        console.set_editor_property("linked_cameras", [camera])
    label(X + 800, -1150, 420, "CAMERA  (cone on the floor)")
    label(X - 500, -1150, 190, "SECURITY STATION  F: cameras off")
    # Turret behind a low wall, aiming down the east lane; its hack panel at the side.
    block(X, -300, 900, 40, 110, GRID_DARK, G)
    turret = spawn(bp("World/Interactables/Security/BP_Turret"), (X + 700, -150, 0), (0, -90, 0), folder=G)
    if turret:
        turret.set_editor_property("range", 1300.0)
    panel = spawn(bp("World/Interactables/Security/BP_HackPanel"), (X - 700, -150, 0), (0, -90, 0), folder=G)
    if turret and panel:
        panel.set_editor_property("linked_turret", turret)
    label(X + 700, -150, 160, "TURRET  laser, F to hack")
    label(X - 700, -150, 180, "HACK PANEL")
    # Breaker room: the breaker outside cuts the lamp inside (dark room, a guard inside).
    rx, ry = X, 650
    wall((rx - 400, ry - 400), (rx + 400, ry - 400), 0, 330, 25, [(400, 110, 0, 230)], GRID_DARK, G)
    wall((rx - 400, ry + 400), (rx + 400, ry + 400), 0, 330, 25, (), GRID_DARK, G)
    wall((rx - 400, ry - 400), (rx - 400, ry + 400), 0, 330, 25, (), GRID_DARK, G)
    wall((rx + 400, ry - 400), (rx + 400, ry + 400), 0, 330, 25, (), GRID_DARK, G)
    box((rx, ry, 340), (830, 830, 20), GRID_DARK, folder=G)
    lamp = spawn(bp("World/Interactables/Power/BP_LightFixture"), (rx, ry, 300), folder=G)
    blackout = spawn(bp("World/Interactables/Power/BP_BlackoutVolume"), (rx, ry, 165), folder=G)
    if blackout:
        blackout.set_actor_scale3d(unreal.Vector(0.5, 0.5, 0.55))
    breaker = spawn(bp("World/Interactables/Power/BP_CircuitBreaker"), (rx - 600, ry - 450, 0), (0, -90, 0), folder=G)
    if breaker and lamp and blackout:
        breaker.set_editor_property("linked_lights", [lamp])
        breaker.set_editor_property("linked_volumes", [blackout])
    npc("guard", (rx, ry + 150, 0), -90, folder=G, stationary=True)
    label(rx - 600, ry - 450, 180, "BREAKER  F: lights off")
    label(rx, ry, 350, "DARK ROOM")
    # Mounted gun and tank on the east lane.
    block(X - 300, 1600, 160, 160, 90, mi("MI_Dark"), G)
    spawn(bp("World/Vehicles/BP_MountedGun"), (X - 300, 1600, 90), (0, 90, 0), folder=G)
    label(X - 300, 1600, 200, "MOUNTED GUN  F")
    spawn(bp("World/Vehicles/BP_Tank"), (X + 500, 2600, 100), (0, 90, 0), folder=G)
    label(X + 500, 2600, 260, "TANK")
    # Sealed room: vent hatch, window and a poisonable drink.
    vx, vy = X - 450, 2500
    room_door = spawn(bp("World/Interactables/Door/BP_Door"), (vx - 55, vy - 350, 0), folder=G)
    wall((vx - 350, vy - 350), (vx + 350, vy - 350), 0, 330, 25, [(350, 110, 0, 230)], GRID_DARK, G)
    wall((vx - 350, vy + 350), (vx + 350, vy + 350), 0, 330, 25, [(350, 120, 100, 220)], GRID_DARK, G)
    room_window = spawn(bp("World/Interactables/Window/BP_Window"), (vx, vy + 350, 0), (0, 90, 0), folder=G)
    wall((vx - 350, vy - 350), (vx - 350, vy + 350), 0, 330, 25, [(350, 120, 0, 120)], GRID_DARK, G)
    # Vent: the hatch in the AI pen's east wall (x 1400), a 1.2 m crouch duct to the room's west wall.
    duct_x0, duct_x1 = 1400, vx - 350
    for side in (-1, 1):
        box(((duct_x0 + duct_x1) / 2, vy + side * 70, 65), (duct_x1 - duct_x0, 20, 130), GRID_DARK, folder=G)
    box(((duct_x0 + duct_x1) / 2, vy, 130), (duct_x1 - duct_x0 + 25, 160, 20), GRID_DARK, folder=G)
    hatch = spawn(bp("World/Interactables/Vent/BP_VentHatch"), (duct_x0, vy, 0), (0, 90, 0), folder=G)
    if hatch:
        hatch.set_editor_property("open_angle", 88.0)  # the duct is as wide as the hatch: it opens flat on its side
    wall((vx + 350, vy - 350), (vx + 350, vy + 350), 0, 330, 25, (), GRID_DARK, G)
    box((vx, vy, 340), (730, 730, 20), GRID_DARK, folder=G)
    room = spawn(bp("World/Interactables/Vent/BP_RoomVolume"), (vx, vy, 165), folder=G)
    if room:
        room.set_actor_scale3d(unreal.Vector(0.7, 0.7, 1.0))
        room.set_editor_property("openings", [a for a in (room_door, room_window, hatch) if a])
    if hatch and room:
        hatch.set_editor_property("linked_rooms", [room])
    block(vx + 150, vy, 160, 90, 90, mi("MI_Wood"), G)
    spawn(bp("World/Interactables/Drink/BP_PoisonableDrink"), (vx + 150, vy, 90), folder=G)
    label(vx, vy, 340, "SEALED ROOM  vent, window, drink")
    label(duct_x0 - 30, vy, 120, "VENT  F, crouch through", 26, yaw=180.0)  # read from the AI pen
    spawn(bp("World/Spawning/BP_RetrievalPoint"), (3250, 3250, 0), folder=G)
    label(3250, 3250, 40, "RETRIEVAL POINT  deliver the VIP")

    # =========================== D. AI (north centre, inside a pen) ===========================
    G = "Gym/AI"
    zone_title(0, 900, "AI")
    py0, py1, px0, px1 = 1200, 3600, -1400, 1400
    wall((px0, py0), (-150, py0), 0, 260, 25, (), GRID_DARK, G)          # entrance gap in the middle
    wall((150, py0), (px1, py0), 0, 260, 25, (), GRID_DARK, G)
    wall((px0, py0), (px0, py1), 0, 260, 25, (), GRID_DARK, G)
    wall((px1, py0), (px1, py1), 0, 260, 25, [(2500 - py0, 120, 0, 120)], GRID_DARK, G)  # vent hole (sealed room)
    # Curved patrol (spline) around a pillar.
    block(0, 2300, 300, 300, 260, GRID_DARK, G)
    loop = patrol([(-500, 1800, 100), (500, 1800, 100), (500, 2800, 100), (-500, 2800, 100)], [2.0, 0.0, 2.0, 0.0], G, curved=True)
    npc("guard", (-500, 1800, 0), 0, route=loop, folder=G)
    label(0, 2300, 260, "SPLINE PATROL  around the pillar")
    # Guard group: a leader on a route, two followers in formation.
    group_route = patrol([(-1100, 1500, 100), (-1100, 3300, 100)], [3.0, 3.0], G, curved=False)
    leader = npc("guard", (-1100, 1500, 0), 90, route=group_route, folder=G)
    followers = [npc("guard", (-1250, 1400, 0), 90, folder=G), npc("guard", (-950, 1400, 0), 90, folder=G)]
    group = spawn(bp("AI/BP_GuardGroup"), (-1100, 1500, 0), label="GuardGroup", folder=G)
    if group and leader:
        group.set_editor_property("members", [m for m in [leader] + followers if m])
    label(-1100, 1450, 200, "GUARD GROUP  leader + 2")
    # Keycard guard to pickpocket (stationary, back turned to the entrance).
    npc("guard", (900, 1700, 0), 90, keycard=1, folder=G, stationary=True)
    label(900, 1700, 200, "KEYCARD GUARD  F from behind: pickpocket")
    # Sentry, sniper tower, VIP candidate.
    npc("guard", (1100, 3200, 0), -90, folder=G, sentry=True)
    label(1100, 3200, 200, "SENTRY  sight only")
    block(-200, 3350, 300, 300, 450, GRID_DARK, G)
    ladder((-200, 3175, 0), -90, 450)
    npc("sniper", (-200, 3350, 450), -90, folder=G)
    label(-200, 3350, 650, "SNIPER  red laser")
    npc("vip", (400, 3300, 0), -90, folder=G, stationary=True)
    label(400, 3300, 200, "VIP CANDIDATE")
    # Hiding spots for the search state.
    spawn(bp("World/Interactables/Locker/BP_Locker"), (1250, 1900, 0), (0, 180, 0), folder=G)
    spawn(bp("World/Interactables/Grass/BP_TallGrass"), (-700, 3300, 55), folder=G).set_actor_scale3d(unreal.Vector(0.4, 0.4, 1.0))
    label(1250, 1900, 215, "SEARCH  lockers / grass")
    # Camera alarm: a camera over the pen entrance calls the guards inside.
    block(-400, py0 - 60, 40, 40, 380, GRID_DARK, G)
    pen_camera = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (-400, py0 - 20, 360), (0, -60, 0), folder=G)
    label(-400, py0 - 60, 380, "CAMERA ALARM  calls the pen")
    check_pickups_clear()
    save_map()


def check_pickups_clear(radius=25.0):
    """Logs every pickup whose spot (a small box at its hover height) overlaps level geometry."""
    world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    blocked = 0
    for actor in EAS.get_all_level_actors():
        name = actor.get_class().get_name()
        if not any(key in name for key in ("Pickup", "LootSpawn")):
            continue
        center = actor.get_actor_location() + unreal.Vector(0, 0, 40)
        hits = unreal.SystemLibrary.box_overlap_actors(world, center, unreal.Vector(radius, radius, radius),
                                                       [unreal.ObjectTypeQuery.OBJECT_TYPE_QUERY1], unreal.StaticMeshActor, [])
        if hits:
            blocked += 1
            log("WARN pickup inside geometry:", actor.get_actor_label(), name, actor.get_actor_location(), [h.get_actor_label() for h in hits])
    log("pickups clear" if blocked == 0 else "pickups blocked: %d" % blocked)


# ---------------------------------------------------------------------------------------------
# LD test: San Lazaro — town, walled manor with basement / vents / control and breaker rooms,
# cave + tunnel, mountains around a basin, invisible walls.
# ---------------------------------------------------------------------------------------------

def building(cx, cy, w, d, floors=2, floor_h=350, door_side="S", folder="Town", windows=True, gutter=True, roof_access=True,
             z0=0.0, ledges=True, shadow_lamp=False, blind_faces=()):
    """Hollow building standing at z0: a door, vaultable windows (BP_Window) on every facade and floor, floors, a
    switchback stair and a gutter. ledges: a continuous band just under the windows all around each floor (hang and
    shimmy along it). Two lamps per floor (shadow_lamp: the first one casts shadows)."""
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2
    H = floors * floor_h
    lane_w = 280  # stair lane (260 cm flight + margins)
    sy0, sy1 = y0 + 120, cy + 60  # every flight spans sy0..sy1
    side_t = (0.5,) if d < 1300 else (0.3, 0.7)
    for f in range(floors):
        z = z0 + f * floor_h
        win = [(w * 0.25, 120, 100, 220), (w * 0.75, 120, 100, 220)] if windows else []
        south = list(win)
        if f == 0 and door_side == "S":
            south = [(w * 0.5, 110, 0, 230)] + [o for o in win if abs(o[0] - w * 0.5) > 200]
        north = list(win)
        if f == 0 and door_side == "N":
            north = [(w * 0.5, 110, 0, 230)] + [o for o in win if abs(o[0] - w * 0.5) > 200]
        wall((x0, y0), (x1, y0), z, floor_h, 25, south, GRID_DARK, folder)
        wall((x0, y1), (x1, y1), z, floor_h, 25, north, GRID_DARK, folder)
        side_win = [(d * t, 120, 100, 220) for t in side_t] if windows else []
        wall((x0, y0), (x0, y1), z, floor_h, 25, [] if "W" in blind_faces else side_win, GRID_DARK, folder)
        wall((x1, y0), (x1, y1), z, floor_h, 25, [] if "E" in blind_faces else side_win, GRID_DARK, folder)
        if f > 0:
            # Floor slab: the main part, then two stair lanes on the east side. The lane of the flight that comes up
            # here has a hole over the flight; a 1.2 m landing stays at each end.
            box(((x0 + x1 - 2 * lane_w) / 2, cy, z), (w - 2 * lane_w, d, 25), GRID, folder=folder)
            for lane in (0, 1):
                lx = x1 - lane_w * (lane + 0.5)
                if lane == (f - 1) % 2:
                    box((lx, (y0 + sy0) / 2, z), (lane_w, sy0 - y0, 25), GRID, folder=folder)
                    box((lx, (sy1 + y1) / 2, z), (lane_w, y1 - sy1, 25), GRID, folder=folder)
                else:
                    box((lx, cy, z), (lane_w, d, 25), GRID, folder=folder)
        if ledges and windows and f > 0:
            # 18 cm band, its top 20 cm under the sills, wrapping the corners.
            lz = z + 80 - 6
            out = 12.5 + 9
            box((cx, y0 - out, lz), (w + 2 * (12.5 + 18), 18, 12), GRID_ALT, folder=folder + "/Ledges")
            box((cx, y1 + out, lz), (w + 2 * (12.5 + 18), 18, 12), GRID_ALT, folder=folder + "/Ledges")
            box((x0 - out, cy, lz), (18, d + 25, 12), GRID_ALT, folder=folder + "/Ledges")
            box((x1 + out, cy, lz), (18, d + 25, 12), GRID_ALT, folder=folder + "/Ledges")
    box((cx, cy, z0 + H), (w, d, 25), GRID, folder=folder)
    # Switchback: even flights rise north on the east lane, odd flights rise south on the next lane, so a flight
    # never runs under the one above it (head room), with room to step on at the bottom.
    for f in range(floors - 1):
        if f % 2 == 0:
            stairs((x1 - lane_w * 0.5, sy0, z0 + f * floor_h), 90, floor_h, sy1 - sy0, 260, folder=folder)
        else:
            stairs((x1 - lane_w * 1.5, sy1, z0 + f * floor_h), -90, floor_h, sy1 - sy0, 260, folder=folder)
    # Doors and windows actors.
    if door_side == "S":
        spawn(bp("World/Interactables/Door/BP_Door"), (cx - 55, y0, z0), folder=folder)
    else:
        spawn(bp("World/Interactables/Door/BP_Door"), (cx + 55, y1, z0), (0, 180, 0), folder=folder)
    # Fixed exposure: two lamps per floor (a key and a fill).
    for f in range(floors):
        lz = z0 + f * floor_h + floor_h - 45
        room_light(cx - w * 0.22, cy - d * 0.15, lz, folder, shadow=shadow_lamp)
        room_light(cx + w * 0.12, cy + d * 0.2, lz, folder, lumens=LAMP_FILL_LUMENS)
    if windows:
        win_actor = bp("World/Interactables/Window/BP_Window")
        for f in range(floors):
            z = z0 + f * floor_h
            for t in (0.25, 0.75):
                if not (f == 0 and abs(w * t - w * 0.5) <= 200 and door_side == "S"):
                    spawn(win_actor, (x0 + w * t, y0, z), (0, 90, 0), folder=folder)
                if not (f == 0 and abs(w * t - w * 0.5) <= 200 and door_side == "N"):
                    spawn(win_actor, (x0 + w * t, y1, z), (0, -90, 0), folder=folder)
            for t in side_t:
                if "W" not in blind_faces:
                    spawn(win_actor, (x0, y0 + d * t, z), (0, 180, 0), folder=folder)
                if "E" not in blind_faces:
                    spawn(win_actor, (x1, y0 + d * t, z), (0, 0, 0), folder=folder)
    if gutter:
        ladder((x0 - 15, cy + d * 0.3, z0), 180, H + 20, gutter=True, folder=folder)
    return (x0, x1, y0, y1, z0 + H)


# Room lamps, in lumens (fixed exposure EV100 -0.3): a key (one per room may cast shadows) and a fill.
LAMP_KEY_LUMENS = 420.0
LAMP_FILL_LUMENS = 260.0
LAMP_RADIUS = 1100.0


def room_light(x, y, z, folder, shadow=False, lumens=LAMP_KEY_LUMENS, radius=LAMP_RADIUS):
    """A ceiling light fixture with explicit units; shadow only when asked (one lamp per room)."""
    lamp = spawn(bp("World/Interactables/Power/BP_LightFixture"), (x, y, z), folder=folder)
    if lamp:
        light = lamp.get_editor_property("light")
        if light:
            light.set_editor_property("intensity_units", unreal.LightUnits.LUMENS)
            light.set_editor_property("intensity", float(lumens))
            light.set_editor_property("attenuation_radius", float(radius))
            light.set_cast_shadows(bool(shadow))
    return lamp


def ground(half, holes, material, folder="Ground"):
    """The ground slab (top at z 0) out to +-half, split into strips around rectangular holes [(x0, y0, x1, y1)]:
    stair wells and footprints that have their own slab (no coplanar faces)."""
    xs = sorted({-half, half} | {h[0] for h in holes} | {h[2] for h in holes})
    ys = sorted({-half, half} | {h[1] for h in holes} | {h[3] for h in holes})

    def solid(x, y):
        return not any(h[0] <= x <= h[2] and h[1] <= y <= h[3] for h in holes)

    for i in range(len(xs) - 1):
        x0, x1 = xs[i], xs[i + 1]
        run_start = None
        for j in range(len(ys) - 1):
            y0, y1 = ys[j], ys[j + 1]
            if solid((x0 + x1) / 2, (y0 + y1) / 2):
                if run_start is None:
                    run_start = y0
                continue
            if run_start is not None:
                floor(((x0 + x1) / 2, (run_start + y0) / 2, 0), (x1 - x0, y0 - run_start, 0), material, folder)
                run_start = None
        if run_start is not None:
            floor(((x0 + x1) / 2, (run_start + ys[-1]) / 2, 0), (x1 - x0, ys[-1] - run_start, 0), material, folder)


def stair_well(rect, bottom_z, rise_dir, run, holes, mouths=None, low_wall=True, folder="StairWell", top_z=0.0):
    """A hole in the ground (rect = x0, y0, x1, y1) down to bottom_z, with stairs rising along rise_dir ("+x", "-x",
    "+y", "-y") to ground level at the far end; a landing fills the rest. Walls line the hole below ground, a 1 m
    parapet runs around it except at the stair top. mouths = {"left" | "right" | "low": [(offset, width, sill, top)]}
    are openings in the underground walls (tunnels, doors), offsets measured from the low end / the left side."""
    x0, y0, x1, y1 = rect
    holes.append(rect)
    mouths = mouths or {}
    depth = top_z - bottom_z
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    floor((cx, cy, bottom_z), (x1 - x0, y1 - y0, 0), GRID_DARK, folder)
    # Low end -> high end along the rise axis, and the side walls (left / right looking up the stairs).
    if rise_dir == "+x":
        low, high, yaw = (x0, cy), (x1, cy), 0
        sides = {"left": ((x0, y1), (x1, y1)), "right": ((x0, y0), (x1, y0))}
        low_edge = ((x0, y1), (x0, y0))
        width, length = y1 - y0, x1 - x0
    elif rise_dir == "-x":
        low, high, yaw = (x1, cy), (x0, cy), 180
        sides = {"left": ((x1, y0), (x0, y0)), "right": ((x1, y1), (x0, y1))}
        low_edge = ((x1, y0), (x1, y1))
        width, length = y1 - y0, x1 - x0
    elif rise_dir == "+y":
        low, high, yaw = (cx, y0), (cx, y1), 90
        sides = {"left": ((x0, y0), (x0, y1)), "right": ((x1, y0), (x1, y1))}
        low_edge = ((x0, y0), (x1, y0))
        width, length = x1 - x0, y1 - y0
    else:
        low, high, yaw = (cx, y1), (cx, y0), -90
        sides = {"left": ((x1, y1), (x1, y0)), "right": ((x0, y1), (x0, y0))}
        low_edge = ((x1, y1), (x0, y1))
        width, length = x1 - x0, y1 - y0
    rad = math.radians(yaw)
    stair_start = (high[0] - math.cos(rad) * run, high[1] - math.sin(rad) * run, bottom_z)
    stairs(stair_start, yaw, depth, run, width, folder=folder)
    for name, (a, b) in sides.items():
        wall(a, b, bottom_z - 20, depth + 20, 20, mouths.get(name, ()), GRID_DARK, folder)
        wall(a, b, top_z, 100, 20, (), GRID_ALT, folder + "/Parapet")
    if low_wall:
        wall(low_edge[0], low_edge[1], bottom_z - 20, depth + 20, 20, mouths.get("low", ()), GRID_DARK, folder)
        wall(low_edge[0], low_edge[1], top_z, 100, 20, (), GRID_ALT, folder + "/Parapet")
    return length


def tunnel(points, z, width=400, height=340, lamp_every=2500, folder="Tunnel"):
    """Underground corridor along a polyline, floor top at z. At each corner the outer wall runs on to close it and
    the inner wall stops short, so the way stays open; both ends are open (they join a stair well or a room)."""
    n = len(points)
    dirs = []
    for a, b in zip(points[:-1], points[1:]):
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        dirs.append(((b[0] - a[0]) / length, (b[1] - a[1]) / length, length))
    hw = width / 2.0

    def turn(j0, j1):
        return dirs[j0][0] * dirs[j1][1] - dirs[j0][1] * dirs[j1][0]

    for i, (a, b) in enumerate(zip(points[:-1], points[1:])):
        dx, dy, length = dirs[i]
        nx, ny = -dy, dx  # left normal
        yaw = math.degrees(math.atan2(dy, dx))
        start_turn = turn(i - 1, i) if i > 0 else 0.0
        end_turn = turn(i, i + 1) if i < n - 2 else 0.0
        for side in (1, -1):
            def ext(t):
                if abs(t) < 1e-6:
                    return 0.0
                inner = (t > 0) == (side > 0)
                return -hw if inner else hw + 20
            t0, t1 = -ext(start_turn), length + ext(end_turn)
            mid = (t0 + t1) / 2
            box((a[0] + dx * mid + nx * (hw + 10) * side, a[1] + dy * mid + ny * (hw + 10) * side, z + height / 2),
                (t1 - t0, 20, height), GRID_DARK, yaw, folder=folder)
        t0 = -(hw + 20) if i > 0 else 0.0
        t1 = length + (hw + 20 if i < n - 2 else 0.0)
        mid = (t0 + t1) / 2
        cx, cy = a[0] + dx * mid, a[1] + dy * mid
        box((cx, cy, z - 10), (t1 - t0, width + 40, 20), GRID_DARK, yaw, folder=folder)
        box((cx, cy, z + height + 10), (t1 - t0, width + 40, 20), GRID_DARK, yaw, folder=folder)
        count = int(length / lamp_every)
        for k in range(1, count + 1):
            t = length * k / (count + 1)
            room_light(a[0] + dx * t, a[1] + dy * t, z + height - 50, folder + "/Lamps")


def road(a, b, width=1000, material="MI_Stone", folder="Roads"):
    """A flat strip on the ground between two points (2 cm high, walkable)."""
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    yaw = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
    box(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 1), (length + width, width, 2), mi(material), yaw, folder=folder)


def hedge_maze(cx, cy, size, cells, folder, seed=7):
    """A square hedge maze (2.2 m hedges): a perfect maze (every cell reachable, seeded so it never changes), an
    entrance in the middle of each side and an open 3 x 3 plaza in the centre. `cells` is odd."""
    rng = random.Random(seed)
    cell = size / cells
    x0, y0 = cx - size / 2, cy - size / 2
    green = mi("MI_Green")
    # open_v: (i, j) = no hedge on the line x = i between cells (i-1, j) and (i, j); open_h: (i, j) = line y = j.
    open_v, open_h = set(), set()
    visited, stack = {(0, 0)}, [(0, 0)]
    while stack:
        i, j = stack[-1]
        options = [(i + di, j + dj) for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1))
                   if 0 <= i + di < cells and 0 <= j + dj < cells and (i + di, j + dj) not in visited]
        if not options:
            stack.pop()
            continue
        ni, nj = rng.choice(options)
        if ni != i:
            open_v.add((max(i, ni), j))
        else:
            open_h.add((i, max(j, nj)))
        visited.add((ni, nj))
        stack.append((ni, nj))
    mid = cells // 2
    for k in (mid - 1, mid, mid + 1):
        open_v |= {(mid, k), (mid + 1, k)}
        open_h |= {(k, mid), (k, mid + 1)}
    # Outer ring with an entrance in the middle cell of each side.
    for (a, b) in (((x0, y0), (x0 + size, y0)), ((x0, y0 + size), (x0 + size, y0 + size)), ((x0, y0), (x0, y0 + size)), ((x0 + size, y0), (x0 + size, y0 + size))):
        wall(a, b, 0, 220, 60, [(size / 2, cell * 0.8, 0, 220)], green, folder)
    for i in range(1, cells):
        for j in range(cells):
            if (i, j) not in open_v:
                box((x0 + i * cell, y0 + (j + 0.5) * cell, 110), (60, cell + 60, 220), green, folder=folder)
    for j in range(1, cells):
        for i in range(cells):
            if (i, j) not in open_h:
                box((x0 + (i + 0.5) * cell, y0 + j * cell, 110), (cell + 60, 60, 220), green, folder=folder)


def build_ldtest():
    """San Lazaro (0.7.0): one organic map on hills, built by hitmen_sanlazaro.py."""
    import importlib
    import hitmen_sanlazaro
    importlib.reload(hitmen_sanlazaro)
    hitmen_sanlazaro.build()


def bake_map_textures():
    """Top-down schematics (minimap, pause map, room preview) for every playable map + the gym."""
    for name in ("LDTest", "Empty_01", "Empty_02", "Gym"):
        data = load(path("Data", "Maps", "DA_Map_" + name))
        if not data or not EAL.does_asset_exist(path("Maps", "L_" + name)):
            continue
        LES.load_level(path("Maps", "L_" + name))
        # LDTest covers 330 m: twice the pixels keeps the minimap as sharp as on the smaller maps.
        size = 1536 if name == "LDTest" else 768
        texture = unreal.HitmenContentTools.bake_map_texture(data, path("UI", "Maps", "T_Map_" + name), size, 260.0)
        if texture:
            save(texture)
            save(data)
            log("map texture", name)


def link_map_definitions():
    for name in ("LDTest", "Empty_01", "Empty_02", "Gym"):
        data = load(path("Data", "Maps", "DA_Map_" + name))
        world = load(path("Maps", "L_" + name))
        if data and world:
            data.set_editor_property("level", world)
            save(data)


# Maps hand-edited in the editor (through the Unreal MCP) after their first generation. The builder
# only bootstraps them when missing; set HITMEN_FORCE_MAPS=L_Gym,L_LDTest to regenerate (wipes edits).
HAND_EDITED_MAPS = ("L_Gym", "L_LDTest")
FORCED_MAPS = [m.strip() for m in os.environ.get("HITMEN_FORCE_MAPS", "").split(",") if m.strip()]


def should_build(name):
    if name in HAND_EDITED_MAPS and EAL.does_asset_exist(path("Maps", name)) and name not in FORCED_MAPS:
        log("map", name, "kept (hand-edited; HITMEN_FORCE_MAPS to regenerate)")
        return False
    return True


OLD_FRONT_END_MAPS = ("L_TitleScreen", "L_Lobby")


def run():
    build_front_end()
    build_empty("L_Empty_01")
    build_empty("L_Empty_02")
    if should_build("L_Gym"):
        build_gym()
    if should_build("L_LDTest"):
        build_ldtest()
    link_map_definitions()
    bake_map_textures()
    LES.load_level(path("Maps", "L_FrontEnd"))
    # The title screen and lobby maps were merged into L_FrontEnd.
    for name in OLD_FRONT_END_MAPS:
        if EAL.does_asset_exist(path("Maps", name)):
            EAL.delete_asset(path("Maps", name))
            log("deleted old map", name)
