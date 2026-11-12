"""Map previews for the Create Room tile, rendered from a fixed camera in the editor (no PIE: no HUD, no first-person
arms, no screen effects). In the open editor:
    python Tools/Editor/ue_remote.py "exec(open(r'<project>/Tools/Editor/hitmen_map_shots.py').read()); capture('LDTest'); finish(['LDTest'])"
capture() opens L_<Map> and writes Saved/MapShots/T_MapShot_<Map>.png; finish() imports the shots as
/Game/Hitmen/UI/MapShots/T_MapShot_<Map> and sets each map's Thumbnail (all maps in VIEWS when no names are given).
"""
import os

import unreal

PROJECT = unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir())
SHOT_DIR = os.path.join(PROJECT, "Saved", "MapShots")
EAL = unreal.EditorAssetLibrary
LES = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
SHOT_SIZE = (1600, 900)
SHOT_FOV = 90.0
SHOT_EV100 = -0.3  # the match maps' fixed exposure (hitmen_levels.MATCH_EV100)

# Camera per map: (location, (pitch, yaw)). Oblique, high, over the playable area.
VIEWS = {
    "LDTest": ((-6000.0, -13000.0, 7500.0), (-35.0, 60.0)),
    "Gym": ((-4200.0, -4200.0, 3600.0), (-35.0, 45.0)),
    "Empty_01": ((-6000.0, -6000.0, 4000.0), (-30.0, 45.0)),
    "Empty_02": ((-6000.0, -6000.0, 4000.0), (-30.0, 45.0)),
}


def capture(name):
    """Renders VIEWS[name] in L_<name> to Saved/MapShots/T_MapShot_<name>.png with a temporary scene capture."""
    LES.load_level("/Game/Hitmen/Maps/L_" + name)
    world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    (x, y, z), (pitch, yaw) = VIEWS[name]
    target = unreal.RenderingLibrary.create_render_target2d(world, SHOT_SIZE[0], SHOT_SIZE[1], unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB)
    camera = EAS.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(x, y, z), unreal.Rotator(roll=0.0, pitch=pitch, yaw=yaw))
    component = camera.capture_component2d
    component.set_editor_property("texture_target", target)
    component.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
    component.set_editor_property("fov_angle", SHOT_FOV)
    settings = component.get_editor_property("post_process_settings")
    for key, value in (("override_auto_exposure_method", True), ("auto_exposure_method", unreal.AutoExposureMethod.AEM_HISTOGRAM),
                       ("override_auto_exposure_min_brightness", True), ("auto_exposure_min_brightness", SHOT_EV100),
                       ("override_auto_exposure_max_brightness", True), ("auto_exposure_max_brightness", SHOT_EV100)):
        settings.set_editor_property(key, value)
    component.set_editor_property("post_process_settings", settings)
    component.capture_scene()
    component.capture_scene()  # the first frame warms up the exposure history
    os.makedirs(SHOT_DIR, exist_ok=True)
    unreal.RenderingLibrary.export_render_target(world, target, SHOT_DIR, "T_MapShot_%s.png" % name)
    EAS.destroy_actor(camera)
    print("shot", name)


def finish(names=None):
    """Imports the shots and sets them as map thumbnails."""
    names = names or list(VIEWS)
    tasks = []
    for name in names:
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
    for name in names:
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
