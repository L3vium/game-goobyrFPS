# Author : Baptiste Vu Liem
"""Clip catalogue: name -> builder(rig) returning an anim_rig.Clip.

Coordinate conventions for every clip (Blender space, exported Y-up/-Z forward for Unity):
character faces -Y (Unity +Z), +X = character's left. Locomotion loops are in-place (hip bob and
sway kept); one-shots that travel (Vault, Mantle, Hang_ClimbUp, deaths, takedowns) keep their root
motion on the hips. Paired takedown clips share one origin (the attacker's start position).
"""
import math

from mathutils import Matrix, Quaternion, Vector

import anim_build as B
import anim_keyed as K
import anim_rig as R

_CACHE = {}


def cached(name, fn):
    if name not in _CACHE:
        _CACHE[name] = fn()
    return _CACHE[name]


def head_pitch(rig, f):
    W, P = rig.fk(f)
    fwd = W["head"] @ (rig.rest["head"].inverted() @ Vector((0, -1, 0)))
    return math.degrees(math.asin(max(-1, min(1, -fwd.z))))


def level_head(rig, frames, target=4.0, max_fix=30.0):
    """Shift the average head pitch to `target` degrees down (keeps the motion's variation)."""
    avg = sum(head_pitch(rig, f) for f in frames) / len(frames)
    c = max(-max_fix, min(max_fix, target - avg))
    for f in frames:
        K.rot(rig, f, "neck_01", (c * 0.5, 0, 0))
        K.rot(rig, f, "head", (c * 0.5, 0, 0))
    return c


def frames_copy(frames):
    return [R.copy_frame(f) for f in frames]


# ---------------------------------------------------------------------------------- locomotion

def loco(rig, name, subj, trial, t0, t1, prange, want, notes, fps=120, fingers=("relaxed", "relaxed"),
         cycles=1, smooth=1, head=4.0):
    """Cyclic locomotion; want = 'F','B','L','R' travel direction, auto-mirrors the source if needed."""
    for mirror in (False, True):
        clip = B.cmu_loop(rig, name, subj, trial, t0, t1, prange, src_fps=fps, mirror=mirror, notes=notes,
                          fingers=fingers, in_place=False, cycles=cycles, smooth_passes=smooth)
        a, b = clip.frames[0]["hips"], clip.frames[-1]["hips"]
        d = Vector((b.x - a.x, b.y - a.y))
        dirs = {"F": Vector((0, -1)), "B": Vector((0, 1)), "L": Vector((1, 0)), "R": Vector((-1, 0))}
        ok = d.length > 0.05 and d.normalized().dot(dirs[want]) > 0.5
        B.log(name, "travel", tuple(round(x, 2) for x in d), "mirror", mirror, "ok", ok)
        if ok or want in ("F",) or mirror:
            break
    # step exactly along the wanted axis (the capture may drift a few degrees off its facing)
    B.align_travel(rig, clip.frames, tuple(dirs[want]))
    if head is not None:
        level_head(rig, clip.frames, head)
    speed, vec = R.in_place(clip.frames)
    clip.in_place = True
    clip.speed = speed
    clip.notes = (notes + " Source speed %.2f m/s." % speed).strip()
    return clip


def idle_clip(rig):
    def build():
        c = B.cmu_oneshot(rig, "Idle", 113, 21, 2.0, 6.0, loop=False, notes="Relaxed standing idle.")
        fr = c.frames
        R.make_loop(rig, fr)
        R.in_place(fr)
        B.lock_feet(rig, fr, loop=True, v_thresh=0.8)
        level_head(rig, fr, 3.0)
        c.loop = True
        return c
    return cached("Idle", build)


def base_stand(rig):
    """Neutral standing frame (first frame of Idle)."""
    return R.copy_frame(idle_clip(rig).frames[0])


def clone(clip, name, **kw):
    c = R.Clip(name, frames_copy(clip.frames), loop=clip.loop, in_place=clip.in_place, upper=clip.upper,
               notes=clip.notes, source=clip.source)
    for k, v in kw.items():
        setattr(c, k, v)
    return c


# ---------------------------------------------------------------------------------- weapons

PISTOL_AIM = dict(
    r=((-0.03, -0.57, 0.25), (0.04, -1.0, -0.05), (0.25, 0.0, 1.0)),
    l=((0.035, -0.52, 0.22), (-0.55, -0.83, -0.1), (0.1, -0.2, 1.0)),
)
PISTOL_LOW = dict(
    r=((-0.03, -0.32, -0.16), (0.03, -0.6, -0.8), (0.25, -0.75, 0.6)),
    l=((0.035, -0.29, -0.18), (-0.55, -0.55, -0.6), (0.1, -0.8, 0.6)),
)
RIFLE_AIM = dict(
    r=((-0.13, -0.26, 0.20), (0.30, -0.75, -0.6), (0.35, -0.45, 0.8)),
    l=((0.00, -0.58, 0.21), (-0.75, -0.45, 0.35), (-0.1, -1.0, 0.05)),
)
RIFLE_LOW = dict(
    r=((-0.15, -0.20, -0.06), (0.35, -0.55, -0.75), (0.4, -0.6, 0.6)),
    l=((0.08, -0.40, -0.14), (-0.8, -0.35, 0.3), (-0.35, -0.9, -0.1)),
)


def hold(rig, f, grip, spine=True, kind="pistol"):
    if spine:
        K.rot(rig, f, "spine_02", (4, 0, 0))
        K.rot(rig, f, "spine_03", (3, 0, 0))
    for side in ("r", "l"):
        pos, fd, td = grip[side]
        K.place_hand(rig, f, side, pos, fd, td, space="chest_yaw")
    B.finger_pose(rig, f, "grip", "r")
    B.finger_pose(rig, f, "support", "l")


def aim_pose(rig, kind):
    f = base_stand(rig)
    # widen the stance a little, weight forward
    if kind == "pistol":
        K.rot(rig, f, "spine_01", (3, 0, 0))
        K.rot(rig, f, "neck_01", (2, 0, 0))
        K.rot(rig, f, "head", (-2, 0, 0))
        hold(rig, f, PISTOL_AIM)
    else:
        K.rot(rig, f, "spine_01", (2, 0, -8))
        K.rot(rig, f, "spine_02", (2, 0, -6))
        K.rot(rig, f, "neck_01", (4, 0, 10))
        K.rot(rig, f, "head", (-6, -10, 8))
        hold(rig, f, RIFLE_AIM)
    return f


def upper_loop(rig, name, pose_fn, dur=2.0, notes="", breath=0.6):
    base = idle_clip(rig)
    n = int(round(dur * R.FPS))
    src = base.frames
    frames = []
    for i in range(n + 1):
        f = R.copy_frame(src[int(i * (len(src) - 1) / n)])
        frames.append(f)
    K.breathe(rig, frames, amp=breath)
    for i, f in enumerate(frames):
        pose_fn(f, i / n)
    R.fix_signs(frames)
    return R.Clip(name, frames, loop=True, upper=True, notes=notes, source="hand-keyed (IK) over CMU 113_21 idle")


def aim_clip(rig, kind):
    def pose_fn(f, u):
        if kind == "pistol":
            K.rot(rig, f, "spine_01", (3, 0, 0))
            K.rot(rig, f, "neck_01", (2, 0, 0))
            K.rot(rig, f, "head", (-2, 0, 0))
            hold(rig, f, PISTOL_AIM)
        else:
            K.rot(rig, f, "spine_01", (2, 0, -8))
            K.rot(rig, f, "spine_02", (2, 0, -6))
            K.rot(rig, f, "neck_01", (4, 0, 10))
            K.rot(rig, f, "head", (-6, -10, 8))
            hold(rig, f, RIFLE_AIM)
    name = "Aim_Pistol" if kind == "pistol" else "Aim_Rifle"
    return upper_loop(rig, name, pose_fn, 2.0,
                      notes="Upper-body aim pose (two-handed %s), breathing loop; mask the legs." % kind)


def fire_clip(rig, kind):
    aim = aim_clip(rig, kind)
    dur = 0.3 if kind == "pistol" else 0.2
    n = int(round(dur * R.FPS))
    frames = []
    for i in range(n + 1):
        f = R.copy_frame(aim.frames[i])
        t = i / R.FPS
        # recoil impulse: fast kick then damped return
        k = math.exp(-t * (18 if kind == "pistol" else 26)) * math.sin(min(t * 60, math.pi / 2))
        W, P = rig.fk(f)
        grip = PISTOL_AIM if kind == "pistol" else RIFLE_AIM
        for side in ("r", "l"):
            pos, fd, td = grip[side]
            if kind == "pistol":
                pos = Vector(pos) + Vector((0, 0.035 * k, 0.03 * k))
                fd = (Matrix.Rotation(math.radians(-14 * k), 3, "X") @ Vector(fd))
            else:
                pos = Vector(pos) + Vector((0, 0.03 * k, 0.012 * k))
                fd = (Matrix.Rotation(math.radians(-5 * k), 3, "X") @ Vector(fd))
            K.place_hand(rig, f, side, pos, fd, td, space="chest_yaw")
        K.rot(rig, f, "spine_03", (2.5 * k, 0, 0))
        K.rot(rig, f, "head", (2.0 * k, 0, 0))
        B.finger_pose(rig, f, "grip", "r")
        B.finger_pose(rig, f, "support", "l")
        frames.append(f)
    return R.Clip("Fire_" + kind.capitalize(), frames, loop=False, upper=True,
                  notes="Single shot recoil from the aim pose (upper body).", source="hand-keyed (IK)")


def reload_clip(rig):
    aim = aim_clip(rig, "pistol")
    low = dict(r=((-0.06, -0.36, 0.02), (0.1, -0.85, 0.5), (0.6, 0.2, 0.75)),
               l=PISTOL_AIM["l"])
    keys_l = [
        (0.0, PISTOL_AIM["l"], "support"),
        (0.35, ((0.05, -0.30, 0.0), (-0.4, -0.6, -0.6), (0.2, -0.8, 0.5)), "relaxed"),
        (0.7, ((0.17, -0.06, -0.33), (0.1, -0.3, -0.95), (0.2, -0.9, 0.1)), "pinch"),   # mag pouch
        (0.95, ((0.17, -0.06, -0.33), (0.1, -0.3, -0.95), (0.2, -0.9, 0.1)), "pinch"),
        (1.35, ((-0.01, -0.36, -0.07), (-0.2, -0.3, 0.93), (0.1, -0.95, 0.1)), "pinch"),  # insert
        (1.55, ((-0.02, -0.36, -0.04), (-0.2, -0.3, 0.93), (0.1, -0.95, 0.1)), "fist"),   # slap
        (1.9, PISTOL_AIM["l"], "support"),
    ]
    keys_r = [(0.0, PISTOL_AIM["r"]), (0.35, low["r"]), (1.6, low["r"]), (1.95, PISTOL_AIM["r"])]
    dur = 2.0
    n = int(round(dur * R.FPS))

    def lerp_key(keys, t):
        k = 0
        while k + 1 < len(keys) and keys[k + 1][0] <= t:
            k += 1
        if k + 1 >= len(keys):
            return keys[-1], keys[-1], 0.0
        u = (t - keys[k][0]) / (keys[k + 1][0] - keys[k][0])
        return keys[k], keys[k + 1], K.ease(u)

    def mix(a, b, u):
        return tuple(Vector(x).lerp(Vector(y), u) for x, y in zip(a, b))

    frames = []
    for i in range(n + 1):
        t = i / R.FPS
        f = R.copy_frame(aim.frames[i % (len(aim.frames) - 1)])
        K.rot(rig, f, "spine_01", (3, 0, 0))
        K.rot(rig, f, "neck_01", (12, 0, 0))
        K.rot(rig, f, "head", (4, 0, 0))
        a, b, u = lerp_key(keys_r, t)
        p, fd, td = mix(a[1], b[1], u)
        K.place_hand(rig, f, "r", p, fd, td, space="chest_yaw")
        a, b, u = lerp_key(keys_l, t)
        p, fd, td = mix(a[1], b[1], u)
        K.place_hand(rig, f, "l", p, fd, td, space="chest_yaw")
        B.finger_pose(rig, f, "grip", "r")
        B.finger_pose(rig, f, a[2] if u < 0.5 else b[2], "l")
        frames.append(f)
    return R.Clip("Reload", frames, upper=True, notes="Pistol magazine reload from the aim pose (2 s, upper body).",
                  source="hand-keyed (IK)")


def idle_armed(rig):
    def pose_fn(f, u):
        K.rot(rig, f, "spine_02", (2, 0, 0))
        hold(rig, f, PISTOL_LOW)
    c = upper_loop(rig, "Idle_Armed", pose_fn, 4.0, notes="Pistol low ready (two hands, muzzle down).")
    c.upper = False
    return c


def patrol_walk(rig):
    c = loco(rig, "Patrol_Walk", 91, 10, 2.0, 12.0, (28, 48), "F", "Slow alert walk, rifle at low ready.")
    for f in c.frames:
        K.rot(rig, f, "spine_02", (3, 0, -4))
        K.rot(rig, f, "head", (0, 0, 4))
        hold(rig, f, RIFLE_LOW)
    R.fix_signs(c.frames)
    return c


# ---------------------------------------------------------------------------------- crouch / crawl

def crouch_pose(rig, hips_drop=0.36, lean=28, step=0.0):
    f = base_stand(rig)
    fl, fr = K.rest_feet(rig)
    f["hips"] = f["hips"] + Vector((0, 0.06, -hips_drop))
    K.rot(rig, f, "pelvis", (lean * 0.5, 0, 0))
    K.rot(rig, f, "spine_01", (lean * 0.25, 0, 0))
    K.rot(rig, f, "spine_02", (lean * 0.25, 0, 0))
    K.rot(rig, f, "spine_03", (lean * 0.1, 0, 0))
    K.rot(rig, f, "neck_01", (-lean * 0.55, 0, 0))
    K.rot(rig, f, "head", (-lean * 0.3, 0, 0))
    K.plant_foot(rig, f, "l", fl + Vector((0.05, -0.08 - step, 0)), yaw_deg=8)
    K.plant_foot(rig, f, "r", fr + Vector((-0.05, 0.10, 0)), yaw_deg=-10)
    # arms hanging forward near the knees
    K.place_hand(rig, f, "l", (0.22, -0.28, -0.42), (0.0, -0.4, -0.9), (0.0, -0.9, 0.3), space="chest")
    K.place_hand(rig, f, "r", (-0.22, -0.26, -0.44), (0.0, -0.4, -0.9), (0.0, -0.9, 0.3), space="chest")
    B.finger_pose(rig, f, "relaxed", "l")
    B.finger_pose(rig, f, "relaxed", "r")
    return f


def crouch_idle(rig):
    f = crouch_pose(rig)
    n = 90
    frames = [R.copy_frame(f) for _ in range(n + 1)]
    for i, g in enumerate(frames):
        s = math.sin(2 * math.pi * i / n)
        g["hips"] = g["hips"] + Vector((0.012 * math.sin(2 * math.pi * i / n), 0, 0.006 * s))
    # re-plant feet after the sway
    fl, fr = K.rest_feet(rig)
    for g in frames:
        K.plant_foot(rig, g, "l", fl + Vector((0.05, -0.08, 0)), yaw_deg=8)
        K.plant_foot(rig, g, "r", fr + Vector((-0.05, 0.10, 0)), yaw_deg=-10)
    K.breathe(rig, frames, amp=0.8)
    return R.Clip("CrouchIdle", frames, loop=True, in_place=True, notes="Crouched idle (hips ~0.36 m lower).",
                  source="hand-keyed (IK)")


def crawl(rig):
    c = loco(rig, "Crawl_F", 133, 1, 3.4, 7.4, (24, 70), "F", "Crawl on hands and knees.",
             fingers=("open", "open"), head=None)
    K.ground_body(rig, c.frames)
    return c


# ---------------------------------------------------------------------------------- jumps

def jump_source(rig):
    def build():
        fr = B.cmu_frames(rig, 16, 1, 0, None, 120)
        R.smooth(fr, 1)
        y0 = R.yaw_of(rig, fr[0])
        R.rotate_clip(rig, fr, -y0)
        h0 = fr[0]["hips"].copy()
        for f in fr:
            f["hips"] -= Vector((h0.x, h0.y, 0))
        R.ground(rig, fr, 0.02)
        B.hands(rig, fr)
        # airborne frames: both balls above the floor
        W, P = rig.fk(rig.rest_frame())
        bz = P["ball_l"].z
        air = []
        for i, f in enumerate(fr):
            W, P = rig.fk(f)
            air.append(min(P["ball_l"].z, P["ball_r"].z, P["foot_l"].z - 0.06, P["foot_r"].z - 0.06) > bz + 0.03)
        take = air.index(True)
        land = take + air[take:].index(False)
        B.log("jump takeoff", take, "land", land, "of", len(fr))
        return fr, take, land
    return cached("jump", build)


def jump_start(rig):
    fr, take, land = jump_source(rig)
    seg = frames_copy(fr[max(0, take - 14):take + 5])
    return R.Clip("Jump_Start", seg, notes="Crouch and take-off; hips rise at the end (physics drives the jump).",
                  source="CMU 16_01")


def land(rig):
    fr, take, land_i = jump_source(rig)
    seg = frames_copy(fr[land_i - 3:min(len(fr), land_i + 22)])
    return R.Clip("Land", seg, notes="Landing absorb back to stand.", source="CMU 16_01")


def fall_loop(rig):
    fr, take, land_i = jump_source(rig)
    apex = R.copy_frame(fr[(take + land_i) // 2])
    n = 30
    frames = []
    for i in range(n + 1):
        f = R.copy_frame(apex)
        s = math.sin(2 * math.pi * i / n)
        c = math.cos(2 * math.pi * i / n)
        K.rot(rig, f, "thigh_l", (-28 + 10 * s, 0, 0))
        K.rot(rig, f, "thigh_r", (-18 - 10 * s, 0, 0))
        K.rot(rig, f, "calf_l", (45 + 10 * c, 0, 0))
        K.rot(rig, f, "calf_r", (35 - 10 * c, 0, 0))
        K.rot(rig, f, "foot_l", (-15, 0, 0))
        K.rot(rig, f, "foot_r", (-15, 0, 0))
        K.rot(rig, f, "upperarm_l", (0, 10 * c - 15, 6 * s))
        K.rot(rig, f, "upperarm_r", (0, -10 * c + 15, -6 * s))
        K.rot(rig, f, "spine_03", (2 * s, 0, 0))
        f["hips"] = Vector((0, 0, rig.head["pelvis"].z))
        frames.append(f)
    return R.Clip("Fall_Loop", frames, loop=True, in_place=True, notes="Airborne loop, limbs balancing.",
                  source="hand-keyed over the CMU 16_01 apex pose")


# ---------------------------------------------------------------------------------- one-shots from mocap

def death(rig, name, subj, trial, t0, t1, dur, notes):
    fr = B.cmu_frames(rig, subj, trial, t0, t1, 120)
    R.smooth(fr, 2)
    fr = fr[::-1]
    y0 = R.yaw_of(rig, fr[0])
    R.rotate_clip(rig, fr, -y0)
    h0 = fr[0]["hips"].copy()
    for f in fr:
        f["hips"] -= Vector((h0.x, h0.y, 0))
    B.hands(rig, fr)
    # collapse accelerates (gravity) then settles
    warped = K.timewarp(fr, lambda u: (u / 0.85) ** 1.7 if u < 0.85 else 1.0, dur)
    # ground: stand on the floor at the start, rest on the floor at the end
    a = K.lowest_body(rig, warped[0])
    W, P = rig.fk(warped[0])
    start_off = min(P["foot_l"].z, P["foot_r"].z) - rig.head["foot_l"].z
    end_off = K.lowest_body(rig, warped[-1])
    n = len(warped) - 1
    for i, f in enumerate(warped):
        u = i / n
        f["hips"].z -= start_off * (1 - u) + end_off * u
    # start from the standing idle pose (the get-up capture ends hunched over)
    stand = base_stand(rig)
    stand["hips"] = Vector((0, 0, stand["hips"].z))
    blend_n = 9
    for i in range(min(blend_n, len(warped))):
        w = K.ease(i / blend_n)
        warped[i] = R.blend_frames(stand, warped[i], w)
    return R.Clip(name, warped, notes=notes, source="CMU %02d_%02d (reversed get-up, time-warped)" % (subj, trial))


def phone(rig):
    c = B.cmu_oneshot(rig, "Talk_Phone", 79, 36, 4.3, 7.0, src_fps=60, fingers=("relaxed", "phone"),
                      notes="Standing on the phone (right hand at the ear), loop.")
    R.make_loop(rig, c.frames)
    R.in_place(c.frames)
    B.lock_feet(rig, c.frames, loop=True, v_thresh=0.8)
    level_head(rig, c.frames, 6.0)
    for f in c.frames:
        K.place_hand(rig, f, "r", (-0.085, 0.01, -0.06), (0.15, 0.25, 0.95), (0.1, -1.0, 0.1), space="head",
                     pole=(-0.35, 0.1, -0.45))
        B.finger_pose(rig, f, "phone", "r")
    c.loop = True
    return c


def smoke(rig):
    c = B.cmu_oneshot(rig, "Smoke_Idle", 80, 28, 6.0, 13.0, src_fps=60, fingers=("relaxed", "pinch"),
                      notes="Standing smoking idle, loop (cigarette in the right hand).")
    R.make_loop(rig, c.frames)
    R.in_place(c.frames)
    B.lock_feet(rig, c.frames, loop=True, v_thresh=0.8)
    c.loop = True
    return c


def cower(rig):
    """Crouched with the arms over the head (pose from the capture) + fear trembling/breathing loop."""
    c = B.cmu_oneshot(rig, "Cower", 142, 16, 34.0, 34.4, notes="Civilian panic: crouched, arms covering the head, loop.")
    pose = R.copy_frame(c.frames[0])
    pose["hips"] = Vector((0, 0, pose["hips"].z))
    W, P = rig.fk(pose)
    fl, fr_ = P["foot_l"].copy(), P["foot_r"].copy()
    n = 60
    frames = []
    for i in range(n + 1):
        f = R.copy_frame(pose)
        u = i / n
        tr = math.sin(2 * math.pi * u * 10) * 0.6 + math.sin(2 * math.pi * u * 7) * 0.4   # trembling
        br = math.sin(2 * math.pi * u * 2)                                                 # fast breathing
        K.rot(rig, f, "spine_02", (1.2 * br, 0, 0.6 * tr))
        K.rot(rig, f, "spine_03", (1.0 * br, 0.5 * tr, 0))
        K.rot(rig, f, "neck_01", (0.8 * tr, 0, 0))
        f["hips"] = f["hips"] + Vector((0.003 * tr, 0, 0.006 * br))
        frames.append(f)
    c.frames = frames
    c.loop = True
    c.in_place = True
    c.source = "CMU 142_16 (cowering pose at 34 s) + hand-keyed trembling"
    return c


def sit_chair(rig):
    fr, = [B.cmu_frames(rig, 143, 18, 1.7, 2.4, 120)]
    R.smooth(fr, 2)
    y0 = R.yaw_of(rig, fr[0])
    R.rotate_clip(rig, fr, -y0)
    seated = R.copy_frame(fr[len(fr) // 2])
    seated["hips"] = Vector((0, 0, seated["hips"].z))
    n = 120
    frames = [R.copy_frame(seated) for _ in range(n + 1)]
    W, P = rig.fk(seated)
    base_z = min(P["foot_l"].z, P["foot_r"].z) - rig.head["foot_l"].z
    for f in frames:
        f["hips"].z -= base_z
    B.hands(rig, frames)
    K.breathe(rig, frames, amp=1.0)
    hz = frames[0]["hips"].z
    return R.Clip("Sit_Chair", frames, loop=True, in_place=True,
                  notes="Seated idle loop; hips at %.2f m (seat ~0.45 m)." % hz, source="CMU 143_18 (seated pose) + breathing")


def look_around(rig):
    base = idle_clip(rig)
    n = 120
    frames = []
    for i in range(n + 1):
        f = R.copy_frame(base.frames[int(i * (len(base.frames) - 1) / n)])
        u = i / n
        yaw = 55 * math.sin(2 * math.pi * u) * (0.6 + 0.4 * abs(math.sin(2 * math.pi * u)))
        pitch = -6 * math.sin(4 * math.pi * u)
        K.rot(rig, f, "spine_02", (0, 0, yaw * 0.15))
        K.rot(rig, f, "spine_03", (pitch * 0.3, 0, yaw * 0.2))
        K.rot(rig, f, "neck_01", (pitch * 0.4, 0, yaw * 0.3))
        K.rot(rig, f, "head", (pitch * 0.5, 0, yaw * 0.35))
        frames.append(f)
    R.fix_signs(frames)
    return R.Clip("Look_Around", frames, loop=True, notes="Searching: scanning left and right (4 s loop).",
                  source="CMU 113_21 idle + hand-keyed head/torso scan")


def run_panic(rig):
    c = loco(rig, "Run_Panic", 16, 35, 0.0, None, (16, 28), "F", "Panicked run, arms protecting the head.")
    n = len(c.frames) - 1
    for i, f in enumerate(c.frames):
        u = i / n
        s = math.sin(4 * math.pi * u)
        K.rot(rig, f, "spine_02", (6, 0, 0))
        K.rot(rig, f, "neck_01", (4, 0, 0))
        K.place_hand(rig, f, "l", (0.10, -0.16 + 0.03 * s, 0.50), (-0.3, 0.2, 0.9), (0.0, -1.0, 0.0), space="chest")
        K.place_hand(rig, f, "r", (-0.10, -0.16 - 0.03 * s, 0.52), (0.3, 0.2, 0.9), (0.0, -1.0, 0.0), space="chest")
        B.finger_pose(rig, f, "open", "l")
        B.finger_pose(rig, f, "open", "r")
    return c


# ---------------------------------------------------------------------------------- hand-keyed full body

def hang_pose(rig, hand_offset=(0.0, 0.0), sway=0.0, knee=0.0):
    """Hanging from a ledge: hands at ledge height 2.10 m, 0.16 m in front of the hips."""
    f = rig.rest_frame()
    f["hips"] = Vector((0.0 + sway * 0.02, 0.10, rig.head["pelvis"].z))
    K.rot(rig, f, "spine_02", (4, 0, 0))
    K.rot(rig, f, "neck_01", (-14, 0, 0))
    K.rot(rig, f, "head", (-8, 0, 0))
    ledge = Vector((0, -0.18, 2.10))
    for side, sx in (("l", 1.0), ("r", -1.0)):
        off = hand_offset[0] if side == "l" else hand_offset[1]
        tgt = ledge + Vector((sx * 0.24 + off, 0, 0))
        K.place_hand(rig, f, side, tgt, (0.0, -0.35, 0.94), (-sx * 1.0, 0, 0), space="world",
                     pole=(sx * 0.6, 0.6, 1.4))
        B.finger_pose(rig, f, "grip", side)
    K.rot(rig, f, "thigh_l", (6 + knee, 0, 0))
    K.rot(rig, f, "thigh_r", (2 - knee * 0.5, 0, 0))
    K.rot(rig, f, "calf_l", (-14 - knee, 0, 0))
    K.rot(rig, f, "calf_r", (-8, 0, 0))
    K.rot(rig, f, "foot_l", (-25, 0, 0))
    K.rot(rig, f, "foot_r", (-25, 0, 0))
    return f


def hang_idle(rig):
    n = 90
    frames = []
    for i in range(n + 1):
        s = math.sin(2 * math.pi * i / n)
        f = hang_pose(rig, sway=s, knee=3 * s)
        frames.append(f)
    return R.Clip("Hang_Idle", frames, loop=True, in_place=True,
                  notes="Hanging from a ledge: wall face 0.12 m in front of the root, top at 2.10 m (hands on top of it).",
                  source="hand-keyed (IK)")


def shimmy(rig, side):
    sgn = 1.0 if side == "L" else -1.0  # L = towards +X (character left)
    step = 0.22
    dur = 1.0
    keys = [
        (0.0, hang_pose(rig, (0, 0))),
        (0.3, hang_pose(rig, (sgn * step if sgn > 0 else 0, sgn * step if sgn < 0 else 0), sway=sgn * 0.5, knee=6)),
        (0.5, hang_pose(rig, (sgn * step if sgn > 0 else 0, sgn * step if sgn < 0 else 0), sway=sgn * 1.0, knee=2)),
        (0.8, hang_pose(rig, (sgn * step, sgn * step), sway=sgn * 1.5, knee=4)),
        (1.0, hang_pose(rig, (sgn * step, sgn * step), sway=sgn * 2.0)),
    ]
    # convert to in-place: the hips drift sideways by `step`; express travel as hand offsets relative
    frames = K.interpolate(keys, dur)
    n = len(frames) - 1
    for i, f in enumerate(frames):
        # move the whole body back so the clip loops (hands end where they started relative to the hips)
        u = K.ease(i / n)
        f["hips"].x += sgn * step * u - sgn * step * (i / n)
    # rebuild: re-place hands each frame in world (ledge-relative) for a clean slide
    out = []
    for i, f in enumerate(frames):
        out.append(f)
    R.make_loop(rig, out)
    return R.Clip("Shimmy_" + side, out, loop=True, in_place=True,
                  notes="Ledge shimmy towards the character's %s, %.2f m per 1 s cycle." % ("left" if sgn > 0 else "right", step),
                  source="hand-keyed (IK)")


def hang_climb_up(rig):
    s = base_stand(rig)
    fl, fr = K.rest_feet(rig)
    ledge_z = 2.10
    k0 = hang_pose(rig)

    def pose(hips, spine, hands_world, feet=None, knees=None, head=0.0):
        f = R.copy_frame(s)
        f["hips"] = Vector(hips)
        K.rot(rig, f, "spine_01", (spine * 0.4, 0, 0))
        K.rot(rig, f, "spine_02", (spine * 0.3, 0, 0))
        K.rot(rig, f, "spine_03", (spine * 0.3, 0, 0))
        K.rot(rig, f, "neck_01", (head, 0, 0))
        for side, sx in (("l", 1.0), ("r", -1.0)):
            p, fd, td = hands_world[side]
            K.place_hand(rig, f, side, Vector(p) + Vector((sx * 0.24, 0, 0)), fd, (-sx, 0, 0) if td is None else td,
                         space="world", pole=(sx * 0.5, 0.8, 1.0))
            B.finger_pose(rig, f, "grip", side)
        if feet:
            for side in ("l", "r"):
                K.plant_foot(rig, f, side, feet[side], pole_fwd=0.6)
        return f

    edge = (0, -0.18, ledge_z)
    top = (0, -0.34, ledge_z)
    grip = {"l": (edge, (0, -0.35, 0.94), None), "r": (edge, (0, -0.35, 0.94), None)}
    push = {"l": (top, (0.0, -0.9, -0.2), (0, 0, 1)), "r": (top, (0.0, -0.9, -0.2), (0, 0, 1))}
    k1 = pose((0, 0.12, 1.40), 4, grip, head=-10)                         # pull up
    k2 = pose((0, 0.05, 1.85), 25, push, head=-10)                        # chest over the edge
    k3 = pose((0, -0.10, 2.30), 45, push, feet={"l": Vector((0.13, -0.25, ledge_z + 0.07)),
                                                  "r": Vector((-0.12, 0.25, ledge_z - 0.35))}, head=-20)  # knee up
    k4 = R.copy_frame(s)
    k4["hips"] = Vector((0, -0.45, ledge_z + rig.head["pelvis"].z - 0.18))
    K.rot(rig, k4, "spine_02", (15, 0, 0))
    K.plant_foot(rig, k4, "l", Vector((0.15, -0.45, ledge_z + 0.07)))
    K.plant_foot(rig, k4, "r", Vector((-0.15, -0.40, ledge_z + 0.07)))
    k5 = R.copy_frame(s)
    k5["hips"] = Vector((0, -0.50, ledge_z + rig.head["pelvis"].z))
    keys = [(0.0, k0), (0.45, k1), (0.95, k2), (1.4, k3), (1.8, k4), (2.2, k5)]
    frames = K.interpolate(keys, 2.2)
    B.hands(rig, frames[-8:])
    return R.Clip("Hang_ClimbUp", frames, notes="From Hang_Idle to standing on the ledge (+2.10 m up, 0.5 m forward root motion).",
                  source="hand-keyed (IK)")


def mantle(rig):
    s = base_stand(rig)
    ledge_z = 1.10
    fl, fr = K.rest_feet(rig)

    def k(hips, spine, hand_p, feet, head=0.0, hands_on=True):
        f = R.copy_frame(s)
        f["hips"] = Vector(hips)
        K.rot(rig, f, "spine_01", (spine * 0.4, 0, 0))
        K.rot(rig, f, "spine_02", (spine * 0.35, 0, 0))
        K.rot(rig, f, "spine_03", (spine * 0.25, 0, 0))
        K.rot(rig, f, "neck_01", (head, 0, 0))
        if hands_on:
            for side, sx in (("l", 1.0), ("r", -1.0)):
                K.place_hand(rig, f, side, Vector(hand_p) + Vector((sx * 0.22, 0, 0)), (0, -0.8, -0.6), (-sx * 0.2, 0, 1),
                             space="world", pole=(sx * 0.7, 0.5, 1.0))
                B.finger_pose(rig, f, "open", side)
        for side in ("l", "r"):
            if side in feet:
                K.plant_foot(rig, f, side, feet[side])
        return f

    hp = (0, -0.47, ledge_z + 0.02)
    k0 = R.copy_frame(s)
    k1 = k((0, 0.0, 0.92), 25, hp, {"l": fl, "r": fr}, head=-15)                          # hands on the edge, crouch
    k2 = k((0, -0.18, 1.55), 45, hp, {"r": fr + Vector((0, 0.05, 0.25))}, head=-25)       # push / jump up
    k3 = k((0, -0.38, 1.70), 40, hp, {"l": Vector((0.14, -0.50, ledge_z + 0.07)),
                                        "r": Vector((-0.12, -0.20, ledge_z - 0.15))}, head=-20)  # knee on top
    k4 = R.copy_frame(s)
    k4["hips"] = Vector((0, -0.62, ledge_z + rig.head["pelvis"].z - 0.15))
    K.rot(rig, k4, "spine_02", (12, 0, 0))
    K.plant_foot(rig, k4, "l", Vector((0.13, -0.62, ledge_z + 0.07)))
    K.plant_foot(rig, k4, "r", Vector((-0.14, -0.55, ledge_z + 0.07)))
    k5 = R.copy_frame(s)
    k5["hips"] = Vector((0, -0.65, ledge_z + rig.head["pelvis"].z))
    keys = [(0.0, k0), (0.3, k1), (0.65, k2), (0.95, k3), (1.25, k4), (1.6, k5)]
    frames = K.interpolate(keys, 1.6)
    return R.Clip("Mantle", frames, notes="Climb onto a 1.10 m ledge from standing (root motion +1.10 m up, 0.65 m forward).",
                  source="hand-keyed (IK)")


def slide(rig):
    run = run_clip(rig)
    k0 = R.copy_frame(run.frames[0])
    s = base_stand(rig)

    def slide_pose(t):
        f = R.copy_frame(s)
        f["hips"] = Vector((0, 0.10, 0.30 + 0.01 * t))
        K.rot(rig, f, "pelvis", (-25, 0, 0))
        K.rot(rig, f, "spine_01", (-8, 0, 0))
        K.rot(rig, f, "spine_02", (-10, 0, 0))
        K.rot(rig, f, "spine_03", (-6, 0, 0))
        K.rot(rig, f, "neck_01", (22, 0, 0))
        K.rot(rig, f, "head", (10, 0, 0))
        K.plant_foot(rig, f, "l", Vector((0.12, -0.70, 0.12)), pitch_deg=30)       # leading leg extended
        K.plant_foot(rig, f, "r", Vector((-0.10, -0.05, 0.08)), yaw_deg=-60, pitch_deg=-10)  # tucked leg
        K.place_hand(rig, f, "r", Vector((-0.32, 0.20, 0.05)), (-0.3, 0.2, -0.93), (0, -1, 0), space="world")
        K.place_hand(rig, f, "l", (0.30, -0.25, -0.05), (0.3, -0.6, -0.7), (0, -0.7, 0.7), space="chest")
        B.finger_pose(rig, f, "open", "r")
        B.finger_pose(rig, f, "relaxed", "l")
        return f

    keys = [(0.0, k0), (0.22, slide_pose(0)), (0.85, slide_pose(1)), (1.0, slide_pose(1))]
    frames = K.interpolate(keys, 1.0)
    return R.Clip("Slide", frames, in_place=True,
                  notes="Run-to-slide (hips drop to 0.30 m); in place, hold the last frame while sliding.",
                  source="hand-keyed (IK), starts from the Run_F pose")


def swim_idle(rig):
    n = 60
    frames = []
    s0 = rig.rest_frame()
    for i in range(n + 1):
        u = i / n
        a = math.sin(2 * math.pi * u)
        c = math.cos(2 * math.pi * u)
        f = R.copy_frame(s0)
        f["hips"] = Vector((0, 0, rig.head["pelvis"].z + 0.03 * a))
        K.rot(rig, f, "spine_02", (-4, 0, 0))
        K.rot(rig, f, "neck_01", (-6, 0, 0))
        # sculling hands at chest height
        for side, sx in (("l", 1.0), ("r", -1.0)):
            p = (sx * (0.40 + 0.08 * a), -0.30, -0.12)
            K.place_hand(rig, f, side, p, (sx * 0.6, -0.6, -0.2), (0, -0.4 + 0.3 * sx * c, 1.0), space="chest")
            B.finger_pose(rig, f, "open", side)
        # bicycle kick
        K.rot(rig, f, "thigh_l", (-25 - 20 * a, 0, 0))
        K.rot(rig, f, "calf_l", (35 + 25 * c, 0, 0))
        K.rot(rig, f, "thigh_r", (-25 + 20 * a, 0, 0))
        K.rot(rig, f, "calf_r", (35 - 25 * c, 0, 0))
        K.rot(rig, f, "foot_l", (-20, 0, 0))
        K.rot(rig, f, "foot_r", (-20, 0, 0))
        frames.append(f)
    return R.Clip("Swim_Idle", frames, loop=True, in_place=True,
                  notes="Treading water, upright; water line at the shoulders (~1.45 m above the root).",
                  source="hand-keyed (IK)")


def swim_f(rig):
    fr = B.cmu_frames(rig, 125, 1, 8.0, 30.0, 120)
    R.smooth(fr, 1)
    # orient: head direction -> -Y
    W, P = rig.fk(fr[0])
    d = P["head"] - P["pelvis"]
    yaw = math.atan2(d.x, -d.y)
    R.rotate_clip(rig, fr, -yaw)
    period = R.find_period(rig, fr, 40, 110)
    start = R.best_loop_start(rig, fr, period, range(0, len(fr) - period - 1))
    cyc = frames_copy(fr[start:start + period + 1])
    B.hands(rig, cyc, "open", "open")
    R.make_loop(rig, cyc)
    R.in_place(cyc)
    hz = sum(f["hips"].z for f in cyc) / len(cyc)
    for f in cyc:
        f["hips"].z += rig.head["pelvis"].z - 0.35 - hz
    return R.Clip("Swim_F", cyc, loop=True, in_place=True,
                  notes="Breaststroke, body horizontal, in place (hips ~0.35 m below the standing hip height).",
                  source="CMU 125_01 (dry-land swim capture)")


def lean_wall(rig):
    s = base_stand(rig)
    f = R.copy_frame(s)
    fl, fr = K.rest_feet(rig)
    f["hips"] = f["hips"] + Vector((0.0, -0.10, -0.03))
    K.rot(rig, f, "spine_01", (-4, 0, 0))
    K.rot(rig, f, "spine_02", (-6, 0, 0))
    K.rot(rig, f, "spine_03", (-4, 0, 0))
    K.rot(rig, f, "neck_01", (8, 0, 4))
    K.rot(rig, f, "head", (4, 0, 6))
    K.plant_foot(rig, f, "l", fl + Vector((0.04, -0.28, 0)), yaw_deg=10)
    # right foot flat against the wall behind
    K.plant_foot(rig, f, "r", Vector((-0.12, 0.05, 0.42)), yaw_deg=-20, pitch_deg=-60)
    # arms crossed
    K.place_hand(rig, f, "l", (-0.16, -0.17, 0.06), (-1.0, 0.25, 0.1), (0, 0, 1), space="chest", pole=(0.5, -0.3, -0.1))
    K.place_hand(rig, f, "r", (0.14, -0.12, 0.02), (1.0, 0.3, 0.0), (0, 0, 1), space="chest", pole=(-0.5, -0.3, -0.1))
    B.finger_pose(rig, f, "relaxed", "l")
    B.finger_pose(rig, f, "relaxed", "r")
    n = 120
    frames = [R.copy_frame(f) for _ in range(n + 1)]
    K.breathe(rig, frames, amp=1.0)
    return R.Clip("Lean_Wall", frames, loop=True, in_place=True,
                  notes="Leaning back against a wall (wall plane 0.08 m behind the root), arms crossed, one foot on the wall.",
                  source="hand-keyed (IK)")


def hit_react(rig, front):
    base = idle_clip(rig)
    s = R.copy_frame(base.frames[0])
    sg = 1.0 if front else -1.0
    hit = R.copy_frame(s)
    K.rot(rig, hit, "spine_01", (-8 * sg, 0, 0))
    K.rot(rig, hit, "spine_02", (-12 * sg, 0, 4))
    K.rot(rig, hit, "spine_03", (-10 * sg, 0, 0))
    K.rot(rig, hit, "neck_01", (-12 * sg, 0, 0))
    K.rot(rig, hit, "head", (-10 * sg, 0, -6))
    K.rot(rig, hit, "upperarm_l", (0, -20, 10 * sg))
    K.rot(rig, hit, "upperarm_r", (0, 25, -10 * sg))
    hit["hips"] = hit["hips"] + Vector((0, 0.06 * sg, -0.03))
    rec = R.copy_frame(s)
    K.rot(rig, rec, "spine_02", (3 * sg, 0, 0))
    rec["hips"] = rec["hips"] + Vector((0, 0.04 * sg, -0.01))
    fl, fr = K.rest_feet(rig)
    for f, step in ((hit, 0.0), (rec, 0.10)):
        K.plant_foot(rig, f, "l", fl)
        K.plant_foot(rig, f, "r", fr + Vector((0, step * sg, 0)))
    keys = [(0.0, s), (0.09, hit), (0.3, rec), (0.6, s)]
    frames = K.interpolate(keys, 0.6)
    B.hands(rig, frames)
    return R.Clip("HitReact_" + ("F" if front else "B"), frames,
                  notes="Hit from the %s (additive-friendly, starts and ends on the idle pose)." % ("front" if front else "back"),
                  source="hand-keyed (IK)")


def takedown(rig, attacker):
    """Rear choke hold. Shared origin = attacker start; victim starts 0.45 m in front, same facing."""
    s = base_stand(rig)
    fl, fr = K.rest_feet(rig)
    vy = -0.45

    def victim_pose(t_phase):
        f = R.copy_frame(s)
        if t_phase == 0:
            f["hips"] = f["hips"] + Vector((0, vy, 0))
        elif t_phase == 1:   # grabbed, pulled back, hands on the arm
            f["hips"] = f["hips"] + Vector((0, vy + 0.10, -0.04))
            K.rot(rig, f, "spine_02", (-10, 0, 0))
            K.rot(rig, f, "neck_01", (-14, 0, 0))
            K.rot(rig, f, "head", (-8, 0, 0))
            K.place_hand(rig, f, "l", (0.05, -0.12, 0.40), (-0.3, 0.3, 0.9), (0, -1, 0), space="chest")
            K.place_hand(rig, f, "r", (-0.05, -0.12, 0.40), (0.3, 0.3, 0.9), (0, -1, 0), space="chest")
            K.plant_foot(rig, f, "l", fl + Vector((0, vy - 0.05, 0)))
            K.plant_foot(rig, f, "r", fr + Vector((0, vy + 0.12, 0)))
        elif t_phase == 2:   # struggling, knees buckling
            f["hips"] = f["hips"] + Vector((0.02, vy + 0.16, -0.18))
            K.rot(rig, f, "spine_02", (-14, 0, 6))
            K.rot(rig, f, "neck_01", (-10, 0, 0))
            K.place_hand(rig, f, "l", (0.08, -0.15, 0.36), (-0.3, 0.3, 0.9), (0, -1, 0), space="chest")
            K.place_hand(rig, f, "r", (-0.2, -0.2, -0.1), (0.1, -0.2, -0.9), (0, -1, 0), space="chest")
            K.plant_foot(rig, f, "l", fl + Vector((0, vy - 0.12, 0)))
            K.plant_foot(rig, f, "r", fr + Vector((0, vy + 0.05, 0)))
        else:                # unconscious, lowered to the ground (sitting slumped)
            f["hips"] = Vector((0, vy + 0.25, 0.16))
            K.rot(rig, f, "pelvis", (-30, 0, 0))
            K.rot(rig, f, "spine_02", (14, 0, 8))
            K.rot(rig, f, "neck_01", (25, 0, 10))
            K.rot(rig, f, "head", (15, 0, 15))
            K.plant_foot(rig, f, "l", Vector((0.18, vy - 0.45, 0.08)), yaw_deg=20)
            K.plant_foot(rig, f, "r", Vector((-0.20, vy - 0.40, 0.08)), yaw_deg=-25)
            K.place_hand(rig, f, "l", Vector((0.30, vy + 0.15, 0.05)), (0.3, 0, -0.95), (0, -1, 0), space="world")
            K.place_hand(rig, f, "r", Vector((-0.30, vy + 0.10, 0.05)), (-0.3, 0, -0.95), (0, -1, 0), space="world")
        return f

    def attacker_pose(t_phase):
        f = R.copy_frame(s)
        if t_phase == 0:
            return f
        neck_v = victim_pose(t_phase)
        Wv, Pv = rig.fk(neck_v)
        neck = Pv["neck_01"] + Vector((0, 0, 0.02))
        drop = {1: 0.06, 2: 0.22, 3: 0.55}[t_phase]
        f["hips"] = f["hips"] + Vector((0, 0.04 * t_phase, -drop))
        K.rot(rig, f, "spine_02", (-4 - 3 * t_phase, 0, 0))
        K.rot(rig, f, "neck_01", (12, 0, 0))
        K.plant_foot(rig, f, "l", fl + Vector((0, -0.12, 0)))
        K.plant_foot(rig, f, "r", fr + Vector((0, 0.20, 0)))
        # right forearm across the throat, left hand locking behind the head
        K.place_hand(rig, f, "r", neck + Vector((0.16, -0.06, -0.01)), (1.0, 0.1, 0.05), (0, -1, 0.2), space="world",
                     pole=(-0.6, -0.2, -0.3))
        K.place_hand(rig, f, "l", neck + Vector((0.08, 0.10, 0.10)), (-0.3, -0.5, 0.8), (-1, 0, 0), space="world",
                     pole=(0.6, 0.2, -0.3))
        B.finger_pose(rig, f, "fist", "r")
        B.finger_pose(rig, f, "support", "l")
        return f

    poses = attacker_pose if attacker else victim_pose
    keys = [(0.0, poses(0)), (0.45, poses(1)), (1.4, poses(2)), (2.1, poses(2)), (3.0, poses(3))]
    frames = K.interpolate(keys, 3.0)
    if not attacker:
        B.hands(rig, frames[:5])
    name = "Takedown_Attacker" if attacker else "Takedown_Victim"
    return R.Clip(name, frames,
                  notes="Rear choke hold, 3 s. Paired clip: put both actors on the same transform (victim starts 0.45 m in front, same facing).",
                  source="hand-keyed (IK)")


def crouch_walk_f(rig):
    return cached("crouchF", lambda: loco(rig, "CrouchWalk_F", 139, 31, 2.3, 4.4, (24, 60), "F",
                                          "Careful crouched creep.", head=0.0))


def crouch_walk_b(rig):
    """Backwards creep = the forward creep played in reverse (slow careful steps reverse cleanly)."""
    c = clone(crouch_walk_f(rig), "CrouchWalk_B")
    c.frames = c.frames[::-1]
    c.notes = c.notes.replace("Careful crouched creep.", "Careful crouched creep backwards (reversed forward creep).")
    return c


def strafe_run(rig, side):
    """Fast lateral shuffle (basketball defensive slide), arms brought in to a neutral run carry."""
    want = "L" if side == "L" else "R"
    c = loco(rig, "Run_" + side, 78, 30, 1.55, 2.5, (8, 26), want, "Fast lateral shuffle (facing forward).", cycles=2)
    n = len(c.frames) - 1
    for i, f in enumerate(c.frames):
        u = i / n
        w = math.sin(4 * math.pi * u)
        K.rot(rig, f, "spine_01", (-12, 0, 0))
        K.rot(rig, f, "spine_02", (-6, 0, 0))
        K.place_hand(rig, f, "l", (0.24, -0.16 + 0.03 * w, -0.20), (-0.2, -0.85, 0.4), (0, -0.4, 0.9), space="chest")
        K.place_hand(rig, f, "r", (-0.24, -0.16 - 0.03 * w, -0.20), (0.2, -0.85, 0.4), (0, -0.4, 0.9), space="chest")
        B.finger_pose(rig, f, "relaxed", "l")
        B.finger_pose(rig, f, "relaxed", "r")
    R.fix_signs(c.frames)
    return c


def peak_window(rig, subj, trial, bone, before, after, fps=120, t0=0.0, t1=None):
    """Clip window centred on the fastest moment of `bone` (e.g. the release of a throw)."""
    fr = B.cmu_frames(rig, subj, trial, t0, t1, fps)
    best, bi = 0.0, 0
    prev = None
    for i, f in enumerate(fr):
        W, P = rig.fk(f)
        p = P[bone] - P["pelvis"]
        if prev is not None and (p - prev).length > best and i > 5:
            best, bi = (p - prev).length, i
        prev = p
    a = t0 + max(0, bi - int(before * R.FPS)) / R.FPS
    b = t0 + min(len(fr) - 1, bi + int(after * R.FPS)) / R.FPS
    B.log("peak", subj, trial, bone, "at %.2fs" % (t0 + bi / R.FPS), "window %.2f-%.2f" % (a, b))
    return a, b


def throw(rig):
    a, b = peak_window(rig, 141, 11, "hand_r", 0.9, 0.7)
    c = B.cmu_oneshot(rig, "Throw", 141, 11, a, b, notes="Overhand throw, release ~0.9 s in.")
    return c


# ---------------------------------------------------------------------------------- catalogue

def walk_clip(rig):
    return cached("walk", lambda: loco(rig, "Walk_F", 16, 15, 0.0, None, (24, 44), "F", "Normal walk."))


def run_clip(rig):
    return cached("run", lambda: loco(rig, "Run_F", 16, 35, 0.0, None, (16, 28), "F", "Jog/run."))


def builders():
    return {
        "Idle": lambda rig: idle_clip(rig),
        "Idle_Armed": idle_armed,
        "Walk_F": walk_clip,
        "Walk_B": lambda rig: loco(rig, "Walk_B", 143, 39, 1.0, 8.5, (24, 50), "B", "Walk backwards."),
        "Walk_L": lambda rig: loco(rig, "Walk_L", 143, 40, 1.0, 7.5, (24, 60), "L", "Side-step walk to the left (facing forward)."),
        "Walk_R": lambda rig: loco(rig, "Walk_R", 143, 40, 1.0, 7.5, (24, 60), "R", "Side-step walk to the right (facing forward)."),
        "Run_F": run_clip,
        "Run_B": lambda rig: loco(rig, "Run_B", 76, 11, 0.8, 3.6, (14, 34), "B", "Fast backpedal."),
        "Run_L": lambda rig: strafe_run(rig, "L"),
        "Run_R": lambda rig: strafe_run(rig, "R"),
        "Sprint_F": lambda rig: loco(rig, "Sprint_F", 9, 1, 0.0, None, (14, 26), "F", "Sprint."),
        "CrouchIdle": crouch_idle,
        "CrouchWalk_F": crouch_walk_f,
        "CrouchWalk_B": crouch_walk_b,
        "CrouchWalk_L": lambda rig: loco(rig, "CrouchWalk_L", 139, 14, 0.5, 8.0, (24, 70), "L", "Crouched sneak side-step left."),
        "CrouchWalk_R": lambda rig: loco(rig, "CrouchWalk_R", 139, 14, 0.5, 8.0, (24, 70), "R", "Crouched sneak side-step right."),
        "Crawl_F": crawl,
        "Jump_Start": jump_start,
        "Fall_Loop": fall_loop,
        "Land": land,
        "Slide": slide,
        "Vault": lambda rig: B.cmu_oneshot(rig, "Vault", 127, 25, 0.0, None,
                                           notes="Running jump over a waist-high obstacle; root motion kept (forward travel)."),
        "Mantle": mantle,
        "Climb_Ladder": climb_ladder,
        "Hang_Idle": hang_idle,
        "Shimmy_L": lambda rig: shimmy(rig, "L"),
        "Shimmy_R": lambda rig: shimmy(rig, "R"),
        "Hang_ClimbUp": hang_climb_up,
        "Swim_Idle": swim_idle,
        "Swim_F": swim_f,
        "Aim_Pistol": lambda rig: aim_clip(rig, "pistol"),
        "Aim_Rifle": lambda rig: aim_clip(rig, "rifle"),
        "Fire_Pistol": lambda rig: fire_clip(rig, "pistol"),
        "Fire_Rifle": lambda rig: fire_clip(rig, "rifle"),
        "Reload": reload_clip,
        "Punch": lambda rig: B.cmu_oneshot(rig, "Punch", 143, 23, 0.2, 1.4, fingers=("fist", "fist"),
                                           notes="Jab/cross from guard."),
        "Takedown_Attacker": lambda rig: takedown(rig, True),
        "Takedown_Victim": lambda rig: takedown(rig, False),
        "Throw": throw,
        "HitReact_F": lambda rig: hit_react(rig, True),
        "HitReact_B": lambda rig: hit_react(rig, False),
        "Death_F": lambda rig: death(rig, "Death_F", 140, 1, 0.6, 4.6, 1.6, "Collapse forward onto the face; root motion kept."),
        "Death_B": lambda rig: death(rig, "Death_B", 140, 8, 0.6, 5.2, 1.7, "Collapse backwards onto the back; root motion kept."),
        "Talk_Phone": phone,
        "Smoke_Idle": smoke,
        "Sit_Chair": sit_chair,
        "Lean_Wall": lean_wall,
        "Look_Around": look_around,
        "Patrol_Walk": patrol_walk,
        "Cower": cower,
        "Run_Panic": run_panic,
    }


def climb_ladder(rig):
    fr = B.cmu_frames(rig, 13, 33, 1.9, 6.2, 120)
    R.smooth(fr, 1)
    R.face_forward(rig, fr)
    B.hands(rig, fr, "grip", "grip")
    period = R.find_period(rig, fr, 78, 100)
    start = R.best_loop_start(rig, fr, period, range(0, max(1, len(fr) - period - 1)))
    cyc = frames_copy(fr[start:start + period + 1])
    rise = cyc[-1]["hips"].z - cyc[0]["hips"].z
    R.make_loop(rig, cyc)       # spreads the vertical rise -> in place vertically
    R.in_place(cyc)
    cyc = K.timewarp(cyc, lambda u: u, 1.9)   # careful capture is slow: play the rung cycle in 1.9 s
    R.fix_signs(cyc)
    return R.Clip("Climb_Ladder", cyc, loop=True, in_place=True,
                  notes="Ladder climb cycle (two rungs), in place: vertical travel %.2f m per 1.9 s cycle removed." % rise,
                  source="CMU 13_33")
