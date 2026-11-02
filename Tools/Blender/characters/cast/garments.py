# Author : Baptiste Vu Liem
"""Garments of the cast as SDF Parts built on a BodyShape (each one a separate closed surface that
overlaps the layers under it, like real clothes). Every builder returns a list of Parts; material
names are slot names (the outfit maps them to looks)."""
import math

import numpy as np

import glove
import sdf

V = lambda *a: np.array(a, dtype=np.float64)


# ------------------------------------------------------------------------------------- helpers

def lumps(B, names=None, regions=None):
    out = []
    for n, pr, k, r in B.lumps:
        if (names and n.split("_")[0] in names) or (regions and any(r.startswith(x) for x in regions)):
            out.append((pr, k))
    return out


def shell(part, items, k_extra=0.02, k_min=0.03):
    for pr, k in items:
        part.add(pr, max(k + k_extra, k_min))


def limb_cut(a, b, r=0.10):
    """Subtractor removing a limb from point a outward along a->b (flat cut at a)."""
    a, b = V(*a), V(*b)
    return sdf.CappedCone(a, b, r, r)


def surface_y(part, x, z, y0=-0.30, y1=0.05, n=700):
    """First y (from the front) where the part is solid at (x, z)."""
    ys = np.linspace(y0, y1, n)
    P = np.stack([np.full_like(ys, x), ys, np.full_like(ys, z)], axis=1).astype(np.float32)
    d = part.eval_points(P)
    i = int(np.argmax(d < 0))
    return float(ys[i]) if d[i] < 0 else None


def surface_along(part, origin, direction, t0=-0.05, t1=0.3, n=700):
    """First solid point of the part along origin + direction * t, t from t0 to t1."""
    o, dvec = V(*origin), sdf.unit(direction)
    ts = np.linspace(t0, t1, n)
    P = (o[None, :] + ts[:, None] * dvec[None, :]).astype(np.float32)
    d = part.eval_points(P)
    i = int(np.argmax(d < 0))
    return o + dvec * ts[i]


def slab(z0, z1):
    return [sdf.HalfSpace(V(0, 0, z0), (0, 0, -1)), sdf.HalfSpace(V(0, 0, z1), (0, 0, 1))]


def fold_arcs(part, centre, axis, radius, side, count=3, spacing=0.022, tube=0.0055, arc=0.55, k=0.010, seed=0):
    """Bunched cloth: `count` partial rings round a limb axis, only on the `side` half (inner elbow,
    back of the knee), a little irregular. Smooth-unioned into `part`."""
    rnd = np.random.RandomState(seed)
    axis, side = sdf.unit(axis), sdf.unit(side)
    side = sdf.unit(side - axis * (side @ axis))
    for i in range(count):
        t = (i - (count - 1) * 0.5) * spacing + rnd.uniform(-0.003, 0.003)
        tilt = sdf.unit(axis + side * rnd.uniform(-0.25, 0.25))
        c = V(*centre) + axis * t
        ring = sdf.Torus(c, tilt, radius + rnd.uniform(-0.002, 0.003), tube * rnd.uniform(0.8, 1.15))
        keep = sdf.HalfSpace(c + side * (radius * np.cos(np.pi * arc)), -side)
        part.add(sdf.Intersect([ring, keep], 0.006, box=(ring.lo, ring.hi)), k)


def creases(part, pts_pairs, tube=0.0045, k=0.010):
    """Straight soft ridges (whiskers, armpit pulls) between surface points."""
    for a, b in pts_pairs:
        part.add(sdf.Capsule(a, b, tube), k)


def stitch_line(path, side_offset=0.0035, both=True):
    """Topstitching beside a seam: returned as polylines (list of point arrays) for the bake-only
    stitch geometry (export.py turns them into dashes)."""
    P = np.array([np.asarray(p, dtype=np.float64) for p in path])
    if len(P) < 2:
        return []
    out = []
    t = np.gradient(P, axis=0)
    t /= np.maximum(np.linalg.norm(t, axis=1, keepdims=True), 1e-9)
    # surface normal guess: away from the body's vertical axis
    radial = P - np.stack([np.zeros(len(P)), np.full(len(P), 0.01), P[:, 2]], axis=1)
    radial /= np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), 1e-9)
    side = np.cross(t, radial)
    side /= np.maximum(np.linalg.norm(side, axis=1, keepdims=True), 1e-9)
    for s in ((1, -1) if both else (1,)):
        out.append(P + side * side_offset * s)
    return out


# ------------------------------------------------------------------------------------- tops

def tee(B, name="Tee", material="Tee", hem=0.895, sleeve=0.42, ease=0.006, neck_drop=0.0, voxel=0.003):
    j = B.j
    part = sdf.Part(name, voxel, material)
    shell(part, lumps(B, regions=["torso", "hips", "shoulder"]), 0.02)
    for s in ("l", "r"):
        sh, el = j["shoulder_" + s], j["elbow_" + s]
        r0 = B.limb_radius("upperarm", 0.0)
        part.add(sdf.RoundCone(sh, sh + (el - sh) * 0.75, r0 + 0.004, B.limb_radius("upperarm", 0.6) + 0.006), 0.035)
    part.offset(ease)
    part.inter(sdf.HalfSpace(V(0, 0, hem), (0, 0, -1)), 0.004)
    for s in ("l", "r"):
        sh, el = j["shoulder_" + s], j["elbow_" + s]
        e = sh + (el - sh) * sleeve
        part.sub(limb_cut(e, e + (el - sh) * 3.0, 0.11), 0.0)
    nr = B.p["neck"]
    part.sub(sdf.RoundCone(V(0, 0.012, 1.40 - neck_drop), V(0, 0.0, 1.62), nr + 0.010, nr + 0.016), 0.006)
    part.add(sdf.Ring(V(0, 0.000, 1.420 - neck_drop * 0.5), (0, -0.30, 1), nr + 0.0135, 0.0042, 0.0058, 0.003), 0.002)
    return [part]


def bomber(B, ease=0.017, hem=0.985, open_w=0.046, pockets=True, armband=True, name="Jacket"):
    """MA-1 flight jacket, worn open: puffy nylon body, rib collar/cuffs/waistband, orange lining
    (team colour) on the open front edges, sleeve utility pocket, zip, team armband."""
    j = B.j
    nr = B.p["neck"]
    jac = sdf.Part(name, 0.003, "Jacket")
    shell(jac, lumps(B, regions=["torso", "shoulder", "upperarm", "forearm"]), 0.02, 0.035)
    for s in ("l", "r"):   # roomier sleeves
        sh, el, fr = j["shoulder_" + s], j["elbow_" + s], B.frame(s)
        jac.add(sdf.RoundCone(sh, el, B.limb_radius("upperarm", 0) + 0.005, B.limb_radius("upperarm", 1) + 0.007), 0.03)
        jac.add(sdf.RoundCone(el, fr.p((-0.08, 0, 0)), B.limb_radius("upperarm", 1) + 0.005, 0.035), 0.02)
    jac.offset(ease)
    jac.inter(sdf.HalfSpace(V(0, 0, hem), (0, 0, -1)), 0.016)
    for s in ("l", "r"):
        fr = B.frame(s)
        jac.sub(limb_cut(fr.p((-0.072, 0, 0)), fr.p((0.4, 0, 0)), 0.09), 0.004)
    jac.sub(sdf.RoundCone(V(0, 0.014, 1.40), V(0, 0.0, 1.64), nr + 0.020, nr + 0.028), 0.01)
    wedge = _front_wedge(open_w)
    jac.sub(wedge, 0.0)
    # cloth folds: bunching at the inner elbow, pulls from the armpits, gathers above the waistband
    for si, s in enumerate(("l", "r")):
        sh, el = j["shoulder_" + s], j["elbow_" + s]
        u = sdf.unit(el - sh)
        inner = sdf.unit(np.cross(u, V(0, 1, 0))) * (1 if s == "l" else -1)
        r_el = B.limb_radius("upperarm", 1) + 0.006 + ease
        fold_arcs(jac, el, u, r_el, -inner + V(0, -0.35, 0), count=4, spacing=0.020, tube=0.0060, arc=0.42, seed=3 + si)
        fold_arcs(jac, sh + (el - sh) * 0.55, u, B.limb_radius("upperarm", 0.55) + 0.006 + ease, -inner, count=2,
                  spacing=0.05, tube=0.0045, arc=0.3, seed=7 + si)
        sx = 1 if s == "l" else -1
        pit = V(sx * (B.sk.shoulder_x - 0.045), -0.060, 1.300)
        creases(jac, [(pit + V(0, -0.03, 0.0), pit + V(-sx * 0.060, -0.055, -0.070)),
                      (pit + V(0, -0.02, -0.025), pit + V(-sx * 0.040, -0.050, -0.120))], 0.0045, 0.012)
    for a in np.radians(np.linspace(-150, 150, 13)):
        d = V(np.sin(a), -np.cos(a), 0)
        if abs(np.sin(a)) < 0.25 and np.cos(a) > 0:
            continue                      # the open front
        p0 = surface_along(jac, V(0, 0.01, hem + 0.030) + d * 0.35, -d, 0.0, 0.35)
        creases(jac, [(p0 + V(0, 0, -0.012) - d * 0.002, p0 + V(0, 0, 0.040) - d * 0.004)], 0.0042, 0.010)
    if pockets:
        # MA-1 utility pocket on the left sleeve (outer side), pen slots
        sh, el = j["shoulder_l"], j["elbow_l"]
        u = sdf.unit(el - sh)
        out = sdf.unit(np.cross(V(0, 1, 0), u))
        if out[2] < 0:
            out = -out
        c = sh + u * 0.125 + out * (B.limb_radius("upperarm", 0.4) + ease + 0.002)
        R = np.stack([u, V(0, 1, 0), out], axis=1)
        jac.add(sdf.Box(c, (0.050, 0.032, 0.007), R, 0.005), 0.004)
        for dy in (-0.012, 0.0):
            jac.add(sdf.Capsule(c - u * 0.045 + V(0, dy, 0) + out * 0.006, c - u * 0.020 + V(0, dy, 0) + out * 0.006, 0.0045), 0.002)
    if pockets:
        for sx in (1, -1):
            x, z = sx * 0.118, 1.060
            y = surface_y(jac, x, z)
            if y is not None:
                R = sdf.rot((0, 0, 1), -sx * 22) @ sdf.rot((0, 1, 0), sx * 14)
                jac.add(sdf.Box(V(x, y + 0.0030, z), (0.040, 0.0042, 0.011), R, 0.0035), 0.003)
    seams = _bomber_seams(B, jac)
    for path in seams:
        jac.sub(sdf.tube(path, 0.0016), 0.0012)
    jac.meta["stitches"] = [line for path in seams for line in stitch_line(path, 0.0045)]
    parts = [jac]
    jac.meta["slots"] = ["Lining"]
    wz = lambda z: open_w + 0.045 * (1.46 - z) / 0.5

    def classify(cen, nrm):
        z = cen[:, 2]
        on_cut = (np.abs(np.abs(cen[:, 0]) - wz(z)) < 0.006) & (cen[:, 1] < -0.02) & (np.abs(nrm[:, 0]) > 0.6)
        inside = (cen[:, 2] < hem + 0.02) & (nrm[:, 2] < -0.5)       # the hem's underside shows the lining
        return (on_cut | inside).astype(np.int32)
    jac.meta["classify"] = classify
    # rib knit: collar, cuffs, waistband
    rib = sdf.Part(name + "Rib", 0.0025, "Rib")
    rib.add(sdf.Ring(V(0, 0.006, 1.445), (0, -0.22, 1), nr + 0.030, 0.0085, 0.021, 0.006))
    band = sdf.Group([pr for pr, k in lumps(B, names=["waist", "belly", "pelvis", "back", "chest", "butt"])], 0.06)
    rib.add(sdf.Intersect([sdf.Displaced(band, lambda P: np.zeros(P.shape[:-1], np.float32) - 0.0185, 0.02),
                           *slab(0.932, hem + 0.024)], 0.005, box=(band.lo - 0.05, band.hi + 0.05)))
    for s in ("l", "r"):
        fr = B.frame(s)
        rib.add(sdf.Scaled(sdf.CappedCone(fr.p((-0.084, 0, 0)), fr.p((-0.034, 0, 0)), 0.0475, 0.0455, 0.007),
                           fr.o, fr.R, (1, 1, 0.90)))
    rib.sub(wedge, 0.0)
    rib.displace(_rib_ridges(), 0.0012)
    parts.append(rib)
    # zip teeth on the two open edges, lining facings folded back beside them (team colour)
    zips = sdf.Part(name + "Zip", 0.0012, "Zip")
    facing = sdf.Part(name + "Facing", 0.0012, "Lining")
    for sx in (1, -1):
        zp, fp = [], []
        for z in np.linspace(hem + 0.006, 1.425, 16):
            x = sx * (wz(z) + 0.0022)
            y = surface_y(jac, x, z)
            if y is not None:
                zp.append(V(x, y + 0.0028, z))
            x2 = sx * (wz(z) + 0.0125)
            y2 = surface_y(jac, x2, z)
            if y2 is not None:
                fp.append(V(x2, y2 - 0.0006, z))
        if len(zp) > 2:
            zips.add(sdf.tube(zp, 0.0026), 0.0)
            if sx > 0:
                # the pull: a tab hanging from the slider at the bottom of the left edge
                tip = zp[0]
                zips.add(sdf.Box(tip + V(0.002, -0.004, 0.006), (0.0045, 0.0018, 0.008), sdf.rot((0, 1, 0), -12), 0.0016), 0.001)
                zips.add(sdf.Box(tip + V(0.0035, -0.006, -0.010), (0.0055, 0.0016, 0.012), sdf.rot((0, 1, 0), -8), 0.0014), 0.0015)
        if len(fp) > 2:
            facing.add(sdf.ribbon(fp, [(sx * 0.15, -1, 0)] * len(fp), 0.0095, 0.0016, 0.0012), 0.002)
    parts.append(zips)
    parts.append(facing)
    if armband:
        sh, el = j["shoulder_l"], j["elbow_l"]
        c = sh + (el - sh) * 0.66
        out = sdf.unit(np.cross(V(0, 1, 0), el - sh))
        if out[2] < 0:
            out = -out
        p = surface_along(jac, c + out * 0.15, -out, 0.0, 0.15)
        r = float(np.linalg.norm(p - c)) + 0.0028
        team = sdf.Part("Armband", 0.0018, "Team")
        team.add(sdf.Ring(c, el - sh, r, 0.0040, 0.022, 0.0032))
        parts.append(team)
    return parts


def _bomber_seams(B, jac):
    """Seam paths projected on the jacket: yoke across the back, centre back, raglan-ish shoulder seams."""
    paths = []
    yoke = []
    for x in np.linspace(-0.20, 0.20, 15):
        p = surface_along(jac, V(x, 0.30, 1.330), (0, -1, 0), 0.0, 0.35)
        yoke.append(p + V(0, 0.0012, 0))
    paths.append(yoke)
    cb = []
    for z in np.linspace(1.00, 1.33, 8):
        p = surface_along(jac, V(0.0, 0.30, z), (0, -1, 0), 0.0, 0.35)
        cb.append(p + V(0, 0.0012, 0))
    paths.append(cb)
    j = B.j
    for s, sx in (("l", 1), ("r", -1)):
        sh = j["shoulder_" + s]
        seam = []
        for t in np.linspace(0.0, 1.0, 9):
            # around the arm hole: from the front armpit, over the shoulder, to the back armpit
            a = np.radians(-80 + 160 * t)
            c = sh + V(-sx * 0.012, 0, 0)
            d = V(sx * 0.35, 0.0, 0.0) * 0 + V(0.0, -np.cos(a), np.sin(a))
            p = surface_along(jac, c + d * 0.2, -d, 0.0, 0.2)
            seam.append(p - d * 0.0012)
        paths.append(seam)
    return paths


def _front_wedge(open_w):
    wz0 = open_w + 0.045 * 1.46 / 0.5
    k = 0.045 / 0.5
    return sdf.Intersect([sdf.HalfSpace(V(wz0, 0, 0), (1, 0, k)), sdf.HalfSpace(V(-wz0, 0, 0), (-1, 0, k)),
                          sdf.HalfSpace(V(0, -0.030, 0), (0, 1, 0))], 0.0, box=((-0.2, -0.4, 0.6), (0.2, 0.0, 1.7)))


def _rib_ridges(spacing=0.0065, amp=0.0016):
    def fn(P):
        ang = np.arctan2(P[..., 0], -P[..., 1])
        return (amp * 0.5 * (1 + np.cos(2 * np.pi * (P[..., 0] + P[..., 1] * 0.3) / spacing))).astype(np.float32)
    return fn


def scarf(B, name="Scarf", material="Scarf", ends=True):
    nr = B.p["neck"]
    part = sdf.Part(name, 0.0018, material)
    n1 = sdf.unit((0, -0.30, 1))
    part.add(sdf.Torus(V(0, 0.006, 1.452), n1, nr + 0.031, 0.025))
    n2 = sdf.unit((0.08, -0.18, 1))
    part.add(sdf.Torus(V(0, 0.010, 1.488), n2, nr + 0.024, 0.021), 0.012)
    part.add(sdf.Ellipsoid(V(0.030, -0.100, 1.410), (0.034, 0.024, 0.030), sdf.rot((0, 0, 1), 20)), 0.012)
    if ends:
        pa = sdf.bezier(V(0.034, -0.112, 1.392), V(0.050, -0.138, 1.320), V(0.050, -0.140, 1.250), V(0.056, -0.136, 1.170), n=8)
        part.add(sdf.ribbon(pa, [(0, -1, 0.15)] * len(pa), [0.030, 0.032, 0.034, 0.034, 0.035, 0.035, 0.036, 0.036, 0.036], 0.0075), 0.008)
        pb = sdf.bezier(V(0.018, -0.110, 1.392), V(-0.004, -0.135, 1.330), V(-0.016, -0.138, 1.290), V(-0.022, -0.134, 1.236), n=6)
        part.add(sdf.ribbon(pb, [(0, -1, 0.1)] * len(pb), [0.026, 0.028, 0.030, 0.031, 0.032, 0.032, 0.032], 0.007), 0.008)

    def knit(P):
        # chunky rib knit: columns (cos across) of chevron rows, readable at a few metres
        x, z = P[..., 0], P[..., 2]
        col = np.cos(x * 2 * np.pi / 0.011 + 0.4 * np.sin(z * 90.0))
        row = np.abs(np.sin((z + 0.35 * np.abs(np.mod(x, 0.011) - 0.0055)) * 2 * np.pi / 0.009))
        return (-0.0012 * (0.5 + 0.5 * col) - 0.0006 * row).astype(np.float32)
    part.displace(knit, 0.0018)
    return [part]


# ------------------------------------------------------------------------------------- bottoms

def trousers(B, name="Trousers", material="Trousers", top=0.995, bottom=0.150, ease=0.009, baggy=0.0,
             cargo=True, belt=True, stack=True, crease=False, voxel=0.003, straight=False, folds=True):
    j = B.j
    part = sdf.Part(name, voxel, material)
    shell(part, lumps(B, names=["pelvis", "butt", "waist", "belly"]), 0.02)
    for s in ("l", "r"):
        hp, kn, an = j["hip_" + s], j["knee_" + s], j["ankle_" + s]
        t0, t1 = B.p["thigh"]
        c0, c1 = B.p["calf"]
        part.add(sdf.RoundCone(hp + V(0.004 if s == "l" else -0.004, 0, -0.035), kn, t0 + baggy, t1 + baggy * 1.4), 0.05)
        if straight:
            part.add(sdf.RoundCone(kn, an + V(0, 0, 0.06), t1 + 0.004 + baggy, t1 - 0.004 + baggy), 0.03)
        else:
            part.add(sdf.RoundCone(kn, an + V(0, 0, 0.06), t1 + baggy * 1.3, c1 + 0.014 + baggy), 0.03)
        part.add(sdf.Sphere(kn + V(0, -0.006, 0), t1 + baggy * 1.2), 0.03)
    shell(part, lumps(B, regions=["thigh", "calf"]), 0.02)
    part.offset(ease)
    part.inter(sdf.HalfSpace(V(0, 0, top), (0, 0, 1)), 0.006)
    part.inter(sdf.HalfSpace(V(0, 0, bottom), (0, 0, -1)), 0.004)
    if stack:
        for s in ("l", "r"):
            an = j["ankle_" + s]
            for i, z in enumerate((bottom + 0.040, bottom + 0.095)):
                r = B.p["calf"][1] + 0.024 + baggy - i * 0.002
                part.add(sdf.Torus(V(an[0], an[1] - 0.002, z), (0.05 * (1 if i % 2 else -1), -0.12 + 0.1 * i, 1), r, 0.0075), 0.014)
    if folds:
        for si, s in enumerate(("l", "r")):
            hp, kn = j["hip_" + s], j["knee_" + s]
            u = sdf.unit(kn - hp)
            r_kn = B.p["thigh"][1] + ease + baggy + 0.006
            fold_arcs(part, kn + V(0, 0.004, 0.005), u, r_kn, V(0, 1, 0), count=3, spacing=0.022, tube=0.0050, arc=0.38, seed=11 + si)
            fold_arcs(part, kn + V(0, -0.002, 0.045), u, r_kn + 0.002, V(0, -1, 0), count=2, spacing=0.03, tube=0.0035, arc=0.25, seed=13 + si)
            sx = 1 if s == "l" else -1
            crotch = V(sx * 0.030, -0.075, 0.835)
            creases(part, [(crotch, crotch + V(sx * 0.055, -0.010, 0.060)), (crotch + V(0, 0, -0.030), crotch + V(sx * 0.060, -0.004, 0.020))],
                    0.0040, 0.012)
    cargo_paths = []
    if cargo:
        for s, sx in (("l", 1), ("r", -1)):
            hp, kn = j["hip_" + s], j["knee_" + s]
            c = hp + (kn - hp) * 0.50
            r = B.p["thigh"][0] * 0.80 + baggy + ease
            pc = V(c[0] + sx * r * 0.97, c[1] - 0.010, c[2])
            R = sdf.rot((0, 1, 0), -sx * 4)
            part.add(sdf.Box(pc, (0.014, 0.060, 0.078), R, 0.010), 0.006)                       # bellows pocket
            part.add(sdf.Box(pc + V(sx * 0.006, 0, 0.066), (0.014, 0.064, 0.020), R, 0.007), 0.003)  # flap
            part.add(sdf.CappedCone(pc + V(sx * 0.018, 0, 0.060), pc + V(sx * 0.024, 0, 0.060), 0.0065, 0.006, 0.002), 0.0)
            cargo_paths.append([pc + V(sx * 0.016, y, z) for y, z in ((-0.056, 0.045), (-0.056, -0.072), (0.056, -0.072), (0.056, 0.045))])
    if crease:
        for s in ("l", "r"):
            hp, kn, an = j["hip_" + s], j["knee_" + s], j["ankle_" + s]
            pts = []
            for z in np.linspace(0.84, bottom + 0.03, 9):
                t = (0.90 - z) / (0.90 - an[2])
                x = hp[0] + (an[0] - hp[0]) * np.clip(t, 0, 1)
                y = surface_y(part, x, z)
                if y is not None:
                    pts.append(V(x, y + 0.0028, z))
            if len(pts) > 2:
                part.add(sdf.tube(pts, 0.0026), 0.006)
    part.meta["stitches"] = [np.array(p) for p in cargo_paths]
    parts = [part]
    if belt:
        b = sdf.Part(name + "Belt", 0.0025, "Belt")
        band = sdf.Group([pr for pr, k in lumps(B, names=["waist", "belly", "pelvis"])], 0.06)
        b.add(sdf.Intersect([sdf.Displaced(band, lambda P: np.zeros(P.shape[:-1], np.float32) - (ease + 0.0045), ease + 0.01),
                             *slab(top - 0.040, top - 0.006)], 0.002, box=(band.lo - 0.05, band.hi + 0.05)))
        y = surface_y(b, 0.0, top - 0.023)
        if y is not None:
            buckle = sdf.Part(name + "Buckle", 0.0012, "Buckle")
            buckle.add(sdf.Box(V(0, y - 0.003, top - 0.023), (0.022, 0.004, 0.018), None, 0.003))
            buckle.sub(sdf.Box(V(0, y - 0.008, top - 0.023), (0.015, 0.006, 0.011), None, 0.002), 0.0)
            parts.append(buckle)
        parts.append(b)
    return parts


def boots(B, name="Boots", upper="Boots", sole_mat="Sole", height=0.20, chunk=1.0, laces=True, voxel=0.0022):
    j = B.j
    up = sdf.Part(name, voxel, upper)
    so = sdf.Part(name + "Sole", voxel, sole_mat)
    lace = sdf.Part(name + "Laces", 0.0012, "Laces")
    for s, sx in (("l", 1), ("r", -1)):
        an, ball, toe = j["ankle_" + s], j["ball_" + s], j["toe_" + s]
        heel = V(an[0], an[1] + 0.038, 0.040)
        up.add(sdf.CappedCone(V(an[0], an[1] + 0.006, 0.05), V(an[0], an[1] + 0.004, height), 0.050 * chunk, 0.047 * chunk, 0.012))
        up.add(sdf.Ellipsoid(heel, (0.043 * chunk, 0.045, 0.045)), 0.03)
        up.add(sdf.RoundCone(V(an[0], an[1] + 0.012, 0.055), V(toe[0], toe[1] + 0.060, 0.040), 0.047 * chunk, 0.043 * chunk), 0.03)
        up.add(sdf.Ellipsoid(V(toe[0], toe[1] + 0.030, 0.040), (0.047 * chunk, 0.052, 0.034)), 0.025)
        # padded collar
        up.add(sdf.Torus(V(an[0], an[1] + 0.004, height - 0.008), (0, 0, 1), 0.046 * chunk, 0.0085), 0.006)
        # sole: a slab under the outline, slightly proud
        outline = sdf.Group([sdf.Ellipsoid(heel + V(0, 0.002, 0), (0.048 * chunk, 0.052, 0.2)),
                             sdf.Ellipsoid(V(toe[0], toe[1] + 0.078, 0.0), (0.052 * chunk, 0.105, 0.2))], 0.05)
        so.add(sdf.Intersect([outline, *slab(0.0, 0.034)], 0.004, box=(outline.lo, outline.hi)))
        if laces:
            for i, t in enumerate(np.linspace(0.0, 1.0, 5)):
                p = V(an[0], an[1] - 0.042, 0.075) * (1 - t) + V(an[0], an[1] - 0.030, height - 0.03) * t
                p0 = surface_along(up, p + V(0, -0.06, 0), (0, 1, 0), -0.01, 0.12)
                lace.add(sdf.Capsule(p0 + V(-0.016, -0.002, -0.006), p0 + V(0.016, -0.002, 0.006), 0.0028))
                lace.add(sdf.Capsule(p0 + V(0.016, -0.002, -0.006), p0 + V(-0.016, -0.002, 0.006), 0.0028))
                for ex in (-0.019, 0.019):           # metal eyelets
                    lace.add(sdf.Torus(p0 + V(ex, -0.0005, 0.0), (0, 1, 0), 0.0032, 0.0012))
        # heel pull tab
        up.add(sdf.Box(V(an[0], an[1] + 0.052, height + 0.006), (0.010, 0.004, 0.018), None, 0.003), 0.004)
    up.inter(sdf.HalfSpace(V(0, 0, 0.026), (0, 0, -1)), 0.004)
    # tread grooves on the sole
    so.sub(sdf.Box(V(0, -0.02, 0.0), (0.2, 0.004, 0.006), None, 0.002), 0.0)
    # welt stitching round the top of the sole
    welt = []
    for s, sx in (("l", 1), ("r", -1)):
        an, toe = j["ankle_" + s], j["toe_" + s]
        line = []
        for a in np.linspace(0, 2 * np.pi, 40):
            c = V(an[0], (an[1] + 0.05 + toe[1] + 0.03) * 0.5, 0.036)
            ry = (an[1] + 0.05 - (toe[1] + 0.03)) * 0.5 + 0.044
            d = V(np.sin(a) * 0.3, np.cos(a), 0)
            p = surface_along(up, c + V(np.sin(a) * 0.08 * chunk, np.cos(a) * ry, 0), -sdf.unit(d), 0.0, 0.1)
            line.append(p)
        welt.append(np.array(line))
    up.meta["stitches"] = welt
    parts = [up, so]
    if laces:
        parts.append(lace)
    return parts


def gloves(B, material="Glove", style="glove"):
    return [B.hand(s, style=style, material=material, ease=0.0026, name="Glove_" + s) for s in ("l", "r")]


def bare_hands(B, material="Skin"):
    return [B.hand(s, style="bare", material=material, name="Hand_" + s) for s in ("l", "r")]


# ------------------------------------------------------------------------------------- accessories

def shades_on_head(B, hair_part, name="Shades", up=True, lens_mat="Lens", frame_mat="Frame", z_up=1.702, tilt_up=-24.0):
    """Chunky wayfarer-ish shades. up=True: pushed up onto the hair; up=False: on the eyes."""
    frame = sdf.Part(name, 0.0008, frame_mat)
    lens = sdf.Part(name + "Lens", 0.0008, lens_mat)
    if up:
        z, tilt = z_up, tilt_up
        ys = []
        for sx in (1, -1):
            for dz in (-0.012, 0.0, 0.012):
                p = surface_along(hair_part, V(sx * 0.036, -0.25, z + dz), (0, 1, 0), 0.0, 0.3)
                ys.append(p[1])
        yc = min(ys) - 0.0085
    else:
        z, tilt = 1.600, 4.0
        yc = -0.146
    R = sdf.rot((1, 0, 0), tilt)
    for sx in (1, -1):
        c = V(sx * 0.0375, yc, z)
        Rl = R @ sdf.rot((0, 1, 0), sx * 5) @ sdf.rot((0, 0, 1), -sx * 6)
        frame.add(sdf.Box(c, (0.0285, 0.0034, 0.0225), Rl, 0.0060), 0.002)
        frame.sub(sdf.Box(c, (0.0228, 0.0100, 0.0168), Rl, 0.0045), 0.0)
        lens.add(sdf.Box(c, (0.0236, 0.0013, 0.0176), Rl, 0.0045))
    up_v = R @ V(0, 0, 1)
    frame.add(sdf.Capsule(V(-0.058, yc + 0.0015, z) + up_v * 0.0175, V(0.058, yc + 0.0015, z) + up_v * 0.0175, 0.0034), 0.004)
    frame.add(sdf.Capsule(V(-0.011, yc - 0.0005, z) + up_v * 0.010, V(0.011, yc - 0.0005, z) + up_v * 0.010, 0.0032), 0.003)
    for sx in (1, -1):
        a = V(sx * 0.064, yc + 0.004, z) + up_v * 0.012
        if up:
            pts = [a, V(sx * 0.100, yc + 0.035, z - 0.004), V(sx * 0.119, 0.000, z - 0.016), V(sx * 0.118, 0.050, z - 0.026)]
        else:
            pts = [a, V(sx * 0.100, yc + 0.035, z + 0.006), V(sx * 0.119, -0.020, z + 0.004), V(sx * 0.116, 0.030, z - 0.010)]
        frame.add(sdf.tube(pts, [0.0040, 0.0037, 0.0035, 0.0033]), 0.003)
    return [frame, lens]


def earpiece(B, name="Earpiece"):
    p = sdf.Part(name, 0.0008, "Earpiece")
    c = V(-0.112, 0.004, 1.584)
    p.add(sdf.Sphere(c, 0.0058))
    pts = sdf.bezier(c + V(0.002, 0.004, -0.004), c + V(0.004, 0.025, -0.05), V(-0.03, 0.06, 1.47), V(-0.02, 0.07, 1.42), n=10)
    p.add(sdf.tube(pts, 0.0014))
    return [p]
