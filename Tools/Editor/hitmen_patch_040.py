"""0.4.0 content patch for an existing project (the builders never recreate existing assets):
focus overlay usable on skeletal guns, NPC aim montages on every gun. Idempotent.
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_040.py"""
import os
import sys

# Remote execution has no __file__: the project's Tools/Editor folder is found from the project dir.
import unreal
sys.path.append(os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir()), "Tools", "Editor"))

import unreal
import hitmen_content
from hitmen_common import log, load, save, path

MEL = unreal.MaterialEditingLibrary

focus = load(path("Materials", "Master") + "/M_FocusFresnel")
if focus:
    focus.set_editor_property("used_with_skeletal_mesh", True)
    MEL.recompile_material(focus)
    save(focus)
    log("focus material: skeletal usage on")

anims = hitmen_content.ANIMS
pistol_aim = hitmen_content.montage("AM_Pistol_Aim", anims + "/Pistol/MF_Pistol_Idle_ADS")
rifle_aim = hitmen_content.montage("AM_Rifle_Aim", anims + "/Rifle/MF_Rifle_Idle_ADS")
for asset in unreal.EditorAssetLibrary.list_assets(path("Data", "Weapons"), recursive=False):
    data = unreal.EditorAssetLibrary.load_asset(asset)
    if not isinstance(data, unreal.HitmenWeaponData):
        continue
    fire = data.get_editor_property("fire_montage")
    if not fire:
        continue
    aim = pistol_aim if "Pistol" in str(fire) else rifle_aim
    data.set_editor_property("aim_montage", aim)
    save(data)
    log("aim montage:", data.get_name(), aim.get_name() if aim else None)
print("\n".join(hitmen_content.LOG if hasattr(hitmen_content, "LOG") else []))
