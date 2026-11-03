# Author : Baptiste Vu Liem
"""Generic garments of the cast (shirts, jackets, aprons, dresses, vests, parkas, hats, shoes,
accessories). Same conventions as garments.py: SDF Parts on a BodyShape, slot names as materials."""
import numpy as np

import sdf
from garments import V, lumps, shell, limb_cut, surface_y, surface_along, slab, _rib_ridges


# ------------------------------------------------------------------------------------- helpers

def _arm_cut_u(B, s, sleeve):
    """Point along the arm where a sleeve ends: sleeve in [0, 1] = upper arm, [1, 2] = forearm (2 = wrist)."""
    j = B.j
    sh, el = j["shoulder_" + s], j["elbow_" + s]
    fr = B.frame(s)
    if sleeve <= 1.0:
        return sh + (el - sh) * sleeve, el - sh
    t = sleeve - 1.0
    wr = fr.p((-0.060, 0, 0))
    return el + (wr - el) * t, wr - el


def _sleeves(B, part, sleeve, ease_extra=0.004, roll=False, cuff_mat=None):
    j = B.j
    extra = []
    shell(part, lumps(B, regions=["upperarm"] + (["forearm"] if sleeve > 1.0 else [])), 0.01, 0.025)
    for s in ("l", "r"):
        sh, el = j["shoulder_" + s], j["elbow_" + s]
        fr = B.frame(s)
        part.add(sdf.RoundCone(sh, el, B.limb_radius("upperarm", 0) + ease_extra, B.limb_radius("upperarm", 1) + ease_extra + 0.002), 0.035)
        if sleeve > 1.0:
            part.add(sdf.RoundCone(el, fr.p((-0.06, 0, 0)), B.limb_radius("upperarm", 1) + ease_extra, 0.033 + ease_extra * 0.5), 0.02)
    return extra


def _cut_sleeves(B, part, sleeve):
    for s in ("l", "r"):
        e, d = _arm_cut_u(B, s, sleeve)
        part.sub(limb_cut(e, e + sdf.unit(d) * 0.6, 0.11), 0.0)


def _roll_rings(B, sleeve, ease, name="Roll", material=None):
    p = sdf.Part(name, 0.0022, material)
    for s in ("l", "r"):
        e, d = _arm_cut_u(B, s, sleeve)
        u = sdf.unit(d)
        r = (B.limb_radius("upperarm", min(sleeve, 1.0)) if sleeve <= 1.0 else B.limb_radius("forearm", sleeve - 1.0)) + ease + 0.009
        p.add(sdf.Ring(e - u * 0.012, u, r, 0.0062, 0.016, 0.005))
    return p


def _neck_hole(B, part, extra=0.010, drop=0.0):
    nr = B.p["neck"]
    part.sub(sdf.RoundCone(V(0, 0.012, 1.40 - drop), V(0, 0.0, 1.62), nr + extra, nr + extra + 0.006), 0.006)


def _v_cut(part, z_bottom, half_top, y_front=-0.03):
    """V-neck: wedge from the collar down to z_bottom at the front."""
    k = half_top / max(1.45 - z_bottom, 0.05)
    part.sub(sdf.Intersect([sdf.HalfSpace(V(0, 0, z_bottom), (1, 0, -k)), sdf.HalfSpace(V(0, 0, z_bottom), (-1, 0, -k)),
                            sdf.HalfSpace(V(0, y_front, 0), (0, 1, 0))], 0.0, box=((-0.2, -0.4, z_bottom - 0.01), (0.2, 0.0, 1.7))), 0.003)


def _front_points(part, xs_zs, lift=0.0):
    out = []
    for x, z in xs_zs:
        y = surface_y(part, x, z)
        if y is not None:
            out.append(V(x, y - lift, z))
    return out


def _buttons(part_for_surface, zs, x=0.0, r=0.0055, name="Buttons", material="Buttons", depth=0.0022):
    p = sdf.Part(name, 0.0009, material)
    for z in zs:
        y = surface_y(part_for_surface, x, z)
        if y is None:
            continue
        c = V(x, y - depth * 0.3, z)
        p.add(sdf.CappedCone(c + V(0, 0.002, 0), c - V(0, depth, 0), r, r * 0.95, 0.0012))
    return p


def _shirt_collar(B, name="Collar", material="Shirt", open_=False, height=0.026, spread=1.0):
    """Standing band round the neck + two points folding down at the front."""
    nr = B.p["neck"]
    p = sdf.Part(name, 0.0016, material)
    p.add(sdf.Ring(V(0, 0.004, 1.452), (0, -0.30, 1), nr + 0.0105, 0.0030, height * 0.5, 0.0022))
    for sx in (1, -1):
        a = V(sx * (0.020 if not open_ else 0.034), -0.050 - 0.004, 1.448)
        b = V(sx * (0.052 + 0.012 * spread), -0.050, 1.402 if not open_ else 1.395)
        c = V(sx * (0.012 if not open_ else 0.030), -0.062, 1.405 if not open_ else 1.385)
        n = sdf.unit(np.cross(b - a, c - a)) * (1 if sx > 0 else -1)
        pts = [a, (a + b) * 0.5 + V(0, -0.006, 0), b]
        p.add(sdf.ribbon([a, (a + c) * 0.5 + V(0, -0.010, 0), c], [n] * 3, [0.012, 0.016, 0.006], 0.0018), 0.002)
        p.add(sdf.ribbon(pts, [n] * 3, [0.010, 0.012, 0.004], 0.0018), 0.002)
    if open_:
        p.sub(sdf.Box(V(0, -0.11, 1.40), (0.026, 0.06, 0.07), None, 0.01), 0.0)
    return p


# ------------------------------------------------------------------------------------- tops

def shirt(B, name="Shirt", material="Shirt", sleeve=2.0, ease=0.007, hem=0.930, collar="shirt", roll=False,
          placket=True, buttons=True, pocket=False, open_neck=False, double=False, collar_mat=None, button_mat="Buttons",
          v_depth=1.30, tuck=True, voxel=0.003, folds=True):
    """Long/short sleeved shirt, polo, crew/V/turtle/mandarin necks, chef double-breast."""
    j = B.j
    part = sdf.Part(name, voxel, material)
    shell(part, lumps(B, regions=["torso", "shoulder"] + (["hips"] if hem < 0.95 else [])), 0.02)
    _sleeves(B, part, sleeve)
    part.offset(ease)
    part.inter(sdf.HalfSpace(V(0, 0, hem), (0, 0, -1)), 0.004)
    _cut_sleeves(B, part, sleeve)
    nr = B.p["neck"]
    if collar in ("crew", "turtle", "mandarin", "shirt", "polo"):
        _neck_hole(B, part, 0.008 if collar in ("turtle", "mandarin") else 0.010)
    if collar == "v" or (collar == "shirt" and open_neck):
        _v_cut(part, v_depth, 0.030 if collar == "v" else 0.026)
        if collar == "v":
            _neck_hole(B, part, 0.010)
    if collar == "crew":
        part.add(sdf.Ring(V(0, 0.000, 1.420), (0, -0.30, 1), nr + 0.0135, 0.0042, 0.0058, 0.003), 0.002)
    if folds:
        for s in ("l", "r"):
            if sleeve > 1.2:
                sh, el = j["shoulder_" + s], j["elbow_" + s]
                u = sdf.unit(el - sh)
                inner = sdf.unit(np.cross(u, V(0, 1, 0))) * (1 if s == "l" else -1)
                for off in (-0.02, 0.012):
                    c = el + u * off - inner * (B.limb_radius("upperarm", 1) + ease * 0.6)
                    part.add(sdf.Capsule(c + V(0, -0.022, 0) + u * 0.008, c + V(0, 0.018, 0) - u * 0.006, 0.0045), 0.008)
    parts = [part]
    if collar == "turtle":
        t = sdf.Part(name + "Neck", 0.002, material)
        t.add(sdf.CappedCone(V(0, 0.010, 1.405), V(0, 0.010, 1.505), nr + 0.014, nr + 0.010, 0.006))
        t.add(sdf.Torus(V(0, 0.008, 1.432), (0, -0.2, 1), nr + 0.014, 0.008), 0.006)
        t.add(sdf.Torus(V(0, 0.010, 1.462), (0, -0.1, 1), nr + 0.012, 0.007), 0.006)
        t.sub(sdf.CappedCone(V(0, 0.010, 1.40), V(0, 0.010, 1.52), nr + 0.001, nr + 0.001), 0.0)
        t.displace(_rib_ridges(0.006, 0.0008), 0.0008)
        parts.append(t)
    elif collar == "mandarin":
        t = sdf.Part(name + "Neck", 0.002, collar_mat or material)
        t.add(sdf.Ring(V(0, 0.004, 1.450), (0, -0.28, 1), nr + 0.012, 0.0032, 0.013, 0.003))
        parts.append(t)
    elif collar in ("shirt", "polo"):
        parts.append(_shirt_collar(B, name + "Collar", collar_mat or material, open_neck))
    if placket and collar in ("shirt", "polo") and not open_neck:
        pk = sdf.Part(name + "Placket", 0.0016, material)
        zb = hem + 0.01 if collar == "shirt" else 1.30
        pts = _front_points(part, [(0.0, z) for z in np.linspace(zb, 1.405, 10)], 0.0012)
        if len(pts) > 2:
            pk.add(sdf.ribbon(pts, [(0, -1, 0)] * len(pts), 0.011, 0.0018), 0.0)
            parts.append(pk)
    if buttons:
        if double:
            zs = np.linspace(1.10, 1.33, 4)
            parts.append(_buttons(part, zs, 0.050, 0.0065, name + "BtnL", button_mat))
            parts.append(_buttons(part, zs, -0.050, 0.0065, name + "BtnR", button_mat))
        elif collar in ("shirt", "polo") and not open_neck:
            zs = np.linspace(max(hem + 0.03, 1.02), 1.39, 6) if collar == "shirt" else [1.33, 1.37]
            parts.append(_buttons(part, zs, 0.0, 0.0045, name + "Btn", button_mat))
    if pocket:
        pp = _front_points(part, [(0.062, 1.30)])
        if pp:
            pk2 = sdf.Part(name + "Pocket", 0.0016, material)
            pk2.add(sdf.Box(pp[0] + V(0, 0.0015, -0.012), (0.026, 0.003, 0.030), None, 0.003))
            pk2.add(sdf.Box(pp[0] + V(0, -0.0008, 0.016), (0.0275, 0.0035, 0.009), None, 0.003), 0.002)
            parts.append(pk2)
    if roll:
        parts.append(_roll_rings(B, sleeve, ease, name + "Roll", material))
    return parts


def suit_jacket(B, name="Suit", material="Suit", hem=0.800, ease=0.014, button_z=1.060, lapel_mat=None, button_mat="Buttons",
                pocket_square=None):
    j = B.j
    part = sdf.Part(name, 0.003, material)
    shell(part, lumps(B, regions=["torso", "shoulder", "hips"]), 0.02, 0.035)
    _sleeves(B, part, 2.0, 0.006)
    # shoulder pads: squarer line
    for s, sx in (("l", 1), ("r", -1)):
        part.add(sdf.Capsule(V(sx * 0.08, 0.004, 1.420), j["shoulder_" + s] + V(sx * 0.02, 0, 0.038), 0.024), 0.03)
    # the cloth hangs straight from the chest instead of hugging the belly
    # the cloth hangs from the chest over the belly: a tilted elliptical cone from the chest's
    # section (top) to the belly / hips section (hem), so a big belly is draped, not hugged
    def section(names):
        ls = [pr for pr, k in lumps(B, names=names)]
        front, back = min(p.lo[1] for p in ls), max(p.hi[1] for p in ls)
        return (front + back) * 0.5, (back - front) * 0.5, max(p.hi[0] for p in ls)
    yt, ryt, rxt = section(["chest", "back", "pec", "bust"])
    yb, ryb, rxb = section(["belly", "waist", "pelvis", "butt"])
    sy = 0.5 * (ryt / rxt + ryb / rxb)
    drape = sdf.CappedCone(V(0, yb, hem), V(0, yt, 1.28), rxb * 0.97, rxt * 0.97, 0.03)
    part.add(sdf.Scaled(drape, V(0, 0.5 * (yt + yb), 1.0), np.eye(3), (1.0, sy, 1.0)), 0.07)
    part.offset(ease)
    part.inter(sdf.HalfSpace(V(0, 0, hem), (0, 0, -1)), 0.006)
    for s in ("l", "r"):
        fr = B.frame(s)
        part.sub(limb_cut(fr.p((-0.058, 0, 0)), fr.p((0.4, 0, 0)), 0.09), 0.003)
    nr = B.p["neck"]
    part.sub(sdf.RoundCone(V(0, 0.016, 1.40), V(0, 0.0, 1.64), nr + 0.020, nr + 0.024), 0.008)
    # deep V to the button, then the skirts part below it
    k_top = 0.040 / max(1.45 - button_z, 0.05)
    part.sub(sdf.Intersect([sdf.HalfSpace(V(0, 0, button_z), (1, 0, -k_top)), sdf.HalfSpace(V(0, 0, button_z), (-1, 0, -k_top)),
                            sdf.HalfSpace(V(0, -0.03, 0), (0, 1, 0)), sdf.HalfSpace(V(0, 0, button_z - 0.01), (0, 0, -1))], 0.0,
                           box=((-0.2, -0.4, button_z - 0.02), (0.2, 0.0, 1.7))), 0.003)
    k_bot = 0.06 / max(button_z - hem, 0.05)
    part.sub(sdf.Intersect([sdf.HalfSpace(V(0, 0, button_z), (1, 0, k_bot)), sdf.HalfSpace(V(0, 0, button_z), (-1, 0, k_bot)),
                            sdf.HalfSpace(V(0, -0.03, 0), (0, 1, 0)), sdf.HalfSpace(V(0, 0, button_z - 0.005), (0, 0, 1))], 0.012,
                           box=((-0.2, -0.4, hem - 0.05), (0.2, 0.0, button_z))), 0.004)
    parts = [part]
    # lapels: a rolled edge along each side of the V and a collar band behind the neck
    lap = sdf.Part(name + "Lapel", 0.0016, lapel_mat or material)
    for sx in (1, -1):
        pts = []
        for t in np.linspace(0.0, 1.0, 10):
            z = button_z + (1.430 - button_z) * t
            x = sx * (0.002 + 0.040 * (z - button_z) / max(1.45 - button_z, 0.05) + 0.004)
            y = surface_y(part, x + sx * 0.006, z)
            if y is not None:
                pts.append(V(x + sx * 0.010, y + 0.001, z))
        if len(pts) > 2:
            lap.add(sdf.tube(pts, [0.0045 + 0.0035 * (i / (len(pts) - 1)) for i in range(len(pts))]), 0.004)
    lap.add(sdf.Ring(V(0, 0.012, 1.452), (0, -0.25, 1), nr + 0.022, 0.0040, 0.017, 0.003), 0.004)
    lap.sub(sdf.Intersect([sdf.HalfSpace(V(0, 0, button_z), (1, 0, -k_top)), sdf.HalfSpace(V(0, 0, button_z), (-1, 0, -k_top)),
                           sdf.HalfSpace(V(0, -0.05, 0), (0, 1, 0))], 0.0, box=((-0.2, -0.4, button_z - 0.02), (0.2, 0.0, 1.7))), 0.002)
    parts.append(lap)
    parts.append(_buttons(part, [button_z - 0.005], 0.0, 0.0085, name + "Btn", button_mat))
    flaps = sdf.Part(name + "Flaps", 0.0016, material)
    for sx in (1, -1):
        pp = _front_points(part, [(sx * 0.10, 0.905)])
        if pp:
            flaps.add(sdf.Box(pp[0] + V(0, 0.0025, 0), (0.042, 0.0045, 0.014), sdf.rot((0, 1, 0), sx * 3), 0.004))
    bp = _front_points(part, [(0.075, 1.290)])
    if bp:
        flaps.add(sdf.Box(bp[0] + V(0, 0.0018, 0), (0.026, 0.0035, 0.006), sdf.rot((0, 1, 0), -8), 0.003))
    parts.append(flaps)
    if pocket_square:
        ps = sdf.Part(name + "Square", 0.0012, pocket_square)
        if bp:
            ps.add(sdf.Ellipsoid(bp[0] + V(0.004, 0.0, 0.010), (0.010, 0.004, 0.009)))
            ps.add(sdf.Ellipsoid(bp[0] + V(-0.007, 0.0, 0.012), (0.008, 0.004, 0.010), sdf.rot((0, 1, 0), 20)), 0.002)
        parts.append(ps)
    return parts


def waistcoat(B, name="Waistcoat", material="Waistcoat", ease=0.012, v_bottom=1.18, hem=0.955, button_mat="Buttons"):
    j = B.j
    part = sdf.Part(name, 0.003, material)
    shell(part, lumps(B, regions=["torso"]) + lumps(B, names=["pelvis"]), 0.02)
    part.offset(ease)
    part.inter(sdf.HalfSpace(V(0, 0, hem), (0, 0, -1)), 0.004)
    # arm holes
    for s, sx in (("l", 1), ("r", -1)):
        sh = j["shoulder_" + s]
        part.sub(sdf.Ellipsoid(sh + V(-sx * 0.010, 0.0, -0.045), (0.058, 0.080, 0.105)), 0.01)
    _neck_hole(B, part, 0.014)
    _v_cut(part, v_bottom, 0.050)
    parts = [part, _buttons(part, np.linspace(hem + 0.06, v_bottom - 0.01, 4), 0.0, 0.0050, name + "Btn", button_mat)]
    return parts


def tactical_vest(B, name="Vest", material="Vest", ease=0.034, strap_mat=None, pouch=True):
    j = B.j
    part = sdf.Part(name, 0.003, material)
    shell(part, lumps(B, names=["chest", "back", "waist", "belly", "pec", "bust"]), 0.02)
    part.offset(ease)
    part.inter(sdf.Intersect(slab(0.965, 1.375), 0.004, box=((-1, -1, 0.9), (1, 1, 1.45))), 0.006)
    for s, sx in (("l", 1), ("r", -1)):
        sh = j["shoulder_" + s]
        part.sub(sdf.Ellipsoid(sh + V(-sx * 0.004, 0.0, -0.060), (0.060, 0.085, 0.110)), 0.012)
    _v_cut(part, 1.30, 0.045)
    parts = [part]
    st = sdf.Part(name + "Straps", 0.0022, strap_mat or material)
    under = sdf.Part("_under", 0.003)
    shell(under, lumps(B, regions=["torso", "shoulder"]), 0.02)
    under.offset(0.012)
    for s, sx in (("l", 1), ("r", -1)):
        c = V(sx * 0.100, 0.004, 1.330)
        pts, nrm = [], []
        for a in np.radians(np.linspace(18, 162, 11)):
            d = V(0, -np.cos(a), np.sin(a))
            p = surface_along(under, c + d * 0.30, -d, 0.0, 0.30)
            pts.append(p + d * 0.0035)
            nrm.append(d)
        st.add(sdf.ribbon(pts, nrm, 0.021, 0.0045, 0.003), 0.004)
    parts.append(st)
    if pouch:
        po = sdf.Part(name + "Pouches", 0.0022, material)
        for x in (-0.078, -0.026, 0.026, 0.078):
            pp = _front_points(part, [(x, 1.075)])
            if pp:
                po.add(sdf.Box(pp[0] + V(0, -0.010, 0), (0.023, 0.014, 0.040), None, 0.006), 0.004)
                po.add(sdf.Box(pp[0] + V(0, -0.012, 0.038), (0.025, 0.016, 0.008), None, 0.005), 0.003)
        parts.append(po)
    return parts


def parka(B, name="Parka", material="Parka", ease=0.030, hem=0.760, hood=True, trim_mat="Fur", zip_mat="Zip"):
    j = B.j
    part = sdf.Part(name, 0.0032, material)
    shell(part, lumps(B, regions=["torso", "shoulder", "hips"]), 0.03, 0.04)
    _sleeves(B, part, 2.0, 0.010)
    part.offset(ease)
    part.inter(sdf.HalfSpace(V(0, 0, hem), (0, 0, -1)), 0.01)
    for s in ("l", "r"):
        fr = B.frame(s)
        part.sub(limb_cut(fr.p((-0.050, 0, 0)), fr.p((0.4, 0, 0)), 0.09), 0.004)
    nr = B.p["neck"]
    part.sub(sdf.RoundCone(V(0, 0.016, 1.40), V(0, 0.0, 1.64), nr + 0.028, nr + 0.032), 0.01)

    def quilt(P):
        return (0.0035 * np.maximum(np.cos(2 * np.pi * P[..., 2] / 0.085), 0.0) ** 6).astype(np.float32)
    part.displace(quilt, 0.0035)
    parts = [part]
    if hood:
        hd = sdf.Part(name + "Hood", 0.003, material)
        hd.add(sdf.Torus(V(0, 0.030, 1.452), (0, -0.55, 1), nr + 0.045, 0.030))
        hd.add(sdf.Ellipsoid(V(0, 0.095, 1.430), (0.11, 0.05, 0.075), sdf.rot((1, 0, 0), 35)), 0.03)
        parts.append(hd)
        fur = sdf.Part(name + "Fur", 0.0022, trim_mat)
        fur.add(sdf.Torus(V(0, 0.015, 1.462), (0, -0.45, 1), nr + 0.050, 0.018))

        def tufts(P):
            return (0.004 * np.sin(P[..., 0] * 900) * np.sin(P[..., 1] * 870) * np.sin(P[..., 2] * 930)).astype(np.float32)
        fur.displace(tufts, 0.004)
        parts.append(fur)
    z = sdf.Part(name + "Zip", 0.0015, zip_mat)
    pts = _front_points(part, [(0.0, zz) for zz in np.linspace(hem + 0.01, 1.42, 40)], 0.0)
    if len(pts) > 2:
        ys = np.array([p[1] for p in pts])
        ys = np.minimum.accumulate(ys[::-1])[::-1] * 0 + np.array([min(ys[max(0, i - 3):i + 4]) for i in range(len(ys))])
        pts = [V(p[0], y - 0.0015, p[2]) for p, y in zip(pts, ys)]
        z.add(sdf.tube(pts, 0.0045), 0.002)
        parts.append(z)
    pk = sdf.Part(name + "Pockets", 0.003, material)
    for sx in (1, -1):
        pp = _front_points(part, [(sx * 0.095, 0.93)])
        if pp:
            pk.add(sdf.Box(pp[0] + V(0, 0.002, 0), (0.050, 0.010, 0.055), sdf.rot((0, 0, 1), sx * 12), 0.010), 0.006)
            pk.add(sdf.Box(pp[0] + V(0, -0.006, 0.055), (0.054, 0.010, 0.014), sdf.rot((0, 0, 1), sx * 12), 0.006), 0.004)
    parts.append(pk)
    return parts


def apron(B, name="Apron", material="Apron", top=1.000, bottom=0.52, half_w=0.17, bib=False, frill=False, strap_mat=None,
          forward=0.0, bib_forward=0.0):
    """Front panel hanging straight from the waist (+ optional bib and neck strap)."""
    p = sdf.Part(name, 0.0022, material)
    # hang the panel just in front of the most forward point of the body between waist and hem
    front = min(min(pr.lo[1] for pr, k in lumps(B, names=["belly", "waist", "pelvis"])), -0.10)
    axis_y = 0.11
    shift = min(0.0, front - 0.012 - (axis_y - 0.232)) - forward
    axis_y += shift
    outer = sdf.CappedCone(V(0, axis_y, bottom), V(0, axis_y + 0.012, top), 0.262, 0.232)
    inner = sdf.CappedCone(V(0, axis_y, bottom - 0.05), V(0, axis_y + 0.012, top + 0.05), 0.254, 0.224)
    p.add(sdf.Custom(lambda P, o=outer, i=inner, hw=half_w: np.maximum.reduce([
        o.dist(P), -i.dist(P), np.abs(P[..., 0]) - hw, P[..., 1] - 0.0]), (-0.3, -0.35, bottom - 0.02), (0.3, 0.1, top + 0.02)))
    if frill:
        def ruffle(P):
            return (0.0035 * np.sin(np.arctan2(P[..., 0], -(P[..., 1] - axis_y)) * 60.0) * np.clip((bottom + 0.06 - P[..., 2]) / 0.06, 0, 1)).astype(np.float32)
        p.displace(ruffle, 0.0035)
    parts = [p]
    band = sdf.Part(name + "Band", 0.0022, strap_mat or material)
    grp = sdf.Group([pr for pr, k in lumps(B, names=["waist", "belly", "pelvis", "back"])], 0.06)
    band.add(sdf.Intersect([sdf.Displaced(grp, lambda P: np.zeros(P.shape[:-1], np.float32) - 0.022, 0.03), *slab(top - 0.022, top + 0.004)],
                           0.003, box=(grp.lo - 0.05, grp.hi + 0.05)))
    if bib:
        bb = sdf.Part(name + "Bib", 0.0022, material)
        by = 0.07 - bib_forward
        o2 = sdf.CappedCone(V(0, by, top), V(0, by - 0.01, 1.36), 0.205, 0.180)
        i2 = sdf.CappedCone(V(0, by, top - 0.05), V(0, by - 0.01, 1.41), 0.198, 0.173)
        bb.add(sdf.Custom(lambda P, o=o2, i=i2: np.maximum.reduce([o.dist(P), -i.dist(P), np.abs(P[..., 0]) - 0.105, P[..., 1] + 0.02,
                                                                   P[..., 2] - 1.36, top - 0.01 - P[..., 2]]),
                          (-0.2, -0.3, top - 0.02), (0.2, 0.1, 1.38)))
        if frill:
            bb.displace(lambda P: (0.003 * np.sin(P[..., 0] * 400) * np.clip((P[..., 2] - 1.33) / 0.03, 0, 1)).astype(np.float32), 0.003)
        parts.append(bb)
        nr = B.p["neck"]
        band.add(sdf.Torus(V(0, 0.01, 1.44), (0, -0.55, 1), nr + 0.040, 0.0045), 0.003)
    parts.append(band)
    return parts


def dress(B, name="Dress", material="Dress", hem=0.52, flare=1.0, neck="scoop", sleeve=0.0, ease=0.006, waist=1.02,
          straps=False, folds=8):
    """Fitted bodice + flared skirt. neck: scoop | square | v | boat; sleeve 0 = none, 0.3 = cap, 0.5 = short puff."""
    j = B.j
    part = sdf.Part(name, 0.003, material)
    shell(part, lumps(B, regions=["torso", "shoulder"] if sleeve > 0 or not straps else ["torso"]), 0.02)
    if sleeve > 0:
        _sleeves(B, part, sleeve, 0.010)
    part.offset(ease)
    hip = B.p["pelvis"][0] + 0.02
    skirt = sdf.Scaled(sdf.CappedCone(V(0, 0.010, hem), V(0, 0.006, waist + 0.02), (hip + 0.10) * flare, hip - 0.005, 0.02),
                       V(0, 0, waist), np.eye(3), (1.0, 0.82, 1.0))
    part.add(skirt, 0.04)

    def pleats(P):
        ang = np.arctan2(P[..., 0], -P[..., 1])
        grow = np.clip((waist - 0.05 - P[..., 2]) / 0.4, 0, 1)
        return (0.008 * grow * np.sin(ang * folds)).astype(np.float32)
    part.displace(pleats, 0.008, lo=V(-0.5, -0.5, hem - 0.02), hi=V(0.5, 0.5, waist))
    part.inter(sdf.HalfSpace(V(0, 0, hem), (0, 0, -1)), 0.004)
    if sleeve > 0:
        _cut_sleeves(B, part, sleeve)
    elif not straps:
        for s, sx in (("l", 1), ("r", -1)):
            sh = j["shoulder_" + s]
            part.sub(limb_cut(sh + V(sx * 0.03, 0, -0.01), sh + V(sx * 0.6, 0, -0.6), 0.10), 0.01)
    nr = B.p["neck"]
    if neck == "scoop":
        part.sub(sdf.Ellipsoid(V(0, -0.06, 1.42), (0.085, 0.10, 0.075)), 0.01)
    elif neck == "square":
        part.sub(sdf.Box(V(0, -0.08, 1.43), (0.075, 0.08, 0.075), None, 0.012), 0.004)
    elif neck == "v":
        _v_cut(part, 1.29, 0.06)
        _neck_hole(B, part, 0.02)
    elif neck == "boat":
        part.sub(sdf.Ellipsoid(V(0, 0.0, 1.47), (0.16, 0.09, 0.06)), 0.01)
    if straps:
        part.inter(sdf.HalfSpace(V(0, 0, 1.36), (0, 0, 1)), 0.01)
    parts = [part]
    if straps:
        st = sdf.Part(name + "Straps", 0.0015, material)
        for sx in (1, -1):
            a, b2 = V(sx * 0.07, -0.07, 1.345), V(sx * 0.07, 0.07, 1.345)
            top = V(sx * 0.09, 0.0, 1.452)
            st.add(sdf.tube(sdf.bezier(a, V(sx * 0.085, -0.06, 1.46), top, n=5) + sdf.bezier(top, V(sx * 0.085, 0.06, 1.46), b2, n=5)[1:], 0.0042))
        parts.append(st)
    return parts


def skirt_only(B, name="Skirt", material="Skirt", hem=0.50, waist=1.0, flare=0.8, folds=10):
    p = sdf.Part(name, 0.003, material)
    hip = B.p["pelvis"][0] + 0.02
    shell(p, lumps(B, names=["pelvis", "butt", "waist", "belly"]), 0.02)
    p.offset(0.008)
    p.add(sdf.Scaled(sdf.CappedCone(V(0, 0.01, hem), V(0, 0.006, waist), (hip + 0.08) * flare + 0.04, hip, 0.02),
                     V(0, 0, waist), np.eye(3), (1.0, 0.82, 1.0)), 0.04)

    def pleats(P):
        ang = np.arctan2(P[..., 0], -P[..., 1])
        return (0.006 * np.clip((waist - 0.06 - P[..., 2]) / 0.3, 0, 1) * np.sin(ang * folds)).astype(np.float32)
    p.displace(pleats, 0.006)
    p.inter(sdf.Intersect(slab(hem, waist), 0.004, box=((-1, -1, hem - 0.1), (1, 1, waist + 0.1))), 0.004)
    return [p]


# ------------------------------------------------------------------------------------- headwear

def _skull(f):
    cr = f["cranium"]
    return sdf.Ellipsoid(V(0, 0.012, f["skull_top"] - cr[2]), cr)


def hat(B, f, kind="cap", name="Hat", material="Hat", brim_mat=None, band_mat=None):
    p = sdf.Part(name, 0.0022, material)
    sk = _skull(f)
    parts = [p]
    if kind == "cap":           # patrol / baseball cap
        p.add(sdf.Intersect([sdf.Displaced(sk, lambda P: np.zeros(P.shape[:-1], np.float32) - 0.016, 0.02),
                             sdf.HalfSpace(V(0, 0, 1.70), (0.0, -0.25, -1))], 0.004, box=(sk.lo - 0.03, sk.hi + 0.03)))
        v = sdf.Part(name + "Visor", 0.0016, brim_mat or material)
        v.add(sdf.Scaled(sdf.Ring(V(0, -0.050, 1.708), (0, 0.32, 1), 0.095, 0.035, 0.0028, 0.0025), V(0, -0.05, 1.708), np.eye(3), (0.95, 1.0, 1.0)))
        v.inter(sdf.HalfSpace(V(0, -0.105, 0), (0, 1, 0)), 0.004)
        parts.append(v)
        p.add(sdf.Sphere(V(0, 0.012, f["skull_top"] + 0.015), 0.006), 0.003)
    elif kind == "boonie":
        p.add(sdf.Intersect([sdf.Displaced(sk, lambda P: np.zeros(P.shape[:-1], np.float32) - 0.018, 0.02),
                             sdf.HalfSpace(V(0, 0, 1.690), (0, -0.1, -1))], 0.004, box=(sk.lo - 0.03, sk.hi + 0.03)))
        p.add(sdf.Ring(V(0, 0.012, 1.716), (0, -0.08, 1), 0.122, 0.010, 0.0016, 0.0016), 0.004)
        br = sdf.Part(name + "Brim", 0.0018, brim_mat or material)
        br.add(sdf.Ring(V(0, 0.012, 1.696), (0, -0.06, 1), 0.150, 0.040, 0.0022, 0.002))

        parts.append(br)
    elif kind == "beanie":
        p.add(sdf.Intersect([sdf.Displaced(sk, lambda P: np.zeros(P.shape[:-1], np.float32) - 0.017, 0.02),
                             sdf.HalfSpace(V(0, 0, 1.672), (0, -0.2, -1))], 0.006, box=(sk.lo - 0.03, sk.hi + 0.04)))
        p.add(sdf.Sphere(V(0, 0.010, f["skull_top"] + 0.012), 0.032), 0.03)
        p.add(sdf.Ring(V(0, 0.010, 1.690), (0, -0.2, 1), 0.122, 0.010, 0.020, 0.008), 0.005)
        p.displace(_rib_ridges(0.008, 0.0012), 0.0012)
    elif kind == "toque":
        p.add(sdf.CappedCone(V(0, 0.012, 1.680), V(0, 0.018, 1.83), 0.118, 0.122, 0.006))
        p.add(sdf.Ellipsoid(V(0, 0.018, 1.86), (0.150, 0.150, 0.075)), 0.04)

        def pleat(P):
            ang = np.arctan2(P[..., 0], -(P[..., 1] - 0.012))
            return (0.004 * np.maximum(np.cos(ang * 16), 0) * np.clip((P[..., 2] - 1.72) / 0.04, 0, 1)).astype(np.float32)
        p.displace(pleat, 0.004)
        bd = sdf.Part(name + "Band", 0.002, band_mat or material)
        bd.add(sdf.Ring(V(0, 0.012, 1.705), (0, -0.05, 1), 0.121, 0.004, 0.022, 0.003))
        parts.append(bd)
    elif kind == "panama":
        crown = sdf.Ellipsoid(V(0, 0.012, 1.755), (0.118, 0.128, 0.090))
        p.add(sdf.Intersect([crown, sdf.HalfSpace(V(0, 0, 1.80), (0, 0, 1)), sdf.HalfSpace(V(0, 0, 1.69), (0, 0, -1))], 0.012,
                            box=(crown.lo, crown.hi)))
        p.sub(sdf.Ellipsoid(V(0, 0.012, 1.815), (0.05, 0.085, 0.030)), 0.012)
        p.add(sdf.Ring(V(0, 0.012, 1.697), (0, -0.05, 1), 0.165, 0.050, 0.0024, 0.002), 0.006)
        bd = sdf.Part(name + "Band", 0.0018, band_mat or material)
        bd.add(sdf.Ring(V(0, 0.012, 1.712), (0, -0.05, 1), 0.1215, 0.0035, 0.011, 0.002))
        parts.append(bd)
        p.displace(lambda P: (0.0006 * np.sin(P[..., 0] * 900) * np.sin(P[..., 2] * 900)).astype(np.float32), 0.0006)
    elif kind == "headband":   # maid
        p.add(sdf.Torus(V(0, 0.010, 1.745), (0, 0.45, 1), 0.112, 0.006))
        p.add(sdf.Ellipsoid(V(0, -0.050, 1.792), (0.060, 0.018, 0.026), sdf.rot((1, 0, 0), -30)), 0.008)
        p.displace(lambda P: (0.0025 * np.sin(P[..., 0] * 500)).astype(np.float32) * (P[..., 2] > 1.77), 0.0025)
    elif kind == "bandana":
        p.add(sdf.Intersect([sdf.Displaced(sk, lambda P: np.zeros(P.shape[:-1], np.float32) - 0.012, 0.02),
                             *slab(1.69, 1.745)], 0.004, box=(sk.lo - 0.03, sk.hi + 0.03)))
        p.add(sdf.Ellipsoid(V(0.02, 0.135, 1.70), (0.02, 0.014, 0.016)), 0.005)
        p.add(sdf.tube([V(0.02, 0.14, 1.70), V(0.03, 0.16, 1.66), V(0.035, 0.162, 1.63)], [0.008, 0.009, 0.006]), 0.004)
    return parts


# ------------------------------------------------------------------------------------- footwear

def shoes(B, kind="oxford", name="Shoes", upper="Shoes", sole_mat="Sole", lace_mat="Laces"):
    """oxford | loafer | sneaker | flat | winter | work."""
    j = B.j
    up = sdf.Part(name, 0.0020, upper)
    so = sdf.Part(name + "Sole", 0.0020, sole_mat)
    lc = sdf.Part(name + "Laces", 0.0012, lace_mat)
    hi_ = {"oxford": 0.085, "loafer": 0.075, "sneaker": 0.100, "flat": 0.062, "winter": 0.24, "work": 0.17}[kind]
    w = {"oxford": 0.040, "loafer": 0.041, "sneaker": 0.046, "flat": 0.036, "winter": 0.054, "work": 0.048}[kind]
    sole_t = {"oxford": 0.016, "loafer": 0.016, "sneaker": 0.030, "flat": 0.010, "winter": 0.034, "work": 0.032}[kind]
    for s, sx in (("l", 1), ("r", -1)):
        an, toe = j["ankle_" + s], j["toe_" + s]
        heel = V(an[0], an[1] + 0.032, 0.038)
        up.add(sdf.Ellipsoid(heel, (w * 0.92, 0.042, 0.040)))
        up.add(sdf.RoundCone(V(an[0], an[1] + 0.010, 0.050), V(toe[0], toe[1] + 0.050, 0.034), w * 0.98, w * 0.84), 0.03)
        up.add(sdf.Ellipsoid(V(toe[0], toe[1] + 0.022, 0.030 + 0.004 * (kind == "sneaker")),
                             (w * 0.96, 0.048 + 0.006 * (kind == "oxford"), 0.026 + 0.006 * (kind in ("sneaker", "work", "winter")))), 0.025)
        if hi_ > 0.09:
            up.add(sdf.CappedCone(V(an[0], an[1] + 0.006, 0.05), V(an[0], an[1] + 0.004, hi_), w * 1.04, w * 1.00, 0.010), 0.02)
        outline = sdf.Group([sdf.Ellipsoid(heel + V(0, 0.004, 0), (w + 0.003, 0.046, 0.2)),
                             sdf.Ellipsoid(V(toe[0], toe[1] + 0.074, 0.0), (w + 0.006, 0.100, 0.2))], 0.05)
        so.add(sdf.Intersect([outline, *slab(0.0, sole_t)], 0.003, box=(outline.lo, outline.hi)))
        if kind in ("oxford", "sneaker", "work", "winter"):
            for t in np.linspace(0.0, 1.0, 4 if kind == "oxford" else 5):
                p = V(an[0], an[1] - 0.060 + 0.022 * t, 0.058 + (min(hi_, 0.12) - 0.065) * t)
                p0 = surface_along(up, p + V(0, -0.07, 0), (0, 1, 0), 0.0, 0.12)
                lc.add(sdf.Capsule(p0 + V(-0.013, -0.0015, -0.004), p0 + V(0.013, -0.0015, 0.004), 0.0022))
                lc.add(sdf.Capsule(p0 + V(0.013, -0.0015, -0.004), p0 + V(-0.013, -0.0015, 0.004), 0.0022))
        if kind == "loafer":
            p0 = surface_along(up, V(an[0], an[1] - 0.12, 0.10), (0, 0, -1), 0.0, 0.12)
            lc.add(sdf.Capsule(p0 + V(-0.016, 0.004, -0.003), p0 + V(0.016, 0.004, -0.003), 0.0028))
        if kind == "winter":
            up.add(sdf.Torus(V(an[0], an[1] + 0.004, hi_ - 0.006), (0, 0, 1), w * 1.02, 0.014), 0.006)
    # foot opening for low shoes (the trouser hem covers it; keep closed for robustness)
    up.inter(sdf.HalfSpace(V(0, 0, sole_t - 0.006), (0, 0, -1)), 0.003)
    if kind == "flat":
        up.inter(sdf.HalfSpace(V(0, 0, 0.060), (0, 0, 1)), 0.004)
    parts = [up, so]
    if lc.ops:
        parts.append(lc)
    return parts


# ------------------------------------------------------------------------------------- accessories

def bow_tie(B, name="BowTie", material="BowTie", shirt_part=None):
    p = sdf.Part(name, 0.0012, material)
    c = V(0, -0.074, 1.425)
    if shirt_part is not None:
        y = surface_y(shirt_part, 0.0, 1.425)
        if y is not None:
            c = V(0, y - 0.006, 1.425)
    p.add(sdf.Ellipsoid(c, (0.008, 0.007, 0.009)))
    for sx in (1, -1):
        p.add(sdf.Ellipsoid(c + V(sx * 0.024, 0.002, 0), (0.022, 0.006, 0.015), sdf.rot((0, 1, 0), sx * 8)), 0.004)
    return [p]


def neckerchief(B, name="Kerchief", material="Kerchief"):
    nr = B.p["neck"]
    p = sdf.Part(name, 0.002, material)
    p.add(sdf.Torus(V(0, 0.004, 1.438), (0, -0.30, 1), nr + 0.022, 0.012))
    p.add(sdf.Ellipsoid(V(0.0, -0.085, 1.395), (0.022, 0.012, 0.024), sdf.rot((0, 1, 0), 30)), 0.006)
    p.add(sdf.Ellipsoid(V(0.018, -0.090, 1.37), (0.014, 0.008, 0.030), sdf.rot((0, 1, 0), -25)), 0.006)
    return [p]


def shemagh(B, name="Shemagh", material="Shemagh"):
    nr = B.p["neck"]
    p = sdf.Part(name, 0.0025, material)
    p.add(sdf.Torus(V(0, 0.006, 1.450), (0, -0.25, 1), nr + 0.034, 0.030))
    p.add(sdf.Torus(V(0, 0.012, 1.492), (0.05, -0.12, 1), nr + 0.026, 0.024), 0.014)
    p.add(sdf.Ellipsoid(V(0.0, -0.120, 1.380), (0.070, 0.022, 0.060), sdf.rot((1, 0, 0), 10)), 0.03)

    def folds(P):
        return (0.0025 * np.sin(P[..., 2] * 160 + P[..., 0] * 40)).astype(np.float32)
    p.displace(folds, 0.0025)
    return [p]


def watch(B, side="l", name="Watch", material="Watch"):
    fr = B.frame(side)
    p = sdf.Part(name, 0.0012, material)
    p.add(sdf.Scaled(sdf.Ring(fr.p((-0.032, 0, 0)), fr.u, 0.033, 0.0035, 0.009, 0.0025), fr.o, fr.R, (1, 1, 0.86)))
    p.add(sdf.CappedCone(fr.p((-0.032, 0, -0.026)), fr.p((-0.032, 0, -0.034)), 0.016, 0.015, 0.002), 0.002)
    return [p]


def necklace(B, name="Necklace", material="Gold", pendant=True):
    nr = B.p["neck"]
    p = sdf.Part(name, 0.0012, material)
    p.add(sdf.Torus(V(0, -0.006, 1.402), (0, -0.55, 1), nr + 0.030, 0.0022))
    if pendant:
        p.add(sdf.Sphere(V(0, -0.106, 1.352), 0.008), 0.002)
    return [p]


def earrings(B, f, name="Earrings", material="Gold"):
    p = sdf.Part(name, 0.0008, material)
    for sx in (1, -1):
        p.add(sdf.Sphere(V(sx * 0.118, 0.006, 1.555), 0.0055))
    return [p]


def name_tag(B, ref_part, name="Tag", material="Tag", x=-0.065, z=1.30):
    p = sdf.Part(name, 0.0012, material)
    pp = _front_points(ref_part, [(x, z)])
    if pp:
        p.add(sdf.Box(pp[0] + V(0, -0.001, 0), (0.022, 0.003, 0.008), None, 0.0015))
    return [p]
