# Changelog

All notable changes to Hitmen. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions before 0.1.0 belong to the Unity prototype (see git history before the Unreal port).

## [Unreleased]

### Added
- Tools/Editor/hitmen_check_reach.py: LD check for the map open in the editor. Every door must be walkable and every
  loot spawn, NPC and retrieval point reachable on foot from a team start (snipers and roof points are listed as
  ladder-only).

### Changed
- Navmesh: 10 cm cells (from 19) so every 110 cm doorway is walkable by the AI, and 16384 A* search nodes (from
  2048) so long patrol legs on a big map are not cut short. L_Empty_01 and L_Empty_02 are re-baked.

## [0.5.0] - 2026-11-11

### Added
- Health regeneration: players heal back 30 per second after 6 s without being hit (UHitmenHealthComponent
  RegenDelay / RegenRate).
- Hold to interact: interactables can ask for F to be held (IHitmenInteractable GetInteractHoldTime). Hacking the
  turret panel takes 3 s and the security console 2 s; a bar fills under the crosshair with a hacking sound;
  releasing F or looking away cancels; the server times the hold too.
- Landing sounds on every landing by fall speed (soft / hard), a view dip on hard landings.











### Changed
- UI red is pure red (sRGB 215, 0, 0) everywhere: menus, alert red, HUD. It was a linear colour shown as pink.
- Menu buttons: hover snaps (no fade, no sliding label) and a lit block turns white with a dark label; the red bar
  that grew on the left of the title buttons is gone.
- Arrows (map carousel, difficulty, agent look) are plain solid white triangles, red while hovered or pressed: no
  square block, no shaft.
- Pause menu: just PAUSE with big square Resume / Options / Leave match blocks (no 'contract in progress' or 'the
  match keeps running' lines); the legend moved to the right and shows the real marker colours (guards white /
  yellow / red as on the map and the minimap, no more orange); the map is dimmed so white markers read; debug tools
  are closed behind a 'Debug tools' button and open over the legend.
- Changing team (and other confirms) plays a soft tick instead of a machine start-up; no thunder sting when the
  match starts.
- Your agent: the arrows sit twice as far from the body (AgentArrowSpacing) and, with the codename and Randomize,
  are placed from the agent's axis and reference pose: they no longer sway with the idle animation; the codename box
  is wider.
- Loadout: Hitman-like tiles, 112 px squares with a big icon and the full name on one or two lines (no more cut
  names such as 'SILENCED PI.'); hover snaps with a white frame, the pick gets a red name band.
- Icons are sorted into /Game/Hitmen/UI/Icons/Weapons, Abilities/Move and Abilities/Manipulation (references kept);
  docs/Pipelines.md explains how to swap one; the importer and hitmen_organize_icons.py keep the folders.
- HUD inventory: the four cards (two weapons, two abilities) have one size and fixed places; switching weapon snaps
  (no sliding); every card shows the item's icon and its full name (font shrinks to fit, no overflow); ammo reads
  'current / max' (e.g. 2 / 15) instead of 'N rounds'; abilities show no key letter and no 'ready', a cooldown
  shade, charge pips, and the jetpack a fuel gauge.
- Crosshair: Hitman-like awareness arcs replace the chevrons: one arc per guard or camera noticing you, toward it,
  filling with its suspicion (white) then yellow / red; cameras that see you get an arc with a camera mark. Headshot
  hits get red, longer hit ticks (not only headshot kills).
- Minimap: your own view cone is gone; the status (Suspicious ... Under fire) is a solid banner inside the top of
  the map that opens with a white flash; Under fire is taller, red and pulses.
- AI hearing is far smaller and never reveals you: footsteps 1.5 m walking / 4.5 m running; a gunshot report is
  heard 6-9 m around the shooter (was 26-60 m), the bullet impact is what draws guards (they investigate where it
  landed); each wall cuts the radius again (more than 3 walls: silence); explosions 15 m.
- Guards fire much less: a 0.6 s raise, bursts of 3 shots at 2.5x the weapon's interval, a 1.2 s pause (match rules,
  NPC fire); difficulty damage multipliers 0.25 / 0.4 / 0.6. A fight in the open now lasts about half a minute
  instead of a few seconds.
- The turret tower itself is no longer interactable: only its hack panel hacks it.
- Lockers: no more instant teleport: the door opens and you step in over 0.5 s turning to face out, then it closes;
  stepping out is the reverse (every machine animates it).
- Radio distraction: once turned on it plays a radio loop for 8 s with a pulsing light (visible and audible
  indicator), and makes its noise (10 m) on every beat.
- Explosive barrels: explosion sound and view shake (effect slot for the toon explosion), chain reactions; the
  default explosion noise is 15 m (was 40 m).
- Silent takedown from any side, standing or crouched, as long as the guard is not looking at you (it used to need
  you behind it); the attacker blends in and the view turns over 0.25 s instead of snapping (also for clients).
- Crouching while walking does a short slide (a sprint gives the long one).
- Thrown weapons no longer tumble as physics bodies: they land at once as a floating pickup a little ahead (before
  any wall).
- First-person arms sit further right; aiming keeps the gun low on the right (it re-centred and filled the screen);
  sprinting with a weapon lowers and tilts it instead of losing it; unarmed arms are higher so punches cross the
  view.
- Mounted gun shots are felt: sound, muzzle light flash, view shake. Tank: a real cannon barrel that follows the aim
  pitch and kicks back; the shell glows, flies straight and near-instantly (no gravity, 20000 cm/s) and blasts 12 m
  wide (was 6.5 m); fire sound, muzzle flash, view shake.
- Explosions (rocket, tank shell, C4, grenade, barrels, flashbang) use 3D toon fire and smoke from
  Anime_Stylized_VFX instead of the 2D flipbook sheets that read as comic book; every slot stays swappable in the
  data.
- Last-known-position ghost: a faint white silhouette (15 % opacity, was 35 %), Hitman-like.





### Fixed
- First person: guns point along the view. The 0.4.0 hold fix was never saved into the weapon data assets (the pistol
  and launchers were still turned -90 degrees, the rifles pitched 45 degrees). `hitmen_fp_solve.py` now solves the
  rotation in each mesh's own frame (its 0.4.0 guess laid guns on their side); launchers are drawn at 0.55 and long
  guns at 0.75 as intended, the placeholder bow at 0.5.
- Guards knew where you were: a gunshot within 9 m or any hit made them lock onto your real position and shoot
  through walls (the target stayed 'visible' forever). Now noise only sends them to the sound or impact, a hit from
  an unseen shooter puts them on alert toward the shot, and only sight reveals you; in the dark a hostile heard
  within 2.5 m is attacked.
- Doors swung toward or away from you depending on which end you stood at (the side test used the axis along the
  door): they now always open away from you, and fill their 110 x 230 openings (a 10 cm gap at the side and 20 cm on
  top before). The NPC door trigger is centred.
- Vent hatches spun inside their own plane like a pendulum: they now swing open on their top edge like a small door,
  away from the opener.
- Window flicker: the frame faces were coplanar with the wall's opening and the closed shutters overlapped in one
  plane; the frame now sits inside the opening, the shutters at two depths, and they open 80 degrees (105 cut into
  the wall).
- Ladders: Space pressed just before or right after grabbing a ladder no longer jumps you straight off it (0.35 s
  grace).
- Mounted gun: its barrel pointed straight up (unrotated cylinder) and it turned 2.5x slower than the player's view
  (raw look input); it now points forward and matches the player's sensitivity.
- Arriving on a map, the image slowly brightened or darkened (default eye adaptation): every match map now has one
  fixed exposure (MatchExposure post process volume, also made by the level builder).
- Guards in combat now raise their weapon (aim pose): only snipers ticked, so the others never played it; and the
  additive fire montage no longer replaces the aim pose on each shot (arms dropping).
- Security camera footage showed a white floor: the feed looked down at the camera's own light pool through a
  34-degree view, untonemapped, at 512 x 288. It now looks ahead with a 90-degree view (FeedFOV), tonemapped at the
  match exposure, at 1024 x 576. The 'no signal' screen works on consoles too (wrong material path, unset on the
  console).
- Footsteps were silent (the per-surface sound slots were empty): players and NPCs now play a footstep per surface
  (grass, stone, dirt, wood, metal, concrete by default).
- Gym: the armor and C4 pickups sat inside blocks (they could not be picked up): they now rest on top.


### Removed
- Every weapon VFX (muzzle flashes, impact puffs, surface dust, blood, rocket back blast, turret and arrow impacts):
  the comic-book flipbooks are gone; the data slots stay for the team's own effects.
- The launch countdown (5-4-3-2-1) and the intro 3-2-1: when everyone is ready the match starts a second later; the
  drop only shows 'Dropping in' and the map name.
- The health line and the health text: health shows only through the red screen edges and the heartbeat.
- The 'TV / silent hack with F' hackable distraction, everywhere: class, Blueprint, its Gym placement and label,
  builders and docs.
- The Gym slide lane (blue strip and label): sliding is a move of the character, not a fixture.

## [0.4.1] - 2026-11-11

### Fixed
- **Crash about one minute after launch** (all 20 reports of the 0.4.0 playtest). The minimap, the Create Room map
  tile and the pause map drew textures through `FSlateBrush` members that were not `UPROPERTY`: the first garbage
  collection (about 61 s in) freed the textures and the next paint read freed memory. PIE never showed it because the
  editor keeps the assets loaded. Every brush is now a `UPROPERTY(Transient)`.
- Related hardening from a GC audit of the whole module: the UI font is kept loaded once found (it silently fell back to
  the engine font after a GC), the barrel chain-explosion timer holds its instigator weakly, the lobby's local loadout
  copies are `UPROPERTY`, retrieval-point markers skip destroyed points.

### Removed
- The version line in the bottom-left corner of the title screen.

### Added
- `Tools/Debug/hitmen_crash_report.py`: groups the crash reports of a build and symbolizes their stacks with the
  build's PDB (checks that the crashed exe matches it).
- `Tools/Packaging/package_builds.ps1`: packages the Dev and Shipping player builds, keeps each build's `Saved` folder
  (crash reports) and archives the PDB per version in `Builds/_Symbols/<version>/`.

## [0.4.0] - 2026-11-10

### Fixed
- Crash candidates from the playtest (no log was available): a voice talker stayed bound to a destroyed body after
  a respawn (now released with the body); Steam searches ran in parallel on Steam's single search slot (public
  rooms, then friends' rooms, now one query at a time through one queue, refresh spam ignored); session delegates
  are cleared when the subsystem goes away; the UI folder is always cooked (font loaded by path).

### Changed
- Front end lit at one fixed exposure (unbound post process volume, min = max brightness): no more dark briefing
  room that slowly brightens, no overexposed agent stage. Briefing fills added, stage key / rim softened.
- Title: the HITMEN wordmark alone (no tagline, no red underline); square menu blocks top to bottom Join room
  (dashed outline), Create room, Options, Quit. Space or Escape goes back on every menu page; menu buttons never
  take keyboard focus (Space no longer re-presses the last button).
- Create Room: the players panel runs from the map to the screen edge; three nested blocks (players panel, opaque
  team blocks with a tinted header, a solid block per player); team role is a square ATTACK / PROTECT block,
  remove is a cross; the watcher toggle is an eye (open / struck) shown when hovering a player; the map tile fills
  its frame with the map image (cropped, never letterboxed) with the whole map small in the top-left corner.
- Arrows are real arrows (shaft and head) on square blocks; fast clicks all count (double clicks are steps).
- Your agent: framed further left, codename above the head, Randomize under the feet, no slot labels; the look
  changes on the stage at once and is sent once clicks pause (no lobby rebuild per click, unchanged cosmetic
  slots are skipped).
- Loadout: three always-visible rows of square tiles (weapon, move, manipulation) with a None tile each; a click
  picks, hover is instant, names stay inside their tile, no hint text. Default loadout: silenced pistol, no
  abilities. The launch countdown is one big number in a square block.

- Traversal: steps up to 55 cm are walked up; Space counts for 0.25 s after the press (late ledge / ladder
  grabs); jumping at a ladder grabs it at once (only the ladder just left keeps its cooldown); vault / climb hints
  only show on the ground; a block wall (top at least 40 cm deep) is climbed onto and walked along, a thinner wall
  is climbed over.
- Doors and vent hatches swing on a hinge (their edge), not around their centre.
- Security cameras: the view is a shadowed spotlight (walls cut it, it spreads on the floor), no translucent cone
  or halo; a player is seen only through open air (visibility and static geometry tested to the head and chest).
- Guards see 35 % less far (sight 1950 / lose 2250, suspicion falloff 1600, instant detection 300).
- Players have 300 health (guards unchanged). Default mouse sensitivity 0.25 (migrated once).
- Minimap is north-up; your arrow turns.
- Parachute: the move keys steer the canopy; guards and cameras cannot see a player under the canopy or for 2 s
  after landing.
- Stealth takedown: the attacker steps in behind the victim, both are held, the attack plays on the body and the
  first-person arms (camera shake), the victim plays its reaction, and dies on the strike (0.55 s of 1 s).
- Swapped / dropped weapons become a floating pickup at once (no physics tumble) at their real size; the focus
  glow works on skeletal guns (rocket / grenade launchers, black hole gun).
- Containers hide their contents while their doors are closed.
- First person: the arms hold guns on the right (BP hip offset was left of centre) and every gun points along the
  view (the old -90 yaw turned them sideways); rotations solved in PIE by `Tools/Editor/hitmen_fp_solve.py`.
  Launchers are drawn smaller; the knife and the bow use the hold pose (the knife sat at the hip).
- Punches are seen: without a weapon the arms move to chest height (`ArmsUnarmedOffset`) so the fists cross the view.
- Windows lie in their wall (they were spawned across it): 100 windows turned in the Gym and LDTest
  (`Tools/Editor/hitmen_patch_levels_040.py`), the level builder spawns them turned. A closed window can no
  longer be vaulted (the shutters left a 1 cm gap on the centre line; the passage check is a sweep on the pawn
  channel), and the vault probe finds the sill under a lintel.
- LDTest: 8 of the 16 spawn points were removed, the ones within guard sight (two had a guard 3 to 5 m away).
  Gym: the guard group keeps one follower.
- Bullet impacts: a dust puff thrown back toward the shooter on surfaces, blood on characters.
- The interaction prompt keeps its target 0.2 s when the trace grazes a frame (no flicker on windows).

### Added
- Placeholder icons for every weapon and ability (`Tools/Icons/make_icons.py` draws them, `hitmen_import_icons.py`
  imports them into `/Game/Hitmen/UI/Icons` and sets each data asset's Icon); shown on the loadout tiles.
- Map previews (`Tools/Editor/ue_map_shot.sh` + `hitmen_map_shots.py`): a spawn-view screenshot per map as its
  Thumbnail, shown full-bleed on the Create Room tile.
- NPC aim pose: guards in combat hold their weapon's aim montage (AM_Pistol_Aim / AM_Rifle_Aim, looped) while
  standing or walking slowly; the fire montage plays over it on each shot.
- Hidden vignette: dark soft edges and an eye-off mark while concealed (tall grass).
- Room codes: every room gets a 6-character code (no 0/O/1/I/L), shown on Create Room; Join has a code field.
- Join: the Steam friends list (playing Hitmen / online / offline, click a friend in a room to join), then the
  rooms with a refresh button beside the header and the messages inside the list.

## [0.3.0] - 2026-11-09

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
- Front end in one scene (`L_FrontEnd`, replaces `L_TitleScreen` and `L_Lobby`): title, room and agent screens
  blend between tagged cameras (`Shot_<Phase>`) with no level load; creating a room listens in place.
- Your agent: the mannequin on the stage with look arrows beside its head, body and legs (live), name editing,
  loadout cards with a hover carousel.
- Ready-only start: when every non-watcher is ready a 5 s countdown launches the match.
- Match intro: parachute drop above your spawn (canopy, slight steering, control on landing) with a
  "Dropping in" / 3-2-1 overlay; spawn clusters (random point per team, teammates next to each other); 16 spread
  spawn points in LDTest.
- Pause map: the baked schematic at two thirds of the screen, wheel zoom, a clickable legend to toggle each marker
  type; debug tools flush right.
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
- Lobby: player rows without letter glyphs, white names, whole-row drag & drop, Watcher checkbox, large map tile
  with square arrow buttons; options reduced to proximity chat and difficulty. The retrieval-point count moved
  into the difficulty presets; sonar timings are designer-only (match rules).
- Title screen: logo and buttons vertically centred on the left. All menus restyled (pause, options, results).

### Fixed
- `F` did nothing after a respawn (the focus trace only started when the pawn was already possessed).
- Front-end screens lost the mouse cursor (Escape toggled an invisible pause; the input mode was set in two places).
- Dead NPC capsules blocked the dropped gun and the body; dropped keycards were invisible and could not be taken.
- The locker teleported you into its solid mesh; window lintels stopped the vault probe.
- The chosen loadout and look were lost after a non-seamless travel (PIE, reconnects): the game instance keeps them
  and re-sends them after every travel; a pawn spawned before they arrive is equipped during the intro.
- Long guns (rifles, shotgun, sniper) were held sideways in first person; every gun now uses the two-hand hold.
- Menu input mode tried to focus a non-focusable widget.

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
