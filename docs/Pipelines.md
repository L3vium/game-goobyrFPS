# Pipelines

Short recipes for the team: where things live and how to change them without touching code.

## Icons (loadout tiles, HUD inventory cards)

Every weapon and ability shows the texture set in its data asset's **Icon** field.

| Kind | Textures | Data assets |
|---|---|---|
| Weapons | `/Game/Hitmen/UI/Icons/Weapons/T_Icon_Weapon_<Name>` | `/Game/Hitmen/Data/Weapons/DA_Weapon_<Name>` |
| Move abilities (`E`) | `/Game/Hitmen/UI/Icons/Abilities/Move/T_Icon_Ability_<Name>` | `/Game/Hitmen/Data/Abilities/DA_Ability_<Name>` |
| Manipulation abilities (`A`) | `/Game/Hitmen/UI/Icons/Abilities/Manipulation/T_Icon_Ability_<Name>` | same |

**Change one icon**: drag a PNG onto the texture in the Content Browser and choose *Reimport* (keep the
name), or set the data asset's **Icon** to any texture. White on transparent, square, 256 px or more; the UI tints
and sizes it.

**Regenerate the placeholders**: `python Tools/Icons/make_icons.py` writes `Tools/Icons/Out/T_Icon_*.png`, then
`python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_import_icons.py` imports them into the folders above
and assigns them (editor open). `Tools/Editor/hitmen_organize_icons.py` re-sorts textures dropped in
`/Game/Hitmen/UI/Icons` into their folders and keeps every reference.

## Map markers (minimap, pause map, legend)

Anything can show on the minimap and the pause map:

1. **Pick or make a kind**: a data asset of class `HitmenMapMarkerType` in `/Game/Hitmen/Data/MapMarkers`
   (duplicate `DA_MapMarker_Camera`). Set **Display Name** (the legend row), **Color**, **Shape** (square, diamond,
   triangle, ring) or an **Icon** (white on transparent, tinted), **Size**, the minimap scale, whether it shows on the
   minimap / the pause map / the legend, **Clamp To Minimap Edge** for objectives, **Legend Order** and the **Layer**:
   *Objective* always draws; *NPC* and *Interactables* draw only where Project Settings → Hitmen → Maps switches that
   layer on (minimap and pause map apart; by default only the minimap's NPC layer is on).
2. **Mark the actor**: in its Blueprint, *Add Component → Hitmen Map Marker*, set **Type** to the kind (and an
   optional **Label** shown next to it on the pause map). Interactables already have a **Map Marker** component: just
   set its Type (`hitmen_content.INTERACTABLE_MARKERS` does it for the built-in ones).
3. Done: the legend gets a row with a toggle as soon as one marker of that kind is on the map. Gameplay code hides a
   marker with **Set Marker Visible** (a disabled camera, an unused retrieval point do it already).

Retrieval points and security cameras carry one (`DA_MapMarker_RetrievalPoint`, `DA_MapMarker_Camera`), and so do
lockers, body containers, distractions, containers, keycards, consoles, hack panels, breakers, drinks and barrels
(`DA_MapMarker_HidingSpot`, `_Distraction`, `_Container`, `_Keycard`, `_Device`, `_Hazard`). You, teammates, the VIP
and the NPCs (diamonds, vision cones) are drawn from the game state, not markers.

## Surfaces (footsteps, landings)

What you walk on decides the step sound, its puff and how far NPCs hear it:

1. **Declare the surface**: Project Settings → Engine → Physics → Physical Surface, a new `SurfaceTypeN` (the builder
   uses 1 Grass, 2 Stone, 3 Sand, 4 Wood, 5 Metal, 6 Tile; saved in `Config/DefaultEngine.ini`).
2. **Make its physical material**: a `PM_<Name>` in `/Game/Hitmen/Physics` with that **Surface Type**, set as the
   **Phys Material** of every material instance that looks like it (`MI_<Name>`, `MI_Grid_<Name>`).
3. **Give it a row**: `DA_SoundSet` → Surfaces → add `Surface`, `Name`, step takes (`Sounds`, five is good), `Volume`,
   `Pitch` (an existing surface's takes pitched up or down work), `Effect` + `EffectScale` (played only with
   Project Settings → Hitmen → Juice → `bFootstepFX`) and `NoiseMultiplier` (how far NPCs hear it).

Surfaces without a row use `Default Surface` (concrete). A character can override a surface in its footstep component
(`Surface Feedback`, used only when the entry has a sound). Terrain tiles (`HitmenTerrain` tag) pick grass / earth /
rock from their slope (`TerrainGrassMinNormalZ`, `TerrainRockMaxNormalZ` on the footstep component).

## Maps (generated layouts, minimap, thumbnail)

`Tools/Editor/hitmen_levels.py` builds the match maps from code (`build_ldtest()`, `build_gym()`), with helpers for
walls with openings, buildings with switchback stairs, stair wells into the ground, underground tunnels and hedge
mazes. After changing a layout, in the open editor:

1. Rebuild the map: `import hitmen_levels; hitmen_levels.build_ldtest()` (ue_remote), then
   `hitmen_content.build_maps()` for its bounds (`DA_Map_<Name>`, World Bounds). San Lazaro lives in
   `hitmen_sanlazaro.py`: the layout constants at the top (outline, terraces, estate, cave rooms), `height(x, y)` for
   the ground (hills, terraces, plateau, cave floor, ravine) and `gz(x, y)` to stand anything on it. Its ground is a
   heightfield: 5 x 5 tiles of 64 m (1 m cells) built by `UHitmenContentTools::BuildHeightfieldMesh` into
   `/Game/Hitmen/Maps/Terrain/LDTest` (collision = mesh, tagged `HitmenTerrain`), with holes for stair wells and slabs
   of their own. Building the map takes a few minutes.
2. Bake its navmesh: `Tools/Editor/ue_bake_nav.sh L_<Name>`.
3. Check it: `python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_check_reach.py` must end with
   `problems 0`. Snipers and roof points are reached by ladder and are only listed. Then the patrols, in PIE:
   `hitmen_check_patrols.py` twice about 15 s apart (every Patrol guard has a route of two points or more, or follows a
   group leader, and walks) must end with `problems 0`.
4. Minimap and pause map: `hitmen_levels.bake_map_textures()` writes `/Game/Hitmen/UI/Maps/T_Map_<Name>`. On hills the
   cut that opens the roofs follows the terrain under each point (tiles tagged `HitmenTerrain`).
5. Create Room thumbnail: `hitmen_map_shots.py` → `capture('<Name>'); finish(['<Name>'])` renders the camera set
   in its `VIEWS` table and assigns `/Game/Hitmen/UI/MapShots/T_MapShot_<Name>`.

Doorways are 110 x 230 cm (the door's size). The navmesh uses 10 cm cells (`Config/DefaultEngine.ini`): with the
engine's 19 cm cells the 35 cm agent radius closed some of those doorways.
