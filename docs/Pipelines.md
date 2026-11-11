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
