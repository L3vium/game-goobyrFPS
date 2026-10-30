# Author : Baptiste Vu Liem
# Goobyr FPS character parts ("Goobs"), modelled in code with bpy/bmesh: torsos (the outfit is the torso),
# heads, hair, facial hair, hats, hands, feet and accessories. A Goob is a set of rigid parts animated
# procedurally by the game (GoobBody / GoobAnimator), so every part is one mesh with its origin at its pivot.
#
# Conventions (see Resources/Goob/manifest.json):
# - Metres, Blender Z up, the front of every part faces Blender +Y (the FBX settings + Unity's 180 deg turn
#   make it the Unity forward).
# - Vertex colour "Col" (BYTE_COLOR, CORNER). RGB is written as-is into the bytes (color_srgb), so Unity
#   receives exactly the authored values. ALPHA is the tint mask: 1 = recoloured by the game
#   (albedo = rgb * lerp(1, tint, alpha)), 0 = fixed detail colour. Tintable areas are authored near white.
# - Flat shading, everything triangulated, < 3k triangles per part.
#
# Run from Blender (Scripting tab) or by exec through MCP. Optional globals before exec:
#   OUT (export folder), ONLY (list of part names), EXPORT (bool), PREVIEW (bool).
# All work happens in the "GoobyrCharacters" scene; the user's current scene is restored afterwards.
import bpy
import bmesh
import json
import math
import os
from math import sin, cos, pi, asin, atan2, sqrt
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

OUT = globals().get("OUT", r"D:\_Project\_GamesUnity\_Isart\Goobyr FPS\Assets\_Game\Art\Resources\Goob")
ONLY = globals().get("ONLY", None)
EXPORT = globals().get("EXPORT", True)
PREVIEW = globals().get("PREVIEW", True)
SCENE_NAME = "GoobyrCharacters"

V = Vector
TAU = 2 * pi


# ----------------------------------------------------------------------------------------------------------
# Colours: T = tinted shade (alpha 1), F = fixed colour (alpha 0).
def T(v=0.95):
    return (v, v, v, 1.0)


def TC(r, g, b):
    return (r, g, b, 1.0)


def F(r, g=None, b=None):
    if g is None:
        g = b = r
    return (r, g, b, 0.0)


SKIN = T(0.95)
INK = F(0.1, 0.1, 0.12)
GOLD = F(1.0, 0.78, 0.25)
SILVER = F(0.78, 0.8, 0.84)
WHITE = F(0.97, 0.97, 0.97)


def clamp(x, a, b):
    return a if x < a else b if x > b else x


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(e0, e1, x):
    t = clamp((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def gauss(d2, s):
    return math.exp(-d2 / (2 * s * s))


# ----------------------------------------------------------------------------------------------------------
# Part builder: one bmesh, a face int layer indexing a small palette of RGBA colours.
class Part:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.lay = self.bm.faces.layers.int.new("pal")
        self.cols = []

    def cid(self, c):
        c = tuple(round(float(x), 4) for x in c)
        if c not in self.cols:
            self.cols.append(c)
        return self.cols.index(c)

    def add(self, pb, col, colfn=None, recalc=True):
        """Merge piece bmesh pb (freed). colfn(centre, normal) may return a colour per face (None = col)."""
        if recalc:
            bmesh.ops.recalc_face_normals(pb, faces=pb.faces[:])
        pb.normal_update()
        vmap = {v: self.bm.verts.new(v.co) for v in pb.verts}
        default = self.cid(col)
        for f in pb.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            c = colfn(f.calc_center_median(), f.normal) if colfn else None
            nf[self.lay] = self.cid(c) if c else default
        pb.free()

    def bvh(self):
        self.bm.normal_update()
        return BVHTree.FromBMesh(self.bm)


def bvh_of(pb):
    """BVH of a closed piece (normals made consistent and up to date first)."""
    bmesh.ops.recalc_face_normals(pb, faces=pb.faces[:])
    pb.normal_update()
    return BVHTree.FromBMesh(pb)


# ----------------------------------------------------------------------------------------------------------
# Primitive pieces (each returns a new bmesh).
def lathe(prof, segs=16, sx=1.0, sy=1.0, phase=0.0, loop=False, caps=True, fn=None, a0=None, a1=None):
    """Revolve a (r, z) profile (top to bottom) around Z. Angle 0 points to +Y (front). r == 0 is a pole.
    loop=True closes the profile (rings, tori). a0/a1 make an open partial revolution."""
    bm = bmesh.new()
    partial = a0 is not None
    cols = segs + 1 if partial else segs
    rings = []
    for (r, z) in prof:
        if r <= 1e-6 and not loop:
            rings.append([bm.verts.new((0, 0, z))])
            continue
        ring = []
        for i in range(cols):
            a = (a0 + (a1 - a0) * i / segs) if partial else phase + TAU * i / segs
            p = V((sin(a) * r * sx, cos(a) * r * sy, z))
            if fn:
                p = fn(p, a, z)
            ring.append(bm.verts.new(p))
        rings.append(ring)
    pairs = list(zip(rings[:-1], rings[1:]))
    if loop:
        pairs.append((rings[-1], rings[0]))
    nq = segs
    for A, B in pairs:
        for i in range(nq):
            i1 = i + 1 if partial else (i + 1) % segs
            if len(A) == 1 and len(B) == 1:
                continue
            if len(A) == 1:
                bm.faces.new((A[0], B[i1], B[i]))
            elif len(B) == 1:
                bm.faces.new((A[i], A[i1], B[0]))
            else:
                bm.faces.new((A[i], A[i1], B[i1], B[i]))
    if caps and not loop and not partial:
        if len(rings[0]) > 1:
            bm.faces.new(list(reversed(rings[0])))
        if len(rings[-1]) > 1:
            bm.faces.new(rings[-1])
    return bm


def ell(rx, ry, rz, segs=12, rings=8, fn=None):
    prof = [(sin(pi * k / rings), rz * cos(pi * k / rings)) for k in range(rings + 1)]
    prof[0] = (0.0, rz)
    prof[-1] = (0.0, -rz)
    return lathe(prof, segs, rx, ry, fn=fn)


def cyl(r, h, segs=12, bevel=0.0, r2=None, sy=1.0):
    """Cylinder along Z centred on the origin; r2 = top radius; bevel = chamfer size."""
    r2 = r if r2 is None else r2
    b = min(bevel, h * 0.45, r * 0.45)
    if b > 0:
        prof = [(0, h / 2), (r2 - b, h / 2), (r2, h / 2 - b), (r, -h / 2 + b), (r - b, -h / 2), (0, -h / 2)]
    else:
        prof = [(0, h / 2), (r2, h / 2), (r, -h / 2), (0, -h / 2)]
    return lathe(prof, segs, 1.0, sy)


def box(size, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = V((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:] + bm.verts[:], offset=min(bevel, min(size) * 0.45), segments=1,
                        affect="EDGES", profile=0.5, clamp_overlap=True)
    return bm


def torus(R, r, segs=12, csegs=6, sy=1.0, rz=None):
    rz = r if rz is None else rz
    prof = [(R + r * cos(TAU * k / csegs), rz * sin(TAU * k / csegs)) for k in range(csegs)]
    return lathe(prof, segs, 1.0, sy, loop=True)


def spike(L, r, segs=5, sx=1.0, sy=1.0, shape=None):
    """A rounded cone along +Z from 0 to L (hair locks, spikes, fringe)."""
    prof = shape or [(0, L), (r * 0.45, L * 0.62), (r * 0.9, L * 0.28), (r, L * 0.08), (r * 0.6, -r * 0.25), (0, -r * 0.35)]
    return lathe(prof, segs, sx, sy)


def tube(path, radius, segs=6, caps=True):
    """Sweep a circle along a list of Vectors; radius may be a float or a list."""
    bm = bmesh.new()
    n = len(path)
    rad = radius if isinstance(radius, (list, tuple)) else [radius] * n
    rings = []
    side = None
    for i, p in enumerate(path):
        t = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        if side is None:
            ref = V((0, 0, 1)) if abs(t.z) < 0.9 else V((1, 0, 0))
            side = t.cross(ref).normalized()
        else:
            side = (side - t * side.dot(t)).normalized()
        up = t.cross(side)
        rings.append([bm.verts.new(p + (side * cos(TAU * k / segs) + up * sin(TAU * k / segs)) * rad[i])
                      for k in range(segs)])
    for A, B in zip(rings[:-1], rings[1:]):
        for k in range(segs):
            bm.faces.new((A[k], A[(k + 1) % segs], B[(k + 1) % segs], B[k]))
    if caps:
        t0 = (path[0] - path[1]).normalized()
        t1 = (path[-1] - path[-2]).normalized()
        p0 = bm.verts.new(path[0] + t0 * rad[0] * 0.6)
        p1 = bm.verts.new(path[-1] + t1 * rad[-1] * 0.6)
        for k in range(segs):
            bm.faces.new((rings[0][(k + 1) % segs], rings[0][k], p0))
            bm.faces.new((rings[-1][k], rings[-1][(k + 1) % segs], p1))
    return bm


def slab(grid, offset, wrap=False):
    """Solid sheet from a grid (rows of Vectors). offset(p) gives the thickness vector at each point."""
    bm = bmesh.new()
    R, C = len(grid), len(grid[0])
    top = [[bm.verts.new(p) for p in row] for row in grid]
    bot = [[bm.verts.new(p + offset(p)) for p in row] for row in grid]
    cmax = C if wrap else C - 1
    for i in range(R - 1):
        for j in range(cmax):
            j1 = (j + 1) % C
            bm.faces.new((top[i][j], top[i][j1], top[i + 1][j1], top[i + 1][j]))
            bm.faces.new((bot[i + 1][j], bot[i + 1][j1], bot[i][j1], bot[i][j]))
    for i in (0, R - 1):
        for j in range(cmax):
            j1 = (j + 1) % C
            bm.faces.new((top[i][j], top[i][j1], bot[i][j1], bot[i][j]))
    if not wrap:
        for j in (0, C - 1):
            for i in range(R - 1):
                bm.faces.new((top[i][j], top[i + 1][j], bot[i + 1][j], bot[i][j]))
    return bm


def dome(r, cz, phimax, segs=24, rows=8, lip=0.015, zig=0.0, flip=False, fn=None):
    """Ellipsoid cap from the pole down to phimax(a) (radians from the pole), open underneath with an
    inward lip. flip=True puts the pole at the bottom (beards)."""
    rx, ry, rz = r
    sz = -1.0 if flip else 1.0
    bm = bmesh.new()
    pole = V((0, 0, cz + sz * rz))
    if fn:
        pole = fn(pole, 0.0, 0.0)
    top = bm.verts.new(pole)
    grid = []
    centre = V((0, 0, cz))
    for j in range(segs):
        a = TAU * j / segs
        pm = phimax(a) + (zig if j % 2 else 0.0)
        col = []
        for k in range(1, rows + 1):
            ph = pm * k / rows
            p = V((sin(ph) * sin(a) * rx, sin(ph) * cos(a) * ry, cz + sz * cos(ph) * rz))
            if fn:
                p = fn(p, a, ph)
            col.append(p)
        d = col[-1] - centre
        col.append(centre + d * (1.0 - lip / max(d.length, 1e-6)))
        grid.append([bm.verts.new(p) for p in col])
    n = len(grid[0])
    for j in range(segs):
        j1 = (j + 1) % segs
        A, B = grid[j], grid[j1]
        tri = (top, B[0], A[0])
        bm.faces.new(tri if not flip else tuple(reversed(tri)))
        for k in range(n - 1):
            q = (A[k], B[k], B[k + 1], A[k + 1])
            bm.faces.new(q if not flip else tuple(reversed(q)))
    return bm


def xf(pb, loc=(0, 0, 0), rot=(0, 0, 0), scl=None, M=None):
    if M is None:
        M = Matrix.Translation(V(loc)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        if scl:
            M = M @ Matrix.Diagonal(V((scl[0], scl[1], scl[2], 1.0)))
    bmesh.ops.transform(pb, matrix=M, verts=pb.verts[:])
    return pb


def aim(d, spin=0.0, up=None):
    """Rotation turning local +Z towards d, local +Y kept as close to `up` (default world up) as possible."""
    d = V(d).normalized()
    if up is None or abs(V(up).normalized().dot(d)) > 0.98:
        up = V((0, 0, 1)) if abs(d.z) < 0.95 else V((0, 1, 0))
    x = V(up).cross(d).normalized()
    y = d.cross(x)
    m = Matrix((x, y, d)).transposed().to_4x4()
    return m @ Matrix.Rotation(spin, 4, "Z")


def place(pb, at, d, spin=0.0, up=None):
    return xf(pb, M=Matrix.Translation(V(at)) @ aim(d, spin, up))


def loft(outline, rings, fn=None, caps=True):
    """Stack an (x, y) outline at rings [(scale, z, dy)]; fn(p) may reshape each vertex."""
    bm = bmesh.new()
    R = []
    for (sc, z, dy) in rings:
        ring = []
        for (x, y) in outline:
            q = V((x * sc, y * sc + dy, z))
            ring.append(bm.verts.new(fn(q) if fn else q))
        R.append(ring)
    n = len(outline)
    for A, B in zip(R[:-1], R[1:]):
        for i in range(n):
            bm.faces.new((A[i], A[(i + 1) % n], B[(i + 1) % n], B[i]))
    if caps:
        bm.faces.new(R[0])
        bm.faces.new(list(reversed(R[-1])))
    return bm


def superellipse(n, hw, hl, e=2.4, taper=0.0):
    """Closed (x, y) outline, angle 0 at +y; taper widens the +y end."""
    pts = []
    for i in range(n):
        t = TAU * i / n
        c, s = cos(t), sin(t)
        x = math.copysign(abs(s) ** (2 / e), s) * hw * (1 - taper / 2 + taper * (c + 1) / 2)
        y = math.copysign(abs(c) ** (2 / e), c) * hl
        pts.append((x, y))
    return pts


def angd(a, b):
    return atan2(sin(a - b), cos(a - b))


def mirror_x(pb):
    """Mirror a piece across X (normals get fixed by recalc when merged)."""
    return xf(pb, M=Matrix.Diagonal(V((-1, 1, 1, 1))))


# ----------------------------------------------------------------------------------------------------------
# Surfaces with pos(u, v, off) -> (point, outward normal), used to lay decals (ribbons, buttons, pouches).
class Prof:
    """(r, z) profile, top to bottom; r(z) by linear interpolation (matches the lathe mesh)."""

    def __init__(self, pts):
        self.pts = sorted(pts, key=lambda p: p[1])

    def r(self, z):
        p = self.pts
        if z <= p[0][1]:
            return p[0][0]
        for (r0, z0), (r1, z1) in zip(p[:-1], p[1:]):
            if z <= z1:
                return lerp(r0, r1, (z - z0) / max(z1 - z0, 1e-9))
        return p[-1][0]

    def dr(self, z):
        e = 0.01
        return (self.r(z + e) - self.r(z - e)) / (2 * e)


class TSurf:
    """Elliptic surface of revolution: u = angle (0 front, +pi/2 = +X), v = z."""

    def __init__(self, prof, cx=0.0, sy=1.0):
        self.p = Prof(prof)
        self.cx = cx
        self.sy = sy

    def pos(self, u, v, off=0.0):
        r = self.p.r(v)
        dr = self.p.dr(v)
        s, c = sin(u), cos(u)
        p = V((self.cx + s * r, c * r * self.sy, v))
        n = V((s * r * self.sy, c * r, -r * dr * self.sy))
        n = n.normalized() if n.length > 1e-9 else V((0, 0, 1))
        return p + n * off, n

    def ux(self, x, z, back=False):
        """Angle of the point of the front (or back) at lateral offset x and height z."""
        a = asin(clamp((x - self.cx) / max(self.p.r(z), 1e-4), -1, 1))
        return pi - a if back else a


class EllS:
    """Ellipsoid: u = azimuth (0 front), v = polar angle from the top pole."""

    def __init__(self, c, r):
        self.c = V(c)
        self.r = V(r)

    def pos(self, u, v, off=0.0):
        n0 = V((sin(v) * sin(u), sin(v) * cos(u), cos(v)))
        p = self.c + V((n0.x * self.r.x, n0.y * self.r.y, n0.z * self.r.z))
        n = V((n0.x / self.r.x, n0.y / self.r.y, n0.z / self.r.z)).normalized()
        return p + n * off, n


class RaySurf:
    """Projects (u, v) onto a mesh along an axis: 'front' casts towards -Y (u = x, v = z),
    'top' casts towards -Z (u = x, v = y)."""

    def __init__(self, bvh, mode="front"):
        self.bvh = bvh
        self.mode = mode

    def pos(self, u, v, off=0.0):
        if self.mode == "front":
            o, d = V((u, 3.0, v)), V((0, -1, 0))
        elif self.mode == "top":
            o, d = V((u, v, 3.0)), V((0, 0, -1))
        else:
            o, d = V((3.0 * self.mode, u, v)), V((-self.mode, 0, 0))
        for j in (V((0, 0, 0)), V((1.3e-4, 0.7e-4, 0.9e-4)), V((-0.8e-4, -1.1e-4, 1.2e-4))):
            hit, n, _, _ = self.bvh.ray_cast(o + j, d)
            if hit is not None:
                if n.dot(d) > 0:
                    n = -n
                return hit + n * off, n
        return o + d * 3.0, -d


def ribbon_x(rows, thick, embed=0.005, closed=False, bevel=0.35):
    """Solid strip from cross-sections rows[i] = [(point, normal), ...] ordered across the strip.
    The top follows each point's normal (conforms to the surface), edges are chamfered."""
    bm = bmesh.new()
    vrows = []
    for sec in rows:
        K = len(sec)
        w = (sec[-1][0] - sec[0][0]).length
        k_in = min(w * 0.5 * bevel, thick * 0.8)
        pts = [sec[0][0] - sec[0][1] * embed]
        for k, (q, n) in enumerate(sec):
            t = q + n * thick
            if k == 0:
                t = t + (sec[1][0] - sec[0][0]).normalized() * k_in
            elif k == K - 1:
                t = t + (sec[-2][0] - sec[-1][0]).normalized() * k_in
            pts.append(t)
        pts.append(sec[-1][0] - sec[-1][1] * embed)
        vrows.append([bm.verts.new(q) for q in pts])
    pairs = list(zip(vrows[:-1], vrows[1:]))
    if closed:
        pairs.append((vrows[-1], vrows[0]))
    for A, B in pairs:
        for k in range(len(A) - 1):
            bm.faces.new((A[k], A[k + 1], B[k + 1], B[k]))
    if not closed:
        bm.faces.new(list(reversed(vrows[0])))
        bm.faces.new(vrows[-1])
    return bm


def ribbon3d(st, widths, thick, embed=0.005, closed=False, bevel=0.35):
    """Strip along stations [(centre, normal)] with a straight (flat) cross-section."""
    n = len(st)
    rows = []
    for i, (c, nrm) in enumerate(st):
        if closed:
            t = st[(i + 1) % n][0] - st[(i - 1) % n][0]
        else:
            t = st[min(i + 1, n - 1)][0] - st[max(i - 1, 0)][0]
        side = t.cross(nrm)
        side = side.normalized() if side.length > 1e-9 else V((1, 0, 0))
        w = max(widths[i], 0.002) * 0.5
        rows.append([(c - side * w, nrm), (c + side * w, nrm)])
    return ribbon_x(rows, thick, embed, closed, bevel)


def cross_section(S, u, v, side, w, off, K):
    """K points across a strip of width w centred on (u, v), following surface S along the 3D direction side."""
    e = 1e-3
    pu = (S.pos(u + e, v)[0] - S.pos(u - e, v)[0]) / (2 * e)
    pv = (S.pos(u, v + e)[0] - S.pos(u, v - e)[0]) / (2 * e)
    a11, a12, a22 = pu.dot(pu), pu.dot(pv), pv.dot(pv)
    b1, b2 = pu.dot(side), pv.dot(side)
    det = a11 * a22 - a12 * a12
    if abs(det) < 1e-12:
        c, n = S.pos(u, v, off)
        return [(c + side * lerp(-w / 2, w / 2, k / (K - 1)), n) for k in range(K)]
    du, dv = (b1 * a22 - b2 * a12) / det, (a11 * b2 - a12 * b1) / det
    return [S.pos(u + du * t, v + dv * t, off) for t in [lerp(-w / 2, w / 2, k / (K - 1)) for k in range(K)]]


def rib(P, S, path, width, col, off=0.004, thick=0.01, closed=False, sub=4, embed=0.006, bevel=0.35, colfn=None, res=0.045):
    """Ribbon on surface S along a (u, v) path (subdivided), width float or f(t); wide ribbons wrap the surface."""
    pts = []
    m = len(path)
    segs = m if closed else m - 1
    for i in range(segs):
        (u0, v0), (u1, v1) = path[i], path[(i + 1) % m]
        for k in range(sub):
            t = k / sub
            pts.append((lerp(u0, u1, t), lerp(v0, v1, t)))
    if not closed:
        pts.append(path[-1])
    N = len(pts)
    cs = [S.pos(u, v, off) for (u, v) in pts]
    ws = [max(width(i / max(N - 1, 1)) if callable(width) else width, 0.002) for i in range(N)]
    K = max(2, int(math.ceil(max(ws) / res)) + 1)
    rows = []
    for i, (u, v) in enumerate(pts):
        if closed:
            t = cs[(i + 1) % N][0] - cs[(i - 1) % N][0]
        else:
            t = cs[min(i + 1, N - 1)][0] - cs[max(i - 1, 0)][0]
        side = t.cross(cs[i][1])
        side = side.normalized() if side.length > 1e-9 else V((1, 0, 0))
        rows.append(cross_section(S, u, v, side, ws[i], off, K))
    P.add(ribbon_x(rows, thick, embed, closed, bevel), col, colfn=colfn, recalc=False)


def stick(P, S, u, v, pb, col, off=0.0, spin=0.0, colfn=None, tilt=None):
    p, n = S.pos(u, v, off)
    d = n if tilt is None else (n + V(tilt)).normalized()
    P.add(place(pb, p, d, spin), col, colfn=colfn)


def button(P, S, u, v, col, r=0.016, h=0.012, segs=8, off=0.0):
    stick(P, S, u, v, cyl(r, h, segs, bevel=h * 0.4), col, off=h * 0.25 + off)


# ----------------------------------------------------------------------------------------------------------
# Torso base: a pear with two trouser legs. Origin = the game's Torso pivot (Unity y 0.78), mesh spans
# z -0.55 .. +0.40 so the top stays under the floating head (head bottom at pivot + 0.40).
SY = 0.6 / 0.72
TORSO_KEYS = [(0.0, 0.388), (0.09, 0.384), (0.16, 0.37), (0.212, 0.345), (0.248, 0.305), (0.268, 0.245),
              (0.282, 0.16), (0.302, 0.06), (0.333, -0.045), (0.355, -0.14), (0.352, -0.22), (0.33, -0.29),
              (0.27, -0.345), (0.16, -0.38), (0.0, -0.392)]
LEGX = 0.148
LSY = 0.92
LEG_KEYS = [(0.0, -0.25), (0.125, -0.25), (0.132, -0.34), (0.131, -0.44), (0.125, -0.515), (0.113, -0.542),
            (0.0, -0.55)]


def catmull(keys, sub=2):
    """Smooth a (r, z) key profile with Catmull-Rom, keeping the end poles."""
    out = []
    n = len(keys)
    for i in range(n - 1):
        p0, p1, p2, p3 = (V(keys[max(i - 1, 0)]), V(keys[i]), V(keys[i + 1]), V(keys[min(i + 2, n - 1)]))
        for k in range(sub):
            t = k / sub
            t2, t3 = t * t, t * t * t
            q = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            out.append((max(q.x, 0.0) if 0 < i or k > 0 else q.x, q.y))
    out.append(keys[-1])
    out[0] = keys[0]
    return out


TORSO = catmull(TORSO_KEYS, 1)
LEG = LEG_KEYS


def torso_base(P, colfn=None, legfn=None, keys=None, legs=True, segs=24, body_col=T(0.95), leg_col=T(0.85), sub=1):
    prof = catmull(keys or TORSO_KEYS, sub)
    S = TSurf(prof, 0.0, SY)
    P.add(lathe(prof, segs, 1.0, SY), body_col, colfn=colfn)
    LS = []
    if legs:
        for sx in (-1, 1):
            P.add(xf(lathe(LEG, 12, 1.0, LSY), loc=(sx * LEGX, 0, 0)), leg_col, colfn=legfn)
            LS.append(TSurf(LEG, sx * LEGX, LSY))
    return S, LS


def collar_ring(P, col, r0=0.118, r1=0.155, z0=0.365, z1=0.4, segs=16, gap=None):
    prof = [(r0, z1), (r1 - 0.01, z1), (r1, z1 - 0.012), (r1 + 0.004, z0), (r0, z0 - 0.005)]
    if gap:
        P.add(lathe(prof[:], segs, 1.0, SY, a0=gap, a1=TAU - gap, caps=False), col, recalc=False)
        # close the ends of the open collar
        return
    P.add(lathe(prof, segs, 1.0, SY, loop=True), col)


def leg_cuffs(P, LS, col, z=-0.53, w=0.03, thick=0.012):
    for L in LS:
        rib(P, L, [(TAU * k / 12, z) for k in range(12)], w, col, off=0.0, thick=thick, closed=True, sub=1)


def leg_line(P, LS, col, u_out=True, w=0.006, z0=-0.3, z1=-0.535, front=False):
    for L in LS:
        sgn = 1 if L.cx > 0 else -1
        u = 0.0 if front else sgn * pi / 2
        rib(P, L, [(u, z0), (u, z1)], w, col, off=0.0, thick=0.004, sub=6)


def belt(P, S, z, col, w=0.05, thick=0.014, buckle=None, bw=0.07):
    rib(P, S, ring_path(z), w, col, off=0.0, thick=thick, closed=True, sub=1)
    if buckle:
        stick(P, S, 0.0, z, box((bw, w * 1.15, 0.016), 0.004), buckle, off=thick)


def camo(c, n):
    v = sin(c.x * 15 + 1.7) + sin(c.z * 13 - c.x * 7 + 0.6) + 0.8 * sin(c.y * 14 + c.z * 9 + 2.1)
    w = sin(c.x * 23 - c.z * 17 + 0.3) + sin(c.y * 21 + c.z * 19 + 1.1)
    if v > 0.7:
        return T(0.5)
    if w > 1.1:
        return T(0.68)
    if v < -1.0:
        return T(0.8)
    return T(0.95)


# ----------------------------------------------------------------------------------------------------------
# TORSOS. Front = +Y. Waist/belt line around z -0.10.
def lapels(P, S, col, top=0.352, notch=0.27, bottom=0.02, x_top=0.135, x_notch=0.105, w_top=0.075, thick=0.014):
    for sx in (-1, 1):
        zm = (top + notch) / 2
        rib(P, S, [(S.ux(sx * x_top, top), top), (S.ux(sx * (x_top + 0.012), zm), zm), (S.ux(sx * x_notch, notch), notch)],
            lambda t: lerp(w_top, 0.05, t), col, off=0.006, thick=thick, sub=3)
        zm = (notch + bottom) / 2
        rib(P, S, [(S.ux(sx * (x_notch - 0.004), notch - 0.012), notch - 0.012), (S.ux(sx * 0.06, zm), zm),
                   (S.ux(sx * 0.012, bottom), bottom)], lambda t: lerp(0.07, 0.018, t), col, off=0.006, thick=thick, sub=4)


def shirt_v(P, S, col, top=0.37, bottom=0.02, w=0.21):
    rib(P, S, [(0.0, top), (0.0, bottom)], lambda t: lerp(w, 0.01, t), col, off=0.0, thick=0.006, sub=8)


def shirt_collar(P, S, col, z=0.372, spread=0.045, thick=0.012):
    for sx in (-1, 1):
        rib(P, S, [(S.ux(sx * spread, z), z), (S.ux(sx * (spread + 0.05), z - 0.065), z - 0.065)],
            lambda t: lerp(0.055, 0.008, t), col, off=0.006, thick=thick, sub=3)


def pocket(P, S, x, z, w, h, col, flap=None, stitch=None, back=False, off=0.0):
    u = S.ux(x, z, back)
    rib(P, S, [(u, z + h / 2), (u, z - h / 2)], w, col, off=off, thick=0.008, sub=3)
    if flap:
        rib(P, S, [(u, z + h / 2 + 0.008), (u, z + h / 2 - 0.022)], w + 0.01, flap, off=off + 0.006, thick=0.008, sub=2)
    if stitch:
        zs = z + h / 2 - 0.012
        rib(P, S, [(S.ux(x - w / 2 + 0.008, zs, back), zs), (S.ux(x + w / 2 - 0.008, zs, back), zs)],
            0.004, stitch, off=off + 0.007, thick=0.003, sub=3)


def star_piece(r, h, points=5, inner=0.45):
    return lathe([(0, h / 2), (r, h / 2), (r, -h / 2), (0, -h / 2)], points * 2,
                 fn=lambda p, a, z: V((p.x, p.y, 0)) * (1.0 if round(a / (TAU / (points * 2))) % 2 == 0 else inner) + V((0, 0, z)))


def ring_path(z, n=20):
    return [(TAU * k / n, z) for k in range(n)]


def torso_suit():
    P = Part("Torso_Suit")
    hem = -0.215
    S, LS = torso_base(P, colfn=lambda c, n: T(0.95) if c.z > hem else T(0.8), leg_col=T(0.8))
    rib(P, S, ring_path(hem), 0.022, T(0.92), off=0.0, thick=0.012, closed=True, sub=1)
    shirt_v(P, S, WHITE)
    shirt_collar(P, S, WHITE)
    TIE = F(0.78, 0.12, 0.16)
    stick(P, S, 0.0, 0.345, box((0.05, 0.042, 0.026), 0.008), TIE, off=0.008)
    rib(P, S, [(0.0, 0.33), (0.0, 0.05)], lambda t: (0.032 + 0.045 * t) if t < 0.86 else lerp(0.07, 0.006, (t - 0.86) / 0.14),
        TIE, off=0.006, thick=0.01, sub=10)
    rib(P, S, [(0.0, 0.22), (0.0, 0.212)], 0.075, GOLD, off=0.016, thick=0.004, sub=1)
    lapels(P, S, T(0.86))
    for z in (-0.04, -0.13):
        button(P, S, 0.0, z, INK, r=0.015)
    xp = -0.17
    rib(P, S, [(S.ux(xp - 0.05, 0.17), 0.17), (S.ux(xp + 0.05, 0.17), 0.17)], 0.016, T(0.82), off=0.002, thick=0.008, sub=3)
    for i, dx in enumerate((-0.03, 0.0, 0.028)):
        stick(P, S, S.ux(xp + dx, 0.18), 0.18, spike(0.035 - 0.008 * abs(i - 1), 0.014, 4, sy=0.5), F(0.35, 0.6, 0.95),
              off=0.0, tilt=(0, 0, 2.5))
    for sx in (-1, 1):
        rib(P, S, [(S.ux(sx * 0.15, -0.12), -0.12), (S.ux(sx * 0.27, -0.12), -0.12)], 0.028, T(0.86), off=0.0, thick=0.01, sub=3)
    rib(P, S, [(pi, hem), (pi, -0.1)], 0.008, T(0.7), off=0.0, thick=0.006, sub=3)
    rib(P, S, [(pi, 0.36), (pi, -0.1)], 0.004, T(0.82), off=0.0, thick=0.003, sub=8)
    leg_line(P, LS, T(0.66), front=True, z0=-0.36, z1=-0.52)
    leg_cuffs(P, LS, T(0.74), z=-0.525, w=0.026, thick=0.008)
    return P


def vest_plates(P, S, col, side_col, top=0.29, bottom=-0.05, w=0.38, flare=0.06, thick=0.032, strap=0.075):
    for u in (0.0, pi):
        rib(P, S, [(u, top), (u, bottom)], lambda t: w + flare * t, col, off=0.004, thick=thick, sub=5, bevel=0.2, res=0.075)
    for sx in (-1, 1):
        a = S.ux(sx * (w / 2 - 0.04), top)
        rib(P, S, [(a, top - 0.02), (sx * 0.9, 0.352), (sx * pi / 2, 0.358), (sx * (pi - 0.9), 0.352), (sx * pi - a, top - 0.02)],
            strap, col, off=0.004, thick=thick - 0.004, sub=3)
        rib(P, S, [(sx * 1.25, top - 0.16), (sx * 1.25, bottom + 0.01)], 0.17, side_col, off=0.004, thick=0.02, sub=3)


def torso_guard():
    P = Part("Torso_Guard")
    TROU = F(0.16, 0.17, 0.22)
    S, LS = torso_base(P, colfn=lambda c, n: T(0.95) if c.z > -0.1 else TROU, leg_col=TROU)
    collar_ring(P, T(0.9))
    shirt_collar(P, S, T(0.9), spread=0.05)
    VEST = F(0.17, 0.19, 0.23)
    VEST2 = F(0.12, 0.13, 0.16)
    vest_plates(P, S, VEST, VEST2)
    for z in (0.02, -0.015):
        rib(P, S, [(S.ux(-0.17, z), z), (S.ux(0.17, z), z)], 0.012, VEST2, off=0.036, thick=0.006, sub=6)
    for x in (-0.09, 0.09):
        stick(P, S, S.ux(x, 0.06), 0.06, box((0.075, 0.08, 0.04), 0.01), VEST2, off=0.042)
        stick(P, S, S.ux(x, 0.095), 0.095, box((0.082, 0.026, 0.046), 0.008), VEST, off=0.04)
    stick(P, S, S.ux(-0.12, 0.215), 0.215, cyl(0.035, 0.012, 6, bevel=0.004), GOLD, off=0.036, spin=pi / 6)
    stick(P, S, S.ux(-0.12, 0.215), 0.215, star_piece(0.02, 0.008), F(0.85, 0.5, 0.12), off=0.044)
    stick(P, S, S.ux(0.11, 0.215), 0.215, box((0.12, 0.035, 0.008), 0.003), F(0.9, 0.9, 0.86), off=0.036)
    stick(P, S, 0.62, 0.32, box((0.04, 0.06, 0.03), 0.008), INK, off=0.03, spin=0.3)
    BELT = F(0.09, 0.09, 0.1)
    belt(P, S, -0.12, BELT, w=0.055, buckle=SILVER)
    u = -1.05
    stick(P, S, u, -0.12, box((0.07, 0.12, 0.045), 0.01), F(0.14, 0.14, 0.15), off=0.012)
    p, n = S.pos(u, -0.12, 0.04)
    P.add(tube([p + V((0.015, 0, 0.05)), p + V((0.018, 0, 0.16))], 0.007, 5), INK)
    stick(P, S, u + 0.04, -0.1, cyl(0.008, 0.01, 6), F(0.3, 0.95, 0.4), off=0.058)
    u = 1.15
    stick(P, S, u, -0.17, box((0.075, 0.16, 0.05), 0.012), F(0.06, 0.06, 0.07), off=0.012)
    stick(P, S, u, -0.085, box((0.04, 0.06, 0.055), 0.01), F(0.22, 0.22, 0.24), off=0.02, spin=0.35)
    rib(P, S, [(u, -0.06), (u, -0.13)], 0.08, BELT, off=0.06, thick=0.006, sub=2)
    stick(P, S, pi - 0.5, -0.12, box((0.07, 0.07, 0.035), 0.01), BELT, off=0.012)
    stick(P, S, pi + 0.5, -0.12, cyl(0.022, 0.12, 8, bevel=0.005), F(0.22, 0.22, 0.25), off=0.03)
    leg_line(P, LS, F(0.35, 0.38, 0.5), w=0.014)
    leg_cuffs(P, LS, F(0.12, 0.13, 0.17), w=0.024, thick=0.008)
    return P


def torso_military():
    P = Part("Torso_Military")
    S, LS = torso_base(P, colfn=camo, legfn=camo, segs=18, sub=2)
    collar_ring(P, T(0.8), r1=0.165)
    VEST = F(0.27, 0.29, 0.2)
    VEST2 = F(0.33, 0.35, 0.25)
    WEB = F(0.2, 0.21, 0.15)
    vest_plates(P, S, VEST, WEB, top=0.3, bottom=-0.07, w=0.42, flare=0.08, thick=0.03, strap=0.085)
    for z in (0.19, 0.14):
        rib(P, S, [(S.ux(-0.19, z), z), (S.ux(0.19, z), z)], 0.012, WEB, off=0.034, thick=0.005, sub=4)
    for x in (-0.11, 0.0, 0.11):
        stick(P, S, S.ux(x, 0.0), 0.0, box((0.085, 0.12, 0.048), 0.012), VEST2, off=0.04)
        stick(P, S, S.ux(x, 0.055), 0.055, box((0.092, 0.035, 0.054), 0.01), VEST, off=0.04)
        stick(P, S, S.ux(x, 0.045), 0.045, cyl(0.009, 0.008, 6), INK, off=0.072)
    stick(P, S, S.ux(0.12, 0.25), 0.25, box((0.09, 0.055, 0.008), 0.003), WEB, off=0.034)
    stick(P, S, S.ux(0.12, 0.25), 0.25, box((0.07, 0.04, 0.008), 0.003), T(0.6), off=0.04)
    stick(P, S, S.ux(-0.13, 0.24), 0.24, box((0.06, 0.1, 0.045), 0.012), VEST2, off=0.034)
    p, n = S.pos(S.ux(-0.15, 0.27), 0.27, 0.06)
    P.add(tube([p, p + V((0.0, 0.0, 0.14))], 0.006, 5), INK)
    belt(P, S, -0.13, WEB, w=0.06, buckle=INK)
    stick(P, S, -1.2, -0.17, cyl(0.055, 0.1, 10, bevel=0.012), VEST2, off=0.06)
    stick(P, S, 1.2, -0.16, box((0.09, 0.09, 0.05), 0.014), VEST2, off=0.012)
    stick(P, S, pi, 0.12, box((0.24, 0.3, 0.07), 0.035), VEST2, off=0.03)
    stick(P, S, pi, 0.2, box((0.18, 0.02, 0.012), 0.004), WEB, off=0.1)
    for L in LS:
        sgn = 1 if L.cx > 0 else -1
        stick(P, L, sgn * pi / 2, -0.4, box((0.08, 0.09, 0.03), 0.01), T(0.7), off=0.0)
        stick(P, L, sgn * pi / 2, -0.36, box((0.086, 0.025, 0.036), 0.008), T(0.6), off=0.0)
        stick(P, L, sgn * 0.1, -0.47, box((0.1, 0.085, 0.035), 0.016), F(0.15, 0.16, 0.13), off=0.0)
    leg_cuffs(P, LS, T(0.6), z=-0.525, w=0.03, thick=0.008)
    return P


def torso_casual():
    P = Part("Torso_Casual")
    JEANS = F(0.24, 0.37, 0.62)
    STITCH = F(0.9, 0.62, 0.25)
    hem = -0.16
    S, LS = torso_base(P, colfn=lambda c, n: T(0.95) if c.z > hem else JEANS, leg_col=JEANS)
    rib(P, S, ring_path(hem + 0.01), 0.05, T(0.84), off=0.0, thick=0.016, closed=True, sub=1)
    collar_ring(P, T(0.88), r1=0.162)
    stick(P, S, pi, 0.27, ell(0.175, 0.105, 0.05, 12, 6), T(0.88), off=0.004)
    rib(P, S, [(pi - 0.45, 0.3), (pi, 0.24), (pi + 0.45, 0.3)], 0.012, T(0.66), off=0.045, thick=0.004, sub=3)
    for sx in (-1, 1):
        pts = [S.pos(S.ux(sx * 0.06, z), z, 0.012)[0] for z in (0.35, 0.3, 0.25, 0.2)]
        P.add(tube(pts, 0.007, 5), WHITE)
        P.add(tube([pts[-1], pts[-1] + V((0, 0.003, -0.03))], 0.009, 5), F(0.6, 0.6, 0.62))
    rib(P, S, [(0.0, 0.02), (0.0, -0.125)], lambda t: lerp(0.24, 0.36, t), T(0.9), off=0.004, thick=0.012, sub=4)
    for sx in (-1, 1):
        rib(P, S, [(S.ux(sx * 0.12, 0.015), 0.015), (S.ux(sx * 0.17, -0.12), -0.12)], 0.01, T(0.7), off=0.016, thick=0.003, sub=3)
    stick(P, S, S.ux(-0.13, 0.2), 0.2, cyl(0.045, 0.006, 12), WHITE, off=0.0)
    stick(P, S, S.ux(-0.13, 0.2), 0.2, star_piece(0.03, 0.008), F(0.95, 0.45, 0.25), off=0.006)
    stick(P, S, 0.0, hem - 0.03, cyl(0.013, 0.01, 8, bevel=0.003), F(0.8, 0.68, 0.35), off=0.004)
    rib(P, S, [(0.0, hem - 0.05), (0.0, -0.33)], 0.005, STITCH, off=0.0, thick=0.003, sub=4)
    for sx in (-1, 1):
        rib(P, S, [(S.ux(sx * 0.13, hem - 0.03), hem - 0.03), (S.ux(sx * 0.2, -0.23), -0.23), (sx * 1.35, -0.26)],
            0.005, STITCH, off=0.0, thick=0.003, sub=4)
        u = sx * (pi - 0.45)
        rib(P, S, [(u, -0.22), (u, -0.32)], lambda t: lerp(0.13, 0.08, t), F(0.21, 0.33, 0.57), off=0.0, thick=0.008, sub=3)
        rib(P, S, [(u, -0.225), (u, -0.232)], 0.12, STITCH, off=0.008, thick=0.002, sub=1)
        for uu in (sx * 0.6, sx * 1.6, sx * 2.6):
            rib(P, S, [(uu, hem - 0.005), (uu, hem - 0.05)], 0.014, F(0.2, 0.32, 0.55), off=0.014, thick=0.006, sub=1)
    leg_line(P, LS, STITCH, w=0.005, z0=-0.3)
    leg_cuffs(P, LS, F(0.33, 0.48, 0.74), z=-0.52, w=0.04, thick=0.014)
    return P


def plaid(c, n):
    i = int(math.floor((c.x + 5) / 0.055))
    j = int(math.floor((c.z + 5) / 0.055))
    k = int(math.floor((c.y + 5) / 0.055))
    a, b = (i + k) % 3 == 0, j % 3 == 0
    if a and b:
        return F(0.24, 0.07, 0.07)
    if a or b:
        return F(0.5, 0.1, 0.1)
    return F(0.78, 0.2, 0.17)


def torso_worker():
    P = Part("Torso_Worker")
    S, LS = torso_base(P, colfn=lambda c, n: plaid(c, n) if c.z > -0.02 else T(0.95), leg_col=T(0.95), segs=24)
    collar_ring(P, F(0.62, 0.14, 0.12))
    shirt_collar(P, S, F(0.62, 0.14, 0.12), spread=0.04)
    OV = T(0.95)
    THREAD = F(0.95, 0.75, 0.3)
    BRASS = F(0.85, 0.68, 0.3)
    rib(P, S, [(0.0, 0.245), (0.0, -0.03)], lambda t: lerp(0.27, 0.32, t), OV, off=0.003, thick=0.014, sub=6)
    rib(P, S, [(S.ux(-0.12, 0.233), 0.233), (S.ux(0.12, 0.233), 0.233)], 0.004, THREAD, off=0.017, thick=0.003, sub=4)
    rib(P, S, [(0.0, 0.17), (0.0, 0.07)], 0.13, T(0.85), off=0.017, thick=0.008, sub=3)
    rib(P, S, [(S.ux(-0.055, 0.165), 0.165), (S.ux(0.055, 0.165), 0.165)], 0.004, THREAD, off=0.025, thick=0.003, sub=3)
    p, n = S.pos(S.ux(0.03, 0.19), 0.19, 0.03)
    P.add(tube([p + V((0, 0, -0.05)), p + V((0.0, 0.0, 0.03))], 0.008, 6), F(0.95, 0.8, 0.15))
    P.add(tube([p + V((-0.025, 0.002, -0.04)), p + V((-0.025, 0.002, 0.02))], 0.006, 6), F(0.3, 0.3, 0.32))
    for sx in (-1, 1):
        a = S.ux(sx * 0.115, 0.245)
        rib(P, S, [(a, 0.245), (sx * 0.7, 0.33), (sx * pi / 2, 0.36), (sx * (pi - 0.75), 0.33),
                   (sx * (pi - 0.35), 0.15), (sx * (pi + 0.3), -0.03)], 0.05, OV, off=0.003, thick=0.012, sub=4)
        stick(P, S, a, 0.235, box((0.06, 0.05, 0.012), 0.005), BRASS, off=0.016)
        button(P, S, a, 0.215, BRASS, r=0.012)
        button(P, S, S.ux(sx * 0.2, -0.04), -0.04, BRASS, r=0.013)
    LEATHER = F(0.46, 0.28, 0.15)
    belt(P, S, -0.1, LEATHER, w=0.05, buckle=BRASS)
    for sx in (-1, 1):
        stick(P, S, sx * 0.85, -0.16, box((0.11, 0.12, 0.05), 0.014), F(0.52, 0.33, 0.18), off=0.012)
        stick(P, S, sx * 0.85, -0.1, box((0.115, 0.03, 0.056), 0.008), LEATHER, off=0.012)
    p, n = S.pos(-1.35, -0.1, 0.06)
    P.add(tube([p + V((0, 0, 0.06)), p + V((0, 0, -0.16))], 0.012, 6), F(0.7, 0.5, 0.28))
    P.add(xf(box((0.03, 0.1, 0.035), 0.008), loc=p + V((0, 0.0, 0.07))), F(0.4, 0.42, 0.46))
    stick(P, S, 1.35, -0.15, cyl(0.04, 0.03, 10, bevel=0.008), F(0.95, 0.8, 0.15), off=0.03)
    leg_cuffs(P, LS, T(0.82), z=-0.52, w=0.045, thick=0.014)
    leg_line(P, LS, THREAD, w=0.004)
    return P


FANCY_KEYS = [(0.0, 0.385), (0.10, 0.382), (0.175, 0.368), (0.235, 0.342), (0.27, 0.30), (0.288, 0.235),
              (0.296, 0.15), (0.305, 0.05), (0.322, -0.05), (0.34, -0.14), (0.355, -0.24), (0.37, -0.34),
              (0.385, -0.44), (0.39, -0.52), (0.37, -0.548), (0.0, -0.55)]


def torso_fancy():
    P = Part("Torso_Fancy")
    segs = 24

    def coat(c, n):
        if c.z < -0.2:
            a = atan2(c.x, c.y / SY)
            return T(0.95) if int(math.floor((a + pi) / (TAU / segs))) % 2 == 0 else T(0.84)
        return T(0.95)

    S, LS = torso_base(P, colfn=coat, keys=FANCY_KEYS, legs=False, segs=segs)
    collar_ring(P, T(0.9), r1=0.16)
    shirt_v(P, S, WHITE, top=0.37, bottom=0.1, w=0.18)
    for i, z in enumerate((0.33, 0.28, 0.23, 0.18)):
        stick(P, S, 0.0, z, ell(0.045 - 0.004 * i, 0.028, 0.012, 8, 4), WHITE, off=0.012)
    stick(P, S, 0.0, 0.355, cyl(0.022, 0.012, 8, bevel=0.004), F(0.25, 0.5, 0.95), off=0.02)
    lapels(P, S, T(0.84), bottom=0.08, x_top=0.13)
    rib(P, S, [(0.0, 0.08), (0.0, -0.52)], 0.006, T(0.7), off=0.0, thick=0.004, sub=8)
    for z in (0.05, -0.03, -0.11):
        for sx in (-1, 1):
            button(P, S, S.ux(sx * 0.075, z), z, GOLD, r=0.016)
    rib(P, S, ring_path(-0.525, 28), 0.035, GOLD, off=0.0, thick=0.01, closed=True, sub=1)
    rib(P, S, ring_path(-0.18, 28), 0.03, T(0.72), off=0.0, thick=0.01, closed=True, sub=1)
    path = [(-0.9, 0.345), (-0.35, 0.22), (0.35, 0.02), (1.05, -0.14), (pi / 2 + 0.3, -0.17), (pi, -0.08),
            (pi + 0.9, 0.12), (pi + 1.6, 0.3), (TAU - 1.05, 0.365)]
    rib(P, S, path, 0.085, GOLD, off=0.006, thick=0.012, closed=True, sub=3)
    rib(P, S, path, 0.065, F(0.82, 0.12, 0.2), off=0.01, thick=0.012, closed=True, sub=3)
    stick(P, S, -0.12, 0.165, star_piece(0.045, 0.012), GOLD, off=0.026)
    stick(P, S, -0.12, 0.165, ell(0.016, 0.016, 0.01, 6, 3), F(0.2, 0.45, 0.95), off=0.034)
    for sx in (-1, 1):
        p, n = S.pos(sx * 1.25, 0.33, 0.012)
        P.add(place(ell(0.07, 0.06, 0.02, 10, 4), p, n), GOLD)
        for k in range(5):
            q, m = S.pos(sx * (0.95 + 0.15 * k), 0.305, 0.03)
            P.add(tube([q, q + V((0, 0, -0.05))], 0.007, 4), F(1.0, 0.7, 0.2))
    return P


def torso_biker():
    P = Part("Torso_Biker")
    LEATHER = F(0.13, 0.11, 0.11)
    TRIM = F(0.06, 0.055, 0.055)
    DENIM = F(0.18, 0.22, 0.32)
    segs = 20
    open_a = TAU / segs

    def body(c, n):
        a = atan2(c.x, c.y / SY)
        if c.z < -0.1:
            return DENIM
        if c.z > -0.065 and not (abs(a) < open_a and c.z < 0.37):
            return LEATHER
        return T(0.95)

    S, LS = torso_base(P, colfn=body, leg_col=DENIM, segs=segs)
    for sx in (-1, 1):
        rib(P, S, [(sx * open_a, 0.37), (sx * open_a, -0.07)], 0.03, TRIM, off=0.0, thick=0.014, sub=6)
        for k in range(4):
            stick(P, S, sx * (0.75 + 0.18 * k), 0.335, spike(0.018, 0.012, 5), SILVER, off=0.004)
    rib(P, S, ring_path(-0.065, 20), 0.028, TRIM, off=0.0, thick=0.012, closed=True, sub=1)
    collar_ring(P, LEATHER, r1=0.16)
    stick(P, S, S.ux(-0.16, 0.18), 0.18, box((0.07, 0.04, 0.008), 0.003), F(0.82, 0.2, 0.18), off=0.0)
    stick(P, S, S.ux(0.17, 0.2), 0.2, cyl(0.03, 0.008, 4), F(0.9, 0.75, 0.25), off=0.0)
    rib(P, S, [(pi - 0.55, 0.26), (pi - 0.25, 0.3), (pi, 0.31), (pi + 0.25, 0.3), (pi + 0.55, 0.26)], 0.045,
        F(0.9, 0.75, 0.25), off=0.0, thick=0.008, sub=3)
    stick(P, S, pi, 0.12, cyl(0.09, 0.008, 14), F(0.9, 0.75, 0.25), off=0.0)
    stick(P, S, pi, 0.12, cyl(0.065, 0.01, 14), F(0.15, 0.13, 0.13), off=0.004)
    stick(P, S, pi, 0.12, cyl(0.025, 0.012, 8), SILVER, off=0.008)
    for sx in (-1, 1):
        stick(P, S, pi + sx * 0.5, 0.14, ell(0.07, 0.03, 0.01, 8, 3), F(0.92, 0.92, 0.9), off=0.004, spin=sx * 0.35)
    belt(P, S, -0.12, F(0.08, 0.07, 0.07), w=0.05, buckle=SILVER, bw=0.09)
    links = []
    for k in range(11):
        t = k / 10
        u = lerp(0.45, 1.7, t)
        z = -0.13 - 0.12 * sin(pi * t)
        links.append(S.pos(u, z, 0.02))
    for k, (p, n) in enumerate(links):
        d = (links[min(k + 1, 10)][0] - links[max(k - 1, 0)][0]).normalized()
        P.add(xf(torus(0.014, 0.004, 6, 4, sy=0.6), M=Matrix.Translation(p) @ aim(n if k % 2 else d.cross(n))), SILVER)
    leg_line(P, LS, F(0.4, 0.45, 0.6), w=0.004)
    for L in LS:
        stick(P, L, 0.1 if L.cx > 0 else -0.1, -0.46, box((0.06, 0.03, 0.004), 0.002), F(0.75, 0.75, 0.8), off=0.0)
    leg_cuffs(P, LS, F(0.22, 0.27, 0.4), w=0.03, thick=0.01)
    return P


def checks(c, n):
    i = int(math.floor((c.x + 5) / 0.045))
    j = int(math.floor((c.z + 5) / 0.045))
    k = int(math.floor((c.y + 5) / 0.045))
    return F(0.12, 0.12, 0.13) if (i + j + k) % 2 else F(0.9, 0.9, 0.9)


def torso_chef():
    P = Part("Torso_Chef")
    hem = -0.2
    S, LS = torso_base(P, colfn=lambda c, n: T(0.97) if c.z > hem else checks(c, n), legfn=checks)
    rib(P, S, ring_path(hem), 0.02, T(0.93), off=0.0, thick=0.012, closed=True, sub=1)
    collar_ring(P, T(0.93), r0=0.12, r1=0.158, z0=0.355, z1=0.405)
    RED = F(0.86, 0.2, 0.2)
    stick(P, S, 0.0, 0.345, ell(0.028, 0.022, 0.02, 8, 4), RED, off=0.012)
    for sx in (-1, 1):
        stick(P, S, S.ux(sx * 0.03, 0.31), 0.31, spike(0.06, 0.022, 4, sy=0.5), RED, off=0.004, tilt=(0, 0, -2.2), spin=sx * 0.3)
    rib(P, S, [(S.ux(0.02, 0.34), 0.34), (S.ux(0.02, hem + 0.01), hem + 0.01)], 0.27, T(0.97), off=0.002, thick=0.007, sub=8)
    for z in (0.25, 0.15, 0.05, -0.05):
        for x in (-0.1, 0.12):
            button(P, S, S.ux(x, z), z, F(0.2, 0.2, 0.22), r=0.014, off=0.008)
    R = RaySurf(P.bvh(), "front")
    grid = []
    yprev = None
    for i in range(8):
        z = lerp(-0.06, -0.5, i / 7)
        xs = [lerp(-0.25, 0.25, j / 6) * (1 + 0.1 * i / 7) for j in range(7)]
        hits = [R.pos(x, z, 0.012)[0] for x in xs]
        ymax = max(p.y for p in hits)
        if yprev is not None:
            ymax = max(ymax, yprev - 0.012)
        yprev = ymax
        grid.append([V((x, max(h.y, ymax - 1.5 * x * x), z)) for x, h in zip(xs, hits)])
    APRON = F(0.2, 0.25, 0.34)
    P.add(slab(grid, lambda p: V((0, -0.008, 0))), APRON)
    rib(P, S, [(S.ux(0.11, -0.13), -0.13), (S.ux(0.11, -0.2), -0.2)], 0.1, F(0.26, 0.31, 0.4), off=0.022, thick=0.006, sub=2)
    p, n = S.pos(S.ux(0.13, -0.13), -0.13, 0.036)
    P.add(tube([p + V((0, 0, -0.04)), p + V((0.0, 0, 0.07))], 0.007, 5), F(0.78, 0.56, 0.3))
    P.add(xf(ell(0.022, 0.008, 0.03, 8, 4), loc=p + V((0, 0, 0.09))), F(0.78, 0.56, 0.3))
    rib(P, S, ring_path(-0.05), 0.022, APRON, off=0.0, thick=0.008, closed=True, sub=1)
    for sx in (-1, 1):
        stick(P, S, pi, -0.05, ell(0.05, 0.03, 0.018, 8, 4), APRON, off=0.01, spin=sx * 0.5)
    stick(P, S, pi, -0.05, ell(0.02, 0.02, 0.02, 6, 4), APRON, off=0.015)
    return P


def torso_doctor():
    P = Part("Torso_Doctor")
    COAT = F(0.96, 0.97, 0.98)
    COAT2 = F(0.86, 0.88, 0.9)
    segs = 20
    open_a = TAU / segs

    def body(c, n):
        a = atan2(c.x, c.y / SY)
        if abs(a) < open_a and c.z < 0.37:
            return T(0.95)
        return COAT if c.z > -0.36 else T(0.95)

    S, LS = torso_base(P, colfn=body, leg_col=T(0.9), segs=segs)
    prof = [(S.p.r(-0.26) + 0.008, -0.26), (0.36, -0.34), (0.37, -0.42), (0.375, -0.475)]
    grid = []
    for (r, z) in prof:
        grid.append([V((sin(a) * r, cos(a) * r * SY, z)) for a in
                     [lerp(open_a + 0.05, TAU - open_a - 0.05, j / 16) for j in range(17)]])
    P.add(slab(grid, lambda p: -V((p.x, p.y / SY, 0)).normalized() * 0.014), COAT)
    for sx in (-1, 1):
        rib(P, S, [(sx * open_a, 0.35), (sx * open_a, -0.27)], 0.03, COAT, off=0.0, thick=0.012, sub=8)
    lapels(P, S, COAT2, bottom=0.05, x_top=0.12, x_notch=0.1)
    rib(P, S, [(0.0, 0.37), (0.0, 0.2)], lambda t: lerp(0.15, 0.01, t), T(0.75), off=0.0, thick=0.005, sub=4)
    pocket(P, S, -0.16, 0.16, 0.1, 0.09, COAT2)
    for dx, cc in ((-0.025, F(0.2, 0.4, 0.95)), (0.0, F(0.9, 0.2, 0.2))):
        p, n = S.pos(S.ux(-0.16 + dx, 0.2), 0.2, 0.015)
        P.add(tube([p + V((0, 0, -0.03)), p + V((0, 0, 0.035))], 0.007, 6), cc)
    stick(P, S, S.ux(0.15, 0.16), 0.16, box((0.06, 0.08, 0.006), 0.002), F(0.3, 0.6, 0.95), off=0.0)
    stick(P, S, S.ux(0.15, 0.165), 0.165, box((0.03, 0.035, 0.006), 0.001), WHITE, off=0.004)
    for sx in (-1, 1):
        pocket(P, S, sx * 0.21, -0.17, 0.11, 0.09, COAT2)
    for z in (0.0, -0.1, -0.2):
        button(P, S, open_a + 0.08, z, COAT2, r=0.012)
    path = [S.pos(lerp(0.55, TAU - 0.55, k / 10), 0.345, 0.022)[0] for k in range(11)]
    l0, _ = S.pos(-0.55, 0.3, 0.02)
    r0, _ = S.pos(0.5, 0.22, 0.026)
    P.add(tube([l0 + V((0, 0, -0.1)), l0] + list(reversed(path)) + [r0, r0 + V((0, 0.002, -0.06))], 0.009, 6), F(0.2, 0.22, 0.26))
    stick(P, S, 0.5, 0.15, cyl(0.03, 0.016, 10, bevel=0.005), SILVER, off=0.03)
    leg_cuffs(P, LS, T(0.8), w=0.026, thick=0.008)
    return P


# ----------------------------------------------------------------------------------------------------------
# HEADS. Origin = head centre. ~0.5 wide, no eyeballs: the game adds eye spheres at (+-0.1, front, +0.07),
# so that area only gets shallow sockets. Skin is tinted by the game (authored light, alpha 1).
HEAD_C = V((0, 0, -0.24))  # head centre seen from a hat/hair origin (head top)
BROW = F(0.24, 0.15, 0.1)
MOUTH = F(0.36, 0.12, 0.14)


def head_shape(jaw=0.0, cheek=0.0, chin=0.0):
    def fn(p, a, z):
        d = p.normalized()
        disp = 0.008 * gauss((p - V((0, -0.2, 0.05))).length_squared, 0.1)
        for sx in (-1, 1):
            disp -= 0.015 * gauss((p - V((sx * 0.1, 0.21, 0.07))).length_squared, 0.04)
            disp += 0.007 * gauss((p - V((sx * 0.1, 0.19, 0.16))).length_squared, 0.035)
            disp += cheek * gauss((p - V((sx * 0.145, 0.17, -0.075))).length_squared, 0.055)
        q = p + d * disp
        q.x *= 1 + jaw * gauss((p.z + 0.09) ** 2, 0.07)
        q += V((0, 0.45, -0.9)).normalized() * chin * gauss((p - V((0, 0.16, -0.2))).length_squared, 0.05)
        if q.z > 0:
            q.z *= 0.975
        return q
    return fn


def head(name, rad, jaw, cheek, chin, nose, ears, brows, mouth, blush):
    P = Part(name)
    pb = ell(rad[0], rad[1], rad[2], 24, 16, fn=head_shape(jaw, cheek, chin))
    R = RaySurf(bvh_of(pb), "front")
    spots = [V((sx * 0.15, 0.175, -0.065)) for sx in (-1, 1)]

    def skin(c, n):
        if blush and any((c - b).length < 0.05 for b in spots):
            return TC(1.0, 0.8, 0.8)
        return SKIN
    P.add(pb, SKIN, colfn=skin)
    # nose
    if nose == "button":
        p, n = R.pos(0.0, -0.025, 0.014)
        P.add(place(ell(0.04, 0.034, 0.034, 10, 6), p, n), T(0.93))
    elif nose == "bulb":
        p, n = R.pos(0.0, -0.035, 0.02)
        P.add(place(ell(0.054, 0.04, 0.044, 12, 6), p, n), T(0.93))
        for sx in (-1, 1):
            q, m = R.pos(sx * 0.03, -0.05, 0.012)
            P.add(place(ell(0.02, 0.02, 0.02, 6, 4), q, m), T(0.93))
    else:
        p, n = R.pos(0.0, -0.01, -0.012)
        P.add(place(spike(0.075, 0.034, 6, sy=0.85, shape=[(0, 0.075), (0.012, 0.06), (0.026, 0.035), (0.034, 0.012), (0.028, -0.01), (0, -0.015)]),
                    p, V((0, 1, -0.45))), T(0.93))
    # ears with a rosy inner shell
    er = {"round": ((0.02, 0.05, 0.065), 0.238, 0.35), "small": ((0.018, 0.04, 0.05), 0.24, 0.3),
          "big": ((0.022, 0.058, 0.082), 0.235, 0.6)}[ears]
    for sx in (-1, 1):
        (ex, ey, ez), cx, yaw = er
        M = Matrix.Translation(V((sx * cx, -0.015, -0.005))) @ Matrix.Rotation(sx * yaw, 4, "Z")
        P.add(xf(ell(ex, ey, ez, 10, 6), M=M), SKIN)
        P.add(xf(ell(0.008, ey * 0.6, ez * 0.62, 8, 5), M=M @ Matrix.Translation(V((sx * ex * 0.8, 0.004, 0)))), TC(0.92, 0.7, 0.7))
    # brows
    bp = {"arch": ([(0.052, 0.163), (0.1, 0.178), (0.148, 0.167)], 0.022),
          "short": ([(0.07, 0.17), (0.13, 0.172)], 0.028),
          "thick": ([(0.048, 0.18), (0.1, 0.17), (0.155, 0.154)], 0.032)}[brows]
    for sx in (-1, 1):
        rib(P, R, [(sx * x, z) for (x, z) in bp[0]], bp[1], BROW, off=0.003, thick=0.016, sub=3, bevel=0.45)
    # mouth
    if mouth == "smile":
        rib(P, R, [(x, -0.11 + 7 * x * x) for x in (-0.048, -0.024, 0.0, 0.024, 0.048)], 0.012, MOUTH, off=0.0, thick=0.006, sub=2)
    elif mouth == "open":
        xs = [lerp(-0.055, 0.055, k / 8) for k in range(9)]
        ws = [0.01 + 0.04 * sin(pi * k / 8) for k in range(9)]
        rib(P, R, [(x, -0.098 + 4.5 * x * x - w / 2) for x, w in zip(xs, ws)], lambda t: 0.01 + 0.04 * sin(pi * t), F(0.42, 0.1, 0.13),
            off=0.0, thick=0.006, sub=1)
        p, n = R.pos(0.0, -0.128, 0.004)
        P.add(place(ell(0.018, 0.016, 0.008, 8, 4), p, n), F(0.95, 0.45, 0.5))
    else:
        rib(P, R, [(-0.04, -0.108), (-0.01, -0.112), (0.02, -0.108), (0.048, -0.094)], 0.012, MOUTH, off=0.0, thick=0.006, sub=2)
        p, n = R.pos(0.058, -0.088, 0.0)
        P.add(place(ell(0.006, 0.006, 0.004, 6, 3), p, n), MOUTH)
    return P


def head_a():
    return head("Head_A", (0.25, 0.24, 0.24), 0.04, 0.0, 0.0, "button", "round", "arch", "smile", True)


def head_b():
    return head("Head_B", (0.255, 0.24, 0.232), 0.06, 0.028, 0.0, "bulb", "small", "short", "open", True)


def head_c():
    return head("Head_C", (0.245, 0.235, 0.245), 0.0, 0.0, 0.035, "pointy", "big", "thick", "smirk", False)


# ----------------------------------------------------------------------------------------------------------
# HAIR (origin = head top) and FACIAL HAIR (origin = head centre). Hair is tinted (game hair colour).
def stripes(segs, a_col=T(0.95), b_col=T(0.87), centre=V((0, 0, 0))):
    def fn(c, n):
        a = atan2(c.x - centre.x, c.y - centre.y)
        return a_col if int(math.floor((a + pi) / (TAU / segs) + 0.5)) % 2 == 0 else b_col
    return fn


def lock(P, base, tip, r, n, col=T(0.95), segs=5, flat=0.55):
    d = tip - base
    P.add(place(spike(d.length, r, segs, sy=flat), base, d, up=n), col)


def hair_short():
    P = Part("Hair_Short")
    r = (0.272, 0.262, 0.258)
    E = EllS(HEAD_C, r)

    def phim(a):
        b = 1.405 - 0.64 * cos(a) - 0.045 * cos(2 * a)
        for sd in (-1, 1):
            b += 0.22 * gauss(angd(a, sd * 1.42) ** 2, 0.12)
        return b

    def quiff(p, a, ph):
        return p + (p - HEAD_C).normalized() * 0.03 * gauss((p - V((0.03, 0.12, -0.06))).length_squared, 0.07)
    P.add(dome(r, HEAD_C.z, phim, 24, 7, lip=0.022, zig=0.07, fn=quiff), T(0.95), colfn=stripes(24, centre=HEAD_C), recalc=False)
    for a in (-0.55, -0.3, -0.05, 0.2, 0.42):
        base, n = E.pos(a, phim(a) - 0.32, 0.0)
        tip, _ = E.pos(a + 0.2, phim(a) + 0.1, 0.014)
        lock(P, base, tip, 0.042, n)
    base, n = E.pos(pi, 0.45, -0.01)
    lock(P, base, base + (n + V((0, -0.6, 0.3))).normalized() * 0.08, 0.03, V((0, -1, 0)))
    return P


def hair_bun():
    P = Part("Hair_Bun")
    r = (0.266, 0.257, 0.252)
    E = EllS(HEAD_C, r)

    def phim(a):
        return 1.33 - 0.6 * cos(a) - 0.05 * cos(2 * a)
    P.add(dome(r, HEAD_C.z, phim, 24, 7, lip=0.02, zig=0.0), T(0.95), colfn=stripes(24, T(0.95), T(0.83), HEAD_C), recalc=False)
    p, n = E.pos(pi, 0.62, 0.055)
    P.add(place(ell(0.09, 0.085, 0.08, 12, 8), p, n), T(0.95))
    P.add(place(torus(0.075, 0.012, 14, 4), p + n * 0.01, n + V((0.3, 0, 0))), T(0.84))
    P.add(place(torus(0.062, 0.018, 14, 6), p - n * 0.05, n), F(0.88, 0.28, 0.42))
    for sd in (-1, 1):
        base, nn = E.pos(sd * 1.2, 1.25, -0.004)
        tip, _ = E.pos(sd * 1.28, 1.9, 0.012)
        lock(P, base, tip, 0.022, nn, segs=5)
    for a in (-0.25, 0.0, 0.25):
        base, nn = E.pos(a, 0.4, -0.002)
        tip, _ = E.pos(a * 1.6, 0.78, 0.008)
        lock(P, base, tip, 0.03, nn, flat=0.4)
    return P


def hair_long():
    P = Part("Hair_Long")
    r = (0.274, 0.264, 0.26)
    E = EllS(HEAD_C, r)

    def phim(a):
        return 1.38 - 0.66 * cos(a) - 0.02 * cos(2 * a)
    P.add(dome(r, HEAD_C.z, phim, 24, 7, lip=0.02, zig=0.05), T(0.95), colfn=stripes(24, centre=HEAD_C), recalc=False)
    rows = [(0.272, 0.06), (0.274, -0.04), (0.278, -0.13), (0.285, -0.21), (0.295, -0.28)]
    grid = []
    cols = 17
    for i, (rr, z) in enumerate(rows):
        row = []
        for j in range(cols):
            a = lerp(1.15, TAU - 1.15, j / (cols - 1))
            zz = z - (0.035 if (i == len(rows) - 1 and j % 2) else 0.0)
            row.append(V((sin(a) * rr, cos(a) * rr * 0.965, zz + HEAD_C.z)))
        grid.append(row)

    def inner(c, n):
        radial = V((c.x, c.y, 0)).normalized()
        return T(0.68) if n.dot(radial) < -0.3 else None
    P.add(slab(grid, lambda q: -V((q.x, q.y, 0)).normalized() * 0.03), T(0.95), colfn=inner)
    for sd in (-1, 1):
        base, n = E.pos(sd * 1.05, 1.0, 0.0)
        tip = V((sd * 0.25, 0.1, HEAD_C.z - 0.2))
        lock(P, base, tip, 0.045, n)
        for a in (0.12, 0.32, 0.52):
            base, n = E.pos(sd * a, 0.38, 0.0)
            tip, _ = E.pos(sd * (a + 0.22), phim(a) + 0.08, 0.014)
            lock(P, base, tip, 0.04, n)
    return P


def hair_mohawk():
    P = Part("Hair_Mohawk")
    r = (0.258, 0.249, 0.246)
    E = EllS(HEAD_C, r)

    def phim(a):
        return 1.405 - 0.64 * cos(a) - 0.045 * cos(2 * a)
    P.add(dome(r, HEAD_C.z, phim, 24, 6, lip=0.008), T(0.45), recalc=False)
    E2 = EllS(HEAD_C, (0.262, 0.253, 0.25))
    path = []
    for k in range(8):
        t = k / 7
        psi = lerp(-0.62, 1.7, t)
        a, v = (0.0, -psi) if psi < 0 else (pi, psi)
        base, n = E2.pos(a, abs(v), -0.004)
        path.append((a, abs(v)))
        L = lerp(0.1, 0.19, 1 - abs(t - 0.38) / 0.62)
        d = (n + V((0, -0.55, 0))).normalized()
        P.add(place(spike(L, 0.055, 6, sx=0.36), base, d, up=V((0, 1, 0))), T(0.95))
    st = []
    for k in range(15):
        psi = lerp(-0.62, 1.7, k / 14)
        a, v = (0.0, -psi) if psi < 0 else (pi, psi)
        st.append(E2.pos(a, abs(v), 0.0))
    P.add(ribbon3d(st, [0.06] * len(st), 0.02), T(0.9), recalc=False)
    return P


def mustache_pieces(P, col=T(0.95)):
    for sx in (-1, 1):
        M = Matrix.Translation(V((sx * 0.045, 0.236, -0.073))) @ Euler((0, sx * 0.3, -sx * 0.35), "XYZ").to_matrix().to_4x4()
        P.add(xf(ell(0.052, 0.022, 0.021, 10, 5), M=M), col)
        P.add(place(spike(0.045, 0.013, 5), V((sx * 0.092, 0.21, -0.088)), V((sx * 0.5, -0.25, 1.0))), col)


def beard_full():
    P = Part("Beard_Full")
    r = (0.27, 0.27, 0.262)

    def phim(a):
        return 1.24 + 0.23 * cos(a) - 0.41 * cos(2 * a)

    def bulk(p, a, ph):
        return p + V((0, 0.35, -1.0)).normalized() * 0.045 * gauss((p - V((0, 0.2, -0.2))).length_squared, 0.09)
    P.add(dome(r, 0.0, phim, 24, 7, lip=0.024, zig=0.05, flip=True, fn=bulk), T(0.95), colfn=stripes(24), recalc=False)
    mustache_pieces(P)
    return P


def mustache():
    P = Part("Mustache")
    mustache_pieces(P)
    return P


# ----------------------------------------------------------------------------------------------------------
# HATS. Origin = head top (head centre + 0.24). Brims/crowns sized to sit on a 0.5 m head.
def brim(rings, segs=28, sy=0.92, z0=-0.08, curl=0.05, dip=0.025, thick=0.014, base_z=None):
    grid = []
    r0, r1 = rings[0], rings[-1]
    for rr in rings:
        t = (rr - r0) / (r1 - r0)
        row = []
        for j in range(segs):
            a = TAU * j / segs
            dz = t * t * curl * sin(a) ** 2 - t * dip * max(cos(a), 0.0) ** 2 - t * dip * 0.5 * max(-cos(a), 0) ** 2
            row.append(V((sin(a) * rr, cos(a) * rr * sy, z0 + dz)))
        grid.append(row)
    return slab(grid, lambda p: V((0, 0, -thick)), wrap=True)


def crown_fn(crease=0.035, pinch=0.2, z_top=0.07):
    def fn(p, a, z):
        q = V(p)
        if z > z_top:
            q.z -= crease * gauss(p.x * p.x, 0.045) * smooth(z_top, z_top + 0.04, z)
        if p.y > 0 and z > 0.0:
            q.x *= 1 - pinch * smooth(0.0, 0.11, z) * smooth(0.0, 0.17, p.y)
        return q
    return fn


def hat_fedora():
    P = Part("Hat_Fedora")
    prof = [(0, 0.105), (0.07, 0.115), (0.14, 0.118), (0.172, 0.095), (0.182, 0.02), (0.19, -0.085), (0, -0.085)]
    P.add(lathe(prof, 24, 1.0, 0.93, fn=crown_fn()), T(0.95))
    P.add(lathe([(0.183, -0.028), (0.192, -0.033), (0.196, -0.072), (0.188, -0.078)], 24, 1.0, 0.93, loop=True), F(0.13, 0.1, 0.1))
    P.add(xf(box((0.02, 0.05, 0.035), 0.006), loc=(-0.195, -0.02, -0.052)), F(0.13, 0.1, 0.1))
    P.add(brim([0.175, 0.22, 0.27, 0.31, 0.33], z0=-0.078, curl=0.05, dip=0.025), T(0.95))
    return P


def hat_cap():
    P = Part("Hat_Cap")
    r = (0.268, 0.262, 0.256)
    E = EllS(HEAD_C, r)

    def phim(a):
        return 1.15 - 0.45 * cos(a)
    P.add(dome(r, HEAD_C.z, phim, 24, 6, lip=0.016), T(0.95), recalc=False)
    for k in range(6):
        a = pi / 6 + k * pi / 3
        rib(P, E, [(a, 0.1), (a, phim(a) - 0.02)], 0.006, T(0.78), off=0.0, thick=0.004, sub=5)
    P.add(xf(ell(0.024, 0.024, 0.012, 8, 4), loc=(0, 0, 0.016)), T(0.85))
    grid = []
    for i in range(4):
        v = i / 3
        row = []
        for j in range(9):
            a = lerp(-1.05, 1.05, j / 8)
            base, n = E.pos(a, phim(a) - 0.03, 0.0)
            h = V((sin(a), cos(a), 0))
            L = 0.16 * cos(a * 0.9) ** 0.7
            row.append(base + h * L * v + V((0, 0, -0.035 * v - 0.035 * v * (a / 1.05) ** 2)))
        grid.append(row)

    def under(c, n):
        return F(0.22, 0.36, 0.24) if n.z < -0.5 else None
    P.add(slab(grid, lambda p: V((0, 0, -0.013))), T(0.95), colfn=under)
    stick(P, E, 0.0, 0.5, cyl(0.045, 0.006, 12), WHITE, off=0.0)
    stick(P, E, 0.0, 0.5, star_piece(0.03, 0.008), F(0.92, 0.3, 0.25), off=0.005)
    stick(P, E, pi, 1.45, box((0.07, 0.04, 0.012), 0.004), F(0.15, 0.15, 0.17), off=0.0)
    return P


def hat_beret():
    P = Part("Hat_Beret")
    prof = [(0, 0.055), (0.1, 0.052), (0.2, 0.035), (0.262, 0.006), (0.27, -0.012), (0.25, -0.03), (0.2, -0.044), (0.168, -0.05), (0, -0.05)]
    M = Matrix.Translation(V((0.035, 0.0, -0.015))) @ Matrix.Rotation(0.22, 4, "Y")
    P.add(xf(lathe(prof, 24, 1.0, 0.95, fn=lambda p, a, z: V((p.x * (1 + 0.035 * sin(5 * a)), p.y * (1 + 0.035 * sin(5 * a)), z))), M=M), T(0.95))
    P.add(xf(tube([V((0, 0, 0.04)), V((0.006, 0, 0.085))], 0.009, 5), M=M), T(0.85))
    return P


def hat_crown():
    P = Part("Hat_Crown")
    segs = 20
    grid = []
    for (rr, zb) in ((0.172, -0.06), (0.178, 0.0)):
        grid.append([V((sin(TAU * j / segs) * rr, cos(TAU * j / segs) * rr, zb)) for j in range(segs)])
    top = []
    for j in range(segs):
        z = {0: 0.125, 2: 0.045}.get(j % 4, 0.085)
        top.append(V((sin(TAU * j / segs) * 0.19, cos(TAU * j / segs) * 0.19, z)))
    grid.append(top)
    P.add(slab(grid, lambda p: -V((p.x, p.y, 0)).normalized() * 0.014, wrap=True), GOLD)
    for j in range(segs):
        a = TAU * j / segs
        if j % 4 == 0:
            P.add(xf(ell(0.018, 0.018, 0.018, 8, 4), loc=(sin(a) * 0.19, cos(a) * 0.19, 0.135)), GOLD)
        if j % 4 == 2:
            gem = lathe([(0, 0.022), (0.02, 0.0), (0, -0.022)], 4)
            col = (F(0.85, 0.1, 0.18), F(0.2, 0.45, 0.95), F(0.2, 0.8, 0.4), F(0.85, 0.1, 0.18), F(0.2, 0.45, 0.95))[j // 4]
            P.add(place(gem, V((sin(a) * 0.19, cos(a) * 0.19, 0.0)), V((sin(a), cos(a), 0))), col)

    def ermine(c, n):
        h = int(abs(c.x * 113 + c.y * 71 + c.z * 37) * 10) % 7
        return F(0.1, 0.1, 0.1) if h == 0 else None
    P.add(xf(torus(0.18, 0.024, 20, 6, rz=0.022), loc=(0, 0, -0.058)), WHITE, colfn=ermine)
    P.add(xf(ell(0.168, 0.168, 0.075, 16, 6), loc=(0, 0, 0.015)), T(0.95))
    P.add(xf(ell(0.022, 0.022, 0.022, 8, 5), loc=(0, 0, 0.098)), GOLD)
    P.add(xf(box((0.012, 0.012, 0.05), 0.003), loc=(0, 0, 0.13)), GOLD)
    P.add(xf(box((0.036, 0.012, 0.012), 0.003), loc=(0, 0, 0.138)), GOLD)
    return P


def hat_cowboy():
    P = Part("Hat_Cowboy")
    prof = [(0, 0.15), (0.08, 0.155), (0.15, 0.15), (0.18, 0.12), (0.19, 0.03), (0.195, -0.085), (0, -0.085)]
    P.add(lathe(prof, 24, 1.0, 0.92, fn=crown_fn(crease=0.05, pinch=0.17, z_top=0.09)), T(0.95))
    P.add(lathe([(0.188, -0.03), (0.197, -0.034), (0.2, -0.072), (0.192, -0.078)], 24, 1.0, 0.92, loop=True), F(0.36, 0.22, 0.12))
    P.add(place(cyl(0.022, 0.01, 8, bevel=0.003), V((-0.13, 0.135, -0.053)), V((-0.7, 0.7, 0))), SILVER)

    def edge(c, n):
        return T(0.82) if V((c.x, c.y / 0.9, 0)).length > 0.375 else None
    P.add(brim([0.18, 0.24, 0.3, 0.36, 0.4], sy=0.9, z0=-0.08, curl=0.12, dip=0.02, thick=0.014), T(0.95), colfn=edge)
    return P


def hat_turban():
    P = Part("Hat_Turban")
    P.add(xf(ell(0.222, 0.215, 0.17, 16, 8), loc=(0, 0, -0.02)), T(0.8))
    for k in range(4):
        M = Matrix.Translation(V((0, 0, -0.07 + 0.05 * k))) @ Matrix.Rotation((0.16 if k % 2 else -0.16), 4, "X")
        P.add(xf(torus(0.2 - 0.03 * k, 0.05, 20, 6, rz=0.042), M=M), T(0.95) if k % 2 == 0 else T(0.86))
    p, n = V((0, 0.235, -0.005)), V((0, 1, 0.15))
    P.add(place(cyl(0.032, 0.014, 10, bevel=0.004), p, n), GOLD)
    P.add(place(ell(0.02, 0.02, 0.012, 8, 4), p + V((0, 0.01, 0.002)), n), F(0.85, 0.1, 0.2))
    P.add(place(spike(0.13, 0.045, 6, sx=0.42), p + V((0, -0.015, 0.02)), V((0, -0.3, 1)), up=V((0, 1, 0))), WHITE)
    return P


def hat_beanie():
    P = Part("Hat_Beanie")
    r = (0.272, 0.264, 0.26)
    E = EllS(HEAD_C, r)

    def phim(a):
        return 1.18 - 0.475 * cos(a)

    def tall(p, a, ph):
        return p + V((0, -0.01, 0.055)) * smooth(0.1, 0.25, p.z - HEAD_C.z)
    P.add(dome(r, HEAD_C.z, phim, 24, 7, lip=0.02, fn=tall), T(0.95), colfn=stripes(24, T(0.95), T(0.86), HEAD_C), recalc=False)
    rib(P, E, [(TAU * k / 24, phim(TAU * k / 24) - 0.16) for k in range(24)], 0.1, T(0.9), off=0.0, thick=0.022, closed=True, sub=1,
        colfn=stripes(24, T(0.9), T(0.8), HEAD_C))
    P.add(xf(ell(0.065, 0.065, 0.058, 10, 6), loc=(0, -0.012, 0.1)), WHITE)
    return P


def hat_tophat():
    P = Part("Hat_TopHat")
    prof = [(0, 0.27), (0.15, 0.27), (0.166, 0.262), (0.163, 0.1), (0.153, -0.02), (0.156, -0.058), (0, -0.058)]
    P.add(lathe(prof, 24, 1.0, 0.93), T(0.95))
    P.add(lathe([(0.152, 0.03), (0.162, 0.026), (0.159, -0.024), (0.15, -0.03)], 24, 1.0, 0.93, loop=True), F(0.6, 0.1, 0.12))
    P.add(place(box((0.045, 0.06, 0.004), 0.002), V((0.16, -0.03, 0.03)), V((1, 0, 0)), spin=0.25), WHITE)
    P.add(place(cyl(0.012, 0.004, 4), V((0.163, -0.03, 0.03)), V((1, 0, 0))), F(0.85, 0.15, 0.2))
    P.add(brim([0.14, 0.19, 0.235, 0.265], sy=0.93, z0=-0.055, curl=0.05, dip=0.008, thick=0.014), T(0.95))
    return P


def hat_helmet():
    P = Part("Hat_Helmet")
    r = (0.292, 0.286, 0.278)
    E = EllS(HEAD_C, r)

    def phim(a):
        return 1.3 - 0.47 * cos(a)

    def lip(p, a, ph):
        pm = phim(a)
        return HEAD_C + (p - HEAD_C) * (1 + 0.07 * smooth(pm - 0.28, pm, ph))
    P.add(dome(r, HEAD_C.z, phim, 24, 8, lip=0.024, fn=lip), T(0.95), colfn=camo, recalc=False)
    BAND = F(0.18, 0.19, 0.14)
    rib(P, E, [(TAU * k / 24, phim(TAU * k / 24) - 0.4) for k in range(24)], 0.032, BAND, off=0.0, thick=0.012, closed=True, sub=1)
    for sd in (-1, 1):
        stick(P, E, sd * 0.26, phim(0.26) - 0.38, cyl(0.042, 0.026, 10, bevel=0.008), F(0.24, 0.26, 0.2), off=0.012)
        stick(P, E, sd * 0.26, phim(0.26) - 0.38, cyl(0.032, 0.01, 10, bevel=0.003), F(0.35, 0.55, 0.72), off=0.03)
        a = sd * pi / 2
        p0, n0 = E.pos(a, phim(a) - 0.06, 0.004)
        p1 = V((sd * 0.24, 0.05, HEAD_C.z - 0.12))
        P.add(tube([p0, p0.lerp(p1, 0.5) + V((sd * 0.012, 0, 0)), p1], 0.009, 5), BAND)
        P.add(place(box((0.028, 0.02, 0.012), 0.003), p1, V((sd, 0.2, 0))), F(0.08))
    P.add(place(ell(0.06, 0.04, 0.02, 8, 4), E.pos(pi, 0.9, 0.01)[0], E.pos(pi, 0.9)[1]), T(0.6))
    return P


def hat_chef():
    P = Part("Hat_Chef")
    P.add(lathe([(0, 0.04), (0.188, 0.04), (0.196, 0.034), (0.196, -0.08), (0.188, -0.088), (0, -0.088)], 20, 1.0, 0.95), T(0.97),
          colfn=stripes(20, T(0.97), T(0.89)))

    def puff(p, a, z):
        k = 1 + 0.055 * cos(7 * a) * smooth(0.04, 0.12, z)
        return V((p.x * k, p.y * k, z + 0.018 * cos(7 * a) * smooth(0.2, 0.29, z)))
    prof = [(0.18, 0.03), (0.226, 0.08), (0.25, 0.15), (0.25, 0.215), (0.228, 0.258), (0.17, 0.285), (0.08, 0.297), (0, 0.3)]
    P.add(lathe(list(reversed(prof)), 28, 1.0, 0.95, fn=puff), T(0.97))
    return P


def hat_headset():
    P = Part("Hat_Headset")
    DARK = F(0.12, 0.12, 0.14)
    st = []
    for k in range(15):
        t = lerp(-1.4, 1.4, k / 14)
        n = V((sin(t) / 0.29, 0, cos(t) / 0.275)).normalized()
        st.append((V((sin(t) * 0.29, 0.0, cos(t) * 0.275)) + HEAD_C + V((0, 0, 0.01)), n))
    P.add(ribbon3d(st, [0.032] * 15, 0.014), DARK, recalc=False)
    for sd in (-1, 1):
        c = V((sd * 0.29, 0.0, HEAD_C.z - 0.02))
        P.add(place(cyl(0.062, 0.04, 14, bevel=0.012), c, V((sd, 0, 0))), DARK)
        P.add(place(cyl(0.04, 0.01, 12, bevel=0.003), c + V((sd * 0.022, 0, 0)), V((sd, 0, 0))), F(0.3, 0.3, 0.34))
        P.add(place(torus(0.05, 0.014, 14, 4), c - V((sd * 0.02, 0, 0)), V((sd, 0, 0))), F(0.25, 0.25, 0.28))
    P.add(place(cyl(0.007, 0.006, 6), V((0.326, 0.03, HEAD_C.z - 0.0)), V((1, 0, 0))), F(0.3, 0.95, 0.4))
    path = [V((0.3, 0.04, HEAD_C.z - 0.04)), V((0.285, 0.12, HEAD_C.z - 0.09)), V((0.22, 0.22, HEAD_C.z - 0.12)), V((0.11, 0.275, HEAD_C.z - 0.115))]
    P.add(tube(path, 0.008, 6), DARK)
    P.add(xf(ell(0.02, 0.02, 0.018, 8, 5), loc=path[-1] + V((-0.01, 0.005, 0))), F(0.05, 0.05, 0.06))
    return P


# ----------------------------------------------------------------------------------------------------------
# HANDS (origin = palm centre, fingers to +Y, thumb up +Z; symmetric so one part serves both hands).
def mitten_body(P, col, cuff_col, groove_col):
    prof = [(0, 0.09), (0.034, 0.087), (0.052, 0.074), (0.06, 0.05), (0.062, 0.02), (0.059, -0.015), (0.052, -0.045),
            (0.043, -0.064), (0.028, -0.076), (0, -0.08)]
    pb = lathe(prof, 14, 0.66, 1.06)
    xf(pb, rot=(-pi / 2, 0, 0))
    for v in pb.verts:
        v.co.z -= 2.5 * max(0.0, v.co.y - 0.02) ** 2
        v.co.x *= 1.0 - 0.18 * smooth(0.0, 0.065, -v.co.z)  # palm side a bit slimmer
    bvh = bvh_of(pb)
    P.add(pb, col)
    P.add(place(ell(0.026, 0.024, 0.05, 8, 6), V((0, 0.0, 0.058)), V((0, 0.75, 1))), col)
    if cuff_col:
        P.add(xf(torus(0.038, 0.008, 14, 4, sy=1.0), rot=(-pi / 2, 0, 0), loc=(0, -0.066, 0)), cuff_col)
    for sd in (-1, 1):
        R = RaySurf(bvh, sd)
        for z in (-0.022, 0.022):
            rib(P, R, [(0.035, z), (0.08, z * 0.8)], 0.005, groove_col, off=0.0, thick=0.003, sub=3)
    return bvh


def hand_mitten():
    P = Part("Hand_Mitten")
    mitten_body(P, SKIN, T(0.84), T(0.8))
    return P


def hand_glove():
    P = Part("Hand_Glove")
    G = F(0.16, 0.16, 0.18)
    bvh = mitten_body(P, G, None, F(0.09, 0.09, 0.1))
    P.add(xf(lathe([(0.05, -0.045), (0.07, -0.1), (0.062, -0.106), (0.044, -0.052)], 14, 0.85, 1.12, loop=True), rot=(-pi / 2, 0, 0)), F(0.1, 0.1, 0.11))
    P.add(xf(torus(0.05, 0.01, 14, 4, sy=1.12), rot=(-pi / 2, 0, 0), loc=(0, -0.04, 0)), F(0.3, 0.3, 0.33))
    for sd in (-1, 1):
        R = RaySurf(bvh, sd)
        stick(P, R, 0.045, 0.0, box((0.045, 0.06, 0.012), 0.005), F(0.28, 0.28, 0.31), off=0.0)
    return P


# ----------------------------------------------------------------------------------------------------------
# FEET (origin = bottom-centre of the sole, toe to +Y). Uppers tinted, soles fixed.
def shoe_upper(rings, slope=0.45, slope_max=0.1, e=2.4):
    out = superellipse(24, 0.094, 0.133, e, 0.16)

    def fn(p):
        if p.z > 0.02:
            k = 1 - slope * smooth(0.0, 0.13, p.y) * smooth(0.0, slope_max, p.z) * (1.0 if p.z <= slope_max + 0.02 else 0.0)
            p.z = 0.02 + (p.z - 0.02) * (k if p.z <= slope_max + 0.02 else 1.0)
        return p
    return loft(out, rings, fn)


def sole(rings):
    return loft(superellipse(24, 0.097, 0.137, 2.4, 0.16), rings)


def opening_dark(zmin, ymax=0.0):
    def fn(c, n):
        return F(0.1, 0.09, 0.09) if (c.z > zmin and n.z > 0.75 and c.y < ymax) else None
    return fn


def foot_shoe():
    P = Part("Foot_Shoe")
    SOLE = F(0.1, 0.08, 0.07)
    P.add(sole([(0.97, 0.0, 0), (1.0, 0.006, 0), (1.0, 0.022, 0), (0.97, 0.026, 0)]), SOLE)
    P.add(xf(box((0.13, 0.07, 0.03), 0.006), loc=(0, -0.098, 0.015)), SOLE)
    up = shoe_upper([(0.985, 0.022, 0), (0.99, 0.045, 0), (0.94, 0.075, -0.006), (0.8, 0.1, -0.02), (0.6, 0.117, -0.035), (0.32, 0.126, -0.045)],
                    slope=0.62, slope_max=0.11)
    bvh = bvh_of(up)
    P.add(up, T(0.95), colfn=opening_dark(0.115, 0.0))
    R = RaySurf(bvh, "top")
    rib(P, R, [(x, 0.06 - 3.5 * x * x) for x in (-0.075, -0.04, 0.0, 0.04, 0.075)], 0.005, T(0.72), off=0.0, thick=0.003, sub=2)
    for y in (0.005, 0.03):
        rib(P, R, [(-0.028, y), (0.028, y)], 0.008, F(0.1, 0.09, 0.09), off=0.0, thick=0.005, sub=2)
    return P


def foot_boot():
    P = Part("Foot_Boot")
    SOLE = F(0.12, 0.11, 0.1)

    def tread(c, n):
        return F(0.06, 0.06, 0.06) if (n.z < 0.3 and n.z > -0.3 and int((c.y + 1) / 0.03) % 2 == 0 and c.z < 0.03) else None
    P.add(sole([(0.97, 0.0, 0), (1.03, 0.008, 0), (1.03, 0.032, 0), (0.98, 0.038, 0)]), SOLE, colfn=tread)
    up = shoe_upper([(0.99, 0.034, 0), (0.99, 0.06, 0), (0.93, 0.085, -0.008), (0.78, 0.11, -0.025), (0.7, 0.14, -0.035),
                     (0.69, 0.168, -0.035), (0.66, 0.178, -0.035)], slope=0.4, slope_max=0.09)
    bvh = bvh_of(up)

    def boot(c, n):
        if c.z > 0.172 and n.z > 0.7:
            return F(0.08, 0.07, 0.07)
        if c.y > 0.075 and c.z < 0.075:
            return T(0.7)
        return None
    P.add(up, T(0.95), colfn=boot)
    P.add(xf(torus(0.064, 0.013, 14, 5, sy=1.36), loc=(0, -0.035, 0.176)), F(0.12, 0.11, 0.1))
    R = RaySurf(bvh, "front")
    for z in (0.1, 0.125, 0.15):
        rib(P, R, [(-0.034, z), (0.034, z + 0.018)], 0.008, F(0.08), off=0.0, thick=0.005, sub=2)
        rib(P, R, [(0.034, z), (-0.034, z + 0.018)], 0.008, F(0.08), off=0.004, thick=0.005, sub=2)
        for sx in (-1, 1):
            stick(P, R, sx * 0.04, z, cyl(0.007, 0.006, 6), SILVER, off=0.002)
    Rb = RaySurf(bvh, "front")
    P.add(xf(box((0.03, 0.012, 0.045), 0.004), loc=(0, -0.13, 0.17)), F(0.12, 0.11, 0.1))
    return P


def foot_sneaker():
    P = Part("Foot_Sneaker")

    def mid(c, n):
        return F(0.42, 0.42, 0.45) if c.z < 0.008 else None
    P.add(sole([(0.97, 0.0, 0), (1.0, 0.008, 0), (1.03, 0.03, 0), (1.0, 0.042, 0)]), WHITE, colfn=mid)
    up = shoe_upper([(0.97, 0.036, 0), (0.98, 0.06, 0), (0.92, 0.085, -0.006), (0.78, 0.108, -0.018), (0.56, 0.122, -0.032),
                     (0.28, 0.128, -0.04)], slope=0.38)
    bvh = bvh_of(up)

    def toe(c, n):
        if c.z > 0.112 and n.z > 0.75 and c.y < 0.0:
            return F(0.12, 0.12, 0.14)
        if c.y > 0.09 and c.z < 0.075:
            return WHITE
        return None
    P.add(up, T(0.95), colfn=toe)
    for sd in (-1, 1):
        R = RaySurf(bvh, sd)
        for k in range(2):
            rib(P, R, [(-0.07 + 0.025 * k, 0.05), (0.02 + 0.025 * k, 0.098)], 0.013, WHITE, off=0.0, thick=0.004, sub=3)
    R = RaySurf(bvh, "top")
    for y in (-0.005, 0.02, 0.045):
        rib(P, R, [(-0.03, y), (0.03, y)], 0.009, WHITE, off=0.0, thick=0.006, sub=2)
    P.add(xf(box((0.03, 0.02, 0.045), 0.006), loc=(0, -0.132, 0.1)), F(0.95, 0.42, 0.3))
    return P


# ----------------------------------------------------------------------------------------------------------
# ACCESSORIES (origin = head centre).
def acc_sunglasses():
    P = Part("Acc_Sunglasses")
    FRAME = F(0.07, 0.07, 0.08)
    for sx in (-1, 1):
        M = Matrix.Translation(V((sx * 0.1, 0.27, 0.075))) @ Matrix.Rotation(sx * 0.18, 4, "Z") @ Matrix.Rotation(pi / 2, 4, "X")
        out = superellipse(20, 0.07, 0.052, 3.0)
        P.add(xf(loft(out, [(1.0, -0.008, 0), (1.0, 0.004, 0)]), M=M), FRAME)
        P.add(xf(loft(out, [(0.86, 0.0, 0), (0.86, 0.01, 0)]), M=M), F(0.07, 0.09, 0.14))
        P.add(xf(loft([(-0.03, 0.02), (-0.01, 0.03), (0.03, -0.01), (0.012, -0.022)], [(1.0, 0.009, 0), (1.0, 0.012, 0)]), M=M), F(0.55, 0.65, 0.78))
        path = [V((sx * 0.168, 0.252, 0.095)), V((sx * 0.19, 0.17, 0.092)), V((sx * 0.228, 0.08, 0.085)), V((sx * 0.252, 0.0, 0.075)), V((sx * 0.256, -0.05, 0.045))]
        P.add(tube(path, 0.007, 5), FRAME)
    P.add(tube([V((-0.035, 0.272, 0.09)), V((0.0, 0.276, 0.1)), V((0.035, 0.272, 0.09))], 0.008, 5), FRAME)
    return P


def acc_earpiece():
    P = Part("Acc_Earpiece")
    P.add(xf(ell(0.016, 0.016, 0.02, 8, 5), loc=(0.255, 0.04, -0.01)), F(0.15, 0.15, 0.17))
    pts = []
    a0, a1 = V((0.255, 0.035, -0.03)), V((0.2, -0.07, -0.3))
    for k in range(73):
        t = k / 72
        c = a0.lerp(a1, t) + V((0.025 * sin(pi * t), 0, 0))
        ang = t * TAU * 9
        pts.append(c + V((cos(ang) * 0.007, sin(ang) * 0.007, 0)))
    P.add(tube(pts, 0.0028, 4), F(0.86, 0.9, 0.94))
    return P


# === MORE PARTS ===


# ----------------------------------------------------------------------------------------------------------
# Registry: name -> (kind, builder, pivot description, preview tint)
HAT_PIVOT = "head top-centre (head centre + 0.24 m up); modelled around a 0.5 m head"
REGISTRY = [
    ("Torso_Suit", "torso", torso_suit, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.25, 0.3, 0.45)),
    ("Torso_Guard", "torso", torso_guard, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.55, 0.65, 0.8)),
    ("Torso_Military", "torso", torso_military, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.45, 0.5, 0.3)),
    ("Torso_Casual", "torso", torso_casual, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.9, 0.45, 0.35)),
    ("Torso_Worker", "torso", torso_worker, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.35, 0.5, 0.75)),
    ("Torso_Fancy", "torso", torso_fancy, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.55, 0.25, 0.6)),
    ("Torso_Biker", "torso", torso_biker, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.85, 0.85, 0.8)),
    ("Torso_Chef", "torso", torso_chef, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (1.0, 1.0, 1.0)),
    ("Torso_Doctor", "torso", torso_doctor, "torso pivot (game Torso y 0.78); mesh spans -0.55..+0.40", (0.35, 0.7, 0.7)),
    ("Head_A", "head", head_a, "head centre (game Head y 1.42)", (1.0, 0.82, 0.68)),
    ("Head_B", "head", head_b, "head centre (game Head y 1.42)", (0.76, 0.55, 0.4)),
    ("Head_C", "head", head_c, "head centre (game Head y 1.42)", (0.98, 0.72, 0.6)),
    ("Hair_Short", "hair", hair_short, HAT_PIVOT, (0.35, 0.22, 0.12)),
    ("Hair_Bun", "hair", hair_bun, HAT_PIVOT, (0.85, 0.65, 0.3)),
    ("Hair_Long", "hair", hair_long, HAT_PIVOT, (0.6, 0.25, 0.15)),
    ("Hair_Mohawk", "hair", hair_mohawk, HAT_PIVOT, (0.3, 0.85, 0.4)),
    ("Beard_Full", "facial", beard_full, "head centre (same pivot as the Head)", (0.4, 0.26, 0.15)),
    ("Mustache", "facial", mustache, "head centre (same pivot as the Head)", (0.25, 0.17, 0.12)),
    ("Hat_Fedora", "hat", hat_fedora, HAT_PIVOT, (0.45, 0.38, 0.3)),
    ("Hat_Cap", "hat", hat_cap, HAT_PIVOT, (0.85, 0.25, 0.2)),
    ("Hat_Beret", "hat", hat_beret, HAT_PIVOT, (0.75, 0.15, 0.25)),
    ("Hat_Crown", "hat", hat_crown, HAT_PIVOT, (0.75, 0.1, 0.2)),
    ("Hat_Cowboy", "hat", hat_cowboy, HAT_PIVOT, (0.7, 0.5, 0.3)),
    ("Hat_Turban", "hat", hat_turban, HAT_PIVOT, (0.95, 0.75, 0.3)),
    ("Hat_Beanie", "hat", hat_beanie, HAT_PIVOT, (0.3, 0.6, 0.85)),
    ("Hat_TopHat", "hat", hat_tophat, HAT_PIVOT, (0.15, 0.15, 0.18)),
    ("Hat_Helmet", "hat", hat_helmet, HAT_PIVOT, (0.45, 0.5, 0.3)),
    ("Hat_Chef", "hat", hat_chef, HAT_PIVOT, (1.0, 1.0, 1.0)),
    ("Hat_Headset", "hat", hat_headset, HAT_PIVOT, (1.0, 1.0, 1.0)),
    ("Hand_Mitten", "hand", hand_mitten, "palm centre; fingers +forward, thumb up; symmetric (both hands)", (1.0, 0.82, 0.68)),
    ("Hand_Glove", "hand", hand_glove, "palm centre; fingers +forward, thumb up; symmetric (both hands)", (1.0, 1.0, 1.0)),
    ("Foot_Shoe", "foot", foot_shoe, "bottom-centre of the sole; toe +forward", (0.3, 0.18, 0.12)),
    ("Foot_Boot", "foot", foot_boot, "bottom-centre of the sole; toe +forward", (0.2, 0.2, 0.18)),
    ("Foot_Sneaker", "foot", foot_sneaker, "bottom-centre of the sole; toe +forward", (0.9, 0.3, 0.3)),
    ("Acc_Sunglasses", "acc", acc_sunglasses, "head centre (same pivot as the Head)", (1.0, 1.0, 1.0)),
    ("Acc_Earpiece", "acc", acc_earpiece, "head centre (same pivot as the Head); on the +X (Blender) ear", (1.0, 1.0, 1.0)),
]
REGISTRY += globals().get("EXTRA_REGISTRY", [])


def finish(P, scene):
    bm = P.bm
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    me = bpy.data.meshes.new(P.name)
    bm.to_mesh(me)
    bm.free()
    pal = [0] * len(me.polygons)
    me.attributes["pal"].data.foreach_get("value", pal)
    me.attributes.remove(me.attributes["pal"])
    attr = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    flat = [0.0] * (len(me.loops) * 4)
    for poly in me.polygons:
        c = P.cols[pal[poly.index]]
        for li in range(poly.loop_start, poly.loop_start + poly.loop_total):
            flat[li * 4:li * 4 + 4] = c
    attr.data.foreach_set("color_srgb", flat)
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = 0
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    me.update()
    obj = bpy.data.objects.new(P.name, me)
    scene.collection.objects.link(obj)
    return obj


def unity_size(obj):
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    return [round(max(xs) - min(xs), 3), round(max(zs) - min(zs), 3), round(max(ys) - min(ys), 3)]


def export(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, obj.name + ".fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", colors_type="SRGB",
                             object_types={"MESH"}, use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False)


def preview_material():
    mat = bpy.data.materials.get("GoobPreview") or bpy.data.materials.new("GoobPreview")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    col = nt.nodes.new("ShaderNodeAttribute")
    col.attribute_type = "GEOMETRY"
    col.attribute_name = "Col"
    tint = nt.nodes.new("ShaderNodeAttribute")
    tint.attribute_type = "OBJECT"
    tint.attribute_name = "tint"
    gam = nt.nodes.new("ShaderNodeGamma")
    gam.inputs[1].default_value = 1 / 2.2
    sub = nt.nodes.new("ShaderNodeVectorMath")
    sub.operation = "SUBTRACT"
    sub.inputs[1].default_value = (1, 1, 1)
    scl = nt.nodes.new("ShaderNodeVectorMath")
    scl.operation = "SCALE"
    add = nt.nodes.new("ShaderNodeVectorMath")
    add.operation = "ADD"
    add.inputs[1].default_value = (1, 1, 1)
    mul = nt.nodes.new("ShaderNodeVectorMath")
    mul.operation = "MULTIPLY"
    L = nt.links
    L.new(col.outputs["Color"], gam.inputs[0])
    L.new(tint.outputs["Vector"], sub.inputs[0])
    L.new(sub.outputs[0], scl.inputs[0])
    L.new(col.outputs["Alpha"], scl.inputs["Scale"])
    L.new(scl.outputs[0], add.inputs[0])
    L.new(gam.outputs[0], mul.inputs[0])
    L.new(add.outputs[0], mul.inputs[1])
    L.new(mul.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    L.new(bsdf.outputs[0], out.inputs[0])
    return mat


def setup_stage(scene):
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    if scene.world is None:
        scene.world = bpy.data.worlds.new("GoobyrCharactersWorld")
    scene.world.use_nodes = True
    bg = next(n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.55, 0.62, 0.75, 1)
    bg.inputs[1].default_value = 0.35
    sun_data = bpy.data.lights.new("GoobSun", "SUN")
    sun_data.energy = 4.5
    sun = bpy.data.objects.new("GoobSun", sun_data)
    sun.rotation_euler = (math.radians(-55), 0, math.radians(-30))
    scene.collection.objects.link(sun)
    cam_data = bpy.data.cameras.new("GoobCam")
    cam = bpy.data.objects.new("GoobCam", cam_data)
    cam.location = (0, -9, 2.2)
    cam.rotation_euler = (math.radians(84), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam


# Where a part sits on an assembled Goob (Blender coords, Goob root at the origin).
SOCKET = {"torso": (0, 0, 0.78), "head": (0, 0, 1.42), "hat": (0, 0, 1.66), "hair": (0, 0, 1.66),
          "facial": (0, 0, 1.42), "acc": (0, 0, 1.42)}


def run():
    prev = bpy.context.window.scene
    scene = bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
    report = []
    try:
        bpy.context.window.scene = scene
        for obj in list(scene.objects):
            data = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if isinstance(data, bpy.types.Mesh) and data.users == 0:
                bpy.data.meshes.remove(data)
        objs = {}
        manifest = []
        for name, kind, builder, pivot, tint in REGISTRY:
            if ONLY and name not in ONLY:
                continue
            P = builder()
            obj = finish(P, scene)
            obj["tint"] = tint
            objs[name] = (obj, kind)
            tris = len(obj.data.polygons)
            size = unity_size(obj)
            if EXPORT:
                export(obj)
            manifest.append({"name": name, "kind": kind, "pivot": pivot, "size": size, "triangles": tris})
            report.append("%s %s tris=%d size=%s" % (name, kind, tris, size))
        if EXPORT and not ONLY:
            with open(os.path.join(OUT, "manifest.json"), "w") as fh:
                json.dump({
                    "units": "metres; size is the mesh bounding box in Unity axes [x, y, z]",
                    "front": "every part faces Blender +Y (Unity forward once the FBX root is turned 180 deg, as for the props)",
                    "colour": "vertex colour 'Col' (bytes = authored values): rgb; alpha = tint mask, 1 = tinted by the game "
                              "(albedo = rgb * lerp(1, tint, alpha)), 0 = fixed detail colour. Skin on Head/Hand_Mitten is tintable.",
                    "sockets": {"torso": "Torso pivot at y 0.78 (mesh y -0.55..+0.40 around it, top stays under the head)",
                                "head": "Head pivot at y 1.42 (no eyeballs: the game keeps its eye spheres at (+-0.1, 0.07, 0.2))",
                                "hat/hair": "origin = head top = Head pivot + (0, 0.24, 0)",
                                "facial/acc": "origin = Head pivot (head centre)",
                                "hand": "origin = palm centre, fingers forward, thumb up, symmetric: same mesh for HandL/HandR",
                                "foot": "origin = bottom-centre of the sole (on the ground), toe forward"},
                    "notes": ["Hair_Bald not made: a bald Goob is simply a Head without hair.",
                              "Hand_Glove is fully fixed colour (alpha 0)."],
                    "parts": manifest}, fh, indent=2)
        if PREVIEW:
            preview(scene, objs)
    finally:
        bpy.context.window.scene = prev
    return report


def preview(scene, objs):
    mat = preview_material()
    setup_stage(scene)
    eyes = preview_eyes(scene, mat)
    # Lineups (part origins on a grid): torsos y=0, heads y=-2, hair/facial/acc on heads y=-4, hats on heads y=-6,
    # hands/feet y=-8. Assembled Goobs at y=+3. Everything faces +Y.
    rows = {"torso": (0.0, 0.78), "head": (10.0, 0.4), "hair": (14.0, 0.64), "facial": (14.0, 0.4), "acc": (14.0, 0.4),
            "hat": (22.0, 0.64), "hand": (32.0, 0.2), "foot": (32.0, 0.0)}
    counters = {}
    heads = [o for o, k in objs.values() if k == "head"]
    for name, (obj, kind) in objs.items():
        if not obj.data.materials:
            obj.data.materials.append(mat)
        row = {"facial": "hair", "acc": "hair", "foot": "hand"}.get(kind, kind)
        i = counters.get(row, 0)
        counters[row] = i + 1
        x0, z = rows[kind]
        y = 0.0
        obj.location = (x0 + i * 0.8, y, z)
        if kind in ("hair", "facial", "acc", "hat") and heads:
            hd = heads[i % len(heads)]
            dup = hd.copy()
            dup.name = "PV_" + name + "_head"
            scene.collection.objects.link(dup)
            dup.location = (x0 + i * 0.8, y, z - (0.24 if kind in ("hair", "hat") else 0.0))
            add_eyes(scene, eyes, dup.location)
        if kind == "head":
            add_eyes(scene, eyes, obj.location)
    get = lambda n: objs.get(n, (None, None))[0]
    looks = [("Torso_Suit", "Head_A", ["Hat_Fedora"], "Hand_Mitten", "Foot_Shoe", (0.2, 0.25, 0.42), (1.0, 0.82, 0.68)),
             ("Torso_Guard", "Head_C", ["Hair_Short", "Hat_Headset", "Acc_Sunglasses"], "Hand_Glove", "Foot_Boot", (0.55, 0.65, 0.85), (0.6, 0.42, 0.3)),
             ("Torso_Military", "Head_B", ["Hat_Helmet"], "Hand_Glove", "Foot_Boot", (0.42, 0.48, 0.28), (0.98, 0.75, 0.6)),
             ("Torso_Casual", "Head_A", ["Hair_Long"], "Hand_Mitten", "Foot_Sneaker", (0.95, 0.5, 0.35), (0.9, 0.68, 0.52)),
             ("Torso_Worker", "Head_C", ["Hat_Cap", "Beard_Full"], "Hand_Mitten", "Foot_Boot", (0.3, 0.45, 0.75), (1.0, 0.8, 0.66)),
             ("Torso_Fancy", "Head_B", ["Hair_Bun", "Hat_Crown"], "Hand_Mitten", "Foot_Shoe", (0.55, 0.22, 0.6), (1.0, 0.84, 0.74)),
             ("Torso_Biker", "Head_A", ["Hair_Mohawk", "Mustache"], "Hand_Glove", "Foot_Boot", (0.9, 0.9, 0.85), (0.75, 0.52, 0.38)),
             ("Torso_Chef", "Head_B", ["Hat_Chef", "Mustache"], "Hand_Mitten", "Foot_Shoe", (1.0, 1.0, 1.0), (1.0, 0.8, 0.68)),
             ("Torso_Doctor", "Head_C", ["Hair_Short"], "Hand_Mitten", "Foot_Sneaker", (0.35, 0.72, 0.72), (0.55, 0.38, 0.28)),
             ("Torso_Casual", "Head_B", ["Hat_Beanie"], "Hand_Mitten", "Foot_Sneaker", (0.35, 0.7, 0.4), (1.0, 0.85, 0.75)),
             ("Torso_Suit", "Head_C", ["Hat_TopHat", "Mustache"], "Hand_Glove", "Foot_Shoe", (0.15, 0.15, 0.17), (1.0, 0.8, 0.7)),
             ("Torso_Worker", "Head_A", ["Hat_Cowboy"], "Hand_Glove", "Foot_Boot", (0.6, 0.4, 0.25), (0.85, 0.6, 0.45)),
             ("Torso_Fancy", "Head_C", ["Hat_Turban", "Beard_Full"], "Hand_Mitten", "Foot_Shoe", (0.25, 0.4, 0.75), (0.7, 0.5, 0.35)),
             ("Torso_Casual", "Head_C", ["Hat_Beret"], "Hand_Mitten", "Foot_Sneaker", (0.85, 0.85, 0.3), (1.0, 0.82, 0.7))]
    hair_tint = [(0.3, 0.2, 0.12), (0.85, 0.65, 0.3), (0.15, 0.12, 0.1), (0.6, 0.25, 0.12)]
    for gi, (tor, hd, tops, hand, foot, body, skin) in enumerate(looks):
        if get(tor) is None or get(hd) is None:
            continue
        gx = -20.0 + gi * 1.2

        def put(name, loc, tint):
            src = get(name)
            if src is None:
                return
            d = src.copy()
            d.name = "GB%02d_%s" % (gi, name)
            d.location = (gx + loc[0], loc[1], loc[2])
            d["tint"] = tint
            scene.collection.objects.link(d)
        put(tor, (0, 0, 0.78), body)
        put(hd, (0, 0, 1.42), skin)
        add_eyes(scene, eyes, V((gx, 0.0, 1.42)))
        for t in tops:
            kind = objs[t][1] if t in objs else "hat"
            tint = hair_tint[gi % 4] if kind in ("hair", "facial") else (body if t not in ("Hat_Crown", "Hat_Headset", "Hat_Chef") else (0.8, 0.15, 0.2) if t == "Hat_Crown" else (1, 1, 1))
            put(t, (0, 0, 1.66 if kind in ("hair", "hat") else 1.42), tint)
        for sx in (-1, 1):
            put(hand, (sx * 0.42, 0.05, 0.8), skin)
            put(foot, (sx * 0.16, 0.03, 0.0), tuple(c * 0.45 for c in body))


def preview_eyes(scene, mat):
    """Preview-only stand-in for the game's procedural eyes (not exported)."""
    P = Part("PV_Eyes")
    for sx in (-1, 1):
        P.add(xf(ell(0.075, 0.05, 0.085, 12, 8), loc=(sx * 0.1, 0.2, 0.07)), F(1.0))
        P.add(xf(ell(0.036, 0.016, 0.041, 10, 6), loc=(sx * 0.1, 0.242, 0.07)), F(0.08, 0.08, 0.1))
    obj = finish(P, scene)
    obj.data.materials.append(mat)
    obj["tint"] = (1, 1, 1)
    obj.hide_render = True
    obj.hide_set(True)
    return obj


def add_eyes(scene, eyes, loc):
    d = eyes.copy()
    d.hide_render = False
    scene.collection.objects.link(d)
    d.hide_set(False)
    d.location = loc


if globals().get("RUN", True):
    RESULT = run()
    print(" | ".join(RESULT))
