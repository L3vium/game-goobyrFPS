#!/usr/bin/env bash
# Usage: Tools/Editor/ue_shot.sh <map name> [seconds before shot] [extra python run in PIE before the shot]
# Stops any PIE session, opens the map in the running editor, starts PIE and renders a high-res shot.
cd "$(dirname "$0")/../.."
MAP="$1"; WAIT="${2:-8}"; EXTRA="${3:-}"
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
if [ -n "$EXTRA" ]; then R "$EXTRA"; sleep 2; fi
NAME="Shot_${MAP}_$(date +%H%M%S)"
R "import unreal
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()
unreal.SystemLibrary.execute_console_command(w, 'HighResShot 1600x900 filename=$NAME')" > /dev/null
sleep 6
echo "Saved/Screenshots/WindowsEditor/$NAME.png"
