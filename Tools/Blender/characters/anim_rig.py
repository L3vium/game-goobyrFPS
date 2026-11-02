# Author : Baptiste Vu Liem
"""Rig model, clip container, clip operations (loop, in-place, ground, foot locking, IK) and
action writing for the animation builder. Pure mathutils: no scene evaluation per frame.

Clip frame = {"q": {bone: local Quaternion (pose basis)}, "hips": pelvis head position (armature space)}
"""
import math

import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

FPS = 30
REST_FRAME = -5


class RigModel:
    def __init__(self, arm):
        self.arm = arm
        self.order = []
        self.parent = {}
        self.rest = {}      # 3x3 armature-space rest orientation
        self.head = {}      # armature-space rest head
        self.length = {}
        self.rest4 = {}
        for b in arm.data.bones:
            self.order.append(b.name)
            self.parent[b.name] = b.parent.name if b.parent else None
            self.rest[b.name] = b.matrix_local.to_3x3().normalized()
            self.head[b.name] = b.head_local.copy()
            self.length[b.name] = b.length
            self.rest4[b.name] = b.matrix_local.copy()
        # bones are listed parents-first by Blender
        self.children = {n: [] for n in self.order}
        for n, p in self.parent.items():
            if p:
                self.children[p].append(n)

    # ---- conversions
    def axis_quat(self, bone, deg_xyz):
        """Local rotation that rotates `bone` about the T-pose world axes (degrees XYZ)."""
        R = self.rest[bone]
        W = Euler([math.radians(a) for a in deg_xyz], "XYZ").to_matrix()
        return (R.inverted() @ W @ R).to_quaternion()

    def fk(self, frame):
        """Return (world 3x3 rotations, world head positions) for every bone."""
        W, P = {}, {}
        q = frame["q"]
        for n in self.order:
            p = self.parent[n]
            L = q.get(n, Quaternion()).to_matrix()
            if p is None:
                W[n] = self.rest[n] @ L
                P[n] = self.head[n].copy()
            else:
                rel = self.rest[p].inverted() @ self.rest[n]
                W[n] = W[p] @ rel @ L
                off = self.head[n] - self.head[p]
                P[n] = P[p] + W[p] @ (self.rest[p].inverted() @ off)
            if n == "pelvis":
                P[n] = frame["hips"].copy()
        return W, P

    def tail(self, W, P, n):
        return P[n] + W[n].col[1] * self.length[n]

    def local_from_world(self, n, Wn, Wp):
        """Local quaternion of bone n given its world rotation Wn and its parent's Wp."""
        p = self.parent[n]
        if p is None:
            return (self.rest[n].inverted() @ Wn).to_quaternion()
        rel = self.rest[p].inverted() @ self.rest[n]
        return ((Wp @ rel).inverted() @ Wn).to_quaternion()

    def set_world(self, frame, W, n, Wn):
        """Set bone n to world rotation Wn (W = current world rotations, updated in place for n)."""
        p = self.parent[n]
        frame["q"][n] = self.local_from_world(n, Wn, W[p] if p else None)
        W[n] = Wn

    def rest_frame(self):
        return {"q": {n: Quaternion() for n in self.order}, "hips": self.head["pelvis"].copy()}


def copy_frame(f):
    return {"q": {k: v.copy() for k, v in f["q"].items()}, "hips": f["hips"].copy()}


class Clip:
    def __init__(self, name, frames, loop=False, in_place=False, upper=False, notes="", source="hand-keyed"):
        self.name = name
        self.frames = frames
        self.loop = loop
        self.in_place = in_place
        self.upper = upper
        self.notes = notes
        self.source = source
        self.speed = 0.0

    @property
    def duration(self):
        return (len(self.frames) - 1) / FPS


# ------------------------------------------------------------------------------------------ ops

def blend_frames(a, b, t):
    out = {"q": {}, "hips": a["hips"].lerp(b["hips"], t)}
    for k in set(a["q"]) | set(b["q"]):
        qa = a["q"].get(k, Quaternion())
        qb = b["q"].get(k, Quaternion())
        if qa.dot(qb) < 0:
            qb = -qb
        out["q"][k] = qa.slerp(qb, t)
    return out


def resample(frames, src_fps, dst_fps=FPS):
    n = len(frames)
    dur = (n - 1) / src_fps
    m = int(round(dur * dst_fps)) + 1
    out = []
    for i in range(m):
        t = i / dst_fps * src_fps
        i0 = min(int(math.floor(t)), n - 1)
        i1 = min(i0 + 1, n - 1)
        out.append(blend_frames(frames[i0], frames[i1], t - i0))
    return out


def smooth(frames, passes=1, bones=None):
    """Light temporal smoothing of rotations (removes mocap jitter)."""
    for _ in range(passes):
        new = []
        for i, f in enumerate(frames):
            a = frames[max(i - 1, 0)]
            b = frames[min(i + 1, len(frames) - 1)]
            g = copy_frame(f)
            for k in (bones or f["q"].keys()):
                q = f["q"][k]
                qa, qb = a["q"][k], b["q"][k]
                if qa.dot(q) < 0:
                    qa = -qa
                if qb.dot(q) < 0:
                    qb = -qb
                g["q"][k] = q.slerp(qa.slerp(qb, 0.5), 0.33)
            new.append(g)
        frames[:] = new
    return frames


def yaw_of(rig, frame):
    """Facing yaw (radians) of the pelvis: angle of its forward (-Y at rest) projected on the ground."""
    W, P = rig.fk(frame)
    fwd = W["pelvis"] @ (rig.rest["pelvis"].inverted() @ Vector((0, -1, 0)))
    return math.atan2(fwd.x, -fwd.y)


def rotate_clip(rig, frames, yaw, pivot=None):
    """Rotate the whole clip about Z by yaw radians (around pivot xy)."""
    Rz = Matrix.Rotation(yaw, 3, "Z")
    pv = pivot if pivot is not None else Vector((0, 0, 0))
    R = rig.rest["pelvis"]
    for f in frames:
        f["q"]["pelvis"] = (R.inverted() @ Rz @ R @ f["q"]["pelvis"].to_matrix()).to_quaternion()
        h = f["hips"] - Vector((pv.x, pv.y, 0))
        f["hips"] = Rz @ h + Vector((pv.x, pv.y, 0))


def face_forward(rig, frames):
    """Rotate so the average facing is -Y (character forward) and centre the start on the origin."""
    ys = [yaw_of(rig, f) for f in frames]
    s = sum(math.sin(y) for y in ys)
    c = sum(math.cos(y) for y in ys)
    mean = math.atan2(s, c)
    rotate_clip(rig, frames, -mean)
    h0 = frames[0]["hips"]
    for f in frames:
        f["hips"] = f["hips"] - Vector((h0.x, h0.y, 0))


def ground(rig, frames, percentile=0.05):
    """Shift vertically so the feet touch the ground like in the rest pose."""
    rest = rig.rest_frame()
    Wr, Pr = rig.fk(rest)
    ankle_rest = Pr["foot_l"].z
    ball_rest = Pr["ball_l"].z
    lows = []
    for f in frames:
        W, P = rig.fk(f)
        lows.append(min(P["foot_l"].z - ankle_rest, P["foot_r"].z - ankle_rest,
                        P["ball_l"].z - ball_rest, P["ball_r"].z - ball_rest))
    lows.sort()
    off = lows[int(len(lows) * percentile)]
    for f in frames:
        f["hips"] = f["hips"] - Vector((0, 0, off))
    return off


def make_loop(rig, frames, blend_bones=None):
    """Make frames[0] == frames[-1] by spreading the end mismatch across the clip (rotations and
    hip height/sway); horizontal hip travel is handled by in_place()."""
    n = len(frames) - 1
    a, b = frames[0], frames[-1]
    for k in a["q"]:
        qa, qb = a["q"][k], b["q"][k]
        if qa.dot(qb) < 0:
            qb = -qb
        # delta that maps end -> start
        d = qa @ qb.inverted()
        for i, f in enumerate(frames):
            t = i / n
            corr = Quaternion().slerp(d, t)
            f["q"][k] = corr @ f["q"][k]
    dz = b["hips"].z - a["hips"].z
    for i, f in enumerate(frames):
        f["hips"].z -= dz * i / n
    # make quaternion signs continuous
    fix_signs(frames)


def in_place(frames, keep_sway=True):
    """Remove the linear horizontal hip travel (keeps the sway); returns the average speed (m/s)."""
    n = len(frames) - 1
    a = frames[0]["hips"].copy()
    b = frames[-1]["hips"].copy()
    dist = (Vector((b.x - a.x, b.y - a.y))).length
    for i, f in enumerate(frames):
        t = i / n
        f["hips"].x -= a.x + (b.x - a.x) * t
        f["hips"].y -= a.y + (b.y - a.y) * t
        if not keep_sway:
            f["hips"].x = 0.0
            f["hips"].y = 0.0
    return dist / (n / FPS), Vector((b.x - a.x, b.y - a.y))


def fix_signs(frames):
    for i in range(1, len(frames)):
        for k, q in frames[i]["q"].items():
            p = frames[i - 1]["q"].get(k)
            if p is not None and p.dot(q) < 0:
                frames[i]["q"][k] = -q


def find_period(rig, frames, lo, hi):
    """Gait period (frames) by matching the left/right foot relative positions."""
    feats = []
    for f in frames:
        W, P = rig.fk(f)
        h = P["pelvis"]
        feats.append([(P["foot_l"] - h).x, (P["foot_l"] - h).y, (P["foot_l"] - h).z,
                      (P["foot_r"] - h).x, (P["foot_r"] - h).y, (P["foot_r"] - h).z])
    best, bp = 1e9, lo
    n = len(feats)
    for p in range(lo, min(hi, n - 1) + 1):
        err = 0.0
        cnt = 0
        for i in range(0, n - p):
            err += sum((x - y) ** 2 for x, y in zip(feats[i], feats[i + p]))
            cnt += 1
        err /= max(cnt, 1)
        if err < best:
            best, bp = err, p
    return bp


def best_loop_start(rig, frames, period, search):
    """Start index in `search` range whose pose best matches start+period."""
    best, bs = 1e9, search[0]
    for s in search:
        if s + period >= len(frames):
            break
        a, b = frames[s], frames[s + period]
        err = 0.0
        for k in ("thigh_l", "thigh_r", "calf_l", "calf_r", "upperarm_l", "upperarm_r", "spine_03", "pelvis"):
            err += 1.0 - abs(a["q"][k].dot(b["q"][k]))
        err += abs(a["hips"].z - b["hips"].z) * 5
        if err < best:
            best, bs = err, s
    return bs


# ------------------------------------------------------------------------------------------ IK

def _frame_map(d0, h0, d1, h1):
    """Rotation mapping orthonormal pair (d0, h0) onto (d1, h1)."""
    def basis(d, h):
        d = d.normalized()
        h = (h - d * h.dot(d)).normalized()
        return Matrix((d, h, d.cross(h))).transposed()
    return basis(d1, h1) @ basis(d0, h0).inverted()


def two_bone_ik(rig, frame, upper, lower, end, target, pole, W=None, P=None, fold=None):
    """Analytic two-bone IK. The joint (elbow/knee) bends in the plane containing `pole`; twist is
    referenced to the rest pose so the hinge stays anatomical. fold: world direction the lower bone
    folds towards at rest (-Y for arms, +Y for legs; guessed from the bone name if None)."""
    if W is None:
        W, P = rig.fk(frame)
    if fold is None:
        fold = Vector((0, 1, 0)) if (upper.startswith("thigh") or upper.startswith("calf")) else Vector((0, -1, 0))
    a = P[upper]
    la = (rig.head[lower] - rig.head[upper]).length
    lb = (rig.head[end] - rig.head[lower]).length
    d = target - a
    dist = min(max(d.length, 1e-4), (la + lb) * 0.9995)
    dn = d.normalized()
    cos_a = (la * la + dist * dist - lb * lb) / (2 * la * dist)
    ang_a = math.acos(max(-1, min(1, cos_a)))
    pv = pole - a
    pn = (pv - dn * pv.dot(dn))
    if pn.length < 1e-6:
        pn = Vector((0, 0, 1)) - dn * dn.z
    pn.normalize()
    upper_dir = (dn * math.cos(ang_a) + pn * math.sin(ang_a)).normalized()
    joint = a + upper_dir * la
    lower_dir = (target - joint).normalized()
    # rest frames
    ur, lr = rig.rest[upper], rig.rest[lower]
    ud_r = (rig.head[lower] - rig.head[upper]).normalized()
    ld_r = (rig.head[end] - rig.head[lower]).normalized()
    hinge_r = ud_r.cross(fold).normalized()
    # posed fold direction: lower bone deviation from the upper axis (or away from the pole side)
    dev = lower_dir - upper_dir * lower_dir.dot(upper_dir)
    if dev.length < 1e-4:
        dev = -pn
    hinge = upper_dir.cross(dev.normalized()).normalized()
    Wu = _frame_map(ud_r, hinge_r, upper_dir, hinge) @ ur
    rig.set_world(frame, W, upper, Wu)
    Wl = _frame_map(ld_r, hinge_r, lower_dir, hinge) @ lr
    rig.set_world(frame, W, lower, Wl)
    # positions of the children changed: refresh P for callers that keep using it
    W2, P2 = rig.fk(frame)
    W.update(W2)
    P.update(P2)
    return W


def aim_bone_world(Wcur, new_dir):
    """Minimal rotation of world matrix so its Y axis points along new_dir."""
    q = Wcur.col[1].normalized().rotation_difference(new_dir.normalized())
    return q.to_matrix() @ Wcur


# ------------------------------------------------------------------------------------------ write

def write_action(arm, clip):
    act = bpy.data.actions.new(clip.name)
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    fix_signs(clip.frames)
    rig_rest = {b.name: b for b in arm.data.bones}
    pel = arm.pose.bones["pelvis"]
    Rp = rig_rest["pelvis"].matrix_local.to_3x3()
    head = rig_rest["pelvis"].head_local
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    for i, f in enumerate(clip.frames):
        for pb in arm.pose.bones:
            q = f["q"].get(pb.name, Quaternion())
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=i, group=pb.name)
            if pb.name == "pelvis":
                pb.location = Rp.inverted() @ (f["hips"] - head)
                pb.keyframe_insert("location", frame=i, group=pb.name)
            elif pb.name == "Root":
                pb.location = (0, 0, 0)
                pb.keyframe_insert("location", frame=i, group=pb.name)
    # rest-pose key one frame before the clip: the exporter writes the skeleton's default (Lcl)
    # transforms from the current frame, so exporting from frame -1 stores the true rest pose
    # (Unity builds avatars from it); the baked take still covers 0..N only.
    for pb in arm.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.keyframe_insert("rotation_quaternion", frame=REST_FRAME, group=pb.name)
        if pb.name in ("pelvis", "Root"):
            pb.location = (0, 0, 0)
            pb.keyframe_insert("location", frame=REST_FRAME, group=pb.name)
    sc = bpy.context.scene
    sc.frame_start = 0
    sc.frame_end = len(clip.frames) - 1
    sc.render.fps = FPS
    # linear interpolation (frames are already dense)
    try:
        fcurves = act.fcurves
    except AttributeError:
        fcurves = []
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags:
                    fcurves.extend(cb.fcurves)
    for fc in fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    return act
