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
	Character = Cast<AHitmenCharacterBase>(TryGetPawnOwner());
	Movement = Character ? Character->GetHitmenMovement() : nullptr;
	if (Character && !AnimSet)
	{
		AnimSet = Character->GetAnimSet();
	}
}

void UHitmenAnimInstance::NativeUpdateAnimation(float DeltaSeconds)
{
	Super::NativeUpdateAnimation(DeltaSeconds);
	if (!Character || !Movement)
	{
		return;
	}
	const FVector Velocity = Character->GetVelocity();
	Speed = Velocity.Size2D();
	Direction = UKismetAnimationLibrary::CalculateDirection(Velocity, Character->GetActorRotation());
	AimPitch = FRotator::NormalizeAxis(Character->GetBaseAimRotation().Pitch);
	bIsFalling = Movement->IsFalling();
	bIsCrouching = Movement->IsCrouching();
	bIsSprinting = Movement->IsSprinting();
	bIsSliding = Movement->IsSliding();
	bIsMantling = Movement->IsMantling();
	bIsClimbing = Movement->IsClimbing();
	const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(Movement->GetClimbTarget());
	bIsZiplining = bIsClimbing && Climbable && Climbable->GetClimbType() == EHitmenClimbType::Zipline;
	ClimbAxis = Movement->GetClimbAxisInput();
	bIsAiming = Character->IsAiming();
	bIsDead = Character->IsDead();

	const UHitmenInventoryComponent* Inventory = Character->FindComponentByClass<UHitmenInventoryComponent>();
	bHasWeapon = Inventory ? Inventory->GetActiveWeapon() != nullptr : false;
	if (const AHitmenNPC* NPC = Cast<AHitmenNPC>(Character))
	{
		bHasWeapon = NPC->GetWeapon() != nullptr;
		AlertState = NPC->GetAlert() ? NPC->GetAlert()->GetAlertState() : EHitmenAlertState::Unaware;
	}
	const UHitmenBodyCarryComponent* Carry = Character->FindComponentByClass<UHitmenBodyCarryComponent>();
	bIsCarryingBody = Carry && Carry->IsCarrying();
	bIsStunned = Character->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Stunned);
	bIsAsleep = Character->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Asleep);
}
