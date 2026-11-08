// Hitmen — anim instance base: exposes state and the animations of the AnimSet; ABP_ graphs
// read these variables (never hard-coded assets)

#pragma once

#include "CoreMinimal.h"
#include "Animation/AnimInstance.h"
#include "Core/HitmenTypes.h"
#include "HitmenAnimInstance.generated.h"

class AHitmenCharacterBase;
class UHitmenCharacterMovementComponent;
class UHitmenAnimSet;
class UAnimSequenceBase;
class UBlendSpace;
class UAimOffsetBlendSpace;

UCLASS()
class HITMEN_API UHitmenAnimInstance : public UAnimInstance
{
	GENERATED_BODY()

public:
	virtual void NativeInitializeAnimation() override;
	virtual void NativeUpdateAnimation(float DeltaSeconds) override;

	/** Re-reads the animation variables from AnimSet (call after swapping the set at runtime). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Anim")
	void ResolveAnimSet();

protected:
	/**
	 * Animation set used for previews and as a fallback. At runtime the owning character's
	 * AnimSet wins when it has one.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Anim")
	TObjectPtr<UHitmenAnimSet> AnimSet;

	// ---- Animations resolved from AnimSet (graph nodes bind their asset pins to these) ----
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UBlendSpace> LocomotionBlendSpace;
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> IdleAnim;
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> JumpStartAnim;
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> JumpLoopAnim;
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> LandAnim;
	/** Falls back to IdleAnim. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> CrouchIdleAnim;
	/** Falls back to LocomotionBlendSpace. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UBlendSpace> CrouchLocomotionBlendSpace;
	/** Falls back to CrouchIdleAnim. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> SlideAnim;
	/** Falls back to IdleAnim. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> LadderClimbAnim;
	/** Falls back to LadderClimbAnim. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> ZiplineAnim;
	/** Falls back to IdleAnim. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> StunnedLoopAnim;
	/** Falls back to StunnedLoopAnim. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> AsleepLoopAnim;
	/** Falls back to IdleAnim. */
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAnimSequenceBase> CarryBodyAnim;
	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim|Assets") TObjectPtr<UAimOffsetBlendSpace> AimOffsetBlendSpace;

	// ---- State ----
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") float Speed = 0.f;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") float Direction = 0.f;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") float AimPitch = 0.f;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsFalling = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsCrouching = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsSprinting = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsSliding = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsClimbing = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsZiplining = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") float ClimbAxis = 0.f;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsMantling = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsAiming = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsDead = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bHasWeapon = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsCarryingBody = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsStunned = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") bool bIsAsleep = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Anim") EHitmenAlertState AlertState = EHitmenAlertState::Unaware;

	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim")
	TObjectPtr<AHitmenCharacterBase> HitmenCharacter;

	UPROPERTY(Transient)
	TObjectPtr<UHitmenCharacterMovementComponent> HitmenMovement;
};
