# Author : Baptiste Vu Liem
"""Procedural realistic weapons and throwables (W_*), baked PBR (see pbr_lib).

    blender -b --factory-startup --python Tools/Blender/props/build_weapons.py -- [W_Pistol ...]

Barrel / front along Blender -Y (Unity +Z), up +Z, pivot at the hand ("Grip" empty at origin),
"Muzzle" empty at the barrel tip. Throwables are pivoted at their centre.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pbr_lib as L  # noqa: E402
from pbr_lib import LIB, box, cyl, dup, extrude, lathe, sphere, sweep, boolean, cutter_box, spec, text, wrap_text  # noqa: E402,E501


def S(name, **kw):
    return dict(LIB[name], **kw)


# =================================================================== pistol family
def _pistol_parts(suppressed=False):
    P = []
    # --- slide (y forward negative)
    slide = extrude([(-0.170, 0.047), (-0.170, 0.071), (-0.166, 0.075), (0.031, 0.075), (0.035, 0.071),
                     (0.035, 0.047)], 0.0255, mat="slide", bv=0.0018)
    for i in range(9):  # rear serrations, both sides
        y = 0.006 + i * 0.0032
        boolean(slide, cutter_box((0.04, 0.0012, 0.022), (0, y, 0.062), (0, 0, 0)))
    for i in range(5):  # front serrations
        y = -0.150 + i * 0.0032
        boolean(slide, cutter_box((0.04, 0.0012, 0.018), (0, y, 0.064)))
    boolean(slide, cutter_box((0.016, 0.034, 0.02), (0.006, -0.042, 0.079)))      # ejection port
    boolean(slide, cyl(0.0068, 0.03, (0, -0.17, 0.0615), (90, 0, 0), mat="_cut", bv=0))  # muzzle hole
    P.append(slide)
    # barrel / chamber hood visible in the port + muzzle crown
    P.append(lathe([(0, 0.12), (0.0048, 0.12), (0.0048, 0.0), (0.0066, 0.0), (0.0066, 0.15), (0, 0.15)], 24,
                   (0, -0.168, 0.0615), (-90, 0, 0), mat="barrel"))
    P.append(box((0.013, 0.036, 0.012), (0.0, -0.042, 0.066), mat="barrel", bv=0.001))
    # sights
    P.append(extrude([(-0.162, 0.074), (-0.162, 0.080), (-0.156, 0.080), (-0.152, 0.074)], 0.0035, mat="slide",
                     bv=0.0006))
    rear = box((0.022, 0.008, 0.007), (0, 0.026, 0.078), mat="slide", bv=0.001)
    boolean(rear, cutter_box((0.0035, 0.02, 0.006), (0, 0.026, 0.0815)))
    P.append(rear)
    for x in (-0.006, 0.006):
        P.append(cyl(0.0011, 0.0012, (x, 0.0218, 0.0795), (90, 0, 0), mat="dot", n=10, bv=0))
    P.append(cyl(0.0011, 0.0012, (0, -0.1621, 0.0785), (90, 0, 0), mat="dot", n=10, bv=0))
    # --- frame (polymer)
    frame = extrude([(-0.163, 0.0465), (0.033, 0.0465), (0.041, 0.041), (0.041, 0.031), (0.036, 0.025),
                     (0.067, -0.074), (0.066, -0.084), (0.031, -0.088), (0.022, -0.068), (0.017, -0.050),
                     (0.010, -0.034), (0.005, -0.016), (-0.001, 0.010), (-0.004, 0.018), (-0.050, 0.018),
                     (-0.058, 0.020), (-0.062, 0.026), (-0.060, 0.031), (-0.163, 0.031), (-0.166, 0.038)],
                    0.0285, mat="frame", bv=0.0025, angle=30)
    boolean(frame, extrude([(-0.050, 0.0215), (-0.010, 0.0215), (-0.006, 0.026), (-0.006, 0.0345),
                            (-0.050, 0.0345), (-0.055, 0.029)], 0.05, mat="_cut", bv=0))
    for y in (-0.135, -0.122, -0.109):  # accessory rail slots
        boolean(frame, cutter_box((0.04, 0.004, 0.004), (0, y, 0.0325)))
    boolean(frame, cutter_box((0.012, 0.03, 0.03), (0, -0.033, 0.037)))  # trigger slot
    P.append(frame)
    # grip panels with stippling (separate material)
    for x in (-0.0146, 0.0146):
        g = extrude([(0.032, 0.015), (0.058, -0.068), (0.034, -0.078), (0.022, -0.060), (0.012, -0.030),
                     (0.010, 0.006)], 0.0012, mat="grip", bv=0.0004, plane="YZ")
        L.place(g, (x, 0, 0))
        P.append(g)
    # magazine base plate
    P.append(extrude([(0.030, -0.086), (0.067, -0.082), (0.069, -0.092), (0.029, -0.095)], 0.030, mat="frame",
                     bv=0.0015))
    # trigger + safety blade
    P.append(extrude([(-0.031, 0.036), (-0.026, 0.036), (-0.023, 0.028), (-0.025, 0.021), (-0.029, 0.0205),
                      (-0.028, 0.026)], 0.0055, mat="frame", bv=0.0007))
    # slide stop, takedown
    P.append(box((0.003, 0.016, 0.004), (-0.0155, 0.004, 0.044), mat="barrel", bv=0.0008))
    P.append(box((0.031, 0.009, 0.003), (0, -0.040, 0.0425), mat="barrel", bv=0.0006))
    # recoil spring guide visible at the muzzle
    P.append(cyl(0.0035, 0.004, (0, -0.1685, 0.051), (90, 0, 0), mat="barrel", n=16, bv=0.0005))
    P.append(text("9x19", 0.0045, (0.01285, -0.11, 0.061), (90, 0, 90), mat="engrave"))
    P.append(text("GV-17   GEN 5", 0.0028, (-0.01285, -0.09, 0.055), (90, 0, -90), mat="engrave"))
    P.append(text("BVL 40871", 0.0024, (0.0144, -0.10, 0.0388), (90, 0, 90), mat="engrave"))
    muzzle_y = -0.172
    if suppressed:
        L_sup = 0.165
        P.append(cyl(0.0058, 0.016, (0, -0.176, 0.0615), (90, 0, 0), mat="barrel", n=20, bv=0.0004))  # thread
        y0 = -0.182
        prof = [(0.0, 0.0), (0.006, 0.0), (0.006, 0.003), (0.0155, 0.004), (0.0165, 0.008), (0.0165, 0.022),
                (0.0158, 0.023), (0.0158, 0.027), (0.0165, 0.028), (0.0165, L_sup - 0.008), (0.0155, L_sup - 0.003),
                (0.0068, L_sup - 0.001), (0.0056, L_sup), (0.0056, L_sup - 0.02), (0.0, L_sup - 0.02)]
        P.append(lathe(prof, 32, (0, y0, 0.0615), (90, 0, 0), mat="suppressor"))
        muzzle_y = y0 - L_sup
    specs = {
        "slide": S("gunmetal"),
        "barrel": S("blued", color=(0.12, 0.115, 0.11)),
        "frame": S("polymer"),
        "grip": S("polymer", grain=0.9, rough=(0.7, 0.85)),
        "dot": spec(None, (0.015, 0.015, 0.015), rough=(0.6, 0.7)),
        "suppressor": S("gunmetal", color=(0.04, 0.04, 0.042), wear=0.35, rough=(0.4, 0.6)),
        "engrave": S("steel", color=(0.3, 0.3, 0.31), scratch=0.0),
    }
    return P, specs, muzzle_y


def build_pistol(name, suppressed):
    P, specs, my = _pistol_parts(suppressed)
    grip = (0, 0.040, -0.030)
    return L.finalize(name, P, specs, "weapon", size=2048, view="weapon", pivot=grip,
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, my - grip[1], 0.0615 - grip[2])})


# =================================================================== SMG (MP5K-style)
def build_smg():
    P = []
    # upper receiver: stamped steel, rounded top
    rec = extrude([(-0.165, 0.022), (-0.165, 0.060), (-0.150, 0.072), (0.120, 0.072), (0.135, 0.062),
                   (0.135, 0.022)], 0.033, mat="receiver", bv=0.006, seg=3, angle=30)
    boolean(rec, cutter_box((0.02, 0.055, 0.016), (0.012, -0.03, 0.05)))  # ejection port
    for y in (-0.12, -0.105):  # stamped ribs (pressings)
        P.append(box((0.0345, 0.006, 0.03), (0, y, 0.045), mat="receiver", bv=0.002))
    P.append(rec)
    # cocking tube + barrel
    P.append(cyl(0.0125, 0.095, (0, -0.1875, 0.060), (90, 0, 0), mat="receiver", n=24, bv=0.002))
    P.append(cyl(0.0045, 0.03, (0.012, -0.205, 0.064), (0, 90, 0), mat="steel", n=12, bv=0.001))  # cocking handle
    P.append(sphere(0.0055, (0.027, -0.205, 0.064), mat="frame", seg=12, rings=8))
    P.append(lathe([(0.0, 0.0), (0.0042, 0.0), (0.0042, 0.003), (0.010, 0.003), (0.010, 0.05), (0.0125, 0.055),
                    (0.0125, 0.075), (0.010, 0.078), (0.0, 0.078)], 24, (0, -0.165, 0.040), (90, 0, 0),
                   mat="steel"))
    P.append(lathe([(0.0068, 0.0005), (0.0042, 0.0005), (0.0042, 0.02), (0, 0.02)], 16, (0, -0.2435, 0.040),
                   (-90, 0, 0), mat="dark"))
    # front sight hood (ring on post) + rear drum
    P.append(box((0.008, 0.012, 0.022), (0, -0.228, 0.082), mat="receiver", bv=0.001))
    ring = lathe([(0.011, 0.0), (0.011, 0.01), (0.008, 0.01), (0.008, 0.0), (0.011, 0.0)], 24,
                 (0, -0.223, 0.096), (90, 0, 0), mat="receiver")
    P.append(ring)
    P.append(box((0.002, 0.004, 0.012), (0, -0.228, 0.094), mat="receiver", bv=0.0004))
    P.append(cyl(0.013, 0.022, (0, 0.10, 0.088), (0, 90, 0), mat="receiver", n=20, bv=0.002))
    P.append(box((0.026, 0.03, 0.012), (0, 0.10, 0.076), mat="receiver", bv=0.002))
    # top rail (claw mount)
    rail = box((0.021, 0.20, 0.008), (0, -0.04, 0.077), mat="steel", bv=0.0008)
    for i in range(16):
        boolean(rail, cutter_box((0.03, 0.005, 0.004), (0, -0.13 + i * 0.012, 0.081)))
    P.append(rail)
    # rear end cap with sling loop
    P.append(extrude([(0.130, 0.020), (0.130, 0.068), (0.150, 0.062), (0.155, 0.030), (0.150, 0.020)], 0.036,
                     mat="frame", bv=0.003))
    P.append(sweep([(0.0, 0.150, 0.03), (0.0, 0.172, 0.040), (0.0, 0.165, 0.058), (0.0, 0.150, 0.06)], 0.002,
                   mat="steel", smooth=True))
    # lower: trigger group + pistol grip (polymer)
    low = extrude([(-0.040, 0.023), (0.105, 0.023), (0.105, 0.010), (0.080, 0.002), (0.098, -0.090),
                   (0.093, -0.098), (0.060, -0.098), (0.055, -0.080), (0.049, -0.050), (0.044, -0.012),
                   (0.040, -0.006), (-0.012, -0.006), (-0.020, 0.0), (-0.024, 0.010), (-0.040, 0.012)],
                  0.030, mat="frame", bv=0.003, angle=30)
    boolean(low, extrude([(-0.012, -0.001), (0.036, -0.001), (0.040, 0.004), (0.040, 0.016), (-0.016, 0.016),
                          (-0.017, 0.006)], 0.06, mat="_cut", bv=0))
    P.append(low)
    for x in (-0.0155, 0.0155):  # grip texture panels
        g = extrude([(0.084, -0.004), (0.093, -0.088), (0.064, -0.090), (0.052, -0.020)], 0.0012, mat="grip",
                    bv=0.0004)
        L.place(g, (x, 0, 0))
        P.append(g)
    P.append(extrude([(0.006, 0.012), (0.012, 0.012), (0.015, 0.003), (0.012, -0.0), (0.008, 0.001),
                      (0.009, 0.006)], 0.006, mat="steel", bv=0.0006))  # trigger
    P.append(cyl(0.008, 0.003, (0.0165, 0.06, 0.012), (0, 90, 0), mat="steel", n=16, bv=0.0006))  # selector
    P.append(box((0.003, 0.016, 0.004), (0.018, 0.06 - 0.008, 0.016), (35, 0, 0), mat="steel", bv=0.0008))
    # magazine well + curved magazine
    P.append(box((0.030, 0.040, 0.03), (0, -0.035, 0.010), mat="receiver", bv=0.003))
    mag_pts_front, mag_pts_back = [], []
    for i in range(9):
        t = i / 8
        ang = math.radians(-4 - 22 * t)
        length = 0.165 * t
        # arc centre line from (y=-0.035, z=0) going down & forward
        cy = -0.035 - math.sin(math.radians(11 * t)) * 0.10 * t - 0.012 * t
        cz = -length
        hw = 0.018
        mag_pts_front.append((cy - hw - 0.004 * t, cz))
        mag_pts_back.append((cy + hw - 0.004 * t, cz))
    mag = extrude(mag_pts_front + [(mag_pts_front[-1][0] - 0.002, mag_pts_front[-1][1] - 0.008),
                                   (mag_pts_back[-1][0] + 0.002, mag_pts_back[-1][1] - 0.008)]
                  + list(reversed(mag_pts_back)), 0.022, mat="mag", bv=0.002)
    P.append(mag)
    # vertical foregrip
    P.append(lathe([(0.0, 0.0), (0.016, 0.0), (0.018, 0.006), (0.016, 0.012), (0.0165, 0.04), (0.015, 0.075),
                    (0.018, 0.085), (0.018, 0.09), (0.0, 0.09)], 24, (0, -0.205, -0.075), (0, 0, 0),
                   mat="frame", smooth_profile=1))
    P.append(box((0.03, 0.05, 0.025), (0, -0.19, 0.018), mat="frame", bv=0.004))
    specs = {
        "receiver": S("gunmetal", color=(0.045, 0.046, 0.05)),
        "steel": S("blued"),
        "frame": S("polymer"),
        "grip": S("polymer", grain=0.9, rough=(0.7, 0.85)),
        "mag": S("gunmetal", color=(0.06, 0.06, 0.06), rough=(0.45, 0.65)),
        "dark": spec(None, (0.01, 0.01, 0.01), rough=(0.6, 0.7)),
    }
    grip = (0, 0.075, -0.045)
    return L.finalize("W_Smg", P, specs, "weapon", size=2048, view="weapon", pivot=grip,
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, -0.2435 - grip[1], 0.040 - grip[2])})


# =================================================================== shotgun (pump)
def build_shotgun():
    P = []
    bz = 0.050  # barrel axis height
    # receiver (steel), slightly rounded
    rec = extrude([(-0.12, 0.012), (-0.12, 0.062), (-0.11, 0.068), (0.105, 0.068), (0.12, 0.058), (0.12, 0.010),
                   (0.08, 0.004), (-0.06, 0.004)], 0.036, mat="receiver", bv=0.004, seg=3)
    boolean(rec, cutter_box((0.02, 0.07, 0.022), (0.016, -0.03, 0.045)))  # ejection port
    boolean(rec, cutter_box((0.022, 0.08, 0.02), (0.0, -0.03, 0.006)))   # loading port
    P.append(rec)
    # barrel w/ vent rib
    P.append(lathe([(0.0115, 0.0), (0.0115, 0.48), (0.0108, 0.49), (0.0092, 0.49), (0.0092, 0.0)], 32,
                   (0, -0.12, bz), (90, 0, 0), mat="barrel"))
    P.append(lathe([(0.0125, 0.0), (0.0125, 0.05), (0.0115, 0.06)], 32, (0, -0.12, bz), (90, 0, 0), mat="barrel"))
    rib = box((0.007, 0.46, 0.003), (0, -0.36, bz + 0.0135), mat="barrel", bv=0.0005)
    P.append(rib)
    for i in range(18):
        P.append(box((0.004, 0.006, 0.003), (0, -0.14 - i * 0.025, bz + 0.011), mat="barrel", bv=0))
    P.append(sphere(0.0018, (0, -0.605, bz + 0.0165), mat="bead", seg=10, rings=6))
    # magazine tube + cap + barrel band
    P.append(cyl(0.0105, 0.42, (0, -0.33, bz - 0.026), (90, 0, 0), mat="barrel", n=24, bv=0.001))
    P.append(lathe([(0.0, 0.0), (0.012, 0.0), (0.0125, 0.004), (0.0125, 0.03), (0.010, 0.036), (0.0, 0.036)], 24,
                   (0, -0.54, bz - 0.026), (90, 0, 0), mat="receiver"))
    P.append(extrude([(-0.512, bz - 0.04), (-0.512, bz + 0.012), (-0.5, bz + 0.012), (-0.5, bz - 0.04)], 0.026,
                     mat="receiver", bv=0.003))
    # pump forend (polymer) with grooves
    pump_prof = [(0.0145, 0.0)]
    for i in range(10):
        z0 = 0.012 + i * 0.016
        pump_prof += [(0.021, z0), (0.021, z0 + 0.010), (0.0195, z0 + 0.0115), (0.0195, z0 + 0.016)]
    pump_prof += [(0.021, 0.19), (0.021, 0.2), (0.0145, 0.205)]
    pump = lathe(pump_prof, 32, (0, -0.15, bz - 0.022), (90, 0, 0), mat="pump")
    L.place(pump, (0, 0, 0), scale=None)
    P.append(pump)
    # trigger group + guard (alloy, black)
    tg = extrude([(-0.02, 0.006), (0.115, 0.006), (0.12, -0.002), (0.10, -0.008), (0.06, -0.012), (0.03, -0.035),
                  (0.0, -0.038), (-0.02, -0.03), (-0.025, -0.01)], 0.022, mat="frame", bv=0.002)
    boolean(tg, extrude([(-0.010, -0.002), (0.045, -0.002), (0.035, -0.022), (0.005, -0.028), (-0.012, -0.022)],
                        0.05, mat="_cut", bv=0))
    P.append(tg)
    P.append(extrude([(0.012, 0.0), (0.018, 0.0), (0.021, -0.012), (0.017, -0.019), (0.014, -0.018),
                      (0.016, -0.009)], 0.006, mat="receiver", bv=0.0006))
    P.append(cyl(0.005, 0.03, (0, 0.10, 0.072), (90, 0, 0), mat="frame", n=12, bv=0.001))  # tang safety
    # stock (walnut) - one piece with pistol-grip-free straight wrist
    stock = extrude([(0.118, 0.064), (0.16, 0.058), (0.30, 0.052), (0.44, 0.046), (0.445, 0.044),
                     (0.445, -0.095), (0.435, -0.098), (0.33, -0.062), (0.24, -0.04), (0.19, -0.036),
                     (0.15, -0.028), (0.128, -0.012), (0.118, 0.004)], 0.042, mat="wood", bv=0.011, seg=4,
                    angle=25)
    L.taper_x(stock, lambda y, z: 0.78 + 0.22 * min(max((y - 0.15) / 0.2, 0.0), 1.0))
    P.append(stock)
    P.append(extrude([(0.445, 0.046), (0.47, 0.046), (0.47, -0.098), (0.445, -0.098)], 0.043, mat="pad",
                     bv=0.007, seg=3))
    specs = {
        "receiver": S("blued", color=(0.05, 0.05, 0.055)),
        "barrel": S("blued", color=(0.06, 0.06, 0.065)),
        "frame": S("gunmetal", color=(0.03, 0.03, 0.03)),
        "pump": S("polymer", color=(0.03, 0.03, 0.03), grain=0.4),
        "wood": spec("walnut_veneer", (0.3, 0.15, 0.07), 0.3, "tint", 1.5, (0.2, 0.4), 0.0, 0.3, wear=0.3,
                     wear_color=(0.45, 0.28, 0.15), scratch=0.1),
        "pad": S("rubber"),
        "bead": S("brass"),
    }
    grip = (0, 0.15, -0.0)
    return L.finalize("W_Shotgun", P, specs, "weapon", size=2048, view="weapon", pivot=grip,
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, -0.61 - grip[1], bz - grip[2])})


# =================================================================== RPG-7 style launcher
def build_rocket():
    P = []
    tz = 0.0
    # launch tube with breech flare (venturi) along -Y
    tube = [(0.0215, -0.40), (0.0225, -0.39), (0.0225, 0.12), (0.027, 0.13), (0.027, 0.16), (0.0225, 0.17),
            (0.0225, 0.30), (0.030, 0.33), (0.042, 0.40), (0.045, 0.42), (0.045, 0.43), (0.040, 0.43),
            (0.028, 0.36), (0.0195, 0.30), (0.0195, -0.40)]
    # profile in (r, axis) where axis maps to +Y via rotation -90? lathe axis Z -> rot (-90,0,0) maps Z->+Y
    P.append(lathe([(r, z) for r, z in tube], 32, (0, 0, tz), (-90, 0, 0), mat="tube"))
    # wooden heat guards
    for y0, y1 in ((-0.20, -0.04), (0.02, 0.12)):
        P.append(lathe([(0.0228, 0.0), (0.031, 0.004), (0.033, 0.012), (0.033, y1 - y0 - 0.012),
                        (0.031, y1 - y0 - 0.004), (0.0228, y1 - y0)], 32, (0, y0, tz), (-90, 0, 0), mat="wood"))
        for k in (0.004, y1 - y0 - 0.004):
            P.append(lathe([(0.0332, -0.002), (0.0335, 0.0), (0.0335, 0.003), (0.0332, 0.005)], 32,
                           (0, y0 + k - 0.0015, tz), (-90, 0, 0), mat="tube"))
    # pistol grip + trigger housing under the tube
    P.append(extrude([(-0.07, -0.018), (0.01, -0.018), (0.01, -0.03), (-0.005, -0.035), (0.012, -0.12),
                      (-0.008, -0.126), (-0.03, -0.124), (-0.036, -0.10), (-0.04, -0.06), (-0.05, -0.042),
                      (-0.07, -0.04)], 0.026, mat="frame", bv=0.003))
    P.append(extrude([(-0.06, -0.04), (-0.06, -0.075), (-0.04, -0.082), (-0.02, -0.075)], 0.008, mat="tube",
                     bv=0.0015))  # guard
    P.append(extrude([(-0.050, -0.040), (-0.045, -0.040), (-0.042, -0.055), (-0.047, -0.062), (-0.050, -0.058)],
                     0.006, mat="tube", bv=0.0006))  # trigger
    # rear grip
    P.append(extrude([(0.13, -0.022), (0.18, -0.022), (0.175, -0.04), (0.17, -0.11), (0.15, -0.115),
                      (0.135, -0.11), (0.14, -0.04)], 0.026, mat="frame", bv=0.004))
    # sights
    P.append(extrude([(-0.385, 0.022), (-0.385, 0.06), (-0.375, 0.062), (-0.370, 0.022)], 0.004, mat="tube",
                     bv=0.0008))
    P.append(box((0.016, 0.024, 0.03), (0, -0.03, 0.034), mat="tube", bv=0.002))
    P.append(box((0.03, 0.006, 0.012), (0, -0.03, 0.052), mat="tube", bv=0.001))
    # optic bracket on the left
    P.append(box((0.008, 0.06, 0.035), (-0.03, -0.06, 0.018), mat="tube", bv=0.002))
    # warhead (PG-7 style), front of the tube
    wy = -0.40
    war = [(0.0, 0.0), (0.006, 0.0), (0.008, 0.012), (0.012, 0.025), (0.020, 0.06), (0.032, 0.11), (0.040, 0.15),
           (0.0425, 0.17), (0.0425, 0.24), (0.040, 0.25), (0.024, 0.27), (0.021, 0.29), (0.021, 0.36)]
    P.append(lathe([(r, 0.36 - z) for r, z in war][::-1], 32, (0, wy + 0.05, tz), (90, 0, 0), mat="warhead",
                   smooth_profile=1))
    P.append(lathe([(0.0, 0.0), (0.0055, 0.0), (0.0062, 0.012), (0.0045, 0.028), (0.0, 0.03)], 16,
                   (0, wy + 0.05 - 0.36, tz), (90, 0, 0), mat="steel"))
    P.append(lathe([(0.0425, 0.0), (0.0435, 0.004), (0.0435, 0.016), (0.0425, 0.02)], 32,
                   (0, wy - 0.50 + 0.40, tz), (90, 0, 0), mat="band"))
    specs = {
        "tube": S("gunmetal", color=(0.05, 0.052, 0.05), tile=0.4),
        "wood": spec("walnut_veneer", (0.35, 0.18, 0.08), 0.3, "tint", 1.4, (0.35, 0.55), 0.0, 0.3, wear=0.4,
                     wear_color=(0.5, 0.32, 0.18), scratch=0.15),
        "frame": S("polymer", color=(0.04, 0.035, 0.03), grain=0.3),
        "warhead": S("paint_olive", tile=0.4),
        "band": spec("green_metal_rust", (0.6, 0.45, 0.05), 0.4, "tint", 0.4, (0.4, 0.6), 0.0, 0.1, wear=0.3),
        "steel": S("steel"),
    }
    grip = (0, -0.045, -0.08)
    return L.finalize("W_RocketLauncher", P, specs, "weapon", size=2048, view="weapon", pivot=grip,
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, wy + 0.05 - 0.39 - grip[1], tz - grip[2])})


# =================================================================== flamethrower
def build_flamer():
    P = []
    wz = 0.04
    # wand tube
    P.append(cyl(0.012, 0.52, (0, -0.16, wz), (90, 0, 0), mat="steel", n=24, bv=0.001))
    # nozzle + perforated igniter shroud
    P.append(lathe([(0.0, 0.0), (0.006, 0.0), (0.006, 0.01), (0.012, 0.02), (0.012, 0.06), (0.0, 0.06)], 24,
                   (0, -0.42, wz), (90, 0, 0), mat="brass"))
    sh = lathe([(0.026, 0.0), (0.026, 0.13), (0.024, 0.13), (0.024, 0.0), (0.026, 0.0)], 32, (0, -0.39, wz), (90, 0, 0),
               mat="shroud")
    for i in range(6):
        for k in range(8):
            a = k * 45 + (i % 2) * 22.5
            c = L.cyl(0.0035, 0.03, (0, 0, 0), (0, 0, 0), mat="_cut", n=10, bv=0)
            L.place(c, (0, 0, 0), (0, 90, 0))
            L.place(c, (0.025 * math.cos(math.radians(a)), -0.40 - i * 0.018, wz + 0.025 * math.sin(math.radians(a))),
                    (0, -a, 0))
            boolean(sh, c)
    P.append(sh)
    P.append(lathe([(0.012, 0.0), (0.028, 0.0), (0.028, 0.012), (0.012, 0.012)], 32, (0, -0.38, wz), (90, 0, 0),
                   mat="steel"))
    # igniter box + spark plug lead
    P.append(box((0.03, 0.06, 0.03), (0, -0.33, wz + 0.028), mat="body", bv=0.003))
    P.append(sweep([(0.0, -0.31, wz + 0.04), (0.0, -0.25, wz + 0.05), (0.01, -0.18, wz + 0.03),
                    (0.012, -0.08, wz + 0.02)], 0.003, mat="hose"))
    # valve body + pistol grip
    P.append(box((0.036, 0.09, 0.05), (0, 0.06, wz), mat="body", bv=0.004))
    P.append(extrude([(0.03, wz - 0.02), (0.09, wz - 0.02), (0.11, wz - 0.12), (0.09, wz - 0.13),
                      (0.06, wz - 0.125), (0.055, wz - 0.06), (0.03, wz - 0.03)], 0.03, mat="grip", bv=0.004))
    P.append(extrude([(0.005, wz - 0.025), (0.035, wz - 0.025), (0.035, wz - 0.07), (0.02, wz - 0.072)], 0.006,
                     mat="steel", bv=0.001))  # guard
    P.append(extrude([(0.012, wz - 0.025), (0.018, wz - 0.025), (0.022, wz - 0.05), (0.017, wz - 0.055)], 0.006,
                     mat="steel", bv=0.0008))  # trigger
    # front grip on the wand
    P.append(lathe([(0.0, 0.0), (0.014, 0.0), (0.016, 0.01), (0.015, 0.09), (0.017, 0.10), (0.0, 0.10)], 20,
                   (0, -0.18, wz - 0.11), (0, 0, 0), mat="grip"))
    P.append(box((0.03, 0.04, 0.03), (0, -0.18, wz - 0.012), mat="body", bv=0.003))
    # fuel tank under the wand (pressurised cylinder) + straps
    tz = wz - 0.085
    P.append(lathe([(0.0, 0.0), (0.03, 0.002), (0.045, 0.012), (0.05, 0.03), (0.05, 0.25), (0.045, 0.268),
                    (0.03, 0.278), (0.0, 0.28)], 32, (0, -0.06, tz), (-90, 0, 0), mat="tank", smooth_profile=1))
    for y in (-0.01, 0.17):
        P.append(lathe([(0.0505, 0.0), (0.054, 0.0), (0.054, 0.018), (0.0505, 0.018)], 32, (0, y, tz), (-90, 0, 0),
                       mat="body"))
        P.append(box((0.012, 0.018, 0.04), (0, y + 0.009, tz + 0.06), mat="body", bv=0.002))
    # valve + hose from tank to valve body
    P.append(cyl(0.008, 0.03, (0, 0.235, tz), (90, 0, 0), mat="brass", n=16, bv=0.001))
    P.append(cyl(0.016, 0.006, (0, 0.255, tz), (90, 0, 0), mat="steel", n=16, bv=0.001))
    P.append(sweep([(0.0, 0.25, tz), (0.0, 0.29, tz + 0.02), (0.0, 0.28, wz + 0.02), (0.0, 0.17, wz + 0.01),
                    (0.0, 0.11, wz)], 0.007, mat="hose"))
    P.append(cyl(0.022, 0.012, (-0.03, 0.02, tz + 0.04), (0, 90, 0), mat="gauge_rim", n=24, bv=0.001))  # gauge
    P.append(cyl(0.019, 0.002, (-0.0365, 0.02, tz + 0.04), (0, 90, 0), mat="glass", n=24, bv=0))
    specs = {
        "steel": S("steel", color=(0.35, 0.35, 0.36)),
        "brass": S("brass"),
        "shroud": S("blued", color=(0.08, 0.075, 0.07), wear=0.2),
        "body": S("paint_black"),
        "grip": S("rubber"),
        "hose": S("rubber", color=(0.02, 0.02, 0.02)),
        "tank": S("paint_red"),
        "gauge_rim": S("chrome"),
        "glass": S("glass"),
    }
    grip = (0, 0.075, wz - 0.07)
    return L.finalize("W_Flamethrower", P, specs, "weapon", size=2048, view="weapon", pivot=grip,
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, -0.52 - grip[1], wz - grip[2])})


# =================================================================== throwables / gadgets
def build_c4():
    P = []
    P.append(box((0.055, 0.28, 0.038), (0, 0, 0.019), mat="block", bv=0.004, seg=3))
    for y in (-0.09, 0.09):  # tape bands
        P.append(box((0.057, 0.025, 0.040), (0, y, 0.019), mat="tape", bv=0.004, seg=3))
    P.append(box((0.0552, 0.11, 0.0382), (0, 0, 0.019), mat="label", bv=0.004, seg=3))
    # detonator / timer
    P.append(box((0.045, 0.07, 0.022), (0, 0.0, 0.049), mat="device", bv=0.003, seg=2))
    P.append(box((0.03, 0.035, 0.002), (0, -0.008, 0.0605), mat="lcd", bv=0.0005))
    for i, x in enumerate((-0.012, 0.0, 0.012)):
        P.append(cyl(0.0035, 0.003, (x, 0.022, 0.0605), mat="red" if i == 1 else "button", n=12, bv=0.0006))
    P.append(cyl(0.0018, 0.03, (0.016, 0.03, 0.075), mat="steel", n=8, bv=0))  # antenna stub
    # blasting cap in the block end + wires
    P.append(cyl(0.0035, 0.04, (0.0, 0.15, 0.022), (90, 0, 0), mat="steel", n=12, bv=0.0005))
    P.append(sweep([(0.002, 0.17, 0.022), (0.004, 0.19, 0.03), (0.014, 0.12, 0.058), (0.01, 0.035, 0.05)],
                   0.0016, mat="wire_r"))
    P.append(sweep([(-0.002, 0.17, 0.022), (-0.004, 0.195, 0.035), (-0.016, 0.11, 0.062), (-0.01, 0.035, 0.05)],
                   0.0016, mat="wire_b"))
    for sx, rz in ((0.0277, 90), (-0.0277, -90)):
        P.append(text("CHARGE, DEMOLITION, M112", 0.0062, (sx, 0.0, 0.027), (90, 0, rz), bold=True))
        P.append(text("1.25 LB  COMPOSITION C-4", 0.0048, (sx, 0.0, 0.017), (90, 0, rz)))
        P.append(text("LOT 2219-07", 0.0038, (sx, 0.0, 0.009), (90, 0, rz)))
    specs = {
        "ink": S("ink"),
        "block": spec("rough_linen", (0.2, 0.22, 0.12), 0.25, "tint", 0.4, (0.5, 0.7), 0.0, 0.3),
        "tape": spec("rough_linen", (0.03, 0.03, 0.03), 0.15, "tint", 0.3, (0.6, 0.8), 0.0, 0.5),
        "label": spec("rough_linen", (0.55, 0.5, 0.35), 0.2, "tint", 0.3, (0.6, 0.8), 0.0, 0.3),
        "device": S("plastic_black"),
        "lcd": spec(None, (0.15, 0.25, 0.1), rough=(0.1, 0.15)),
        "button": S("rubber", color=(0.05, 0.05, 0.05)),
        "red": spec(None, (0.6, 0.02, 0.02), rough=(0.3, 0.4)),
        "steel": S("alu"),
        "wire_r": spec(None, (0.5, 0.02, 0.02), rough=(0.4, 0.5), grain=0.05),
        "wire_b": spec(None, (0.02, 0.05, 0.4), rough=(0.4, 0.5), grain=0.05),
    }
    return L.finalize("W_C4", P, specs, "throwable", size=1024, view="top", pivot=(0, 0, 0.03),
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, -0.14, 0)})


def build_smoke():
    P = []
    h = 0.115
    P.append(lathe([(0.0, 0.0), (0.028, 0.0), (0.031, 0.003), (0.031, h - 0.003), (0.029, h), (0.0, h)], 32,
                   mat="can"))
    P.append(lathe([(0.0312, 0.098), (0.0315, 0.099), (0.0315, 0.108), (0.0312, 0.109)], 32, mat="band"))
    for line, sz, z in (("GRENADE, HAND", 0.0055, 0.083), ("SMOKE GREEN", 0.0085, 0.068), ("M18", 0.0075, 0.053),
                        ("LOT MA-04-118", 0.004, 0.038)):
        P.append(wrap_text(text(line, sz, mat="stencil"), 0.031, z))
    for k in range(4):  # emission holes on top
        a = math.radians(45 + 90 * k)
        P.append(cyl(0.004, 0.002, (0.018 * math.cos(a), 0.018 * math.sin(a), h + 0.0005), mat="dark", n=12, bv=0))
    # fuze body
    P.append(lathe([(0.0, h), (0.009, h), (0.009, h + 0.022), (0.006, h + 0.026), (0.0, h + 0.026)], 20,
                   mat="fuze"))
    # spoon (lever) down the side
    P.append(extrude([(0.0, h + 0.024), (0.0095, h + 0.024), (0.034, h + 0.012), (0.034, h - 0.07),
                      (0.0325, h - 0.075), (0.0325, h + 0.008), (0.0095, h + 0.020), (0.0, h + 0.020)],
                     0.012, mat="fuze", bv=0.0008, plane="XZ"))
    # pin + ring
    P.append(cyl(0.0012, 0.03, (0.0, 0.0, h + 0.015), (90, 0, 0), mat="ring", n=8, bv=0))
    ring = lathe([(0.012, -0.0012), (0.0136, -0.0012), (0.0136, 0.0012), (0.012, 0.0012)], 24,
                 (-0.0, -0.025, h + 0.015), (0, 90, 0), mat="ring")
    P.append(L.place(ring, (0, 0, 0)))
    specs = {
        "can": S("paint_olive", tile=0.3),
        "band": spec("green_metal_rust", (0.12, 0.45, 0.1), 0.3, "tint", 0.3, (0.45, 0.6), 0.0, 0.1, wear=0.2),
        "stencil": spec(None, (0.75, 0.7, 0.35), rough=(0.6, 0.7)),
        "dark": spec(None, (0.02, 0.02, 0.02), rough=(0.7, 0.8)),
        "fuze": S("steel", color=(0.45, 0.45, 0.42), wear=0.0),
        "ring": S("steel"),
    }
    return L.finalize("W_SmokeGrenade", P, specs, "throwable", size=1024, view="prop", pivot=(0, 0, 0.07),
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, 0, 0.07)})


def build_coin():
    P = [lathe([(0.0, 0.0002), (0.0115, 0.0002), (0.0122, 0.0), (0.01245, 0.0004), (0.01245, 0.0021),
                (0.0122, 0.0025), (0.0112, 0.0023), (0.0, 0.0021)], 48, mat="coin")]
    P.append(lathe([(0.0, 0.0021), (0.0085, 0.0021), (0.0085, 0.0023), (0.0, 0.0023)], 48, mat="coin2"))
    # reeded edge
    edge = P[0]
    for k in range(60):
        a = 360 * k / 60
        c = cutter_box((0.0006, 0.0005, 0.004))
        L.place(c, (0.01245, 0, 0.00125), (0, 0, 0))
        L.place(c, (0, 0, 0), (0, 0, a))
        boolean(edge, c)
    P.append(text("1", 0.011, (0, 0, 0.00232), mat="coin2", depth=0.00012))
    P.append(text("EURO", 0.0022, (0, -0.0062, 0.00232), mat="coin2", depth=0.0001))
    specs = {"coin": S("brass", color=(0.72, 0.58, 0.3), scratch=0.5, wear=0.3),
             "coin2": S("steel", color=(0.66, 0.66, 0.68), scratch=0.5)}
    return L.finalize("W_Coin", P, specs, "throwable", size=512, view="top", pivot=(0, 0, 0.00125),
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, -0.0125, 0)})


def build_brick():
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(0.1025, 0.215, 0.065), verts=bm.verts)
    ob = L._mesh_obj("brick", bm, "brick")
    L.bevel(ob, 0.004, 2)
    m = ob.modifiers.new("sub", "SUBSURF")
    m.subdivision_type = "SIMPLE"
    m.levels = 2
    L.apply_mods(ob)
    tex = __import__("bpy").data.textures.new("brick_noise", "CLOUDS")
    tex.noise_scale = 0.02
    d = ob.modifiers.new("disp", "DISPLACE")
    d.texture = tex
    d.strength = 0.003
    d.mid_level = 0.5
    L.apply_mods(ob)
    frog = cutter_box((0.05, 0.12, 0.02), (0, 0, 0.0325))
    L.bevel(frog, 0.006, 2)
    boolean(ob, frog)
    specs = {"brick": spec("concrete_floor_painted", (0.42, 0.13, 0.07), 0.25, "tint", 0.9, (0.75, 0.95), 0.0,
                           0.8, wear=0.5, wear_color=(0.55, 0.42, 0.33))}
    return L.finalize("W_Brick", [ob], specs, "throwable", size=1024, view="prop", pivot=(0, 0, 0),
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, -0.11, 0)})


def build_poison():
    P = []
    vial = [(0.0, 0.0), (0.0075, 0.0), (0.0085, 0.002), (0.0085, 0.045), (0.006, 0.05), (0.0055, 0.053),
            (0.0062, 0.054), (0.0062, 0.057), (0.0, 0.057)]
    P.append(lathe(vial, 32, mat="glass", smooth_profile=1))
    P.append(lathe([(0.0, 0.002), (0.0073, 0.002), (0.0073, 0.032), (0.0, 0.032)], 32, mat="liquid"))
    P.append(lathe([(0.0, 0.052), (0.0066, 0.052), (0.0068, 0.0545), (0.0068, 0.064), (0.0055, 0.065),
                    (0.0, 0.065)], 32, mat="cap"))
    P.append(lathe([(0.0, 0.064), (0.004, 0.064), (0.004, 0.0655), (0.0, 0.0655)], 20, mat="rubber"))
    P.append(lathe([(0.0087, 0.012), (0.0088, 0.013), (0.0088, 0.035), (0.0087, 0.036)], 32, mat="label"))
    for line, sz, z, m in (("KCN", 0.0042, 0.029, "ink"), ("TOXIC", 0.0028, 0.022, "red"),
                           ("10 mg/mL", 0.002, 0.016, "ink")):
        P.append(wrap_text(text(line, sz, mat=m, bold=True), 0.0088, z, lift=0.0001))
    specs = {"ink": S("ink"), "red": spec(None, (0.5, 0.02, 0.02), rough=(0.4, 0.5)),
             "glass": S("glass"), "liquid": spec(None, (0.15, 0.5, 0.08), glass=True, rough=(0.05, 0.05), alpha=0.8),
             "cap": S("alu"), "rubber": S("rubber", color=(0.3, 0.02, 0.02)),
             "label": spec("rough_linen", (0.8, 0.78, 0.7), 0.05, "tint", 0.2, (0.6, 0.7), 0.0, 0.1)}
    return L.finalize("W_Poison", P, specs, "throwable", size=512, view="prop", pivot=(0, 0, 0.032),
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, 0, 0.033)})


def build_emetic():
    P = []
    P.append(lathe([(0.0, 0.0), (0.0145, 0.0), (0.0155, 0.002), (0.0155, 0.058), (0.0145, 0.06), (0.0135, 0.062),
                    (0.0135, 0.066), (0.0, 0.066)], 32, mat="bottle_glass"))
    P.append(lathe([(0.0, 0.058), (0.0165, 0.058), (0.0168, 0.06), (0.0168, 0.078), (0.0155, 0.08), (0.0, 0.08)],
                   40, mat="cap"))
    cap = P[-1]
    for k in range(36):
        c = cutter_box((0.0012, 0.0008, 0.016))
        L.place(c, (0.0168, 0, 0.068))
        L.place(c, (0, 0, 0), (0, 0, k * 10))
        boolean(cap, c)
    P.append(lathe([(0.0156, 0.01), (0.0157, 0.011), (0.0157, 0.048), (0.0156, 0.049)], 40, mat="label"))
    import random
    rnd = random.Random(3)
    for i in range(12):  # capsules inside
        a = rnd.random() * 6.28
        r = rnd.random() * 0.004
        c = L.sphere(0.003, (r * math.cos(a), r * math.sin(a), 0.004 + i * 0.0032), mat="pill", seg=10, rings=6,
                     scale=(1, 2.2, 1))
        L.place(c, (0, 0, 0), (rnd.random() * 40, 0, rnd.random() * 180))
        P.append(c)
    P.append(lathe([(0.0157, 0.043), (0.0158, 0.0435), (0.0158, 0.048), (0.0157, 0.0485)], 40, mat="stripe"))
    for line, sz, z in (("IPECAC", 0.0062, 0.035), ("EMETIC  TABLETS  50 mg", 0.0026, 0.027),
                        ("Rx 0447-218    QTY 12", 0.0024, 0.021), ("TAKE 1 TABLET AS DIRECTED", 0.002, 0.015)):
        P.append(wrap_text(text(line, sz, mat="ink", bold=sz > 0.005), 0.0157, z, lift=0.0001))
    specs = {"bottle_glass": S("glass_amber"), "cap": S("plastic_white"), "ink": S("ink"),
             "stripe": spec(None, (0.6, 0.05, 0.03), rough=(0.5, 0.6)),
             "label": spec("rough_linen", (0.85, 0.85, 0.82), 0.05, "tint", 0.2, (0.6, 0.7), 0.0, 0.1),
             "pill": spec(None, (0.75, 0.2, 0.15), rough=(0.25, 0.3))}
    return L.finalize("W_EmeticPoison", P, specs, "throwable", size=512, view="prop", pivot=(0, 0, 0.04),
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, 0, 0.04)})


def build_remote():
    P = []
    P.append(box((0.055, 0.10, 0.028), (0, 0, 0.0), mat="body", bv=0.006, seg=3))
    P.append(box((0.045, 0.06, 0.004), (0, 0.012, -0.0145), mat="rubber", bv=0.0015))  # grip pad
    # flip guard + red button
    P.append(cyl(0.0095, 0.006, (0, -0.025, 0.016), mat="ring", n=24, bv=0.001))
    P.append(cyl(0.0075, 0.006, (0, -0.025, 0.019), mat="red", n=24, bv=0.0015))
    guard = box((0.024, 0.028, 0.012), (0, -0.025, 0.026), (-25, 0, 0), mat="cover", bv=0.002)
    P.append(guard)
    # toggle switch + LEDs
    P.append(cyl(0.005, 0.004, (0.012, 0.018, 0.015), mat="ring", n=16, bv=0.0008))
    P.append(cyl(0.0015, 0.014, (0.012, 0.016, 0.022), (-20, 0, 0), mat="ring", n=8, bv=0))
    P.append(cyl(0.0025, 0.002, (-0.012, 0.018, 0.0145), mat="led", n=12, bv=0.0004))
    P.append(cyl(0.0025, 0.002, (-0.012, 0.03, 0.0145), mat="led2", n=12, bv=0.0004))
    # antenna
    P.append(cyl(0.004, 0.012, (0.018, 0.045, 0.018), mat="body", n=12, bv=0.001))
    P.append(cyl(0.0022, 0.12, (0.018, 0.045, 0.08), mat="rubber", n=10, bv=0.0008))
    for i in range(3):
        P.append(cyl(0.0006, 0.02, (-0.01 + i * 0.01, 0.045, 0.014), (90, 0, 0), mat="dark", n=6, bv=0))
    P.append(text("ARM", 0.0045, (0.012, 0.0055, 0.01405), mat="paint"))
    P.append(text("FIRE", 0.0045, (0.0, -0.0425, 0.01405), mat="paint"))
    P.append(text("PWR", 0.0035, (-0.012, 0.0085, 0.01405), mat="paint"))
    specs = {"body": S("plastic_black", color=(0.05, 0.055, 0.05)), "rubber": S("rubber"),
             "paint": spec(None, (0.8, 0.8, 0.78), rough=(0.5, 0.6)),
             "ring": S("steel"), "red": spec(None, (0.7, 0.02, 0.02), rough=(0.3, 0.35)),
             "cover": spec(None, (0.6, 0.45, 0.02), rough=(0.2, 0.3), alpha=0.6),
             "led": spec(None, (0.05, 0.8, 0.1), rough=(0.1, 0.1)), "led2": spec(None, (0.9, 0.1, 0.02), rough=(0.1, 0.1)),
             "dark": spec(None, (0.01, 0.01, 0.01), rough=(0.6, 0.7))}
    return L.finalize("W_Remote", P, specs, "weapon", size=1024, view="top", pivot=(0, 0.02, 0),
                      empties={"Grip": (0, 0, 0), "Muzzle": (0, -0.07, 0.0)})


BUILDERS = {
    "W_Pistol": lambda: build_pistol("W_Pistol", False),
    "W_SilencedPistol": lambda: build_pistol("W_SilencedPistol", True),
    "W_Smg": build_smg,
    "W_Shotgun": build_shotgun,
    "W_RocketLauncher": build_rocket,
    "W_Flamethrower": build_flamer,
    "W_C4": build_c4,
    "W_SmokeGrenade": build_smoke,
    "W_Coin": build_coin,
    "W_Brick": build_brick,
    "W_Poison": build_poison,
    "W_EmeticPoison": build_emetic,
    "W_Remote": build_remote,
}

if __name__ == "__main__":
    L.run(BUILDERS, "weapon")
