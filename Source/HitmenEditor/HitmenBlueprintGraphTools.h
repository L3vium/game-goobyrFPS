// Hitmen — editor tools exposed to Python / the MCP: lay out Blueprint graphs by the project
// convention (named graphs, colour-coded comment boxes, one stub per Juice_On… hook)

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenBlueprintGraphTools.generated.h"

class UBlueprint;
class UEdGraph;
class UEdGraphNode_Comment;

UCLASS()
class HITMENEDITOR_API UHitmenBlueprintGraphTools : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * Lays out a Blueprint by docs/Conventions.md:
	 * - a "Juice" graph with an override event for every Juice_On… BlueprintImplementableEvent of
	 *   its parent classes, inside a purple comment box (cosmetic only);
	 * - for actors, an "Init" graph with BeginPlay inside a green comment box, replacing the
	 *   greyed default BeginPlay / ActorBeginOverlap / Tick ghost nodes of the EventGraph.
	 * Idempotent: events already overridden anywhere are left where they are.
	 * @return Number of Juice events added, or -1 when the Blueprint is invalid.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static int32 LayOutConventionGraphs(UBlueprint* Blueprint, bool bCompile = true);

	/** Returns the ubergraph page named GraphName, creating it when missing. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static UEdGraph* FindOrAddGraph(UBlueprint* Blueprint, FName GraphName);

	/** Adds a coloured comment box (project colour code) to a graph of the Blueprint. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static UEdGraphNode_Comment* AddCommentBox(UBlueprint* Blueprint, FName GraphName, const FString& Text, FLinearColor Color, FVector2D Position, FVector2D Size);

	/** Colours of docs/Conventions.md. */
	static const FLinearColor InitColor;
	static const FLinearColor JuiceColor;
};
