# Changelog

## 0.9.0 — The new look, the new sound, fuller maps

The look
- The whole game is flat-coloured and faceted now: pastel skies, clean colours, soft shadows, barely
  any haze, and buildings as bright inside as out. Each map keeps one mood.
- A new cast with big floppy ears: agents in dark suits with a tie in their team's colour, guards
  dressed for the climate, targets who stand out. Ears, arms and heads swing as they move.
- Deaths are physical: the body staggers and folds as its muscles let go. Your own body falls too.
- New trees, palms, firs, bushes and rocks, and fat clumps of grass. Walking and crouching no longer
  scurry, and the flickering leg should be gone.

The sound
- Every sound was thrown away and generated again: guns with a real room tail, a set of footsteps per
  surface, punchy hits, wordless gibberish for the guards, a radio for the talkie-walkie, and one
  ambience per map. Rooms ring when you are indoors.

The maps
- More to do: more than twice the tall grass around the enemies, about 120 guns, a second man on every
  patrol, two ways up every building, and guards who use the ladders.
- Fixed: rows of the same sink, rocks indoors, explosive barrels in villages (they stay in military
  places), house stairs with no way out at the top, tunnels blocked by the ground or by boulders, trees
  floating over slopes, and falling under the map (you are put back where you last stood).
- The map's edge is an organic outline instead of a square, and the plan is sharper, pale on dark.
- The room shows a picture of each map with an agent in front, and the title uses the first one.

## 0.8.1 — One screen

The front end
- The title is PLAY, a gear and an exit icon. The backdrop is a still colour for now (no more
  flickering); the real picture comes with the maps' rebuild.
- PLAY opens your room at once, and everything happens there: the map on the left, the agents on the
  right, joining someone else by code or IP. Playing solo is just starting without inviting anyone.
- No more mode, team count, time-of-day, caster or shuffle options. The team boxes are the mode: a +
  adds a box, agents are dragged between boxes. Invite and shuffle sit under the agents. Your icon (16 to
  choose from) and your Agent/Caster role are on your own line. Proximity chat is the only option left.
- Map arrows stop at the first and last map, and the plan shows the whole map.
- Each map has its own mood: San Lázaro by day, Qasr at dusk, Weisshorn at night.

Protect the target
- Any team can be set to protect the target: it starts at its side, the guards are its friends, and it
  gets their alerts on a talkie-walkie. The attackers win by killing the target, with no proof to carry;
  the protectors by killing every attacker or holding out until the end of the clock.

Map and contract
- Fixed the map's names staying behind when zooming: labels now go with their marks.
- Zoom follows the cursor, any mouse button pans, marks are filtered by floor and each legend line
  switches its marks on and off. Repeated names show once until you zoom in.
- The contract tells more: who, where, habits, protection and orders.

End of match and caster
- The end screen sits over the match, the pause menu goes above it, and SPECTATE lets you watch on.
- The caster view has three modes: Switch, Multi (everybody's screen) and Fly. Dying makes you a
  caster, and a caster alone can start a match just to fly around the map.

## 0.8.0 — Rules and controls

Only enemies
- No more civilians and no more trespassing: every person on a map is an armed enemy, the whole map
  is hostile, and the target fights instead of running. Nobody panics, cowers or ducks any more.
- Guards really look: they see up to 30 m and notice faster. A guard checking out a sound or a body
  who sees you has seen you, full stop. Civilian posts are armed guards now, so maps hold about
  twice as many enemies.

Controls, modelled on Dishonored
- Left click attacks with whatever you hold (guns fire from the hip), right click aims or throws.
  Crouch toggles on C, lean on Q/E (A/E on AZERTY), block on left Ctrl, zoom on left Alt.
- The selection wheel is gone: the inventory is a horizontal bar of four slots at the bottom of the
  screen. Keys and tools no longer take a slot.

Feel
- The hands no longer drag behind the camera, and bare hands stay hidden except during a punch or
  a block. Punches are 2.5 times faster.
- A jump or a climb no longer breaks your run. Ladders: getting over the top works, an overhang no
  longer jams you, and you can't re-grab one by accident.
- Takedowns from behind are instant: the body drops on the spot.

Items
- New blades and clubs: knife, machete, bat, lying around the maps. You start with a lightly loaded
  pistol. Pickups rest on the ground with no aura or spin, and swell a little when you look at them.

## 0.7.0 — Unreleased

Guards and civilians
- Guards attack again. A guard who is searching (after a shot, a lost confrontation, a radio call)
  and glimpses you comes straight at you, weapon up; he no longer calms down into a casual look.
- Guards remember. Shoot, kill, punch or take someone down in front of a guard, flee a
  confrontation, or get into a fight, and that zone knows you for two minutes: seen means shot,
  wherever you stand. Everyone else still gets the benefit of the doubt in public places.
- A gun in your hand is noticed. On the plaza a guard walks up and tells you to put it away
  (holster it and he lets you go); anywhere else a drawn gun is a crime and guards open fire.
  Cameras see it too.
- Restricted areas: a guard warns you, then shoots, about 3-4 seconds after he first sees you.
  Hostile areas: guards shoot on sight after a short reaction. A calm, unarmed player in a public
  place is never shot.
- Gunfire carries. Guards' shots are heard like yours and every armed guard in earshot comes
  running; real walls (not crates or fences) muffle sound, and guards see through windows.
- Smarter firefights: guards' first shots are wild and they need a moment to track you again after
  losing you; they duck behind low cover and pop up to shoot; one pushes to where you were last
  seen; searches fan out in widening circles, each guard looking around at every spot; at night they
  carry torches. The last guard standing keeps shooting instead of phoning for help, radio calls are
  spaced out, guards aim properly while they move, and lookouts on towers see the ground below.
- Civilians panic like people: a scream and a frozen stare, then a stampede for the exits, away
  from the danger and never past it, and they cower when cornered. Panic spreads through a crowd.
- Witnesses talk. A guest who sees a crime, a body or a gun runs to the nearest guard (a pulsing
  amber "!" over their head and a WITNESS warning): stop them before they get there, or the guard
  comes to look and knows who you are.
- Guests and staff mingle: they wander to the bar, the buffet or the garden and step out for a smoke
  or a call. Patrols have errands: they stop to look around, try locked doors, smoke, take a call or
  stand watch, and pairs walk side by side and chat.
- Everyone talks gibberish ("huh?", "hey!", "over there!", screams, "help!", "clear", radio chatter
  and small talk) in four goofy voices.
- Fixes: patrols stay and look around where they went to check something; crowds run away from the
  trouble (not from the middle of the map); the target has its 120 HP.

First person
- The hands are authored around every weapon: both hands wrap the real grips (the support hand round
  the firing hand on pistols, on the fore-end, pump or vertical grip on long guns), the trigger
  finger rests on the trigger when you aim and along the frame otherwise. This replaces the
  0.6.0 hands.
- Aiming puts the sights exactly on the centre of the screen, for every gun, and recoil kicks about
  the sights instead of swinging the gun off line.
- Your own arms are dark leather gloves under an olive bomber sleeve with a rib cuff, the same as
  the third-person Rook: the black suit sleeves and white shirt cuffs of 0.6.0 are gone.
- Fists up in a guard by the chin. Left click alternates a jab and a cross that pull back and drive
  to the middle of the screen; the hit lands on contact with a freeze of the arms, a jolt of the view
  and a squash of whoever you hit. Holding something, you throw a quick left jab; takedowns grab
  and twist.
- Throws are overhand and the item leaves the hand at the release.

World
- Colour grade redone in HDR: calmer, cleaner contrast and a look per biome that holds at every
  hour (warm and humid jungle, golden desert, cool and crisp snow). Dawn and dusk are warm and bright
  enough to play, nights are moonlit and readable instead of black, and dawn and dusk under trees
  are no longer black against the low sun. Qasr al-Rimal's desert grade is calmer.
- Trees, palms, bushes and ferns sway in one shared wind whose gusts roll across the land; leaves
  glow when the sun is behind them, detail levels cross-fade instead of popping, and bushes, rocks
  and boulders cast shadows.
- Real grass: blades, not a texture. Dense curved tufts sway in the shared wind (gusts roll over a
  meadow as waves of light), glow when the sun is behind them, part where someone walks and stand
  back up, and stay pressed round dropped items so they can be found. Seen from low down they no
  longer turn black at the roots. The ground keeps the meadow's colour where the blades end.
- Ground without repeating tiles: grass, earth, sand and rock no longer repeat as a visible grid
  across fields and slopes, and broad patches vary their colour a little, like real land. Slopes
  and banks keep their shape at a distance.
- Local reflections: every building floor and the streets round the buildings reflect their
  surroundings (glossy floors, glass, metal and water), not just the sky.
- Window glass is clear and a little dark so lit interiors read through it, and shows a dim room
  where there is nothing behind it. Cliffs are layered rock with strata and snow on every ledge.
- A softer sky: rounder, billowing clouds with lit tops, soft grey bellies and bright edges round
  the sun; sky and cloud gradients no longer band.
- Fresh snow sparkles more: about 265 sparse stars toward the sun instead of a handful.
- Truer colour textures: ground, bark, walls and props are crisper (no blocky smears in subtle
  greens and browns); the armchairs and the metal props no longer look washed out.

Characters
- A new original cast is designed: the hero **Rook** (copper
  quiff, amber shades pushed up, olive bomber with an orange lining in your team colour, mustard
  scarf, cargo trousers, boots, leather gloves; male and female), three body bases and every outfit
  (jungle / desert / snow guards, bodyguard, waiter, cook, maid, mechanic, two guests, two targets).
- Faces come alive: people blink, frown when they aim at you, look scared when they flee or cower,
  wince when hit, and show X eyes when they die.
- Bodies wobble: heads and chests lag a little behind every hit, landing and sudden stop.
- Goofier ragdolls: big heads, looser joints, deaths that spin rather than fly, and still at rest within seconds.
- Hands no longer sink into bellies and hips: they slide around the body (head and torso kept clear).
- Sprinting characters may scurry with quick little steps (pace up to 2.5).
- Everyone wears the new cast: you play Rook (man or woman by seat) with your team's colour on the
  jacket lining, facings and armband; guards dress for the map, staff are waiters, cooks, maids and
  mechanics, guests wear summer clothes, and the targets are the tycoon and the magnate.
- Guards and civilians look surprised the moment they spot you.
- Feet slide less: walk, run and crouch speeds re-measured on the new bodies; arms swing clear of bellies.
- Third-person guns sit in the new gloved hands (grips measured on Rook's hand).
- Guards on a search open closets, dumpsters and crates near where they lost you and pull out whoever hides inside.

Maps
- Sentry guns guard each map's restricted cores (San Lázaro: the vault and the Don's private wing;
  Qasr al-Rimal: the keep's vault and armoury, the war room and the governor's study; Weisshorn: the
  VIP floor and the observatory bunker). Each group answers to a control PC hidden 15-40 m away; a
  thick yellow cable runs from the guns along walls and ceilings, through rooms, to it. Hack the PC and
  the guns turn on everyone but you — a target at his desk in front of a gun dies in an "accident".
- Every map has at least 8 security cameras over its gates, the corridors into restricted floors and
  the approaches to the guns, with blind spots to learn.
- New distractions everywhere: office PCs on desks, pedestal fans in kitchens and guard rooms, garden
  hoses on yard and garden walls. Each pulls the nearest guard off his post.
- More throwables (150+ per map), dropped beside guard posts and patrol stops.
- Guards walk: every floor of each key building has a patrol loop (the casa's cellar ring, portico and
  galleries; the keep's cellar, ground floor, light-well galleries and roof; every floor of the Grand
  Hotel, its roof walk and delivery yard), patrol pairs walk the plazas, market streets, souk, courts,
  chalet street and lake shore, and well over a third of the armed staff is on the move.
- Fleeing civilians have somewhere to run: each map marks its exits (docks, stations, roads, trails).
- The editor no longer crashes while building Weisshorn (cliff boulders are scene objects now), and
  steep cliffs carry a rock skin whose texture no longer streaks down the walls.
- Patrols do errands: pairs stop to chat, one has a smoke or takes a call, every other corner is a
  look around; inside, each stop fits its room (a smoke in the guard room, a watch at the vault, a lean
  on the terrace) and a patrol passing a locked door tries it. Every guarded building has someone
  walking its rooms.
- Crowds know where to run: every group of guests or staff has two ways out on opposite sides, 25-55 m
  away on open ground, besides the map's own exits (docks, stations, roads, trails).
- Staff and guests drift between break spots in every social room (the bar, the buffet, the lounge, the
  kitchen, the terrace) instead of standing on their marks all match.
- Patrol stops no longer drop to the floor below when they fall on a stair well or inside furniture.
- Buildings lose the boxy look: dressed-stone quoins on the corners of the grand houses (the hotel, the
  casa, the keep, the churches), chamfered corners on plain walls, moulded cornices and stepped string
  courses, a plinth with a projecting coping, drip moulds over the windows and stone surrounds round
  the street doors.
- Weathering: rising damp along the foot of the walls, rain streaks under window sills, hairline
  cracks, and posters and notices pasted on the town houses' street fronts.
- Shop signs you can read over the doors of the open shops and bars: a bakery, an ironmonger's, a
  billiard hall in San Lázaro (and a faded "SE ARRIENDA" on some shut houses), an épicerie, a
  pharmacie and a salon de thé in the souk, a Bäckerei, a Metzgerei, a Käserei and a Stübli on the
  chalet street.
- Roofs: ridge tiles along every pitched roof (snowed over in Weisshorn) and chimney stacks on the
  houses, chalets and the Grand Hotel.
- Real grass on the maps (the world track's GPU grass): San Lázaro's pastures and terraces, the
  Qasr oasis grove (straw tufts out in the desert), and Weisshorn's new snow-free meadows on the valley
  floor and the sunny lower slopes (the pistes, the lake shore and everything high stay white). The
  hiding fields of tall grass grow tall blades, and in Weisshorn they stand in the meadows.
- Weisshorn's ice rink has two gates: the skaters can run when trouble starts.
- Lived-in interiors: no enterable room is an empty shell (8+ pieces in any room over 12 m2), framed
  paintings and brass sconces on the walls, curtains at the guest-room windows, a row of chandeliers
  in the big halls, lobbies, salons, dining rooms and stair halls; luggage at the Grand Hotel's desk.
- Stealth entries show from their approach: a caged lamp on a post or a bracket over every vent, chute,
  well, hatch, tunnel mouth and drop ladder. Qasr gets a drop ladder from the keep roof to the third
  court (a shortcut locked from outside, like the mirador's and the spa garden's).
- Cliffs are layered rock instead of boulder wallpaper: strata ledges along the contour, basalt
  organ-pipe columns in San Lázaro, sandstone buttresses in Qasr, grey alpine rock with snow on every
  ledge in Weisshorn, talus at the foot; hills, mesas and snow peaks beyond the map box.
- Lone guards on streets and in clearings stand in pairs.
- San Lázaro: a hedged gravel drive from the frisk gate, a fountain, beds, benches and garden cover on
  the casa lawns; stalls, benches, trees in planters and a bandstand on the plaza.
- Qasr al-Rimal: Moorish openings (horseshoe arches in an alfiz, mashrabiya oriels and lattice
  balconies instead of shutters and iron railings) on the keep and the town; four crenellated corner
  towers rising above the keep; tents, shades, stalls and palms in planters in the courts; palms in
  clusters; scrub on the desert; the date press lit and signed, with sacks and a brazier; the
  STAFF ONLY plates read the right way round.
- Weisshorn: snowbanks, sledges, ski racks, a snowcat and woodsheds round the hotel; dark steel stilts
  under the terrace deck instead of red timber.
- Round 4 (judge fixes): the San Lázaro stage's PA stacks are painted plywood cabinets with steel
  corners, woofers and horns (no more black boxes); entry lamps are iron lanterns with a stepped cap
  and a warm core; the coal hatch has a stone curb and rusted iron lids; the tunnel mouths no longer
  open a hole in the ground in front of them.
- Qasr interiors: zellige in the keep's halls and salons, plank floors in the barracks and guard rooms,
  terracotta on the terraces, plaster tiles in kitchens and baths, stone flags in the stores and
  cellars (cobbles stay in the courts). Big sitting rooms get a rug, a brass low table, cushions and
  hanging lanterns; stores and cellars crates, cellar walls plank shelving with jars and sacks under an
  iron lantern; garages and workshops a workbench; guard rooms a table and chairs.
- The rooms the sentry guns guard have a workbench, military crates and a cable tray under the ceiling.
- Qasr streets: doors with a second step, a striped awning or a tiled hood, an iron lantern on a
  bracket and potted plants; iron grilles on the ground-floor windows, the odd mashrabiya oriel at
  street level, heavier stains along the foot of the walls.
- The rock walls closing each map vary in height along their length, dip in saddles where the hills
  and peaks beyond show through, and climb in two or three benches over a talus apron.
- Weisshorn's Grand Hotel: flower boxes and shallow Juliet rails under the guest-floor windows (not
  on the VIP floor, nothing to climb), dormers on the attic roof, red, green or blue shutters by wing.

## 0.6.0 — realistic preview
- Everyone is the same size: your eyes are at the height of everyone else's (the crowd no longer
  looks like children, the bodyguards no longer tower over you), and headshots follow the head.
- The HUD tells you when you are TRESPASSING or in a HOSTILE AREA. Sentry guns only fire on
  trespassers, never in public areas.
- Fresh snow sparkles: ice crystals glint in the sun and twinkle as you move.
- New distractions you switch on: a pedestal fan, a garden hose that sprays across the yard and an
  office PC you hack into a flashing, beeping alarm. Whoever is nearest walks over to turn it off.
- Sentry guns guard the most restricted rooms: a red laser sweeps, a beep warns you, then it fires.
  A thick yellow cable runs from each gun to its control PC, hidden somewhere quiet; follow the
  cable (Instinct shows it through walls), hack the PC and the guns turn green and fire on the
  guards instead. Instinct also shows the cameras, which still call guards when they see you.
- Realistic people everywhere: the cartoon characters are gone. Agents wear a black suit (a man or a
  woman) with an armband in their team's colour; guards are dressed for the map's climate,
  bodyguards in black suits, staff in uniform, guests in civilian clothes, targets as VIPs.
- New animation from motion capture: walking, running, sprinting, crouching, crawling, sliding,
  jumping, falling, landing, vaulting, climbing ladders, hanging and shimmying, swimming, aiming,
  firing, reloading, punching, throwing, phone calls, smoking, looking around, cowering. The feet no
  longer slide (each clip plays at the speed it really covers), the feet stay on slopes and stairs,
  and running down stairs no longer flickers into a fall. Heads follow where people look.
- Bodies fall as real ragdolls built from the pose they died in, keep their momentum and take the
  blow where they were hit; a takedown victim struggles before going limp.
- First person: your own sleeves and hands hold every weapon and item, aim down the sights, recoil,
  reload, punch and throw. The arms are lit and graded like the world (no more white hands), the
  suit sleeves end in white shirt cuffs, the support hand cups under the firing hand, the forearms
  stay low in the frame, and bare hands raise a boxing guard.
- The proof is the target's dossier (or the item it carries) instead of a head. The contract shows
  a studio portrait of the target; new key art on the title screen and a new app icon.
- Three new maps replace Pinecrest Hollow, Mirage Bay and Elmridge, built from the map design
  documents with a realistic look: **San Lázaro** (a Colombian river town on fiesta day, coffee
  terraces, an airstrip and the white Hacienda La Cumbre with its mirador), **Qasr al-Rimal** (a
  desert oasis souk, a palm grove and dry wadi, the governor's citadel on the Great Mesa and a
  radar on the Little Mesa) and **Weisshorn** (an alpine gala night: chalet village, frozen lake,
  ski area, the Grand Hotel and an observatory on the summit, with falling snow). Every building
  is generated room by room: real walls, doors and windows with sills and shutters, stairs, roofs,
  balconies, ledges you can hang from, drainpipes, crawlable vents, locked doors that need a
  crowbar, a keycard, a staff key or a code, cameras and a recorder, furnished rooms. Caves,
  qanats and mine galleries link the zones underground.
- The maps look lived in: every room is furnished for what it is (offices, kitchens with an island,
  bedrooms, archives in aisles, armouries with gun racks, wine cellars, a coal heap, lobbies with a
  fireplace and a reception desk, ballrooms with dressed tables), guest rooms have wallpaper,
  panelling and plaster ceilings, the back of house is bare concrete. Each key building has a
  grand stair with a carpet runner and balustrades. Facades show their ways in: cast-iron
  drainpipes, ivy, trellises, a ledge at every floor, real balconies, coal-chute hatches.
  Backstage reads at a glance: delivery yards with trucks and bins, guard booths with barriers,
  rope queues, staff-only signs, inner walls and gates between the citadel's three courts. Open
  ground is broken up with cover. Stages have truss, PA stacks and lights; market stalls sell real
  goods; cliffs are broken by boulders; cave mouths are framed and lit; rivers run.
- Version 0.6.0.
- Public, restricted and hostile areas, like Hitman: walk the plaza or the market freely as long
  as you behave (no raised gun, no sprinting or climbing in front of guards); staff areas get you
  noticed more slowly and warned first; compounds and private floors are trespassing.
- Parkour: climb onto crates and walls up to 2.4 m, vault low walls, fences and open windows, climb
  over garden walls, grab ledges in the air and hang from them (shimmy, climb up, drop, leap to the
  next ledge), and top out of drainpipes onto the roof. Thin walls and window sills now work (they
  used to fail). Crouch into a vent and you crawl through it. Going down stairs no longer makes you
  hop or flicker out of a sprint, and steps no longer jolt the view. Big drops are heard by guards.
- Controls: left click punches; right click raises your gun and left click then fires; the mouse wheel
  cycles everything you carry, fists included, without getting stuck on empty slots. A punch from
  behind on a guard who hasn't noticed you is a silent takedown that kills at once (with a beat of
  slow motion in solo). Rocks, bottles and wrenches join the coins and bricks: hold right click to see
  the throw's arc, they knock out whoever they hit and draw the nearest guard to where they land.
  Weapons have plain names now (Silenced Pistol, SMG, Sniper Rifle...).
- AI rewritten around hearing that makes sense: a sound carries a set distance in the open, walls and
  floors muffle it, and only the one to three guards who hear it best go and look - the others turn
  their heads. Nobody learns your exact position from a sound any more; a distant shot no longer
  pulls every guard on the map. Footsteps follow how you really move: silent standing still, a
  whisper crouched, loud sprinting. Searching guards now fight back when they see you or are shot
  (they used to wander), reinforcements too. A body is radioed in by whoever finds it and two mates
  come to secure it; guards on posts take breaks (a smoke, a call) you can wait for.
- Version 0.5.0.
- Agents' squash-and-stretch no longer breaks (NaN body scale errors) after a long frame hitch.
- Caster mode, tournament style: join a room (even a contract already running) as a caster to watch
  instead of playing. Up to four live feeds at once (one, two side by side, 2x2, or a main feed with
  three small ones), each following an agent from behind (orbit and zoom) or through their eyes, a
  free camera, or the tactical map with every agent, NPC and objective. Focus one feed or two full
  screen and back, switch feeds and agents with the keys, the mouse or a gamepad, put any of the 8
  agents on a feed from their cards (health, state, kills), and let the auto-director keep the
  action on the main feed. Names over agents, a plate per feed, a contract bar on top; H for a
  clean stream. Eliminated agents get the same view, limited to the agents they may watch.
- Version 0.4.0.
- Sound design rebuilt (still all synthesised, now layered like recorded foley): gunshots with
  crack, body, thump, mechanism and an outdoor tail; footsteps for players and NPCs that follow the
  surface underfoot (grass, dirt, stone, sand, wood, metal, water); swim strokes and splashes; an
  ambience bed per map (forest morning, desert wind and surf, suburban night) that turns to a
  muffled underwater bed when you dive; a tension drone that swells with suspicion, a stinger when
  you are spotted and a hit when it turns to combat; new impacts, landings, slides and UI ticks.
  Sounds now fade naturally with distance.
- Tall grass you can hide in, on every map: 8 to 12 fields of 1.3-1.8 m grass on the approaches to
  the buildings, with hiding triggers (the validator wants at least 6). Trees are taller, their
  canopies above eye level, so the view under them stays open. New water shader: clear in the
  shallows and deep blue-green further out, the bed seen through the ripples, sun glints, the sky at
  grazing angles, foam on the shores. Map builds put buried starts back on the ground; Mirage Bay's
  south start (in the sea) moves to the north-west ridge. Teleports no longer count as falls; the
  end screen layout is fixed.
- Water: every lake, river and sea can be swum in. Crouch dives, jump rises or climbs out onto a
  ledge, rivers carry you, and breath lasts 20 s under water (the status chip counts it down). Under
  water NPCs cannot see you beyond arm's length, the view turns blue-green and the sound is muffled.
  Splashes (yours are heard by NPCs) and bullets hitting water throw up spray; NPCs keep out of deep
  water. The slide goes much further out of a sprint and speeds up downhill. The player's arrow on
  the maps is a proper navigation arrow.
- Menus rebuilt for playing with friends: the main menu is a clear list (create a room, join a room
  with its code, solo, options, quit) with icons and a line on what each does; the briefing shows
  the map card with its plan, the mode as explicit cards (free-for-all 1v1v1v1, teams, co-op), teams
  and options as rows, the agents in aligned columns, the room code with a copy button. Settings are
  rows with the label left and the control right (switches, sliders with values) and cannot overlap.
  The pause menu holds the map (floors, zoom, pan), the contract and the options. The accent is now
  signal amber.
- HUD: a heading strip across the top with the target's marker and distance always on it; the
  minimap becomes the map's baked stylized plan of your current floor, north up and still, with N
  marked, the target always shown, and a rim that turns amber (suspected, searched for), red
  (combat) or blue (hidden). The inventory is a list bottom right shown on a switch; the weapon
  wheel only opens while Q / LB is held. Instinct shows the target at any range, NPCs within 55 m
  and objects within 25 m. Map builds bake the plans (one per floor).
- Threat read-out: a screen-space diamond over each NPC that fills with its gauge (white, then
  amber), turns amber with a "!" when it confronts you, red in combat, "?" while searching, and a
  phone with a countdown ring while it calls (pinned to the screen edge when off-screen). Arcs
  around the crosshair point at whoever is noticing you, a status chip above the minimap says hidden
  / concealed / suspicious / searching / spotted / combat, and the target keeps a red marker with
  its distance at any range. Notifications for bodies found, calls made and calls cut.
- Stealth AI in three contained stages: vision shortened to 20 m in a 90° cone with a slower edge-
  of-vision band; NPCs grow suspicious (stop and stare, walk over past half the gauge), then
  confront you for 2 s with their weapon up (break line of sight to make them search instead, aim at
  them and they fire), then fight. A fight no longer alerts the zone: others join only by seeing it
  within 18 m, hearing shots, or by the phone call, which reaches the caller's zone mates within 60
  m. Crowds scream instead of reporting. The alarm flare is fired rarely.
- Pinecrest Hollow and Mirage Bay: starts moved 80-100 m from any guard (Pinecrest's north trail was
  6 m from one, Mirage Bay's corniche start 7 m), maps rebuilt with props seated on the ground.
  Piers and boats over water no longer count as floating.
- Elmridge: the south and east starts move away from the guards (the south one was 3 m from a door
  guard). The security room's three monitors stand on a three-desk console instead of overlapping in
  mid-air, and vent grilles sit flush under the ceilings. The validator counts a prop touching a
  wall or furniture as supported.
- Map builds snap every NPC post and patrol point that sits a little off the baked NavMesh onto the
  closest walkable point within 4 m (the Elmridge door guard and cinema crowd member included); the
  validator fails NPC posts off the NavMesh.
- Death and end of match: the look at your own body lasts 1.2 s before spectating (the HUD reminds
  that Esc leaves). The end screen has buttons: the host takes the room back to the lobby at once,
  anyone can leave the room; without a click it goes back after 45 s.
- Map dressing sits on the ground: props, nature pieces and terrain trees are lowered to the lowest
  ground under their footprint (rocks bed in deeper), explosive barrels stand on the patrol's floor
  (never on a roof or in the air over a dip), and crowd or patrol members off any floor stand on the
  terrain.
- Safe starts: the match draws its start point among those with no guard post or patrol point within
  50 m (else the roomiest). The map validator fails a start closer than that, and reports props
  floating over the terrain.
- Agent name: the nickname typed in the menu wins over the Steam persona, is saved, and updates live
  for everyone already in the room; until one is typed the field follows the Steam persona as soon
  as Steam comes up.
- No more screen vignette when crouching or hiding (concealment will show on the HUD). The camera
  sits higher: eyes at 1.74 m standing, 0.98 m crouched (NPC sight uses the same heights).
- The cursor stays locked in the game window during a match: the lock is re-applied every frame the
  window has focus and the moment focus comes back (alt-tab, a click outside), so the mouse can no
  longer leave the window or click other tabs.
- Maps rebuilt at 500 m on Unity Terrain (HillMap): valleys, terraces, cliffs, graded streets,
  buildings stepping down the slopes on retaining walls, dense forests; 210-230 NPCs per map with
  patrol groups, room guards and crowds around staged scenes.
- Title screen: key art (an agent against a stone facade, depth of field) and a Hitman-style menu
  (tab bar, picture tiles, big title, footer); gamepad selection highlights buttons and tiles.
- Stylized shading: painterly light ramp, soft specular, colour drift and grain, lit chamfers.
- Minimal Hitman-style HUD: minimap, transient weapon/ammo and health, weapon wheel (Q / LB),
  no objective panel; the map screen gets a contract column with a live portrait of the target.
- Instinct senses the surroundings only (target 60 m, people 25 m, objects 20 m).
- Crouch and sprint are held; crouching while moving slides (further out of a sprint); Q/E slides
  removed. Patrols walk slowly and in groups of up to five; crowds flee from trouble.
- Unity Terrain for the maps (LandShape stamps, eight painted ground layers, forests, grass).
- Instinct vision (hold Tab, D-pad left): the world dims and what matters shows through the walls,
  the target and the proof in red, teammates in blue, nearby NPCs in white or amber when hunting,
  interactables and pickups around you in white. The scoreboard moves to the key left of 1
  (D-pad right).
- Mirage Bay rebuilt as a 1 km² sandbox (`MapGulf` partials: terrain, souk, docks, Zenith, between):
  sea on two sides, dunes and a sandstone ridge on the others, 105 NPCs, 62 weapon spawns, four
  hidden spots each for the rocket launcher and the flamethrower, four edge starts, four
  extractions (marina speedboat, Zenith helipad, desert road pickup, quay storm drain).
- `MapKit.DistanceCulling()`: the forest's distance culling and small-item shadow cut-off, now a
  shared opt-in step a map calls before `Finish` (Pinecrest Hollow uses it, output unchanged).

## 0.1.0 — first playable
- Steam / LAN / solo sessions, lobby with mode, map, teams, proximity voice option.
- Three modes (free-for-all, teams up to 4, co-op), elimination and spectating.
- Three generated maps with three zones each, 15 original targets per map.
- Hostile AI with squads, last known positions, cover, flanking, searches, radio reinforcements,
  map-wide alarm, bodyguards, target routines, patrol cars.
- Systemic props: fire and sprinklers, power cuts, chandeliers, drinks and toilets, ventilation
  gas, distractions, hiding spots, ziplines, jump pads, climbing, tall grass, explosive barrels next
  to every patrol route.
- Weapons and gadgets: hush pistol, pistol, SMG, shotgun, sniper, unique rocket launcher and
  flamethrower, coin, brick, smoke, C4 + remote, lethal and emetic poison, punches and takedowns.
- FPS controller with slides, mantling, climbing; viewmodel; juice; HUD, map screen, end screen.
- GD Tool and map validation; EditMode tests for the match rules.

### Known limits (to iterate)
- Online play was verified with one machine only (solo host in the editor); a real two-account Steam
  session still has to be tried.
- Hero props (cars, furniture, weapons) are procedural boxes; Blender models in
  `Resources/Models` (`W_<Kind>`, `Car`) replace them automatically when added.
- NPC and level balance comes from short playtests; expect tuning in the GD Tool.
