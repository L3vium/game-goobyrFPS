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
- Difficulty (drives the number of retrieval points — Easy 4, Normal 3, Hard 2 —, loot counts, AI
  parameters and the NPC damage multiplier). NPCs stroll on patrol (170 cm/s), walk briskly when
  investigating or searching (320) and run in combat (480).

Designer-only values (match rules asset, never in the lobby): sonar timing (first interval, fastest
interval, speed-up time) and the per-difficulty presets.

The whole front end is **one scene** (`L_FrontEnd`): title, room and agent screens never load a
level. The camera blends between `CameraActor`s tagged `Shot_Title`, `Shot_Lobby` and
`Shot_Customization` (the match phase picks the shot; `AHitmenHUD::ShotBlendTime`). Creating a room
starts listening in place; joining players load `L_FrontEnd` once; a finished match travels back
to it, in the room. The front end has one fixed exposure (an unbound post process volume, no eye
adaptation): every room is lit for it. Space or Escape goes back on every menu page.

1. **Title screen** — the **HITMEN** wordmark alone, then square menu blocks vertically centred on
   the left, top to bottom: **Join room** (dashed outline), **Create room**, **Options**, **Quit**,
   plus a small **debug** button (Gym). The museum hall behind, the assassin idling on the right.
2. **Join** — left: **Steam friends** (playing Hitmen / online / offline; a friend in a room is a
   click to join), then **Rooms** with a refresh button beside the header (messages such as "No room
   found" sit inside the list). Right: the **room code** field and **Join**. Every room has a
   6-character code (no 0/O/1/I/L) shown on Create Room. Steam allows one search at a time, so public
   rooms, the friends list and friends' rooms are queried one after the other.
3. **Create Room** (briefing room) — left: the map tile (a screenshot of the map filling the tile, the
   whole map small in its top-left corner, the name at the bottom, arrows on square blocks) and two
   options: proximity chat and difficulty. Right, from the map to the screen edge: one players panel
   (count and room code on top) holding opaque team blocks (tinted header, square **ATTACK / PROTECT**
   toggle, a cross to remove) which hold one solid block per player; the host drags players between
   blocks. Hovering a player without team shows the watcher eye (open = watching, struck = playing).
   A dashed **+ Create team** block below. Bottom: **Leave room** and **Choose your agent**.
4. **Your agent** (stage) — the agent stands on the left; the codename above its head, white triangle arrows (every
   click counts) level with its head, torso and legs in two columns `AgentArrowSpacing` away from its axis, and
   **Randomize** under its feet — all placed from the reference pose, so they never move with the idle animation;
   the look changes on the stage at once and is sent once the clicks pause. Right: the loadout, three
   always-visible rows of square icon tiles (weapon, move `E`, manipulation `A`), each starting with
   **None**; a click picks. Default: silenced pistol, no abilities. **Ready** only: when every
   non-watcher is ready the match starts a second later (no countdown on screen; un-ready cancels it).
5. **Match** — intro (parachute drop, "Dropping in" and the map name, no countdown), play, then results.

Spawns: maps carry many `BP_TeamStart` points. Each team (or solo player) takes a random free point
and teammates take the nearest free points around it. During the intro (`bParachuteIntro`,
`IntroDropHeight`, `IntroDuration` on the match game mode) everyone falls from above their point
under a canopy, steering a little; control returns on landing. The Gym skips the intro.

Watchers get a spectator mode (`V` cycles): free fly, single-player view (LMB / RMB switch between
players), multi-screen grid (mouse wheel: 1, 2, 3… all players). Dead players follow teammates.

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
- **Movement**: walk (480), sprint (820), crouch (240, the view lowers smoothly), slide (crouch while
  sprinting), jump, ladders / gutters (automatic, forward = up, smooth pull-up at the top), ziplines (`F` at a
  post: rides to the other end on its own), jump pads, lockers (you stand inside and look through the slit).
- **Traversal metrics** (Space while moving into it; the action shows at the crosshair):

  | Obstacle | Action |
  |---|---|
  | up to 55 cm | step (walked up, no key) |
  | up to 120 cm, thin, floor behind | vault (also through an open window) |
  | up to 250 cm, top at least 40 cm deep | climb onto it (a block wall: walk along its top) |
  | up to 250 cm, thinner wall | climb over |
  | above 250 cm | out of reach (ladder, gutter, ability) |
- The hint only shows on the ground. Space counts for 0.25 s after the press (late ledge grabs); jumping at a
  ladder grabs it at once.
- **First person**: no body or arms while moving (the body's shadow stays); the arms appear with a weapon in
  hand (held on the right, barrel along the view) or while punching (arms at chest height, the fists cross the
  view). Recoil kicks the view up and it recovers smoothly. On death the view leaves the head.
- **Inventory**: 2 weapon slots (mouse wheel). `F` with no interactable in focus throws the held
  item: if slot 1 is thrown, slot 2 moves to slot 1 and is equipped; if slot 2 is thrown, slot 1
  stays. Firing with an empty magazine and no reserve throws the weapon where you look. No weapon
  in hand = fists (melee).
- **Ammo**: one ammo pool per weapon (no magazine / reload). Walking over a weapon of the same
  type picks its ammo automatically and removes it; a different type shows **Swap** on `F`. The
  pistol is silenced.
- **Keycards**: acquired items shown next to the inventory, cannot be dropped. Doors may require
  a keycard ID (0, 1, 2…). NPCs can carry keycards and drop them on death.
- **Stealth kill**: `F` behind an unaware target: both are held, the attacker steps in behind, the
  attack plays (body and first-person arms), the victim reacts and dies on the strike (1 s).
- **Health**: players 300, guards 100 (guards also hit softer, difficulty damage multiplier).
- **Intro**: the move keys steer the parachute; nobody can see a player under the canopy or for 2 s
  after landing.
- **Sightroom (`Tab`)**: a scan sweeps the surroundings; enemies within a radius are shown in
  white through walls, interactables in white only when in line of sight, the target in red at
  any distance. No waypoint in normal view. Radii live on the player controller
  (`Hitmen|Sightroom`) so a 3C swap keeps them.
- **Emotes**: hold `R` for a radial wheel.
- **Footsteps**: different sounds per surface (grass, stone, sand, wood, metal) through physical
  materials and an anim notify.

## 7. Weapons
All weapons share ammo, fire rate, spread (fixed: firing never widens it), recoil, damage, headshot
multiplier, noise radius, silenced flag, mesh / visual transform / VFX / SFX / montage / first-person pose
references — every value is in a `DA_Weapon_*` data asset. Weapons lying in the world float and turn. The
sniper shows a scope overlay; zoom keeps the mouse feel (sensitivity follows the field of view).

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
One **move** ability (`E`) and one **manipulation** ability (`A`) per loadout. C++ families:
`UHitmenAbility` → `UHitmenMoveAbility` / `UHitmenManipulationAbility` → each ability. A
`DA_Ability_*` data asset names the ability (display, icon, class); its tuning (cooldown, charges,
range, duration, specific values) is on the `BP_Ability_*` class defaults. All abilities affect
NPCs **and players**. A failed use (e.g. poisoning an unsealed room) reports the reason to the
user and does not spend a charge.

**Move**: Blink (Dishonored-like teleport), Double jump, Jetpack, Dash, Hook (grapple),
Transposition (swap positions with an aimed NPC/player), Pill (boost damage/speed/resistance with
a distorted view, `PP_Pill`), Invisibility, Armor, C4 (place, then detonate). The jetpack is
client-predicted like sprint.

**Manipulation**: Stone (throw, noise), Remote distraction (activate a distraction at range),
Sleep dart (one target), Charm (target becomes an ally and attacks others; a charmed player is
driven by the NPC behavior tree, reckless), Flashbang (stuns around, blinds players with
`PP_Blind`), Smoke grenade (blocks sight for NPCs and players; basic-shape puffs until a Niagara
system is assigned), Lighter (vanish a body), Multi-kill (mark several targets; killing one kills all),
Trap (NPCs stuck; players freed after a delay), Wind mine (big knockback), Poison vial (pour in a
drink, or in a vent to poison the sealed rooms it serves).

## 9. World fixtures
| Fixture | Behaviour |
|---|---|
| Tall grass | Crouched inside = hidden (dark soft vignette and an eye-off mark on screen) |
| Explosive barrel (red) | Explodes when shot |
| Ladder / gutter | Automatic climb |
| Zipline | Automatic, up/down |
| Jump pad | Launches high |
| Locker | Hollow cabinet; hide inside, look through the door slit |
| Door | Open/close; optional keycard ID (`[Locked - Keycard N]` in the prompt) |
| Window | Thin frame, two shutters swinging sideways; vaultable when open |
| Container | Two-door cabinet; fixed contents or a random weighted pick, items float on its shelves |
| Distraction | Interact → noise that attracts NPCs |
| Hackable distraction | Silent, interact or remote-activate |
| Turret | Red laser while locking on, then shoots players; hack with `F` (or its panel): shoots NPCs |
| Security camera | A shadowed spotlight aimed at the floor (white → red when it sees you; walls cut it); sees you only through open air; spotting calls reinforcements |
| Security station | Desk + screen cycling through its cameras; `F` disables / enables them ("no signal") |
| Circuit breaker | Turns off its linked lights: NPCs cannot see (only hear), players see less |
| Vent | Crouch-size duct with small doors; poison spreads to linked rooms only if fully closed |
| Poisonable drink | Poison vial target |
| Breakable wall | Breaks on any explosion (weapon, C4, tank) |
| Mounted machine gun | Usable from any side (option), 180° arc; takes damage, part of it reaches the gunner |
| Tank | Enter, third-person view, cannon; server-driven from any client; damageable, driver ejected when destroyed |
| Teleporter | Fixed pair: in → out |
| Retrieval point candidate | See §2 |
| Weapon / item spawn candidate | See §2 |
| Keycard pickup | Grants keycard ID |

## 10. AI
All NPCs are hostile. Sight takes priority over hearing. Sight range 1950 cm (lose at 2250), times the
difficulty multiplier; suspicion fills slower with distance (falloff 1600 cm). Guards in combat hold their
weapon's aim pose and play its fire montage on each shot.

There are no civilians: every NPC is a guard (patrol, stationary, sentry, sniper) or a VIP candidate.

**Two NPC types** (`Behavior` on the NPC, "Hitmen|NPC|Behavior"):
- **Patrol**: walks its `PatrolRoute` (stands at its post if it has none). Routes can be **curved**
  (spline through the points). A **guard group** (`BP_GuardGroup`) makes followers walk in formation behind
  the first member and shares alerts inside the group.
- **Keycards** hang on the carrier's hip; from behind an unaware carrier, `F` pickpockets it (before any kill).
- **Stationary**: stands at its placement, facing the way it was placed; after an alert it walks
  back and turns to that direction again.

Options on any NPC:
- **React To Noises** (on by default). Off = only reacts to what it sees and to being hurt:
  footsteps, gunshots, distractions, radio calls and bodies are ignored.
- **Hold Position** (Stationary only). Never leaves its post: shoots what it sees, turns toward
  attackers, searches and investigates by looking around.
- Stationary + React To Noises off + Hold Position = a **sentry** (manor gate in LDTest, gym pen).

Whoever sees: one shared sight test (`UHitmenAISubsystem::CanObserverSee`: range, cone, walls,
smoke, darkness, invisibility) drives perception, kill witnesses and body sightings.

| State | Meaning | Indicator |
|---|---|---|
| Unaware | Routine (patrol, idle) | none |
| White | Saw something, not alerted yet | white |
| Yellow | Alerted, investigating | yellow |
| Red | Found you, attacks | red |
| Yellow (search) | Lost you, searches likely spots | yellow |

- **White → yellow**: a white NPC that loses you after its fill passed `InvestigateSuspicionThreshold`
  goes yellow and walks to where it saw you; below it, it cools down. Red when the fill completes
  while it sees you, when shot, when it witnesses a kill, hears close gunfire, or was already
  searching for you.
- Losing sight for a while → search state: C++ scoring of hiding spots (lockers, tall grass,
  actors tagged `HitmenHidingSpot`), the last known position and reachable nav points, favouring
  spots out of sight; spots are claimed so NPCs split up.
- **Last known position**: a mannequin ghost (`BP_LastKnownGhost`) is shown where you were last
  seen while at least one NPC searches for you; never on your corpse.
- **Hearing**: running is heard far, walking only close, crouching not at all; gunshots have a
  radius (silenced: none). Radii are moderate, occluded by walls, and only the
  `MaxNoiseResponders` (2) nearest NPCs that would react respond, for every noise type.
- **Bodies, distractions, weapons** are investigated. Only sentries ignore bodies.
- **Squad tactics**: alerted NPCs take slots around the player; when the fight spans floors one
  flanker takes another floor / the stairs while another pushes directly; snipers keep far, high
  slots.
- **Reinforcement call**: an alerted NPC that is not shooting at a visible target calls (icon
  above its head); if not interrupted by damage before the timer ends, NPCs in a radius are
  alerted. NPCs alerted by a call do not relay it (`bReinforcementCallsRelay`). Cameras call too.
- Behavior Tree with subtrees: `BT_NPC` → `BT_CallReinforcements` (top, aborts on sight),
  `BT_Combat` (→ `BT_Sniper`, hold-position branch), `BT_Search`, `BT_Investigate`, `BT_Guard`
  (stationary), `BT_Patrol`. "Target visible" decorators abort lower branches immediately.
  Charmed pawns (players included) reuse the search/combat branches. Trees are generated by
  `Tools/Editor/hitmen_build_ai.py` (headless, see CLAUDE.md).

## 11. HUD
Font: Barlow Condensed (`FNT_Barlow`). Painted in code (`HitmenHUDElements`, `HitmenHUDWidgets`).
- **Compass** (top): N/S/E/W only (`bShowIntercardinals` off); optional VIP marker; retrieval
  directions while carrying.
- **Inventory bar** (bottom centre): four cards of one size, in fixed places — the two weapon slots (the drawn one lit;
  switching snaps, nothing slides), a divider, the manipulation (A) and move (E) abilities. Each card shows the item's
  icon and full name (the font shrinks to fit), weapons their ammo as `current / max`, abilities a cooldown shade that
  shrinks as they come back, charge pips and a fuel gauge for the jetpack. No key letters, no "ready", no health bar:
  keycard chips sit above.
- **Minimap** (bottom left, square): the baked map, north up (your arrow turns, no view cone), alerted NPCs, sonar pings;
  its frame takes the colour of the worst alert aimed at you and a banner opens across its top with a white flash
  (Suspicious, Alerted, Searching, Spotted; **Under fire** is taller, red and pulses).
- **Crosshair**: a thin "+"; hit ticks (red, longer and thicker on a headshot); an "x" on a kill (red on a headshot);
  Hitman-like awareness arcs around it, one per guard or camera noticing you, placed toward it: the arc fills from its
  middle as suspicion grows (white), then turns yellow or red; cameras add a small camera mark.
- **Prompts** under the crosshair: `[F] Open  [Locked - Keycard 1]`, `[SPACE] Vault`; remote-ability targets
  glow with `[A] Charm` under them.
- **Damage**: red edges on each hit, a breathing red glow and a heartbeat below 35 % health.
- **Scope** overlay for the sniper. **Sightroom** (Tab): a scan wave, a grid on the floor, a cool world.
- **Alert indicators** above NPCs and at screen centre.
- **Pause menu**: **PAUSE** and three big square blocks on the left (Resume, Options, Leave match); the baked
  schematic in the middle (north up, dimmed under the markers, wheel to zoom around you); the legend on the right
  toggles each marker type and shows its exact colours (you, teammates, VIP, retrieval points, guards on alert in
  white / yellow / red like the minimap, cameras). Debug tools (dev builds) open over the legend from a small
  "Debug tools" button.
- **Intro**: "Dropping in" and the map name during the parachute drop (no countdown, no sting).

## 12. Maps
| Map | Purpose |
|---|---|
| `L_FrontEnd` | The whole front end in one scene: museum hall (title), briefing room (create room), agent stage (`BP_AgentPreview`, "Your agent"); one tagged camera per screen |
| `L_Gym` | Debug gym: a compact walled hall (74 x 74 m) laid out like a mini LD — movement course with the traversal metrics, armoury (weapons, containers, loot points), abilities, fixtures, and an AI pen (spline patrol, guard group, keycard guard to pickpocket, sentry, sniper, VIP, search spots, camera alarm); every station is labelled just above itself |
| `L_LDTest` | Basic Hitman-like sandbox: town (gutters beside back windows, street lamps on a breaker in a hidden keycard utility room), walled manor (keycard gates, sealed dining room fed by a crouch vent duct, upstairs keycard control room, basement foundations via exterior side stairs with a keycard breaker room), cave with a ramp down to a tunnel into the basement, a garden-shed secret passage down to the tunnel, AIs, a basin of mountains and invisible walls; checker materials only |
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
  hard references to gameplay actors: the player controller binds to its pawn's components and
  pushes view models (inventory, abilities, vitals, carry, alert blips, emotes, spectator views)
  to `UHitmenUIEventSubsystem`; widgets bind to it, PlayerState and GameState only. NPC overhead
  indicators are pushed by the NPC. Tick is only used for continuous work and disabled otherwise.
- **Spawn once, show/hide.** Weapons and attachments are created at BeginPlay and hidden.
- **Input lives in the PlayerController.** All Enhanced Input bindings are made in
  `AHitmenPlayerController`; the pawn only exposes functions, so the 3C Blueprint can be swapped.
- **Server-authoritative.** Listen server; the server owns damage, AI, match state, pickups and
  spawns. Character movement is client-predicted through `UHitmenCharacterMovementComponent`.
- **Data-driven tuning (GD tools).** `Project Settings → Hitmen` (`UHitmenGameSettings`) and
  data assets (`DA_MatchRules_*`, `DA_Weapon_*`, `DA_Ability_*`, `DA_Map_*`, `DA_AnimSet_*`).
- **Framework Blueprints.** `BP_GameInstance` (Options volume mix), `BP_GameState`,
  `BP_PlayerState`, `BP_SpectatorPawn`, `BP_PlayerController` and the `BP_GM_*` game modes.
- **Animations in one place.** `UHitmenAnimInstance` exposes state; a `DA_AnimSet_*` lists every
  sequence, blend space and montage so ABPs swap animations without code.
