"""Sorts the icon textures into one folder per kind, so the team finds and swaps them easily:
    /Game/Hitmen/UI/Icons/Weapons                  T_Icon_Weapon_<Name>   (DA_Weapon_<Name>.Icon)
    /Game/Hitmen/UI/Icons/Abilities/Move           T_Icon_Ability_<Name>  (DA_Ability_<Name>.Icon, move slot)
    /Game/Hitmen/UI/Icons/Abilities/Manipulation   T_Icon_Ability_<Name>  (manipulation slot)
References are kept (rename + redirector fix-up). Idempotent. Editor open, PIE stopped:
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_organize_icons.py"""
import unreal

EAL = unreal.EditorAssetLibrary
ROOT = "/Game/Hitmen/UI/Icons"


def folder_for(name):
    if name.startswith("T_Icon_Weapon_"):
        return ROOT + "/Weapons"
    if name.startswith("T_Icon_Ability_"):
        data_path = "/Game/Hitmen/Data/Abilities/DA_Ability_" + name[len("T_Icon_Ability_"):]
        data = EAL.load_asset(data_path) if EAL.does_asset_exist(data_path) else None
        slot = data.get_editor_property("slot") if data else None
        return ROOT + ("/Abilities/Move" if slot == unreal.HitmenAbilitySlot.MOVE else "/Abilities/Manipulation")
    return ROOT + "/Misc"


moved = 0
for asset in EAL.list_assets(ROOT, recursive=False, include_folder=False):
    obj = EAL.load_asset(asset)
    if not isinstance(obj, unreal.Texture2D):
        continue
    name = obj.get_name()
    target_folder = folder_for(name)
    if not EAL.does_directory_exist(target_folder):
        EAL.make_directory(target_folder)
    if EAL.rename_asset(obj.get_path_name().split(".")[0], target_folder + "/" + name):
        moved += 1
    else:
        print("could not move", name)

# Point every referencer at the new paths and delete the redirectors left behind.
registry = unreal.AssetRegistryHelpers.get_asset_registry()
redirectors = []
for data in registry.get_assets_by_path(ROOT, recursive=True):
    if str(data.asset_class_path.asset_name) == "ObjectRedirector":
        redirectors.append(data.get_asset())
if redirectors:
    unreal.AssetToolsHelpers.get_asset_tools().fixup_referencers(redirectors)
for asset in EAL.list_assets("/Game/Hitmen/Data", recursive=True):
    obj = EAL.load_asset(asset)
    if isinstance(obj, (unreal.HitmenWeaponData, unreal.HitmenAbilityData)):
        EAL.save_loaded_asset(obj, only_if_is_dirty=True)
print("icons moved:", moved, "redirectors fixed:", len(redirectors))
