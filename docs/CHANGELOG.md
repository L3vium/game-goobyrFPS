# Changelog

All notable changes to Hitmen. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions before 0.1.0 belong to the Unity prototype (see git history before the Unreal port).

## [0.7.0] - 2026-11-12

### Added
- San Lazaro rebuilt as one organic map on hills (about 240 x 210 m): a favela on five terraces (houses of 1 to 3
  floors, alleys, stairs, yards, roofs reached from the terrace above), a dry ravine with two arched bridges and a high
  walkway, the mansion on a plateau with grounds about 40 % smaller, and a real cave dug into a ridge with a tunnel
  to the mansion basement. The ground is a heightfield (`UHitmenContentTools::BuildHeightfieldMesh`).
- Every facade of a generated building has vaultable windows and a ledge band under them from the first floor up.
- `hitmen_check_patrols.py`: every patrolling guard has a route (or a group leader) and walks.
- Cheats on F1-F8 with a panel on the right (gym always, dev builds elsewhere): invulnerable, infinite ammo, no
  cooldown, invisible to AI, respawn NPCs as placed, kill all, reload the map, fly.
- Gym: ladder behind the ladder block with a path on the right and a stair up to the zipline tower; room in front of
  the zipline start and a 5 x 12 m landing; TV and alarm distractions, a body container; the mounted gun on its tripod.
- Options rebuilt: a centred panel with Gameplay / Controls / Graphics / Audio tabs, a description column, `[Esc] Back`
  and `[R] Defaults`; Escape closes them from the title too. Controls by section with conflicts in red; graphics
  presets, auto-detect (also on the first launch), resolution, window mode, vsync, frame cap, render scale and every
  quality; an Interface volume.
- Match end without a timer: a result banner, everyone flies free, the host goes back to the lobby with Enter (or the
  pause menu); the others wait for the host.
- Free fly like the Unreal editor (ZQSD, Shift faster, Space / A up, Ctrl / E down) with its own rebindable keys,
  kept inside the map bounds and under a ceiling.
- Watchers: a watching host gets Start; alone, the match starts at once; in "Your agent" they see who is ready.
- "3 / 8" counters under the agent's look arrows.
- Vehicle HUD: health bar at the top, reload ring around the crosshair, `[F] Exit` bottom right; keycards moved bottom
  right above it.
- Security cameras can be shot to pieces (30 health): sparks, the head drops, the feed is lost for good.
- Distraction variants (radio, TV, alarm clock) as data-only Blueprints with toon notes on every pulse.
- Carry any body in your arms (it takes the left weapon slot; no shooting while carrying; F or the wheel puts it
  down); hide bodies in a body container (BP_BodyContainer, 2 per container by default; never the VIP).
- Remote abilities target the best candidate in an 8° cone with a line of sight (fresnel + `[A] action`): the remote
  distraction works at range again; transposition swaps with bodies too.
- Lighter: `[A] Lighter` on a body you look at nearby; it burns (toon fire) and is gone.
- Multi-kill: hold A to mark up to 5 enemies in view one after another (red glow, red links); release confirms.
- Stone rests where it lands with a toon "noise" effect; a guard who comes to look picks it up.
- Wind grenade (the wind mine, now thrown): bursts on impact and blows everyone away, the thrower too.
- Trap catches anyone, you too once armed; players mash the move keys to break free.
- Sleep dart: the target goes limp (ragdoll) with "Z z z" over its head, then stands up.
- Picking up an ability with the slot taken swaps them; screen effects while invisible and armored.
- Toon weapon juice (no blood): muzzle flash (Anime_Stylized), muzzle light and soft glow, toon impacts on walls and
  bodies, tracers (AHitmenTracer) for shotgun pellets, sniper shots and part of the machine-gun bullets, explosion
  flashes (AHitmenFlashBurst; the flashbang is a big white flash), jetpack smoke, ladder / gutter puffs and grab
  sounds, zipline sparks and cable sound, tank muzzle blast / hull rock / 120 cm barrel recoil, turret flash and
  red tracers.
- Taking a hit: a red arc toward the shooter, a colour fringe and red tint, a small shake; the view greys with low
  health.
- First-person motion, procedural and per weapon (`FHitmenFPMotion` in DA_Weapon_*): camera head bob with the
  footstep on each low point, mouse sway, walk lag and strafe tilt, a fast equip from below, a spring kick per shot,
  a spring hip <-> aim travel toward the centre, the sniper's raise / wobble / zoom sequence with a breathing aim,
  alternating punch jabs, knife slash, throw wind-up, takedown lunge, minigun shake, bow draw, and a pull-back near
  walls. Body montages no longer play on the first-person arms (no torso in view when punching).
- Thrown weapons really fly and spin; a head hit stuns 3 s; an empty gun vanishes after its throw.
- UHitmenDebugLibrary: Start / Stop / Inject Enhanced Input actions from editor scripts (QA).
- Hang: thin ledges (the ledge under every window, roof edges) can be hung from with Space in the air; shimmy left /
  right (Shift faster), Space climbs into the window above or onto the ledge, Ctrl lets go. Vaulting out of an upper
  window over a drop hangs from the ledge under it.
- `HitmenNoTraversal` actor tag: barrels, hack panels, turrets, cameras, mounted guns, the tank and distractions are
  never vaulted or climbed.
- Sound spaces: every 3D sound goes through an attenuation per space (SA_Hitmen_World, _Footstep, _Gunshot, _Impact
  in DA_SoundSet), with falloff and occlusion. Guards' steps across the map are no longer heard; gunshots carry far
  and are muffled with distance.
- Headshots always kill (bullets and arrows), Project Settings -> Hitmen -> Combat (`bHeadshotKills`; NPC shots are
  excluded by `bHeadshotKillsFromNPCs`).
- NPC `ShootingRange` (15 m): guards only shoot within it and walk closer when farther.

### Changed
- Rendering: room for 32 shadowed lights per pixel in the virtual shadow map projection (the default 16 overflowed with an
  on-screen warning); Lumen in software by default (hardware ray tracing stays available).
- Packaged builds (Shipping and Dev) never print engine messages on screen.
- Room lamps in lumens, two per room; only one per room casts shadows (estate and cave), fixtures cast none by default.
- The map bake cuts roofs at a height above the terrain under each point; the reach check's "ladder only" is relative to
  the ground.
- HUD: the ability cards show their key (A / E as bound), a stronger divider, the bar is centred (it was 12 px off);
  prompt keys (F, SPACE, Exit) follow the rebinding.
- Sightroom: the wave takes 0.35 s (0.8) and highlights follow it every frame (no 0.15 s steps); when it ends the whole
  map turns to the scan look and every enemy on the map shows; highlights are anti-aliased with a thin outline (no more
  pixel blocks); line of sight aims at an interactable's middle, so windows light up.
- Doors, vent hatches and lockers hinge on the other edge.
- Cameras: one colour (yellow); the zone is the camera's shadowed spotlight (walls cut it), much brighter; the floor ring
  decal tests the global distance field so walls hide it too; the monitor keeps the last frame (dimmed, NO SIGNAL).
- Distractions stay on until a guard who comes to look switches them off (or you, with F or remotely).
- Hacking: a ring fills around the F key (the bar under the crosshair overlapped the prompt).
- Circuit breaker lever with sparks; bulbs go dark with the power; room lamps never glow in volumetric fog.
- Mounted gun: free-standing tripod, gun higher, view above it. Tank reload 1.25 s (2.5).
- Gun sounds: one recording pitched and levelled per weapon (FireSoundPitch / FireSoundVolume); the silenced pistol is
  a quiet high snap (it played a dust puff); dry fire is a metal click instead of a clock tick.
- The smoke grenade shows the Smoke_Bombs toon cloud (its path was wrong: grey spheres were drawn) and plays 1.8x faster.
- First-person scale 0.35 (0.6) with a 3 cm near plane, so the arms stay clear of walls; machine guns sit lower.
- Slides always brake (friction + deceleration), left / right only bend them, they last about 0.5 s from a walk and
  0.7 s from a sprint, and Ctrl must be released before the next one: no more endless sideways slides or chained
  sprint slides. Releasing the keys after a sprint stops you crisply.
- Ledge grab: probes the centre and 25 cm to each side, reaches further (76 cm) and, in the air, catches ledges down to
  the feet with any input toward the wall: jumping at the edge of a ledge grabs it.
- Dash on a tap of Shift (cooldown 1.5 s); jetpack on Space held in the air; the second jump goes 1.35x higher;
  holding E with the blink shows the landing spot; the hook climbs the ledge at its anchor.
- Ziplines ride at 16 m/s (9) and reach full speed in 0.25 s; the parachute drop falls at 15 m/s (5.2) in a 3 s intro.
- Guards hold their ground in a fight: they stand and shoot while in range, close in when out of range or sight, and
  never back away (they used to retreat to a flank point 7 m from the player every 1.5 s). They turn to face their
  target while shooting (strafing when they move).
- Hearing: steps are heard 4 m away walking and 9 m sprinting (1.5 / 4.5), and a height difference within one storey
  no longer counts (a step right behind a guard was 1.5 m below its ear and never heard).
- Players have 150 health (300); guards hit harder (difficulty multipliers 0.35 / 0.55 / 0.8).
- Picked-up weapons always go to the left slot; the other weapon slides right.
- Minigun: fires at once at 450 rpm and ramps to 1100 rpm over 1.2 s (0.4 s grace on release) instead of 0.8 s of
  silence then 1500 rpm.
- A silenced bullet into a body is only heard 1.5 m away.
- Footsteps: volume per gait and a random pitch; your own steps are quieter.
- The overhead alert marker is the hatched diamond again.
- Tools find the engine through the Epic launcher's install list (UE_ROOT still wins).

### Removed
- Every Flipbook (comic book) effect on weapons: blood, dust and muzzle smoke.
- The burst rifle (data asset, catalog, gym pedestal).

### Fixed
- After entering a vehicle the "F Use" prompt stayed on screen: a new pawn always starts without a prompt.

## [0.6.1] - 2026-11-11

### Added
- Headshots play a metallic hit sound for the shooter (HeartBeats MetalHeartbeat1, the HeadshotHit sound of
  DA_SoundSet) with the red hit ticks.
- Guard state animations from the Mannequin pack: searching guards draw their gun (weapon equip animation) and hold
  it ready (its aim pose) when they stop to look around; hit guards play a hit reaction (AM_HitReact from
  MM_HitReact_Front_Lgt_01). The look-around and reinforcement-call montages are multicast so clients see them.
- Lobby log lines (LogHitmen): who asked to start and how many players are ready, to diagnose Ready in player
  builds.


### Changed
- Create Room on one 8 px grid: 112 px side margins, a fixed title band, the map tile and the players panel start on
  the same line and the panel ends on the same edge as Choose your agent; the player name has room again; a dim line
  says how friends join.
- Overhead alert marker redrawn Hitman-like: a small triangle pointing at the head, white and filling from the tip
  while suspicion builds, solid yellow or red with a dark "!" once alerted, two pulsing arcs while calling for
  backup (it was a hatched diamond).

### Fixed
- A network or travel failure of one game instance no longer closes another's room: the handlers only act on their
  own world (with several players in one process, the PIE client dropping its first connection closed the host's
  room and cleared the room code).

## [0.6.0] - 2026-11-11

### Added
- Tools/Editor/hitmen_check_reach.py: LD check for the map open in the editor. Every door must be walkable and every
  loot spawn, NPC and retrieval point reachable on foot from a team start (snipers and roof points are listed as
  ladder-only).
- Gym parkour run: ten numbered rooftops along the west and north walls (vault, jump and mantle, gap, jump and grab,
  beam, ledge grab onto the gutter tower, a zipline north, step-ups, a plank and an overlook above the AI pen); a
  ladder on the back of the north roofs.
- Map marker pipeline: a HitmenMapMarker component on any actor shows it on the minimap and the pause map, drawn and
  named by its kind (DA_MapMarker_* data asset: legend name, colour, shape or icon, size, maps, legend order). The
  pause legend builds its rows from the kinds on the map. Retrieval points and cameras use it (retrieval points now
  show on the minimap, pinned to the frame when out of range). Recipe in docs/Pipelines.md.
- Security cameras draw a Hitman-like electronic ring on the floor exactly where their light ends (the decal
  material tests the camera's own cone per pixel): a crisp edge, a turning dashed inner ring and faint scanlines; it
  fills from the edge inward with the detection meter (replicated), and the light and the ring go white, then yellow
  to red as the meter fills (M_Hitmen_CameraRing).
- Hook: a small marker where it will grab while it is ready and in range, and a thin cable (1.2 cm) from the hand to
  the anchor while it pulls, seen by every player. Abilities can ask for both (GetAimMarker, GetCableAnchor); the
  ability component draws them.

### Changed
- Navmesh: 10 cm cells (from 19) so every 110 cm doorway is walkable by the AI, and 16384 A* search nodes (from
  2048) so long patrol legs on a big map are not cut short. L_Empty_01 and L_Empty_02 are re-baked.
- L_LDTest is rebuilt as San Lazaro, about 3x the area: a village (market street, 20 houses with rooftop planks,
  plaza, church bell tower), a river with two bridges, a walled mansion estate (three-floor house, sealed dining
  room, control room, basement down an outside stair well, a seeded hedge maze, greenhouse, garage with the tank,
  graveyard and chapel) and a cliff zone with three caves, a tunnel to the basement and a cove. Every room is lit;
  the minimap texture is 1536 px.
- Map thumbnails are rendered from a fixed camera in the editor (hitmen_map_shots.py capture), no longer from the
  PIE spawn view, so the shot has no HUD, arms or screen effects; ue_map_shot.sh is gone. New San Lazaro thumbnail.
  docs/Pipelines.md has the map recipe (build, bake, check, minimap, thumbnail).
- Vent hatches open like small doors: a vertical hinge on their side edge, swinging away from the opener (they
  flapped up on their top edge).
- hitmen_check_reach.py also checks pickups: each needs a reachable standing spot within grabbing range (a pedestal
  top is a navmesh island of its own).




### Fixed
- Generated buildings: door openings match the door (110 x 230), stairs leave room to step on and switch back on
  three-floor houses (upper floors were unreachable); the ground has holes for stair wells instead of covering them.
- Gym vent: the sealed room overlapped the AI pen wall, so the hatch opened onto a 50 cm gap. The room moved east;
  the hatch sits in a hole in the pen wall and a crouch duct leads into the room.
- Gym: the zipline landing and its stairs covered the armor and C4 pickups; the zipline now runs north from the
  gutter tower. The builder logs any pickup that overlaps geometry. The camera cover wall added by hand is
  generated.

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
