# Author : Baptiste Vu Liem
"""Build every animation clip on the Chr_Agent skeleton and export one FBX per clip.

Usage (headless, after chr_build.py Chr_Agent):
  blender -b --python Tools/Blender/characters/anim_build.py -- [all | Clip1 Clip2 ...] [--preview] [--no-export]

Output: Assets/_Game/Art/Characters/Animations/<Clip>.fbx + clips.json; contact sheets in $GOOBYR_PREVIEW_DIR.
Sources: CMU mocap (mocap.cs.cmu.edu) retargeted by anim_cmu.py, plus hand-keyed clips (anim_keyed.py).
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import chr_common as C  # noqa: E402
import anim_cmu as M  # noqa: E402
import anim_rig as R  # noqa: E402

REF_CHARACTER = "Chr_Agent"


def log(*a):
    print("[anim]", *a, flush=True)


# ------------------------------------------------------------------------------------------ hands

def finger_pose(rig, frame, preset, side, weight=1.0):
    """Overwrite finger local rotations with a preset (degrees of flexion per phalanx)."""
    s = 1.0 if side == "l" else -1.0
    P = FINGER_PRESETS[preset]
    for f in ("index", "middle", "ring", "pinky"):
        for i, ang in enumerate(P[f]):
            b = "%s_%02d_%s" % (f, i + 1, side)
            q = rig.axis_quat(b, (0, s * ang, 0))
            frame["q"][b] = Quaternion().slerp(q, weight) if weight < 1 else q
    t = P["thumb"]
    for i, (ax, ay, az) in enumerate(t):
        b = "thumb_%02d_%s" % (i + 1, side)
        frame["q"][b] = rig.axis_quat(b, (ax, s * ay, s * az))


FINGER_PRESETS = {
    "relaxed": dict(index=(14, 18, 10), middle=(20, 24, 12), ring=(26, 28, 14), pinky=(32, 30, 16),
                    thumb=((0, 8, -10), (0, 8, 0), (0, 8, 0))),
    "fist": dict(index=(80, 95, 60), middle=(85, 95, 60), ring=(88, 95, 60), pinky=(90, 95, 60),
                 thumb=((10, 25, -25), (0, 30, 0), (0, 35, 0))),
    "grip": dict(index=(30, 35, 15), middle=(70, 80, 45), ring=(75, 82, 45), pinky=(78, 85, 45),
                 thumb=((10, 20, -30), (0, 20, 0), (0, 20, 0))),
    "support": dict(index=(25, 30, 15), middle=(35, 40, 20), ring=(40, 45, 22), pinky=(45, 45, 22),
                    thumb=((0, 15, -20), (0, 10, 0), (0, 10, 0))),
    "phone": dict(index=(35, 40, 20), middle=(40, 45, 25), ring=(48, 50, 25), pinky=(55, 50, 25),
                  thumb=((10, 20, -35), (0, 15, 0), (0, 10, 0))),
    "pinch": dict(index=(35, 45, 30), middle=(45, 60, 40), ring=(60, 70, 45), pinky=(70, 75, 45),
                  thumb=((10, 25, -35), (0, 25, 0), (0, 20, 0))),
    "open": dict(index=(4, 4, 2), middle=(4, 4, 2), ring=(5, 4, 2), pinky=(6, 4, 2),
                 thumb=((0, 0, -5), (0, 0, 0), (0, 0, 0))),
}


def hands(rig, frames, left="relaxed", right="relaxed"):
    for f in frames:
        finger_pose(rig, f, left, "l")
        finger_pose(rig, f, right, "r")


# ------------------------------------------------------------------------------------------ CMU

_SK_CACHE = {}


def cmu_frames(rig, subject, trial, t0=None, t1=None, src_fps=120, mirror=False):
    asf, amc = M.fetch(subject, trial)
    if asf not in _SK_CACHE:
        _SK_CACHE[asf] = M.Skeleton(asf)
    sk = _SK_CACHE[asf]
    raw = M.read_amc(amc)
    a = int((t0 or 0) * src_fps)
    b = int(t1 * src_fps) if t1 is not None else len(raw) - 1
    b = min(b, len(raw) - 1)
    step = max(1, int(round(src_fps / R.FPS)))
    rt = M.Retargeter(sk, rig)
    scale = (rig.length["thigh_l"] + rig.length["calf_l"]) / M.leg_length(sk)
    frames = []
    for i in range(a, b + 1, step):
        wb, jb = M.fk_frame(sk, raw[i])
        if mirror:
            wb, jb = M.mirror_frame(wb, jb)
        tw = rt.world_targets(wb)
        f = {"q": {}, "hips": jb["root"] * scale}
        W = {}
        for n in rig.order:
            p = rig.parent[n]
            if n in tw:
                f["q"][n] = rig.local_from_world(n, tw[n], W[p] if p else None)
                W[n] = tw[n]
            else:
                f["q"][n] = Quaternion()
                if p is None:
                    W[n] = rig.rest[n].copy()
                else:
                    W[n] = W[p] @ (rig.rest[p].inverted() @ rig.rest[n])
        frames.append(f)
    R.fix_signs(frames)
    return frames


def cmu_loop(rig, name, subject, trial, t0, t1, period_range, src_fps=120, mirror=False, travel=None,
             notes="", fingers=("relaxed", "relaxed"), in_place=True, smooth_passes=1, cycles=1):
    """Cyclic locomotion clip: find the gait period, cut one seamless cycle, make it in-place."""
    fr = cmu_frames(rig, subject, trial, t0, t1, src_fps, mirror)
    R.smooth(fr, smooth_passes)
    R.face_forward(rig, fr)
    period = R.find_period(rig, fr, *period_range) * cycles
    start = R.best_loop_start(rig, fr, period, range(0, max(1, len(fr) - period - 1)))
    cyc = [R.copy_frame(f) for f in fr[start:start + period + 1]]
    R.face_forward(rig, cyc)
    if travel is not None:
        align_travel(rig, cyc, travel)
    hands(rig, cyc, *fingers)
    R.make_loop(rig, cyc)
    R.ground(rig, cyc)
    lock_feet(rig, cyc, loop=True)
    speed = 0.0
    if in_place:
        speed, vec = R.in_place(cyc)
    clip = R.Clip(name, cyc, loop=True, in_place=in_place,
                  notes=(notes + (" Source speed %.2f m/s." % speed if in_place else "")).strip(),
                  source="CMU %02d_%02d%s" % (subject, trial, " (mirrored)" if mirror else ""))
    clip.speed = speed
    log(name, "period", period, "start", start, "speed %.2f" % speed)
    return clip


def cmu_oneshot(rig, name, subject, trial, t0, t1, src_fps=120, mirror=False, notes="", loop=False,
                fingers=("relaxed", "relaxed"), smooth_passes=1, in_place=False, ground=True, yaw_deg=0.0):
    fr = cmu_frames(rig, subject, trial, t0, t1, src_fps, mirror)
    R.smooth(fr, smooth_passes)
    # face -Y at the first frame, start at origin
    y0 = R.yaw_of(rig, fr[0])
    R.rotate_clip(rig, fr, -y0 + math.radians(yaw_deg))
    h0 = fr[0]["hips"].copy()
    for f in fr:
        f["hips"] -= Vector((h0.x, h0.y, 0))
    hands(rig, fr, *fingers)
    if ground:
        R.ground(rig, fr, percentile=0.02)
    if loop:
        R.make_loop(rig, fr)
    if in_place:
        R.in_place(fr)
    return R.Clip(name, fr, loop=loop, in_place=in_place, notes=notes,
                  source="CMU %02d_%02d%s" % (subject, trial, " (mirrored)" if mirror else ""))


def align_travel(rig, frames, travel):
    """Rotate the whole clip so the hip travel points along `travel` (e.g. (0,-1) forward) while the
    character keeps facing within a few degrees of -Y (strafe/back clips: the travel wins)."""
    a, b = frames[0]["hips"], frames[-1]["hips"]
    d = Vector((b.x - a.x, b.y - a.y))
    if d.length < 0.05:
        return
    cur = math.atan2(d.y, d.x)
    want = math.atan2(travel[1], travel[0])
    yaw = want - cur
    yaw = (yaw + math.pi) % (2 * math.pi) - math.pi
    R.rotate_clip(rig, frames, yaw)


# ------------------------------------------------------------------------------------------ feet

def lock_feet(rig, frames, loop=False, h_thresh=0.045, v_thresh=0.5):
    """Detect foot contacts (ball low and slow) and pin the foot during each contact with leg IK."""
    n = len(frames)
    rest = rig.rest_frame()
    Wr, Pr = rig.fk(rest)
    err = 0.0
    nseg = 0
    for side in ("l", "r"):
        foot, ball, thigh, calf = "foot_" + side, "ball_" + side, "thigh_" + side, "calf_" + side
        pos = []
        for f in frames:
            W, P = rig.fk(f)
            pos.append((P[foot].copy(), P[ball].copy()))
        contact = []
        for i in range(n):
            j = (i + 1) % n if loop else min(i + 1, n - 1)
            k = (i - 1) % n if loop else max(i - 1, 0)
            v = (pos[j][1] - pos[k][1]).length * R.FPS / max(1, (j - k) % n if loop else (j - k))
            low = pos[i][1].z - Pr[ball].z < h_thresh or pos[i][0].z - Pr[foot].z < h_thresh
            contact.append(low and v < v_thresh)
        # segments
        segs = []
        i = 0
        while i < n:
            if contact[i]:
                j = i
                while j + 1 < n and contact[j + 1]:
                    j += 1
                segs.append((i, j))
                i = j + 1
            else:
                i += 1
        for (s, e) in segs:
            if e - s < 2:
                continue
            nseg += 1
            # anchor = ankle position at the middle of the contact
            mid = (s + e) // 2
            anchor = pos[mid][0].copy()
            for i in range(s, e + 1):
                # ease in/out over 2 frames at segment ends
                w = min(1.0, (i - s + 1) / 3.0, (e - i + 1) / 3.0)
                f = frames[i]
                W, P = rig.fk(f)
                tgt = P[foot].lerp(Vector((anchor.x, anchor.y, P[foot].z)), w)
                foot_w = W[foot].copy()
                knee = P[calf]
                pole = knee + (knee - (P[thigh] + tgt) * 0.5).normalized() * 0.5
                R.two_bone_ik(rig, f, thigh, calf, foot, tgt, pole, W, P)
                # keep the foot's world orientation
                rig.set_world(f, W, foot, foot_w)
                W2, P2 = rig.fk(f)
                err = max(err, (P2[foot] - tgt).length)
    log("foot lock: %d contacts, max IK miss %.1f mm" % (nseg, err * 1000))


# ------------------------------------------------------------------------------------------ export

def export_clip(arm, clip):
    act = R.write_action(arm, clip)
    path = os.path.join(C.ANIM_DIR, clip.name + ".fbx")
    bpy.context.scene.frame_set(R.REST_FRAME)
    bpy.context.scene.name = clip.name   # FBX take name = clip name
    C.export_fbx(path, [arm], bake_anim=True)
    return path


def load_reference():
    path = os.path.join(C.WORK_DIR, REF_CHARACTER + ".blend")
    bpy.ops.wm.open_mainfile(filepath=path)
    arm = bpy.data.objects[C.ARMATURE_NAME]
    return arm


def main():
    args = C.argv_after_dashes()
    names = [a for a in args if not a.startswith("--")]
    preview_dir = os.environ.get("GOOBYR_PREVIEW_DIR") if "--preview" in args else None
    arm = load_reference()
    rig = R.RigModel(arm)
    import anim_clips
    builders = anim_clips.builders()
    if not names or names == ["all"]:
        names = list(builders.keys())
    meta_path = os.path.join(C.ANIM_DIR, "clips.json")
    meta = {}
    if os.path.isfile(meta_path):
        for m in json.load(open(meta_path)):
            meta[m["name"]] = m
    os.makedirs(C.ANIM_DIR, exist_ok=True)
    for n in names:
        log("=== clip", n)
        clip = builders[n](rig)
        clip.name = n
        if "--no-export" not in args:
            export_clip(arm, clip)
        else:
            R.write_action(arm, clip)
        meta[n] = dict(name=n, file=n + ".fbx", loop=clip.loop, inPlace=clip.in_place,
                       durationSec=round(clip.duration, 3), upperBodyOnly=clip.upper,
                       notes=(clip.notes + " Source: " + clip.source + ".").strip())
        if preview_dir:
            import anim_preview
            anim_preview.contact_sheet(arm, clip, preview_dir)
    order = list(anim_clips.builders().keys())
    out = [meta[k] for k in order if k in meta] + [v for k, v in meta.items() if k not in order]
    with open(meta_path, "w") as f:
        json.dump(out, f, indent=2)
    log("wrote", meta_path)


if __name__ == "__main__":
    main()
