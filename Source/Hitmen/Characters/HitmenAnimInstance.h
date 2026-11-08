// Hitmen — anim instance base: exposes state; ABP_ graphs read animations from the AnimSet

#pragma once

#include "CoreMinimal.h"
#include "Animation/AnimInstance.h"
#include "Core/HitmenTypes.h"
#include "HitmenAnimInstance.generated.h"

class AHitmenCharacterBase;
class UHitmenCharacterMovementComponent;
class UHitmenAnimSet;

UCLASS()
class HITMEN_API UHitmenAnimInstance : public UAnimInstance
{
	GENERATED_BODY()

public:
	virtual void NativeInitializeAnimation() override;
	virtual void NativeUpdateAnimation(float DeltaSeconds) override;

protected:
	/** Animation set of the owner (override per ABP for previews). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Anim")
	TObjectPtr<UHitmenAnimSet> AnimSet;

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
