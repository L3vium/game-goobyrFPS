# Goobyr FPS — level design brief

Read `tasks/lessons.md` first. The first pass of the maps was rejected: too small (320 m), grids
of identical boxes, no verticality, no staging. This brief is the bar for every map.

## Philosophy: the assassination sandbox
The reference is the *design philosophy* of the big stealth-assassination sandboxes (the user
shared top-down maps of that genre as inspiration). Our maps, places and names are **original** —
never reproduce an existing game's layout, landmark or name.

1. **One landmark per zone**, readable from everywhere (a tower, a mansion, a ship, a mast, a
   water tower). Players orient by them; snipers use them.
2. **Every key building has 4+ ways in**: guarded front door, service / kitchen entrance,
   an open window or balcony reached by a drainpipe / ladder / scaffold, the roof (zipline, jump pad,
   neighbouring rooftop), and something low (basement ramp, tunnel, boathouse, sewer, crawl through
   tall grass). Each way trades risk for time.
3. **Security layers**: public areas (streets, markets, gardens) → restricted (staff areas,
   compounds, yards, patrols) → VIP (the target's floor, the office, the suite). Guards thicken
   inward; posts guard the choke points between layers.
4. **Verticality everywhere**: multi-storey buildings with roof access, balconies, rooftop links
   (planks, bridges), towers with sniper nests, cliffs and terraces, basements and cellars, docks
   below street level, bridges over rivers/canals with walkable banks underneath.
5. **Mise en scène**: staged situations that tell a story *and* create opportunities, each with
   activity nodes for the targets: a garden party with a bar and a stage (Speech, Drink), a deal in a
   warehouse (Inspect), a rehearsal, a briefing room, a kitchen in rush, a workshop, a gym, a
   card game, guards smoking behind a building (Smoke), a chef tasting (Dining). Place props so the
   scene reads at a glance.
6. **Loops, not dead ends**: streets and alleys connect in rings; interiors have two exits; a chase
   can always continue.
7. **Systemic opportunities seeded on purpose**: chandelier above the party, breaker in the utility
   room of the main building, air handler in its basement, poisonable drinks on the target's
   routine, toilets reachable alone, explosive barrels and gas bottles next to patrol routes,
   distractions (radio, TV, phone, generator, car alarm) near choke points, hiding spots (wardrobes,
   dumpsters, haystacks) at the edges of patrols, tall grass to approach unseen, smoke at choke
   points.
8. **Density and dressing**: no empty lots. Streets get lamps, benches, bins, signs, hydrants,
   parked cars, market stalls, parasols, planters; interiors are furnished (use `Room` presets);
   yards get crates, pallets, barrels; nature gets clusters (trees + bushes + rocks), not uniform
   sprinkles. Vary heights and footprints of buildings; mix the styles that fit the map.

## Size and structure
- **About 1 km²**: `K.Grid(250, 250, 4f, new Vector2(-500, -500), …)` (1000 m × 1000 m). The play
  space is the whole square; keep edges natural (water, cliffs, forest walls, fences) rather than a
  void.
- **Three zones** (index 0 town, 1 lair, 2 mansion/building), each ~250-400 m across, joined by
  roads, paths, a river or a canal, and an in-between area (outskirts, park, fields, docks) that is
  not empty.
- **NPCs**: 70-110 spawn markers per map (guards on posts, patrols with routes, lookouts in nests);
  every patrol gets its explosive barrels automatically (`K.Patrol`).
- **Weapons**: 40+ weapon spawns (many SMG/pistol/shotgun/sniper), gadgets, and 3-4 hidden
  candidate spots for each unique weapon (rocket launcher, flamethrower).
- **Match points**: 4 start points (on the edges), 4 extractions (boat, helipad, car, tunnel…).
- **Targets**: every activity type of every target's routine must exist in its zone (the validator
  checks it). Use `BaseActivities(zone, centre, radius)` plus staged nodes.

## Tools (see `Assets/_Game/Editor/Maps/`)
- `MapKit` terrain: `Grid`, `Height(rect, h, colour)`, `Paint(rect, colour)`, `Noise(rect, amp, scale)`,
  `Smooth(rect, iterations)`, `Hole(rect)`, `GroundAt`, `At(x, z, up)`; `Water`, `Bridge`, `Ramp`,
  `Stairs`, `Climbable`, `Fence`, `Block`.
- Kit (`MapKit.Kit.cs`): `Piece(style, name, pos, yawFacing, tint)`, `Prop(name, …)`,
  `NaturePiece(name, …)`, `Scatter(area, count, pieces, …)`, `Building(KitSpec)` → `KitBuilding`
  (`Local(x, y, z)`, `FloorY`, `volume`), `Furnish(building, floor, Room, rect, rng)`,
  `PartitionKit`, `DrinksOn`, `ToiletKit`, `ChandelierKit`, `BreakerKit`, `VentKit`, `DistractionKit`,
  `HidingKit`. Styles: `Timber`, `Stucco`, `Suburb`, `Classic`, `Props`, `Nature` — the piece list
  and sizes are in `Art/Resources/Kit/manifest.json`. Modules are 3 m × 3.4 m.
- Gameplay: `Npc`, `Patrol`, `Route`, `Activity`, `Activities`, `Cover`, `Point`, `Weapon`,
  `Gadget`, `UniqueWeapon`, `Zone`, `BuildingVolume`, `Sprinklers`, `Zipline`, `JumpPad`, `Alarm`,
  `Lamp`, `PatrolCar`, `Grass`, `Barrel`, `Crate`, `Car`.
- Ambiance (sky, volumetric fog, grading) comes from the `MapDefinition` and is applied by
  `Finish()`.
- Performance: call `K.DistanceCulling()` at the end of `Compose()` (one-level LODGroups per tree,
  building and prop; small items stop casting sun shadows). Keep the saved scene under 90 MB:
  every kit piece is a GameObject, so prefer fewer, larger buildings.

## Verification loop (mandatory)
Build from the map's menu item, then:
1. `Goobyr → Validate Maps` (or `MapValidator.ValidateAll()`): must say `OK`.
2. Screenshots from at least six viewpoints — aerial of each zone and ground-level shots in the
   streets and interiors — and fix what looks empty, repetitive or broken (floating pieces, walls
   facing inward, blocked doors, unreachable roofs).
3. A solo play test (`NetSession.Instance.StartSolo(mapIndex, MatchMode.Coop)` in play mode): the
   match starts, NPCs spawn on the NavMesh, no errors in the console.
