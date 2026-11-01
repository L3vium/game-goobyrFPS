# How IO Interactive designs World of Assassination levels: a study for original multiplayer stealth maps

Scope: HITMAN (2016), HITMAN 2 (2018) and HITMAN 3 (2021). The goal is to pull out the transferable philosophy and measurable rules so a small team can build **original** maps (a Colombian jungle hacienda, a desert oasis town with mesa fortresses, a snowy alpine lodge gala) for a multiplayer stealth FPS. Nothing here is meant to be copied as a layout.

Conventions:
- **[S]** = stated by IOI staff, or reported from an IOI talk or interview.
- **[O]** = observed in the shipped maps, mostly from walkthrough and map data.
- **[D]** = a rule I derived for this project. These are not IOI numbers.
- Where no published figure exists (metric zone sizes, for example), I say so and give a working range marked [D].

---

## 1. Sources (37 read, plus several that were only partly reachable)

Primary and near-primary (IOI talks and interviews):
1. **GDC Vault, "Level Design Workshop: 'Hitman' Levels as Social Spaces" (Mette Podenphant Andersen, GDC 2019), session page.** The talk has three parts: the analysis that changed IOI's approach for Hitman 2, game spaces read as social spaces, and the new vocabulary that resulted.
2. **Game Developer, "Mapping out the subtle social cues throughout Hitman's level design" (McAloon, 2019), report on that talk.** Gives the full taxonomy: public (basic / purpose / rule) and private (basic / professional / personal). Also covers Bourdieu and Goffman's front stage and backstage, the studio motto "It starts and ends with level design", and the lesson from Miami that a level can keep players engaged without trespassing.
3. **Impress GAME Watch (Japan), GDC 2019 report on the same talk.** Six-step difficulty ladder (street corner, back alley, church, private road, kitchen, surveillance room). Difficulty comes from "who is watching and what the rules are". Rewards scale with risk. IOI colour-codes maps by space type.
4. **Game Developer, "Video: How Hitman guides players through non-linear level design" (GDC Europe 2016, Andersen and Mikkelsen).** User research found players had fun, but the learning curve was too steep and Paris's scale was "incomprehensible". The answer was a linear guidance layer that does not overshadow the sandbox.
5. **GDC Vault, "Modular Sandbox Design: Tools and Workflows for Hitman" (Marinus Rørbech, GDC Europe 2015).** IOI moved from monolithic levels to modular ones so components could be reused across missions.
6. **Game Maker's Toolkit, "The Making of Hitman 2's Miami Level" (Jakob Mikkelsen and Eskil Mohl).** Read through a full Persian translation (Digikala mag, GMTK #80) and the Kotaku write-up. Covers the "what if" concept, dweller and roamer targets, routines (Robert takes about 5 minutes to cross an area, the race lasts about 20 minutes), a map split in two by the track and stitched by 2 bridges plus tunnels, "snail house with Swiss cheese" blockouts, rooms with at least 2 exits (toilets excepted), deeper security per layer, careful choice of the first disguise, and challenges as design constraints.
7. **Kotaku, "IKEA stores and Hitman levels are both snail houses" (on GMTK).** Compact footprint with long visible routes, hidden shortcuts that reward mastery, no dead ends.
8. **80.lv, "The Snail Structure of Hitman's Levels" (via RPS).** Christensen: Sapienza and Hokkaido "wrap around themselves, with their layers interconnected with slopes and stairways"; they feel bigger than they are; no backtracking is needed.
9. **Game Developer, "Hitman dev says the secret to expansive level design is spirals" (RPS summary).** "Basically spirals, or a snail house." Hylling: levels feel big because "you can keep moving ahead all the time". Every room is designed so players pass through without missing stairwells or doorways.
10. **80.lv, "Hitman Sapienza level analysis."** Rough Sapienza prototype built in about 2 weeks by Christensen and one environment artist. Elverdam's Swiss Cheese idea and "contrast is a key tool". The antagonist drove the underground hideout.
11. **Game Developer, "From coastal town to Sapienza" (republishing PC Gamer).** Elverdam: "a volume filled with connections… you will never get lost"; hidden boundaries make it "much harder to guess where the level starts and stops". Verticality and how streets and corridors link.
12. **PCGamesN, "We never went to Italy" (Christensen and Hylling).** Research done remotely through Street View. Walls slimmed to fit a throw animation. "If a structure looks like it can't hold its own weight then people will start noticing." Contrast (high-tech spa against grim underbelly) makes a level feel bigger. RFID doors on Hokkaido.
13. **Wikipedia, "Sapienza (Hitman)".** Mansion conceived like a palace: several floors, basement, attic, main and rear entrances, garden. Pathways have no dead ends. Some pockets were dense but still had room to "breathe". Playtests confined De Santis's loop to the mansion after church and cave detours felt too slow. The lab objective was criticised as linear.
14. **Wikipedia, "Hokkaido (Hitman)".** Three tiers in a spiral: resort on top, surgery and staff quarters in the middle, morgue at the bottom. Hospital, resort and morgue were each given plausible reasons to connect. An early area teaches the AI door mechanic, and doors and clothing are marked. Dense population within the roughly 300 NPC limit.
15. **Wikipedia, "Hitman (2016)".** Swiss cheese with no "visual dead ends". Fortress versus snailhouse levels. Social stealth with each area a "microbiome". Up to about 300 NPCs. Paris was the vertical slice that set rules, layout and NPC count. Playtest fixes: guards escort trespassers instead of shooting, and mission stories were added because levels felt lifeless. Colorado was criticised for no public spaces and little verticality.
16. **Wikipedia, "Hitman 2 (2018)".** "Fortresses" against "snailhouses". A track team per map. Familiar spaces plus surprises. Crowds and foliage as blending spaces.
17. **Wikipedia, "Hitman 3".** Each mission starts from keywords (Dubai: "an inviting beginning / open and airy / on top of the world"; Dartmoor: "house filled with secrets / ominous / vulnerable"; Berlin: "loneliness / isolation"). Dubai's start was changed to outside-in to sell vertigo. Highlighting every point of interest in Dartmoor was dull, so cues moved to audio and conversation. Persistent shortcuts. Ambrose's rocky terrain splits the map into zones. Engström: after Mumbai and Miami, "it's better to be a little sharper and more focused."
18. **Vice, "How Hitman uses thoughtful level design to tell stories" (Jonathan Rowe, art director).** Bangkok's hotel gets cleaner and lighter floor by floor, with a garden surprise on top. A butterfly motif unifies it. Every hotel room got a written backstory. Hokkaido's sterility as negative space.
19. **Haywire Magazine, "The Language of Level Design in Hitman."** Thematic reading of architecture: Paris imperial staircase as power, Sapienza's arch through the cliff to a private ledge path, distant hills that stay visible but unreachable, Marrakesh market against Western goods.
20. **Unsupervised Nerds, "Level Design in Hitman."** Miami's two contrasting halves (dark interiors against bright exteriors) unified by Kronstadt branding, with a bridge that serves as a sniper perch seeing both targets. Sgàil's routes (drainpipe, mid-stair path, shack window), "every tower reachable by halls/stairs or ledges/drainpipes". Haven's 4 servers. Sapienza's areas reused by later missions.
21. **Unsupervised Nerds, "How Hitman 3 Hides a Tutorial" (Dubai).** Linear opening outside the building. The lobby shows a locked shortcut and both targets overhead (a tease). Up the stairs, the level fans into 3 choices (left, right, behind). Terminals pull the player without forcing a route.
22. **PCGamesN, Hitman 2 AI (Travis Barbour).** Mission-story backend pulls a target's loop toward the player's plan so players are not "sat there waiting, holding instinct mode". Some deliberate waiting is kept.
23. **PCGamesN, Ambrose Island (Kevin Goyon).** Natural boundaries avoid "artificial borders". Rocky topology gives distinct zones and more verticality. Untamed land is harder to read. Routes must pass "near death points in a natural way". Two factions on the brink of conflict.
24. **Inverse, Mendoza (Martin Skov Ansdal).** The real reference was adjusted for "more verticality and vegetation" to support stealth. A signature machine (grape press) was planned into the kills from day one.
25. **Game Informer, "Hitman 3's developers share what makes its Berlin level special."** Keywords, forest approach to the club, bass heard on approach, the rave as the "event location" (like Paris's fashion show), a dense crowd that both hides 47 and hides targets.
26. **Vice, "The astonishing Berlin level…"** No mission stories or markers. 10 possible agents, kill 5. New starts and secret entrances unlock over revisits.
27. **GamesBeat, "Hitman is a living sandbox."** Elverdam: players should learn "where everyone and everything goes". No progressive difficulty curve, because early levels must stay replayable.
28. **TechCrunch, Elverdam season-one retrospective.** Geometry is built late and flexibly. "There's nothing more honest than watching people playing your game."
29. **Kotaku, Elverdam preview (2015).** About 300 AI in Paris against 20 to 40 in Absolution. "We don't have to pressure you up front." Traps are left for players to discover.
30. **Kotaku, "The Hitman experiment has been a success."** Hostile levels (Marrakesh, Colorado) played worse than public-space levels. Escalations build intimacy with one corner of a map.
31. **Gamepressure map guides (one source family, 17 pages):** Paris palace infiltration, Sapienza, Marrakesh, Bangkok, Colorado, Hokkaido, Miami, Santa Fortuna, Mumbai and the Crows hideout, Whittleton Creek, Isle of Sgàil, Dubai, Dartmoor, Berlin's Club Hölle entrances, Chongqing, Mendoza. These supply entry counts, floors, starting locations, disguise tiers and target domains.
32. **Game Informer, Santa Fortuna preview, and GamingTrend Santa Fortuna hands-on.** Village party, compounds, three domains. No single disguise satisfies the bodyguards, soldiers and staff at once.
33. **PCGamesN, Santa Fortuna hands-on.** Elephant grass, a fortified gate, a garage, a window over a beam. Much sparser crowds than Miami.
34. **Azhdarchid, "Topology of Hitman levels."** Onion (Sapienza, Mendoza, Dartmoor), festival (Paris, Berlin, Bangkok, Hokkaido, Sgàil, Dubai, New York) and archipelago (Marrakesh, Chongqing, Whittleton, Haven, Ambrose, Santa Fortuna, Colorado, Mumbai). Topology is defined by disguise-validity zones and how they connect.
35. **HitmanForum threads:** "Appreciating the verticality/parkour" (pipes, ladders, Ambrose stilt huts, Haven's two hidden server entrances), "Why is Chongqing so cramped?" (empty shells hurt; the ICA facility has 5 or more entrances and many vents), "Why is Colorado hated" (open plus flat plus hostile compounds badly; 4 mandatory targets thinned out; one scripted exit was later patched to many), and "most ingenious design" threads.
36. **VGC Hitman 3 hands-on, PowerUp Gaming (the cut city-sized Paris was about 3x Sapienza and incoherent), PCGamesN design trailers** (paper sketches, then iteration; levels "credible, fluent, yet dangerous and enclosed").
37. **Portfolio "Operation Steel Veil" (Hitman-style student level) and Scientific Gamer "Thoughts: Hitman 2".** Practitioner method: pillars, floor plans, two blockout passes, 19 rooms over 2 floors with 2 staircases per floor. Levels as self-contained puzzle boxes with utility gear limited.

Partly reachable (headline or abstract only, used for framing): PC Gamer "Making of Sapienza", "How the creators of Hitman use social science", "Remakes levels by changing the people, not the polygons", "How IO made the trilogy"; GamesRadar "There's level design and then there's Hitman level design" and "How you make a Hitman level"; the Nordic Game 2024 abstract for Lasse Bruun Hansen's talk on curiosity and iteration across the trilogy; the Noclip episode listings (track teams, explorable spaces, satisfying targets).

---

## 2. Core philosophy

### 2.1 "It starts and ends with level design" [S]
At IOI the level is the game. A track team, led by a "track owner" who acts as a mini-director, owns a location end to end: geometry, targets, routines, opportunities and challenges (sources 2, 16, 27). This matters for a small team. **One designer owns one map's whole ecosystem**, and NPCs are not a separate department.

### 2.2 The sandbox: a puzzle box that is fully simulated [S]
- Each level is a self-contained puzzle with many solutions. There is "no wrong answer" once players know enough (15).
- The level is "fully simulated": take an NPC out and the simulation adapts (15).
- About 300 NPCs per level, each with a routine. That is the ceiling, not the target (14, 15, 29).
- There is no difficulty curve across levels, so every map stays replayable (27).

### 2.3 Swiss cheese, a volume full of connections [S]
Elverdam: "a volume filled with connections… you will never get lost." Ingredients:
- many routes in and out of every space;
- **no visual dead ends**, so the player always sees a next opening (15);
- hidden boundaries, so players "can't guess where the level starts and stops" (11);
- shortcuts (pipes, fences, windows, elevator shafts, back doors) found through mastery (6, 7).

### 2.4 Spiral, or snail house [S]
Christensen: levels "wrap around themselves, with their layers interconnected with slopes and stairways" (8, 9). Effects:
- a compact footprint that feels large because "you can keep moving ahead all the time";
- no backtracking needed (it is allowed, but never forced);
- each layer of the spiral is a tier of depth or security, ending at a core (Sapienza's lab, Hokkaido's morgue).

Hitman 2 formalised two macro forms. **Fortress**: one area to infiltrate. **Snailhouse**: a circular level where you explore the periphery to reach the centre (15, 16). Miami's blockout brief was literally "snail house with Swiss cheese" (6).

### 2.5 Three topologies (community framework, consistent with IOI's fortress and snailhouse) [O]
Topology is defined by **zones of disguise validity and how they connect** (34):
- **Onion.** Start outside and peel concentric layers toward a central high-security fortress with many entry points. Examples: Sapienza (streets, then grounds, then mansion, then lab), Mendoza, Dartmoor.
- **Festival.** Start inside a crowded public event surrounded by a connected backstage. Crossing the public spine in different disguises opens different doors. Examples: Paris (catwalk spine touching the basement, backstage and auction), Berlin, Bangkok, Hokkaido, Sgàil, Dubai.
- **Archipelago.** A public "sea" connects several independent fortresses, each with its own disguise rules and usually its own target, often linked by a secret passage. Targets can be lured out of their fortress. Examples: Marrakesh, Chongqing, Santa Fortuna, Mumbai, Whittleton, Haven, Ambrose, Colorado.

### 2.6 Levels as social spaces [S]
Andersen applies Bourdieu (social space as a market of behaviours) and Goffman (front stage and backstage) (2, 3). Six space types, from easy to hard:

| Type | Rules / enforcers | Role | Example |
|---|---|---|---|
| Public, basic | Almost none | Orientation, rest | Sapienza square |
| Public, purpose | Few, nudges actions | Suggests behaviours safely | Alleys |
| Public, rule | Implied by real life; social enforcers | Teaches trespass and social stealth safely | Church |
| Private, basic | Trespass, light danger | Stealth practice; **breather between intense zones** | Private alley or road |
| Private, professional | Staff-only, readable as such | Disguise play | Kitchen |
| Private, personal | Rare, "should feel special" | Reveals the target | Surveillance room, target's office or bedroom |

Rewards (information, kill setups) scale with how far up the ladder the player goes (3). The trilogy shows the extremes. Miami proved a level can engage through large public areas "without introducing trespassing" (2). Colorado, with no public space, was the least loved map (15, 30, 35).

### 2.7 Mission stories and opportunities: guidance without hand-holding [S]
- Mission stories were added after playtests called levels "lifeless" (15). They are optional "rails" that teach the layout while leading to a kill.
- Guidance is layered: overheard conversation (always), then the tracked story (opt-in), then challenges as hints, then nothing. Berlin ships with no mission stories at all (17, 26).
- Hitman 3 Dartmoor: highlighting every point of interest was "dull", so cues moved to audio (a draft, rustling paper) and staff chatter (17).
- Placed items hint at solutions without forcing them (gunpowder plus an explosive golf ball in Miami) (6).
- The backend bends target loops toward an active plan so waiting stays short (22).
- The tutorial was rebuilt around replaying the same space with fewer guides each time (13, 15).

### 2.8 Every NPC has a purpose (or seems to) [S/O]
- Targets are **dwellers** (stay in a domain) or **roamers** (move through public space) (6).
- Routines exist so players can **observe and plan**, and they are also levers (a thermostat, a satellite dish, a car) the player can pull to redirect them (6).
- Social enforcers police each space's rules (2). Guards escort trespassers back to public space instead of shooting, which came from playtests (15).
- Staff are disguise dispensers placed with intent. The first disguise found is chosen carefully, because a strong early outfit makes the map redundant (6). In Santa Fortuna no single disguise satisfies bodyguards, soldiers and staff together (32).
- Empty shells kill a map. Chongqing's streets outside the two mission areas drew the most complaints (35).

### 2.9 Replayability by changing people, not polygons [S/O]
- Sapienza was reused for Landslide (the mansion closed, the church and square in use), The Icon (the square became a film set) and The Author (church and apartments) (13, 20). Elusive Targets, Contracts and Escalations put new actors on the same geometry (27, 30).
- Rule: **every area should be good for something** in at least one future scenario (20).
- Mastery unlocks more starting locations, typically 5 to 11 per map, and persistent shortcuts (Hitman 3) (17, 31).

### 2.10 Believability and contrast [S]
- Real-world research, but "evoke" rather than replicate (16). Structures must look like they bear their own weight (12).
- Contrast inside a map makes it feel bigger and gives identity: luxury spa against grim morgue (12), dark Kronstadt interiors against the sunny race (20), forest loneliness against rave (17), a dark rainy city above a bright facility below (17).
- One or two keywords guide each mission. Unifying motifs (Bangkok butterflies, Kronstadt branding) bind distinct zones together (17, 18, 20).

---

## 3. Concrete design rules (measurable)

### 3.1 Landmarks, vistas and orientation
IOI says little in public about landmark metrics, so most of these are inferred from the maps.
- **R1 [O/D] One "hero" vertical landmark visible from at least 70% of exterior space**, sitting on or beside the core objective. Examples: Sapienza's clock and church towers and the cliff mansion, Mumbai's tower, Chongqing's Block, Dubai's spire seen from outside, Sgàil's castle (9 floors), Berlin's radio tower and plant chimneys.
- **R2 [O] A secondary landmark per district** (a fountain, a statue, a bridge) so players can name places: "meet at the statue." Santa Fortuna's Delgado statue sits in the plaza and is itself an opportunity (31).
- **R3 [S/O] High ground that overlooks the next objective is a reward, not a spawn.** Miami's overpass sees both targets (20). Sapienza's church balcony and tower are sniper perches into the mansion (31). Colorado's water tower is the farm's only real overlook (31). Rule: every key building has **at least 1 overlook** across a public or semi-public gap, reached through a mildly restricted route.
- **R4 [S] Show the goal early, then deny the direct line.** In Dubai's lobby you can see both targets overhead and a locked shortcut door (21). Berlin's bass is audible from the forest (25).
- **R5 [O] Distant unreachable landscape frames the map and sets the boundary** (Sapienza's hills, Ambrose's cliffs: "no artificial borders") (19, 23).
- **R6 [S] Wayfinding props in professional spaces**: floor lines, signs and maps (Miami) (6). Doors and clothing marked by access level (Hokkaido) (14). A directory terminal that unlocks the map (Dubai) (21). Disguise legality shown on the minimap (35).
- **R7 [S/D] Light and colour gradient = depth gradient.** Bangkok gets cleaner and lighter as you climb (18). Use warm, saturated public spaces and cooler, harsher private and professional spaces. The personal space gets a distinct accent.

### 3.2 Security layers
- **R8 [O] 3 to 5 disguise tiers per map, plus "enforcers" who see through same-tier disguises.**
  - Paris: guest, then palace staff (floors 0 and 1), then stage crew (floors 0 to 2), then auction staff and CICADA bodyguard (floor 3), with attic bodyguards who may recognise you (31).
  - Miami: public, then VIP (security uniform), then upper floors (specific outfits) (6).
  - Hokkaido: public resort, then staff, then doctor, then surgeon or director, enforced by RFID (14, 31).
  - Chongqing: dongle tiers 1, 2 and 3 (31).
- **R9 [S] The six-step social ladder should appear in every map**, colour-coded on the design map. Target mix for a festival or onion map [D]: about 30% public (basic, purpose, rule), 25% private basic (breathers), 30% professional, 10 to 15% personal.
- **R10 [O] Frisk checkpoints sit on the main entrances of key buildings** (Sapienza main gate, Dartmoor front door, Mumbai tower, Berlin queue, Paris floor-3 stairs). Every frisked entrance has **at least 1 unfrisked alternative**: open windows at Dartmoor, a gutter at Paris, a climbable wall at Santa Fortuna (31).
- **R11 [O] Cameras plus one monitoring room per map** where recordings can be wiped. The room sits in professional space, usually in a basement (Paris, Colorado, Santa Fortuna, Whittleton, Dubai level 2) (31).

### 3.3 Entry points per key building

| Key building | Entries (from map data) | Types |
|---|---|---|
| Club Hölle, Berlin | **8** | Queue (invite or frisk), one-way parking shortcut, 2 side doors (crowbar or key, or through a container), Chill Out bar (public), boarded hole to toilets, rear door under a camera (with climb to the roof), overpass from the biker building, tunnel |
| ICA facility, Chongqing | **6** | Coded front door with guards, boardwalk side door, crowbar double doors, inner courtyard shortcut, coded secret hatch to level -3, Block ladder shortcut, plus vents |
| Villa Caruso, Sapienza | **6 to 8** | Main gate (frisk), metal gate, garden gate (gardener), 2 card doors, walled-up door (explosive), ruins wall jump, ledges |
| Caruso lab, Sapienza | **6 or more** | Guarded door (disguise plus card), card doors, bridge hole, chimney, a building with lab access, sewers |
| Paris palace (floor transitions) | **3 to 4 per floor** | Unguarded central basement stairs, invite back door, main stairs (disguise), stairs behind the catwalk, rear gutters, balcony ladder to the attic |
| Thornbridge Manor, Dartmoor | **5 or more** | Front (frisk), open windows, delivery area, wall and balcony climbs, roof ladder shortcut, secret rooms inside |
| Delgado mansion, Santa Fortuna | **4 or more** | Main gate (disguise), wall climb to the right, garage secret passage to the caves, coca-field side entrances |
| Crows hideout, Mumbai | **3** | Canal, guarded alley, lockpicked beach house |
| Farmhouse, Colorado | **4** | Side kitchen door (2+ guards), back door (2 to 3), upper window climb, outside basement stairs |
| Sgàil castle (outer) | **6** | Main gate, gutter beside it, guarded passage, side gate, initiate door with gutter, sewer |

Rules:
- **R12 [O/D] A core building needs at least 5 entries; a secondary fortress needs at least 3.** Critics docked Marrakesh for "fewer entryways to each area" compared with Sapienza (search snippets of reviews; see Marrakesh in section 4). Colorado's farmhouse, with 4 entries all on hostile ground, was disliked.
- **R13 [O/D] The entries must differ in kind**, covering at least 4 of these 6 types:
  1. *Social front*: invite, disguise or frisk. Slow but safe.
  2. *Service*: kitchen, delivery or staff door. Needs a professional disguise.
  3. *Climb*: gutter, ledge, balcony, vine or scaffold. Fast and exposed, risky if seen.
  4. *Underground*: sewer, cave, tunnel or basement. Long and isolated, a good place to dispose of bodies.
  5. *Tool-gated*: crowbar, lockpick, keycard, code or explosive. Needs an item found elsewhere.
  6. *One-way shortcut*: opened from inside and persistent (Hitman 3). Rewards a previous run.
- **R14 [O] At least 1 entry is a secret passage linking two fortresses or the fortress and the public sea.** Examples: Marrakesh's shoe-store tunnel to the consulate, Whittleton's Griswold-to-Janus tunnel, Santa Fortuna's garage-to-caves passage, Chongqing's hatch, Mendoza's parking-to-silos tunnel (31).
- **R15 [D] Risk and time trade-off**: put the fastest route (a climb) under at least 1 sightline that is not permanently watched, and make the safest route (the social front) take 2 to 3 times longer, because the disguise or invite has to be fetched first.

### 3.4 Verticality
- **R16 [S] Layers connected by slopes and stairways.** That is the snail (8). Sgàil has 9 floors (31). Mumbai's tower runs lobby 2, security 3, film set 4, office 5, unfinished 6 and 7, and the train yard has 3 levels plus a roof (31). Dubai has about 7 floors plus a terrace and helipad (31). Hokkaido has 3 tiers (14).
- **R17 [O] Every vertical tier is reachable by at least 2 kinds of connector**: interior stairs (social) and an exterior climb (gutter, drainpipe, ledge, vines). "Every tower can be reached through halls and stairs or through ledges and drainpipes" (20).
- **R18 [O] Ledge networks link private rooms at the same height.** Hokkaido's apartment-to-apartment ledges, Bangkok's gutter from room 102 to 202, Dubai's electronic windows opening onto ledges (31).
- **R19 [O] Basements and underground are the private-professional and personal floor.** Paris basement, Colorado's basement and bunker, Whittleton's basements, Chongqing's levels -1 to -3, Santa Fortuna's caves, Mendoza's cellar (31).
- **R20 [O] Vents and ducts appear in high-security cores** (Chongqing's ventilation shaft connects the main rooms; Mendoza's vent links the tech room to the wine fridge; Haven has a poisonable vent) (20, 31).
- **R21 [S/O] Avoid flat and open together.** Colorado was "open and flat", let you "run straight" and "lacks the maze character" (35). Mendoza's reference site was altered for "more verticality and vegetation" (24). Ambrose used rock to gain verticality (23).
- **R22 [O] Rooftops act as a secondary road network in dense towns** (Marrakesh market roofs, Mumbai upper slums, Chongqing, where you exit an apartment window onto another building's "ground floor") (35).

### 3.5 Interior architecture
- **R23 [S] Every room has at least 2 exits.** The only exception is the toilet, a deliberate trap or ambush pocket (6). No dead ends (8, 9).
- **R24 [S] Rooms are designed so the flow through them reveals their exits.** No missed stairwells or doors (9).
- **R25 [S/O] A key building follows the palace programme**: several floors, a basement, an attic, main and rear entrances, a garden (13). Floor counts in practice: Dartmoor 3 plus roof, Villa Caruso 3 plus basement plus observatory, Paris 4 plus basement and attic, Mendoza villa 2 plus attic plus cellar, Colorado 2 plus basement (31).
- **R26 [O] At least 2 stair systems per key building.** A grand social stair (Paris imperial stair, Dartmoor foyer) plus at least 1 service stair or hidden stair (Paris's central basement stair, "staircase behind catwalk").
- **R27 [O] Interlocking loops: every floor should allow at least 1 full circuit** (room to room to corridor and back) so a pursued player can break line of sight and a patroller's route can be read.
- **R28 [O] Secret rooms and passages tie public rooms to private ones** (Dartmoor secret rooms, Sapienza attic with 2 entrances) (31).
- **R29 [D] Room count targets for a key building**: 15 to 30 enterable rooms over 3 to 4 floors. The student reference used 19 rooms over 2 floors with 2 staircases per floor (37). Aim for about 40% professional, 40% social, 20% personal or secret.
- **R30 [S] Do not ship empty shells.** If a building can be entered, give it a purpose. If it can't be entered, make that read clearly (35).

### 3.6 NPC placement
- **R31 [S] Targets come in two kinds: a dweller (with a domain) and a roamer (crosses public space).** Each has a routine of a few minutes per area (Robert Knox about 5 minutes to cross an area) (6). Total loops should stay short. Playtests cut De Santis's church and cave detours because they were too slow (13).
- **R32 [S] Each routine has 2 to 4 redirect levers** the player can trigger (thermostat, satellite dish, car, plant, statue, flag) (6, 31).
- **R33 [O] Static guards sit on choke points and checkpoints in pairs** (Dartmoor gate 2, Mendoza and Dartmoor two-man patrols, Sgàil main entrance 3, Crows alley 2, Chongqing front 2+1) (31). Lone NPCs are placed **on purpose** as disguise sources, beside a closet or bathroom (Paris lone CICADA guard in the garden, Marrakesh elite soldier "in a quiet spot with a closet") (31).
- **R34 [S] Crowds are a tool.** Hide in them (Berlin, Miami stands, Marrakesh market), but crowds also slow movement and hide targets (25, 30). Sapienza's town is deliberately sparse for a siesta feel (from PC Gamer snippets). Santa Fortuna is much sparser than Miami (33).
- **R35 [S] Enforcers per disguise**: each disguise has 1 or 2 NPC types who can see through it (Paris attic bodyguards, Mendoza cellar mercenaries) (31).
- **R36 [O] Faction friction creates opportunities** (Ambrose militia against pirates, Mumbai Crows against bandits, Colorado militia sub-groups stationed in separate areas) (23, 31).
- **R37 [S] Budget**: up to about 300 NPCs in a single-player map (14). For a multiplayer FPS with a host-authoritative AI, plan **60 to 150** simulated NPCs [D] and fake the rest with ambient props and sound.

### 3.7 Item placement
- **R38 [S] Items are hints.** A poison next to a drink that can be poisoned, gunpowder next to golf balls (6).
- **R39 [O] Every map has**: 1 or 2 lethal poison sources (doctor's office, greenhouse, cemetery rat poison), 1 crowbar, keys carried by NPCs (bartender, undertaker), a camera or code puzzle, tools at a construction site, and stashes in public and semi-public places (Santa Fortuna has large-item stashes at the construction site, coca fields and wine cellar, and small-item stashes at the hostel, fishing village, jungle and caves) (31).
- **R40 [O] Throwables (bottles, wrenches, coins) sit in every room that has a guard**, so distraction is always possible.
- **R41 [S] Signature kill machines are planned into the environment from the pitch**: Mendoza grape press, Santa Fortuna hippo pool and coca shredder, Colorado hay baler, Hokkaido surgical robot (24, 31).

### 3.8 Hiding and body disposal
- **R42 [O] A container (closet, crate, dumpster, baler, open grave, water) within about 10 to 15 m of every planned takedown spot** [D for the distance]. Marrakesh places its disguise NPC next to a closet on purpose (31).
- **R43 [S] Soft hiding**: foliage and tall grass (Hitman 2 added foliage hiding) (16), elephant grass at Santa Fortuna (33), bushes and hedges at Dartmoor, tall grass in the Mendoza gardens (31).
- **R44 [O] Water and drops act as disposal** (Berlin canals, Sgàil cliffs, Dubai's roof edge, Bangkok's railing) (31).

### 3.9 Sightlines and cover
- **R45 [O] Long sightlines across public space; short, broken sightlines inside private space.** Colorado's "wide unobstructed sightlines" across hostile ground were a failure (35).
- **R46 [O/D] Each overlook (R3) sees at least 1 target's routine but not the inside of the personal space.** You can watch the target pass a window. You cannot snipe their bed.
- **R47 [S] Mirrors and reflections can expose the player** (16). Use glass and mirrors deliberately in private corridors.

### 3.10 Scale
- IOI gives **no official metric dimensions**. Known proxies: the cut Paris prototype was about 3x Sapienza and was incoherent (36); Mumbai and Miami "pushed the limits" and Hitman 3 chose "sharper and more focused" (17); Chongqing felt "small" because outside space was empty (35).
- **[D] Working ranges** (my estimates from playing; verify in your own blockout):
  - small or medium map 200 x 200 m;
  - large 350 x 350 m;
  - a key building's footprint 40 to 70 m with 3 to 5 floors;
  - a public plaza 25 to 45 m across;
  - streets and alleys 2.5 to 8 m wide;
  - breather pockets 8 to 20 m;
  - about 60 to 120 s of brisk walking from spawn to the first sight of the core landmark.
- For multiplayer, cap the walk from any spawn to the nearest objective at about 90 to 120 s [D].

### 3.11 Pacing
- **R48 [S] Alternate intensities**: public (rest), then private basic (breather), then professional (tense), then private basic (breather), then personal (peak) (2).
- **R49 [S] Open with a linear, low-stakes approach that frames the goal** (Dubai's outside-in prologue, Berlin's forest walk, Sapienza's sea-view start) (17, 21). After that, **fan out to at least 3 choices** at the first decision point (21).
- **R50 [S] "We don't have to pressure you up front"** (29). There is no time pressure at the start. Tension comes from going deeper.

### 3.12 Orientation (summary)
High ground over the next objective (R3), a hero landmark (R1), a light and colour gradient by tier (R7), readable professional signage (R6), audio beacons (Berlin bass, Dartmoor drafts) (17, 25), and a visible but locked shortcut that promises a future route (21).

---

## 4. Case studies

**Paris, Palais de Walewska (H1): festival.** The landmark is the palace façade, its imperial staircase and the catwalk. The catwalk spine connects the basement, backstage and upstairs auction (34). The palace was chosen from Palais de Luxembourg references because its ledges suit climbing (search snippet). Tiers: guest areas (main and banquet halls, catwalk, terraces), then staff (floors 0 and 1), then stage crew (floors 0 to 2), then auction (floor 3, frisked), then the attic (bodyguards). Routes between floors include an unguarded central basement stair, a back door that takes the IAGO invitation, rear gutters, the stair behind the catwalk, and a ladder from an eastern balcony to the attic (31). Lesson: put the event where every tier must brush past it. It was the vertical slice that set the NPC count and rules (15). Paris was cut from a city-sized map because it was incoherent (36).

**Sapienza (H1): onion and snail, the reference level.** It moves from town to grounds to Villa Caruso to an underground lab in sea caves. Landmarks are the church tower (sniper perch, camera terminal), the cliff mansion with its observatory, and the ruined castle. The mansion has about 6 to 8 entries and the lab about 6, including sewers, chimney and bridge hole (31). It is "the pinnacle" of Swiss cheese (15). The prototype took 2 weeks (10). De Santis's loop was cut to the mansion (13). Every district was reused by later missions (20). Weakness: the lab objective was linear (13).

**Marrakesh (H1): archipelago, the template.** A crowded market (the public sea) and rooftops, the Swedish consulate (Strandberg) and a military-held school (Zaydan), linked by a shoe-store tunnel to the consulate basement, where the targets can be made to meet (31, 34). Lesson: two fortresses plus a secret link plus lures. Weakness: reviewers felt it was less open, with fewer entryways than Sapienza, and the crowds slowed movement (search snippets of reviews; 30).

**Bangkok (H1): festival in a vertical hotel.** You arrive by water at the quay. The hotel goes from a grimy basement to a light garden on top (18). Gutters link guest rooms (102 to 202), with a tunnel and stairs, the recording studio and garden as the targets' domain, and a rooftop for a push kill (31). Lesson: a light gradient by floor, and a backstory per room.

**Colorado (H1): archipelago, the cautionary case.** A fully hostile farm: farmhouse (4 entries), barns gated to spec ops, a water tower as the only overlook, a basement bunker (31). Problems: flat, open and hostile at once, 4 mandatory targets, 3 of them clustered in the house, one scripted exit at launch (35). Lesson: **always include a public or semi-public zone and real verticality.**

**Hokkaido (H1): spiral festival with a tech gate.** Three tiers: resort with sushi, garden and onsen; surgery with staff quarters; morgue (14). Ledges link apartments, a pipe climbs to the doctors' room, the northern entrance is unguarded, the side entrance leads to the morgue and upper floors, and the operating theatre is reachable through 2 side doors as well as its guarded front (31). RFID clothing access is taught early, with marked doors (14). A cable car is the escape. Lesson: make "who may open which door" the level's grammar. The contrast between luxury and morgue makes it feel bigger (12).

**Miami (H2): festival split in two.** The race and paddock side is bright, open and crowded. The Kronstadt Bayside Center is a dark interior with upper floors for engineers only. The track splits the map, and **2 bridges plus several tunnels** stitch it back together. One bridge is a sniper perch with sightlines to both targets (6, 20). Robert is a dweller in the expo, Sierra a roamer whose race takes about 20 minutes, and the level fast-forwards to its end (6). There are 10 unlockable starts plus the default (31). Lesson: split for contrast, stitch with multiple crossings, and unify through branding.

**Santa Fortuna (H2): archipelago in the jungle, three domains.** The village (bus-stop start, bar, hostel, plaza with the statue unveiling, doctor's office, construction site), the Delgado mansion (main gate, wall climb, garage passage to caves, coca-field side entries; a basement with surveillance and torture rooms; exhibits on the ground floor; offices upstairs; a hippo pool) and the coca fields and jungle (sheds, botany lab, plane crash, shaman's hut). **A cave network** links the bar basement (a secret switch), the construction site, the mansion garage and the submarine cave. There are 7 mastery starts (31). Hiding uses elephant grass (33). Crowds are sparse. The village party, with band and statue, provides the festival beat (29, 32). Targets: Rico (dweller, mansion), Jorge (dweller, coca field), Andrea (roamer, town to compound) (31).

**Mumbai (H2): archipelago at maximum size.** A high-rise tower under construction (lobby on level 2, office on level 5, unfinished floors 6 and 7, a crane for a drop kill), the train yard (Vanya's throne, 3 levels plus roof), the slums with the Maelstrom roaming, an underground Crows hideout with 3 routes, a chawl with a rival sniper (31). It answered "how big can a Hitman level be" and became the performance benchmark (search snippets of the GamesRadar interview). Lesson: big maps need a roamer to tie the districts together, plus rooftop and ladder networks.

**Whittleton Creek (H2): suburban archipelago.** Six or more houses on the same floor plan with very different states: a party, an empty house for sale, a guarded house, a murder basement (34). The Griswold basement tunnel leads to Janus's basement (31). Lesson: modular reuse works if each copy changes its social rules. The weakness is identical layouts that confuse, and inaccessible shells (31, 35).

**Isle of Sgàil (H2): vertical festival.** The castle is "small on the surface" but tallest, at 9 floors (31). Outer entries: main gate, gutter, guarded passage, side gate, initiate door and gutter, sewer. Stairs plus gutter pipes on every tower (20, 31). Lesson: stack the onion upward.

**Dubai (H3): vertical festival.** Keywords "inviting / open and airy / on top of the world". The start was changed to outside-in to give vertigo (17). The lobby teases the targets and a locked shortcut, then the stairs split 3 ways (21). Seven floors, 4 persistent shortcuts, electronic windows onto ledges (31). Lesson: a one-way shortcut shown early as a promise.

**Dartmoor (H3): onion, small and dense.** A 3-floor manor with a restricted top floor, a frisked front door, open windows, a delivery door, balcony and gutter climbs, a roof ladder, secret rooms (31). Guidance through audio and chatter instead of markers (17). Lesson: a small map can hold depth through secret connections inside one house.

**Berlin (H3): festival in an industrial ruin.** A forest approach (loneliness), then a rave (17, 25). The club has **8 entries** of every type, with an overpass and tunnel from the biker building (31). No mission stories; 10 agents, kill 5 (26). Lesson: a crowd as both cover and fog, and target identification as the puzzle.

**Chongqing (H3): vertical archipelago.** The Block (Hush) above ground and the underground ICA facility (6 entries, levels 0 to -3, vents, dongle tiers) (31). Players praised the vertical climbing but criticised the empty street shells (35).

**Mendoza (H3): onion in a vineyard.** Grape fields (gaucho cliff path), a 3-level winery (party floor, production, cellar), the villa with its attic sniper spot and a level-3 cellar, silos with snipers, a tunnel from the parking lot to the silos, 3 one-way shortcuts, 7 starts (31). The reference was altered for verticality and vegetation, and the grape press was planned as a kill from the start (24).

**Haven Island (H2 DLC), Ambrose (H3 DLC), Carpathian (H3).** Haven's server room has 2 hidden entrances (35) and 4 servers that each pull scientists to them; break all 4 and every target comes to the room (20). That is environment-driven emergence. Ambrose uses natural rock zones, traversable stilt huts and two factions on edge (23, 35). Carpathian is a linear train, criticised as "out of place". **Do not use it as a sandbox model** (17).

---

## 5. Biome notes for the three original maps

### 5.1 Colombian jungle hacienda (lessons from Santa Fortuna, Ambrose, Mendoza)
- **Topology**: an archipelago with an onion core [D]. Three domains, each with a different disguise ecology: (a) a riverside **pueblo** as the public sea with a festival beat (a fiesta, a procession, a saint's-day stage), (b) the **hacienda** as an onion (outer wall, patio garden, a 2- or 3-floor casa, cellar or panic room), (c) a **work landscape** (coffee or cacao terraces, a drying shed, a processing barn, an airstrip) as the professional zone.
- **Natural borders**: the jungle canopy, a river gorge and cliffs set the boundary and split the zones. Goyon: no artificial borders (23). Untamed land is **hard to read**, so cut readable trails, add fences, power lines and a river as handrails (23).
- **Underground swiss cheese**: a cave or old-mine network linking a cantina cellar, a quarry or construction site, the hacienda garage and a river dock is the trilogy's most reusable jungle idea, as in Santa Fortuna (31). Do not copy the node set. Pick your own three or four.
- **Hacienda entries (at least 5)**: main gate (frisk); service gate by the kitchen and stables; a terrace-wall climb from the plantation (a vine trellis); an underground route from the caves; a one-way balcony ladder. Add a riverside boathouse as a sixth for multiplayer.
- **Verticality**: terraced slopes act as the snail (switchback paths with steps), with a casa on stilts or a hillside, a water tower or bell tower overlook, and a mirador over the pueblo (R3).
- **Hiding**: tall grass and banana leaves (soft), drying sheds (hard), the river (disposal). Keep crowds sparser than a city festival (33), but give the plaza a dense event.
- **Signature machine**: a coffee roaster, a sugar-cane press or a river turbine (R41).
- **Watch-out**: Colorado's failure. The plantation must not be flat, open and fully hostile. Give it rows that break sightlines (Mendoza's vineyard rows plus tall grass, 24, 31) and a semi-public workers' area where the pueblo outfit is legal.

### 5.2 Desert: oasis town plus mesa fortresses (lessons from Marrakesh, Dubai, Mendoza, Mumbai)
- **Topology**: a classic archipelago (Marrakesh template) (34). The oasis town is the public sea (a souk with a market day as the festival, a palm grove, a well plaza), with 2 or 3 mesa fortresses each holding its own disguise rules and target.
- **Fix Marrakesh's weakness**: give each fortress at least 5 entries (R12), and put the secret link (a qanat tunnel, an old dry riverbed, a cliff stair cut in the rock) between town and fortress and between fortresses (R14).
- **The mesa as a vertical snail**: a switchback road (social), a rope-and-ledge climb face (climb), a cliff cave or cistern (underground), a cable or goods lift (service), an airstrip or helipad on top (personal). Dubai's lesson: **start outside and below and work in and up** to sell height (17).
- **Roofs as a second road** in the town (R22), with awnings for shade cover. The souk crowd hides you but slows you (30).
- **Sightlines**: desert means long open views. That is the Colorado trap. Break them with dunes, wadis, palm groves, rock fins and wind-blown cloth. Each mesa gets an overlook from a neighbouring rock (R3), but the inside of the personal space can't be seen from it (R46).
- **Light and colour**: the bright, hot, saturated town is public; cool, shaded fortress interiors are professional; one luxurious contrasting core (a pool terrace, a glass penthouse) is personal. Contrast makes it feel bigger (12).
- **Signature machine**: a well winch, a solar mirror array, a cable lift, a date press.

### 5.3 Snowy mountain: alpine lodge gala (lessons from Hokkaido, Sgàil, Colorado, Carpathian)
- **Topology**: a festival inside a spiral [D]. The gala (ballroom, terrace, bar) is the public spine. The lodge is built into the slope in 3 tiers like Hokkaido (14): guest tier above (suites, spa, observatory), staff and operations tier (kitchens, ski room, security, laundry), and a grim underbelly (boiler, generator, cold storage, an old mine or bunker). Use the Hokkaido contrast (luxury against stark) to make it feel bigger (12).
- **Snail connections**: exterior slopes, ski paths, covered walkways and stair towers wrap the building. Inside, a grand stair (social) plus a service stair plus a dumbwaiter or lift shaft (R26).
- **Ledge network**: balconies along the suites linked by ledges and snow-covered roofs (Hokkaido's apartment ledges, R18). A gutter or icicle-free drainpipe to the observatory.
- **Entry diversity for the lodge (at least 6)**: front door (frisk for gala guests), a delivery bay by the kitchen, the ski room (staff), a funicular or cable-car station (tool or schedule gated), a boiler-room coal chute (underground), a one-way terrace ladder.
- **Readability**: white on white kills orientation. Use warm interior window light as a beacon (R7), with coloured trail markers and signposts on the pistes (R6). The landmark is a ski-jump tower, a radio mast, or an observatory dome on the ridge (R1).
- **Exterior is not a hostile plain**: Colorado and the Siberia sniper map show that snowfields plus hostile guards equals a gallery shoot. Put the hostile perimeter on broken ground (pine stands, rock, outbuildings) and make the village, chalet street or ski-lift queue public.
- **Avoid Carpathian's linearity**. A train or funicular can be an opportunity or an exit, never the level (17).
- **Signature machine**: a snow groomer, a cable car (Hokkaido's escape), an avalanche-control cannon, a sauna or cryotherapy chamber.

---

## 6. Scoring rubric: "Hitman quality" (0 to 20)

How to use it: apply each criterion to (A) the written map design document and (B) screenshots of the built map: a top-down layout, overview shots and in-level shots. Score each criterion 0, 1 or 2.
- **2** = every pass condition is evidenced.
- **1** = most are, or the evidence exists in only one of A or B.
- **0** = missing, or a fail condition is present.

A map reaches "Hitman quality" at **18/20 or more with no criterion at 0**. Criteria 3, 4 and 6 are gates: they must score 2.

### C1. Topology and spiral flow
- **Pass (doc)**: names a topology (onion, festival or archipelago) and draws the disguise-validity zones and their connections. Shows a spiral or snail circulation where layers are linked by at least 2 slopes or stairs each. States that there are no dead ends, and lists the boundaries as natural or visual edges.
- **Pass (screens)**: the top-down shows a loop around a core, with no corridor ending without an exit (toilets excepted). Edges read as terrain or architecture, not invisible walls.
- **Fail**: a linear path, a hub with spokes that dead-end, or invisible walls.

### C2. Landmarks and orientation
- **Pass (doc)**: 1 hero landmark visible from at least 70% of exterior space, 1 secondary landmark per district, at least 1 overlook per key building, a light and colour gradient by security tier, and a wayfinding plan (signage, audio beacons).
- **Pass (screens)**: the hero landmark appears in at least 3 of 5 random exterior shots. Overlook shots show the target's routine area. Lighting visibly shifts between public and private areas.
- **Fail**: homogeneous districts, no vista, or players need a map marker to orient.

### C3. Social-space gradient and security tiers (gate)
- **Pass (doc)**: a colour-coded map with all 6 social space types, 3 to 5 disguise tiers with their legal zones, enforcers listed per tier, private-basic breather pockets between professional zones, and personal spaces kept rare (10 to 15%).
- **Pass (screens)**: you can read which spaces are public or staff-only from architecture and props alone (kitchens look like kitchens, signage, barriers, uniforms).
- **Fail**: the whole map is hostile (Colorado), or the whole map is public with no backstage.

### C4. Key building entry diversity (gate)
- **Pass (doc)**: each core building has at least 5 entries and each secondary fortress at least 3, covering at least 4 of the 6 entry types (social front, service, climb, underground, tool-gated, one-way shortcut). At least 1 secret link between zones. Every frisked entry has an unfrisked alternative. Each entry has a stated risk and time trade-off.
- **Pass (screens)**: each listed entry is visible and readable in-game (a pipe, a door, a hatch, a window), with at least 1 shortcut shown locked from the outside.
- **Fail**: one door plus a window, or several entries of the same kind.

### C5. Verticality and traversal layers
- **Pass (doc)**: at least 3 height layers in the key area (underground, ground, upper or roof). Every tier is reachable by at least 2 kinds of connector (stairs plus a climb). Ledge or balcony links between private rooms, vents in the core, rooftops as a secondary network where the setting allows.
- **Pass (screens)**: climbable affordances read clearly (pipes, ledges, vines, ladders), and section or elevation views show stacked play spaces.
- **Fail**: a flat ground plan with stairs only, or decorative heights you can't use.

### C6. Interior architecture (gate)
- **Pass (doc)**: the key building has 3 or more floors plus a basement or attic, 15 to 30 enterable rooms, a grand stair plus at least 1 service or hidden stair, at least 1 full loop per floor, every room with 2 or more exits (toilets excepted), and secret passages linking public and private rooms. No empty enterable shells.
- **Pass (screens)**: floor plans show loops. Interiors have distinct room functions and dressing.
- **Fail**: corridor-and-cell plans, single-exit rooms, or empty dressed shells.

### C7. NPC ecology and routines
- **Pass (doc)**: targets defined as dweller or roamer with domains. Routine loops of about 2 to 6 minutes per area, each with 2 to 4 redirect levers. Paired static guards at chokes, lone NPCs placed deliberately as disguise sources beside hiding spots, crowd density zones, factions or enforcers. Escort-before-shoot behaviour. Every NPC type has a stated purpose.
- **Pass (screens)**: crowds in public spaces, staff working in professional ones, guards posted at the doors that the entry list says are guarded.
- **Fail**: guards scattered at random, isolated posts with no reason, or empty streets.

### C8. Opportunities, items and signature setups
- **Pass (doc)**: at least 3 environmental kill or "accident" setups per target, including at least 1 signature machine. Items placed as hints next to their use (poison by a drink). At least 1 crowbar, keys carried by NPCs, a code or camera puzzle. Throwables in every guarded room. Layered guidance (chatter, then optional story, then challenge hints) with no forced markers.
- **Pass (screens)**: interactable setups are visible and plausible (a chandelier rig, a broken railing, a machine), with tools staged near their use.
- **Fail**: only weapons, or opportunities that need UI markers to be found.

### C9. Hiding, sightlines, cover and pacing
- **Pass (doc)**: a hiding container within about 15 m of every planned takedown spot, soft hiding (foliage, crowds), disposal options (water, drops, machines). Long sightlines only across public space and short broken ones inside private space. The intensity sequence alternates (public, breather, professional, breather, personal). A low-stakes opening that fans out into 3 or more choices.
- **Pass (screens)**: sightline overlays or shots show occlusion in private areas, breather pockets exist between hot zones, and the first fork is visible from the spawn approach.
- **Fail**: open hostile plains (Colorado or desert flat), or nowhere to stash bodies.

### C10. Replayability, contrast and identity
- **Pass (doc)**: 1 to 3 theme keywords, at least 1 strong internal contrast (light against dark, luxury against grim), a unifying motif, 5 to 10 alternative starting locations or multiplayer spawns spread over the tiers, persistent one-way shortcuts, and every district useful in at least 2 scenarios (people, not polygons). Research-based believability (structures that bear their weight, plausible connections between zones).
- **Pass (screens)**: districts are recognisable in a blind test (someone can name the area from one screenshot), and the motif repeats across zones.
- **Fail**: a generic look, one-off districts, a single spawn, or nothing that changes on a second run.

### Multiplayer addendum (recommended checks, not scored)
- No single choke: at least 2 independent routes from each spawn cluster to each objective, so players do not funnel into the same door.
- Contested overlooks: each overlook can itself be seen from at least 1 other point, so snipers can be countered.
- Disguise legality and frisk zones are communicated identically for all clients. The host is authoritative over NPC suspicion, which matches the project's host-authority rule for AI.

---

## 7. Five-line digest for the team
1. Build a **snail**: layers that wrap a core, joined by slopes and stairs, with no dead ends and hidden edges.
2. Then **Swiss-cheese** it: at least 5 entries of at least 4 kinds per core building, a secret link between zones, and one-way shortcuts.
3. Lay down the **social ladder** (public, then breather, then professional, then personal) and let disguises be the keys. Never make the whole map hostile.
4. Fill it with **people who have jobs**: dwellers, roamers, paired chokes, deliberately placed lone disguise sources, crowds that both hide and hinder.
5. Use **landmarks, overlooks, light and sound** for orientation instead of markers, and design every district for reuse.
