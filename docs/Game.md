# Goobyr FPS — design

Multiplayer (1-8) stealth-assassination FPS with realistic PBR art and realistic humans
(MakeHuman bodies, CMU mocap). **Every NPC is hostile to every player.** A target is drawn from the
map's pool of 15; kill it, take the proof from its body (its dossier or the item it carries),
carry it to one of the active extraction points. PvP is on: killing the carrier drops the proof.
There are no disguises — it is stealth (sight, sound, cover, grass, smoke, darkness), and when
it goes loud, everyone is lethal both ways. Maps follow Hitman's **social ladder** instead
(`AI/SecurityZone`, `DetectionModel.TierFactor`): in **public** places (streets, plazas, the
market, the gala floor) a player who behaves - no gun raised, not sprinting, not climbing walls,
not carrying the proof - is just another passer-by; in **restricted** areas (staff corridors,
yards) guards notice at 55 % speed and confront before shooting; **hostile** areas (compounds,
VIP floors, bunkers) are trespassing with full detection.

## Modes
- **Free-for-all** — everyone for themselves.
- **Teams** — 2 to 4 teams of any size (host picks the count, players pick their team or shuffle);
  whoever extracts makes their team win. Teammates cannot hurt each other.
- **Co-op** — one team against the AI; lost if everyone is eliminated.

Every player starts together at one of the map's start points (drawn per match among those with no
NPC post or patrol point within 50 m) with the starting
loadout (GD Tool; hush pistol + coins by default). Death = eliminated: a 1.2 s look at your body,
then the caster view restricted to whom you may watch (teammates in Teams, anyone in FFA and co-op;
no free camera or map in Teams). The match ends on an extraction, when every player is eliminated, or at the
time limit (draw); the end screen's buttons go back to the lobby (host) or leave the room at once. The proof carrier is revealed to everyone every 10 s.

## Maps (three zones each, each with its own reinforcements)
Designed in `docs/maps/*.md` (zones, social tiers, rooms, entries, NPC tables, targets, starts),
built by `Editor/Maps/Map<Name>` on the realistic pipeline below.

| Map | Zone 0 | Zone 1 | Zone 2 (key building) |
|---|---|---|---|
| **San Lázaro** (Colombian jungle, fiesta day) | Pueblo: plaza and stage, church and bell tower (zipline to the dock), cantina and its cellar, market street, police post, 12 open houses of ~45, dock, bus stop | Cafetal and airstrip: five coffee terraces with stairs, picker huts, drying sheds, the beneficio (gas roaster, coffee chute zipline into the stable yard), water tower, quarry, banana grove, jungle chapel ruin; ridge checkpoint, hangars A/B, radio shack and mast, DC-3 wreck, fuel depot | Hacienda La Cumbre: walled grounds (frisk gate, padlocked service gate, rusted cemetery gate, vine trellis, ivy), Casa La Cumbre (vault, cellar, 3 floors round a patio, mirador tower to +22, 30 rooms), Cueva del Diablo tunnels (cantina - quarry - casa wine cellar, quarry - waterfall dock) |
| **Qasr al-Rimal** (desert oasis, audience day) | Ain Zarqa: well plaza, two covered souk streets framing the keep, tea house, mosque and minaret, caravanserai, bus station, flat roofs and planks | Grove and wadi: date palms, irrigation channels from the spring pool, date press (screw press), rock arch, the Needle, abandoned kasbah, goods-lift base station; radar on the Little Mesa (bunker, dish, radome, motor pool, ladder face, rope bridge) | The citadel on the Great Mesa: hairpins, walls, bastions, three courts, the keep (cellar, 3 floors round a screened light well, glass penthouse, roof helipad, wind-tower duct); the qanat (two branches, well shaft) and the smugglers' cleft |
| **Weisshorn** (alpine gala night, snow) | Sankt Anna: chalet street, rink, church and spire, Gasthaus and its cellar (lake culvert), ski rental, funicular valley station, frozen lake, boathouse | Ski area: pistes between pine bands, chairlift (bullwheel), mid-station cafe, groomer garage (tiller) and mine gallery, ski-jump; observatory and bunker on the summit (dome, mast, cable car) | Grand Hotel Weisshorn: terraces, delivery yard, spa garden, 70 x 30 m hotel (underbelly, staff tier, lobby atrium, double-height ballroom with mezzanine and chandelier, suites, VIP floor, attic, clock tower) |

Each map: ~116 NPC markers following its design table, 6 edge starts and 3 inside starts with a
pass, 4 extractions, 30-50 weapon spots, 80+ throwables, 3 spots per unique weapon, cameras and a
recorder, secure doors (crowbar, keycard, padlock, keypad), vents, drop ladders, drainpipes,
chatter spots that reveal the opportunities, and pause-map POIs for every landmark, entrance,
climb, vent, tunnel and overlook. `SecurityOutside` sets the tier of the ground outside every zone.

## Map recipe (500 m, `Editor/Maps/RealMap` + `Editor/Maps/Arch`)
- **Land** (`RealMap`, `MapKit.Terrain`): a relief function shaped by `LandShape` stamps (graded
  roads, building pads, terraces, mesas, gorges), PBR ground layers per biome (`PbrLibrary.Layer`),
  holes cut over cellars and tunnel mouths, `Veg_*` trees, bushes and rocks as terrain trees
  (`VegLibrary`: LOD groups, trunk colliders), instanced `Veg_*` grass details, 8-12 tall grass
  fields with hiding triggers, invisible map edges, a `TimeOfDay` with the biome's presets.
  Cliff faces (over 52°) carry big boulders sunk into them so walls read broken, not stretched;
  bare rock starts at a per-map slope (snow maps keep snow higher). Tunnels get timber-framed,
  lantern-lit mouths with boulders where they reach the open; rivers and falls use a faster,
  choppier copy of the water material.
- **Buildings** (`ArchSpec` → `ArchBuilder`): rooms on a 0.5 m grid per floor; walls found on the
  room boundaries (two-leaf facades: plaster outside, painted plaster inside; partitions; arcades
  with columns and arches; parapets; balustrades over voids); doors with casings and open leaves,
  windows with frames, glass, 0.3 m+ stone sills and shutters; slabs with stair wells; straight
  and dogleg stairs (visible steps on a plastered soffit between stringers, smooth ramp colliders,
  ≤ 0.18 m risers) and **grand stairs** (marble treads, a carpet runner, turned balustrades and
  newels on both sides); hip, gable and shed roofs with eaves and snow; flat roofs with parapets;
  cornice ledges and a 0.3 m string course at every upper floor line (hangable); balconies on
  carved consoles with wrought-iron rails; exterior climbs (`ArchDrain`: cast-iron downpipes on
  brackets, ivy sheets, espalier trellises, all climbable); coal-chute hatches; crawlable 1 m vent
  ducts hung from the slab with grilles; secure doors; room lights; security zones per room; a
  building volume. Wall finishes follow the tier: wallpaper over a walnut dado and a crown
  moulding in guest rooms with plaster ceilings (`ArchStyles.Grand`), painted plaster for staff,
  bare concrete back of house. Walls that run on into the next one hide their end faces (no
  seams). One combined mesh per material (`ArchMesh`, world box UVs, smooth cylinders and domes),
  mesh colliders. `ArchFurnish` furnishes every room by type with the `Furn_*` models until its
  walls are lined (offices: desks, cabinets, shelves; kitchens: an island; bedrooms: beds,
  wardrobes, rugs; halls and lobbies: a fireplace, a sofa group, a reception desk; archives and
  libraries: aisles of shelving; armouries: gun racks and crates; wine cellars: racks; coal
  stores: a heap; ballrooms: dressed round tables and a hanging chandelier) and a throwable;
  `ArchKit` places the systemic props (drinks, toilets, wardrobes and dumpsters to hide in,
  distractions, explosives, cameras, recorder, breaker, air handler, drop ladders, chandeliers,
  ziplines, chatter, POIs); `ArchOutdoor` builds walls with gates, stairs, retaining walls, sheds,
  market stalls with baskets, sacks and crates, piers, towers, masts, tanks, concert stages (box
  truss, PA stacks, spots), box trucks, guard booths with barrier arms, rope queue lines, painted
  signs, bins, patio heaters, planters, hedges, and cover clusters that break long sightlines.
- **Validation** (`Goobyr/Validate Maps`): 6+ edge starts 50 m clear of NPCs and 3+ inside starts,
  3+ extractions, 30+ weapons, 80+ throwables, 100+ NPCs all on the NavMesh and none more than
  25 m from another, 5+ entries into the key building (3+ into the next two), vents, climbs,
  cameras and a recorder, 6+ tall grass fields, no floating prop, every target's routine
  available in its zone, the scene under 90 MB. `MapShots` renders review screenshots, including
  per-building sets (every climb, balcony and the largest rooms per floor) and, in play mode, the
  busiest groups of NPCs.

## Systems
- **AI** (`AI/Npc`, rules in `AI/DetectionModel`): sight is a 90° cone out to 20 m (lookouts
  30 m) with an edge-of-vision band out to 75° that fills three times slower, weighed by distance,
  light (power cut), stance, grass, smoke and walls; anyone closer than 2.2 m is felt. Three stages:
  **suspicious** (the gauge fills, the NPC stops and stares, walks over past half, forgets when it
  drains), **engaged** (gauge full: weapon up, it walks over and shouts for 2 s - break line of sight
  for 1.2 s and it searches instead; aim at it and it fires at once), **combat** (it shoots). A fight
  stays contained: another NPC joins only if it sees the player, sees the confrontation or the fight
  within 18 m, or gets the phone call. **Hearing** (`DetectionModel.Loudness`): every sound has a
  reach in the open (silenced 9 m, pistol 45 m, explosion 90 m...), each wall or floor in between
  cuts it to 40 %, and only the 1-3 NPCs who hear it best go and look (`Responders`) - at a walk for
  a distraction, weapon up for gunfire - while the others only turn their heads. A sound never gives
  a position: they head for a guess a few metres off. Footsteps follow the player's real speed and
  stance (silent standing still, 1.5 m crouched, 5 m walking, 14 m sprinting, 8 m sliding) and are
  muffled by walls too. A searching or alarmed NPC fills its gauge twice as fast and opens fire on
  sight (no confrontation); a searcher that is shot fights back. Bodies: the first to find one radios
  it in, its two nearest mates come to secure the area and the zone sees further for 90 s; later
  finders are only wary. Guards and staff take breaks at activities near their post (smoke, phone,
  drink, a look around), the windows a player waits for. Zone squads
  (`LevelZone`) share the *last known position* among the NPCs that joined, hand out suppress /
  flank / push roles over scored cover points, then search around that position. NPCs never track a
  player they cannot see - the hunted player sees their own "shadow" at the believed position.
  Fights warm up (wild first shots). Once someone in a zone fights, one fighter phones for help
  (icon + ring): when the call ends, the six nearest mates within 60 m of the caller search the last
  known position and reinforcements arrive. Kill the caller first. Patrols walk slowly (1.15 m/s) and come
  in groups of up to five on one route: the first still on its round leads, the others follow in a
  column of twos. Crowds (unarmed `Worker` NPCs around a scene) scream (guards close by come and
  look) and run. The alarm flare (rarely fired, by a guard who runs to it once a fight starts) puts every zone on
  edge and sends its four nearest guards to the flare; the rest hold their posts. Patrol
  cars spot players with their headlights, brake and drop two guards.
- **Systemic props** (`Systems/`): fire spreads between flammables and is put out by sprinkler
  zones; breakers cut a building's lights (and an NPC comes to fix it); chandeliers drop when
  their chain is shot; poisonable drinks (lethal, or emetic → the drinker runs to a toilet,
  alone); the basement air handler gasses the whole building; distractions (radio, TV, phone,
  generator, car alarm); hiding spots; ziplines; jump pads; climbable surfaces; tall grass.
- **Player** (eyes at 1.74 m, 0.98 m crouched): walk, sprint (held), crouch (held; crouching while moving slides,
  much further out of a sprint and faster downhill), crawl (crouched where a crouch doesn't fit:
  vents, crawlspaces), jump, zipline, hide, swim, and **parkour** (`FpsMotor`): jump in front of
  anything 0.45-2.4 m high to mantle onto it - the ledge is probed at six heights and four depths past
  the face, so 30 cm walls and window sills count; thin obstacles with floor beyond (fences, garden
  walls, open windows) are vaulted over or through; in the air, the hands grab any ledge they reach
  and hang: shimmy with A/D, climb up with W or jump, drop with crouch or S, leap to the next ledge
  with jump + A/D, kick off with jump + S. Drainpipes, ladders and ivy are climbed and topped out
  onto the roof. Stairs keep you glued to the steps and step-ups ease the view; landings from a real
  drop are heard (3 m per metre fallen, half crouched); remote players get the motion state
  (`PlayerAvatar.Motion`) for their animation. Swimming:
  (`Systems/WaterVolume`: crouch dives, jump rises or climbs out at the surface, rivers carry you,
  20 s of breath under water; under water nobody sees you beyond arm's length, the view turns
  blue-green and the sound muffles; splashes are heard; deep water is cut out of the NavMesh); head bob, sway, tilt, landing dip, FOV kicks; 6-slot inventory;
  guns, rocket launcher, flamethrower, coin / brick / smoke / C4 + remote (T), poisons, punches and
  silent takedowns from behind (`DetectionModel.CanTakedown`: within 70° of its back, the NPC not
  alerted, its gauge under half - a 2.5 m sound instead of a fall). Rocks, bottles, bricks and
  wrenches lie around every map: picked up with F, thrown with an arc preview, they knock out whoever
  they hit and pull the nearest guard or two to where they land.
- **Instinct** (`FX/InstinctVision`, `FX/Rendering/InstinctFeature`, hold Tab): the world dims
  to a cold grey with a scan wave rolling out from the eye, and what matters glows through the
  walls: the target and the proof (or whoever carries it) in red at any range, teammates in blue,
  NPCs within 55 m in white (amber when they are hunting you), everything usable or pickable within
  25 m in white. Purely local and visual. Hidden parts are a solid silhouette, visible parts a rim
  of light; a stencil bit gives each pixel to the first colour drawn, so red wins.
- **HUD** (`UI/Hud`, `UI/Minimap`, `UI/Compass`): minimal, nothing by default.
  - A heading strip across the top (cardinal letters, ticks, heading) with the target's marker and
    distance always on it, plus extractions, teammates and the dropped proof.
  - A round minimap bottom left: the map's baked stylized plan of the floor you stand on, north up
    and still, your arrow turning; the target in red (pinned to the rim when far), teammates, NPCs
    noticing or hunting you, revealed extractions; its rim takes the threat colour.
  - Weapon and ammo fade in on a change or while aiming; health only when it changes or runs low.
  - The inventory is a list bottom right, shown on a switch (wheel, 1-6); the weapon wheel around
    the crosshair only while Q / LB is held (the look picks, release selects).
  - No objective panel: the contract is in the in-match menu.
- **Map plans** (`Editor/Maps/MapPlanBaker`, read by `UI/MapPlan`): every map build bakes one flat
  plan per floor (ground and three storeys) from the scene - ground dark, tree crowns darker,
  water blue-grey, paths grey, streets light, building floors mid grey with their walls as lines,
  the outdoors faded on upper floors.
- **In-match menu** (`UI/PauseMenu`, Esc, or M straight to the map): MAP (the whole plan, floor tabs,
  zoom and pan, zones, target, you, teammates, proof, extractions), CONTRACT (portrait, name, one
  line, proof, status), OPTIONS; resume and leave. The game keeps running.
- **Sound** (`Editor/Audio/SfxBuilder` + `SfxSynth`, all synthesised): guns in layers (crack,
  body, thump, action, outdoor slap and rumble), footsteps per surface for players and NPCs
  (`Audio/Footsteps`: terrain layer or the piece underfoot - grass, dirt, stone, sand, wood, metal,
  water), swim strokes and splashes, an ambience bed per map (`Audio/Ambience`: forest morning,
  desert wind and surf, suburban night; underwater bed when diving), a tension drone that follows
  your threat status with a stinger when spotted and a hit when it turns to combat. Sounds fade
  logarithmically with distance.
- **Juice**: trauma screen shake, hit flash, chromatic aberration / bloom / exposure pulses,
  hurt vignette, squash and stretch, fluff puffs on hits, explosions with fireball, shockwave,
  debris and light flash, smoke clouds, hitmarkers, damage arcs, kill feed, banners.
- **Online**: played between friends. *Create a room* opens a Steam room (found only by its
  six-letter code; LAN when Steam is not running), *Join a room* takes the code (or a direct IP), and
  Steam invites work too (Facepunch transport, App ID 480 for now); LAN/IP and solo through Unity Transport. The agent name is the typed nickname, else the
  Steam persona. Proximity voice chat on Steam Voice, relayed by
  the host to players within 30 m (host toggle in the lobby; push-to-talk B or open mic).

## Casters (tournament spectators)
A player can join a room as a **caster**: no avatar, no team, never counted by the rules, the
voice never recorded (it needs a living avatar). Up to 4 casters on top of the 8 agents; casters
(not agents) may also join a contract already running (`NetSession` approval reads a one-byte
connection payload, `PlayerIdentity.JoinAsCaster`). Join screen switch "Join as a caster", or the
briefing switch "Watch as a caster" (the host can cast its own room; at least one agent must play).

The caster view (`Player/CasterView`, overlays in `UI/CasterHud`, pure rules in
`Player/CasterLayoutLogic`, tested):
- up to **4 live feeds at once**, each its own camera; layouts single, duo (two 16:9 side by side),
  2x2, and main + 3 small ones with the agent cards in the band below;
- a feed shows an agent from behind the shoulder (orbit with right-drag or the right stick, zoom
  with the wheel) or through their eyes (their head hidden for that camera only), flies freely
  (WASD/ZQSD, E/Q or triggers up and down, Shift fast), or shows the tactical map (agents, NPCs
  coloured by state, target, proof, extractions);
- **focus**: the selected feed full screen (F, double-click, D-pad down), or it and the main feed
  side by side (Shift+F); again to go back;
- the **8 agent cards** (number, team, health, state, kills, which feeds show them): 1-8 or a click
  puts the agent on the selected feed; Enter promotes the selected feed to main;
- the **auto-director** (P) keeps the agent worth watching on the main feed (proof carrier, fights,
  spotted, hit, close to the target), holds a pick 5 s and switches only for clearly better; a
  manual change of the main feed pauses it 15 s. A feed whose agent dies stays 3 s, then takes the
  next agent nobody watches;
- names over every agent and the target in each feed, a plate per feed (team, health, weapon,
  hidden / in the grass / underwater / spotted / combat / proof), the top bar (map, mode, clock,
  contract), H hides everything for a clean stream. The audio follows the selected feed.

## Characters and animation
- **Bodies** (`FX/HumanBody`): ten skinned humans sharing one 53-bone skeleton (`Art/Characters/Chr_*`,
  LOD0/LOD1), spawned from the `ArtRegistry`. Who wears what (`FX/HumanCast`, a pure function of
  networked data): agents in the black suit (man or woman by seat) with a team-coloured armband;
  guards dressed for the map's climate (jungle, desert, snow from the map's names); bodyguards in
  black; the crowd as staff (where the staff pass is needed) or guests; targets as VIPs, or as their
  profile's `character`.
- **Pipeline** (`Goobyr/Build/Characters`, `Editor/Pipeline/CharacterBuilder`): URP Lit materials per
  slot (skin, glossy eyes, alpha-clipped hair/brows/lashes, emission kept for the hit flash);
  Humanoid avatars built on a real T-pose (the A-pose arms are raised to the horizontal before the
  avatar is made) for every character, and for the clips from the reference skeleton (the clips copy
  `Idle`'s avatar); clip settings from `clips.json` (rotation always baked, in-place clips bake
  everything, travelling clips let their travel go: the motor moves the body); the upper-body and
  arms masks; the `Characters/Human` and `Characters/Arms` controllers; the grips (`FX/HumanRig`,
  from the hand's own geometry: knuckle line along the raked handle, wrist-to-knuckles along the
  barrel, palm against it; support hand cupping a pistol or palm-up under a fore-end). Hand
  rotations are written on the bones after the IK pass (the grips are raw-bone, not IK-goal space);
  aiming, a third-person gun turns about its grip onto the aim line.
- **Animation** (`FX/HumanAnimator`): states are chosen in code and reached by cross-fades (no
  transition graph to get stuck in). Velocity comes from the motor / NavMeshAgent or, for remote
  bodies, from the interpolated transform over 0.15 s - never a frame delta. Locomotion is a
  gait-space blend (walk ring 1, run 2, sprint 3, capped by intent) and the pace parameter plays the
  clips at speed / their measured stance-foot speed (scaled by each body's human scale), so the feet
  stay planted. The air state is debounced and checked against the ground below (stairs never flash
  a fall); jump → fall → land. Upper body: weapon hold / aim / fire / reload / punch / throw / phone;
  additive hit flinches; foot IK on slopes and stairs, look IK on the aim pitch (spine too when
  aiming), left hand on long guns. Remote players read `PlayerAvatar.Motion`, crouch, sprint, aim,
  pitch; NPCs read their state, `Npc.Pose` (phone, smoke, looking around, cowering) and weapon.
- **Death** (`FX/HumanRagdoll`): the model leaves the character and its bones become a ragdoll built
  from the pose it died in (realistic mass split, hinge limits from the current bend), keeping its
  speed and taking the blow at the hit point; capped at 45. A takedown victim struggles in the choke
  (`Takedown_Victim`) before going limp.
- **First person** (`Weapons/Viewmodel`): Chr_Arms (sleeves and hands) under the camera, arms-only
  mask; the gun is posed procedurally (hip, sights on the screen centre, sway, bob, recoil, sprint,
  switch) and the hands are put on it by IK with the measured grips; reload, punch and throw clips
  lead the hands during the action; small items ride in the hand; fists hold a guard (the punch
  clip's first frame). The overlay camera renders with post-processing (same exposure and
  tonemapping as the world) and the arms get their own slightly darker skin instances and white shirt
  cuffs; shoulders sit low and back so the forearms rise from the bottom of the screen.
- The contract portrait (`UI/PortraitStudio`), the title key art and the app icon
  (`Goobyr/Build/Icon`, a camera render) use the same bodies.

## Architecture invariants
- Host-authoritative: AI, damage, match state, inventory, pickups, proof, fire, systems. Player
  movement is owner-authoritative (NetworkTransform owner mode); shots are predicted by the owner
  and re-traced by the host with the same spread seed.
- Interactables are scene objects numbered identically on every machine (`WorldRegistry`, sorted by
  position) and routed through `MapSession`; only dynamic things are NetworkObjects.
- Patrol cars move as a function of the match clock, so they need no transform sync.
- One MonoBehaviour per file named after it (scene/prefab serialization).
- Never `AddComponent` a component that another `RequireComponent` already brings.
- Adding a TextMeshPro turns the Transform into a RectTransform: re-read `transform` after.
- All content is generated: change the builders in `Assets/_Game/Editor/`, never the outputs.
- Gameplay code lives in Assembly-CSharp (no asmdefs, as in Hangtime) so editor tools and tests
  in `Assets/_Game/Editor` see it.

## Controls (physical keys, AZERTY-safe)
Move WASD/ZQSD · look mouse · **LMB punches** (from behind an NPC that hasn't noticed you: a silent
takedown, dead at once) · **RMB raises the weapon** and LMB then fires · with a throwable, RMB shows the
arc and LMB throws · wheel cycles what you carry (fists included, empty slots skipped) · reload R ·
interact / pick up F · crouch Ctrl/C · jump Space · sprint Shift (hold) · crouch-slide Ctrl/C while
moving · punch also V/MMB · throw G · detonate T · slots 1-6 · map M ·
instinct Tab (hold) · scoreboard ` (key left of 1, ² on AZERTY) · push-to-talk B · pause Esc.

Caster view: agents 1-8 · next agent arrows / LB RB · feed Tab / D-pad left right · layouts F1-F4 /
D-pad up · focus F (Shift+F: with the main feed) / D-pad down · to main Enter / A · eyes-chase V / X ·
free camera C / Y · tactical map T / B · auto-director P / R3 · hide overlays H · reset camera R.

## Interface
Restrained and typographic (Barlow), dark panels, thin rules, one accent: signal amber (red only for
the target and danger). Settings and lobby options are layout-driven rows (`UI/UIWidgets`): label on
the left, control on the right (switch, slider with its value, stepper, button), so nothing
overlaps. The briefing shows the map card (its plan), the mode as explicit cards (free-for-all
1v1v1v1, teams, co-op), the agents in aligned columns, the room code with a copy button.

## Tests and tools
- `Goobyr → GD Tool` (Ctrl+Shift+G) for tuning, `Goobyr → Validate Maps`, EditMode tests in
  `Assets/_Game/Editor/Tests`.
- Command line: `-solo <map> <mode>`, `-host`, `-join <ip>`.
