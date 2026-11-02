# Author : Baptiste Vu Liem
"""Facial expressions of the cast as vertex deformations of the head parts (the step-2 shape keys:
Blink, Smile, Surprised, Angry, Scared, Pain; Dead = X pupils via the eye texture + slack face).

Each expression = lid hinge angles (rotation of the lid shells about the eye's lateral axis),
brow moves (inner/outer ends), jaw opening (rotation of the lower face about a jaw pivot) and mouth
corner moves, all with smooth falloffs. apply() deforms the objects in place from their rest
coordinates (kept in a custom attribute) so expressions can be swapped freely.
"""
import math

import numpy as np

import head

V = lambda *a: np.array(a, dtype=np.float64)

EXPRESSIONS = {
    #             upper lid, lower lid, brow (inner dz, outer dz, inner dx), jaw deg, corners (dz, dx_in, dy)
    "Neutral":   dict(up=0, low=0, brow=(0, 0, 0), jaw=0, corner=(0, 0, 0), cheek=0),
    "Blink":     dict(up=None, low=-3, brow=(-0.001, -0.001, 0), jaw=0, corner=(0, 0, 0), cheek=0),
    "Smile":     dict(up=7, low=-9, brow=(0.002, 0.002, 0), jaw=2.5, corner=(0.0055, -0.002, 0.003), cheek=0.0035),
    "Surprised": dict(up=-11, low=5, brow=(0.0075, 0.0060, 0), jaw=11, corner=(0.0, 0.0045, 0.0), cheek=0),
    "Angry":     dict(up=11, low=-4, brow=(-0.0055, 0.0015, 0.0035), jaw=1.5, corner=(-0.0035, 0.0, 0.0), cheek=0),
    "Scared":    dict(up=-9, low=4, brow=(0.0070, -0.0010, 0.0020), jaw=7, corner=(-0.0035, -0.002, 0.0), cheek=0),
    "Pain":      dict(up=34, low=-12, brow=(0.0045, -0.0025, 0.0030), jaw=7, corner=(-0.0030, -0.004, 0.002), cheek=0.002),
    "Dead":      dict(up=16, low=0, brow=(0.0, -0.001, 0), jaw=6, corner=(-0.002, 0.0, 0.0), cheek=0),
}


def _rot_x(P, centre, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    q = P - centre
    y = q[:, 1] * c - q[:, 2] * s
    z = q[:, 1] * s + q[:, 2] * c
    return np.stack([q[:, 0], y, z], axis=1) + centre


def _get(ob):
    co = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3).astype(np.float64)


def _set(ob, P):
    ob.data.vertices.foreach_set("co", P.astype(np.float32).ravel())
    ob.data.update()


_REST = {}


def remember(objs):
    for ob in objs:
        key = ob.as_pointer()
        if key not in _REST or len(_REST[key]) != len(ob.data.vertices):
            _REST[key] = _get(ob)


def rest(ob):
    return _REST[ob.as_pointer()].copy()


def apply(objs, f, kind):
    """Deform head / lids / lashes / brows objects (dict name -> object) to the expression `kind`."""
    e = EXPRESSIONS[kind]
    remember(objs.values())
    centres = head.eye_centres(f)
    for name, ob in objs.items():
        P = rest(ob)
        if name.startswith(("LidUp", "Lash")):
            c = centres[0] if name.endswith("_l") else centres[1]
            ang = (f["lid_up"] - f["lid_low"] - 3.0) if e["up"] is None else e["up"]
            P = _rot_x(P, c, ang)
        elif name.startswith("LidLow"):
            c = centres[0] if name.endswith("_l") else centres[1]
            P = _rot_x(P, c, -e["low"]) if e["low"] else P
        elif name == "Brows":
            ax = np.abs(P[:, 0])
            t = np.clip((ax - 0.0105) / 0.055, 0, 1)
            di, do, dx = e["brow"]
            P[:, 2] += di * (1 - t) + do * t
            P[:, 0] -= np.sign(P[:, 0]) * dx * (1 - t)
            P[:, 1] -= 0.002 * (abs(di) > 0.005) * (1 - t)
        elif name in ("Head", "Beard"):
            P = _deform_face(P, e, f)
        _set(ob, P)


def _deform_face(P, e, f):
    P = P.copy()
    # jaw: everything under the lip line in the front of the face rotates down about the jaw pivot
    if e["jaw"]:
        pivot = V(0, 0.000, 1.552)
        below = np.clip((1.5335 - P[:, 2]) / 0.004, 0, 1)
        front = np.clip((0.035 - P[:, 1]) / 0.05, 0, 1)
        neck = np.clip((P[:, 2] - 1.455) / 0.04, 0, 1)
        side = np.clip((0.085 - np.abs(P[:, 0])) / 0.03, 0, 1)
        w = below * front * neck * side
        Q = _rot_x(P, pivot, e["jaw"])
        P = P + (Q - P) * w[:, None]
    # mouth corners
    dz, dxin, dy = e["corner"]
    if dz or dxin or dy:
        for sx in (1, -1):
            c = V(sx * 0.0265, -0.107, 1.5335)
            d = np.linalg.norm((P - c) / V(0.016, 0.02, 0.012), axis=1)
            w = np.exp(-d * d * 1.6)
            P[:, 2] += dz * w
            P[:, 0] -= sx * dxin * w
            P[:, 1] += dy * w
    if e["cheek"]:
        for sx in (1, -1):
            c = V(sx * 0.047, -0.088, 1.565)
            d = np.linalg.norm(P - c, axis=1) / 0.026
            w = np.exp(-d * d * 1.5)
            P[:, 2] += e["cheek"] * w
            P[:, 1] -= e["cheek"] * 0.4 * w
    return P
