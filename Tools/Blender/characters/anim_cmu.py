# Author : Baptiste Vu Liem
"""CMU Graphics Lab Motion Capture (ASF/AMC) reader, forward kinematics and retargeting onto the
MPFB game_engine rig (see chr_common for the rig conventions).

Data: http://mocap.cs.cmu.edu (free to use, credit "mocap.cs.cmu.edu"), downloaded on demand into
$GOOBYR_CMU_CACHE (default: <tmp>/goobyr_cmu).
"""
import math
import os
import tempfile
import urllib.request

from mathutils import Euler, Matrix, Quaternion, Vector

CMU_CACHE = os.environ.get("GOOBYR_CMU_CACHE", os.path.join(tempfile.gettempdir(), "goobyr_cmu"))
CMU_URL = "http://mocap.cs.cmu.edu/subjects/%02d/%s"

# CMU (x right->left?, y up, z forward) -> Blender (x = character left, -y forward, z up)
A = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))
AT = A.transposed()
UNIT = 0.0254 / 0.45  # ASF "length 0.45": inches / 0.45 -> metres

# target bone <- source bone(s); a tuple = slerp blend of several source world rotations
MAP = {
    "pelvis": "root",
    "spine_01": "lowerback",
    "spine_02": "upperback",
    "spine_03": "thorax",
    "neck_01": ("lowerneck", "upperneck"),
    "head": "head",
    "clavicle_l": "lclavicle", "clavicle_r": "rclavicle",
    "upperarm_l": "lhumerus", "upperarm_r": "rhumerus",
    "lowerarm_l": ("lradius", "lwrist"), "lowerarm_r": ("rradius", "rwrist"),
    "hand_l": "lhand", "hand_r": "rhand",
    "thigh_l": "lfemur", "thigh_r": "rfemur",
    "calf_l": "ltibia", "calf_r": "rtibia",
    "foot_l": "lfoot", "foot_r": "rfoot",
    "ball_l": "ltoes", "ball_r": "rtoes",
}


def fetch(subject, trial):
    os.makedirs(CMU_CACHE, exist_ok=True)
    out = []
    for fn in ("%02d.asf" % subject if subject < 10 else "%d.asf" % subject,
               "%02d_%02d.amc" % (subject, trial)):
        p = os.path.join(CMU_CACHE, fn)
        if not os.path.isfile(p) or os.path.getsize(p) < 100:
            url = CMU_URL % (subject, fn)
            print("[cmu] download", url, flush=True)
            urllib.request.urlretrieve(url, p)
        out.append(p)
    return out


class Skeleton:
    def __init__(self, path):
        self.bones = {}
        self.children = {"root": []}
        self.root_order = []
        self._parse(path)
        for n, b in self.bones.items():
            ax = [math.radians(a) for a in b["axis"]]
            b["C"] = Euler(ax, "XYZ").to_matrix()
            b["Cinv"] = b["C"].inverted()

    def _parse(self, path):
        lines = [l.strip() for l in open(path, encoding="utf-8", errors="ignore")]
        i = 0
        section = None
        cur = None
        while i < len(lines):
            l = lines[i]
            i += 1
            if not l or l.startswith("#"):
                continue
            if l.startswith(":"):
                section = l.split()[0]
                if section == ":root":
                    pass
                continue
            tok = l.split()
            if section == ":root" and tok[0] == "order":
                self.root_order = [t.lower() for t in tok[1:]]
            elif section == ":bonedata":
                if tok[0] == "begin":
                    cur = dict(dof=[], axis=(0, 0, 0), direction=Vector((0, 0, 0)), length=0.0)
                elif tok[0] == "name":
                    cur["name"] = tok[1]
                elif tok[0] == "direction":
                    cur["direction"] = Vector([float(t) for t in tok[1:4]])
                elif tok[0] == "length":
                    cur["length"] = float(tok[1]) * UNIT
                elif tok[0] == "axis":
                    cur["axis"] = tuple(float(t) for t in tok[1:4])
                elif tok[0] == "dof":
                    cur["dof"] = [t.lower() for t in tok[1:]]
                elif tok[0] == "end":
                    self.bones[cur["name"]] = cur
            elif section == ":hierarchy":
                if tok[0] in ("begin", "end"):
                    continue
                parent = tok[0]
                for c in tok[1:]:
                    self.children.setdefault(parent, []).append(c)
                    self.bones[c]["parent"] = parent

    def rest_direction(self, name):
        """Rest direction of a source bone in Blender coordinates."""
        return (A @ self.bones[name]["direction"]).normalized()


def read_amc(path):
    frames = []
    cur = None
    for l in open(path, encoding="utf-8", errors="ignore"):
        l = l.strip()
        if not l or l.startswith("#") or l.startswith(":"):
            continue
        tok = l.split()
        if tok[0].isdigit() and len(tok) == 1:
            cur = {}
            frames.append(cur)
        elif cur is not None:
            cur[tok[0]] = [float(t) for t in tok[1:]]
    return frames


def fk_frame(sk, fr):
    """World rotation (Blender coords, 3x3) of every CMU bone and root position (m, Blender coords)."""
    rv = fr.get("root", [0] * 6)
    vals = dict(zip(sk.root_order, rv))
    pos = Vector((vals.get("tx", 0), vals.get("ty", 0), vals.get("tz", 0))) * UNIT
    rroot = Euler([math.radians(vals.get(k, 0)) for k in ("rx", "ry", "rz")], "XYZ").to_matrix()
    world = {"root": rroot}
    joints = {"root": pos.copy()}
    stack = [("root", c) for c in sk.children.get("root", [])]
    while stack:
        parent, name = stack.pop()
        b = sk.bones[name]
        d = fr.get(name)
        ang = {"rx": 0.0, "ry": 0.0, "rz": 0.0}
        if d:
            for k, v in zip(b["dof"], d):
                if k in ang:
                    ang[k] = math.radians(v)
        M = Euler((ang["rx"], ang["ry"], ang["rz"]), "XYZ").to_matrix()
        W = world[parent] @ b["C"] @ M @ b["Cinv"]
        world[name] = W
        joints[name] = joints[parent] + (W @ b["direction"]) * b["length"]
        for c in sk.children.get(name, []):
            stack.append((name, c))
    wb = {k: A @ v @ AT for k, v in world.items()}
    jb = {k: A @ v for k, v in joints.items()}
    return wb, jb


def mirror_frame(wb, jb):
    """Mirror across the character's sagittal plane (x -> -x) and swap sides."""
    S = Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1)))

    def sw(n):
        if n.startswith("l") and n[1:] in SIDE_NAMES:
            return "r" + n[1:]
        if n.startswith("r") and n[1:] in SIDE_NAMES:
            return "l" + n[1:]
        return n

    w2 = {sw(k): S @ v @ S for k, v in wb.items()}
    j2 = {sw(k): S @ v for k, v in jb.items()}
    return w2, j2


SIDE_NAMES = {"hipjoint", "femur", "tibia", "foot", "toes", "clavicle", "humerus", "radius", "wrist", "hand",
              "fingers", "thumb"}


class Retargeter:
    """Maps CMU world rotations onto the target armature (rest = T-pose)."""

    def __init__(self, sk, rig):
        self.sk = sk
        self.rig = rig  # chr anim RigModel
        self.corr = {}
        for tb, src in MAP.items():
            s0 = src[0] if isinstance(src, tuple) else src
            if s0 == "root" or tb.startswith("clavicle"):
                # clavicles: CMU bone runs from the sternum up to the shoulder, ours runs flat;
                # both are anatomically neutral at rest -> no direction correction
                self.corr[tb] = Matrix.Identity(3)
                continue
            d_s = sk.rest_direction(s0)
            if tb.startswith("upperarm") or tb.startswith("lowerarm") or tb.startswith("hand"):
                # CMU arms rest exactly along +-X (T-pose); ours is the shared A-pose: the
                # correction is then a pure rotation in the frontal plane (exact twist reference)
                d_s = Vector((1 if tb.endswith("_l") else -1, 0, 0))
            d_t = rig.rest[tb].col[1].normalized()
            self.corr[tb] = d_t.rotation_difference(d_s).to_matrix()

    def world_targets(self, wb):
        out = {}
        for tb, src in MAP.items():
            if isinstance(src, tuple):
                qs = [wb[s].to_quaternion() for s in src]
                q = qs[0].slerp(qs[1], 0.5)
                R = q.to_matrix()
            else:
                R = wb[src]
            out[tb] = R @ self.corr[tb] @ self.rig.rest[tb]
        return out


def leg_length(sk):
    """Hip-joint-to-ankle vertical length at rest (m)."""
    tot = 0.0
    for n in ("lfemur", "ltibia"):
        b = sk.bones[n]
        tot += abs((A @ b["direction"]).normalized().z) * b["length"]
    return tot
