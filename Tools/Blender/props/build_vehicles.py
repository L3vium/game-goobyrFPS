# Author : Baptiste Vu Liem
"""Procedural vehicles: sedan, pickup, small motorboat, light helicopter (static props), baked PBR.

    blender -b --factory-startup --python Tools/Blender/props/build_vehicles.py -- [Prop_Car_Sedan ...]

Bodies are lofts of superellipse sections through Catmull-Rom interpolated stations; the
greenhouse is tinted glass with body-colour pillars / roof laid over it. Front = -Y (Unity +Z),
pivot bottom centre (ground contact).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pbr_lib as L  # noqa: E402
from pbr_lib import LIB, box, cyl, extrude, lathe, sphere, sweep, boolean, cutter_box, spec, text  # noqa: E402


def S(name, **kw):
    return dict(LIB[name], **kw)


# ------------------------------------------------------------------ loft helpers
def catmull(stations, n):
    """stations: [(y, a, b, c, ...)] -> n rows interpolated (Catmull-Rom on every column)."""
    out = []
    k = len(stations)
    for i in range(n):
        t = i / (n - 1) * (k - 1)
        j = min(int(t), k - 2)
        u = t - j
        p0 = stations[max(j - 1, 0)]
        p1, p2 = stations[j], stations[j + 1]
        p3 = stations[min(j + 2, k - 1)]
        row = []
        for c in range(len(p1)):
            a, b, cc, d = p0[c], p1[c], p2[c], p3[c]
            row.append(0.5 * ((2 * b) + (-a + cc) * u + (2 * a - 5 * b + 4 * cc - d) * u * u +
                              (-a + 3 * b - 3 * cc + d) * u ** 3))
        out.append(row)
    return out


def superellipse(hw, zb, zt, p=3.5, n=32, taper=0.0):
    """Closed section (x, z): half width hw, from zb to zt; taper narrows the top (tumblehome)."""
    zc, hh = (zb + zt) / 2, (zt - zb) / 2
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n - math.pi / 2
        c, s = math.cos(t), math.sin(t)
        x = hw * math.copysign(abs(c) ** (2 / p), c)
        z = zc + hh * math.copysign(abs(s) ** (2 / p), s)
        x *= 1.0 - taper * max(0.0, (z - zc) / max(hh, 1e-4))
        pts.append((x, z))
    return pts


def loft(sections, ys, mat, name="loft", caps=True):
    bm = bmesh.new()
    rings = [[bm.verts.new((x, y, z)) for x, z in sec] for sec, y in zip(sections, ys)]
    n = len(sections[0])
    for a, b in zip(rings[:-1], rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if caps:
        for ring in (rings[0], rings[-1]):
            c = Vector((0, 0, 0))
            for v in ring:
                c += v.co
            cv = bm.verts.new(c / len(ring))
            for i in range(n):
                bm.faces.new((ring[i], ring[(i + 1) % n], cv))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return L._mesh_obj(name, bm, mat)


def body_loft(stations, n_rows=28, n_per=32, p=3.5, mat="paint", taper_col=None):
    """stations: [(y, hw, zb, zt[, taper])]"""
    rows = catmull(stations, n_rows)
    secs = [superellipse(r[1], r[2], r[3], p, n_per, r[4] if len(r) > 4 else 0.0) for r in rows]
    return loft(secs, [r[0] for r in rows], mat)


def greenhouse(stations, n_rows=20, mat="glass"):
    """stations: [(y, hb, ht, zb, zt)] -> trapezoid lofts (bottom half width hb, top ht)."""
    rows = catmull(stations, n_rows)
    secs = []
    for y, hb, ht, zb, zt in rows:
        zt = max(zt, zb + 0.01)
        r = min(0.06, (zt - zb) * 0.4)
        sec = [(-hb, zb), (hb, zb), (ht + (hb - ht) * 0.15, zb + (zt - zb) * 0.85), (ht - r * 0.3, zt),
               (-(ht - r * 0.3), zt), (-(ht + (hb - ht) * 0.15), zb + (zt - zb) * 0.85)]
        secs.append(sec)
    return loft(secs, [r[0] for r in rows], mat), rows


# ------------------------------------------------------------------ wheels
def wheel(x, y, r, w, side, P, rim_mat="rim", tyre_mat="tyre"):
    """Tyre + 5-spoke alloy + brake disc, axis along X, outer face towards `side` (+1/-1)."""
    rot = (0, 90 * side, 0)
    prof = [(r * 0.72, -w / 2), (r * 0.9, -w / 2 - 0.01), (r * 0.98, -w / 2 + 0.005), (r, -w / 2 + 0.03),
            (r, w / 2 - 0.03), (r * 0.98, w / 2 - 0.005), (r * 0.9, w / 2 + 0.01), (r * 0.72, w / 2),
            (r * 0.7, w / 2 - 0.02), (r * 0.7, -w / 2 + 0.02), (r * 0.72, -w / 2)]
    tyre = lathe(prof, 40, (x, y, r), rot, mat=tyre_mat)
    P.append(tyre)
    rr = r * 0.7
    P.append(lathe([(rr, -w / 2 + 0.02), (rr + 0.004, w / 2 - 0.03), (rr * 0.92, w / 2 - 0.02),
                    (rr * 0.92, -w / 2 + 0.03)], 40, (x, y, r), rot, mat=rim_mat))
    # spokes: star extrusion on the outer face
    pts = []
    for k in range(10):
        a = math.pi * 2 * k / 10 + math.pi / 2
        rad = rr * 0.93 if k % 2 == 0 else rr * 0.38
        pts.append((rad * math.cos(a), rad * math.sin(a)))
    star = extrude(pts, 0.03, mat=rim_mat, bv=0.006, plane="XY")
    L.place(star, (0, 0, 0), (0, 90 * side, 0))
    L.place(star, (x + side * (w / 2 - 0.03), y, r))
    P.append(star)
    P.append(cyl(rr * 0.25, 0.05, (x + side * (w / 2 - 0.02), y, r), (0, 90, 0), mat=rim_mat, n=20, bv=0.006))
    for k in range(5):
        a = 2 * math.pi * k / 5
        P.append(cyl(0.012, 0.02, (x + side * (w / 2 + 0.005), y + 0.05 * math.cos(a), r + 0.05 * math.sin(a)),
                     (0, 90, 0), mat="chrome", n=8, bv=0.002))
    P.append(cyl(rr * 0.82, 0.025, (x + side * 0.0, y, r), (0, 90, 0), mat="disc", n=32, bv=0.003))
    P.append(box((0.06, 0.12, 0.1), (x - side * 0.0, y - 0.0, r + rr * 0.62), mat="caliper", bv=0.01))


# ------------------------------------------------------------------ car
def build_car(name, cfg):
    P = []
    st = cfg["body"]
    body = body_loft(st, n_rows=34, n_per=36, p=cfg.get("p", 3.6))
    # wheel arches
    for ay in cfg["axles"]:
        for side in (-1, 1):
            c = L.cyl(cfg["arch_r"], 0.6, (side * (cfg["track"] / 2 + 0.25), ay, cfg["wheel_r"] + 0.02), (0, 90, 0),
                      mat="_cut", n=40, bv=0)
            boolean(body, c)
    P.append(body)
    for ay in cfg["axles"]:  # arch liners
        for side in (-1, 1):
            P.append(lathe([(cfg["arch_r"] - 0.01, -0.12), (cfg["arch_r"], -0.12), (cfg["arch_r"], 0.12),
                            (cfg["arch_r"] - 0.01, 0.12)], 32, (side * (cfg["track"] / 2 + 0.06), ay,
                                                                 cfg["wheel_r"] + 0.02), (0, 90, 0), mat="liner"))
            wheel(side * cfg["track"] / 2, ay, cfg["wheel_r"], cfg["tyre_w"], side, P)
    # greenhouse (tinted glass) + roof / pillars in body colour
    gh, rows = greenhouse(cfg["glass"], 24)
    P.append(gh)
    roof_rows = [r for r in rows if r[4] - r[3] > cfg["roof_min_h"]]
    if roof_rows:
        secs = []
        for y, hb, ht, zb, zt in roof_rows:
            secs.append([(-ht - 0.012, zt - 0.03), (ht + 0.012, zt - 0.03), (ht - 0.04, zt + 0.012),
                         (-(ht - 0.04), zt + 0.012)])
        P.append(loft(secs, [r[0] for r in roof_rows], "paint", "roof"))
    for y_p, kind in cfg["pillars"]:  # B / C pillars: vertical bands on both sides
        r = min(rows, key=lambda r: abs(r[0] - y_p))
        _, hb, ht, zb, zt = r
        for side in (-1, 1):
            P.append(sweep([(side * (hb + 0.004), y_p, zb + 0.01), (side * (ht + (hb - ht) * 0.1 + 0.006), y_p,
                                                                     zt - 0.02)], 0.035 if kind == "B" else 0.06,
                           mat="paint" if kind != "B" else "trim", smooth=False, n=8))
    # A pillars along the windshield edges
    for side in (-1, 1):
        pts = [(side * (r[1] + (r[2] - r[1]) * 0.9 + 0.006), r[0], r[3] + (r[4] - r[3]) * 0.92)
               for r in rows if cfg["glass"][0][0] <= r[0] <= cfg["a_end"]]
        if len(pts) >= 2:
            P.append(sweep(pts, 0.035, mat="paint", smooth=False, n=8))
    # window rubber/belt trim
    for side in (-1, 1):
        pts = [(side * (r[1] + 0.006), r[0], r[3] + 0.01) for r in rows]
        P.append(sweep(pts, 0.012, mat="trim", smooth=False, n=6))
    fy, ry = cfg["front_y"], cfg["rear_y"]
    hl_z, tl_z = cfg["hl_z"], cfg["tl_z"]
    for side in (-1, 1):
        # headlight: chrome reflector + glass lens
        hx = side * cfg["hl_x"]
        P.append(box((0.3, 0.06, 0.1), (hx, fy + 0.09, hl_z), (0, 0, side * -8), mat="reflector", bv=0.03, seg=3))
        P.append(box((0.31, 0.04, 0.11), (hx, fy + 0.065, hl_z), (0, 0, side * -8), mat="lens", bv=0.03, seg=3))
        P.append(cyl(0.035, 0.03, (hx + side * 0.06, fy + 0.05, hl_z), (90, 0, 0), mat="bulb", n=16, bv=0.004))
        # tail light
        P.append(box((0.3, 0.05, 0.1), (side * cfg["tl_x"], ry - 0.07, tl_z), (0, 0, side * 6), mat="taillight",
                     bv=0.02, seg=3))
        # mirror
        my = cfg["mirror_y"]
        P.append(box((0.04, 0.06, 0.03), (side * (cfg["mirror_x"] - 0.04), my, cfg["mirror_z"] - 0.02), mat="trim",
                     bv=0.01))
        P.append(box((0.16, 0.08, 0.11), (side * cfg["mirror_x"], my, cfg["mirror_z"]), (0, 0, side * 8),
                     mat="paint", bv=0.03, seg=3))
        P.append(box((0.13, 0.004, 0.08), (side * cfg["mirror_x"], my + 0.042, cfg["mirror_z"]), (0, 0, side * 8),
                     mat="mirror", bv=0.002))
        for dy in cfg["handles"]:  # door handles + door seams
            P.append(box((0.02, 0.15, 0.03), (side * (cfg["side_x"] + 0.008), dy, cfg["handle_z"]), mat="chrome",
                         bv=0.01))
        for sy in cfg["seams"]:
            P.append(box((0.006, 0.004, cfg["seam_h"]), (side * (cfg["side_x"] + 0.001), sy, cfg["seam_z"]),
                         mat="seam", bv=0.0))
    # grille + bumpers + plates + exhaust
    gz = cfg["grille_z"]
    P.append(box((cfg["grille_w"], 0.05, cfg["grille_h"]), (0, fy + 0.02, gz), mat="grille", bv=0.015))
    for i in range(4):
        P.append(box((cfg["grille_w"] - 0.04, 0.012, 0.012), (0, fy - 0.005, gz - cfg["grille_h"] / 2 + 0.03 + i *
                                                               (cfg["grille_h"] - 0.06) / 3), mat="chrome", bv=0.004))
    P.append(box((cfg["bumper_w"], 0.12, 0.14), (0, fy + 0.04, cfg["bumper_z"]), mat="bumper", bv=0.04, seg=3))
    P.append(box((cfg["bumper_w"], 0.12, 0.14), (0, ry - 0.04, cfg["bumper_z"] + 0.02), mat="bumper", bv=0.04, seg=3))
    for (py, rz, z) in ((fy - 0.025, 0, cfg["bumper_z"] + 0.02), (ry + 0.025, 180, cfg["plate_rz"])):
        P.append(box((0.52, 0.01, 0.11), (0, py, z), mat="plate", bv=0.004))
        P.append(text(cfg["plate"], 0.07, (0, py - 0.006 if rz == 0 else py + 0.006, z), (90, 0, rz), mat="ink",
                      bold=True))
    P.append(cyl(0.035, 0.1, (0.45, ry - 0.02, cfg["bumper_z"] - 0.08), (90, 0, 0), mat="chrome", n=16, bv=0.004))
    P.append(box((cfg["track"] - 0.2, cfg["wheelbase"] + 0.6, 0.06), (0, sum(cfg["axles"]) / 2, cfg["floor_z"]),
                 mat="under", bv=0.02))
    P += cfg.get("extra", lambda: [])()
    iz = cfg["floor_z"] + 0.05
    for (sy, seats) in cfg["seat_rows"]:
        for sx in seats:
            P.append(box((0.5, 0.5, 0.14), (sx, sy, iz + 0.28), mat="seat", bv=0.05, seg=2))
            P.append(box((0.5, 0.14, 0.6), (sx, sy + 0.3, iz + 0.6), (-12, 0, 0), mat="seat", bv=0.05, seg=2))
    dy = cfg["glass"][0][0] + 0.15
    P.append(box((2 * cfg["side_x"] - 0.2, 0.4, 0.25), (0, dy, cfg["glass"][0][3] - 0.05), mat="dash", bv=0.05))
    P.append(lathe([(0.17, -0.015), (0.19, -0.015), (0.19, 0.015), (0.17, 0.015)], 24, (-0.38, dy + 0.3,
                   cfg["glass"][0][3] + 0.02), (-65, 0, 0), mat="dash"))
    specs = {"paint": spec(None, cfg["color"], 0.4, "none", 0.0, (0.12, 0.2), 0.45, 0.0, noise=0.03, grain=0.02,
                           scratch=0.03, wear_color=tuple(min(c * 1.8 + 0.1, 1) for c in cfg["color"])),
             "glass": spec(None, (0.015, 0.018, 0.022), glass=True, rough=(0.02, 0.02), alpha=0.85),
             "seat": S("leather_black"), "dash": S("plastic_black", grain=0.3),
             "lens": spec(None, (0.85, 0.88, 0.9), glass=True, rough=(0.02, 0.02), alpha=0.2),
             "reflector": S("chrome"), "bulb": spec(None, (0.95, 0.95, 0.9), rough=(0.1, 0.1)),
             "taillight": spec(None, (0.45, 0.02, 0.02), rough=(0.05, 0.1)),
             "trim": S("plastic_black", color=(0.015, 0.015, 0.015), rough=(0.4, 0.55)),
             "mirror": S("chrome"), "chrome": S("chrome"), "seam": spec(None, (0.005, 0.005, 0.005), rough=(0.8, 0.9)),
             "grille": S("plastic_black", color=(0.02, 0.02, 0.02)), "bumper": cfg.get("bumper_spec") or
             spec(None, cfg["color"], 0.4, "none", 0.0, (0.12, 0.2), 0.45, 0.0, noise=0.03),
             "plate": spec(None, (0.85, 0.85, 0.82), rough=(0.3, 0.4)), "ink": S("ink"),
             "under": spec(None, (0.03, 0.03, 0.03), rough=(0.8, 0.9)), "liner": S("plastic_black", grain=0.3),
             "tyre": S("rubber", color=(0.02, 0.02, 0.02), rough=(0.75, 0.9)),
             "rim": S("alu", color=(0.62, 0.63, 0.65), rough=(0.2, 0.35)), "disc": S("steel", color=(0.35, 0.33, 0.32)),
             "caliper": spec(None, (0.4, 0.03, 0.02), rough=(0.4, 0.5))}
    specs.update(cfg.get("specs", {}))
    return L.finalize(name, P, specs, "vehicle", size=2048, max_tris=25000, smooth_angle=40, notes=cfg["notes"])


SEDAN = dict(
    body=[(-2.38, 0.6, 0.36, 0.6), (-2.3, 0.82, 0.26, 0.72), (-2.05, 0.89, 0.22, 0.79), (-1.4, 0.91, 0.2, 0.86),
          (-0.8, 0.92, 0.2, 0.93), (0.0, 0.925, 0.2, 0.96), (0.9, 0.925, 0.2, 0.98), (1.6, 0.915, 0.22, 1.0),
          (2.2, 0.89, 0.27, 0.99), (2.42, 0.82, 0.32, 0.94), (2.47, 0.6, 0.4, 0.82)],
    glass=[(-0.85, 0.88, 0.74, 0.93, 0.95), (-0.45, 0.86, 0.72, 0.945, 1.25), (-0.05, 0.85, 0.7, 0.955, 1.42),
           (0.5, 0.85, 0.7, 0.965, 1.45), (1.05, 0.85, 0.7, 0.975, 1.40), (1.45, 0.86, 0.72, 0.985, 1.2),
           (1.75, 0.87, 0.74, 0.995, 1.01)],
    axles=(-1.42, 1.42), wheelbase=2.84, track=1.58, wheel_r=0.33, tyre_w=0.23, arch_r=0.385,
    roof_min_h=0.38, pillars=[(0.42, "B"), (1.45, "C")], a_end=-0.05, front_y=-2.36, rear_y=2.46,
    hl_x=0.6, hl_z=0.62, tl_x=0.66, tl_z=0.86, mirror_x=0.99, mirror_y=-0.62, mirror_z=1.0,
    handles=(-0.05, 0.85), side_x=0.925, handle_z=0.88, seams=(-0.75, 0.4, 1.4), seam_h=0.5, seam_z=0.66,
    seat_rows=[(0.0, (-0.38, 0.38)), (0.95, (-0.38, 0.38))],
    grille_w=0.7, grille_h=0.18, grille_z=0.55, bumper_w=1.7, bumper_z=0.36, plate="AB-447-HT", plate_rz=0.62,
    floor_z=0.2, color=(0.02, 0.035, 0.08),
    notes="4.85 m executive saloon, dark blue metallic, tinted glass greenhouse")


def _pickup_bed():
    P = []
    y0, y1, z0, z1, hw = 0.55, 2.55, 0.62, 1.12, 0.92
    P.append(box((2 * hw - 0.08, y1 - y0, 0.04), (0, (y0 + y1) / 2, z0), mat="bedliner", bv=0.005))
    for side in (-1, 1):
        P.append(box((0.06, y1 - y0, z1 - z0), (side * (hw - 0.03), (y0 + y1) / 2, (z0 + z1) / 2), mat="paint",
                     bv=0.02, seg=2))
        P.append(box((0.1, y1 - y0, 0.04), (side * (hw - 0.04), (y0 + y1) / 2, z1), mat="trim", bv=0.012))
    P.append(box((2 * hw, 0.06, z1 - z0), (0, y0 + 0.03, (z0 + z1) / 2), mat="paint", bv=0.02))
    P.append(box((2 * hw, 0.06, z1 - z0 - 0.02), (0, y1 - 0.03, (z0 + z1) / 2 - 0.01), mat="paint", bv=0.02))  # tailgate
    P.append(box((0.24, 0.02, 0.05), (0, y1 + 0.005, z1 - 0.1), mat="chrome", bv=0.01))
    P.append(text("RANGER", 0.06, (0, y1 + 0.002, z1 - 0.22), (90, 0, 180), mat="chrome"))
    for i in range(6):
        P.append(box((0.04, y1 - y0 - 0.1, 0.012), (-0.6 + i * 0.24, (y0 + y1) / 2, z0 + 0.025), mat="bedliner",
                     bv=0.004))
    return P


PICKUP = dict(
    body=[(-2.72, 0.7, 0.45, 0.85), (-2.65, 0.9, 0.36, 0.98), (-2.4, 0.95, 0.34, 1.07), (-1.6, 0.97, 0.34, 1.12),
          (-0.95, 0.98, 0.34, 1.15), (-0.3, 0.98, 0.34, 1.16), (0.3, 0.98, 0.36, 1.16), (0.55, 0.96, 0.42, 1.1),
          (0.6, 0.6, 0.5, 0.9)],
    glass=[(-0.98, 0.93, 0.8, 1.14, 1.16), (-0.6, 0.92, 0.78, 1.15, 1.6), (-0.3, 0.91, 0.77, 1.155, 1.82),
           (0.1, 0.91, 0.77, 1.16, 1.84), (0.42, 0.92, 0.78, 1.16, 1.82), (0.52, 0.93, 0.8, 1.16, 1.4),
           (0.55, 0.93, 0.8, 1.16, 1.17)],
    axles=(-1.75, 1.75), wheelbase=3.5, track=1.7, wheel_r=0.39, tyre_w=0.27, arch_r=0.45,
    roof_min_h=0.5, pillars=[(0.0, "B")], a_end=-0.3, front_y=-2.7, rear_y=2.58,
    hl_x=0.68, hl_z=0.9, tl_x=0.86, tl_z=0.95, mirror_x=1.06, mirror_y=-0.85, mirror_z=1.28,
    handles=(-0.4, 0.35), side_x=0.98, handle_z=1.08, seams=(-0.95, -0.02, 0.52), seam_h=0.55, seam_z=0.85,
    seat_rows=[(-0.1, (-0.4, 0.4))],
    grille_w=1.1, grille_h=0.32, grille_z=0.78, bumper_w=1.95, bumper_z=0.5, plate="GT-208-LK", plate_rz=0.62,
    floor_z=0.42, color=(0.42, 0.42, 0.4),
    bumper_spec=spec(None, (0.6, 0.6, 0.6), 0.4, "none", 0.0, (0.1, 0.2), 1.0, 0.0, noise=0.05, scratch=0.2,
                     wear_color=(0.8, 0.8, 0.8)),
    specs={"bedliner": S("rubber", color=(0.03, 0.03, 0.03), grain=0.6)},
    extra=_pickup_bed,
    notes="5.3 m crew-less single-cab pickup, silver, open bed (cover / throwables)")


# ------------------------------------------------------------------ boat
def _hull_section(hw, zk, zc, zd, n=24):
    """V hull: keel at zk, chine (half width hw at zc), gunwale at zd (flare)."""
    pts = []
    half = [(0.0, zk), (hw * 0.55, zk + (zc - zk) * 0.45), (hw * 0.92, zc - 0.02), (hw, zc), (hw * 1.03, zc + 0.02),
            (hw * 1.08, zd - 0.05), (hw * 1.08, zd), (hw * 0.0, zd)]
    right = half[:-1]
    left = [(-x, z) for x, z in reversed(right[1:])]
    sec = right + [(right[-1][0] * 0.0 + 0.0, zd)] + left
    # resample to n points along the polygon
    return sec


def build_boat():
    P = []
    st = [(-2.7, 0.03, 0.55, 0.75, 1.05), (-2.4, 0.35, 0.3, 0.6, 1.0), (-1.8, 0.7, 0.12, 0.45, 0.95),
          (-0.8, 0.95, 0.04, 0.36, 0.92), (0.5, 1.02, 0.02, 0.32, 0.9), (1.8, 1.02, 0.02, 0.3, 0.88),
          (2.6, 0.98, 0.04, 0.3, 0.86)]
    rows = catmull(st, 26)
    secs = [_hull_section(r[1], r[2], r[3], r[4]) for r in rows]
    hull = loft(secs, [r[0] for r in rows], "hull")
    P.append(hull)
    # deck cap inset (cockpit floor) + rub rail
    P.append(box((1.7, 3.2, 0.04), (0, 0.9, 0.55), mat="deck", bv=0.01))
    rail = [(r[1] * 1.085 + 0.015, r[0], r[4] - 0.02) for r in rows]
    for side in (-1, 1):
        P.append(sweep([(side * x, y, z) for x, y, z in rail], 0.025, mat="rail", smooth=True))
    # foredeck
    fore = [r for r in rows if r[0] < -0.6]
    secs = [[(-r[1] * 1.08, r[4] - 0.04), (r[1] * 1.08, r[4] - 0.04), (r[1] * 0.9, r[4] + 0.03),
             (-r[1] * 0.9, r[4] + 0.03)] for r in fore]
    P.append(loft(secs, [r[0] for r in fore], "hull", "fore"))
    # console with windscreen, wheel, seat
    P.append(box((0.8, 0.6, 0.55), (0.0, -0.2, 0.84), mat="hull", bv=0.06, seg=3))
    ws = extrude([(-0.45, 0.0), (0.45, 0.0), (0.4, 0.35), (-0.4, 0.35)], 0.012, mat="glass", bv=0.0, plane="XZ")
    L.place(ws, (0, 0, 0), (-25, 0, 0))
    L.place(ws, (0, -0.47, 1.1))
    P.append(ws)
    P.append(box((0.92, 0.04, 0.04), (0, -0.47, 1.12), mat="rail", bv=0.012))
    P.append(cyl(0.16, 0.02, (0.12, -0.02, 1.25), (-60, 0, 0), mat="wheel", n=24, bv=0.006))
    P.append(cyl(0.02, 0.15, (0.12, 0.03, 1.2), (-60, 0, 0), mat="rail", n=12, bv=0.004))
    P.append(box((0.3, 0.05, 0.14), (-0.18, -0.08, 1.14), (30, 0, 0), mat="dash", bv=0.01))
    P.append(box((0.9, 0.55, 0.35), (0.0, 1.2, 0.75), mat="cushion", bv=0.08, seg=3))
    P.append(box((0.9, 0.18, 0.4), (0.0, 1.45, 1.05), (12, 0, 0), mat="cushion", bv=0.07, seg=3))
    # outboard motor on the transom
    P.append(box((0.42, 0.5, 0.55), (0, 2.85, 1.05), mat="motor", bv=0.12, seg=3))
    P.append(box((0.2, 0.25, 0.7), (0, 2.85, 0.5), mat="motor", bv=0.04, seg=2))
    P.append(box((0.08, 0.35, 0.03), (0, 2.85, 0.2), mat="motor", bv=0.012))
    P.append(cyl(0.06, 0.2, (0, 2.82, 0.12), (90, 0, 0), mat="motor", n=16, bv=0.01))
    for k in range(3):
        bl = box((0.03, 0.12, 0.2), (0, 3.0, 0.12), (0, k * 120, 0), mat="prop", bv=0.008)
        P.append(bl)
    P.append(text("MERIDIAN 150", 0.06, (0.211, 2.85, 1.1), (90, 0, 90), mat="decal"))
    P.append(text("MERIDIAN 150", 0.06, (-0.211, 2.85, 1.1), (90, 0, -90), mat="decal"))
    for y in (-1.8, 2.3):  # cleats
        for side in (-1, 1):
            r = min(rows, key=lambda r: abs(r[0] - y))
            P.append(box((0.03, 0.16, 0.03), (side * r[1] * 0.95, y, r[4] + 0.03), mat="rail", bv=0.01))
    P.append(text("LA BELLE ROSE", 0.12, (0.98, 1.6, 0.68), (90, 0, 90), mat="name"))
    P.append(text("LA BELLE ROSE", 0.12, (-0.98, 1.6, 0.68), (90, 0, -90), mat="name"))
    specs = {"hull": S("plastic_white", color=(0.85, 0.85, 0.82), rough=(0.12, 0.25)),
             "deck": spec("rubber_tiles", (0.55, 0.55, 0.52), 0.5, "tint", 0.2, (0.6, 0.8), 0.0, 0.4, grain=0.5),
             "rail": S("brushed"), "glass": spec(None, (0.3, 0.35, 0.35), glass=True, rough=(0.02, 0.02), alpha=0.3),
             "wheel": S("plastic_black"), "dash": S("plastic_black"), "cushion": spec("leather_white", (0.9, 0.88, 0.84), 0.4, "mul", 0, (0.35, 0.5), 0.0, 0.4),
             "motor": S("plastic_black", color=(0.015, 0.015, 0.015), rough=(0.15, 0.25)), "prop": S("alu"),
             "decal": spec(None, (0.8, 0.8, 0.8), rough=(0.3, 0.3)), "name": spec(None, (0.05, 0.12, 0.3), rough=(0.3, 0.3))}
    return L.finalize("Prop_Boat", P, specs, "vehicle", size=2048, max_tris=20000, smooth_angle=45,
                      notes="5.6 m centre-console motorboat with outboard; waterline about 0.3 m above the pivot")


# ------------------------------------------------------------------ helicopter
def build_helicopter():
    P = []
    st = [(-2.3, 0.05, 0.85, 1.1), (-2.1, 0.45, 0.55, 1.55), (-1.6, 0.68, 0.45, 1.95), (-0.9, 0.78, 0.45, 2.15),
          (0.0, 0.8, 0.48, 2.2), (0.8, 0.75, 0.55, 2.15), (1.4, 0.55, 0.85, 2.05), (1.9, 0.3, 1.25, 1.85),
          (2.2, 0.16, 1.45, 1.75)]
    body = body_loft(st, n_rows=30, n_per=36, p=2.6, mat="paint")
    P.append(body)
    # canopy glass (front bubble) + side door windows
    can = body_loft([(-2.26, 0.12, 0.95, 1.12), (-2.1, 0.47, 0.75, 1.6), (-1.6, 0.7, 0.7, 2.0), (-1.0, 0.78, 0.9, 2.17),
                     (-0.6, 0.79, 1.15, 2.19)], n_rows=14, n_per=36, p=2.6, mat="glass")
    L.place(can, (0, -0.012, 0.0), scale=(1.01, 1.0, 1.0))
    P.append(can)
    for side in (-1, 1):
        P.append(box((0.01, 0.75, 0.55), (side * 0.805, 0.15, 1.6), mat="glass", bv=0.06, seg=3))
        P.append(box((0.012, 0.8, 0.04), (side * 0.8, 0.15, 1.3), mat="trim", bv=0.01))
        P.append(box((0.012, 0.03, 0.9), (side * 0.805, -0.32, 1.45), mat="trim", bv=0.01))
        P.append(box((0.03, 0.18, 0.03), (side * 0.82, 0.5, 1.4), mat="chrome", bv=0.01))
    # engine fairing + mast + rotor hub + blades
    P.append(box((0.9, 1.6, 0.45), (0, 0.6, 2.35), mat="paint", bv=0.2, seg=4))
    P.append(box((0.4, 0.3, 0.2), (0, 1.2, 2.4), mat="grille", bv=0.05))
    for side in (-1, 1):
        P.append(cyl(0.12, 0.35, (side * 0.22, 1.45, 2.32), (90, 0, 0), mat="exhaust", n=20, bv=0.01))
    P.append(cyl(0.07, 0.45, (0, 0.2, 2.75), mat="steel", n=20, bv=0.01))
    P.append(cyl(0.22, 0.12, (0, 0.2, 2.98), mat="steel", n=24, bv=0.03))
    for k in range(4):
        a = k * 90 + 20
        bl = box((5.0, 0.28, 0.035), (2.6, 0, 0), mat="blade", bv=0.012)
        L.place(bl, (0, 0, 0), (2, 0, a))
        L.place(bl, (0, 0.2, 3.0))
        P.append(bl)
    # tail boom, fins, tail rotor
    P.append(lathe([(0.25, 0.0), (0.2, 1.5), (0.12, 3.6), (0.1, 3.9), (0.0, 3.95)], 24, (0, 2.0, 1.6), (-88, 0, 0),
                   mat="paint"))
    P.append(extrude([(5.5, 1.3), (5.95, 1.35), (6.1, 2.3), (5.85, 2.3)], 0.06, mat="paint", bv=0.02, plane="YZ"))
    P.append(extrude([(5.6, 1.45), (6.0, 1.45), (6.05, 1.1), (5.8, 1.1)], 0.05, mat="paint", bv=0.02, plane="YZ"))
    hstab = extrude([(-0.85, 0.0), (0.85, 0.0), (0.8, 0.28), (-0.8, 0.28)], 0.04, mat="paint", bv=0.015, plane="XY")
    L.place(hstab, (0, 4.6, 1.55))
    P.append(hstab)
    for k in range(2):
        bl = box((0.03, 0.12, 1.1), (0.12, 5.9, 1.95), (k * 90, 0, 0), mat="blade", bv=0.01)
        P.append(bl)
    P.append(cyl(0.06, 0.12, (0.08, 5.9, 1.95), (0, 90, 0), mat="steel", n=16, bv=0.01))
    # skids
    for side in (-1, 1):
        P.append(sweep([(side * 1.05, -1.6, 0.25), (side * 1.05, -1.9, 0.12), (side * 1.05, -1.6, 0.04),
                        (side * 1.05, 1.5, 0.04)], 0.04, mat="skid", smooth=True))
        for y in (-0.9, 0.7):
            P.append(sweep([(side * 1.05, y, 0.06), (side * 0.95, y, 0.45), (side * 0.6, y, 0.6)], 0.035,
                           mat="skid", smooth=True))
    # markings, lights, antenna
    P.append(text("F-HBVL", 0.32, (0.79, 1.1, 1.0), (90, 0, 90), mat="ink"))
    P.append(text("F-HBVL", 0.32, (-0.79, 1.1, 1.0), (90, 0, -90), mat="ink"))
    P.append(sphere(0.05, (0, 0.6, 2.58), mat="beacon", seg=12, rings=8))
    P.append(cyl(0.008, 0.5, (0, -0.4, 2.35), (20, 0, 0), mat="steel", n=6, bv=0))
    specs = {"paint": spec(None, (0.02, 0.025, 0.04), 0.4, "none", 0.0, (0.12, 0.2), 0.5, 0.0, noise=0.03, scratch=0.04,
                           wear_color=(0.2, 0.2, 0.25)),
             "glass": spec(None, (0.08, 0.1, 0.12), glass=True, rough=(0.02, 0.02), alpha=0.45),
             "trim": S("plastic_black"), "chrome": S("chrome"), "grille": S("plastic_black"),
             "exhaust": S("steel", color=(0.25, 0.22, 0.2)), "steel": S("steel"),
             "blade": spec(None, (0.04, 0.04, 0.04), rough=(0.35, 0.5), scratch=0.1, wear_color=(0.4, 0.4, 0.4)),
             "skid": S("alu"), "stripe": spec(None, (0.6, 0.45, 0.15), rough=(0.2, 0.3), metal=0.6),
             "ink": spec(None, (0.85, 0.85, 0.85), rough=(0.3, 0.3)), "beacon": spec(None, (0.8, 0.05, 0.02), rough=(0.1, 0.1))}
    return L.finalize("Prop_Helicopter", P, specs, "vehicle", size=2048, max_tris=30000, smooth_angle=45,
                      notes="light utility helicopter (static), 10 m rotor, ~8.3 m long; for helipads")


BUILDERS = {
    "Prop_Car_Sedan": lambda: build_car("Prop_Car_Sedan", SEDAN),
    "Prop_Car_Pickup": lambda: build_car("Prop_Car_Pickup", PICKUP),
    "Prop_Boat": build_boat,
    "Prop_Helicopter": build_helicopter,
}

if __name__ == "__main__":
    L.run(BUILDERS, "vehicle")
