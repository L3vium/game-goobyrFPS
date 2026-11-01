# Weisshorn: alpine gala in the snow (map design, v3)

**Keywords:** gala night, snow, the lodge on the ridge. **Motif:** dark timber, stone, amber
windows. **Contrast:** the glittering ballroom against the boiler underbelly, the mine and a
cold-war bunker. **Topology:** onion with a festival core (the gala spine inside the hotel).
Terrain 500 × 500 m, play ~360 m. Borders: glacier (N), avalanche cliffs (E), frozen lake and
pine wall (SW).

## 0. Rules used (code systems only)
- **Tags** (`SecurityZone`): **[P]** Public · **[R]** Restricted, no pass (breather) · **[R/Gu]
  [R/St]** Restricted, legal with Invitation / Staff pass · **[H/Se]** Hostile, legal with Keycard ·
  **[H/Pr]** Private, never legitimised. Static: **the gala lasts the whole match**. Restricted =
  4 s escort, then fire.
- **Enforcers (see through every pass):** Guest: the 4 bodyguards. Staff: the **duty manager**,
  **Chief Brandt** (a target). Security: Brandt, the clock-tower and ski-jump lookouts.
- **One shared trick:** the ballroom **chandelier**.

## 1. Zones and the snail (every outdoor area tagged)

```
 N glacier, summit ----------------------------------------------------------
 [D] OBSERVATORY + BUNKER +70 [H/Se]: dome, antenna huts, bunker -1/-2,
     command room [H/Pr] · ridge path, outcrops [R] · cable-car top [H/Se]
       ^ cable car (Keycard) from the delivery yard   ^ ridge path   zipline v
 [C] HOTEL PLATFORM +45 (front faces south, slope rises north):
     funicular top + front terrace +8 [R/Gu] · ballroom terrace [R/Gu] ·
     smoking terrace [R/Gu] · ski-room terrace +4 [R/St] · lower terrace 0 [R/St]
     delivery yard [R/St] · spa garden + greenhouse [R] · crevasse wall (N) [R]
       ^ guest funicular   ^ service road switchbacks   ^ chairlift (top: east)
 [B] SKI AREA +10 -> +40: pistes, lift queue, mid-station cafe [P] · chairlift
     bottom station + bullwheel [R/St] · groomer garage + mine mouth [R/St] ·
     ski-jump tower [R] · pine copse, mine hut [R]
       ^ footpaths, chairlift
 [A] SANKT ANNA 0 m: chalet street, rink, church, Gasthaus, ski rental,
     valley station [P] · spire [R] · Gasthaus cellar [R/St] · boathouse [R]
 S frozen lake --------------------------------------------------------------
```

**Spiral:** chalet street → funicular / chairlift → pistes and service road wrap the slope → the
hotel's three terraces step up → ridge path / cable car → observatory. **Secret links:** the
**mine gallery** (groomer garage → cold store) and the **bunker escape tunnel** (→ under the
ski-jump). **No dead ends:** pistes loop, both tunnels have two mouths, hotel floors are rings.

## 2. Social ladder

| Andersen type | Where | Tags | Share |
|---|---|---|---|
| Public basic | chalet street, rink, lift queue, pistes | [P] | 18 % |
| Public purpose | ski rental, mid-station cafe, valley station | [P] | 8 % |
| Public rule | church, Gasthaus, **lobby, ballroom, mezzanine, bar, spa, smoking terrace** | [P], [R/Gu] | 12 % |
| Private basic (breathers) | boathouse, copse, mine hut, spa garden, **linen room, wax store, suite A2, attic** | [R] | 22 % |
| Private professional | kitchens, ski room, laundry, boiler, lift stations, groomer garage, VIP floor, bunker | [R/St], [H/Se] | 28 % |
| Private personal | presidential suite, private cinema, banker's suite, bunker command | [H/Pr] | 12 % |

Hotel **Restricted breathers:** the L1 linen room and suite A2; the smoking terrace is public rule.

## 3. Landmarks, overlooks, light
- **Hero:** the **clock tower** (+34 over the platform) beside the lit **ski-jump**. On a 25 m grid
  (≈ 200 exterior points) **74 %** see the clock face; blind: mine, boathouse shore, copse floor.
  Unenterable rooms keep curtains drawn.
- **Secondary:** observatory dome (D), church spire (A), chairlift wheel house, mid-station (B).
- **Overlooks (each seen from another):** ski-jump → ballroom terrace and presidential balcony,
  never the bedroom; spire → funicular top; dome catwalk → ballroom-roof helipad.
- **Sightlines:** long only on the chalet street and lake; pistes capped at 40 m by pine bands,
  fences, huts; ballroom broken by columns and balustrade.
- **Light:** amber interiors as beacons, blue moonlit snow, fluorescent underbelly, red bunker.
  **Wayfinding:** gala band, boiler thud, piste signs, funicular bell.

## 4. Key building: Grand Hotel Weisshorn
70 × 30 m on a slope: **west wing** (lobby and spa, two suite floors stacked only over them),
**east wing** (ballroom **double height +8 → +16**, mezzanine +12, **nothing above** but roof and
helipad); underbelly and staff tier under both. D door, K keycard/keypad, S stair, V vent, C
camera, REC recorder, (bN) entry.

```
L0 UNDERBELLY 0 (opens south on the lower terrace)
+-----------+-----------+-----------+-----------+
| LOCKERS   | BOILER    | COLD STORE| WAX STORE |
| [R/St]  S D [R/St]    D [R/St]    D [R]       |
| svc stair |           | mine K(b5)|           |
+-----D-----+-----------+-----D-----+-----D-----+
| LAUNDRY [R/St] chute lands  D  service corridor [R/St] |
+---------------------------------------------------------+
L1 STAFF TIER +4 (opens south on the ski-room terrace)
+-----------+-----------+-----------+-----------+
| SKI ROOM  | SECURITY  | KITCHENS  | WINE      |
| [R/St](b3)K OFFICE  C5K [R/St](b2)D CELLAR    |
|           | [H/Se] REC| goods lift| dumbwaiter|
+-----D-----+-----D-----+-----D-----+-----D-----+
| LINEN [R] D  staff corridor [R/St]  S svc    D  CANTEEN [R/St] |
+-----------------------------------------------------------------+
L2 GUEST TIER +8              west wing                 ||  east wing
+-----------+-----------+-----------++-----------------------------+
| SPA [R/Gu]| BAR       | CLOAKROOM ||  BALLROOM [R/Gu]            |
| pool,     D [R/Gu]    D [R/St] S  D  double height +8..+16      |
| sauna (b4)| dumbwaiter| svc stair ||  chandelier over the stage  |
+-----D-----+-----D-----+-----D-----++  side stairs S up to +12    |
| LOBBY [R/Gu] grand stair S  C1    D                             |
+------D (b1) front doors-----------++-----------D terrace--------+
L3 +12                                  ||
+-----------+-----------+-----------+   ||  MEZZANINE [R/Gu]
| A1 PARTY  | A2 EMPTY  | A3 MAID   |   ||  gallery on 3 sides over
| [R/Gu]    D [R]       D [R/St]    |   ||  the ballroom void
+-----D-----+-----D-----+-----D-----+   ||
| CORRIDOR A ring [R/St] round the  D---D  (door, camera C3)
| lobby atrium void   C3   S svc    |   ||
+-----D-----+-----------+-----D-----+   ||
| A4 SINGER D  chute  S D A5 DOCTOR |   ||
| [R/St]    |  VIP stair| [R/Gu]    |   ||
+-----------+-----------+-----------+   ||
  south balconies joined by a continuous ledge (b6)
L4 VIP FLOOR +16                        ||  BALLROOM ROOF [H/Se]
+-----------+-----------+-----------+   ||  helipad, skylight V,
| B1 GUARD  | B2 DEALER | B3 BANKER |   ||  zipline lands (b7) C6
| POST[H/Se]D [H/Se]    | [H/Pr]    |   ||
+-----D-----+-----D-----+-----D-----+   ||
| CORRIDOR B ring [H/Se]  C4        D---D roof door
+-----------+-----K-----+-----K-----+   ||
| BOOTH     | PRESID.   | CINEMA    |   ||
| [R/St]    | SUITE     D [H/Pr]    |   ||
| dumbwaiter| [H/Pr]    | glass port|   ||
+--D svc landing (padlock)----------+   ||
L5 ATTIC +20: ATTIC STORE [R] (crevasse window b8) D CLOCK ROOM [H/Se] lookout D roof walk
```

- **30 enterable rooms** (5 + 6 + 5 + 6 + 6 + 2), 3 personal (10 %); every room ≥ 2 exits
  (suites: corridor door + connecting door or balcony ledge; toilets excepted).
- **Loops:** L0/L1 rooms → corridor; L2 lobby → bar → spa and lobby → ballroom → cloakroom; L3/L4
  corridor rings round the atrium void; L5 store → clock room → roof walk.
- **Stairs:** grand (lobby → L3), guarded **VIP stair** (L3 → L4), **service stair** (L0 → L5),
  ballroom side stairs, goods lift, clock-tower spiral, laundry chute (L3 → L0, one way).
- **Secret passage (dumbwaiter):** a rung shaft with vent hatches: wine cellar → behind the bar
  counter (public rule) → the **projection booth** [R/St]. No stop at L3 or the presidential suite;
  the booth sees the cinema through glass only and opens on the service landing by a **Padlock**.
- **Vent:** skylight duct (1 m crawl): roof → mezzanine ceiling → cloakroom → L1 linen.
- **Camera puzzle:** C1-C6 each call 2 guards; the **recorder** (security office) shuts all six.
  The **presidential door is a keypad**: the **butler** carries the CodeNote and breaks alone in
  the linen room.

**Eight distinct suites:** A1 after-party (3 guests), A2 empty renovation (breather), A3
housekeeping (door open, cart), A4 singer dressing, A5 gala doctor (ipecac), B1 bodyguards' post,
B2 dealer (briefcase, bodyguard), B3 **banker**.

### Hotel-platform entries (onto the terraces)

| # | Entry | Kind | Lands | Risk / time |
|---|---|---|---|---|
| G1 | Funicular top, frisk pair checks the Invitation | Social front | front terrace (S) [R/Gu] | safe; invitation ~3 min + 30 s |
| G2 | Service-road barrier (Padlock gate, Staff pass) | Service | delivery yard (NE) [R/St] | yard pair, camera C2; 20 s |
| G3 | Chairlift from the public queue | Public ride | ballroom terrace (E) [R/Gu] | 2 min ride in full view; attendant at the top |
| G4 | Pine copse → 2.2 m wall mantle | Climb | spa garden (SW) [R] | 10 s, patrol every 90 s |
| G5 | Crevasse ice wall | Climb | north roof walk | 60 s, cold, unseen |

### Hotel entries (into the building)

| # | Entry | Kind | Lands | Risk / time |
|---|---|---|---|---|
| b1 | Front doors | Social front | L2 lobby | camera C1, duty manager; safest |
| b2 | Kitchen delivery bay | Service (Staff) | L1 kitchens (E) | **high:** camera C2, bay pair, sous-chef; 15 s |
| b3 | Ski-room terrace door | Service (Staff) | L1 ski room (W) | instructors in and out; 10 s |
| b4 | Spa pool-plant door (Padlock, Staff pass) | Tool-gated | L2 spa (W) | spa attendant 6 m away |
| b5 | Mine gallery → rusted cold-store door (Crowbar) | Underground + tool | L0 cold store | **loud 10 m**, 3 min walk, Brandt inspects the gallery every loop |
| b6 | South drainpipe + balcony ledge | Climb | L3 suites | 20 s, in the ski-jump lookout's view |
| b7 | **Zipline** from the antenna hut → ballroom roof → skylight duct | One-way + vent | roof / L3 mezzanine | **no retreat**; clock-tower and dome lookouts see the roof; 30 s |
| b8 | Attic window from the crevasse roof walk | Climb | L5 attic store | slow, unseen |

## 5. Secondary fortresses
- **Observatory + bunker** [H/Se]: cable car (Keycard), ridge gate, mast ladder, escape tunnel,
  dome vent.
- **Ski area:** chairlift station (queue gate, machine-room door, wheel-house ladder); ski-jump
  (stair, ladder, tunnel hatch); groomer garage (doors, skylight, mine). Signature machines:
  **groomer tiller**, **chairlift bullwheel**.
- **Village:** 10 enterable lit chalets, 18 shuttered; Gasthaus cellar → lake culvert.

## 6. NPC ecology: 120 simulated + ambient dancers and skiers

| Zone | Groups (count) | Sum |
|---|---|---|
| Village | skiers + strollers 14 · rink crowd 8 · gendarme pair 2 · Gasthaus landlord, waitress, concierge (Invitations) 3 | 27 |
| Ski area | lift operators 3 · instructors (Staff pass) 2 · piste patrol pair 2 · groomer driver 1 · garage mechanic 1 · ski-jump lookout 1 | 10 |
| Hotel security | front frisk pair 2 · terrace patrol pairs 4 · delivery-bay pair 2 · VIP stair pair 2 · corridor B pair 2 · mezzanine pair 2 · security operator 1 · clock-tower lookout 1 · bodyguards 4 | 20 |
| Gala | guests 26 · waiters 6 · band 4 · after-party guests 3 | 39 |
| Hotel staff | duty manager (enforcer), sous-chef, 2 kitchen hands, boiler mechanic, laundry maid, housekeeping maid, butler, projectionist, singer, doctor, spa attendant, cloakroom attendant | 13 |
| Observatory + bunker | guard pairs 4 · technicians 2 · cable-car operator 1 | 7 |
| Targets | 4 | 4 |
| **Total** | | **120** |

| Target | Type, loop | Lethal setups (≥ 3) | Redirect levers |
|---|---|---|---|
| **Konrad Aschauer** (host) | dweller: presidential suite 4 → cinema 3 → ballroom speech 3 → **spa sauna 3** | **chandelier** over the speech; **sauna**: jam the door and max the stove; **aconite** in his champagne | suite phone; cinema TV; ipecac emetic → spa toilet, alone |
| **Mara Lindqvist** (banker) | roamer: Gasthaus fondue 3 → **chairlift, boarding at the bullwheel** 2 → spa pool 4 → suite B3 3 | **bullwheel**: open its guard, she is caught boarding; aconite fondue; hot-tub heater gas tank (explosive) | Gasthaus TV; taxi car alarm; spa radio |
| **Viktor Sorel** (arms dealer) | roamer: cable car 2 → bunker 4 → bar 3 → smoking terrace 2 | bunker generator gas tanks; aconite at the bar; patio-heater tank on the terrace | bunker radio; bar phone; snowcat car alarm |
| **Chief Brandt** (security chief) | dweller: security office 4 → corridor B 3 → **groomer garage** 3 → mine-gallery inspection 2 | **groomer tiller**: rig it, he stands at it to inspect; old blasting-charge crate in the gallery (explosive); aconite in his office coffee | office phone; garage radio; garage generator |

## 7. Opportunities, items, guidance
- **Alpine-native kit:** groomer tiller, bullwheel, sauna, hot-tub and patio heaters, blasting
  crate; **aconite** (spa-garden greenhouse, lethal), **ipecac** (doctor's bag, emetic).
- **Items:** Crowbar (groomer garage); Staff passes (sous-chef, instructors, mechanic,
  projectionist); Invitations (concierge, lone guest at the valley station); Keycards (operator,
  Brandt, cable-car operator); CodeNote (butler). Throwables in every guarded room.

| Takedown spot | Lone NPC | Container |
|---|---|---|
| Linen room | butler (CodeNote) | laundry cart 2 m |
| Ski room | instructor (Staff pass) | wardrobe 3 m |
| Valley-station waiting room | lone guest (Invitation) | crate 4 m |
| Boiler room | mechanic (Staff pass) | crate 5 m |
| Groomer garage | garage mechanic | dumpster 6 m |
| Security office | operator (Keycard) | wardrobe 3 m |
| Suite A2 (empty) | anyone lured by a bottle | wardrobe 2 m |
| Projection booth | projectionist (Staff pass) | crate 2 m |
| Cable-car top station | operator (Keycard) | crate 5 m |
| Mine hut | piste patroller on break | crate 4 m |

Soft hiding: pine bands, gala crowd; disposal: lake ice hole, crevasse.

**Guidance:** ChatterSpots, then contract page, then pause-map POIs. Pairs reveal: the butler's
code card (maids), the sauna dial (spa attendant), the banker boarding by the bullwheel (lift
operators), Brandt's groomer inspection (mechanic), the chandelier rigging (waiters), aconite in
the greenhouse (kitchen hands), the crowbar in the garage (instructors).

## 8. Starts, pacing, multiplayer, reuse

| Start | Tag / pass | Fans out to |
|---|---|---|
| Bus stop | [P] | funicular / chairlift / Gasthaus |
| Boathouse | [R] | lake culvert / rink / chalet roofs |
| Lake shore | [P] | service road / boathouse / valley station |
| Chairlift bottom | [P] | lift / pistes / groomer garage |
| Pine copse | [R] | spa wall / mid-station / ski-jump |
| Glacier trail | [R] | ridge path / crevasse / observatory |
| **Gala guest at the Gasthaus** | [P], Invitation | funicular / bar crowd / chairlift |
| **Ski instructor, mid-station** | [P], Staff pass | ski-room terrace / groomer garage / pistes |
| **Waiter, delivery yard** | [R/St], Staff pass | kitchens / cable car / service road |

- **Intensity:** village → copse (breather) → pistes → mine hut (breather) → ballroom → VIP.
- **Multiplayer:** ≥ 3 routes per start cluster; alerts stay in their zone.
- **Reuse:** chalet street = cover, chase, snowmobile extraction; pistes = route, ski-jump vs
  spire duel; ballroom = social approach, chandelier kill, dumbwaiter escape. **Extractions:**
  helicopter (ballroom roof), cable car, snowmobile, funicular.
