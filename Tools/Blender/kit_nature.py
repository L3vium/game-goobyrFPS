# Author : Baptiste Vu Liem
# Nature kit of Goobyr FPS (style "Nature"): layered pines, puffy broadleaves, birch, curved palms,
# cypress, dead tree, bushes, clipped hedge, flower bed, grass, reeds, faceted rocks, a cliff chunk
# and a fallen log. Foliage / rock surfaces are tintable (alpha 1), bark and flowers fixed (alpha 0).
# Origin at the bottom centre. Exports to Assets/_Game/Art/Resources/Kit/Nature/.
import importlib
import math
import sys

import bmesh

KIT_DIR = globals().get("KIT_DIR", r"D:\_Project\_GamesUnity\_Isart\Goobyr FPS\Tools\Blender")
if KIT_DIR not in sys.path:
    sys.path.insert(0, KIT_DIR)
import kit_common as kc  # noqa: E402

importlib.reload(kc)
from kit_common import Kit, Piece, fixed, tinted, shade, T, R, S, rng_for  # noqa: E402
from mathutils import Vector  # noqa: E402

LEAF = tinted(0.93, 0.96, 0.9)
ROCK = tinted(0.92, 0.92, 0.9)
BARK = fixed(0.4, 0.28, 0.18)
BARK_D = fixed(0.3, 0.21, 0.14)
BIRCH = fixed(0.92, 0.9, 0.85)
DEAD = fixed(0.48, 0.42, 0.36)
SOIL = fixed(0.32, 0.22, 0.15)
WOOD_CUT = fixed(0.78, 0.62, 0.42)
MOSS = fixed(0.35, 0.5, 0.22)
FLOWERS = [fixed(0.92, 0.25, 0.3), fixed(0.97, 0.8, 0.2), fixed(0.65, 0.4, 0.85), fixed(0.97, 0.97, 0.95), fixed(0.95, 0.5, 0.2)]


# ---------------------------------------------------------------- organic helpers
def bm_skirt(r_out, r_in, h, tips, droop, rng):
    """Jagged cone of a pine tier: star-shaped rim (tips hang down by droop), concave underside."""
    bm = bmesh.new()
    apex = bm.verts.new((rng.uniform(-0.05, 0.05) * r_out, rng.uniform(-0.05, 0.05) * r_out, h))
    under = bm.verts.new((0, 0, h * 0.3))
    rim = []
    phase = rng.uniform(0, math.pi)
    for i in range(tips * 2):
        a = phase + math.pi * i / tips
        tip = i % 2 == 0
        r = (r_out * rng.uniform(0.9, 1.08)) if tip else r_in
        z = -droop * rng.uniform(0.8, 1.2) if tip else 0.0
        rim.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
    n = len(rim)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((apex, rim[i], rim[j]))
        bm.faces.new((under, rim[j], rim[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def flatten_bottom(bm, z=0.0):
    for v in bm.verts:
        if v.co.z < z:
            v.co.z = z
    return bm


def lumpy_rock(k, c, size, rng, col=ROCK, points=16, sink=0.15):
    """Faceted boulder: convex hull of random points on an ellipsoid, bottom sunk and flattened."""
    pts = []
    for _ in range(points):
        d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized()
        r = rng.uniform(0.82, 1.08)
        pts.append(Vector((c[0] + d.x * size[0] * r, c[1] + d.y * size[1] * r, max(c[2] + d.z * size[2] * r, c[2] - size[2] * (1 - sink)))))
    bm = kc.bm_hull([tuple(p) for p in pts])
    bm.transform(T(c[0], c[1], 0) @ R("Z", rng.uniform(0, 360)) @ T(-c[0], -c[1], 0))
    flatten_bottom(bm, 0.0)
    kc.bevel_bm(bm, min(size) * 0.04, 25)

    def colour(face):
        return shade(col, 1.06) if face.normal.z > 0.6 else shade(col, rng.uniform(0.84, 0.98))

    k.add(bm, colour)


def puff(k, c, r, rng, col=LEAF, sub=1, jitter=0.14, scale=(1, 1, 1)):
    k.blob(c, r, shade(col, rng.uniform(0.82, 1.0)), rng, sub=sub, jitter=jitter, scale=scale)


def trunk(k, points, r0, r1, col=BARK, n=8):
    count = len(points)
    radii = [r0 + (r1 - r0) * i / (count - 1) for i in range(count)]
    k.tube(points, radii, col, n=n)


def curve(a, b, bend, steps=5):
    """Points from a to b bowed sideways by the vector bend."""
    a, b, bend = Vector(a), Vector(b), Vector(bend)
    return [tuple(a.lerp(b, t) + bend * math.sin(math.pi * t)) for t in (i / steps for i in range(steps + 1))]


# ---------------------------------------------------------------- trees
def pine(height, tiers, seed):
    def build():
        k = Kit()
        rng = rng_for("pine" + seed)
        trunk(k, [(0, 0, 0), (0.05, 0.0, height * 0.5), (0.0, 0.03, height * 0.92)], height * 0.035, height * 0.012, BARK_D, n=7)
        k.cyl((0, 0, 0.1), height * 0.05, 0.2, BARK_D, n=7, r2=height * 0.035, bevel=0.0)
        base = height * 0.18
        top = height
        for i in range(tiers):
            t = i / (tiers - 1)
            z = base + (top - base - height * 0.12) * (t ** 0.9)
            r = height * 0.26 * (1 - t * 0.82)
            h = height * 0.22 * (1 - t * 0.35)
            col = shade(LEAF, 0.78 + 0.22 * t * rng.uniform(0.9, 1.05))
            spin = rng.uniform(0, 360)
            k.add(bm_skirt(r, r * 0.6, h, 10 + (2 if i % 2 else 0), r * 0.24, rng), col,
                  T(rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), z) @ R("Z", spin))
            # inner, offset skirt: a second row of needle tips for depth
            k.add(bm_skirt(r * 0.72, r * 0.45, h * 0.8, 9, r * 0.2, rng), shade(col, 1.08), T(0, 0, z + h * 0.22) @ R("Z", spin + 17))
        k.add(bm_skirt(height * 0.05, height * 0.03, height * 0.12, 5, 0.05, rng), LEAF, T(0, 0, top - height * 0.12))
        return k
    return build


def broadleaf(height, spread, seed, puffs=9):
    def build():
        k = Kit()
        rng = rng_for("broad" + seed)
        h0 = height * 0.42
        trunk(k, curve((0, 0, 0), (0.1, 0.05, h0), (0.15, 0.0, 0.0), 4), height * 0.06, height * 0.04)
        k.cyl((0, 0, 0.12), height * 0.085, 0.24, BARK, n=8, r2=height * 0.06, bevel=0.0)
        ends = []
        for i in range(3):
            a = 2 * math.pi * i / 3 + rng.uniform(-0.3, 0.3)
            end = (math.cos(a) * spread * 0.45, math.sin(a) * spread * 0.45, height * rng.uniform(0.6, 0.72))
            trunk(k, curve((0.1, 0.05, h0 - 0.2), end, (0, 0, 0.3), 3), height * 0.035, height * 0.015)
            ends.append(end)
        crown = height * 0.68
        puff(k, (0, 0, crown), spread * 0.55, rng, sub=2, jitter=0.1, scale=(1, 1, 0.82))
        for i in range(puffs):
            a = 2 * math.pi * i / puffs + rng.uniform(-0.25, 0.25)
            r = spread * rng.uniform(0.4, 0.62)
            z = crown + rng.uniform(-0.18, 0.3) * height * 0.3
            puff(k, (math.cos(a) * r, math.sin(a) * r, z), spread * rng.uniform(0.3, 0.42), rng)
        for e in ends:
            puff(k, (e[0] * 1.2, e[1] * 1.2, e[2] + 0.2), spread * 0.33, rng)
        puff(k, (0, 0, crown + spread * 0.5), spread * 0.36, rng)
        return k
    return build


def birch():
    k = Kit()
    rng = rng_for("birch")
    stems = [((0, 0, 0), (0.3, 0.1, 7.5), (0.25, 0, 0)), ((0.1, 0.05, 0), (-0.6, 0.4, 6.4), (-0.2, 0.1, 0))]
    for a, b, bend in stems:
        pts = curve(a, b, bend, 6)
        trunk(k, pts, 0.16, 0.05, BIRCH, n=7)
        for i in range(1, 6):
            p = Vector(pts[i])
            k.box((p.x + 0.12, p.y, p.z + rng.uniform(-0.3, 0.3)), (0.06, 0.12, 0.05), fixed(0.15, 0.14, 0.13), 0.0, R("Z", rng.uniform(0, 360)))
        top = Vector(b)
        for j in range(6):
            off = Vector((rng.uniform(-1.1, 1.1), rng.uniform(-1.1, 1.1), rng.uniform(-2.2, 0.4)))
            puff(k, tuple(top + off), rng.uniform(0.7, 1.0), rng, scale=(1, 1, 1.25))
    return k


def palm(height, lean, seed):
    def build():
        k = Kit()
        rng = rng_for("palm" + seed)
        steps = 9
        top = Vector((lean, lean * 0.3, height))
        pts = []
        for i in range(steps + 1):
            t = i / steps
            pts.append(Vector((lean * t * t, lean * 0.3 * t * t, height * t)))
        for i in range(steps):
            a, b = pts[i], pts[i + 1]
            r = 0.24 - 0.09 * i / steps
            k.tube([tuple(a), tuple(a.lerp(b, 0.85)), tuple(b)], [r, r * 1.08, r * 0.9], shade(fixed(0.55, 0.42, 0.28), 0.9 + 0.1 * (i % 2)), n=8)
        k.blob(tuple(top + Vector((0, 0, 0.15))), 0.35, BARK, rng, sub=1, jitter=0.1)
        for j in range(3):
            a = 2 * math.pi * j / 3
            k.blob(tuple(top + Vector((math.cos(a) * 0.22, math.sin(a) * 0.22, -0.12))), 0.13, fixed(0.45, 0.32, 0.16), rng, sub=1, jitter=0.05)
        fronds = 9
        for f in range(fronds):
            a = 2 * math.pi * f / fronds + rng.uniform(-0.15, 0.15)
            d = Vector((math.cos(a), math.sin(a), 0))
            side = Vector((-d.y, d.x, 0))
            length = height * rng.uniform(0.3, 0.38)
            lift = rng.uniform(0.25, 0.6)
            segs = 6
            spine = []
            for i in range(segs + 1):
                t = i / segs
                spine.append(top + Vector((0, 0, 0.2)) + d * length * t + Vector((0, 0, length * (lift * t - 0.9 * t * t))))
            for i in range(segs):
                p0, p1 = spine[i], spine[i + 1]
                w0 = 0.12 + 0.55 * math.sin(math.pi * (i / segs) * 0.9 + 0.15)
                w1 = 0.12 + 0.55 * math.sin(math.pi * ((i + 1) / segs) * 0.9 + 0.15)
                droop = Vector((0, 0, -0.18))
                pts_h = [p0, p1, p0 + side * w0 + droop, p1 + side * w1 * 0.9 + droop, p0 - side * w0 + droop, p1 - side * w1 * 0.9 + droop]
                pts_h += [p + Vector((0, 0, -0.04)) for p in pts_h]
                k.hull([tuple(p) for p in pts_h], shade(LEAF, 0.82 + 0.18 * (i % 2)))
        return k
    return build


def cypress():
    k = Kit()
    rng = rng_for("cypress")
    trunk(k, [(0, 0, 0), (0, 0, 1.2)], 0.22, 0.16)
    profile = [(0.3, 0.0), (1.0, 1.2), (1.2, 3.0), (1.1, 5.0), (0.8, 7.0), (0.45, 8.6), (0.0, 10.0)]
    bm = kc.bm_lathe(profile, 10)
    for v in bm.verts:
        if 0.1 < v.co.z < 9.9:
            v.co.x *= rng.uniform(0.85, 1.15)
            v.co.y *= rng.uniform(0.85, 1.15)
    k.add(bm, shade(LEAF, 0.85), T(0, 0, 0.8))
    for i in range(14):
        z = rng.uniform(2.0, 9.0)
        rr = 1.1 * (1 - (z - 2) / 9) + 0.3
        a = rng.uniform(0, 2 * math.pi)
        puff(k, (math.cos(a) * rr * 0.8, math.sin(a) * rr * 0.8, z), rr * 0.55, rng, scale=(1, 1, 1.8))
    return k


def dead_tree():
    k = Kit()
    rng = rng_for("dead")
    trunk(k, curve((0, 0, 0), (0.4, 0.2, 5.5), (0.3, -0.2, 0), 6), 0.32, 0.06, DEAD)
    k.cyl((0, 0, 0.15), 0.45, 0.3, DEAD, n=8, r2=0.32, bevel=0.0)
    for i in range(6):
        z = rng.uniform(1.8, 4.6)
        a = rng.uniform(0, 2 * math.pi)
        base = Vector((0.4 * z / 5.5, 0.2 * z / 5.5, z))
        end = base + Vector((math.cos(a) * rng.uniform(1.2, 2.2), math.sin(a) * rng.uniform(1.2, 2.2), rng.uniform(0.6, 1.6)))
        pts = curve(base, end, (0, 0, 0.35), 3)
        trunk(k, pts, 0.11 * (1 - z / 7), 0.025, DEAD, n=6)
        tip = Vector(pts[-1])
        trunk(k, [tuple(tip), tuple(tip + Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 0.7)))], 0.03, 0.012, DEAD, n=5)
    for a in range(4):
        ang = math.radians(a * 90 + 20)
        trunk(k, [(0, 0, 0.25), (math.cos(ang) * 0.8, math.sin(ang) * 0.8, 0.0)], 0.16, 0.05, DEAD, n=6)
    return k


def bush(size, seed, flowers=False):
    def build():
        k = Kit()
        rng = rng_for("bush" + seed)
        n = 6 + int(size * 2)
        puff(k, (0, 0, size * 0.42), size * 0.5, rng, sub=1, jitter=0.16, scale=(1.1, 1, 0.85))
        for i in range(n):
            a = 2 * math.pi * i / n + rng.uniform(-0.3, 0.3)
            r = size * rng.uniform(0.3, 0.5)
            puff(k, (math.cos(a) * r, math.sin(a) * r, size * rng.uniform(0.25, 0.6)), size * rng.uniform(0.25, 0.36), rng)
        if flowers:
            for i in range(14):
                a = rng.uniform(0, 2 * math.pi)
                e = rng.uniform(0.2, 1.0)
                k.blob((math.cos(a) * size * 0.55 * e, math.sin(a) * size * 0.55 * e, size * (0.4 + 0.5 * (1 - e))), 0.06, rng.choice(FLOWERS), rng, sub=0, jitter=0.1)
        return k
    return build


def hedge_block():
    k = Kit()
    rng = rng_for("hedge")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3, use_grid_fill=True)
    half = Vector((1.0, 0.5, 1.1))
    for v in bm.verts:
        v.co = Vector((v.co.x * 2.0, v.co.y * 1.0, v.co.z * 2.2 + 1.1))
        if v.co.z < 0.01:
            continue
        rel = Vector((v.co.x / half.x, v.co.y / half.y, (v.co.z - 1.1) / half.z))
        out = Vector(tuple(math.copysign(1.0, c) if abs(c) > 0.99 else 0.0 for c in rel))
        out.z = max(out.z, 0.0)
        v.co += out * rng.uniform(-0.04, 0.05)  # clipped-leaf bumps on the outer faces
    kc.bevel_bm(bm, 0.06)
    k.add(bm, lambda f: shade(LEAF, 1.0 if f.normal.z > 0.5 else rng.uniform(0.82, 0.94)))
    for i in range(18):
        side = rng.choice((-1, 1))
        if rng.random() < 0.5:
            c = (side * 1.0, rng.uniform(-0.4, 0.4), rng.uniform(0.3, 2.0))
        else:
            c = (rng.uniform(-0.9, 0.9), side * 0.5, rng.uniform(0.3, 2.0))
        puff(k, c, rng.uniform(0.1, 0.16), rng, sub=1, jitter=0.2)
    return k


def flower_bed():
    k = Kit()
    rng = rng_for("flowerbed")
    stone = fixed(0.62, 0.6, 0.56)
    for sx in (-1, 1):
        k.box((sx * 0.95, 0, 0.15), (0.1, 1.0, 0.3), stone, 0.02)
    for sy in (-1, 1):
        k.box((0, sy * 0.45, 0.15), (2.0, 0.1, 0.3), stone, 0.02)
    k.box((0, 0, 0.12), (1.8, 0.8, 0.24), SOIL, 0.0)
    for i in range(12):
        x = -0.75 + (i % 6) * 0.3 + rng.uniform(-0.05, 0.05)
        y = -0.18 + (i // 6) * 0.36 + rng.uniform(-0.05, 0.05)
        puff(k, (x, y, 0.34), 0.16, rng, sub=1, jitter=0.2, scale=(1, 1, 0.75))
        col = rng.choice(FLOWERS)
        for j in range(3):
            k.blob((x + rng.uniform(-0.1, 0.1), y + rng.uniform(-0.1, 0.1), 0.46 + rng.uniform(0, 0.08)), 0.05, col, rng, sub=0, jitter=0.1)
    return k


def grass_tuft():
    k = Kit()
    rng = rng_for("grass")
    for i in range(14):
        a = 2 * math.pi * i / 14 + rng.uniform(-0.2, 0.2)
        r = rng.uniform(0.02, 0.12)
        base = (math.cos(a) * r, math.sin(a) * r, 0.0)
        h = rng.uniform(0.25, 0.45)
        tilt = rng.uniform(8, 30)
        k.cyl((0, 0, 0), 0.03, h, shade(LEAF, rng.uniform(0.8, 1.0)), n=3, r2=0.002, bevel=0.0,
              m=T(*base) @ R("Z", math.degrees(a)) @ R("Y", tilt) @ T(0, 0, h / 2))
    return k


def reeds_clump():
    k = Kit()
    rng = rng_for("reeds")
    for i in range(16):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.35)
        base = (math.cos(a) * r, math.sin(a) * r, 0.0)
        h = rng.uniform(1.0, 1.7)
        tilt = rng.uniform(0, 12)
        m = T(*base) @ R("Z", math.degrees(a)) @ R("Y", tilt)
        k.cyl((0, 0, 0), 0.025, h, shade(LEAF, rng.uniform(0.8, 1.0)), n=4, r2=0.006, bevel=0.0, m=m @ T(0, 0, h / 2))
        if i % 3 == 0:
            top = m @ Vector((0, 0, h * 0.82))
            k.cyl(tuple(top), 0.04, 0.22, fixed(0.42, 0.26, 0.14), n=6, bevel=0.0, m=R("Z", math.degrees(a)) @ R("Y", tilt))
    for i in range(6):
        a = 2 * math.pi * i / 6
        k.cyl((0, 0, 0), 0.07, 0.7, shade(LEAF, 0.85), n=3, r2=0.002, bevel=0.0,
              m=T(math.cos(a) * 0.1, math.sin(a) * 0.1, 0.0) @ R("Z", math.degrees(a)) @ R("Y", 35) @ T(0, 0, 0.35))
    return k


def rock(size, seed, extra=1):
    def build():
        k = Kit()
        rng = rng_for("rock" + seed)
        lumpy_rock(k, (0, 0, size * 0.32), (size * 0.6, size * 0.48, size * 0.42), rng)
        for i in range(extra):
            a = rng.uniform(0, 2 * math.pi)
            r = size * 0.45
            s = size * rng.uniform(0.18, 0.28)
            lumpy_rock(k, (math.cos(a) * r, math.sin(a) * r, s * 0.4), (s, s * 0.85, s * 0.7), rng)
        return k
    return build


def cliff_chunk():
    k = Kit()
    rng = rng_for("cliff")
    blocks = [((0, 0, 2.6), (3.6, 2.6, 2.9)), ((-2.2, 0.3, 1.8), (2.0, 2.0, 2.0)), ((2.3, -0.2, 2.1), (1.9, 2.2, 2.3)),
              ((0.4, 0.6, 4.6), (2.4, 2.0, 1.5)), ((-1.0, -0.6, 4.2), (2.0, 1.8, 1.6)), ((1.8, 0.9, 0.9), (1.6, 1.6, 1.0))]
    for c, s in blocks:
        lumpy_rock(k, c, s, rng, points=22, sink=0.0)
    for z in (1.6, 3.4):
        lumpy_rock(k, (rng.uniform(-1, 1), 1.9, z), (2.8, 0.7, 0.35), rng, points=14, sink=0.0)
    for i in range(5):
        puff(k, (rng.uniform(-3, 3), rng.uniform(-1.5, 1.5), 5.4 + rng.uniform(-0.3, 0.2)), rng.uniform(0.4, 0.7), rng, col=fixed(0.42, 0.6, 0.27))
    return k


def log():
    k = Kit()
    rng = rng_for("log")
    k.cyl((0, 0, 0.32), 0.32, 4.0, BARK, n=9, axis="X", bevel=0.04)
    for x in (-2.0, 2.0):
        k.cyl((x * 1.005, 0, 0.32), 0.27, 0.02, WOOD_CUT, n=9, axis="X", bevel=0.0)
        k.torus((x * 1.008, 0, 0.32), 0.15, 0.012, shade(WOOD_CUT, 0.85), seg=9, n=3, axis="X")
    trunk(k, [(0.6, 0.15, 0.55), (0.9, 0.5, 1.0)], 0.09, 0.05, BARK, n=6)
    for i in range(4):
        k.blob((rng.uniform(-1.6, 1.6), rng.uniform(-0.1, 0.1), 0.6), 1.0, MOSS, rng, sub=1, jitter=0.1, scale=(0.35, 0.25, 0.08))
    return k


def pieces():
    P = Piece
    pine_t, leaf_t, birch_t = (0.22, 0.42, 0.26), (0.36, 0.6, 0.26), (0.55, 0.72, 0.3)
    return [
        P("PineA", "tree", pine(8.0, 6, "A"), "Layered pine 8 m, foliage tintable.", pine_t),
        P("PineB", "tree", pine(11.0, 7, "B"), "Layered pine 11 m.", pine_t),
        P("PineC", "tree", pine(14.0, 8, "C"), "Layered pine 14 m.", pine_t),
        P("BroadleafA", "tree", broadleaf(7.0, 5.0, "A"), "Round puffy broadleaf 7 m.", leaf_t),
        P("BroadleafB", "tree", broadleaf(9.0, 6.0, "B", 11), "Round puffy broadleaf 9 m.", (0.42, 0.62, 0.25)),
        P("BroadleafC", "tree", broadleaf(5.5, 4.2, "C", 7), "Small broadleaf 5.5 m.", (0.5, 0.65, 0.28)),
        P("BirchA", "tree", birch, "Twin-stem birch 8 m, white bark.", birch_t),
        P("PalmA", "tree", palm(8.0, 1.4, "A"), "Curved palm 8.5 m.", (0.4, 0.66, 0.27)),
        P("PalmB", "tree", palm(10.5, 2.2, "B"), "Tall curved palm 11 m.", (0.42, 0.66, 0.28)),
        P("CypressA", "tree", cypress, "Columnar cypress 11 m.", (0.2, 0.4, 0.24)),
        P("DeadTree", "tree", dead_tree, "Leafless dead tree 6.5 m (all fixed colour)."),
        P("BushA", "bush", bush(1.2, "A"), "Bush 1.2 m.", (0.32, 0.55, 0.25)),
        P("BushB", "bush", bush(1.8, "B"), "Bush 1.8 m.", (0.28, 0.5, 0.24)),
        P("BushC", "bush", bush(1.0, "C", True), "Flowering bush 1 m.", (0.35, 0.58, 0.28)),
        P("HedgeBlock", "bush", hedge_block, "Clipped hedge block 2 x 2.2 x 1 (tileable along x).", (0.26, 0.48, 0.22)),
        P("FlowerBed", "bush", flower_bed, "Raised flower bed 2x1 with stone border.", (0.32, 0.55, 0.25)),
        P("GrassTuft", "grass", grass_tuft, "Grass tuft 0.45 m.", (0.45, 0.7, 0.3)),
        P("ReedsClump", "grass", reeds_clump, "Reeds with cattails 1.7 m (shore).", (0.5, 0.62, 0.3)),
        P("RockA", "rock", rock(1.0, "A", 0), "Faceted rock ~1 m, tintable.", (0.58, 0.57, 0.55)),
        P("RockB", "rock", rock(1.8, "B", 1), "Faceted rock ~1.8 m.", (0.58, 0.57, 0.55)),
        P("RockC", "rock", rock(2.8, "C", 2), "Faceted rock ~2.8 m.", (0.55, 0.54, 0.52)),
        P("RockD", "rock", rock(4.0, "D", 2), "Faceted boulder ~4 m.", (0.55, 0.54, 0.52)),
        P("CliffChunk", "rock", cliff_chunk, "Cliff face chunk ~8 x 6 x 6 m with ledges and grass tufts on top (fixed green); rock tintable.", (0.6, 0.56, 0.5)),
        P("Log", "rock", log, "Fallen log 4 m along x with a branch stub and moss."),
    ]


if __name__ == "__main__":
    kc.run_style("Nature", pieces(), only=globals().get("KIT_ONLY"), columns=10, spacing=16.0, row=30)
