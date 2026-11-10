"""Placeholder icons for the loadout (weapons and abilities): flat white glyphs on transparency,
256 px, drawn at 4x and downsampled. The team replaces them with real art (same file names).
    python Tools/Icons/make_icons.py            -> Tools/Icons/Out/T_Icon_*.png
Then import them with Tools/Editor/hitmen_import_icons.py (editor open)."""
import math
import os

from PIL import Image, ImageDraw

SIZE = 256
SS = 4
W = SIZE * SS
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Out")
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)


def P(x, y):
    """Icon space (0..100) to supersampled pixels."""
    return (x * W / 100.0, y * W / 100.0)


def poly(d, pts, fill=WHITE):
    d.polygon([P(*p) for p in pts], fill=fill)


def rect(d, x0, y0, x1, y1, fill=WHITE):
    d.rectangle([P(x0, y0), P(x1, y1)], fill=fill)


def line(d, pts, width, fill=WHITE):
    d.line([P(*p) for p in pts], fill=fill, width=int(width * W / 100.0), joint="curve")
    r = width * W / 200.0
    for p in (pts[0], pts[-1]):
        x, y = P(*p)
        d.ellipse([x - r, y - r, x + r, y + r], fill=fill)


def circle(d, cx, cy, r, fill=WHITE, outline=None, width=0):
    box = [P(cx - r, cy - r), P(cx + r, cy + r)]
    if outline:
        d.ellipse(box, outline=outline, width=int(width * W / 100.0))
    else:
        d.ellipse(box, fill=fill)


def arc(d, cx, cy, r, start, end, width):
    d.arc([P(cx - r, cy - r), P(cx + r, cy + r)], start, end, fill=WHITE, width=int(width * W / 100.0))


# ---------------------------------------------------------------------------------------------
# Weapons (side views, barrel to the right)
# ---------------------------------------------------------------------------------------------

def pistol(d):
    rect(d, 22, 38, 74, 48)                 # slide
    rect(d, 74, 40, 84, 46)                 # suppressor stub
    rect(d, 84, 39, 96, 47)                 # suppressor
    poly(d, [(26, 48), (42, 48), (38, 74), (24, 74)])  # grip
    line(d, [(42, 52), (48, 58), (42, 60)], 2.2)        # trigger guard


def smg(d):
    rect(d, 20, 40, 78, 50)
    rect(d, 78, 43, 90, 47)
    poly(d, [(30, 50), (40, 50), (37, 70), (28, 70)])
    rect(d, 46, 50, 52, 72)                 # magazine
    rect(d, 8, 42, 20, 47)                  # stock


def rifle(d, burst=False):
    rect(d, 22, 40, 74, 49)
    rect(d, 74, 43, 94, 46)
    rect(d, 34, 34, 56, 40)                 # sight rail
    poly(d, [(6, 41), (22, 40), (22, 50), (8, 56)])  # stock
    poly(d, [(28, 49), (36, 49), (33, 66), (25, 66)])
    poly(d, [(44, 49), (52, 49), (56, 68), (48, 70)])  # curved magazine
    if burst:
        for i in range(3):
            circle(d, 70 + i * 8, 60, 2.6)


def shotgun(d):
    rect(d, 24, 41, 92, 47)
    rect(d, 46, 47, 74, 52)                 # pump
    poly(d, [(4, 43), (24, 41), (24, 49), (8, 58)])
    poly(d, [(28, 47), (36, 47), (33, 63), (25, 63)])


def minigun(d):
    for i, y in enumerate((36, 42, 48)):
        rect(d, 34, y, 96, y + 4)
    rect(d, 18, 32, 36, 56)
    poly(d, [(22, 56), (30, 56), (30, 70), (20, 70)])
    rect(d, 36, 30, 40, 58)


def sniper(d):
    rect(d, 20, 44, 72, 50)
    rect(d, 72, 46, 98, 48)
    rect(d, 34, 34, 60, 40)                 # scope
    rect(d, 30, 35, 34, 39)
    rect(d, 60, 35, 64, 39)
    rect(d, 44, 40, 48, 44)
    poly(d, [(2, 44), (20, 44), (20, 52), (6, 60)])
    poly(d, [(24, 50), (32, 50), (29, 64), (21, 64)])
    line(d, [(80, 48), (76, 64)], 1.6)      # bipod
    line(d, [(80, 48), (86, 64)], 1.6)


def bow(d):
    arc(d, 30, 50, 40, -62, 62, 5)
    line(d, [(48.8, 14.7), (48.8, 85.3)], 1.2)
    line(d, [(20, 50), (88, 50)], 2.4)
    poly(d, [(88, 45), (98, 50), (88, 55)])
    poly(d, [(20, 50), (14, 44), (17, 50), (14, 56)])


def launcher(d, grenade=False, black_hole=False):
    if grenade:
        rect(d, 30, 38, 82, 52)
        circle(d, 44, 45, 11)
        rect(d, 82, 41, 92, 49)
    else:
        rect(d, 10, 38, 92, 50)
        rect(d, 92, 36, 96, 52)
        rect(d, 4, 36, 10, 52)
    poly(d, [(36, 50), (44, 50), (42, 66), (34, 66)])
    poly(d, [(54, 50), (60, 50), (60, 62), (54, 62)])
    if black_hole:
        circle(d, 74, 24, 10, outline=WHITE, width=3)
        circle(d, 74, 24, 4)


def knife(d):
    poly(d, [(36, 46), (92, 40), (96, 44), (36, 54)])   # blade
    rect(d, 30, 40, 36, 60)                              # guard
    rect(d, 8, 46, 30, 54)                               # handle


WEAPONS = {
    "Pistol": pistol, "SMG": smg, "AssaultRifle": rifle, "BurstRifle": lambda d: rifle(d, True),
    "Shotgun": shotgun, "Minigun": minigun, "Sniper": sniper, "Bow": bow,
    "RocketLauncher": launcher, "GrenadeLauncher": lambda d: launcher(d, grenade=True),
    "BlackHole": lambda d: launcher(d, grenade=True, black_hole=True), "Sword": knife,
}


# ---------------------------------------------------------------------------------------------
# Abilities (symbols)
# ---------------------------------------------------------------------------------------------

def figure(d, x, y, s=1.0, fill=WHITE):
    circle(d, x, y - 22 * s, 7 * s, fill=fill)
    poly(d, [(x - 9 * s, y - 13 * s), (x + 9 * s, y - 13 * s), (x + 7 * s, y + 8 * s), (x - 7 * s, y + 8 * s)], fill=fill)
    poly(d, [(x - 7 * s, y + 8 * s), (x - 1 * s, y + 8 * s), (x - 2 * s, y + 30 * s), (x - 8 * s, y + 30 * s)], fill=fill)
    poly(d, [(x + 1 * s, y + 8 * s), (x + 7 * s, y + 8 * s), (x + 8 * s, y + 30 * s), (x + 2 * s, y + 30 * s)], fill=fill)


def blink(d):
    for i, a in enumerate((70, 140, 255)):
        figure(d, 26 + i * 24, 50, 0.8, fill=(255, 255, 255, a))


def double_jump(d):
    for y in (70, 46):
        poly(d, [(30, y), (50, y - 20), (70, y), (62, y), (50, y - 10), (38, y)])
    rect(d, 26, 82, 74, 86)


def jetpack(d):
    rect(d, 34, 18, 48, 56)
    rect(d, 52, 18, 66, 56)
    for x in (41, 59):
        poly(d, [(x - 6, 58), (x + 6, 58), (x, 86)])


def dash(d):
    figure(d, 64, 46, 0.85)
    for i, y in enumerate((36, 48, 60)):
        line(d, [(12 + i * 4, y), (44, y)], 3)


def hook(d):
    arc(d, 50, 66, 16, 0, 180, 5)
    line(d, [(66, 66), (66, 20)], 5)
    poly(d, [(28, 66), (40, 56), (40, 72)])
    line(d, [(66, 20), (80, 10)], 2)


def transposition(d):
    line(d, [(20, 38), (78, 38)], 4)
    poly(d, [(76, 30), (88, 38), (76, 46)])
    line(d, [(80, 62), (22, 62)], 4)
    poly(d, [(24, 54), (12, 62), (24, 70)])


def pill(d):
    img = Image.new("RGBA", (W, W), CLEAR)
    dd = ImageDraw.Draw(img)
    dd.rounded_rectangle([P(18, 38), P(82, 62)], radius=12 * W / 100.0, fill=WHITE)
    dd.rectangle([P(50, 36), P(51.5, 64)], fill=CLEAR)
    img = img.rotate(-35, resample=Image.BICUBIC)
    d._image.alpha_composite(img)


def invisibility(d):
    for i in range(7):
        a = 255 if i % 2 == 0 else 0
        y0 = 18 + i * 10
        rect(d, 0, y0, 100, y0 + 10, fill=(0, 0, 0, 0))
    figure(d, 50, 50, 1.1, fill=(255, 255, 255, 110))
    for x in (30, 50, 70):
        line(d, [(x - 8, 88), (x + 8, 12)], 1.4)


def armor(d):
    poly(d, [(50, 12), (82, 24), (78, 58), (50, 88), (22, 58), (18, 24)])
    poly(d, [(50, 22), (72, 30), (69, 55), (50, 76)], fill=(0, 0, 0, 0))


def c4(d):
    for i in range(3):
        rect(d, 22, 30 + i * 13, 70, 40 + i * 13)
    rect(d, 70, 36, 82, 62)
    line(d, [(82, 40), (92, 26)], 2)
    circle(d, 76, 49, 2.5, fill=(0, 0, 0, 255))


def stone(d):
    poly(d, [(26, 64), (32, 40), (52, 30), (72, 38), (78, 60), (60, 74), (38, 74)])
    for i, r in enumerate((10, 18)):
        arc(d, 50, 54, 30 + r, 200, 340, 2)


def remote_distraction(d):
    rect(d, 40, 40, 60, 84)
    circle(d, 50, 50, 4, fill=(0, 0, 0, 255))
    line(d, [(50, 40), (50, 26)], 2)
    for r in (10, 18, 26):
        arc(d, 50, 26, r, 210, 330, 2.4)


def sleep_dart(d):
    line(d, [(16, 76), (70, 30)], 3.5)
    poly(d, [(66, 24), (84, 18), (76, 36)])
    poly(d, [(16, 76), (12, 66), (22, 70)])
    for i, (x, y, s) in enumerate(((74, 60, 8), (84, 72, 6))):
        line(d, [(x, y), (x + s, y), (x, y + s), (x + s, y + s)], 1.8)


def charm(d):
    circle(d, 38, 40, 14)
    circle(d, 62, 40, 14)
    poly(d, [(24.5, 44), (75.5, 44), (50, 80)])


def flashbang(d):
    circle(d, 50, 56, 16)
    rect(d, 46, 30, 54, 40)
    for a in range(0, 360, 45):
        r0, r1 = 24, 38
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        line(d, [(50 + ca * r0, 56 + sa * r0), (50 + ca * r1, 56 + sa * r1)], 2.6)


def smoke_grenade(d):
    rect(d, 40, 50, 60, 84)
    rect(d, 44, 44, 56, 50)
    for cx, cy, r in ((38, 30, 12), (54, 24, 14), (68, 34, 11)):
        circle(d, cx, cy, r, fill=(255, 255, 255, 170))


def lighter(d):
    rect(d, 36, 52, 64, 86)
    rect(d, 40, 44, 58, 52)
    poly(d, [(50, 14), (60, 30), (56, 42), (44, 42), (40, 30)])


def multi_kill(d):
    for x in (28, 50, 72):
        circle(d, x, 50, 10, outline=WHITE, width=2.2)
        circle(d, x, 50, 3)
    line(d, [(28, 50), (72, 50)], 1.4)


def trap(d):
    arc(d, 50, 60, 30, 180, 360, 4)
    for i in range(7):
        x = 24 + i * 8.5
        poly(d, [(x - 3, 60), (x + 3, 60), (x, 48)])
    rect(d, 16, 60, 84, 66)


def wind_mine(d):
    circle(d, 50, 66, 12)
    for i, r in enumerate((10, 18, 26)):
        arc(d, 50 + i * 4, 40 - i * 4, r, 150, 300, 2.6)


def poison_vial(d):
    poly(d, [(40, 20), (60, 20), (60, 40), (74, 70), (66, 84), (34, 84), (26, 70), (40, 40)])
    poly(d, [(36, 62), (64, 62), (70, 72), (64, 80), (36, 80), (30, 72)], fill=(0, 0, 0, 0))
    circle(d, 50, 72, 5)


ABILITIES = {
    "Blink": blink, "DoubleJump": double_jump, "Jetpack": jetpack, "Dash": dash, "Hook": hook,
    "Transposition": transposition, "Pill": pill, "Invisibility": invisibility, "Armor": armor, "C4": c4,
    "Stone": stone, "RemoteDistraction": remote_distraction, "SleepDart": sleep_dart, "Charm": charm,
    "Flashbang": flashbang, "SmokeGrenade": smoke_grenade, "Lighter": lighter, "MultiKill": multi_kill,
    "Trap": trap, "WindMine": wind_mine, "PoisonVial": poison_vial,
}


def render(name, painter):
    image = Image.new("RGBA", (W, W), CLEAR)
    draw = ImageDraw.Draw(image)
    painter(draw)
    image = image.resize((SIZE, SIZE), Image.LANCZOS)
    image.save(os.path.join(OUT, name + ".png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for key, painter in WEAPONS.items():
        render("T_Icon_Weapon_" + key, painter)
    for key, painter in ABILITIES.items():
        render("T_Icon_Ability_" + key, painter)
    print("icons:", len(WEAPONS) + len(ABILITIES), "->", OUT)
