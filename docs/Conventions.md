# Hitmen — Conventions

## Asset prefixes
| Prefix | Asset | Prefix | Asset |
|---|---|---|---|
| `BP_` | Blueprint class | `WBP_` | Widget Blueprint |
| `ABP_` | Animation Blueprint | `BPI_` | Blueprint interface |
| `BFL_` | Blueprint function library | `BPML_` | Blueprint macro library |
| `IA_` | Input action | `IMC_` | Input mapping context |
| `BT_` | Behavior tree | `BB_` | Blackboard |
| `BTT_` | BT task (BP) | `BTS_` | BT service (BP) |
| `BTD_` | BT decorator (BP) | `EQS_` | Env query |
| `DA_` | Data asset | `DT_` | Data table |
| `E_` | Enum (BP) | `F_` | Struct (BP) |
| `CV_` | Curve | `M_` | Material |
| `MI_` | Material instance | `MF_` | Material function |
| `PP_` | Post-process material | `T_` | Texture |
| `RT_` | Render target | `SM_` | Static mesh |
| `SK_` | Skeletal mesh | `SKEL_` | Skeleton |
| `PHYS_` | Physics asset | `AM_` | Anim montage |
| `AS_` | Anim sequence | `BS_` | Blend space |
| `AO_` | Aim offset | `AN_` | Anim notify |
| `NS_` | Niagara system | `NE_` | Niagara emitter |
| `SW_` | Sound wave | `SC_` | Sound cue / MetaSound source |
| `SA_` | Sound attenuation | `SCL_` | Sound class |
| `PM_` | Physical material | `L_` | Level |
| `FNT_` | Font | `FF_` | Font face |

C++ classes use the `Hitmen` prefix after the Unreal letter: `AHitmenWeaponBase`,
`UHitmenAbility`, `FHitmenTeam`, `EHitmenAlertState`.

## Content folders
```
Content/Hitmen/
  Core/            GameModes, GameInstance, PlayerController, GameState BPs
  Characters/      Player/ NPC/ Shared/   (BP_, ABP_, Anims/, Meshes/)
  Weapons/         <WeaponName>/  (BP_, DA_, Meshes/, VFX/, SFX/)
  Abilities/       Move/<Name>/  Manipulation/<Name>/
  World/           Interactables/<Type>/  Spawning/  Vehicles/
  AI/              BehaviorTrees/  Blackboards/  EQS/  Tasks/
  UI/              Menus/  Lobby/  HUD/  Common/  Fonts/  Textures/
  Input/           Actions/  Contexts/
  Maps/            L_TitleScreen, L_Lobby, L_Gym, L_LDTest, L_Empty_01, L_Empty_02
  Materials/       Master/  Instances/  PostProcess/
  VFX/             <Category>/
  Audio/           SFX/<Category>/  Music/  Ambience/  Attenuation/
  Data/            Rules/  Maps/  Weapons/  Abilities/  AnimSets/  Cosmetics/
  Physics/         PM_ physical materials
```
Third-party packs stay in their own root folders (`Vefects/`, `Weapons_Free/`, `Characters/`).

## C++ source folders
```
Source/Hitmen/
  Core/ Framework/ Online/ Characters/ Components/ Movement/ Inventory/ Weapons/
  Abilities/{Move,Manipulation}/ StatusEffects/ Interaction/ World/{Interactables,Spawning,Vehicles}/
  AI/{Tasks,Services,Decorators}/ UI/ Spectator/ Debug/
```

## Blueprint graphs
- Split event graphs into named graphs: **Init**, **Gameplay**, **Juice**, **UI**, **Debug**.
- Bind delegates with **Create Event** (pick the function by name) instead of dragging a red
  event wire across the graph.
- Collapse repeated logic into functions or macros; keep graphs formatted with
  Blueprint Assistant (`F` on a selection).
- Group every block with a comment box using the project colour code:

| Block | Colour | RGB |
|---|---|---|
| Init / BeginPlay / binding | Green | (0.15, 0.55, 0.20) |
| Input / incoming events | Red | (0.70, 0.15, 0.15) |
| Gameplay logic | Blue | (0.15, 0.35, 0.75) |
| UI | Orange | (0.85, 0.45, 0.10) |
| **Juice** (VFX, SFX, camera shake, feedback) | Purple | (0.55, 0.20, 0.70) |
| Networking / replication | Cyan | (0.10, 0.60, 0.65) |
| Debug | Grey | (0.35, 0.35, 0.35) |

## Juice hooks
Every C++ gameplay class exposes `BlueprintImplementableEvent`s named `Juice_On<Event>`
(e.g. `Juice_OnFired`, `Juice_OnHit`, `Juice_OnAlertChanged`). They are cosmetic only: never
put gameplay logic in them. They run on every machine that sees the event.

## C++ style
- Unreal coding standard; header comment `// Hitmen — <one-line purpose>`.
- `UPROPERTY(EditDefaultsOnly/EditAnywhere, BlueprintReadOnly, Category="Hitmen|<Area>")` for
  tunables; no magic numbers in .cpp.
- Delegates: `DECLARE_DYNAMIC_MULTICAST_DELEGATE_*` named `FOnHitmen<Something>` and exposed as
  `BlueprintAssignable` properties named `On<Something>`.
- Server RPCs `Server_`, client RPCs `Client_`, multicasts `Multicast_`, rep notifies `OnRep_`.
