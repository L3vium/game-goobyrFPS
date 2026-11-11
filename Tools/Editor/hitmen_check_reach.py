"""LD check for the map open in the editor (navmesh baked): every door can be walked through, and every loot spawn,
NPC and retrieval point is on the navmesh and reachable on foot from a team start.

    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_check_reach.py

Snipers and points above LADDER_ONLY_Z (roofs, towers) are reached by ladder, which the navmesh does not link:
they are listed as information, not as problems. Ends with "problems N".
"""
import math

import unreal

LADDER_ONLY_Z = 1000.0
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
WORLD = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
NAV = unreal.NavigationSystemV1
V = unreal.Vector


def project(location, extent):
    point = NAV.project_point_to_navigation(WORLD, location, None, None, extent)
    return point if point and abs(point.x) + abs(point.y) + abs(point.z) > 0 else None


def reachable(a, b):
    path = NAV.find_path_to_location_synchronously(WORLD, a, b)
    return bool(path and path.path_points and not path.is_partial()), path


def class_name(actor):
    return actor.get_class().get_name()


def run():
    actors = EAS.get_all_level_actors()
    problems = 0

    # Doors: from 1.5 m on one side to 1.5 m on the other, in a short straight path.
    doors = [a for a in actors if class_name(a).startswith("BP_Door")]
    for door in doors:
        loc = door.get_actor_location()
        yaw = math.radians(door.get_actor_rotation().yaw)
        fx, fy = math.cos(yaw), math.sin(yaw)
        mid = (loc.x + fx * 55, loc.y + fy * 55)  # the door mesh spans 110 cm from its hinge
        side_a = project(V(mid[0] - fy * 150, mid[1] + fx * 150, loc.z + 20), V(60, 60, 200))
        side_b = project(V(mid[0] + fy * 150, mid[1] - fx * 150, loc.z + 20), V(60, 60, 200))
        ok, path = reachable(side_a, side_b) if side_a and side_b else (False, None)
        if not ok or path.get_path_length() > 600:
            print("DOOR BLOCKED", door.get_actor_label(), door.get_folder_path(), loc)
            problems += 1
    print("doors", len(doors))

    starts = [a for a in actors if class_name(a).startswith("BP_TeamStart")]
    origin = next((p for p in (project(s.get_actor_location(), V(200, 200, 400)) for s in starts) if p), None)
    if not origin:
        print("NO TEAM START ON THE NAVMESH")
        print("problems", problems + 1)
        return
    for start in starts:
        point = project(start.get_actor_location(), V(200, 200, 400))
        if not point or not reachable(origin, point)[0]:
            print("START UNREACHABLE", start.get_actor_label(), start.get_actor_location())
            problems += 1

    targets = [a for a in actors if class_name(a).startswith(("BP_LootSpawn", "BP_RetrievalPoint")) or "NPC" in class_name(a)]
    for target in targets:
        loc = target.get_actor_location()
        ladder_only = "Sniper" in class_name(target) or loc.z > LADDER_ONLY_Z
        point = project(loc, V(150, 150, 300))
        ok = bool(point) and reachable(origin, point)[0]
        if ok:
            continue
        if ladder_only:
            print("ladder only", class_name(target), target.get_actor_label(), loc)
            continue
        print("UNREACHABLE" if point else "OFF NAVMESH", class_name(target), target.get_actor_label(), loc)
        problems += 1
    print("targets", len(targets), "starts", len(starts))
    print("problems", problems)


run()
