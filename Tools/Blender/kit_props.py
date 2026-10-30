# Author : Baptiste Vu Liem
# Shared prop kit of Goobyr FPS (style "Props"): furniture, lights, street dressing, industrial and
# utility gear, waterfront, military and chunky toy vehicles. Origin at the bottom centre of the
# footprint, front on +Y (wall-mounted pieces: back on the origin plane; Chandelier/Sprinkler: origin
# at the ceiling point). Lamps export their bulbs as <Piece>_Glow.fbx. Exports to Kit/Props/.
import importlib
import math
import sys

KIT_DIR = globals().get("KIT_DIR", r"D:\_Project\_GamesUnity\_Isart\Goobyr FPS\Tools\Blender")
if KIT_DIR not in sys.path:
    sys.path.insert(0, KIT_DIR)
import kit_common as kc  # noqa: E402

importlib.reload(kc)
from kit_common import Kit, Piece, fixed, tinted, shade, T, R, S, GLASS, GLOW, rng_for  # noqa: E402

# ---------------------------------------------------------------- palette
WOOD = fixed(0.58, 0.4, 0.24)
WOOD_D = fixed(0.36, 0.23, 0.13)
WOOD_L = fixed(0.76, 0.6, 0.4)
METAL = fixed(0.6, 0.62, 0.65)
METAL_D = fixed(0.24, 0.25, 0.27)
IRON = fixed(0.12, 0.12, 0.13)
BRASS = fixed(0.85, 0.66, 0.3)
CHROME = fixed(0.82, 0.84, 0.87)
WHITE = fixed(0.95, 0.95, 0.93)
CERAMIC = fixed(0.97, 0.97, 0.96)
BLACK = fixed(0.06, 0.06, 0.07)
RUBBER = fixed(0.1, 0.1, 0.11)
CONCRETE = fixed(0.68, 0.67, 0.64)
STONE = fixed(0.8, 0.78, 0.73)
RED = fixed(0.85, 0.17, 0.14)
ORANGE = fixed(0.98, 0.45, 0.1)
YELLOW = fixed(0.97, 0.8, 0.2)
GREEN = fixed(0.3, 0.55, 0.25)
WATER = fixed(0.42, 0.68, 0.85)
CRYSTAL = fixed(0.88, 0.94, 1.0)
STRAW = fixed(0.86, 0.72, 0.38)
SCREEN = fixed(0.08, 0.1, 0.13)
LIGHT_F = fixed(1.0, 0.95, 0.75)
LIGHT_R = fixed(0.9, 0.12, 0.1)
PAINT = tinted(0.95, 0.95, 0.95)      # recolourable painted / fabric surface
FABRIC = tinted(0.93, 0.93, 0.93)
LEAF = tinted(0.9, 0.95, 0.88)


def box(k, x0, x1, y0, y1, z0, z1, col, bevel=0.015):
    """Box from its bounds."""
    k.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), col, bevel)


def leg(k, x, y, h, r, col):
    """Turned furniture leg."""
    k.lathe((x, y, 0), [(r * 0.6, 0.0), (r * 0.7, 0.05), (r, 0.12 * h), (r * 0.65, 0.3 * h), (r * 0.8, 0.75 * h), (r, 0.85 * h), (r, h)], col, n=8)


def glow_bulb(k, c, r):
    k.glow_kit().blob(c, r, GLOW, rng_for("bulb"), sub=1, jitter=0.0)


# ================================================================ furniture
def bench():
    k = Kit()
    for x in (-0.75, 0.75):
        k.beam((x, 0.2, 0.0), (x, 0.17, 0.45), 0.06, 0.06, IRON, 0.01)
        k.beam((x, -0.26, 0.0), (x, -0.2, 0.45), 0.06, 0.06, IRON, 0.01)
        k.beam((x, -0.21, 0.44), (x, -0.33, 0.9), 0.06, 0.05, IRON, 0.01)
        k.beam((x, -0.28, 0.43), (x, 0.26, 0.43), 0.06, 0.05, IRON, 0.01)
        k.beam((x, 0.2, 0.44), (x, 0.22, 0.66), 0.05, 0.05, IRON, 0.01)
        k.beam((x, 0.27, 0.67), (x, -0.25, 0.67), 0.08, 0.05, IRON, 0.01)
    for i in range(4):
        y = -0.17 + i * 0.12
        k.box((0, y, 0.47), (1.8, 0.1, 0.04), shade(WOOD, 0.95 + 0.05 * (i % 2)), 0.01)
    for i in range(3):
        z = 0.58 + i * 0.12
        k.box((0, -0.25 - (z - 0.55) * 0.25, z), (1.8, 0.035, 0.1), shade(WOOD, 0.95 + 0.05 * (i % 2)), 0.01, R("X", -14))
    return k


def table():
    k = Kit()
    k.box((0, 0, 0.735), (1.6, 0.9, 0.05), WOOD, 0.015)
    k.box((0, 0, 0.67), (1.42, 0.72, 0.09), WOOD_D, 0.01)
    for sx in (-1, 1):
        for sy in (-1, 1):
            leg(k, sx * 0.71, sy * 0.36, 0.71, 0.045, WOOD_D)
    return k


def table_round():
    k = Kit()
    k.cyl((0, 0, 0.74), 0.6, 0.05, WOOD, n=20, bevel=0.015)
    k.lathe((0, 0, 0), [(0.32, 0.0), (0.32, 0.04), (0.08, 0.1), (0.06, 0.3), (0.09, 0.45), (0.06, 0.6), (0.14, 0.715)], WOOD_D, n=12)
    return k


def chair():
    k = Kit()
    for sx in (-1, 1):
        k.box((sx * 0.19, 0.19, 0.225), (0.045, 0.045, 0.45), WOOD_D, 0.008)
        k.beam((sx * 0.19, -0.19, 0.0), (sx * 0.19, -0.23, 0.98), 0.045, 0.045, WOOD_D, 0.008)
    k.box((0, 0, 0.47), (0.44, 0.44, 0.05), WOOD, 0.01)
    k.box((0, 0.01, 0.51), (0.4, 0.4, 0.05), FABRIC, 0.02)
    k.box((0, -0.225, 0.93), (0.42, 0.045, 0.1), WOOD, 0.01, R("X", -3))
    for x in (-0.1, 0.0, 0.1):
        k.box((x, -0.215, 0.72), (0.05, 0.025, 0.34), WOOD, 0.006, R("X", -3))
    return k


def stool():
    k = Kit()
    k.cyl((0, 0, 0.745), 0.2, 0.07, FABRIC, n=14, bevel=0.02)
    for a in range(4):
        ang = math.radians(45 + 90 * a)
        k.beam((0.2 * math.cos(ang), 0.2 * math.sin(ang), 0.0), (0.11 * math.cos(ang), 0.11 * math.sin(ang), 0.71), 0.03, 0.03, METAL_D, 0.0)
    k.torus((0, 0, 0.28), 0.17, 0.014, CHROME, seg=12)
    k.cyl((0, 0, 0.7), 0.14, 0.03, METAL_D, n=12)
    return k


def bar_counter():
    k = Kit()
    k.box((0, 0, 0.52), (3.0, 0.58, 0.96), PAINT, 0.02)
    k.box((0, 0.0, 0.05), (3.0, 0.62, 0.1), WOOD_D, 0.01)
    for i in range(5):
        x = -1.2 + i * 0.6
        k.box((x, 0.295, 0.55), (0.5, 0.02, 0.7), shade(PAINT, 0.88), 0.01)
    k.box((0, 0.05, 1.03), (3.12, 0.78, 0.06), WOOD_D, 0.02)
    k.cyl((0, 0.42, 0.2), 0.03, 2.95, BRASS, n=8, axis="X", bevel=0.0)
    for x in (-1.2, 0.0, 1.2):
        k.beam((x, 0.3, 0.2), (x, 0.42, 0.2), 0.03, 0.03, BRASS, 0.0)
    box(k, -1.45, 1.45, -0.32, -0.2, 0.45, 0.48, WOOD, 0.005)
    rng = rng_for("barcounter")
    for i in range(10):
        x = -1.3 + i * 0.29
        k.lathe((x, -0.26, 0.48), [(0.035, 0.0), (0.04, 0.02), (0.04, 0.1), (0.02, 0.12)], GLASS, n=8)
        if i % 3 == 0:
            k.lathe((x + 0.1, -0.26, 0.48), [(0.03, 0), (0.035, 0.18), (0.015, 0.24), (0.013, 0.3)], fixed(0.2, 0.45, 0.25), n=6)
    for x in (-0.6, 0.7):
        k.lathe((x, 0.1, 1.06), [(0.03, 0.0), (0.012, 0.1), (0.05, 0.15), (0.05, 0.19)], GLASS, n=8)
    return k


BOTTLES = [fixed(0.15, 0.42, 0.2), fixed(0.58, 0.32, 0.08), fixed(0.72, 0.86, 0.86), fixed(0.2, 0.35, 0.72),
           fixed(0.58, 0.08, 0.12), fixed(0.85, 0.65, 0.2)]


def bottle(k, x, y, z, rng):
    col = rng.choice(BOTTLES)
    kind = rng.randrange(3)
    if kind == 0:
        h = rng.uniform(0.28, 0.33)
        k.lathe((x, y, z), [(0.035, 0), (0.038, 0.02), (0.038, h * 0.62), (0.014, h * 0.8), (0.013, h)], col, n=7)
    elif kind == 1:
        h = rng.uniform(0.22, 0.27)
        k.box((x, y, z + h * 0.35), (0.075, 0.06, h * 0.7), col, 0.01)
        k.cyl((x, y, z + h * 0.85), 0.016, h * 0.3, col, n=6)
    else:
        h = rng.uniform(0.18, 0.24)
        k.lathe((x, y, z), [(0.03, 0), (0.05, 0.04), (0.05, h * 0.45), (0.018, h * 0.7), (0.016, h)], col, n=7)
    k.cyl((x, y, z + h + 0.01), 0.017, 0.025, BLACK if kind else BRASS, n=6, bevel=0.0)
    if rng.random() < 0.6 and kind != 1:
        k.box((x, y + 0.032 + (0.012 if kind == 2 else 0.0), z + h * 0.3), (0.05, 0.008, 0.07), fixed(0.95, 0.92, 0.82), 0.0)


def bar_shelf():
    k = Kit()
    rng = rng_for("barshelf")
    w, d = 2.4, 0.4
    box(k, -w / 2, w / 2, -d / 2, d / 2, 0.0, 0.9, WOOD_D, 0.015)
    for x in (-0.6, 0.6):
        box(k, x - 0.55, x + 0.55, d / 2, d / 2 + 0.02, 0.12, 0.82, WOOD, 0.012)
        k.cyl((x + (0.45 if x < 0 else -0.45), d / 2 + 0.04, 0.6), 0.02, 0.04, BRASS, n=6, axis="Y")
    box(k, -w / 2 - 0.02, w / 2 + 0.02, -d / 2 - 0.02, d / 2 + 0.04, 0.9, 0.95, WOOD, 0.012)
    box(k, -w / 2, w / 2, -d / 2, -d / 2 + 0.03, 0.95, 2.25, WOOD_D, 0.01)
    box(k, -w / 2 + 0.1, w / 2 - 0.1, -d / 2 + 0.03, -d / 2 + 0.04, 1.0, 2.1, GLASS, 0.0)
    for x in (-w / 2 + 0.04, w / 2 - 0.04):
        box(k, x - 0.04, x + 0.04, -d / 2, 0.0, 0.95, 2.25, WOOD_D, 0.01)
    box(k, -w / 2 - 0.04, w / 2 + 0.04, -d / 2, 0.04, 2.25, 2.33, WOOD, 0.015)
    for z in (1.38, 1.8):
        box(k, -w / 2, w / 2, -d / 2, -0.02, z - 0.03, z, WOOD, 0.008)
    for z in (0.95, 1.38, 1.8):
        n = 10
        for i in range(n):
            if rng.random() < 0.12:
                continue
            bottle(k, -w / 2 + 0.15 + i * (w - 0.3) / (n - 1) + rng.uniform(-0.03, 0.03), -0.1, z, rng)
    return k


def sofa(width=2.1, seats=3):
    k = Kit()
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((sx * (width / 2 - 0.1), sy * 0.33, 0.04), 0.03, 0.08, WOOD_D, n=8, r2=0.022)
    k.box((0, 0, 0.22), (width, 0.86, 0.28), shade(FABRIC, 0.9), 0.04)
    k.box((0, -0.34, 0.58), (width, 0.2, 0.52), shade(FABRIC, 0.9), 0.05)
    inner = width - 0.44
    sw = inner / seats
    for i in range(seats):
        x = -inner / 2 + (i + 0.5) * sw
        k.box((x, 0.06, 0.44), (sw - 0.02, 0.66, 0.17), FABRIC, 0.06)
        k.box((x, -0.18, 0.72), (sw - 0.04, 0.18, 0.42), FABRIC, 0.07, R("X", -10))
    for sx in (-1, 1):
        k.box((sx * (width / 2 - 0.11), 0.0, 0.42), (0.22, 0.86, 0.42), shade(FABRIC, 0.95), 0.08)
    return k


def armchair():
    return sofa(1.0, 1)


def bed():
    k = Kit()
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((sx * 0.76, sy * 1.0, 0.08), (0.08, 0.08, 0.16), WOOD_D, 0.01)
    k.box((0, 0, 0.26), (1.66, 2.12, 0.22), WOOD, 0.02)
    k.box((0, 0.02, 0.48), (1.56, 2.02, 0.22), WHITE, 0.05)
    k.box((0, 0.33, 0.62), (1.64, 1.46, 0.09), FABRIC, 0.04)
    k.cyl((0, -0.4, 0.62), 0.07, 1.64, FABRIC, n=8, axis="X")
    for x in (-0.38, 0.38):
        k.box((x, -0.75, 0.64), (0.62, 0.38, 0.14), fixed(0.97, 0.97, 0.95), 0.06)
    k.box((0, -1.06, 0.62), (1.7, 0.08, 1.24), WOOD_D, 0.02)
    for x in (-0.4, 0.4):
        k.box((x, -1.01, 0.85), (0.66, 0.02, 0.5), WOOD, 0.01)
    k.box((0, 1.06, 0.36), (1.7, 0.06, 0.4), WOOD_D, 0.015)
    return k


def desk():
    k = Kit()
    k.box((0, 0, 0.74), (1.4, 0.7, 0.04), WOOD, 0.012)
    box(k, 0.24, 0.68, -0.33, 0.33, 0.0, 0.72, WOOD_D, 0.012)
    for i in range(3):
        z = 0.12 + i * 0.21
        box(k, 0.27, 0.65, 0.33, 0.35, z, z + 0.18, WOOD, 0.008)
        k.box((0.46, 0.365, z + 0.09), (0.1, 0.02, 0.02), BRASS, 0.004)
    for y in (-0.3, 0.3):
        k.box((-0.65, y, 0.36), (0.05, 0.05, 0.72), WOOD_D, 0.008)
    box(k, -0.66, 0.24, -0.33, -0.31, 0.35, 0.7, WOOD_D, 0.008)
    k.box((-0.3, -0.1, 0.77), (0.42, 0.3, 0.015), WHITE, 0.0)
    k.cyl((0.0, 0.1, 0.79), 0.04, 0.09, fixed(0.3, 0.45, 0.6), n=8)
    return k


def office_chair():
    k = Kit()
    for a in range(5):
        ang = math.radians(90 + 72 * a)
        end = (0.3 * math.cos(ang), 0.3 * math.sin(ang))
        k.beam((0, 0, 0.09), (end[0], end[1], 0.07), 0.05, 0.04, METAL_D, 0.006)
        k.cyl((end[0], end[1], 0.035), 0.035, 0.04, BLACK, n=8, axis="X")
    k.cyl((0, 0, 0.27), 0.03, 0.36, CHROME, n=8, bevel=0.0)
    k.cyl((0, 0, 0.1), 0.05, 0.06, METAL_D, n=8)
    k.box((0, 0.02, 0.5), (0.5, 0.5, 0.1), FABRIC, 0.04)
    k.beam((0, -0.2, 0.46), (0, -0.25, 0.7), 0.06, 0.03, METAL_D, 0.005)
    k.box((0, -0.27, 0.92), (0.46, 0.08, 0.56), FABRIC, 0.04, R("X", -6))
    for sx in (-1, 1):
        k.beam((sx * 0.24, 0.0, 0.5), (sx * 0.24, 0.0, 0.7), 0.03, 0.03, METAL_D, 0.0)
        k.box((sx * 0.24, 0.0, 0.71), (0.06, 0.28, 0.04), BLACK, 0.012)
    return k


BOOK_COLS = [fixed(0.6, 0.15, 0.12), fixed(0.15, 0.3, 0.5), fixed(0.2, 0.45, 0.25), fixed(0.85, 0.7, 0.3),
             fixed(0.4, 0.22, 0.4), fixed(0.9, 0.88, 0.8), fixed(0.25, 0.25, 0.28), fixed(0.7, 0.4, 0.2)]


def books(k, x0, x1, y, z, depth, rng, max_h=0.3):
    x = x0
    while x < x1 - 0.06:
        if rng.random() < 0.08:
            x += rng.uniform(0.05, 0.12)
            continue
        w = rng.uniform(0.03, 0.065)
        h = rng.uniform(0.6, 1.0) * max_h
        lean = 0.0
        if rng.random() < 0.08 and x + 0.12 < x1:
            lean = -14.0
        k.box((x + w / 2, y, z + h / 2), (w, depth * rng.uniform(0.75, 0.95), h), rng.choice(BOOK_COLS), 0.004, R("Y", lean))
        x += w + 0.004 + (0.05 if lean else 0.0)


def bookshelf():
    k = Kit()
    rng = rng_for("bookshelf")
    w, d, h = 1.0, 0.35, 2.0
    for x in (-w / 2 + 0.02, w / 2 - 0.02):
        box(k, x - 0.02, x + 0.02, -d / 2, d / 2, 0.0, h, WOOD_D, 0.008)
    box(k, -w / 2, w / 2, -d / 2, -d / 2 + 0.02, 0.0, h, WOOD, 0.0)
    box(k, -w / 2 - 0.02, w / 2 + 0.02, -d / 2, d / 2 + 0.02, h, h + 0.05, WOOD_D, 0.012)
    box(k, -w / 2, w / 2, -d / 2, d / 2, 0.0, 0.1, WOOD_D, 0.008)
    levels = [0.1, 0.5, 0.9, 1.3, 1.65]
    for z in levels[1:]:
        box(k, -w / 2 + 0.04, w / 2 - 0.04, -d / 2, d / 2 - 0.01, z - 0.025, z, WOOD, 0.004)
    for z in levels:
        books(k, -w / 2 + 0.05, w / 2 - 0.05, 0.0, z, d * 0.85, rng, 0.32)
    return k


def cabinet():
    k = Kit()
    w, d, h = 1.2, 0.45, 0.85
    box(k, -w / 2 + 0.02, w / 2 - 0.02, -d / 2, d / 2, 0.08, h, PAINT, 0.015)
    box(k, -w / 2 + 0.04, w / 2 - 0.04, -d / 2 + 0.02, d / 2 - 0.02, 0.0, 0.08, WOOD_D, 0.008)
    box(k, -w / 2 - 0.02, w / 2 + 0.02, -d / 2 - 0.01, d / 2 + 0.03, h, h + 0.04, WOOD, 0.012)
    for x in (-0.3, 0.3):
        box(k, x - 0.27, x + 0.27, d / 2, d / 2 + 0.02, h - 0.2, h - 0.04, shade(PAINT, 0.92), 0.008)
        k.lathe((x, d / 2 + 0.02, h - 0.12), [(0.015, 0), (0.022, 0.02), (0.0, 0.035)], BRASS, n=6, axis="Y")
        box(k, x - 0.27, x + 0.27, d / 2, d / 2 + 0.02, 0.12, h - 0.24, shade(PAINT, 0.92), 0.008)
        box(k, x - 0.2, x + 0.2, d / 2 + 0.02, d / 2 + 0.03, 0.2, h - 0.32, shade(PAINT, 0.85), 0.004)
        k.cyl((x + (0.2 if x < 0 else -0.2), d / 2 + 0.04, 0.4), 0.015, 0.03, BRASS, n=6, axis="Y")
    return k


def wardrobe():
    k = Kit()
    w, d, h = 1.2, 0.8, 2.2
    box(k, -w / 2, w / 2, -d / 2, d / 2 - 0.03, 0.1, h - 0.1, WOOD, 0.015)
    box(k, -w / 2 + 0.02, w / 2 - 0.02, -d / 2 + 0.02, d / 2 - 0.02, 0.0, 0.1, WOOD_D, 0.01)
    box(k, -w / 2 - 0.04, w / 2 + 0.04, -d / 2 - 0.02, d / 2 + 0.03, h - 0.1, h - 0.04, WOOD_D, 0.012)
    box(k, -w / 2 - 0.06, w / 2 + 0.06, -d / 2 - 0.03, d / 2 + 0.05, h - 0.04, h, WOOD, 0.012)
    for s in (-1, 1):
        x = s * 0.295
        box(k, x - 0.29, x + 0.29, d / 2 - 0.03, d / 2, 0.12, h - 0.12, shade(WOOD, 1.05), 0.008)
        for z0, z1 in ((0.25, 1.0), (1.12, 1.95)):
            box(k, x - 0.21, x + 0.21, d / 2, d / 2 + 0.02, z0, z1, shade(WOOD, 0.92), 0.01)
        k.box((s * 0.04, d / 2 + 0.03, 1.08), (0.02, 0.025, 0.2), BRASS, 0.004)
    return k


def rug():
    k = Kit()
    k.box((0, 0, 0.008), (2.4, 1.6, 0.016), fixed(0.55, 0.16, 0.14), 0.004)
    k.box((0, 0, 0.012), (2.1, 1.3, 0.016), FABRIC, 0.003)
    k.box((0, 0, 0.016), (0.62, 0.62, 0.016), fixed(0.85, 0.66, 0.3), 0.0, R("Z", 45))
    for y in (-0.62, 0.62):
        for x in (-0.95, 0.95):
            k.box((x, y, 0.016), (0.2, 0.2, 0.016), fixed(0.85, 0.66, 0.3), 0.0, R("Z", 45))
    for sx in (-1, 1):
        for i in range(12):
            y = -0.72 + i * 1.44 / 11
            k.box((sx * 1.24, y, 0.006), (0.08, 0.03, 0.012), fixed(0.9, 0.86, 0.75), 0.0)
    return k


def pot(k, r, h, col=fixed(0.72, 0.38, 0.22)):
    k.lathe((0, 0, 0), [(r * 0.7, 0.0), (r * 0.78, 0.04), (r * 0.95, h * 0.85), (r * 1.08, h * 0.87), (r * 1.08, h), (r * 0.95, h)], col, n=12)
    k.cyl((0, 0, h - 0.03), r * 0.93, 0.04, fixed(0.3, 0.2, 0.13), n=12, bevel=0.0)


def plant_pot():
    k = Kit()
    rng = rng_for("plantpot")
    pot(k, 0.2, 0.36)
    for i in range(9):
        a = 2 * math.pi * i / 9 + rng.uniform(-0.2, 0.2)
        tilt = rng.uniform(25, 55)
        length = rng.uniform(0.22, 0.32)
        m = R("Z", math.degrees(a)) @ R("Y", tilt)
        top = m @ kc.Vector((0, 0, length))
        k.tube([(0, 0, 0.34), (top.x * 0.5, top.y * 0.5, 0.34 + top.z * 0.5)], 0.008, GREEN, n=4)
        k.blob((top.x * 0.75, top.y * 0.75, 0.34 + top.z * 0.75), 1.0, shade(LEAF, rng.uniform(0.85, 1.0)), rng, sub=1, jitter=0.05,
               scale=(0.05, 0.12, 0.018), m=R("Z", math.degrees(a) - 90) @ R("X", -tilt * 0.6))
    k.blob((0, 0, 0.42), 0.1, LEAF, rng, sub=1, jitter=0.15)
    return k


def plant_big():
    k = Kit()
    rng = rng_for("plantbig")
    pot(k, 0.3, 0.55, fixed(0.82, 0.8, 0.76))
    k.tube([(0, 0, 0.5), (0.05, 0.02, 0.9), (0.0, -0.03, 1.3)], [0.05, 0.04, 0.03], WOOD_D, n=6)
    for c, r in (((0.0, 0.0, 1.45), 0.36), ((0.25, 0.1, 1.25), 0.27), ((-0.22, -0.1, 1.3), 0.28), ((0.05, -0.2, 1.65), 0.25),
                 ((-0.1, 0.18, 1.62), 0.24)):
        k.blob(c, r, shade(LEAF, rng.uniform(0.85, 1.0)), rng, sub=1, jitter=0.15)
    return k


def piano():
    k = Kit()
    outline = [(-0.74, 0.0), (0.74, 0.0), (0.74, -0.55), (0.6, -0.85), (0.32, -1.1), (0.12, -1.45), (-0.05, -1.75),
               (-0.3, -1.9), (-0.55, -1.92), (-0.74, -1.8)]
    k.prism(outline, 0.62, 0.95, BLACK, "Z", 0.02)
    lid = Kit()
    lid.prism([(p[0] + 0.74, p[1]) for p in outline], 0.0, 0.025, BLACK, "Z", 0.008)
    k.merge(lid, T(-0.74, 0, 0.955) @ R("Y", -38))
    k.beam((0.55, -0.9, 0.95), (0.45, -0.95, 1.85), 0.025, 0.025, BLACK, 0.0)
    box(k, -0.74, 0.74, 0.0, 0.36, 0.62, 0.74, BLACK, 0.015)
    box(k, -0.68, 0.68, 0.1, 0.32, 0.74, 0.77, CERAMIC, 0.004)
    pattern = [1, 1, 0, 1, 1, 1, 0]  # black keys between white keys: groups of 2 and 3
    for i in range(27):
        if pattern[i % 7]:
            x = -0.68 + (i + 1) * 1.36 / 28
            k.box((x, 0.24, 0.79), (0.022, 0.14, 0.03), BLACK, 0.003)
    box(k, -0.74, 0.74, 0.0, 0.08, 0.77, 0.92, BLACK, 0.01)
    k.box((0, -0.02, 1.08), (0.6, 0.02, 0.26), BLACK, 0.006, R("X", -12))
    for x, y in ((-0.62, 0.15), (0.62, 0.15), (-0.5, -1.6)):
        k.lathe((x, y, 0), [(0.04, 0.0), (0.06, 0.04), (0.045, 0.2), (0.06, 0.45), (0.07, 0.62)], BLACK, n=8)
    k.box((0, -0.25, 0.3), (0.12, 0.06, 0.6), BLACK, 0.01)
    for x in (-0.05, 0.05):
        k.box((x, -0.18, 0.04), (0.03, 0.1, 0.02), BRASS, 0.0)
    return k


def fireplace():
    k = Kit()
    marble = fixed(0.9, 0.88, 0.84)
    soot = fixed(0.12, 0.1, 0.1)
    box(k, -0.95, 0.95, 0.0, 0.7, 0.0, 0.06, fixed(0.6, 0.58, 0.55), 0.015)
    for s in (-1, 1):
        box(k, s * 0.75 - 0.17, s * 0.75 + 0.17, 0.0, 0.42, 0.06, 1.05, marble, 0.02)
        box(k, s * 0.75 - 0.2, s * 0.75 + 0.2, 0.0, 0.45, 0.06, 0.18, marble, 0.02)
    box(k, -0.95, 0.95, 0.0, 0.44, 1.05, 1.22, marble, 0.02)
    box(k, -1.02, 1.02, -0.02, 0.52, 1.22, 1.29, marble, 0.02)
    box(k, -0.58, 0.58, 0.0, 0.1, 0.06, 1.05, soot, 0.0)
    for s in (-1, 1):
        box(k, s * 0.58 - 0.02, s * 0.58 + 0.02, 0.0, 0.42, 0.06, 1.05, soot, 0.0)
    box(k, -0.58, 0.58, 0.0, 0.42, 0.98, 1.05, soot, 0.0)
    for x in (-0.38, 0.38):
        k.box((x, 0.28, 0.12), (0.03, 0.3, 0.12), IRON, 0.0)
    for i, (a, b) in enumerate((((-0.35, 0.22, 0.2), (0.3, 0.3, 0.2)), ((-0.3, 0.32, 0.22), (0.35, 0.18, 0.24)), ((-0.1, 0.18, 0.3), (0.15, 0.35, 0.3)))):
        k.tube([a, b], 0.06, shade(WOOD_D, 1.0 + 0.1 * i), n=7)
    rng = rng_for("fire")
    for i in range(5):
        x = -0.2 + i * 0.1
        h = rng.uniform(0.22, 0.4)
        k.cyl((x, 0.26, 0.32 + h / 2), 0.06, h, ORANGE if i % 2 else YELLOW, n=5, r2=0.0, bevel=0.0)
    for x in (-0.6, 0.6):
        k.lathe((x, 0.25, 1.29), [(0.06, 0.0), (0.04, 0.05), (0.08, 0.2), (0.06, 0.3)], BRASS, n=8)
    return k


def chandelier():
    """Crystal chandelier, 2.2 m wide. Origin at the ceiling hook, hangs down (-Z)."""
    k = Kit()
    k.lathe((0, 0, -0.12), [(0.03, 0.0), (0.16, 0.04), (0.18, 0.1), (0.0, 0.12)], BRASS, n=12)
    for i in range(7):
        z = -0.17 - i * 0.07
        k.torus((0, 0, z), 0.035, 0.009, BRASS, seg=8, n=4, axis="X" if i % 2 else "Y")
    k.lathe((0, 0, -1.5), [(0.0, 0.0), (0.08, 0.06), (0.14, 0.18), (0.06, 0.3), (0.05, 0.5), (0.12, 0.6), (0.05, 0.72),
                            (0.04, 0.85), (0.08, 0.95), (0.03, 1.0)], BRASS, n=10)
    for tier, (count, rad, z, up) in enumerate(((8, 0.85, -1.18, 0.2), (6, 0.5, -0.82, 0.16))):
        for i in range(count):
            a = 2 * math.pi * (i + 0.5 * tier) / count
            c, s = math.cos(a), math.sin(a)
            pts = [(c * 0.06, s * 0.06, z - 0.02), (c * rad * 0.5, s * rad * 0.5, z - 0.1), (c * rad * 0.92, s * rad * 0.92, z - 0.04),
                   (c * rad, s * rad, z + up * 0.4)]
            k.tube(pts, 0.018, BRASS, n=5)
            cup = (c * rad, s * rad, z + up * 0.4)
            k.lathe(cup, [(0.02, 0.0), (0.06, 0.04), (0.06, 0.05)], BRASS, n=8)
            k.cyl((cup[0], cup[1], cup[2] + 0.12), 0.022, 0.15, CERAMIC, n=6, bevel=0.0)
            glow_bulb(k, (cup[0], cup[1], cup[2] + 0.23), 0.035)
            k.add(kc.bm_hull([(0, 0, 0.04), (0, 0, -0.08), (0.02, 0, -0.02), (-0.02, 0, -0.02), (0, 0.02, -0.02), (0, -0.02, -0.02)]),
                  CRYSTAL, T(c * rad * 0.7, s * rad * 0.7, z - 0.17))
    k.torus((0, 0, -1.12), 1.08, 0.015, BRASS, seg=24, n=4)
    for i in range(24):
        a = 2 * math.pi * i / 24
        x, y = 1.08 * math.cos(a), 1.08 * math.sin(a)
        drop = 0.1 + 0.06 * (i % 2)
        k.add(kc.bm_hull([(0, 0, 0.0), (0, 0, -drop), (0.025, 0, -drop * 0.4), (-0.025, 0, -drop * 0.4), (0, 0.025, -drop * 0.4),
                          (0, -0.025, -drop * 0.4)]), CRYSTAL, T(x, y, -1.14))
    for i in range(12):
        a = 2 * math.pi * i / 12
        for j in range(3):
            r = 0.3 + j * 0.25
            k.add(kc.bm_hull([(0, 0, 0.025), (0, 0, -0.025), (0.02, 0, 0), (-0.02, 0, 0), (0, 0.02, 0), (0, -0.02, 0)]), CRYSTAL,
                  T(r * math.cos(a), r * math.sin(a), -1.3 - 0.05 * j + 0.1))
    k.add(kc.bm_hull([(0, 0, 0.0), (0, 0, -0.2), (0.07, 0, -0.08), (-0.07, 0, -0.08), (0, 0.07, -0.08), (0, -0.07, -0.08)]), CRYSTAL, T(0, 0, -1.5))
    return k


def wall_lamp():
    k = Kit()
    k.box((0, 0.015, 0.12), (0.12, 0.03, 0.24), BRASS, 0.01)
    k.tube([(0, 0.03, 0.1), (0, 0.15, 0.1), (0, 0.2, 0.18)], 0.012, BRASS, n=5)
    k.lathe((0, 0.2, 0.18), [(0.03, 0.0), (0.05, 0.03), (0.1, 0.12), (0.11, 0.16), (0.1, 0.17)], fixed(0.98, 0.94, 0.82), n=10)
    glow_bulb(k, (0, 0.2, 0.25), 0.045)
    return k


def floor_lamp():
    k = Kit()
    k.cyl((0, 0, 0.02), 0.18, 0.04, BRASS, n=14, r2=0.16)
    k.cyl((0, 0, 0.8), 0.014, 1.5, BRASS, n=6, bevel=0.0)
    k.lathe((0, 0, 1.38), [(0.24, 0.0), (0.18, 0.3)], FABRIC, n=14)
    k.lathe((0, 0, 1.38), [(0.235, 0.002), (0.175, 0.298), (0.17, 0.298), (0.23, 0.002)], shade(FABRIC, 0.85), n=14, closed=True)
    k.cyl((0, 0, 1.5), 0.02, 0.06, BRASS, n=6)
    glow_bulb(k, (0, 0, 1.5), 0.06)
    k.cyl((0.08, 0, 1.3), 0.004, 0.16, BRASS, n=4, bevel=0.0)
    return k


def street_lamp():
    k = Kit()
    k.lathe((0, 0, 0), [(0.16, 0.0), (0.16, 0.3), (0.12, 0.36), (0.09, 0.45)], METAL_D, n=10)
    k.cyl((0, 0, 0.45 + 1.95), 0.08, 3.9, METAL_D, n=10, r2=0.06, bevel=0.0)
    k.tube([(0, 0, 4.2), (0, 0.15, 4.45), (0, 0.5, 4.52), (0, 0.9, 4.48)], 0.045, METAL_D, n=6)
    k.box((0, 1.05, 4.46), (0.28, 0.55, 0.1), METAL_D, 0.03)
    k.box((0, 1.05, 4.53), (0.22, 0.45, 0.05), METAL_D, 0.02)
    k.glow_kit().box((0, 1.05, 4.405), (0.22, 0.45, 0.02), GLOW, 0.005)
    k.box((0, -0.08, 1.2), (0.12, 0.05, 0.3), METAL, 0.01)
    return k


def lamp_post_ornate():
    k = Kit()
    k.lathe((0, 0, 0), [(0.24, 0.0), (0.24, 0.08), (0.2, 0.12), (0.17, 0.5), (0.2, 0.55), (0.12, 0.62), (0.09, 0.9), (0.12, 0.95)], IRON, n=12)
    k.cyl((0, 0, 0.95 + 1.2), 0.06, 2.4, IRON, n=8, r2=0.05, bevel=0.0)
    for z in (1.6, 2.6, 3.2):
        k.torus((0, 0, z), 0.065, 0.02, IRON, seg=10, n=4)
    for s in (-1, 1):
        k.tube([(0, 0, 2.9), (s * 0.18, 0, 2.95), (s * 0.25, 0, 3.12), (s * 0.18, 0, 3.22), (s * 0.12, 0, 3.15)], 0.018, IRON, n=4)
    k.lathe((0, 0, 3.32), [(0.06, 0.0), (0.16, 0.08), (0.16, 0.11)], IRON, n=8)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.beam((sx * 0.12, sy * 0.12, 3.43), (sx * 0.17, sy * 0.17, 3.85), 0.025, 0.025, IRON, 0.0)
    k.hull([(sx * 0.11, sy * 0.11, 3.44) for sx in (-1, 1) for sy in (-1, 1)] + [(sx * 0.16, sy * 0.16, 3.84) for sx in (-1, 1) for sy in (-1, 1)],
           fixed(0.95, 0.88, 0.65))
    k.hull([(sx * 0.21, sy * 0.21, 3.86) for sx in (-1, 1) for sy in (-1, 1)] + [(0, 0, 4.08)], IRON, 0.01)
    k.lathe((0, 0, 4.06), [(0.03, 0.0), (0.05, 0.05), (0.0, 0.14)], IRON, n=6)
    glow_bulb(k, (0, 0, 3.62), 0.09)
    return k


# ================================================================ street dressing
def trash_bin():
    k = Kit()
    k.lathe((0, 0, 0), [(0.24, 0.0), (0.26, 0.05), (0.28, 0.85), (0.3, 0.86), (0.3, 0.9)], PAINT, n=14)
    for z in (0.25, 0.6):
        k.torus((0, 0, z), 0.272, 0.018, shade(PAINT, 0.8), seg=14, n=4)
    k.lathe((0, 0, 0.9), [(0.31, 0.0), (0.31, 0.04), (0.2, 0.12), (0.0, 0.15)], METAL_D, n=14)
    k.box((0, 0.29, 0.75), (0.2, 0.04, 0.08), BLACK, 0.01)
    return k


def dumpster():
    k = Kit()
    prof = [(-0.6, 0.18), (0.6, 0.18), (0.7, 1.25), (-0.62, 1.25)]
    k.prism(prof, -0.95, 0.95, PAINT, "X", 0.03)
    k.prism([(0.66, 1.18), (0.78, 1.18), (0.78, 1.28), (-0.66, 1.28), (-0.66, 1.18), (-0.6, 1.18)], -1.0, 1.0, shade(PAINT, 0.85), "X", 0.015)
    for x in (-0.6, -0.2, 0.2, 0.6):
        k.prism([(0.6, 0.22), (0.66, 0.22), (0.75, 1.17), (0.69, 1.17)], x - 0.04, x + 0.04, shade(PAINT, 0.85), "X", 0.008)
    for s in (-1, 1):
        lid = Kit()
        lid.box((0, 0, 0), (0.96, 1.45, 0.04), fixed(0.18, 0.2, 0.2), 0.012)
        for i in range(3):
            lid.box((0, -0.5 + i * 0.5, 0.03), (0.9, 0.08, 0.03), fixed(0.18, 0.2, 0.2), 0.01)
        k.merge(lid, T(s * 0.49, 0.06, 1.3) @ R("X", -3))
        box(k, s * 0.95 - (0.1 if s > 0 else 0.0), s * 0.95 + (0.0 if s > 0 else 0.1), -0.35, 0.35, 0.5, 0.75, shade(PAINT, 0.8), 0.015)
        for y in (-0.45, 0.45):
            k.cyl((s * 0.75, y, 0.08), 0.08, 0.06, RUBBER, n=10, axis="X")
            box(k, s * 0.75 - 0.05, s * 0.75 + 0.05, y - 0.06, y + 0.06, 0.12, 0.2, METAL_D, 0.005)
    return k


def hydrant():
    k = Kit()
    k.lathe((0, 0, 0), [(0.16, 0.0), (0.16, 0.05), (0.12, 0.07), (0.11, 0.45), (0.14, 0.47), (0.14, 0.52), (0.12, 0.55),
                        (0.1, 0.62), (0.04, 0.68), (0.03, 0.72)], PAINT, n=10)
    for axis, c in (("X", (0.15, 0, 0.36)), ("X", (-0.15, 0, 0.36)), ("Y", (0, 0.16, 0.33))):
        r = 0.05 if axis == "X" else 0.07
        k.cyl(c, r, 0.1, PAINT, n=8, axis=axis)
        cap = (c[0] * 1.35, c[1], c[2]) if axis == "X" else (c[0], c[1] * 1.3, c[2])
        k.cyl(cap, r * 1.1, 0.04, CHROME, n=6, axis=axis)
    return k


def mailbox():
    k = Kit()
    k.box((0, 0, 0.55), (0.1, 0.1, 1.1), WOOD, 0.012)
    k.box((0, 0, 1.08), (0.22, 0.5, 0.04), WOOD, 0.01)
    body = Kit()
    prof = [(-0.12, 0.0), (0.12, 0.0)] + [(0.12 * math.cos(math.pi * i / 8), 0.17 + 0.12 * math.sin(math.pi * i / 8)) for i in range(9)]
    body.prism(prof, -0.24, 0.24, PAINT, "Y", 0.012)
    body.prism([(p[0] * 1.06, p[1] * 1.03 - 0.005) for p in prof], 0.24, 0.26, shade(PAINT, 0.85), "Y", 0.006)
    body.box((0, 0.265, 0.2), (0.05, 0.02, 0.03), CHROME, 0.004)
    k.merge(body, T(0, 0, 1.1))
    k.box((0.14, -0.05, 1.32), (0.015, 0.04, 0.22), RED, 0.004)
    k.box((0.14, 0.0, 1.4), (0.015, 0.12, 0.08), RED, 0.004)
    return k


def street_sign():
    k = Kit()
    k.cyl((0, 0, 1.55), 0.035, 3.1, METAL, n=8, bevel=0.0)
    k.cyl((0, 0, 3.12), 0.045, 0.04, METAL, n=8)
    k.box((0.42, 0, 2.96), (0.9, 0.025, 0.18), fixed(0.1, 0.45, 0.25), 0.008)
    k.box((0, 0.42, 2.76), (0.025, 0.9, 0.18), fixed(0.1, 0.45, 0.25), 0.008)
    octa = [(0.33 * math.cos(math.pi / 8 + math.pi * i / 4), 0.33 * math.sin(math.pi / 8 + math.pi * i / 4)) for i in range(8)]
    k.prism([(p[0], p[1] + 2.05) for p in octa], 0.04, 0.06, WHITE, "Y", 0.004)
    k.prism([(p[0] * 0.88, p[1] * 0.88 + 2.05) for p in octa], 0.06, 0.07, RED, "Y", 0.003)
    k.box((0, 0.072, 2.05), (0.36, 0.006, 0.08), WHITE, 0.0)
    k.box((0, 0.045, 1.45), (0.36, 0.02, 0.46), WHITE, 0.006)
    k.box((0, 0.056, 1.45), (0.3, 0.006, 0.38), fixed(0.15, 0.3, 0.7), 0.0)
    return k


def traffic_cone():
    k = Kit()
    k.box((0, 0, 0.02), (0.38, 0.38, 0.04), fixed(0.85, 0.35, 0.08), 0.01)
    k.lathe((0, 0, 0.04), [(0.15, 0.0), (0.03, 0.66), (0.0, 0.67)], ORANGE, n=12)
    k.lathe((0, 0, 0.28), [(0.114, 0.0), (0.096, 0.1)], WHITE, n=12)
    k.lathe((0, 0, 0.28), [(0.112, 0.0), (0.094, 0.1), (0.09, 0.1), (0.108, 0.0)], WHITE, n=12, closed=True)
    return k


def barrier():
    k = Kit()
    prof = [(-0.3, 0.0), (0.3, 0.0), (0.3, 0.08), (0.18, 0.25), (0.09, 0.8), (-0.09, 0.8), (-0.18, 0.25), (-0.3, 0.08)]
    k.prism(prof, -1.0, 1.0, CONCRETE, "X", 0.02)
    for x in (-0.7, -0.1, 0.5):
        k.box((x, 0.131, 0.53), (0.36, 0.012, 0.18), RED, 0.0, R("X", -9))
        k.box((x + 0.3, 0.131, 0.53), (0.24, 0.012, 0.18), WHITE, 0.0, R("X", -9))
    for x in (-0.55, 0.55):
        k.box((x, 0, 0.03), (0.2, 0.62, 0.08), shade(CONCRETE, 0.7), 0.0)
    return k


def bollard():
    k = Kit()
    k.lathe((0, 0, 0), [(0.13, 0.0), (0.13, 0.04), (0.1, 0.06), (0.1, 0.82), (0.07, 0.9), (0.0, 0.92)], METAL_D, n=12)
    k.lathe((0, 0, 0.66), [(0.104, 0.0), (0.104, 0.06), (0.1, 0.06), (0.1, 0.0)], YELLOW, n=12, closed=True)
    return k


def crate(size=1.0):
    k = Kit()
    s = size
    t = 0.07 * s
    k.box((0, 0, s / 2), (s - 0.04, s - 0.04, s - 0.04), WOOD_L, 0.01)
    for i in range(1, 4):
        z = i * s / 4
        for y in (-s / 2 + 0.015, s / 2 - 0.015):
            box(k, -s / 2 + t, s / 2 - t, y - 0.012, y + 0.012, z - 0.006, z + 0.006, shade(WOOD_L, 0.75), 0.0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((sx * (s / 2 - t / 2), sy * (s / 2 - t / 2), s / 2), (t, t, s), WOOD, 0.012)
        for z in (t / 2, s - t / 2):
            k.box((0, sx * (s / 2 - t / 2), z), (s, t, t), WOOD, 0.012)
            k.box((sx * (s / 2 - t / 2), 0, z), (t, s, t), WOOD, 0.012)
    for sy in (-1, 1):
        k.beam((-s / 2 + t, sy * (s / 2 - 0.005), t), (s / 2 - t, sy * (s / 2 - 0.005), s - t), t * 0.9, 0.03, WOOD, 0.008)
    for sx in (-1, 1):
        k.beam((sx * (s / 2 - 0.005), -s / 2 + t, s - t), (sx * (s / 2 - 0.005), s / 2 - t, t), 0.03, t * 0.9, WOOD, 0.008)
    k.box((0, s / 2 + 0.003, s * 0.55), (s * 0.3, 0.01, s * 0.12), fixed(0.25, 0.18, 0.1), 0.0)
    return k


def crate_large():
    return crate(1.6)


def pallet():
    k = Kit()
    for i in range(7):
        y = -0.5 + 0.05 + i * 0.9 / 6
        k.box((0, y, 0.133), (1.2, 0.1, 0.022), shade(WOOD_L, 0.95 + 0.05 * (i % 2)), 0.005)
    for y in (-0.45, 0.0, 0.45):
        k.box((0, y, 0.11), (1.2, 0.1, 0.022), WOOD, 0.004)
        for x in (-0.55, 0.0, 0.55):
            k.box((x, y, 0.06), (0.1, 0.1, 0.078), WOOD, 0.006)
    for x in (-0.55, 0.0, 0.55):
        k.box((x, 0, 0.011), (0.1, 1.0, 0.022), WOOD, 0.004)
    return k


def barrel():
    k = Kit()
    k.lathe((0, 0, 0), [(0.27, 0.0), (0.29, 0.02), (0.29, 0.86), (0.27, 0.88)], PAINT, n=16)
    for z in (0.29, 0.59):
        k.torus((0, 0, z), 0.292, 0.018, shade(PAINT, 0.85), seg=16, n=4)
    k.torus((0, 0, 0.87), 0.275, 0.016, shade(PAINT, 0.8), seg=16, n=4)
    k.cyl((0, 0, 0.875), 0.26, 0.01, shade(PAINT, 0.9), n=16, bevel=0.0)
    k.cyl((0.14, 0.06, 0.885), 0.035, 0.02, METAL_D, n=6)
    k.cyl((-0.15, -0.05, 0.882), 0.022, 0.015, METAL_D, n=6)
    return k


def gas_bottle():
    k = Kit()
    k.lathe((0, 0, 0), [(0.14, 0.0), (0.14, 0.06), (0.15, 0.07), (0.15, 0.95), (0.12, 1.05), (0.06, 1.1), (0.03, 1.12)], PAINT, n=12)
    k.lathe((0, 0, 0), [(0.15, 0.0), (0.15, 0.07), (0.14, 0.07), (0.14, 0.0)], METAL_D, n=12, closed=True)
    k.cyl((0, 0, 1.15), 0.025, 0.07, BRASS, n=6)
    k.cyl((0.04, 0, 1.17), 0.012, 0.06, BRASS, n=6, axis="X")
    k.torus((0, 0, 1.24), 0.07, 0.012, METAL_D, seg=10, n=4)
    for a in (0, 120, 240):
        x, y = 0.07 * math.cos(math.radians(a)), 0.07 * math.sin(math.radians(a))
        k.beam((x * 0.9, y * 0.9, 1.08), (x, y, 1.24), 0.015, 0.015, METAL_D, 0.0)
    return k


def shipping_container():
    """6.1 x 2.5 x 2.6 corrugated container, doors on +X."""
    k = Kit()
    L, Wd, Ht = 6.1, 2.5, 2.6
    body = PAINT
    k.box((0, 0, Ht / 2), (L - 0.2, Wd - 0.2, Ht - 0.2), shade(body, 0.8), 0.0)
    period, depth = 0.28, 0.045
    for side in (-1, 1):
        y_in = side * (Wd / 2 - 0.12)
        pts = []
        n = int((L - 0.3) / period)
        x0 = -(n * period) / 2
        for i in range(n + 1):
            x = x0 + i * period
            pts.append((x, side * (Wd / 2 - 0.06)))
            if i < n:
                pts.append((x + period * 0.2, side * (Wd / 2 - 0.06 + depth)))
                pts.append((x + period * 0.5, side * (Wd / 2 - 0.06 + depth)))
                pts.append((x + period * 0.7, side * (Wd / 2 - 0.06)))
        pts += [(pts[-1][0], y_in), (pts[0][0], y_in)]
        k.prism(pts, 0.14, Ht - 0.14, body, "Z", 0.0)
    pts = []
    n = int((Wd - 0.3) / period)
    y0 = -(n * period) / 2
    for i in range(n + 1):
        y = y0 + i * period
        pts.append((y, 0.0))
        if i < n:
            pts.append((y + period * 0.2, -depth))
            pts.append((y + period * 0.5, -depth))
            pts.append((y + period * 0.7, 0.0))
    pts += [(pts[-1][0], 0.08), (pts[0][0], 0.08)]
    end = Kit()
    end.prism(pts, 0.14, Ht - 0.14, body, "Z", 0.0)
    k.merge(end, T(-L / 2 + 0.06, 0, 0) @ R("Z", 90) @ S(1, -1, 1))
    for i in range(5):
        x = -L / 2 + 0.6 + i * (L - 1.2) / 4
        k.box((x, 0, Ht - 0.06), (0.9, Wd - 0.15, 0.04), shade(body, 0.93), 0.01)
    rail = shade(body, 0.75)
    for sz in (0.07, Ht - 0.07):
        for sy in (-1, 1):
            k.box((0, sy * (Wd / 2 - 0.07), sz), (L, 0.14, 0.14), rail, 0.015)
        for sx in (-1, 1):
            k.box((sx * (L / 2 - 0.07), 0, sz), (0.14, Wd, 0.14), rail, 0.015)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((sx * (L / 2 - 0.07), sy * (Wd / 2 - 0.07), Ht / 2), (0.15, 0.15, Ht), rail, 0.015)
            for sz in (0.09, Ht - 0.09):
                k.box((sx * (L / 2 - 0.08), sy * (Wd / 2 - 0.08), sz), (0.19, 0.19, 0.19), METAL_D, 0.015)
    for sy in (-1, 1):
        yc = sy * Wd / 4
        box(k, L / 2 - 0.08, L / 2 - 0.02, yc - Wd / 4 + 0.13, yc + Wd / 4 - 0.02, 0.15, Ht - 0.15, body, 0.01)
        for j in range(3):
            box(k, L / 2 - 0.02, L / 2, yc - Wd / 4 + 0.2, yc + Wd / 4 - 0.1, 0.35 + j * 0.7, 0.75 + j * 0.7, shade(body, 0.9), 0.005)
        for yb in (yc - 0.25, yc + 0.25):
            k.cyl((L / 2 + 0.03, yb, Ht / 2), 0.022, Ht - 0.3, METAL, n=6, bevel=0.0)
            k.box((L / 2 + 0.06, yb, 1.2), (0.05, 0.18, 0.06), METAL, 0.008)
            for zb in (0.25, Ht - 0.25):
                k.box((L / 2 + 0.025, yb, zb), (0.05, 0.08, 0.1), METAL_D, 0.005)
    return k


def market_stall():
    k = Kit()
    rng = rng_for("stall")
    box(k, -1.2, 1.2, 0.05, 0.85, 0.0, 0.85, WOOD, 0.015)
    for i in range(4):
        box(k, -1.2 + 0.05 + i * 0.6, -1.2 + (i + 1) * 0.6 - 0.02, 0.85, 0.87, 0.06, 0.8, shade(WOOD, 0.9), 0.006)
    box(k, -1.25, 1.25, 0.0, 0.9, 0.85, 0.9, WOOD_D, 0.012)
    for x in (-1.2, 1.2):
        k.box((x, 0.85, 1.15), (0.08, 0.08, 2.3), WOOD_D, 0.01)
        k.box((x, -0.7, 1.35), (0.08, 0.08, 2.7), WOOD_D, 0.01)
    fruit = [fixed(0.95, 0.5, 0.1), fixed(0.85, 0.15, 0.12), fixed(0.55, 0.75, 0.2), fixed(0.95, 0.85, 0.25)]
    for i, x in enumerate((-0.8, 0.0, 0.8)):
        box(k, x - 0.33, x + 0.33, 0.15, 0.75, 0.9, 1.05, WOOD_L, 0.01)
        col = fruit[i]
        for j in range(9):
            fx = x - 0.22 + (j % 3) * 0.22 + rng.uniform(-0.03, 0.03)
            fy = 0.28 + (j // 3) * 0.17
            k.blob((fx, fy, 1.1), 0.075, col, rng, sub=1, jitter=0.08)
    rows = 8
    sw = 2.6 / rows
    for i in range(rows):
        x = -1.3 + (i + 0.5) * sw
        col = FABRIC if i % 2 == 0 else fixed(0.96, 0.94, 0.88)
        k.beam((x, -0.85, 2.72), (x, 1.05, 2.25), sw, 0.03, col, 0.005)
        k.box((x, 1.05, 2.13), (sw, 0.02, 0.2), col, 0.003)
        arc = [(x + sw / 2 * math.cos(math.pi * t / 6), 2.03 - 0.12 * math.sin(math.pi * t / 6)) for t in range(7)]
        k.prism(arc, 1.04, 1.06, col, "Y", 0.0)
    box(k, -0.6, 0.6, 0.86, 0.9, 1.75, 2.0, WOOD_L, 0.01)
    return k


def parasol_kit(radius=1.3, height=2.4):
    k = Kit()
    k.cyl((0, 0, height / 2), 0.025, height, WOOD_L, n=6, bevel=0.0)
    seg = 8
    top = (0, 0, height + 0.05)
    drop = 0.45
    for i in range(seg):
        a0, a1 = 2 * math.pi * i / seg, 2 * math.pi * (i + 1) / seg
        p0 = (radius * math.cos(a0), radius * math.sin(a0), height + 0.05 - drop)
        p1 = (radius * math.cos(a1), radius * math.sin(a1), height + 0.05 - drop)
        col = FABRIC if i % 2 == 0 else fixed(0.97, 0.96, 0.92)
        k.hull([top, p0, p1, (0, 0, top[2] - 0.03), (p0[0], p0[1], p0[2] - 0.03), (p1[0], p1[1], p1[2] - 0.03)], col)
        mid = ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2)
        k.box((mid[0], mid[1], p0[2] - 0.09), (math.dist(p0[:2], p1[:2]), 0.015, 0.14), col, 0.0, R("Z", math.degrees((a0 + a1) / 2) + 90))
        k.beam((0, 0, height - 0.3), (p0[0] * 0.6, p0[1] * 0.6, p0[2] - 0.02), 0.015, 0.015, WOOD_L, 0.0)
    k.lathe(top, [(0.04, 0.0), (0.0, 0.1)], WOOD_L, n=6)
    return k


def parasol():
    k = parasol_kit()
    k.cyl((0, 0, 0.04), 0.35, 0.08, CONCRETE, n=12)
    return k


def umbrella_table():
    k = Kit()
    k.cyl((0, 0, 0.735), 0.5, 0.03, METAL, n=16)
    k.torus((0, 0, 0.72), 0.5, 0.015, METAL_D, seg=16, n=4)
    for a in range(4):
        ang = math.radians(45 + 90 * a)
        k.beam((0.42 * math.cos(ang), 0.42 * math.sin(ang), 0.0), (0.12 * math.cos(ang), 0.12 * math.sin(ang), 0.72), 0.03, 0.03, METAL_D, 0.0)
    k.merge(parasol_kit(1.25, 2.3))
    return k


def fountain():
    k = Kit()
    k.lathe((0, 0, 0), [(1.72, 0.0), (2.0, 0.0), (2.0, 0.42), (2.06, 0.46), (2.06, 0.56), (1.78, 0.56), (1.78, 0.46), (1.72, 0.44)], STONE, n=24, closed=True)
    k.cyl((0, 0, 0.05), 1.75, 0.1, shade(STONE, 0.8), n=24, bevel=0.0)
    k.cyl((0, 0, 0.36), 1.76, 0.04, WATER, n=24, bevel=0.0)
    k.lathe((0, 0, 0.1), [(0.4, 0.0), (0.4, 0.15), (0.26, 0.25), (0.2, 0.9), (0.28, 1.0), (0.85, 1.1), (0.9, 1.2), (0.82, 1.22), (0.0, 1.22)], STONE, n=16)
    k.cyl((0, 0, 1.31), 0.8, 0.03, WATER, n=16, bevel=0.0)
    k.lathe((0, 0, 1.3), [(0.14, 0.0), (0.1, 0.5), (0.45, 0.62), (0.48, 0.7), (0.0, 0.7)], STONE, n=12)
    k.cyl((0, 0, 2.01), 0.43, 0.03, WATER, n=12, bevel=0.0)
    k.lathe((0, 0, 2.0), [(0.08, 0.0), (0.12, 0.12), (0.06, 0.3), (0.0, 0.4)], STONE, n=8)
    k.cyl((0, 0, 2.55), 0.035, 0.3, WATER, n=6, bevel=0.0)
    for i in range(8):
        a = 2 * math.pi * i / 8
        k.tube([(0.7 * math.cos(a), 0.7 * math.sin(a), 1.32), (0.95 * math.cos(a), 0.95 * math.sin(a), 1.2),
                (1.1 * math.cos(a), 1.1 * math.sin(a), 0.4)], [0.03, 0.025, 0.02], WATER, n=4)
    return k


def statue():
    k = Kit()
    bronze = fixed(0.36, 0.56, 0.5)
    k.box((0, 0, 0.1), (1.4, 1.4, 0.2), shade(STONE, 0.9), 0.03)
    k.box((0, 0, 0.75), (1.1, 1.1, 1.1), STONE, 0.03)
    k.box((0, 0, 1.35), (1.25, 1.25, 0.12), shade(STONE, 0.95), 0.03)
    k.box((0, 0.552, 0.8), (0.6, 0.01, 0.25), BRASS, 0.004)
    rng = rng_for("statue")
    base = 1.41
    for s in (-1, 1):
        k.tube([(s * 0.14, 0.0, base), (s * 0.16, 0.05, base + 0.45), (s * 0.12, 0.0, base + 0.85)], [0.1, 0.11, 0.12], bronze, n=8)
    k.blob((0, 0, base + 1.2), 1.0, bronze, rng, sub=2, jitter=0.03, scale=(0.3, 0.22, 0.45))
    k.blob((0, 0.02, base + 1.85), 0.2, bronze, rng, sub=2, jitter=0.02)
    k.tube([(0.26, 0, base + 1.45), (0.4, 0.05, base + 1.8), (0.38, 0.0, base + 2.2)], [0.08, 0.07, 0.06], bronze, n=7)
    k.tube([(-0.26, 0, base + 1.45), (-0.4, 0.15, base + 1.2), (-0.3, 0.3, base + 1.05)], [0.08, 0.07, 0.06], bronze, n=7)
    k.torus((0.38, 0.0, base + 2.42), 0.22, 0.04, bronze, seg=16, n=6, axis="Y")
    k.add(kc.bm_hull([(0, 0, 0.12), (0, 0, -0.12), (0.1, 0, 0), (-0.1, 0, 0), (0, 0.1, 0), (0, -0.1, 0)]), BRASS, T(0.38, 0.0, base + 2.42))
    return k


# ================================================================ rooms & appliances
def toilet():
    k = Kit()
    k.hull([(sx * 0.13, sy, 0.0) for sx in (-1, 1) for sy in (-0.2, 0.12)] + [(sx * 0.11, sy, 0.3) for sx in (-1, 1) for sy in (-0.18, 0.1)], CERAMIC, 0.02)
    k.lathe((0, 0.05, 0.28), [(0.12, 0.0), (0.2, 0.1), (0.21, 0.14)], CERAMIC, n=14, m=S(0.95, 1.25, 1))
    k.torus((0, 0.05, 0.43), 0.17, 0.025, WHITE, seg=14, n=5, m=S(0.95, 1.25, 1))
    k.box((0, -0.28, 0.6), (0.42, 0.18, 0.36), CERAMIC, 0.03)
    k.box((0, -0.28, 0.8), (0.45, 0.21, 0.04), CERAMIC, 0.015)
    k.box((-0.17, -0.18, 0.72), (0.06, 0.02, 0.02), CHROME, 0.004)
    k.box((0, -0.15, 0.62), (0.4, 0.03, 0.34), WHITE, 0.012, R("X", -8))
    return k


def sink():
    k = Kit()
    k.lathe((0, 0, 0), [(0.13, 0.0), (0.1, 0.05), (0.08, 0.6), (0.12, 0.72)], CERAMIC, n=10)
    basin = kc.bm_box(0.58, 0.46, 0.18)
    basin.transform(T(0, 0.0, 0.8))
    cut = kc.bm_blob(1.0, 2, 0.0, rng_for("sink"))
    cut.transform(T(0, 0.04, 0.9) @ S(0.23, 0.17, 0.14))
    k.add(kc.bm_boolean(basin, [cut]), CERAMIC, bevel=0.015)
    k.cyl((0, 0.05, 0.775), 0.02, 0.01, METAL_D, n=8)
    k.tube([(0, -0.17, 0.89), (0, -0.17, 1.0), (0, -0.07, 1.03), (0, -0.04, 0.98)], 0.015, CHROME, n=6)
    for x in (-0.12, 0.12):
        k.cyl((x, -0.17, 0.91), 0.025, 0.04, CHROME, n=6)
        k.box((x, -0.17, 0.94), (0.06, 0.012, 0.012), CHROME, 0.0)
    return k


def fridge():
    k = Kit()
    k.box((0, 0, 0.93), (0.78, 0.7, 1.82), PAINT, 0.05)
    for z0, z1 in ((0.06, 1.2), (1.24, 1.8)):
        box(k, -0.38, 0.38, 0.34, 0.37, z0, z1, PAINT, 0.025)
        k.box((-0.3, 0.4, (z0 + z1) / 2 + (0.2 if z1 < 1.5 else 0.0)), (0.03, 0.04, min(0.5, (z1 - z0) * 0.6)), CHROME, 0.01)
    k.box((0, 0.352, 1.22), (0.76, 0.01, 0.03), METAL_D, 0.0)
    k.box((0.2, 0.375, 1.7), (0.12, 0.01, 0.04), CHROME, 0.0)
    for x in (-0.3, 0.3):
        k.cyl((x, 0.2, 0.01), 0.03, 0.03, BLACK, n=6)
    return k


def stove():
    k = Kit()
    steel = fixed(0.85, 0.86, 0.88)
    k.box((0, 0, 0.45), (0.76, 0.65, 0.9), steel, 0.02)
    k.box((0, 0, 0.915), (0.76, 0.65, 0.03), BLACK, 0.008)
    for x in (-0.18, 0.18):
        for y in (-0.15, 0.13):
            k.cyl((x, y, 0.935), 0.1, 0.012, METAL_D, n=12, bevel=0.0)
            k.torus((x, y, 0.95), 0.08, 0.008, IRON, seg=10, n=4)
            k.box((x, y, 0.955), (0.2, 0.015, 0.012), IRON, 0.0)
            k.box((x, y, 0.955), (0.015, 0.2, 0.012), IRON, 0.0)
    box(k, -0.36, 0.36, 0.325, 0.345, 0.12, 0.7, steel, 0.012)
    box(k, -0.24, 0.24, 0.345, 0.35, 0.25, 0.55, SCREEN, 0.0)
    k.cyl((0, 0.39, 0.64), 0.015, 0.6, CHROME, n=6, axis="X", bevel=0.0)
    for x in (-0.3, 0.3):
        k.box((x, 0.36, 0.64), (0.02, 0.04, 0.02), CHROME, 0.0)
    for i in range(4):
        k.cyl((-0.27 + i * 0.18, 0.34, 0.82), 0.025, 0.03, BLACK, n=8, axis="Y")
    box(k, -0.38, 0.38, -0.33, -0.28, 0.93, 1.15, steel, 0.012)
    k.box((0, -0.275, 1.05), (0.12, 0.01, 0.06), SCREEN, 0.0)
    return k


def tv():
    k = Kit()
    k.box((0, 0, 0.01), (0.5, 0.25, 0.02), BLACK, 0.006)
    k.box((0, -0.02, 0.15), (0.06, 0.03, 0.28), BLACK, 0.006)
    k.box((0, 0, 0.62), (1.2, 0.06, 0.72), BLACK, 0.015)
    k.box((0, 0.032, 0.62), (1.12, 0.005, 0.64), SCREEN, 0.0)
    k.box((0.5, 0.034, 0.3), (0.04, 0.003, 0.01), fixed(0.3, 0.8, 1.0), 0.0)
    return k


def radio():
    k = Kit()
    k.box((0, 0, 0.13), (0.42, 0.18, 0.24), WOOD, 0.03)
    box(k, -0.19, 0.02, 0.09, 0.095, 0.04, 0.22, fixed(0.85, 0.78, 0.6), 0.008)
    for i in range(6):
        k.box((-0.085, 0.097, 0.06 + i * 0.03), (0.19, 0.008, 0.012), WOOD_D, 0.0)
    box(k, 0.05, 0.19, 0.09, 0.095, 0.14, 0.22, fixed(0.95, 0.9, 0.7), 0.004)
    k.box((0.11, 0.097, 0.18), (0.006, 0.004, 0.07), RED, 0.0)
    for x in (0.08, 0.16):
        k.cyl((x, 0.1, 0.08), 0.025, 0.03, METAL_D, n=8, axis="Y")
    k.tube([(-0.12, 0, 0.25), (-0.1, 0, 0.3), (0.1, 0, 0.3), (0.12, 0, 0.25)], 0.012, METAL_D, n=5)
    k.cyl((0.18, -0.05, 0.38), 0.005, 0.3, CHROME, n=4, bevel=0.0, m=R("Y", 25))
    return k


def phone():
    k = Kit()
    k.hull([(sx * 0.11, sy * 0.1, 0.0) for sx in (-1, 1) for sy in (-1, 1)] + [(sx * 0.08, sy * 0.07, 0.08) for sx in (-1, 1) for sy in (-1, 1)], PAINT, 0.01)
    k.cyl((0, 0.06, 0.07), 0.055, 0.015, CERAMIC, n=10, m=R("X", -25))
    k.cyl((0, 0.062, 0.075), 0.02, 0.015, PAINT, n=8, m=R("X", -25))
    k.box((0, -0.02, 0.115), (0.26, 0.05, 0.04), PAINT, 0.015)
    for x in (-0.11, 0.11):
        k.box((x, -0.02, 0.1), (0.06, 0.07, 0.04), PAINT, 0.015)
    k.tube([(-0.12, -0.05, 0.09), (-0.17, -0.06, 0.06), (-0.18, 0.02, 0.02), (-0.14, 0.08, 0.01)], 0.008, BLACK, n=4)
    return k


def generator():
    k = Kit()
    for sx in (-1, 1):
        k.tube([(sx * 0.6, -0.35, 0.0), (sx * 0.6, -0.35, 0.8), (sx * 0.6, 0.35, 0.8), (sx * 0.6, 0.35, 0.0)], 0.025, IRON, n=6)
        k.cyl((sx * 0.6, 0.0, 0.02), 0.022, 0.7, IRON, n=6, axis="Y", bevel=0.0)
    k.cyl((0, -0.35, 0.8), 0.022, 1.2, IRON, n=6, axis="X", bevel=0.0)
    k.cyl((0, 0.35, 0.8), 0.022, 1.2, IRON, n=6, axis="X", bevel=0.0)
    k.box((-0.15, 0, 0.36), (0.6, 0.5, 0.5), METAL_D, 0.03)
    for i in range(5):
        k.box((-0.15, 0, 0.64 + i * 0.03), (0.4, 0.42, 0.012), METAL, 0.0)
    k.box((0.38, 0, 0.32), (0.36, 0.5, 0.44), PAINT, 0.04)
    k.box((-0.1, 0, 0.84), (0.8, 0.5, 0.12), PAINT, 0.05)
    k.cyl((-0.25, 0.0, 0.92), 0.05, 0.05, BLACK, n=8)
    k.cyl((0.4, -0.3, 0.45), 0.1, 0.35, METAL, n=8, axis="X")
    k.box((0.57, 0.1, 0.38), (0.02, 0.3, 0.24), BLACK, 0.006)
    for z in (0.32, 0.44):
        k.box((0.585, 0.05, z), (0.01, 0.05, 0.05), WHITE, 0.0)
    k.box((0.585, 0.2, 0.44), (0.01, 0.04, 0.04), RED, 0.0)
    return k


def breaker_box():
    """Wall utility panel, back on the origin plane (+Y front), with a big main lever on the right."""
    k = Kit()
    grey = fixed(0.55, 0.58, 0.58)
    box(k, -0.3, 0.3, 0.0, 0.22, 0.0, 0.9, grey, 0.015)
    box(k, -0.28, 0.28, 0.22, 0.24, 0.02, 0.88, shade(grey, 1.08), 0.008)
    for i in range(6):
        k.box((-0.1, 0.245, 0.62 + i * 0.03), (0.2, 0.008, 0.012), METAL_D, 0.0)
    k.prism([(0.08, 0.66), (0.2, 0.66), (0.14, 0.76)], 0.24, 0.25, YELLOW, "Y", 0.0)
    k.box((-0.2, 0.25, 0.45), (0.03, 0.03, 0.1), METAL_D, 0.005)
    k.cyl((0.33, 0.12, 0.5), 0.05, 0.08, METAL_D, n=8, axis="X")
    k.beam((0.36, 0.12, 0.5), (0.36, 0.12 + 0.18, 0.5 + 0.22), 0.04, 0.04, METAL_D, 0.005)
    k.box((0.36, 0.33, 0.76), (0.05, 0.06, 0.12), RED, 0.012, R("X", -40))
    for x in (-0.15, 0.15):
        k.cyl((x, 0.08, 1.2), 0.03, 0.6, METAL, n=6, bevel=0.0)
    k.box((0, 0.25, 0.25), (0.32, 0.004, 0.06), WHITE, 0.0)
    return k


def air_handler():
    k = Kit()
    body = fixed(0.62, 0.66, 0.68)
    k.box((0, 0, 1.0), (3.0, 1.6, 1.85), body, 0.03)
    box(k, -1.55, 1.55, -0.85, 0.85, 0.0, 0.1, METAL_D, 0.01)
    for x in (-0.75, 0.25):
        box(k, x - 0.45, x + 0.45, 0.8, 0.82, 0.2, 1.75, shade(body, 1.06), 0.01)
        for z in (0.5, 1.45):
            k.box((x + 0.35, 0.84, z), (0.05, 0.03, 0.12), METAL_D, 0.005)
    for x in (-1.25, -0.25, 0.75):
        k.box((x, 0.81, 1.0), (0.02, 0.02, 1.8), shade(body, 0.85), 0.0)
    k.cyl((1.05, 0.81, 1.0), 0.48, 0.03, METAL_D, n=16, axis="Y")
    k.torus((1.05, 0.84, 1.0), 0.47, 0.025, body, seg=16, n=4, axis="Y")
    k.torus((1.05, 0.84, 1.0), 0.3, 0.015, body, seg=16, n=4, axis="Y")
    k.box((1.05, 0.845, 1.0), (0.94, 0.02, 0.03), body, 0.0)
    k.box((1.05, 0.845, 1.0), (0.03, 0.02, 0.94), body, 0.0)
    k.box((-0.6, -0.1, 2.2), (1.0, 0.8, 0.5), shade(body, 0.95), 0.02)
    k.box((-0.6, -0.1, 1.97), (1.08, 0.88, 0.06), METAL_D, 0.01)
    k.box((-0.6, -0.95, 2.55), (1.0, 0.9 + 0.8, 0.6), shade(body, 0.95), 0.02)
    for y in (-0.5, -1.2):
        k.box((-0.6, y, 2.55), (1.08, 0.06, 0.68), METAL_D, 0.01)
    for i, y in enumerate((0.35, 0.55)):
        k.tube([(1.55, y, 0.5), (1.7, y, 0.5), (1.7, y, 2.2), (1.7, y - 0.4, 2.4)], 0.05, fixed(0.8, 0.5, 0.3) if i else fixed(0.15, 0.15, 0.16), n=6)
    k.box((-1.2, 0.83, 1.8), (0.3, 0.01, 0.12), WHITE, 0.0)
    return k


def vent_grill():
    k = Kit()
    grey = fixed(0.75, 0.76, 0.76)
    box(k, -0.3, 0.3, 0.0, 0.04, 0.0, 0.4, grey, 0.008)
    box(k, -0.25, 0.25, 0.0, 0.041, 0.05, 0.35, fixed(0.1, 0.1, 0.11), 0.0)
    for i in range(6):
        k.box((0, 0.03, 0.08 + i * 0.048), (0.5, 0.008, 0.045), grey, 0.0, R("X", 35))
    for x in (-0.27, 0.27):
        for z in (0.025, 0.375):
            k.cyl((x, 0.042, z), 0.008, 0.006, METAL_D, n=6, axis="Y", bevel=0.0)
    return k


def sprinkler():
    """Ceiling sprinkler head, origin at the ceiling (hangs down)."""
    k = Kit()
    k.cyl((0, 0, -0.008), 0.03, 0.016, CHROME, n=10)
    k.cyl((0, 0, -0.03), 0.012, 0.03, BRASS, n=6, bevel=0.0)
    k.cyl((0, 0, -0.05), 0.015, 0.012, BRASS, n=6, bevel=0.0)
    for s in (-1, 1):
        k.beam((s * 0.012, 0, -0.05), (s * 0.006, 0, -0.085), 0.004, 0.006, BRASS, 0.0)
    k.cyl((0, 0, -0.068), 0.0045, 0.025, RED, n=6, bevel=0.0)
    k.cyl((0, 0, -0.088), 0.022, 0.003, BRASS, n=10, bevel=0.0)
    return k


def power_pole():
    k = Kit()
    k.cyl((0, 0, 4.5), 0.15, 9.0, fixed(0.4, 0.3, 0.2), n=10, r2=0.11, bevel=0.0)
    for z, w in ((8.55, 2.2), (7.7, 1.6)):
        k.box((0, 0, z), (w, 0.11, 0.11), WOOD, 0.01)
        k.beam((-w * 0.3, 0, z - 0.05), (0, 0, z - 0.5), 0.04, 0.04, METAL_D, 0.0)
        k.beam((w * 0.3, 0, z - 0.05), (0, 0, z - 0.5), 0.04, 0.04, METAL_D, 0.0)
        xs = (-w / 2 + 0.12, 0.0 if w > 2 else None, w / 2 - 0.12)
        for x in xs:
            if x is None:
                continue
            xo = x if x != 0.0 else 0.35
            k.lathe((xo, 0, z + 0.055), [(0.04, 0.0), (0.06, 0.04), (0.04, 0.07), (0.06, 0.1), (0.035, 0.14), (0.0, 0.15)], fixed(0.55, 0.75, 0.6), n=8)
            for sy in (-1, 1):
                k.tube([(xo, 0, z + 0.18), (xo, sy * 0.8, z + 0.05), (xo, sy * 1.6, z + 0.1)], 0.012, BLACK, n=4)
    k.cyl((0.32, 0, 6.6), 0.24, 0.8, fixed(0.55, 0.58, 0.58), n=12)
    k.cyl((0.32, 0, 7.04), 0.26, 0.06, fixed(0.45, 0.48, 0.48), n=12)
    k.box((0.13, 0, 6.6), (0.1, 0.2, 0.5), METAL_D, 0.01)
    k.tube([(0.32, 0, 7.07), (0.32, 0, 7.4), (0.0, 0, 7.6)], 0.012, BLACK, n=4)
    for i in range(10):
        z = 2.2 + i * 0.4
        a = 0 if i % 2 else 180
        x, y = 0.18 * math.cos(math.radians(a + 90)), 0.18 * math.sin(math.radians(a + 90))
        k.cyl((x * 0.7, y * 0.7, z), 0.015, 0.22, METAL, n=4, axis="Y", bevel=0.0)
    return k


# ================================================================ waterfront & military
def boat():
    """Small motor boat, bow on +Y."""
    k = Kit()
    pts = []
    for y, hw, keel, sheer in ((-2.2, 0.82, 0.12, 0.8), (-1.2, 0.95, 0.0, 0.8), (0.4, 0.95, 0.02, 0.84), (1.4, 0.7, 0.18, 0.92), (2.2, 0.08, 0.5, 1.0)):
        pts += [(0, y, keel), (hw * 0.6, y, keel + 0.06), (-hw * 0.6, y, keel + 0.06), (hw, y, keel + 0.32), (-hw, y, keel + 0.32),
                (hw * 1.02, y, sheer), (-hw * 1.02, y, sheer)]
    hull = kc.bm_hull(pts)
    cut = kc.bm_hull([(sx * 0.78, y, z) for sx in (-1, 1) for y in (-2.05, 0.6) for z in (0.32, 2.0)] + [(sx * 0.35, 1.3, z) for sx in (-1, 1) for z in (0.6, 2.0)])
    shell = kc.bm_boolean(hull, [cut])

    def hull_colour(face):
        if face.normal.z < -0.2 and face.calc_center_median().z < 0.35:
            return fixed(0.75, 0.2, 0.15)
        return PAINT

    k.add(shell, hull_colour, bevel=0.012)
    for side in (-1, 1):
        rail = [(side * 0.98, -2.2, 0.82), (side * 1.0, -1.2, 0.82), (side * 0.99, 0.4, 0.86), (side * 0.73, 1.4, 0.94), (side * 0.1, 2.2, 1.02)]
        k.tube(rail, 0.035, WOOD_D, n=5)
    k.box((0, -0.1, 0.38), (1.5, 2.6, 0.04), WOOD_L, 0.005)
    for y in (-1.55, -0.2):
        k.box((0, y, 0.55), (1.45, 0.45, 0.12), WHITE, 0.03)
        k.box((0, y, 0.45), (1.3, 0.38, 0.12), shade(WHITE, 0.9), 0.01)
    k.box((0.4, 0.35, 0.65), (0.5, 0.4, 0.55), WHITE, 0.04)
    k.cyl((0.4, 0.4, 0.98), 0.1, 0.03, BLACK, n=10, axis="Y", m=R("X", 35))
    k.hull([(sx, 0.55, 0.93) for sx in (0.1, 0.7)] + [(sx, 0.48, 1.2) for sx in (0.12, 0.68)] + [(sx, 0.57, 0.93) for sx in (0.1, 0.7)] + [(sx, 0.5, 1.2) for sx in (0.12, 0.68)], GLASS)
    k.box((0, -2.36, 0.85), (0.32, 0.36, 0.42), fixed(0.15, 0.16, 0.18), 0.06)
    k.box((0, -2.33, 0.6), (0.14, 0.14, 0.3), METAL_D, 0.02)
    k.cyl((0, -2.33, 0.25), 0.06, 0.45, METAL_D, n=8)
    k.cyl((0, -2.3, 0.08), 0.12, 0.04, METAL, n=3, axis="Y")
    for x in (-0.6, 0.6):
        k.box((x, 1.6, 0.97), (0.14, 0.05, 0.04), CHROME, 0.008)
    return k


def pier_segment():
    """3 m pier segment (walk along X), deck top at 1.5 m, piles down to the origin."""
    k = Kit()
    deck = 1.5
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((sx * 1.35, sy * 1.1, (deck + 0.3) / 2), 0.13, deck + 0.3, fixed(0.42, 0.32, 0.22), n=8, bevel=0.03)
            k.cyl((sx * 1.35, sy * 1.1, 0.5), 0.135, 0.15, fixed(0.25, 0.32, 0.25), n=8, bevel=0.0)
        k.beam((sx * 1.35, -1.1, 0.4), (sx * 1.35, 1.1, deck - 0.3), 0.08, 0.06, WOOD_D, 0.01)
    for sy in (-1, 1):
        k.box((0, sy * 1.0, deck - 0.2), (3.0, 0.12, 0.22), WOOD_D, 0.015)
        k.box((0, sy * 1.18, deck - 0.14), (3.0, 0.06, 0.2), WOOD, 0.01)
    k.box((0, 0, deck - 0.2), (3.0, 0.12, 0.22), WOOD_D, 0.015)
    n = 13
    rng = rng_for("pier")
    for i in range(n):
        x = -1.5 + (i + 0.5) * 3.0 / n
        k.box((x, 0, deck - 0.03), (3.0 / n - 0.025, 2.4, 0.06), shade(WOOD_L, rng.uniform(0.85, 1.05)), 0.008)
    k.box((1.0, 1.05, deck + 0.04), (0.3, 0.07, 0.05), IRON, 0.01)
    for x in (0.9, 1.1):
        k.box((x, 1.05, deck + 0.02), (0.05, 0.07, 0.04), IRON, 0.0)
    return k


def haystack():
    k = Kit()
    rng = rng_for("hay")
    k.blob((0, 0, 0.7), 1.0, STRAW, rng, sub=2, jitter=0.07, scale=(1.15, 1.05, 0.85))
    k.blob((0.1, -0.05, 1.3), 1.0, shade(STRAW, 1.05), rng, sub=1, jitter=0.1, scale=(0.75, 0.7, 0.55))
    for i in range(36):
        a = rng.uniform(0, 2 * math.pi)
        e = rng.uniform(0.1, 1.2)
        r = 1.0 - e * 0.35
        base = (math.cos(a) * r * 1.05, math.sin(a) * r * 0.95, 0.15 + e)
        tip = (base[0] * 1.25, base[1] * 1.25, base[2] + rng.uniform(0.05, 0.2))
        k.beam(base, tip, 0.02, 0.02, shade(STRAW, rng.uniform(0.85, 1.15)), 0.0)
    return k


def sandbags():
    k = Kit()
    rng = rng_for("sandbags")
    burlap = fixed(0.7, 0.61, 0.44)
    for row, count in enumerate((4, 4, 3)):
        width = 0.5
        for i in range(count):
            x = -((count - 1) * width) / 2 + i * width + rng.uniform(-0.02, 0.02)
            for yrow in ((-0.15, 0.15) if row < 2 else (0.0,)):
                k.blob((x, yrow, 0.09 + row * 0.17), 1.0, shade(burlap, rng.uniform(0.85, 1.08)), rng, sub=1, jitter=0.04,
                       scale=(0.27, 0.16, 0.1), m=R("Z", rng.uniform(-6, 6)))
    return k


def watchtower():
    k = Kit()
    rng = rng_for("tower")
    log = fixed(0.45, 0.32, 0.2)
    plank = fixed(0.58, 0.42, 0.26)
    top = 7.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.tube([(sx * 1.75, sy * 1.75, 0.0), (sx * 1.45, sy * 1.45, top + 2.2)], [0.13, 0.1], log, n=8)
    for z0, z1 in ((0.4, 3.6), (3.6, 6.8)):
        f0, f1 = 1.75 - 0.3 * z0 / (top + 2.2), 1.75 - 0.3 * z1 / (top + 2.2)
        for s in (-1, 1):
            k.beam((s * f0, -f0, z0), (s * f1, f1, z1), 0.08, 0.08, plank, 0.01)
            k.beam((s * f0, f0, z0), (s * f1, -f1, z1), 0.08, 0.08, plank, 0.01)
            k.beam((-f0, s * f0, z0), (f1, s * f1, z1), 0.08, 0.08, plank, 0.01)
            k.beam((f0, s * f0, z0), (-f1, s * f1, z1), 0.08, 0.08, plank, 0.01)
        for s in (-1, 1):
            k.box((0, s * f1, z1), (2 * f1 + 0.1, 0.12, 0.14), plank, 0.012)
            k.box((s * f1, 0, z1), (0.12, 2 * f1 + 0.1, 0.14), plank, 0.012)
    k.box((0, 0, top - 0.08), (3.6, 3.6, 0.16), WOOD_D, 0.02)
    for i in range(12):
        y = -1.8 + (i + 0.5) * 3.6 / 12
        k.box((0, y, top + 0.02), (3.6, 3.6 / 12 - 0.02, 0.05), shade(plank, rng.uniform(0.88, 1.05)), 0.006)
    for s in (-1, 1):
        for j in range(3):
            z = top + 0.2 + j * 0.32
            k.box((s * 1.78, 0, z), (0.05, 3.6, 0.28), shade(plank, 0.95 + 0.05 * j), 0.008)
            k.box((0, -1.78, z), (3.6, 0.05, 0.28), shade(plank, 0.95 + 0.05 * j), 0.008)
            for x0, x1 in ((-1.8, -0.4), (0.4, 1.8)):
                box(k, x0, x1, 1.755, 1.805, z - 0.14, z + 0.14, shade(plank, 0.95 + 0.05 * j), 0.008)
    k.hull([(sx * 2.1, sy * 2.1, top + 2.2) for sx in (-1, 1) for sy in (-1, 1)] + [(0, 0, top + 3.2)], tinted(0.88, 0.85, 0.78), 0.02)
    k.box((0, 0, top + 2.17), (4.25, 4.25, 0.06), WOOD_D, 0.01)
    for x in (-0.28, 0.28):
        k.beam((x, 2.25, 0.0), (x, 1.82, top + 1.0), 0.06, 0.08, METAL_D, 0.008)
    n = int((top + 0.9) / 0.35)
    for i in range(1, n):
        z = i * 0.35
        y = 2.25 - (2.25 - 1.82) * z / (top + 1.0)
        k.cyl((0, y, z), 0.022, 0.56, METAL, n=6, axis="X", bevel=0.0)
    k.cyl((1.5, -1.5, top + 1.25), 0.15, 0.3, METAL_D, n=10, axis="Y")
    k.cyl((1.5, -1.33, top + 1.25), 0.12, 0.02, GLASS, n=10, axis="Y", bevel=0.0)
    k.beam((1.5, -1.5, top + 1.1), (1.5, -1.5, top + 0.8), 0.05, 0.05, METAL_D, 0.0)
    for x in (-1.0, -0.5):
        k.blob((x, -1.5, top + 0.15), 1.0, fixed(0.7, 0.61, 0.44), rng, sub=1, jitter=0.04, scale=(0.27, 0.16, 0.1))
    return k


def tent_military():
    k = Kit()
    canvas = tinted(0.9, 0.9, 0.86)
    prof = [(-1.8, 0.0), (1.8, 0.0), (1.8, 0.9), (0.0, 2.4), (-1.8, 0.9)]
    inner = [(-1.74, -0.1), (1.74, -0.1), (1.74, 0.88), (0.0, 2.32), (-1.74, 0.88)]
    outer = kc.bm_prism(prof, -2.3, 2.3, "Y")
    cut = kc.bm_prism(inner, -2.24, 2.24, "Y")
    door = kc.bm_prism([(-0.65, -0.1), (0.65, -0.1), (0.65, 1.6), (0.0, 2.05), (-0.65, 1.6)], 2.0, 2.6, "Y")
    k.add(kc.bm_boolean(outer, [cut, door]), canvas, bevel=0.01)
    for s in (-1, 1):
        k.cyl((s * 0.78, 2.33, 1.0), 0.09, 1.9, shade(canvas, 0.9), n=8)
    for y in (-2.32, 2.32):
        k.cyl((0, y, 1.25), 0.04, 2.5, WOOD, n=6, bevel=0.0)
    k.cyl((0, 0, 2.42), 0.04, 4.9, WOOD, n=6, axis="Y", bevel=0.0)
    for y in (-1.8, 0.0, 1.8):
        for s in (-1, 1):
            k.beam((s * 1.8, y, 0.9), (s * 2.7, y, 0.0), 0.02, 0.02, fixed(0.75, 0.7, 0.55), 0.0)
            k.box((s * 2.7, y, 0.08), (0.04, 0.04, 0.2), WOOD_D, 0.0)
    k.box((0, 0, 0.01), (3.5, 4.5, 0.02), shade(canvas, 0.75), 0.0)
    return k


# ================================================================ vehicles
def wheel(k, x, y, r, w, side):
    k.cyl((x, y, r), r, w, RUBBER, n=14, axis="X", bevel=0.035)
    k.cyl((x + side * w * 0.42, y, r), r * 0.58, 0.04, CHROME, n=10, axis="X", bevel=0.012)
    k.cyl((x + side * w * 0.5, y, r), r * 0.2, 0.04, METAL_D, n=8, axis="X", bevel=0.0)


def octo(hw, y0, y1, z, c):
    """Plan octagon (chamfer c) of a car body section at height z."""
    return [(-hw + c, y0, z), (hw - c, y0, z), (hw, y0 + c, z), (hw, y1 - c, z), (hw - c, y1, z), (-hw + c, y1, z), (-hw, y1 - c, z), (-hw, y0 + c, z)]


def car_body(k, length, width, z0, z1, col, chamfer=0.3):
    hw, hl = width / 2, length / 2
    pts = octo(hw - 0.06, -hl + 0.05, hl - 0.05, z0, chamfer) + octo(hw, -hl, hl, z0 + (z1 - z0) * 0.45, chamfer) + octo(hw - 0.05, -hl + 0.08, hl - 0.1, z1, chamfer * 1.1)
    k.hull(pts, col, 0.03, angle=25)


def cabin(k, width, yb0, yb1, yt0, yt1, zb, zt, col, glass=GLASS):
    """Glass greenhouse + painted roof + pillars."""
    hw = width / 2
    k.hull([(sx * hw, y, zb) for sx in (-1, 1) for y in (yb0, yb1)] + [(sx * (hw - 0.12), y, zt) for sx in (-1, 1) for y in (yt0, yt1)], glass)
    k.hull([(sx * (hw - 0.1), y, zt - 0.02) for sx in (-1, 1) for y in (yt0 - 0.03, yt1 + 0.03)] + [(sx * (hw - 0.16), y, zt + 0.07) for sx in (-1, 1) for y in (yt0 + 0.02, yt1 - 0.02)], col, 0.02)
    for sx in (-1, 1):
        for (yb, yt) in ((yb0, yt0), (yb1, yt1)):
            k.beam((sx * (hw + 0.005), yb, zb), (sx * (hw - 0.115), yt, zt), 0.07, 0.09, col, 0.01)
        ym = (yb0 + yb1) / 2
        k.beam((sx * (hw + 0.005), ym, zb), (sx * (hw - 0.115), (yt0 + yt1) / 2, zt), 0.06, 0.1, col, 0.008)


def car_details(k, length, width, zl, front_y=None, rear_y=None, police=False):
    hl, hw = length / 2, width / 2
    fy = front_y if front_y is not None else hl
    ry = rear_y if rear_y is not None else -hl
    for sx in (-1, 1):
        k.cyl((sx * (hw - 0.28), fy - 0.02, zl), 0.11, 0.06, LIGHT_F, n=10, axis="Y")
        k.box((sx * (hw - 0.25), ry + 0.02, zl + 0.02), (0.3, 0.06, 0.12), LIGHT_R, 0.012)
    k.box((0, fy + 0.04, zl - 0.25), (width + 0.04, 0.16, 0.16), METAL_D, 0.04)
    k.box((0, ry - 0.04, zl - 0.25), (width + 0.04, 0.16, 0.16), METAL_D, 0.04)
    k.box((0, fy + 0.01, zl), (width * 0.38, 0.05, 0.16), METAL_D, 0.015)
    for i in range(4):
        k.box((0, fy + 0.035, zl - 0.05 + i * 0.035), (width * 0.34, 0.01, 0.012), CHROME, 0.0)
    k.box((0, ry - 0.06, zl - 0.06), (0.36, 0.02, 0.12), WHITE, 0.004)


def sedan_kit(body=PAINT, police=False):
    k = Kit()
    L, Wd, r = 4.1, 1.85, 0.38
    for sx in (-1, 1):
        for y in (-1.3, 1.3):
            wheel(k, sx * (Wd / 2 - 0.14), y, r, 0.3, sx)
    car_body(k, L, Wd, 0.28, 0.98, body)
    cabin(k, Wd - 0.08, -1.05, 0.65, -0.82, 0.25, 0.98, 1.5, body)
    car_details(k, L, Wd, 0.72)
    for sx in (-1, 1):
        k.box((sx * (Wd / 2 + 0.04), 0.6, 1.05), (0.08, 0.12, 0.08), body, 0.02)
        for y in (-0.35, 0.45):
            k.box((sx * (Wd / 2 + 0.005), y, 0.86), (0.02, 0.14, 0.03), CHROME, 0.0)
        k.box((sx * (Wd / 2 + 0.002), 0.08, 0.68), (0.012, 0.02, 0.55), shade(body, 0.6) if not police else BLACK, 0.0)
    return k


def sedan():
    return sedan_kit()


def police_car():
    k = sedan_kit(PAINT, police=True)
    Wd = 1.85
    for sx in (-1, 1):
        box(k, sx * (Wd / 2 - 0.02) - (0.0 if sx > 0 else 0.025), sx * (Wd / 2 - 0.02) + (0.025 if sx > 0 else 0.0), -0.75, 0.7, 0.45, 0.93, WHITE, 0.01)
    k.box((0, -0.28, 1.6), (1.1, 0.26, 0.05), METAL_D, 0.01)
    k.box((-0.3, -0.28, 1.68), (0.48, 0.24, 0.12), fixed(0.95, 0.12, 0.1), 0.03)
    k.box((0.3, -0.28, 1.68), (0.48, 0.24, 0.12), fixed(0.15, 0.35, 0.95), 0.03)
    for sx in (-1, 1):
        k.box((sx * 0.45, 2.16, 0.6), (0.06, 0.12, 0.5), METAL_D, 0.01)
    k.box((0, 2.2, 0.55), (1.0, 0.06, 0.06), METAL_D, 0.01)
    k.box((0, 2.2, 0.78), (1.0, 0.06, 0.06), METAL_D, 0.01)
    k.cyl((0.6, -1.6, 1.3), 0.008, 0.7, BLACK, n=4, bevel=0.0)
    return k


def pickup():
    k = Kit()
    L, Wd, r = 4.9, 1.95, 0.42
    for sx in (-1, 1):
        for y in (-1.6, 1.5):
            wheel(k, sx * (Wd / 2 - 0.14), y, r, 0.32, sx)
    car_body(k, L, Wd, 0.34, 1.08, PAINT, 0.25)
    cabin(k, Wd - 0.08, -0.4, 0.95, -0.35, 0.55, 1.08, 1.75, PAINT)
    box(k, -Wd / 2 + 0.08, Wd / 2 - 0.08, -2.3, -0.5, 0.98, 1.1, fixed(0.2, 0.2, 0.22), 0.0)
    for sx in (-1, 1):
        box(k, sx * (Wd / 2 - 0.04) - 0.04, sx * (Wd / 2 - 0.04) + 0.04, -2.4, -0.45, 1.06, 1.42, PAINT, 0.02)
    box(k, -Wd / 2, Wd / 2, -2.45, -2.36, 1.0, 1.42, PAINT, 0.02)
    box(k, -Wd / 2, Wd / 2, -0.5, -0.4, 1.06, 1.44, PAINT, 0.02)
    car_details(k, L, Wd, 0.82)
    for sx in (-1, 1):
        k.box((sx * (Wd / 2 + 0.04), 0.9, 1.15), (0.08, 0.12, 0.1), METAL_D, 0.02)
    return k


def van():
    k = Kit()
    L, Wd, r = 4.7, 1.95, 0.38
    for sx in (-1, 1):
        for y in (-1.5, 1.45):
            wheel(k, sx * (Wd / 2 - 0.14), y, r, 0.3, sx)
    car_body(k, L, Wd, 0.3, 1.05, PAINT, 0.25)
    k.hull([(sx * (Wd / 2 - 0.02), y, z) for sx in (-1, 1) for y, z in ((-2.3, 1.0), (-2.3, 2.15), (0.9, 2.15), (1.15, 1.0))], PAINT, 0.04, angle=25)
    k.hull([(sx * (Wd / 2 - 0.1), y, z) for sx in (-1, 1) for y, z in ((1.13, 1.1), (0.86, 1.95))] + [(sx * (Wd / 2 - 0.1), y, z) for sx in (-1, 1) for y, z in ((1.1, 1.1), (0.83, 1.95))], GLASS)
    for sx in (-1, 1):
        box(k, sx * (Wd / 2 - 0.01) - 0.01, sx * (Wd / 2 - 0.01) + 0.01, 0.15, 0.75, 1.3, 1.85, GLASS, 0.0)
        box(k, sx * (Wd / 2 - 0.01) - 0.01, sx * (Wd / 2 - 0.01) + 0.01, -1.9, -0.3, 1.4, 1.85, GLASS, 0.0)
        k.box((sx * (Wd / 2 + 0.005), 0.05, 1.05), (0.012, 0.02, 1.5), shade(PAINT, 0.6), 0.0)
        k.box((sx * (Wd / 2 + 0.06), 1.0, 1.55), (0.08, 0.1, 0.2), METAL_D, 0.02)
    k.box((0, -2.34, 1.6), (1.3, 0.02, 0.4), GLASS, 0.0)
    car_details(k, L, Wd, 0.75)
    return k


def jeep():
    k = Kit()
    L, Wd, r = 3.7, 1.75, 0.42
    for sx in (-1, 1):
        for y in (-1.15, 1.2):
            wheel(k, sx * (Wd / 2 - 0.08), y, r, 0.34, sx)
    shell = kc.bm_hull([(sx * (Wd / 2 - 0.12), y, z) for sx in (-1, 1) for y in (-1.75, 1.8) for z in (0.42, 1.15)])
    cut = kc.bm_box(Wd - 0.4, 2.1, 1.0)
    cut.transform(T(0, -0.55, 1.45))
    k.add(kc.bm_boolean(shell, [cut]), PAINT, bevel=0.03)
    k.hull([(sx * (Wd / 2 - 0.16), y, z) for sx in (-1, 1) for y, z in ((0.55, 1.15), (1.8, 1.15), (1.8, 1.02))] + [(sx * (Wd / 2 - 0.2), 0.6, 1.2) for sx in (-1, 1)], shade(PAINT, 0.95), 0.02)
    for sx in (-1, 1):
        for y in (-1.15, 1.2):
            k.box((sx * (Wd / 2 - 0.02), y, 0.95), (0.42, 0.95, 0.06), shade(PAINT, 0.9), 0.02)
    for i in range(7):
        k.box((-0.3 + i * 0.1, 1.81, 0.9), (0.04, 0.02, 0.32), BLACK, 0.0)
    for sx in (-1, 1):
        k.cyl((sx * 0.55, 1.82, 0.95), 0.1, 0.05, LIGHT_F, n=10, axis="Y")
        k.beam((sx * (Wd / 2 - 0.18), 0.5, 1.15), (sx * (Wd / 2 - 0.2), 0.42, 1.7), 0.05, 0.05, METAL_D, 0.0)
    k.box((0, 0.46, 1.72), (Wd - 0.38, 0.05, 0.05), METAL_D, 0.0)
    k.box((0, 0.48, 1.45), (Wd - 0.45, 0.02, 0.48), GLASS, 0.0, R("X", -8))
    k.tube([(-(Wd / 2 - 0.2), -0.85, 1.15), (-(Wd / 2 - 0.25), -0.85, 1.9), ((Wd / 2 - 0.25), -0.85, 1.9), ((Wd / 2 - 0.2), -0.85, 1.15)], 0.04, METAL_D, n=6)
    for x in (-0.38, 0.38):
        k.box((x, -0.1, 0.95), (0.5, 0.5, 0.14), fixed(0.25, 0.22, 0.18), 0.04)
        k.box((x, -0.38, 1.25), (0.5, 0.12, 0.5), fixed(0.25, 0.22, 0.18), 0.04)
    k.box((0, -1.2, 0.95), (1.2, 0.45, 0.14), fixed(0.25, 0.22, 0.18), 0.04)
    k.cyl((0, -1.9, 1.0), r * 0.95, 0.28, RUBBER, n=14, axis="Y", bevel=0.03)
    k.cyl((0, -2.0, 1.0), r * 0.5, 0.06, METAL_D, n=10, axis="Y")
    k.box((0, 1.85, 0.5), (Wd - 0.1, 0.12, 0.12), METAL_D, 0.02)
    k.box((0, -1.8, 0.5), (Wd - 0.1, 0.12, 0.12), METAL_D, 0.02)
    for sx in (-1, 1):
        k.box((sx * 0.6, -1.76, 0.9), (0.18, 0.04, 0.1), LIGHT_R, 0.01)
    return k


# ================================================================ piece list
def pieces():
    P = Piece
    return [
        P("Bench", "furniture", bench, "Park bench 1.8 m, cast iron + slats."),
        P("Table", "furniture", table, "Rectangular table 1.6x0.9, turned legs."),
        P("TableRound", "furniture", table_round, "Round pedestal table d=1.2."),
        P("Chair", "furniture", chair, "Wooden chair, cushion tintable.", (0.7, 0.2, 0.2)),
        P("Stool", "furniture", stool, "Bar stool, seat tintable.", (0.65, 0.2, 0.18)),
        P("BarCounter", "furniture", bar_counter, "3 m bar counter: customer side +Z, glasses/bottles on the back shelf. Body tintable.", (0.45, 0.25, 0.18)),
        P("BarShelf", "furniture", bar_shelf, "2.4 m back-bar shelf with mirror and ~27 bottles."),
        P("Sofa", "furniture", sofa, "3-seat sofa 2.1 m, fabric tintable.", (0.35, 0.45, 0.6)),
        P("Armchair", "furniture", armchair, "Armchair, fabric tintable.", (0.6, 0.3, 0.25)),
        P("Bed", "furniture", bed, "Double bed 1.6x2.1, headboard at -Z, duvet tintable.", (0.4, 0.55, 0.7)),
        P("Desk", "furniture", desk, "Desk 1.4x0.7 with drawer pedestal, papers, cup."),
        P("OfficeChair", "furniture", office_chair, "Swivel office chair, upholstery tintable.", (0.2, 0.22, 0.25)),
        P("Bookshelf", "furniture", bookshelf, "Bookshelf 1.0x0.35x2.0 full of books."),
        P("Cabinet", "furniture", cabinet, "Sideboard 1.2x0.45x0.9, painted body tintable.", (0.55, 0.65, 0.6)),
        P("Wardrobe", "hiding", wardrobe, "Hiding closet 1.2 wide x 2.2 high x 0.8 deep, double doors on +Z (closed, solid mesh)."),
        P("Rug", "furniture", rug, "Rug 2.4x1.6, field tintable, border fixed.", (0.7, 0.25, 0.2)),
        P("PlantPot", "furniture", plant_pot, "Small potted plant, leaves tintable.", (0.35, 0.6, 0.28)),
        P("PlantBig", "furniture", plant_big, "Big indoor tree in a planter 1.9 m, foliage tintable.", (0.3, 0.55, 0.25)),
        P("Piano", "furniture", piano, "Grand piano, keyboard on +Z, lid propped open."),
        P("Fireplace", "furniture", fireplace, "Marble fireplace 1.9 m wide, back on -Z against the wall, fire inside."),
        P("Chandelier", "light", chandelier, "Crystal chandelier d=2.2 m; ORIGIN AT THE TOP HOOK, hangs down 1.7 m."),
        P("WallLamp", "light", wall_lamp, "Wall sconce: back plate on the origin plane, sticks out to +Z."),
        P("FloorLamp", "light", floor_lamp, "Floor lamp 1.7 m, shade tintable.", (0.95, 0.88, 0.7)),
        P("StreetLamp", "light", street_lamp, "Modern street lamp 4.5 m, arm toward +Z."),
        P("LampPostOrnate", "light", lamp_post_ornate, "Cast-iron ornate lamp post 4.2 m with lantern."),
        P("TrashBin", "street", trash_bin, "Street bin, body tintable.", (0.25, 0.45, 0.3)),
        P("Dumpster", "hiding", dumpster, "Dumpster 2x1.4x1.3 (hiding spot), body tintable.", (0.2, 0.45, 0.3)),
        P("Hydrant", "street", hydrant, "Fire hydrant, tintable.", (0.85, 0.15, 0.12)),
        P("Mailbox", "street", mailbox, "Curbside mailbox on a post, box tintable, red flag.", (0.25, 0.27, 0.3)),
        P("StreetSign", "street", street_sign, "Pole with street blades, stop sign, parking sign."),
        P("TrafficCone", "street", traffic_cone, "Traffic cone."),
        P("Barrier", "cover", barrier, "Concrete jersey barrier 2 m (cover)."),
        P("Bollard", "street", bollard, "Steel bollard."),
        P("Crate", "cover", crate, "Wooden crate 1 m (cover)."),
        P("CrateLarge", "cover", crate_large, "Wooden crate 1.6 m (cover)."),
        P("Pallet", "prop", pallet, "Euro pallet 1.2x1.0."),
        P("Barrel", "cover", barrel, "Oil drum, tintable.", (0.2, 0.4, 0.65)),
        P("GasBottle", "prop", gas_bottle, "Gas cylinder 1.25 m, tintable.", (0.85, 0.2, 0.15)),
        P("ShippingContainer", "cover", shipping_container, "Corrugated container 6.1 x 2.6 x 2.5, doors on +X end, tintable.", (0.75, 0.3, 0.2)),
        P("MarketStall", "street", market_stall, "Market stall with fruit crates and striped fabric roof (tintable stripes); counter on +Z.", (0.85, 0.3, 0.25)),
        P("Parasol", "street", parasol, "Cafe parasol 2.6 m wide, tintable stripes.", (0.2, 0.5, 0.75)),
        P("UmbrellaTable", "street", umbrella_table, "Round cafe table with parasol, tintable stripes.", (0.85, 0.55, 0.2)),
        P("Fountain", "landmark", fountain, "Two-tier stone fountain d=4.1 m with water."),
        P("Statue", "landmark", statue, "Original abstract bronze figure holding a ring, on a stone plinth, 4.3 m."),
        P("Toilet", "furniture", toilet, "Toilet, tank at -Z."),
        P("Sink", "furniture", sink, "Pedestal sink, taps at -Z."),
        P("Fridge", "furniture", fridge, "Retro fridge, tintable.", (0.75, 0.88, 0.85)),
        P("Stove", "furniture", stove, "Kitchen range with oven, knobs on +Z."),
        P("TV", "furniture", tv, "Flat TV 1.2 m on a stand."),
        P("Radio", "prop", radio, "Retro tabletop radio."),
        P("Phone", "prop", phone, "Desk phone, tintable.", (0.8, 0.15, 0.15)),
        P("Generator", "utility", generator, "Portable generator in a tube frame, tank tintable.", (0.95, 0.75, 0.15)),
        P("BreakerBox", "utility", breaker_box, "Utility breaker panel with big main lever on +X side; back on the origin plane (mount on a wall)."),
        P("AirHandler", "utility", air_handler, "Basement HVAC unit 3x1.6x2 with ducts rising and bending to -Z."),
        P("VentGrill", "utility", vent_grill, "Wall vent grille 0.6x0.4, back on the origin plane."),
        P("Sprinkler", "utility", sprinkler, "Ceiling sprinkler head; ORIGIN AT THE CEILING, hangs down 9 cm."),
        P("PowerPole", "utility", power_pole, "Wooden power pole 9 m, crossarms along x, wire stubs along z, transformer."),
        P("Boat", "vehicle", boat, "Small motor boat 4.7 m, bow +Z, hollow cockpit, hull tintable.", (0.95, 0.95, 0.95)),
        P("PierSegment", "structure", pier_segment, "Pier segment 3 m along x, 2.4 wide; deck top at 1.5 m, piles to the origin."),
        P("Haystack", "hiding", haystack, "Haystack mound (hiding spot) 2.3 m wide."),
        P("Sandbags", "cover", sandbags, "Sandbag wall 2 m x 0.55 m (cover)."),
        P("Watchtower", "structure", watchtower, "Wooden military watchtower 3.6x3.6, platform at 7 m, ladder on +Z, roof tintable.", (0.45, 0.5, 0.35)),
        P("TentMilitary", "structure", tent_military, "Hollow ridge tent 3.6x4.6, door on +Z, canvas tintable.", (0.42, 0.47, 0.3)),
        P("Jeep", "vehicle", jeep, "Open off-road jeep, front +Z, body tintable.", (0.38, 0.45, 0.28)),
        P("Sedan", "vehicle", sedan, "Chunky toy sedan, front +Z, body tintable.", (0.8, 0.25, 0.22)),
        P("Pickup", "vehicle", pickup, "Pickup truck with bed, front +Z, body tintable.", (0.25, 0.4, 0.65)),
        P("Van", "vehicle", van, "Panel van, front +Z, body tintable.", (0.92, 0.92, 0.9)),
        P("PoliceCar", "vehicle", police_car, "Police sedan with light bar and push bar, body tintable (doors white).", (0.1, 0.12, 0.18)),
    ]


if __name__ == "__main__":
    kc.run_style("Props", pieces(), only=globals().get("KIT_ONLY"), columns=10, spacing=7.0, row=20)
