# Author : Baptiste Vu Liem
"""Procedural furniture (Furn_*) that Poly Haven does not cover, baked PBR (see pbr_lib).

    blender -b --factory-startup --python Tools/Blender/props/build_furniture.py -- [Furn_Wardrobe ...]

Front faces -Y (Unity +Z), pivot bottom centre, back against a wall at +Y where it applies.
"""
import math
import os
import random
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pbr_lib as L  # noqa: E402
from pbr_lib import LIB, box, cyl, extrude, lathe, sphere, sweep, boolean, cutter_box, spec, text  # noqa: E402


def S(name, **kw):
    return dict(LIB[name], **kw)


def panel_door(w, h, t, loc, mat="wood", inset=0.06, handle=None, handle_mat="brass"):
    """Shaker door: slab with a recessed centre panel and a handle. loc = centre of the door's back face."""
    x, y, z = loc
    P = [box((w, t, h), (x, y - t / 2, z), mat=mat, bv=0.004)]
    cut = cutter_box((w - 2 * inset, 0.012, h - 2 * inset), (x, y - t - 0.002, z))
    L.bevel(cut, 0.006, 2)
    boolean(P[0], cut)
    if handle:
        hx, hz, kind = handle
        if kind == "bar":
            P.append(cyl(0.007, 0.16, (x + hx, y - t - 0.03, z + hz), mat=handle_mat, n=12, bv=0.002))
            for dz in (-0.07, 0.07):
                P.append(cyl(0.005, 0.03, (x + hx, y - t - 0.015, z + hz + dz), (90, 0, 0), mat=handle_mat, n=10,
                             bv=0.001))
        elif kind == "hbar":
            P.append(cyl(0.007, 0.16, (x + hx, y - t - 0.03, z + hz), (0, 90, 0), mat=handle_mat, n=12, bv=0.002))
            for dx in (-0.07, 0.07):
                P.append(cyl(0.005, 0.03, (x + hx + dx, y - t - 0.015, z + hz), (90, 0, 0), mat=handle_mat, n=10,
                             bv=0.001))
        else:
            P.append(lathe([(0.0, 0.0), (0.006, 0.0), (0.006, 0.012), (0.016, 0.022), (0.016, 0.03), (0.0, 0.032)],
                           16, (x + hx, y - t, z + hz), (90, 0, 0), mat=handle_mat))
    return P


# ------------------------------------------------------------------ wardrobe
def build_wardrobe():
    W, D, H, t = 1.0, 0.6, 2.1, 0.02
    P = []
    P.append(box((t, D, H - 0.08), (-W / 2 + t / 2, 0, 0.08 + (H - 0.08) / 2), mat="wood", bv=0.003))
    P.append(box((t, D, H - 0.08), (W / 2 - t / 2, 0, 0.08 + (H - 0.08) / 2), mat="wood", bv=0.003))
    P.append(box((W - 2 * t, t, H - 0.08), (0, D / 2 - t / 2, 0.08 + (H - 0.08) / 2), mat="inner", bv=0.0))
    P.append(box((W - 2 * t, D - t, t), (0, -t / 2, 0.08 + t / 2), mat="inner", bv=0.0))
    P.append(box((W - 2 * t, D - t, t), (0, -t / 2, H - 0.06 - t / 2), mat="inner", bv=0.0))
    P.append(box((W - 0.04, D - 0.06, 0.08), (0, 0.02, 0.04), mat="wood", bv=0.004))  # plinth
    P.append(box((W + 0.06, D + 0.04, 0.06), (0, -0.01, H - 0.03), mat="wood", bv=0.008))  # cornice
    P.append(box((W + 0.03, D + 0.02, 0.025), (0, -0.0, H - 0.07), mat="wood", bv=0.006))
    P.append(cyl(0.012, W - 2 * t, (0, 0.0, H - 0.25), (0, 90, 0), mat="brass", n=12, bv=0.001))  # hanging rail
    P.append(box((W - 2 * t, D - 0.08, t), (0, 0.02, H - 0.2 + 0.0), mat="inner", bv=0))  # hat shelf
    rnd = random.Random(2)
    for i in range(5):  # a few hangers
        x = -0.35 + i * 0.16 + rnd.uniform(-0.02, 0.02)
        P.append(sweep([(x, -0.2, H - 0.36), (x, 0.0, H - 0.3), (x, 0.2, H - 0.36)], 0.004, mat="hanger",
                       smooth=True))
        P.append(sweep([(x, 0.0, H - 0.3), (x, 0.0, H - 0.25)], 0.0025, mat="brass", smooth=False))
    dw, dh, dt = W / 2 - 0.005, H - 0.17, 0.022
    zc = 0.09 + dh / 2
    door_l = panel_door(dw, dh, dt, (-W / 4 - 0.001, -D / 2, zc), handle=(dw / 2 - 0.05, 0.0, "bar"))
    door_r = panel_door(dw, dh, dt, (W / 4 + 0.001, -D / 2, zc), handle=(-dw / 2 + 0.05, 0.0, "bar"))
    specs = {"wood": S("walnut", tile=0.9, color=(0.62, 0.48, 0.38), rough=(0.3, 0.5)),
             "inner": S("oak", tile=0.9, color=(0.75, 0.68, 0.58), rough=(0.5, 0.7)),
             "brass": S("brass"), "hanger": S("walnut", tile=0.3, color=(0.5, 0.35, 0.25))}
    return L.finalize("Furn_Wardrobe", P, specs, "furniture", size=2048,
                      children={"Door_L": (door_l, (-W / 2, -D / 2, 0.09)), "Door_R": (door_r, (W / 2, -D / 2, 0.09))},
                      notes="1.0 x 0.6 x 2.1 m, hollow (hide-in), doors Door_L / Door_R pivot on their hinges "
                            "(rotate around local Y in Unity)")


# ------------------------------------------------------------------ office chair
def build_office_chair():
    P = []
    for k in range(5):
        a = k * 72
        arm = extrude([(0.02, 0.0), (0.32, 0.01), (0.33, 0.035), (0.03, 0.055)], 0.045, mat="base", bv=0.008,
                      plane="XZ")
        L.place(arm, (0, 0, 0.06), (0, 0, a))
        P.append(arm)
        ca = math.radians(a)
        cx, cy = 0.31 * math.cos(ca), 0.31 * math.sin(ca)
        P.append(cyl(0.008, 0.04, (cx, cy, 0.06), mat="chrome", n=10, bv=0.001))
        for side in (-1, 1):
            P.append(cyl(0.028, 0.012, (cx + side * 0.009 * -math.sin(ca), cy + side * 0.009 * math.cos(ca), 0.03),
                         (90, 0, a), mat="caster", n=16, bv=0.003))
        P.append(box((0.05, 0.03, 0.03), (cx, cy, 0.05), (0, 0, a), mat="base", bv=0.008))
    P.append(cyl(0.05, 0.08, (0, 0, 0.1), mat="base", n=24, bv=0.01))
    P.append(cyl(0.028, 0.2, (0, 0, 0.24), mat="base", n=20, bv=0.004))
    P.append(cyl(0.02, 0.14, (0, 0, 0.38), mat="chrome", n=16, bv=0.002))
    P.append(box((0.26, 0.26, 0.05), (0, 0, 0.46), mat="base", bv=0.01))
    seat = box((0.5, 0.48, 0.09), (0, -0.02, 0.52), mat="fabric", bv=0.035, seg=4)
    P.append(seat)
    P.append(box((0.48, 0.46, 0.02), (0, -0.02, 0.47), mat="base", bv=0.008))
    back = box((0.46, 0.07, 0.58), (0, 0.27, 0.92), (-8, 0, 0), mat="fabric", bv=0.03, seg=4)
    P.append(back)
    P.append(box((0.44, 0.03, 0.56), (0, 0.31, 0.91), (-8, 0, 0), mat="base", bv=0.012))
    P.append(extrude([(0.0, 0.47), (0.05, 0.47), (0.12, 0.60), (0.08, 0.62)], 0.05, (0, 0.24, 0), mat="base",
                     bv=0.01, plane="YZ"))
    for x in (-0.27, 0.27):  # armrests
        P.append(box((0.04, 0.05, 0.2), (x, 0.0, 0.6), mat="base", bv=0.012))
        P.append(box((0.07, 0.26, 0.035), (x, -0.02, 0.715), mat="pad", bv=0.014, seg=3))
        P.append(box((0.03, 0.04, 0.08), (x * 0.92, 0.0, 0.5), mat="base", bv=0.01))
    P.append(box((0.28, 0.025, 0.012), (0.2, -0.05, 0.43), (0, 0, 20), mat="base", bv=0.004))  # lever
    specs = {"base": S("plastic_black"), "chrome": S("chrome"), "caster": S("rubber", color=(0.03, 0.03, 0.03)),
             "fabric": S("fabric_grey", color=(0.06, 0.065, 0.07), tile=0.15), "pad": S("rubber", grain=0.5)}
    return L.finalize("Furn_OfficeChair", P, specs, "furniture", size=1024, smooth_angle=45)


# ------------------------------------------------------------------ dining table
def build_dining_table():
    P = []
    Lt, Wt, Ht = 2.2, 1.0, 0.76
    P.append(box((Lt, Wt, 0.04), (0, 0, Ht - 0.02), mat="top", bv=0.008, seg=3))
    for (sx, sy, w, d) in ((0, -Wt / 2 + 0.08, Lt - 0.24, 0.022), (0, Wt / 2 - 0.08, Lt - 0.24, 0.022),
                           (-Lt / 2 + 0.12, 0, 0.022, Wt - 0.16), (Lt / 2 - 0.12, 0, 0.022, Wt - 0.16)):
        P.append(box((w, d, 0.1), (sx, sy, Ht - 0.09), mat="wood", bv=0.004))
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (Lt / 2 - 0.12), sy * (Wt / 2 - 0.08)
            leg = lathe([(0.0, 0.0), (0.022, 0.0), (0.024, 0.01), (0.03, 0.4), (0.034, 0.62), (0.036, Ht - 0.04),
                         (0.0, Ht - 0.04)], 16, (x, y, 0), mat="wood")
            P.append(leg)
            P.append(cyl(0.024, 0.006, (x, y, 0.003), mat="felt", n=16, bv=0.001))
    specs = {"top": S("walnut", tile=1.4, color=(0.6, 0.45, 0.35), rough=(0.15, 0.3)),
             "wood": S("walnut", tile=0.8, color=(0.5, 0.36, 0.27)), "felt": S("rubber")}
    return L.finalize("Furn_DiningTable", P, specs, "furniture", size=2048, notes="2.2 x 1.0 x 0.76 m, seats 8")


# ------------------------------------------------------------------ TV
def build_tv():
    P = []
    W, Hs, zc = 1.24, 0.72, 0.5
    P.append(box((W, 0.026, Hs), (0, 0, zc), mat="bezel", bv=0.004))
    P.append(box((W - 0.012, 0.002, Hs - 0.012), (0, -0.0135, zc), mat="screen", bv=0.0))
    P.append(box((W - 0.2, 0.05, Hs - 0.2), (0, 0.035, zc - 0.03), mat="back", bv=0.02, seg=3))
    for x in (-0.48, 0.48):  # feet
        P.append(extrude([(-0.11, 0.0), (0.09, 0.0), (0.0, 0.14)], 0.018, (x, 0.0, 0.0), mat="feet", bv=0.004,
                         plane="YZ"))
        P.append(box((0.03, 0.03, 0.03), (x, 0.0, 0.14), mat="feet", bv=0.004))
    P.append(box((0.12, 0.004, 0.012), (0, -0.014, zc - Hs / 2 + 0.006), mat="logo", bv=0.001))
    P.append(text("KORVAN", 0.009, (0, -0.0142, zc - Hs / 2 - 0.0), (90, 0, 0), mat="logo"))
    for i in range(4):
        P.append(box((0.02, 0.004, 0.012), (0.45 + i * 0.03, 0.06, zc - 0.1), mat="port", bv=0.001))
    specs = {"bezel": S("plastic_black", color=(0.02, 0.02, 0.022), rough=(0.3, 0.4)),
             "screen": S("screen"), "back": S("plastic_black", color=(0.03, 0.03, 0.032), grain=0.1),
             "feet": S("alu", color=(0.2, 0.2, 0.21)), "logo": S("alu"), "port": spec(None, (0.01, 0.01, 0.01))}
    return L.finalize("Furn_TV", P, specs, "furniture", size=1024, notes="55-inch flat TV on feet")


# ------------------------------------------------------------------ kitchen counter
def build_kitchen_counter():
    P = []
    Lc, D, H = 2.4, 0.62, 0.9
    P.append(box((Lc, 0.56, 0.1), (0, 0.01, 0.05), mat="plinth", bv=0.002))
    P.append(box((Lc, 0.58, H - 0.14), (0, 0.02, 0.1 + (H - 0.14) / 2), mat="carcass", bv=0.002))
    P.append(box((Lc + 0.02, D + 0.02, 0.04), (0, -0.0, H - 0.02), mat="stone", bv=0.004, seg=2))
    units = [(-0.9, "drawers"), (-0.3, "door2"), (0.3, "door2"), (0.9, "drawers")]
    fy = -0.27
    for ux, kind in units:
        w = 0.596
        if kind == "drawers":
            for k, (h, zc) in enumerate(((0.16, 0.77), (0.25, 0.555), (0.31, 0.265))):
                P += panel_door(w, h - 0.004, 0.019, (ux, fy, zc), mat="front", inset=0.04,
                                handle=(0.0, h / 2 - 0.045, "hbar"), handle_mat="steel")
        else:
            P += panel_door(w, 0.15, 0.019, (ux, fy, 0.77), mat="front", inset=0.04,
                            handle=(0.0, 0.03, "hbar"), handle_mat="steel")
            for side in (-1, 1):
                P += panel_door(w / 2 - 0.003, 0.58, 0.019, (ux + side * w / 4, fy, 0.39), mat="front", inset=0.04,
                                handle=(-side * (w / 4 - 0.04), 0.2, "bar"), handle_mat="steel")
    specs = {"plinth": S("plastic_black", color=(0.05, 0.05, 0.05)), "carcass": S("plastic_white"),
             "stone": spec(None, (0.06, 0.06, 0.065), 0.3, "none", 0.0, (0.08, 0.18), 0.0, 0.0, noise=0.35,
                           grain=0.05, scratch=0.05, wear_color=(0.2, 0.2, 0.2)),
             "front": S("paint_white", color=(0.18, 0.24, 0.22), tile=0.8, wear=0.05, rough=(0.3, 0.4)),
             "steel": S("brushed")}
    return L.finalize("Furn_KitchenCounter", P, specs, "furniture", size=2048,
                      notes="2.4 m base-cabinet run, marble top at 0.9 m")


# ------------------------------------------------------------------ fridge
def build_fridge():
    P = []
    W, D, H = 0.7, 0.68, 1.85
    P.append(box((W, D - 0.06, H - 0.02), (0, 0.03, 0.01 + (H - 0.02) / 2), mat="body", bv=0.01))
    for (zc, h) in ((0.38, 0.66), (1.24, 1.18)):
        P.append(box((W, 0.06, h), (0, -D / 2 + 0.03, zc), mat="door", bv=0.012, seg=3))
        P.append(box((W - 0.01, 0.004, h - 0.01), (0, -D / 2 + 0.061, zc), mat="gasket", bv=0.002))
    for (zc, ln) in ((0.55, 0.3), (1.0, 0.45)):
        P.append(cyl(0.012, ln, (W / 2 - 0.06, -D / 2 - 0.045, zc), mat="handle", n=16, bv=0.004))
        for dz in (-ln / 2 + 0.02, ln / 2 - 0.02):
            P.append(cyl(0.008, 0.045, (W / 2 - 0.06, -D / 2 - 0.022, zc + dz), (90, 0, 0), mat="handle", n=12,
                         bv=0.002))
    P.append(box((W - 0.04, 0.01, 0.06), (0, -D / 2 + 0.05, 0.03), mat="grille", bv=0.002))
    for i in range(12):
        P.append(box((0.04, 0.004, 0.04), (-0.27 + i * 0.049, -D / 2 + 0.044, 0.03), mat="dark", bv=0.0))
    P.append(box((0.12, 0.004, 0.06), (0, -D / 2 - 0.001, 1.62), mat="display", bv=0.003))
    P.append(text("-18  4", 0.02, (0, -D / 2 - 0.0035, 1.62), (90, 0, 0), mat="led"))
    specs = {"body": S("brushed", color=(0.6, 0.6, 0.61), tile=0.6, noise=0.02),
             "door": S("brushed", color=(0.7, 0.7, 0.71), tile=0.6, noise=0.02),
             "gasket": S("rubber"), "handle": S("brushed", color=(0.75, 0.75, 0.76)),
             "grille": S("plastic_black"), "dark": spec(None, (0.01, 0.01, 0.01)),
             "display": S("plastic_black", rough=(0.05, 0.1)), "led": spec(None, (0.2, 0.7, 1.0), rough=(0.1, 0.1))}
    return L.finalize("Furn_Fridge", P, specs, "furniture", size=1024, notes="stainless fridge-freezer 0.7 x 0.68 x 1.85 m")


# ------------------------------------------------------------------ bar counter
def build_bar_counter():
    P = []
    Lb, H = 2.6, 1.1
    P.append(box((Lb, 0.5, H - 0.1), (0, 0.05, 0.1 + (H - 0.1) / 2 - 0.04), mat="wood", bv=0.006))
    P.append(box((Lb - 0.04, 0.46, 0.1), (0, 0.06, 0.05), mat="plinth", bv=0.004))
    n = 6
    pw = (Lb - 0.1) / n
    for i in range(n):  # raised panels on the front
        x = -Lb / 2 + 0.05 + pw * (i + 0.5)
        P.append(box((pw - 0.06, 0.03, 0.62), (x, -0.205, 0.55), mat="wood", bv=0.012, seg=3))
        P.append(box((pw - 0.12, 0.012, 0.52), (x, -0.226, 0.55), mat="wood", bv=0.006, seg=2))
    for x in [-Lb / 2 + 0.05 + pw * i for i in range(n + 1)]:  # pilasters
        P.append(box((0.05, 0.05, 0.86), (x, -0.215, 0.53), mat="wood", bv=0.008))
    P.append(box((Lb + 0.1, 0.72, 0.05), (0, -0.04, H - 0.025), mat="stone", bv=0.01, seg=3))
    P.append(box((Lb + 0.08, 0.08, 0.03), (0, -0.38, H - 0.06), mat="wood", bv=0.012))  # bullnose under the top
    P.append(cyl(0.025, Lb, (0, -0.4, 0.2), (0, 90, 0), mat="brass", n=20, bv=0.003))  # foot rail
    for x in (-Lb / 2 + 0.2, 0.0, Lb / 2 - 0.2):
        P.append(sweep([(x, -0.4, 0.2), (x, -0.32, 0.19), (x, -0.25, 0.25)], 0.012, mat="brass", smooth=True))
    specs = {"wood": S("darkwood", tile=1.0, rough=(0.25, 0.45)), "plinth": S("plastic_black"),
             "stone": spec(None, (0.12, 0.1, 0.09), 0.3, "none", 0.0, (0.06, 0.14), 0.0, 0.0, noise=0.3,
                           grain=0.04), "brass": S("brass")}
    return L.finalize("Furn_BarCounter", P, specs, "furniture", size=2048,
                      notes="2.6 m bar, customer side -Y (Unity +Z) with brass foot rail")


# ------------------------------------------------------------------ bathroom
def _oval(ob, sy):
    from mathutils import Matrix
    ob.data.transform(Matrix.Diagonal((1, sy, 1, 1)))
    return ob


def build_toilet():
    P = []
    bowl = lathe([(0.0, 0.0), (0.11, 0.0), (0.12, 0.01), (0.1, 0.06), (0.105, 0.2), (0.155, 0.36), (0.17, 0.39),
                  (0.168, 0.405), (0.14, 0.405), (0.11, 0.36), (0.05, 0.2), (0.0, 0.18)], 40, mat="ceramic",
                 smooth_profile=1)
    _oval(bowl, 1.32)
    L.place(bowl, (0, -0.06, 0))
    P.append(bowl)
    P.append(box((0.3, 0.3, 0.32), (0, 0.13, 0.19), mat="ceramic", bv=0.06, seg=4))  # trapway / pedestal back
    P.append(box((0.4, 0.17, 0.36), (0, 0.24, 0.6), mat="ceramic", bv=0.035, seg=4))  # cistern
    P.append(box((0.42, 0.19, 0.04), (0, 0.24, 0.8), mat="ceramic", bv=0.015, seg=3))  # lid
    P.append(cyl(0.025, 0.008, (0, 0.24, 0.823), mat="chrome", n=24, bv=0.003))
    seat = lathe([(0.12, 0.0), (0.172, 0.0), (0.178, 0.012), (0.172, 0.022), (0.12, 0.022), (0.115, 0.01)], 40,
                 mat="seat")
    _oval(seat, 1.3)
    L.place(seat, (0, -0.06, 0.408))
    P.append(seat)
    lid = lathe([(0.0, 0.0), (0.172, 0.0), (0.176, 0.01), (0.16, 0.02), (0.0, 0.024)], 40, mat="seat")
    _oval(lid, 1.3)
    L.place(lid, (0, 0.0, 0.0), (-97, 0, 0))
    L.place(lid, (0, 0.17, 0.44))
    P.append(lid)
    for x in (-0.07, 0.07):
        P.append(cyl(0.012, 0.03, (x, 0.15, 0.425), (0, 90, 0), mat="chrome", n=12, bv=0.002))
    specs = {"ceramic": S("ceramic"), "seat": S("plastic_white", color=(0.85, 0.85, 0.83), rough=(0.2, 0.3)),
             "chrome": S("chrome")}
    return L.finalize("Furn_Toilet", P, specs, "furniture", size=1024, smooth_angle=50,
                      notes="close-coupled WC, cistern against the wall at +Y (Unity -Z); seat lid raised")


def build_sink():
    P = []
    basin = lathe([(0.0, 0.0), (0.06, 0.0), (0.12, 0.02), (0.2, 0.08), (0.24, 0.13), (0.26, 0.17), (0.255, 0.18),
                   (0.23, 0.18), (0.22, 0.165), (0.18, 0.11), (0.1, 0.06), (0.03, 0.05), (0.0, 0.05)], 40,
                  mat="ceramic", smooth_profile=1)
    _oval(basin, 0.78)
    L.place(basin, (0, -0.02, 0.68))
    P.append(basin)
    P.append(box((0.5, 0.12, 0.05), (0, 0.15, 0.835), mat="ceramic", bv=0.02, seg=3))  # tap deck
    ped = lathe([(0.0, 0.0), (0.11, 0.0), (0.12, 0.01), (0.1, 0.05), (0.075, 0.3), (0.07, 0.55), (0.09, 0.7),
                 (0.0, 0.72)], 32, mat="ceramic", smooth_profile=1)
    _oval(ped, 0.8)
    L.place(ped, (0, 0.05, 0))
    P.append(ped)
    P.append(cyl(0.022, 0.012, (0, 0.15, 0.866), mat="chrome", n=24, bv=0.003))
    P.append(sweep([(0, 0.15, 0.86), (0, 0.15, 1.0), (0, 0.08, 1.02), (0, 0.04, 0.96)], 0.012, mat="chrome",
                   smooth=True))
    P.append(box((0.016, 0.07, 0.012), (0, 0.18, 1.0), (15, 0, 0), mat="chrome", bv=0.004))  # lever
    P.append(cyl(0.018, 0.006, (0, -0.03, 0.735), mat="chrome", n=16, bv=0.002))  # drain
    specs = {"ceramic": S("ceramic"), "chrome": S("chrome")}
    return L.finalize("Furn_Sink", P, specs, "furniture", size=1024, smooth_angle=50,
                      notes="pedestal washbasin with mixer tap, wall at +Y (Unity -Z)")


def build_bathtub():
    P = []
    Lb, Wb, Hb = 1.7, 0.78, 0.58
    shell = box((Lb, Wb, Hb), (0, 0, Hb / 2), mat="acrylic", bv=0.03, seg=4)
    inner = cutter_box((Lb - 0.14, Wb - 0.14, Hb), (0, 0, Hb / 2 + 0.12))
    L.bevel(inner, 0.12, 5)
    boolean(shell, inner)
    P.append(shell)
    P.append(cyl(0.022, 0.008, (-Lb / 2 + 0.2, 0, 0.125), mat="chrome", n=20, bv=0.002))  # drain
    P.append(cyl(0.025, 0.06, (-Lb / 2 + 0.08, 0.25, Hb + 0.03), mat="chrome", n=20, bv=0.004))
    P.append(sweep([(-Lb / 2 + 0.08, 0.25, Hb + 0.05), (-Lb / 2 + 0.08, 0.25, Hb + 0.16),
                    (-Lb / 2 + 0.16, 0.25, Hb + 0.13)], 0.014, mat="chrome", smooth=True))
    for x in (-0.07, 0.07):
        P.append(cyl(0.02, 0.05, (-Lb / 2 + 0.08 + 0.0, 0.25 + x * 1.6, Hb + 0.025), mat="chrome", n=16, bv=0.004))
        P.append(box((0.06, 0.012, 0.012), (-Lb / 2 + 0.08, 0.25 + x * 1.6, Hb + 0.055), mat="chrome", bv=0.004))
    specs = {"acrylic": S("ceramic", color=(0.9, 0.9, 0.88), rough=(0.06, 0.12)), "chrome": S("chrome")}
    return L.finalize("Furn_Bathtub", P, specs, "furniture", size=1024, smooth_angle=50,
                      notes="1.7 x 0.78 m built-in bathtub, taps at -X / +Y corner")


# ------------------------------------------------------------------ rug (pattern generated with numpy)
def rug_pattern(res=1024, seed=5):
    """Persian-style rug albedo (sRGB 0..1) from geometry rules + Poly Haven wool fibre detail."""
    rnd = np.random.default_rng(seed)
    v, u = np.mgrid[0:res, 0:res].astype(np.float32) / res
    red = np.array([0.42, 0.06, 0.06])
    navy = np.array([0.06, 0.08, 0.2])
    cream = np.array([0.78, 0.7, 0.55])
    gold = np.array([0.7, 0.5, 0.2])
    teal = np.array([0.1, 0.3, 0.3])
    img = np.zeros((res, res, 3), np.float32) + red
    du = np.minimum(u, 1 - u)
    dv = np.minimum(v, 1 - v)
    d = np.minimum(du, dv)
    # borders
    for lo, hi, c in ((0.0, 0.025, navy), (0.025, 0.035, cream), (0.035, 0.1, navy), (0.1, 0.11, gold),
                      (0.11, 0.12, cream)):
        m = (d >= lo) & (d < hi)
        img[m] = c
    # border motif: repeating rosettes in the navy band
    band = (d >= 0.04) & (d < 0.095)
    along = np.where(du < dv, v, u)
    ph = (along * 18) % 1.0
    across = (d - 0.0675) / 0.0275
    ros = ((ph - 0.5) ** 2 * 4 + across ** 2) < 0.35
    img[band & ros] = gold
    ros2 = ((ph - 0.5) ** 2 * 4 + across ** 2) < 0.12
    img[band & ros2] = red
    # field lattice of small diamonds
    field = d >= 0.12
    fu, fv = (u * 14) % 1.0 - 0.5, (v * 10) % 1.0 - 0.5
    dia = (np.abs(fu) + np.abs(fv)) < 0.18
    img[field & dia] = navy
    dia2 = (np.abs(fu) + np.abs(fv)) < 0.08
    img[field & dia2] = cream
    # central medallion
    cu, cv = (u - 0.5) / 0.22, (v - 0.5) / 0.3
    r = np.abs(cu) + np.abs(cv)
    ang = np.arctan2(cv, cu)
    star = r < (0.9 + 0.12 * np.cos(ang * 8))
    img[star] = navy
    img[(r < 0.7 + 0.08 * np.cos(ang * 8))] = cream
    img[(r < 0.45)] = red
    img[(r < 0.25)] = gold
    img[(r < 0.12)] = teal
    # corner spandrels
    cr = np.minimum(np.hypot(u - 0.12, v - 0.12), np.minimum(np.hypot(u - 0.88, v - 0.12),
                    np.minimum(np.hypot(u - 0.12, v - 0.88), np.hypot(u - 0.88, v - 0.88))))
    img[field & (cr < 0.13)] = navy
    img[field & (cr < 0.07)] = cream
    # wear + abrash (dye streaks)
    streak = 1.0 + 0.06 * np.sin(v * 40 + rnd.random() * 6) * np.sin(v * 7)
    img *= streak[..., None]
    return np.clip(img, 0, 1)


def build_rug():
    import bmesh
    Lr, Wr = 2.4, 1.7
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
    bmesh.ops.scale(bm, vec=(Lr, Wr, 1), verts=bm.verts)
    res = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, vec=(0, 0, 0.01), verts=[e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = L._mesh_obj("Furn_Rug", bm, "rug")
    me = ob.data
    uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (co.x / Lr + 0.5, co.y / Wr + 0.5)
    folder = os.path.join(L.MODELS, "Furn_Rug")
    os.makedirs(folder, exist_ok=True)
    slot = "Furn_Rug_wool"
    base = rug_pattern(1024)
    wool = L.img_array(L.load_image(L.tex_path("wool_boucle", "diff")))[:1024, :1024, :3]
    lum = (wool * np.array([0.3, 0.59, 0.11])).sum(2, keepdims=True)
    tiled = np.tile(lum, (1, 1, 1))
    k = 0.75 + 0.5 * (tiled / max(float(lum.mean()), 1e-3) - 1.0) * 0.5
    L.save_array(np.clip(base * k, 0, 1), os.path.join(folder, slot + "_diff.jpg"))
    nor = L.img_array(L.load_image(L.tex_path("wool_boucle", "nor_gl"), data=True))[:1024, :1024, :3]
    L.save_array(nor, os.path.join(folder, slot + "_nor_gl.jpg"), True, 95)
    arm = np.ones((1024, 1024, 3), np.float32) * np.array([1.0, 0.95, 0.0], np.float32)
    L.save_array(arm, os.path.join(folder, slot + "_arm.jpg"), True)
    me.materials.clear()
    me.materials.append(L._export_material(slot, folder))
    L.finish_normals(ob, 30, weighted=False)
    L.export_fbx(ob, "Furn_Rug", folder)
    entry = L.write_part("Furn_Rug", "furniture", [ob], "Procedural pattern (numpy) + Poly Haven 'wool_boucle' fibre",
                         "Original + CC0 (Poly Haven)", "2.4 x 1.7 m Persian-style rug, 1 cm thick")
    L.preview([ob], "Furn_Rug", "top")
    return entry


# ------------------------------------------------------------------ floor lamp
def build_floor_lamp():
    P = []
    P.append(lathe([(0.0, 0.0), (0.16, 0.0), (0.165, 0.006), (0.15, 0.025), (0.03, 0.035), (0.0, 0.035)], 40,
                   mat="metal", smooth_profile=1))
    P.append(cyl(0.012, 1.45, (0, 0, 0.76), mat="metal", n=16, bv=0.002))
    P.append(cyl(0.018, 0.04, (0, 0, 0.6), mat="metal", n=16, bv=0.004))
    P.append(lathe([(0.2, 0.0), (0.2, 0.32), (0.196, 0.32), (0.196, 0.004), (0.2, 0.0)], 48, (0, 0, 1.36),
                   mat="shade"))
    P.append(lathe([(0.201, 0.0), (0.203, 0.0), (0.203, 0.012), (0.201, 0.012)], 48, (0, 0, 1.36), mat="metal"))
    P.append(lathe([(0.201, 0.0), (0.203, 0.0), (0.203, 0.012), (0.201, 0.012)], 48, (0, 0, 1.668), mat="metal"))
    for k in range(3):
        a = math.radians(k * 120)
        P.append(sweep([(0, 0, 1.5), (0.198 * math.cos(a), 0.198 * math.sin(a), 1.672)], 0.003, mat="metal",
                       smooth=False))
    P.append(lathe([(0.0, 0.0), (0.02, 0.0), (0.02, 0.03), (0.03, 0.06), (0.03, 0.09), (0.0, 0.11)], 20,
                   (0, 0, 1.45), mat="bulb"))
    P.append(sweep([(0.0, 0.03, 0.02), (0.0, 0.25, 0.0), (0.3, 0.6, 0.0)], 0.003, mat="cable", smooth=True))
    specs = {"metal": S("brass", color=(0.62, 0.48, 0.28)), "shade": S("fabric_beige", color=(0.85, 0.8, 0.68)),
             "bulb": spec(None, (0.95, 0.92, 0.85), rough=(0.1, 0.1)), "cable": S("rubber")}
    return L.finalize("Furn_FloorLamp", P, specs, "furniture", size=1024, notes="1.7 m brass floor lamp, drum shade")


# ------------------------------------------------------------------ grand piano
def _piano_outline(n=28):
    pts = [(-0.75, -0.95), (0.75, -0.95), (0.75, -0.35)]
    for i in range(1, n + 1):  # bentside: S curve from the treble side to the tail
        t = i / n
        x = 0.75 - 1.25 * (1 - math.cos(t * math.pi / 2)) ** 0.9 + 0.12 * math.sin(t * math.pi) * (1 - t)
        y = -0.35 + 1.3 * t
        pts.append((x, y))
    tail = pts[-1]
    for i in range(1, 9):  # rounded tail to the bass side
        a = math.pi * i / 9
        cx = (tail[0] - 0.75) / 2
        r = (tail[0] + 0.75) / 2
        pts.append((cx + r * math.cos(a), tail[1] + 0.12 * math.sin(a)))
    pts.append((-0.75, tail[1]))
    return pts


def build_piano():
    P = []
    out = _piano_outline()
    z0, rim = 0.62, 0.3
    case = extrude(out, rim, (0, 0, z0 + rim / 2), mat="lacquer", bv=0.01, plane="XY", angle=40)
    inner = [(x * 0.95, y * 0.96 + 0.01) for x, y in out]
    boolean(case, extrude(inner, rim, (0, 0, z0 + rim / 2 + 0.05), mat="_cut", bv=0, plane="XY"))
    P.append(case)
    P.append(extrude(inner, 0.01, (0, 0, z0 + 0.09), mat="soundboard", bv=0, plane="XY"))
    plate = extrude([(x * 0.85, y * 0.85 - 0.05) for x, y in out], 0.02, (0, 0.0, z0 + 0.12), mat="plate", bv=0.004,
                    plane="XY")
    for i in range(5):
        c = L.cyl(0.07, 0.1, (-0.35 + i * 0.17, 0.25 + (i % 2) * 0.15, z0 + 0.12), mat="_cut", n=20, bv=0)
        boolean(plate, c)
    P.append(plate)
    for i in range(48):  # strings
        x = -0.6 + i * 0.026
        ln = 1.6 - 0.95 * (i / 48) ** 1.3
        P.append(box((0.0016, ln, 0.0016), (x, -0.62 + ln / 2, z0 + 0.135), mat="strings", bv=0))
    # keybed + keys
    kz = 0.72
    P.append(box((1.5, 0.3, 0.06), (0, -1.08, kz - 0.05), mat="lacquer", bv=0.008))
    for side in (-1, 1):
        P.append(box((0.07, 0.32, 0.14), (side * 0.715, -1.08, kz + 0.02), mat="lacquer", bv=0.012, seg=3))
    P.append(box((1.36, 0.06, 0.1), (0, -0.95, kz + 0.04), mat="lacquer", bv=0.006))  # fallboard
    nw = 52
    kw = 1.34 / nw
    for i in range(nw):
        P.append(box((kw - 0.0015, 0.15, 0.02), (-0.67 + kw * (i + 0.5), -1.11, kz), mat="ivory", bv=0.0015))
    pattern = [1, 1, 0, 1, 1, 1, 0]  # black keys after C, D, (no E), F, G, A, (no B)
    for i in range(nw - 1):
        if pattern[(i + 5) % 7]:  # start at A0
            P.append(box((kw * 0.55, 0.095, 0.016), (-0.67 + kw * (i + 1), -1.08, kz + 0.016), mat="ebony",
                         bv=0.002))
    # music desk
    P.append(box((0.8, 0.02, 0.24), (0, -0.82, z0 + rim + 0.13), (-15, 0, 0), mat="lacquer", bv=0.005))
    P.append(box((0.8, 0.05, 0.015), (0, -0.85, z0 + rim + 0.02), mat="lacquer", bv=0.004))
    # lid propped open (hinged on the bass side, -X)
    lid = extrude(out, 0.02, (0, 0, 0), mat="lacquer", bv=0.006, plane="XY")
    L.place(lid, (0.75, 0, 0))
    L.place(lid, (0, 0, 0), (0, -38, 0))
    L.place(lid, (-0.75, 0, z0 + rim + 0.01))
    P.append(lid)
    P.append(cyl(0.012, 0.75, (0.25, 0.1, z0 + rim + 0.33), (0, -55, 0), mat="lacquer", n=12, bv=0.002))  # stick
    # legs with brass casters
    for (x, y) in ((-0.65, -0.82), (0.65, -0.82), (-0.45, 0.75)):
        P.append(lathe([(0.04, 0.05), (0.05, 0.12), (0.06, 0.3), (0.075, 0.5), (0.085, z0 - 0.02), (0.0, z0)], 20,
                       (x, y, 0), mat="lacquer", smooth_profile=1))
        P.append(cyl(0.035, 0.05, (x, y, 0.025), (90, 0, 0), mat="brass", n=16, bv=0.004))
    # pedal lyre
    P.append(box((0.22, 0.05, 0.42), (0, -0.4, 0.33), mat="lacquer", bv=0.012))
    P.append(box((0.26, 0.12, 0.05), (0, -0.42, 0.07), mat="lacquer", bv=0.01))
    for x in (-0.06, 0.0, 0.06):
        P.append(box((0.028, 0.11, 0.012), (x, -0.5, 0.06), (8, 0, 0), mat="brass", bv=0.004))
    P.append(text("STEINHAUS & SOHN", 0.022, (0, -0.981, kz + 0.06), (90, 0, 0), mat="gilt"))
    specs = {"lacquer": spec(None, (0.012, 0.012, 0.013), 0.3, "none", 0, (0.04, 0.1), 0.0, 0.0, noise=0.01,
                             scratch=0.05, wear_color=(0.08, 0.08, 0.08)),
             "soundboard": S("oak", tile=0.6, color=(1.0, 0.85, 0.65)),
             "plate": S("brass", color=(0.65, 0.5, 0.22), rough=(0.35, 0.5)),
             "strings": S("steel", color=(0.7, 0.68, 0.6)), "ivory": S("plastic_white", color=(0.88, 0.86, 0.8)),
             "ebony": S("plastic_black", rough=(0.25, 0.35)), "brass": S("brass"),
             "gilt": spec(None, (0.8, 0.62, 0.3), rough=(0.3, 0.3), metal=1.0)}
    return L.finalize("Furn_PianoGrand", P, specs, "furniture", size=2048, max_tris=20000,
                      notes="concert grand ~1.5 x 2.2 m, lid propped open, keyboard faces -Y (Unity +Z)")


# ------------------------------------------------------------------ billiard table
def build_billiard():
    P = []
    Lt, Wt, H = 2.84, 1.6, 0.8
    P.append(box((Lt - 0.1, Wt - 0.1, 0.2), (0, 0, H - 0.17), mat="wood", bv=0.02, seg=3))  # apron/body
    P.append(box((Lt - 0.24, Wt - 0.24, 0.04), (0, 0, H - 0.05), mat="felt", bv=0.002))  # bed
    rails = [((Lt, 0.14), (0, -(Wt / 2 - 0.07))), ((Lt, 0.14), (0, Wt / 2 - 0.07)),
             ((0.14, Wt - 0.28), (-(Lt / 2 - 0.07), 0)), ((0.14, Wt - 0.28), (Lt / 2 - 0.07, 0))]
    for (w, d), (x, y) in rails:
        P.append(box((w, d, 0.06), (x, y, H - 0.0), mat="wood", bv=0.02, seg=3))
    # cushions
    for (w, d), (x, y) in (((Lt / 2 - 0.26, 0.05), (-Lt / 4 + 0.0, -(Wt / 2 - 0.145))),
                           ((Lt / 2 - 0.26, 0.05), (Lt / 4, -(Wt / 2 - 0.145))),
                           ((Lt / 2 - 0.26, 0.05), (-Lt / 4, Wt / 2 - 0.145)),
                           ((Lt / 2 - 0.26, 0.05), (Lt / 4, Wt / 2 - 0.145)),
                           ((0.05, Wt - 0.5), (-(Lt / 2 - 0.145), 0)), ((0.05, Wt - 0.5), (Lt / 2 - 0.145, 0))):
        P.append(box((w, d, 0.04), (x, y, H - 0.01), mat="felt", bv=0.015, seg=3))
    for x in (-(Lt / 2 - 0.12), 0.0, Lt / 2 - 0.12):  # pockets
        for y in (-(Wt / 2 - 0.12), Wt / 2 - 0.12):
            P.append(cyl(0.065, 0.08, (x, y, H - 0.04), mat="pocket", n=20, bv=0.005))
            P.append(lathe([(0.06, 0.0), (0.075, 0.0), (0.075, 0.012), (0.06, 0.012)], 20, (x, y, H + 0.024),
                           mat="brass"))
    for i in range(1, 8):  # diamond sights
        if i == 4:
            continue
        for y in (-(Wt / 2 - 0.035), Wt / 2 - 0.035):
            P.append(sphere(0.008, (-Lt / 2 + i * Lt / 8, y, H + 0.031), mat="ivory", seg=8, rings=4, scale=(1, 1, 0.3)))
    for (x, y) in ((-(Lt / 2 - 0.2), -(Wt / 2 - 0.2)), ((Lt / 2 - 0.2), -(Wt / 2 - 0.2)),
                   (-(Lt / 2 - 0.2), Wt / 2 - 0.2), (Lt / 2 - 0.2, Wt / 2 - 0.2)):
        P.append(lathe([(0.0, 0.0), (0.09, 0.0), (0.1, 0.03), (0.07, 0.08), (0.06, 0.2), (0.09, 0.32), (0.065, 0.45),
                        (0.08, 0.55), (0.0, 0.58)], 24, (x, y, 0), mat="wood", smooth_profile=1))
    specs = {"wood": S("darkwood", tile=0.9, rough=(0.2, 0.35)), "felt": S("velvet_green", tile=0.4),
             "pocket": spec(None, (0.02, 0.02, 0.02), rough=(0.9, 0.95)), "brass": S("brass"),
             "ivory": S("plastic_white", color=(0.85, 0.82, 0.72))}
    return L.finalize("Furn_BilliardTable", P, specs, "furniture", size=2048, notes="9 ft pool table 2.84 x 1.6 m")


# ------------------------------------------------------------------ server rack
def build_server_rack():
    P = []
    W, D, H = 0.6, 1.07, 2.0
    for x in (-W / 2 + 0.015, W / 2 - 0.015):  # side panels
        P.append(box((0.03, D, H - 0.1), (x, 0, 0.05 + (H - 0.1) / 2), mat="frame", bv=0.004))
    P.append(box((W, D, 0.05), (0, 0, H - 0.025), mat="frame", bv=0.006))
    P.append(box((W, D, 0.05), (0, 0, 0.075), mat="frame", bv=0.006))
    for x in (-W / 2 + 0.05, W / 2 - 0.05):
        for y in (-D / 2 + 0.05, D / 2 - 0.05):
            P.append(cyl(0.02, 0.05, (x, y, 0.025), mat="frame", n=12, bv=0.004))
    P.append(box((W - 0.06, 0.01, H - 0.15), (0, D / 2 - 0.005, H / 2 + 0.025), mat="frame", bv=0.002))  # back
    for x in (-0.24, 0.24):  # mounting rails
        P.append(box((0.02, 0.02, H - 0.15), (x, -D / 2 + 0.06, H / 2 + 0.025), mat="rail", bv=0.002))
    rnd = random.Random(4)
    z = 0.12
    u = 0.04445
    while z < H - 0.2:
        n = rnd.choice([1, 1, 2, 2, 2, 4])
        h = n * u - 0.002
        if rnd.random() < 0.12:
            z += u
            continue
        kind = rnd.choice(["srv", "srv", "srv", "sw"] if n == 1 else ["srv", "srv", "stor"])
        P.append(box((0.44, 0.8, h), (0, -0.05, z + h / 2), mat="chassis", bv=0.002))
        P.append(box((0.48, 0.006, h), (0, -D / 2 + 0.055, z + h / 2), mat="bezel", bv=0.0015))
        if kind == "stor":
            for i in range(12):
                cx = -0.2 + (i % 6) * 0.07 + 0.035
                cz = z + (0.25 + 0.5 * (i // 6)) * h
                P.append(box((0.065, 0.004, h / 2 - 0.006), (cx - 0.035 + 0.0, -D / 2 + 0.05, cz), mat="tray",
                             bv=0.001))
                P.append(box((0.004, 0.003, 0.004), (cx - 0.005, -D / 2 + 0.047, cz + h / 4 - 0.01),
                             mat=rnd.choice(["led_g", "led_g", "led_b"]), bv=0))
        elif kind == "sw":
            for i in range(24):
                P.append(box((0.012, 0.004, 0.011), (-0.17 + i * 0.0145, -D / 2 + 0.051, z + h / 2), mat="port",
                             bv=0))
            for i in range(0, 24, 3):
                P.append(box((0.003, 0.003, 0.003), (-0.17 + i * 0.0145, -D / 2 + 0.049, z + h - 0.006), mat="led_g",
                             bv=0))
        else:
            for i in range(8):
                P.append(box((0.04, 0.004, h * 0.6), (-0.19 + i * 0.045, -D / 2 + 0.051, z + h / 2), mat="tray",
                             bv=0.001))
            P.append(box((0.004, 0.003, 0.004), (0.2, -D / 2 + 0.049, z + h - 0.008), mat="led_b", bv=0))
            P.append(box((0.004, 0.003, 0.004), (0.21, -D / 2 + 0.049, z + h - 0.008),
                         mat=rnd.choice(["led_g", "led_o"]), bv=0))
        z += n * u
    # glass front door with frame
    P.append(box((W, 0.025, H - 0.1), (0, -D / 2 - 0.0125, H / 2 + 0.025), mat="frame", bv=0.004))
    door_cut = cutter_box((W - 0.08, 0.1, H - 0.25), (0, -D / 2 - 0.0125, H / 2 + 0.025))
    boolean(P[-1], door_cut)
    P.append(box((W - 0.075, 0.006, H - 0.245), (0, -D / 2 - 0.012, H / 2 + 0.025), mat="glass", bv=0))
    P.append(box((0.02, 0.03, 0.2), (W / 2 - 0.035, -D / 2 - 0.04, 1.0), mat="rail", bv=0.006))
    P.append(text("RACK B-07", 0.025, (0, -D / 2 - 0.0255, H - 0.06), (90, 0, 0), mat="label"))
    specs = {"frame": S("paint_black", tile=0.8, wear=0.1, scratch=0.05),
             "rail": S("galvanized", tile=0.4), "chassis": S("galvanized", tile=0.6),
             "bezel": S("plastic_black", color=(0.04, 0.04, 0.045)), "tray": S("plastic_grey", color=(0.12, 0.12, 0.13)),
             "port": spec(None, (0.01, 0.01, 0.01)), "led_g": spec(None, (0.1, 1.0, 0.2), rough=(0.1, 0.1)),
             "led_b": spec(None, (0.1, 0.4, 1.0), rough=(0.1, 0.1)), "led_o": spec(None, (1.0, 0.5, 0.05), rough=(0.1, 0.1)),
             "glass": spec(None, (0.12, 0.14, 0.15), glass=True, rough=(0.03, 0.03), alpha=0.35), "label": spec(None, (0.85, 0.85, 0.85), rough=(0.5, 0.5))}
    return L.finalize("Furn_ServerRack", P, specs, "furniture", size=2048, max_tris=20000,
                      notes="42U rack 0.6 x 1.07 x 2.0 m with tinted glass door")


# ------------------------------------------------------------------ locker
def build_locker():
    P = []
    W, D, H = 0.4, 0.5, 1.8
    P.append(box((W, D, H - 0.1), (0, 0, 0.1 + (H - 0.1) / 2), mat="steel", bv=0.004))
    inner = cutter_box((W - 0.03, D - 0.03, H - 0.16), (0, -0.02, 0.1 + (H - 0.1) / 2))
    boolean(P[0], inner)
    P.append(box((W - 0.03, D - 0.05, 0.012), (0, 0.0, 1.5), mat="steel", bv=0.002))  # shelf
    P.append(cyl(0.008, W - 0.04, (0, 0.0, 1.42), (0, 90, 0), mat="chrome", n=10, bv=0.001))
    P.append(box((W, D - 0.02, 0.1), (0, 0.01, 0.05), mat="base", bv=0.004))
    door = [box((W - 0.006, 0.018, H - 0.12), (0, -D / 2 - 0.009, 0.11 + (H - 0.12) / 2), mat="steel", bv=0.003)]
    for k in range(6):  # vent louvres (top + bottom)
        for zb in (1.62, 0.3):
            c = cutter_box((0.2, 0.05, 0.008), (0, -D / 2 - 0.009, zb + k * 0.018))
            boolean(door[0], c)
    door.append(box((0.03, 0.03, 0.12), (W / 2 - 0.05, -D / 2 - 0.03, 1.0), mat="chrome", bv=0.008))  # handle
    door.append(cyl(0.012, 0.01, (W / 2 - 0.05, -D / 2 - 0.047, 1.08), (90, 0, 0), mat="chrome", n=16, bv=0.002))
    door.append(box((0.08, 0.004, 0.04), (0, -D / 2 - 0.0185, 1.5), mat="plate", bv=0.002))
    door.append(text("117", 0.025, (0, -D / 2 - 0.0208, 1.5), (90, 0, 0), mat="ink"))
    specs = {"steel": S("paint_grey", color=(0.2, 0.3, 0.38), tile=0.8, wear=0.35, scratch=0.25),
             "base": S("paint_black"), "chrome": S("chrome"), "plate": S("alu"), "ink": S("ink")}
    return L.finalize("Furn_Locker", P, specs, "furniture", size=1024,
                      children={"Door": (door, (-W / 2, -D / 2, 0.11))},
                      notes="steel locker 0.4 x 0.5 x 1.8 m, hollow; Door pivots on its left hinge")


BUILDERS = {
    "Furn_Wardrobe": build_wardrobe,
    "Furn_OfficeChair": build_office_chair,
    "Furn_DiningTable": build_dining_table,
    "Furn_TV": build_tv,
    "Furn_KitchenCounter": build_kitchen_counter,
    "Furn_Fridge": build_fridge,
    "Furn_BarCounter": build_bar_counter,
    "Furn_Toilet": build_toilet,
    "Furn_Sink": build_sink,
    "Furn_Bathtub": build_bathtub,
    "Furn_Rug": build_rug,
    "Furn_FloorLamp": build_floor_lamp,
    "Furn_PianoGrand": build_piano,
    "Furn_BilliardTable": build_billiard,
    "Furn_ServerRack": build_server_rack,
    "Furn_Locker": build_locker,
}

if __name__ == "__main__":
    L.run(BUILDERS, "furniture")
