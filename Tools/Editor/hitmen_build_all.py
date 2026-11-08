"""Entry point: builds every generated asset, then the maps. See hitmen_common.py for usage."""
import os
import sys
import traceback

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import unreal
import hitmen_common
import hitmen_content

STEPS = [s.strip() for s in os.environ.get("HITMEN_BUILD_STEPS", "content,levels").split(",")]
log_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Saved", "HitmenBuild.log")
try:
    result = None
    if "content" in STEPS:
        result = hitmen_content.run()
    if "levels" in STEPS:
        import hitmen_levels
        hitmen_levels.run()
    unreal.EditorAssetLibrary.save_directory("/Game/Hitmen", only_if_is_dirty=True, recursive=True)
    hitmen_common.log("BUILD OK")
except Exception:
    hitmen_common.log("BUILD FAILED\n" + traceback.format_exc())
finally:
    os.makedirs(os.path.dirname(log_path), exist_ok=True)
    with open(log_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(hitmen_common.LOG))
