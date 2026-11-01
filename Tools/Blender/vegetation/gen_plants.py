# Author : Baptiste Vu Liem
"""Procedural vegetation generators (Blender, Z-up metres, pivot at the trunk base on the ground).

Every generator takes a parameter dict and a LOD index (0 = full, 1 = ~30 %) and returns a MeshBuilder.
Material indices are given by the caller through P["mat"] = {"bark": i, "leaf": j, ...}.
The branching structure is seeded per branch id, so LOD0 and LOD1 share the same skeleton.
"""
import math
import random

from mathutils import Quaternion, Vector, noise

import veg_lib as vl
from veg_lib import UP, MeshBuilder

TAU = math.tau


def rvec(rng):
    while True:
        v = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
        if 0.01 < v.length <= 1:
            return v.normalized()


def brng(seed, bid):
    return random.Random(hash((seed,) + tuple(bid)) & 0xFFFFFFFF)


def sample(path, t):
    """Point, tangent, segment index and fraction at arc-length fraction t of a polyline."""
    segs = [(path[i + 1] - path[i]).length for i in range(len(path) - 1)]
    total = sum(segs)
    target = min(max(t, 0), 1) * total
    acc = 0.0
    for i, sl in enumerate(segs):
        if acc + sl >= target or i == len(segs) - 1:
            k = (target - acc) / max(sl, 1e-6)
            k = min(max(k, 0), 1)
            return path[i].lerp(path[i + 1], k), (path[i + 1] - path[i]).normalized(), i, k
        acc += sl


def subsample(path, radii, step):
    if step <= 1 or len(path) <= 3:
        return path, radii
    idx = list(range(0, len(path) - 1, step)) + [len(path) - 1]
    return [path[i] for i in idx], [radii[i] for i in idx]


def pick(cells, kind, rng):
    c = [x for x in cells if x["kind"] == kind] or cells
    return c[rng.randrange(len(c))]


# ================================================================== broadleaf / shrubs / dead trees
class _Tree:
    def __init__(self, P, lod):
        self.P, self.lod = P, lod
        self.seed = P["seed"]
        self.mb = MeshBuilder()
        self.cards = []  # (pos, axis, roll_normal, size, kind, phase)
        self.H = P["height"]
        self.mat = P["mat"]

    def col(self, level, phase):
        H = self.H

        def fn(t, co):
            w = min(1.0, max(0.0, co.z / H) * 0.35 + level * 0.18 + t * 0.12) if level else min(0.25, co.z / H * 0.25)
            ao = 0.55 + 0.45 * min(1.0, co.z / (H * 0.4) + 0.2) if level == 0 else 0.8
            return (ao, phase, w, 1)
        return fn

    # ------------------------------------------------------------ branches
    def grow(self, li, start, d, length, radius, bid):
        P = self.P
        L = P["levels"][li]
        rng = brng(self.seed, bid)
        seg = L.get("seg", max(length / 6, 0.15))
        nseg = max(2, int(math.ceil(length / seg)))
        path, cur = [start.copy()], d.normalized()
        grav, up, wob = L.get("gravity", 0.0), L.get("up", 0.0), L.get("wobble", 0.12)
        zc = L.get("zclamp")
        for i in range(nseg):
            cur = cur + Vector((0, 0, (up - grav) / nseg * 2.0)) + rvec(rng) * wob / math.sqrt(nseg)
            cur.normalize()
            if zc is not None and abs(cur.z) > zc:
                cur.z = math.copysign(zc, cur.z)
                cur.normalize()
            path.append(path[-1] + cur * (length / nseg))
        tip = L.get("tip", 0.2)
        radii = [max(radius * (1 - (1 - tip) * (i / nseg) ** L.get("taper", 0.9)), P.get("min_r", 0.004)) for i in range(nseg + 1)]
        phase = rng.random()
        # tube (pruned / simplified at LOD1)
        min_r = P.get("tube_min_r", 0.006) * (2.6 if self.lod else 1)
        if L.get("tube", True) and radius >= min_r and not (self.lod and L.get("lod1_skip")):
            sides = L.get("sides", 6)
            pp, rr = path, radii
            if self.lod:
                sides = max(3, int(round(sides * 0.5)))
                pp, rr = subsample(path, radii, 2)
            vl.tube(self.mb, pp, rr, sides, self.mat["bark"], tile=P.get("bark_tile", 1.0), col_fn=self.col(li, phase),
                    seam_rot=rng.uniform(0, TAU))
        # children
        if li + 1 < len(P["levels"]):
            C = P["levels"][li + 1]
            cnt = C["count"]
            if C.get("count_by_len"):
                cnt = max(1, int(round(cnt * length / C["count_by_len"])))
            for k in range(cnt):
                t = C.get("start", 0.2) + (C.get("end", 1.0) - C.get("start", 0.2)) * (k + rng.uniform(0, 0.9)) / cnt
                p, dt, i, f = sample(path, t)
                rh = radii[i] + (radii[min(i + 1, nseg)] - radii[i]) * f
                perp = dt.orthogonal().normalized()
                perp = Quaternion(dt, k * 2.39996 + rng.uniform(-0.4, 0.4)) @ perp
                if C.get("planar"):  # keep children roughly horizontal (umbrella / flat sprays)
                    perp.z *= 0.3
                    perp.normalize()
                ang = math.radians(C["angle"] + rng.uniform(-C.get("angle_var", 10), C.get("angle_var", 10)))
                cd = (dt * math.cos(ang) + perp * math.sin(ang)).normalized()
                shape = C.get("shape", "even")
                s = 1.0
                if shape == "conical":
                    s = 1.0 - 0.6 * t
                elif shape == "tapered":
                    s = 0.5 + 0.5 * math.sin(math.pi * min(1, t * 1.1))
                elif shape == "tip":
                    s = 0.6 + 0.4 * t
                clen = length * C["len"] * s * rng.uniform(0.8, 1.15)
                cr = min(rh * C.get("r_max", 0.75), radius * C.get("r", 0.5))
                self.grow(li + 1, p - dt * rh * 0.3, cd, clen, cr, bid + (k,))
        # leaf cards
        if L.get("leaves"):
            self.leaves(L, path, rng, phase, length)

    def leaves(self, L, path, rng, phase, length):
        n = L["leaves"]
        if L.get("leaves_by_len"):
            n = max(1, int(round(n * length / L["leaves_by_len"])))
        s0 = L.get("leaf_start", 0.35)
        for i in range(n):
            t = s0 + (1 - s0) * (i + rng.uniform(0, 1)) / n
            p, dt, _, _ = sample(path, t)
            out = Vector((p.x, p.y, 0))
            out = out.normalized() if out.length > 1e-3 else rvec(rng)
            axis = (dt * L.get("leaf_follow", 0.6) + rvec(rng) * 0.45 + out * 0.3 + UP * L.get("leaf_up", 0.15)).normalized()
            lz = L.get("leaf_zclamp")
            if lz is not None and abs(axis.z) > lz:
                axis.z = math.copysign(lz, axis.z)
                axis.normalize()
            flat = L.get("leaf_flat", 0.4)
            roll = (rvec(rng) * (1 - flat) + UP * flat).normalized()
            size = rng.uniform(*L["leaf_size"])
            kind = "cluster" if (i >= n - L.get("tip_clusters", 1) and rng.random() < 0.9) else L.get("leaf_kind", "twig")
            self.cards.append((p, axis, roll, size, kind, phase))

    # ------------------------------------------------------------ trunk
    def trunk(self):
        P = self.P
        H = self.H
        rng = random.Random(self.seed)
        tl = H * P.get("trunk_frac", 0.7)
        n = P.get("trunk_segs", 20)
        lean_dir = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized()
        lean = P.get("lean", 0.0)
        wob = P.get("trunk_wobble", 0.02)
        path, ts = [], []
        for i in range(n + 1):
            t = (i / n) ** P.get("trunk_bias", 1.5)
            z = t * tl
            off = lean_dir * lean * t * t * tl
            off += Vector((noise.noise(Vector((t * 2.5, self.seed * 0.37, 0.5))), noise.noise(Vector((self.seed * 0.71, t * 2.5, 3.5))), 0)) * wob * tl * min(1, t * 3)
            path.append(Vector((off.x, off.y, z)))
            ts.append(t)
        r0 = P["trunk_r"]
        tip = P.get("trunk_tip", 0.35)
        flare, flare_h = P.get("flare", 0.6), P.get("flare_h", 0.06) * H
        radii = []
        for t, p in zip(ts, path):
            r = r0 * (1 - (1 - tip) * t ** 0.9)
            r *= 1 + flare * math.exp(-p.z / max(flare_h, 1e-3))
            radii.append(r)
        nb = P.get("buttress", 0)
        fins = [k * TAU / nb + rng.uniform(-0.35, 0.35) for k in range(nb)] if nb else []
        bh, bs = P.get("buttress_h", 0.12) * H, P.get("buttress_s", 1.5)
        gn = P.get("gnarl", 0.06)

        def radial(i, a):
            z = path[i].z
            m = 1.0
            if fins and z < bh:
                f = (1 - z / bh) ** 2.2
                for fa in fins:
                    da = abs((a - fa + math.pi) % TAU - math.pi)
                    m += bs * f * math.exp(-(da / 0.22) ** 2)
            m *= 1 + gn * noise.noise(Vector((math.cos(a) * 1.7, math.sin(a) * 1.7, z * 0.8 + self.seed)))
            return m
        sides = P.get("trunk_sides", 16)
        pp, rr = path, radii
        if self.lod:
            sides = max(6, sides // 2 + (2 if fins else 0))
            pp, rr = subsample(path, radii, 2)
            idx = list(range(0, len(path) - 1, 2)) + [len(path) - 1]
            radial_l = lambda i, a: radial(idx[i], a)  # noqa: E731
        else:
            radial_l = radial
        vl.tube(self.mb, pp, rr, sides, self.mat["bark"], tile=P.get("bark_tile", 1.0), col_fn=self.col(0, 0.0), radial_fn=radial_l,
                close_tip=True)
        # surface roots continuing the buttress fins
        if P.get("roots"):
            for k, fa in enumerate(fins or [k * TAU / P["roots"] for k in range(P["roots"])]):
                rr_ = random.Random(self.seed * 31 + k)
                ln = rr_.uniform(*P.get("root_len", (2, 4)))
                d = Vector((math.cos(fa), math.sin(fa), 0))
                pts, rads = [], []
                m = 5
                for i in range(m + 1):
                    t = i / m
                    q = d * (r0 * (0.6 + bs * 0.8) + ln * t) + Vector((0, 0, max(0.02, bh * 0.15 * (1 - t) ** 2)))
                    q += d.cross(UP) * math.sin(t * 3 + k) * 0.15 * ln * t
                    pts.append(q)
                    rads.append(r0 * 0.28 * (1 - 0.8 * t))
                vl.tube(self.mb, pts if not self.lod else pts[::2] + ([pts[-1]] if m % 2 else []), rads if not self.lod else rads[::2] + ([rads[-1]] if m % 2 else []),
                        8 if not self.lod else 4, self.mat["bark"], tile=P.get("bark_tile", 1.0), col_fn=self.col(0, 0.0))
        return path, radii

    def build(self, cells):
        P = self.P
        if P.get("stems"):
            S = P["stems"]
            rng = random.Random(self.seed + 1)
            for k in range(S["count"]):
                az = k * 2.39996 + rng.uniform(-0.3, 0.3)
                ang = math.radians(rng.uniform(*S["angle"]))
                d = Vector((math.cos(az) * math.sin(ang), math.sin(az) * math.sin(ang), math.cos(ang)))
                base = Vector((math.cos(az), math.sin(az), 0)) * rng.uniform(0, S.get("spread", 0.1))
                self.grow(1, base, d, self.H * rng.uniform(*S["len"]), S["r"] * rng.uniform(0.8, 1.15), (k,))
        else:
            path, radii = self.trunk()
            C = P["levels"][1]
            rng = random.Random(self.seed + 2)
            cnt = C["count"]
            cs, ce = P.get("crown_start", 0.5), C.get("end", 1.0)
            for k in range(cnt):
                t = cs + (ce - cs) * (k + rng.uniform(0, 0.8)) / cnt
                p, dt, i, f = sample(path, t)
                rh = radii[i] + (radii[min(i + 1, len(radii) - 1)] - radii[i]) * f
                az = k * 2.39996 + rng.uniform(-0.3, 0.3)
                ang = math.radians(C["angle"] + rng.uniform(-C.get("angle_var", 10), C.get("angle_var", 10)))
                d = Vector((math.cos(az) * math.sin(ang), math.sin(az) * math.sin(ang), math.cos(ang)))
                tt = (t - cs) / max(1e-3, 1 - cs)
                s = {"conical": 1 - 0.6 * tt, "tapered": 0.5 + 0.5 * math.sin(math.pi * min(1, tt * 1.1)), "tip": 0.6 + 0.4 * tt}.get(C.get("shape"), 1.0)
                ln = self.H * C["len"] * s * rng.uniform(0.85, 1.15)
                self.grow(1, p - d * rh * 0.3, d, ln, min(rh * C.get("r_max", 0.7), P["trunk_r"] * C.get("r", 0.5)), (k,))
        self.emit_cards(cells)
        return self.mb

    # ------------------------------------------------------------ foliage
    def emit_cards(self, cells):
        P = self.P
        if not self.cards:
            return
        n = len(self.cards)
        cx = sum((c[0] for c in self.cards), Vector()) / n
        ext = Vector((max(abs(c[0].x - cx.x) for c in self.cards), max(abs(c[0].y - cx.y) for c in self.cards),
                      max(abs(c[0].z - cx.z) for c in self.cards))) + Vector((0.3, 0.3, 0.3))
        rng = random.Random(self.seed + 99)
        keep = P.get("lod1_keep", 0.4)
        grow = P.get("lod1_grow", 1.35)
        k_norm = P.get("normal_k", 0.65)
        snow = self.mat.get("snow")

        def nfn(co, nrm):
            o = Vector(((co.x - cx.x) / ext.x, (co.y - cx.y) / ext.y, (co.z - cx.z) / ext.z))
            o = o.normalized() if o.length > 1e-4 else UP
            f = nrm if nrm.dot(o) >= 0 else -nrm
            return (f * (1 - k_norm) + o * k_norm + UP * 0.2).normalized()

        for (pos, axis, roll, size, kind, phase) in self.cards:
            r = rng.random()
            cell = pick(cells, kind, rng)
            if self.lod:
                if r > keep:
                    continue
                size *= grow
            o = Vector(((pos.x - cx.x) / ext.x, (pos.y - cx.y) / ext.y, (pos.z - cx.z) / ext.z))
            ao = max(0.35, min(1.0, 0.35 + 0.75 * o.length))

            def cfn(t, co, ao=ao, phase=phase):
                return (ao, phase, 1.0, 1)
            segs = 1 if self.lod else P.get("card_segs", 2)
            vl.card(self.mb, pos, axis, roll, size, cell, self.mat["leaf"], segs=segs, droop=P.get("card_droop", 0.25),
                    normal_fn=nfn, col_fn=cfn)
            if snow is not None and roll.dot(UP) > 0.55:
                up_axis = axis
                vl.card(self.mb, pos + roll.normalized() * P.get("snow_off", 0.035), up_axis, roll, size, cell, snow, segs=segs,
                        droop=P.get("card_droop", 0.25), normal_fn=lambda co, n: (UP * 0.7 + n * 0.3).normalized(), col_fn=cfn)


def broadleaf(P, lod, cells):
    return _Tree(P, lod).build(cells)


# ================================================================== conifers
def conifer(P, lod, cells, snow_cells=None):
    mb = MeshBuilder()
    H, seed = P["height"], P["seed"]
    rng = random.Random(seed)
    mat = P["mat"]
    r0 = P["trunk_r"]
    n = 24
    path, radii = [], []
    for i in range(n + 1):
        t = i / n
        off = Vector((noise.noise(Vector((t * 2, seed, 0))), noise.noise(Vector((seed, t * 2, 1))), 0)) * 0.012 * H * t
        path.append(Vector((off.x, off.y, t * H)))
        radii.append(max(r0 * (1 - t) ** 1.05 * (1 + 0.5 * math.exp(-t * H / 0.6)), 0.01))

    def tcol(t, co):
        return (0.6 + 0.4 * min(1, co.z / H + 0.3), 0.0, min(0.3, co.z / H * 0.3), 1)
    pp, rr = (path, radii) if not lod else subsample(path, radii, 2)
    vl.tube(mb, pp, rr, P.get("trunk_sides", 12) if not lod else 6, mat["bark"], tile=P.get("bark_tile", 0.8), col_fn=tcol)
    clear = P.get("clear", 0.08) * H
    z = clear
    k = 0
    branch_cells = [c for c in cells if c["kind"] == "branch"]
    twig_cells = [c for c in cells if c["kind"] == "twig"]
    keep = P.get("lod1_keep", 0.25)
    lrng = random.Random(seed + 5)
    snow = mat.get("snow")
    max_len = P["max_len"]
    while z < H * 0.985:
        t = (z - clear) / (H - clear)
        nb = P.get("per_whorl", 5)
        az0 = rng.uniform(0, TAU)
        for b in range(nb):
            br = random.Random(seed * 1000 + k * 17 + b)
            az = az0 + b * TAU / nb + br.uniform(-0.35, 0.35)
            prof = (1 - t) ** P.get("cone_exp", 0.95)
            ln = max(0.25, max_len * prof * br.uniform(0.8, 1.1) + 0.2 * (1 - t))
            if t < 0.06:
                ln *= 0.85
            elev = math.radians(-P.get("droop_low", 28) * (1 - t) + P.get("rise_top", 40) * t ** 2 + br.uniform(-8, 8))
            hd = Vector((math.cos(az), math.sin(az), 0))
            d = (hd * math.cos(elev) + UP * math.sin(elev)).normalized()
            p0 = path[min(int(z / H * n), n)].copy()
            p0.z = z
            # branch skeleton: droops more at the tip
            m = 4
            pts = [p0]
            cur = d.copy()
            for i in range(m):
                cur = (cur + Vector((0, 0, -P.get("sag", 0.18) * (ln / max_len) * 1.2 / m))).normalized()
                pts.append(pts[-1] + cur * ln / m)
            rad = [max(0.012, r0 * 0.22 * (ln / max_len) ** 0.8) * (1 - 0.85 * i / m) for i in range(m + 1)]
            phase = br.random()
            if not lod or ln > max_len * 0.55:
                half = pts[:3] if not lod else [pts[0], pts[2]]
                hr = rad[:3] if not lod else [rad[0], rad[2]]
                vl.tube(mb, half, hr, 3 if not lod else 3, mat["bark"], tile=0.4, col_fn=lambda t_, co, ph=phase: (0.6, ph, 0.5, 1))
            ao = 0.45 + 0.55 * min(1, ln / max_len * 0.5 + t * 0.5 + 0.15)

            def cfn(t_, co, ao=ao, ph=phase):
                return (ao * (0.8 + 0.2 * t_), ph, min(1, 0.4 + t_ * 0.6), 1)

            def nfn(co, nrm):
                o = Vector((co.x, co.y, 0))
                o = o.normalized() if o.length > 1e-3 else UP
                f = nrm if nrm.z >= 0 else -nrm
                return (f * 0.45 + o * 0.4 + UP * 0.35).normalized()
            cards = [(0.0, 1.05, branch_cells), (br.uniform(0.5, 0.8), 0.8, branch_cells), (-br.uniform(0.5, 0.8), 0.75, branch_cells)]
            if ln > max_len * 0.35:
                cards.append((br.uniform(-1.3, 1.3), 0.5, twig_cells))
            for ci, (roll_a, sc, pool) in enumerate(cards):
                if lod and ci > 0 and lrng.random() > keep:
                    continue
                cell = pool[br.randrange(len(pool))] if pool else cells[0]
                axis = (pts[-1] - pts[0]).normalized()
                side = axis.cross(UP).normalized() if abs(axis.z) < 0.99 else Vector((1, 0, 0))
                nrm0 = side.cross(axis).normalized()
                if nrm0.z < 0:
                    nrm0 = -nrm0
                roll = Quaternion(axis, roll_a) @ nrm0
                start = pts[0] + axis * (ln * (1 - sc) * 0.6 if ci == 3 else 0.05)
                size = ln * sc * (1.25 if lod and ci == 0 else 1.0)
                segs = 2 if not lod else 1
                vl.card(mb, start, axis, roll, size, cell, mat["leaf"], segs=segs, droop=P.get("card_droop", 0.25),
                        normal_fn=nfn, col_fn=cfn, width_mul=P.get("card_width", 1.0))
                if snow is not None and ci < 3 and roll.z > 0.6 and snow_cells:
                    scell = snow_cells[cells.index(cell)]
                    vl.card(mb, start + roll * 0.03 + UP * 0.02, axis, roll, size, scell, snow, segs=segs,
                            droop=P.get("card_droop", 0.25), normal_fn=lambda co, n: (UP * 0.75 + n * 0.25).normalized(), col_fn=cfn,
                            width_mul=P.get("card_width", 1.0) * 1.02)
        z += P.get("whorl", 0.5) * (0.8 + 0.4 * rng.random()) * (1 - 0.35 * t)
        k += 1
    # leader: crossed upright sprays
    for i in range(3 if not lod else 2):
        a = i * math.pi / (3 if not lod else 2)
        roll = Vector((math.cos(a), math.sin(a), 0))
        cell = twig_cells[i % len(twig_cells)] if twig_cells else cells[0]
        top = H * 0.965
        vl.card(mb, Vector((path[-3].x, path[-3].y, top - H * 0.02)), UP, roll, H * 0.035 + 0.35, cell, mat["leaf"], segs=1,
                normal_fn=lambda co, n: (n * 0.4 + UP * 0.6).normalized(), col_fn=lambda t_, co: (1, 0.3, 1, 1))
    return mb


# ================================================================== palms / fronds
def frond(mb, base, d0, length, cell, mat, rng, droop, fold, twist=0.0, rows=9, col=None, width_mul=1.0, curl=0.0):
    """Arching frond strip from base along d0, bending down with `droop` (radians accumulated)."""
    pts, sides, widths = [], [], []
    p = base.copy()
    d = d0.normalized()
    hd = Vector((d.x, d.y, 0))
    hd = hd.normalized() if hd.length > 1e-3 else Vector((1, 0, 0))
    side0 = hd.cross(UP).normalized()
    w = length * cell["aspect"] * width_mul
    for i in range(rows):
        t = i / (rows - 1)
        pts.append(p.copy())
        s = Quaternion(d, twist * t + rng.uniform(-0.05, 0.05)) @ side0
        sides.append(s.normalized())
        widths.append(w)
        # rotate direction downward around the side axis
        d = Quaternion(side0, -droop / (rows - 1) * (0.4 + 1.2 * t) + curl / (rows - 1)) @ d
        p = p + d * (length / (rows - 1))
    def nfn(co, nrm):
        f = nrm if nrm.z >= 0 else -nrm
        o = Vector((co.x, co.y, 0))
        o = o.normalized() if o.length > 1e-3 else UP
        return (f * 0.5 + UP * 0.35 + o * 0.25).normalized()
    vl.strip(mb, pts, sides, widths, cell, mat, fold=fold, normal_fn=nfn, col_fn=col)


def palm(P, lod, cells, shaft_cells=None):
    mb = MeshBuilder()
    H, seed = P["height"], P["seed"]
    rng = random.Random(seed)
    mat = P["mat"]
    n = 28 if not lod else 12
    lean_dir = Vector((math.cos(P.get("lean_az", 0.6)), math.sin(P.get("lean_az", 0.6)), 0))
    bend = P.get("bend", 0.0)  # horizontal offset of the top, fraction of H
    s_curve = P.get("s_curve", 0.0)
    path, radii = [], []
    r0 = P["trunk_r"]
    for i in range(n + 1):
        t = i / n
        off = lean_dir * (bend * H * (t ** 1.6) + s_curve * H * math.sin(t * math.pi) * (1 - t))
        path.append(Vector((off.x, off.y, t * H * (1 - 0.25 * bend * bend))))
        r = r0 * (1 + P.get("base_swell", 0.5) * math.exp(-t * H / 0.7)) * (1 - P.get("taper", 0.25) * t)
        r *= 1 + P.get("bulge", 0.0) * math.exp(-((t - 0.45) / 0.2) ** 2)
        r *= 1 + 0.025 * math.sin(t * H * 9)  # growth rings
        radii.append(r)

    def tcol(t, co):
        return (0.7 + 0.3 * min(1, co.z / H + 0.2), 0, min(0.4, co.z / H * 0.4), 1)
    vl.tube(mb, path, radii, P.get("sides", 14) if not lod else 7, mat["bark"], tile=P.get("bark_tile", 1.2), col_fn=tcol, close_tip=False)
    top = path[-1]
    tdir = (path[-1] - path[-2]).normalized()
    crown = top
    # royal palm: smooth green crownshaft on top of the trunk
    if P.get("shaft"):
        sl = P["shaft"]
        sp = [top + tdir * sl * i / 6 for i in range(7)]
        sr = [radii[-1] * (1.15 + 0.1 * math.sin(i / 6 * math.pi)) * (1 - 0.35 * (i / 6) ** 3) for i in range(7)]
        vl.tube(mb, sp, sr, P.get("sides", 14) if not lod else 7, mat["shaft"], tile=0.8, col_fn=tcol)
        crown = sp[-1]
    nf = P["fronds"] if not lod else max(8, int(P["fronds"] * 0.6))
    for k in range(nf):
        fr = random.Random(seed * 77 + k)
        age = k / max(nf - 1, 1)  # 0 = young upright, 1 = old drooping
        az = k * 2.39996 + fr.uniform(-0.2, 0.2)
        elev = math.radians(P.get("elev_young", 70) * (1 - age) + P.get("elev_old", -15) * age + fr.uniform(-8, 8))
        d = Vector((math.cos(az) * math.cos(elev), math.sin(az) * math.cos(elev), math.sin(elev)))
        ln = P["frond_len"] * fr.uniform(0.85, 1.1) * (0.75 + 0.25 * min(1, age * 2 + 0.3))
        dead = age > 0.85 and P.get("dead", False) and fr.random() < 0.7
        cell = pick(cells, "frond", fr) if not dead else cells[-1]
        if not dead and cell is cells[-1] and len(cells) > 1:
            cell = cells[fr.randrange(len(cells) - 1)]
        base = crown + d * 0.15 - tdir * age * P.get("crown_drop", 0.4)
        droop = P.get("droop", 1.2) * (0.6 + 0.7 * age) * fr.uniform(0.85, 1.15)
        if dead:
            d = Vector((d.x * 0.3, d.y * 0.3, -1)).normalized()
            droop = 0.2
        frond(mb, base, d, ln, cell, mat["leaf"], fr, droop, P.get("fold", -0.5), twist=fr.uniform(-0.4, 0.4),
              rows=P.get("rows", 10) if not lod else 5, col=lambda t, co, ph=fr.random(), a=age: (0.55 + 0.45 * (1 - a * 0.5), ph, 0.5 + 0.5 * t, 1),
              width_mul=P.get("frond_width", 1.0))
    # coconuts / dates cluster under the crown
    if P.get("nuts") and not lod:
        for k in range(P["nuts"]):
            fr = random.Random(seed * 13 + k)
            a = fr.uniform(0, TAU)
            c = crown - tdir * 0.25 + Vector((math.cos(a), math.sin(a), 0)) * radii[-1] * 1.6 + Vector((0, 0, -fr.uniform(0, 0.25)))
            rr = P.get("nut_r", 0.13)
            ring = [c + Vector((0, 0, rr * z)) for z in (-1, -0.5, 0, 0.5, 1)]
            rad = [0.001, rr * 0.85, rr, rr * 0.85, 0.001]
            vl.tube(mb, ring, rad, 6, mat.get("shaft", mat["bark"]), tile=0.5, col_fn=lambda t, co: (0.7, 0, 0.3, 1), close_tip=False)
    return mb


# ================================================================== herbaceous
def rosette(P, lod, cells):
    """Fern / banana / tree fern: optional stems + fronds radiating from the crown(s)."""
    mb = MeshBuilder()
    seed = P["seed"]
    mat = P["mat"]
    stems = P.get("stems", [(Vector((0, 0, 0)), 0.0, 0.0)])  # (base, height, radius)
    for si, (sb, sh, sr) in enumerate(stems):
        rng = random.Random(seed * 7 + si)
        top = sb + Vector((0, 0, sh))
        if sh > 0:
            m = 10 if not lod else 5
            lean = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * P.get("stem_lean", 0.04) * sh
            pts = [sb + lean * (i / m) ** 2 + Vector((0, 0, sh * i / m)) for i in range(m + 1)]
            rad = [sr * (1 + P.get("stem_swell", 0.25) * math.exp(-i / m * 4)) * (1 - P.get("stem_taper", 0.3) * i / m) for i in range(m + 1)]
            vl.tube(mb, pts, rad, P.get("stem_sides", 12) if not lod else 6, mat["bark"], tile=P.get("bark_tile", 0.8),
                    col_fn=lambda t, co: (0.7 + 0.3 * t, 0, 0.3 * t, 1), close_tip=False)
            top = pts[-1]
        nf = P["fronds"] if si == 0 else int(P["fronds"] * P.get("sucker_ratio", 0.6))
        if lod:
            nf = max(4, int(nf * P.get("lod1_keep", 0.55)))
        scale = 1.0 if si == 0 else P.get("sucker_scale", 0.6)
        for k in range(nf):
            fr = random.Random(seed * 101 + si * 1000 + k)
            age = k / max(nf - 1, 1)
            az = k * 2.39996 + fr.uniform(-0.25, 0.25)
            elev = math.radians(P.get("elev_young", 70) * (1 - age) + P.get("elev_old", 15) * age + fr.uniform(-10, 10))
            d = Vector((math.cos(az) * math.cos(elev), math.sin(az) * math.cos(elev), math.sin(elev)))
            ln = P["frond_len"] * scale * fr.uniform(0.75, 1.1) * (1 - 0.3 * (1 - age) * P.get("young_short", 1.0))
            dead = P.get("dead", 0) and age > 1 - P["dead"] and fr.random() < 0.8
            pool = [c for c in cells if c["kind"] in ("frond", "leaf")]
            cell = cells[-1] if dead else pool[fr.randrange(len(pool) - (1 if P.get("last_is_dead") else 0))]
            droop = P.get("droop", 1.0) * (0.5 + age) * fr.uniform(0.8, 1.2)
            if dead:
                d = Vector((d.x * 0.4, d.y * 0.4, -1)).normalized()
                droop = 0.3
            base = top + Vector((math.cos(az), math.sin(az), 0)) * P.get("crown_r", 0.03)
            frond(mb, base, d, ln, cell, mat["leaf"], fr, droop, P.get("fold", 0.2), twist=fr.uniform(-0.5, 0.5),
                  rows=P.get("rows", 7) if not lod else 4, col=lambda t, co, ph=fr.random(): (0.55 + 0.45 * t, ph, t, 1),
                  width_mul=P.get("frond_width", 1.0), curl=P.get("curl", 0.0))
    return mb


def leafy_plant(P, lod, cells):
    """Understory plant: petioles carrying single big leaves (calathea / anthurium)."""
    mb = MeshBuilder()
    seed = P["seed"]
    mat = P["mat"]
    n = P["leaves"] if not lod else max(6, int(P["leaves"] * 0.5))
    for k in range(n):
        fr = random.Random(seed * 41 + k)
        az = k * 2.39996 + fr.uniform(-0.3, 0.3)
        h = fr.uniform(*P["petiole"])
        lean = math.radians(fr.uniform(8, 40))
        d = Vector((math.cos(az) * math.sin(lean), math.sin(az) * math.sin(lean), math.cos(lean)))
        base = Vector((math.cos(az), math.sin(az), 0)) * fr.uniform(0, P.get("spread", 0.1))
        pts = [base]
        cur = d.copy()
        for i in range(3):
            cur = (cur + Vector((math.cos(az), math.sin(az), 0)) * 0.25).normalized()
            pts.append(pts[-1] + cur * h / 3)
        if not lod:
            vl.tube(mb, pts, [0.009, 0.007, 0.006, 0.005], 4, mat["bark"], tile=0.3, col_fn=lambda t, co: (0.6, 0, 0.5 * t, 1))
        else:
            vl.tube(mb, [pts[0], pts[-1]], [0.009, 0.006], 3, mat["bark"], tile=0.3, col_fn=lambda t, co: (0.6, 0, 0.5 * t, 1), close_tip=False)
        cell = cells[fr.randrange(len(cells))]
        leaf_d = (cur * 0.7 + UP * fr.uniform(0.0, 0.6)).normalized()
        roll = (UP + Vector((math.cos(az), math.sin(az), 0)) * 0.3 + rvec(fr) * 0.3).normalized()
        size = fr.uniform(*P["leaf_size"])
        vl.card(mb, pts[-1] - cur * size * 0.05, leaf_d, roll, size, cell, mat["leaf"], segs=2 if not lod else 1,
                droop=0.5, normal_fn=lambda co, nrm: ((nrm if nrm.z >= 0 else -nrm) * 0.5 + UP * 0.5).normalized(),
                col_fn=lambda t, co, ph=fr.random(): (0.6 + 0.4 * t, ph, 0.6 + 0.4 * t, 1), curl=fr.uniform(-0.2, 0.2))
    return mb


def grass(P, lod, cells):
    mb = MeshBuilder()
    seed = P["seed"]
    rng = random.Random(seed)
    n = P["cards"] if not lod else max(4, int(P["cards"] * P.get("lod1_keep", 0.35)))
    R = P["radius"]
    for k in range(n):
        fr = random.Random(seed * 3 + k * (3 if lod else 1))
        r = R * math.sqrt(fr.random())
        a = fr.uniform(0, TAU)
        h0 = fr.uniform(*P["height"])
        base = Vector((math.cos(a) * r, math.sin(a) * r, -h0 * fr.uniform(0.02, P.get("sink", 0.1))))  # staggered, sunk bases
        out = Vector((math.cos(a), math.sin(a), 0))
        tilt = math.radians(fr.uniform(*P.get("tilt", (3, 18)))) * (0.4 + 0.6 * r / R)
        axis = (UP * math.cos(tilt) + out * math.sin(tilt)).normalized()
        face_az = fr.uniform(0, TAU)
        roll = Vector((math.cos(face_az), math.sin(face_az), 0))
        h = h0 * (1.15 if lod else 1.0)
        cell = cells[fr.randrange(len(cells))]
        segs = P.get("segs", 2) if not lod else 1
        vl.card(mb, base, axis, roll, h, cell, P["mat"]["leaf"], segs=segs, droop=P.get("droop", 0.15),
                normal_fn=lambda co, nrm, o=out: (UP * 0.65 + o * 0.25 + (nrm if nrm.dot(o) >= 0 else -nrm) * 0.1).normalized(),
                col_fn=lambda t, co, ph=fr.random(): (0.55 + 0.45 * t, ph, t, 1), width_mul=P.get("width", 1.0) * (1.3 if lod else 1.0))
    return mb
