"""Patrol check, with PIE running on a match map: every guard with the Patrol behaviour follows a route of at least two
points (or follows a guard group leader), and actually walks. Run it twice, about 15 s apart: the first run records
where everyone stands, the second compares and ends with "problems N".

    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_check_patrols.py   # record
    (wait 15 s)
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_check_patrols.py   # check
"""
import json
import os
import time

import unreal

MIN_MOVE = 300.0   # cm walked between the two runs
STATE = os.path.join(unreal.Paths.project_saved_dir(), "PatrolCheck.json")

world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()
if not world:
    print("PIE is not running")
    raise SystemExit

followers = set()
for group in unreal.GameplayStatics.get_all_actors_of_class(world, unreal.HitmenGuardGroup):
    members = list(group.get_editor_property("members"))
    for member in members[1:]:
        if member:
            followers.add(member.get_name())

patrollers = {}
problems = 0
for npc in unreal.GameplayStatics.get_all_actors_of_class(world, unreal.HitmenNPC):
    # Guards only: VIP candidates wander and snipers hold their perch.
    if "Guard" not in npc.get_class().get_name() or npc.get_editor_property("behavior") != unreal.HitmenNPCBehavior.PATROL or npc.is_dead():
        continue
    name = npc.get_name()
    if name not in followers:
        route = npc.get_patrol_route()
        points = len(route.get_editor_property("points")) if route else 0
        if points < 2:
            print("PROBLEM no route:", name, npc.get_actor_location())
            problems += 1
            continue
    loc = npc.get_actor_location()
    patrollers[name] = (loc.x, loc.y, loc.z)

previous = None
if os.path.exists(STATE):
    with open(STATE, encoding="utf-8") as f:
        previous = json.load(f)
    if time.time() - previous.get("time", 0) > 120 or previous.get("map") != world.get_name():
        previous = None

if previous is None:
    with open(STATE, "w", encoding="utf-8") as f:
        json.dump({"time": time.time(), "map": world.get_name(), "npcs": patrollers}, f)
    print("recorded", len(patrollers), "patrollers; run again in about 15 s")
else:
    os.remove(STATE)
    for name, (x, y, z) in patrollers.items():
        old = previous["npcs"].get(name)
        if not old:
            continue
        moved = ((x - old[0]) ** 2 + (y - old[1]) ** 2) ** 0.5
        if moved < MIN_MOVE:
            print("PROBLEM not walking:", name, "moved %.0f cm" % moved, (x, y, z))
            problems += 1
    print("patrollers", len(patrollers), "followers", len(followers))
    print("problems", problems)
