#!/usr/bin/env bash
# Screenshot WITH the UI (HighResShot leaves widgets out). Uses the running PIE session.
# Usage: Tools/Editor/ue_ui_shot.sh <name> [python run in the editor before the shot]
#   Tools/Editor/ue_ui_shot.sh Title
#   Tools/Editor/ue_ui_shot.sh Pause "import unreal; ..."
# Prints the PNG path. Start PIE first (Tools/Editor/ue_pie.sh <map>).
cd "$(dirname "$0")/../.."
NAME="$1"; EXTRA="${2:-}"
R() { python Tools/Editor/ue_remote.py "$1"; }
if [ -n "$EXTRA" ]; then R "$EXTRA" > /dev/null; sleep 1.5; fi
DIR="Saved/Screenshots/WindowsEditor"
before=$(ls -t "$DIR"/ScreenShot*.png 2>/dev/null | head -1)
R "import unreal
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()
unreal.SystemLibrary.execute_console_command(w, 'Shot showui')" > /dev/null
for i in $(seq 1 20); do
  sleep 0.5
  latest=$(ls -t "$DIR"/ScreenShot*.png 2>/dev/null | head -1)
  if [ -n "$latest" ] && [ "$latest" != "$before" ]; then break; fi
done
OUT="$DIR/UI_${NAME}.png"
sleep 0.5
mv -f "$latest" "$OUT" && echo "$OUT"
