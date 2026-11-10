"""Front-end lighting patch for the existing L_FrontEnd (idempotent, editor open, PIE stopped):
a fixed exposure for the whole scene (no eye adaptation between screens), a lit briefing room,
a softer agent stage, the agent framed further left.
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_frontend.py
Same values as hitmen_levels.build_front_end (FRONTEND_* constants)."""
import os
import sys

import unreal
sys.path.append(os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir()), "Tools", "Editor"))
import importlib
import hitmen_levels
importlib.reload(hitmen_levels)
from hitmen_common import log, LOG

LES = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)

LES.load_level("/Game/Hitmen/Maps/L_FrontEnd")
actors = EAS.get_all_level_actors()
by_label = {a.get_actor_label(): a for a in actors}

hitmen_levels.front_end_exposure(by_label.get("FrontEndExposure"))
hitmen_levels.front_end_tune_lights(actors)
LES.save_current_level()
log("front end patched")
print("\n".join(LOG))
