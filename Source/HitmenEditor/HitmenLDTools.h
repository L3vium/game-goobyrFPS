// Hitmen — level design tools: the PCG graphs the LD structures use, stamping structures and builder pieces where the
// viewport looks, and the checks. Python: unreal.HitmenLDTools; the "Hitmen LD" tab (Window menu, toolbar) wraps it.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenLDTools.generated.h"

UCLASS()
class HITMENEDITOR_API UHitmenLDTools : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/** Creates (or rebuilds in place) /Game/Hitmen/PCG/PCG_Hitmen_CoverLine, PCG_Hitmen_Rocks and
	 *  PCG_Hitmen_RoofDressing, and saves them. Returns how many graphs were built. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static int32 BuildPCGGraphs();

	/** Where the level viewport looks (its centre ray onto the world, else 15 m ahead on the ground), snapped to Grid. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static FVector GetViewportFocus(float Grid = 50.f);

	/** Spawns an actor of Class at the viewport focus (folder "LD"), selects it and returns it. Graph (optional) is set
	 *  on a PCG line. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static AActor* StampActor(UClass* Class, float Grid = 50.f, const FString& GraphPath = TEXT(""));

	/** Runs a builder piece from Tools/Editor/hitmen_levels.py at the viewport focus: Python with {x} {y} {z}. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static bool StampBuilderPiece(const FString& PythonTemplate, float Grid = 50.f);

	/** Generates the PCG of the selected LD buildings (roof dressing) and PCG lines. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static int32 GenerateSelectedPCG();

	/** Bakes the selected LD structures to static mesh actors. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static int32 BakeSelected();

	/** Runs a Python file of Tools/Editor in the editor (hitmen_check_reach.py...). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static bool RunToolScript(const FString& FileName);

	/** Rebuilds the navmesh of the open map (save the map after it finishes). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|LD")
	static void RebuildNavigation();
};
