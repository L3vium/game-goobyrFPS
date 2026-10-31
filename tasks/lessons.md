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
  `Goobyr/Build/Player` → commit + push the build → `git checkout main && git merge --ff-only dev
  && git push origin main && git checkout dev`. A turn that changed code and stops before this is
  not finished.
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
- **Unity MCP for this machine is 8082**, not registered as a Claude MCP server: drive it over HTTP.
