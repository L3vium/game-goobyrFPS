# Cast generator (Track A) — notes for the first-person track

Headless Blender 5.2 scripts that build the 0.7.0 cast from signed-distance parts
(`sdf.py`, meshed with OpenVDB), so every surface is smooth whatever the polygon budget.

    blender -b --factory-startup --python Tools/Blender/characters/cast/cast_build.py -- Chr_Rook --render
    python Tools/Blender/characters/cast/sheet.py            # design sheets from the renders

Outputs (git-ignored): `Tools/Blender/out/cast/{work,renders,sheets,polyhaven}`.

## The one hand/forearm module: `glove.py`

Every character (and the first-person arms) uses the same hand: thumb, separate index finger and a
fused three-finger paddle (middle + ring + pinky). All 30 finger bones exist; the paddle is skinned
equally to the three chains, so curling them together bends it as one.

* **Hand frame** — `Frame(origin=wrist, u=toward the fingers, v=toward the thumb, w=palm normal)`.
  Build it from your rig with `frame_from_arm(wrist, along, thumb_dir, side)` (the right hand gets
  the mirrored frame, so one local layout serves both sides).
* **Layout** — `LAYOUT` (metres, hand frame, already scaled by `SCALE` = 1.10): `hand_tail` and, per
  finger, the heads of `_01`, `_02`, `_03` and the tip. `bone_segments(fr, side)` gives world
  (head, tail) for `hand_<s>` and the 15 finger bones — use it to place your arm bones.
* **Meshes** — `make_fp_arm(side, shoulder, elbow, fr)` (needs bpy) returns the glove, the bomber
  sleeve, the rib cuff and the team armband as objects with vertex groups named like the shared
  53-bone skeleton (`upperarm_*`, `lowerarm_*`, `hand_*`, finger bones). Material slot names are in
  `obj["cast_material"]`: `Glove`, `Jacket`, `Rib`, `Team` (team colour).
  Lower level: `hand_part()` / `sleeve_parts()` return SDF parts, `hand_weights()` / `arm_weights()`
  return capsule-distance weights for any vertex array.
* **Rest pose** — the third-person rest is the shared A-pose (arms 45 degrees down, palms down/in,
  thumbs forward, fingers straight). Nothing in the module assumes it: pass any frame.

Sleeve and glove are the hero's (olive MA-1 sleeve, dark leather glove). Proportions: upper arm
0.300 m, forearm 0.265 m (shoulder joints 0.38 m apart, eye 1.60 m, top of skull 1.80 m).
