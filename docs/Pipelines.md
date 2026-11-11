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
   minimap / the pause map / the legend, **Clamp To Minimap Edge** for objectives, and **Legend Order**.
2. **Mark the actor**: in its Blueprint, *Add Component → Hitmen Map Marker*, set **Type** to the kind (and an
   optional **Label** shown next to it on the pause map).
3. Done: the legend gets a row with a toggle as soon as one marker of that kind is on the map. Gameplay code hides a
   marker with **Set Marker Visible** (a disabled camera, an unused retrieval point do it already).

Retrieval points and security cameras carry one (`DA_MapMarker_RetrievalPoint`, `DA_MapMarker_Camera`). You,
teammates, the VIP and guards on alert are drawn from the game state, not markers.

## Maps (generated layouts, minimap, thumbnail)

`Tools/Editor/hitmen_levels.py` builds the match maps from code (`build_ldtest()`, `build_gym()`), with helpers for
walls with openings, buildings with switchback stairs, stair wells into the ground, underground tunnels and hedge
mazes. After changing a layout, in the open editor:

1. Rebuild the map: `import hitmen_levels; hitmen_levels.build_ldtest()` (ue_remote), then
   `hitmen_content.build_maps()` for its bounds (`DA_Map_<Name>`, World Bounds).
2. Bake its navmesh: `Tools/Editor/ue_bake_nav.sh L_<Name>`.
3. Check it: `python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_check_reach.py` must end with
   `problems 0`. Snipers and roof points are reached by ladder and are only listed.
4. Minimap and pause map: `hitmen_levels.bake_map_textures()` writes `/Game/Hitmen/UI/Maps/T_Map_<Name>`.
5. Create Room thumbnail: `hitmen_map_shots.py` → `capture('<Name>'); finish(['<Name>'])` renders the camera set
   in its `VIEWS` table and assigns `/Game/Hitmen/UI/MapShots/T_MapShot_<Name>`.

Doorways are 110 x 230 cm (the door's size). The navmesh uses 10 cm cells (`Config/DefaultEngine.ini`): with the
engine's 19 cm cells the 35 cm agent radius closed some of those doorways.
