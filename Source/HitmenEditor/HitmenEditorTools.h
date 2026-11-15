// Hitmen — editor tools exposed to Python (Tools/Editor/*.py): behavior tree / blackboard builder, QA helpers

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenEditorTools.generated.h"

class UBlackboardData;
class UBehaviorTree;

UCLASS()
class HITMENEDITOR_API UHitmenEditorTools : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * Creates (or rebuilds) BB_NPC and BT_NPC with its subtrees in Folder:
	 * BT_Patrol, BT_Investigate, BT_Search, BT_Combat, BT_Sniper, BT_CallReinforcements.
	 * The trees are real editor graphs, editable by designers afterwards.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static bool BuildNPCBehaviorTrees(const FString& Folder);

	/**
	 * QA (PIE recordings from scripts): keeps the editor ticking at full rate while another window has the focus
	 * ("Use Less CPU when in Background" off for this editor session only; nothing is saved to the config).
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static void SetBackgroundThrottling(bool bThrottle);

	/**
	 * QA: the next PIE session runs as a listen server with NumPlayers - 1 clients in one process (1 = standalone).
	 * Editor session only (the play settings are not saved).
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static void SetPIENetMode(int32 NumPlayers);

	/** QA: every Play In Editor world (the listen server first, then the clients), for multiplayer test scripts. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static TArray<UWorld*> GetPIEWorlds();

	/**
	 * QA: runs a console command as World's first local player, with RPCs going over the network (editor Python runs
	 * with actor script execution allowed, which makes every RPC it triggers run locally).
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static void RunPIEConsoleCommand(UWorld* World, const FString& Command);
};
