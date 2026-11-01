# Qasr al-Rimal: desert oasis and mesas (map design, v3)

**Keywords:** audience day, wind, the fortress on the rock. **Motif:** ochre adobe, blue doors,
striped awnings. **Contrast:** glaring souk against dim citadel halls and the cold glass
penthouse. **Topology:** archipelago (Marrakesh, fixed: ≥ 5 entries per fortress, two secret
links, a social layer inside). Terrain 500 × 500 m, play ~380 m. Borders: dune sea (S), canyon
walls (N, E), salt flat and ruined caravan road (W).

## 0. Rules used (code systems only)
- **Tags** (`SecurityZone`): **[P]** Public · **[R]** Restricted, no pass (breather) · **[R/Gu]
  [R/St] [R/Se]** Restricted, legal with Invitation (*petition seal*) / Staff pass / Keycard ·
  **[H/St] [H/Se]** Hostile, legal with that pass · **[H/Pr]** Private, never legitimised. Static:
  **audience day lasts the whole match**. Restricted = 4 s escort, then fire.
- **Enforcers (see through every pass):** Guest: the **governor's guard** (4). Staff: the
  **chamberlain**. Security: **Adjutant Karim**, the keep-roof and gantry lookouts.
- **One shared trick:** the keep's evaporative cooler is the **air handler** (gas).

## 1. Zones and the snail (every outdoor area tagged)

```
 N canyon walls -------------------------------------------------------------
                 THE NEEDLE +60 [R] (cliff path from the wadi)
 [C] CITADEL, Great Mesa +42                    [D] RADAR, Little Mesa +38 [H/Se]
   first court (S) [R/Gu] · second court (E)       bunker, dish gantry, motor pool
   [R/St] · third court (W) [R/St]  <- rope bridge ->  fuel tanks; berms, containers
   bastions [R] · north terrace [R] · keep          ladder face [R]
   ^ 4 switchback hairpins [P] · cliff chimney + scaffold [R] · well shaft [R]
   ^ goods lift (wadi base station [R/St] -> third court)
 [B] PALM GROVE + WADI -4 m: grove paths [P], irrigation channels [P], date press
     [R/St], reed beds [R], rock arch [P], abandoned kasbah [R] (halfway up)
   ^ footbridges, two qanat branches [R]
 [A] AIN ZARQA 0 m: well plaza [P], two covered souk streets [P], tea house [P],
     caravanserai [P], mosque court [P], minaret [R], bus station [P], roof planks [P]
 S dune sea -----------------------------------------------------------------
```

**Spiral:** bus station → souk → grove → wadi → hairpins wrap the mesa → three courts climb to the
keep → roof; the Little Mesa joins by rope bridge and by the wadi ladder face. **Water logic:** the
spring pool feeds the grove channels downhill; the qanat is a **dry smugglers' tunnel** (two
branches) ending in a **well shaft** (two ladders) up to the keep cistern. Secret link 2: the
**smugglers' cleft**, radar bunker → north terrace. **No dead ends:** alleys loop, qanat branches
rejoin, the cleft has two mouths.

## 2. Social ladder

| Andersen type | Where | Tags | Share |
|---|---|---|---|
| Public basic | well plaza, souk, bus station, caravanserai | [P] | 20 % |
| Public purpose | roof planks, grove paths, hairpins | [P] | 8 % |
| Public rule | mosque, tea house, **first court, gate hall, petitioners' and audience halls** | [P], [R/Gu] | 10 % |
| Private basic (breathers) | reeds, kasbah, bastions, cistern landing, cells, linen room | [R] | 22 % |
| Private professional | date press, second/third courts, kitchen wing, scribes, archive, officers' floor, radar | [R/St], [R/Se], [H/Se] | 28 % |
| Private personal | vault, war room, penthouse study and bedroom | [H/Pr] | 12 % |

## 3. Landmarks, overlooks, light
- **Hero:** the **keep**, 56 m over the town, wind tower and glass penthouse. On a 25 m grid
  (≈ 210 exterior points) **76 %** see it; blind: qanat, covered souk, cleft. Both souk streets run
  axial on it: the **Crossing of Scales** and the **Dyers' Arch** frame the keep in their arches.
- **Secondary:** minaret (A), rock arch, date press (B), radar dome (D), the Needle.
- **Overlooks (each seen from another):** **Needle** → first court and pool terrace (tea), not the
  bedroom; **kasbah roof** → hairpins and gate; **radar gantry** → the keep-roof helipad, the
  map's **only helipad**.
- **Sightlines:** long only over roofs and dunes. Mesa face broken by gullies, scaffolding, caves;
  courts by stables, trucks, colonnade, hung carpets; radar by berms; the light well by
  mashrabiya screens.
- **Light:** white glare → green grove shade → dim slit-lit halls → cold marble penthouse.
  **Wayfinding:** call to prayer, press donkey bell, blue doors on public ways.

## 4. Key building: the keep
34 × 34 m round a screened light well. D door, K keycard/keypad, S stair, V vent, C camera, REC
recorder, (bN) entry.

```
CELLAR -3.6                               GROUND 0
+-----------+-----------+-----------+     +-----------+-----------+-----------+
| CISTERN   | COOLER    | ARMOURY   |     | KITCHEN   | MESS HALL | GRANARY   |
| LANDING   D PLANT S C6K [H/Se]    |     | [R/St](b2)D [R/St]  S D [R/St]    |
| [R] (b3)  | [R/St] V  |           |     | lift door | V         | chute out |
+-----D-----+-----------+-----K-----+     +-----D-----+-----------+-----D-----+
| OLD CELLS |  CISTERN  | (keypad)  |     | PETITION- |  INNER    | GUARD     |
| [R]       |  water,   | VAULT     |     | ERS' HALL D  COURT   D ROOM      |
|           |  disposal | [H/Pr]    |     | [R/Gu]    |  [R/Gu]   | [H/Se]    |
+-----K-----+-----------+-----D-----+     |           +-----D-----+-----D-----+
| MONITORING ROOM [H/Se] REC  airtight|     |           D GATE HALL | LINEN [R] |
| door D to the vault (lever here)  |     |           | S [R/Gu]C1D (b4) pad. |
+-----------------------------------+     +-----------+---D (b1)--+-----------+
FLOOR 1 +3.6                              FLOOR 2 +7.2
+-----------+-----------+-----------+     +-----------+-----------+-----------+
| SCRIBES'  | SERVERY   | ARCHIVE   |     | OFFICERS' | RADIO ROOM| WAR ROOM  |
| OFFICE    D [R/St]  S D [R/St]    |     | QUARTERS  D [H/Se]  S K [H/Pr]    |
| [R/St](b6)| N gallery [R/St] C3   |     | [R/Se]    | V         |           |
+-----D-----+-----------+-----D-----+     +-----D-----+-----------+           |
| (scribes) |  LIGHT    | COUNTING  |     | ANTEROOM  K  GALLERY  D           |
|           D  WELL     K ROOM      |     | [R/Se] S* |  [R/Se] C4|           |
|           |  gallery  | [R/Se]    |     +-----D-----+-----------+-----D-----+
+-----D-----+--D--------+-----D-----+     | STAIR HEAD| OFFICERS' MESS [R/Se] |
| WAITING   D AUDIENCE HALL [R/Gu]  |     | S private | south balconies (b5)  |
| GALLERY * | S grand stair, throne |     | stair up  D                       |
| [R/Gu]    | S gallery half [R/Gu] |     +-----------+-----------------------+
+-----------+-----------------------+
FLOOR 3 +10.8 penthouse                   ROOF +14.4
+-----------+-----------+-----------+     HELIPAD [H/Se] C5 · wind tower V (b7)
| DINING    | PANTRY    | STUDY     |     lookout post · zipline -> kasbah roof
| [H/Se]    D [H/St] S V D [H/Pr]    |
+-----D-----+-----------+-----D-----+
| VESTIBULE D  GLASS    D BEDROOM   |
| [H/Se] C5 |  GALLERY  | [H/Pr]    |
| private S |  ring     |   wc      |
+-----D-----+-----D-----+-----D-----+
| MAJLIS [H/Se] glass salon  D POOL TERRACE (outdoor) |
+-----------------------------------------------------+
```

- **30 enterable rooms** (6 + 7 + 6 + 5 + 6), 4 personal (13 %); every room ≥ 2 exits (bedroom
  toilet excepted).
- **Loops:** cellar landing → cooler → armoury → vault → monitoring → cells; ground and F1 round
  court and well gallery (F1 gallery: Guest south half, Staff north half, a door between); F2
  gallery ring; **F3**: six rooms round a glass gallery ring plus the majlis-terrace loop.
- **Stairs:** grand (gate hall → audience hall → F2 stair head), **service** in the north core
  (cellar → roof), private (F2 → F3 vestibule), well-shaft ladders.
- **Secret passage:** the waiting gallery's **calligraphy niche** (*) hides a scribes' stair to the
  F2 **anteroom** [R/Se], which opens on the gallery only through a **keycard door**.
- **Vent:** wind-tower duct (1 m crawl): roof → F3 pantry → radio room → mess hall → cooler plant.
- **Camera puzzle:** C1-C6 each call 2 guards; the **recorder** (monitoring room) shuts all six.
  The **vault keypad** CodeNote is pencilled in the **tithe ledger on the date-press scale desk**.
- **Goods lift, no chicken-and-egg:** the Keycard panel's **operator** smokes alone at the **wadi
  base station**; a spare card lies in the date-press office.

### Citadel-wall entries (into the courts)

| # | Entry | Kind | Lands | Risk / time |
|---|---|---|---|---|
| W1 | Main gate; frisk pair checks the seal | Social front | first court (S) [R/Gu] | safe; seal ~3 min + 60 s queue |
| W2 | Goods lift from the wadi (Keycard) | Service + tool | third court (W) [R/St] | camera C2, cook; 40 s ride |
| W3 | Cliff chimney + scaffolding | Climb | west bastion [R] | 45 s, seen from kasbah and keep roof |
| W4 | Smugglers' cleft from the radar bunker | Underground | north terrace [R] | 3 min, isolated; starts hostile |
| W5 | Rope bridge from the Little Mesa | Crossing | NE bastion [R] | 25 s exposed under the gantry lookout |
| W6 | Bricked SE postern (Crowbar from the scaffolding) | Tool-gated | second court [R/St] | loud 10 m, drill squad 20 m away |

### Keep entries (into the building)

| # | Entry | Kind | Lands | Risk / time |
|---|---|---|---|---|
| b1 | Gate-hall door from the first court | Social front | ground S | camera C1, governor's guard pass by |
| b2 | Kitchen delivery door, third court | Service (Staff) | ground NW | camera C2, cook, chamberlain |
| b3 | Qanat → well shaft → cistern landing | Underground | cellar NW | 4 min, isolated; disposal in the cistern |
| b4 | Linen-room yard door (Padlock, Staff pass) | Tool-gated | ground SE | guard room next door |
| b5 | South ledges between officers' balconies (colonnade roof) | Climb | F2 S | 20 s, seen from the first court |
| b6 | West drainpipe from the bastion to the scribes' window | Climb | F1 W | 15 s, out of court view |
| b7 | Wind-tower climb from the second-court scaffold → roof → duct | Climb + vent | roof / F3 pantry | 60 s, keep-roof lookout |
| b8 | Grain chute to the wadi; roof zipline to the kasbah | One-way exits | out | instant, no return |
| b9 | Officers' balcony **DropLadder** (F2 south) lowered into the second court, stays down for everyone | One-way shortcut | second court -> F2 S | first opened from inside; then a 15 s climb for all |

**Breather on the upper floors:** the disused F2 bunk room (behind the officers' quarters) is
Restricted with no access required — a pocket to wait in between the Security rooms.

## 5. Secondary fortresses
- **Radar** [H/Se] (bunker, gantry, motor pool): front gate (Keycard), motor-pool ramp (Padlock),
  wadi ladder face, the cleft, dome vent.
- **Date press** [R/St]: yard gate, loading door, loft hatch via a palm trunk, channel culvert.
  Signature: the **screw press**.
- **Caravanserai** [P]: looping gallery court; gate, stable door, roof planks, well.
- **Town:** 16 enterable lit houses, ~44 shut (blue doors chained).

## 6. NPC ecology: 120 simulated + ambient souk extras (citadel 27 armed, radar 6)

| Zone | Groups (count) | Sum |
|---|---|---|
| Town | souk crowd 28 · sellers, tea-house owner, storyteller 6 · tea-house petitioners (seals) 8 · caravan traders + stable hands 5 · police pair 2 · bus-station clerk 1 | 50 |
| Grove + wadi | farmers 4 · press foreman (Staff pass) 1 · press hands 3 · lift operator (Keycard) 1 | 9 |
| Citadel garrison | gate frisk pair 2 · bastion patrol pairs 6 · court drill squad 4 · interior pairs (grand stair head, war-room door, vestibule, armoury) 8 · guard-room shift 2 · keep-roof lookout 1 · governor's guard 4 | 27 |
| Citadel civilians | petitioners 10 · motor-shed mechanics 2 · cook, 2 kitchen hands, 2 servants, archivist, monitoring operator, chamberlain (enforcer), 2 scribes 10 | 22 |
| Radar | guard pairs 4 · technicians 2 · Adjutant Karim (enforcer) 1 · gantry lookout 1 | 8 |
| Targets | 4 | 4 |
| **Total** | | **120** |

| Target | Type, loop | Lethal setups (≥ 3) | Redirect levers |
|---|---|---|---|
| **Governor Nasser al-Hadd** | dweller: study 4 → war room 3 → audience throne 3 → terrace tea 3 | cooler **gas** during the war room; **oleander** in his tea; war-room gas heater (explosive); Needle shot at tea | study phone; war-room radio; colocynth → bedroom toilet, alone |
| **Treasurer Idris** | roamer: vault 4 → archive 2 → counting room 3 → goods lift → **date-press tithe 4** | **screw press** (he weighs the tithe on its bed); **airtight vault door** (lever in the monitoring room); **archive shelf** collapse | counting-room phone; archive radio; car alarm on the tithe truck |
| **Arms buyer Selin Arda** | roamer: bus station 3 → tea house 4 → caravanserai 3 → audience hall 4 | **tea-house brazier** (rigged canister; she sits at it for the storyteller); oleander tea; caravanserai fuel drums | bus car alarm; tea-house radio; caravanserai phone |
| **Colonel Rashed** | roamer: bunker 4 → dish gantry 2 → rope bridge 2 → keep guard room 3 | **dish drive**: release the brake, the dish sweeps him off the gantry; motor-pool fuel tanks; oleander coffee | bunker radio; motor-pool generator; jeep car alarm |

## 7. Opportunities, items, guidance
- **Desert-native kit:** screw press, airtight vault door, archive shelf, tea brazier, dish drive,
  fuel tanks, gas heater; **oleander** (grove channel banks, lethal), **colocynth** (wadi, emetic);
  pool chlorine drums by the cooler hint the gas.
- **Items:** Crowbar (scaffolding); Staff passes (foreman, cook, servants); seals (clerk,
  petitioners); Keycards (operator, colonel, monitoring operator, treasurer, spare); CodeNote
  (ledger). Throwables in every guarded room.

| Takedown spot | Lone NPC | Nearest container |
|---|---|---|
| Wadi base station smoke spot | lift operator (Keycard) | crate 5 m |
| Tea-house back room | petitioner (seal) | wardrobe 3 m |
| Date-press office | foreman (Staff pass) | crate 4 m |
| Kitchen larder | cook (Staff pass) | dumpster, third court 8 m |
| Archive | archivist | crate 3 m |
| Linen room | servant | laundry cart 2 m |
| Monitoring room | operator (Keycard) | wardrobe 3 m |
| Caravanserai stable | stable hand | crate 4 m |
| Radar motor pool | technician | dumpster 6 m |
| Penthouse pantry | servant | laundry cart 4 m |

Soft hiding: reeds, souk crowd; disposal: cistern, canyon drop.

**Guidance:** ChatterSpots, then contract page, then pause-map POIs. Pairs reveal: the ledger code
(press hands), the operator's smoke (cooks), the tithe weighing (foreman), the sagging shelf
(scribes), the buyer's brazier (tea-house owner), the dish brake (technicians), chlorine by the
cooler (servants), oleander (farmers).

## 8. Starts, pacing, multiplayer, reuse

| Start | Tag / pass | Fans out to |
|---|---|---|
| Bus station | [P] | souk / qanat mouth / roof planks |
| Caravan road | [P] | caravanserai / grove / town wall |
| Dune edge | [R] | qanat exit / grove / wadi |
| Grove ford | [P] | press / wadi / hairpins |
| Wadi arch | [P] | Needle path / well shaft / radar track |
| Canyon trail | [R] | radar ladder face / cleft mouth / Needle |
| **Tea-house petitioner** | [P], Invitation | main gate / souk / bus station |
| **Press hand** | [R/St], Staff pass | press office / goods lift / grove |
| **Water carrier, base station** | [R/St], Staff pass | goods lift / chimney / Needle path |

- **Intensity:** souk → grove (breather) → courts → kasbah (breather) → audience → penthouse.
- **Multiplayer:** ≥ 3 routes per start cluster; two qanat branches, two shaft ladders; alerts
  stay in their zone.
- **Reuse:** souk = cover, chase, bus extraction; wadi = route, Needle-vs-gantry duel, disposal;
  audience hall = social approach, throne kill, niche escape. **Extractions:** helicopter (keep
  roof), car, bus, qanat.
