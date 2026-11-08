// Hitmen — editor tools exposed to Python (Tools/Editor/*.py): behavior tree / blackboard builder

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
};
