# Changelog

## Unreleased
- Threat read-out: a screen-space diamond over each NPC that fills with its gauge (white, then
  amber), turns amber with a "!" when it confronts you, red in combat, "?" while searching, and a
  phone with a countdown ring while it calls (pinned to the screen edge when off-screen). Arcs
  around the crosshair point at whoever is noticing you, a status chip above the minimap says hidden
  / concealed / suspicious / searching / spotted / combat, and the target keeps a red marker with
  its distance at any range. Notifications for bodies found, calls made and calls cut.
- Stealth AI in three contained stages: vision shortened to 20 m in a 90° cone with a slower edge-
  of-vision band; NPCs grow suspicious (stop and stare, walk over past half the gauge), then
  confront you for 2 s with their weapon up (break line of sight to make them search instead, aim at
  them and they fire), then fight. A fight no longer alerts the zone: others join only by seeing it
  within 18 m, hearing shots, or by the phone call, which reaches the caller's zone mates within 60
  m. Crowds scream instead of reporting. The alarm flare is fired rarely.
- Pinecrest Hollow and Mirage Bay: starts moved 80-100 m from any guard (Pinecrest's north trail was
  6 m from one, Mirage Bay's corniche start 7 m), maps rebuilt with props seated on the ground.
  Piers and boats over water no longer count as floating.
- Elmridge: the south and east starts move away from the guards (the south one was 3 m from a door
  guard). The security room's three monitors stand on a three-desk console instead of overlapping in
  mid-air, and vent grilles sit flush under the ceilings. The validator counts a prop touching a
  wall or furniture as supported.
- Map builds snap every NPC post and patrol point that sits a little off the baked NavMesh onto the
  closest walkable point within 4 m (the Elmridge door guard and cinema crowd member included); the
  validator fails NPC posts off the NavMesh.
- Death and end of match: the look at your own body lasts 1.2 s before spectating (the HUD reminds
  that Esc leaves). The end screen has buttons: the host takes the room back to the lobby at once,
  anyone can leave the room; without a click it goes back after 45 s.
- Map dressing sits on the ground: props, nature pieces and terrain trees are lowered to the lowest
  ground under their footprint (rocks bed in deeper), explosive barrels stand on the patrol's floor
  (never on a roof or in the air over a dip), and crowd or patrol members off any floor stand on the
  terrain.
- Safe starts: the match draws its start point among those with no guard post or patrol point within
  50 m (else the roomiest). The map validator fails a start closer than that, and reports props
  floating over the terrain.
- Agent name: the nickname typed in the menu wins over the Steam persona, is saved, and updates live
  for everyone already in the room; until one is typed the field follows the Steam persona as soon
  as Steam comes up.
- No more screen vignette when crouching or hiding (concealment will show on the HUD). The camera
  sits higher: eyes at 1.74 m standing, 0.98 m crouched (NPC sight uses the same heights).
- The cursor stays locked in the game window during a match: the lock is re-applied every frame the
  window has focus and the moment focus comes back (alt-tab, a click outside), so the mouse can no
  longer leave the window or click other tabs.
- Maps rebuilt at 500 m on Unity Terrain (HillMap): valleys, terraces, cliffs, graded streets,
  buildings stepping down the slopes on retaining walls, dense forests; 210-230 NPCs per map with
  patrol groups, room guards and crowds around staged scenes.
- Title screen: key art (an agent against a stone facade, depth of field) and a Hitman-style menu
  (tab bar, picture tiles, big title, footer); gamepad selection highlights buttons and tiles.
- Stylized shading: painterly light ramp, soft specular, colour drift and grain, lit chamfers.
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
