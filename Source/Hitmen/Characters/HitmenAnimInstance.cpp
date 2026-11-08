// Hitmen — anim instance

#include "Characters/HitmenAnimInstance.h"
#include "Characters/HitmenCharacterBase.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "AI/HitmenAlertComponent.h"
#include "Core/HitmenInterfaces.h"
#include "Characters/HitmenNPC.h"
#include "KismetAnimationLibrary.h"

void UHitmenAnimInstance::NativeInitializeAnimation()
{
	Super::NativeInitializeAnimation();
	HitmenCharacter = Cast<AHitmenCharacterBase>(TryGetPawnOwner());
	HitmenMovement = HitmenCharacter ? HitmenCharacter->GetHitmenMovement() : nullptr;
	if (HitmenCharacter && !AnimSet)
	{
		AnimSet = HitmenCharacter->GetAnimSet();
	}
}

void UHitmenAnimInstance::NativeUpdateAnimation(float DeltaSeconds)
{
	Super::NativeUpdateAnimation(DeltaSeconds);
	if (!HitmenCharacter || !HitmenMovement)
	{
		return;
	}
	const FVector Velocity = HitmenCharacter->GetVelocity();
	Speed = Velocity.Size2D();
	Direction = UKismetAnimationLibrary::CalculateDirection(Velocity, HitmenCharacter->GetActorRotation());
	AimPitch = FRotator::NormalizeAxis(HitmenCharacter->GetBaseAimRotation().Pitch);
	bIsFalling = HitmenMovement->IsFalling();
	bIsCrouching = HitmenMovement->IsCrouching();
	bIsSprinting = HitmenMovement->IsSprinting();
	bIsSliding = HitmenMovement->IsSliding();
	bIsMantling = HitmenMovement->IsMantling();
	bIsClimbing = HitmenMovement->IsClimbing();
	const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(HitmenMovement->GetClimbTarget());
	bIsZiplining = bIsClimbing && Climbable && Climbable->GetClimbType() == EHitmenClimbType::Zipline;
	ClimbAxis = HitmenMovement->GetClimbAxisInput();
	bIsAiming = HitmenCharacter->IsAiming();
	bIsDead = HitmenCharacter->IsDead();

	const UHitmenInventoryComponent* Inventory = HitmenCharacter->FindComponentByClass<UHitmenInventoryComponent>();
	bHasWeapon = Inventory ? Inventory->GetActiveWeapon() != nullptr : false;
	if (const AHitmenNPC* NPC = Cast<AHitmenNPC>(HitmenCharacter))
	{
		bHasWeapon = NPC->GetWeapon() != nullptr;
		AlertState = NPC->GetAlert() ? NPC->GetAlert()->GetAlertState() : EHitmenAlertState::Unaware;
	}
	const UHitmenBodyCarryComponent* Carry = HitmenCharacter->FindComponentByClass<UHitmenBodyCarryComponent>();
	bIsCarryingBody = Carry && Carry->IsCarrying();
	bIsStunned = HitmenCharacter->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Stunned);
	bIsAsleep = HitmenCharacter->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Asleep);
}
