// Hitmen — anim instance base: exposes state and the animations of the AnimSet; ABP_ graphs
// read these variables (never hard-coded assets)

#pragma once

#include "CoreMinimal.h"
#include "Animation/AnimInstance.h"
#include "Core/HitmenTypes.h"
#include "Characters/HitmenAnimNodes.h"
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

	/** Procedural layer state (FAnimNode_HitmenBodyIK reads it on the game thread). */
	const FHitmenBodyIKState& GetBodyIKState() const { return BodyIK; }

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

	// ---- Procedural layer (no crouch / slide / climb animations in the project: the body is posed in code) ----
	/** Crouch and slide poses from the procedural layer; off by itself when the anim set has crouch animations. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural")
	bool bProceduralCrouch = true;

	/** Crouched: the pelvis drops this much (cm) and the spine leans forward (deg); the feet stay planted. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(ClampMin="0", Units="cm"))
	float CrouchPelvisDrop = 38.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(Units="deg"))
	float CrouchSpineLean = 14.f;

	/** Sliding: lower, leaning back, the feet out in front. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(ClampMin="0", Units="cm"))
	float SlidePelvisDrop = 52.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(Units="deg"))
	float SlideSpineLean = -12.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(Units="cm"))
	float SlideFeetForward = 30.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(ClampMin="1"))
	float PoseBlendSpeed = 12.f;

	/** Climbing a ledge: the hands land this far apart on its top and the legs tuck (crouch alpha at mid-climb). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(ClampMin="0", Units="cm"))
	float LedgeHandSpacing = 22.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Anim|Procedural", meta=(ClampMin="0", ClampMax="1"))
	float MantleTuck = 0.6f;

	FHitmenBodyIKState BodyIK;
	float CrouchBlend = 0.f;
	float SlideBlend = 0.f;
	bool bWasMantling = false;
	float MantleElapsed = 0.f;
	bool bLedgeValid = false;
	FVector LedgePoint = FVector::ZeroVector;
	FVector LedgeNormal = FVector::ZeroVector;

	void UpdateProceduralLayer(float DeltaSeconds);

	UPROPERTY(Transient, BlueprintReadOnly, Category="Hitmen|Anim")
	TObjectPtr<AHitmenCharacterBase> HitmenCharacter;

	UPROPERTY(Transient)
	TObjectPtr<UHitmenCharacterMovementComponent> HitmenMovement;
};
