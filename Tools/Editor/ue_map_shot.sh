#!/usr/bin/env bash
# Map preview for the Create Room tile: plays <Map> in PIE and saves the spawn view as
# Saved/MapShots/T_MapShot_<Map>.png. Then import with hitmen_map_shots.py finish().
# Usage: Tools/Editor/ue_map_shot.sh <Map>
cd "$(dirname "$0")/../.."
R() { timeout 60 python Tools/Editor/ue_remote.py "$1"; }
R "import unreal
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
if unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world(): les.editor_request_end_play()" > /dev/null
sleep 4
R "import unreal
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
les.load_level('/Game/Hitmen/Maps/L_$1')
les.editor_request_begin_play()" > /dev/null
sleep 10
R "import unreal
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()
unreal.SystemLibrary.execute_console_command(w, 'HighResShot 1600x900 filename=T_MapShot_$1')" > /dev/null
sleep 6
mkdir -p Saved/MapShots && cp "Saved/Screenshots/WindowsEditor/T_MapShot_$1.png" "Saved/MapShots/T_MapShot_$1.png" && echo "shot $1"
