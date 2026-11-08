"""Regenerate BB_NPC and the BT_* behavior trees. Run headless (editor closed):
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Editor/ue_rebuild.ps1 -RunPython Tools/Editor/hitmen_build_ai.py
Rebuilding trees inside a running editor can crash it (stale runtime templates), so don't.
"""
import unreal

FOLDER = "/Game/Hitmen/AI/BehaviorTrees"
TREES = ("BB_NPC", "BT_Patrol", "BT_Guard", "BT_Investigate", "BT_Search", "BT_CallReinforcements",
         "BT_Sniper", "BT_Combat", "BT_NPC")

ok = unreal.HitmenEditorTools.build_npc_behavior_trees(FOLDER)
saved = [name for name in TREES if unreal.EditorAssetLibrary.save_asset(FOLDER + "/" + name, only_if_is_dirty=False)]
unreal.log("[Hitmen] behavior trees built=%s saved=%s" % (ok, ", ".join(saved)))
