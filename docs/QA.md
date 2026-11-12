# QA checklist

One line per intention, rebuilt from `GAME.md`, the changelog and the playtest feedback. Run it in PIE and in the Dev
build (`Builds/Windows Dev/Hitmen.exe -log`) before a release. `[x]` = checked for the release named at the end of the
line (how: PIE script, screenshot, or by hand); `[ ]` = still to check by hand.

PIE helpers: `Tools/Editor/ue_pie.sh <map>`, `Tools/Editor/ue_ui_shot.sh <name> [python]` (screenshot with widgets),
`Tools/Editor/hitmen_pie_loadout.py` (give weapons / abilities), `Tools/Editor/ue_ui_call.py` (call a widget function).

## Stability
- [ ] Dev build: no engine message on screen anywhere (shadow map warnings and the like), on San Lazaro included
- [ ] First launch of a packaged build picks a graphics preset (Options > Graphics shows it; delete
  `Saved/Config/Windows/GameUserSettings.ini` to try again)
- [x] GC stress: the Dev build with `-ExecCmds="gc.CollectGarbageEveryFrame 1"` — 100 s in the Gym (minimap) and
  400 s on the title screen, no crash (0.4.1); 100 s in the Gym and 90 s on San Lazaro, no crash (0.6.0, 0.6.1);
  [ ] Create Room map tiles and the pause map by hand
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
- [x] Ready with two players (PIE listen server + client joining by room code): nobody leaves while one is not
  ready, both travel ~1 s after the last Ready, no 5-4-3-2-1 (PIE script, 0.6.1); [ ] two machines over Steam
- [x] Pause: PAUSE, three big blocks, legend on the right with the real colours, debug tools behind a button (screenshot, 0.5.0)
- [ ] Space and Escape go back on every page; Space never re-presses the last clicked button
- [ ] Create Room: watcher eye shows only when hovering a player without team; a click toggles it
- [x] Create Room on one grid: map tile and players panel aligned, full player name, join hint (screenshot, 0.6.1)

## Match start
- [x] Spawn points are out of guard sight (LDTest: the 8 closest removed; Gym start 4.5 km+ away) — script
- [ ] Parachute steers with the move keys; nobody sees you under the canopy or for 2 s after landing
- [ ] No brightness drift on arrival (fixed exposure on every match map)

## Player
- [x] Pistol and rifle at the hip with both hands, low right (PIE screenshots, 0.7.0); [ ] aim: toward the centre, hands
  in view, settles with a small overshoot
- [ ] Head bob on, a footstep on each dip; Options head bob 0 removes it
- [ ] Switching weapons: the new one rises from below in ~0.2 s
- [ ] Every gun kicks per shot (shotgun / sniper hard, SMG light); the minigun shakes more as it spins up
- [ ] Sniper: raise, wobble, then zoom; the zoomed aim breathes
- [ ] Punch alternates fists, no torso in view; knife slashes; takedown lunges; throwing winds up
- [ ] Thrown empty pistol to a guard's head stuns it and vanishes; a loaded weapon lands as a pickup
- [ ] Facing a wall at 30 cm the gun pulls back instead of going through
- [x] A sprint slide with no input brakes and ends near 220 cm/s (PIE recording, 0.7.0); [ ] sideways input only bends it
- [ ] Holding Ctrl after a slide does not slide again; release and press slides
- [ ] Jumping at the corner of a crate grabs it; no vault onto a barrel or the hack panel
- [ ] Hang under a window: shimmy both ways, Space enters the window, Ctrl drops
- [ ] Shift tap dashes (dash equipped), Shift held sprints; Space held in the air flies (jetpack equipped)
- [ ] Double jump: the second jump is higher; blink: holding E shows the landing marker
- [ ] A headshot kills a guard and a player with any gun; guards never one-shot you
- [ ] A picked-up weapon lands in the left slot
- [ ] Minigun fires at once and speeds up; a short release keeps the speed
- [ ] Only nearby guards' steps are heard; gunshots are muffled far away
- [x] Every gun points along the view, held bottom-right (screenshots of 6 weapons, 0.5.0)
- [x] Sprinting with a pistol: lowered and tilted, still in view (screenshot, 0.5.0); [ ] aiming stays low right
- [x] Takedown from the side of an unaware guard (PIE script, 0.5.0); [ ] from the front in the dark; [ ] smooth blend
- [x] Thrown weapon is a floating pickup at once, no physics (PIE script, 0.5.0)
- [x] Hook ready: a small marker where it will grab, only in range (PIE screenshot, 0.6.0); [ ] E: a thin cable from
  the hand to the anchor while pulling, seen by others too (not triggered by script: check by hand)
- [ ] Short slide when crouching while walking; long slide from a sprint
- [ ] Space right after grabbing a ladder does not jump off
- [ ] Landing sounds (soft / hard) and footsteps per surface
- [ ] Health comes back after 6 s without being hit; no health bar
- [ ] Mouse sensitivity default 0.25 feels right

## Bodies
- [x] Picking up a body with two weapons: the held one is put down, the other moves right, hands empty (PIE script,
  0.7.0); [x] the body lies across the arms in view (PIE screenshot, 0.7.0)
- [ ] No firing while carrying; F / wheel puts it down and the weapon comes back
- [ ] Container: hide 2 bodies, the third says [Full]; the VIP says it can't be hidden; guards never find them

## Abilities
- [x] Stone rests on the ground after the throw (PIE script, 0.7.0); [ ] a guard who comes picks it up; notes on landing
- [x] Multi-kill: holding A marks 5 guards in view (PIE script, 0.7.0); [ ] red links and glow; one dies, all die
- [ ] Remote distraction on the gym radio from 25 m; lighter on a body (`[A] Lighter`, fire, gone)
- [ ] Transposition with a body; trap catches you after 1 s, mashing frees you
- [ ] Wind grenade blows you away too; sleep dart: ragdoll + Z z z, stands up after
- [ ] Ability pickup with the slot taken: swap, the old one stays

## World
- [x] Gym cheats: F1 / F8 / F6 / F5 / F7 from a PIE script (invulnerable, fly up 47 m, 9 killed, 9 respawned with
  their routes, map reloaded); panel screenshot (0.7.0); [ ] F2 / F3 / F4 by hand; [ ] F1-F8 in the Dev build
- [x] Gym zipline: tower to landing, the rider ends on the platform (PIE script, 0.7.0); reach check `problems 0`
- [ ] Gym door opens on its other edge
- [x] Camera zone: yellow, lit from above, cut by a wall (PIE screenshot, 0.7.0); [ ] shooting a camera breaks it
- [ ] Doors and lockers open on the other hinge, away from you
- [ ] Hold F on the turret panel: a ring fills around F
- [ ] Monitor after disabling: frozen dim frame with NO SIGNAL
- [ ] Distraction stays on until a guard comes and turns it off; F turns it off
- [ ] Tank: health at the top, reload ring, [F] Exit bottom right; no stuck "F Use"; keycards bottom right
- [ ] Breaker lever flips with sparks; bulbs go dark
- [x] Muzzle flash, light and glow on firing; toon impact puff (PIE screenshots, 0.7.0); [ ] flash size in first person
- [ ] Shotgun pellets and sniper shots draw tracers; the turret fires red tracers
- [ ] Flashbang: a big white flash; smoke grenade: the beige toon cloud fills quickly
- [ ] Jetpack smoke under the feet (seen by another player); zipline sparks and sound; ladder grab clicks
- [ ] Tank shot: barrel slides back, hull rocks, muzzle blast
- [ ] Hit: red arc toward the shooter, fringe, tint; low health greys the view
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
- [x] San Lazaro (0.7.0 rebuild): `hitmen_check_reach.py` problems 0 (54 doors, 82 targets, 24 starts; snipers and the
  helipad by ladder); `hitmen_check_patrols.py` problems 0 (22 patrollers walking, 3 followers); room luminance in
  PIE screenshots: village house 131 / 127 (ground / first floor), estate wing room 105, cave chamber 98 (0-255);
  minimap and thumbnail rebaked (0.7.0)
- [ ] San Lazaro by hand: climb the terraces by their stairs, hang and shimmy along a ledge band, cross both bridges
  and the walkway, enter the cave through the trench, take the tunnel to the basement, the tank out of the garage

## AI
- [x] Guards in range hold their ground, face you and shoot; nobody backs away (PIE script, gym AI pen, 0.7.0)
- [x] A step right behind an unaware guard is heard: it turns and engages (PIE script, 0.7.0)
- [ ] Guards beyond 15 m walk closer before shooting
- [x] Attack chain: an armed player in view gets shot, damage arrives slowly (~9 HP/s), guard holds the aim pose (PIE script, 0.5.0)
- [x] Attack chain on San Lazaro: shot after 6-9 s in a guard's view, ~9 HP/s (PIE script, 0.6.0)
- [x] A searching guard draws its pistol, then holds it ready each time it stops to look around (PIE log and
  screenshot, 0.6.1); [ ] the hit reaction on a shot guard; [ ] both seen from a client
- [ ] A gunshot 15 m away behind a wall is not heard; a bullet impact draws the nearest guard to the impact
- [ ] A guard shot from out of its sight turns toward the shot and searches, it does not know where you are
- [ ] In the dark, a guard that hears you within 2.5 m attacks

## HUD
- [x] Options: four tabs, rows, description, key hints; Controls by section in play order (screenshots, 0.7.0);
  [ ] Esc closes from the title, R resets the tab, a rebound key changes the HUD letter and the prompt
- [x] HUD: A / E key caps on the ability cards, divider, centred bar (screenshot, 0.7.0)
- [x] Watcher: waiting text in Your agent; alone, Start goes straight to the map in free fly (PIE with
  `net.AllowPIESeamlessTravel 1`, 0.7.0); free fly up and the bounds clamp (PIE script)
- [x] Match end: banner, everyone spectates, Enter (host) back to the lobby (PIE, 0.7.0); [ ] two players: the client
  sees "Waiting for the host"
- [x] Agent look counters "n / m" (screenshot, 0.7.0)
- [x] Sightroom: whole map tinted after the wave, far enemies sharp white dots, target red, window and gutter
  highlighted with smooth edges (PIE screenshots, 0.7.0); [ ] the wave reveals guards as it passes them
- [x] Inventory: four equal cards with icons, ammo x / max, no key letters, jetpack gauge (screenshot, 0.5.0)
- [x] Awareness arcs toward guards noticing you; minimap Under fire banner (screenshot, 0.5.0)
- [ ] Headshot hit ticks are red and a metallic hit sound plays (shooter only)
- [ ] Minimap: no own view cone, north-up, your arrow turns
- [x] Overhead alert marker: yellow / red triangle with "!" and backup arcs (screenshots, 0.6.1); [ ] white, filling with suspicion
- [x] Map markers: cameras (diamonds) and the retrieval point (yellow, pinned to the edge) on the minimap and the pause map;
  legend rows come from the marker kinds on the map (screenshots, 0.6.0); [ ] a new DA_MapMarker kind shows up by hand
