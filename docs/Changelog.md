# Changelog

## Unreleased
- Minimal Hitman-style HUD: minimap, transient weapon/ammo and health, weapon wheel (Q / LB),
  no objective panel; the map screen gets a contract column with a live portrait of the target.
- Instinct senses the surroundings only (target 60 m, people 25 m, objects 20 m).
- Crouch and sprint are held; crouching while moving slides (further out of a sprint); Q/E slides
  removed. Patrols walk slowly and in groups of up to five; crowds flee from trouble.
- Unity Terrain for the maps (LandShape stamps, eight painted ground layers, forests, grass).
- Instinct vision (hold Tab, D-pad left): the world dims and what matters shows through the walls,
  the target and the proof in red, teammates in blue, nearby NPCs in white or amber when hunting,
  interactables and pickups around you in white. The scoreboard moves to the key left of 1
  (D-pad right).
- Mirage Bay rebuilt as a 1 km² sandbox (`MapGulf` partials: terrain, souk, docks, Zenith, between):
  sea on two sides, dunes and a sandstone ridge on the others, 105 NPCs, 62 weapon spawns, four
  hidden spots each for the rocket launcher and the flamethrower, four edge starts, four
  extractions (marina speedboat, Zenith helipad, desert road pickup, quay storm drain).
- `MapKit.DistanceCulling()`: the forest's distance culling and small-item shadow cut-off, now a
  shared opt-in step a map calls before `Finish` (Pinecrest Hollow uses it, output unchanged).

## 0.1.0 — first playable
- Steam / LAN / solo sessions, lobby with mode, map, teams, proximity voice option.
- Three modes (free-for-all, teams up to 4, co-op), elimination and spectating.
- Three generated maps with three zones each, 15 original targets per map.
- Hostile AI with squads, last known positions, cover, flanking, searches, radio reinforcements,
  map-wide alarm, bodyguards, target routines, patrol cars.
- Systemic props: fire and sprinklers, power cuts, chandeliers, drinks and toilets, ventilation
  gas, distractions, hiding spots, ziplines, jump pads, climbing, tall grass, explosive barrels next
  to every patrol route.
- Weapons and gadgets: hush pistol, pistol, SMG, shotgun, sniper, unique rocket launcher and
  flamethrower, coin, brick, smoke, C4 + remote, lethal and emetic poison, punches and takedowns.
- FPS controller with slides, mantling, climbing; viewmodel; juice; HUD, map screen, end screen.
- GD Tool and map validation; EditMode tests for the match rules.

### Known limits (to iterate)
- Online play was verified with one machine only (solo host in the editor); a real two-account Steam
  session still has to be tried.
- Hero props (cars, furniture, weapons) are procedural boxes; Blender models in
  `Resources/Models` (`W_<Kind>`, `Car`) replace them automatically when added.
- NPC and level balance comes from short playtests; expect tuning in the GD Tool.
