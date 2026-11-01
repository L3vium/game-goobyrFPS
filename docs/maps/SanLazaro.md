# San Lázaro: Colombian jungle (map design, v3)

**Keywords:** fiesta, coffee, the white house on the hill. **Motif:** white lime, terracotta,
blue-green shutters in every district. **Contrast:** loud warm fiesta against the cool tiled casa
and damp caves. **Topology:** archipelago with an onion core; terrain 500 × 500 m, play ~360 m.
Natural borders: Río Claro gorge (S, W), basalt cliffs (N), canopy and waterfall (E).

## 0. Rules used (code systems only)
- **Tags** (`SecurityZone` tier/access): **[P]** Public · **[R]** Restricted, no pass (breather) ·
  **[R/Gu] [R/St] [R/Se]** Restricted, legal with Invitation / Staff pass / Keycard · **[H]** Hostile,
  no pass · **[H/Se]** Hostile, Keycard · **[H/Pr]** Private, never legitimised. Zones are static:
  the **reception lasts the whole match**. Restricted = 4 s escort before a guard fires.
- **Enforcers (see through every pass):** Guest tier: Ramiro's 2 bodyguards, the buyer's 2. Staff:
  the **mayordomo** (casa, cellar), the **capataz** (cafetal). Security: **Captain Duarte**
  (airstrip, ridge), the mirador and water-tower lookouts.
- **One shared trick:** the mirador **DropLadder**. All other setups are biome-native.

## 1. Zones and the snail (every outdoor area tagged)

```
 N basalt cliffs ------------------------------------------------------------
 [D] AIRSTRIP +30 [H/Se]: hangar A, hangar B,     [C] HACIENDA LA CUMBRE +48
     radio shack, DC-3 wreck, fuel depot;              forecourt [R/Gu] · orchard [R]
     runway grass [H] · ridge checkpoint [R/Se]        service yard, garage, stables [R/St]
       |  ridge track, pines + rock [R]                kitchen garden [R/St] · chapel garden [R]
       |                                               mirador tower [H/Se]
 [B] CAFETAL +8 -> +40 [R/St]: 5 terraces, guamo shade, picker huts, drying sheds,
     BENEFICIO [R/St], water tower [R] · banana grove [R] · quarry [R] · jungle chapel ruin [R]
       ^ mule track + stone stairs + quarry ramp
 [A] PUEBLO 0 m: plaza + stage [P], market street [P], church nave [P], bell tower [R],
     sacristy [R], cantina [P] + cellar [R/St], police post [H], dock [P], roof terraces [P]
     ======== CUEVA DEL DIABLO -6 -> -20 m [R]: branch 1 cantina cellar - quarry - casa
              wine cellar · branch 2 quarry - waterfall dock ========
 S Río Claro gorge ----------------------------------------------------------
```

**Spiral:** dock → plaza → market → mule track wraps the terraces → ridge track to the airstrip →
garden terraces close on the casa; each layer joins the next by ≥ 2 connectors (stairs, ramp,
climb). **No dead ends:** every alley, terrace and cave branch rejoins a loop.

## 2. Social ladder

| Andersen type | Where | Tags | Share |
|---|---|---|---|
| Public basic | plaza, market, dock | [P] | 17 % |
| Public purpose | roof terraces, laundry alleys, bus stop | [P] | 8 % |
| Public rule | church nave, cantina, **casa reception: forecourt, patio, loggia, salon, dining, chapel, hall** | [P], [R/Gu] | 10 % |
| Private basic (breathers) | banana grove, caves, jungle chapel, orchard, crypt, sacristy, linen room, attic store | [R] | 22 % |
| Private professional | cafetal, beneficio, kitchen wing, cellar, garage, airstrip | [R/St], [R/Se], [H/Se] | 31 % |
| Private personal | study, master suite, vault | [H/Pr] | 12 % |

## 3. Landmarks, overlooks, light
- **Hero:** the white floodlit **mirador tower**, 70 m above the river. On a 25 m sample grid
  (≈ 190 exterior points) **78 %** see it; blind: caves, terrace 1, the apron behind hangar A. Its
  lookout scans a 90° arc (garden, terrace 5) and smokes 30 s every 3 min.
- **Secondary:** church bell tower (A), water tower (B), radio mast (D), waterfall.
- **Overlooks (each seen from another):** bell deck (+22) → the casa **main gate** and last
  switchback only. **Water tower** (+52) → the **south loggia**, left open for it, and the F1 south
  gallery: the toast and the buyer's balcony, never the north-range study or suite. Hangar B roof →
  apron.
- **Sightlines:** long only over plaza, river, valley. Cafetal runs capped at 35 m (shade trees,
  huts, sheds); airstrip broken by hangars, DC-3, drums, ditch, elephant grass; patio by fountain,
  planters, columns.
- **Light:** saturated fiesta → dappled green cafetal → cool blue-tiled casa → red study lamp;
  lantern pools in the caves. **Wayfinding:** band music, generator hum, painted signs, Staff plaques.

## 4. Key building: Casa La Cumbre
Courtyard house 40 × 32 m, cellar + 3 floors + mirador, in a 90 × 70 m wall. D door, K
keycard/keypad, S stair, V vent, C camera, REC recorder, (bN) entry.

```
CELLAR -3.6
+------------+------------+------------+------------+
| WINE CELLAR| COLD ROOM  | COAL STORE | LAUNDRY  S |
| [R/St] (b3)D [R/St]     D [R/St](b4) D [R/St]     |
| cave gate  |            |            | chute lands|
+-----D------+------------+------------+-----D------+
| CRYPT [R]  D   CORRIDOR [R/St]   C4          D    |
| chapel S   |                                      |
+------------+------K---------------------K--------+
             | SECURITY ROOM [H/Se] REC, CO2 panel  |
             +--K hatch (keypad)----V purge duct----+
             | VAULT [H/Pr]  -6.5 m                 |
             +--------------------------------------+
GROUND 0          service yard [R/St] C2
+------------+------------+------------+------------+
| SACRISTY   | SALON      | DINING     | KITCHEN    |
| [R]   (b8) D [R/Gu]     D [R/Gu]     D [R/St] (b2)|
+-----D------+-----D------+-----D------+-----D------+
| CHAPEL     |                         | PANTRY   S |
| [R/Gu]     D     PATIO [R/Gu]        D [R/St]   V |
| confessio* |  fountain, orange trees |            |
+-----D------+                         +-----D------+
| HALL     S |                         | OFFICE     |
| [R/Gu] C1  D                         K [R/Se]     |
+--D (b1)----+==== LOGGIA, open arcade [R/Gu] =====+
  forecourt [R/Gu]                      orchard [R]
FLOOR 1 +3.6
+------------+------------+------------+------------+
| GUEST B    | MASTER     | STUDY      | GUARD ROOM |
| [R/St]     D SUITE   wc D [H/Pr]   V D [H/Se]     |
+-----D------+-----D------+-----K------+-----D------+
| ANTEROOM   K   GALLERY ring [R/St] C3| LINEN [R]  |
| [R/Se] S*  |   around the patio void D chute S V  |
+-----D------+                         +-----D------+
| STAIR    S D                         | GUEST A    |
| landing    |                         D [R/Se](b5) |
+------------+== open south gallery [R/St] ========+
FLOOR 2 +7.2
+------------+------------+------------+------------+
| ATTIC STORE| DORMITORY  | ARCHIVE    | MIRADOR    |
| [R] V (b6) D [R/St]     D [R/Se]     D BASE [H/Se]|
+-----D------+------------+------------+-----D------+
| roof walk [R] (ledges)  patio void   | STAIR HALL |
|                                      | [R/St] S   |
+---- roof terrace [R] C5, over the south gallery --+
```

- **27 enterable rooms** (7 + 8 + 7 + 5), 3 personal (11 %). Every room has ≥ 2 exits except the
  suite toilet and the vault (hatch + purge duct, both into the security room: a trap pocket).
- **Loops:** cellar wine → cold → coal → laundry → corridor → crypt; ground and F1 round the patio;
  F2 rooms → stair hall → roof terrace → roof walk.
- **Stairs:** grand (hall → F1), service (laundry → pantry → linen → F2), mirador spiral (→ +22),
  crypt stair, laundry chute (F1 → cellar, one way).
- **Secret passage:** the chapel confessional (*) hides the **priest's stair** to the F1 oratory
  anteroom [R/Se], which opens on the gallery through a keycard door: it never skips the
  professional layer.
- **Vent:** kitchen-hood duct (1 m crawl): pantry → linen → study ceiling → roof.
- **Camera puzzle:** C1-C5 each call 2 guards; the **recorder** shuts all five for the match. The
  security room's two keycard doors open with the **operator's** card (he smokes at the yard
  dumpster every 3 min). The **vault hatch is a keypad**: the CodeNote sits in the accountant's
  missal in the **pueblo sacristy**.

### Compound-wall entries (into the grounds)

| # | Entry | Kind | Lands | Risk / time |
|---|---|---|---|---|
| W1 | Main gate, frisk pair checks the Invitation | Social front | forecourt (SW) [R/Gu] | safe; ~4 min to get one + 30 s frisk |
| W2 | Service gate behind the produce truck (Padlock, Staff pass) | Service | service yard (N) [R/St] | camera C2, cook; 20 s |
| W3 | Vine trellis from terrace 5 | Climb | orchard (SE) [R] | 10 s, in the lookout's arc: time his smoke |
| W4 | Coffee chute from the beneficio loft (slide) | One-way | stable yard (E) [R/St] | 8 s, no way back; lands beside the mechanic |
| W5 | Ridge scramble + ivy on the north wall | Climb | kitchen garden (NW) [R/St] | 40 s, hidden from the mirador; patrol pair every 2 min |
| W6 | Rusted cemetery gate, west wall (Crowbar from the quarry) | Tool-gated | chapel garden (W) [R] | loud 10 m, gardener 12 m away; 5 s |

### Casa entries (into the building)

| # | Entry | Kind | Lands | Risk / time |
|---|---|---|---|---|
| b1 | Hall door from the forecourt | Social front | ground SW | camera C1, bodyguards; safest |
| b2 | Kitchen yard door | Service (Staff) | ground NE | camera C2, cook and mayordomo |
| b3 | Cave branch 1 → wine-cellar gate | Underground | cellar NW | 3-4 min, isolated; smugglers at the cave dock |
| b4 | Coal chute (Padlock, Staff pass) | Tool-gated | cellar N | short; mechanic 10 m away in the garage |
| b5 | Orange espalier + drainpipe → buyer's balcony | Climb | F1 SE | 15 s, in view of the water tower and the loggia |
| b6 | North drainpipe → roof walk → attic vent | Climb + vent | F2 NW | 30 s, unseen from the garden |
| b7 | Mirador DropLadder, lowered from the tower gallery, stays down | One-way shortcut | tower → F2 NE | first run only from inside; then open for all |
| b8 | Boarded sacristy window (Crowbar) | Tool-gated | ground NW | loud 10 m, reception 12 m away |

**Secret links:** both cave branches, the coffee chute.

## 5. Secondary fortresses
- **Beneficio** [R/St] (drying patio, roaster floor, sacking loft, office): main doors, loading
  ramp, loft window via sacks (climb), quarry cave hatch, skylight. Signature: the **gas roaster**.
- **Airstrip** [H/Se]: ridge checkpoint (Keycard), culvert from the jungle chapel, hangar A catwalk
  ladder, radio-shack window, hangar B side door (Padlock).
- **Pueblo:** 12 enterable lit houses, 22 shut and barred; plank-linked roofs; **zipline** bell
  tower → dock.

## 6. NPC ecology: 120 simulated + ambient fiesta extras

| Zone | Groups (count) | Sum |
|---|---|---|
| Pueblo | fiesta crowd 26 · sellers, barkeep, boatman 6 · priest, sacristan 2 · police pair 2 · sponsor's secretary (Invitation) 1 | 37 |
| Cafetal | pickers 10 · capataz (enforcer, Staff pass) 1 · roastmaster + hands 4 · patrol pairs 4 · water-tower lookout 1 | 20 |
| Hacienda security | gate frisk pair 2 · grounds patrol pairs 6 · interior pairs (study door, cellar corridor, F1 gallery) 6 · mirador lookout 1 · bodyguards 4 | 19 |
| Household | mayordomo (enforcer), cook, 2 maids, gardener, mechanic, operator: each alone by a container | 7 |
| Reception | guests 12 · waiters 3 · musicians 3 | 18 |
| Airstrip + ridge | sicarios 6 · Captain Duarte 1 · pilots 2 · mechanic 1 · radio op 1 · checkpoint pair 2 | 13 |
| Caves | smugglers 2 | 2 |
| Targets | 4 | 4 |
| **Total** | | **120** |

| Target | Type, loop | Lethal setups (≥ 3) | Redirect levers |
|---|---|---|---|
| **Don Ramiro Elizalde** (patron) | dweller: study 4 min → loggia toast 3 → chapel 2 → dining 3 | datura in his aguardiente; chapel candles + spilled lamp oil (fire as he prays alone); toast fireworks crate on the loggia; water-tower shot at the toast | study phone; patio radio; emetic → suite toilet, alone |
| **Teodoro Aranda** (accountant) | dweller: vault 5 → office 3 → kitchen coffee 2 → wine cellar 2 | **vault CO₂ accident** fired from the security-room panel; fungicide from the beneficio in his coffee; laundry gas boiler (explosive) as he passes | office phone; cellar radio; emetic coffee → cellar toilet |
| **Ilse Varga** (buyer) | roamer: hangar 3 → ridge checkpoint 2 → **beneficio roaster tasting 3** → casa salon 4 | **rigged roaster valve** (she stands at the cooling tray); fungicide in the cupping cup; fuel bowser by the DC-3 (explosive) | hangar generator; jeep car alarm at the checkpoint; beneficio radio |
| **Doña Celestina Ruiz** (fiesta sponsor) | roamer: stage 4 → cantina 3 → bell deck 2 → dock 2 | fireworks mortars under the stage; datura in her cantina glass; empanada-cart gas tank on the cantina terrace | stage PA radio; cantina TV (football); bus-stop car alarm |

## 7. Opportunities, items, guidance
- **Biome-native kit:** roaster, vault CO₂, fireworks crates, fuel bowser, gas tanks, chapel
  candles + lamp oil; poisons near their drinks: **datura** (chapel-ruin garden, lethal), **tobacco
  infusion** (cantina cigar roller, emetic), copper **fungicide** (beneficio, lethal).
- **Items:** Crowbar (quarry); Staff passes (capataz, cook, mechanic, waiters); Invitations
  (secretary, guests); Keycards (operator, Duarte, accountant); CodeNote (missal). Throwables in
  every guarded room.

| Takedown spot | Lone NPC | Nearest container |
|---|---|---|
| Kitchen-yard smoke spot | operator (Keycard) | dumpster 4 m |
| Cellar laundry | maid | laundry cart 3 m |
| Garage pit | mechanic (Staff pass) | crate 6 m |
| Terrace-5 picker hut | capataz (Staff pass) | crate 5 m |
| Cantina back alley | secretary (Invitation) | dumpster 3 m |
| Church sacristy | sacristan | wardrobe 2 m |
| F1 guest room B | maid | laundry cart 2 m |
| Orchard tool shed | gardener | crate 8 m |
| Hangar B bunks | pilot | wardrobe 3 m |
| Cave dock | smuggler | water 2 m |

Soft hiding: banana leaves, elephant grass; disposal: river, sinkhole.

**Guidance:** ChatterSpots first, then the contract page, then pause-map POIs. Pairs reveal: the
missal code (laundry maids), the sticky roaster valve (roastmaster), "never touch the CO₂ panel
while Aranda counts" (corridor guards), the operator's smoke (cook), the bowser leak (pilots),
datura at the chapel ruin (cook + waiter), Ramiro praying among candles (bodyguards), the sponsor
on the bell deck (priest), the chute into the casa (capataz).

## 8. Starts, pacing, multiplayer, reuse

| Start | Tag / pass | Fans out to |
|---|---|---|
| River dock | [P] | plaza / promenade / cantina cellar |
| Bus stop | [P] | market / mule track / cantina |
| Banana grove | [R] | cafetal / caves / plaza |
| Jungle chapel ruin | [R] | airstrip culvert / ridge / terrace 5 |
| Waterfall cave mouth | [R] | branch 2 / river path / orchard wall |
| Ridge pine copse | [R] | checkpoint / airstrip / north wall |
| **Picker hut, terrace 4** | [R/St], Staff pass | beneficio / coffee chute / service gate |
| **Reception waiter, cantina kitchen** | [P], Staff pass | produce truck / W2 / plaza |
| **Sponsor's table guest** | [P], Invitation | main gate / church / cafetal |

- **Intensity:** plaza → grove (breather) → cafetal → orchard (breather) → reception → study.
- **Multiplayer:** ≥ 3 independent routes per start cluster; alerts stay in their zone; the
  DropLadder and the recorder cut are shared by design.
- **Reuse:** plaza = fiesta, chase, car extraction; cafetal = chute route, water-tower sniper duel;
  caves = route, disposal, waterfall extraction; reception = social approach, toast kill, priest's
  stair escape. **Extractions:** boat, plane, car, waterfall.
