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
- Any body can be picked up with `F` (the VIP too): it lies across your arms in front of you, takes the left weapon
  slot (with both slots full, the weapon in hand is put down; the other one stays slung in the right slot), you cannot
  shoot or punch. In your own view the body is drawn like your gun (first-person render, no collision): it never goes
  into a wall. Variables on the carry component (`BP_PlayerCharacter`): `bCanSprintWhileCarrying` (off: you walk),
  `WalkSpeedPenaltyPercent` and `SprintSpeedPenaltyPercent` (0: no slower). `F` with nothing in focus or the mouse wheel
  puts it down, and the remaining weapon comes back to hand: the body is set back on your side of any wall it was
  carried into and above the floor (`DropClearance`), then falls as a ragdoll (no push). The HUD's left card reads BODY
  (TARGET for the VIP).
- **Hiding spots** (`AHitmenHidingSpot`: lockers and body containers). Empty-handed, `F` hides you inside: it opens, you
  step in facing out, it closes; guards cannot see you; the view turns within `ViewArcDegrees` (180 = left to right,
  360 = free) and `ViewPitchMin / Max`. Carrying a body, `F` stows it in the next slot (`BodySlots`), up to
  `BodyCapacity`, never the VIP: the body stays there, visible whenever it opens, and guards never find it. With
  `bBlockHidingWhenBodyInside` (on) a spot holding a body has no room for you: no key shows, only its info line ("Body container  -  1 / 2 bodies  -  no room to hide"); with a body in your arms `F` stows it while there is room.
  Looking at one always shows its name and contents under the prompt, usable or not ("Body container - 1 / 2 bodies",
  "Locker - 1 / 1 body - no room to hide").
  - **Locker** (`BP_Locker`): stand inside and look through the door slit; holds 1 body (upright at the back).
  - **Body container** (`BP_BodyContainer`): a hollow bin (210 x 110 x 118, `Width / Depth / Height`) with a lid hinged on
    its back edge and a peek slit in the front wall (`SlitHeight` 88): inside you crouch with your eye at the slit;
    holds 2 bodies lying side by side.
- **The VIP dies**: everyone gets a toast at the top ("TARGET ELIMINATED") with a sound (`EHitmenSound::TargetEliminated`)
  and, top left, a small camera filming the body for 4 s (Project Settings → Hitmen → HUD: `bEventCameraOnTargetKill`,
  `EventCameraDuration`, `EventCameraResolution`). The same camera shows you the guard who comes to look at a distraction
  you switched on (`bEventCameraOnDistraction`; at most every `DistractionCameraCooldown` 8 s).
- **Sonar** (from the VIP's death, for everyone): the body shows **yellow through walls** (`PP_Markers`). While it is in
  your view and nothing is in between it is outlined live; otherwise a yellow ghost (a still copy of the body,
  `AHitmenBodyGhost`) stays at the newest place you know: where you last saw it, or the last sonar ping, which moves
  it to where the body is. Pings come every N seconds and more and more often (`SonarStartInterval`,
  `SonarEndInterval`, `SonarRampDuration`, `SonarRampCurve` in the match rules, from the death); the minimap shows each
  ping as a yellow ring. Carrying it yourself, there is no marker. The VIP is always network-relevant.

### Retrieval points
- Level designers place any number of **retrieval point candidates**. Once the VIP is dead, the active ones show **gold
  through walls** for everyone (`bShowRetrievalPointsAfterVIPDeath`). Carrying the VIP's body (Attack role, or no team),
  look at the point and press `F`: **Extract the target** (walking in no longer delivers). The body goes in, every
  player's view blends to the point's `CinematicCamera` (HUD hidden, letterbox bars) and watches the extraction leave
  for `CinematicDuration` (6 s); then everyone becomes a watcher.
- The model is data: `ModelParts` (mesh, material, transform, `bSpins` for rotors) under `Model`, `DepartureStyle`
  (`LiftOff`: rises and flies off, rotors spool up; `DriveAway`; `None`), `DisplayName`. `BP_RetrievalPoint_Helicopter`,
  `_Car` and `_Crate` are built from basic shapes; the team swaps the parts for their own mesh.
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
   A line on how friends join and a dashed **+ Create team** block below. Bottom: **Leave room** and **Choose your
   agent**. One 8 px grid: 112 px side margins, the map tile and the players panel start on the same line under
   the title, the panel ends on the same edge as **Choose your agent**.
4. **Your agent** (stage) — the agent stands on the left; the codename above its head, white triangle arrows (every
   click counts) level with its head, torso and legs in two columns `AgentArrowSpacing` away from its axis, and
   **Randomize** under its feet — all placed from the reference pose, so they never move with the idle animation;
   the look changes on the stage at once and is sent once the clicks pause. Right: the loadout, three
   always-visible rows of square icon tiles (weapon, move `E`, manipulation `A`, keys as bound), each starting with
   **None**; a click picks. A "3 / 8" counter under each pair of look arrows. Default: silenced pistol, no abilities.
   **Ready** only: when every non-watcher is ready the match starts a second later (no countdown on screen; un-ready
   cancels it). Watchers have nothing to dress: they see "Waiting for other players (n / m ready)". A watching host's
   button reads **Start** instead of "Choose your agent"; with only watchers in the room the match starts at once.
5. **Match** — intro (parachute drop, "Dropping in" and the map name, no countdown), play, then the end: a banner at
   the top (winner, reason) — after an extraction, the extraction cinematic first — and two seconds later everyone flies
   free over the map (no timer). The host sends everyone back to the lobby with `Enter` (the hint sits bottom right; or
   **Back to the lobby** in the pause menu); the others see "Waiting for the host" there. A match with only watchers never ends by itself.

Spawns: maps carry many `BP_TeamStart` points. Each team (or solo player) takes a random free point
and teammates take the nearest free points around it. During the intro (`bParachuteIntro`,
`IntroDropHeight`, `IntroDuration` on the match game mode) everyone falls from above their point
under a canopy, steering a little (falling at 15 m/s, 3 s intro); control returns on landing. The Gym skips the intro.

**Watchers** (and dead players) watch with three views (`IMC_Spectator`, its own rebindable keys above the game keys; on
AZERTY the number row also works without Shift, & é " being 1 2 3, and so does the numpad). With nobody alive to watch,
`1` and `2` do nothing and free fly comes on by itself (also when the last player you watched dies):
- `1` **player view**: one player through their eyes; `V` switches to third person over their shoulder (the mouse
  orbits, the camera stays in front of walls); `LMB` / `RMB` switch player.
- `2` **split screen**: every living player at once, up to 8 (`MaxSplitViews`): 1, 2 side by side, 2x2, 3x2, 4x2; each
  picture is shaped like its cell and redrawn `SpectatorCaptureRate` (20) times a second, the captures staggered across
  frames (960 px wide up to 4 views, 640 above).
- `3` **free fly** (watchers only; dead players follow their team): like the Unreal editor on AZERTY, `ZQSD`, `E` /
  `Space` up, `A` / `Ctrl` down, `Shift` x2, the **mouse wheel** sets the speed (x0.25 to x8, shown briefly). It goes
  **through walls and objects** and **under the ground** (down to `WatcherDepthBelowGround` 25 m) inside an invisible
  box: the map's world bounds (`DA_Map`, minus `BoundsMargin`) and a ceiling `MaxHeightAboveGround` over the highest
  ground flown over. Without terrain pass-through it stays just above the surface under the camera (caves included).
  Free fly starts where the camera was.

What a watcher sees: every living player **outlined in cyan through walls with their name** (`N` hides them), the other
watchers as a **glowing orb with their name** (`B`), the info line of what it looks at (name and state, no key, the
thing glows: "Locker - 0 / 1 body", "Light - On"), and `Tab` scans (free fly, third person) with every guard lit.
Players can be allowed to see name tags, the watchers (as **ghosts**: a pale see-through body, `WatcherGhostMesh`,
`WatcherGhostAnimation`, `WatcherGhostMaterial`, `WatcherGhostColor`) and a "WATCHED BY n PLAYERS" line (all off by default).
**Voice**: the living never hear the dead or the watchers (`bDeadCanTalkToLiving`, off); the dead and watchers hear
everyone and each other; with proximity chat on, a flying watcher's voice comes from where it flies
(`bWatcherProximityVoice`, falloff `VoiceAttenuation` = `SA_Hitmen_Voice`, full within 4 m, gone at 25 m, also used by
bodies). No Unreal debug UI ever comes up in game: the Gameplay Debugger key is cleared (`ActivationKey=None` in
DefaultEngine.ini; it was the apostrophe, the AZERTY 4, and Tab then opened its debug camera) and so are the engine's
debug camera (`;`), debug target and F9 screenshot bindings. Every rule is an editor variable in **Project Settings ->
Hitmen -> Watchers**: `bWatchersFlyThroughWalls`, `bWatchersFlyThroughTerrain`,
`WatcherDepthBelowGround`, `bWatchersCanScan`, `WatcherSeePlayers` / `WatcherSeeWatchers` (Hidden, Shown, **Shown and
toggleable** by default), `bWatchersSeeInteractableInfo`, `bWatchersHighlightInteractables`, `WatcherFocusRange`,
`bPlayersSeeNameTags` (`PlayerNameTagRange`), `bPlayersSeeWatchers`, `bPlayersSeeWatchedIndicator`, `WatcherViewRate`,
`bDeadCanTalkToLiving`, `bWatcherProximityVoice`, `VoiceAttenuation`.
A watcher's view (position, direction, followed player) goes to the server 10 times a second while it moves
(`Server_ReportWatcherView`) and is replicated on its PlayerState (`FHitmenWatcherView`); every machine spawns its own
avatars (`AHitmenWatcherAvatar`, local) and paints the tags in one layer (`UHitmenNameTagsWidget`). QA: the console
command `HitmenWatch` (gym and dev builds) turns you into a watcher. Only spectators see the keys line at the bottom,
views first (`[1] player view  [2] split screen  [3] free fly`); every spectator may free fly (dead players and
everyone at match end too, `bSpectatorsCanFreeFly`, on).

## 5. Controls (defaults, AZERTY, all rebindable)
On a QWERTY keyboard the keys sit at the same places: W A S D to move, Q for the manipulation ability. The game
reads the keyboard itself (the key at the top-left letter place types A on AZERTY: a French Windows with a US keyboard is
QWERTY), moves only the keys the player never rebound (Options -> Controls -> Defaults places them again); prompts, HUD keys and the watcher line follow. `KeyboardLayoutOverride` (Project Settings ->
Hitmen -> Input: Auto / AZERTY / QWERTY) forces one for testing.

| Action | Key |
|---|---|
| Move | Z Q S D |
| Look | Mouse |
| Jump / ledge grab / vault (held in the air: jetpack) | Space |
| Crouch (held) — keeps your momentum: a short lunge from a walk, a slide from a sprint; with Shift, an instant slide | Left Ctrl |
| Sprint (held, any direction by default: `SprintDirections` ForwardOnly / ForwardAndSides / AllDirections) / dash (tap, when equipped) | Left Shift |
| Fire / melee | LMB |
| Aim | RMB |
| Interact / stealth kill / swap; throw the held weapon while aiming | F |
| Move ability | E |
| Manipulation ability | A |
| Switch weapon slot | Mouse wheel |
| Sightroom | Tab |
| Emote wheel (hold, release on an emote); R again ends the emote | R |
| Pause (no time stop) | Esc |
| Confirm (match over, host: back to the lobby) | Enter |
| Watching: player view / split screen / free fly | 1 / 2 / 3 |
| Watching: first / third person, switch player | V, LMB / RMB |
| Watching: fly up / down / faster / speed | Space or E / Ctrl or A / Shift / mouse wheel |
| Watching: show / hide players, watchers | N, B |

**Options** (title and pause; `Esc` closes them, `R` resets the tab on screen, `Q` / `E` switch tabs): a centred panel
with tabs, one row per setting (label left, `< value >` or a slider right) and the hovered row described on the right.
- **Gameplay**: language (Auto follows the system, then every language the text sheet has), mouse and aiming
  sensitivity, invert Y, crouch and sprint hold / toggle, ladder direction.
- **Controls**: every key by section (Movement, Combat, Abilities, Interaction, Spectator, Menus); click then press a key
  or a mouse button (`Esc` cancels); a key used twice turns red and the description names the other action. Spectator
  keys only clash with each other. Prompts and HUD keys follow the rebinding.
- **Graphics**: quality preset (Low / Medium / High / Epic, Custom when mixed), **Detect the best settings** (hardware
  benchmark; runs once by itself on the first launch of a packaged build), resolution, window mode, vertical sync,
  frame rate limit, render scale, shadows, global illumination (Low / Medium turn Lumen off), reflections, effects,
  view distance, post-processing, textures, anti-aliasing, field of view, head bob.
- **Audio**: master, music, effects, voice, interface.

## 6. Player
- **Movement**: walk (420), sprint (720), crouch (210, the view lowers smoothly), jump (instant, never turned into a
  climb onto a curb, a step or the floor), ladders / gutters (automatic; forward climbs toward where you look: facing the ladder up, facing away down — Options "Ladders and gutters: Forward is up" keeps the 0.9.0 controls; a jump off follows the move keys: left / right hop along the wall (`ClimbSideJumpSpeed`, soft enough to catch a ledge beside it), anything else jumps away; smooth pull-up at the top),
  ziplines (`F` at either post: rides to the other end on its own at 16 m/s, up or down), jump pads, lockers (you stand inside and look
  through the slit). Releasing the keys stops you crisply.
- **Stiff ledges** (Hitman-like, `bStiffLedges` on the movement component, on): walking never takes you over a drop
  deeper than `StiffLedgeMaxDrop` (120 cm) — you stop at the edge and slide along it; smaller steps down still walk
  off. A jump still leaves a ledge (`bCanJumpOffStiffLedges`, on; off: a ground jump toward such a drop goes straight
  up). Ladders, gutters and hanging are unchanged. Players only (NPCs follow the navmesh); predicted like the rest.
- **Momentum crouch / slide** (the common FPS rules: CoD, Apex / Titanfall, Halo, Battlefield): crouching while moving
  keeps going the way you were going and brakes down to crouch speed — from a walk a short lunge (+110, about 0.35 s /
  1.4 m), from a sprint a slide (+160, about 0.75 s / 3.9 m). **Crouch + Shift** slides at once at 820 in the move
  direction, from a crouch-walk or a standstill (no stand-up, no ramp-up). Letting go of the keys keeps the momentum,
  left / right only bend it (60°/s), slopes speed it up or slow it down, releasing Ctrl stands up with the speed, a
  jump keeps it. A boost every 0.9 s at most; Ctrl (or Shift for crouch + Shift) must be released before the next
  slide. The view drops, widens 4° and rolls toward the steering. Predicted on the client (saved moves).
- **Window ledges**: every upper floor has a **walkable ledge** (70 cm) all around, its top just under the room floor:
  vault out of an open window onto it, walk along it to the next window (stiff ledges keep you on it), vault back in.
  Crouched on it you stay under the sills (a guard inside does not see you); standing, you are seen. A gap in the
  ledge lets a gutter climb past.
- **Hang**: Space in the air in front of a thin ledge (a roof edge, a wall top) hangs from it. Left / right shimmy along
  it (Shift: faster), Space climbs onto it or into an open window above, Ctrl lets go. Vaulting out of a window over a
  drop with no ledge ends hanging from the sill.
- **Traversal metrics** (Space while moving into it; the action shows at the crosshair):

  | Obstacle | Action |
  |---|---|
  | up to 55 cm | step (walked up, no key) |
  | up to 120 cm, thin, floor behind | vault (also through an open window) |
  | up to 250 cm, top at least 40 cm deep | climb onto it (a block wall: walk along its top) |
  | up to 250 cm, thinner wall | climb over |
  | window sill over a ledge | vault out onto the ledge (a landing just past the sill is tried first) |
  | window sill over a drop | climb out and hang from the sill |
  | above 250 cm | out of reach (ladder, gutter, ability) |
- The hint only shows on the ground. Space counts for 0.25 s after the press (late ledge grabs); jumping at a
  ladder grabs it at once. Ledges are probed at the centre and 25 cm to each side (a corner is caught), and in the air
  down to the feet. Fixtures (barrels, hack panels, turrets, cameras, mounted guns, distractions) carry the
  `HitmenNoTraversal` actor tag and are never climbed.
- **First-person motion** (procedural, `FHitmenFPMotion` per weapon in its data asset): the camera bobs 3 cm and sways
  2.4 cm left / right with every step (about two steps a second walking, three sprinting), rolls 0.7° with the sway,
  lifts on a jump and dips on landing; your footstep plays on the low point of each bob (strength in Options). The arms
  sway with the mouse, lag when you walk and tilt when you strafe. A weapon coming into hand rises from below in about
  0.2 s; each shot kicks it back and up on a spring (per weapon); aiming sends it toward the centre at once on a stiff
  spring (settled in about 0.15 s, the view zooms as fast), still low; the sniper rises to the eye and zooms almost at
  once (about 0.2 s) with a short wobble, and the zoomed aim breathes a little. Punches cock the fist back, snap it out to the crosshair and back while the other fist tucks in (a hit: a puff, a thump, a view shake and kick, a short hit stop); jabs alternate one fist after
  the other, the knife slashes right to left, a throw winds up and releases, a takedown lunges; body montages no longer
  play on the first-person arms. Near a wall the weapon is pulled back and lowered (first-person scale 0.35, near plane
  3 cm). Machine guns sit lower in the hand; the minigun shakes harder as it spins up; the bow arms pull back with the
  charge.
- **First person**: no body or arms while moving without a weapon (the body's shadow stays, crouched when you
  crouch); the arms appear with a weapon in hand (held low on the right, barrel along the view; sprinting lowers and
  tilts it, still in view), while punching (a guard with closed fists low in the corners, each jab driven to the
  crosshair by IK) or climbing a ledge (the hands land on it, push while the view rises and let go; with a gun only the
  left hand, and the view dips toward a ledge below the eye). First-person hand targets sit at least 50 cm out along
  their view ray (the first-person scale clips nearer geometry) and undo the arms' own field of view.
- **Body pose** (no crouch, slide or climb animation in the project: a procedural layer, `FAnimNode_HitmenBodyIK` in
  both anim blueprints): crouched, the pelvis drops 38 cm, the spine leans and the feet stay planted (knees bend);
  sliding is lower, leaning back with the feet in front; climbing a ledge, the hands reach its top (found by a trace on
  every machine, so other players see it) and the legs tuck. An anim set with crouch animations turns it off.
  Recoil kicks the view up and it recovers smoothly. On death the view leaves the head and orbits your body as it falls (third person). Landings are heard on the
  surface you land on (soft or hard, by fall speed); every landing dips the view a little, a hard one shakes it.
- **Inventory**: 2 weapon slots (mouse wheel); the HUD always shows the held weapon on the left card and the two cards trade places in 0.12 s when you switch. A new weapon always goes to the left slot (the other one slides right;
  with both full, the held weapon is swapped out). While aiming (right mouse), `F` with no interactable in focus throws
  the held weapon (`[F] Throw` shows; `bThrowRequiresAim` on the player, on): if slot 1 is thrown, slot 2 moves to slot 1 and is equipped; if slot 2 is thrown, slot 1
  stays. Firing with an empty magazine and no reserve throws the weapon where you look. Thrown weapons fly (physics,
  spinning): the first hostile they hit takes the throw damage and is stunned 3 s on a head hit; an empty gun then
  vanishes, a loaded one settles and floats as a pickup. No weapon
  in hand = fists (melee).
- **Ammo**: one ammo pool per weapon (no magazine / reload). Walking over a weapon of the same
  type picks its ammo automatically and removes it; a different type shows **Swap** on `F`. The
  pistol is silenced.
- **Keycards**: acquired items shown next to the inventory, cannot be dropped. Doors may require
  a keycard ID (0, 1, 2…). NPCs can carry keycards and drop them on death.
- **Stealth kill**: `F` next to a target that is not looking at you — from any side, standing or crouched (a guard
  that sees you coming detects you first and fights). The attacker blends in to the strike position and the view
  turns over 0.25 s (no snap); both are held, the attack plays (body and first-person arms), the victim reacts and dies
  on the strike (1 s). Pickpocketing needs you behind it (crouched, beside it is enough: `CrouchedPickpocketMaxDot`)
  and within `PickpocketReach` (2.2 m) of an NPC that is not looking at you.
- **Health**: players 150, guards 100. A bullet or an arrow in the head always kills (`bHeadshotKills` in Project
  Settings → Hitmen; NPC shots excluded by `bHeadshotKillsFromNPCs`). Players heal back (30 per second) after 6 s
  without being hit; there is no health bar (red screen edges and heartbeat).
- **Intro**: the move keys steer the parachute; nobody can see a player under the canopy or for 2 s
  after landing.
- **Sightroom (`Tab`)**, like Hitman's Instinct, only for you (local render state, never replicated): a fast scan wave
  (0.5 s) sweeps 90 m out from you and the whole map takes the cool scan look. Enemies light up in white through walls
  only within 20 m (`SightroomEnemyRadius` on the player controller, as the wave passes them); the target is red at any
  distance. Usable objects within 18 m light up only where you can really see them (depth-tested in `PP_Sightroom`, never
  through a wall). Highlights are anti-aliased with a thin outline. **Nothing pops**: the scan keeps updating while Tab
  is held, and `PP_Sightroom` lights each pixel from the object's own distance to where you are now (rebuilt from its
  custom depth), fading in over the last 3.5 m of each radius (`HighlightSoftEdge`) and behind the wave front (whose
  passing brightens the outline); an object coming round a corner lights up as it shows. On release the highlights fade
  out with the look. C++ only keeps custom depth on the candidates (`CandidateMargin` beyond each radius, refreshed every
  0.1 s). No waypoint in normal view.
- **Emotes**: hold `R` for a radial wheel (the mouse moves its pointer, the view stays), release on an emote: the view
  flies out to a third-person orbit in front of you (`EmoteCamera*` on the player: 3 m, the mouse turns it, never through
  walls) and the emote's animation loops (`bLoopEmotes` on the emote component) until `R` again, moving, jumping, firing,
  aiming, interacting, an ability or a hit ends it; the view then flies back into your head (0.35 s). Not while
  climbing, hanging, falling, riding or carrying a body. Replicated state (`ActiveEmote`): everyone sees it.
- **Footsteps**: five takes per surface (grass, stone, sand / dirt, wood, metal, tile; concrete elsewhere; Kenney
  Impact Sounds, CC0, sources in `Tools/Audio/Footsteps`), one picked at random and never twice in a row, for players
  and NPCs; the surface comes from the physical material under the feet (PM_*; rocks and mountains are stone) and
  terrain tiles read it from their slope like their colour (grass on the flats, earth on slopes, rock on cliffs); it also
  scales how far NPCs hear the step. One surface table for every character: `DA_SoundSet` → Surfaces (takes, volume,
  pitch, puff, noise multiplier; Tile reuses the stone takes higher and softer). Adding a surface: `docs/Pipelines.md`.
- **Juice switches** (Project Settings → Hitmen → Juice; every effect keeps its asset slot): `bLadderClimbFX` (puff at
  the hands climbing a ladder / gutter, off), `bLadderDescendFX` (`LadderDescendFX` going down, off), `bSlideFX` (dust
  along a slide, off), `bSlideSound` (scrape loop, on), `bFootstepFX` (each surface's puff on steps and landings, off), `bMantleHandsFX` (dust where the hands land on a ledge when climbing or vaulting, off), `bMantleLandFX` (dust under the feet when a vault or climb ends, off): no particles at all on climbs and vaults by default. Volume per gait (walk, sprint, crouch) and a small random pitch; your own steps are 2D and
  quieter. **Landings** use the same surface query: that surface's step plus a body thump (heavier on a hard landing).
- **Sound spaces**: every 3D sound goes through one attenuation per space (`DA_SoundSet` → Attenuations:
  `SA_Hitmen_World`, `_Footstep`, `_Gunshot`, `_Impact`), with distance falloff and occlusion: a guard's steps are
  heard a few metres away, gunshots far and muffled.

## 7. Weapons
**Juice (toon only, no blood)**: other players' and guards' guns flash at the muzzle (Anime_Stylized `NS_Gunshot_Cast`);
your own first-person gun shows a small soft glow (`M_Hitmen_FlashGlow`) instead, because the toon flash washed the whole
view warm on every shot. The muzzle light sits at the barrel as it is drawn (your first-person gun is drawn nearer the eye) and every machine flashes at its own copy of the gun. It only lives with a muzzle flash: weak (2.5-15 cd, a room lamp is ~8), direct
only (no Lumen bounce or fog), 0.035 s, at most once every 0.09 s; off, it is at zero intensity (never a hidden light that
showing the arms could switch back on). Hits make a toon puff
(`NS_Gunshot_Hit`) on walls and bodies alike; a miss (the shot's range ends in the air) makes none. Tracers
(`AHitmenTracer`, `M_Hitmen_Tracer`, style per weapon in `FHitmenTracerStyle`) show every shotgun pellet and part of the
machine-gun bullets as fast streaks; the sniper draws a brief trail over the whole shot (it fades in 0.12 s), leaves a
white smoke trail along it that widens and thins out over 2.2 s (`TracerStyle.Smoke*`, `M_Hitmen_SmokeTrail` in Project
Settings → Hitmen → Visuals) and throws debris out of the surface it hits (`ImpactBurstFX`). All guns share one recording, pitched and levelled per weapon
(`FireSoundPitch` / `FireSoundVolume`: the silenced pistol is a quiet high snap). Muzzle points are computed from each
mesh (the Weapons_Free guns have no socket). Explosions flash (`ExplosionFlash*` on projectiles; the flashbang is a big
white flash and light), the smoke grenade is the Smoke_Bombs toon cloud (plays 1.8x faster), the jetpack blows smoke
under your feet, ladders and gutters click at each grab, hands landing on a ledge throw dust and a grab sound, slides
scrape, vault landings puff, ziplines throw
sparks, clamp on with a clack (`ZiplineEnter`) and sing one steady cable loop for the whole ride (`ZiplineLoop`, pitch
following the speed; both synthesized in `Tools/Audio/hitmen_synth_sfx.py`), the tank's
barrel slides back 120 cm, its hull rocks and the muzzle blasts, and the turret flashes and fires red tracers. Taking
a hit: a red arc toward the shooter, a colour fringe and a red tint on the view, a small shake; the world greys as
health drops, with the heartbeat below 35 %.

All weapons share ammo, fire rate, spread (fixed: firing never widens it), recoil, damage, headshot
multiplier, noise radius, silenced flag, mesh / visual transform / VFX / SFX / montage / first-person pose
references — every value is in a `DA_Weapon_*` data asset:
- **Damage**: `Damage` per bullet / pellet, `Range` (nothing is hit beyond), full damage up to `FalloffStartRange` then
  linear down to `FalloffAtMaxRange` x damage at `FalloffEndRange`; `HeadshotMultiplier`, and `bHeadshotKills` (on for
  every weapon: a bullet or arrow in the head kills; Project Settings → Hitmen → `bHeadshotKills` is the master switch,
  `bHeadshotKillsFromNPCs` off).
- **Pellets** (shotgun): `PelletCount`, `PelletSpread` / `AimPelletSpread` (cone half-angle, hip / aiming),
  `bFixedPelletPattern` (one in the middle, two rings, a little jitter, instead of a random cone).
- **Accuracy**: `HipSpread`, `AimSpread`, `CrouchSpreadMultiplier` (0.8), `MovingSpreadMultiplier` above
  `MovingSpreadSpeed`; recoil; `AimFOV`.
- **Fire**: `RoundsPerMinute`, fire mode, burst count, minigun spin-up.
- **AI**: `NPCEngageRange` (guards with this weapon close in to it before shooting; 0 = the NPC's own range).

| Weapon | Damage | Rate | Falloff (full → min) | Notes |
|---|---|---|---|---|
| Pistol | 34 | 260 | 15 → 50 m, x0.6 | silenced |
| SMG | 13 | 900 | 12 → 40 m, x0.5 | |
| Assault rifle | 22 | 620 | 30 → 80 m, x0.6 | |
| Shotgun | 25 x 9 pellets (half of them kill a guard; pellets on one target add up into one hit) | 70 | 7 → 25 m, x0.25 | pellets 6° hip / 4.5° aiming |
| Minigun | 16 | 450 → 1100 | 30 → 80 m, x0.6 | spread 2.2° / 1.4° |
| Sniper | 120 | 40 | none (300 m) | zoom, smoke trail | Weapons lying in the world float and turn. The
sniper shows a scope overlay; zoom keeps the mouse feel (sensitivity follows the field of view).

| Weapon | Family | Notes |
|---|---|---|
| Sword | Melee | |
| Bow | Projectile | charge to fire further; 55 damage (two arrows to the body), a headshot kills; arrows find the bone they hit (capsule hits trace on to the body) and count a hit within `HeadshotAssistRadius` (15 cm) of the head as a headshot; the 110 cm arrow (`SM_Arrow`) sticks in what it hits and follows the ragdoll; `SM_Bow` from basic shapes |
| Pistol | Hitscan | silenced |
| SMG | Hitscan, automatic | fast, inaccurate |
| Assault rifle | Hitscan, automatic | balanced |
| Shotgun | Hitscan, pellets | |
| Minigun | Hitscan, automatic | fires at once, ramps 450 → 1100 rpm over 1.2 s (0.4 s grace on release) |
| Sniper | Hitscan | high damage, zoom |
| Rocket launcher | Projectile | explosion breaks walls |
| Grenade launcher | Projectile | bouncing, timed |
| Black hole | Projectile | pulls pawns and physics to a point |
| Goobyr gun | Projectile | a slow lob that bursts into 5 hamsters (`BP_Hamster`, basic shapes): they run to the nearest enemy of the shooter within 20 m and bite (12 every 0.6 s, 10 health, gone after 15 s); a bitten guard is alarmed where it stands; guards may shoot them |

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
client-predicted like sprint and also runs on Space held in the air; the dash (Celeste-like, predicted: 3000 cm/s for 0.12 s with no gravity, then 700, one in the air between landings; `DashSpeed`, `DashDuration`, `DashExitSpeed`, `bAirDash` on the movement component; a view kick, a streak, a whoosh) also runs on a tap of Shift (cooldown 1.5 s); the second jump of the double jump goes 1.35x higher; the blink is Dishonored-like: hold E to aim (a glowing ring on the ground where you land, `MarkerFX`), release or left click to blink, right click / F / Esc cancel without spending it (Esc also opens the pause menu); aiming at a wall within `LedgeUpMaxHeight` (350 cm) of its top lands you on top (an arrow shows over the ring), into the air drops you on the ground below within `MaxGroundDrop` (400 cm); NPCs hunting you lose you if they cannot see where you landed; the
hook climbs (or hangs from) the ledge at its anchor. The hook shows a small marker where it will grab while it is ready and in range, and a thin cable from the hand
to the anchor while it pulls (seen by everyone; `UHitmenAbilityComponent` draws both for any ability that asks).

**Manipulation**: Stone (thrown; it rests where it lands, makes a noise with toon "sound" notes, and a guard who
comes to look picks it up), Remote distraction (sets off a distraction at range; `AllowedTargetClasses`, default the distraction class: drag consoles, panels or breakers in to allow them), Sleep dart
(one target goes limp in a ragdoll with "Z z z" over its head, then stands up), Charm (target becomes an ally and
attacks others; a charmed player is driven by the NPC behavior tree, reckless), Flashbang (stuns around, blinds
players with `PP_Blind`), Smoke grenade (blocks sight for NPCs and players), Lighter (the body burns for `BurnTime` 2 s with fire on it, then ash where it lay; look at a body nearby:
`[A] Lighter`, it burns and is gone), Multi-kill (hold A: every enemy you look at is marked, up to 5; marks glow red
and are linked by a red line; killing one kills all), Trap (catches anyone, you too once armed; NPCs stuck 30 s,
players break free by mashing the move keys), Wind grenade (thrown, bursts on impact and blows everyone around away,
you too), Poison vial (pour in a drink, or in a vent to poison the sealed rooms it serves).

**Remote targeting**: remote abilities (remote distraction, charm, transposition, lighter, multi-kill) pick the best
target within `TargetConeAngle` (8°) of the crosshair and in range, with a line of sight to the middle of it; the
target glows (fresnel) and `[A] action` shows under it. Transposition also swaps with bodies: you stand where the body lies, the ragdoll lies where you stood (both on their floors); nothing moves when either place has no room. Invisibility and the pill show a ring above the crosshair; the invisible look fades in (0.3 s) and out (0.6 s). Picking up an ability
when the slot is taken swaps them (the old one stays on the pedestal). Invisible: a cool, dark-edged, slightly warped
view; armored: steel-blue edges that flash when the armor takes a hit. Abilities can play an `ActivatedFX` where they
act (fire for the lighter, notes for the remote distraction, a swirl for blink and transposition).

## 9. World fixtures
| Fixture | Behaviour |
|---|---|
| Tall grass | Crouched inside = hidden (dark soft vignette and an eye-off mark on screen) |
| Explosive barrel (red) | Explodes when shot (effect, sound, view shake); blasts set off nearby barrels in a chain |
| Ladder / gutter | Automatic climb. `bAllowTopEntry` (shaft ladders): stepping or dropping into the shaft from any side grabs the ladder at its top, and the keys that walked you in take you down until released (`TopEntryReach`: the grab fills the shaft) |
| Zipline | Automatic, up/down |
| Jump pad | Launches high |
| Locker | Hollow cabinet; the door (hinged on its right edge) opens and you step in (0.5 s, turning to face out), the door closes; look through the door slit, turning within 180 degrees; stepping out is the reverse; holds one body (see Hiding spots in §2) |
| Body container | Hollow bin with a lid; hide inside crouched (peek slit, 180 degrees) or stow up to two bodies, visible inside (see §2) |
| Door | Open/close, swings away from whoever opens it; hinged on its +X edge (`bHingeAtPositiveEnd`, the other edge when off); `Width` x `Height` (110 x 230 by default) fill the opening the builder cuts (a wide opening gets a double door: two leaves hinged on their outer edges); optional keycard ID (`BP_Door_Keycard1` blue, `2` red, `3` yellow; `[Locked - Keycard N]` in the prompt). `hitmen_check_doors.py` checks every door fills its opening |
| Window | Frame inside the wall opening, two shutters opening 80 degrees away from the opener; vaultable when open |
| Container | Two-door cabinet; fixed contents or a random weighted pick, items float on its shelves |
| Distraction | Radio, TV, alarm clock (`BP_Distraction_*`: mesh, loop sound, light colour, toon notes on every pulse are data); its sound is one steady loop while it is on (a clip that ends is restarted at once). `F` (or the remote distraction) turns it on or off; once on it plays until switched off (`PlayTime` 0); only guards within its noise radius hear it (walls cut it, the nearest two come), and a guard who comes to look switches it off |
| Turret | `TurretHealth` 80: bullets and blasts break it for good like a camera (sparks, the gun droops, it never fires again, it cannot be hacked any more). Sees and shoots within `Range` (25 m) inside `VisionArcDegrees` (180; 270, 360) around the way it was placed, `Damage` 6, `RoundsPerMinute` 200; red laser while locking on, then shoots players (muzzle flash, light, red tracers); hacked only from its panel by **holding** `F` 3 s (a ring fills around the F key; releasing or looking away cancels): then shoots NPCs |
| Security camera | A shadowed spotlight aimed at the floor: its lit zone is the camera's view, so walls, obstacles and bodies cut it; white while nobody is in view, turning red as its meter fills (`IdleColor`, `DetectingColor`, `AlertColor`); the meter rises gradually (`DetectionTime` 3 s at mid range, x`NearDetectionMultiplier` 1.6 right under it, x`FarDetectionMultiplier` 0.6 at the end of its view) and drains once you are out (`DetectionDecayRate` 0.3/s), so you can brush the cone and get out; seeing someone stops the sweep where it looks, with no tracking, and it sweeps again once they leave the cone (`bFreezeSweepWhileSeeing`); no floor ring; sees you only through open air; a full meter calls reinforcements, who come and **search** (never a fight from the camera alone); on the HUD the meter fills a white arc, then a yellow one after the alarm; a marker on the maps. 30 health: shot to pieces it sparks, hangs dead and its feed is lost for good |
| Security station | Desk + screen cycling through its cameras (a 90° feed, tonemapped at the match exposure, without the zone pattern); holding `F` 2 s disables / enables them: the screen keeps the last frame, dimmed and noisy, with NO SIGNAL over it |
| Circuit breaker | A lever thrown with sparks and a clunk turns off its linked lights (the bulbs go dark too): NPCs cannot see (only hear), players see less |
| Light switch | A small breaker by a door (`BP_LightSwitch`, info "Light switch - On / Off"): `F` switches the room's lamps; every lamp shows "Light - On / Off" when looked at. The builders put one by the door of every building |
| Weapon chest | A low dark container (`BP_WeaponChest`, a `BP_Container_Random` child) whose random pool is the long guns (SMG, rifle, shotgun, sniper) |
| Vent | Crouch-size duct (120 x 120 inside; crouched you are 110 cm); its hatches are small doors (vertical hinge on the side) that swing away from the opener; poison spreads to linked rooms only if fully closed. Builders: `vent_duct()` / `vent_hatch()`, `building(vents=[(face, t, floor)])` for a hatch in a wall at floor level |
| Poisonable drink | Poison vial target |
| Breakable wall | Breaks on any explosion (weapon, C4, tank): dust over its face (`BreakFX`), a crumbling sound (`BreakSound`, else `EHitmenSound::WallBreak`) and chunks thrown out (`DebrisCount`, gone after `DebrisLifetime`) |
| Mounted machine gun | A free-standing tripod (no pedestal: place it anywhere), the gun high on it; usable from any side (`bUsableFromAnySide`), `YawArcDegrees` 180 (270, 360 = all around), `Damage` 18, `RoundsPerMinute` 660, `Range` 80 m, look speed matches the player's; the view sits above the gun and the shield plate sits low, so the barrel shows; bullets go where the crosshair points (traced from the view, fired from the muzzle); the camera uses your field of view; every shot has a sound, a muzzle light flash and a view shake; takes damage, part of it reaches the gunner |
| Tank | Enter, third-person view; health bar at the top of the HUD while driving; shells go where the crosshair points (the view line is traced from the camera behind the tank); a parked, empty tank takes no damage (`bEmptyVehicleTakesDamage` off), so it is always whole when you get in; a reload ring around the crosshair (1.25 s), `[F] Exit` bottom right; its cannon barrel follows the aim, slides back 120 cm and the hull rocks on a shot; the shell is a glowing slug that flies straight and near-instantly (no gravity, 20000 cm/s) and blasts 12 m wide; fire sound, muzzle blast, view shake; server-driven from any client; damageable (a driven tank belongs to its driver's team; its own shells never hurt it or its driver), driver ejected when destroyed |
| Teleporter | Fixed pair: in → out |
| Retrieval point candidate | See §2 |
| Weapon / item spawn candidate | See §2 |
| Keycard pickup | Grants keycard ID |

## 10. AI
All NPCs are hostile: the whole map is what Hitman calls a hostile area (no disguises, no civilians). Within that, the
NPCs behave like Hitman's: graded vision, "Hm?" then a walk over, shared knowledge, bodies called in once, evidence
confiscated, on-edge guards after an alert, cover, VIP evacuation and barks for every change of awareness. Sight takes priority over hearing. Sight range 1950 cm (lose at 2250), times the
difficulty multiplier. How fast the white fill rises depends on the distance (`SuspicionRateByDistance`, a curve on the alert component): near instant point blank (about 0.15 s at 2 m), 0.7 s at 5 m, 1.7 s at 10 m, 4 s at 20 m (before peripheral vision, cover, alarming behaviour and difficulty); red at once within `InstantDetectionDistance` (150 cm). **Sixth sense**: a hostile within `SixthSenseRadius` (250 cm) but out of view is heard (never through a wall) while it walks or runs: the white fill rises at `SixthSenseRate` (0.5/s walking, x`SixthSenseSprintScale` 2 sprinting), never to red, and the NPC turns to look. Crouched (moving or not) or standing still, you are never heard or felt from behind (`SixthSenseCrouchScale` 0): NPCs never look behind them, which is what lets you sneak up for a takedown or a pickpocket. Console `HitmenAI.Cones 1` draws every NPC's focal and peripheral cones and its sixth sense ring. **Facing**: the body always follows the AI controller's rotation; walking, a focus more than `MaxStrafeAngle` (100°) away from the walking direction gives way to it, so no NPC ever walks backward. Guards in combat hold their
weapon's aim pose and play its fire montage on each shot. Searching guards draw their gun (the weapon's equip
animation) and hold it ready (its aim pose) each time they stop to look around; a hit plays a hit reaction. All of
these are the Mannequin pack animations (pistol / rifle equip and ADS idle, `MM_HitReact_Front_Lgt_01`), seen on
every machine. They fire like Hitman guards: a short raise
(`NPCFirstShotDelay` 0.6 s), bursts of `NPCBurstShots` (3) at 2.5x the weapon's interval, a 1.2 s pause; low
damage multiplier per difficulty (Easy 0.35 / Normal 0.55 / Hard 0.8).

**Snipers** show a small red star at their scope only while they aim at someone they see (`bGlintOnlyWhileAiming`, on; `ScopeGlint`, `M_Hitmen_Glint`, the same size at any distance), and a red line from the scope to whoever they aim at,
about two pixels wide at any distance (`AimLaserScreenWidth`).

**In combat** guards hold their ground: within `ShootingRange` (15 m, on the NPC; snipers unlimited) they stand and shoot,
farther away or out of sight they walk closer until in range, and they never back away. They turn to face their target
(strafing while they move) instead of facing where they walk. With you in sight they take **cover** when they find some
(`UBTTask_HitmenFindCover`: a reachable point within 8 m and inside their range that hides a crouched guard from you but
lets it shoot standing), duck behind it during the pause between bursts and stand up 0.35 s before the next one.
Unarmed NPCs only fight within `UnarmedReach` (1.6 m).

There are no civilians: every NPC is a guard (patrol, stationary, sentry, sniper) or a VIP candidate, and at match start every candidate not picked becomes a plain armed guard with the normal guard behaviour (`CandidateGuardWeapon`, else its `DefaultWeapon`, else Project Settings -> Hitmen -> AI -> `DefaultGuardWeapon`, the pistol; named "Guard"; the VIP reads "Target").

**Hands and hips**: an NPC carries things in three places, its hands and its left and right hips. A pistol (weapon data
`bCanHolster`) rides in the hip holster (`HolsterSide`, right) of a guard set to `WeaponCarry` Holstered (default, every
patrol): it stays there while the guard is calm, is drawn for real threats (gunfire, a body, a search, a fight, on edge)
and goes back once it calms down. Guards on duty (stationary posts, `WeaponCarry` InHands) and long guns stay in the hands.
A keycard rides on the other hip. From behind (or beside, crouched) `F` pickpockets what is on a hip: the keycard or the
holstered pistol (the hip nearest to where you look when both are there; "Pickpocket keycard 2", "Pickpocket Silenced
pistol"); a pistol in the hands cannot be taken. A guard robbed of its pistol fights with its fists. `bGuest` (off in every map) makes an NPC a neutral guest: unarmed, nobody's enemy, it runs from danger.

**VIP evacuation**: an unarmed VIP in danger (combat, a search, or a real threat being investigated: gunfire, a body)
runs to the nearest `AHitmenVIPSafeSpot` (placed by LD; with none placed, a reachable point far from the threat and out
of its sight) and waits there, looking around, until the alert ends. Its 2 nearest armed guards are sent to the spot as
escorts (at most every 20 s).

**Two NPC types** (`Behavior` on the NPC, "Hitmen|NPC|Behavior"):
- **Patrol**: walks its `PatrolRoute` (stands at its post if it has none); an open route (`bClosedLoop` off) is walked
  back and forth, and a walk interrupted by an alert resumes the point it was heading to. Routes can be **curved**
  (spline through the points). A **patrol group**: list several NPCs in a route's `AssignedNPCs`
  (or give them the same `PatrolRoute`) and they walk it together (`bAssignedNPCsPatrolAsGroup`, on): the first leads
  along the route, the others follow in rows behind it (`GroupFollowDistance` 1.7 m, `GroupFollowSpacing` 1.4 m,
  `GroupColumns` 2), the leader slows to half pace while a follower lags more than 2.6 m behind its slot, and alerts are
  shared inside the group. A placed **guard group** (`BP_GuardGroup`, `Members`, `Columns`) does the same for any NPCs.
- **Keycards** sit on the hip opposite the holster (`KeycardHipOffset`, mirrored); from behind an unaware carrier, `F`
  pickpockets it (before any kill; `bPickpocketRequiresCrouch`, off, also asks you to crouch).
- **What you see**: looking at a guard up close shows its name, weapon and keycard under the prompt ("Guard - Shotgun -
  Keycard 2"; the VIP is "Target"). **Loadouts**: `DefaultWeapon` per NPC (placed instances too) and ready
  Blueprints `BP_NPC_Guard` (silenced pistol), `_SMG`, `_Rifle`, `_Shotgun`, `BP_NPC_Sniper`; a guard closes in to its
  weapon's `NPCEngageRange` (shotgun 9 m, SMG / pistol 14-15 m) before shooting.
- **Stationary**: stands at its placement, facing the way it was placed; after an alert it walks
  back and turns to that direction again.

Options on any NPC:
- **React To Noises** (on by default). Off = only reacts to what it sees and to being hurt:
  footsteps, gunshots, distractions, radio calls and bodies are ignored.
- **Hold Position** (Stationary only). Never leaves its post: shoots what it sees, turns toward
  attackers, searches and investigates by looking around.
- Stationary + React To Noises off + Hold Position = a **sentry** (manor gate in LDTest, gym pen).

Whoever sees: one shared sight test (`UHitmenAISubsystem::CanObserverSee`: range, cone, walls,
smoke, darkness, invisibility) drives perception, kill witnesses and body sightings (a body is seen where its ragdoll
lies).

**Vision** (`AHitmenAIController`, "Hitmen|AI|Sight"): the white fill rises at full rate in the focal cone (±30°) and at
0.35x at the edge of the view (65°); head, chest and pelvis are traced, so a body half hidden behind cover fills slower
(0.4x with one part in view). Alarming in view (2.5x): a weapon in hand, sprinting, climbing, vaulting, hanging,
sliding, crouch-walking.

| State | Meaning | Indicator |
|---|---|---|
| Unaware | Routine (patrol, idle) | none |
| White | Saw something, not alerted yet | white |
| Yellow | Alerted, investigating | yellow |
| Red | Found you, attacks | red |
| Yellow (search) | Lost you, searches likely spots | yellow |

- **White → yellow** (Hitman's "Hm?"): a white NPC stops and turns toward you; if it loses you after its fill passed
  `InvestigateSuspicionThreshold` (0.15) it goes yellow and walks over to where it saw you; below it, it cools down.
  An investigation's timer starts on arrival (within 2.5 m), a walk that never arrives gives up after 30 s. Red when
  the fill completes while it sees you, when shot, when it witnesses a kill, or was already searching for you; gunfire
  heard sends it (yellow, gun drawn) to where the bullet landed.
- Losing sight for a while → search state, driven by **EQS** (`EQ_HitmenSearch`, run by `BT_Search` with the
  random-best-25% mode): candidates from `UEnvQueryGenerator_HitmenSearchSpots` (hiding spots — lockers, body
  containers, tall grass, actors tagged `HitmenHidingSpot` — seen from their front, the search center, random reachable
  points), reachable only, scored by: not claimed by a colleague (`UEnvQueryTest_HitmenSearchFree`, x2), in front of a
  hiding spot (x1.5), hidden from the search center (x1), hidden from the guard (x0.8), close to the center (x1). The pick
  is claimed so NPCs split up. The search center (`UEnvQueryContext_HitmenSearchCenter`) is the last known position, or
  the body found. The query is generated by `UHitmenEditorTools` (edit the builder, not the asset).
- **Alerts end**: a repeated radio call or noise about the place an NPC already investigates keeps its running timer
  (`SameInvestigationRadius` 4 m), so every alert runs out (repeated calls used to restart it for ever).
- **Last known position**: where an NPC last **saw** you. A mannequin ghost (`BP_LastKnownGhost`) is shown there while
  at least one NPC searches for you; never on your corpse. Noise never reveals your position: NPCs go to the sound.
- **Hearing**: walking is heard within 4 m, sprinting 9 m, crouching not at all (height within one storey does not
  count). A guard first **turns toward what it heard** ("Hm?", `UBTTask_HitmenTurnToNoise`, 1 s) then walks over. Every
  sound is checked from a **reachable spot** (`UHitmenAlertComponent::ResolveSoundSpot`): a step back off the wall it hit,
  down to the floor, onto the navmesh, and the end of the path when it cannot be reached, so guards go around walls and
  through mazes instead of stopping at the nearest wall. Each wall in between cuts the radius again (x0.45 per wall,
  more than 3 walls: silence), and only the `MaxNoiseResponders` (2) nearest NPCs that would react respond (3 for
  gunshots, `NoiseResponderOverrides`).
- **Gunshots are heard close by only** (`UHitmenAISubsystem::ReportShot`): a bullet passing within `NearMissRadius`
  (3 m, match rules) of an NPC, or landing that close, is a **near miss**: it always reacts, whatever the walls; where
  it lands is heard within the weapon's `ImpactNoiseRadius` (silenced pistol 3 m; a silenced bullet into a body 1.5 m);
  a loud report carries per weapon (`NoiseRadius`: SMG 20 m, rifle and shotgun 25 m, minigun 30 m, sniper 35 m;
  silenced: not at all). The guard walks to **where the bullet landed** (a near miss that flew on: where it whizzed by;
  a report heard from afar: a point down the shot), never to the shooter, and only if it does not already see the
  shooter (then sight decides). **Three close shots** within `ShotsHeardWindow` (20 s, `PanicShotsHeard`) make it panic:
  it hides crouched from where the shots seem to come from (`PanicDuration`), then searches. Hearing sends NPCs to the
  sound; only in the dark does a hostile heard within 2.5 m get attacked (`DarkCloseEngageDistance`).
- **Search first, then a fight**: a fight against someone the NPC has not seen yet (shot at from nowhere, colleagues
  dropping, panic) shows as a **search** on every HUD (yellow indicator, minimap, crosshair arcs, "SEARCHING"); red and
  **UNDER FIRE** only once it has seen you (`GetDisplayAlertState`, replicated `bTargetSighted`).
- **Shot by an unseen shooter**: the NPC ducks into cover from where the shot seemed to come from (a guess along the shot) for `UnseenThreatCoverDuration` (6 s), crouched, then searches there; it never learns the shooter's real position without seeing them. **Colleagues dropping**: an NPC that sees (or hears, within `AllyDeathHearRadius` 8 m) a colleague die without seeing the killer takes cover the same way ("Get down!"); after `PanicAllyDeaths` (2) within `AllyDeathWindow` (30 s) it panics ("They're picking us off!") and hides crouched for `PanicDuration` (12 s) before searching; its radio call waits until it stops hiding (`BT_Combat` hide branch, BB `TakeCover`, `UBTTask_HitmenHoldCover`). **Teleports**: a hunted pawn moving `LoseAggroTeleportDistance` (6 m) or more at once (teleporter, blink, swap) is lost by every NPC that cannot see where it went: they search where it was; an unseen target is always faced and flanked where it was last known, never where it really is. **Explosions** are heard by four guards (`NoiseResponderOverrides`), carry through walls better (`ExplosionOcclusionMultiplier` 0.8) and barrels are heard 40 m away (`ExplosionNoiseRadius`); the responders walk to the blast with their gun drawn.
- **Bodies, distractions, weapons** are investigated. Only sentries ignore bodies. Whoever finds a body walks to it,
  then **searches around it** (`BodySearchDuration` 20 s, the EQS search centred on the body), then calms down (on edge)
  and **carries the body to the nearest body container or locker with room** (`bNPCsDisposeBodies`,
  `MaxBodyDisposalDistance` 60 m along the navmesh; `BT_DisposeBody`): it stows it there, where it stays visible inside.
  A sniper or a guard holding its post gives the job to the nearest guard that can walk. A guard carrying a body puts
  its gun away; any alert makes it put the body down. **The VIP's body is never carried by a guard.**
- **Shared knowledge** (Hitman's "services", `UHitmenAISubsystem`): a body (or a sleeper) is reported once map-wide,
  the finder calls it in and the others come because of the call; searching and fighting NPCs read their target's
  latest sighting by any colleague every second; a guard group shares what the member that raised it knows (never your
  real position) and only for real threats (a footstep stays that guard's business). Radio calls send the responders to
  where the caller knows something happened (your last seen spot, or the body / noise it investigates).
- **On edge**: after a search or a real threat, NPCs go back to their routine nervous for 60 s: they notice you 1.5x
  faster, walk briskly (240 cm/s), keep the gun ready and look around at each patrol point instead of idling.
- **Sleepers**: a colleague asleep (sleep dart) is walked to and woken.
- **Evidence**: a weapon lying around after someone carried it (thrown, dropped, a dead guard's; spawned loot is not)
  is noticed within 12 m by a calm NPC, which walks over and takes it away (one guard per weapon; nothing to take if a
  player picked it up first).
- **Barks**: every change of awareness is said out loud (`DA_Barks`: lines per reason, optional voices per line;
  `Juice_OnBark` on the alert component). Every machine derives it from the replicated state and reason (no RPC);
  local players within 15 m get a subtitle bottom centre, the speaker in its alert colour, at most 2 lines.
- **Squad tactics**: alerted NPCs take slots around the player; when the fight spans floors one
  flanker takes another floor / the stairs while another pushes directly; snipers keep far, high
  slots.
- **Reinforcement call**: an alerted NPC that is not shooting at a visible target calls (icon
  above its head); if not interrupted by damage before the timer ends, NPCs in a radius are
  alerted. NPCs alerted by a call do not relay it (`bReinforcementCallsRelay`). Cameras call too.
- Behavior Tree with subtrees: `BT_NPC` → VIP evacuation (top, `Evacuate` key), `BT_CallReinforcements` (aborts on
  sight), `BT_Combat` (→ `BT_Sniper`, hold-position branch, cover then flank), `BT_Search` (EQS), `BT_Investigate`
  (turn to the noise, walk over, look around), `BT_DisposeBody` (calm, `DisposeBody` set), `BT_Guard` (stationary),
  `BT_Patrol` (on edge: a look around at each point). "Target visible" decorators abort lower branches immediately.
  Charmed pawns (players included) reuse the search/combat branches. Trees are generated by
  `Tools/Editor/hitmen_build_ai.py` (headless, see CLAUDE.md).

## 11. HUD
Font: Barlow Condensed (`FNT_Barlow`). Painted in code (`HitmenHUDElements`, `HitmenHUDWidgets`).
- **Compass** (top): N/S/E/W only (`bShowIntercardinals` off); optional VIP marker; retrieval
  directions while carrying.
- **Inventory bar** (bottom centre): four cards of one size — the held weapon on the left, the other beside it (switching makes them trade places in 0.12 s, `SwapTime`), a 3 px divider, the manipulation and move abilities with the key that triggers each in the top-right corner (as bound on this keyboard: A / E on AZERTY, Q / E on QWERTY, Space for the jetpack, Shift for the dash; `UHitmenAbility::GetHUDKey`); an empty ability slot shows no key. The agent screen's ability rows and the debug tools show the same keys. Each card shows the item's icon and full name (the font shrinks to fit), weapons their
  ammo as `current / max`, abilities a cooldown shade that shrinks as they come back, charge pips and a fuel gauge for
  the jetpack. No "ready", no health bar. The bar is centred on the screen.
- **Minimap** (bottom left, square): the baked map, north up (your arrow turns, no view cone), every NPC as a diamond
  (dark grey calm, white suspicious, yellow alerted, red in combat), sonar pings, map markers (cameras, retrieval points
  pinned to the frame when out of range);
  its frame takes the colour of the worst alert aimed at you and a banner opens across its top with a white flash
  (Suspicious, Alerted, Searching, Spotted; **Under fire** is taller, red and pulses, and only shows once an NPC fighting
  you has seen you).
- **Crosshair**: a thin "+"; hit ticks (red, longer and thicker on a headshot, with a metallic hit sound only the shooter
  hears); an "x" on a kill (red on a headshot);
  Hitman-like awareness arcs around it, one per guard or camera noticing you, placed toward it: the arc fills from its
  middle as suspicion grows (white), then turns yellow or red; cameras add a small camera mark.
- **Timed effects** (invisibility, pill, armor): a small ring each above the crosshair, emptying clockwise as it runs out, blinking in the last 1.5 s.
- **Throw**: while aiming with nothing in focus, a small plain "F  Throw" bottom right (no key cap).
- **Prompts** under the crosshair: `[F] Open  [Locked - Keycard 1]`, `[SPACE] Vault` (the keys as bound); remote-ability
  targets glow with `[A] Charm` under them; holding F fills a ring around the key. A smaller info line under the prompt
  says what you look at and its state (`GetInteractionInfo`, the fixture's `DisplayName` by default); something with info
  is highlighted and shows it even when it cannot be used now (then without a key). Keycards are chips bottom right
  (above `[F] Exit` while driving).
- **Damage**: red edges on each hit, a breathing red glow and a heartbeat below 35 % health.
- **Scope** overlay for the sniper. **Sightroom** (Tab): a fast scan wave and a floor grid around you, then the whole map in a cool look;
  highlights with a thin outline.
- **Alert indicators** above NPCs (a hatched diamond: white and filling from the bottom while suspicion builds, solid
  yellow or red once alerted; it pulses while the NPC calls for backup) and around the crosshair.
- **Pause menu**: **PAUSE** and three big square blocks on the left (Resume, Options, Leave match; the host gets
  **Back to the lobby** once the match is over); the baked
  schematic in the middle (north up, dimmed under the markers; the wheel zooms toward the cursor, a left drag pans, nothing
  is drawn outside the map square); by default it shows **you, the VIP and the retrieval points** (and your teammates
  when you have some). The legend on the right lists **only what the map draws** and toggles each row with its exact
  colours (you, teammates, VIP, guards in grey / white / yellow / red like the minimap, then one row per map marker kind:
  retrieval points, cameras, hiding spots...). Debug tools (dev builds) open over the legend from a small "Debug tools"
  button; the cheat panel hides while the pause menu is open.
- **Map layers** (Project Settings -> Hitmen -> Maps, editor variables): `bMinimapShowNPCs` (on: NPCs and cameras),
  `bMinimapShowVisionCones` (off), `bMinimapShowInteractables` (off), `bPauseMapShowNPCs` (off),
  `bPauseMapShowVisionCones` (off), `bPauseMapShowInteractables` (off), `bPauseMapShowTeammates` (on),
  `MapVisionConeOpacity`, `MinimapNPCSize`, `PauseMapNPCSize`. A **vision cone** is a fan in front of the NPC over its
  sight radius and peripheral angle, fading out toward the edge (a dark shade while calm or suspicious, yellow / red once
  alerted). The **Interactables** layer: hiding spots (lockers, body containers), distractions, containers, keycards,
  security devices (consoles, hack panels, breakers) and hazards (poisonable drinks, explosive barrels).
- **Cheats**: a toggle on each game mode, `CheatAccess` (Never / DevelopmentBuilds / Always): the gym is Always (Shipping
  included), the match DevelopmentBuilds (Development and editor builds; Shipping only with `bAllowCheatsInShipping`).
  Where they are on: the pause menu's debug tools (any weapon, ability, keycard) and the panel on the right, centred
  vertically, a lit square when on (also on the spectator screen while you are dead):
  `F1` invulnerable, `F2` infinite ammo, `F3` no cooldown, `F4` invisible to AI and cameras, `F5` respawn every NPC as
  placed (class, place, behaviour, route, group, keycard: recorded at start), `F6` kill all NPCs, `F7` reload the map,
  `F8` fly through walls (look to steer, `Space` / `Ctrl` up / down, `Shift` x3, `CheatFlyFastMultiplier`), `F9`
  **revive**: dead, you stand up where you died with your loadout (also the console command `Revive`). Server-checked
  (`AllowsDebugCommands`); the engine's F1-F5 view modes and F9 screenshot are cleared in `DefaultInput.ini`.
- **Map markers**: anything with a `HitmenMapMarker` component shows on the minimap and the pause map, drawn and named
  by its kind (a `DA_MapMarker_*` data asset: legend name, colour, shape or icon, size, which maps, legend order, and
  its **layer**: Objective always drawn, NPC or Interactables per the map layers above). Every interactable has a
  `MapMarker` component: pick a kind on its Blueprint and it joins the Interactables layer.
  Recipe in `docs/Pipelines.md`.
- **Intro**: "Dropping in" and the map name during the parachute drop (no countdown, no sting).
- **Match events** (`UHitmenMatchEventsWidget`, with or without a pawn): toasts under the compass, the event camera top
  left (title above the picture), letterbox bars during the end cinematic.

## 12. Maps
| Map | Purpose |
|---|---|
| `L_FrontEnd` | The whole front end in one scene: museum hall (title), briefing room (create room), agent stage (`BP_AgentPreview`, "Your agent"); one tagged camera per screen |
| `L_Gym` | Debug gym: a compact walled hall (74 x 74 m) laid out like a mini LD — movement course with the traversal metrics, a numbered **parkour run** over rooftops (vault, gaps, mantle, jump and grab, beam, ledge grab onto the gutter tower, zipline north with room in front of the post and a 5 x 12 m landing, step-ups, plank, an overlook above the AI pen); the ladder block's ladder is behind it, reached by a gold path on the right under a five-step stair up to the zipline tower; distractions (radio, TV, alarm clock), a free-standing mounted gun, a body container by the AI pen, armoury (weapons on pedestals, containers, loot points), abilities, fixtures (a sealed room reached from the AI pen by a vent hatch and a crouch duct), and an AI pen with one of each kind, labelled (a static sentry, a stationary rifleman with a radio to switch on, a lone patroller on a curve, a patrol group of four on one curved route — shotgun, SMG, pistol, rifle —, a keycard guard to pickpocket, the sniper with its red star, the VIP candidate, lockers and grass to search, the camera alarm; `hitmen_levels.gym_ai_pen`, applied by `hitmen_patch_090` step `gym_ai`); every station is labelled just above itself; the HITMEN GYM title floats 11 m up over the middle of the hall, facing the spawn |
| `L_LDTest` | **San Lazaro**, one organic map on hills, about 240 x 210 m playable (`hitmen_sanlazaro.py`; the ground is a heightfield of 1 m cells, tiles tagged `HitmenTerrain`). Outside an irregular outline the ground rises into mountains (invisible walls on the outline). **Favela** (south on the minimap): five terraces up a hillside, small houses of 1 to 3 floors in rows along paved alleys, cross alleys with stairs up to the next terrace behind retaining walls, yards with low walls, roofs one floor high reached by a plank from the terrace above, planks between roofs of the same height; every facade has vaultable windows and a ledge band under them (hang and shimmy); a market with stalls and the radio, the alarm clock in an alley, a body container, street lamps on a breaker (keycard kiosk), a bell tower with a sniper. **Dry ravine** (across the middle, no water): rocks in its bed, two stone bridges arching from rim to rim, a high walkway between two watchtowers. **Mansion estate** (north) on a plateau, its grounds about 40 % smaller: the main gate with gatehouse, console, camera and a mounted gun on its tripod, a keycard side gate, a staff gate toward the ridge; the same three-floor house (rooms on both wings, a hall with the switchback stair, the sealed dining room with its vent hatch, the TV in the lounge, the keycard control room, the basement with the breaker and the turret panel down an outside stair well), a small hedge maze with a gazebo, the greenhouse, the garage with the tank, a body container, the helipad on the roof. **Ridge and cave** (east): a rocky ridge with a cave dug into its flank (an entrance under an overhang, three chambers under the rock linked by passages, lit, the den with a mounted gun), a stair well in the last chamber down to the tunnel that ends on the mansion basement stairs. Every room has two lamps (lumens; one per room casts shadows in the estate and the cave); patrols in every alley, the estate and the cave, two guard groups, posts, 3 snipers, VIP spots in the three zones, 6 retrieval points, 6 start groups near the edge  **0.9.0**: walkable window ledges on every upper floor, gutters on the alley side of every village house of two floors or more, wood floors in the village, tile in the mansion, metal in the garage, sand in the greenhouse; a crawl vent from the alley into one house per row; the mansion's double front door and a high vent (hatches 2.8 m up in both wing walls, a duct under the hall ceiling, a crate under each hatch); the **smugglers' tunnel** from the village kiosk under the ravine and the plain to the gatehouse, with shafts and ladders in the kiosk, the ravine bed, the plain and the gatehouse and a keycard 3 door before the end; two vent shafts down into the cave chambers; keycard doors 1 (control room, side gate, kiosk), 2 (garage side door) and 3 (tunnel), carried by guards and lying on a market table, the cave den table and the bell tower; weapon chests (market, gatehouse, garage, cave den); lockers and body containers in every zone; a light switch in every building; about 70 tall grass fields; guards with pistols, SMGs, rifles and shotguns; patrol groups of 4 (estate perimeter) and 3 (front garden, market); retrieval models: helicopters (helipad, north plain), cars, crates. **0.10.0**: shaft rims are 45 cm with a 50 cm coping (a step you walk onto), the shaft ladders catch you from above (cave vents, tunnel shafts); every crawl hatch has a real duct outside the wall with a hatch at its end (2 m into the alley in the village, 4 m onto the lawn for the dining room); the ravine bed's rocks are flat-topped blocks on a parkour line at the gym metrics (a 45 cm step, a 220 cm climb block, a second one 1.5 m beyond to jump to, 100 cm vault blocks between the groups); turrets in sight on the gatehouse roof (the gate courtyard) and in the room before the control room's keycard door, besides the roof one. `hitmen_check_features.py` lists every placeable in the map (missing 0) |
| `L_Empty_01`, `L_Empty_02` | Empty maps for the team |

Every match map has one fixed exposure (`MatchExposure`, an unbound post process volume, EV100 -0.3): no eye
adaptation, so the image never drifts when you land, enter a room or leave a cave. Rooms are lit by light fixtures.

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
  Client requests are checked: a shot must start within `MaxFireOriginError` (150 cm + lag) of the shooter's eye and
  go within `MaxFireAngleError` (30°) of where the server sees it look, never from a dead or stunned shooter; loadouts
  and cosmetics must come from `DA_Catalog`; interactions are range-checked. Cheat flags replicate to their owner
  only; vehicle aim is sent at most 20 times a second and only when it moved; projectile impacts and blasts are a
  reliable multicast (the projectile is destroyed right after).
- **Data-driven tuning (GD tools).** `Project Settings → Hitmen` (`UHitmenGameSettings`) and
  data assets (`DA_MatchRules_*`, `DA_Weapon_*`, `DA_Ability_*`, `DA_Map_*`, `DA_AnimSet_*`).
- **Framework Blueprints.** `BP_GameInstance` (Options volume mix), `BP_GameState`,
  `BP_PlayerState`, `BP_SpectatorPawn`, `BP_PlayerController` and the `BP_GM_*` game modes.
- **Every text from one sheet.** Player-facing text is `HITMEN_TEXT("<Area>.<Key>", "<English>")`, read from
  `Content/Hitmen/Localization/HitmenText.csv` (Key, en, fr, ...) by `UHitmenTextSubsystem`; data-asset names are
  matched by their English (`Asset.*` rows). A new column is a new language. Recipe in `docs/Pipelines.md`.
- **Level design structures.** `AHitmenLDStructure` children (`World/LevelDesign`): block, wall, stairs, building,
  vent duct, built from boxes in three instanced meshes (walls, floors, trim; tagged `HitmenLD_*` for PCG) with their
  doors, windows, hatches, gutters and lamps as real attached actors, so gameplay is the same as the builders'.
  `AHitmenLDPCGLine` runs a PCG graph along a spline. The "Hitmen LD" tab places them; recipe in `docs/Pipelines.md`.
- **Animations in one place.** `UHitmenAnimInstance` exposes state; a `DA_AnimSet_*` lists every
  sequence, blend space and montage so ABPs swap animations without code.
