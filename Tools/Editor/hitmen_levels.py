"""Hitmen level builder: every map is generated from engine shapes + the checker material.
Re-running rebuilds the maps from scratch (hand edits to these maps are overwritten)."""
import math
import os
import unreal
from hitmen_common import *

LES = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
UES = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)

GRID = "/Game/LevelPrototyping/Materials/MI_PrototypeGrid_Gray"
GRID_DARK = "/Game/LevelPrototyping/Materials/MI_PrototypeGrid_TopDark"
GRID_ALT = "/Game/LevelPrototyping/Materials/MI_PrototypeGrid_Gray_02"


def mi(name):
    return path("Materials", "Instances", name)


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
    comp.set_editor_property("text_render_color", unreal.Color(int(color[0] * 255), int(color[1] * 255), int(color[2] * 255), 255))
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


def environment(sun_pitch=-40.0, sun_yaw=30.0, fog=0.01, intensity=8.0):
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


def patrol(points, waits=None, folder="AI"):
    route = spawn(bp("AI/BP_PatrolRoute"), points[0], label="PatrolRoute", folder=folder)
    entries = []
    for i, p in enumerate(points):
        e = unreal.HitmenPatrolPoint()
        e.set_editor_property("location", unreal.Vector(p[0] - points[0][0], p[1] - points[0][1], p[2] - points[0][2]))
        e.set_editor_property("wait_time", (waits or [3.0] * len(points))[i])
        entries.append(e)
    route.set_editor_property("points", entries)
    return route


def npc(kind, loc, yaw=0, route=None, keycard=None, folder="AI", stationary=False, sentry=False):
    """Place an NPC. stationary = stands at its post facing yaw; sentry = stationary, sight only,
    never leaves its post."""
    cls = {"guard": "Characters/NPC/BP_NPC_Guard", "sniper": "Characters/NPC/BP_NPC_Sniper",
           "vip": "Characters/NPC/BP_NPC_VIPCandidate", "civilian": "Characters/NPC/BP_NPC_Civilian"}[kind]
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
# Title screen: a museum hall, the assassin on the right, ambient sound
# ---------------------------------------------------------------------------------------------

def build_title():
    new_map("L_TitleScreen")
    game_mode("BP_GM_Menu")
    floor((1200, 0, 0), (3600, 2400, 0), mi("MI_White"))
    box((1200, 0, 900), (3600, 2400, 20), mi("MI_Dark"), folder="Museum")
    for side in (-1, 1):
        wall((-600, side * 1200), (3000, side * 1200), 0, 900, 40, material=mi("MI_Stone"), folder="Museum")
        for i in range(6):
            x = -200 + i * 600
            box((x, side * 1050, 400), (90, 90, 800), mi("MI_White"), folder="Museum", shape="cylinder")
            # Paintings in gold frames.
            if i % 2 == 0:
                box((x + 300, side * 1170, 360), (260, 20, 180), mi("MI_Gold"), folder="Museum")
                box((x + 300, side * 1160, 360), (220, 10, 140), mi(["MI_Red", "MI_Blue", "MI_Green"][i // 2 % 3]), folder="Museum")
    wall((3000, -1200), (3000, 1200), 0, 900, 40, material=mi("MI_Stone"), folder="Museum")
    box((2980, 0, 420), (20, 500, 320), mi("MI_Gold"), folder="Museum")
    box((2970, 0, 420), (10, 440, 260), mi("MI_Dark"), folder="Museum")
    # Pedestals with sculptures.
    for x, y, shape, mat in ((900, -500, "sphere", "MI_White"), (1700, 450, "cone", "MI_Gold"), (2300, -300, "cube", "MI_Stone")):
        box((x, y, 60), (120, 120, 120), mi("MI_Dark"), folder="Museum")
        box((x, y, 180), (90, 90, 120), mi(mat), folder="Museum", shape=shape)
    # Lighting: dim ambience, spotlights.
    environment(sun_pitch=-60, intensity=0.0, fog=0.02)
    for x in (300, 1200, 2100):
        spot = spawn(unreal.SpotLight, (x, 0, 850), (-90, 0, 0), None, "Lighting")
        spot.spot_light_component.set_editor_property("intensity", 6000.0)
        spot.spot_light_component.set_editor_property("outer_cone_angle", 40.0)
    for x in (-200, 500, 1200, 1900, 2600):
        for y in (-700, 700):
            fill = spawn(unreal.PointLight, (x, y, 650), label=None, folder="Lighting")
            fill.point_light_component.set_editor_property("intensity", 4000.0)
            fill.point_light_component.set_editor_property("attenuation_radius", 1600.0)
            fill.point_light_component.set_editor_property("light_color", unreal.Color(255, 214, 170, 255))
    key = spawn(unreal.SpotLight, (-150, 60, 330), (-30, 25, 0), "KeyLight", "Lighting")
    key.spot_light_component.set_editor_property("intensity", 12000.0)
    key.spot_light_component.set_editor_property("outer_cone_angle", 30.0)

    # The assassin (engine mannequin) on the right, idling.
    manny = spawn(unreal.SkeletalMeshActor, (120, 150, 0), (0, 115, 0), "Assassin", "Museum")
    comp = manny.skeletal_mesh_component
    comp.set_skeletal_mesh_asset(load("/Game/Characters/Mannequins/Meshes/SKM_Manny_Simple"))
    comp.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
    anim = unreal.SingleAnimationPlayData()
    anim.set_editor_property("anim_to_play", load("/Game/Characters/Mannequins/Anims/Unarmed/MM_Idle"))
    anim.set_editor_property("saved_looping", True)
    anim.set_editor_property("saved_playing", True)
    comp.set_editor_property("animation_data", anim)
    

    camera = spawn(unreal.CameraActor, (-380, -60, 165), (-4, 6, 0), "MenuCamera", "Camera")
    camera.set_editor_property("auto_activate_for_player", unreal.AutoReceiveInput.PLAYER0)
    camera.camera_component.set_editor_property("field_of_view", 70.0)
    spawn(unreal.AmbientSound, (1200, 0, 300), label="MuseumAmbience (assign a sound)", folder="Audio")
    save_map()


def build_lobby():
    new_map("L_Lobby")
    game_mode("BP_GM_Lobby")
    floor((0, 0, 0), (3000, 3000, 0), mi("MI_Dark"))
    environment(intensity=1.0, fog=0.03)
    camera = spawn(unreal.CameraActor, (-600, 0, 200), (-10, 0, 0), "LobbyCamera", "Camera")
    camera.set_editor_property("auto_activate_for_player", unreal.AutoReceiveInput.PLAYER0)
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
# Debug gym: every move, weapon, ability, fixture and AI state, spaced and labelled
# ---------------------------------------------------------------------------------------------

def build_gym():
    new_map("L_Gym")
    game_mode("BP_GM_Gym")
    floor((0, 0, 0), (24000, 24000, 0), GRID)
    environment()
    nav_bounds((0, 0, 0), (12000, 12000, 2000))
    team_starts([(0, -9000, 90)])
    text((0, -8200, 300), "HITMEN DEBUG GYM", 220)
    text((0, -7900, 150), "Esc: debug panel (weapons, abilities, keycards, god mode)", 60)

    # ---- Row 1: movement course ----
    y = -6500
    text((-9000, y - 700, 250), "MOVEMENT", 160, color=(1, 0.8, 0.1))
    for i, h in enumerate((50, 100, 140, 200, 240, 350)):
        x = -9000 + i * 900
        box((x, y, h / 2.0), (400, 60, h), GRID_DARK, folder="Gym/Movement")
        text((x, y - 300, h + 80), "%d cm %s" % (h, "vault" if h <= 120 else ("ledge grab" if h <= 230 else "too high")), 50)
    # Window frame with sill (open the window, vault through).
    x = -3200
    wall((x - 300, y), (x + 300, y), 0, 350, 25, [(300, 120, 100, 220)], GRID_DARK, "Gym/Movement")
    win = spawn(bp("World/Interactables/Window/BP_Window"), (x, y, 0), folder="Gym/Movement")
    text((x, y - 300, 420), "Window (F open, vault)", 50)
    # Ladder and gutter on tall walls.
    x = -2000
    box((x, y + 200, 200), (400, 400, 400), GRID_DARK, folder="Gym/Movement")
    ladder((x, y - 25, 0), -90, 400)
    text((x, y - 300, 480), "Ladder (walk into it)", 50)
    x = -1100
    box((x, y + 200, 300), (400, 400, 600), GRID_DARK, folder="Gym/Movement")
    ladder((x, y - 25, 0), -90, 600, gutter=True)
    text((x, y - 300, 680), "Gutter", 50)
    # Zipline between two towers.
    box((0, y, 400), (300, 300, 800), GRID_DARK, folder="Gym/Movement")
    box((2000, y, 150), (300, 300, 300), GRID_DARK, folder="Gym/Movement")
    ladder((0, y - 175, 0), -90, 800)
    zip_line = spawn(bp("World/Interactables/Zipline/BP_Zipline"), (150, y, 900), folder="Gym/Movement")
    if zip_line:
        zip_line.set_editor_property("end_point", unreal.Vector(1700, 0, -500))
    text((1000, y - 400, 950), "Zipline", 60)
    # Jump pad, slide lane, stairs, teleporters, locker, tall grass.
    spawn(bp("World/Interactables/JumpPad/BP_JumpPad"), (3000, y, 0), folder="Gym/Movement")
    text((3000, y - 300, 200), "Jump pad", 50)
    box((4200, y, -5), (2000, 300, 10), mi("MI_Blue"), folder="Gym/Movement")
    text((4200, y - 300, 150), "Slide lane (sprint + crouch)", 50)
    stairs((5600, y - 100, 0), 0, 300, 600)
    text((5900, y - 300, 420), "Stairs", 50)
    tp_a = spawn(bp("World/Interactables/Teleporter/BP_Teleporter"), (7200, y, 0), (0, 0, 0), folder="Gym/Movement")
    tp_b = spawn(bp("World/Interactables/Teleporter/BP_Teleporter"), (7200, y + 2000, 0), (0, 180, 0), folder="Gym/Movement")
    if tp_a and tp_b:
        tp_a.set_editor_property("destination", tp_b)
        tp_b.set_editor_property("destination", tp_a)
    text((7200, y - 300, 200), "Teleporter pair", 50)
    spawn(bp("World/Interactables/Locker/BP_Locker"), (8400, y, 0), (0, -90, 0), folder="Gym/Movement")
    text((8400, y - 300, 260), "Locker (hide)", 50)
    spawn(bp("World/Interactables/Grass/BP_TallGrass"), (9800, y, 55), folder="Gym/Movement")
    text((9800, y - 400, 200), "Tall grass (crouch)", 50)

    # ---- Row 2: weapons ----
    y = -3500
    text((-9000, y - 700, 250), "WEAPONS", 160, color=(1, 0.8, 0.1))
    weapons = ["Pistol", "SMG", "AssaultRifle", "BurstRifle", "Shotgun", "Minigun", "Sniper", "Bow", "RocketLauncher", "GrenadeLauncher", "BlackHole", "Sword"]
    for i, name in enumerate(weapons):
        x = -9000 + i * 1500
        box((x, y, 50), (120, 120, 100), mi("MI_Dark"), folder="Gym/Weapons")
        weapon_pickup((x, y, 130), name, "Gym/Weapons")
        text((x, y - 250, 220), name, 50)
    # Targets: barrels, breakable wall.
    for i in range(4):
        spawn(bp("World/Interactables/Barrel/BP_ExplosiveBarrel"), (-9000 + i * 400, y + 1300, 0), folder="Gym/Weapons")
    text((-8400, y + 1000, 200), "Explosive barrels", 50)
    spawn(bp("World/Interactables/Breakable/BP_BreakableWall"), (-5500, y + 1300, 0), folder="Gym/Weapons")
    text((-5500, y + 1000, 380), "Breakable wall (explosions)", 50)

    # ---- Row 3: abilities ----
    y = -1000
    text((-9000, y - 700, 250), "ABILITIES  (E move / A manipulation)", 140, color=(1, 0.8, 0.1))
    abilities = ["Blink", "DoubleJump", "Jetpack", "Dash", "Hook", "Transposition", "Pill", "Invisibility", "Armor", "C4",
                 "Stone", "RemoteDistraction", "SleepDart", "Charm", "Flashbang", "SmokeGrenade", "Lighter", "MultiKill", "Trap", "WindMine", "PoisonVial"]
    for i, name in enumerate(abilities):
        x = -10000 + i * 1000
        ability_pickup((x, y, 0), name, "Gym/Abilities")
        text((x, y - 250, 200), name, 40)

    # ---- Row 4: fixtures ----
    y = 2000
    text((-9000, y - 900, 250), "FIXTURES", 160, color=(1, 0.8, 0.1))
    x = -9500
    wall((x - 300, y), (x + 300, y), 0, 300, 25, [(250, 110, 0, 230)], GRID_DARK, "Gym/Fixtures")
    spawn(bp("World/Interactables/Door/BP_Door"), (x - 105, y, 0), folder="Gym/Fixtures")
    text((x, y - 300, 380), "Door", 50)
    x = -8300
    wall((x - 300, y), (x + 300, y), 0, 300, 25, [(250, 110, 0, 230)], GRID_DARK, "Gym/Fixtures")
    spawn(bp("World/Interactables/Door/BP_Door_Keycard1"), (x - 105, y, 0), folder="Gym/Fixtures")
    spawn(bp("World/Interactables/Keycard/BP_KeycardPickup"), (x, y - 500, 0), folder="Gym/Fixtures").set_editor_property("keycard_id", 1)
    text((x, y - 300, 380), "Keycard 1 door + keycard", 50)
    spawn(bp("World/Interactables/Distraction/BP_Distraction_Radio"), (-7000, y, 80), folder="Gym/Fixtures")
    box((-7000, y, 40), (100, 100, 80), mi("MI_Dark"), folder="Gym/Fixtures")
    text((-7000, y - 300, 250), "Distraction (noise)", 50)
    box((-6000, y + 60, 150), (300, 20, 300), GRID_DARK, folder="Gym/Fixtures")
    spawn(bp("World/Interactables/Distraction/BP_HackableDistraction_TV"), (-6000, y, 0), folder="Gym/Fixtures")
    text((-6000, y - 300, 350), "Hackable TV (silent)", 50)
    # Camera network with control console and monitor.
    box((-4600, y + 300, 250), (60, 60, 500), GRID_DARK, folder="Gym/Fixtures")
    camera = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (-4600, y + 250, 450), (-30, -90, 0), folder="Gym/Fixtures")
    monitor = spawn(bp("World/Interactables/Security/BP_SecurityMonitor"), (-3800, y + 400, 180), (0, -90, 0), folder="Gym/Fixtures")
    console = spawn(bp("World/Interactables/Security/BP_ControlConsole"), (-3800, y + 200, 0), (0, -90, 0), folder="Gym/Fixtures")
    if camera and monitor and console:
        monitor.set_editor_property("linked_camera", camera)
        console.set_editor_property("linked_cameras", [camera])
    text((-4200, y - 300, 300), "Camera + monitor + console", 50)
    # Turret wired to a hack panel.
    turret = spawn(bp("World/Interactables/Security/BP_Turret"), (-2400, y + 600, 0), (0, -90, 0), folder="Gym/Fixtures")
    panel = spawn(bp("World/Interactables/Security/BP_HackPanel"), (-2000, y, 0), (0, -90, 0), folder="Gym/Fixtures")
    if turret and panel:
        panel.set_editor_property("linked_turret", turret)
    text((-2200, y - 300, 300), "Turret + hack panel", 50)
    # Blackout room: breaker cuts the lights inside.
    rx, ry = -500, y + 500
    wall((rx - 500, ry - 500), (rx + 500, ry - 500), 0, 350, 25, [(500, 110, 0, 230)], GRID_DARK, "Gym/Fixtures")
    wall((rx - 500, ry + 500), (rx + 500, ry + 500), 0, 350, 25, (), GRID_DARK, "Gym/Fixtures")
    wall((rx - 500, ry - 500), (rx - 500, ry + 500), 0, 350, 25, (), GRID_DARK, "Gym/Fixtures")
    wall((rx + 500, ry - 500), (rx + 500, ry + 500), 0, 350, 25, (), GRID_DARK, "Gym/Fixtures")
    box((rx, ry, 360), (1050, 1050, 20), GRID_DARK, folder="Gym/Fixtures")
    lamp = spawn(bp("World/Interactables/Power/BP_LightFixture"), (rx, ry, 320), folder="Gym/Fixtures")
    blackout = spawn(bp("World/Interactables/Power/BP_BlackoutVolume"), (rx, ry, 175), folder="Gym/Fixtures")
    if blackout:
        blackout.set_actor_scale3d(unreal.Vector(0.62, 0.62, 0.6))
    breaker = spawn(bp("World/Interactables/Power/BP_CircuitBreaker"), (rx - 700, ry - 600, 0), (0, -90, 0), folder="Gym/Fixtures")
    if breaker and lamp and blackout:
        breaker.set_editor_property("linked_lights", [lamp])
        breaker.set_editor_property("linked_volumes", [blackout])
    npc("guard", (rx, ry, 0), folder="Gym/Fixtures")
    text((rx, ry - 800, 420), "Breaker + blackout room", 50)
    # Sealed room with vent hatch and poisonable drink.
    vx, vy = 1700, y + 500
    room_door = spawn(bp("World/Interactables/Door/BP_Door"), (vx - 55, vy - 500, 0), folder="Gym/Fixtures")
    wall((vx - 500, vy - 500), (vx + 500, vy - 500), 0, 350, 25, [(500, 110, 0, 230)], GRID_DARK, "Gym/Fixtures")
    wall((vx - 500, vy + 500), (vx + 500, vy + 500), 0, 350, 25, [(500, 120, 100, 220)], GRID_DARK, "Gym/Fixtures")
    room_window = spawn(bp("World/Interactables/Window/BP_Window"), (vx, vy + 500, 0), folder="Gym/Fixtures")
    wall((vx - 500, vy - 500), (vx - 500, vy + 500), 0, 350, 25, [(500, 120, 0, 120)], GRID_DARK, "Gym/Fixtures")
    hatch = spawn(bp("World/Interactables/Vent/BP_VentHatch"), (vx - 500, vy, 0), (0, 90, 0), folder="Gym/Fixtures")
    wall((vx + 500, vy - 500), (vx + 500, vy + 500), 0, 350, 25, (), GRID_DARK, "Gym/Fixtures")
    box((vx, vy, 360), (1050, 1050, 20), GRID_DARK, folder="Gym/Fixtures")
    room = spawn(bp("World/Interactables/Vent/BP_RoomVolume"), (vx, vy, 175), folder="Gym/Fixtures")
    if room:
        room.set_editor_property("openings", [a for a in (room_door, room_window, hatch) if a])
    if hatch and room:
        hatch.set_editor_property("linked_rooms", [room])
    box((vx + 200, vy, 45), (200, 100, 90), mi("MI_Wood"), folder="Gym/Fixtures")
    drink = spawn(bp("World/Interactables/Drink/BP_PoisonableDrink"), (vx + 200, vy, 90), folder="Gym/Fixtures")
    drinker_route = patrol([(vx - 200, vy, 100), (vx + 100, vy, 100)], [2.0, 4.0], "Gym/Fixtures")
    if drink and drinker_route:
        points = list(drinker_route.get_editor_property("points"))
        points[1].set_editor_property("use_actor", drink)
        drinker_route.set_editor_property("points", points)
    npc("civilian", (vx - 200, vy, 0), route=drinker_route, folder="Gym/Fixtures")
    # Crawl duct to the hatch.
    box((vx - 1100, vy + 90, 150), (1200, 20, 300), GRID_DARK, folder="Gym/Fixtures")
    box((vx - 1100, vy - 90, 150), (1200, 20, 300), GRID_DARK, folder="Gym/Fixtures")
    box((vx - 1100, vy, 135), (1200, 200, 20), GRID_DARK, folder="Gym/Fixtures")
    text((vx, vy - 800, 420), "Sealed room: door, window, vent, drink", 50)
    # Vehicles, retrieval and loot.
    spawn(bp("World/Vehicles/BP_MountedGun"), (4000, y, 100), (0, 90, 0), folder="Gym/Fixtures")
    box((4000, y, 50), (150, 150, 100), mi("MI_Dark"), folder="Gym/Fixtures")
    text((4000, y - 300, 250), "Mounted gun (180)", 50)
    spawn(bp("World/Vehicles/BP_Tank"), (6000, y + 400, 100), (0, 90, 0), folder="Gym/Fixtures")
    text((6000, y - 300, 350), "Tank", 50)
    spawn(bp("World/Spawning/BP_RetrievalPoint"), (8500, y + 400, 0), folder="Gym/Fixtures")
    text((8500, y - 300, 250), "Retrieval point", 50)
    spawn(bp("World/Spawning/BP_LootSpawn_Weapon"), (10000, y, 50), folder="Gym/Fixtures")
    spawn(bp("World/Spawning/BP_LootSpawn_Item"), (10000, y + 500, 50), folder="Gym/Fixtures")
    text((10000, y - 300, 250), "Loot spawn points", 50)

    # ---- Row 5: AI pens ----
    y = 6500
    text((-9000, y - 900, 250), "AI", 160, color=(1, 0.8, 0.1))
    pens = [("Patrolling guard", "guard", "patrol"), ("Guard with keycard 1", "guard", "patrol"),
            ("Stationary guard", "guard", "stationary"), ("Sentry: sight only, holds position", "guard", "sentry"),
            ("VIP candidate", "vip", ""), ("Civilian", "civilian", ""), ("Sniper (tower)", "sniper", "")]
    for i, (label, kind, mode) in enumerate(pens):
        x = -9000 + i * 3000
        wall((x - 1200, y + 1200), (x + 1200, y + 1200), 0, 300, 25, (), GRID_DARK, "Gym/AI")
        wall((x - 1200, y - 600), (x - 1200, y + 1200), 0, 300, 25, (), GRID_DARK, "Gym/AI")
        wall((x + 1200, y - 600), (x + 1200, y + 1200), 0, 300, 25, (), GRID_DARK, "Gym/AI")
        text((x, y - 700, 320), label, 60)
        if kind == "sniper":
            box((x, y + 800, 300), (400, 400, 600), GRID_DARK, folder="Gym/AI")
            ladder((x, y + 575, 0), -90, 600)
            npc("sniper", (x, y + 800, 600), -90, folder="Gym/AI")
        else:
            route = patrol([(x - 700, y, 100), (x + 700, y, 100), (x + 700, y + 900, 100), (x - 700, y + 900, 100)], folder="Gym/AI") if mode == "patrol" else None
            npc(kind, (x, y + 300, 0), -90, route=route, keycard=1 if i == 1 else None, folder="Gym/AI",
                stationary=mode == "stationary", sentry=mode == "sentry")
    save_map()


# ---------------------------------------------------------------------------------------------
# LD test: San Lazaro — town, walled manor with basement / vents / control and breaker rooms,
# cave + tunnel, mountains around a basin, invisible walls.
# ---------------------------------------------------------------------------------------------

def building(cx, cy, w, d, floors=2, floor_h=350, door_side="S", folder="Town", windows=True, gutter=True, roof_access=True):
    """Hollow building with a door, vaultable windows per floor, floors, a stair and a gutter."""
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2
    H = floors * floor_h
    for f in range(floors):
        z = f * floor_h
        win = [(w * 0.25, 120, 100, 220), (w * 0.75, 120, 100, 220)] if windows else []
        south = list(win)
        if f == 0 and door_side == "S":
            south = [(w * 0.5, 120, 0, 240)] + [o for o in win if abs(o[0] - w * 0.5) > 200]
        north = list(win)
        if f == 0 and door_side == "N":
            north = [(w * 0.5, 120, 0, 240)] + [o for o in win if abs(o[0] - w * 0.5) > 200]
        wall((x0, y0), (x1, y0), z, floor_h, 25, south, GRID_DARK, folder)
        wall((x0, y1), (x1, y1), z, floor_h, 25, north, GRID_DARK, folder)
        side_win = [(d * 0.5, 120, 100, 220)] if windows else []
        wall((x0, y0), (x0, y1), z, floor_h, 25, side_win, GRID_DARK, folder)
        wall((x1, y0), (x1, y1), z, floor_h, 25, side_win, GRID_DARK, folder)
        if f > 0:
            # Floor slab with a stair hole on the east side.
            box((cx - 150, cy, z), (w - 300, d, 25), GRID, folder=folder)
            box((x1 - 150, cy + d / 4, z), (300, d / 2, 25), GRID, folder=folder)
    box((cx, cy, H), (w, d, 25), GRID, folder=folder)
    if floors > 1:
        for f in range(floors - 1):
            stairs((x1 - 150, cy - d / 2 + 60, f * floor_h), 90, floor_h, d / 2 - 60, 260, folder=folder)
    # Doors and windows actors.
    if door_side == "S":
        spawn(bp("World/Interactables/Door/BP_Door"), (cx - 60, y0, 0), folder=folder)
    else:
        spawn(bp("World/Interactables/Door/BP_Door"), (cx - 60, y1, 0), (0, 180, 0), folder=folder)
    if windows:
        for f in range(floors):
            for t in (0.25, 0.75):
                spawn(bp("World/Interactables/Window/BP_Window"), (x0 + w * t, y0, f * floor_h), folder=folder)
    if gutter:
        ladder((x0 - 15, cy + d * 0.3, 0), 180, H + 20, gutter=True, folder=folder)
    return (x0, x1, y0, y1, H)


def build_ldtest():
    new_map("L_LDTest")
    game_mode("BP_GM_Match")
    half = 9000
    floor((0, 0, 0), (2 * half + 4000, 2 * half + 4000, 0), GRID)
    environment(sun_pitch=-35, sun_yaw=60)
    nav_bounds((0, 0, 500), (half, half, 2500))

    # Basin: mountains all around (engine cones), invisible walls on the border.
    for i in range(36):
        a = math.radians(i * 10)
        r = half + 1500 + (i % 3) * 500
        size = 3500 + (i % 4) * 900
        box((math.cos(a) * r, math.sin(a) * r, size * 0.4), (size, size, size * 0.8 + (i % 5) * 600), mi("MI_Mountain"), folder="Mountains", shape="cone")
    for (cx, cy, sx, sy) in ((0, half, 2 * half, 100), (0, -half, 2 * half, 100), (half, 0, 100, 2 * half), (-half, 0, 100, 2 * half)):
        box((cx, cy, 2500), (sx, sy, 5000), GRID, folder="InvisibleWalls", hidden=True)

    # ---------------- Town (west) ----------------
    town = [(-6500, -4000, 1400, 1000, 2), (-4300, -4000, 1200, 1000, 3), (-6500, -1200, 1400, 1200, 2), (-4300, -1300, 1200, 1000, 2),
            (-6500, 1800, 1400, 1000, 3), (-4300, 1800, 1200, 1200, 2), (-6500, 4600, 1400, 1000, 2), (-4300, 4600, 1200, 1000, 3)]
    roofs = []
    for i, (cx, cy, w, d, floors) in enumerate(town):
        roofs.append(building(cx, cy, w, d, floors, door_side="S" if i % 2 == 0 else "N", folder="Town/Building%02d" % i))
    # Plaza with fountain, market, park with tall grass, barrels, distraction.
    box((-2400, 300, 60), (500, 500, 120), mi("MI_Stone"), folder="Town/Plaza", shape="cylinder")
    for i in range(4):
        box((-2600 + i * 350, -1500, 110), (250, 150, 220), mi(["MI_Red", "MI_Yellow", "MI_Blue", "MI_Green"][i]), folder="Town/Plaza")
    spawn(bp("World/Interactables/Distraction/BP_Distraction_Radio"), (-2600, -1500, 220), folder="Town/Plaza")
    for gx, gy in ((-2400, 3200), (-2400, 3800), (-1800, 3500)):
        spawn(bp("World/Interactables/Grass/BP_TallGrass"), (gx, gy, 55), folder="Town/Park")
    for bx, by in ((-3300, -2600), (-3200, 2800), (-5400, 3200)):
        spawn(bp("World/Interactables/Barrel/BP_ExplosiveBarrel"), (bx, by, 0), folder="Town/Props")
    spawn(bp("World/Interactables/Locker/BP_Locker"), (-3400, 600, 0), (0, 90, 0), folder="Town/Props")

    # ---------------- Manor compound (east) ----------------
    mx, my = 4500, 0
    cw, cd = 7000, 7000
    gate = (cw / 2, 400, 0, 260)
    wall((mx - cw / 2, my - cd / 2), (mx + cw / 2, my - cd / 2), 0, 350, 40, [gate], GRID_ALT, "Manor/Wall")
    wall((mx - cw / 2, my + cd / 2), (mx + cw / 2, my + cd / 2), 0, 350, 40, [(1500, 110, 0, 230)], GRID_ALT, "Manor/Wall")
    wall((mx - cw / 2, my - cd / 2), (mx - cw / 2, my + cd / 2), 0, 350, 40, [(cd / 2, 110, 0, 230)], GRID_ALT, "Manor/Wall")
    wall((mx + cw / 2, my - cd / 2), (mx + cw / 2, my + cd / 2), 0, 350, 40, (), GRID_ALT, "Manor/Wall")
    spawn(bp("World/Interactables/Door/BP_Door_Keycard1"), (mx - cw / 2, my - 55, 0), (0, 90, 0), folder="Manor/Wall")
    spawn(bp("World/Interactables/Door/BP_Door"), (mx - cw / 2 + 1445, my + cd / 2, 0), (0, 180, 0), folder="Manor/Wall")
    ladder((mx + cw / 2 + 40, my + 1500, 0), 0, 350, gutter=True, folder="Manor/Wall")
    # Gate defences: camera, mounted gun.
    gate_cam = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (mx - 300, my - cd / 2 + 60, 330), (-25, 90, 0), folder="Manor/Security")
    spawn(bp("World/Vehicles/BP_MountedGun"), (mx + 600, my - cd / 2 + 400, 100), (0, -90, 0), folder="Manor/Security")
    box((mx + 600, my - cd / 2 + 400, 50), (150, 150, 100), mi("MI_Dark"), folder="Manor/Security")
    # Garden: hedges and tall grass.
    for gx in (-2400, -1200, 1200, 2400):
        box((mx + gx, my - 2200, 70), (800, 120, 140), mi("MI_Green"), folder="Manor/Garden")
    for gx, gy in ((mx - 2600, my + 2400), (mx + 2600, my + 2400)):
        spawn(bp("World/Interactables/Grass/BP_TallGrass"), (gx, gy, 55), folder="Manor/Garden")

    # Manor house: 2 floors, rooms, basement.
    hx0, hx1, hy0, hy1 = mx - 1800, mx + 1800, my - 1000, my + 1400
    FH = 380
    for f in range(2):
        z = f * FH
        wall((hx0, hy0), (hx1, hy0), z, FH, 30, [(1800, 160, 0, 260)] if f == 0 else [(900, 120, 100, 220), (2700, 120, 100, 220)], GRID_DARK, "Manor/House")
        wall((hx0, hy1), (hx1, hy1), z, FH, 30, [(900, 120, 100, 220), (2700, 120, 100, 220)], GRID_DARK, "Manor/House")
        wall((hx0, hy0), (hx0, hy1), z, FH, 30, [(1200, 120, 100, 220)], GRID_DARK, "Manor/House")
        wall((hx1, hy0), (hx1, hy1), z, FH, 30, [(1200, 120, 100, 220)], GRID_DARK, "Manor/House")
        if f == 1:
            box((mx - 200, my + 200, z), (3200, 2400, 25), GRID, folder="Manor/House")
    box((mx, my + 200, 2 * FH), (3600, 2400, 25), GRID, folder="Manor/House")
    spawn(bp("World/Interactables/Door/BP_Door"), (mx - 80, hy0, 0), folder="Manor/House")
    for f in range(2):
        for t in (0.25, 0.75):
            spawn(bp("World/Interactables/Window/BP_Window"), (hx0 + 3600 * t, hy1, f * FH), (0, 180, 0), folder="Manor/House")
    stairs((hx1 - 200, hy0 + 150, 0), 90, FH, 1100, 300, folder="Manor/House")
    ladder((hx1 + 20, hy1 - 400, 0), 0, 2 * FH + 20, gutter=True, folder="Manor/House")
    # Interior: dining room (sealed: door + window + vent hatch) on ground floor west.
    dining_door = spawn(bp("World/Interactables/Door/BP_Door"), (hx0 + 1300, my + 200 - 55, 0), (0, 90, 0), folder="Manor/Dining")
    wall((hx0 + 1300, hy0), (hx0 + 1300, hy1), 0, FH, 25, [(1200, 110, 0, 230)], GRID_DARK, "Manor/Dining")
    wall((hx0, my + 200), (hx0 + 1300, my + 200), 0, FH, 25, [(650, 120, 0, 120)], GRID_DARK, "Manor/Dining")
    dining_hatch = spawn(bp("World/Interactables/Vent/BP_VentHatch"), (hx0 + 650, my + 200, 0), folder="Manor/Dining")
    dining = spawn(bp("World/Interactables/Vent/BP_RoomVolume"), (hx0 + 650, my + 800, 190), folder="Manor/Dining")
    if dining:
        dining.set_actor_scale3d(unreal.Vector(1.6, 1.4, 1.2))
        west_windows = [a for a in EAS.get_all_level_actors() if a.get_class().get_name().startswith("BP_Window") and abs(a.get_actor_location().y - hy1) < 5 and a.get_actor_location().x < hx0 + 1300 and a.get_actor_location().z < 10]
        dining.set_editor_property("openings", [a for a in [dining_door, dining_hatch] + west_windows if a])
    if dining_hatch and dining:
        dining_hatch.set_editor_property("linked_rooms", [dining])
    box((hx0 + 650, my + 800, 40), (600, 200, 80), mi("MI_Wood"), folder="Manor/Dining")
    drink = spawn(bp("World/Interactables/Drink/BP_PoisonableDrink"), (hx0 + 650, my + 800, 80), folder="Manor/Dining")
    # Study with the VIP desk upstairs; control room behind a keycard door.
    control_door = spawn(bp("World/Interactables/Door/BP_Door_Keycard1"), (hx1 - 1500, my + 600 - 55, FH), (0, 90, 0), folder="Manor/Control")
    wall((hx1 - 1500, my + 100), (hx1 - 1500, hy1), FH, FH, 25, [(500, 110, 0, 230)], GRID_DARK, "Manor/Control")
    wall((hx1 - 1500, my + 100), (hx1, my + 100), FH, FH, 25, (), GRID_DARK, "Manor/Control")
    console = spawn(bp("World/Interactables/Security/BP_ControlConsole"), (hx1 - 700, hy1 - 250, FH), (0, 180, 0), folder="Manor/Control")
    hall_cam = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (hx0 + 200, hy0 + 200, FH - 40), (-30, 45, 0), folder="Manor/Security")
    cams = [c for c in (gate_cam, hall_cam) if c]
    if console:
        console.set_editor_property("linked_cameras", cams)
    for i, cam in enumerate(cams):
        monitor = spawn(bp("World/Interactables/Security/BP_SecurityMonitor"), (hx1 - 1000 + i * 400, hy1 - 60, FH + 200), (0, 180, 0), folder="Manor/Control")
        if monitor:
            monitor.set_editor_property("linked_camera", cam)
    # Basement / foundations: side stairs down, breaker room, vent duct, tunnel to the cave.
    BZ = -450
    floor((mx, my + 200, BZ), (3600, 2400, 0), GRID_DARK, "Manor/Basement")
    box((mx, my + 200, -25), (3600, 2400, 50), GRID, folder="Manor/Basement")
    for (a, b) in (((hx0, hy0), (hx1, hy0)), ((hx0, hy1), (hx1, hy1)), ((hx0, hy0), (hx0, hy1)), ((hx1, hy0), (hx1, hy1))):
        wall(a, b, BZ, -BZ - 50, 40, [(1200, 300, 0, 300)] if a == (hx1, hy0) else (), GRID_DARK, "Manor/Basement")
    # Exterior side stairs going down (east side).
    box((hx1 + 300, my + 200, BZ / 2), (600, 400, -BZ), GRID, folder="Manor/Basement")  # stair well wall
    for i in range(10):
        box((hx1 + 600 - i * 60, my + 200, -i * 45 - 22), (600, 380, 45), GRID_DARK, folder="Manor/Basement")
    breaker = spawn(bp("World/Interactables/Power/BP_CircuitBreaker"), (hx0 + 300, hy0 + 60, BZ), folder="Manor/Basement")
    blackout = spawn(bp("World/Interactables/Power/BP_BlackoutVolume"), (mx, my + 200, FH), folder="Manor/House")
    if blackout:
        blackout.set_actor_scale3d(unreal.Vector(2.3, 1.6, 1.4))
    lights = []
    for lx in (-1200, 0, 1200):
        for f in range(2):
            lamp = spawn(bp("World/Interactables/Power/BP_LightFixture"), (mx + lx, my + 200, f * FH + FH - 40), folder="Manor/House")
            if lamp:
                lights.append(lamp)
    if breaker:
        breaker.set_editor_property("linked_lights", lights)
        breaker.set_editor_property("linked_volumes", [blackout] if blackout else [])
    # Crawl vent: from the basement up to the dining hatch.
    box((hx0 + 650, my + 60, BZ + 400), (130, 20, 820), GRID_DARK, folder="Manor/Vent")
    turret = spawn(bp("World/Interactables/Security/BP_Turret"), (mx, hy0 + 300, 2 * FH + 15), (0, -90, 0), folder="Manor/Security")
    panel = spawn(bp("World/Interactables/Security/BP_HackPanel"), (hx1 - 400, hy1 - 60, BZ), (0, 180, 0), folder="Manor/Basement")
    if turret and panel:
        panel.set_editor_property("linked_turret", turret)
    # Sniper tower in the compound corner, zipline down to a town roof.
    tx, ty = mx + cw / 2 - 400, my - cd / 2 + 400
    box((tx, ty, 600), (500, 500, 1200), GRID_ALT, folder="Manor/Tower")
    ladder((tx - 275, ty, 0), 180, 1200, folder="Manor/Tower")
    npc("sniper", (tx, ty, 1200), 180, folder="Manor/AI")
    zl = spawn(bp("World/Interactables/Zipline/BP_Zipline"), (mx - cw / 2 - 100, my - 3000, 700), folder="Manor/Tower")
    if zl:
        x0, x1, y0, y1, H = roofs[1]
        zl.set_editor_property("end_point", unreal.Vector((x1 - 200) - (mx - cw / 2 - 100), (y0 + 500) - (my - 3000), H + 150 - 700))
    box((mx - cw / 2 - 100, my - 3000, 350), (300, 300, 700), GRID_ALT, folder="Manor/Tower")
    ladder((mx - cw / 2 - 275, my - 3000, 0), 180, 700, folder="Manor/Tower")

    # ---------------- Cave and tunnel (north) ----------------
    cx, cy = 1000, 6800
    for (a, b, op) in (((cx - 1500, cy - 1200), (cx + 1500, cy - 1200), [(1500, 500, 0, 450)]), ((cx - 1500, cy + 1200), (cx + 1500, cy + 1200), ()),
                       ((cx - 1500, cy - 1200), (cx - 1500, cy + 1200), ()), ((cx + 1500, cy - 1200), (cx + 1500, cy + 1200), ())):
        wall(a, b, 0, 700, 300, op, mi("MI_Mountain"), "Cave")
    box((cx, cy, 750), (3300, 2700, 100), mi("MI_Mountain"), folder="Cave")
    for ox, oy in ((-800, 300), (700, -200), (200, 700)):
        box((cx + ox, cy + oy, 150), (300, 300, 300), mi("MI_Stone"), folder="Cave", shape="sphere")
    spawn(bp("World/Vehicles/BP_Tank"), (cx - 600, cy - 300, 100), (0, -90, 0), folder="Cave")
    # Tunnel from the cave to the manor basement (underground corridor).
    tz = BZ
    tunnel_points = [(cx + 1500, cy - 600), (hx1 + 1200, cy - 600), (hx1 + 1200, my + 1600)]
    for (a, b) in zip(tunnel_points[:-1], tunnel_points[1:]):
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        yaw = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        nx, ny = -math.sin(math.radians(yaw)), math.cos(math.radians(yaw))
        box((mid[0], mid[1], tz - 10), (length + 400, 400, 20), GRID_DARK, yaw, folder="Tunnel")
        box((mid[0], mid[1], tz + 320), (length + 400, 400, 20), GRID_DARK, yaw, folder="Tunnel")
        for s in (-1, 1):
            box((mid[0] + nx * 200 * s, mid[1] + ny * 200 * s, tz + 160), (length + 400, 20, 340), GRID_DARK, yaw, folder="Tunnel")
    ramp_x = cx + 1300
    for i in range(10):
        box((ramp_x + i * 60, cy - 600, -i * 45 - 22), (120, 380, 45), GRID_DARK, folder="Tunnel")
    tp_town = spawn(bp("World/Interactables/Teleporter/BP_Teleporter"), (-2400, 4500, 0), (0, 0, 0), folder="Gameplay/Teleport")
    tp_cave = spawn(bp("World/Interactables/Teleporter/BP_Teleporter"), (cx + 900, cy + 600, 0), (0, 180, 0), folder="Gameplay/Teleport")
    if tp_town and tp_cave:
        tp_town.set_editor_property("destination", tp_cave)
        tp_cave.set_editor_property("destination", tp_town)

    # ---------------- Gameplay: VIP candidates, guards, loot, retrieval, starts ----------------
    vip_spots = [(hx0 + 650, my + 1000, 0), (hx1 - 600, my - 600, FH), (mx, my - 600, 0), (mx - 900, my + 900, FH), (cx, cy, 0),
                 (-2400, -400, 0), (-6500, -4000, 0), (-4300, 1800, 0), (-6500, 4600, 350), (mx + 2500, my + 2500, 0),
                 (mx - 2500, my - 2500, 0), (-2000, 2600, 0), (2000, -5500, 0), (hx0 + 300, hy0 + 400, BZ), (-4300, -1300, 350)]
    for loc in vip_spots:
        npc("vip", loc, 0, folder="AI/VIPCandidates")
    guard_routes = [
        [(mx - 2500, my - 3000, 100), (mx + 2500, my - 3000, 100), (mx + 2500, my + 3000, 100), (mx - 2500, my + 3000, 100)],
        [(mx - 1500, my - 1500, 100), (mx + 1500, my - 1500, 100)],
        [(hx0 + 300, hy0 + 300, 100), (hx1 - 300, hy0 + 300, 100), (hx1 - 300, hy1 - 300, 100)],
        [(hx0 + 300, hy0 + 300, FH + 100), (hx1 - 300, hy1 - 300, FH + 100)],
        [(hx0 + 400, hy0 + 400, BZ + 100), (hx1 - 400, hy1 - 400, BZ + 100)],
        [(-3200, -5000, 100), (-3200, 5500, 100)],
        [(-5400, -2800, 100), (-5400, 3200, 100), (-2000, 3200, 100)],
        [(-2400, -900, 100), (-1600, 1200, 100), (-2800, 1500, 100)],
        [(cx - 1000, cy - 800, 100), (cx + 1000, cy + 800, 100)],
        [(2000, -6000, 100), (2000, 6000, 100)],
    ]
    for i, route_points in enumerate(guard_routes):
        route = patrol(route_points, folder="AI/Routes")
        start = route_points[0]
        npc("guard", (start[0], start[1], start[2] - 100), 0, route=route, keycard=1 if i in (2, 5) else None, folder="AI/Guards")
    # Stationary guards; the pair at the manor gate are sentries (sight only, never leave the gate).
    for i, (gx, gy, gz) in enumerate(((mx - 400, my - cd / 2 + 300, 0), (mx + 400, my - cd / 2 + 300, 0), (hx1 - 200, hy0 - 200, 0),
                                      (mx, my + cd / 2 - 300, 0), (-4300, -2600, 0), (-6500, 3200, 0))):
        npc("guard", (gx, gy, gz), 180, folder="AI/Guards", stationary=True, sentry=i < 2)
    for gx, gy in ((-2200, 500), (-2700, -1200), (-1900, 3600), (mx - 2000, my + 1800)):
        npc("civilian", (gx, gy, 0), 90, folder="AI/Civilians")

    for x, y in ((mx + 2800, my + 2800), (-7800, -7600), (cx, cy + 900), (-2400, 0), (2500, -7500), (-7500, 7500)):
        spawn(bp("World/Spawning/BP_RetrievalPoint"), (x, y, 0), folder="Gameplay/Retrieval")
    loot_w = [(-6500, -4000, 30), (-4300, 4600, 380), (-2600, -1500, 230), (mx, my - 2600, 30), (hx1 - 500, hy1 - 500, FH + 30), (mx - 1000, my + 300, BZ + 30),
              (cx + 400, cy - 400, 30), (2000, 0, 30), (-1500, -6000, 30), (-7500, 0, 30), (6500, -6500, 30), (mx + 3000, my + 1000, 30)]
    loot_i = [(-6500, -1200, 30), (-4300, -4000, 380), (-2300, 3000, 30), (mx - 2600, my - 1000, 30), (hx0 + 400, hy1 - 400, FH + 30), (hx1 - 600, hy0 + 600, BZ + 30),
              (cx - 900, cy + 600, 30), (0, -3000, 30), (-3000, 6000, 30), (-7000, -7000, 30), (7000, 7000, 30), (mx + 2000, my - 3000, 30)]
    for loc in loot_w:
        spawn(bp("World/Spawning/BP_LootSpawn_Weapon"), loc, folder="Gameplay/Loot")
    for loc in loot_i:
        spawn(bp("World/Spawning/BP_LootSpawn_Item"), loc, folder="Gameplay/Loot")
    # One sheltered start per team, in the corners.
    corners = ((-6200, -6800), (7600, -6400), (-6800, 6800), (6800, 5600))
    for (sx, sy) in corners:
        box((sx, sy + 450 * (1 if sy < 0 else -1), 200), (1000, 40, 400), GRID_ALT, folder="Gameplay/Starts")
        box((sx + 450 * (1 if sx < 0 else -1), sy, 200), (40, 1000, 400), GRID_ALT, folder="Gameplay/Starts")
    team_starts([(corners[0][0], corners[0][1], 45), (corners[1][0], corners[1][1], 135), (corners[2][0], corners[2][1], -45), (corners[3][0], corners[3][1], -135)])
    save_map()


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


def run():
    build_title()
    build_lobby()
    build_empty("L_Empty_01")
    build_empty("L_Empty_02")
    if should_build("L_Gym"):
        build_gym()
    if should_build("L_LDTest"):
        build_ldtest()
    link_map_definitions()
    LES.load_level(path("Maps", "L_TitleScreen"))
