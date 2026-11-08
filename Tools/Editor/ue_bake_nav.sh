#!/usr/bin/env bash
# Usage: Tools/Editor/ue_bake_nav.sh L_Gym L_LDTest ... — bakes navmesh into maps in the running editor.
cd "$(dirname "$0")/../.."
R() { python Tools/Editor/ue_remote.py "$1"; }
for MAP in "$@"; do
  R "import unreal
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
les.load_level('/Game/Hitmen/Maps/$MAP')
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
unreal.SystemLibrary.execute_console_command(w, 'RebuildNavigation')" > /dev/null
  for i in $(seq 1 120); do
    sleep 3
    out=$(R "import unreal
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
print('BUILDING', unreal.NavigationSystemV1.is_navigation_being_built(w))")
    echo "$out" | grep -q "BUILDING False" && break
  done
  R "import unreal
print('$MAP saved', unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level())"
done
