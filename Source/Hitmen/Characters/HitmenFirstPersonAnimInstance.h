// Hitmen — first-person arms: one looping hold pose (weapon or unarmed guard) under a montage slot

#pragma once

#include "CoreMinimal.h"
#include "Animation/AnimInstance.h"
#include "HitmenFirstPersonAnimInstance.generated.h"

class UAnimSequenceBase;

/**
 * ABP_FirstPersonArms graph: Sequence Player (asset bound to HoldPose) -> Slot DefaultSlot ->
 * Output. Fire / equip / punch montages play in the slot; the hold pose comes from the weapon
 * in hand (UHitmenWeaponData::FirstPersonPose) or the anim set's unarmed pose.
 */
UCLASS()
class HITMEN_API UHitmenFirstPersonAnimInstance : public UAnimInstance
{
	GENERATED_BODY()

public:
	virtual void NativeUpdateAnimation(float DeltaSeconds) override;

protected:
	/** Shown when the weapon has no first-person pose of its own (editor preview too). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Anim")
	TObjectPtr<UAnimSequenceBase> DefaultHoldPose;

	/** Pose currently held (graph binds the sequence player to it). */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim")
	TObjectPtr<UAnimSequenceBase> HoldPose;
};
