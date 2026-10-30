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

Every player starts together at one of the map's start points (drawn per match) with the starting
loadout (GD Tool; hush pistol + coins by default). Death = eliminated, then spectate (teammates,
or anyone in FFA). The match ends on an extraction, when every player is eliminated, or at the
time limit (draw). The proof carrier is revealed to everyone every 10 s.

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

## Systems
- **AI** (`AI/Npc`): vision cone weighed by distance, light (power cut), stance, grass, smoke and
  walls, filling an awareness gauge ("?" then "!"); hearing (shots, explosions, distractions,
  footsteps of running players). Bodies found raise the alarm. Zone squads (`LevelZone`) share
  the *last known position* of each player, hand out suppress / flank / push roles over scored
  cover points, then search around that position. NPCs never track a player they cannot see —
  the hunted player sees their own "shadow" at the believed position. Fights warm up (wild
  first shots). One NPC per zone radios for reinforcements (icon + countdown): kill it before it
  finishes. The alarm flare alerts the whole map. Patrol cars spot players with their headlights,
  brake and drop two guards.
- **Systemic props** (`Systems/`): fire spreads between flammables and is put out by sprinkler
  zones; breakers cut a building's lights (and an NPC comes to fix it); chandeliers drop when
  their chain is shot; poisonable drinks (lethal, or emetic → the drinker runs to a toilet,
  alone); the basement air handler gasses the whole building; distractions (radio, TV, phone,
  generator, car alarm); hiding spots; ziplines; jump pads; climbable surfaces; tall grass.
- **Player**: walk, sprint, crouch (toggle), jump, Q/E lateral cover slides, sprint-crouch slide,
  mantle, climb, zipline, hide; head bob, sway, tilt, landing dip, FOV kicks; 6-slot inventory;
  guns, rocket launcher, flamethrower, coin / brick / smoke / C4 + remote (T), poisons, punches and
  takedowns from behind.
- **Juice**: trauma screen shake, hit flash, chromatic aberration / bloom / exposure pulses,
  hurt vignette, squash and stretch, fluff puffs on hits, explosions with fireball, shockwave,
  debris and light flash, smoke clouds, hitmarkers, damage arcs, kill feed, banners.
- **Online**: Steam (Facepunch transport, App ID 480 for now) with room codes, friends rooms and
  invites; LAN/IP and solo through Unity Transport. Proximity voice chat on Steam Voice, relayed by
  the host to players within 30 m (host toggle in the lobby; push-to-talk B or open mic).

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
sprint Shift · slide Q/E · punch V/MMB · throw G · detonate T · slots 1-6 / wheel · map M ·
scoreboard Tab · push-to-talk B · pause Esc.

## Tests and tools
- `Goobyr → GD Tool` (Ctrl+Shift+G) for tuning, `Goobyr → Validate Maps`, EditMode tests in
  `Assets/_Game/Editor/Tests`.
- Command line: `-solo <map> <mode>`, `-host`, `-join <ip>`.
