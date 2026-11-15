"""LD check for the map open in the editor: every door fills its wall opening (no gap a player sees through).

    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_check_doors.py

For each door (closed), rays from the panel's middle run sideways along it and up: the wall must start within TOLERANCE
of the panel's edges and the lintel within TOLERANCE of its top. Two leaves of a double door count as one opening.
Ends with "problems N".
"""
import unreal

TOLERANCE = 3.0
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
WORLD = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
V = unreal.Vector

doors = [a for a in EAS.get_all_level_actors() if isinstance(a, unreal.HitmenDoor)]


def first_hit(start, end, ignore):
    hit = unreal.SystemLibrary.line_trace_single(WORLD, start, end, unreal.TraceTypeQuery.TRACE_TYPE_QUERY1, False, ignore,
                                                 unreal.DrawDebugTrace.NONE, True)
    if not hit:
        return None
    return hit.to_tuple()[4]  # location


problems = 0
for door in doors:
    width = door.get_editor_property("width")
    height = door.get_editor_property("height")
    origin = door.get_actor_location()
    fwd = door.get_actor_forward_vector()   # along the panel (+X)
    up = V(0, 0, 1)
    mid = origin + fwd * (width * 0.5) + up * (height * 0.5)
    ignore = [door] + [d for d in doors if (d.get_actor_location() - origin).length() < 300]
    gaps = []
    for sign, label in ((-1.0, "-X"), (1.0, "+X")):
        for dz in (-60.0, 60.0):
            start = mid + up * dz
            hit = first_hit(start, start + fwd * (sign * (width * 0.5 + 60.0)), ignore)
            edge = start + fwd * (sign * width * 0.5)
            if hit is None:
                # A double door: the other leaf continues the opening; skip this side.
                partner = any((d.get_actor_location() - (origin + fwd * (sign * width))).length() < 5 or
                              (d.get_actor_location() - (origin + fwd * (sign * d.get_editor_property("width")))).length() < 5
                              for d in doors if d != door)
                if not partner:
                    gaps.append("%s no wall within 60 cm" % label)
                break
            gap = (hit - edge).length()
            if gap > TOLERANCE:
                gaps.append("%s gap %.0f cm" % (label, gap))
                break
    hit = first_hit(mid, mid + up * (height * 0.5 + 60.0), ignore)
    if hit is None:
        gaps.append("top: no lintel within 60 cm")
    else:
        gap = (hit - (mid + up * (height * 0.5))).length()
        if gap > TOLERANCE:
            gaps.append("top gap %.0f cm" % gap)
    if gaps:
        problems += 1
        print("DOOR", door.get_actor_label(), "at", [round(origin.x), round(origin.y), round(origin.z)], "; ".join(gaps))
print("doors", len(doors), "problems", problems)
