# Hitmen — Game Design & Architecture

> Multiplayer stealth-assassination sandbox on Unreal Engine 5.7.
> This repository is the **code-first base**: every system is hard-coded in C++ and exposed to
> Blueprints for values and juice. Graphics, juice, balancing, level design, PCG, VFX and audio
> are produced by the team on top of it. Maps use engine basic shapes and the checker material.

Version: see [CHANGELOG.md](CHANGELOG.md). Conventions: see [Conventions.md](Conventions.md).

---

## 1. Pillars
1. **Sandbox assassination** — one VIP hidden among NPCs; many routes (doors, windows, gutters,
   vents, basements, tunnels), many tools (weapons, movement, AI manipulation).
2. **Players versus players** — every format from free-for-all to coop, with a Protect role.
3. **Readable stealth** — gradual detection (white → yellow → red), sound and sight, visible
   camera cones, alert colours on HUD and minimap. No objective markers in the world.
4. **Built for the team** — every value is editable in data, every visual is swappable, every
   gameplay event has a `Juice_On…` hook.

## 2. Match formats
A lobby holds players, teams and watchers.

| Element | Rule |
|---|---|
| Player without team | Plays alone (free-for-all) or is switched to **Watcher** |
| Team | Members never damage each other (no friendly fire) |
| Team role **Attack** | Kill the VIP and deliver the body to a retrieval point |
| Team role **Protect** | Kill every player who is not in a Protect team |
| Coop | Everyone in one Attack team |

Possible setups: 1v1v1v1, team vs team, Protect vs one or several teams, 2v1v1, coop, etc.
If any member of a team delivers the VIP, the whole team wins.

### VIP
- Up to 15 NPCs in a map are flagged as **VIP candidates**; at match start one is picked at random.
- The VIP is shown in red in the Sightroom; once dead, its highlight turns **yellow**.
- Killing the VIP lets a player pick the body up with `F`: it appears on the back and the
  carrier moves slower (`CarrySpeedMultiplier`). Carrying is a per-player toggle (drop the body).
- **Sonar**: while a body is carried, a red ping appears at the carrier position, stays fixed even
  if the carrier moves, and refreshes every N seconds; the interval shortens over time
  (curve in match rules).

### Retrieval points
- Level designers place any number of **retrieval point candidates** (helicopter, boat, van…;
  meshes are set by the team).
- At match start N are activated (match option), picked at random **with spatial spread**
  (randomized farthest-point sampling) so they are never clustered.
- The compass shows retrieval directions only while carrying the VIP.

### Spawned loot
Weapon spawn candidates and item (move / manipulation) spawn candidates work like retrieval
points: a count per difficulty, picked with spatial spread, random content from a weighted list.

## 3. Match options (lobby)
Shown under the map carousel. Every option has a default in the match rules data asset and a
`bShowInLobbyUI` flag so designers can hide it and fix its value.

- Proximity voice chat (on/off)
- Number of retrieval points
- Difficulty (drives loot counts, AI parameters, NPC damage multiplier). NPCs stroll on patrol (170 cm/s), walk briskly when investigating or searching (320) and run in combat (480).
- Sonar timing (interval start/end, ramp duration)

## 4. Flow & screens
1. **Title screen** — huge bold **HITMEN** title top-left; square, left-aligned buttons:
   **Join** (dashed outline, no fill) above **Create Room**, **Options**, **Quit**. A museum-like
   backdrop built from basic shapes with ambient sound; the assassin (engine mannequin) on the
   right with idle animations. A small **debug** button bottom-right opens the Gym.
2. **Join** — the title screen darkened; joinable rooms, then Steam friends' rooms.
3. **Create Room** — left: map carousel (arrows hide at the ends) and match options;
   right: players list (rename, icon), a dashed **+ Create team** button, drag & drop players
   into teams, Protect/Attack toggle per team, Watcher toggle for players without a team.
   Bottom-left **Back**, bottom-right **Start**.
4. **Customization** — left: the assassin's look (head: hat/moustache…, top, body, bottom,
   randomize, name — defaults to the Steam name or a random name); right: the loadout
   (one weapon, one move ability, one manipulation ability).
5. **Match** — then results.

Watchers get a spectator mode: free fly, single-player view (switch between players at will),
multi-screen grid (1, 2, 3… all players).

## 5. Controls (defaults, AZERTY, all rebindable)
| Action | Key |
|---|---|
| Move | Z Q S D |
| Look | Mouse |
| Jump / ledge grab / vault | Space |
| Crouch (held) — slide when moving, longer slide when sprinting | Left Ctrl |
| Sprint (held) | Left Shift |
| Fire / melee | LMB |
| Aim | RMB |
| Interact / stealth kill / swap / throw held item | F |
| Move ability | E |
| Manipulation ability | A |
| Switch weapon slot | Mouse wheel |
| Sightroom | Tab |
| Emote wheel (hold) | R |
| Pause (no time stop) | Esc |

Options: crouch toggle, sprint toggle, ladder direction relative to camera (off by default),
mouse sensitivity, invert Y, audio volumes, full key rebinding.

## 6. Player
- **Movement**: walk, sprint, crouch, slide, jump, ledge grab (tall walls), vault (low obstacles,
  windows), ladders and ziplines (automatic, no `F`, no snap animation, permissive look angle;
  forward = up, back = down), jump pads, lockers.
- **Inventory**: 2 weapon slots (mouse wheel). `F` with no interactable in focus throws the held
  item: if slot 1 is thrown, slot 2 moves to slot 1 and is equipped; if slot 2 is thrown, slot 1
  stays. Firing with an empty magazine and no reserve throws the weapon where you look. No weapon
  in hand = fists (melee).
- **Ammo**: walking over a weapon of the same type picks its ammo automatically and removes it;
  a different type shows **Swap** on `F`. The pistol is silenced.
- **Keycards**: acquired items shown next to the inventory, cannot be dropped. Doors may require
  a keycard ID (0, 1, 2…). NPCs can carry keycards and drop them on death.
- **Stealth kill**: `F` behind an unaware target plays a kill montage.
- **Sightroom (`Tab`)**: a scan sweeps the surroundings; enemies within a radius are shown in
  white through walls, interactables in white only when in line of sight, the target in red at
  any distance. No waypoint in normal view.
- **Emotes**: hold `R` for a radial wheel.
- **Footsteps**: different sounds per surface (grass, stone, sand, wood, metal) through physical
  materials and an anim notify.

## 7. Weapons
All weapons share ammo, magazine, fire rate, spread, damage, headshot multiplier, noise radius,
silenced flag, mesh/VFX/SFX soft references — every value is in a `DA_Weapon_*` data asset.

| Weapon | Family | Notes |
|---|---|---|
| Sword | Melee | |
| Bow | Projectile | charge to fire further |
| Pistol | Hitscan | silenced |
| SMG | Hitscan, automatic | fast, inaccurate |
| Assault rifle | Hitscan, automatic | balanced |
| Burst rifle | Hitscan, burst | 3-round burst |
| Shotgun | Hitscan, pellets | |
| Minigun | Hitscan, automatic | spin-up |
| Sniper | Hitscan | high damage, zoom |
| Rocket launcher | Projectile | explosion breaks walls |
| Grenade launcher | Projectile | bouncing, timed |
| Black hole | Projectile | pulls pawns and physics to a point |

## 8. Abilities
One **move** ability (`E`) and one **manipulation** ability (`A`) per loadout. Every ability has
its own data asset (cooldown, charges, range, duration, plus specific values). All abilities
affect NPCs **and players**.

**Move**: Blink (Dishonored-like teleport), Double jump, Jetpack, Dash, Hook (grapple),
Transposition (swap positions with an aimed NPC/player), Pill (boost damage/speed/resistance with
a distorted view), Invisibility, Armor, C4 (place, then detonate).

**Manipulation**: Stone (throw, noise), Remote distraction (activate a distraction at range),
Sleep dart (one target), Charm (target becomes an ally and attacks others; a charmed player is
driven by AI, reckless), Flashbang (stuns around), Smoke grenade (blocks sight for NPCs and
players), Lighter (vanish a body), Multi-kill (mark several targets; killing one kills all),
Trap (NPCs stuck; players freed after a delay), Wind mine (big knockback), Poison vial (pour in a
drink, or in a vent to poison the sealed rooms it serves).

## 9. World fixtures
| Fixture | Behaviour |
|---|---|
| Tall grass | Crouched inside = hidden |
| Explosive barrel (red) | Explodes when shot |
| Ladder / gutter | Automatic climb |
| Zipline | Automatic, up/down |
| Jump pad | Launches high |
| Locker | Hide inside |
| Door | Open/close; optional keycard ID |
| Window | Vaultable, open/close |
| Distraction | Interact → noise that attracts NPCs |
| Hackable distraction | Silent, interact or remote-activate |
| Turret | Shoots players; wired to a hack panel, hacked = shoots NPCs |
| Security camera | Visible cone (white → red when you are inside, also on the floor); spotting calls reinforcements |
| Control room / monitors | Each monitor shows a linked camera; the console disables only its linked cameras |
| Circuit breaker | Turns off its linked lights: NPCs cannot see (only hear), players see less |
| Vent | Crouch-size duct with small doors; poison spreads to linked rooms only if fully closed |
| Poisonable drink | Poison vial target |
| Breakable wall | Breaks on any explosion (weapon, C4, tank) |
| Mounted machine gun | Use from behind, 180° arc |
| Tank | Enter, third-person view, cannon |
| Teleporter | Fixed pair: in → out |
| Retrieval point candidate | See §2 |
| Weapon / item spawn candidate | See §2 |
| Keycard pickup | Grants keycard ID |

## 10. AI
All NPCs are hostile. Sight takes priority over hearing.

| State | Meaning | Indicator |
|---|---|---|
| Unaware | Routine (patrol, idle) | none |
| White | Saw something, not alerted yet | white |
| Yellow | Alerted, investigating | yellow |
| Red | Found you, attacks | red |
| Yellow (search) | Lost you, searches likely spots | yellow |

- Attacking an NPC, or an NPC seeing a kill nearby / hearing loud gunfire close → straight to red.
- Losing sight for a while → search state (EQS over places you could be).
- **Last known position**: a silhouette is shown where you were last seen.
- **Hearing**: running is heard far, walking only close, crouching not at all; gunshots have a
  radius (silenced: none). Radii are moderate, occluded by walls, and only the 1–2 nearest NPCs
  respond.
- **Bodies, distractions, weapons** are investigated.
- **Squad tactics**: alerted NPCs coordinate to surround the player (flank left via stairs,
  push forward…), staying close except snipers.
- **Reinforcement call**: an NPC not in combat can call (icon above its head); if not
  interrupted by damage before the timer ends, NPCs in a radius are alerted. Cameras call too.
- Behavior Tree with subtrees: `BT_NPC` → `BT_Patrol`, `BT_Investigate`, `BT_Search`,
  `BT_Combat`, `BT_CallReinforcements`, `BT_Sniper`, `BT_Charmed`.

## 11. HUD
- **Compass** (top): N/S/E/W only; optional VIP marker; retrieval directions while carrying.
- **Inventory** (bottom centre) with keycards next to it.
- **Minimap** (bottom right, square): alert states in white/yellow/red.
- **Crosshair** with a ring; hit marker; red cross on a headshot kill.
- **Interaction prompt** at screen centre (Open, Swap, Hack, Climb…).
- **Alert indicators** above NPCs and at screen centre.
- **Pause map**: your position, the VIP, retrieval points.

## 12. Maps
| Map | Purpose |
|---|---|
| `L_TitleScreen` | Menus backdrop |
| `L_Lobby` | Create room / customization after hosting |
| `L_Gym` | Debug gym: checker grid, spaced labelled zones showcasing every move, weapon, ability, fixture and AI state |
| `L_LDTest` | Basic Hitman-like sandbox: town, manor (basement foundations via side stairs), cave and tunnels, windows, gutters, keycard doors, vents, hidden control and breaker rooms, AIs, a basin of mountains and invisible walls |
| `L_Empty_01`, `L_Empty_02` | Empty maps for the team |

## 13. Architecture invariants
- **C++ for systems, Blueprints for values and juice.** Every gameplay class is C++; its `BP_`
  child sets values, meshes, VFX, sounds and implements `Juice_On…` events.
- **Components over inheritance for features.** Health, inventory, abilities, interaction,
  footsteps, carry, alert, status effects… are `UActorComponent`s added in BPs.
- **Inheritance for families.** `AHitmenCharacterBase` → player / NPC;
  `AHitmenWeaponBase` → hitscan / projectile / melee; `UHitmenAbility` → move / manipulation;
  `AHitmenInteractableBase` → every fixture.
- **Events, not polling.** Objects talk through delegates bound at BeginPlay. The UI never holds
  hard references to gameplay actors: widgets bind to `UHitmenUIEventSubsystem`, PlayerState and
  GameState delegates. Tick is only used for continuous work and disabled otherwise.
- **Spawn once, show/hide.** Weapons and attachments are created at BeginPlay and hidden.
- **Input lives in the PlayerController.** All Enhanced Input bindings are made in
  `AHitmenPlayerController`; the pawn only exposes functions, so the 3C Blueprint can be swapped.
- **Server-authoritative.** Listen server; the server owns damage, AI, match state, pickups and
  spawns. Character movement is client-predicted through `UHitmenCharacterMovementComponent`.
- **Data-driven tuning (GD tools).** `Project Settings → Hitmen` (`UHitmenGameSettings`) and
  data assets (`DA_MatchRules_*`, `DA_Weapon_*`, `DA_Ability_*`, `DA_Map_*`, `DA_AnimSet_*`).
- **Animations in one place.** `UHitmenAnimInstance` exposes state; a `DA_AnimSet_*` lists every
  sequence, blend space and montage so ABPs swap animations without code.
