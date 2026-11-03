# Author : Baptiste Vu Liem
"""Signed-distance modelling kernel of the cast generator (numpy + OpenVDB, no bpy).

A Part is an ordered stack of CSG operations on primitives placed in world space (metres, Z up,
the character faces -Y, +X is the character's left): union / subtract / intersect, each optionally
smooth (blend radius k), plus global offsets and local displacements. The same stack is evaluated

  * on a dense voxel grid (each operation only inside its own bounding box) whose zero level set
    OpenVDB turns into quads, then
  * on the mesh vertices, to project them onto the exact surface and to read analytic normals
    (no faceting whatever the polygon budget).

Every part is a closed surface; garments are separate parts that overlap like real layered clothes.
"""
import math

import numpy as np

F = np.float32
BIG = F(1.0)


def v3(a):
    return np.asarray(a, dtype=np.float64).reshape(3)


def unit(a):
    a = v3(a)
    return a / max(np.linalg.norm(a), 1e-12)


def _dot(a, b):
    return np.einsum("...i,...i->...", a, b)


def _len(a):
    return np.sqrt(np.maximum(_dot(a, a), 0.0))


def frame(z, x_hint=(1, 0, 0)):
    """Rotation matrix (columns = local x, y, z in world) whose z axis is `z`."""
    z = unit(z)
    x = v3(x_hint)
    x = x - z * (x @ z)
    if np.linalg.norm(x) < 1e-6:
        x = np.cross(z, (0, 1, 0)) if abs(z[1]) < 0.9 else np.cross(z, (1, 0, 0))
    x = unit(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], axis=1)


def rot(axis, deg):
    axis = unit(axis)
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    x, y, z = axis
    return np.array([[c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s],
                     [y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s],
                     [z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)]])


# ----------------------------------------------------------------------------------- operators

def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * (k * 0.25)


def smax(a, b, k):
    if k <= 0:
        return np.maximum(a, b)
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.maximum(a, b) + h * h * (k * 0.25)


# ----------------------------------------------------------------------------------- primitives

class Prim:
    lo = None
    hi = None

    def dist(self, P):
        raise NotImplementedError


class Sphere(Prim):
    def __init__(self, c, r):
        self.c, self.r = v3(c), float(r)
        self.lo, self.hi = self.c - self.r, self.c + self.r

    def dist(self, P):
        return _len(P - self.c.astype(F)) - F(self.r)


class Ellipsoid(Prim):
    """Ellipsoid with radii r along the columns of R (approximate distance, exact on the surface)."""

    def __init__(self, c, r, R=None):
        self.c, self.r = v3(c), v3(r)
        self.R = np.eye(3) if R is None else np.asarray(R, dtype=np.float64)
        ext = np.sqrt(((self.R * self.r[None, :]) ** 2).sum(1))
        self.lo, self.hi = self.c - ext, self.c + ext

    def dist(self, P):
        q = (P - self.c.astype(F)) @ self.R.astype(F)
        k0 = _len(q / self.r.astype(F))
        k1 = _len(q / (self.r * self.r).astype(F))
        return k0 * (k0 - 1.0) / np.maximum(k1, F(1e-9))


class RoundCone(Prim):
    """Two spheres (a, ra) and (b, rb) and the cone tangent to both (a capsule when ra == rb)."""

    def __init__(self, a, b, ra, rb=None):
        self.a, self.b = v3(a), v3(b)
        self.ra = float(ra)
        self.rb = float(ra if rb is None else rb)
        ba = self.b - self.a
        if np.linalg.norm(ba) < 1e-6:
            self.b = self.a + np.array([0, 0, 1e-5])
        if abs(self.ra - self.rb) >= np.linalg.norm(self.b - self.a) * 0.999:
            # one sphere swallows the other: shrink the smaller radius so the cone stays defined
            L = np.linalg.norm(self.b - self.a)
            if self.ra > self.rb:
                self.rb = self.ra - L * 0.98
            else:
                self.ra = self.rb - L * 0.98
        self.lo = np.minimum(self.a - self.ra, self.b - self.rb)
        self.hi = np.maximum(self.a + self.ra, self.b + self.rb)

    def dist(self, P):
        ba = (self.b - self.a)
        l2 = float(ba @ ba)
        rr = self.ra - self.rb
        a2 = l2 - rr * rr
        il2 = 1.0 / l2
        baf = ba.astype(F)
        pa = P - self.a.astype(F)
        y = pa @ baf
        z = y - F(l2)
        x2v = pa * F(l2) - y[..., None] * baf
        x2 = _dot(x2v, x2v)
        y2 = y * y * F(l2)
        z2 = z * z * F(l2)
        k = F(math.copysign(1.0, rr) * rr * rr) * x2
        d_tip = np.sqrt(x2 + z2) * F(il2) - F(self.rb)
        d_base = np.sqrt(x2 + y2) * F(il2) - F(self.ra)
        d_side = (np.sqrt(np.maximum(x2 * F(a2 * il2), 0)) + y * F(rr)) * F(il2) - F(self.ra)
        return np.where(np.sign(z) * F(a2) * z2 > k, d_tip,
                        np.where(np.sign(y) * F(a2) * y2 < k, d_base, d_side))


def Capsule(a, b, r):
    return RoundCone(a, b, r, r)


class CappedCone(Prim):
    """Flat-ended cone (cylinder when ra == rb) from a (radius ra) to b (radius rb), edges rounded by e."""

    def __init__(self, a, b, ra, rb=None, e=0.0):
        a, b = v3(a), v3(b)
        rb = ra if rb is None else rb
        u = unit(b - a)
        self.e = float(e)
        self.a, self.b = a + u * self.e, b - u * self.e
        self.ra, self.rb = float(ra) - self.e, float(rb) - self.e
        m = max(ra, rb)
        self.lo = np.minimum(a, b) - m
        self.hi = np.maximum(a, b) + m

    def dist(self, P):
        a = self.a.astype(F)
        ba = (self.b - self.a)
        baba = float(ba @ ba)
        rba = self.rb - self.ra
        pa = P - a
        papa = _dot(pa, pa)
        paba = (pa @ ba.astype(F)) / F(baba)
        x = np.sqrt(np.maximum(papa - paba * paba * F(baba), 0.0))
        cax = np.maximum(0.0, x - np.where(paba < 0.5, F(self.ra), F(self.rb)))
        cay = np.abs(paba - 0.5) - 0.5
        k = rba * rba + baba
        f = np.clip((F(rba) * (x - F(self.ra)) + paba * F(baba)) / F(k), 0.0, 1.0)
        cbx = x - F(self.ra) - f * F(rba)
        cby = paba - f
        s = np.where((cbx < 0.0) & (cay < 0.0), F(-1.0), F(1.0))
        d = s * np.sqrt(np.minimum(cax * cax + cay * cay * F(baba), cbx * cbx + cby * cby * F(baba)))
        return d - F(self.e)


class Box(Prim):
    """Box of half sizes `half` along the columns of R, edges rounded by e."""

    def __init__(self, c, half, R=None, e=0.0):
        self.c, self.half = v3(c), v3(half)
        self.R = np.eye(3) if R is None else np.asarray(R, dtype=np.float64)
        self.e = float(e)
        ext = np.abs(self.R) @ self.half
        self.lo, self.hi = self.c - ext, self.c + ext

    def dist(self, P):
        q = np.abs((P - self.c.astype(F)) @ self.R.astype(F)) - (self.half - self.e).astype(F)
        return _len(np.maximum(q, 0.0)) + np.minimum(q.max(-1), 0.0) - F(self.e)


class Ring(Prim):
    """Solid of revolution of a rounded rectangle around axis n through c: a band, cuff, belt, brim.
    R = radius of the rectangle's centre, ht = half radial thickness, hh = half height, e = rounding."""

    def __init__(self, c, n, R, ht, hh, e=0.0):
        self.c, self.n = v3(c), unit(n)
        self.R, self.ht, self.hh, self.e = float(R), float(ht), float(hh), float(e)
        m = self.R + self.ht
        self.lo, self.hi = self.c - (m + self.hh), self.c + (m + self.hh)

    def dist(self, P):
        d = P - self.c.astype(F)
        h = d @ self.n.astype(F)
        rad = _len(d - h[..., None] * self.n.astype(F))
        qx = np.abs(rad - F(self.R)) - F(self.ht - self.e)
        qy = np.abs(h) - F(self.hh - self.e)
        out = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2)
        return out + np.minimum(np.maximum(qx, qy), 0.0) - F(self.e)


class Torus(Prim):
    def __init__(self, c, n, R, r):
        self.c, self.n, self.R, self.r = v3(c), unit(n), float(R), float(r)
        self.lo, self.hi = self.c - (self.R + self.r), self.c + (self.R + self.r)

    def dist(self, P):
        d = P - self.c.astype(F)
        h = d @ self.n.astype(F)
        rad = _len(d - h[..., None] * self.n.astype(F))
        return np.sqrt((rad - F(self.R)) ** 2 + h * h) - F(self.r)


class HalfSpace(Prim):
    """Everything on the -n side of the plane through p is inside."""

    def __init__(self, p, n):
        self.p, self.n = v3(p), unit(n)

    def dist(self, P):
        return (P - self.p.astype(F)) @ self.n.astype(F)


class Scaled(Prim):
    """A primitive squashed along the columns of R by factors s (approximate distance)."""

    def __init__(self, prim, c, R, s):
        self.p, self.c, self.R, self.s = prim, v3(c), np.asarray(R, dtype=np.float64), v3(s)
        corners = []
        for x in (prim.lo[0], prim.hi[0]):
            for y in (prim.lo[1], prim.hi[1]):
                for z in (prim.lo[2], prim.hi[2]):
                    q = (np.array([x, y, z]) - self.c) @ self.R
                    corners.append(self.c + self.R @ (q * self.s))
        corners = np.array(corners)
        self.lo, self.hi = corners.min(0), corners.max(0)

    def dist(self, P):
        q = (P - self.c.astype(F)) @ self.R.astype(F)
        q = q / self.s.astype(F)
        Pw = q @ self.R.T.astype(F) + self.c.astype(F)
        return self.p.dist(Pw) * F(self.s.min())


class UniformScaled(Prim):
    """Exact uniform scale of a primitive about `centre`."""

    def __init__(self, prim, centre, s):
        self.p, self.c, self.s = prim, v3(centre), float(s)
        self.lo = self.c + (prim.lo - self.c) * self.s
        self.hi = self.c + (prim.hi - self.c) * self.s

    def dist(self, P):
        c = self.c.astype(F)
        return self.p.dist(c + (P - c) / F(self.s)) * F(self.s)


class Displaced(Prim):
    """prim + fn(P) (fn bounded by amp)."""

    def __init__(self, prim, fn, amp):
        self.p, self.fn, self.amp = prim, fn, float(amp)
        self.lo, self.hi = prim.lo - self.amp, prim.hi + self.amp

    def dist(self, P):
        return self.p.dist(P) + self.fn(P)


class Group(Prim):
    """Smooth union of several primitives, used as one primitive (e.g. a whole torso as an intersector)."""

    def __init__(self, prims, k=0.0):
        self.prims, self.k = list(prims), float(k)
        self.lo = np.min([p.lo for p in self.prims], axis=0)
        self.hi = np.max([p.hi for p in self.prims], axis=0)

    def dist(self, P):
        d = None
        for p in self.prims:
            x = p.dist(P)
            d = x if d is None else smin(d, x, self.k)
        return d


class Intersect(Prim):
    """Smooth intersection of several primitives (e.g. a wedge from half-spaces), bounded by `box`."""

    def __init__(self, prims, k=0.0, box=None):
        self.prims, self.k = list(prims), float(k)
        if box is not None:
            self.lo, self.hi = v3(box[0]), v3(box[1])
        else:
            los = [p.lo for p in self.prims if p.lo is not None]
            his = [p.hi for p in self.prims if p.hi is not None]
            self.lo = np.max(los, axis=0) if los else None
            self.hi = np.min(his, axis=0) if his else None

    def dist(self, P):
        d = None
        for p in self.prims:
            x = p.dist(P)
            d = x if d is None else smax(d, x, self.k)
        return d


class Custom(Prim):
    """Any callable distance with an explicit bounding box."""

    def __init__(self, fn, lo, hi):
        self.fn, self.lo, self.hi = fn, v3(lo), v3(hi)

    def dist(self, P):
        return self.fn(P).astype(F)


def tube(points, radii):
    """Round cones along a polyline (radius per point)."""
    pts = [v3(p) for p in points]
    rs = list(radii) if hasattr(radii, "__len__") else [radii] * len(pts)
    return [RoundCone(pts[i], pts[i + 1], rs[i], rs[i + 1]) for i in range(len(pts) - 1)]


def bezier(p0, p1, p2, p3=None, n=8):
    """Points along a quadratic (3 points) or cubic (4 points) Bezier curve."""
    p0, p1, p2 = v3(p0), v3(p1), v3(p2)
    out = []
    for i in range(n + 1):
        t = i / n
        if p3 is None:
            out.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2)
        else:
            q3 = v3(p3)
            out.append((1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t * t * p2 + t ** 3 * q3)
    return out


def ribbon(points, normals, half_w, half_t, e=None):
    """Flat strip along a polyline: one rounded box per segment (normals = strip face normal per point)."""
    out = []
    pts = [v3(p) for p in points]
    ws = list(half_w) if hasattr(half_w, "__len__") else [half_w] * len(pts)
    ns = [unit(n) for n in normals] if hasattr(normals[0], "__len__") else [unit(normals)] * len(pts)
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        d = b - a
        L = np.linalg.norm(d)
        if L < 1e-6:
            continue
        u = d / L
        n = unit(ns[i] + ns[i + 1])
        n = unit(n - u * (n @ u))
        w = np.cross(n, u)
        R = np.stack([u, w, n], axis=1)
        hw = 0.5 * (ws[i] + ws[i + 1])
        ee = min(half_t, hw) * 0.95 if e is None else e
        out.append(Box((a + b) * 0.5, (L * 0.5 + half_t * 0.8, hw, half_t), R, ee))
    return out


# ----------------------------------------------------------------------------------- parts

class Part:
    """One closed surface = an ordered CSG stack. kind: u(nion) s(ubtract) i(ntersect) o(ffset) d(isplace)."""

    def __init__(self, name, voxel=0.003, material=None):
        self.name = name
        self.voxel = float(voxel)
        self.material = material or name
        self.ops = []
        self.meta = {}

    # -- building
    def add(self, prim, k=0.0):
        if isinstance(prim, (list, tuple)):
            for p in prim:
                self.ops.append(("u", p, float(k)))
        else:
            self.ops.append(("u", prim, float(k)))
        return self

    def sub(self, prim, k=0.0):
        if isinstance(prim, (list, tuple)):
            for p in prim:
                self.ops.append(("s", p, float(k)))
        else:
            self.ops.append(("s", prim, float(k)))
        return self

    def inter(self, prim, k=0.0):
        self.ops.append(("i", prim, float(k)))
        return self

    def offset(self, t):
        self.ops.append(("o", None, float(t)))
        return self

    def displace(self, fn, amp, lo=None, hi=None):
        self.ops.append(("d", fn, float(amp), None if lo is None else v3(lo), None if hi is None else v3(hi)))
        return self

    # -- evaluation
    def _margin(self):
        ks = [op[2] for op in self.ops if op[0] in "usi"] + [0.0]
        offs = sum(max(op[2], 0) for op in self.ops if op[0] == "o")
        return max(ks) + offs + 4 * self.voxel

    def bounds(self):
        lo, hi = None, None
        offs = sum(max(op[2], 0) for op in self.ops if op[0] == "o")
        for op in self.ops:
            if op[0] == "u":
                p, k = op[1], op[2]
                a, b = p.lo - k - offs, p.hi + k + offs
                lo = a if lo is None else np.minimum(lo, a)
                hi = b if hi is None else np.maximum(hi, b)
        pad = 4 * self.voxel
        clip = self.meta.get("clip")
        if clip is not None:
            lo, hi = np.maximum(lo, clip[0]), np.minimum(hi, clip[1])
        return lo - pad, hi + pad

    def _op_boxes(self):
        """Influence boxes of the ops as arrays (lo (n,3), hi (n,3)); global ops get infinite boxes."""
        M = self._margin()
        lo = np.full((len(self.ops), 3), -np.inf)
        hi = np.full((len(self.ops), 3), np.inf)
        for i, op in enumerate(self.ops):
            if op[0] in "us":
                lo[i], hi[i] = op[1].lo - (op[2] + M), op[1].hi + (op[2] + M)
            elif op[0] == "d" and op[3] is not None:
                lo[i], hi[i] = op[3], op[4]
        return lo, hi

    def _eval_block(self, P, active):
        d = np.full(P.shape[0], BIG, F)
        for op, on in zip(self.ops, active):
            if not on:
                continue
            kind = op[0]
            if kind == "u":
                d = smin(d, op[1].dist(P), op[2])
            elif kind == "s":
                d = smax(d, -op[1].dist(P), op[2])
            elif kind == "i":
                d = smax(d, op[1].dist(P), op[2])
            elif kind == "o":
                d = d - F(op[2])
            elif kind == "d":
                d = d + op[1](P).astype(F)
        return d

    def eval_points(self, P, brick=0.03):
        """Exact stack value at points, evaluated brick by brick (each brick only runs the ops whose
        influence box touches it)."""
        P = np.asarray(P, dtype=F)
        n = P.shape[0]
        if n == 0:
            return np.zeros(0, F)
        blo, bhi = self._op_boxes()
        lo = P.min(0)
        cell = np.floor((P - lo) / F(brick)).astype(np.int64)
        dims = cell.max(0) + 1
        key = (cell[:, 0] * dims[1] + cell[:, 1]) * dims[2] + cell[:, 2]
        order = np.argsort(key, kind="stable")
        ks = key[order]
        starts = np.flatnonzero(np.r_[True, ks[1:] != ks[:-1]])
        ends = np.r_[starts[1:], n]
        out = np.empty(n, F)
        for s, e in zip(starts, ends):
            idx = order[s:e]
            Pb = P[idx]
            mn, mx = Pb.min(0), Pb.max(0)
            active = np.all(blo <= mx, axis=1) & np.all(bhi >= mn, axis=1)
            out[idx] = self._eval_block(Pb, active)
        return out

    def eval_grid(self, lo, n, h):
        V = np.full(tuple(int(x) for x in n), BIG, F)
        axes = [(lo[i] + h * np.arange(n[i])).astype(F) for i in range(3)]
        M = self._margin()

        def box_slices(blo, bhi):
            i0 = np.maximum(np.floor((blo - lo) / h).astype(int), 0)
            i1 = np.minimum(np.ceil((bhi - lo) / h).astype(int) + 1, np.array(n))
            if np.any(i1 <= i0):
                return None
            return i0, i1

        def points(i0, i1, xa=None):
            xs = axes[0][i0[0]:i1[0]] if xa is None else xa
            X, Y, Z = np.meshgrid(xs, axes[1][i0[1]:i1[1]], axes[2][i0[2]:i1[2]], indexing="ij")
            return np.stack([X, Y, Z], axis=-1)

        def chunks(i0, i1, max_pts=3_000_000):
            plane = (i1[1] - i0[1]) * (i1[2] - i0[2])
            step = max(1, max_pts // max(plane, 1))
            for x0 in range(i0[0], i1[0], step):
                yield x0, min(x0 + step, i1[0])

        for op in self.ops:
            kind = op[0]
            if kind in "us":
                prim, k = op[1], op[2]
                s = box_slices(prim.lo - (k + M), prim.hi + (k + M))
                if s is None:
                    continue
                i0, i1 = s
                for x0, x1 in chunks(i0, i1):
                    sub = V[x0:x1, i0[1]:i1[1], i0[2]:i1[2]]
                    P = points(np.array([x0, i0[1], i0[2]]), np.array([x1, i1[1], i1[2]]))
                    x = prim.dist(P)
                    sub[...] = smin(sub, x, k) if kind == "u" else smax(sub, -x, k)
            elif kind == "i":
                i0, i1 = np.zeros(3, int), np.array(n)
                for x0, x1 in chunks(i0, i1):
                    sub = V[x0:x1]
                    P = points(np.array([x0, 0, 0]), np.array([x1, n[1], n[2]]))
                    sub[...] = smax(sub, op[1].dist(P), op[2])
            elif kind == "o":
                V -= F(op[2])
            elif kind == "d":
                fn, amp, dlo, dhi = op[1], op[2], op[3], op[4]
                if dlo is None:
                    i0, i1 = np.zeros(3, int), np.array(n)
                else:
                    s = box_slices(dlo, dhi)
                    if s is None:
                        continue
                    i0, i1 = s
                for x0, x1 in chunks(i0, i1):
                    sub = V[x0:x1, i0[1]:i1[1], i0[2]:i1[2]]
                    P = points(np.array([x0, i0[1], i0[2]]), np.array([x1, i1[1], i1[2]]))
                    near = np.abs(sub) < F(amp + 4 * h)
                    if near.any():
                        sub[near] = sub[near] + fn(P[near]).astype(F)
        return V

    def gradient(self, P, eps):
        """Tetrahedral finite differences (4 evaluations)."""
        e = F(eps)
        g = np.zeros_like(P)
        for k in ((1, -1, -1), (-1, -1, 1), (-1, 1, -1), (1, 1, 1)):
            kv = np.array(k, F)
            g += kv[None, :] * self.eval_points(P + kv * e)[:, None]
        return g / (4 * e)

    def eval_narrow(self, lo, n, h, factor=4, reach=1.6):
        """Coarse grid every `factor` voxels, then exact values only for the voxels whose coarse
        sample lies within `reach` fine voxels of the surface; the rest keep the coarse sign (+-band)."""
        nc = (np.array(n) + factor - 1) // factor + 1
        C = self.eval_grid(lo, nc, h * factor)
        band = F(3 * h)
        # fine voxel -> nearest coarse sample
        idx = [np.minimum((np.arange(n[i]) + factor // 2) // factor, nc[i] - 1) for i in range(3)]
        Cf = C[np.ix_(idx[0], idx[1], idx[2])]
        V = np.where(Cf < 0, -band, band).astype(F)
        active = np.abs(Cf) < F(reach * h + factor * h * 0.87)
        del Cf
        ii = np.nonzero(active)
        del active
        total = len(ii[0])
        step = 1_500_000
        for s0 in range(0, total, step):
            a = ii[0][s0:s0 + step]
            b = ii[1][s0:s0 + step]
            c = ii[2][s0:s0 + step]
            P = np.stack([lo[0] + a * h, lo[1] + b * h, lo[2] + c * h], axis=1).astype(F)
            V[a, b, c] = np.clip(self.eval_points(P), -band, band)
        return V

    def mesh(self, adaptivity=0.0, project=0, normals=False):
        """Polygonise: returns (verts (N,3) float64, faces (M,4|3) int, normals (N,3))."""
        import openvdb as vdb
        h = self.voxel
        lo, hi = self.bounds()
        n = np.ceil((hi - lo) / h).astype(int) + 1
        V = self.eval_narrow(lo, n, h) if self.meta.get("narrow", True) else self.eval_grid(lo, n, h)
        band = F(3 * h)
        np.clip(V, -band, band, out=V)
        g = vdb.FloatGrid(float(band))
        g.copyFromArray(V)
        del V
        if adaptivity > 0:
            pts, tris, quads = g.convertToPolygons(0.0, adaptivity)
            faces = [quads] + ([tris] if len(tris) else [])
        else:
            pts, quads = g.convertToQuads(0.0)
            faces = [quads]
        if len(pts) == 0:
            return np.zeros((0, 3)), [], np.zeros((0, 3))
        P = (lo + pts.astype(np.float64) * h).astype(F)
        eps = h * 0.35
        gr = None
        for _ in range(project):
            d = self.eval_points(P)
            gr = self.gradient(P, eps)
            g2 = np.maximum(_dot(gr, gr), F(1e-6))
            step = (d / g2)[:, None] * gr
            sl = _len(step)
            step *= (np.minimum(sl, F(h)) / np.maximum(sl, F(1e-9)))[:, None]
            P = P - step
        if normals:
            gr = self.gradient(P, eps)
            nrm = gr / np.maximum(_len(gr), F(1e-9))[:, None]
        else:
            nrm = None
        # orient faces outward: VDB winds them consistently, test against the outward gradient once
        out = []
        probe = None
        for f in faces:
            f = np.asarray(f, dtype=np.int64)
            if len(f) == 0:
                continue
            if probe is None:
                sel = f[:: max(1, len(f) // 48)]
                a, b, c = P[sel[:, 0]], P[sel[:, 1]], P[sel[:, 2]]
                fn = np.cross(b - a, c - a)
                cen = (a + b + c) / 3.0
                g = self.gradient(cen, eps) if len(cen) else np.zeros_like(cen)
                probe = np.mean(_dot(fn, g) < 0) > 0.5
            if probe:
                f = f[:, ::-1]
            out.append(f)
        return P.astype(np.float64), out, (None if nrm is None else nrm.astype(np.float64))
