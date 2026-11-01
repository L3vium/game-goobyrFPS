# Goobyr FPS — design

Multiplayer (1-8) stealth-assassination FPS with a wobbly low-poly look and original
characters (the Goobs). **Every NPC is hostile to every player.** A target is drawn from the
map's pool of 15; kill it, take the proof from its body (its plush head or the item it carries),
carry it to one of the active extraction points. PvP is on: killing the carrier drops the proof.
There are no disguises — it is stealth (sight, sound, cover, grass, smoke, darkness), and when
it goes loud, everyone is lethal both ways.

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
| Map | Town | Lair | Mansion / building |
|---|---|---|---|
| **Pinecrest Hollow** (forest) | Millbrook: main street, sawmill, saloon, water tower + zipline, bridges over the river | The Grove (palisade, marsh with stilt walkways, tall grass) + military outpost (watchtowers, bunker, alarm flare) | Lakeside Lodge on a plinth: chandelier hall, bar, basement breaker + air handler, sprinklers, boathouse |
| **Mirage Bay** (Gulf city, 1 km²) | Old Souk on a walled plateau over its harbour: ~70 flat-roofed houses joined by roof planks and ladders, two covered market streets meeting at the Spice Crossing, Wind Clock tower (sniper nest, zipline to the customs house), Seven Lamps tea house (storytelling evening), hammam, spice warehouse, customs house, riads | Smugglers' Docks under the elevated Gulf Highway (walkable deck, maintenance stair): container lanes and vantage stacks, three gantry cranes (one ziplines onto the deal warehouse), the cargo ship with holds and bridge, the Bluewater deal, canteen and crew talk, forger's container, dockers' gym, fuel depot, storm drain under the quay (tunnel extraction), alarm flare | The Zenith: podium (lobby chandelier, lobby bar, restaurant, kitchen, spa and offices, basement parking with breaker and air handler, ramp), pool deck, 18-floor tower (Sky Lounge, penthouse, helipad), west-face scaffold and gondola, construction site and tower crane zipline, gardens, marina with yachts (boat extraction), Azure beach club with jump pads |
| **Elmridge** (suburb) | Downtown strip: diner, mall, police, cinema roof, gas station, patrol cars | Junkyard Bowl: wreck walls, bowling alley, drive-in screen, tall grass, canal culverts | Hilltop Mansion: gate, hedge maze, pool house, greenhouse, ballroom chandelier, basement vault |

Every patrol route has a pair of red explosive barrels next to a leg it walks (shoot them as the
patrol passes; one blast sets off the other). Weapons lie everywhere (~35-95 spawns per map, and
every dead guard drops his gun); one rocket launcher and one flamethrower per map, each at one of
several hidden spots drawn per match. Between the Mirage Bay zones: the corniche, the dry wadi and
its bridges, the Palm Interchange, the Sandgate apartments, the caravanserai, embassy villas, the bus
terminal, an oasis camp, a relay mast, a solar farm, a fort ruin and the desert road petrol station
(car extraction); two patrol-car loops (corniche / Gulf Road, souk ring).

## Map recipe (500 m, `Editor/Maps/HillMap`)
A relief function shaped by `LandShape` stamps (graded roads that climb, building pads cut at their
street's height, plateaus, terraces, carved rivers), painted ground layers, terrain trees (tall,
canopies above eye level) and grass, 8-12 tall grass fields to hide in on the approaches
(`MapKit.TallGrassField`: a 1.3-1.8 m grass layer and hiding triggers), stone retaining walls where pads meet the slope, and dressing helpers (`Frontage` rows,
`Staff` guards per room, `Squad` patrol groups, `Crowd`s, `Routines`). The validator wants 150+ NPC
markers, 40+ weapon spots, three spots per unique weapon, starts 50 m clear of NPCs, 6+ tall
grass fields, NPC posts on the NavMesh and no prop floating over the terrain. Water uses
`Goobyr/Water` (depth-tinted, refracting, rippling, foam on the shores).

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
  much further out of a sprint and faster downhill), jump, mantle, climb, zipline, hide, swim
  (`Systems/WaterVolume`: crouch dives, jump rises or climbs out at the surface, rivers carry you,
  20 s of breath under water; under water nobody sees you beyond arm's length, the view turns
  blue-green and the sound muffles; splashes are heard; deep water is cut out of the NavMesh); head bob, sway, tilt, landing dip, FOV kicks; 6-slot inventory;
  guns, rocket launcher, flamethrower, coin / brick / smoke / C4 + remote (T), poisons, punches and
  takedowns from behind.
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
Move WASD/ZQSD · look mouse · fire LMB · aim RMB · reload R · interact F · crouch Ctrl/C · jump Space ·
sprint Shift (hold) · crouch-slide Ctrl/C while moving · punch V/MMB · throw G · detonate T · slots 1-6 / wheel · map M ·
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
