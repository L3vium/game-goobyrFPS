# Author : Baptiste Vu Liem
"""Body metrics and the shared 53-bone skeleton of the cast (UE-mannequin names, as CharacterBuilder.BONES).

Blender space: metres, Z up, the character faces -Y, +X is the character's left, feet on Z = 0.
Rest pose = the shared A-pose of the current pipeline (chr_common.REST_ARM_ANGLE): arms straight
45 degrees below horizontal in the frontal plane, palms down/in, thumbs forward, legs straight.

Contract (Core/BodyMetrics): top of the skull 1.80 m (hair and hats extra), eyes 1.60 m.
Mocap-safe proportions (brief): head 0.30-0.32 m, shoulder joints 0.36-0.40 m apart, real arm
length, hip joints >= 0.88 m, the mass low (belly, hips), narrow at the armpits.
"""
import math

import numpy as np

HEIGHT = 1.80          # top of the skull
EYE_Z = 1.60           # Core/BodyMetrics.EYE
CHIN_Z = 1.488         # head = 0.312 m
ARM_ANGLE = 45.0       # A-pose, degrees below horizontal

BONE_NAMES = [
    "Root", "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
    "thigh_l", "calf_l", "foot_l", "ball_l", "thigh_r", "calf_r", "foot_r", "ball_r",
] + ["%s_%02d_%s" % (f, i, s) for s in ("l", "r") for f in ("thumb", "index", "middle", "ring", "pinky") for i in (1, 2, 3)]

PARENT = {
    "Root": None, "pelvis": "Root", "spine_01": "pelvis", "spine_02": "spine_01", "spine_03": "spine_02",
    "neck_01": "spine_03", "head": "neck_01",
}
for _s in ("l", "r"):
    PARENT.update({
        "clavicle_" + _s: "spine_03", "upperarm_" + _s: "clavicle_" + _s, "lowerarm_" + _s: "upperarm_" + _s,
        "hand_" + _s: "lowerarm_" + _s,
        "thigh_" + _s: "pelvis", "calf_" + _s: "thigh_" + _s, "foot_" + _s: "calf_" + _s, "ball_" + _s: "foot_" + _s,
    })
    for _f in ("thumb", "index", "middle", "ring", "pinky"):
        PARENT["%s_01_%s" % (_f, _s)] = "hand_" + _s
        PARENT["%s_02_%s" % (_f, _s)] = "%s_01_%s" % (_f, _s)
        PARENT["%s_03_%s" % (_f, _s)] = "%s_02_%s" % (_f, _s)

assert len(BONE_NAMES) == 53


def V(*a):
    return np.array(a, dtype=np.float64)


class Build:
    """Skeleton-level description of one body (joints in world space, A-pose)."""

    def __init__(self, shoulder_x=0.19, hip_x=0.09, female=False, arm_scale=1.0):
        self.female = female
        self.shoulder_x = shoulder_x
        self.hip_x = hip_x
        a = math.radians(ARM_ANGLE)
        self.arm_dir = {"l": V(math.cos(a), 0, -math.sin(a)), "r": V(-math.cos(a), 0, -math.sin(a))}
        self.upper_arm = 0.300 * arm_scale
        self.forearm = 0.265 * arm_scale
        j = {}
        j["root"] = V(0, 0, 0)
        j["pelvis"] = V(0, 0.000, 0.935)
        j["spine_01"] = V(0, 0.012, 1.010)
        j["spine_02"] = V(0, 0.008, 1.085)
        j["spine_03"] = V(0, 0.012, 1.165)
        j["spine_03_tail"] = V(0, 0.030, 1.330)
        j["neck"] = V(0, 0.010, 1.425)
        j["head"] = V(0, 0.000, EYE_Z - 0.040)     # BodyMetrics.HEAD_BONE_TO_EYE: HumanBody's eye normalisation stays 1
        j["head_tail"] = V(0, 0.000, HEIGHT)
        for s, sx in (("l", 1.0), ("r", -1.0)):
            j["clavicle_" + s] = V(sx * 0.025, -0.022, 1.405)
            j["shoulder_" + s] = V(sx * shoulder_x, 0.0, 1.390)
            j["elbow_" + s] = j["shoulder_" + s] + self.arm_dir[s] * self.upper_arm
            j["wrist_" + s] = j["elbow_" + s] + self.arm_dir[s] * self.forearm
            j["hip_" + s] = V(sx * hip_x, 0.006, 0.900)
            j["knee_" + s] = V(sx * (hip_x + 0.012), 0.000, 0.490)
            j["ankle_" + s] = V(sx * (hip_x + 0.022), 0.012, 0.078)
            j["ball_" + s] = V(sx * (hip_x + 0.026), -0.128, 0.014)
            j["toe_" + s] = V(sx * (hip_x + 0.026), -0.198, 0.014)
        self.j = j

    def hand_frame(self, side):
        """(origin, u = along the fingers, v = toward the thumb, w = palm normal) of the A-pose hand."""
        u = self.arm_dir[side]
        v = V(0, -1, 0)
        w = np.cross(u, v) if side == "l" else -np.cross(u, v)
        return self.j["wrist_" + side], u, v, w

    def bones(self, hand_layout):
        """Bone (head, tail) for the 53 bones; fingers from the shared glove layout."""
        j = self.j
        b = {
            "Root": (j["root"], j["root"] + V(0, 0.03, 0)),
            "pelvis": (j["pelvis"], j["spine_01"]),
            "spine_01": (j["spine_01"], j["spine_02"]),
            "spine_02": (j["spine_02"], j["spine_03"]),
            "spine_03": (j["spine_03"], j["spine_03_tail"]),
            "neck_01": (j["neck"], j["head"]),
            "head": (j["head"], j["head_tail"]),
        }
        for s in ("l", "r"):
            b["clavicle_" + s] = (j["clavicle_" + s], j["shoulder_" + s])
            b["upperarm_" + s] = (j["shoulder_" + s], j["elbow_" + s])
            b["lowerarm_" + s] = (j["elbow_" + s], j["wrist_" + s])
            o, u, v, w = self.hand_frame(s)
            L = lambda p: o + u * p[0] + v * p[1] + w * p[2]
            b["hand_" + s] = (o, L(hand_layout["hand_tail"]))
            for f in ("thumb", "index", "middle", "ring", "pinky"):
                pts = hand_layout[f]
                for i in range(3):
                    b["%s_%02d_%s" % (f, i + 1, s)] = (L(pts[i]), L(pts[i + 1]))
            b["thigh_" + s] = (j["hip_" + s], j["knee_" + s])
            b["calf_" + s] = (j["knee_" + s], j["ankle_" + s])
            b["foot_" + s] = (j["ankle_" + s], j["ball_" + s])
            b["ball_" + s] = (j["ball_" + s], j["toe_" + s])
        return b


def segment_point(a, b, t):
    return a + (b - a) * t
