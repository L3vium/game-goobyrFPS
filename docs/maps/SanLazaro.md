# San Lázaro — Colombian jungle (map design, v2)

**Keywords:** fiesta, coffee, the white house on the hill. **Motif:** white lime walls, terracotta
roofs and blue-green shutters, from the pueblo church to the cafetal sheds and the casa.
**Contrast:** the loud, warm, crowded fiesta against the cool, silent, tiled casa and the damp,
lantern-lit caves. **Topology:** archipelago with an onion core (lesson from Santa Fortuna,
original layout). Terrain 500 × 500 m, play space ~360 × 360 m. Borders are natural and visible:
the Río Claro gorge (S, W), basalt cliffs (N), canopy and the waterfall (E). No invisible walls.

## 0. Systems this map relies on (game rules)
- **Security tiers** (`SecurityZone`): Public / Restricted / Hostile, each zone tagged with the
  **access** that legitimises it: *Guest* (fiesta reception), *Staff* (cafetal, kitchen, garage),
  *Security* (study wing, vault, airstrip). Carrying the matching **pass** (Invitation, Staff pass,
  Keycard) makes that zone Public for you while you behave. **Enforcers** (bodyguards, the
  sicario captain) see through passes.
- **Restricted = escort before shooting:** a guard who spots you there confronts you, walks you
  out and only fires if you stay 4 s, run at him or raise a gun.
- **Tool gates:** crowbar doors (boarded, rusted), keycard doors; passes and keycards are carried
  by specific NPCs and dropped when they are knocked out.
- **Overheard chatter:** pairs of NPCs talk when you are close (subtitles): this is the guidance
  layer, then the contract page, then the map points of interest. No objective markers.

## 1. Zones and the snail

```
 N basalt cliffs ─────────────────────────────────────────────────────────
  [D] AIRSTRIP (+30 m)                         [C] HACIENDA LA CUMBRE (hill +48 m)
   hangar A + hangar B, wrecked DC-3,     ◄─ridge track (pine/rock cover)─►  wall · gardens · casa · mirador
   fuel drums, ditch, elephant grass                                     │ garage · stables
        │ switchback with retaining walls                    terraced garden paths
        ▼                                                                ▲      │ cave door
  [B] CAFETAL (+8 → +40 m): 5 terraces, guamo + plantain shade trees,     │      │
      picker huts, drying sheds every terrace, BENEFICIO, water tower ────┘      │
        ▲ mule track + stone stairs + quarry ramp                                 │
  [A] PUEBLO SAN LÁZARO (river 0 m): plaza (fiesta stage), church + tower,       │
      cantina, market street, 34 houses (12 enterable), police post, dock        │
        ═════════════ CUEVA DEL DIABLO (−6 → −20 m), two branches ═══════════════╛
 S Río Claro gorge ───────────────────────────────────────────────────────
```

**Spiral:** dock → plaza → market street → mule track wraps the cafetal's five terraces → ridge
track curls round the hill to the airstrip → the casa's garden terraces close the spiral at the
top. Each layer joins the next by stairs + slope + a climb (≥ 2 connectors). **Caves** run beneath:
branch 1 cantina cellar ↔ quarry ↔ casa garage; branch 2 quarry ↔ waterfall dock, so two players
never share one tunnel. **No dead ends:** every alley, terrace and cave branch rejoins a loop; the
only one-exit rooms are toilets.

## 2. Social ladder (tiers, access, shares)

| Andersen type | Where | Tier / access | Share |
|---|---|---|---|
| Public open | plaza, market street, promenade, dock | Public | 18 % |
| Public purpose | laundry alleys, roof terraces (rooftop road), bus stop | Public | 8 % |
| Public rule | church, cantina, **the casa's reception (patio, salon, chapel) during the fiesta reception** | Public for everyone outside the casa; *Guest* inside | 9 % |
| Private basic (breathers) | banana grove, jungle chapel ruin, garden folly, cave gallery, **casa pantry/serving corridor** | Restricted (no access) | 22 % |
| Private professional | cafetal, beneficio, kitchen wing, garage, stables, hangars | Restricted, *Staff* | 30 % |
| Private personal | study, master suite, vault, mirador | Hostile, *Security* | 13 % |

**Legitimacy sources:** *Invitation*: from the fiesta sponsor's secretary (roams plaza ↔ cantina)
or at the printer's in the pueblo. *Staff pass*: the overseer (cafetal), the cook (kitchen), the
mechanic (garage); each works alone next to a hiding spot. *Keycard*: the accountant (vault ↔
office) and the sicario captain. **Enforcers:** bodyguards (see through Guest), the captain (sees
through Staff), the lookout (sees everything in his arc).

## 3. Landmarks, overlooks, light, sightlines

- **Hero landmark:** the white **mirador tower** (70 m above the river), floodlit at night,
  visible from the plaza, every terrace and the airstrip (>75 % of the exterior). The **lookout**
  in it scans only a **90° arc** (garden + top terrace) and takes a 30 s cigarette break every 3 min.
- **Secondary landmarks:** church tower (A), water tower (B), radio mast + windsock (D), waterfall.
- **Overlooks:** church tower → casa gate and the patio (the reception), water tower → garden
  terrace walk (target's afternoon stroll), hangar B roof → airstrip apron. Each overlook is seen
  from another (contested). None sees inside the study or the suite (R46).
- **Sightline plan:** long views only across public space (plaza, river, the valley from the
  promenade). The cafetal's open terrace runs are capped at **35 m** by shade trees, huts and
  drying sheds. The airstrip is broken by two hangars, a wrecked DC-3, drum stacks, a drainage ditch
  and elephant grass along the runway. Inside the casa, the patio is broken by a fountain, orange
  trees in planters, arcade columns and doors along the gallery — never a 360° view.
- **Light:** warm saturated pueblo (bunting, lanterns, fiesta lights); dappled green cafetal; cool
  white-and-blue tiled casa; a red lamp in the study (personal accent); lantern pools in the caves.
- **Wayfinding:** fiesta music (audio beacon), generator hum at the airstrip, painted track markers,
  signs "Beneficio / La Cumbre / Pista", staff-only plaques on Staff doors.

## 4. Key building — Casa La Cumbre (onion core)

Colonial courtyard house 40 × 32 m, **3 full floors + cellar + mirador**, inside a 90 × 70 m wall
with gatehouse, two garden terraces, garage, stables, staff quarters and a chapel.

```
CELLAR (−3.6)          GROUND (0)                       FLOOR 1 (3.6)                    FLOOR 2 (7.2)
┌─────┬─────┬─────┐    ┌──────┬──────┬────────┬──────┐  ┌──────┬──────┬──────┬──────┐  ┌──────┬──────┬──────┬──────┐
│Wine │Gener│Air  │    │Chapel│Library│ SALON  │Dining│  │Guest1│Guest2│MASTER│Dress.│  │Attic │Servant│Archive│Linen│
│cellar│+brkr│hand.│    │(pub.)│(pub.) │(chand.)│      │  │      │      │SUITE │      │  │store │ rooms │       │(brth)│
├─────┼─────┼─────┤    ├──────┼──────┴────────┼──────┤  ├──────┼──────────────┼──────┤  ├──────┼──────────────┼──────┤
│Coal │ TUNNEL→ │Secur│    │Office│   PATIO      │Pantry│  │Bath  │   GALLERY    │STUDY │  │Roof  │  roof terrace │Mira- │
│chute│ garage  │room │    │(acct)│ fountain,    │/serv.│  │      │  balcony     │(PERS)│  │ walk │ (ledges)      │dor   │
├─────┼─────┼─────┤    ├──────┤ orange trees  ├──────┤  ├──────┼──────────────┼──────┤  │stair │               │stair │
│Laundry│VAULT│Store│   │GRAND │               │Kitchen│ │Guest3│Laundry│Terrace│Sec. │  └──────┴──────┴──────┴──────┘
│      │(PERS)│     │   │STAIR │ Hall │ Staff  │+SERV. │  │      │chute  │(vine) │office│
└─────┴─────┴─────┘    └──────┴──────┴────────┴──────┘  └──────┴──────┴──────┴──────┘
```

**27 enterable rooms.** Every floor is a **loop**: around the patio gallery, and a second inner
loop through connecting doors (suite ↔ dressing ↔ study ↔ security office). **Two stair systems**:
the grand stair (hall → floor 1) and the service stair in the kitchen wing (cellar → floor 2), plus
the mirador spiral and the laundry chute (a one-way drop from floor 1 to the cellar laundry).
**Two exits per room** (toilets excepted). The **vault** has its tunnel door and a hidden service
hatch to the security room. **Secret passage, public → private:** the library bookcase (public
reception room) opens onto the service stair. **Vent:** the cellar air handler's duct climbs the
service core, has grilles in the security office, the study ceiling and the attic, and exits on the
roof walk. During the reception the patio, salon, chapel, library and dining room are **Public
rule (Guest)**; floor 1 is Staff; the study wing and cellar vault are Security.

### Entries (9, all six kinds)

| # | Entry | Kind | Risk / time |
|---|---|---|---|
| 1 | Main gate (frisk pair checks the Invitation) → patio reception | Social front | safest, slowest: fetch an invitation first |
| 2 | Service gate with the produce truck from the beneficio | Service (Staff) | Staff pass or slip in behind the truck |
| 3 | Vine trellis from the top terrace to the floor-1 terrace | Climb | fast; inside the lookout's arc (time his break) |
| 4 | North drainpipe → roof walk → attic vent grille | Climb + vent | fast, hidden from the garden, ends in the attic |
| 5 | Garage tunnel from the caves → cellar | Underground | long, isolated; disposal in the sinkhole |
| 6 | Coal chute (padlocked: the mechanic's key = Staff pass) | Tool-gated | short, mechanic nearby |
| 7 | Walled-up garage door, crowbar from the quarry | Tool-gated | loud (noise 10 m), quick |
| 8 | Ledge walk chapel roof → guest balconies | Climb (ledges) | medium, needs the chapel drainpipe |
| 9 | Mirador drop-ladder lowered from the roof walk, then stays down for everyone | One-way shortcut | rewards a first climb (shared in multiplayer by design) |

**Secret links between zones:** the caves (two branches) and the **coffee chute** — a slide from
the beneficio's sacking loft down to the casa service yard (one way).

## 5. Secondary fortresses (≥ 3 entries each)

- **Beneficio (B)**, 2 floors: drying patio, roaster floor, sacking loft, overseer office. Entries:
  main doors (Staff), loading ramp, loft window via sack stacks (climb), quarry cave hatch
  (underground), skylight (climb). Signature machine: the **gas-fired coffee roaster** (rig the
  valve: the next worker to light it burns).
- **Airstrip (D):** hangar A (floor + catwalk office), hangar B (pilots' bunks), radio shack,
  fuel depot. Entries: apron doors (Security), side door, catwalk ladder, roof vent, drainage
  culvert from the jungle chapel. Opportunity: fuel bowser by the DC-3, loose antenna on the mast.
- **Pueblo (A):** 12 enterable houses (lit, open doors and shutters), 22 shut (barred shutters,
  dark windows — clearly not enterable). Roof terraces joined by planks and ladders (rooftop road).
  Cantina (cellar → caves), church (tower, crypt), printer's shop (invitations), police post.

## 6. NPC ecology (120 simulated + ambient fiesta crowd)

| Group | Count | Purpose |
|---|---|---|
| Fiesta crowd in the plaza | 30 (+ ambient extras on rooftops/benches) | cover, witnesses; concentrated in the plaza, not spread |
| Market sellers, barkeep, priest, printer | 9 | shops, keys of the cantina cellar (barkeep) |
| Police pair | 2 | public order; restrict the church tower and police post |
| Cafetal pickers / overseer / patrols | 12 / 1 / 6 (3 pairs) | Staff zone life; overseer = Staff pass + radio |
| Casa: gate pair, garden patrols (3 × 2), interior choke pairs (grand stair, gallery corner, study door) | 14 | the onion |
| Casa staff (cook, 2 maids, gardener, mechanic, accountant) | 6 | each alone next to a hiding spot, each carries a pass/key |
| Reception guests | 14 | Guest public-rule enclave |
| Bodyguards (enforcers) | 4 | escort targets, see through Guest |
| Airstrip sicarios / captain / pilot / mechanic | 6 / 1 / 2 | Security; faction friction with the casa guards (they argue at the ridge checkpoint: a distraction that pulls both pairs together every 4 min) |
| Lookouts (mirador, water tower) | 2 | overlooks, scan arcs |

**Targets** (dwellers and roamers, 2-6 min per area, each with 2-4 redirect levers):

| Target | Type | Loop | Levers |
|---|---|---|---|
| Don Ramiro (patron) | dweller | study 4 min → reception patio 3 min → chapel 2 min → dining 4 min | breaker (lights out, he calls the mechanic), radio in the patio, poisoned aguardiente at the dining table, the chapel bell |
| The accountant | dweller | vault 5 min → office 3 min → kitchen coffee 2 min | vault alarm test, coffee poison, office printer jam |
| The buyer | roamer | airstrip 4 min → ridge checkpoint 2 min → casa salon 4 min | fuel bowser leak, plane engine start, chandelier above the salon |
| The fiesta sponsor | roamer | plaza stage 4 min → cantina 3 min → church tower 2 min | fireworks crate, cantina drink, church bell, the stage spotlight rig |

## 7. Opportunities, items, guidance

- Accidents: salon chandelier (buyer, sponsor at the reception toast); breaker + air handler in
  the cellar; poisonable drinks (dining table, cantina bar, hangar coffee pot, kitchen) with the
  **rat poison in the stables** and **pills in the church sacristy** next to them; fuel bowser;
  rigged roaster; fireworks crate under the stage; the mirador's loose railing at dusk.
- Tools: crowbar in the quarry, padlock key with the mechanic, keycard with the accountant/captain,
  invitation at the printer's. Throwables in every guarded room (bottles, rocks, wrenches). Guns:
  police pistol (post), shotgun (beneficio office), sniper (water tower), SMGs (sicarios); rocket
  launcher / flamethrower at hidden spots (hangar crate, quarry, chapel crypt).
- Hiding within 15 m of every planned takedown: laundry hampers, wardrobes, coffee sacks, oil
  drums, hay in the stables; soft hiding in banana leaves and elephant grass; disposal in the river,
  the sinkhole, the cane press.
- **Guidance:** chatter — the maids complain about the boss's study lamp (breaker), the overseer
  boasts about the roaster's temper, the pilots mention the fuel leak, the guests mention the toast
  under the chandelier.

## 8. Pacing, starts, multiplayer, reuse

- **Starts (6):** river dock, bus stop, banana grove, jungle chapel ruin, waterfall dock, ridge
  pine copse. Each fans out into ≥ 3 choices: dock → plaza / promenade / cantina cellar; bus stop
  → market / mule track / printer's; banana grove → cafetal / caves / plaza; chapel ruin → airstrip
  culvert / ridge / cafetal top; waterfall → caves branch 2 / river path / garden folly; pine copse →
  ridge checkpoint / airstrip / casa north wall.
- **Intensity:** plaza (public) → banana grove (breather) → cafetal (professional) → garden folly
  (breather) → reception (public rule) → study (personal peak).
- **Multiplayer:** ≥ 3 independent routes from every start cluster to the casa; alerts stay inside
  their zone (LevelZone squad, calls reach the six nearest mates) and cool down after 2 min; the drop
  ladder and opened doors are shared on purpose; overlooks counter each other.
- **Reuse (people, not polygons):** the plaza serves the fiesta, a chase and an extraction (car);
  the cafetal hosts the overseer's routine, the coffee chute route and the sniper duel at the water
  tower; the caves are a route, a disposal site and an extraction; the reception patio is a social
  approach, a chandelier kill and a getaway through the library.
- **Extractions:** river boat (dock), plane (airstrip), car (pueblo road), waterfall (caves).
