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
