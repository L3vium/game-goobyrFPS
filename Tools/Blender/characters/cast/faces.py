# Author : Baptiste Vu Liem
"""Facial expressions of the cast as vertex deformations (exported as the head mesh's shape keys:
Blink, Smile, Surprised, Angry, Scared, Pain, Dead; X eyes on death are an eye-texture swap).

Each expression = lid hinge angles (rotation of the lid shells about the eye's lateral axis), brow
moves (inner / outer ends), jaw opening (rotation of the lower face about a jaw pivot) and mouth
corner moves, all with smooth falloffs. deform() works on any vertex array given each vertex's part
name, so it serves the look-dev renders (apply) and the exported shape keys alike.
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
SHAPE_KEYS = ["Blink", "Smile", "Surprised", "Angry", "Scared", "Pain", "Dead"]
FACE_PARTS = ("Head", "Beard", "Brows", "LidUp_l", "LidUp_r", "LidLow_l", "LidLow_r", "Lash_l", "Lash_r")


def _rot_x(P, centre, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    q = P - centre
    y = q[:, 1] * c - q[:, 2] * s
    z = q[:, 1] * s + q[:, 2] * c
    return np.stack([q[:, 0], y, z], axis=1) + centre


def deform(P, part_of, f, kind):
    """New positions for vertices P (N,3) whose part names are part_of (N,) for expression `kind`."""
    e = EXPRESSIONS[kind]
    P = np.asarray(P, dtype=np.float64).copy()
    part_of = np.asarray(part_of)
    centres = head.eye_centres(f)
    for side, c in (("l", centres[0]), ("r", centres[1])):
        m = (part_of == "LidUp_" + side) | (part_of == "Lash_" + side)
        if m.any():
            ang = (f["lid_up"] - f["lid_low"] - 3.0) if e["up"] is None else e["up"]
            P[m] = _rot_x(P[m], c, ang)
        m = part_of == "LidLow_" + side
        if m.any() and e["low"]:
            P[m] = _rot_x(P[m], c, -e["low"])
    m = part_of == "Brows"
    if m.any():
        Q = P[m]
        ax = np.abs(Q[:, 0])
        t = np.clip((ax - 0.0105) / 0.055, 0, 1)
        di, do, dx = e["brow"]
        Q[:, 2] += di * (1 - t) + do * t
        Q[:, 0] -= np.sign(Q[:, 0]) * dx * (1 - t)
        Q[:, 1] -= 0.002 * (abs(di) > 0.005) * (1 - t)
        P[m] = Q
    m = (part_of == "Head") | (part_of == "Beard")
    if m.any():
        P[m] = _deform_face(P[m], e)
    return P


def _deform_face(P, e):
    P = P.copy()
    if e["jaw"]:
        pivot = V(0, 0.000, 1.552)
        below = np.clip((1.5335 - P[:, 2]) / 0.004, 0, 1)
        front = np.clip((0.035 - P[:, 1]) / 0.05, 0, 1)
        neck = np.clip((P[:, 2] - 1.455) / 0.04, 0, 1)
        side = np.clip((0.085 - np.abs(P[:, 0])) / 0.03, 0, 1)
        w = below * front * neck * side
        Q = _rot_x(P, pivot, e["jaw"])
        P = P + (Q - P) * w[:, None]
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


# ------------------------------------------------------------------------------------- look-dev (bpy)

_REST = {}


def _get(ob):
    co = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3).astype(np.float64)


def apply(objs, f, kind):
    """Deform separate part objects (dict name -> object) to `kind` from their rest coordinates."""
    for name, ob in objs.items():
        key = ob.as_pointer()
        if key not in _REST or len(_REST[key]) != len(ob.data.vertices):
            _REST[key] = _get(ob)
        P = _REST[key]
        Q = deform(P, np.full(len(P), name), f, kind)
        ob.data.vertices.foreach_set("co", Q.astype(np.float32).ravel())
        ob.data.update()
