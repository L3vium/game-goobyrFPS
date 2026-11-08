// Hitmen — editor tools exposed to Python: bind AnimBlueprint asset pins to UHitmenAnimInstance
// variables so ABPs read the DA_AnimSet instead of hard-coded sequences

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenAnimGraphTools.generated.h"

class UAnimBlueprint;
class UHitmenAnimSet;
class UAnimSequenceBase;

UCLASS()
class HITMENEDITOR_API UHitmenAnimGraphTools : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * For every Sequence Player / Sequence Evaluator / Blend Space Player (and evaluator, aim
	 * offset) node of the AnimBlueprint (AnimGraph, state machines, states, layers), whose current
	 * asset is a key of AssetToVariable, exposes its asset pin ("Sequence" / "BlendSpace") and wires
	 * a variable-get node reading the mapped variable (a UHitmenAnimInstance property such as
	 * "LocomotionBlendSpace"). Keys are asset paths, with or without the object name
	 * ("/Game/X/MM_Idle" or "/Game/X/MM_Idle.MM_Idle"). Pins already linked are left alone, so it
	 * is idempotent. Then compiles and marks the package dirty (save it afterwards).
	 * Returns the number of pins wired, or -1 on invalid input.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static int32 BindAssetPinsToVariables(UAnimBlueprint* AnimBlueprint, const TMap<FString, FName>& AssetToVariable, bool bCompile = true);

	/**
	 * Same, with the map built from an anim set: each asset of the set maps to the
	 * UHitmenAnimInstance variable resolved from that field (Idle -> IdleAnim, Locomotion ->
	 * LocomotionBlendSpace, FallLoop -> JumpLoopAnim...).
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static int32 BindAssetPinsToAnimSet(UAnimBlueprint* AnimBlueprint, UHitmenAnimSet* AnimSet, bool bCompile = true);

	/**
	 * Montages (fire, mantle, death, hit react...) only show if the AnimGraph has a Slot node.
	 * Inserts a Slot (SlotName; None = "DefaultSlot") right before the Output Pose when the
	 * AnimGraph has no Slot node yet. Returns true if a slot exists afterwards.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static bool EnsureSlotBeforeOutput(UAnimBlueprint* AnimBlueprint, FName SlotName = NAME_None, bool bCompile = true);

	/**
	 * Single-pose graph: Sequence Player (asset pin bound to PoseVariable, preview asset
	 * PreviewPose) -> Slot (DefaultSlot) -> Output Pose. Used by ABP_FirstPersonArms. Leaves an
	 * existing graph alone when it already has a sequence player wired to the variable.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static bool BuildPoseSlotGraph(UAnimBlueprint* AnimBlueprint, UAnimSequenceBase* PreviewPose, FName PoseVariable, bool bCompile = true);
};
