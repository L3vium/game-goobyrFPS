"""Map previews for the Create Room tile. First Tools/Editor/ue_map_shot.sh <Map> for each map
(PIE spawn view -> Saved/MapShots/T_MapShot_<Map>.png), then, PIE stopped:
    python Tools/Editor/ue_remote.py "exec(open(r'<project>/Tools/Editor/hitmen_map_shots.py').read()); finish()"
imports them as /Game/Hitmen/UI/MapShots/T_MapShot_<Map> and sets each map's Thumbnail.
"""
import os

import unreal

PROJECT = unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir())
SHOT_DIR = os.path.join(PROJECT, "Saved", "MapShots")
EAL = unreal.EditorAssetLibrary
LES = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)

# Camera per map: (location, (pitch, yaw)). Oblique, high, over the playable area.
VIEWS = {
    "LDTest": ((-9500.0, -9500.0, 7000.0), (-32.0, 45.0)),
    "Gym": ((-4200.0, -4200.0, 3600.0), (-35.0, 45.0)),
    "Empty_01": ((-6000.0, -6000.0, 4000.0), (-30.0, 45.0)),
    "Empty_02": ((-6000.0, -6000.0, 4000.0), (-30.0, 45.0)),
}


def finish():
    """Imports the shots and sets them as map thumbnails."""
    tasks = []
    for name in VIEWS:
        path = os.path.join(SHOT_DIR, "T_MapShot_%s.png" % name)
        if not os.path.exists(path):
            print("missing shot", path)
            continue
        task = unreal.AssetImportTask()
        task.filename = path
        task.destination_path = "/Game/Hitmen/UI/MapShots"
        task.replace_existing = True
        task.automated = True
        tasks.append(task)
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)
    for name in VIEWS:
        texture = EAL.load_asset("/Game/Hitmen/UI/MapShots/T_MapShot_" + name) if EAL.does_asset_exist("/Game/Hitmen/UI/MapShots/T_MapShot_" + name) else None
        data = EAL.load_asset("/Game/Hitmen/Data/Maps/DA_Map_" + name)
        if texture and data:
            texture.set_editor_property("lod_group", unreal.TextureGroup.TEXTUREGROUP_UI)
            texture.set_editor_property("mip_gen_settings", unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS)
            EAL.save_loaded_asset(texture)
            data.set_editor_property("thumbnail", texture)
            EAL.save_loaded_asset(data)
            print("thumbnail set", name)
    LES.load_level("/Game/Hitmen/Maps/L_FrontEnd")
