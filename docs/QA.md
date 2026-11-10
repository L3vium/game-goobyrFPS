# QA checklist

One line per intention, rebuilt from `GAME.md`, the changelog and the playtest feedback (the original prompts
are gone). Run it in PIE and in the Dev build (`Builds/Windows Dev/Hitmen.exe -log`) before a release.
`[x]` = checked for 0.4.0 (how: PIE script, screenshot, or by hand); `[ ]` = still to check by hand.

## Stability
- [ ] No crash: 10 min of menu stress with Steam running (create / leave / join / refresh spam / map cycling)
- [ ] No crash: 10 min in the Gym and LDTest, dying and respawning with proximity chat on
- [x] Steam searches never overlap (one queue: public rooms, friends list, friends' rooms) — code review

## Front end
- [x] One fixed exposure: no brightness drift when the camera moves between hall, room and stage (screenshots)
- [x] Title: HITMEN wordmark alone; blocks top to bottom Join room (dashed), Create room, Options, Quit (screenshot)
- [ ] Space and Escape go back on every page; Space never re-presses the last clicked button
- [x] Join: Steam friends first, then Rooms with a refresh button beside the header, "No room found" inside (screenshot)
- [x] Join: room code field on the right (screenshot); [ ] joining by code between two machines
- [x] Create Room: players panel from the map to the screen edge, nested blocks (panel > team > player) (screenshot)
- [x] Create Room: ATTACK / PROTECT is a square block, remove is a cross, room code shown (screenshot)
- [ ] Create Room: watcher eye shows only when hovering a player without team; a click toggles it
- [x] Map tile: screenshot fills the tile (no side bars), minimap small top-left (screenshot)
- [x] Arrows are real arrows on square blocks (screenshot); [ ] fast clicks all count
- [x] Your agent: agent on the left, codename above, Randomize below, no slot labels (screenshot)
- [ ] Look arrows: spamming changes the look on every click with no lag
- [x] Loadout: three always-visible rows of icon tiles, None in each, names inside the tiles, no hint text (screenshot)
- [x] Default loadout: silenced pistol, no abilities (screenshot)
- [ ] Countdown: one big number in a square block when everyone is ready

## Match start
- [x] Spawn points are out of guard sight (LDTest: the 8 closest removed; Gym start 4.5 km+ away) — script
- [ ] Parachute steers with the move keys; nobody sees you under the canopy or for 2 s after landing

## Player
- [x] Health 300 — PIE script
- [ ] Mouse sensitivity default 0.25 feels right (migrated once from older settings)
- [x] Hands on the right, every gun points along the view (screenshots of 10 weapons)
- [x] Punch: the fists cross the view (slowed screenshot)
- [x] Stealth takedown: victim alive at the press, dead after the strike, attacker held — PIE script
- [x] Swapped weapon floats at once (no tumble) — code; [ ] check by hand

## Traversal
- [x] 50 cm step walked up (no hint), vault 120, climb onto a block, climb over a thin wall — PIE script
- [x] Open window: vault; closed window: nothing (shutter gap closed) — PIE script
- [ ] Hints only on the ground (jumping at a high wall shows nothing)
- [ ] Jumping at a ladder grabs it at once; Space right after leaving a ladder does not regrab it

## World
- [x] Doors and vent hatches swing on their edge — PIE script (door centre moved 50 cm around the hinge)
- [x] Windows lie in their wall (100 turned) — script; [ ] prompt does not flicker
- [ ] Security camera: a spotlight on the floor, cut by walls, no halo; never sees through a wall
- [ ] Containers: nothing visible inside while closed
- [ ] Focus glow on the rocket / grenade launcher and black hole gun
- [ ] Tall grass: dark vignette and eye-off mark while hidden

## AI
- [x] Attack chain: an armed player in view is shot within ~2 s and the fire montage plays — PIE script
- [ ] Guards notice from about 20 m, not 30 m
- [ ] Bullet impacts: dust on surfaces, blood on characters

## HUD
- [ ] Minimap north-up, your arrow turns
