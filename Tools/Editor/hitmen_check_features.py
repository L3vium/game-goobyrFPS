"""LD check for the map open in the editor: every placeable feature is in it at least once (San Lazaro shows them all).

    python Tools/Editor/ue_remote.py --file Tools/Editor/hitmen_check_features.py

Counts the placed actors by Blueprint and lists the expected ones that are missing; route-assigned patrol groups and
the NPC loadouts are checked too. Ends with "missing N".
"""
import unreal

EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)

# Every Blueprint a level designer places (World/, Characters/NPC, Spawning). Gym-only toys are listed apart.
EXPECTED = [
    "BP_Door", "BP_Door_Keycard1", "BP_Door_Keycard2", "BP_Door_Keycard3", "BP_Window", "BP_VentHatch", "BP_KeycardPickup",
    "BP_Ladder", "BP_Gutter", "BP_Locker", "BP_BodyContainer", "BP_WeaponChest", "BP_TallGrass",
    "BP_Distraction_Radio", "BP_Distraction_Alarm", "BP_Distraction_TV", "BP_SecurityCamera", "BP_SecurityMonitor",
    "BP_ControlConsole", "BP_Turret", "BP_HackPanel", "BP_CircuitBreaker", "BP_LightSwitch", "BP_LightFixture",
    "BP_BlackoutVolume", "BP_RoomVolume", "BP_PoisonableDrink", "BP_ExplosiveBarrel", "BP_MountedGun", "BP_Tank",
    "BP_RetrievalPoint_Helicopter", "BP_RetrievalPoint_Car", "BP_RetrievalPoint_Crate", "BP_LootSpawn_Weapon",
    "BP_LootSpawn_Item", "BP_TeamStart", "BP_PatrolRoute", "BP_NPC_Guard", "BP_NPC_Guard_SMG", "BP_NPC_Guard_Rifle",
    "BP_NPC_Guard_Shotgun", "BP_NPC_Sniper", "BP_NPC_VIPCandidate",
]
GYM_ONLY = ["BP_Zipline", "BP_JumpPad", "BP_Teleporter", "BP_BreakableWall", "BP_Container", "BP_Container_Random", "BP_GuardGroup"]

counts = {}
groups = 0
for actor in EAS.get_all_level_actors():
    name = actor.get_class().get_name()
    if name.endswith("_C"):
        name = name[:-2]
    counts[name] = counts.get(name, 0) + 1
    if isinstance(actor, unreal.HitmenPatrolRoute) and len(actor.get_editor_property("assigned_np_cs")) >= 3:
        groups += 1

missing = [n for n in EXPECTED if counts.get(n, 0) == 0]
for n in EXPECTED:
    print("%-30s %d" % (n, counts.get(n, 0)))
print("patrol groups of 3+ on a route:", groups)
print("gym-only (not required):", ", ".join("%s %d" % (n, counts.get(n, 0)) for n in GYM_ONLY))
if groups == 0:
    missing.append("patrol group")
print("missing", len(missing), ", ".join(missing))
