# Author : Baptiste Vu Liem
"""The ONE hand/forearm module of the whole cast (and of the first-person arms).

A big readable gloved hand: a thumb, a separate index finger and a fused three-finger paddle
(middle + ring + pinky, skinned equally to the three chains, so all 30 finger bones still exist and
any curl of the three chains bends the paddle as one). Bare hands are the same mesh with the skin
material; gloves add a cuff and stitching.

Hand frame (any side): origin = wrist joint, u = along the fingers, v = toward the thumb,
w = palm normal (the palm faces +w). In the shared A-pose: u = arm direction, v = -Y (thumbs
forward), w = down and in. For the right hand the frame is the mirror image (left-handed), so the
same local layout gives the mirrored hand. skeleton.Build.hand_frame(side) returns it.

No bpy here except in make_objects(): the first-person track can import the layout, the parts and
the weights from its own Blender scripts (see README.md).
"""
import numpy as np

import sdf

# Joint layout in the hand frame (metres). Fingers: 4 points = heads of _01, _02, _03 and the tip.
HAND = {
    "hand_tail": (0.046, 0.0, 0.0),
    "index": [(0.094, 0.030, -0.003), (0.137, 0.031, -0.003), (0.164, 0.031, -0.002), (0.188, 0.031, -0.001)],
    "middle": [(0.096, 0.004, -0.003), (0.142, 0.004, -0.003), (0.171, 0.004, -0.002), (0.196, 0.004, -0.001)],
    "ring": [(0.093, -0.016, -0.002), (0.136, -0.016, -0.002), (0.163, -0.016, -0.001), (0.186, -0.016, 0.000)],
    "pinky": [(0.086, -0.035, -0.001), (0.119, -0.036, -0.001), (0.140, -0.037, 0.000), (0.160, -0.037, 0.001)],
    "thumb": [(0.022, 0.022, 0.006), (0.052, 0.051, 0.025), (0.078, 0.071, 0.038), (0.102, 0.086, 0.048)],
}
FINGER_R = {"index": (0.0106, 0.0100, 0.0094, 0.0088), "middle": (0.0106, 0.0101, 0.0095, 0.0089),
            "ring": (0.0104, 0.0099, 0.0093, 0.0087), "pinky": (0.0096, 0.0092, 0.0087, 0.0082),
            "thumb": (0.0150, 0.0128, 0.0118, 0.0104)}
PADDLE = ("middle", "ring", "pinky")
VOXEL = 0.0010
SCALE = 1.10            # big readable hands: the whole module is authored at 1.0 and scaled about the wrist
LAYOUT = {k: ([tuple(c * SCALE for c in p) for p in v] if isinstance(v, list) else tuple(c * SCALE for c in v))
          for k, v in HAND.items()}


class Frame:
    def __init__(self, origin, u, v, w):
        self.o, self.u, self.v, self.w = (np.asarray(x, dtype=np.float64) for x in (origin, u, v, w))
        self.R = np.stack([self.u, self.v, self.w], axis=1)

    def p(self, local):
        l = np.asarray(local, dtype=np.float64)
        return self.o + self.u * l[0] + self.v * l[1] + self.w * l[2]

    def d(self, local):
        l = np.asarray(local, dtype=np.float64)
        return self.u * l[0] + self.v * l[1] + self.w * l[2]


def hand_prims(fr, style="glove"):
    """(prims for the hand Part as (prim, k) in order). style: 'glove' | 'bare' | 'tactical'."""
    P = fr.p
    out = []
    # palm: rounded box + thenar / hypothenar mounds, wrist neck
    out.append((sdf.Box(P((0.050, -0.003, 0.0)), (0.047, 0.042, 0.0155), fr.R, 0.0145), 0.0))
    out.append((sdf.Ellipsoid(P((0.034, 0.020, 0.009)), (0.033, 0.021, 0.0145), fr.R), 0.012))
    out.append((sdf.Ellipsoid(P((0.042, -0.029, 0.007)), (0.036, 0.016, 0.0115), fr.R), 0.012))
    out.append((sdf.Scaled(sdf.RoundCone(P((-0.012, 0, 0)), P((0.026, 0, 0)), 0.0275, 0.030), P((0, 0, 0)), fr.R, (1, 1, 0.80)), 0.02))
    # knuckle ridge: a soft roll across the back of the hand
    out.append((sdf.Capsule(P((0.088, 0.024, -0.006)), P((0.084, -0.034, -0.005)), 0.0118), 0.012))
    # paddle: middle + ring + pinky fused
    for f in PADDLE:
        pts = [P(p) for p in HAND[f]]
        for c in sdf.tube(pts, FINGER_R[f]):
            out.append((c, 0.012))
    # thumb
    pts = [P(p) for p in HAND["thumb"]]
    for i, c in enumerate(sdf.tube(pts, FINGER_R["thumb"])):
        out.append((c, 0.012 if i == 0 else 0.004))
    # index: separate (small blend so it never webs to the paddle)
    pts = [P(p) for p in HAND["index"]]
    for i, c in enumerate(sdf.tube(pts, FINGER_R["index"])):
        out.append((c, 0.004 if i == 0 else 0.0))
    if style in ("glove", "tactical"):
        # flared cuff round the wrist
        cuff = sdf.CappedCone(P((-0.062, 0, 0)), P((0.010, 0, 0)), 0.0355, 0.0375, 0.004)
        out.append((sdf.Scaled(cuff, P((0, 0, 0)), fr.R, (1, 1, 0.84)), 0.003))
    if style == "tactical":
        out.append((sdf.Box(P((0.090, -0.004, -0.017)), (0.012, 0.040, 0.0045), fr.R, 0.004), 0.004))
        out.append((sdf.Box(P((0.040, -0.002, -0.018)), (0.026, 0.032, 0.003), fr.R, 0.003), 0.006))
    return out


def hand_subs(fr, style="glove"):
    """Subtractions: the grooves that read the paddle as three fingers, stitching channels."""
    P = fr.p
    out = []
    for vv in (-0.006, -0.026):
        for ww in (-0.0098, 0.0098):
            out.append((sdf.Capsule(P((0.128, vv, ww)), P((0.205, vv, ww)), 0.0016), 0.0015))
    if style in ("glove", "tactical"):
        for vv in (0.020, 0.0, -0.021):
            out.append((sdf.Capsule(P((0.020, vv, -0.0162)), P((0.078, vv * 1.1, -0.0168)), 0.0010), 0.0008))
        # cuff opening (the forearm/sleeve goes in)
        out.append((sdf.Scaled(sdf.CappedCone(P((-0.075, 0, 0)), P((-0.052, 0, 0)), 0.031, 0.031, 0.002),
                               P((0, 0, 0)), fr.R, (1, 1, 0.84)), 0.0))
    return out


WRIST_CUT = -0.070      # the body's forearm stops here; the hand Part carries the same forearm surface on


def hand_part(name, fr, style="glove", material="Glove", elbow=None, forearm_r=0.041, ease=0.0):
    """The hand. With `elbow`, the bare hand also carries the forearm (same SDF as the body's) from
    WRIST_CUT on, so skin hands join the body without a seam (see body_cut). `ease` inflates the
    whole hand (a glove over a bare hand)."""
    part = sdf.Part(name, VOXEL, material)
    for prim, k in hand_prims(fr, style):
        part.add(sdf.UniformScaled(prim, fr.o, SCALE), k * SCALE)
    if elbow is not None:
        for i, (prim, k) in enumerate(forearm_prims(elbow, fr, forearm_r)):
            part.add(prim, 0.02 if i == 0 else k)
        part.inter(sdf.HalfSpace(fr.p((WRIST_CUT, 0, 0)), -fr.u), 0.0)
    if ease:
        part.offset(ease)
    for prim, k in hand_subs(fr, style):
        part.sub(sdf.UniformScaled(prim, fr.o, SCALE), k * SCALE)
    part.meta["style"] = style
    return part


def body_cut(fr):
    """Box removing the body's forearm beyond WRIST_CUT (the hand Part continues it)."""
    return sdf.Box(fr.p((WRIST_CUT + 0.15, 0, 0)), (0.15, 0.09, 0.09), fr.R, 0.0)


def forearm_prims(elbow, fr, r_elbow=0.041, r_wrist=0.0285 * SCALE):
    """Bare forearm from the elbow to just past the wrist (flattened along the palm normal)."""
    wrist_in = fr.p((0.012, 0, 0))
    cone = sdf.RoundCone(np.asarray(elbow, dtype=np.float64), wrist_in, r_elbow, r_wrist)
    mid = np.asarray(elbow) * 0.7 + fr.o * 0.3
    bulge = sdf.Ellipsoid(mid + fr.v * 0.004, (0.060, r_elbow * 1.02, r_elbow * 0.92), fr.R)
    return [(sdf.Scaled(cone, fr.o, fr.R, (1, 1, 0.86)), 0.0), (bulge, 0.03)]


def forearm_part(name, elbow, fr, material="Skin", r_elbow=0.041):
    part = sdf.Part(name, 0.0015, material)
    for prim, k in forearm_prims(elbow, fr, r_elbow):
        part.add(prim, k)
    return part


# ------------------------------------------------------------------------------------- skinning

def _seg_dist(P, a, b):
    ab = b - a
    t = np.clip(((P - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1), t


def hand_weights(P, fr, side):
    """Vertex weights {bone name: (N,) array} for hand vertices P (world space), capsule distance based.
    The paddle region is shared equally by the middle, ring and pinky chains."""
    P = np.asarray(P, dtype=np.float64)
    s = "_" + side
    bones = {}
    o = fr.o
    bones["lowerarm" + s] = (o + fr.u * -0.25, o + fr.u * -0.01)
    bones["hand" + s] = (o, fr.p((0.080 * SCALE, -0.004, 0.0)))
    for f in ("thumb", "index") + PADDLE:
        pts = [fr.p(p) for p in LAYOUT[f]]
        for i in range(3):
            bones["%s_%02d%s" % (f, i + 1, s)] = (pts[i], pts[i + 1])
    names = list(bones)
    D = np.stack([_seg_dist(P, *bones[n])[0] for n in names], axis=1)
    W = 1.0 / np.maximum(D, 0.002) ** 4
    # palm and wrist: never pulled by the far finger segments
    for i, n in enumerate(names):
        if n.endswith(("_02" + s, "_03" + s)):
            W[:, i] *= (D[:, i] < 0.03)
    W /= W.sum(1, keepdims=True)
    out = {n: W[:, i] for i, n in enumerate(names)}
    # paddle: the three chains share each segment's weight equally
    for i in (1, 2, 3):
        group = ["%s_%02d%s" % (f, i, s) for f in PADDLE]
        tot = sum(out[g] for g in group)
        for g in group:
            out[g] = tot / 3.0
    return out


def make_objects(fr, side, style="glove", material="Glove", elbow=None, name_prefix="", collection=None):
    """Blender objects (needs bpy): the hand (carrying the forearm from WRIST_CUT when `elbow` is
    given), with vertex groups named after the 53-bone skeleton."""
    import blmesh
    obs = []
    hp = hand_part("%sHand_%s" % (name_prefix, side), fr, style, material, elbow=elbow)
    ob = blmesh.build_part(hp, collection=collection)
    if ob is not None:
        co = np.empty(len(ob.data.vertices) * 3, np.float32)
        ob.data.vertices.foreach_get("co", co)
        for bone, wts in hand_weights(co.reshape(-1, 3), fr, side).items():
            g = ob.vertex_groups.new(name=bone)
            for vi in np.nonzero(wts > 0.002)[0]:
                g.add([int(vi)], float(wts[vi]), "REPLACE")
        obs.append(ob)
    return obs


# ------------------------------------------------------------------------------------- first person

def frame_from_arm(wrist, along, thumb, side):
    """Hand frame from a wrist position, the direction toward the fingers and the thumb-side direction
    (both world space, need not be orthogonal). The right hand gets the mirrored (left-handed) frame."""
    u = np.asarray(along, dtype=np.float64)
    u = u / np.linalg.norm(u)
    v = np.asarray(thumb, dtype=np.float64)
    v = v - u * (v @ u)
    v = v / np.linalg.norm(v)
    w = np.cross(u, v) if side == "l" else -np.cross(u, v)
    return Frame(wrist, u, v, w)


def bone_segments(fr, side):
    """(head, tail) world positions of hand_<side> and the 15 finger bones for an armature builder."""
    s = "_" + side
    out = {"hand" + s: (fr.o, fr.p(LAYOUT["hand_tail"]))}
    for f in ("thumb", "index", "middle", "ring", "pinky"):
        pts = [fr.p(p) for p in LAYOUT[f]]
        for i in range(3):
            out["%s_%02d%s" % (f, i + 1, s)] = (pts[i], pts[i + 1])
    return out


def sleeve_parts(side, shoulder, elbow, fr, r_upper=(0.053, 0.041), ease=0.017, team=True, prefix="FP_"):
    """The hero's bomber sleeve as the third-person jacket builds it: nylon sleeve to the cuff line,
    rib-knit cuff over the glove cuff, team armband at 66 % of the upper arm."""
    shoulder, elbow = np.asarray(shoulder, dtype=np.float64), np.asarray(elbow, dtype=np.float64)
    sl = sdf.Part("%sSleeve_%s" % (prefix, side), 0.0025, "Jacket")
    sl.add(sdf.RoundCone(shoulder, elbow, r_upper[0] + 0.005, r_upper[1] + 0.007))
    sl.add(sdf.Sphere(elbow, r_upper[1] * 0.98 + 0.004), 0.02)
    sl.add(sdf.RoundCone(elbow, fr.p((-0.08, 0, 0)), r_upper[1] + 0.005, 0.035), 0.02)
    sl.offset(ease)
    sl.sub(sdf.CappedCone(fr.p((-0.072, 0, 0)), fr.p((0.4, 0, 0)), 0.09, 0.09), 0.004)
    rib = sdf.Part("%sCuff_%s" % (prefix, side), 0.0020, "Rib")
    rib.add(sdf.Scaled(sdf.CappedCone(fr.p((-0.084, 0, 0)), fr.p((-0.034, 0, 0)), 0.0475, 0.0455, 0.007), fr.o, fr.R, (1, 1, 0.90)))
    parts = [sl, rib]
    if team:
        c = shoulder + (elbow - shoulder) * 0.66
        r = r_upper[0] + (r_upper[1] - r_upper[0]) * 0.66 + 0.005 + ease + 0.0115
        band = sdf.Part("%sArmband_%s" % (prefix, side), 0.0018, "Team")
        band.add(sdf.Ring(c, elbow - shoulder, r, 0.0040, 0.022, 0.0032))
        parts.append(band)
    return parts


def arm_weights(P, side, shoulder, elbow, fr):
    """Weights {bone: (N,)} for sleeve / forearm vertices: upperarm, lowerarm, hand, blended at the joints."""
    P = np.asarray(P, dtype=np.float64)
    s = "_" + side
    segs = {"upperarm" + s: (np.asarray(shoulder), np.asarray(elbow)), "lowerarm" + s: (np.asarray(elbow), fr.o),
            "hand" + s: (fr.o, fr.p((0.08, 0, 0)))}
    names = list(segs)
    D = np.stack([_seg_dist(P, *segs[n])[0] for n in names], axis=1)
    W = 1.0 / np.maximum(D, 0.004) ** 4
    W /= W.sum(1, keepdims=True)
    return {n: W[:, i] for i, n in enumerate(names)}


def make_fp_arm(side, shoulder, elbow, fr, glove_style="glove", collection=None, team=True):
    """Blender objects (needs bpy) for one first-person arm: glove + sleeve + rib cuff (+ armband),
    vertex groups named after the shared skeleton. Materials are left to the caller (slot names in
    obj['cast_material'])."""
    import blmesh
    obs = make_objects(fr, side, glove_style, "Glove", None, "FP_", collection)
    for part in sleeve_parts(side, shoulder, elbow, fr, team=team):
        ob = blmesh.build_part(part, collection=collection)
        if ob is None:
            continue
        co = np.empty(len(ob.data.vertices) * 3, np.float32)
        ob.data.vertices.foreach_get("co", co)
        for bone, wts in arm_weights(co.reshape(-1, 3), side, shoulder, elbow, fr).items():
            g = ob.vertex_groups.new(name=bone)
            for vi in np.nonzero(wts > 0.002)[0]:
                g.add([int(vi)], float(wts[vi]), "REPLACE")
        obs.append(ob)
    return obs
