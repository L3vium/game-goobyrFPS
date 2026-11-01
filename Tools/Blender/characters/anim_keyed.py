# Author : Baptiste Vu Liem
"""Hand-keying helpers on top of anim_rig: poses described as rotation offsets (degrees about the
rest-pose world axes: +X character left, -Y forward, +Z up), IK hand/foot targets, then eased
interpolation between key poses at 30 fps."""
import math

from mathutils import Matrix, Quaternion, Vector

import anim_rig as R

UPPER = ["spine_01", "spine_02", "spine_03", "neck_01", "head", "clavicle_l", "clavicle_r", "upperarm_l",
         "upperarm_r", "lowerarm_l", "lowerarm_r", "hand_l", "hand_r"]
RADIUS = {"pelvis": 0.11, "spine_01": 0.12, "spine_02": 0.13, "spine_03": 0.13, "neck_01": 0.06, "head": 0.09,
          "upperarm_l": 0.05, "upperarm_r": 0.05, "lowerarm_l": 0.04, "lowerarm_r": 0.04, "hand_l": 0.03,
          "hand_r": 0.03, "thigh_l": 0.08, "thigh_r": 0.08, "calf_l": 0.05, "calf_r": 0.05, "foot_l": 0.07,
          "foot_r": 0.07, "ball_l": 0.025, "ball_r": 0.025}


def ease(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def chest_delta(rig, W):
    return W["spine_03"] @ rig.rest["spine_03"].inverted()


def rot(rig, f, bone, deg):
    """Compose a rest-world-axis rotation offset onto the bone's current local rotation."""
    f["q"][bone] = f["q"].get(bone, Quaternion()) @ rig.axis_quat(bone, deg)


def rot_world(rig, f, bone, deg, W=None):
    """Rotate bone about the *current* world axes (degrees XYZ) around its head."""
    if W is None:
        W, _ = rig.fk(f)
    Rw = Matrix.Rotation(math.radians(deg[0]), 3, "X") @ Matrix.Rotation(math.radians(deg[1]), 3, "Y") @ \
        Matrix.Rotation(math.radians(deg[2]), 3, "Z")
    rig.set_world(f, W, bone, Rw @ W[bone])


def hand_world(rig, side, fdir, tdir):
    """World rotation of hand_<side> whose fingers point along fdir and whose thumb side (pinky->index
    line) points along tdir."""
    hb = "hand_" + side
    Rr = rig.rest[hb]
    a = Rr.col[1].normalized()
    tr = Vector((0, -1, 0))
    tr = (tr - a * tr.dot(a)).normalized()
    f = fdir.normalized()
    t = (tdir - f * tdir.dot(f)).normalized()
    src = Matrix((a, tr, a.cross(tr))).transposed()
    dst = Matrix((f, t, f.cross(t))).transposed()
    return dst @ src.inverted() @ Rr


def place_hand(rig, f, side, pos, fdir, tdir, space="chest", pole=None):
    """IK the arm so the wrist reaches pos and orient the hand. space: 'chest' (rest-world axes moving
    with spine_03), 'head' or 'world'."""
    W, P = rig.fk(f)
    if space == "chest":
        D = chest_delta(rig, W)
        base = P["spine_03"]
    elif space == "chest_yaw":
        # chest position, but only the torso's heading: hands keep their world height when the spine bends
        Dc = chest_delta(rig, W)
        fwd = Dc @ Vector((0, -1, 0))
        yaw = math.atan2(fwd.x, -fwd.y)
        D = Matrix.Rotation(yaw, 3, "Z")
        base = P["spine_03"]
    elif space == "head":
        D = W["head"] @ rig.rest["head"].inverted()
        base = P["head"]
    elif space == "pelvis":
        D = W["pelvis"] @ rig.rest["pelvis"].inverted()
        base = P["pelvis"]
    else:
        D = Matrix.Identity(3)
        base = Vector((0, 0, 0))
    tgt = base + D @ Vector(pos)
    up, lo = "upperarm_" + side, "lowerarm_" + side
    sx = 1.0 if side == "l" else -1.0
    if pole is None:
        pole_w = P[up] + D @ Vector((sx * 0.25, 0.45, -0.35))
    else:
        pole_w = base + D @ Vector(pole)
    R.two_bone_ik(rig, f, up, lo, "hand_" + side, tgt, pole_w, W, P)
    Wh = D @ hand_world(rig, side, Vector(fdir), Vector(tdir))
    rig.set_world(f, W, "hand_" + side, Wh)
    spread_twist(rig, f, side, W, Wh)


def spread_twist(rig, f, side, W, Wh, amount=0.5):
    """Move part of the wrist twist into the forearm (avoids candy-wrapper wrists)."""
    lo, hb = "lowerarm_" + side, "hand_" + side
    q = f["q"][hb]
    tw = Quaternion((q.w, 0.0, q.y, 0.0))
    if tw.magnitude < 1e-6:
        return
    tw.normalize()
    ang = 2 * math.atan2(tw.y, tw.w)
    f["q"][lo] = f["q"][lo] @ Quaternion((0, 1, 0), ang * amount)
    W2, _ = rig.fk(f)
    W.update(W2)
    rig.set_world(f, W, hb, Wh)


def plant_foot(rig, f, side, pos, yaw_deg=0.0, pitch_deg=0.0, pole_fwd=0.6):
    """IK a leg so the ankle reaches pos (world) with a flat foot rotated by yaw/pitch."""
    W, P = rig.fk(f)
    th, ca, fo = "thigh_" + side, "calf_" + side, "foot_" + side
    sx = 1.0 if side == "l" else -1.0
    Dp = W["pelvis"] @ rig.rest["pelvis"].inverted()
    knee_dir = Dp @ Vector((sx * 0.15, -1.0, 0.0))
    pole = (P[th] + Vector(pos)) * 0.5 + knee_dir * pole_fwd
    R.two_bone_ik(rig, f, th, ca, fo, Vector(pos), pole, W, P)
    Rf = Matrix.Rotation(math.radians(yaw_deg), 3, "Z") @ Matrix.Rotation(math.radians(pitch_deg), 3, "X") @ rig.rest[fo]
    rig.set_world(f, W, fo, Rf)


def rest_feet(rig):
    W, P = rig.fk(rig.rest_frame())
    return P["foot_l"].copy(), P["foot_r"].copy()


def interpolate(keys, dur, fps=R.FPS, easing=True):
    """keys: list of (time, frame). Returns dense frames from 0..dur."""
    n = int(round(dur * fps))
    out = []
    for i in range(n + 1):
        t = i / fps
        k = 0
        while k + 1 < len(keys) and keys[k + 1][0] <= t:
            k += 1
        if k + 1 >= len(keys):
            out.append(R.copy_frame(keys[-1][1]))
            continue
        t0, f0 = keys[k]
        t1, f1 = keys[k + 1]
        u = (t - t0) / max(t1 - t0, 1e-6)
        out.append(R.blend_frames(f0, f1, ease(u) if easing else u))
    R.fix_signs(out)
    return out


def breathe(rig, frames, amp=1.0, period=None, phase=0.0):
    """Subtle breathing / weight shift, loop-safe when period divides the clip length."""
    n = len(frames) - 1
    period = period or n
    for i, f in enumerate(frames):
        s = math.sin(2 * math.pi * (i / period) + phase)
        c = math.sin(2 * math.pi * (i / period) * 0.5 + phase) if n % (2 * period) == 0 else 0.0
        rot(rig, f, "spine_02", (-0.8 * amp * s, 0, 0))
        rot(rig, f, "spine_03", (-1.0 * amp * s, 0, 0))
        rot(rig, f, "clavicle_l", (0, -0.8 * amp * s, 0))
        rot(rig, f, "clavicle_r", (0, 0.8 * amp * s, 0))
        rot(rig, f, "neck_01", (0.9 * amp * s, 0, 0))
        f["hips"] = f["hips"] + Vector((0.004 * amp * c, 0, 0.002 * amp * s))


def ground_body(rig, frames, from_frac=0.0):
    """Lift/lower so the lowest body part touches the floor (for lying / crawling poses)."""
    lows = []
    n = len(frames)
    for f in frames[int(from_frac * n):]:
        W, P = rig.fk(f)
        lows.append(min(P[b].z - r for b, r in RADIUS.items()))
    off = min(lows)
    for f in frames:
        f["hips"].z -= off
    return off


def lowest_body(rig, f):
    W, P = rig.fk(f)
    return min(P[b].z - r for b, r in RADIUS.items())


def timewarp(frames, mapping, dur, fps=R.FPS):
    """Resample frames along normalized time mapping(u) -> s (both 0..1)."""
    n = len(frames) - 1
    m = int(round(dur * fps))
    out = []
    for i in range(m + 1):
        s = mapping(i / m) * n
        a = min(int(math.floor(s)), n)
        b = min(a + 1, n)
        out.append(R.blend_frames(frames[a], frames[b], s - a))
    return out
