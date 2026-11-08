# Changelog

All notable changes to Hitmen. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions before 0.1.0 belong to the Unity prototype (see git history before the Unreal port).

## [Unreleased]

### Added
- First-person arms (`ABP_FirstPersonArms`, `UHitmenFirstPersonAnimInstance`): shown only with a weapon in hand
  or while punching / acting, holding the weapon's pose, with sway, walk bob and hip / aim offsets. The body is
  hidden from its owner but still casts its shadow.
- Recoil spring: the view kicks up and comes back down smoothly; explosion view shake.
- Death camera: the view leaves the head and orbits the body.
- Traversal metrics: vault up to 120 cm, ledge grab up to 250 cm, climb over thin walls; cubic eased paths with a
  view dip; `[SPACE] Vault / Climb / Climb over` hint at the crosshair; windows (openings in walls) are vaultable.
- Ladders / gutters end with a smooth pull-up over the top; climb enter / exit, zipline, locker, pickpocket and
  parachute montage slots in the anim set.
- Ziplines are used with `F` at a post and ride on their own to the other end (direction option: both ways,
  start to end, end to start), letting go before the anchor.
- Locker: a hollow cabinet with a slitted door; you stand inside and look out; the door opens and closes.
- Window: a thin frame with two shutters that swing open sideways.
- Container (`BP_Container`, `BP_Container_Random`): two-door cabinet with a shelf; fixed contents or a random
  weighted pick (catalog loot when the pool is empty); items float in their slots.
- Security station: the console has a screen cycling through its cameras ("no signal" when off).
- Turret: red laser while locking on, lock-on delay before the first shot, barrel kick, muzzle / impact effects,
  hackable with `F` on the turret itself.
- Mounted gun usable from any side; tripod and shield plate.
- Remote-ability targets glow (blue fresnel) with `[A] Charm` / `[A] Activate remotely` / `[E] Swap places` under them.
- White fresnel highlight on whatever `F` would act on.
- Dropped weapons settle, then float and turn (no physics) instead of lying on the floor.
- VFX from the Vefects packs: muzzle flashes, impacts, explosions (rockets, grenades, tank shells, barrels),
  smoke grenade cloud, flashbang, rocket smoke trail and launcher back-blast.
- `DA_SoundSet` (`EHitmenSound`, Project Settings > Hitmen > Sound Set): placeholder sounds from HeartBeats,
  SFX Essentials and Vefects for UI, doors, lockers, zipline, pickups, explosions, heartbeat, Sightroom, turret.
- AI: spline (curved) patrol routes, guard groups (`BP_GuardGroup`: leader + followers in formation, shared
  alerts), keycards visible on the hip and pickpocketed with `F` from behind, red aiming laser for snipers.
- HUD: action prompt under the crosshair (`[F]` key cap, locked reason in red), damage vignette and heartbeat at
  low health, sniper scope overlay, inventory bar (weapon cards with dimmed ammo, sliding on switch, A / E ability
  cards, keycard chips, health line), threat arrows around the crosshair, minimap on the baked map with an outline
  and status (Suspicious / Alerted / Searching / Spotted / Under fire).
- Map baker (`UHitmenContentTools::BakeMapTexture`): top-down schematic textures `T_Map_*` for every map.
- Sightroom: scan wave from the player, floor grid behind it, cool desaturated world, fade in / out.
- UI font: Barlow Condensed (OFL, `Tools/Fonts`) as `FNT_Barlow`; menu kit (`HitmenMenuKit`): menu buttons with an
  accent bar, arrow buttons, toggles, cards, choice carousel, backdrop.

### Changed
- Gym rebuilt as a compact walled hall (74 x 74 m): movement course, armoury, abilities, fixtures and an AI pen,
  labels just above each station.
- Faster player (walk 480, sprint 820, crouch 240); crouch lowers the view smoothly; slides only start from a sprint.
- Lower default mouse sensitivity (0.45, migrated once) and zoom-compensated aiming sensitivity; no mouse smoothing.
- Firing never widens the spread (no bloom); crosshair is a thin "+" that turns into an "x" on a kill.
- Weapons use the Weapons_Free gun pack (pistol, rifles, shotgun, sniper, knife), scaled and turned by
  `VisualTransform`; one mesh per weapon.
- NPCs hit softer (difficulty damage x0.3 / x0.45 / x0.7) and walk / run with path acceleration (locomotion plays).
- Security cameras: shorter, narrower cone aimed at the floor with a round footprint that follows the sweep.
- Civilians removed (`BP_NPC_Civilian` deleted).

### Fixed
- `F` did nothing after a respawn (the focus trace only started when the pawn was already possessed).
- Front-end screens lost the mouse cursor (Escape toggled an invisible pause; the input mode was set in two places).
- Dead NPC capsules blocked the dropped gun and the body; dropped keycards were invisible and could not be taken.
- The locker teleported you into its solid mesh; window lintels stopped the vault probe.

## [0.2.0] - 2026-11-08

### Added
- `Tools/Editor/ue_rebuild.ps1` / `Rebuild.bat`: compiles C++ with Live Coding when only `.cpp`
  files changed, otherwise saves, closes, builds and always reopens the editor.
- Two NPC types: **Patrol** (route) and **Stationary** (holds its post and facing, `BT_Guard`).
  New options: React To Noises (off = sight and damage only) and Hold Position (never moves;
  shoots on sight, searches by looking around). Sentries at the LDTest manor gate; gym pens for
  every type.
- `Report Noise` Blueprint node (`UHitmenCombatLibrary::ReportNoise`).
- `ue_rebuild.ps1 -RunPython <script>` runs editor Python headless between the build and the
  reopen; `Tools/Editor/hitmen_build_ai.py` regenerates the behavior trees that way.
- Lobby sonar timing options (first interval, fastest interval, speed-up time), hideable like
  every option; match rules sanitize lobby settings in one place.
- Steam friends' rooms: friend-session query for online friends playing Hitmen, merged with the
  public search; join failures are shown on the Join screen.
- Spectator grid size on the mouse wheel (1, 2, 3… all players); grid captures owned by the
  player controller.
- HUD view models on `UHitmenUIEventSubsystem` (inventory, abilities, vitals, carry, alert blips,
  emotes, spectator views) pushed by the player controller.
- Options apply live: FOV and ladder direction on the character, volumes through `SCM_Volume`
  and the engine Master / Music / SFX / Voice classes (`BP_GameInstance`).
- Placeholder player icons (colour + glyph) until icon textures exist; placeholder cosmetics per
  slot (cap, glasses, vest, holster, knee pad).
- `BP_GameInstance`, `BP_GameState`, `BP_PlayerState`, `BP_SpectatorPawn`.
- `UHitmenMoveAbility` / `UHitmenManipulationAbility` base classes; ability failure feedback
  (`Client_AbilityFailed`, `Juice_OnFailed`).
- Pill and flashbang screen effects (`PP_Pill`, `PP_Blind`) on the local player; visible smoke
  puffs for players (`M_Smoke`).
- Vehicles have health (`DriverDamageShare` reaches the driver, ejected when destroyed); the
  mounted gun is used from behind only.
- AI: white → yellow investigation, C++ search scoring over hiding spots with claims, shared sight
  test (`CanObserverSee`), floor-aware flanking, alert snapshots and delegates for UI,
  `DefaultBehaviorTree` fallback (charmed players), reference-counted last-known ghost
  (`BP_LastKnownGhost`).
- Weapons: one actor for a weapon's whole life (the thrown weapon is the pickup), fire / equip
  montages, dry-fire sound; death, hit-react, mantle, vault, throw and interact montages from
  the anim set; `ABP_HitmenCharacter` reads every animation from `DA_AnimSet` (via
  `UHitmenAnimGraphTools`) and has a montage slot.
- Footstep notifies on the walk / jog clips; default per-surface footstep entries.
- "No signal" material on monitors of disabled cameras.
- Blueprint graphs laid out by convention through the Unreal MCP and
  `UHitmenBlueprintGraphTools`: every Blueprint has an Init graph (BeginPlay) and a Juice graph
  with one stub per `Juice_On…` hook, in colour-coded comment boxes.
- Gym AI row: alert states and search, camera alarm with responders, bodies.

### Changed
- `ue_remote.py` targets the Hitmen editor when several Unreal editors are open.
- Work in the open editor: Live Coding for `.cpp`, one batched header build, Blueprint graphs and
  LD through the Unreal MCP. `L_Gym` / `L_LDTest` are hand-edited; the level builder only creates
  them when missing (`HITMEN_FORCE_MAPS` regenerates).
- Cosmetics use three slots: Head, Body, Legs.
- Minimap moved to the bottom left; abilities and health to the bottom right; the compass letters
  N / S / E / W only.
- Noise responders capped at 2 for every noise type; reinforcement calls come from alerted NPCs
  that are not shooting (never relayed by default); only sentries ignore bodies.
- VIP candidates beyond 15 are drawn at random.
- Content root tidied: the template first-person ABP moved to
  `Hitmen/Characters/Player/Anims/ABP_FirstPerson`; unused template input and touch assets removed.
- LDTest reconnected (basement, tunnel, upper floor), 130 cm doorways, secret shed passage, vent
  duct, breaker rooms, street lamps, checker materials (`MI_Grid_*`).

### Fixed
- Rebuilding a behavior tree over an existing one lost decorators (one per old decorator), so
  NPCs neither moved nor shot; the builder now purges the old tree first.
- `ue_rebuild.ps1` closes leftover crash reporters and retries a link blocked by a locked DLL.
- Spectator free fly did nothing (input went to the null pawn); dead players now switch to
  spectating on their client too.
- Charmed players stood still (no behavior tree on their controller).
- Tank driving / aim and jetpack thrust only worked for the host.
- Vehicle drivers could not be damaged.
- Lobby player icon stuck on #1; typing a name was lost when the lobby refreshed.
- Behavior-tree "target visible" checks did not abort running branches.
- Kills were witnessed in the dark or by NPCs that could not see the killer.
- Difficulty sight range multiplier was never applied.
- Poison vial spent a charge on unsealed rooms; Hook stopped pulling on clients after one use.
- Auto ammo and VIP delivery only triggered when entering the area.
- HUD, crosshair, minimap, compass, emote wheel and spectator widgets read gameplay components
  directly.

### Known gaps (next)
- Lobby / session flow not yet tested with real clients (Null in PIE multi-client, Steam friends'
  rooms in the packaged Development build with two Steam accounts). Charm, spectator, camera
  alarm and the attack chain were verified on the listen-server host only; tank / jetpack client
  prediction needs a two-client PIE session.
- `Juice_On…` graphs are empty stubs for the team to fill; component hooks need Blueprint
  subclasses of the components.
- Title-screen ambience slot still has no sound (no usable engine ambience asset).
- Placeholder art only (engine shapes, checker material, mannequins); no SFX/VFX assigned yet.

## [0.1.0] - 2026-11-08

### Added
- Unreal Engine 5.7 project replacing the Unity prototype; Unreal `.gitignore` and LFS
  `.gitattributes` (LFS for `.uasset`/`.umap`, media and build binaries).
- `docs/GAME.md` (design + architecture invariants), `docs/Conventions.md` (prefixes, folders,
  Blueprint colour code), this changelog, `CLAUDE.md`.
- Module `Hitmen` (C++): core types, team rules, interfaces, `Project Settings > Hitmen`, player options, data assets (match rules, maps, weapons, abilities, cosmetics, anim sets, catalog); Steam/voice/trace-channel/surface config.
- Framework: menu/lobby/match/gym game modes, replicated game/player state, input-owning player controller with lobby RPCs and spectator modes, HUD widget manager, UI event hub, Steam session subsystem.
- Characters: shared base, predicted movement (sprint, slide, ledge grab, vault, ladders, ziplines), health, status effects, footsteps per surface with AI noise, cosmetics, anim instance + anim set.
- Inventory (2 slots, throw rules, ammo merge, keycards) and data-driven weapons: hitscan/projectile/melee families, projectiles incl. black hole, flashbang, smoke; weapon pickups.
- Ability system (E move / A manipulation) with blink, double jump, jetpack, dash, hook, transposition, pill, invisibility, armor, C4, stone, remote distraction, sleep dart, charm, flashbang, smoke, lighter, multi-kill, trap, wind mine, poison vial.
- Player character: interaction prompt, fists + stealth kill, body carry, Sightroom (Tab), emotes, charm takeover, proximity voice.
- AI: alert state machine, sight perception, capped/occluded hearing, kill witnesses, reinforcement calls, flanking, NPC class, patrol routes, C++ BT tasks and decorator.
- World: doors/keycards, windows, vents + sealed-room poison, ladders, ziplines, jump pads, teleporters, lockers, tall grass, distractions, cameras + control rooms, turrets, breakers/lights/blackouts, barrels, breakable walls, drinks, mounted gun, tank, retrieval/loot/team-start candidates with spread selection.
- UI: title/join, lobby (carousel, options, drag-and-drop teams, roles, watchers), customization + loadout, HUD (compass, crosshair + alert arcs + headshot cross, prompt, inventory, abilities, minimap), alert indicator, pause + map + debug panel, options + key rebinding, emote wheel, spectator grid, results.
- `HitmenEditor` module: Python-callable builder for BB_NPC and the NPC behavior trees (real editable graphs); camera cone floor footprint; remote Python execution, custom depth stencil, runtime navmesh.
- Generated content (`Tools/Editor/hitmen_build_all.py`): input (AZERTY, rebindable), materials incl. Sightroom post-process, data for 12 weapons / 21 abilities / cosmetics / rules / maps, BP_ children of every class, WBP_ widgets, behavior trees, and maps L_TitleScreen, L_Lobby, L_Empty_01/02, L_Gym, L_LDTest.
- Editor tooling: `ue_remote.py` (Python in the open editor), `ue_shot.sh` (PIE capture), `ue_bake_nav.sh`; baked navmeshes; level builder fixes (rotations, in-place rebuild, title lighting, labels, team starts).
- NPC walk speed per alert state (calm/alert/combat) and NPC damage scaled by the difficulty preset
- Packaged player builds: Shipping in `Builds/Windows`, Development in `Builds/Windows Dev`

### Removed
- Unity project, template Horror/Shooter variants, marketplace demo maps.
