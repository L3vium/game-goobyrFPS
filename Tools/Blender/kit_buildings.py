# Author : Baptiste Vu Liem
# Modular building kit of Goobyr FPS, four styles sharing the same piece names:
# Timber (log lodge), Stucco (Gulf city), Suburb (clapboard + brick) and Classic (stone mansion).
# Wall modules are 3.0 x 3.4 m, 0.3 m thick, origin at the bottom centre of the wall centre line,
# exterior on +Y. Openings are real boolean holes. Exports to Assets/_Game/Art/Resources/Kit/<Style>/.
import importlib
import math
import sys

KIT_DIR = globals().get("KIT_DIR", r"D:\_Project\_GamesUnity\_Isart\Goobyr FPS\Tools\Blender")
if KIT_DIR not in sys.path:
    sys.path.insert(0, KIT_DIR)
import kit_common as kc  # noqa: E402

importlib.reload(kc)
from kit_common import Kit, Piece, fixed, tinted, shade, T, R, S, rng_for  # noqa: E402

W, H, TH = 3.0, 3.4, 0.3
FY = TH / 2                 # front face of the wall slab
POINT_R = 0.62              # pointed-arch radius, in opening widths
STAIR_RUN = 5.4


# ================================================================ openings
class Opening:
    """A rectangular or arched hole in a wall. z0 = sill height, h = full height (apex for arches)."""

    def __init__(self, kind, cx, z0, w, h, arch=None):
        self.kind, self.cx, self.z0, self.w, self.h, self.arch = kind, cx, z0, w, h, arch
        off, r = self.arc(0.0)
        self.rise = 0.0 if arch is None else math.sqrt(r * r - off * off)

    @property
    def top(self):
        return self.z0 + self.h

    @property
    def spring(self):
        return self.top - self.rise

    def arc(self, pad):
        """(centre offset from the axis, radius) of the arch curve grown by pad."""
        if self.arch == "pointed":
            return POINT_R * self.w - self.w / 2, POINT_R * self.w + pad
        return 0.0, self.w / 2 + pad

    def half_width(self, z, pad=0.0):
        if self.arch is None or z <= self.spring:
            return self.w / 2 + pad
        off, r = self.arc(pad)
        dz = z - self.spring
        if dz >= r:
            return None
        hw = math.sqrt(r * r - dz * dz) - off
        return hw if hw > 0.0 else None

    def arch_height(self, x, pad=0.0):
        """Height of the arch curve above the axis offset x (for lattice bars)."""
        off, r = self.arc(pad)
        d = abs(x - self.cx) + off
        return self.spring + math.sqrt(max(r * r - d * d, 0.0))

    def span(self, z0, z1, pad=0.0):
        """X interval the opening (grown by pad) occupies inside the band [z0, z1], or None."""
        apex = self.top + pad if self.arch is None else self.spring + math.sqrt(max(self.arc(pad)[1] ** 2 - self.arc(pad)[0] ** 2, 0))
        if z1 <= self.z0 - pad or z0 >= apex:
            return None
        hw = self.half_width(max(z0, self.z0), pad)
        if hw is None:
            return None
        return self.cx - hw, self.cx + hw

    def arc_points(self, pad, segs, side):
        """Points of one side of the arch (side +1 right, -1 left) from the spring up to the apex."""
        off, r = self.arc(pad)
        amax = math.acos(min(off / r, 1.0)) if self.arch == "pointed" else math.pi / 2
        pts = []
        for i in range(segs + 1):
            a = amax * i / segs
            x = -off + r * math.cos(a)
            pts.append((self.cx + side * x, self.spring + r * math.sin(a)))
        return pts

    def outline(self, pad=0.0, segs=8):
        z0 = -0.3 if self.z0 <= 0.01 else self.z0 - pad
        hw = self.w / 2 + pad
        pts = [(self.cx - hw, z0), (self.cx + hw, z0)]
        if self.arch is None:
            return pts + [(self.cx + hw, self.top + pad), (self.cx - hw, self.top + pad)]
        right = self.arc_points(pad, segs, 1)
        left = self.arc_points(pad, segs, -1)
        return pts + right + left[::-1][1:]


def window(arch=None):
    return Opening("window", 0.0, 0.9, 1.2, 1.5, arch)


def window_tall(arch=None):
    return Opening("tall", 0.0, 0.5, 1.2, 2.4, arch)


def french():
    return Opening("french", 0.0, 0.0, 1.2, 2.4)


def door(width=1.2):
    return Opening("door" if width < 2 else "double", 0.0, 0.0, width, 2.4)


def intervals(x0, x1, z0, z1, openings, pad=0.0, min_len=0.03):
    """[x0, x1] minus every opening crossing the band [z0, z1]."""
    segs = [(x0, x1)]
    for o in openings:
        sp = o.span(z0, z1, pad)
        if sp is None:
            continue
        a, b = sp
        cut = []
        for s0, s1 in segs:
            if b <= s0 or a >= s1:
                cut.append((s0, s1))
                continue
            if a > s0:
                cut.append((s0, a))
            if b < s1:
                cut.append((b, s1))
        segs = cut
    return [(a, b) for a, b in segs if b - a > min_len]


def hbox(k, a, b, z0, z1, y0, y1, col, bevel=kc.BEVEL):
    """Axis-aligned box from its bounds."""
    k.box(((a + b) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (b - a, y1 - y0, z1 - z0), col, bevel)


def surround(k, o, pad0, pad1, y0, y1, col, col2=None, bottom=False, segs=6, bevel=kc.BEVEL):
    """Band around an opening between offsets pad0 and pad1 (jambs, head, arch ring, optional sill)."""
    hw = o.w / 2
    zb = o.z0 - pad1 if bottom else max(o.z0, 0.0)
    ztop = o.top + pad1 if o.arch is None else o.spring
    for s in (-1, 1):
        if ztop - zb < 0.01:
            break  # arch springing straight from its base: no jambs
        a, b = sorted((o.cx + s * (hw + pad0), o.cx + s * (hw + pad1)))
        hbox(k, a, b, zb, ztop, y0, y1, col, bevel)
    if o.arch is None:
        hbox(k, o.cx - hw - pad0, o.cx + hw + pad0, o.top + pad0, o.top + pad1, y0, y1, col, bevel)
    else:
        for side in (-1, 1):
            inner = o.arc_points(pad0, segs, side)
            outer = o.arc_points(pad1, segs, side)
            for i in range(segs):
                c = col2 if (col2 is not None and i % 2) else col
                quad = [inner[i], inner[i + 1], outer[i + 1], outer[i]]
                k.prism(quad, y0, y1, c, "Y", bevel)
    if bottom:
        hbox(k, o.cx - hw - pad0, o.cx + hw + pad0, o.z0 - pad1, o.z0 - pad0, y0, y1, col, bevel)


def lattice(k, x0, x1, z0, z1, y0, y1, col, step=0.11, bar=0.035, clip=None):
    """Wooden mashrabiya-like grid. clip(x) -> max z (arched heads)."""
    nx = max(int((x1 - x0) / step), 1)
    nz = max(int((z1 - z0) / step), 1)
    for i in range(1, nx):
        x = x0 + (x1 - x0) * i / nx
        top = min(z1, clip(x)) if clip else z1
        if top - z0 > 0.05:
            hbox(k, x - bar / 2, x + bar / 2, z0, top, y0, y1, col, 0.0)
    for j in range(1, nz):
        z = z0 + (z1 - z0) * j / nz
        a, b = x0, x1
        if clip:
            xs = [x0 + (x1 - x0) * t / 40 for t in range(41)]
            inside = [x for x in xs if clip(x) > z + bar]
            if not inside:
                continue
            a, b = inside[0], inside[-1]
        hbox(k, a, b, z - bar / 2, z + bar / 2, y0 + 0.005, y1 - 0.005, col, 0.0)


# ================================================================ base style
class Style:
    """Colours and shared builders. Subclasses override the hooks that give each style its character."""
    name = "Base"
    plinth_h = 0.45
    window_arch = None
    arch_kind = "round"
    tint_wall = (1.0, 1.0, 1.0)
    tint_roof = (0.7, 0.35, 0.3)
    wall_col = tinted(0.95, 0.95, 0.95)
    back_col = tinted(0.92, 0.92, 0.92)
    trim = fixed(0.95, 0.95, 0.93)
    trim2 = fixed(0.8, 0.8, 0.78)
    trim_in = fixed(0.85, 0.85, 0.82)
    stone = fixed(0.5, 0.5, 0.5)
    wood = fixed(0.4, 0.26, 0.15)
    wood_light = fixed(0.6, 0.42, 0.25)
    metal = fixed(0.2, 0.2, 0.22)
    roof_col = tinted(0.9, 0.88, 0.85)
    gable_col = fixed(0.9, 0.9, 0.88)
    ridge_col = fixed(0.35, 0.33, 0.32)
    tread_col = fixed(0.55, 0.38, 0.22)
    step_col = None      # outdoor steps (defaults to stone)
    cheek_col = None     # outdoor stair side walls (defaults to the wall colour)
    clad_out = 0.19      # outermost Y of the cladding

    def __init__(self):
        self.rng = rng_for(self.name)

    # ---------------------------------------------------------- wall assembly
    def slab_colour(self, face):
        return self.back_col if face.normal.y < -0.5 else self.wall_col

    def wall(self, openings=(), width=W, height=H, plinth=True):
        k = Kit()
        bm = kc.bm_box(width, TH, height)
        bm.transform(T(0, 0, height / 2))
        if openings:
            cutters = [kc.bm_prism(o.outline(), -1.0, 1.0, "Y") for o in openings]
            bm = kc.bm_boolean(bm, cutters)
        k.add(bm, self.slab_colour, bevel=0.0)
        if plinth:
            self.plinth(k, openings, width)
        self.cladding(k, openings, width, height, self.plinth_h if plinth else 0.0)
        self.frieze(k, openings, width, height)
        for o in openings:
            getattr(self, "dress_" + o.kind)(k, o)
            self.back_casing(k, o)
        self.baseboard(k, openings, width)
        return k

    def plinth(self, k, openings, width):
        for a, b in intervals(-width / 2, width / 2, 0.0, self.plinth_h, openings):
            hbox(k, a, b, 0.0, self.plinth_h, FY - 0.02, FY + 0.06, self.stone, 0.025)

    def cladding(self, k, openings, width, height, z0):
        pass

    def frieze(self, k, openings, width, height):
        pass

    def baseboard(self, k, openings, width):
        for a, b in intervals(-width / 2, width / 2, 0.0, 0.12, openings):
            hbox(k, a, b, 0.0, 0.12, -FY - 0.025, -FY + 0.01, self.trim_in, 0.01)

    def back_casing(self, k, o):
        surround(k, o, 0.0, 0.09, -FY - 0.03, -FY + 0.01, self.trim_in, bottom=False, bevel=0.012)
        if o.kind in ("window", "tall"):
            hbox(k, o.cx - o.w / 2 - 0.09, o.cx + o.w / 2 + 0.09, o.z0 - 0.06, o.z0, -FY - 0.06, -FY + 0.02, self.trim_in, 0.012)

    # ---------------------------------------------------------- opening dressings (defaults)
    def dress_window(self, k, o):
        surround(k, o, 0.0, 0.12, 0.0, self.clad_out + 0.04, self.trim)
        self.sill(k, o)

    def dress_tall(self, k, o):
        self.dress_window(k, o)

    def dress_french(self, k, o):
        surround(k, o, 0.0, 0.12, 0.0, self.clad_out + 0.04, self.trim)

    def dress_door(self, k, o):
        surround(k, o, 0.0, 0.14, -0.02, self.clad_out + 0.05, self.trim)
        self.step(k, o)

    def dress_double(self, k, o):
        self.dress_door(k, o)

    def dress_arch(self, k, o):
        surround(k, o, 0.0, 0.16, -0.02, self.clad_out + 0.05, self.trim, self.trim2)

    def sill(self, k, o, depth=0.14, overhang=0.12, col=None):
        col = col or self.trim
        hbox(k, o.cx - o.w / 2 - overhang, o.cx + o.w / 2 + overhang, o.z0 - 0.07, o.z0, -0.02,
             self.clad_out + depth, col, 0.015)

    def step(self, k, o):
        hbox(k, o.cx - o.w / 2 - 0.25, o.cx + o.w / 2 + 0.25, 0.0, 0.12, -FY, FY + 0.5, self.stone, 0.025)

    # ---------------------------------------------------------- balcony
    def balcony(self, k, o):
        width, depth = 2.4, 0.9
        y0, y1 = FY, FY + depth
        hbox(k, -width / 2, width / 2, -0.16, 0.02, -0.05, y1, self.stone, 0.03)
        for x in (-width / 2 + 0.25, width / 2 - 0.25):
            k.prism([(y0, -0.16), (y0 + depth * 0.8, -0.16), (y0, -0.7)], x - 0.07, x + 0.07, self.stone, "X", 0.02)
        rail = self.railing_kit(width)
        k.merge(rail, T(0, y1 - 0.06, 0.02))
        side = self.railing_kit(depth - 0.06, ends=False)
        for s in (-1, 1):
            k.merge(side, T(s * (width / 2 - 0.06), y0 + (depth - 0.06) / 2, 0.02) @ R("Z", 90))

    # ---------------------------------------------------------- railings & balusters
    def railing_kit(self, length, height=1.0, ends=True):
        k = Kit()
        posts = [-length / 2 + 0.06, length / 2 - 0.06] if ends else []
        n_mid = int(length / 1.5)
        posts += [-length / 2 + length * (i + 1) / (n_mid + 1) for i in range(n_mid)]
        for x in posts:
            k.box((x, 0, height / 2), (0.11, 0.11, height), self.trim, 0.015)
            k.box((x, 0, height + 0.03), (0.15, 0.15, 0.06), self.trim, 0.015)
        k.box((0, 0, height - 0.03), (length, 0.1, 0.07), self.trim, 0.015)
        k.box((0, 0, 0.1), (length, 0.07, 0.06), self.trim, 0.012)
        n = int(length / 0.13)
        for i in range(n):
            x = -length / 2 + (i + 0.5) * length / n
            k.box((x, 0, (height - 0.07 + 0.13) / 2), (0.04, 0.04, height - 0.2), self.trim, 0.0)
        return k

    def baluster(self, k, x, y, z, height, col):
        p = [(0.075, 0), (0.075, 0.06), (0.05, 0.08), (0.045, 0.13), (0.09, 0.3), (0.06, 0.4),
             (0.04, 0.47), (0.05, 0.5), (0.075, 0.52), (0.075, 0.58)]
        sc = height / 0.58
        k.lathe((x, y, z), [(r * min(sc, 1.2), zz * sc) for r, zz in p], col, n=8)

    def balustrade_kit(self, length=W, height=1.0, col=None, cap=None):
        col, cap = col or self.trim, cap or self.trim
        k = Kit()
        hbox(k, -length / 2, length / 2, 0.0, 0.16, -0.16, 0.16, cap, 0.02)
        hbox(k, -length / 2, length / 2, height - 0.14, height, -0.2, 0.2, cap, 0.025)
        hbox(k, -length / 2 + 0.02, length / 2 - 0.02, height - 0.2, height - 0.14, -0.15, 0.15, cap, 0.015)
        for x in (-length / 2 + 0.17, length / 2 - 0.17):
            hbox(k, x - 0.17, x + 0.17, 0.16, height - 0.2, -0.17, 0.17, cap, 0.02)
        inner = length - 0.68
        n = max(int(inner / 0.24), 1)
        for i in range(n):
            x = -inner / 2 + (i + 0.5) * inner / n
            self.baluster(k, x, 0.0, 0.16, height - 0.36, col)
        return k

    # ---------------------------------------------------------- the pieces
    def p_wall(self):
        return self.wall()

    def p_wall_window(self):
        return self.wall([window(self.window_arch)])

    def p_wall_window_tall(self):
        return self.wall([window_tall(self.window_arch)])

    def p_wall_window_balcony(self):
        o = french()
        k = self.wall([o])
        self.balcony(k, o)
        return k

    def p_wall_door(self):
        return self.wall([door()])

    def p_wall_door_double(self):
        return self.wall([door(2.4)])

    def p_wall_arch(self):
        if self.arch_kind is None:
            return self.wall([Opening("arch", 0.0, 0.0, 2.2, 2.7)])
        return self.wall([Opening("arch", 0.0, 0.0, 2.2, 3.0, self.arch_kind)])

    def p_wall_half(self):
        return self.wall(width=W / 2)

    def p_corner(self):
        k = Kit()
        k.box((0, 0, H / 2), (0.4, 0.4, H), self.wall_col, 0.04)
        k.box((0, 0, self.plinth_h / 2), (0.46, 0.46, self.plinth_h), self.stone, 0.03)
        return k

    def p_cornice(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, 0.0, 0.14, -FY, FY + 0.1, self.trim, 0.02)
        hbox(k, -W / 2, W / 2, 0.14, 0.3, -FY, FY + 0.25, self.trim, 0.025)
        return k

    def p_floor_band(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, -0.1, 0.1, 0.05, self.clad_out + 0.07, self.trim, 0.02)
        return k

    def p_parapet(self):
        k = self.wall(height=0.9, plinth=False)
        hbox(k, -W / 2, W / 2, 0.9, 1.0, -FY - 0.06, FY + 0.08, self.trim, 0.025)
        return k

    def p_roof_gable(self):
        return roof_gable(self)

    def p_roof_hip(self):
        return roof_hip(self)

    def p_roof_slab(self):
        k = Kit()
        k.box((0, 0, 0.08), (3.0, 3.0, 0.16), tinted(0.86, 0.85, 0.82), 0.02)
        for s in (-1, 1):
            hbox(k, -1.5, 1.5, 0.16, 0.26, s * 1.5 - 0.06, s * 1.5 + 0.06, self.trim2, 0.02)
            hbox(k, s * 1.5 - 0.06, s * 1.5 + 0.06, 0.16, 0.26, -1.44, 1.44, self.trim2, 0.02)
        k.box((1.2, 1.2, 0.18), (0.18, 0.18, 0.04), self.metal, 0.01)
        return k

    def p_stairs(self):
        return stairs_interior(self)

    def p_stairs_outdoor(self):
        return stairs_outdoor(self)

    def p_railing(self):
        return self.railing_kit(W)

    def p_column(self):
        k = Kit()
        k.box((0, 0, 0.1), (0.5, 0.5, 0.2), self.stone, 0.03)
        k.cyl((0, 0, H / 2), 0.17, H - 0.4, self.wall_col, n=14, bevel=0.0)
        k.box((0, 0, H - 0.1), (0.5, 0.5, 0.2), self.trim, 0.03)
        return k

    def p_awning(self):
        return awning(self)

    def p_chimney(self):
        k = Kit()
        k.box((0, 0, 1.2), (0.9, 0.6, 2.4), self.wall_col, 0.03)
        k.box((0, 0, 2.45), (1.0, 0.7, 0.12), self.trim, 0.02)
        for x in (-0.2, 0.2):
            k.cyl((x, 0, 2.62), 0.1, 0.25, fixed(0.62, 0.36, 0.25), n=10)
        return k

    def p_balustrade(self):
        return self.balustrade_kit()

    def p_drainpipe(self):
        return drainpipe(self)

    def p_ladder(self):
        return ladder(self)

    def p_scaffold(self):
        return scaffold(self)

    def pieces(self):
        tw, tr = self.tint_wall, self.tint_roof
        note_wall = "3x3.4 wall module; origin bottom centre of the wall centre line; exterior +Z."
        return [
            Piece("Wall", "wall", self.p_wall, note_wall, tw),
            Piece("WallWindow", "wall", self.p_wall_window, "1.2x1.5 window hole at 0.9 m, no glass. " + note_wall, tw),
            Piece("WallWindowTall", "wall", self.p_wall_window_tall, "1.2x2.4 tall window hole at 0.5 m. " + note_wall, tw),
            Piece("WallWindowBalcony", "wall", self.p_wall_window_balcony,
                  "1.2x2.4 french opening at floor level + 2.4x0.9 balcony (floor at y=0) with railing, brackets hang below 0. " + note_wall, tw),
            Piece("WallDoor", "wall", self.p_wall_door, "1.2x2.4 door hole (no leaf), step in front. " + note_wall, tw),
            Piece("WallDoorDouble", "wall", self.p_wall_door_double, "2.4x2.4 double door hole (no leaf). " + note_wall, tw),
            Piece("WallArch", "wall", self.p_wall_arch, "2.2 m wide open archway / wide opening. " + note_wall, tw),
            Piece("WallHalf", "wall", self.p_wall_half, "1.5 m filler wall. " + note_wall, tw),
            Piece("Corner", "corner", self.p_corner, "Corner pillar centred on the origin, full floor height.", tw),
            Piece("Cornice", "trim", self.p_cornice, "Top trim: sits on top of a wall (origin on wall centre line), overhangs the front.", tw),
            Piece("FloorBand", "trim", self.p_floor_band, "Band between floors, vertically centred on the origin: place at the floor seam.", tw),
            Piece("Parapet", "parapet", self.p_parapet, "Flat-roof edge wall 3x1 m with coping, on the wall centre line.", tw),
            Piece("RoofGable", "roof", self.p_roof_gable, "Unit gable roof 1x1x1 (ridge along x) + eaves; scale to fit. Roof tintable, gables fixed.", tr),
            Piece("RoofHip", "roof", self.p_roof_hip, "Unit hip roof 1x1x1 (ridge along x) + eaves; scale to fit.", tr),
            Piece("RoofSlab", "roof", self.p_roof_slab, "3x3 flat roof tile, origin bottom centre, lip on the edges.", (0.75, 0.74, 0.72)),
            Piece("Stairs", "stairs", self.p_stairs, "Interior flight 1.4 m wide rising 3.4 m; bottom step at +Z (front), climbs toward -Z.", tw),
            Piece("StairsOutdoor", "stairs", self.p_stairs_outdoor, "Outdoor steps 3 m wide rising 1.7 m; bottom at +Z, climbs toward -Z.", tw),
            Piece("Railing", "railing", self.p_railing, "3 m railing, 1 m high, origin bottom centre.", tw),
            Piece("Column", "column", self.p_column, "Free-standing column, floor height.", tw),
            Piece("Awning", "awning", self.p_awning, "3 m fabric awning on the wall centre line: sticks out to +Z, origin at its lowest point (valance), top mount at ~0.9 m. Fabric tintable.", (0.8, 0.25, 0.22)),
            Piece("Chimney", "chimney", self.p_chimney, "Roof chimney / stack, origin bottom centre.", tw),
            Piece("Balustrade", "railing", self.p_balustrade, "3 m balustrade 1 m high, origin bottom centre.", tw),
            Piece("Drainpipe", "climb", self.p_drainpipe, "Climbable drainpipe, floor height, on the wall centre line (pipe sits in front of the wall face).", None),
            Piece("Ladder", "climb", self.p_ladder, "Metal ladder floor height; origin bottom centre, brackets at -Z go against the wall.", None),
            Piece("Scaffold", "climb", self.p_scaffold, "Scaffold bay 3 x 3.4 x 1.2 with deck on top and inner ladder; stackable.", None),
        ]


# ================================================================ shared structures
def roof_gable(st, rows=6, ov_y=0.08, ov_x=0.05, thick=0.03):
    k = Kit()
    gable = st.gable_col
    k.prism([(-0.5, 0.0), (0.5, 0.0), (0.0, 1.0)], -0.5, 0.5, lambda f: gable if abs(f.normal.x) > 0.5 else shade(st.roof_col, 0.6), "X", 0.0)
    slope = math.atan2(1.0, 0.5)
    length = math.hypot(0.5, 1.0) + ov_y / math.cos(slope)
    for side in (-1, 1):
        d = (-side * 0.5 / math.hypot(0.5, 1.0), 1.0 / math.hypot(0.5, 1.0))
        n = (side * abs(d[1]), abs(d[0]))
        start = (side * 0.5 - d[0] * (length - math.hypot(0.5, 1.0)), -d[1] * (length - math.hypot(0.5, 1.0)))
        seg = length / rows
        for i in range(rows):
            mid = (i + 0.55) * seg
            cy = start[0] + d[0] * mid + n[0] * thick
            cz = start[1] + d[1] * mid + n[1] * thick
            tilt = side * (90.0 - math.degrees(slope) + 6.0)
            col = shade(st.roof_col, 0.88 + 0.12 * ((i + (side > 0)) % 2))
            k.box((0, cy, cz), (1.0 + 2 * ov_x, thick * 1.8, seg * 1.15), col, 0.01, R("X", tilt))
        a = (side * (0.5 + ov_y * 0.6), -0.03)
        for x in (-0.5 - ov_x, 0.5 + ov_x):
            k.beam((x, a[0], a[1] + 0.02), (x, 0.0, 1.05), 0.03, 0.08, st.trim, 0.008)
    k.box((0, 0, 1.02), (1.0 + 2 * ov_x + 0.04, 0.09, 0.09), st.ridge_col, 0.01, R("X", 45))
    return k


def roof_hip(st, rows=6, ov=0.08, ridge=0.25):
    k = Kit()

    def half(z):
        return 0.5 - (0.5 - ridge) * z, 0.5 * (1 - z)

    k.hull([(sx * 0.5, sy * 0.5, 0.0) for sx in (-1, 1) for sy in (-1, 1)] + [(-ridge, 0, 1.0), (ridge, 0, 1.0)],
           shade(st.roof_col, 0.6))
    for i in range(rows):
        z0, z1 = i / rows, (i + 1) / rows
        e0 = ov if i == 0 else 0.045
        hx0, hy0 = half(z0)
        hx1, hy1 = half(z1)
        pts = [(sx * (hx0 + e0), sy * (hy0 + e0), z0 - (0.03 if i == 0 else 0.0)) for sx in (-1, 1) for sy in (-1, 1)]
        pts += [(sx * (hx1 + 0.012), sy * (hy1 + 0.012), z1 + 0.012) for sx in (-1, 1) for sy in (-1, 1)]
        k.hull(pts, shade(st.roof_col, 0.88 + 0.12 * (i % 2)), 0.008)
    k.beam((-ridge - 0.02, 0, 1.03), (ridge + 0.02, 0, 1.03), 0.07, 0.07, st.ridge_col, 0.008)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.beam((sx * ridge, 0, 1.03), (sx * (0.5 + ov), sy * (0.5 + ov), -0.0), 0.05, 0.05, st.ridge_col, 0.006)
    return k


def stairs_interior(st, steps=17, width=1.4):
    k = Kit()
    rise, run = H / steps, STAIR_RUN / steps
    yf = STAIR_RUN / 2
    for i in range(steps):
        zt = (i + 1) * rise
        y1 = yf - i * run
        hbox(k, -width / 2 + 0.07, width / 2 - 0.07, zt - 0.05, zt, y1 - run, y1 + 0.03, st.tread_col, 0.012)
        hbox(k, -width / 2 + 0.08, width / 2 - 0.08, zt - rise, zt - 0.05, y1 - 0.05, y1 - 0.01, st.trim_in, 0.0)
    yb = yf - steps * run
    for s in (-1, 1):
        x = s * (width / 2 - 0.04)
        k.prism([(yf + 0.03, 0.0), (yf + 0.03, 0.32), (yb, H + 0.12), (yb, H - 0.34)], x - 0.04, x + 0.04, st.wall_col, "X", 0.015)
    k.prism([(yf, 0.0), (yf, 0.05), (yb, H - 0.29), (yb, H - 0.34)], -width / 2 + 0.06, width / 2 - 0.06, st.wall_col, "X", 0.0)
    # handrail on +X with a newel post at each end
    x = width / 2 - 0.04
    for y, z in ((yf - 0.15, 0.2), (yb + 0.1, H)):
        k.box((x, y, z + 0.5), (0.1, 0.1, 1.0), st.wood, 0.015)
        k.box((x, y, z + 1.04), (0.14, 0.14, 0.08), st.wood, 0.015)
    k.beam((x, yf - 0.15, 1.1), (x, yb + 0.1, H + 0.9), 0.07, 0.06, st.wood, 0.015)
    for i in range(1, steps - 1):
        y = yf - (i + 0.5) * run
        zt = (i + 1) * rise
        k.box((x, y, zt + 0.42), (0.035, 0.035, 0.84), st.trim, 0.0)
    return k


def stairs_outdoor(st, steps=9, width=3.0, rise_total=1.7, run=0.36, cheeks=True):
    k = Kit()
    rise = rise_total / steps
    yf = steps * run / 2
    inner = width - (0.6 if cheeks else 0.0)
    for i in range(steps):
        zt = (i + 1) * rise
        y1 = yf - i * run
        step = st.step_col or st.stone
        hbox(k, -inner / 2, inner / 2, 0.0, zt - 0.06, y1 - run, y1, step, 0.0)
        hbox(k, -inner / 2, inner / 2, zt - 0.06, zt, y1 - run, y1 + 0.04, shade(step, 1.08), 0.015)
    yb = yf - steps * run
    if cheeks:
        for s in (-1, 1):
            x0, x1 = sorted((s * width / 2, s * (width / 2 - 0.3)))
            k.prism([(yf + 0.1, 0.0), (yb, 0.0), (yb, rise_total + 0.35), (yf - 0.3, 0.45), (yf + 0.1, 0.45)], x0, x1, st.cheek_col or st.wall_col, "X", 0.025)
            k.prism([(yf + 0.14, 0.45), (yf - 0.3, 0.45), (yb, rise_total + 0.35), (yb, rise_total + 0.43), (yf - 0.32, 0.53), (yf + 0.14, 0.53)],
                    x0 - 0.04, x1 + 0.04, st.trim, "X", 0.015)
    return k


def awning(st, depth=1.3, drop=0.55, stripes=8):
    k = Kit()
    top = 0.95
    y0 = FY + 0.03
    tint = tinted(0.97, 0.97, 0.96)
    cream = fixed(0.95, 0.93, 0.86)
    sw = W / stripes
    for i in range(stripes):
        x = -W / 2 + (i + 0.5) * sw
        col = tint if i % 2 == 0 else cream
        k.beam((x, y0, top), (x, y0 + depth, top - drop), sw, 0.03, col, 0.006)
        zf = top - drop
        hbox(k, x - sw / 2, x + sw / 2, zf - 0.22, zf + 0.02, y0 + depth - 0.015, y0 + depth + 0.015, col, 0.004)
        arc = [(x + sw / 2 * math.cos(math.pi * t / 6), zf - 0.22 - 0.18 * math.sin(math.pi * t / 6)) for t in range(7)]
        k.prism(arc, y0 + depth - 0.015, y0 + depth + 0.015, col, "Y", 0.0)
    for s in (-1, 1):
        x = s * W / 2
        k.prism([(y0, top), (y0 + depth, top - drop), (y0 + depth, top - drop - 0.2), (y0, top - 0.35)], x - 0.015 * s - 0.015, x - 0.015 * s + 0.015,
                tint, "X", 0.0)
        k.beam((x - s * 0.1, FY, top - 0.6), (x - s * 0.1, y0 + depth - 0.05, top - drop - 0.02), 0.035, 0.035, st.metal, 0.0)
    k.cyl((0, y0 + 0.02, top + 0.02), 0.05, W, st.metal, n=8, axis="X")
    return k


def drainpipe(st):
    k = Kit()
    y = FY + 0.11
    metal = st.metal
    k.cyl((0, y, (0.3 + H - 0.35) / 2), 0.055, H - 0.65, metal, n=8, bevel=0.0)
    k.tube([(0, y, 0.32), (0, y, 0.2), (0, y + 0.14, 0.07), (0, y + 0.24, 0.05)], 0.055, metal, n=8)
    k.box((0, y, H - 0.2), (0.24, 0.2, 0.3), metal, 0.02)
    k.box((0, y, H - 0.04), (0.3, 0.26, 0.04), metal, 0.01)
    for z in (0.7, 1.7, 2.7):
        k.box((0, y - 0.03, z), (0.16, 0.16, 0.05), shade(metal, 0.7), 0.01)
        k.box((0, FY + 0.02, z), (0.06, 0.06, 0.05), shade(metal, 0.7), 0.0)
    return k


def ladder(st, height=H):
    k = Kit()
    metal = fixed(0.58, 0.6, 0.62)
    for x in (-0.23, 0.23):
        k.box((x, 0, height / 2), (0.05, 0.07, height), metal, 0.01)
        for z in (0.6, height - 0.5):
            k.box((x, -0.11, z), (0.04, 0.18, 0.04), metal, 0.0)
            k.box((x, -0.2, z), (0.1, 0.02, 0.1), metal, 0.0)
    n = int(height / 0.3)
    for i in range(n):
        k.cyl((0, 0, 0.25 + i * 0.3), 0.018, 0.46, metal, n=6, axis="X", bevel=0.0)
    for x in (-0.23, 0.23):
        k.tube([(x, 0, height - 0.02), (x, 0.08, height + 0.25), (x, 0.0, height + 0.55)], 0.025, metal, n=6)
    return k


def scaffold(st, depth=1.2):
    k = Kit()
    tube = fixed(0.62, 0.64, 0.66)
    clamp = fixed(0.85, 0.65, 0.15)
    plank = fixed(0.62, 0.46, 0.28)
    hx, hy = W / 2 - 0.05, depth / 2 - 0.05
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((sx * hx, sy * hy, H / 2), 0.03, H, tube, n=6, bevel=0.0)
            k.box((sx * hx, sy * hy, 0.02), (0.16, 0.16, 0.04), clamp, 0.005)
    for z in (0.15, H - 0.1):
        for sy in (-1, 1):
            k.cyl((0, sy * hy, z), 0.028, W, tube, n=6, axis="X", bevel=0.0)
        for sx in (-1, 1):
            k.cyl((sx * hx, 0, z), 0.028, depth, tube, n=6, axis="Y", bevel=0.0)
            for sy in (-1, 1):
                k.box((sx * hx, sy * hy, z), (0.08, 0.08, 0.08), clamp, 0.01)
    k.cyl((0, hy, 1.0), 0.025, W, tube, n=6, axis="X", bevel=0.0)
    k.beam((-hx, hy, 0.15), (hx, hy, H - 0.1), 0.05, 0.05, tube, 0.0)
    for i in range(5):
        y = -hy + 0.1 + i * (2 * hy - 0.2) / 4
        k.box((0, y, H - 0.02), (W - 0.02, 0.2, 0.04), shade(plank, 0.9 + 0.05 * (i % 3)), 0.01)
    for sy in (-1, 1):
        k.box((0, sy * (hy + 0.03), H + 0.08), (W, 0.025, 0.15), plank, 0.005)
    lad = ladder(st, H)
    k.merge(lad, T(hx - 0.45, -hy + 0.25, 0.0))
    hbox(k, hx - 0.95, hx - 0.05, H - 0.05, H, -hy, -hy + 0.5, fixed(0.25, 0.25, 0.27), 0.0)
    return k


# ================================================================ Timber (log lodge)
class Timber(Style):
    name = "Timber"
    plinth_h = 0.6
    arch_kind = None
    tint_wall = (0.62, 0.42, 0.27)
    tint_roof = (0.42, 0.36, 0.33)
    wall_col = tinted(0.96, 0.93, 0.88)
    back_col = tinted(0.9, 0.86, 0.8)
    chink = fixed(0.66, 0.6, 0.5)
    trim = fixed(0.34, 0.21, 0.12)
    trim2 = fixed(0.47, 0.31, 0.18)
    trim_in = fixed(0.4, 0.26, 0.15)
    stone = fixed(0.47, 0.47, 0.49)
    wood = fixed(0.34, 0.21, 0.12)
    roof_col = tinted(0.92, 0.88, 0.84)
    gable_col = fixed(0.5, 0.34, 0.2)
    ridge_col = fixed(0.3, 0.2, 0.12)
    tread_col = fixed(0.5, 0.33, 0.19)
    shutter = fixed(0.52, 0.36, 0.2)
    clad_out = 0.25
    log_r = 0.106

    def slab_colour(self, face):
        return self.back_col if face.normal.y < -0.5 else self.chink

    def plinth(self, k, openings, width):
        rng = rng_for(self.name + str(len(k.faces)))
        courses = 2
        ch = self.plinth_h / courses
        for c in range(courses):
            z0, z1 = c * ch, (c + 1) * ch
            for a, b in intervals(-width / 2, width / 2, z0, z1, openings):
                x = a
                while x < b - 0.05:
                    w = min(rng.uniform(0.28, 0.55), b - x)
                    if b - (x + w) < 0.15:
                        w = b - x
                    out = FY + rng.uniform(0.05, 0.09)
                    hbox(k, x + 0.012, x + w - 0.012, z0 + 0.012, z1 - 0.012, FY - 0.05, out,
                         shade(self.stone, rng.uniform(0.85, 1.12)), 0.035)
                    x += w

    def cladding(self, k, openings, width, height, z0):
        rng = rng_for(self.name + str(len(k.faces)))
        n = max(int(round((height - z0) / (2 * self.log_r))), 1)
        step = (height - z0) / n
        r = step * 0.53
        for i in range(n):
            z = z0 + (i + 0.5) * step
            for a, b in intervals(-width / 2, width / 2, z - r * 0.8, z + r * 0.8, openings):
                k.cyl(((a + b) / 2, FY - 0.01, z), r * rng.uniform(0.94, 1.0), b - a,
                      shade(self.wall_col, rng.uniform(0.84, 1.0)), n=8, axis="X", bevel=0.022, m=R("X", 22.5))

    def shutter_panel(self, k, x, z0, w, h, y):
        n = 3
        for i in range(n):
            px = x - w / 2 + (i + 0.5) * w / n
            k.box((px, y, z0 + h / 2), (w / n - 0.012, 0.04, h), shade(self.shutter, 0.92 + 0.08 * (i % 2)), 0.008)
        for zz in (z0 + 0.22, z0 + h - 0.22):
            k.box((x, y + 0.03, zz), (w - 0.06, 0.03, 0.09), self.trim, 0.008)
        k.beam((x - w / 2 + 0.06, y + 0.03, z0 + 0.26), (x + w / 2 - 0.06, y + 0.03, z0 + h - 0.26), 0.03, 0.07, self.trim, 0.006)

    def dress_window(self, k, o, flowers=True):
        hw = o.w / 2
        surround(k, o, 0.0, 0.13, -0.02, 0.3, self.trim)
        hbox(k, o.cx - hw - 0.36, o.cx + hw + 0.36, o.top + 0.1, o.top + 0.3, -0.02, 0.33, self.trim2, 0.03)
        hbox(k, o.cx - hw - 0.2, o.cx + hw + 0.2, o.z0 - 0.1, o.z0, -0.02, 0.4, self.trim2, 0.02)
        sw = hw * 0.78
        for s in (-1, 1):
            self.shutter_panel(k, o.cx + s * (hw + 0.15 + sw / 2), o.z0, sw, o.h, 0.29)
        if flowers:
            self.flower_box(k, o)

    def flower_box(self, k, o):
        rng = rng_for("flowers" + str(o.z0))
        hbox(k, o.cx - o.w / 2, o.cx + o.w / 2, o.z0 - 0.34, o.z0 - 0.12, 0.33, 0.55, self.trim2, 0.02)
        for x in (o.cx - o.w / 2 + 0.15, o.cx + o.w / 2 - 0.15):
            k.prism([(0.25, o.z0 - 0.1), (0.5, o.z0 - 0.34), (0.25, o.z0 - 0.48)], x - 0.03, x + 0.03, self.trim, "X", 0.0)
        colours = [fixed(0.9, 0.25, 0.25), fixed(0.95, 0.75, 0.2), fixed(0.85, 0.4, 0.7), fixed(0.95, 0.95, 0.9)]
        for i in range(7):
            x = o.cx - o.w / 2 + 0.1 + i * (o.w - 0.2) / 6
            k.blob((x, 0.44, o.z0 - 0.1), 0.085, fixed(0.3, 0.55, 0.22), rng, sub=1, jitter=0.2)
            k.blob((x + 0.04, 0.48, o.z0 - 0.03), 0.045, colours[i % 4], rng, sub=0, jitter=0.1)

    def dress_tall(self, k, o):
        self.dress_window(k, o, flowers=False)

    def dress_french(self, k, o):
        surround(k, o, 0.0, 0.13, -0.02, 0.3, self.trim)
        hbox(k, o.cx - o.w / 2 - 0.36, o.cx + o.w / 2 + 0.36, o.top + 0.1, o.top + 0.3, -0.02, 0.33, self.trim2, 0.03)

    def dress_door(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.16, -0.04, 0.31, self.trim)
        hbox(k, o.cx - hw - 0.45, o.cx + hw + 0.45, o.top + 0.12, o.top + 0.34, -0.04, 0.35, self.trim2, 0.03)
        hbox(k, o.cx - hw - 0.3, o.cx + hw + 0.3, 0.0, 0.14, -FY, FY + 0.55, shade(self.stone, 1.05), 0.03)
        # little shingle hood on two knee braces
        zc = o.top + 0.45
        for s in (-1, 1):
            k.beam((o.cx + s * (hw + 0.05), 0.24, zc - 0.5), (o.cx + s * (hw + 0.05), 0.85, zc - 0.02), 0.08, 0.08, self.trim, 0.01)
        k.box((o.cx, 0.6, zc + 0.04), (o.w + 0.7, 0.85, 0.06), self.trim2, 0.015, R("X", -14))
        for i in range(4):
            k.box((o.cx, 0.32 + i * 0.2, zc + 0.15 - i * 0.05), (o.w + 0.75, 0.26, 0.035), shade(fixed(0.36, 0.28, 0.22), 1 + 0.1 * (i % 2)), 0.006, R("X", -20))

    def dress_double(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.16, -0.04, 0.31, self.trim)
        hbox(k, -W / 2, W / 2, o.top + 0.12, o.top + 0.36, -0.04, 0.35, self.trim2, 0.03)
        hbox(k, o.cx - hw - 0.15, o.cx + hw + 0.15, 0.0, 0.14, -FY, FY + 0.55, shade(self.stone, 1.05), 0.03)

    def dress_arch(self, k, o):
        hw = o.w / 2
        for s in (-1, 1):
            hbox(k, o.cx + s * hw - 0.11, o.cx + s * hw + 0.11, 0.0, o.top, -FY - 0.03, FY + 0.15, self.trim, 0.03)
            k.beam((o.cx + s * (hw - 0.1), 0.0, o.top - 0.6), (o.cx + s * (hw - 0.6), 0.0, o.top - 0.05), 0.12, 0.14, self.trim2, 0.02)
        hbox(k, -W / 2, W / 2, o.top - 0.02, o.top + 0.3, -FY - 0.03, FY + 0.15, self.trim2, 0.03)

    def p_corner(self):
        k = Kit()
        rng = rng_for("TimberCorner")
        hbox(k, -0.27, 0.27, 0.0, self.plinth_h, -0.27, 0.27, self.stone, 0.04)
        n = int(round((H - self.plinth_h) / (2 * self.log_r)))
        step = (H - self.plinth_h) / n
        for i in range(n):
            z = self.plinth_h + (i + 0.5) * step
            axis = "X" if i % 2 == 0 else "Y"
            k.cyl((0, 0, z), step * 0.55, 0.56, shade(self.wall_col, rng.uniform(0.84, 1.0)), n=8, axis=axis, bevel=0.03, m=R(axis, 22.5))
        return k

    def p_cornice(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, 0.0, 0.22, -FY, FY + 0.06, self.trim2, 0.02)
        for i in range(7):
            x = -W / 2 + 0.2 + i * (W - 0.4) / 6
            hbox(k, x - 0.05, x + 0.05, 0.06, 0.2, FY, FY + 0.3, self.trim, 0.015)
        hbox(k, -W / 2, W / 2, 0.06, 0.26, FY + 0.28, FY + 0.33, self.trim2, 0.012)
        self.shingle_row(k, 0.3, FY - 0.05, FY + 0.36, 0)
        self.shingle_row(k, 0.38, FY - 0.15, FY + 0.18, 1)
        return k

    def shingle_row(self, k, z, y0, y1, seed, tilt=18):
        rng = rng_for("shingle" + str(seed) + str(z))
        x = -W / 2 + (0.12 if seed % 2 else 0.0)
        hbox(k, -W / 2, W / 2, z - 0.05, z, y0, y1 - 0.06, self.trim, 0.0)
        while x < W / 2 - 0.02:
            w = min(rng.uniform(0.2, 0.32), W / 2 - x)
            col = shade(fixed(0.45, 0.38, 0.32), rng.uniform(0.85, 1.12))
            k.box((x + w / 2, (y0 + y1) / 2, z + 0.02), (w - 0.015, y1 - y0, 0.04), col, 0.008, R("X", -tilt))
            x += w

    def p_floor_band(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, 0.0, 0.16, FY, FY + 0.12, self.trim, 0.025)
        self.shingle_row(k, -0.02, FY + 0.02, FY + 0.3, 3, tilt=24)
        return k

    def p_parapet(self):
        k = self.wall(height=0.88, plinth=False)
        hbox(k, -W / 2, W / 2, 0.88, 0.96, -FY - 0.08, FY + 0.12, self.trim2, 0.02)
        for x in (-1.2, 0.0, 1.2):
            hbox(k, x - 0.08, x + 0.08, 0.0, 1.0, FY + 0.12, FY + 0.22, self.trim, 0.02)
        return k

    def p_roof_slab(self):
        k = Kit()
        k.box((0, 0, 0.08), (3.0, 3.0, 0.16), tinted(0.9, 0.86, 0.8), 0.02)
        for i in range(10):
            hbox(k, -1.5, 1.5, 0.16, 0.18, -1.5 + i * 0.3 + 0.01, -1.5 + (i + 1) * 0.3 - 0.01, shade(self.trim2, 1 + 0.06 * (i % 2)), 0.006)
        for s in (-1, 1):
            hbox(k, -1.5, 1.5, 0.16, 0.3, s * 1.5 - 0.07, s * 1.5 + 0.07, self.trim, 0.02)
        return k

    def p_stairs_outdoor(self):
        k = Kit()
        steps, rise_total, run = 9, 1.7, 0.36
        rise = rise_total / steps
        yf = steps * run / 2
        for i in range(steps):
            zt = (i + 1) * rise
            y1 = yf - i * run
            for j in range(3):
                hbox(k, -1.35, 1.35, zt - 0.06, zt, y1 - run + j * run / 3, y1 - run + (j + 1) * run / 3 - 0.01,
                     shade(self.tread_col, 0.92 + 0.06 * ((i + j) % 2)), 0.01)
            hbox(k, -1.3, 1.3, zt - rise, zt - 0.06, y1 - 0.04, y1, self.trim, 0.0)
        yb = yf - steps * run
        for s in (-1, 1):
            x = s * 1.42
            k.prism([(yf + 0.05, 0.0), (yf + 0.05, 0.3), (yb, rise_total + 0.05), (yb, rise_total - 0.3)], x - 0.08, x + 0.08, self.trim, "X", 0.025)
            for y, z in ((yf - 0.1, 0.25), (yb + 0.1, rise_total)):
                k.cyl((x, y, z + 0.5), 0.07, 1.0, self.trim2, n=8)
            k.beam((x, yf - 0.1, 1.15), (x, yb + 0.1, rise_total + 0.95), 0.08, 0.08, self.trim2, 0.015)
        return k

    def railing_kit(self, length, height=1.0, ends=True):
        k = Kit()
        posts = [-length / 2 + 0.07, length / 2 - 0.07] if ends else []
        n_mid = int(length / 1.4)
        posts += [-length / 2 + length * (i + 1) / (n_mid + 1) for i in range(n_mid)]
        for x in posts:
            k.cyl((x, 0, height / 2 + 0.03), 0.07, height + 0.06, self.trim, n=8)
        k.cyl((0, 0, height - 0.04), 0.055, length, self.trim2, n=8, axis="X")
        k.cyl((0, 0, 0.12), 0.04, length, self.trim2, n=8, axis="X")
        bays = [-length / 2] + sorted(posts) + [length / 2]
        for a, b in zip(bays, bays[1:]):
            if b - a < 0.3:
                continue
            k.beam((a + 0.08, 0, 0.16), (b - 0.08, 0, height - 0.1), 0.06, 0.05, self.trim2, 0.01)
            k.beam((a + 0.08, 0, height - 0.1), (b - 0.08, 0, 0.16), 0.06, 0.05, self.trim2, 0.01)
        return k

    def balcony(self, k, o):
        width, depth = 2.4, 0.9
        y1 = FY + depth
        for i in range(6):
            x = -width / 2 + (i + 0.5) * width / 6
            hbox(k, x - width / 12 + 0.01, x + width / 12 - 0.01, -0.06, 0.02, -0.05, y1, shade(self.tread_col, 0.94 + 0.06 * (i % 2)), 0.01)
        for x in (-width / 2 + 0.1, width / 2 - 0.1):
            hbox(k, x - 0.08, x + 0.08, -0.22, -0.06, -0.05, y1, self.trim, 0.02)
            k.beam((x, FY + 0.02, -0.9), (x, FY + depth * 0.7, -0.2), 0.1, 0.1, self.trim, 0.015)
        k.merge(self.railing_kit(width), T(0, y1 - 0.06, 0.02))
        side = self.railing_kit(depth - 0.06, ends=False)
        for s in (-1, 1):
            k.merge(side, T(s * (width / 2 - 0.06), FY + (depth - 0.06) / 2, 0.02) @ R("Z", 90))

    def p_column(self):
        k = Kit()
        hbox(k, -0.26, 0.26, 0.0, 0.3, -0.26, 0.26, self.stone, 0.04)
        k.cyl((0, 0, (0.3 + H - 0.2) / 2), 0.15, H - 0.5, self.wall_col, n=8, bevel=0.03, m=R("Z", 22.5))
        hbox(k, -0.32, 0.32, H - 0.2, H, -0.14, 0.14, self.trim, 0.03)
        for s in (-1, 1):
            k.beam((0, 0, H - 0.75), (s * 0.55, 0, H - 0.12), 0.1, 0.1, self.trim2, 0.015)
        return k

    def p_chimney(self):
        k = Kit()
        rng = rng_for("TimberChimney")
        for c in range(8):
            z0, z1 = c * 0.3, (c + 1) * 0.3
            off = 0.0 if c % 2 == 0 else 0.15
            x = -0.5 + off - (0.15 if c % 2 else 0.0)
            while x < 0.5:
                w = min(rng.uniform(0.25, 0.4), 0.5 - x)
                hbox(k, max(x, -0.5) + 0.01, x + w - 0.01, z0 + 0.01, z1 - 0.01, -0.35, 0.35, shade(self.stone, rng.uniform(0.85, 1.12)), 0.03)
                x += w
        hbox(k, -0.58, 0.58, 2.4, 2.5, -0.43, 0.43, shade(self.stone, 0.8), 0.03)
        hbox(k, -0.3, 0.3, 2.5, 2.62, -0.2, 0.2, shade(self.stone, 0.7), 0.02)
        return k


# ================================================================ Stucco (Gulf city)
class Stucco(Style):
    name = "Stucco"
    plinth_h = 0.45
    window_arch = "pointed"
    arch_kind = "pointed"
    tint_wall = (0.95, 0.88, 0.76)
    tint_roof = (0.78, 0.45, 0.32)
    wall_col = tinted(0.97, 0.96, 0.93)
    back_col = tinted(0.95, 0.94, 0.92)
    trim = fixed(0.82, 0.7, 0.52)
    trim2 = fixed(0.7, 0.57, 0.4)
    trim_in = fixed(0.78, 0.68, 0.52)
    stone = fixed(0.72, 0.6, 0.44)
    wood = fixed(0.36, 0.22, 0.13)
    roof_col = tinted(0.95, 0.9, 0.85)
    gable_col = fixed(0.9, 0.85, 0.75)
    ridge_col = fixed(0.62, 0.36, 0.25)
    tread_col = fixed(0.78, 0.7, 0.58)
    clad_out = 0.16

    def frieze(self, k, openings, width, height):
        z = height - 0.42
        for a, b in intervals(-width / 2, width / 2, z - 0.12, z + 0.12, openings):
            hbox(k, a, b, z - 0.13, z - 0.1, FY, FY + 0.03, self.trim, 0.006)
            hbox(k, a, b, z + 0.1, z + 0.13, FY, FY + 0.03, self.trim, 0.006)
            n = int((b - a) / 0.25)
            for i in range(n):
                x = a + (i + 0.5) * (b - a) / n
                k.box((x, FY + 0.02, z), (0.11, 0.05, 0.11), self.trim2, 0.012, R("Y", 45))

    def dress_window(self, k, o):
        surround(k, o, 0.0, 0.12, -0.02, 0.22, self.trim, segs=6)
        surround(k, o, 0.12, 0.2, 0.0, 0.18, self.trim2, segs=6)
        hbox(k, o.cx - o.w / 2 - 0.25, o.cx + o.w / 2 + 0.25, o.z0 - 0.1, o.z0, -0.02, 0.3, self.trim, 0.02)
        for x in (o.cx - o.w / 2 - 0.12, o.cx + o.w / 2 + 0.12):
            k.prism([(FY, o.z0 - 0.1), (0.28, o.z0 - 0.1), (FY, o.z0 - 0.3)], x - 0.06, x + 0.06, self.trim, "X", 0.01)
        if o.arch is not None:
            hbox(k, o.cx - o.w / 2, o.cx + o.w / 2, o.spring - 0.03, o.spring + 0.03, -0.03, 0.05, self.wood, 0.008)
            lattice(k, o.cx - o.w / 2, o.cx + o.w / 2, o.spring, o.top, -0.02, 0.04, self.wood, 0.1, 0.03,
                    clip=lambda x: o.arch_height(x) - 0.01)

    def mashrabiya_bay(self, k, x0, x1, z0, z1, out):
        y0, y1 = FY, FY + out
        for x in (x0, x1):
            for y in (y0 + 0.04, y1 - 0.04):
                hbox(k, x - 0.05, x + 0.05, z0, z1, y - 0.04, y + 0.04, self.wood, 0.01)
        hbox(k, x0 - 0.04, x1 + 0.04, z0, z0 + 0.32, y0, y1, self.wood, 0.015)
        hbox(k, x0 - 0.1, x1 + 0.1, z0 - 0.06, z0, y0, y1 + 0.06, self.trim, 0.015)
        for x in (x0 + 0.1, x1 - 0.1):
            k.prism([(y0, z0 - 0.06), (y1, z0 - 0.06), (y0, z0 - 0.5)], x - 0.06, x + 0.06, self.trim, "X", 0.01)
        lattice(k, x0 + 0.04, x1 - 0.04, z0 + 0.32, z1 - 0.12, y1 - 0.05, y1 - 0.01, self.wood, 0.1, 0.03)
        for x in (x0, x1):
            k.merge(_lattice_side(self, y0 + 0.08, y1 - 0.08, z0 + 0.32, z1 - 0.12), T(x, 0, 0))
        hbox(k, x0 - 0.04, x1 + 0.04, z1 - 0.12, z1, y0, y1, self.wood, 0.015)
        k.prism([(y0, z1), (y1 + 0.1, z1), (y1 + 0.1, z1 + 0.05), (y0, z1 + 0.22)], x0 - 0.12, x1 + 0.12, self.trim, "X", 0.015)

    def dress_tall(self, k, o):
        surround(k, o, 0.0, 0.12, -0.02, 0.2, self.trim, segs=6)
        hw = o.w / 2
        self.mashrabiya_bay(k, o.cx - hw - 0.05, o.cx + hw + 0.05, o.z0 + 0.05, o.top - 0.1, 0.42)

    def dress_french(self, k, o):
        surround(k, o, 0.0, 0.12, -0.02, 0.2, self.trim)
        hbox(k, o.cx - 0.85, o.cx + 0.85, o.top + 0.12, o.top + 0.24, -0.02, 0.3, self.trim2, 0.02)

    def dress_door(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.16, -0.03, 0.24, self.trim)
        dw = o.w + 0.12
        deco = Opening("deco", o.cx, o.top + 0.12, dw, dw / 2, "round")
        k.prism(deco.outline(0.0)[2:] + [(o.cx - dw / 2, o.top + 0.12)], 0.05, 0.12, self.wood, "Y", 0.0)
        lattice(k, o.cx - dw / 2 + 0.04, o.cx + dw / 2 - 0.04, o.top + 0.12, deco.top, 0.1, 0.16, self.trim2, 0.11, 0.035,
                clip=lambda x: deco.arch_height(x) - 0.02)
        surround(k, deco, 0.0, 0.14, -0.01, 0.22, self.trim, self.trim2, segs=7)
        k.box((o.cx, 0.2, deco.top + 0.06), (0.18, 0.12, 0.18), self.trim2, 0.02)
        self.step(k, o)

    def dress_double(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.18, -0.03, 0.24, self.trim)
        hbox(k, o.cx - hw - 0.25, o.cx + hw + 0.25, o.top + 0.18, o.top + 0.62, -0.03, 0.22, self.trim2, 0.02)
        n = 8
        for i in range(n):
            x = o.cx - hw + (i + 0.5) * o.w / n
            k.box((x, 0.24, o.top + 0.4), (0.14, 0.06, 0.14), self.trim, 0.012, R("Y", 45))
        hbox(k, o.cx - hw - 0.3, o.cx + hw + 0.3, o.top + 0.62, o.top + 0.7, -0.03, 0.28, self.trim, 0.02)
        self.step(k, o)

    def dress_arch(self, k, o):
        surround(k, o, 0.0, 0.2, -FY - 0.02, 0.24, self.trim, self.trim2, segs=8)
        for s in (-1, 1):
            hbox(k, o.cx + s * (o.w / 2) - 0.25, o.cx + s * (o.w / 2) + 0.25, o.spring - 0.12, o.spring, -FY - 0.04, 0.27, self.trim2, 0.02)
            hbox(k, o.cx + s * (o.w / 2) - 0.26, o.cx + s * (o.w / 2) + 0.26, 0.0, 0.3, -FY - 0.04, 0.27, self.stone, 0.03)

    def back_casing(self, k, o):
        if o.kind == "arch":
            return
        super().back_casing(k, o)

    def balcony(self, k, o):
        width, depth = 2.4, 0.9
        y1 = FY + depth
        hbox(k, -width / 2, width / 2, -0.2, 0.02, -0.05, y1, self.trim, 0.03)
        for i in range(5):
            x = -width / 2 + 0.2 + i * (width - 0.4) / 4
            k.prism([(FY, -0.2), (FY + 0.6, -0.2), (FY + 0.2, -0.38), (FY, -0.55)], x - 0.06, x + 0.06, self.trim2, "X", 0.01)
        k.merge(self.railing_kit(width), T(0, y1 - 0.07, 0.02))
        side = self.railing_kit(depth - 0.07, ends=False)
        for s in (-1, 1):
            k.merge(side, T(s * (width / 2 - 0.07), FY + (depth - 0.07) / 2, 0.02) @ R("Z", 90))

    def railing_kit(self, length, height=1.0, ends=True):
        k = Kit()
        posts = [-length / 2 + 0.07, length / 2 - 0.07] if ends else []
        n_mid = int(length / 1.2)
        posts += [-length / 2 + length * (i + 1) / (n_mid + 1) for i in range(n_mid)]
        for x in posts:
            k.box((x, 0, height / 2), (0.14, 0.14, height), self.wall_col, 0.02)
            k.box((x, 0, height + 0.04), (0.18, 0.18, 0.08), self.trim, 0.015)
        hbox(k, -length / 2, length / 2, height - 0.08, height, -0.07, 0.07, self.trim, 0.015)
        hbox(k, -length / 2, length / 2, 0.0, 0.12, -0.07, 0.07, self.trim, 0.015)
        bays = [-length / 2] + sorted(posts) + [length / 2]
        for a, b in zip(bays, bays[1:]):
            a2, b2 = a + (0.07 if a > -length / 2 else 0.0), b - (0.07 if b < length / 2 else 0.0)
            if b2 - a2 > 0.15:
                lattice(k, a2, b2, 0.12, height - 0.08, -0.02, 0.02, self.wood, 0.1, 0.03)
                hbox(k, a2, b2, 0.12, height - 0.08, -0.005, 0.005, self.wood, 0.0)
        return k

    def balustrade_kit(self, length=W, height=1.0, col=None, cap=None):
        return super().balustrade_kit(length, height, self.wall_col, self.trim)

    def p_corner(self):
        k = Kit()
        k.box((0, 0, H / 2), (0.4, 0.4, H), self.wall_col, 0.05)
        k.box((0, 0, self.plinth_h / 2), (0.46, 0.46, self.plinth_h), self.stone, 0.03)
        for z in (H - 0.42, H - 0.08):
            k.box((0, 0, z), (0.46, 0.46, 0.1), self.trim, 0.02)
        return k

    def p_cornice(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, 0.0, 0.12, -FY, FY + 0.06, self.wall_col, 0.015)
        for i in range(10):
            x = -W / 2 + (i + 0.5) * W / 10
            hbox(k, x - 0.07, x + 0.07, 0.0, 0.12, FY + 0.06, FY + 0.12, self.trim, 0.012)
            hbox(k, x - 0.1, x + 0.1, 0.12, 0.2, FY + 0.06, FY + 0.18, self.trim, 0.012)
        hbox(k, -W / 2, W / 2, 0.2, 0.3, -FY, FY + 0.22, self.trim2, 0.015)
        hbox(k, -W / 2, W / 2, 0.3, 0.42, -FY, FY + 0.25, self.wall_col, 0.02)
        return k

    def p_floor_band(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, -0.12, 0.12, FY - 0.02, FY + 0.06, self.wall_col, 0.015)
        for z in (-0.12, 0.08):
            hbox(k, -W / 2, W / 2, z, z + 0.04, FY + 0.04, FY + 0.09, self.trim, 0.008)
        for i in range(12):
            k.box((-W / 2 + (i + 0.5) * W / 12, FY + 0.07, 0.0), (0.1, 0.04, 0.1), self.trim2, 0.01, R("Y", 45))
        return k

    def p_parapet(self):
        k = self.wall(height=0.7, plinth=False)
        hbox(k, -W / 2, W / 2, 0.7, 0.78, -FY - 0.05, FY + 0.05, self.trim, 0.02)
        for i in range(6):
            x = -W / 2 + (i + 0.5) * W / 6
            for j, (w, h) in enumerate(((0.36, 0.08), (0.24, 0.07), (0.1, 0.07))):
                z0 = 0.78 + sum(hh for _, hh in ((0.36, 0.08), (0.24, 0.07), (0.1, 0.07))[:j])
                hbox(k, x - w / 2, x + w / 2, z0, z0 + h, -0.1, 0.1, self.wall_col, 0.012)
        return k

    def p_roof_slab(self):
        k = Kit()
        k.box((0, 0, 0.08), (3.0, 3.0, 0.16), tinted(0.9, 0.86, 0.8), 0.02)
        for i in range(3):
            for j in range(3):
                k.box((-1.0 + i, -1.0 + j, 0.165), (0.96, 0.96, 0.02), shade(tinted(0.92, 0.88, 0.82), 0.95 + 0.05 * ((i + j) % 2)), 0.005)
        return k

    def p_column(self):
        k = Kit()
        k.box((0, 0, 0.15), (0.5, 0.5, 0.3), self.stone, 0.03)
        k.lathe((0, 0, 0.3), [(0.22, 0.0), (0.22, 0.06), (0.18, 0.1), (0.18, 0.14)], self.trim, n=14)
        k.cyl((0, 0, 0.44 + (H - 0.9) / 2), 0.17, H - 0.9, self.wall_col, n=14, r2=0.15, bevel=0.0)
        k.lathe((0, 0, H - 0.46), [(0.15, 0.0), (0.17, 0.04), (0.17, 0.08), (0.2, 0.14), (0.25, 0.26), (0.27, 0.3)], self.trim, n=14)
        k.box((0, 0, H - 0.08), (0.6, 0.6, 0.16), self.trim2, 0.025)
        return k

    def p_chimney(self):
        # Wind tower (barjeel): slotted shaft with a crenellated crown.
        k = Kit()
        k.box((0, 0, 1.1), (1.1, 1.1, 2.2), self.wall_col, 0.03)
        for s in (-1, 1):
            for axis in ("X", "Y"):
                for i in range(3):
                    off = -0.3 + i * 0.3
                    c = (off, s * 0.55, 1.75) if axis == "X" else (s * 0.55, off, 1.75)
                    size = (0.14, 0.06, 0.8) if axis == "X" else (0.06, 0.14, 0.8)
                    k.box(c, size, self.wood, 0.01)
        k.box((0, 0, 2.25), (1.24, 1.24, 0.1), self.trim, 0.02)
        for sx in (-1, 0, 1):
            for sy in (-1, 0, 1):
                if sx == 0 and sy == 0:
                    continue
                k.box((sx * 0.5, sy * 0.5, 2.4), (0.18, 0.18, 0.2), self.wall_col, 0.02)
        return k


def _lattice_side(st, y0, y1, z0, z1):
    """Side panel of a mashrabiya bay (in the YZ plane, centred on x=0)."""
    k = Kit()
    n = max(int((y1 - y0) / 0.1), 1)
    for i in range(1, n):
        y = y0 + (y1 - y0) * i / n
        k.box((0, y, (z0 + z1) / 2), (0.03, 0.03, z1 - z0), st.wood, 0.0)
    m = max(int((z1 - z0) / 0.1), 1)
    for j in range(1, m):
        z = z0 + (z1 - z0) * j / m
        k.box((0, (y0 + y1) / 2, z), (0.03, y1 - y0, 0.03), st.wood, 0.0)
    return k


# ================================================================ Suburb (clapboard + brick)
class Suburb(Style):
    name = "Suburb"
    plinth_h = 0.35
    arch_kind = None
    tint_wall = (0.62, 0.74, 0.82)
    tint_roof = (0.38, 0.38, 0.42)
    wall_col = tinted(0.96, 0.96, 0.95)
    back_col = tinted(0.94, 0.93, 0.9)
    trim = fixed(0.95, 0.95, 0.93)
    trim2 = fixed(0.85, 0.85, 0.83)
    trim_in = fixed(0.93, 0.92, 0.9)
    stone = fixed(0.6, 0.6, 0.6)
    wood = fixed(0.45, 0.3, 0.18)
    shutter = fixed(0.16, 0.22, 0.28)
    step_col = fixed(0.66, 0.66, 0.64)
    cheek_col = fixed(0.62, 0.62, 0.6)
    brick_mortar = fixed(0.75, 0.73, 0.7)
    roof_col = tinted(0.92, 0.92, 0.92)
    gable_col = fixed(0.92, 0.92, 0.9)
    ridge_col = fixed(0.3, 0.3, 0.33)
    tread_col = fixed(0.5, 0.34, 0.2)
    clad_out = 0.18
    brick = False

    def slab_colour(self, face):
        if face.normal.y < -0.5:
            return self.back_col
        return self.brick_mortar if self.brick else self.wall_col

    def cladding(self, k, openings, width, height, z0):
        if self.brick:
            self.brick_courses(k, openings, width, height, z0)
            return
        n = max(int(round((height - z0) / 0.18)), 1)
        step = (height - z0) / n
        for i in range(n):
            zb = z0 + i * step
            for a, b in intervals(-width / 2, width / 2, zb, zb + step, openings):
                k.box(((a + b) / 2, FY + 0.016, zb + step / 2 - 0.001), (b - a, 0.022, step + 0.004),
                      shade(self.wall_col, 0.97 + 0.03 * (i % 2)), 0.006, R("X", -7))

    def brick_courses(self, k, openings, width, height, z0):
        rng = rng_for("brick" + str(len(k.faces)))
        ch = 0.15
        n = int((height - z0) / ch)
        ch = (height - z0) / n
        for i in range(n):
            zb = z0 + i * ch
            for a, b in intervals(-width / 2, width / 2, zb, zb + ch, openings):
                x = a - (0.23 if i % 2 else 0.0)
                while x < b:
                    x0, x1 = max(x, a), min(x + 0.46, b)
                    if x1 - x0 > 0.06:
                        out = FY + rng.uniform(0.018, 0.035)
                        hbox(k, x0 + 0.012, x1 - 0.012, zb + 0.014, zb + ch - 0.014, FY - 0.02, out,
                             shade(self.wall_col, rng.uniform(0.82, 1.0)), 0.0)
                    x += 0.46

    def shutter_panel(self, k, x, z0, w, h, y):
        for s in (-1, 1):
            k.box((x + s * (w / 2 - 0.03), y, z0 + h / 2), (0.06, 0.04, h), self.shutter, 0.008)
        for z in (z0 + 0.03, z0 + h / 2, z0 + h - 0.03):
            k.box((x, y, z), (w - 0.1, 0.04, 0.06), self.shutter, 0.008)
        n = int((h - 0.12) / 0.09)
        for half in (0, 1):
            za = z0 + 0.06 if half == 0 else z0 + h / 2 + 0.03
            zb = z0 + h / 2 - 0.03 if half == 0 else z0 + h - 0.06
            m = max(int((zb - za) / 0.085), 1)
            for i in range(m):
                z = za + (i + 0.5) * (zb - za) / m
                k.box((x, y, z), (w - 0.1, 0.012, 0.07), shade(self.shutter, 1.15), 0.0, R("X", 35))
        return n

    def dress_window(self, k, o, shutters=True):
        hw = o.w / 2
        surround(k, o, 0.0, 0.11, -0.02, 0.22, self.trim, bevel=0.012)
        hbox(k, o.cx - hw - 0.16, o.cx + hw + 0.16, o.top + 0.11, o.top + 0.2, 0.0, 0.25, self.trim, 0.012)
        hbox(k, o.cx - hw - 0.2, o.cx + hw + 0.2, o.top + 0.2, o.top + 0.25, 0.0, 0.29, self.trim, 0.012)
        hbox(k, o.cx - hw - 0.15, o.cx + hw + 0.15, o.z0 - 0.06, o.z0, -0.02, 0.3, self.trim, 0.012)
        hbox(k, o.cx - hw - 0.05, o.cx + hw + 0.05, o.z0 - 0.16, o.z0 - 0.06, 0.0, 0.22, self.trim, 0.01)
        if shutters and not self.brick:
            sw = 0.48
            for s in (-1, 1):
                self.shutter_panel(k, o.cx + s * (hw + 0.13 + sw / 2), o.z0, sw, o.h, FY + 0.06)
        if self.brick:
            self.soldier_lintel(k, o)

    def soldier_lintel(self, k, o):
        n = int((o.w + 0.4) / 0.11)
        for i in range(n):
            x = o.cx - o.w / 2 - 0.2 + (i + 0.5) * (o.w + 0.4) / n
            hbox(k, x - 0.045, x + 0.045, o.top + 0.26, o.top + 0.5, FY - 0.02, FY + 0.045, shade(self.wall_col, 0.85 + 0.1 * (i % 2)), 0.0)

    def dress_tall(self, k, o):
        self.dress_window(k, o)

    def dress_french(self, k, o):
        self.dress_window(k, o, shutters=False)

    def dress_door(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.13, -0.02, 0.23, self.trim, bevel=0.012)
        hbox(k, o.cx - hw - 0.22, o.cx + hw + 0.22, o.top + 0.13, o.top + 0.3, 0.0, 0.26, self.trim, 0.012)
        hbox(k, o.cx - hw - 0.28, o.cx + hw + 0.28, o.top + 0.3, o.top + 0.36, 0.0, 0.32, self.trim, 0.012)
        k.prism([(o.cx - hw - 0.24, o.top + 0.36), (o.cx + hw + 0.24, o.top + 0.36), (o.cx, o.top + 0.62)], 0.02, 0.26, self.trim, "Y", 0.012)
        hbox(k, o.cx - hw - 0.35, o.cx + hw + 0.35, 0.0, 0.16, -FY, FY + 0.6, self.stone, 0.02)
        if self.brick:
            return

    def dress_double(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.13, -0.02, 0.23, self.trim, bevel=0.012)
        hbox(k, -W / 2 + 0.02, W / 2 - 0.02, o.top + 0.13, o.top + 0.3, 0.0, 0.26, self.trim, 0.012)
        hbox(k, -W / 2, W / 2, o.top + 0.3, o.top + 0.36, 0.0, 0.32, self.trim, 0.012)
        hbox(k, o.cx - hw - 0.2, o.cx + hw + 0.2, 0.0, 0.16, -FY, FY + 0.6, self.stone, 0.02)

    def dress_arch(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.14, -FY - 0.03, 0.23, self.trim, bevel=0.012)
        hbox(k, o.cx - hw - 0.2, o.cx + hw + 0.2, o.top + 0.14, o.top + 0.3, -FY - 0.03, 0.27, self.trim, 0.012)

    def back_casing(self, k, o):
        if o.kind != "arch":
            super().back_casing(k, o)

    def balcony(self, k, o):
        width, depth = 2.4, 0.9
        y1 = FY + depth
        for i in range(8):
            x = -width / 2 + (i + 0.5) * width / 8
            hbox(k, x - width / 16 + 0.008, x + width / 16 - 0.008, -0.05, 0.02, -0.05, y1, shade(self.tread_col, 0.95 + 0.05 * (i % 2)), 0.008)
        hbox(k, -width / 2, width / 2, -0.22, -0.05, y1 - 0.05, y1 + 0.02, self.trim, 0.012)
        for x in (-width / 2 + 0.1, width / 2 - 0.1):
            hbox(k, x - 0.06, x + 0.06, -0.22, -0.05, -0.05, y1, self.trim, 0.012)
            k.prism([(FY, -0.05), (FY + 0.6, -0.05), (FY, -0.65)], x - 0.05, x + 0.05, self.trim, "X", 0.01)
        k.merge(self.railing_kit(width), T(0, y1 - 0.06, 0.02))
        side = self.railing_kit(depth - 0.06, ends=False)
        for s in (-1, 1):
            k.merge(side, T(s * (width / 2 - 0.06), FY + (depth - 0.06) / 2, 0.02) @ R("Z", 90))

    def p_corner(self):
        k = Kit()
        k.box((0, 0, self.plinth_h / 2), (0.44, 0.44, self.plinth_h), self.stone, 0.02)
        k.box((0, 0, (H + self.plinth_h) / 2), (0.4, 0.4, H - self.plinth_h), self.trim, 0.025)
        for s in (-1, 1):
            k.box((s * 0.12, 0.205, (H + self.plinth_h) / 2), (0.13, 0.02, H - self.plinth_h - 0.04), self.trim2, 0.005)
            k.box((0.205, s * 0.12, (H + self.plinth_h) / 2), (0.02, 0.13, H - self.plinth_h - 0.04), self.trim2, 0.005)
        return k

    def p_cornice(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, 0.0, 0.24, FY, FY + 0.06, self.trim, 0.01)
        hbox(k, -W / 2, W / 2, 0.24, 0.3, -FY, FY + 0.42, self.trim, 0.01)
        hbox(k, -W / 2, W / 2, 0.22, 0.44, FY + 0.38, FY + 0.44, self.trim, 0.012)
        hbox(k, -W / 2, W / 2, 0.44, 0.48, FY + 0.34, FY + 0.48, self.trim, 0.01)
        hbox(k, -W / 2, W / 2, 0.06, 0.1, FY + 0.06, FY + 0.1, self.trim, 0.008)
        k.prism([(FY + 0.48, 0.32), (FY + 0.62, 0.32), (FY + 0.64, 0.44), (FY + 0.6, 0.44), (FY + 0.56, 0.36), (FY + 0.48, 0.36)],
                -W / 2, W / 2, fixed(0.88, 0.88, 0.88), "X", 0.005)
        return k

    def p_floor_band(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, -0.11, 0.11, FY, 0.22, self.trim, 0.012)
        hbox(k, -W / 2, W / 2, 0.11, 0.15, FY, 0.25, self.trim, 0.01)
        return k

    def p_parapet(self):
        k = self.wall(height=0.88, plinth=False)
        hbox(k, -W / 2, W / 2, 0.88, 0.94, -FY - 0.04, FY + 0.07, self.trim, 0.012)
        hbox(k, -W / 2, W / 2, 0.94, 1.0, -FY - 0.06, FY + 0.09, self.trim, 0.012)
        return k

    def p_column(self):
        k = Kit()
        k.box((0, 0, 0.12), (0.46, 0.46, 0.24), self.trim, 0.02)
        k.lathe((0, 0, 0.24), [(0.19, 0.0), (0.19, 0.06), (0.16, 0.09)], self.trim, n=14)
        k.cyl((0, 0, 0.33 + (H - 0.66) / 2), 0.15, H - 0.66, self.trim, n=14, r2=0.125, bevel=0.0)
        k.lathe((0, 0, H - 0.33), [(0.125, 0.0), (0.15, 0.05), (0.15, 0.1), (0.17, 0.14)], self.trim, n=14)
        k.box((0, 0, H - 0.1), (0.42, 0.42, 0.2), self.trim, 0.02)
        return k

    def p_chimney(self):
        k = Kit()
        bm_col = tinted(0.85, 0.55, 0.45)
        k.box((0, 0, 1.2), (0.86, 0.56, 2.4), self.brick_mortar, 0.01)
        rng = rng_for("SuburbChimney")
        for i in range(16):
            zb = i * 0.15
            for face_y, length in ((0.28, 0.86), (-0.28, 0.86)):
                x = -length / 2 - (0.2 if i % 2 else 0.0)
                while x < length / 2:
                    x0, x1 = max(x, -length / 2), min(x + 0.4, length / 2)
                    if x1 - x0 > 0.05:
                        hbox(k, x0 + 0.01, x1 - 0.01, zb + 0.012, zb + 0.138, face_y - 0.02 * (1 if face_y > 0 else -1),
                             face_y + 0.02 * (1 if face_y > 0 else -1), shade(bm_col, rng.uniform(0.8, 1.0)), 0.0)
                    x += 0.4
            for face_x in (0.43, -0.43):
                y = -0.28 - (0.2 if i % 2 == 0 else 0.0)
                while y < 0.28:
                    y0, y1 = max(y, -0.28), min(y + 0.4, 0.28)
                    if y1 - y0 > 0.05:
                        a, b = sorted((face_x - 0.02 * (1 if face_x > 0 else -1), face_x + 0.02 * (1 if face_x > 0 else -1)))
                        k.box(((a + b) / 2, (y0 + y1) / 2, zb + 0.075), (b - a, y1 - y0 - 0.02, 0.126), shade(bm_col, rng.uniform(0.8, 1.0)), 0.0)
                    y += 0.4
        k.box((0, 0, 2.45), (1.0, 0.7, 0.1), fixed(0.6, 0.6, 0.6), 0.02)
        k.cyl((0.18, 0, 2.62), 0.09, 0.26, fixed(0.55, 0.55, 0.57), n=10)
        return k

    def pieces(self):
        base = super().pieces()
        brick = SuburbBrick()
        tw = (0.78, 0.38, 0.3)
        note = " Brick variant (bricks tintable, mortar fixed). 3x3.4 module, origin bottom centre of the wall centre line."
        base += [
            Piece("WallBrick", "wall", brick.p_wall, "Plain brick wall." + note, tw),
            Piece("WallBrickWindow", "wall", brick.p_wall_window, "1.2x1.5 window hole at 0.9 m." + note, tw),
            Piece("WallBrickDoor", "wall", brick.p_wall_door, "1.2x2.4 door hole." + note, tw),
            Piece("WallBrickHalf", "wall", brick.p_wall_half, "1.5 m filler." + note, tw),
        ]
        return base


class SuburbBrick(Suburb):
    name = "SuburbBrick"
    brick = True
    wall_col = tinted(0.97, 0.92, 0.88)


# ================================================================ Classic (stone mansion / hotel)
class Classic(Style):
    name = "Classic"
    plinth_h = 0.6
    arch_kind = "round"
    tint_wall = (0.93, 0.88, 0.78)
    tint_roof = (0.36, 0.42, 0.48)
    wall_col = tinted(0.97, 0.96, 0.93)
    back_col = tinted(0.95, 0.93, 0.9)
    joint = tinted(0.78, 0.76, 0.72)
    trim = fixed(0.93, 0.91, 0.86)
    trim2 = fixed(0.8, 0.77, 0.7)
    trim_in = fixed(0.9, 0.88, 0.84)
    stone = fixed(0.6, 0.58, 0.55)
    wood = fixed(0.32, 0.2, 0.12)
    metal = fixed(0.1, 0.1, 0.12)
    roof_col = tinted(0.9, 0.92, 0.95)
    gable_col = fixed(0.9, 0.88, 0.83)
    ridge_col = fixed(0.3, 0.32, 0.35)
    tread_col = fixed(0.88, 0.86, 0.84)
    step_col = fixed(0.84, 0.82, 0.78)
    clad_out = 0.18

    def slab_colour(self, face):
        return self.back_col if face.normal.y < -0.5 else self.joint

    def plinth(self, k, openings, width):
        for a, b in intervals(-width / 2, width / 2, 0.0, self.plinth_h, openings):
            hbox(k, a, b, 0.0, self.plinth_h - 0.1, FY - 0.02, FY + 0.07, self.stone, 0.03)
            hbox(k, a, b, self.plinth_h - 0.1, self.plinth_h, FY - 0.02, FY + 0.05, shade(self.stone, 1.12), 0.035)

    def cladding(self, k, openings, width, height, z0):
        rng = rng_for("ashlar" + str(len(k.faces)))
        n = max(int(round((height - z0) / 0.4)), 1)
        ch = (height - z0) / n
        for i in range(n):
            zb = z0 + i * ch
            for a, b in intervals(-width / 2, width / 2, zb, zb + ch, openings):
                x = a - (0.5 if i % 2 else 0.0)
                while x < b:
                    x0, x1 = max(x, a), min(x + 1.0, b)
                    if x1 - x0 > 0.08:
                        hbox(k, x0 + 0.015, x1 - 0.015, zb + 0.015, zb + ch - 0.015, FY - 0.02, FY + 0.03,
                             shade(self.wall_col, rng.uniform(0.93, 1.0)), 0.018)
                    x += 1.0

    def frieze(self, k, openings, width, height):
        pass

    def keystone(self, k, cx, z, h=0.32, w=0.2, y1=0.28):
        k.prism([(cx - w * 0.38, z), (cx + w * 0.38, z), (cx + w / 2, z + h), (cx - w / 2, z + h)], 0.0, y1, self.trim, "Y", 0.015)

    def dress_window(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.15, -0.02, 0.24, self.trim)
        surround(k, o, 0.15, 0.2, -0.02, 0.21, self.trim2)
        self.keystone(k, o.cx, o.top - 0.05)
        hbox(k, o.cx - hw - 0.3, o.cx + hw + 0.3, o.top + 0.22, o.top + 0.32, -0.02, 0.3, self.trim2, 0.015)
        hbox(k, o.cx - hw - 0.36, o.cx + hw + 0.36, o.top + 0.32, o.top + 0.4, -0.02, 0.36, self.trim, 0.02)
        self.sill_corbels(k, o)

    def sill_corbels(self, k, o):
        hw = o.w / 2
        hbox(k, o.cx - hw - 0.25, o.cx + hw + 0.25, o.z0 - 0.1, o.z0, -0.02, 0.33, self.trim, 0.02)
        for s in (-1, 1):
            k.prism([(FY, o.z0 - 0.1), (0.3, o.z0 - 0.1), (0.24, o.z0 - 0.2), (FY, o.z0 - 0.36)], o.cx + s * (hw + 0.1) - 0.07, o.cx + s * (hw + 0.1) + 0.07,
                    self.trim, "X", 0.012)
        if o.z0 > 0.7:
            hbox(k, o.cx - hw + 0.05, o.cx + hw - 0.05, o.z0 - 0.42, o.z0 - 0.14, FY, FY + 0.05, self.trim2, 0.012)

    def pediment(self, k, cx, z, half, rise, y1):
        k.prism([(cx - half, z), (cx + half, z), (cx, z + rise)], 0.0, y1 - 0.06, self.trim2, "Y", 0.015)
        for s in (-1, 1):
            # beam(): for a beam lying in the XZ plane, w is the depth along Y and d the in-plane thickness
            k.beam((cx + s * (half + 0.03), y1 / 2, z + 0.02), (cx, y1 / 2, z + rise + 0.04), y1, 0.08, self.trim, 0.015)

    def dress_tall(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.15, -0.02, 0.24, self.trim)
        hbox(k, o.cx - hw - 0.32, o.cx + hw + 0.32, o.top + 0.15, o.top + 0.22, -0.02, 0.3, self.trim2, 0.015)
        hbox(k, o.cx - hw - 0.36, o.cx + hw + 0.36, o.top + 0.22, o.top + 0.28, -0.02, 0.35, self.trim, 0.02)
        self.pediment(k, o.cx, o.top + 0.28, hw + 0.36, 0.14, 0.35)  # stays under the 3.4 m module top
        hbox(k, o.cx - hw - 0.2, o.cx + hw + 0.2, o.z0 - 0.08, o.z0, -0.02, 0.3, self.trim, 0.015)
        for i in range(5):
            self.baluster(k, o.cx - 0.48 + i * 0.24, FY + 0.12, 0.0, max(o.z0 - 0.12, 0.2), self.trim)

    def dress_french(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.15, -0.02, 0.24, self.trim)
        self.keystone(k, o.cx, o.top - 0.05, 0.36)
        hbox(k, o.cx - hw - 0.36, o.cx + hw + 0.36, o.top + 0.3, o.top + 0.4, -0.02, 0.36, self.trim, 0.02)
        for s in (-1, 1):
            k.prism([(FY, o.top + 0.3), (0.36, o.top + 0.3), (FY, o.top - 0.1)], o.cx + s * (hw + 0.22) - 0.06, o.cx + s * (hw + 0.22) + 0.06, self.trim, "X", 0.01)

    def pilaster(self, k, x, z0, z1, w=0.24, y0=0.0, y1=0.27):
        hbox(k, x - w / 2 - 0.04, x + w / 2 + 0.04, z0, z0 + 0.2, y0, y1 + 0.03, self.trim2, 0.015)
        hbox(k, x - w / 2, x + w / 2, z0 + 0.2, z1 - 0.18, y0, y1, self.trim, 0.015)
        for i in range(3):
            fx = x - w / 2 + (i + 1) * w / 4
            hbox(k, fx - 0.015, fx + 0.015, z0 + 0.3, z1 - 0.28, y1 - 0.01, y1 + 0.012, self.trim2, 0.0)
        hbox(k, x - w / 2 - 0.05, x + w / 2 + 0.05, z1 - 0.18, z1, y0, y1 + 0.04, self.trim2, 0.015)

    def dress_door(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.1, -0.02, 0.22, self.trim)
        for s in (-1, 1):
            self.pilaster(k, o.cx + s * (hw + 0.23), 0.0, o.top + 0.05)
        hbox(k, o.cx - hw - 0.4, o.cx + hw + 0.4, o.top + 0.05, o.top + 0.3, -0.02, 0.28, self.trim, 0.015)
        hbox(k, o.cx - hw - 0.46, o.cx + hw + 0.46, o.top + 0.3, o.top + 0.4, -0.02, 0.36, self.trim2, 0.02)
        self.pediment(k, o.cx, o.top + 0.4, hw + 0.46, 0.4, 0.36)
        hbox(k, o.cx - hw - 0.45, o.cx + hw + 0.45, 0.0, 0.14, -FY, FY + 0.6, fixed(0.85, 0.83, 0.8), 0.02)

    def dress_double(self, k, o):
        hw = o.w / 2
        surround(k, o, 0.0, 0.08, -0.02, 0.22, self.trim)
        for s in (-1, 1):
            self.pilaster(k, o.cx + s * (hw + 0.18), 0.0, o.top + 0.05, 0.2)
        hbox(k, -W / 2, W / 2, o.top + 0.05, o.top + 0.3, -0.02, 0.28, self.trim, 0.015)
        n = 14
        for i in range(n):
            x = -W / 2 + 0.1 + (i + 0.5) * (W - 0.2) / n
            hbox(k, x - 0.04, x + 0.04, o.top + 0.3, o.top + 0.38, 0.0, 0.32, self.trim2, 0.008)
        hbox(k, -W / 2, W / 2, o.top + 0.38, o.top + 0.48, -0.02, 0.38, self.trim, 0.02)
        k.box((o.cx, 0.27, o.top + 0.18), (0.36, 0.08, 0.2), self.trim2, 0.02)
        hbox(k, o.cx - hw - 0.3, o.cx + hw + 0.3, 0.0, 0.14, -FY, FY + 0.6, fixed(0.85, 0.83, 0.8), 0.02)

    def dress_arch(self, k, o):
        surround(k, o, 0.0, 0.24, -FY - 0.03, 0.24, self.trim, self.trim2, segs=9)
        self.keystone(k, o.cx, o.top - 0.06, 0.42, 0.28, 0.3)
        for s in (-1, 1):
            hbox(k, o.cx + s * (o.w / 2) - 0.28, o.cx + s * (o.w / 2) + 0.28, o.spring - 0.16, o.spring, -FY - 0.05, 0.28, self.trim2, 0.02)

    def back_casing(self, k, o):
        if o.kind != "arch":
            super().back_casing(k, o)

    def balcony(self, k, o):
        width, depth = 2.4, 0.9
        y1 = FY + depth
        hbox(k, -width / 2, width / 2, -0.2, 0.02, -0.05, y1, self.trim, 0.03)
        hbox(k, -width / 2 - 0.04, width / 2 + 0.04, -0.26, -0.2, -0.05, y1 + 0.04, self.trim2, 0.015)
        for x in (-width / 2 + 0.25, 0.0, width / 2 - 0.25):
            k.prism([(FY, -0.26), (FY + 0.7, -0.26), (FY + 0.5, -0.4), (FY + 0.1, -0.5), (FY, -0.75)], x - 0.08, x + 0.08, self.trim, "X", 0.012)
        k.merge(self.balustrade_kit(width, 0.95), T(0, y1 - 0.18, 0.02))
        side = self.balustrade_kit(depth - 0.36, 0.95)
        for s in (-1, 1):
            k.merge(side, T(s * (width / 2 - 0.18), FY + (depth - 0.36) / 2, 0.02) @ R("Z", 90))

    def railing_kit(self, length, height=1.0, ends=True):
        k = Kit()
        iron = self.metal
        gold = fixed(0.75, 0.6, 0.25)
        hbox(k, -length / 2, length / 2, height - 0.05, height, -0.035, 0.035, iron, 0.01)
        hbox(k, -length / 2, length / 2, 0.08, 0.12, -0.025, 0.025, iron, 0.005)
        if ends:
            for x in (-length / 2 + 0.05, length / 2 - 0.05):
                k.box((x, 0, height / 2), (0.07, 0.07, height), iron, 0.01)
                k.lathe((x, 0, height), [(0.05, 0.0), (0.05, 0.03), (0.06, 0.07), (0.0, 0.13)], gold, n=8)
        n = int(length / 0.11)
        for i in range(n):
            x = -length / 2 + (i + 0.5) * length / n
            k.box((x, 0, (0.0 + height - 0.05) / 2), (0.018, 0.018, height - 0.05), iron, 0.0)
            if i % 3 == 1:
                k.box((x, 0, 0.55), (0.06, 0.03, 0.06), gold, 0.005, R("Y", 45))
        return k

    def p_corner(self):
        k = Kit()
        k.box((0, 0, self.plinth_h / 2), (0.5, 0.5, self.plinth_h), self.stone, 0.03)
        n = 7
        ch = (H - self.plinth_h - 0.3) / n
        for i in range(n):
            z = self.plinth_h + (i + 0.5) * ch
            s = 0.46 if i % 2 == 0 else 0.42
            k.box((0, 0, z), (s, s, ch - 0.03), shade(self.trim, 0.96 + 0.04 * (i % 2)), 0.03)
        k.box((0, 0, H - 0.22), (0.5, 0.5, 0.16), self.trim2, 0.02)
        k.box((0, 0, H - 0.06), (0.56, 0.56, 0.12), self.trim, 0.02)
        return k

    def p_cornice(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, 0.0, 0.14, -FY, FY + 0.04, self.trim2, 0.012)
        hbox(k, -W / 2, W / 2, 0.14, 0.2, -FY, FY + 0.08, self.trim, 0.012)
        n = 24
        for i in range(n):
            x = -W / 2 + (i + 0.5) * W / n
            hbox(k, x - 0.035, x + 0.035, 0.2, 0.29, FY, FY + 0.12, self.trim, 0.006)
        for i in range(6):
            x = -W / 2 + (i + 0.5) * W / 6
            k.prism([(FY, 0.29), (FY + 0.24, 0.29), (FY + 0.24, 0.24), (FY + 0.06, 0.12), (FY, 0.12)], x - 0.06, x + 0.06, self.trim2, "X", 0.01)
        hbox(k, -W / 2, W / 2, 0.29, 0.4, -FY, FY + 0.27, self.trim, 0.02)
        k.prism([(-FY, 0.4), (FY + 0.25, 0.4), (FY + 0.18, 0.5), (-FY, 0.5)], -W / 2, W / 2, self.trim2, "X", 0.01)
        return k

    def p_floor_band(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, -0.13, 0.05, FY - 0.02, FY + 0.08, self.trim, 0.015)
        k.cyl((0, FY + 0.07, 0.08), 0.05, W, self.trim2, n=8, axis="X", bevel=0.0)
        hbox(k, -W / 2, W / 2, 0.08, 0.13, FY - 0.02, FY + 0.06, self.trim, 0.01)
        return k

    def p_parapet(self):
        k = Kit()
        hbox(k, -W / 2, W / 2, 0.0, 0.85, -FY, FY, self.wall_col, 0.02)
        hbox(k, -W / 2, W / 2, 0.0, 0.15, -FY - 0.03, FY + 0.04, self.trim2, 0.015)
        for x in (-0.75, 0.75):
            hbox(k, x - 0.6, x + 0.6, 0.25, 0.75, FY, FY + 0.03, self.trim2, 0.012)
            hbox(k, x - 0.5, x + 0.5, 0.33, 0.67, FY + 0.01, FY + 0.035, self.joint, 0.008)
        hbox(k, -W / 2, W / 2, 0.85, 1.0, -FY - 0.06, FY + 0.08, self.trim, 0.025)
        return k

    def p_column(self):
        k = Kit()
        k.box((0, 0, 0.12), (0.62, 0.62, 0.24), self.trim2, 0.025)
        k.lathe((0, 0, 0.24), [(0.27, 0.0), (0.27, 0.05), (0.24, 0.09), (0.25, 0.12), (0.21, 0.16), (0.21, 0.2)], self.trim, n=16)
        pts = []
        for i in range(32):
            a = 2 * math.pi * i / 32
            r = 0.2 if i % 2 == 0 else 0.18
            pts.append((r * math.cos(a), r * math.sin(a)))
        k.prism(pts, 0.44, H - 0.5, self.trim, "Z", 0.0)
        k.lathe((0, 0, H - 0.5), [(0.2, 0.0), (0.22, 0.04), (0.22, 0.08), (0.2, 0.1), (0.24, 0.16), (0.3, 0.26)], self.trim, n=16)
        k.box((0, 0, H - 0.12), (0.68, 0.68, 0.24), self.trim2, 0.025)
        for s in (-1, 1):
            k.cyl((s * 0.28, 0, H - 0.3), 0.08, 0.6, self.trim, n=10, axis="Y")
        return k

    def p_chimney(self):
        k = Kit()
        k.box((0, 0, 1.15), (1.0, 0.6, 2.3), self.wall_col, 0.03)
        for z in (0.6, 1.2, 1.8):
            k.box((0, 0, z), (1.03, 0.63, 0.04), self.joint, 0.005)
        k.box((0, 0, 2.36), (1.12, 0.72, 0.12), self.trim2, 0.02)
        k.box((0, 0, 2.48), (1.2, 0.8, 0.12), self.trim, 0.025)
        for x in (-0.28, 0.0, 0.28):
            k.lathe((x, 0, 2.54), [(0.1, 0.0), (0.1, 0.05), (0.07, 0.08), (0.07, 0.26), (0.09, 0.3), (0.09, 0.34)], fixed(0.65, 0.42, 0.32), n=10)
        return k

    def p_stairs_outdoor(self):
        k = stairs_outdoor(self)
        for s in (-1, 1):
            x = s * 1.35
            y = 0.36 * 9 / 2 - 0.05
            k.box((x, y + 0.08, 0.75), (0.4, 0.4, 0.5), self.trim2, 0.02)
            k.lathe((x, y + 0.08, 1.0), [(0.12, 0.0), (0.08, 0.05), (0.06, 0.08), (0.14, 0.2), (0.12, 0.3), (0.0, 0.36)], self.trim, n=10)
        return k


STYLES = [Timber, Stucco, Suburb, Classic]


def build_all(names=None, only=None):
    report = {}
    for row, cls in enumerate(STYLES):
        if names and cls.name not in names:
            continue
        st = cls()
        entries, failures = kc.run_style(st.name, st.pieces(), only=only, columns=10, spacing=6.0, row=row * 4)
        report[st.name] = (len(entries), failures)
    return report


if __name__ == "__main__":
    print(build_all(globals().get("KIT_STYLES"), globals().get("KIT_ONLY")))
