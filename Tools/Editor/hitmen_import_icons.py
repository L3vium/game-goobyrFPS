"""Imports Tools/Icons/Out/T_Icon_*.png into /Game/Hitmen/UI/Icons and assigns them to the matching
DA_Weapon_<Name> / DA_Ability_<Name> (Icon). Re-run after replacing a PNG. Editor open:
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_import_icons.py"""
import glob
import os

import unreal

PROJECT = unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir())
SOURCE = os.path.join(PROJECT, "Tools", "Icons", "Out")
DEST = "/Game/Hitmen/UI/Icons"
EAL = unreal.EditorAssetLibrary

tasks = []
for png in sorted(glob.glob(os.path.join(SOURCE, "T_Icon_*.png"))):
    task = unreal.AssetImportTask()
    task.filename = png
    task.destination_path = DEST
    task.replace_existing = True
    task.automated = True
    task.save = False
    tasks.append(task)
unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)

assigned = 0
for png in sorted(glob.glob(os.path.join(SOURCE, "T_Icon_*.png"))):
    name = os.path.splitext(os.path.basename(png))[0]
    texture = EAL.load_asset(DEST + "/" + name)
    if not texture:
        print("missing", name)
        continue
    texture.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_EDITOR_ICON)
    texture.set_editor_property("mip_gen_settings", unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS)
    texture.set_editor_property("lod_group", unreal.TextureGroup.TEXTUREGROUP_UI)
    texture.set_editor_property("srgb", True)
    EAL.save_loaded_asset(texture)
    kind, key = name[len("T_Icon_"):].split("_", 1)
    data_path = "/Game/Hitmen/Data/%ss/DA_%s_%s" % ("Weapon" if kind == "Weapon" else "Abilitie", kind, key)
    data = EAL.load_asset(data_path) if EAL.does_asset_exist(data_path) else None
    if data:
        data.set_editor_property("icon", texture)
        EAL.save_loaded_asset(data)
        assigned += 1
    else:
        print("no data asset for", name, data_path)
print("icons imported:", len(tasks), "assigned:", assigned)
