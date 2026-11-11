"""0.5.0 content patch for an existing project: the builders never recreate existing assets, so 0.5.0 values
that must reach assets that already exist are applied here. Idempotent; run in the open editor (PIE stopped):
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_050.py
Every step is safe to re-run. To run some steps only, set STEPS below (empty = all)."""
import os
import sys

import unreal
sys.path.append(os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir()), "Tools", "Editor"))

import hitmen_content
from hitmen_common import log, LOG

STEPS = []
EAL = unreal.EditorAssetLibrary


def wanted(step):
    return not STEPS or step in STEPS


def clear_bp_defaults(bp_path, fields):
    bp = EAL.load_asset(bp_path) if EAL.does_asset_exist(bp_path) else None
    if not bp:
        log("WARN missing", bp_path)
        return
    default = unreal.get_default_object(bp.generated_class())
    for field in fields:
        default.set_editor_property(field, None)
    unreal.BlueprintEditorLibrary.compile_blueprint(bp)
    EAL.save_loaded_asset(bp, only_if_is_dirty=False)


if wanted("sounds"):
    # UI confirm (team change) is a soft tick instead of a machine start-up; no thunder at match start.
    hitmen_content.build_sound_set()
    log("sound set rebuilt")

if wanted("weapon_vfx"):
    # Guns carry no VFX at all (muzzle flash, impact puffs, dust, blood, back blast), nor the turret or arrows.
    for asset in EAL.list_assets("/Game/Hitmen/Data/Weapons", recursive=False):
        data = EAL.load_asset(asset)
        if isinstance(data, unreal.HitmenWeaponData):
            for field in ("muzzle_fx", "impact_fx", "surface_impact_fx", "flesh_impact_fx", "back_blast_fx"):
                data.set_editor_property(field, None)
            EAL.save_loaded_asset(data, only_if_is_dirty=False)
    clear_bp_defaults("/Game/Hitmen/World/Interactables/Security/BP_Turret", ("muzzle_fx", "impact_fx"))
    clear_bp_defaults("/Game/Hitmen/Weapons/Projectiles/BP_Projectile_Arrow", ("impact_effect",))
    log("weapon VFX cleared")

print("\n".join(LOG))
