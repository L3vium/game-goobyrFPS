# Lessons

Rules I write for myself after a correction. Read at the start of every session.

## 2026-10-30 — first playable judged ugly, levels too small, UI unprofessional
- **Quality bar for art.** Never ship primitive boxes as final art. Environment and characters
  are detailed modular assets (bevels, frames, trims, layered silhouettes) — Blender kit in
  `Tools/Blender/`, assembled by the builders. Compare every screenshot against the references the
  user gave before calling a visual task done.
- **Ambiance is a feature.** A map is not finished without a designed sky (clouds, sun halo),
  volumetric fog and light shafts, colour grading per map, and dressed interiors.
- **Level design = Hitman sandbox philosophy.** Maps around 1 km² (10x the first pass), dense and
  detailed, strong verticality (rooftops, balconies, towers, basements, cliffs), several layered
  routes into every key building (guarded front, service entrance, window, roof, tunnel, water),
  security layers (public → restricted → VIP), landmarks visible from everywhere, and *mise en
  scène*: staged situations (a party, a briefing, a deal, a rehearsal) that tell a story and create
  opportunities. Small grids of identical boxes are not level design.
- **UI must look professional.** Clean, restrained, typographic (real font: Barlow), flat panels,
  thin rules, subtle fades and slides — no squash-and-stretch on UI elements. Keep the juice for the
  game world, not the menus.
- **Name.** The game is "Goobyr FPS" (with a space) everywhere, including the title screen.

## 2026-10-30 — ended prompts without building and pushing
- **Every prompt ends with a player build, pushed, and merged to main.** No exceptions, even when
  a background agent still works, even for a small feature. Order: commit + push each change →
  `Goobyr/Build/Player` → commit + push the build → release main without checking it out (the
  `commit-tree` procedure in CLAUDE.md). A turn that changed code and stops before this is not
  finished.
- **Background agents don't block the build.** Ask them to pause editor use, build from what is
  on disk, and say in the report which in-progress work the build does or does not contain.

## 2026-10-30 — opened Unity myself, slow opaque subagents
- **Never launch, close or restart the Unity editor.** If MCP is unreachable, say so and ask the
  user; they open it. The MCP port can change (8084 → 8080): ask, don't guess.
- **Work in the main thread, visibly.** Background subagents are opaque to the user and slow.
  Do the work myself with short progress lines, and finish the whole request in the same turn
  instead of stopping to wait on something.

## 2026-10-30 — level 1 judged flat, empty, ugly; HUD too big
- **Verticality is the master word.** Every map is hills, slopes, terraces, cliffs, ravines and
  towns on slopes, with buildings above and below each other (Far Cry-like terrain). Use Unity
  Terrain (heightmap, splat layers, tree instances, details) at full potential, never a flat grid.
- **Size 500 × 500 m, concise like Hitman.** Rooms, partitions, low walls, obstacles: every metre
  is designed. No empty space; an open area exists only for a scene (a helicopter landing, a speech
  with a crowd). Enemies everywhere: 150-220 markers per map, patrols of up to 5, static guards in
  rooms, crowds at the scenes. Forests are huge and dense.
- **Look: stylized but AAA finish** (Hitman / Sea of Thieves level), realistic proportioned humans,
  not goofy mascots.
- **HUD minimal like Hitman:** nothing by default, small minimap, weapon/ammo only when relevant,
  health only when hurt, weapon wheel, no mission/objective panel. Everything is discovered.
  Gamepad playable everywhere. Instinct shows the surroundings only.
- **Controls:** crouch and sprint are held, not toggled; crouch while moving slides, sprint +
  crouch slides further; no separate slide buttons. Patrols walk slowly.
- **Title screen:** key art like Hitman (one detailed character, close, posed, simple building
  backdrop) and a tiled tab menu; contracts must be minimal and instantly understandable.

## 2026-10-31 — 0.3 judged unreadable, unrealistic, buggy (cursor, floating props, spawns)
- **Readability first, like Hitman but not a copy.** Detection is gradual (suspicious → engaged with
  a confrontation window → combat) and contained (only witnesses, gunfire or a phone call spread
  it). Every state has an indication: overhead diamond, arcs around the crosshair, status chip,
  minimap ring, notifications, tension audio. The target is shown always (marker, compass, maps).
- **Validate what the player sees, not only counts.** The validator now catches floating props,
  starts near NPCs or off the NavMesh, missing tall grass; run it after every map build and look at
  screenshots in game before calling a map done.
- **UI pivots on the anchor in `UIFactory`.** Every element's position is its anchored corner/edge,
  not its centre: compute positions with that, and use layout groups (`UIWidgets`) for lists so
  nothing can overlap. Screenshot every changed screen.
- **Generated only, realistic target.** No downloads or packs (user choice): textures, sounds,
  props and humans come from builders, Blender scripts and `generate_3d`.
- **Unity MCP for this machine is 8080** (8082 until 2026-11-02, then 8084; the user says when it moves), not registered as a Claude MCP server: drive it over HTTP with `Tools/unity_mcp.py`.

## 2026-10-31 — Caster mode (tournament spectators)
- **Spectating is a product, not a fallback.** A spectator must be able to join on purpose (even
  mid-match), see several agents at once, pick, focus and move freely: esports-grade view, same
  view for eliminated players (limited by the team rules).
- **A new script Unity never imported stays out of the compile.** When the run shows fewer tests
  than written, check `CompilationPipeline.GetAssemblies(...).sourceFiles`; renaming the file (and
  its .meta) on disk forces a clean import. Count the tests after every new test file.

## 2026-11-01 — 0.5 judged ugly, buggy, dumb AI, bad LD; "not merged to main"
- **main must look like its own branch.** A fast-forward merge leaves main and dev on the same
  commit, so Fork draws one line and the user believes nothing was merged. Give main its own
  merge commit (`git commit-tree "dev^{tree}" -p main -p dev`, see CLAUDE.md); never `--ff-only`,
  and never check main out under the open editor (it reimports the whole project).
- **Realistic means real PBR sources.** Stylized/low-poly is rejected. Art comes from Poly Haven
  (CC0 textures, models, HDRIs, via its API), MakeHuman/MPFB (CC0 humans) and CMU mocap,
  processed by scripts in `Tools/` and credited in `docs/Credits.md`. Still no store packs and no
  accounts. This replaces "generated only".
- **Sound: tune hearing to the sight range.** A noise radius several times the vision range with no
  wall occlusion and no responder cap makes every NPC on the map converge. Hearing is graded,
  occluded, and only the 1-2 nearest respond.
- **Parkour probes must handle thin geometry.** Probe ledges at several depths past the face
  (walls and sills are 0.3 m), and grab ledges in the air.
- **No isolated NPCs.** Every guard belongs to a bastion, a perimeter, a scene or a patrol.

## 2026-11-03 — 0.6 judged "PS3", Hitman-like hero, enemies passive, hands and punch wrong
- **Original characters only.** No bald man in a suit and red tie, nothing that reads as another
  game's hero. The protagonist is our own design (Rook: copper quiff, bomber jacket, scarf).
- **Direction: beautiful and a bit goofy, "TABS-flavoured" not TABS-literal.** The world is
  realistic (PBR, real light, real grass); people are human-shaped and slightly stylized. The
  goofiness comes from motion and reactions (wobble, faces, barks, silly ragdolls), never from
  mascot bodies or low-poly shapes.
- **Grass is geometry, not a texture.** GPU blades with wind and trampling wherever the ground is
  soft, edges feathered, mown lawns in gardens. A flat green splat reads as PS3.
- **One body contract.** Every eye, chest, head and capsule height comes from `Core/BodyMetrics`;
  the camera, NPC senses, headshots and markers all read it. A hard-coded height is a bug.
- **First-person grips are authored, not solved.** Poses are made in Blender around each weapon on
  an arms rig with a weapon socket; IK and bone-measured grips drift and look wrong.
- **Test the attack chain after any AI change.** A guard who sees an armed trespasser must shoot
  within seconds; the AI scenario runner (`Goobyr/Test/AI Scenarios`) proves it. A passive enemy
  is the worst AI bug, worse than a dumb one.
- **Several agents, one editor.** Each agent sets `UNITY_MCP_AGENT`, holds the editor lock for
  anything that changes the editor and checks it still holds it before each step; commits go
  through `Tools/commit.py` (own paths only). Long MCP calls on Unity 6.6 lose their reply: write
  results to a file and poll it.
- **Unity 6.6:** `Object.GetInstanceID()` no longer compiles (use `GetHashCode()` or
  `GetEntityId()`); renderer features can come back null after the upgrade (relink them); huge
  or NaN bounds in procedural draws hang the editor (sanity-check every `worldBounds`).
- **Judge the shots before the judge does.** Screenshot scripts check every framing (camera not in
  a collider, nothing within 2-3 m of the centre ray) and re-roll; a judge round spent on shots
  of walls is wasted. Also scan for null or default materials before each judge round.

## 2026-11-05 — the look of 0.9.0: empty maps, stuck on nothing
- **Hitmen, goofy on purpose.** The game is called Hitmen, and its look is the flat goofy one: this replaces
  "Goobyr FPS" and "realistic proportioned humans" above.
- **Read the console after a map build.** Every terrain tree printed "couldn't be instanced" for a whole
  release: a prefab without an LOD group or renderer on its root draws nothing and keeps its collider. A
  build log with thousands of trees proves nothing about what is on screen: look at a screenshot from the air.
- **Find the MCP port by asking each server** (`mcpforunity://project/info`, 8080 to 8084): it moves.
