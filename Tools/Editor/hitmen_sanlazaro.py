"""San Lazaro (L_LDTest), 0.7.0: one organic map on hills, about 240 x 210 m playable.

- West: a dense favela on a hillside, five terraces of small houses (1 to 3 floors) facing paved alleys, stairs
  between terraces in cross alleys, yards with low walls, roofs reachable from the terrace above, a market, a bell
  tower; every facade has vaultable windows and a ledge band under them (hang and shimmy).
- Middle: a dry ravine (no water) crossed by two arched bridges that meet the ground on both sides, and a high
  walkway between two watchtowers.
- East: the walled mansion on a plateau (grounds about 40 % smaller than 0.6), the same house with its basement, a
  small maze, the greenhouse, the garage with the tank.
- North: a rocky ridge with a real cave dug into it (an entrance in its flank, three chambers under the rock, lit),
  and a tunnel from the last chamber to the mansion basement.
- Outside an irregular outline the ground rises into mountains; invisible walls follow the outline.

The ground is a heightfield (static mesh tiles, collision = mesh) built by UHitmenContentTools::BuildHeightfieldMesh.
Run through hitmen_levels.build_ldtest (HITMEN_FORCE_MAPS=L_LDTest). Then: bake the navmesh, run
hitmen_check_reach.py (problems 0), rebake the map texture (docs/Pipelines.md).
"""
import math
import random

import unreal

import hitmen_levels as L
from hitmen_common import *
from hitmen_levels import (GRID, GRID_ALT, GRID_DARK, box, bp, building, environment, floor, game_mode, hedge_maze, ladder,
                           mi, nav_bounds, new_map, npc, patrol, room_light, save_map, spawn, stair_well, stairs, team_starts,
                           text, tunnel, wall)

# ---------------------------------------------------------------------------------------------
# Layout (cm)
# ---------------------------------------------------------------------------------------------

BOUNDS = 12800            # DA_Map world bounds (square): minimap, pause map, spectator limits
TERRAIN_HALF = 16000      # the heightfield runs into the mountains
CELL = 100.0              # heightfield spacing (terrace steps hide inside 1 m retaining walls)
TILE = 64                 # cells per tile side
TERRAIN_FOLDER = "/Game/Hitmen/Maps/Terrain/LDTest"

# Hand-placed outline (clockwise from the west), subdivided with small seeded offsets so it never looks drawn.
OUTLINE_KEYS = [(-12000, -2000), (-11800, -6000), (-10800, -8800), (-8000, -10200), (-4000, -10600), (-1000, -10200),
                (2500, -10400), (6500, -10000), (10500, -9800), (12300, -8000), (12600, -4000), (12200, 0),
                (12500, 4000), (11500, 7500), (9000, 10000), (5000, 11200), (1000, 11000), (-2500, 10600),
                (-5500, 9500), (-8500, 8000), (-10800, 5500), (-12200, 2500)]

# Village terraces, east (low, by the ravine) to west (high): (x west, x east, floor height).
TERRACES = [(-4700, -3200, 100.0), (-6200, -4700, 500.0), (-7700, -6200, 950.0), (-9200, -7700, 1300.0), (-10700, -9200, 1550.0)]
VILLAGE_Y = (-7500.0, 3000.0)
ALLEY_W = 420.0
HOUSE_D = 900.0           # house depth (x)
MARKET_Y = (-1700.0, 1700.0)   # on terrace 1: no houses, the market

# Estate plateau.
EZ = 300.0
ESTATE = (1500.0, 11000.0, -8500.0, 2500.0)          # walls: x0, x1, y0, y1
ESTATE_PAD = (1100.0, 11400.0, -8900.0, 4600.0)      # flat at EZ (north to the ridge foot: the tunnel runs under it)
GATE_Y = -3000.0

# Cave: the floor is cut into the ridge (the walls are the terrain, lumpy), a heightfield roof continues the ridge above
# it and a rock slab is its ceiling. Chambers are lumpy ellipses (cx, cy, rx, ry); passages are segments with a half
# width; the first passage is the entrance: an open trench through the rock mound, covered from COVER_Y on.
CZ = 0.0
CAVE_CHAMBERS = [(300.0, 6400.0, 1100.0, 950.0), (3400.0, 7400.0, 1350.0, 1000.0), (6400.0, 6900.0, 1050.0, 850.0)]
CAVE_LINKS = [((300.0, 3500.0), (300.0, 5600.0), 320.0), ((1250.0, 6650.0), (2200.0, 7150.0), 300.0), ((4650.0, 7250.0), (5500.0, 7000.0), 300.0)]
COVER_Y = 4400.0
ENTRANCE_RAMP = (3500.0, 4100.0)   # y: the trench floor goes from the ground outside down to the cave floor
_MOUTH_H = []
CAVE_HEIGHT = 720.0
PASSAGE_HEIGHT = 460.0
CAVE_ROOF_RECT = (-1200.0, 7800.0, 4300.0, 8800.0)   # x0, x1, y0, y1 of the roof heightfield

BRIDGES_Y = (-3000.0, 2400.0)
WALKWAY_Y = -500.0


def smoothstep(e0, e1, x):
    t = (x - e0) / (e1 - e0)
    t = 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)
    return t * t * (3.0 - 2.0 * t)


def ravine_x(y):
    return -1300.0 + 450.0 * math.sin(y / 3500.0)


def segment_coords(x, y, a, b):
    """(distance along a->b, distance from the segment line, length)."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    t = (x - a[0]) * ux + (y - a[1]) * uy
    return t, abs(-(x - a[0]) * uy + (y - a[1]) * ux), length


def in_cave(x, y):
    for (cx, cy, rx, ry) in CAVE_CHAMBERS:
        dx, dy = (x - cx) / rx, (y - cy) / ry
        r2 = dx * dx + dy * dy
        if r2 < 1.5:
            ang = math.atan2(dy, dx)
            lump = 1.0 + 0.12 * math.sin(3 * ang + cx * 0.001) + 0.07 * math.sin(7 * ang + cy * 0.001)
            if r2 < lump * lump:
                return True
    for (a, b, hw) in CAVE_LINKS:
        t, d, length = segment_coords(x, y, a, b)
        if -60.0 <= t <= length + 60.0 and d < hw + 70.0 * math.sin(t * 0.004 + a[0] * 0.001):
            return True
    return False


def cave_rects():
    """Bounding rectangles (x0, x1, y0, y1) of every chamber and passage."""
    rects = [(cx - rx * 1.25, cx + rx * 1.25, cy - ry * 1.25, cy + ry * 1.25) for (cx, cy, rx, ry) in CAVE_CHAMBERS]
    for (a, b, hw) in CAVE_LINKS:
        m = hw + 120
        rects.append((min(a[0], b[0]) - m, max(a[0], b[0]) + m, min(a[1], b[1]) - m, max(a[1], b[1]) + m))
    return rects


# ---------------------------------------------------------------------------------------------
# Outline
# ---------------------------------------------------------------------------------------------

def outline():
    rng = random.Random(11)
    pts = []
    n = len(OUTLINE_KEYS)
    for i in range(n):
        a = OUTLINE_KEYS[i]
        b = OUTLINE_KEYS[(i + 1) % n]
        pts.append(a)
        mx, my = (a[0] + b[0]) / 2.0, (a[1] + b[1]) / 2.0
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        nx, ny = -(b[1] - a[1]) / length, (b[0] - a[0]) / length
        off = rng.uniform(-350.0, 350.0)
        pts.append((mx + nx * off, my + ny * off))
    return pts


OUTLINE = outline()
_SEGS = [(OUTLINE[i], OUTLINE[(i + 1) % len(OUTLINE)]) for i in range(len(OUTLINE))]


def outline_distance(x, y):
    """Signed distance to the outline: negative inside the playable area."""
    inside = False
    best = 1e18
    for (ax, ay), (bx, by) in _SEGS:
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            inside = not inside
        dx, dy = bx - ax, by - ay
        t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)))
        px, py = ax + dx * t - x, ay + dy * t - y
        d2 = px * px + py * py
        if d2 < best:
            best = d2
    d = math.sqrt(best)
    return -d if inside else d


# ---------------------------------------------------------------------------------------------
# Height
# ---------------------------------------------------------------------------------------------

def pad_weight(x, y, rect, feather):
    x0, x1, y0, y1 = rect
    dx = max(x0 - x, 0.0, x - x1)
    dy = max(y0 - y, 0.0, y - y1)
    if dx <= 0.0 and dy <= 0.0:
        return 1.0
    if feather <= 0.0:
        return 0.0
    return 1.0 - smoothstep(0.0, feather, math.hypot(dx, dy))


def in_rect(x, y, rect, margin=0.0):
    return rect[0] - margin <= x <= rect[1] + margin and rect[2] - margin <= y <= rect[3] + margin


def natural_height(x, y, d_out):
    h = 110.0 * math.sin(x / 2300.0 + 0.4) * math.sin(y / 2700.0 - 0.8) + 70.0 * math.sin((x + y) / 3900.0)
    h += 1550.0 * smoothstep(-3000.0, -11000.0, x)                          # the village hillside
    ridge = 2600.0 * smoothstep(4000.0, 6200.0, y) + 500.0 * smoothstep(7000.0, 10500.0, y)
    ridge += (260.0 * math.sin(x / 900.0) * math.sin(y / 1100.0) + 150.0 * math.sin(x / 430.0 + y / 610.0)) * smoothstep(4600.0, 6400.0, y)
    h += ridge
    # The rock mound the cave entrance is cut through.
    h += 1100.0 * (1.0 - smoothstep(350.0, 1500.0, abs(x - 300.0))) * smoothstep(3500.0, 4600.0, y) * (1.0 - smoothstep(5600.0, 6800.0, y))
    if d_out > -800.0:
        crag = 500.0 * math.sin(x / 1300.0 + y / 900.0) * math.sin(y / 1700.0 - x / 2100.0)
        h += 2600.0 * smoothstep(-800.0, 2600.0, d_out) + 2200.0 * smoothstep(2600.0, 6000.0, d_out) + crag * smoothstep(0.0, 2500.0, d_out)
    return h


def height(x, y, cave=True):
    d_out = outline_distance(x, y)
    h = natural_height(x, y, d_out)
    # The estate plateau.
    w = pad_weight(x, y, ESTATE_PAD, 1700.0)
    if w > 0.0:
        h += (EZ - h) * w
    # Village terraces: sharp between terraces (retaining walls), feathered at the row ends and the outer edges.
    for k, (xw, xe, tz) in enumerate(TERRACES):
        if xw <= x <= xe:
            wx = 1.0
        elif k == 0 and x > xe:
            wx = 1.0 - smoothstep(0.0, 400.0, x - xe)
        elif k == len(TERRACES) - 1 and x < xw:
            wx = 1.0 - smoothstep(0.0, 800.0, xw - x)
        else:
            wx = 0.0
        if wx <= 0.0:
            continue
        dy = max(VILLAGE_Y[0] - y, 0.0, y - VILLAGE_Y[1])
        wy = 1.0 - smoothstep(0.0, 1300.0, dy)
        w = wx * wy
        if w > 0.0:
            h += (tz - h) * w
    # The cave, cut into the ridge; the entrance trench ramps down from the ground outside.
    if cave and in_cave(x, y):
        h = CZ
        a, b, hw = CAVE_LINKS[0]
        if abs(x - a[0]) < hw + 200 and y < ENTRANCE_RAMP[1]:
            if not _MOUTH_H:
                _MOUTH_H.append(height(a[0], ENTRANCE_RAMP[0] - 150, cave=False))
            h = CZ + (_MOUTH_H[0] - CZ) * (1.0 - smoothstep(ENTRANCE_RAMP[0], ENTRANCE_RAMP[1], y))
    # The dry ravine.
    prof = 1.0 - smoothstep(400.0, 1300.0, abs(x - ravine_x(y)))
    if prof > 0.0:
        h -= 700.0 * prof * (1.0 - smoothstep(3200.0, 4600.0, y))
    return h


def gz(x, y):
    """Ground height at a point (for placing actors)."""
    return height(x, y)


# ---------------------------------------------------------------------------------------------
# Terrain tiles
# ---------------------------------------------------------------------------------------------

def build_terrain(holes):
    material = load(path("Materials", "Master", "M_Hitmen_Terrain"))
    boxes = [unreal.Box2D(unreal.Vector2D(h[0], h[1]), unreal.Vector2D(h[2], h[3])) for h in holes]
    tiles = int(2 * TERRAIN_HALF / (TILE * CELL))
    n = TILE + 1
    for ty in range(tiles):
        for tx in range(tiles):
            ox = -TERRAIN_HALF + tx * TILE * CELL
            oy = -TERRAIN_HALF + ty * TILE * CELL
            heights = []
            for iy in range(n):
                y = oy + iy * CELL
                for ix in range(n):
                    heights.append(float(height(ox + ix * CELL, y)))
            asset = "%s/SM_Terrain_%d_%d" % (TERRAIN_FOLDER, tx, ty)
            tile_holes = [b for b, h in zip(boxes, holes) if h[2] > ox and h[0] < ox + TILE * CELL and h[3] > oy and h[1] < oy + TILE * CELL]
            mesh = unreal.HitmenContentTools.build_heightfield_mesh(asset, heights, n, n, CELL, unreal.Vector2D(ox, oy), tile_holes, material, False)
            actor = spawn(unreal.StaticMeshActor, (ox, oy, 0), label="Terrain_%d_%d" % (tx, ty), folder="Terrain")
            actor.static_mesh_component.set_static_mesh(mesh)
            actor.tags = [unreal.Name("HitmenTerrain")]
    log("terrain", tiles * tiles, "tiles")


def build_outline_walls():
    for (ax, ay), (bx, by) in _SEGS:
        length = math.hypot(bx - ax, by - ay)
        yaw = math.degrees(math.atan2(by - ay, bx - ax))
        top = max(gz(ax, ay), gz(bx, by)) + 3000.0
        box(((ax + bx) / 2, (ay + by) / 2, top / 2 - 500), (length + 200, 100, top + 1000), GRID, yaw, folder="InvisibleWalls", hidden=True)



# ---------------------------------------------------------------------------------------------
# Village (favela)
# ---------------------------------------------------------------------------------------------

def street_lamp(x, y, z, folder, lamps):
    box((x, y, z + 220), (25, 25, 440), mi("MI_Dark"), folder=folder)
    box((x - 40, y, z + 445), (100, 20, 15), mi("MI_Dark"), folder=folder)
    lamp = room_light(x - 80, y, z + 430, folder, lumens=700.0, radius=1400.0)
    if lamp:
        lamps.append(lamp)


def build_village(info):
    rng = random.Random(70)
    lamps = []
    houses = []
    stairs_at = []
    for k, (xw, xe, tz) in enumerate(TERRACES):
        row = "Village/Row%d" % k
        hx = xe - ALLEY_W - 40 - HOUSE_D / 2          # house centre x (the alley along the east side)
        back = hx - HOUSE_D / 2
        # Paved alley.
        box((xe - ALLEY_W / 2, sum(VILLAGE_Y) / 2, tz + 1), (ALLEY_W, VILLAGE_Y[1] - VILLAGE_Y[0], 2), mi("MI_Stone"), folder=row + "/Alley")
        for ly in range(int(VILLAGE_Y[0]) + 700, int(VILLAGE_Y[1]), 1500):
            street_lamp(xe - 30, ly, tz, row + "/Lamps", lamps)
        y = VILLAGE_Y[0] + 250
        slot = 0
        upper = TERRACES[k + 1][2] if k + 1 < len(TERRACES) else None
        roofs = []
        planks_y = []
        while y < VILLAGE_Y[1] - 500:
            if k == 1 and MARKET_Y[0] - 1500 < y < MARKET_Y[1]:
                y = MARKET_Y[1] + 300                      # the market
                continue
            if k == 4 and y < -6200:
                y = -6200                                  # the bell tower stands here
                continue
            if slot % 3 == 2 and upper is not None:
                # Cross alley with a stair up to the next terrace.
                cy = y + 210
                rise = upper - tz
                run = min(HOUSE_D - 40, max(500.0, rise * 2.0))
                stairs((xw + 110 + run, cy, tz), 180, rise, run, 360, material=mi("MI_Stone"), folder=row + "/Stairs")
                stairs_at.append((k, cy))
                y += ALLEY_W
                slot += 1
                continue
            wy = rng.choice((900.0, 1000.0, 1100.0, 1200.0))
            if y + wy > VILLAGE_Y[1] - 250:
                break
            floors = rng.choice((1, 2, 2, 3)) if k < 4 else rng.choice((1, 2))
            cy = y + wy / 2
            # Never toward a cross alley (its stair fills it): after one, the door faces north; before one, south.
            rng.random()
            door = "N" if slot % 3 == 0 else "S"
            r = building(hx, cy, HOUSE_D, wy, floors, 350, door_side=door, folder="%s/House%02d" % (row, slot), z0=tz, gutter=False,
                         blind_faces=("W",))
            houses.append((hx, cy, HOUSE_D, wy, floors, tz))
            roofs.append((cy, wy, floors, r[4]))
            # Roofs one floor high sit about the next terrace's level: a plank from its edge onto the roof.
            if upper is not None and floors == 1:
                box(((xw + 110 + back) / 2, cy, (upper + r[4]) / 2 + 12), (back - xw - 110 + 60, 160, 10), mi("MI_Wood"), folder=row + "/Planks")
                planks_y.append(cy)
            gap = rng.choice((260.0, 300.0, 300.0, 460.0))
            if gap > 400:
                # A small yard: low walls on the alley side (with a gap) and the sides.
                yy0, yy1 = y + wy + 30, y + wy + gap - 30
                wall((xe - ALLEY_W - 10, yy0), (xe - ALLEY_W - 10, yy1), tz, 110, 20, [((yy1 - yy0) / 2, 140, 0, 110)], mi("MI_Stone"), row + "/Yards")
                if rng.random() < 0.5:
                    spawn(bp("World/Interactables/Barrel/BP_ExplosiveBarrel"), (hx, y + wy + gap / 2, tz), folder=row + "/Yards")
            y += wy + gap
            slot += 1
        # Planks between neighbouring roofs of the same height.
        for (ay, aw, af, az), (by_, bw, bf, bz) in zip(roofs[:-1], roofs[1:]):
            if af == bf and af >= 2:
                y0, y1 = ay + aw / 2, by_ - bw / 2
                box((hx, (y0 + y1) / 2, az + 18), (160, y1 - y0 + 120, 10), mi("MI_Wood"), folder=row + "/Planks")
        # Retaining wall up to the next terrace, its parapet open at the stair tops.
        if upper is not None:
            y0, y1 = VILLAGE_Y[0] - 600, VILLAGE_Y[1] + 600
            box((xw + 50, (y0 + y1) / 2, (tz - 40 + upper) / 2), (120, y1 - y0, upper - tz + 40), mi("MI_Stone"), folder=row + "/Retaining")
            openings = [(cy - y0, 380, 0, 100) for (kk, cy) in stairs_at if kk == k] + [(py - y0, 180, 0, 100) for py in planks_y]
            wall((xw - 10, y0), (xw - 10, y1), upper, 100, 20, openings, GRID_ALT, row + "/Parapet")
    info["village_lamps"] = lamps
    info["houses"] = houses
    info["stairs"] = stairs_at

    # Market on terrace 1: stalls, a fountain, cafe tables, the radio.
    xw, xe, tz = TERRACES[1]
    mx, my = (xw + xe - ALLEY_W) / 2, 0.0
    box((mx, my, tz + 60), (500, 500, 120), mi("MI_Stone"), folder="Village/Market", shape="cylinder")
    for i in range(4):
        sx = mx + (-1 if i % 2 else 1) * 450
        sy = my + (-1 if i < 2 else 1) * 1100
        box((sx, sy, tz + 110), (420, 260, 220), mi(["MI_Red", "MI_Yellow", "MI_Blue", "MI_Green"][i]), folder="Village/Market")
        box((sx, sy, tz + 260), (520, 360, 10), mi("MI_White"), folder="Village/Market")
    for i in range(3):
        box((mx - 450, my - 400 + i * 400, tz + 40), (100, 100, 80), mi("MI_Wood"), folder="Village/Market", shape="cylinder")
    spawn(bp("World/Interactables/Distraction/BP_Distraction_Radio"), (mx + 450, my - 1100, tz + 220), folder="Village/Market")
    info["market"] = (mx, my, tz)

    # Kiosk with the street lamps' breaker on terrace 0 (keycard door).
    xw, xe, tz = TERRACES[0]
    kx, ky = xe - ALLEY_W - 40 - 300, VILLAGE_Y[0] - 900
    for (a, b, op) in (((kx - 300, ky - 250), (kx + 300, ky - 250), ()), ((kx - 300, ky + 250), (kx + 300, ky + 250), [(300, 110, 0, 230)]),
                       ((kx - 300, ky - 250), (kx - 300, ky + 250), ()), ((kx + 300, ky - 250), (kx + 300, ky + 250), ())):
        wall(a, b, gz(kx, ky), 300, 25, op, GRID_DARK, "Village/Kiosk")
    kz = gz(kx, ky)
    box((kx, ky, kz + 300), (650, 550, 25), GRID, folder="Village/Kiosk")
    spawn(bp("World/Interactables/Door/BP_Door_Keycard1"), (kx + 55, ky + 250, kz), (0, 180, 0), folder="Village/Kiosk")
    room_light(kx, ky, kz + 260, "Village/Kiosk", shadow=True)
    breaker = spawn(bp("World/Interactables/Power/BP_CircuitBreaker"), (kx, ky - 220, kz), folder="Village/Kiosk")
    if breaker:
        breaker.set_editor_property("linked_lights", lamps)

    # Bell tower on the top terrace (sniper nest, ladder inside).
    xw, xe, tz = TERRACES[4]
    tx, ty = (xw + xe) / 2 - 150, -6800
    for (a, b, op) in (((tx - 400, ty - 400), (tx + 400, ty - 400), ()), ((tx - 400, ty + 400), (tx + 400, ty + 400), [(400, 110, 0, 230)]),
                       ((tx - 400, ty - 400), (tx - 400, ty + 400), ()), ((tx + 400, ty - 400), (tx + 400, ty + 400), [(400, 120, 300, 420)])):
        wall(a, b, tz, 2200, 40, op, GRID_ALT, "Village/BellTower")
    box((tx, ty, tz + 2200), (840, 840, 30), GRID_ALT, folder="Village/BellTower")
    for (a, b) in (((tx - 400, ty - 400), (tx + 400, ty - 400)), ((tx - 400, ty + 400), (tx + 400, ty + 400)), ((tx - 400, ty - 400), (tx - 400, ty + 400)), ((tx + 400, ty - 400), (tx + 400, ty + 400))):
        wall(a, b, tz + 2215, 110, 30, (), GRID_ALT, "Village/BellTower")
    ladder((tx, ty - 375, tz), 90, 2215, folder="Village/BellTower")
    room_light(tx, ty, tz + 2100, "Village/BellTower")
    room_light(tx, ty, tz + 300, "Village/BellTower", lumens=300.0)
    spawn(bp("World/Interactables/Door/BP_Door"), (tx + 55, ty + 400, tz), (0, 180, 0), folder="Village/BellTower")
    info["bell_tower"] = (tx, ty, tz + 2215)
    # Props.
    for (k, yy) in ((0, -5200), (2, 1900), (3, -2600)):
        xw, xe, tz = TERRACES[k]
        spawn(bp("World/Interactables/Locker/BP_Locker"), (xe - 60, yy, tz), (0, 180, 0), folder="Village/Props")
    xw, xe, tz = TERRACES[2]
    spawn(bp("World/Interactables/Distraction/BP_Distraction_Alarm"), (xe - 60, -400, tz + 80), folder="Village/Props")
    box((xe - 60, -400, tz + 40), (80, 80, 80), mi("MI_Wood"), folder="Village/Props")
    xw, xe, tz = TERRACES[0]
    spawn(bp("World/Interactables/Container/BP_BodyContainer"), (xe - 70, 1500, tz), (0, 180, 0), folder="Village/Props")
    for gx, gy in ((-11200, -1500), (-11300, 1500), (-3000, -6800)):
        spawn(bp("World/Interactables/Grass/BP_TallGrass"), (gx, gy, gz(gx, gy) + 55), folder="Village/Gardens")


# ---------------------------------------------------------------------------------------------
# Ravine: arched bridges, watchtowers and the high walkway
# ---------------------------------------------------------------------------------------------

def arched_bridge(y, folder):
    xr = ravine_x(y)
    xa, xb = xr - 1450.0, xr + 1450.0
    za, zb = gz(xa, y), gz(xb, y)
    segments = 16
    rise = 230.0
    width = 440.0
    pts = []
    for i in range(segments + 1):
        t = i / segments
        pts.append((xa + (xb - xa) * t, za + (zb - za) * t + rise * math.sin(math.pi * t)))
    for (x0, z0), (x1, z1) in zip(pts[:-1], pts[1:]):
        length = math.hypot(x1 - x0, z1 - z0)
        pitch = math.degrees(math.atan2(z1 - z0, x1 - x0))
        for side in (-1, 1):
            rail = spawn(unreal.StaticMeshActor, ((x0 + x1) / 2, y + side * (width / 2 - 15), (z0 + z1) / 2 + 45), (pitch, 0, 0), folder=folder + "/Rails")
            rail.static_mesh_component.set_static_mesh(load(SHAPES["cube"]))
            rail.set_actor_scale3d(unreal.Vector((length + 6) / 100.0, 0.3, 0.9))
            rail.static_mesh_component.set_material(0, load(mi("MI_Stone")))
        deck = spawn(unreal.StaticMeshActor, ((x0 + x1) / 2, y, (z0 + z1) / 2 - 15), (pitch, 0, 0), folder=folder)
        deck.static_mesh_component.set_static_mesh(load(SHAPES["cube"]))
        deck.set_actor_scale3d(unreal.Vector((length + 6) / 100.0, width / 100.0, 0.3))
        deck.static_mesh_component.set_material(0, load(mi("MI_Stone")))
    # Under the deck: two stone ribs down to the bed (the arch) and abutments on both rims.
    for (x0, z0) in pts[3:-3:2]:
        bed = gz(x0, y)
        top = z0 - 30
        if top - bed > 60:
            box((x0, y, (bed + top) / 2), (120, width - 60, top - bed), mi("MI_Stone"), folder=folder + "/Piers")
    for (x0, z0) in (pts[0], pts[-1]):
        box((x0, y, z0 - 120), (300, width + 80, 240), mi("MI_Stone"), folder=folder + "/Abutments")
    return pts


def build_ravine(info):
    # Rocks in the bed (cover), a few dead trees' trunks.
    rng = random.Random(23)
    for i in range(18):
        y = -9000 + i * 700 + rng.uniform(-200, 200)
        x = ravine_x(y) + rng.uniform(-300, 300)
        size = rng.uniform(120, 260)
        box((x, y, gz(x, y) + size * 0.35), (size, size * 1.2, size * 0.9), mi("MI_Stone"), rng.uniform(0, 180), folder="Ravine/Rocks", shape="sphere")
    for y in BRIDGES_Y:
        arched_bridge(y, "Ravine/Bridge%d" % int(y))
    # Watchtowers on both rims and the high walkway between them.
    xr = ravine_x(WALKWAY_Y)
    top = 700.0
    ends = []
    for side in (-1, 1):
        x = xr + side * 1600.0
        g = gz(x, WALKWAY_Y)
        deck = g + top
        for (dx, dy) in ((-180, -180), (180, -180), (-180, 180), (180, 180)):
            box((x + dx, WALKWAY_Y + dy, (g + deck) / 2), (30, 30, deck - g), mi("MI_Wood"), folder="Ravine/Watchtowers")
        box((x, WALKWAY_Y, deck), (420, 420, 20), mi("MI_Wood"), folder="Ravine/Watchtowers")
        for (a, b) in (((x - 210, WALKWAY_Y - 210), (x + 210, WALKWAY_Y - 210)), ((x - 210, WALKWAY_Y + 210), (x + 210, WALKWAY_Y + 210))):
            wall(a, b, deck + 10, 100, 15, (), mi("MI_Wood"), "Ravine/Watchtowers")
        ladder((x + side * 225, WALKWAY_Y, g), 180 if side < 0 else 0, top + 15, folder="Ravine/Watchtowers")
        ends.append((x, deck))
    (xa, za), (xb, zb) = ends
    length = math.hypot(xb - xa - 420, zb - za)
    pitch = math.degrees(math.atan2(zb - za, xb - xa - 420))
    for (dy, dz, sy, sz) in ((0, 0, 1.4, 0.12), (-70, 90, 0.08, 0.08), (70, 90, 0.08, 0.08)):
        piece = spawn(unreal.StaticMeshActor, ((xa + xb) / 2, WALKWAY_Y + dy, (za + zb) / 2 + dz), (pitch, 0, 0), folder="Ravine/Walkway")
        piece.static_mesh_component.set_static_mesh(load(SHAPES["cube"]))
        piece.set_actor_scale3d(unreal.Vector(length / 100.0, sy, sz))
        piece.static_mesh_component.set_material(0, load(mi("MI_Wood")))
    info["walkway"] = ((xa + xb) / 2, WALKWAY_Y, (za + zb) / 2)


# ---------------------------------------------------------------------------------------------
# Estate (plateau)
# ---------------------------------------------------------------------------------------------

def build_estate(holes, info):
    ex0, ex1, ey0, ey1 = ESTATE
    z = EZ
    estate_wall = GRID_ALT
    wall((ex0, ey0), (ex0, ey1), z, 420, 50, [(GATE_Y - ey0, 900, 0, 420)], estate_wall, "Estate/Wall")
    wall((ex1, ey0), (ex1, ey1), z, 420, 50, [(6500, 110, 0, 230)], estate_wall, "Estate/Wall")
    wall((ex0, ey0), (ex1, ey0), z, 420, 50, [(4300, 110, 0, 230)], estate_wall, "Estate/Wall")
    wall((ex0, ey1), (ex1, ey1), z, 420, 50, [(6000, 600, 0, 420)], estate_wall, "Estate/Wall")
    spawn(bp("World/Interactables/Door/BP_Door"), (ex0 + 4300 - 55, ey0, z), folder="Estate/Wall")
    spawn(bp("World/Interactables/Door/BP_Door_Keycard1"), (ex1, ey0 + 6500 - 55, z), (0, 90, 0), folder="Estate/Wall")
    ladder((ex1 + 50, ey0 + 2500, z), 0, 420, gutter=True, folder="Estate/Wall")
    ladder((ex0 + 6800, ey1 + 50, z), 90, 420, gutter=True, folder="Estate/Wall")
    # Gatehouse, the gate camera, a mounted gun on its tripod.
    ghx, ghy = ex0 + 700, GATE_Y - 1100
    for (a, b, op) in (((ghx - 500, ghy - 350), (ghx + 500, ghy - 350), ()), ((ghx - 500, ghy + 350), (ghx + 500, ghy + 350), [(500, 110, 0, 230)]),
                       ((ghx - 500, ghy - 350), (ghx - 500, ghy + 350), [(350, 120, 100, 220)]), ((ghx + 500, ghy - 350), (ghx + 500, ghy + 350), ())):
        wall(a, b, z, 320, 25, op, GRID_DARK, "Estate/Gatehouse")
    box((ghx, ghy, z + 320), (1050, 750, 25), GRID, folder="Estate/Gatehouse")
    spawn(bp("World/Interactables/Door/BP_Door"), (ghx - 55, ghy + 350, z), (0, 180, 0), folder="Estate/Gatehouse")
    room_light(ghx - 200, ghy, z + 280, "Estate/Gatehouse", shadow=True)
    room_light(ghx + 250, ghy, z + 280, "Estate/Gatehouse", lumens=260.0)
    gate_cam = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (ex0 + 80, GATE_Y + 700, z + 400), (-25, 0, 0), folder="Estate/Security")
    spawn(bp("World/Vehicles/BP_MountedGun"), (ex0 + 1400, GATE_Y + 900, z), (0, 180, 0), folder="Estate/Security")
    # The house: 3 floors and a basement, a central hall with the stair, rooms on both sides.
    hx, hy = 6800.0, -3000.0
    hw, hd = 5600.0, 3200.0
    hx0, hx1, hy0, hy1 = hx - hw / 2, hx + hw / 2, hy - hd / 2, hy + hd / 2
    FH = 420
    house = "Estate/House"
    for f in range(3):
        fz = z + f * FH
        front = [(hw / 2, 240, 0, 280)] if f == 0 else []
        front += [(hw * t, 120, 100, 220) for t in (0.12, 0.3, 0.7, 0.88)]
        wall((hx0, hy0), (hx1, hy0), fz, FH, 35, front, GRID_DARK, house)
        wall((hx0, hy1), (hx1, hy1), fz, FH, 35, [(hw * t, 120, 100, 220) for t in (0.12, 0.3, 0.5, 0.7, 0.88)], GRID_DARK, house)
        west = [(hd * 0.5, 120, 100, 220)] + ([(hd * 0.2, 110, 0, 230), (hd * 0.5 + 1100, 120, 0, 120)] if f == 0 else [])
        wall((hx0, hy0), (hx0, hy1), fz, FH, 35, west, GRID_DARK, house)
        wall((hx1, hy0), (hx1, hy1), fz, FH, 35, [(hd * 0.5, 120, 100, 220)], GRID_DARK, house)
        for sx in (hx - 900, hx + 900):
            wall((sx, hy0), (sx, hy1), fz, FH, 25, [(hd * 0.3, 110, 0, 230), (hd * 0.75, 110, 0, 230)], GRID, house)
            spawn(bp("World/Interactables/Door/BP_Door"), (sx, hy0 + hd * 0.3 - 55, fz), (0, 90, 0), folder=house)
            spawn(bp("World/Interactables/Door/BP_Door"), (sx, hy0 + hd * 0.75 - 55, fz), (0, 90, 0), folder=house)
        for (wx0, wx1) in ((hx0, hx - 900), (hx + 900, hx1)):
            wall((wx0, hy), (wx1, hy), fz, FH, 25, [((wx1 - wx0) * 0.5, 110, 0, 230)], GRID, house)
            spawn(bp("World/Interactables/Door/BP_Door"), ((wx0 + wx1) / 2 - 55, hy, fz), folder=house)
        # Two lamps per room (the first casts shadows) and two in the hall.
        # The wing rooms are large (about 19 x 16 m): brighter lamps than a village house.
        for (lx, ly) in ((hx0 + 950, hy - 800), (hx0 + 950, hy + 800), (hx1 - 950, hy - 800), (hx1 - 950, hy + 800)):
            room_light(lx - 300, ly, fz + FH - 50, house, shadow=True, lumens=950.0, radius=1800.0)
            room_light(lx + 350, ly + 150, fz + FH - 50, house, lumens=650.0, radius=1600.0)
        room_light(hx, hy - 600, fz + FH - 50, house, shadow=True, lumens=800.0, radius=1600.0)
        room_light(hx, hy + 700, fz + FH - 50, house, lumens=550.0, radius=1500.0)
        if f > 0:
            box(((hx0 + hx - 900) / 2, hy, fz), (hx - 900 - hx0, hd, 25), GRID, folder=house)
            box(((hx + 900 + hx1) / 2, hy, fz), (hx1 - hx - 900, hd, 25), GRID, folder=house)
            hole_x0, hole_x1 = (hx + 450, hx + 750) if f == 1 else (hx + 100, hx + 400)
            hole_y1 = hy0 + 1650
            box(((hx - 900 + hole_x0) / 2, hy, fz), (hole_x0 - (hx - 900), hd, 25), GRID, folder=house)
            box(((hole_x1 + hx + 900) / 2, hy, fz), (hx + 900 - hole_x1, hd, 25), GRID, folder=house)
            box(((hole_x0 + hole_x1) / 2, (hole_y1 + hy1) / 2, fz), (hole_x1 - hole_x0, hy1 - hole_y1, 25), GRID, folder=house)
    box((hx, hy, z + 3 * FH), (hw + 100, hd + 100, 30), GRID_ALT, folder=house)
    stairs((hx + 600, hy0 + 150, z), 90, FH, 1500, 300, folder=house)
    stairs((hx + 250, hy0 + 1650, z + FH), -90, FH, 1500, 300, folder=house)
    spawn(bp("World/Interactables/Door/BP_Door"), (hx - 55, hy0, z), folder=house)
    spawn(bp("World/Interactables/Door/BP_Door"), (hx0, hy0 + hd * 0.2 - 55, z), (0, 90, 0), folder=house)
    win = bp("World/Interactables/Window/BP_Window")
    for f in range(3):
        fz = z + f * FH
        for t in (0.12, 0.3, 0.7, 0.88):
            spawn(win, (hx0 + hw * t, hy0, fz), (0, 90, 0), folder=house)
        for t in (0.12, 0.3, 0.5, 0.7, 0.88):
            spawn(win, (hx0 + hw * t, hy1, fz), (0, -90, 0), folder=house)
        spawn(win, (hx0, hy0 + hd * 0.5, fz), (0, 180, 0), folder=house)
        spawn(win, (hx1, hy0 + hd * 0.5, fz), (0, 0, 0), folder=house)
        # The ledge band under the windows, all around (from the first floor: on the ground it would bar the doors).
        if f == 0:
            continue
        lz = fz + 80 - 6
        box((hx, hy0 - 26, lz), (hw + 90, 18, 12), GRID_ALT, folder=house + "/Ledges")
        box((hx, hy1 + 26, lz), (hw + 90, 18, 12), GRID_ALT, folder=house + "/Ledges")
        box((hx0 - 26, hy, lz), (18, hd + 35, 12), GRID_ALT, folder=house + "/Ledges")
        box((hx1 + 26, hy, lz), (18, hd + 35, 12), GRID_ALT, folder=house + "/Ledges")
    ladder((hx1 + 25, hy + 1200, z), 0, 3 * FH + 30, gutter=True, folder=house)
    ladder((hx0 - 25, hy - 1200, z), 180, 3 * FH + 30, gutter=True, folder=house)
    roof_z = z + 3 * FH
    box((hx + 1600, hy, roof_z + 20), (1400, 1400, 10), mi("MI_Yellow"), folder="Estate/Roof")
    turret = spawn(bp("World/Interactables/Security/BP_Turret"), (hx - 1500, hy0 + 400, roof_z + 15), (0, -90, 0), folder="Estate/Roof")
    # Dining room (sealed: door, window and a vent hatch).
    dining_door = None
    for a in L.EAS.get_all_level_actors():
        loc = a.get_actor_location()
        if a.get_class().get_name().startswith("BP_Door") and abs(loc.x - ((hx0 + hx - 900) / 2 - 55)) < 2 and abs(loc.y - hy) < 2 and abs(loc.z - z) < 5:
            dining_door = a
    dining = spawn(bp("World/Interactables/Vent/BP_RoomVolume"), (hx0 + 950, hy + 800, z + 210), folder="Estate/Dining")
    hatch = spawn(bp("World/Interactables/Vent/BP_VentHatch"), (hx0, hy + 1100, z), (0, 90, 0), folder="Estate/Dining")
    if dining:
        dining.set_actor_scale3d(unreal.Vector(1.7, 1.5, 1.3))
        dining.set_editor_property("openings", [o for o in (dining_door, hatch) if o])
    if hatch and dining:
        hatch.set_editor_property("linked_rooms", [dining])
    box((hx0 + 950, hy + 800, z + 40), (900, 300, 80), mi("MI_Wood"), folder="Estate/Dining")
    spawn(bp("World/Interactables/Drink/BP_PoisonableDrink"), (hx0 + 950, hy + 800, z + 80), folder="Estate/Dining")
    spawn(bp("World/Interactables/Distraction/BP_Distraction_TV"), (hx1 - 950, hy - 1300, z + 80), (0, 90, 0), folder="Estate/Lounge")
    box((hx1 - 950, hy - 1300, z + 40), (120, 60, 80), mi("MI_Wood"), folder="Estate/Lounge")
    # Control room upstairs behind the keycard door (console, monitors).
    spawn(bp("World/Interactables/Door/BP_Door_Keycard1"), (hx + 2000 - 55, hy, z + FH), folder="Estate/Control")
    console = spawn(bp("World/Interactables/Security/BP_ControlConsole"), (hx1 - 600, hy + 1300, z + FH), (0, 180, 0), folder="Estate/Control")
    hall_cam = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (hx - 800, hy0 + 150, z + FH - 40), (-30, 60, 0), folder="Estate/Security")
    garden_cam = spawn(bp("World/Interactables/Security/BP_SecurityCamera"), (hx1 - 100, hy1 + 100, z + 2 * FH + 300), (-30, 45, 0), folder="Estate/Security")
    cams = [c for c in (gate_cam, hall_cam, garden_cam) if c]
    if console:
        console.set_editor_property("linked_cameras", cams)
    for i, cam in enumerate(cams):
        monitor = spawn(bp("World/Interactables/Security/BP_SecurityMonitor"), (hx1 - 1300 + i * 400, hy1 - 70, z + FH + 200), (0, 180, 0), folder="Estate/Control")
        if monitor:
            monitor.set_editor_property("linked_camera", cam)
    gatehouse_console = spawn(bp("World/Interactables/Security/BP_ControlConsole"), (ghx + 200, ghy - 200, z), (0, 90, 0), folder="Estate/Gatehouse")
    if gatehouse_console:
        gatehouse_console.set_editor_property("linked_cameras", cams)
    # Basement: breaker (house lights), the turret hack panel, the vent, the tunnel exit.
    BZ = z - 500
    floor((hx, hy, BZ), (hw, hd, 0), GRID_DARK, "Estate/Basement")
    box((hx, hy, z - 25), (hw, hd, 50), GRID, folder="Estate/Basement")
    for (a, b) in (((hx0, hy0), (hx1, hy0)), ((hx0, hy1), (hx1, hy1)), ((hx0, hy0), (hx0, hy1)), ((hx1, hy0), (hx1, hy1))):
        wall(a, b, BZ, z - 50 - BZ, 40, [(hd * 0.5, 400, 0, 320)] if a == (hx1, hy0) else (), GRID_DARK, "Estate/Basement")
    holes.append((hx0, hy0, hx1, hy1))
    stair_well((hx1, hy - 250, hx1 + 1100, hy + 250), BZ, "+x", 800, holes, {"left": [(300, 400, 0, 340)]}, low_wall=False,
               folder="Estate/BasementStairs", top_z=z)
    for (lx, ly) in ((hx - 1500, hy), (hx, hy), (hx + 1500, hy)):
        room_light(lx, ly, z - 70, "Estate/Basement", shadow=(lx == hx), lumens=800.0, radius=1700.0)
    breaker = spawn(bp("World/Interactables/Power/BP_CircuitBreaker"), (hx0 + 400, hy0 + 70, BZ), folder="Estate/Basement")
    blackout = spawn(bp("World/Interactables/Power/BP_BlackoutVolume"), (hx, hy, z + FH * 1.5), folder="Estate/House")
    if blackout:
        blackout.set_actor_scale3d(unreal.Vector(3.6, 2.1, 2.2))
    house_lights = [a for a in L.EAS.get_all_level_actors() if a.get_class().get_name().startswith("BP_LightFixture")
                    and hx0 <= a.get_actor_location().x <= hx1 and hy0 <= a.get_actor_location().y <= hy1 and a.get_actor_location().z > z]
    if breaker:
        breaker.set_editor_property("linked_lights", house_lights)
        breaker.set_editor_property("linked_volumes", [blackout] if blackout else [])
    panel = spawn(bp("World/Interactables/Security/BP_HackPanel"), (hx1 - 500, hy1 - 70, BZ), (0, 180, 0), folder="Estate/Basement")
    if turret and panel:
        panel.set_editor_property("linked_turret", turret)
    # Front garden: hedges and a fountain.
    for gx in (-2200, -1100, 1100, 2200):
        box((hx + gx, hy0 - 1600, z + 70), (800, 120, 140), mi("MI_Green"), folder="Estate/FrontGarden")
    box((hx, hy0 - 2500, z + 60), (900, 900, 120), mi("MI_Stone"), folder="Estate/FrontGarden", shape="cylinder")
    # A small hedge maze with a gazebo, north of the house.
    maze_x, maze_y = 8400.0, 800.0
    hedge_maze_at(maze_x, maze_y, z, 2600, 7, "Estate/Maze")
    box((maze_x, maze_y, z + 290), (600, 600, 20), mi("MI_White"), folder="Estate/Maze", shape="cylinder")
    for ang in range(0, 360, 60):
        box((maze_x + math.cos(math.radians(ang)) * 250, maze_y + math.sin(math.radians(ang)) * 250, z + 140), (30, 30, 280), mi("MI_White"), folder="Estate/Maze", shape="cylinder")
    room_light(maze_x, maze_y, z + 270, "Estate/Maze")
    # Greenhouse.
    gx0, gy0 = 4000.0, 900.0
    for (a, b, op) in (((gx0 - 1000, gy0 - 600), (gx0 + 1000, gy0 - 600), [(1000, 110, 0, 230)]), ((gx0 - 1000, gy0 + 600), (gx0 + 1000, gy0 + 600), ()),
                       ((gx0 - 1000, gy0 - 600), (gx0 - 1000, gy0 + 600), ()), ((gx0 + 1000, gy0 - 600), (gx0 + 1000, gy0 + 600), [(600, 110, 0, 230)])):
        wall(a, b, z, 350, 15, op, mi("MI_Glass"), "Estate/Greenhouse")
    box((gx0, gy0, z + 350), (2030, 1230, 15), mi("MI_Glass"), folder="Estate/Greenhouse")
    for i in range(6):
        box((gx0 - 700 + i * 280, gy0 + (200 if i % 2 else -200), z + 60), (160, 160, 120), mi("MI_Green"), folder="Estate/Greenhouse", shape="sphere")
    room_light(gx0 - 400, gy0, z + 300, "Estate/Greenhouse")
    room_light(gx0 + 400, gy0, z + 300, "Estate/Greenhouse", lumens=260.0)
    spawn(bp("World/Interactables/Grass/BP_TallGrass"), (gx0 - 400, gy0 - 1200, z + 55), folder="Estate/Greenhouse")
    # Garage (the tank) in the south-west corner, its wide door to the north.
    grx, gry = 3300.0, -7200.0
    for (a, b, op) in (((grx - 1300, gry - 900), (grx + 1300, gry - 900), ()), ((grx - 1300, gry + 900), (grx + 1300, gry + 900), [(1300, 800, 0, 450)]),
                       ((grx - 1300, gry - 900), (grx - 1300, gry + 900), [(900, 110, 0, 230)]), ((grx + 1300, gry - 900), (grx + 1300, gry + 900), ())):
        wall(a, b, z, 550, 30, op, GRID_DARK, "Estate/Garage")
    box((grx, gry, z + 550), (2650, 1850, 30), GRID, folder="Estate/Garage")
    spawn(bp("World/Vehicles/BP_Tank"), (grx, gry, z + 100), (0, 90, 0), folder="Estate/Garage")
    spawn(bp("World/Interactables/Door/BP_Door"), (grx - 1300, gry - 55, z), (0, 90, 0), folder="Estate/Garage")
    room_light(grx - 700, gry, z + 500, "Estate/Garage", shadow=True, lumens=900.0, radius=1600.0)
    room_light(grx + 700, gry, z + 500, "Estate/Garage", lumens=600.0, radius=1500.0)
    spawn(bp("World/Interactables/Container/BP_BodyContainer"), (grx + 1500, gry - 600, z), (0, 0, 0), folder="Estate/Props")
    spawn(bp("World/Interactables/Locker/BP_Locker"), (ghx + 400, ghy - 250, z), (0, 90, 0), folder="Estate/Props")
    for bx, by in ((ex0 + 2500, GATE_Y - 500), (10200, 1500), (10300, -6800)):
        spawn(bp("World/Interactables/Barrel/BP_ExplosiveBarrel"), (bx, by, z), folder="Estate/Props")
    info.update(house=(hx, hy, hx0, hx1, hy0, hy1, FH, BZ), maze=(maze_x, maze_y), greenhouse=(gx0, gy0), garage=(grx, gry),
                gatehouse=(ghx, ghy), roof_z=roof_z)


def hedge_maze_at(cx, cy, z, size, cells, folder):
    """hedge_maze() raised onto the plateau: build at the origin height, then lift the new boxes."""
    before = set(a.get_name() for a in L.EAS.get_all_level_actors())
    hedge_maze(cx, cy, size, cells, folder)
    for a in L.EAS.get_all_level_actors():
        if a.get_name() not in before:
            loc = a.get_actor_location()
            a.set_actor_location(unreal.Vector(loc.x, loc.y, loc.z + z), False, False)


# ---------------------------------------------------------------------------------------------
# Ridge and cave
# ---------------------------------------------------------------------------------------------

def subtract_rect(rect, hole):
    """rect minus hole as up to four rectangles (x0, x1, y0, y1)."""
    x0, x1, y0, y1 = rect
    hx0, hx1, hy0, hy1 = max(hole[0], x0), min(hole[1], x1), max(hole[2], y0), min(hole[3], y1)
    if hx0 >= hx1 or hy0 >= hy1:
        return [rect]
    parts = [(x0, x1, y0, hy0), (x0, x1, hy1, y1), (x0, hx0, hy0, hy1), (hx1, x1, hy0, hy1)]
    return [p for p in parts if p[1] - p[0] > 1 and p[3] - p[2] > 1]


def build_cave(holes, info):
    rock = mi("MI_Mountain")
    hx, hy, hx0, hx1, hy0, hy1, FH, BZ = info["house"]
    # Stair well in the last chamber, its low end at the chamber's south wall, then the tunnel to the mansion
    # basement stair (under the ridge and the plateau).
    cx3, cy3, rx3, ry3 = CAVE_CHAMBERS[2]
    mouth_x = cx3 - 300
    well = (mouth_x - 250, cy3 - ry3 * 0.88, mouth_x + 250, cy3 - ry3 * 0.88 + 1000)
    stair_well(well, BZ, "+y", 1000, holes, {"low": [(250, 400, 0, 340)]}, folder="Cave/StairWell", top_z=CZ)
    tunnel([(mouth_x, well[1]), (mouth_x, 3500), (hx1 + 300, 3500), (hx1 + 300, hy + 250)], BZ)
    # Rock floor and ceiling over each room's bounding rectangle (the parts beyond the lumpy walls are inside the rock).
    well_rect = (well[0], well[2], well[1], well[3])
    for i, (x0, x1, y0, y1) in enumerate(cave_rects()):
        passage = i >= len(CAVE_CHAMBERS)
        if i == len(CAVE_CHAMBERS):
            y0 = max(y0, COVER_Y)   # the entrance trench is open to the sky
        h = PASSAGE_HEIGHT if passage else CAVE_HEIGHT
        box(((x0 + x1) / 2, (y0 + y1) / 2, CZ + h + 25), (x1 - x0, y1 - y0, 50), rock, folder="Cave/Ceiling")
    for i, rect in enumerate(cave_rects()):
        if i == len(CAVE_CHAMBERS):
            rect = (rect[0], rect[1], max(rect[2], ENTRANCE_RAMP[1]), rect[3])   # the trench ramp is ground
        for (x0, x1, y0, y1) in subtract_rect(rect, well_rect):
            box(((x0 + x1) / 2, (y0 + y1) / 2, CZ + 1), (x1 - x0, y1 - y0, 2), rock, folder="Cave/Floor")
    # The roof: a heightfield continuing the ridge over the cave, only on the cells above it (every other cell is a
    # hole), so its edge vertices are the ground's own and the ridge reads as one surface.
    rx0, rx1, ry0, ry1 = CAVE_ROOF_RECT
    nx, ny = int((rx1 - rx0) / CELL) + 1, int((ry1 - ry0) / CELL) + 1
    roof = []
    cave_corner = []
    for iy in range(ny):
        y = ry0 + iy * CELL
        for ix in range(nx):
            x = rx0 + ix * CELL
            roof.append(float(height(x, y, cave=False) + 4.0))   # 4 cm above the ground: the shared triangles never z-fight
            cave_corner.append(in_cave(x, y))
    # One cell wider than the cave: the ground's vertices next to the cut have steep normals (their neighbour is the
    # cave floor) and would shade as dark triangles; the roof hides them.
    grown = list(cave_corner)
    for iy in range(ny):
        for ix in range(nx):
            if cave_corner[iy * nx + ix]:
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if 0 <= iy + dy < ny and 0 <= ix + dx < nx:
                            grown[(iy + dy) * nx + ix + dx] = True
    cave_corner = grown
    roof_holes = []
    for iy in range(ny - 1):
        run = None
        for ix in range(nx):
            covered = ix < nx - 1 and ry0 + iy * CELL >= COVER_Y and any(cave_corner[(iy + dy) * nx + ix + dx] for dx in (0, 1) for dy in (0, 1))
            if not covered and ix < nx - 1:
                run = ix if run is None else run
            elif run is not None:
                roof_holes.append(unreal.Box2D(unreal.Vector2D(rx0 + run * CELL, ry0 + iy * CELL), unreal.Vector2D(rx0 + ix * CELL, ry0 + (iy + 1) * CELL)))
                run = None
    mesh = unreal.HitmenContentTools.build_heightfield_mesh(TERRAIN_FOLDER + "/SM_CaveRoof", roof, nx, ny, CELL, unreal.Vector2D(rx0, ry0), roof_holes,
                                                             load(path("Materials", "Master", "M_Hitmen_Terrain")), False)
    actor = spawn(unreal.StaticMeshActor, (rx0, ry0, 0), label="CaveRoof", folder="Cave")
    actor.static_mesh_component.set_static_mesh(mesh)
    # The mouth: a rock lintel where the roof starts (its underside would show the void), boulders on both sides.
    a, b, hw = CAVE_LINKS[0]
    lintel_top = height(a[0], COVER_Y + 100, cave=False) + 60
    box((a[0], COVER_Y + 40, (CZ + PASSAGE_HEIGHT + lintel_top) / 2), (hw * 2 + 400, 180, lintel_top - CZ - PASSAGE_HEIGHT), rock, folder="Cave/Mouth")
    for (dx, size) in ((-hw - 550, 900), (hw + 600, 1000), (-hw - 350, 500), (hw + 400, 600)):
        box((a[0] + dx, COVER_Y - 300, gz(a[0] + dx, COVER_Y - 300) + size * 0.25), (size, size * 0.8, size * 0.9), rock, 20, folder="Cave/Mouth", shape="sphere")
    # Pillars and boulders, two lamps per chamber (one with shadows), one per passage.
    rng = random.Random(41)
    for (cx, cy, rx, ry) in CAVE_CHAMBERS:
        for k in range(3):
            ang = rng.uniform(0, 2 * math.pi)
            px, py = cx + math.cos(ang) * rx * 0.6, cy + math.sin(ang) * ry * 0.6
            size = rng.uniform(180, 320)
            box((px, py, CZ + size * 0.4), (size, size, size * 0.8), mi("MI_Stone"), rng.uniform(0, 90), folder="Cave/Boulders", shape="sphere")
        box((cx + rx * 0.35, cy - ry * 0.3, CZ + CAVE_HEIGHT / 2), (170, 170, CAVE_HEIGHT), rock, 30, folder="Cave/Pillars", shape="cylinder")
        room_light(cx - rx * 0.35, cy, CZ + CAVE_HEIGHT - 60, "Cave/Lamps", shadow=True, lumens=950.0, radius=1900.0)
        room_light(cx + rx * 0.3, cy + ry * 0.3, CZ + CAVE_HEIGHT - 60, "Cave/Lamps", lumens=650.0, radius=1700.0)
    for (a, b, hw) in CAVE_LINKS:
        t0 = 0.75 if a == CAVE_LINKS[0][0] else 0.5
        room_light(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0, CZ + PASSAGE_HEIGHT - 40, "Cave/Lamps", lumens=300.0, radius=900.0)
    # The den in the middle chamber: a table, the mounted gun.
    cx, cy, rx, ry = CAVE_CHAMBERS[1]
    box((cx, cy - 300, CZ + 40), (500, 300, 80), mi("MI_Wood"), folder="Cave/Den")
    spawn(bp("World/Vehicles/BP_MountedGun"), (cx + 600, cy - 500, CZ), (0, -90, 0), folder="Cave/Den")
    info["cave"] = (cx, cy)


# ---------------------------------------------------------------------------------------------
# Gameplay
# ---------------------------------------------------------------------------------------------

def ground_point(x, y, dz=0.0):
    return (x, y, gz(x, y) + dz)


def build_gameplay(info):
    hx, hy, hx0, hx1, hy0, hy1, FH, BZ = info["house"]
    maze_x, maze_y = info["maze"]
    gx0, gy0 = info["greenhouse"]
    grx, gry = info["garage"]
    ghx, ghy = info["gatehouse"]
    mx, my, mz = info["market"]
    ccx, ccy = info["cave"]
    ex0, ex1, ey0, ey1 = ESTATE
    z = EZ
    houses = info["houses"]
    # VIP candidates: house rooms, estate buildings, village houses, the market, the cave.
    vip_spots = [(hx0 + 950, hy + 400, z), (hx1 - 950, hy - 800, z + FH), (hx, hy + 700, z), (hx0 + 950, hy - 800, z + 2 * FH),
                 (hx1 - 950, hy + 800, z + 2 * FH), (maze_x, maze_y, z), (gx0, gy0, z), (hx1 - 600, hy1 - 600, BZ), (grx, gry + 400, z),
                 (mx - 450, my + 400, mz), (ccx, ccy, CZ)]
    for (hcx, hcy, w, d, floors, tz) in houses[2::7]:
        vip_spots.append((hcx - 150, hcy, tz + (350 if floors > 1 else 0)))
    for loc in vip_spots:
        npc("vip", loc, 0, folder="AI/VIPCandidates")
    routes = []
    # Estate: inside the walls, around the house, the front garden, the maze, each floor and the basement.
    routes.append([(ex0 + 1200, ey0 + 1200, z + 100), (ex1 - 1200, ey0 + 1200, z + 100), (ex1 - 1200, ey1 - 1200, z + 100), (ex0 + 1200, ey1 - 1200, z + 100)])
    routes.append([(hx0 - 1000, hy0 - 900, z + 100), (hx1 + 1500, hy0 - 900, z + 100), (hx1 + 1500, hy1 + 900, z + 100), (hx0 - 1000, hy1 + 900, z + 100)])
    routes.append([(hx - 2400, hy0 - 2000, z + 100), (hx + 2400, hy0 - 2000, z + 100)])
    routes.append([(maze_x - 1600, maze_y - 1600, z + 100), (maze_x + 1600, maze_y - 1600, z + 100), (maze_x + 1600, maze_y + 1600, z + 100), (maze_x - 1600, maze_y + 1600, z + 100)])
    routes.append([(hx0 + 600, hy0 + 400, z + 100), (hx - 300, hy0 + 400, z + 100), (hx - 300, hy1 - 400, z + 100), (hx1 - 600, hy1 - 400, z + 100)])
    routes.append([(hx0 + 600, hy0 + 400, z + FH + 100), (hx1 - 600, hy0 + 400, z + FH + 100), (hx1 - 600, hy1 - 400, z + FH + 100)])
    routes.append([(hx1 - 600, hy0 + 400, z + 2 * FH + 100), (hx0 + 600, hy1 - 400, z + 2 * FH + 100)])
    routes.append([(hx0 + 500, hy0 + 500, BZ + 100), (hx1 - 500, hy1 - 500, BZ + 100)])
    # Village: every terrace alley (out and back), a loop over two terraces by their stairs, the market.
    for k, (xw, xe, tz) in enumerate(TERRACES):
        ax = xe - ALLEY_W / 2
        routes.append([(ax, VILLAGE_Y[0] + 300, tz + 100), (ax, VILLAGE_Y[1] - 300, tz + 100)])
    st = [cy for (k, cy) in info["stairs"] if k == 0]
    if len(st) >= 2:
        xw0, xe0, t0 = TERRACES[0]
        xw1, xe1, t1 = TERRACES[1]
        routes.append([(xe0 - ALLEY_W / 2, st[0], t0 + 100), (xw0 - 300, st[0], t1 + 100), (xe1 - ALLEY_W / 2, st[0], t1 + 100),
                       (xe1 - ALLEY_W / 2, st[1], t1 + 100), (xw0 - 300, st[1], t1 + 100), (xe0 - ALLEY_W / 2, st[1], t0 + 100)])
    market_route = len(routes)
    routes.append([(mx - 400, my - 1400, mz + 100), (mx + 650, my - 1400, mz + 100), (mx + 650, my + 1400, mz + 100), (mx - 400, my + 1400, mz + 100)])
    # Ravine: the bed, the bridges; the plain north; the cave.
    routes.append([ground_point(ravine_x(-7000), -7000, 100), ground_point(ravine_x(1500), 1500, 100)])
    routes.append([ground_point(ravine_x(BRIDGES_Y[0]) - 2400, BRIDGES_Y[0], 100), ground_point(ravine_x(BRIDGES_Y[0]) + 2200, BRIDGES_Y[0], 100),
                   ground_point(ravine_x(BRIDGES_Y[1]) + 2200, BRIDGES_Y[1], 100), ground_point(ravine_x(BRIDGES_Y[1]) - 2400, BRIDGES_Y[1], 100)])
    routes.append([ground_point(1300, 3200, 100), ground_point(1600, 3800, 100), (300, 3800, CZ + 100), (300, 5200, CZ + 100)])
    c = [(cx, cy) for (cx, cy, rx, ry) in CAVE_CHAMBERS]
    routes.append([(c[0][0], c[0][1], CZ + 100), (c[1][0], c[1][1], CZ + 100), (c[2][0] + 300, c[2][1] + 300, CZ + 100)])
    guards = []
    for i, points in enumerate(routes):
        route = patrol(points, folder="AI/Routes")
        start = points[0]
        guards.append(npc("guard", (start[0], start[1], start[2] - 100), 0, route=route, keycard=1 if i in (5, 9) else None, folder="AI/Guards"))
    # Groups: two followers behind the front garden patrol, one behind the market patrol.
    group_specs = [(2, 2), (market_route, 1)]
    for leader_index, followers in group_specs:
        leader = guards[leader_index]
        if not leader:
            continue
        members = [leader]
        loc = leader.get_actor_location()
        for f in range(followers):
            members.append(npc("guard", (loc.x + 180 * (f + 1), loc.y + 120, loc.z - 100), 0, folder="AI/Groups"))
        group = spawn(bp("AI/BP_GuardGroup"), (loc.x, loc.y, loc.z), label="GuardGroup", folder="AI/Groups")
        if group:
            group.set_editor_property("members", [m for m in members if m])
    # Posts: the gate (sentries), the house front and back, the east door, the garage, the market, a bridge, the cave.
    posts = [((ex0 + 300, GATE_Y - 700, z), 180, True), ((ex0 + 300, GATE_Y + 700, z), 180, True), ((hx - 300, hy0 - 300, z), 270, False),
             ((hx1 - 300, hy1 + 300, z), 0, False), ((ex1 - 300, ey0 + 6500, z), 0, False), ((grx + 1500, gry + 1300, z), 180, False),
             ((mx + 600, my, mz), 0, False), (ground_point(ravine_x(BRIDGES_Y[1]) + 1800, BRIDGES_Y[1] + 400), 180, False),
             ((CAVE_LINKS[0][0][0] + 150, COVER_Y - 600, CZ), 270, False)]
    for (loc, yaw, sentry) in posts:
        npc("guard", loc, yaw, folder="AI/Guards", stationary=True, sentry=sentry)
    tx, ty, tz = info["bell_tower"]
    npc("sniper", (tx, ty, tz), 0, folder="AI/Snipers")
    npc("sniper", (hx + 2300, hy0 + 300, info["roof_z"] + 30), 270, folder="AI/Snipers")
    # A rock perch on the ridge over the estate (the slope there is too steep to stand on).
    rx, ry = 7800.0, 5400.0
    perch = max(gz(rx + dx, ry + dy) for dx in (-250, 0, 250) for dy in (-250, 0, 250)) + 20
    box((rx, ry, perch - 300), (520, 520, 600), mi("MI_Mountain"), folder="AI/Snipers")
    npc("sniper", (rx, ry, perch), 270, folder="AI/Snipers")
    # Retrieval points: the helipad, the bridge foot, the far favela, the cave, east gate, the north plain.
    for loc in ((hx + 1600, hy, info["roof_z"] + 25), ground_point(ravine_x(-8500), -8500), (TERRACES[4][1] - 200, 2400, TERRACES[4][2]),
                (c[2][0] + 500, c[2][1] + 400, CZ), ground_point(11900, -2500), ground_point(1600, 3300)):
        spawn(bp("World/Spawning/BP_RetrievalPoint"), loc, folder="Gameplay/Retrieval")
    # Loot: weapons and items spread over the three zones.
    loot_w = [(ghx, ghy, z + 30), (hx0 + 950, hy - 800, z + 30), (hx1 - 950, hy + 800, z + FH + 30), (hx, hy, BZ + 30), (grx + 800, gry, z + 30),
              (gx0, gy0, z + 30), (ccx, ccy, CZ + 30), (c[0][0], c[0][1], CZ + 30), (mx, my - 650, mz + 30),
              ground_point(ravine_x(-5000), -5000, 30), ground_point(1500, 3600, 30)]
    loot_i = [(hx0 + 950, hy + 800, z + FH + 30), (hx1 - 950, hy - 800, z + 2 * FH + 30), (hx0 + 500, hy1 - 500, BZ + 30), (maze_x, maze_y, z + 30),
              (ex1 - 1500, ey1 - 1500, z + 30), (c[2][0] + 300, c[2][1] + 200, CZ + 30), (mx + 100, my + 650, mz + 30),
              ground_point(ravine_x(500), 500, 30), ground_point(info["walkway"][0] - 1600, WALKWAY_Y + 400, 30)]
    for i, (hcx, hcy, w, d, floors, tz) in enumerate(houses[1::4]):
        (loot_w if i % 2 == 0 else loot_i).append((hcx - 200, hcy, tz + 30 + (350 if floors > 1 and i % 3 == 0 else 0)))
    for loc in loot_w:
        spawn(bp("World/Spawning/BP_LootSpawn_Weapon"), loc, folder="Gameplay/Loot")
    for loc in loot_i:
        spawn(bp("World/Spawning/BP_LootSpawn_Item"), loc, folder="Gameplay/Loot")
    # Starts: six groups of four near the edge, each behind a low shelter.
    starts = [(-9600, -8300, 45), (-4500, -9500, 90), (6000, -9300, 90), (11900, -1800, 180), (10500, 5000, -135), (-6950, 3700, -45)]
    for (sx, sy, yaw) in starts:
        sz = gz(sx, sy)
        rad = math.radians(yaw)
        box((sx + math.cos(rad) * 450, sy + math.sin(rad) * 450, sz + 150), (40, 900, 300), GRID_ALT, yaw, folder="Gameplay/Starts")
    team_starts([(sx, sy, yaw) for (sx, sy, yaw) in starts])
    # team_starts() places at z 120: lift every start onto the ground.
    for a in L.EAS.get_all_level_actors():
        if a.get_class().get_name().startswith("BP_TeamStart"):
            loc = a.get_actor_location()
            a.set_actor_location(unreal.Vector(loc.x, loc.y, gz(loc.x, loc.y) + 120), False, False)


# ---------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------

def build():
    new_map("L_LDTest")
    game_mode("BP_GM_Match")
    holes = []
    info = {}
    environment(sun_pitch=-38, sun_yaw=55)
    for a in L.EAS.get_all_level_actors():
        if isinstance(a, unreal.ExponentialHeightFog):
            fog = a.component
            fog.set_editor_property("enable_volumetric_fog", True)
            fog.set_editor_property("volumetric_fog_scattering_distribution", 0.35)
    nav_bounds((0, 0, 1000), (BOUNDS + 200, BOUNDS + 200, 4000))
    build_outline_walls()
    build_village(info)
    build_ravine(info)
    build_estate(holes, info)
    build_cave(holes, info)
    build_gameplay(info)
    build_terrain(holes)
    # Map definition: square bounds around the outline (minimap, pause map, spectator limits).
    data = load(path("Data", "Maps", "DA_Map_LDTest"))
    if data:
        data.set_editor_property("world_bounds", unreal.Box2D(unreal.Vector2D(-BOUNDS, -BOUNDS), unreal.Vector2D(BOUNDS, BOUNDS)))
        save(data)
    save_map()
    log("San Lazaro built")
