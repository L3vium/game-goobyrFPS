#!/usr/bin/env bash
# Stop any PIE session, open a map in the running editor and start PIE.
# Usage: Tools/Editor/ue_pie.sh <map name> [seconds to wait after start, default 8]
cd "$(dirname "$0")/../.."
MAP="$1"; WAIT="${2:-8}"
R() { python Tools/Editor/ue_remote.py "$1"; }
R "import unreal
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
if unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world(): les.editor_request_end_play()" > /dev/null
for i in $(seq 1 20); do
  out=$(R "import unreal; print('PIE', unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world() is not None)")
  echo "$out" | grep -q "PIE False" && break; sleep 1
done
R "import unreal
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
les.load_level('/Game/Hitmen/Maps/$MAP')
les.editor_request_begin_play()" > /dev/null
sleep "$WAIT"
R "import unreal; print('PIE', unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world() is not None)" | tail -1
