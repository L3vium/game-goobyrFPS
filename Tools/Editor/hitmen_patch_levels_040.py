"""0.4.0 level patch for the hand-edited maps (idempotent, editor open, PIE stopped):
- windows turned to lie in their wall (the builder spawned them across it);
- Gym: the guard group keeps one follower (fewer guards, the station still works);
- LDTest: spawn points inside a guard's sight range (plus margin) are removed.
    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_patch_levels_040.py"""
import unreal

LES = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
SAFE_START_DISTANCE = 2600.0   # guard sight is 1950 (Normal) x 1.2 (Hard) = 2340
MIN_STARTS = 8


def fix_windows(actors):
    turned = 0
    for actor in actors:
        if actor.get_class().get_name().startswith("BP_Window") and not actor.tags.count(unreal.Name("Hitmen040Turned")):
            rot = actor.get_actor_rotation()
            actor.set_actor_rotation(unreal.Rotator(pitch=rot.pitch, yaw=rot.yaw + 90.0, roll=rot.roll), False)
            actor.tags = list(actor.tags) + [unreal.Name("Hitmen040Turned")]
            turned += 1
    return turned


for level in ("L_Gym", "L_LDTest"):
    LES.load_level("/Game/Hitmen/Maps/" + level)
    actors = EAS.get_all_level_actors()
    print(level, "windows turned:", fix_windows(actors))
    if level == "L_Gym":
        for actor in actors:
            if actor.get_actor_label() == "BP_NPC_Guard5":
                EAS.destroy_actor(actor)
                print("Gym: removed one group follower")
    else:
        guards = [a for a in actors if a.get_class().get_name().startswith(("BP_NPC_Guard", "BP_NPC_Sniper"))]
        starts = [a for a in actors if a.get_class().get_name().startswith("BP_TeamStart")]
        unsafe = []
        for start in starts:
            loc = start.get_actor_location()
            nearest = min((loc - g.get_actor_location()).length() for g in guards) if guards else 1e9
            if nearest < SAFE_START_DISTANCE:
                unsafe.append((nearest, start))
        unsafe.sort(key=lambda pair: pair[0])
        removable = max(0, len(starts) - MIN_STARTS)
        for nearest, start in unsafe[:removable]:
            print("LDTest: removed start", start.get_actor_label(), "guard at", int(nearest))
            EAS.destroy_actor(start)
        print("LDTest: starts", len(starts), "unsafe", len(unsafe), "kept", len(starts) - min(len(unsafe), removable))
    LES.save_current_level()
