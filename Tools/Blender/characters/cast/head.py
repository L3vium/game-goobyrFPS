# Author : Baptiste Vu Liem
"""Heads of the cast: a big readable stylised head (0.31 m, eyes at 1.60 m) on human proportions.

Parts (separate meshes, shape keys later): the head (skull, face, ears, nose, lips, a mouth bag
behind a closed slit so the jaw can open), two eyeballs (planar UVs: the iris/pupil texture, swapped
for X eyes on death), upper/lower lids (spherical shells hinged on the eye centre: blink = rotate),
a lash line, chunky brows. Everything is parameterised by a face spec (dict) so the cast varies.
"""
import math

import numpy as np

import sdf
from skeleton import EYE_Z, HEIGHT

V = lambda *a: np.array(a, dtype=np.float64)

FACE = dict(
    eye_r=0.0205, eye_x=0.0362, eye_y=-0.098, iris=(0.33, 0.22, 0.12), converge=2.0,
    lid_up=24.0, lid_low=-21.0, lid_tilt=-6.0,           # lid edge elevations (deg), outer-corner tilt (<0 = upturned)
    cranium=(0.110, 0.128, 0.122), skull_top=1.790, jaw=(0.072, 0.076, 0.064), jaw_square=0.6, chin=1.0, cheek=0.0,
    nose="button", nose_scale=1.0, ears=1.15, lips=1.0, mouth_w=1.0, smile=1.0,
    brow=(1.0, 1.0), brow_angle=0.0, brow_y=0.0,        # (thickness, length) scale, inner raise (m)
    fat=0.0, age=0.0, female=False, lashes=False,
    skin=(0.80, 0.58, 0.45), blush=0.35, brow_colour=(0.18, 0.10, 0.06),
)


def spec(**kw):
    s = dict(FACE)
    s.update(kw)
    return s


def eye_centres(f):
    return [V(sx * f["eye_x"], f["eye_y"], EYE_Z) for sx in (1, -1)]


# ------------------------------------------------------------------------------------- head part

def _ear_frame(sx):
    R = sdf.rot((0, 0, 1), sx * 28) @ sdf.rot((1, 0, 0), -16)
    return R[:, 0] * sx, R[:, 1], R[:, 2]          # outward, backward, up (in the ear plane)


def _ears(part, f, root_x):
    """Simple readable ears: a pinna with a rolled rim and a tragus; the bowl is carved later."""
    e = f["ears"]
    for sx in (1, -1):
        out, back, up = _ear_frame(sx)
        R = np.stack([out, back, up], axis=1)
        c0 = V(sx * root_x, 0.018, 1.588) + out * 0.006 * e
        part.add(sdf.Ellipsoid(c0 + up * 0.002, (0.0078 * e, 0.0225 * e, 0.0300 * e), R), 0.010)
        rim = []
        for th in (122, 95, 60, 25, -10, -45, -75):
            t = np.radians(th)
            rim.append(c0 + back * (np.cos(t) * 0.0195 * e) + up * (0.002 + np.sin(t) * 0.0262 * e) + out * 0.0026 * e)
        part.add(sdf.tube(rim, [0.0030 * e, 0.0038 * e, 0.0040 * e, 0.0039 * e, 0.0036 * e, 0.0036 * e, 0.0040 * e]), 0.0025)
        part.add(sdf.Sphere(c0 + back * (-0.0128 * e) + up * (-0.006 * e) + out * 0.0040 * e, 0.0046 * e), 0.003)


def _ear_carve(part, f, root_x):
    e = f["ears"]
    for sx in (1, -1):
        out, back, up = _ear_frame(sx)
        c0 = V(sx * root_x, 0.018, 1.588) + out * 0.006 * e
        part.sub(sdf.Ellipsoid(c0 + out * 0.0070 * e + back * (0.0005 * e) + up * (-0.003 * e), (0.0052 * e, 0.0122 * e, 0.0172 * e),
                               np.stack([out, back, up], axis=1)), 0.0035)


def head_part(f, voxel=0.0012):
    part = sdf.Part("Head", voxel, "Skin")
    cr = f["cranium"]
    part.add(sdf.Ellipsoid(V(0, 0.012, f["skull_top"] - cr[2]), cr))
    jx, jy, jz = f["jaw"]
    part.add(sdf.Ellipsoid(V(0, -0.030, 1.560), (jx, jy, jz)), 0.05)
    # zygomatic arches under the outer eye corners
    for sx in (1, -1):
        if f["female"]:
            part.add(sdf.Ellipsoid(V(sx * 0.064, -0.066, 1.594), (0.025, 0.024, 0.015)), 0.025)
        else:
            part.add(sdf.Ellipsoid(V(sx * 0.068, -0.062, 1.588), (0.028, 0.026, 0.017)), 0.025)
    # mandible: angles + jaw line to a defined chin (smaller, higher and softer on female faces)
    js = f["jaw_square"]
    fem = 1.0 if f["female"] else 0.0
    for sx in (1, -1):
        gon = V(sx * (0.053 + 0.006 * js - 0.004 * fem), -0.008, 1.524 + 0.006 * fem)
        part.add(sdf.Sphere(gon, 0.020 + 0.004 * js - 0.004 * fem), 0.03)
        part.add(sdf.Capsule(gon, V(sx * (0.020 - 0.004 * fem), -0.084, 1.498 + 0.002 * fem), 0.014 + 0.003 * js - 0.002 * fem), 0.025)
    part.add(sdf.Ellipsoid(V(0, -0.088 - 0.002 * fem, 1.505 + 0.002 * fem),
                           (0.026 * f["chin"] - 0.004 * fem, 0.024 - 0.003 * fem, 0.022 * f["chin"] - 0.003 * fem)), 0.02)
    c = f["cheek"]
    if c > 0:
        for sx in (1, -1):
            part.add(sdf.Sphere(V(sx * 0.047, -0.080, 1.562), 0.026 * c), 0.03)
    if f["fat"] > 0:
        fa = f["fat"]
        part.add(sdf.Ellipsoid(V(0, -0.052, 1.494), (0.064 * fa, 0.054 * fa, 0.028 * fa)), 0.035)
        for sx in (1, -1):
            part.add(sdf.Sphere(V(sx * 0.056, -0.060, 1.545), 0.034 * (0.8 + 0.3 * fa)), 0.04)
    # brow ridge (softer on female faces)
    part.add(sdf.Capsule(V(-0.052, -0.106 + 0.003 * f["female"], 1.633), V(0.052, -0.106 + 0.003 * f["female"], 1.633),
                         0.0155 - 0.003 * f["female"]), 0.03)
    # muzzle and lips
    lw = f["mouth_w"]
    part.add(sdf.Ellipsoid(V(0, -0.092, 1.538), (0.034 * lw, 0.028, 0.028)), 0.02)
    L = f["lips"]
    if f["female"]:
        for sx in (1, -1):
            part.add(sdf.Ellipsoid(V(sx * 0.0085, -0.1192, 1.5402), (0.0135 * lw, 0.0074 * L, 0.0062 * L), sdf.rot((0, 1, 0), sx * 8)), 0.005)
        part.add(sdf.Ellipsoid(V(0, -0.1168, 1.5266), (0.0185 * lw, 0.0094 * L, 0.0082 * L)), 0.007)
    else:
        part.add(sdf.Ellipsoid(V(0, -0.1188, 1.5398), (0.0225 * lw, 0.0072 * L, 0.0062 * L)), 0.007)
        part.add(sdf.Ellipsoid(V(0, -0.1162, 1.5270), (0.0190 * lw, 0.0086 * L, 0.0076 * L)), 0.007)
    # nose
    _nose(part, f)
    # upper neck (stays inside the body's neck below the jaw)
    part.add(sdf.RoundCone(V(0, 0.014, 1.432), V(0, 0.018, 1.565), 0.045, 0.051), 0.03)
    # ears rooted on the measured side of the skull
    xs = np.linspace(0.16, 0.0, 600)
    probe = np.stack([xs, np.full_like(xs, 0.018), np.full_like(xs, 1.588)], axis=1).astype(np.float32)
    root_x = float(xs[int(np.argmax(part.eval_points(probe) < 0))])
    _ears(part, f, root_x)
    # --- carving: eye sockets and the almond openings in front of them
    for c in eye_centres(f):
        sx = 1.0 if c[0] > 0 else -1.0
        r_s = f["eye_r"] + 0.0034
        part.sub(sdf.Ellipsoid(c + V(0, 0, 0.0035), (r_s, r_s, r_s - 0.0028)), 0.004)
    _ear_carve(part, f, root_x)
    # mouth: a closed slit (gentle smile) and the bag behind it (the jaw opens it)
    mw = 0.0258 * lw
    sm = 0.0024 * f["smile"]
    pts = [V(-mw, -0.1070, 1.5332 + sm), V(-mw * 0.48, -0.1175, 1.5320 + sm * 0.25), V(0, -0.1207, 1.5318),
           V(mw * 0.48, -0.1175, 1.5320 + sm * 0.25), V(mw, -0.1070, 1.5332 + sm)]
    for dy in (-0.007, -0.0035, 0.0, 0.0035, 0.007):
        part.sub(sdf.tube([p + V(0, dy * (1.0 - abs(p[0]) / (mw * 1.6)), 0) for p in pts], [0.0014, 0.0017, 0.0017, 0.0017, 0.0014]), 0.0006)
    for sx in (1, -1):
        part.sub(sdf.Sphere(V(sx * mw * 1.02, -0.1064, 1.5334 + sm), 0.0026), 0.002)
    part.sub(sdf.Ellipsoid(V(0, -0.094, 1.531), (0.021 * lw, 0.022, 0.010)), 0.002)
    if f["age"] > 0:
        a = f["age"]
        for z in (1.688, 1.700):
            part.sub(sdf.Capsule(V(-0.045, -0.118, z), V(0.045, -0.118, z), 0.0011 * a + 0.0004), 0.001)
        for sx in (1, -1):
            part.sub(sdf.tube([V(sx * 0.024, -0.122, 1.556), V(sx * 0.034, -0.116, 1.538), V(sx * 0.034, -0.110, 1.520)], 0.0012 * a), 0.001)
    part.meta["spec"] = "head"
    part.meta["ear_x"] = root_x
    return part


def _nose(part, f):
    n = f["nose_scale"]
    kind = f["nose"]
    if kind == "button":
        up = 0.0025 if f["female"] else 0.0
        part.add(sdf.RoundCone(V(0, -0.110, 1.608), V(0, -0.128, 1.578 + up), 0.0075 * n, 0.0115 * n), 0.012)
        part.add(sdf.Sphere(V(0, -0.1335 - 0.004 * (n - 1), 1.5705 + up), 0.0150 * n), 0.01)
    elif kind == "long":
        part.add(sdf.RoundCone(V(0, -0.112, 1.620), V(0, -0.137, 1.567), 0.0078 * n, 0.0115 * n), 0.012)
        part.add(sdf.Sphere(V(0, -0.1405, 1.5625), 0.0140 * n), 0.008)
    elif kind == "bulb":
        part.add(sdf.RoundCone(V(0, -0.112, 1.618), V(0, -0.129, 1.575), 0.009 * n, 0.0135 * n), 0.012)
        part.add(sdf.Sphere(V(0, -0.1375, 1.5645), 0.0195 * n), 0.01)
    elif kind == "hook":
        part.add(sdf.tube([V(0, -0.112, 1.622), V(0, -0.132, 1.600), V(0, -0.141, 1.573)], [0.0080 * n, 0.0105 * n, 0.0115 * n]), 0.012)
        part.add(sdf.Sphere(V(0, -0.1365, 1.5635), 0.0135 * n), 0.008)
    w = 0.0142 * n
    for sx in (1, -1):
        part.add(sdf.Sphere(V(sx * w, -0.1215, 1.5635), 0.0098 * n), 0.008)
    for sx in (1, -1):
        part.sub(sdf.Sphere(V(sx * 0.0070 * n, -0.1275 - 0.003 * (n - 1), 1.5555), 0.0030 * n), 0.0015)


# ------------------------------------------------------------------------------------- eyes and lids

def _eye_frame(c):
    """Eye local frame: x = outward (character's side), y = forward (-Y world), z = up."""
    sx = 1.0 if c[0] > 0 else -1.0
    R = sdf.rot((0, 0, 1), sx * 3.0)          # slight divergence
    return R


def _width_clip(c, r, k=0.90):
    """Half-spaces keeping |x - c.x| < k * r (the lid ends stay inside the eye socket)."""
    return [sdf.HalfSpace(c + V(k * r, 0, 0), (1, 0, 0)), sdf.HalfSpace(c - V(k * r, 0, 0), (-1, 0, 0))]


def lid_parts(f, voxel=0.0004):
    """Upper and lower lids + the lash line, as spherical shells clipped by hinge planes."""
    parts = []
    r = f["eye_r"]
    up = math.radians(f["lid_up"])
    lo = math.radians(f["lid_low"])
    for idx, c in enumerate(eye_centres(f)):
        sx = 1.0 if c[0] > 0 else -1.0
        side = "l" if sx > 0 else "r"
        tilt = sdf.rot((0, -1, 0), -sx * f["lid_tilt"])
        # upper lid: points above the plane through the centre whose front edge sits at elevation `up`
        n_up = tilt @ V(0, math.sin(up), math.cos(up))        # tilted backwards by `up`
        shell_o = sdf.Sphere(c, r + 0.0034)
        upper = sdf.Part("LidUp_" + side, voxel, "Skin")
        upper.add(shell_o)
        upper.sub(sdf.Sphere(c, r + 0.0006), 0.0)
        upper.inter(sdf.HalfSpace(c, -n_up), 0.0012)
        upper.inter(sdf.HalfSpace(c + V(0, 0.55 * r, 0), (0, 1, 0)), 0.0)
        for hs in _width_clip(c, r, 0.97):
            upper.inter(hs, 0.0)
        upper.meta.update(eye=idx, hinge="up")
        parts.append(upper)
        # lower lid: points below the plane whose front edge sits at elevation `lo` (negative)
        n_lo = tilt @ V(0, math.sin(lo), math.cos(lo))
        lower = sdf.Part("LidLow_" + side, voxel, "Skin")
        # it reaches down and sideways under the socket rim (r + 0.0034), so no eyeball shows below it
        lower.add(sdf.Sphere(c, r + 0.0029))
        lower.sub(sdf.Sphere(c, r + 0.0006), 0.0)
        lower.inter(sdf.HalfSpace(c, n_lo), 0.0010)
        lower.inter(sdf.HalfSpace(c + V(0, 0, -0.97 * r), (0, 0, -1)), 0.0)
        lower.inter(sdf.HalfSpace(c + V(0, 0.55 * r, 0), (0, 1, 0)), 0.0)
        for hs in _width_clip(c, r, 0.98):
            lower.inter(hs, 0.0)
        lower.meta.update(eye=idx, hinge="low")
        parts.append(lower)
        # lash line: a thin rim along the upper lid edge
        lash = sdf.Part("Lash_" + side, voxel, "Lashes")
        lash.add(sdf.Sphere(c, r + 0.0046 + (0.0010 if f["lashes"] else 0.0)))
        lash.sub(sdf.Sphere(c, r + 0.0003), 0.0)
        lash.inter(sdf.HalfSpace(c - n_up * 0.0007, -n_up), 0.0)
        lash.inter(sdf.HalfSpace(c + n_up * (0.0020 if f["lashes"] else 0.0015), n_up), 0.0)
        lash.inter(sdf.HalfSpace(c + V(0, 0.05 * r, 0), (0, 1, 0)), 0.0)
        if f["lashes"]:
            wing = c + V(sx * r * 0.92, -r * 0.30, r * 0.30)
            lash.add(sdf.tube([wing + V(-sx * 0.004, -0.001, -0.002), wing, wing + V(sx * 0.004, 0.001, 0.0028)], [0.0012, 0.0011, 0.0005]), 0.001)
        for hs in _width_clip(c, r, 0.97 if f["lashes"] else 0.90):
            lash.inter(hs, 0.0)
        lash.meta.update(eye=idx, hinge="up")
        parts.append(lash)
    return parts


def brow_points(f, head, n=10):
    """Brow centre-lines on the forehead (world space), per side: a Bezier curve projected along Y,
    depths smoothed so the brow reads as one confident stroke."""
    th, ln = f["brow"]
    out = []
    ang, lift = f["brow_angle"], f["brow_y"]
    for sx in (1, -1):
        arch = 0.004 if f["female"] else 0.0
        p0 = (0.0110, 1.6275 + ang + lift)
        p1 = (0.0380, 1.6490 + lift + arch)
        p2 = (0.0640 * ln, 1.6300 + lift)
        raw = []
        for i in range(n):
            t = i / (n - 1)
            x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
            z = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
            raw.append((x, z))
        ys = np.linspace(-0.20, 0.0, 800)
        depth = []
        for x, z in raw:
            P = np.stack([np.full_like(ys, sx * x), ys, np.full_like(ys, z)], axis=1).astype(np.float32)
            d = head.eval_points(P)
            depth.append(ys[int(np.argmax(d < 0.0))])
        depth = np.array(depth)
        sm = depth.copy()
        sm[1:-1] = (depth[:-2] + 2 * depth[1:-1] + depth[2:]) / 4.0
        out.append([V(sx * x, y + 0.0004, z) for (x, z), y in zip(raw, sm)])
    return out


def brow_part(f, head, voxel=0.0006):
    """Brows: a confident stroke, thickest a third of the way out, tapering to a fine tip, flat on
    the forehead (no sausage tubes)."""
    th, ln = f["brow"]
    part = sdf.Part("Brows", voxel, "Brows")
    for pts in brow_points(f, head, n=14):
        n = len(pts)
        rs = []
        for i in range(n):
            t = i / (n - 1)
            body = 0.0056 + 0.0018 * np.sin(np.pi * min(t * 1.9, 1.0))
            taper = 1.0 - 0.78 * np.clip((t - 0.45) / 0.55, 0, 1) ** 1.4
            rs.append(body * taper * th)
        c = (pts[0] + pts[-1]) * 0.5
        for seg in sdf.tube(pts, rs):
            part.add(sdf.Scaled(seg, c, np.eye(3), (1.0, 0.56, 1.0)), 0.002)
    part.meta["flow"] = (1.0, 0.25, 0.25)
    return part


# ------------------------------------------------------------------------------------- colours

def skin_colours(P, f):
    """Per-vertex multiplier (RGB) on the skin: blush on cheeks, nose, ears; lips; lid shadow."""
    P = np.asarray(P, dtype=np.float64)
    col = np.ones((len(P), 3))
    blush = np.array([1.0, 0.80, 0.78])

    def bump(c, r, amount, tint):
        d = np.linalg.norm(P - c, axis=1)
        w = np.clip(1.0 - d / r, 0, 1) ** 2 * amount
        return col * (1 - w[:, None]) + col * tint[None, :] * w[:, None]

    b = f["blush"]
    for sx in (1, -1):
        col = bump(V(sx * 0.055, -0.098, 1.560), 0.040, b, blush)
        col = bump(V(sx * 0.108, 0.012, 1.588), 0.035, b * 0.8, blush)
    col = bump(V(0, -0.140, 1.566), 0.025, b * 0.9, blush)
    lips = np.array([0.92, 0.70, 0.68]) if not f["female"] else np.array([0.86, 0.55, 0.55])
    col = bump(V(0, -0.118, 1.533), 0.020, 0.9, lips)
    # freckles: small darker dots over the nose bridge and the cheeks
    fr = f.get("freckles", 0.0)
    if fr > 0:
        cell = np.floor(P / 0.0042).astype(np.int64)
        h = (cell[:, 0] * 73856093 ^ cell[:, 1] * 19349663 ^ cell[:, 2] * 83492791) & 0xFFFF
        rnd = h / 65535.0
        region = np.exp(-((np.abs(P[:, 0]) - 0.035) / 0.030) ** 2 - ((P[:, 2] - 1.580) / 0.022) ** 2) * (P[:, 1] < -0.09)
        dots = (rnd > 0.80) & (region > 0.25)
        col[dots] *= 1.0 - 0.22 * fr * np.array([0.8, 1.0, 1.15])
    # mouth bag interior: dark
    inside = np.linalg.norm((P - V(0, -0.093, 1.531)) / V(0.026, 0.026, 0.0135), axis=1) < 1.0
    col[inside] = np.array([0.28, 0.08, 0.08])
    return np.clip(col, 0, 1)


def skin_masks(P, f):
    """Per-vertex (stubble, oily, 0): stubble over the jaw, chin and upper lip (lips excluded), oily
    T-zone (nose, lips, centre of the forehead) where the skin is glossier."""
    P = np.asarray(P, dtype=np.float64)
    x, y, z = P[:, 0], P[:, 1], P[:, 2]

    def ss(e0, e1, v):
        t = np.clip((v - e0) / (e1 - e0), 0, 1)
        return t * t * (3 - 2 * t)
    st = f.get("stubble", 0.0)
    beard = ss(1.572, 1.552, z) * ss(0.030, -0.010, y) * ss(1.440, 1.470, z)
    lip = np.linalg.norm((P - V(0, -0.118, 1.533)) / V(0.030, 0.020, 0.0115), axis=1)
    beard *= ss(0.85, 1.25, lip)
    side = ss(0.100, 0.080, np.abs(x))
    stubble = np.clip(beard * side * st, 0, 1)

    def bump(c, r):
        d = np.linalg.norm((P - c) / r, axis=1)
        return np.clip(1.0 - d, 0, 1) ** 1.5
    oily = np.clip(bump(V(0, -0.140, 1.570), V(0.020, 0.030, 0.035)) + bump(V(0, -0.118, 1.533), V(0.026, 0.020, 0.013))
                   + 0.6 * bump(V(0, -0.110, 1.672), V(0.030, 0.030, 0.035)), 0, 1)
    return np.stack([stubble, oily, np.zeros_like(oily)], axis=1)
