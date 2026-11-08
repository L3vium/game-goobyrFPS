"""Run builder steps inside the open editor (remote execution):
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_run_steps.py
Edit STEPS / FORCE below (or set them in the environment before launching the editor).
Modules are reloaded so edits to the builders are picked up without restarting the editor."""
import importlib
import os
import sys

HERE = r"C:/Users/bapti/Documents/_GamesUnreal/_Isart/Hitman/Tools/Editor"
if HERE not in sys.path:
    sys.path.append(HERE)

STEPS = os.environ.get("HITMEN_RUN_STEPS", "content,levels")
FORCE = os.environ.get("HITMEN_RUN_FORCE_MAPS", "L_Gym")

os.environ["HITMEN_BUILD_STEPS"] = STEPS
os.environ["HITMEN_FORCE_MAPS"] = FORCE

import hitmen_common
import hitmen_content
import hitmen_levels
for module in (hitmen_common, hitmen_content, hitmen_levels):
    importlib.reload(module)
hitmen_common.LOG.clear()

exec(open(os.path.join(HERE, "hitmen_build_all.py"), encoding="utf-8").read(), {"__file__": os.path.join(HERE, "hitmen_build_all.py"), "__name__": "__main__"})
print("\n".join(line for line in hitmen_common.LOG if "WARN" in line or "FAIL" in line or "OK" in line)[-6000:])
