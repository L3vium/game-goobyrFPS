# Author : Baptiste Vu Liem
"""Builds the first-person arms: rig, mesh, every authored pose and clip, the pose manifest.

    blender -b --factory-startup --python Tools/Blender/fp/fp_build.py -- [--nobake] [--preview] [--only Pistol,Fists]

Weapon-first authoring, one family at a time:
  1. the weapon is placed by its Sight: on the camera axis when aimed (ADS), at a hip / sprint spot
     otherwise (positions and turns given in Unity camera space: x right, y up, z forward);
  2. each hand is put on the real geometry of what it holds - the handle, fore-end, pump or vertical
     grip described as a cylinder in the weapon's frame (centre, axis, palm side) -, the arms reach it
     by two-bone IK (elbows towards a pole, twist shared down the forearm);
  3. the fingers close joint by joint until their pads touch the mesh (trigger finger on the trigger
     when aimed, indexed along the frame otherwise), the thumb lies where the grip style wants it;
  4. the socket bone weapon_r is keyed equal to the weapon's Grip frame, so Unity parents the model
     there at identity and it lands where it was authored.
Static poses are 2-frame actions <Family>_<Hip|ADS|Sprint>; clips (Jab, Cross, Takedown, reloads,
throws) are keyframed hand paths solved and baked at 30 fps. Output (staging, see fp_common.OUT):
FP_Arms.fbx (mesh, rig, every action as a take), the baked textures, fp_poses.json (socket and Sight
per pose in Unity camera space, clip events) and previews.
"""
import math
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

import fp_common as F  # noqa: E402
import fp_rig as R  # noqa: E402
import fp_solve as S  # noqa: E402
from fp_common import L  # noqa: E402

G_INV = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))  # inverse of the FBX axis conversion (Y-up, -Z forward)


def socket_matrix(world):
    """The socket bone's armature matrix for a frame that should come out as `world` in Unity
    (bone nodes keep Blender axes under the converted armature node; a model parented under the bone
    at identity carries baked axes, so the bone is the frame times the inverse conversion)."""
    m = world.to_3x3() @ G_INV
    out = m.to_4x4()
    out.translation = world.translation
    return out


# ------------------------------------------------------------------ grips
def grip_hand(side, W, g):
    """Hand bone matrix for a grip on a cylinder given in the weapon frame:
    g = dict(c=centre on the axis at the hand's middle, axis=towards the index/thumb side,
             palm=from the palm towards the axis, depth=hand-bone-to-axis distance along the palm normal,
             reach=wrist-to-axis distance along the fingers, slide=shift along the axis,
             twist=(deg about F, deg about T, deg about P) fine turns about the grip point)."""
    Wr = W.to_3x3()
    if "fingers" in g:
        # pistol-style grip: the metacarpals run along the given direction (the barrel), the palm
        # faces the handle, the raked handle crosses the palm diagonally
        p = (Wr @ Vector(g["palm"])).normalized()
        f = Wr @ Vector(g["fingers"])
        f = (f - p * f.dot(p)).normalized()
        t = f.cross(p) if side == "r" else p.cross(f)
    else:
        t = (Wr @ Vector(g["axis"])).normalized()
        p = Wr @ Vector(g["palm"])
        p = (p - t * p.dot(t)).normalized()
        f = p.cross(t) if side == "r" else t.cross(p)
    c = W @ Vector(g["c"])
    rot = Matrix((f.cross(p), f, p)).transposed()  # columns X, Y (fingers), Z (palm normal)
    tw = g.get("twist", (0, 0, 0))
    turn = (Matrix.Rotation(math.radians(tw[0]), 3, f) @ Matrix.Rotation(math.radians(tw[1]), 3, t)
            @ Matrix.Rotation(math.radians(tw[2]), 3, p))
    rot = turn @ rot
    f, p, t = rot.col[1], rot.col[2], (rot.col[1].cross(rot.col[2]) if side == "r" else rot.col[2].cross(rot.col[1]))
    wrist = c - f * g.get("reach", 0.062) - p * g.get("depth", 0.04) - t * g.get("slide", 0.0)
    m = rot.to_4x4()
    m.translation = wrist
    return m


# Grips per weapon, in its frame (Blender: Grip at the origin, barrel -Y, up +Z, left +X).
UP_PISTOL = (0.0, -0.287, 0.958)
GRIPS = {
    "W_Pistol": dict(
        # metacarpals along the barrel tipped 8 deg down, palm on the right panel, web under the tang
        r=dict(c=(0.0, -0.0153, 0.028), fingers=(0.0, -0.99, -0.14), palm=(1.0, -0.12, 0.0), depth=0.037,
               reach=0.066, slide=-0.004),
        # support: palm on the left panel over the firing fingertips, its knuckle line up the front strap so
        # the fingers wrap the firing fingers below the guard, the thumb up the grip to reach forward
        l=dict(c=(0.0, 0.004, 0.0), axis=UP_PISTOL, palm=(-1.0, 0.0, 0.0), depth=0.038, reach=0.051),
        # thumbs forward on the left of the frame, the firing thumb resting over the support thumb
        thumb_r=dict(swing=(0, 0), start=(20, 0, 0), target=(0.034, -0.050, 0.064)),
        thumb_l=dict(swing=(0, 0), start=(10, 0, 0), target=(0.032, -0.085, 0.040)),
        trigger=(0.0, -0.070, 0.056)),
    "W_Smg": dict(
        r=dict(c=(0.0, -0.0095, 0.019), axis=(0.0, -0.166, 0.986), palm=(1.0, -0.35, 0.0), depth=0.041,
               reach=0.060),
        l=dict(c=(0.0, -0.28, 0.008), axis=(0.0, -0.10, 0.995), palm=(-1.0, 0.3, 0.0), depth=0.040, reach=0.060),
        thumb_r=dict(swing=(0, 0), start=(20, 10, 10), target=(0.028, -0.020, 0.022)),
        # the support thumb wraps the front of the magazine/grip on the right instead of standing up
        thumb_l=dict(swing=(0, 0), start=(20, 10, 10), target=(-0.020, -0.295, 0.030)),
        trigger=(0.0, -0.068, 0.050)),
    "W_Shotgun": dict(
        # the straight stock wrist crosses the palm diagonally: metacarpals forward-down, thumb round onto the left side (low: the big glove sits a hand-width from the eye in ADS)
        r=dict(c=(0.0, -0.02, 0.008), fingers=(0.0, -0.78, -0.62), palm=(1.0, 0.0, 0.0), depth=0.039, reach=0.060),
        l=dict(c=(0.0, -0.355, 0.028), axis=(0.0, -1.0, 0.0), palm=(0.15, 0.0, 1.0), depth=0.045, reach=0.060),
        thumb_r=dict(swing=(0, 0), start=(20, 10, 10), target=(0.034, -0.035, 0.020)), thumb_l=dict(swing=(5, 0), start=(10, 10, 10)),
        trigger=(0.0, -0.136, -0.010)),
    "W_Sniper": dict(
        r=dict(c=(0.0, -0.01, 0.035), fingers=(0.0, -0.85, -0.53), palm=(1.0, -0.1, 0.0), depth=0.039, reach=0.062),
        l=dict(c=(0.0, -0.31, 0.095), axis=(0.0, -1.0, 0.0), palm=(0.15, 0.0, 1.0), depth=0.042, reach=0.058),
        thumb_r=dict(swing=(0, 0), start=(20, 10, 10), target=(0.026, -0.03, 0.060)), thumb_l=dict(swing=(5, 0), start=(10, 10, 10)),
        trigger=(0.0, -0.07, 0.04)),
    "W_RocketLauncher": dict(
        r=dict(c=(0.0, 0.0317, -0.022), axis=(0.0, -0.158, 0.987), palm=(1.0, -0.35, 0.0), depth=0.040,
               reach=0.060),
        l=dict(c=(0.0, -0.125, 0.08), axis=(0.0, -1.0, 0.0), palm=(0.25, 0.0, 1.0), depth=0.057, reach=0.058),
        thumb_r=dict(swing=(0, 0), start=(20, 10, 10), target=(0.026, 0.02, -0.005)), thumb_l=dict(swing=(5, 0), start=(10, 10, 10)),
        trigger=(0.0, -0.005, 0.030)),
    "W_Flamethrower": dict(
        r=dict(c=(0.0, 0.0038, -0.013), axis=(0.0, -0.146, 0.989), palm=(1.0, -0.35, 0.0), depth=0.041,
               reach=0.060),
        l=dict(c=(0.0, -0.255, 0.01), axis=(0.0, 0.0, 1.0), palm=(-1.0, 0.3, 0.0), depth=0.038, reach=0.060),
        thumb_r=dict(swing=(0, 0), start=(20, 10, 10), target=(0.030, 0.022, 0.022)), thumb_l=dict(swing=(-10, 20), start=(20, 25, 20)),
        trigger=(0.0, -0.061, 0.030)),
}

# Families: the weapon, its static poses (Sight position in Unity camera space + pitch/yaw/roll),
# body set-up (chest yaw, elbow poles in Unity space), which hands hold it.
FAMILIES = {
    "Pistol": dict(weapon="W_Pistol", hands="both", poses=dict(
        Hip=dict(sight=(0.07, -0.105, 0.40), turn=(4, -5, -7), chest=0, trig="frame", clav=(12, 4),
                 pole_r=(1.0, -0.6, -0.1), pole_l=(-1.0, -0.6, 0.0)),
        # wrists in a V under the gun, elbows down and out: the sleeves leave by the bottom edge
        ADS=dict(sight=(0.0, 0.0, 0.43), turn=(0, 0, 0), chest=0, trig="trigger", clav=(18, 7),
                 pole_r=(0.45, -1.0, -0.1), pole_l=(-0.45, -1.0, 0.0)),
        Sprint=dict(sight=(0.12, -0.25, 0.32), turn=(38, -22, -35), chest=-4, trig="frame", left="free"))),
    "Smg": dict(weapon="W_Smg", hands="both", poses=dict(
        Hip=dict(sight=(0.105, -0.088, 0.26), turn=(2, -5, -7), chest=14, trig="frame", clav_l=(18, 6)),
        ADS=dict(sight=(0.0, 0.0, 0.15), turn=(0, 0, 0), chest=12, trig="trigger"),
        Sprint=dict(sight=(0.07, -0.20, 0.20), turn=(30, -38, -28), chest=14, trig="frame"))),
    "Shotgun": dict(weapon="W_Shotgun", hands="both", poses=dict(
        Hip=dict(sight=(0.11, -0.092, 0.24), turn=(2, -5, -7), chest=22, trig="frame", clav_l=(24, 6)),
        ADS=dict(sight=(0.0, 0.0, 0.17), turn=(0, 0, 0), chest=22, trig="trigger", clav_l=(24, 6)),
        Sprint=dict(sight=(0.06, -0.21, 0.18), turn=(28, -38, -28), chest=16, trig="frame"))),
    "Sniper": dict(weapon="W_Sniper", hands="both", poses=dict(
        Hip=dict(sight=(0.11, -0.088, 0.22), turn=(2, -5, -7), chest=22, trig="frame", clav_l=(24, 6)),
        ADS=dict(sight=(0.0, 0.0, 0.09), turn=(0, 0, 0), chest=22, trig="trigger", clav_l=(24, 6)),
        Sprint=dict(sight=(0.06, -0.20, 0.17), turn=(28, -38, -28), chest=16, trig="frame"))),
    "Launcher": dict(weapon="W_RocketLauncher", hands="both", poses=dict(
        Hip=dict(sight=(0.13, -0.06, 0.18), turn=(2, -3, -4), chest=14, trig="frame"),
        ADS=dict(sight=(0.0, 0.0, 0.22), turn=(0, 0, 0), chest=16, trig="trigger"),
        Sprint=dict(sight=(0.10, -0.14, 0.12), turn=(20, -30, -22), chest=16, trig="frame"))),
    "Flamer": dict(weapon="W_Flamethrower", hands="both", poses=dict(
        Hip=dict(sight=(0.10, -0.08, 0.24), turn=(2, -5, -7), chest=18, trig="frame", clav_l=(22, 6)),
        ADS=dict(sight=(0.0, 0.0, 0.18), turn=(0, 0, 0), chest=18, trig="trigger", clav_l=(22, 6)),
        Sprint=dict(sight=(0.07, -0.16, 0.22), turn=(28, -38, -28), chest=14, trig="frame"))),
}

POLES = dict(r=(0.55, -1.0, -0.15), l=(-0.55, -1.0, -0.05))  # elbows down and out (Unity space)

# ------------------------------------------------------------------ free hands (fists, items)
# A hand: knuckles = middle knuckle position, fwd = metacarpal direction, palm = palm normal (Unity
# space); grip = finger curls (index, paddle) and thumb (abduction, roll, curls).
FIST = dict(index=(92, 106, 62), paddle=(96, 108, 64), thumb=(38, 30, 28, 42, 34), tuck=True)
OPEN = dict(index=(6, 8, 4), paddle=(8, 10, 6), thumb=(-10, 0, 6, 8, 6))
CLAW = dict(index=(40, 55, 35), paddle=(45, 60, 40), thumb=(20, 20, 20, 25, 20))
PINCH = dict(index=(48, 62, 30), paddle=(70, 88, 52), thumb=(18, 32, 30, 22, 16), key=True)
CUP = dict(index=(30, 38, 22), paddle=(36, 44, 26), thumb=(10, 20, 24, 18, 12))
WRAP = dict(index=(58, 66, 38), paddle=(64, 72, 42), thumb=(-6, 18, 34, 16, 10))
LOOSE = dict(index=(25, 35, 20), paddle=(32, 44, 26), thumb=(8, 10, 12, 16, 10))

GUARD_R = dict(knuckles=(0.115, -0.080, 0.34), fwd=(-0.18, 0.28, 1.0), palm=(-0.72, -0.55, 0.0), grip=FIST,
               pole=(0.35, -1.0, -0.25))
GUARD_L = dict(knuckles=(-0.090, -0.065, 0.39), fwd=(0.14, 0.26, 1.0), palm=(0.72, -0.55, 0.0), grip=FIST,
               pole=(-0.35, -1.0, -0.25))
LEFT_DOWN = dict(knuckles=(-0.20, -0.60, 0.20), fwd=(0.1, -1.0, 0.25), palm=(1.0, 0.0, 0.0), grip=LOOSE,
                 pole=(-0.8, -0.3, -0.6))

HAND_FAMILIES = {
    "Fists": dict(item=None, poses=dict(
        Hip=dict(r=GUARD_R, l=GUARD_L, clav=(6, 10)),
        ADS=dict(r=GUARD_R, l=GUARD_L, clav=(6, 10)),
        Sprint=dict(r=dict(GUARD_R, knuckles=(0.15, -0.30, 0.24), fwd=(-0.1, 0.9, 1.0), palm=(-1.0, -0.1, 0.0)),
                    l=dict(GUARD_L, knuckles=(-0.15, -0.29, 0.27), fwd=(0.1, 0.9, 1.0), palm=(1.0, -0.1, 0.0)),
                    clav=(0, 4)))),
    # coin, cards, notes, vials: between the thumb and the index
    "Pinch": dict(item="pinch", poses=dict(
        Hip=dict(r=dict(knuckles=(0.115, -0.115, 0.34), fwd=(-0.35, 0.45, 1.0), palm=(-0.55, 0.8, 0.0),
                        grip=PINCH, pole=(0.6, -1.0, -0.2)), l=LEFT_DOWN, clav=(4, 2)),
        ADS=dict(r=dict(knuckles=(0.17, 0.0, 0.20), fwd=(-0.2, 0.9, 0.5), palm=(-0.6, 0.0, 0.8),
                        grip=PINCH, pole=(1.0, -0.6, -0.4)),
                 l=dict(knuckles=(-0.07, -0.10, 0.44), fwd=(0.25, 0.15, 1.0), palm=(0.7, -0.7, 0.0), grip=OPEN,
                        pole=(-0.5, -1.0, -0.2)), chest=-6, clav_r=(-8, 14)),
        Sprint=dict(r=dict(knuckles=(0.15, -0.30, 0.24), fwd=(-0.2, 0.8, 1.0), palm=(-1.0, 0.0, 0.0),
                           grip=PINCH, pole=(0.6, -1.0, -0.3)), l=LEFT_DOWN))),
    # rocks, bricks, C4, smoke, remote: held in the palm, fingers round it
    "Palm": dict(item="palm", poses=dict(
        Hip=dict(r=dict(knuckles=(0.125, -0.115, 0.37), fwd=(-0.25, 0.35, 1.0), palm=(-1.0, 0.25, 0.0),
                        grip=WRAP, pole=(0.6, -1.0, -0.2)), l=LEFT_DOWN, clav=(4, 2)),
        ADS=dict(r=dict(knuckles=(0.17, 0.0, 0.20), fwd=(-0.2, 0.9, 0.5), palm=(-0.6, 0.0, 0.8),
                        grip=WRAP, pole=(1.0, -0.6, -0.4)),
                 l=dict(knuckles=(-0.07, -0.10, 0.44), fwd=(0.25, 0.15, 1.0), palm=(0.7, -0.7, 0.0), grip=OPEN,
                        pole=(-0.5, -1.0, -0.2)), chest=-6, clav_r=(-8, 14)),
        Sprint=dict(r=dict(knuckles=(0.15, -0.30, 0.24), fwd=(-0.2, 0.8, 1.0), palm=(-1.0, 0.0, 0.0),
                           grip=WRAP, pole=(0.6, -1.0, -0.3)), l=LEFT_DOWN))),
    # wrench, crowbar, bottle by the neck: a hammer grip, the tool rising from the thumb side
    "Handle": dict(item="handle", poses=dict(
        Hip=dict(r=dict(knuckles=(0.13, -0.15, 0.34), fwd=(-0.25, -0.55, 1.0), palm=(-1.0, -0.1, -0.1),
                        grip=FIST, pole=(0.6, -1.0, -0.2)), l=LEFT_DOWN, clav=(4, 2)),
        ADS=dict(r=dict(knuckles=(0.17, 0.0, 0.20), fwd=(-0.2, 0.6, 0.8), palm=(-1.0, 0.0, 0.2),
                        grip=FIST, pole=(1.0, -0.6, -0.4)),
                 l=dict(knuckles=(-0.07, -0.10, 0.44), fwd=(0.25, 0.15, 1.0), palm=(0.7, -0.7, 0.0), grip=OPEN,
                        pole=(-0.5, -1.0, -0.2)), chest=-6, clav_r=(-8, 14)),
        Sprint=dict(r=dict(knuckles=(0.15, -0.30, 0.24), fwd=(-0.2, 0.8, 1.0), palm=(-1.0, 0.0, 0.0),
                           grip=FIST, pole=(0.6, -1.0, -0.3)), l=LEFT_DOWN))),
}


def hand_rot(fwd_u, palm_u):
    """Hand bone rotation from a metacarpal direction and a palm normal given in Unity space."""
    f = F.u2b(fwd_u).normalized()
    p = F.u2b(palm_u)
    p = (p - f * p.dot(f)).normalized()
    return Matrix((f.cross(p), f, p)).transposed()


def place_hand(P, side, h):
    """Free hand: wrist from the knuckle target, IK, then the finger set."""
    rot = hand_rot(h["fwd"], h["palm"])
    knuck = F.u2b(h["knuckles"])
    wrist = knuck - rot.col[1] * 0.095
    P.arm_ik(side, wrist, rot, F.u2b(h.get("pole", POLES[side])))
    set_grip(P, side, h.get("grip", OPEN))


class _NoMesh:
    """A BVH stand-in with nothing in it (thumb reaches with no obstacle)."""
    @staticmethod
    def find_nearest(p):
        return None, None, None, None


def set_grip(P, side, g):
    """Finger curls, then the thumb: its raw angles, or (weights 'tuck' / 'key', 0..1, blended so
    clips never pop) the fist tuck across the fingers or the key pinch against the index."""
    P.finger_set("index", side, g["index"])
    for name in R.PADDLE:
        P.finger_set(name, side, g["paddle"])
    raw = list(g["thumb"])
    w_tuck, w_key = float(g.get("tuck", 0.0)), float(g.get("key", 0.0))
    target = None
    if w_tuck > 0.01:
        # fist: the thumb pad across the middle phalanges of the index and the paddle
        h1, t1, _ = P.segment(f"index_02_{side}")
        h2, t2, _ = P.segment(f"middle_02_{side}")
        m = (h1.lerp(t1, 0.5) + h2.lerp(t2, 0.5)) * 0.5
        hm = P.get("hand_" + side)
        out = (m - (hm.translation + hm.to_3x3().col[1] * 0.05)).normalized()
        # outside the phalanges (finger + thumb radius), across their front, not sunk in
        target, w = m + out * (R.FINGER_SPEC["index"]["r"][1] + R.THUMB_SPEC["r"][1] + 0.003), w_tuck
    elif w_key > 0.01:
        # key pinch: the thumb pad presses the item on the thumb side of the index's middle phalanx
        target, w = key_point(P, side, 0.004 + R.THUMB_SPEC["r"][1] * 0.6), w_key
    if target is not None:
        _, sol = P.reach("thumb", side, target, _NoMesh, start=raw, iters=40)
        raw = [a + (b - a) * w for a, b in zip(raw, sol)]
    P.thumb_swing(side, raw[0], raw[1])
    P.thumb_set(side, raw[2:5])


def key_point(P, side, out):
    """A point beside the index's middle phalanx, `out` metres towards the thumb side."""
    h, t, m = P.segment(f"index_02_{side}")
    hm = P.get("hand_" + side).to_3x3()
    ts = hm.col[1].cross(hm.col[2]) if side == "r" else hm.col[2].cross(hm.col[1])
    return h.lerp(t, 0.55) + ts.normalized() * (R.FINGER_SPEC["index"]["r"][0] + out)


def lerp_grip(a, b, k):
    return {key: tuple(x + (y - x) * k for x, y in zip(a[key], b[key])) for key in ("index", "paddle", "thumb")}


# Where an item sits in the right hand (hand-bone space: f along the fingers, t thumb side, p palm side),
# as the socket's frame: the item's Grip goes there with its +Z along `up` and its -Y along `fwd`.
ITEM_SOCKETS = {
    "pinch": dict(at=(0.122, 0.040, 0.030), fwd=(0.2, 0.9, 0.3), up=(0.0, 0.0, 1.0)),
    "palm": dict(at=(0.066, 0.002, 0.052), fwd=(1.0, 0.0, 0.0), up=(0.0, 1.0, 0.0)),
    "handle": dict(at=(0.068, 0.004, 0.036), fwd=(0.0, 1.0, 0.0), up=(-1.0, 0.0, 0.0)),
    None: dict(at=(0.07, 0.0, 0.035), fwd=(1.0, 0.0, 0.0), up=(0.0, 0.0, 1.0)),
}


def item_socket(P, kind):
    """Socket frame for an item in the right hand (Blender scene space)."""
    s = ITEM_SOCKETS[kind]
    hm = P.get("hand_r")
    rot = hm.to_3x3()
    f, p = rot.col[1], rot.col[2]
    t = f.cross(p)  # right hand: thumb side
    if kind == "pinch":
        # flat items between the side of the index and the thumb pad: face normal towards the thumb
        h, tl, _ = P.segment("index_02_r")
        return F.frame(F.look_frame((tl - h).normalized(), t), key_point(P, "r", 0.002))
    at = hm.translation + f * s["at"][0] + t * s["at"][1] + p * s["at"][2]

    def hv(v):
        return (f * v[0] + t * v[1] + p * v[2]).normalized()
    W = F.frame(F.look_frame(hv(s["fwd"]), hv(s["up"])), at)
    return W


def _h(base, **kw):
    d = dict(base)
    d.update(kw)
    return d


GUARD = dict(r=GUARD_R, l=GUARD_L, chest=0.0, clav_r=(6, 10), clav_l=(6, 10))

# Authored clips: keys of hands (knuckles / fwd / palm / grip / pole), chest yaw and shoulders.
# Times in seconds; Catmull-Rom through the keys gives the arcs, extra keys give the anticipation,
# the overshoot ("goofy") and the settle. `events` are written to the manifest (strike = contact).
CLIPS = {
    # read from the camera: the anticipation pulls the fist back 5 cm, it travels to the screen centre at
    # eye level turning palm-down (a slight inward corkscrew), the other fist stays up as the guard
    "Jab": dict(family="Fists", length=0.50, events=dict(strike=0.16), keys=[
        (0.00, GUARD),
        (0.05, dict(l=_h(GUARD_L, knuckles=(-0.098, -0.075, 0.34)), chest=-4.0)),
        (0.12, dict(l=_h(GUARD_L, knuckles=(-0.060, -0.045, 0.45), fwd=(0.10, 0.10, 1.0), palm=(0.35, -1.0, 0.0)),
                    r=_h(GUARD_R, knuckles=(0.118, -0.085, 0.33)), chest=8.0, clav_l=(18, 8))),
        (0.16, dict(l=_h(GUARD_L, knuckles=(-0.030, -0.030, 0.50), fwd=(0.07, 0.05, 1.0), palm=(0.06, -1.0, 0.0)),
                    chest=12.0, clav_l=(28, 8))),
        (0.20, dict(l=_h(GUARD_L, knuckles=(-0.027, -0.028, 0.51), fwd=(0.06, 0.05, 1.0), palm=(0.04, -1.0, 0.0)),
                    chest=13.0, clav_l=(29, 8))),
        (0.27, dict(l=_h(GUARD_L, knuckles=(-0.055, -0.050, 0.45), fwd=(0.12, 0.15, 1.0), palm=(0.5, -0.9, 0.0)),
                    r=GUARD_R, chest=6.0, clav_l=(14, 8))),
        (0.40, dict(l=_h(GUARD_L, knuckles=(-0.092, -0.068, 0.385)), chest=-1.0, clav_l=(6, 10))),
        (0.50, GUARD),
    ]),
    "Cross": dict(family="Fists", length=0.56, events=dict(strike=0.18), keys=[
        (0.00, GUARD),
        (0.06, dict(r=_h(GUARD_R, knuckles=(0.125, -0.090, 0.295)), chest=4.0)),
        (0.14, dict(r=_h(GUARD_R, knuckles=(0.070, -0.050, 0.44), fwd=(-0.06, 0.10, 1.0), palm=(-0.35, -1.0, 0.0)),
                    l=_h(GUARD_L, knuckles=(-0.095, -0.070, 0.37)), chest=-14.0, clav_r=(20, 6))),
        (0.18, dict(r=_h(GUARD_R, knuckles=(0.025, -0.032, 0.50), fwd=(-0.05, 0.05, 1.0), palm=(-0.05, -1.0, 0.0)),
                    chest=-20.0, clav_r=(30, 6))),
        (0.22, dict(r=_h(GUARD_R, knuckles=(0.022, -0.030, 0.51), fwd=(-0.04, 0.05, 1.0), palm=(-0.04, -1.0, 0.0)),
                    chest=-21.0, clav_r=(31, 6))),
        (0.30, dict(r=_h(GUARD_R, knuckles=(0.070, -0.055, 0.44), fwd=(-0.12, 0.15, 1.0), palm=(-0.5, -0.9, 0.0)),
                    l=GUARD_L, chest=-10.0, clav_r=(14, 8))),
        (0.45, dict(r=_h(GUARD_R, knuckles=(0.118, -0.082, 0.35)), chest=1.0, clav_r=(6, 10))),
        (0.56, GUARD),
    ]),
    "Takedown": dict(family="Fists", length=1.10, events=dict(strike=0.30), keys=[
        (0.00, GUARD),
        (0.12, dict(r=dict(knuckles=(0.17, -0.03, 0.33), fwd=(-0.4, 0.35, 1.0), palm=(-1.0, 0.0, 0.0), grip=OPEN,
                           pole=(0.8, -0.8, -0.2)),
                    l=dict(knuckles=(-0.16, -0.02, 0.36), fwd=(0.4, 0.35, 1.0), palm=(1.0, 0.0, 0.0), grip=OPEN,
                           pole=(-0.8, -0.8, -0.2)), clav_r=(4, 14), clav_l=(4, 14))),
        (0.24, dict(r=dict(knuckles=(0.088, -0.035, 0.50), fwd=(-0.25, 0.1, 1.0), palm=(-1.0, -0.1, 0.0), grip=CLAW,
                           pole=(0.8, -0.8, -0.2)),
                    l=dict(knuckles=(-0.086, -0.03, 0.51), fwd=(0.25, 0.1, 1.0), palm=(1.0, -0.1, 0.0), grip=CLAW,
                           pole=(-0.8, -0.8, -0.2)), clav_r=(16, 10), clav_l=(16, 10))),
        (0.30, dict(r=dict(knuckles=(0.082, -0.04, 0.505), fwd=(-0.25, 0.1, 1.0), palm=(-1.0, -0.1, 0.0), grip=FIST,
                           pole=(0.8, -0.8, -0.2)),
                    l=dict(knuckles=(-0.08, -0.035, 0.515), fwd=(0.25, 0.1, 1.0), palm=(1.0, -0.1, 0.0), grip=FIST,
                           pole=(-0.8, -0.8, -0.2)))),
        (0.42, dict(r=dict(knuckles=(0.086, -0.05, 0.50), fwd=(-0.25, 0.05, 1.0), palm=(-1.0, 0.15, 0.0), grip=FIST,
                           pole=(0.8, -0.8, -0.2)),
                    l=dict(knuckles=(-0.08, -0.02, 0.51), fwd=(0.25, 0.15, 1.0), palm=(1.0, 0.15, 0.0), grip=FIST,
                           pole=(-0.8, -0.8, -0.2)))),
        (0.52, dict(r=dict(knuckles=(0.055, 0.035, 0.50), fwd=(-0.2, 0.15, 1.0), palm=(-0.25, -1.0, 0.0), grip=FIST,
                           pole=(0.9, -0.6, -0.2)),
                    l=dict(knuckles=(-0.055, -0.10, 0.51), fwd=(0.2, 0.0, 1.0), palm=(0.25, 1.0, 0.0), grip=FIST,
                           pole=(-0.9, -0.7, -0.2)), chest=-6.0)),
        (0.60, dict(r=dict(knuckles=(0.05, 0.042, 0.50), fwd=(-0.2, 0.15, 1.0), palm=(-0.12, -1.0, 0.0), grip=FIST,
                           pole=(0.9, -0.6, -0.2)),
                    l=dict(knuckles=(-0.05, -0.108, 0.51), fwd=(0.2, 0.0, 1.0), palm=(0.12, 1.0, 0.0), grip=FIST,
                           pole=(-0.9, -0.7, -0.2)), chest=-7.0)),
        (0.76, dict(r=dict(knuckles=(0.055, 0.03, 0.49), fwd=(-0.2, 0.15, 1.0), palm=(-0.25, -1.0, 0.0), grip=CLAW,
                           pole=(0.9, -0.6, -0.2)),
                    l=dict(knuckles=(-0.055, -0.10, 0.50), fwd=(0.2, 0.0, 1.0), palm=(0.25, 1.0, 0.0), grip=CLAW,
                           pole=(-0.9, -0.7, -0.2)), chest=-4.0)),
        (0.88, dict(r=dict(knuckles=(0.15, -0.20, 0.36), fwd=(-0.3, 0.3, 1.0), palm=(-0.9, -0.4, 0.0), grip=OPEN,
                           pole=(0.6, -1.0, -0.2)),
                    l=dict(knuckles=(-0.15, -0.20, 0.38), fwd=(0.3, 0.3, 1.0), palm=(0.9, -0.4, 0.0), grip=OPEN,
                           pole=(-0.6, -1.0, -0.2)), chest=0.0, clav_r=(6, 10), clav_l=(6, 10))),
        (1.10, GUARD),
    ]),
}


def throw_clip(fam):
    """Overhand throw of a small item, from and back to the family's hip pose. The item leaves the
    hand at `release` (the viewmodel hides it there and the server spawns the thrown item)."""
    hip = HAND_FAMILIES[fam]["poses"]["Hip"]
    hold = hip["r"]
    g = hold.get("grip", OPEN)
    return dict(family=fam, length=0.66, events=dict(release=0.27), keys=[
        (0.00, dict(r=hold, l=hip["l"], chest=0.0, clav_r=(4, 2), clav_l=(0, 0))),
        (0.11, dict(r=_h(hold, knuckles=(0.20, 0.07, 0.07), fwd=(0.0, 1.0, -0.35), palm=(-0.4, 0.0, 1.0),
                         pole=(1.0, -0.4, -0.5)),
                    l=dict(knuckles=(-0.08, -0.07, 0.45), fwd=(0.2, 0.2, 1.0), palm=(0.7, -0.7, 0.0), grip=OPEN,
                           pole=(-0.5, -1.0, -0.2)), chest=-10.0, clav_r=(-14, 20))),
        (0.21, dict(r=_h(hold, knuckles=(0.16, 0.06, 0.31), fwd=(-0.1, 0.7, 0.7), palm=(-0.5, -0.1, 1.0),
                         pole=(1.0, -0.5, -0.3)), chest=2.0, clav_r=(6, 14))),
        (0.27, dict(r=_h(hold, knuckles=(0.075, 0.0, 0.50), fwd=(-0.2, 0.1, 1.0), palm=(-0.3, -0.9, 0.2),
                         grip=g, pole=(0.9, -0.7, -0.2)), chest=12.0, clav_r=(24, 4))),
        (0.31, dict(r=_h(hold, knuckles=(0.04, -0.07, 0.50), fwd=(-0.3, -0.2, 1.0), palm=(-0.3, -1.0, 0.2),
                         grip=OPEN, pole=(0.9, -0.7, -0.2)))),
        (0.40, dict(r=_h(hold, knuckles=(-0.03, -0.24, 0.42), fwd=(-0.5, -0.6, 1.0), palm=(-0.2, -1.0, 0.3),
                         grip=OPEN, pole=(0.8, -0.8, -0.3)),
                    l=dict(LEFT_DOWN, knuckles=(-0.19, -0.40, 0.22)), chest=16.0, clav_r=(20, 2))),
        (0.66, dict(r=hold, l=hip["l"], chest=0.0, clav_r=(4, 2), clav_l=(0, 0))),
    ])


for _fam in ("Pinch", "Palm", "Handle"):
    CLIPS[_fam + "_Throw"] = throw_clip(_fam)

# How the support hand holds a magazine (hand-bone space): fingers up its front, base in the palm,
# the feed end (the magazine's pivot, +Z up its axis) past the fingertips.
MAG_IN_HAND = dict(at=(0.150, 0.006, 0.024), axis=(1.0, 0.0, 0.0), front=(0.0, 1.0, 0.0))
MAG_GRIP = dict(index=(14, 18, 10), paddle=(58, 66, 40), thumb=(24, 20, 30, 24, 14))

# Weapon-held clips: the gun's Sight path (Unity space + turn), the right hand on the grip all along,
# the left hand "grip" (its support grip on the gun), a free hand dict, or ("mag", d, offset) holding the
# magazine d metres out of the well along its axis (+ an offset in the gun's frame). Events drive the
# viewmodel: magOut (the gun's magazine goes), magGrab (the one in hand shows), magIn (back in the gun).
_PH = FAMILIES["Pistol"]["poses"]["Hip"]
_SH = FAMILIES["Smg"]["poses"]["Hip"]
RELOADS = {
    "Pistol_Reload": dict(family="Pistol", length=1.50, events=dict(magOut=0.28, magGrab=0.58, magIn=1.02), keys=[
        (0.00, dict(sight=_PH["sight"], turn=_PH["turn"], l="grip", clav=(12, 4))),
        (0.16, dict(sight=(0.05, -0.09, 0.36), turn=(-14, -12, -32), l="grip", clav=(8, 4))),
        (0.30, dict(sight=(0.05, -0.095, 0.36), turn=(-16, -12, -34),
                    l=dict(knuckles=(-0.06, -0.20, 0.33), fwd=(0.3, -0.6, 1.0), palm=(1.0, 0.0, 0.2), grip=OPEN,
                           pole=(-0.6, -1.0, -0.2)))),
        (0.48, dict(l=dict(knuckles=(-0.17, -0.56, 0.16), fwd=(0.2, -1.0, 0.3), palm=(1.0, 0.0, 0.0), grip=CUP,
                           pole=(-0.8, -0.4, -0.6)))),
        (0.60, dict(l=dict(knuckles=(-0.16, -0.54, 0.18), fwd=(0.2, -0.6, 0.6), palm=(1.0, 0.2, 0.0), grip=MAG_GRIP,
                           pole=(-0.8, -0.4, -0.6)))),
        (0.84, dict(sight=(0.045, -0.085, 0.37), turn=(-12, -10, -30), l=("mag", 0.075, (0.0, 0.0, -0.01)))),
        (1.00, dict(l=("mag", 0.0, (0.0, 0.0, 0.0)))),
        (1.08, dict(sight=(0.045, -0.078, 0.37), turn=(-9, -9, -28), l=("mag", -0.004, (0.0, 0.0, 0.0)))),
        (1.24, dict(sight=(0.06, -0.10, 0.39), turn=(0, -6, -12), l="grip")),
        (1.50, dict(sight=_PH["sight"], turn=_PH["turn"], l="grip", clav=(12, 4))),
    ]),
    "Smg_Reload": dict(family="Smg", length=2.00, events=dict(magOut=0.42, magGrab=0.42, magIn=1.36), keys=[
        (0.00, dict(sight=_SH["sight"], turn=_SH["turn"], l="grip", chest=10)),
        (0.18, dict(sight=(0.07, -0.10, 0.22), turn=(-8, -16, -24), l="grip", chest=8)),
        (0.42, dict(sight=(0.07, -0.10, 0.23), turn=(-8, -18, -26), l=("mag", 0.0, (0.0, 0.0, 0.0)))),
        (0.62, dict(l=("mag", 0.13, (0.02, 0.02, -0.02)))),
        (0.84, dict(l=dict(knuckles=(-0.17, -0.55, 0.17), fwd=(0.2, -1.0, 0.3), palm=(1.0, 0.0, 0.0),
                           grip=MAG_GRIP, pole=(-0.8, -0.4, -0.6)))),
        (1.06, dict(l=dict(knuckles=(-0.15, -0.50, 0.19), fwd=(0.2, -0.6, 0.6), palm=(1.0, 0.2, 0.0),
                           grip=MAG_GRIP, pole=(-0.8, -0.4, -0.6)))),
        (1.24, dict(sight=(0.07, -0.10, 0.23), turn=(-8, -18, -26), l=("mag", 0.07, (0.0, 0.0, 0.0)))),
        (1.36, dict(l=("mag", 0.0, (0.0, 0.0, 0.0)))),
        (1.44, dict(sight=(0.07, -0.095, 0.23), turn=(-6, -17, -24), l=("mag", -0.004, (0.0, 0.0, 0.0)))),
        (1.70, dict(sight=(0.09, -0.11, 0.21), turn=(0, -8, -4), l="grip", chest=9)),
        (2.00, dict(sight=_SH["sight"], turn=_SH["turn"], l="grip", chest=10)),
    ]),
}


class Track:
    """A keyed channel: (time, value) pairs, Catmull-Rom between them (positions, scalars, tuples)."""

    def __init__(self, keys):
        self.keys = sorted(keys, key=lambda k: k[0])

    def at(self, t):
        ks = self.keys
        if t <= ks[0][0]:
            return ks[0][1]
        if t >= ks[-1][0]:
            return ks[-1][1]
        i = max(j for j in range(len(ks) - 1) if ks[j][0] <= t)
        t0, v0 = ks[i]
        t1, v1 = ks[i + 1]
        vm = ks[i - 1][1] if i > 0 else v0
        vp = ks[i + 2][1] if i + 2 < len(ks) else v1
        u = (t - t0) / (t1 - t0)
        u2, u3 = u * u, u * u * u

        def cr(a, b, c, d):
            return 0.5 * ((2 * b) + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u2 + (-a + 3 * b - 3 * c + d) * u3)
        if isinstance(v0, (tuple, list)):
            return tuple(cr(a, b, c, d) for a, b, c, d in zip(vm, v0, v1, vp))
        return cr(vm, v0, v1, vp)


class Builder:
    def __init__(self, bake=True):
        F.reset()
        self.arm = R.build_armature()
        self.mesh = R.build_mesh(self.arm, bake=bake)
        if not bake:  # diagnostic colours: a light glove reads against the dark guns
            cols = {"__glove": (0.45, 0.28, 0.15, 1), "__sleeve": (0.12, 0.14, 0.075, 1), "__cuff": (0.06, 0.07, 0.04, 1)}
            for m in self.mesh.data.materials:
                b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
                b.inputs["Base Color"].default_value = cols.get(m.name, (0.4, 0.4, 0.4, 1))
                b.inputs["Roughness"].default_value = 0.5
        self.arm.animation_data_create()
        self.weapons = {}
        self.manifest = dict(asset=F.ASSET, fps=F.FPS, space="unity camera: x right, y up, z forward; metres",
                             poses={}, clips={}, families={})
        F.studio()
        F.fp_camera()

    # ---------------------------------------------------------------- weapons
    def weapon(self, name):
        if name not in self.weapons:
            root, points, mag = F.import_weapon(name)
            self.weapons[name] = (root, points, mag)
        for n, (r, _, _) in self.weapons.items():
            for o in [r] + list(r.children):
                o.hide_render = n != name
        return self.weapons[name]

    def hide_weapons(self):
        for r, _, _ in self.weapons.values():
            for o in [r] + list(r.children):
                o.hide_render = True

    # ---------------------------------------------------------------- one static pose
    def solve_weapon_pose(self, fam, pose_name, spec):
        self.arm.animation_data.action = None  # the solve reads the evaluated mesh: no stored action
        wname = FAMILIES[fam]["weapon"]
        root, points, mag = self.weapon(wname)
        grips = GRIPS[wname]
        sight_local = points["Sight"].translation
        # the sight line, not the bore, is squared with the view (the shotgun's raised, tilted comb line)
        rot = F.euler_u(*spec["turn"]) @ points["Sight"].to_3x3().inverted()
        W = F.frame(rot, F.u2b(spec["sight"]) - rot @ sight_local)
        root.matrix_world = W
        bpy.context.view_layer.update()
        meshes = [root] + [c for c in root.children if c.type == "MESH"]
        bvh = S.bvh_of(meshes)

        P = S.Pose(self.arm)
        self.body(P, spec)
        # right hand on the handle
        grips = dict(grips, r=self.trigger_fit(wname, P, W, grips, spec, bvh))
        hr = grip_hand("r", W, grips["r"])
        P.arm_ik("r", hr.translation, hr.to_3x3(), F.u2b(spec.get("pole_r", POLES["r"])))
        P.set("weapon_r", socket_matrix(W))
        self.close_hand(P, "r", bvh, grips, spec, W)
        # left hand: on the fore-end / around the firing hand, or free
        if spec.get("left") == "free":
            self.free_left(P)
        else:
            hl = grip_hand("l", W, grips["l"])
            P.arm_ik("l", hl.translation, hl.to_3x3(), F.u2b(spec.get("pole_l", POLES["l"])))
            bvh_l = bvh
            if fam == "Pistol":  # the support fingers wrap the firing hand: it joins the obstacles
                P.apply()
                bpy.context.view_layer.update()
                bvh_l = S.bvh_of(meshes + [self.right_glove_proxy()])
            self.close_hand(P, "l", bvh_l, grips, spec, W)
        P.apply()
        bpy.context.view_layer.update()
        return P, W, points

    # ---------------------------------------------------------------- hands without a weapon
    def solve_hand_pose(self, fam, spec):
        self.arm.animation_data.action = None
        P = S.Pose(self.arm)
        self.body(P, spec)
        place_hand(P, "r", spec["r"])
        place_hand(P, "l", spec["l"])
        W = item_socket(P, HAND_FAMILIES[fam]["item"])
        P.set("weapon_r", socket_matrix(W))
        P.apply()
        bpy.context.view_layer.update()
        self.show_item(HAND_FAMILIES[fam]["item"], W)
        return P

    PREVIEW_ITEMS = {"pinch": "W_Coin", "palm": "W_Rock", "handle": "W_Wrench"}

    def show_item(self, kind, W):
        """Preview only: a typical item of the family on the socket (bone-parented, so clips carry it)."""
        for k, name in self.PREVIEW_ITEMS.items():
            if name in self.weapons:
                for o in [self.weapons[name][0]] + list(self.weapons[name][0].children):
                    o.hide_render = True
        name = self.PREVIEW_ITEMS.get(kind)
        if name is None or F.weapon_fbx(name) is None:
            return
        if name not in self.weapons:
            self.weapons[name] = F.import_weapon(name)
        root = self.weapons[name][0]
        root.parent = self.arm
        root.parent_type = "BONE"
        root.parent_bone = "weapon_r"
        bpy.context.view_layer.update()
        root.matrix_world = W
        for o in [root] + list(root.children):
            o.hide_render = False

    def play_clip(self, name, clip):
        """Solves a clip frame by frame from its keys and keys every bone (baked, 30 fps)."""
        keys = clip["keys"]
        tracks = {}
        for side in R.SIDES:
            for ch in ("knuckles", "fwd", "palm", "pole"):
                ks = [(t, k[side][ch]) for t, k in keys if side in k and ch in k[side]]
                if ks:
                    tracks[(side, ch)] = Track(ks)
            def flat(g):
                return (tuple(g["index"]) + tuple(g["paddle"]) + tuple(g["thumb"])
                        + (float(g.get("tuck", 0.0)), float(g.get("key", 0.0))))
            gk = [(t, flat(k[side].get("grip", OPEN))) for t, k in keys if side in k]
            tracks[(side, "grip")] = Track(gk)
        for ch in ("chest", "clav_r", "clav_l"):
            ks = [(t, k[ch]) for t, k in keys if ch in k]
            if ks:
                tracks[ch] = Track(ks)
        fam = clip["family"]
        kind = HAND_FAMILIES[fam]["item"] if fam in HAND_FAMILIES else None
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        self.arm.animation_data.action = act
        n = int(round(clip["length"] * F.FPS))
        poses = {}
        for fr in range(n + 1):
            t = fr / F.FPS
            P = S.Pose(self.arm)
            spec = {}
            if "chest" in tracks:
                spec["chest"] = tracks["chest"].at(t)
            for c in ("clav_r", "clav_l"):
                if c in tracks:
                    spec[c] = tracks[c].at(t)
            self.body(P, spec)
            for side in R.SIDES:
                h = {ch: tracks[(side, ch)].at(t) for ch in ("knuckles", "fwd", "palm", "pole") if (side, ch) in tracks}
                g = tracks[(side, "grip")].at(t)
                h["grip"] = dict(index=g[0:3], paddle=g[3:6], thumb=g[6:11], tuck=min(max(g[11], 0.0), 1.0),
                                 key=min(max(g[12], 0.0), 1.0))
                place_hand(P, side, h)
            P.set("weapon_r", socket_matrix(item_socket(P, kind)))
            P.apply()
            P.key(fr)
            poses[fr] = P
        print("CLIP", name, n + 1, "frames")
        return act, poses

    # ---------------------------------------------------------------- weapon-held clips (reloads)
    def hold_reference(self, fam):
        """The family's hip pose solved once: the hands and fingers relative to the gun, reused by its clips."""
        spec = FAMILIES[fam]["poses"]["Hip"]
        P, W, points = self.solve_weapon_pose(fam, "Hip", spec)
        Winv = W.inverted()
        ref = dict(W=W, points=points, hand_r=Winv @ P.get("hand_r"), hand_l=Winv @ P.get("hand_l"), fingers={})
        for side in R.SIDES:
            hm_inv = P.get("hand_" + side).inverted()
            for name in R.FINGERS + ("thumb",):
                for i in range(3):
                    bn = f"{name}_0{i + 1}_{side}"
                    ref["fingers"][bn] = hm_inv @ P.get(bn)
        return ref

    def play_weapon_clip(self, name, clip):
        fam = clip["family"]
        ref = self.hold_reference(fam)
        root, points, _ = self.weapon(FAMILIES[fam]["weapon"])
        sight_local = points["Sight"].translation
        mag_well = points["MagWell"]
        keys = clip["keys"]
        tr = {}
        for ch in ("sight", "turn", "chest", "clav", "clav_l", "clav_r"):
            ks = [(t, k[ch]) for t, k in keys if ch in k]
            if ks:
                tr[ch] = Track(ks)
        lkeys = [(t, k["l"]) for t, k in keys if "l" in k]
        mag_hand = Matrix(((0, 0, 1), (0, 0, 0), (0, 0, 0)))  # placeholder, replaced below
        mi = MAG_IN_HAND
        # the magazine's frame in hand-bone space (X = Y x Z, Y = -front, Z = axis)
        z = Vector(mi["axis"]).normalized()
        y = -Vector(mi["front"]).normalized()
        x = y.cross(z)
        mag_hand = Matrix((x, y, z)).transposed().to_4x4()
        mag_hand.translation = Vector(mi["at"])

        def W_at(t):
            rot = F.euler_u(*tr["turn"].at(t))
            return F.frame(rot, F.u2b(tr["sight"].at(t)) - rot @ sight_local)

        def left_target(spec, W):
            """(hand matrix, finger locals or grip dict, gun-relative?) for a left-hand key."""
            if spec == "grip":
                return W @ ref["hand_l"], ("locals", None), True
            if isinstance(spec, tuple) and spec[0] == "mag":
                d, off = spec[1], spec[2]
                prop = W @ Matrix.Translation(Vector(off)) @ mag_well @ Matrix.Translation((0, 0, -d))
                return prop @ mag_hand.inverted(), ("grip", MAG_GRIP), True
            rot = hand_rot(spec["fwd"], spec["palm"])
            m = rot.to_4x4()
            m.translation = F.u2b(spec["knuckles"]) - rot.col[1] * 0.095
            return m, ("grip", spec.get("grip", OPEN)), False

        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        self.arm.animation_data.action = act
        n = int(round(clip["length"] * F.FPS))
        poses = {}
        for fr in range(n + 1):
            t = fr / F.FPS
            W = W_at(t)
            root.matrix_world = W
            P = S.Pose(self.arm)
            spec = {ch: tr[ch].at(t) for ch in ("chest", "clav", "clav_l", "clav_r") if ch in tr}
            self.body(P, spec)
            # right hand: the hip grip, carried by the gun
            hr = W @ ref["hand_r"]
            P.arm_ik("r", hr.translation, hr.to_3x3(), F.u2b(POLES["r"]))
            self.apply_fingers(P, "r", ref)
            P.set("weapon_r", socket_matrix(W))
            # left hand: between the two keys around t
            i = max(j for j in range(len(lkeys)) if lkeys[j][0] <= t) if t >= lkeys[0][0] else 0
            k0 = lkeys[i]
            k1 = lkeys[min(i + 1, len(lkeys) - 1)]
            u = 0.0 if k1[0] <= k0[0] else (t - k0[0]) / (k1[0] - k0[0])
            u = u * u * (3 - 2 * u)
            m0, f0, g0 = left_target(k0[1], W)
            m1, f1, g1 = left_target(k1[1], W)
            pos = m0.translation.lerp(m1.translation, u)
            q = m0.to_quaternion().slerp(m1.to_quaternion(), u)
            hl = q.to_matrix().to_4x4()
            hl.translation = pos
            pole = k1[1].get("pole", POLES["l"]) if isinstance(k1[1], dict) else (
                k0[1].get("pole", POLES["l"]) if isinstance(k0[1], dict) else POLES["l"])
            P.arm_ik("l", hl.translation, hl.to_3x3(), F.u2b(pole))
            self.blend_fingers(P, "l", ref, f0, f1, u)
            P.set("prop_l", socket_matrix(P.get("hand_l") @ mag_hand))
            P.apply()
            P.key(fr)
            poses[fr] = P
        print("CLIP", name, n + 1, "frames")
        return act, poses, ref

    @staticmethod
    def apply_fingers(P, side, ref):
        hm = P.get("hand_" + side)
        for name in R.FINGERS + ("thumb",):
            for i in range(3):
                bn = f"{name}_0{i + 1}_{side}"
                P.set(bn, hm @ ref["fingers"][bn])

    def blend_fingers(self, P, side, ref, f0, f1, u):
        """Fingers between two keys: each as local matrices (stored grip, or a grip dict applied), slerped."""
        def locals_of(f):
            if f[0] == "locals":
                return {bn: m for bn, m in ref["fingers"].items() if bn.endswith("_" + side)}
            Q = S.Pose(self.arm)
            Q.P = dict(P.P)
            set_grip(Q, side, f[1])
            hm_inv = Q.get("hand_" + side).inverted()
            return {f"{n}_0{i + 1}_{side}": hm_inv @ Q.get(f"{n}_0{i + 1}_{side}")
                    for n in R.FINGERS + ("thumb",) for i in range(3)}
        a, b = locals_of(f0), locals_of(f1)
        hm = P.get("hand_" + side)
        for name in R.FINGERS + ("thumb",):
            for i in range(3):
                bn = f"{name}_0{i + 1}_{side}"
                q = a[bn].to_quaternion().slerp(b[bn].to_quaternion(), u)
                m = q.to_matrix().to_4x4()
                m.translation = a[bn].translation.lerp(b[bn].translation, u)
                P.set(bn, hm @ m)

    @staticmethod
    def body(P, spec):
        """Chest yaw (bladed stance, + turns the right shoulder back) and shoulders pushed forward /
        lifted (clav_r / clav_l = (forward deg, up deg)), before the arms reach."""
        if spec.get("chest"):
            P.rotate_about("spine_03", F.euler_u(0, spec["chest"], 0))
        for side in R.SIDES:
            fwd, up = spec.get("clav_" + side, spec.get("clav", (0.0, 0.0)))
            if fwd or up:
                sgn = 1.0 if side == "r" else -1.0
                # forward: about the vertical through the clavicle head; up: about the forward axis
                rot = (F.euler_u(0, -fwd * sgn, 0) @ F.euler_u(0, 0, up * sgn))
                P.rotate_about("clavicle_" + side, rot)

    def trigger_fit(self, wname, P, W, grips, spec, bvh):
        """The firing-hand grip slid along the handle (reach: along the fingers, slide: along the thumb
        side) so the trigger finger's pad lands on the trigger face, within ~3 mm where it can; searched
        once per gun and kept for all its poses (the hand does not slide on the grip between them)."""
        cache = self.__dict__.setdefault("_trigger_fit", {})
        if wname not in cache:
            g0 = grips["r"]
            pole = F.u2b(spec.get("pole_r", POLES["r"]))
            def fit(dr, ds):
                g = dict(g0, reach=g0.get("reach", 0.062) + dr, slide=g0.get("slide", 0.0) + ds)
                hr = grip_hand("r", W, g)
                P.arm_ik("r", hr.translation, hr.to_3x3(), pole)
                P.set("weapon_r", socket_matrix(W))
                err = P.trigger("r", W @ Vector(grips["trigger"]), W.to_3x3() @ Vector((0, -1, 0)), bvh)
                return (err + 0.15 * math.hypot(dr, ds), err, dr, ds, g)  # the smallest move that reaches
            # coordinate search: along the thumb side (up/down the handle), then along the fingers, then again
            best = fit(0.0, 0.0)
            for axis, steps in (("s", range(-6, 7)), ("r", range(-6, 7)), ("s", range(-2, 3))):
                for k in steps:
                    dr, ds = (best[2], best[3] + k * 0.006 / (3 if axis == "s" and len(steps) == 5 else 1))                         if axis == "s" else (best[2] + k * 0.004, best[3])
                    c = fit(dr, ds)
                    if c[0] < best[0]:
                        best = c
            cache[wname] = best[4]
            print("TRIGGER FIT %s reach %+.0f mm slide %+.0f mm -> %.1f mm"
                  % (wname, best[2] * 1000, best[3] * 1000, best[1] * 1000))
            P.report.pop("r_trigger_err_mm", None)
        return cache[wname]

    def close_hand(self, P, side, bvh, grips, spec, W):
        th = grips["thumb_" + side]
        if side == "r":
            trig = spec.get("trig", "frame")
            if trig == "trigger":
                face = W @ Vector(grips["trigger"])
                P.trigger("r", face, W.to_3x3() @ Vector((0, -1, 0)), bvh)
            else:
                P.finger_set("index", "r", (6, 4, 2), spread=-2)
                P.curl("index", "r", bvh, start=(2, 3, 2), spread=-2, stop_after=0)
            for name in R.PADDLE:
                P.curl(name, "r", bvh)
        else:
            for name in R.FINGERS:
                P.curl(name, "l", bvh)
        if "target" in th:  # pad onto a point (thumbs forward along a frame)
            P.reach("thumb", side, W @ Vector(th["target"]), bvh, start=list(th["swing"]) + list(th["start"]))
        else:
            P.thumb_swing(side, *th["swing"])
            P.curl("thumb", side, bvh, start=th["start"])

    def free_left(self, P):
        """Left arm down out of sight, hand relaxed (sprinting with a pistol, one-handed items)."""
        rot = F.euler_u(70, 10, 80)
        f, t, p = R.hand_frame("l")
        base = Matrix((f.cross(p), f, p)).transposed()
        P.arm_ik("l", F.u2b((-0.20, -0.62, 0.16)), rot @ base, F.u2b((-0.8, -0.3, -0.6)))
        for name in R.FINGERS:
            P.finger_set(name, "l", (25, 35, 20))
        P.thumb_swing("l", 0, 0)
        P.thumb_set("l", (10, 15, 10))

    def right_glove_proxy(self):
        """The posed right glove as its own temporary mesh (an obstacle for the support fingers)."""
        dg = bpy.context.evaluated_depsgraph_get()
        ev = self.mesh.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        if not hasattr(self, "_right_side"):
            # vertices skinned mostly to right-side bones (the hand and its fingers)
            names = {g.index: g.name for g in self.mesh.vertex_groups}
            self._right_side = set()
            for v in self.mesh.data.vertices:
                w = sum(g.weight for g in v.groups if names[g.group].endswith("_r") and
                        (names[g.group].startswith(("hand", "index", "middle", "ring", "pinky", "thumb"))))
                if w > 0.5:
                    self._right_side.add(v.index)
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in self._right_side], context="VERTS")
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.get("__right_glove") or bpy.data.objects.new("__right_glove", me)
        ob.data = me
        if ob.name not in bpy.context.scene.collection.objects:
            bpy.context.scene.collection.objects.link(ob)
        ob.matrix_world = self.mesh.matrix_world
        ob.hide_render = True
        return ob

    # ---------------------------------------------------------------- output
    def store(self, action_name, P, frames=(0, 1)):
        act = bpy.data.actions.new(action_name)
        act.use_fake_user = True
        self.arm.animation_data.action = act
        for fr in frames:
            P.key(fr)
        return act

    def record_pose(self, key, P, W=None, points=None, frame=0, clip=None):
        sock = P.get("weapon_r")
        entry = dict(clip=clip or key, time=frame / F.FPS,
                     socket=dict(pos=list(F.b2u(sock.translation)), rot=list(F.rot_b2u(sock.to_3x3() @ G_INV.inverted()))))
        if W is not None and points is not None and "Sight" in points:
            s = W @ points["Sight"]
            entry["sight"] = dict(pos=list(F.b2u(s.translation)), rot=list(F.rot_b2u(s.to_3x3())))
        entry["report"] = {k: round(v, 3) for k, v in P.report.items()}
        self.manifest["poses"][key] = entry

    def preview(self, name, aspect=16 / 9):
        F.fp_camera(aspect)
        return F.render(os.path.join(F.PREVIEW, name + ".png"))

    def details(self, name, W, focus=(0.0, -0.03, 0.0)):
        """Close views of the hands on the weapon, from its right, its left and below (weapon frame)."""
        tgt = W @ Vector(focus)
        up = W.to_3x3() @ Vector((0, 0, 1))
        for tag, off in (("R", (-0.55, -0.10, 0.12)), ("L", (0.55, -0.10, 0.12)), ("U", (-0.15, -0.20, -0.52))):
            self.detail(f"{name}_{tag}", None, None, lens=50, eye_b=W @ (Vector(focus) + Vector(off)), tgt_b=tgt,
                        up_b=up)

    def detail(self, name, target_u, from_u, lens=55, eye_b=None, tgt_b=None, up_b=None):
        cam = bpy.data.objects.get("DetailCam") or bpy.data.objects.new("DetailCam", bpy.data.cameras.new("DetailCam"))
        if cam.name not in bpy.context.scene.collection.objects:
            bpy.context.scene.collection.objects.link(cam)
        tgt = tgt_b if tgt_b is not None else F.u2b(target_u)
        eye = eye_b if eye_b is not None else F.u2b(from_u)
        cam.location = eye
        fwd = (tgt - eye).normalized()
        upv = Vector(up_b) if up_b is not None else Vector((0, 0, 1))
        upv = (upv - fwd * upv.dot(fwd)).normalized()
        cam.rotation_euler = Matrix((fwd.cross(upv), upv, -fwd)).transposed().to_euler()
        cam.data.lens = lens
        cam.data.clip_start = 0.01
        sc = bpy.context.scene
        old = sc.camera
        sc.camera = cam
        sc.render.resolution_x, sc.render.resolution_y = 1000, 750
        F.render(os.path.join(F.PREVIEW, name + ".png"))
        sc.camera = old

    def export(self):
        os.makedirs(F.OUT, exist_ok=True)
        # importing the weapon FBXs set the scene to their 24 fps: the keys are 60 fps frames
        bpy.context.scene.render.fps = F.FPS
        bpy.context.scene.render.fps_base = 1.0
        self.arm.animation_data.action = None
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        self.arm.select_set(True)
        self.mesh.select_set(True)
        bpy.context.view_layer.objects.active = self.arm
        path = os.path.join(F.OUT, F.ASSET + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                                 axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False,
                                 primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                                 armature_nodetype="NULL", mesh_smooth_type="FACE", use_tspace=True,
                                 use_mesh_modifiers=False, path_mode="STRIP", embed_textures=False,
                                 bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                                 bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
                                 bake_anim_step=1.0, bake_anim_simplify_factor=0.25)
        F.write_json(os.path.join(F.OUT, "fp_poses.json"), self.manifest)
        print("EXPORTED", path)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if "--only" in args:
        only = set(args[args.index("--only") + 1].split(","))
    t0 = time.time()
    b = Builder(bake="--nobake" not in args)
    preview = "--preview" in args
    for fam, fspec in FAMILIES.items():
        if only and fam not in only:
            continue
        for pose_name, spec in fspec["poses"].items():
            P, W, points = b.solve_weapon_pose(fam, pose_name, spec)
            key = f"{fam}_{pose_name}"
            b.store(key, P)
            b.record_pose(key, P, W, points)
            print("POSE", key, P.report)
            if preview:
                b.preview(key)
                b.details(key, W)
        b.manifest["families"][fam] = dict(weapon=fspec["weapon"])
    b.hide_weapons()
    for fam, fspec in HAND_FAMILIES.items():
        if only and fam not in only:
            continue
        for pose_name, spec in fspec["poses"].items():
            P = b.solve_hand_pose(fam, spec)
            key = f"{fam}_{pose_name}"
            b.store(key, P)
            b.record_pose(key, P)
            print("POSE", key, P.report)
            if preview:
                b.preview(key)
        b.manifest["families"][fam] = dict(item=fspec["item"])
    for name, clip in CLIPS.items():
        if only and name not in only and clip["family"] not in only:
            continue
        act, poses = b.play_clip(name, clip)
        b.manifest["clips"][name] = dict(family=clip["family"], length=clip["length"], frames=len(poses),
                                         events=clip.get("events", {}))
        # sample a few frames into the manifest for the import test
        for tag, t in [("start", 0.0)] + [(k, v) for k, v in clip.get("events", {}).items()]:
            fr = int(round(t * F.FPS))
            b.record_pose(f"{name}@{tag}", poses[fr], frame=fr, clip=name)
        if preview:
            kind = HAND_FAMILIES[clip["family"]]["item"] if clip["family"] in HAND_FAMILIES else None
            bpy.context.scene.frame_set(0)
            b.show_item(kind, item_socket(poses[0], kind))
            for tag, t in [(k, v) for k, v in clip.get("events", {}).items()] + [("mid", clip["length"] * 0.45)]:
                bpy.context.scene.frame_set(int(round(t * F.FPS)))
                b.preview(f"{name}_{tag}")
    for name, clip in RELOADS.items():
        if only and name not in only and clip["family"] not in only:
            continue
        b.hide_weapons()
        act, poses, ref = b.play_weapon_clip(name, clip)
        b.manifest["clips"][name] = dict(family=clip["family"], length=clip["length"], frames=len(poses),
                                         events=clip.get("events", {}))
        for tag, t in [("start", 0.0)] + [(k, v) for k, v in clip.get("events", {}).items()]:
            fr = int(round(t * F.FPS))
            b.record_pose(f"{name}@{tag}", poses[fr], frame=fr, clip=name)
        if preview:
            root = b.weapon(FAMILIES[clip["family"]]["weapon"])[0]
            for tag, t in [(k, v) for k, v in clip.get("events", {}).items()] + [("mid", clip["length"] * 0.5)]:
                fr = int(round(t * F.FPS))
                bpy.context.scene.frame_set(fr)
                root.matrix_world = poses[fr].get("weapon_r") @ G_INV.inverted().to_4x4()
                b.preview(f"{name}_{tag}")
    if "--export" in args:
        b.export()
    print("DONE", round(time.time() - t0, 1), "s")


if __name__ == "__main__":
    main()
