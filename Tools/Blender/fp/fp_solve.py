# Author : Baptiste Vu Liem
"""Pose solving for the first-person arms (armature at the origin, so armature space = scene space).

A Pose holds the wanted armature-space matrix of every bone. Bones not set follow their parent
rigidly. apply() turns them into pose-bone matrix_basis values (no depsgraph round trips), key()
keyframes them.

  * arm(): analytic two-bone IK - shoulder to wrist, elbow on the side of a pole direction; the
    upper arm and forearm roll so the elbow is a pure hinge, the two twist bones take 40 % / 75 % of
    the hand's roll about the forearm (no candy-wrapper wrist).
  * curl(): closes a finger joint by joint onto a mesh (BVH) until the phalanx pad touches it,
    within joint limits - the hand wraps the real grip surface instead of a generic fist.
"""
import math

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

import fp_rig as R

FINGER_LIMITS = ((-12.0, 95.0), (0.0, 108.0), (0.0, 80.0))
THUMB_LIMITS = ((-25.0, 60.0), (-10.0, 70.0), (-15.0, 85.0))


def axis_frame(y_axis, z_hint, head):
    """Bone matrix with +Y along y_axis, +Z as close to z_hint as possible."""
    y = Vector(y_axis).normalized()
    z = Vector(z_hint) - y * Vector(z_hint).dot(y)
    if z.length < 1e-6:
        z = y.orthogonal()
    z.normalize()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = head
    return m


class Pose:
    def __init__(self, arm):
        self.arm = arm
        self.rest = {b.name: b.matrix_local.copy() for b in arm.data.bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
        self.order = [b.name for b in arm.data.bones]  # parents come before children
        self.P = {}
        self.report = {}

    # ---------------------------------------------------------------- matrices
    def get(self, name):
        """Current wanted matrix of a bone (following its parent if not set)."""
        if name in self.P:
            return self.P[name]
        p = self.parent[name]
        if p is None:
            return self.rest[name]
        return self.get(p) @ self.rest[p].inverted() @ self.rest[name]

    def set(self, name, m):
        self.P[name] = m.copy()

    def follow(self, name):
        """Forget a bone's own matrix: it follows its parent again."""
        self.P.pop(name, None)

    def rotate_about(self, name, rot3, pivot=None):
        """Rotates a bone (and so its children) by a world rotation about a pivot (its head)."""
        m = self.get(name)
        c = m.translation if pivot is None else Vector(pivot)
        r = Matrix.Translation(c) @ rot3.to_4x4() @ Matrix.Translation(-c)
        self.set(name, r @ m)

    def local_rot(self, name, axis, deg):
        """Rotates a bone about one of its own axes ('X', 'Y', 'Z') from the parent-following pose."""
        base = self.get(self.parent[name]) @ self.rest[self.parent[name]].inverted() @ self.rest[name]
        self.set(name, base @ Matrix.Rotation(math.radians(deg), 4, axis))

    # ---------------------------------------------------------------- arms
    def arm_ik(self, side, wrist_target, hand_rot3, pole, reach_slack=0.002):
        """Puts the hand bone at wrist_target with rotation hand_rot3 (its +Y the fingers, +Z the palm
        normal), solving the elbow towards the pole direction. Returns the unreachable distance."""
        s = side
        sh = self.get("upperarm_" + s).translation.copy()
        a, b = R.UPPER_ARM, R.FOREARM
        w = Vector(wrist_target)
        d = w - sh
        dist = d.length
        miss = 0.0
        if dist > a + b - reach_slack:
            miss = dist - (a + b - reach_slack)
            dist = a + b - reach_slack
            w = sh + d.normalized() * dist
        u = d.normalized()
        v = Vector(pole) - u * Vector(pole).dot(u)
        if v.length < 1e-6:
            v = u.orthogonal()
        v.normalize()
        cos_a = (a * a + dist * dist - b * b) / (2 * a * dist)
        alpha = math.acos(max(-1.0, min(1.0, cos_a)))
        e = sh + (u * math.cos(alpha) + v * math.sin(alpha)) * a
        h = v.cross(u).normalized()  # hinge: rotating the upper arm by the elbow angle about h gives the forearm
        # the rest frames have +Z on the palm side; bending towards the thumb is about -Z on the
        # right arm and +Z on the left one, so the hinge maps to -h / +h
        zs = -h if s == "r" else h
        up = axis_frame(e - sh, zs, sh)
        lo = axis_frame(w - e, zs, e)
        self.set("upperarm_" + s, up)
        self.set("lowerarm_" + s, lo)
        hand = hand_rot3.to_4x4()
        hand.translation = w
        self.set("hand_" + s, hand)
        # twist: the hand's roll about the forearm, shared down the twist bones
        y = (w - e).normalized()
        z_lo = lo.to_3x3().col[2]
        z_h = hand_rot3.col[2] - y * hand_rot3.col[2].dot(y)
        twist = 0.0
        if z_h.length > 1e-6:
            z_h.normalize()
            twist = math.atan2(z_lo.cross(z_h).dot(y), z_lo.dot(z_h))
        for tw, frac in R.TWIST.items():
            bn = tw + "_" + s
            base = lo @ self.rest["lowerarm_" + s].inverted() @ self.rest[bn]
            rot = Matrix.Rotation(twist * frac, 4, y)
            m = Matrix.Translation(base.translation) @ rot @ Matrix.Translation(-base.translation) @ base
            self.set(bn, m)
        self.report[s + "_twist_deg"] = math.degrees(twist)
        self.report[s + "_miss"] = miss
        self.report[s + "_elbow_deg"] = math.degrees((e - sh).angle(w - e)) if dist > 1e-4 else 0.0
        return miss

    # ---------------------------------------------------------------- fingers
    def finger_set(self, name, side, angles, spread=0.0):
        """Sets a finger's three joints (curl degrees about each bone's X; spread about the first
        bone's Z, positive towards the thumb side)."""
        for i in range(3):
            bn = f"{name}_0{i + 1}_{side}"
            self.follow(bn)
            base = self.get(bn)
            m = base @ Matrix.Rotation(math.radians(angles[i]), 4, "X")
            if i == 0 and spread:
                sgn = 1.0 if side == "r" else -1.0
                m = base @ Matrix.Rotation(math.radians(spread * sgn), 4, "Z") @ Matrix.Rotation(math.radians(angles[i]), 4, "X")
            self.set(bn, m)

    def segment(self, bn):
        m = self.get(bn)
        b = self.arm.data.bones[bn]
        return m.translation.copy(), (m @ Vector((0, b.length, 0))), m

    def curl(self, name, side, bvh, start=(4.0, 6.0, 4.0), gap=0.0015, step=1.0, limits=None,
             spread=0.0, radius_scale=1.0, stop_after=None):
        """Closes a finger onto the mesh in `bvh` (scene space), joint after joint, each until any
        phalanx from that joint out touches the surface (pad radius + gap). Returns the angles."""
        limits = limits or (THUMB_LIMITS if name == "thumb" else FINGER_LIMITS)
        angles = list(start)
        r0, r1 = (R.THUMB_SPEC["r"] if name == "thumb" else R.FINGER_SPEC[name]["r"])
        radii = [r0 * radius_scale, (r0 + r1) * 0.5 * radius_scale, r1 * radius_scale]
        joints = range(3) if stop_after is None else range(stop_after + 1)

        def apply():
            if name == "thumb":
                self.thumb_set(side, angles)
            else:
                self.finger_set(name, side, angles, spread)

        def touching(first):
            for k in range(first, 3):
                h, t, _ = self.segment(f"{name}_0{k + 1}_{side}")
                samples = [h.lerp(t, s) for s in np.linspace(0.15, 1.0, 5)]
                if k == 2:  # the glove's fingertip reaches past the bone's end
                    samples.append(t + (t - h).normalized() * radii[2] * 0.7)
                for p in samples:
                    hit = bvh.find_nearest(p)
                    if hit[0] is None:
                        continue
                    loc, nrm, _, dist = hit
                    inside = (p - loc).dot(nrm) < 0
                    if inside or dist < radii[k] + gap:
                        return True
            return False

        apply()
        for j in joints:
            lo, hi = limits[j]
            if touching(j):
                # already in contact: open until clear (never sink into the grip)
                while angles[j] > lo and touching(j):
                    angles[j] -= step
                    apply()
                continue
            while angles[j] + step <= hi:
                angles[j] += step
                apply()
                if touching(j):
                    angles[j] -= step
                    apply()
                    break
        return angles

    def trigger(self, side, face, forward, bvh, radius=None):
        """Index on a trigger: searches the three joint angles (distal coupled to the middle one) that
        put the distal pad - its axis 45 % along, a pad radius in front of the face - on the trigger
        face point, without the finger entering the mesh. Returns the remaining error (m)."""
        r = radius or R.FINGER_SPEC["index"]["r"][1]
        target = Vector(face) + Vector(forward).normalized() * r
        best = []
        for a in range(-6, 72, 2):
            for b in range(0, 104, 3):
                c = min(80.0, 0.65 * b + 4)
                self.finger_set("index", side, (a, b, c))
                h, t, _ = self.segment(f"index_03_{side}")
                err = (h.lerp(t, 0.45) - target).length
                best.append((err, a, b, c))
        best.sort()
        for err, a, b, c in best[:40]:
            self.finger_set("index", side, (a, b, c))
            clear = True
            for k in range(2):  # the first two phalanges must stay out of the frame and the guard
                h, t, _ = self.segment(f"index_0{k + 1}_{side}")
                for s in np.linspace(0.2, 1.0, 4):
                    p = h.lerp(t, s)
                    loc, nrm, _, dist = bvh.find_nearest(p)
                    if loc is not None and ((p - loc).dot(nrm) < 0 or dist < R.FINGER_SPEC["index"]["r"][0] * 0.85):
                        clear = False
            if clear:
                self.report[side + "_trigger_err_mm"] = err * 1000
                return err
        err, a, b, c = best[0]
        self.finger_set("index", side, (a, b, c))
        self.report[side + "_trigger_err_mm"] = err * 1000
        return err

    def reach(self, name, side, target, bvh, start=None, iters=60):
        """Puts a digit's pad on a target point (scene space) by coordinate descent over its joints
        (thumb: abduction, roll and three curls; fingers: spread and three curls), penalising any
        sample of the digit inside the mesh or closer to it than its radius. Returns the error (m)."""
        thumb = name == "thumb"
        r0, r1 = (R.THUMB_SPEC["r"] if thumb else R.FINGER_SPEC[name]["r"])
        radii = [r0, (r0 + r1) * 0.5, r1]
        lim = ([(-70, 70), (-70, 70)] + list(THUMB_LIMITS)) if thumb else ([(-15, 15)] + list(FINGER_LIMITS))
        x = list(start) if start else [0.0] * len(lim)
        target = Vector(target)

        def pose(v):
            if thumb:
                self.thumb_swing(side, v[0], v[1])
                self.thumb_set(side, v[2:5])
            else:
                self.finger_set(name, side, v[1:4], spread=v[0])

        def cost(v):
            pose(v)
            h, t, m = self.segment(f"{name}_03_{side}")
            pad = h.lerp(t, 0.5) + m.to_3x3().col[2].normalized() * radii[2] * 0.6
            c = (pad - target).length ** 2
            for k in range(3):
                hk, tk, _ = self.segment(f"{name}_0{k + 1}_{side}")
                for s in (0.35, 0.7, 1.0):
                    p = hk.lerp(tk, s)
                    loc, nrm, _, dist = bvh.find_nearest(p)
                    if loc is None:
                        continue
                    sd = dist if (p - loc).dot(nrm) >= 0 else -dist
                    pen = radii[k] * 0.9 - sd
                    if pen > 0:
                        c += 40.0 * pen * pen
            return c

        best = cost(x)
        # a coarse sweep of the two swing angles first (the descent alone stalls behind the frame)
        if thumb:
            for a in range(-60, 61, 15):
                for b in range(-60, 61, 15):
                    y = [float(a), float(b)] + x[2:]
                    cy = cost(y)
                    if cy < best:
                        x, best = y, cy
        step = 12.0
        for _ in range(iters):
            improved = False
            for i in range(len(x)):
                for d in (step, -step):
                    y = list(x)
                    y[i] = min(max(y[i] + d, lim[i][0]), lim[i][1])
                    cy = cost(y)
                    if cy < best - 1e-12:
                        x, best, improved = y, cy, True
            if not improved:
                step *= 0.5
                if step < 0.25:
                    break
        pose(x)
        h, t, m = self.segment(f"{name}_03_{side}")
        err = ((h.lerp(t, 0.5) + m.to_3x3().col[2].normalized() * radii[2] * 0.6) - target).length
        self.report[f"{side}_{name}_reach_mm"] = round(err * 1000, 1)
        return err, x

    def thumb_set(self, side, angles, swing=(0.0, 0.0)):
        """Thumb: curl about each bone's X (across the palm); swing = (abduction about the first
        bone's Z, opposition roll about its Y) in degrees."""
        sw = getattr(self, "_thumb_swing", {}).get(side, swing)
        for i in range(3):
            bn = f"thumb_0{i + 1}_{side}"
            self.follow(bn)
            base = self.get(bn)
            m = base
            if i == 0:
                sgn = 1.0 if side == "r" else -1.0
                m = m @ Matrix.Rotation(math.radians(sw[1] * sgn), 4, "Y") @ Matrix.Rotation(math.radians(sw[0] * sgn), 4, "Z")
            m = m @ Matrix.Rotation(math.radians(angles[i]), 4, "X")
            self.set(bn, m)

    def thumb_swing(self, side, abduction, roll):
        if not hasattr(self, "_thumb_swing"):
            self._thumb_swing = {}
        self._thumb_swing[side] = (abduction, roll)

    # ---------------------------------------------------------------- output
    def apply(self):
        """Writes matrix_basis on every pose bone from the wanted matrices."""
        for name in self.order:
            pb = self.arm.pose.bones[name]
            p = self.parent[name]
            want = self.get(name)
            if p is None:
                basis = self.rest[name].inverted() @ want
            else:
                basis = self.rest[name].inverted() @ self.rest[p] @ self.get(p).inverted() @ want
            pb.matrix_basis = basis

    def key(self, frame):
        for name in self.order:
            pb = self.arm.pose.bones[name]
            pb.keyframe_insert("location", frame=frame, group=name)
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=name)
            pb.keyframe_insert("scale", frame=frame, group=name)


def bvh_of(objs):
    """One BVH (scene space) of the evaluated meshes of several objects."""
    dg = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for ob in objs:
        ev = ob.evaluated_get(dg)
        me = ev.to_mesh()
        mw = ob.matrix_world
        base = len(verts)
        verts += [mw @ v.co for v in me.vertices]
        polys += [[base + i for i in p.vertices] for p in me.polygons]
        ev.to_mesh_clear()
    return BVHTree.FromPolygons(verts, polys)


def quat_smooth(prev, q):
    """Keeps consecutive quaternions in the same hemisphere (no flips in baked curves)."""
    return q if prev is None or prev.dot(q) >= 0 else -q
