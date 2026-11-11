"""PIE helper: give the local player weapons and abilities (UI checks, tests).
    exec(open(r'<project>/Tools/Editor/hitmen_pie_loadout.py').read()); loadout(['Pistol', 'Sniper'], ['Jetpack', 'Stone'])"""
import unreal


def loadout(weapons=(), abilities=(), slot=0):
    world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()
    pc = unreal.GameplayStatics.get_player_controller(world, 0)
    pawn = pc.get_controlled_pawn()
    inv = pawn.get_component_by_class(unreal.HitmenInventoryComponent)
    for name in weapons:
        inv.give_weapon(unreal.load_asset("/Game/Hitmen/Data/Weapons/DA_Weapon_" + name), -1, True)
    if weapons:
        inv.select_slot(slot)
    comp = pawn.get_component_by_class(unreal.HitmenAbilityComponent)
    for name in abilities:
        comp.set_ability(unreal.load_asset("/Game/Hitmen/Data/Abilities/DA_Ability_" + name))
    return pawn
