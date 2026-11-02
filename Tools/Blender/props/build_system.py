# Author : Baptiste Vu Liem
"""Procedural systemic / architectural props: vents and crawl ducts, drainpipes, ladder, alarm panel,
air handler, pallet, sandbags.

    blender -b --factory-startup --python Tools/Blender/props/build_system.py -- [Prop_VentDuct_T ...]

Front faces -Y (Unity +Z), pivot bottom centre unless noted. Ducts: 1.0 x 1.0 m inner section,
3 m long along Y (corner / T legs end 1.5 m from the centre so pieces snap on a 1.5 m grid), open
ends, interior faces in their own material (<Name>_interior).
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pbr_lib as L  # noqa: E402
from pbr_lib import LIB, box, cyl, extrude, lathe, sphere, sweep, boolean, cutter_box, spec, text  # noqa: E402


def S(name, **kw):
    return dict(LIB[name], **kw)


SHEET = spec("metal_plate_02", (0.62, 0.63, 0.64), 1.6, "tint", 0.45, (0.3, 0.55), 1.0, 0.25)
SHEET_IN = spec("metal_plate_02", (0.36, 0.36, 0.35), 1.6, "tint", 0.6, (0.45, 0.7), 1.0, 0.25)

# ------------------------------------------------------------------ ducts
W_IN, T = 1.0, 0.03
H = W_IN / 2 + T  # half outer width (0.53)
LEG = 1.5


def _duct(name, outline, open_edges, ribs=()):
    """outline: CCW polygon (x, y) of the outer footprint; open_edges: indices of edges left open."""
    P = []
    P.append(extrude(outline, T, (0, 0, T / 2), mat="sheet", bv=0, plane="XY"))
    P.append(extrude(outline, T, (0, 0, 2 * H - T / 2), mat="sheet", bv=0, plane="XY"))
    n = len(outline)
    for i in range(n):
        if i in open_edges:
            continue
        a, b = outline[i], outline[(i + 1) % n]
        dx, dy = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dx, dy)
        ux, uy = dx / ln, dy / ln
        nx, ny = uy, -ux  # outward normal for a CCW polygon
        cx, cy = (a[0] + b[0]) / 2 - nx * T / 2, (a[1] + b[1]) / 2 - ny * T / 2
        ang = math.degrees(math.atan2(dy, dx))
        P.append(box((ln, T, 2 * H - 2 * T), (cx, cy, H), (0, 0, ang), mat="sheet", bv=0))
    # flanges around each open end
    for i in open_edges:
        a, b = outline[i], outline[(i + 1) % n]
        dx, dy = b[0] - a[0], b[1] - a[1]
        ang = math.degrees(math.atan2(dy, dx))
        ln = math.hypot(dx, dy)
        nx, ny = dy / ln, -dx / ln
        cx, cy = (a[0] + b[0]) / 2 - nx * 0.012, (a[1] + b[1]) / 2 - ny * 0.012
        for (w, h, oz) in ((ln + 0.06, 0.03, -0.015), (ln + 0.06, 0.03, 2 * H + 0.015)):
            P.append(box((w, 0.024, h), (cx, cy, oz + H - H), (0, 0, ang), mat="flange", bv=0.003))
        for side in (-1, 1):
            sx, sy = (a[0] + b[0]) / 2 + side * (ln / 2 + 0.015) * dx / ln - nx * 0.012, \
                (a[1] + b[1]) / 2 + side * (ln / 2 + 0.015) * dy / ln - ny * 0.012
            P.append(box((0.03, 0.024, 2 * H + 0.06), (sx, sy, H), (0, 0, ang), mat="flange", bv=0.003))
        # flange bolts
        for k in range(5):
            t = (k + 0.5) / 5 - 0.5
            for z in (-0.015, 2 * H + 0.015):
                bx = (a[0] + b[0]) / 2 + t * dx - nx * 0.025
                by = (a[1] + b[1]) / 2 + t * dy - ny * 0.025
                P.append(cyl(0.007, 0.012, (bx, by, z), (90, 0, ang), mat="bolt", n=6, bv=0.001))
    # stiffening ribs (rectangular bands)
    for y in ribs:
        for (sx, sz, w, h) in ((0, -0.006, 2 * H + 0.02, 0.012), (0, 2 * H + 0.006, 2 * H + 0.02, 0.012),
                               (-H - 0.006, H, 0.012, 2 * H + 0.012), (H + 0.006, H, 0.012, 2 * H + 0.012)):
            P.append(box((w, 0.04, h), (sx, y, sz), mat="flange", bv=0.003))
    # seam beads along the outer long edges (lock seams)
    return P


def _inside_fn(inner_test):
    def fn(c, nrm):
        if not (T - 0.002 < c.z < 2 * H - T + 0.002):
            return False
        return inner_test(c.x, c.y)
    return fn


def _post_interior(inner_test):
    def post(ob):
        L.assign_part(ob, _inside_fn(inner_test), "interior")
    return post


def _duct_specs():
    return {"sheet": SHEET, "interior": SHEET_IN, "flange": SHEET,
            "bolt": S("steel", color=(0.5, 0.5, 0.5))}


def build_duct_straight():
    outline = [(-H, -LEG), (H, -LEG), (H, LEG), (-H, LEG)]
    P = _duct("Prop_VentDuct_Straight", outline, open_edges=(0, 2), ribs=(-0.5, 0.5))
    inside = lambda x, y: abs(x) < H - T + 0.002 and abs(y) < LEG + 0.01  # noqa: E731
    return L.finalize("Prop_VentDuct_Straight", P, _duct_specs(), "architecture", mode="tile",
                      post=_post_interior(inside), notes="1.0 m inner section, 3 m long along Z, open ends")


def build_duct_corner():
    outline = [(-H, -LEG), (H, -LEG), (H, -H), (LEG, -H), (LEG, H), (-H, H)]
    P = _duct("Prop_VentDuct_Corner", outline, open_edges=(0, 3))
    inside = lambda x, y: (abs(x) < H - T + 0.002 and -LEG - 0.01 < y < H - T + 0.002) or \
        (abs(y) < H - T + 0.002 and -H + T - 0.002 < x < LEG + 0.01)  # noqa: E731
    return L.finalize("Prop_VentDuct_Corner", P, _duct_specs(), "architecture", mode="tile",
                      post=_post_interior(inside),
                      notes="90 deg elbow, legs to -Z and +X (Unity), each ending 1.5 m from the pivot")


def build_duct_t():
    outline = [(-LEG, -H), (-H, -H), (-H, -LEG), (H, -LEG), (H, -H), (LEG, -H), (LEG, H), (-LEG, H)]
    P = _duct("Prop_VentDuct_T", outline, open_edges=(7, 2, 5))
    inside = lambda x, y: (abs(y) < H - T + 0.002 and abs(x) < LEG + 0.01) or \
        (abs(x) < H - T + 0.002 and -LEG - 0.01 < y < 0)  # noqa: E731
    return L.finalize("Prop_VentDuct_T", P, _duct_specs(), "architecture", mode="tile",
                      post=_post_interior(inside), notes="T junction, legs to -X, +X and -Z (Unity), 1.5 m each")


def build_vent_grille():
    P = []
    S_ = 0.9
    fw, d = 0.05, 0.025
    for (sx, sz, w, h) in ((0, S_ / 2 - fw / 2, S_, fw), (0, -S_ / 2 + fw / 2, S_, fw),
                           (S_ / 2 - fw / 2, 0, fw, S_ - 2 * fw), (-S_ / 2 + fw / 2, 0, fw, S_ - 2 * fw)):
        P.append(box((w, d, h), (sx, -d / 2, sz), mat="frame", bv=0.006, seg=3))
    # inner return lip
    for (sx, sz, w, h) in ((0, S_ / 2 - fw - 0.004, S_ - 2 * fw, 0.008), (0, -S_ / 2 + fw + 0.004, S_ - 2 * fw, 0.008),
                           (S_ / 2 - fw - 0.004, 0, 0.008, S_ - 2 * fw), (-(S_ / 2 - fw - 0.004), 0, 0.008, S_ - 2 * fw)):
        P.append(box((w, 0.06, h), (sx, 0.03 - 0.0, sz), mat="frame", bv=0.002))
    n = 16
    pitch = (S_ - 2 * fw) / n
    for i in range(n):
        z = -S_ / 2 + fw + pitch * (i + 0.5)
        P.append(box((S_ - 2 * fw, 0.045, 0.0025), (0, -0.0, z), (40, 0, 0), mat="frame", bv=0.0008, seg=1))
    for sx in (-0.15, 0.15):  # vertical stiffeners behind the louvres
        P.append(box((0.01, 0.012, S_ - 2 * fw), (sx, 0.022, 0), mat="frame", bv=0.002))
    for sx in (-1, 1):
        for sz in (-1, 1):
            p = (sx * (S_ / 2 - fw / 2), -d - 0.001, sz * (S_ / 2 - fw / 2))
            P.append(lathe([(0.0, 0.0), (0.008, 0.0), (0.0075, 0.002), (0.004, 0.0035), (0.0, 0.004)], 16, p,
                           (90, 0, 0), mat="screw"))
            P.append(box((0.009, 0.002, 0.0015), (p[0], p[1] - 0.004, p[2]), (0, 0, 0), mat="slot", bv=0))
    specs = {"frame": S("paint_white", color=(0.78, 0.78, 0.76), tile=0.5, wear=0.2, scratch=0.1),
             "screw": S("steel"), "slot": spec(None, (0.02, 0.02, 0.02), rough=(0.6, 0.7))}
    return L.finalize("Prop_VentGrille", P, specs, "system", size=1024, pivot=(0, 0.0, 0),
                      notes="0.9 x 0.9 m removable louvred cover; pivot at the centre of its back (wall) plane, "
                            "louvres face -Y (Unity +Z)")


# ------------------------------------------------------------------ drainpipe
R_PIPE = 0.05
WALL_OFF = 0.09  # pipe axis distance from the wall (wall at +Y)


def _bracket(z):
    P = []
    P.append(lathe([(R_PIPE + 0.0005, 0.0), (R_PIPE + 0.006, 0.0), (R_PIPE + 0.006, 0.03), (R_PIPE + 0.0005, 0.03)],
                   24, (0, 0, z), mat="pipe"))
    P.append(box((0.03, WALL_OFF - R_PIPE, 0.024), (0, (R_PIPE + WALL_OFF) / 2 + 0.003, z + 0.015), mat="pipe",
                 bv=0.003))
    P.append(box((0.12, 0.008, 0.04), (0, WALL_OFF - 0.004, z + 0.015), mat="pipe", bv=0.003))
    for x in (-0.045, 0.045):
        P.append(cyl(0.006, 0.006, (x, WALL_OFF - 0.009, z + 0.015), (90, 0, 0), mat="bolt", n=8, bv=0.001))
    return P


def build_drainpipe():
    P = [lathe([(R_PIPE - 0.003, 0.0), (R_PIPE, 0.0), (R_PIPE, 2.88), (R_PIPE + 0.006, 2.9),
                (R_PIPE + 0.006, 3.0), (R_PIPE - 0.003, 3.0)], 24, mat="pipe")]
    for z in (0.45, 2.55):
        P += _bracket(z)
    return L.finalize("Prop_Drainpipe", P, _pipe_specs(), "architecture", size=1024,
                      notes="3 m vertical downpipe (100 mm), socket at the top, wall at +Y (Unity -Z) 9 cm behind "
                            "the axis; stack segments every 3 m; climbable")


def build_drainpipe_top():
    P = []
    # hopper head (rainwater head) fed by the gutter outlet, then an offset to the downpipe socket
    P.append(lathe([(R_PIPE - 0.003, 0.0), (R_PIPE, 0.0), (R_PIPE, 0.25), (R_PIPE - 0.003, 0.25)], 24, mat="pipe"))
    hop = extrude([(-0.16, 0.0), (0.16, 0.0), (0.2, 0.25), (-0.2, 0.25)], 0.2, (0, WALL_OFF - 0.1, 0.42),
                  mat="pipe", bv=0.006, plane="XZ")
    boolean(hop, extrude([(-0.15, 0.03), (0.15, 0.03), (0.19, 0.27), (-0.19, 0.27)], 0.18,
                         (0, WALL_OFF - 0.1, 0.42), mat="_cut", bv=0, plane="XZ"))
    P.append(hop)
    P.append(lathe([(R_PIPE - 0.003, 0.0), (R_PIPE + 0.004, 0.0), (R_PIPE + 0.004, 0.2), (R_PIPE - 0.003, 0.2)],
                   24, (0, 0, 0.24), mat="pipe"))
    P.append(box((0.42, 0.01, 0.06), (0, WALL_OFF - 0.003, 0.69), mat="pipe", bv=0.003))  # rim
    for x in (-0.12, 0.12):
        P.append(box((0.05, 0.012, 0.3), (x, WALL_OFF - 0.006, 0.55), mat="pipe", bv=0.003))  # ears
        P.append(cyl(0.008, 0.008, (x, WALL_OFF - 0.012, 0.6), (90, 0, 0), mat="bolt", n=8, bv=0.001))
    P += _bracket(0.08)
    return L.finalize("Prop_Drainpipe_Top", P, _pipe_specs(), "architecture", size=1024,
                      notes="rainwater hopper head; its bottom spigot plugs into Prop_Drainpipe's top socket")


def build_drainpipe_bottom():
    P = []
    P.append(lathe([(R_PIPE - 0.003, 0.0), (R_PIPE, 0.0), (R_PIPE, 0.35), (R_PIPE + 0.006, 0.37),
                    (R_PIPE + 0.006, 0.45), (R_PIPE - 0.003, 0.45)], 24, (0, 0, 0.12), mat="pipe"))
    # shoe: 45 deg kick-out at the bottom
    pts = [(0, 0, 0.13), (0, 0, 0.09), (0, -0.04, 0.045), (0, -0.12, 0.02)]
    P.append(sweep(pts, R_PIPE, mat="pipe", n=24, smooth=True, caps=False, resolution=8))
    P.append(cyl(R_PIPE + 0.004, 0.015, (0, -0.125, 0.02), (90, 0, 0), mat="pipe", n=24, bv=0.002))
    # drain gully grate under the shoe
    P.append(box((0.3, 0.3, 0.02), (0, -0.15, 0.01), mat="grate", bv=0.004))
    for i in range(7):
        P.append(box((0.03 if False else 0.012, 0.26, 0.004), (-0.12 + i * 0.04, -0.15, 0.021), mat="grate", bv=0.001))
    P += _bracket(0.3)
    return L.finalize("Prop_Drainpipe_Bottom", P, _pipe_specs(), "architecture", size=1024,
                      notes="shoe + ground gully; its top spigot (0.57 m) takes Prop_Drainpipe's bottom")


def _pipe_specs():
    return {"pipe": S("paint_black", color=(0.04, 0.042, 0.045), tile=0.6, wear=0.45,
                      wear_color=(0.3, 0.17, 0.08), scratch=0.15),
            "bolt": S("steel", color=(0.35, 0.3, 0.25)),
            "grate": spec("rust_coarse_01", (0.3, 0.3, 0.3), 0.4, "mul", 0, (0.6, 0.85), 1.0, 0.4)}


# ------------------------------------------------------------------ ladder
def build_ladder():
    P = []
    Hh = 3.0
    for x in (-0.225, 0.225):
        P.append(box((0.012, 0.065, Hh), (x, 0, Hh / 2), mat="steel", bv=0.002))
    for i in range(10):
        z = 0.25 + i * 0.3
        P.append(cyl(0.012, 0.462, (0, 0, z), (0, 90, 0), mat="rung", n=12, bv=0.001))
    for z in (0.35, 1.5, 2.65):  # wall brackets (wall at +Y)
        for x in (-0.225, 0.225):
            P.append(box((0.012, 0.16, 0.05), (x, 0.11, z), mat="steel", bv=0.002))
            P.append(box((0.06, 0.01, 0.08), (x, 0.19, z), mat="steel", bv=0.002))
            P.append(cyl(0.006, 0.01, (x, 0.183, z + 0.02), (90, 0, 0), mat="bolt", n=8, bv=0.001))
    return L.finalize("Prop_Ladder", P, {"steel": S("galvanized", tile=0.8, metal=1.0),
                                          "rung": spec(None, (0.55, 0.56, 0.55), 0.3, "none", 0, (0.3, 0.5), 1.0,
                                                       0.0, noise=0.12, scratch=0.5,
                                                       wear_color=(0.75, 0.75, 0.75)),
                                          "bolt": S("steel")}, "architecture", size=1024,
                      notes="3 m fixed steel ladder, rungs every 0.3 m, wall at +Y (Unity -Z) 0.2 m behind")


# ------------------------------------------------------------------ alarm panel
def build_alarm():
    P = []
    P.append(box((0.24, 0.045, 0.18), (0, 0, 0.09), mat="case", bv=0.008, seg=3))
    P.append(box((0.22, 0.004, 0.16), (0, -0.0235, 0.09), mat="face", bv=0.004))
    P.append(box((0.11, 0.003, 0.035), (-0.045, -0.026, 0.135), mat="lcd_frame", bv=0.002))
    P.append(box((0.098, 0.002, 0.026), (-0.045, -0.0275, 0.135), mat="lcd", bv=0.001))
    P.append(text("SYSTEM ARMED", 0.0062, (-0.045, -0.0287, 0.137), (90, 0, 0), mat="lcd_text"))
    P.append(text("12:47  ZONE 3", 0.0045, (-0.045, -0.0287, 0.128), (90, 0, 0), mat="lcd_text"))
    keys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#"]
    for i, k in enumerate(keys):
        cx = -0.075 + (i % 3) * 0.03
        cz = 0.095 - (i // 3) * 0.022
        P.append(box((0.024, 0.008, 0.016), (cx, -0.027, cz), mat="key", bv=0.003, seg=2))
        P.append(text(k, 0.008, (cx, -0.0312, cz), (90, 0, 0), mat="key_text"))
    for i, (lbl, m) in enumerate((("ARM", "led_r"), ("PWR", "led_g"), ("FAULT", "led_y"))):
        cz = 0.14 - i * 0.03
        P.append(cyl(0.0035, 0.004, (0.055, -0.027, cz), (90, 0, 0), mat=m, n=12, bv=0.001))
        P.append(text(lbl, 0.0055, (0.068, -0.0262, cz), (90, 0, 0), mat="key_text", align="LEFT"))
    for i, lbl in enumerate(("AWAY", "STAY", "OFF")):
        P.append(box((0.03, 0.008, 0.012), (0.075 - 0.0, -0.027, 0.06 - i * 0.02), mat="key", bv=0.003))
        P.append(text(lbl, 0.0045, (0.075, -0.0312, 0.06 - i * 0.02), (90, 0, 0), mat="key_text"))
    P.append(text("SECURE-LINE  9000", 0.0065, (0, -0.0257, 0.025), (90, 0, 0), mat="brand"))
    P.append(box((0.035, 0.03, 0.02), (0, 0.0, -0.01), mat="case", bv=0.004))  # cable gland
    P.append(cyl(0.004, 0.25, (0, 0.005, -0.14), mat="cable", n=8, bv=0))
    specs = {"case": S("plastic_white", color=(0.8, 0.8, 0.77)), "face": S("plastic_white", color=(0.72, 0.72, 0.7)),
             "lcd_frame": S("plastic_black"), "lcd": spec(None, (0.18, 0.32, 0.22), rough=(0.15, 0.2)),
             "lcd_text": spec(None, (0.02, 0.04, 0.03), rough=(0.2, 0.2)),
             "key": S("rubber", color=(0.55, 0.56, 0.56), rough=(0.5, 0.6)),
             "key_text": spec(None, (0.03, 0.03, 0.03), rough=(0.5, 0.5)),
             "led_r": spec(None, (0.8, 0.03, 0.02), rough=(0.1, 0.1)), "led_g": spec(None, (0.05, 0.7, 0.1), rough=(0.1, 0.1)),
             "led_y": spec(None, (0.8, 0.55, 0.02), rough=(0.1, 0.1)),
             "brand": spec(None, (0.25, 0.3, 0.4), rough=(0.4, 0.4)), "cable": S("rubber", color=(0.7, 0.7, 0.68))}
    return L.finalize("Prop_AlarmPanel", P, specs, "system", size=1024, pivot=(0, 0.0225, 0.09),
                      notes="wall keypad; pivot at the centre of its back (wall) face, wall at +Y (Unity -Z)")


# ------------------------------------------------------------------ air handler
def build_air_handler():
    P = []
    Wd, D, Hh = 2.2, 1.0, 1.7
    P.append(box((Wd, D, Hh - 0.12), (0, 0, 0.12 + (Hh - 0.12) / 2), mat="cabinet", bv=0.01))
    for x in (-Wd / 2 + 0.1, Wd / 2 - 0.1):  # base rails
        P.append(box((0.12, D + 0.05, 0.12), (x, 0, 0.06), mat="rail", bv=0.006))
    P.append(box((Wd + 0.02, 0.1, 0.12), (0, 0, 0.06), mat="rail", bv=0.006))
    # access panels on the front with handles + latches
    panels = [(-0.75, 0.62), (0.0, 0.7), (0.72, 0.6)]
    for cx, w in panels:
        P.append(box((w, 0.012, Hh - 0.32), (cx, -D / 2 - 0.006, 0.12 + (Hh - 0.12) / 2), mat="panel", bv=0.006))
        for z in (0.55, 1.3):
            P.append(box((0.025, 0.035, 0.12), (cx + w / 2 - 0.07, -D / 2 - 0.025, z), mat="handle", bv=0.008))
        for (sx, sz) in ((-1, -1), (-1, 1), (1, -1), (1, 1)):
            P.append(cyl(0.007, 0.006, (cx + sx * (w / 2 - 0.03), -D / 2 - 0.013,
                                       0.12 + (Hh - 0.12) / 2 + sz * ((Hh - 0.32) / 2 - 0.03)), (90, 0, 0),
                         mat="bolt", n=8, bv=0.001))
    # side intake louvres (left)
    for i in range(14):
        P.append(box((0.03, 0.7, 0.012), (-Wd / 2 - 0.012, 0, 0.45 + i * 0.065), (0, -35, 0), mat="panel", bv=0.002))
    P.append(box((0.02, 0.76, 0.98), (-Wd / 2 - 0.005, 0, 0.87), mat="dark", bv=0.004))
    # supply duct collar on top + flex connector
    P.append(box((0.8, 0.6, 0.18), (0.5, 0, Hh + 0.09), mat="cabinet", bv=0.008))
    P.append(box((0.86, 0.66, 0.03), (0.5, 0, Hh + 0.195), mat="rail", bv=0.004))
    P.append(cyl(0.25, 0.2, (-0.5, 0.0, Hh + 0.1), mat="cabinet", n=32, bv=0.006))
    P.append(lathe([(0.25, 0.0), (0.27, 0.01), (0.25, 0.03), (0.27, 0.05), (0.25, 0.07), (0.27, 0.09),
                    (0.25, 0.11)], 32, (-0.5, 0, Hh + 0.2), mat="flex"))
    # control box, conduit, nameplate, pipes
    P.append(box((0.4, 0.15, 0.5), (Wd / 2 + 0.075, -0.2, 1.05), mat="ctrl", bv=0.008))
    P.append(box((0.01, 0.1, 0.06), (Wd / 2 + 0.155, -0.2, 1.15), mat="handle", bv=0.003))
    P.append(sweep([(Wd / 2 + 0.07, -0.2, 1.3), (Wd / 2 + 0.07, -0.2, 1.75), (Wd / 2 + 0.07, -0.2, 2.2)], 0.02,
                   mat="rail", smooth=False))
    P.append(box((0.28, 0.004, 0.14), (-0.0, -D / 2 - 0.0135, 1.45), mat="plate", bv=0.002))
    P.append(text("AHU-02  BASEMENT", 0.018, (0, -D / 2 - 0.0158, 1.48), (90, 0, 0), mat="ink"))
    P.append(text("12 kW   400V 3~   1450 rpm", 0.011, (0, -D / 2 - 0.0158, 1.44), (90, 0, 0), mat="ink"))
    P.append(text("DANGER - ISOLATE BEFORE OPENING", 0.011, (0, -D / 2 - 0.0158, 1.41), (90, 0, 0), mat="red"))
    for z in (0.35, 0.55):  # coil pipes on the right side
        P.append(sweep([(Wd / 2 - 0.3, D / 2 - 0.1, z), (Wd / 2 + 0.15, D / 2 - 0.1, z),
                        (Wd / 2 + 0.25, D / 2 - 0.1, z + 0.1), (Wd / 2 + 0.25, D / 2 - 0.1, 2.3)], 0.025,
                       mat="copper", smooth=True))
        P.append(lathe([(0.026, 0), (0.04, 0.01), (0.04, 0.08), (0.026, 0.09)], 16,
                       (Wd / 2 + 0.25, D / 2 - 0.1, z + 0.5), mat="insul"))
    specs = {"cabinet": S("paint_grey", color=(0.55, 0.57, 0.58), tile=1.2, wear=0.25, scratch=0.15),
             "panel": S("paint_grey", color=(0.5, 0.52, 0.53), tile=1.2, wear=0.3, scratch=0.2),
             "rail": S("galvanized", tile=0.8), "handle": S("plastic_black"), "bolt": S("steel"),
             "dark": spec(None, (0.02, 0.02, 0.02), rough=(0.8, 0.9)),
             "flex": S("rubber", color=(0.05, 0.05, 0.05)), "ctrl": S("paint_grey", color=(0.7, 0.7, 0.68)),
             "plate": S("alu"), "ink": S("ink"), "red": spec(None, (0.5, 0.03, 0.02), rough=(0.4, 0.4)),
             "copper": S("copper"), "insul": S("rubber", color=(0.04, 0.04, 0.04))}
    return L.finalize("Prop_AirHandler", P, specs, "system", size=2048,
                      notes="basement air-handling unit 2.2 x 1.0 x 1.7 m, access panels on the front (Unity +Z)")


# ------------------------------------------------------------------ pallet
def build_pallet():
    P = []
    Lp, Wp = 1.2, 0.8
    tops = [(-0.3275, 0.145), (-0.1625, 0.1), (0.0, 0.145), (0.1625, 0.1), (0.3275, 0.145)]
    rnd = random.Random(7)
    for x, w in tops:  # top deck boards along X (1.2 m)
        P.append(box((Lp, w - 0.004, 0.022), (0, x * 1.0, 0.144 - 0.011), (0, 0, rnd.uniform(-0.4, 0.4)),
                     mat="wood", bv=0.003))
    for x in (-0.5275, 0.0, 0.5275):  # cross boards under the deck
        P.append(box((0.145, Wp, 0.022), (x, 0, 0.111), mat="wood", bv=0.003))
    for x in (-0.5275, 0.0, 0.5275):  # blocks
        for y in (-0.3275, 0.0, 0.3275):
            w = 0.145 if x != 0.0 else 0.145
            P.append(box((0.145 if x != 0 else 0.145, w, 0.078), (x, y, 0.061), mat="block", bv=0.004))
            if y == -0.3275:
                P.append(text("EPAL" if x == 0 else "EUR", 0.03, (x, y - 0.0726, 0.065), (90, 0, 0), mat="burn"))
    for y in (-0.3275, 0.0, 0.3275):  # bottom boards along X
        P.append(box((Lp, 0.145, 0.022), (0, y, 0.011), mat="wood", bv=0.003))
    specs = {"wood": spec("wooden_planks", (0.95, 0.88, 0.75), 0.9, "mul", 0, (0.65, 0.9), 0.0, 0.6),
             "block": spec("rough_wood", (0.9, 0.82, 0.7), 0.5, "mul", 0, (0.7, 0.9), 0.0, 0.6),
             "burn": spec(None, (0.08, 0.05, 0.03), rough=(0.8, 0.9))}
    return L.finalize("Prop_Pallet", P, specs, "prop", size=1024, notes="EUR pallet 1.2 x 0.8 x 0.144 m")


# ------------------------------------------------------------------ sandbags
def _bag(loc, rot, rnd, length=0.5):
    """Filled sandbag: heavily bevelled box, slumped, one tied end, lumpy."""
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(length, 0.3, 0.13), verts=bm.verts)
    ob = L._mesh_obj("bag", bm, "bag")
    L.bevel(ob, 0.055, 3, angle=30)
    m = ob.modifiers.new("sub", "SUBSURF")
    m.levels = 1
    L.apply_mods(ob)
    me = ob.data
    ph = rnd.random() * 6.0
    for v in me.vertices:
        x, y, z = v.co
        t = x / (length / 2)
        if t > 0.6:  # tied, pinched end
            k = 1.0 - 0.55 * ((t - 0.6) / 0.4) ** 2
            v.co.y *= k
            v.co.z *= k
        bulge = 1.0 + 0.12 * (1 - min(abs(t), 1) ** 2)  # fuller in the middle
        v.co.y *= bulge if z > -0.03 else 1.0
        v.co.z *= 1.0 + 0.05 * math.sin(x * 19 + ph) * math.sin(y * 23 + ph)
        if z < -0.04:
            v.co.z = -0.065 + (z + 0.065) * 0.4  # flattened under its own weight
    me.update()
    L.place(ob, loc, rot)
    return ob


def build_sandbags():
    rnd = random.Random(11)
    P = []
    z = 0.065
    for r in range(4):
        xs = (-0.5, 0.0, 0.5) if r % 2 == 0 else (-0.5, 0.0, 0.5)
        off = 0.0 if r % 2 == 0 else 0.25
        for i, x in enumerate(xs):
            xx = x + off
            if xx > 0.76:
                continue
            flip = 180 if (i + r) % 2 else 0
            P.append(_bag((xx + rnd.uniform(-0.015, 0.015), rnd.uniform(-0.02, 0.02), z),
                          (rnd.uniform(-2, 2), rnd.uniform(-2, 2), flip + rnd.uniform(-4, 4)), rnd))
        z += 0.112
    specs = {"bag": spec("hessian_230", (0.72, 0.64, 0.46), 0.1, "mul", 0, (0.85, 1.0), 0.0, 0.9)}
    return L.finalize("Prop_Sandbags", P, specs, "prop", size=1024, smooth_angle=60,
                      notes="4-row sandbag wall segment, ~1.5 m long, 0.45 m high")


BUILDERS = {
    "Prop_VentGrille": build_vent_grille,
    "Prop_VentDuct_Straight": build_duct_straight,
    "Prop_VentDuct_Corner": build_duct_corner,
    "Prop_VentDuct_T": build_duct_t,
    "Prop_Drainpipe": build_drainpipe,
    "Prop_Drainpipe_Top": build_drainpipe_top,
    "Prop_Drainpipe_Bottom": build_drainpipe_bottom,
    "Prop_Ladder": build_ladder,
    "Prop_AlarmPanel": build_alarm,
    "Prop_AirHandler": build_air_handler,
    "Prop_Pallet": build_pallet,
    "Prop_Sandbags": build_sandbags,
}

if __name__ == "__main__":
    L.run(BUILDERS, "system")
