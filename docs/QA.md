# QA checklist

One line per intention, rebuilt from `GAME.md`, the changelog and the playtest feedback. Run it in PIE and in the Dev
build (`Builds/Windows Dev/Hitmen.exe -log`) before a release. `[x]` = checked for the release named at the end of the
line (how: PIE script, screenshot, or by hand); `[ ]` = still to check by hand.

PIE helpers: `Tools/Editor/ue_pie.sh <map>`, `Tools/Editor/ue_ui_shot.sh <name> [python]` (screenshot with widgets),
`Tools/Editor/hitmen_pie_loadout.py` (give weapons / abilities), `Tools/Editor/ue_ui_call.py` (call a widget function).

## Stability
- [x] GC stress: the Dev build with `-ExecCmds="gc.CollectGarbageEveryFrame 1"` — 100 s in the Gym (minimap) and
  400 s on the title screen, no crash (0.4.1); [ ] Create Room map tiles and the pause map by hand
- [ ] Crash reports, if any: `python Tools/Debug/hitmen_crash_report.py "Builds/Windows Dev"`
- [ ] No crash: 10 min of menu stress with Steam running (create / leave / join / refresh spam / map cycling)
- [ ] No crash: 10 min in the Gym and LDTest, dying and respawning with proximity chat on
- [x] Steam searches never overlap (one queue: public rooms, friends list, friends' rooms) — code review

## Front end
- [x] No version line on the title; hover is an instant white block, no red side bar (screenshot, 0.5.0)
- [x] Arrows are plain white triangles (screenshot, 0.5.0); [ ] fast clicks all count
- [x] UI red is pure red, not pink (screenshot, 0.5.0)
- [ ] Changing team plays a soft tick
- [x] Your agent: arrows further out, fixed while the agent idles, codename and Randomize centred (screenshot, 0.5.0)
- [x] Loadout: big tiles, full names on up to two lines, red band on the pick (screenshot, 0.5.0)
- [ ] Icons: swapping a texture in `/Game/Hitmen/UI/Icons/...` changes the tile and the HUD card
- [ ] Ready: the match starts a second after everyone is ready, no 5-4-3-2-1; the drop shows no 3-2-1
- [x] Pause: PAUSE, three big blocks, legend on the right with the real colours, debug tools behind a button (screenshot, 0.5.0)
- [ ] Space and Escape go back on every page; Space never re-presses the last clicked button
- [ ] Create Room: watcher eye shows only when hovering a player without team; a click toggles it

## Match start
- [x] Spawn points are out of guard sight (LDTest: the 8 closest removed; Gym start 4.5 km+ away) — script
- [ ] Parachute steers with the move keys; nobody sees you under the canopy or for 2 s after landing
- [ ] No brightness drift on arrival (fixed exposure on every match map)

## Player
- [x] Every gun points along the view, held bottom-right (screenshots of 6 weapons, 0.5.0)
- [x] Sprinting with a pistol: lowered and tilted, still in view (screenshot, 0.5.0); [ ] aiming stays low right
- [x] Takedown from the side of an unaware guard (PIE script, 0.5.0); [ ] from the front in the dark; [ ] smooth blend
- [x] Thrown weapon is a floating pickup at once, no physics (PIE script, 0.5.0)
- [ ] Short slide when crouching while walking; long slide from a sprint
- [ ] Space right after grabbing a ladder does not jump off
- [ ] Landing sounds (soft / hard) and footsteps per surface
- [ ] Health comes back after 6 s without being hit; no health bar
- [ ] Mouse sensitivity default 0.25 feels right

## World
- [x] Doors open away from you from both sides and fill their opening (PIE script, 0.5.0)
- [x] Vent hatch swings on a side hinge like a small door, away from you (PIE screenshot, 0.6.0)
- [x] Window frame inside the opening (screenshot, 0.5.0); [ ] no flicker in motion
- [x] Turret panel: hold F 3 s, bar fills, turret hacked only at the end (PIE script, 0.5.0); [ ] the tower itself has no prompt
- [x] Security feed shows the room, wide (screenshot, 0.5.0); [ ] "no signal" after disabling
- [x] Camera floor ring on the light's edge, fills and turns red when it sees you (PIE screenshots, 0.6.0);
  [ ] yellow while the meter fills, seen from a client
- [ ] Locker: you step in and out (no teleport)
- [ ] Radio: plays 8 s with a pulsing light; guards come
- [ ] Barrels: toon explosion, sound, shake, chain reaction
- [x] Mounted gun barrel points forward (PIE script, 0.5.0); [ ] turns at the player's speed, shots felt
- [ ] Tank: glowing straight shell, big blast, barrel follows the aim
- [x] Gym: every pickup clear of geometry and reachable (builder check + `hitmen_check_reach.py`, 0.6.0); no slide lane
- [x] Gym vent: from the AI pen, F opens the hatch, crouch through the duct into the sealed room (PIE script, 0.6.0)
- [ ] Gym parkour run 1 to 10 in one go by hand (zipline from the gutter tower, plank to the overlook)
- [x] San Lazaro: every door walkable, every loot / NPC / retrieval point reachable on foot (`hitmen_check_reach.py`,
  0.6.0); [ ] walk the village, the estate, the maze, the caves and the tunnel by hand

## AI
- [x] Attack chain: an armed player in view gets shot, damage arrives slowly (~9 HP/s), guard holds the aim pose (PIE script, 0.5.0)
- [x] Attack chain on San Lazaro: shot after 6-9 s in a guard's view, ~9 HP/s (PIE script, 0.6.0)
- [ ] A gunshot 15 m away behind a wall is not heard; a bullet impact draws the nearest guard to the impact
- [ ] A guard shot from out of its sight turns toward the shot and searches, it does not know where you are
- [ ] In the dark, a guard that hears you within 2.5 m attacks

## HUD
- [x] Inventory: four equal cards with icons, ammo x / max, no key letters, jetpack gauge (screenshot, 0.5.0)
- [x] Awareness arcs toward guards noticing you; minimap Under fire banner (screenshot, 0.5.0)
- [ ] Headshot hit ticks are red
- [ ] Minimap: no own view cone, north-up, your arrow turns
- [x] Map markers: cameras (diamonds) and the retrieval point (yellow, pinned to the edge) on the minimap and the pause map;
  legend rows come from the marker kinds on the map (screenshots, 0.6.0); [ ] a new DA_MapMarker kind shows up by hand
