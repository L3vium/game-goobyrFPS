# Author : Baptiste Vu Liem
"""Sculpted hair of the cast: a cap following the skull above a hairline, plus style volumes (quiff
locks, bun, bob, ponytail...), with strand grooves carved as a displacement so the hair reads as
clumps from far away. Every style is one SDF Part (material "Hair")."""
import math

import numpy as np

import sdf
from skeleton import HEIGHT

V = lambda *a: np.array(a, dtype=np.float64)
F = np.float32


def _interp(keys, x):
    xs = np.array([k[0] for k in keys], np.float32)
    ys = np.array([k[1] for k in keys], np.float32)
    return np.interp(x, xs, ys).astype(F)


# hairline height by azimuth (deg from the front, 0..180, symmetric)
LINE_MALE = [(0, 1.690), (25, 1.686), (45, 1.680), (58, 1.660), (68, 1.632), (76, 1.612), (86, 1.622),
             (98, 1.636), (112, 1.614), (130, 1.584), (155, 1.560), (180, 1.552)]
LINE_FEMALE = [(0, 1.705), (25, 1.700), (48, 1.676), (60, 1.650), (70, 1.628), (78, 1.600), (86, 1.606),
               (96, 1.630), (108, 1.612), (122, 1.585), (150, 1.552), (180, 1.540)]
LINE_HIGH = [(0, 1.745), (20, 1.742), (40, 1.725), (55, 1.700), (68, 1.660), (77, 1.605), (85, 1.605),
             (95, 1.628), (108, 1.612), (122, 1.585), (150, 1.560), (180, 1.552)]


class HairCap(sdf.Prim):
    """Skull ellipsoid inflated by a thickness field, cut under a hairline (by azimuth)."""

    def __init__(self, f, line, t_top=0.010, t_side=0.004, t_back=0.007, z_top=(1.69, 1.76), soft=0.006):
        cr = f["cranium"]
        self.skull = sdf.Ellipsoid(V(0, 0.012, HEIGHT - cr[2]), cr)
        self.line, self.t_top, self.t_side, self.t_back, self.z_top, self.soft = line, t_top, t_side, t_back, z_top, soft
        m = max(t_top, t_side, t_back) + 0.02
        self.lo, self.hi = self.skull.lo - m, self.skull.hi + m

    def thickness(self, P):
        z = P[..., 2]
        y = P[..., 1]
        a = np.clip((z - self.z_top[0]) / (self.z_top[1] - self.z_top[0]), 0, 1)
        a = a * a * (3 - 2 * a)
        b = np.clip((y - 0.00) / 0.08, 0, 1)
        side = self.t_side + (self.t_back - self.t_side) * b
        return (side + (self.t_top - side) * a).astype(F)

    def dist(self, P):
        az = np.degrees(np.arctan2(np.abs(P[..., 0]), -(P[..., 1] - 0.012)))
        zl = _interp(self.line, az)
        d_line = zl - P[..., 2]
        fade = np.clip((P[..., 2] - zl) / 0.018, 0.25, 1.0)
        d = self.skull.dist(P) - self.thickness(P) * fade
        return sdf.smax(d, d_line, self.soft)


def grooves_x(spacing=0.011, amp=0.0016, warp=0.003):
    """Grooves along lines of constant x (flow front-to-back / bottom-to-top), irregular clump widths."""
    def fn(P):
        x = P[..., 0] + warp * np.sin(P[..., 1] * 31.0 + P[..., 2] * 17.0)
        ph = 2 * np.pi * x / spacing + 0.9 * np.sin(2 * np.pi * x / (2.7 * spacing)) + 0.5 * np.sin(P[..., 1] * 45.0)
        side = np.clip((0.090 - np.abs(P[..., 0])) / 0.025, 0.0, 1.0)
        return (side * amp * 0.5 * (1.0 + np.cos(ph)) ** 1.5 / 1.414).astype(F)
    return fn


def grooves_radial(centre, axis, count=44, amp=0.0015):
    """Grooves converging toward `centre` along `axis` (pulled-back hair, buns, ponytails)."""
    c = np.asarray(centre, np.float32)
    a = np.asarray(sdf.unit(axis), np.float32)
    e1 = np.asarray(sdf.unit(np.cross(a, (1, 0, 0)) if abs(a[0]) < 0.9 else np.cross(a, (0, 1, 0))), np.float32)
    e2 = np.cross(a, e1).astype(np.float32)

    def fn(P):
        q = P - c
        ang = np.arctan2(q @ e2, q @ e1)
        ang = ang + 0.08 * np.sin(q @ a * 40.0)
        return (amp * 0.5 * (1.0 + np.cos(count * ang))).astype(F)
    return fn


def lock(p0, p1, p2, p3, r0, r1, r2, r3, n=10):
    pts = sdf.bezier(p0, p1, p2, p3, n=n)
    rs = []
    for i in range(n + 1):
        t = i / n
        rs.append((1 - t) ** 3 * r0 + 3 * (1 - t) ** 2 * t * r1 + 3 * (1 - t) * t * t * r2 + t ** 3 * r3)
    return sdf.tube(pts, rs)


# ------------------------------------------------------------------------------------- styles

def quiff(f, voxel=0.0012, lean=0.012, height=1.0):
    """Rook: short faded sides, a broad quiff of distinct locks swept up, back and to his left."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_MALE, t_top=0.012, t_side=0.0028, t_back=0.0055))
    h = height
    locks = ((-0.064, -0.018, 0.84, 0.004), (-0.038, -0.004, 0.96, -0.004), (-0.010, 0.004, 1.0, 0.002),
             (0.017, 0.0, 0.98, -0.003), (0.043, -0.008, 0.92, 0.003), (0.064, -0.020, 0.80, 0.0))
    for x0, dz, rr, dy in locks:
        part.add(lock(V(x0, -0.080, 1.690),
                      V(x0 * 1.06 + lean * 0.3, -0.126 + dy, 1.758 + dz * 0.5 * h),
                      V(x0 * 0.98 + lean, -0.062 + dy, 1.792 + (0.020 + dz) * h),
                      V(x0 * 0.82 + lean * 0.6, 0.050, 1.800 + dz * 0.4),
                      0.014 * rr, 0.0245 * rr, 0.0255 * rr, 0.017 * rr), 0.007)
    part.displace(grooves_x(0.0165, 0.0026, 0.004), 0.0026, lo=V(-0.13, -0.17, 1.712), hi=V(0.13, 0.15, 1.88))
    part.meta["flow"] = (1.0, 0.2, 1.0)
    return part


def bun(f, voxel=0.0012, strands=True):
    """Rook F: hair pulled back into a high copper bun, two loose strands at the temples."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_FEMALE, t_top=0.008, t_side=0.006, t_back=0.007, z_top=(1.66, 1.74)))
    c = V(0, 0.070, 1.800)
    part.add(sdf.Ellipsoid(c, (0.046, 0.044, 0.040), sdf.rot((1, 0, 0), -35)), 0.016)
    part.add(sdf.Torus(c + V(0, -0.004, -0.010), sdf.rot((1, 0, 0), -35) @ V(0, 0, 1), 0.032, 0.016), 0.010)
    for sx in ((1, -1) if strands else ()):
        part.add(lock(V(sx * 0.070, -0.072, 1.690), V(sx * 0.090, -0.094, 1.655), V(sx * 0.094, -0.090, 1.615),
                      V(sx * 0.087, -0.084, 1.592), 0.0060, 0.0050, 0.0040, 0.0022, n=8), 0.006)
    part.displace(grooves_radial(c, c - V(0, -0.12, 1.66), 46, 0.0016), 0.0016, lo=V(-0.13, -0.16, 1.55), hi=V(0.13, 0.16, 1.86))
    part.meta["flow"] = (1.0, 1.0, 1.0)
    return part


def buzz(f, voxel=0.0014, line=None):
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, line or LINE_MALE, t_top=0.005, t_side=0.003, t_back=0.004))
    part.displace(lambda P: (0.0004 * np.sin(P[..., 0] * 1500) * np.sin(P[..., 1] * 1430) * np.sin(P[..., 2] * 1470)).astype(F), 0.0004)
    part.meta["flow"] = (1.0, 1.0, 1.0)
    part.meta["rough"] = 0.86
    return part


def side_part(f, voxel=0.0012, volume=1.0):
    """Neat side parting (staff, guests): volume swept from the parting toward the other side."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_MALE, t_top=0.013 * volume, t_side=0.005, t_back=0.008))
    px = 0.035
    for x0, rr in ((px - 0.01, 0.9), (px - 0.035, 1.0), (px - 0.065, 0.95), (px - 0.095, 0.8)):
        part.add(lock(V(px, -0.090, 1.735), V(x0, -0.112, 1.765), V(x0 - 0.02, -0.07, 1.800), V(x0 - 0.025, 0.04, 1.790),
                      0.010 * rr, 0.020 * rr * volume, 0.022 * rr * volume, 0.016 * rr), 0.014)
    part.sub(sdf.tube([V(px, -0.115, 1.755), V(px + 0.002, -0.05, 1.808), V(px + 0.004, 0.03, 1.810)], 0.0022), 0.002)
    part.displace(grooves_x(0.011, 0.0016), 0.0016, lo=V(-0.13, -0.17, 1.66), hi=V(0.13, 0.15, 1.86))
    part.meta["flow"] = (1.0, 0.2, 1.0)
    return part


def slick(f, voxel=0.0012, line=None):
    """Slicked back (VIP): smooth, glossy, grooves front to back."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, line or LINE_HIGH, t_top=0.011, t_side=0.006, t_back=0.009))
    part.add(lock(V(0, -0.10, 1.735), V(0, -0.11, 1.765), V(0, -0.04, 1.800), V(0, 0.06, 1.795), 0.020, 0.026, 0.028, 0.02), 0.03)
    part.displace(grooves_x(0.009, 0.0012), 0.0012, lo=V(-0.13, -0.17, 1.66), hi=V(0.13, 0.15, 1.86))
    part.meta["flow"] = (1.0, 0.2, 1.0)
    return part


def horseshoe(f, voxel=0.0014):
    """Balding (old VIP): hair only on the sides and the back."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_MALE, t_top=0.006, t_side=0.006, t_back=0.008))
    part.inter(sdf.HalfSpace(V(0, 0, 1.690), (0, 0, 1)), 0.01)
    part.sub(sdf.Ellipsoid(V(0, -0.06, 1.69), (0.095, 0.08, 0.05)), 0.02)
    part.displace(grooves_x(0.008, 0.001), 0.001, lo=V(-0.14, -0.16, 1.5), hi=V(0.14, 0.16, 1.8))
    part.meta["flow"] = (1.0, 1.0, 0.2)
    return part


def curly(f, voxel=0.0013, size=1.0):
    """Short curly / afro-textured: a rounder volume covered in bumps."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_MALE, t_top=0.022 * size, t_side=0.010 * size, t_back=0.016 * size, z_top=(1.62, 1.74)))

    def bumps(P):
        x, y, z = P[..., 0], P[..., 1], P[..., 2]
        a = np.sin(x * 390 + np.sin(z * 150) * 2.0) * np.sin(y * 410 + np.sin(x * 170) * 2.0) * np.sin(z * 380 + np.sin(y * 160) * 2.0)
        b = np.sin(x * 900 + 1.3) * np.sin(y * 870 + 0.4) * np.sin(z * 940 + 2.1)
        return (-0.0040 * np.abs(a) - 0.0012 * b).astype(F)
    part.displace(bumps, 0.0052, lo=V(-0.15, -0.17, 1.5), hi=V(0.15, 0.17, 1.9))
    part.meta["flow"] = (1.0, 1.0, 1.0)
    part.meta["rough"] = 0.78
    return part


def bob(f, voxel=0.0013, length=1.530):
    """Chin-length bob framing the face, with a swept fringe (female guest)."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_FEMALE, t_top=0.013, t_side=0.012, t_back=0.012, z_top=(1.6, 1.7)))
    bell = sdf.Scaled(sdf.CappedCone(V(0, 0.022, length), V(0, 0.016, 1.735), 0.128, 0.124, 0.022), V(0, 0.02, 1.63),
                      np.eye(3), (1.0, 1.06, 1.0))
    part.add(sdf.Intersect([bell, sdf.HalfSpace(V(0, -0.068, 0), (0, -1, 0))], 0.012, box=(bell.lo, bell.hi)), 0.03)
    part.add(lock(V(0.05, -0.085, 1.745), V(0.0, -0.140, 1.738), V(-0.05, -0.138, 1.705), V(-0.088, -0.105, 1.660),
                  0.013, 0.019, 0.017, 0.011), 0.02)
    part.sub(sdf.Ellipsoid(V(0, -0.118, 1.595), (0.086, 0.070, 0.088)), 0.012)

    def flip(P):
        r = np.clip((length + 0.035 - P[..., 2]) / 0.035, 0, 1)
        return (-0.004 * r * r).astype(F)
    part.displace(flip, 0.004, lo=V(-0.2, -0.2, length - 0.02), hi=V(0.2, 0.2, length + 0.04))
    part.displace(grooves_radial(V(0, 0.0, 1.86), (0, 0, 1), 70, 0.0013), 0.0013, lo=V(-0.16, -0.17, 1.5), hi=V(0.16, 0.17, 1.9))
    part.meta["flow"] = (1.0, 1.0, 0.25)
    return part


def ponytail(f, voxel=0.0012):
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_FEMALE, t_top=0.008, t_side=0.006, t_back=0.007, z_top=(1.66, 1.74)))
    c = V(0, 0.115, 1.700)
    part.add(sdf.Sphere(c, 0.024), 0.01)
    part.add(lock(c, c + V(0, 0.05, -0.04), c + V(0, 0.05, -0.13), c + V(0, 0.03, -0.20), 0.022, 0.026, 0.020, 0.008), 0.012)
    part.displace(grooves_radial(c, c - V(0, -0.12, 1.66), 44, 0.0015), 0.0015, lo=V(-0.13, -0.16, 1.45), hi=V(0.13, 0.25, 1.86))
    part.meta["flow"] = (1.0, 1.0, 1.0)
    return part


def flattop(f, voxel=0.0013):
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_MALE, t_top=0.008, t_side=0.0025, t_back=0.004))
    part.add(sdf.Intersect([sdf.Ellipsoid(V(0, 0.01, 1.73), (0.112, 0.128, 0.11)), sdf.HalfSpace(V(0, 0, 1.828), (0, 0, 1)),
                            sdf.HalfSpace(V(0, 0, 1.70), (0, 0, -1))], 0.006, box=((-0.12, -0.13, 1.6), (0.12, 0.14, 1.84))), 0.01)
    part.meta["flow"] = (1.0, 1.0, 1.0)
    return part


def long_wavy(f, voxel=0.0013):
    """Shoulder-length waves (VIP F)."""
    part = sdf.Part("Hair", voxel, "Hair")
    part.add(HairCap(f, LINE_FEMALE, t_top=0.016, t_side=0.014, t_back=0.016, z_top=(1.6, 1.7)))
    for sx in (1, -1):
        part.add(lock(V(sx * 0.07, -0.05, 1.72), V(sx * 0.125, -0.02, 1.62), V(sx * 0.115, 0.02, 1.50), V(sx * 0.13, 0.03, 1.43),
                      0.03, 0.035, 0.032, 0.022), 0.03)
        part.add(lock(V(sx * 0.04, 0.06, 1.74), V(sx * 0.11, 0.09, 1.60), V(sx * 0.08, 0.10, 1.48), V(sx * 0.09, 0.08, 1.42),
                      0.035, 0.04, 0.035, 0.024), 0.03)
    part.add(lock(V(0.0, 0.08, 1.74), V(0.0, 0.13, 1.62), V(0.0, 0.12, 1.50), V(0.0, 0.10, 1.42), 0.04, 0.045, 0.04, 0.028), 0.03)
    part.add(lock(V(0.06, -0.09, 1.74), V(0.0, -0.14, 1.75), V(-0.06, -0.13, 1.72), V(-0.10, -0.09, 1.66), 0.014, 0.02, 0.018, 0.012), 0.02)
    part.sub(sdf.Ellipsoid(V(0, -0.115, 1.58), (0.088, 0.07, 0.09)), 0.012)

    def waves(P):
        return (0.0025 * np.sin(P[..., 2] * 90.0 + P[..., 0] * 30.0) + 0.0012 * np.cos(P[..., 0] * 560.0 + P[..., 2] * 40.0)).astype(F)
    part.displace(waves, 0.0035, lo=V(-0.18, -0.17, 1.38), hi=V(0.18, 0.2, 1.9))
    part.meta["flow"] = (1.0, 1.0, 0.25)
    return part


STYLES = dict(quiff=quiff, bun=bun, buzz=buzz, side_part=side_part, slick=slick, horseshoe=horseshoe, curly=curly,
              bob=bob, ponytail=ponytail, flattop=flattop, long_wavy=long_wavy)


# ------------------------------------------------------------------------------------- facial hair

def _jaw_group(f, grow):
    jx, jy, jz = f["jaw"]
    js = f["jaw_square"]
    prims = [sdf.Ellipsoid(V(0, -0.030, 1.560), (jx, jy, jz)),
             sdf.Ellipsoid(V(0, -0.088, 1.505), (0.026 * f["chin"], 0.024, 0.022 * f["chin"]))]
    for sx in (1, -1):
        gon = V(sx * (0.053 + 0.006 * js), -0.008, 1.524)
        prims.append(sdf.Sphere(gon, 0.020 + 0.004 * js))
        prims.append(sdf.Capsule(gon, V(sx * 0.020, -0.084, 1.498), 0.014 + 0.003 * js))
    g = sdf.Group(prims, 0.03)
    return sdf.Displaced(g, lambda P: np.zeros(P.shape[:-1], np.float32) - grow, grow + 0.01)


def mustache(f, head=None, voxel=0.0009, bushy=1.0):
    part = sdf.Part("Beard", voxel, "Hair")
    pts = [V(-0.031, -0.116, 1.537), V(-0.017, -0.1275, 1.5465), V(0, -0.1315, 1.5480),
           V(0.017, -0.1275, 1.5465), V(0.031, -0.116, 1.537)]
    part.add(sdf.tube(pts, [0.0035 * bushy, 0.0062 * bushy, 0.0060 * bushy, 0.0062 * bushy, 0.0035 * bushy]), 0.003)
    part.displace(lambda P: (0.0006 * np.cos(P[..., 0] * 2 * np.pi / 0.0025)).astype(F), 0.0006)
    return [part]


def beard(f, head=None, voxel=0.0012, grow=0.009):
    part = sdf.Part("Beard", voxel, "Hair")
    jg = _jaw_group(f, grow)
    part.add(sdf.Intersect([jg, sdf.HalfSpace(V(0, 0, 1.598), (0, -0.47, 1)), sdf.HalfSpace(V(0, 0.035, 0), (0, 1, 0))], 0.008,
                           box=(jg.lo, jg.hi)))
    part.sub(sdf.Ellipsoid(V(0, -0.122, 1.533), (0.030, 0.030, 0.0125)), 0.006)
    for op in mustache(f)[0].ops:
        if op[0] == "u":
            part.add(op[1], 0.004)
    part.displace(lambda P: (0.0014 * np.sin(P[..., 0] * 700) * np.sin(P[..., 2] * 650 + P[..., 1] * 300)).astype(F), 0.0014)
    return [part]


def goatee(f, head=None, voxel=0.0010):
    part = sdf.Part("Beard", voxel, "Hair")
    part.add(sdf.Ellipsoid(V(0, -0.100, 1.500), (0.020, 0.018, 0.024)))
    for op in mustache(f, bushy=0.8)[0].ops:
        if op[0] == "u":
            part.add(op[1], 0.004)
    part.displace(lambda P: (0.0008 * np.sin(P[..., 0] * 900) * np.sin(P[..., 2] * 800)).astype(F), 0.0008)
    return [part]


BEARDS = dict(mustache=mustache, beard=beard, goatee=goatee)
