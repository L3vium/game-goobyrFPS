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
#include "Data/HitmenAnimSet.h"
#include "Animation/AnimSequenceBase.h"
#include "Animation/BlendSpace.h"
#include "Animation/AimOffsetBlendSpace.h"
#include "KismetAnimationLibrary.h"

void UHitmenAnimInstance::NativeInitializeAnimation()
{
	Super::NativeInitializeAnimation();
	HitmenCharacter = Cast<AHitmenCharacterBase>(TryGetPawnOwner());
	HitmenMovement = HitmenCharacter ? HitmenCharacter->GetHitmenMovement() : nullptr;
	// The character's set wins; the ABP default only serves previews and characters without one.
	if (HitmenCharacter && HitmenCharacter->GetAnimSet())
	{
		AnimSet = HitmenCharacter->GetAnimSet();
	}
	ResolveAnimSet();
}

void UHitmenAnimInstance::ResolveAnimSet()
{
	if (!AnimSet)
	{
		return;
	}
	LocomotionBlendSpace = AnimSet->Locomotion;
	IdleAnim = AnimSet->Idle;
	JumpStartAnim = AnimSet->JumpStart;
	JumpLoopAnim = AnimSet->FallLoop ? AnimSet->FallLoop.Get() : AnimSet->JumpStart.Get();
	LandAnim = AnimSet->Land;
	// Optional entries fall back so a bound graph node never plays nothing (reference pose).
	CrouchIdleAnim = AnimSet->CrouchIdle ? AnimSet->CrouchIdle.Get() : IdleAnim.Get();
	CrouchLocomotionBlendSpace = AnimSet->CrouchLocomotion ? AnimSet->CrouchLocomotion.Get() : LocomotionBlendSpace.Get();
	SlideAnim = AnimSet->Slide ? AnimSet->Slide.Get() : CrouchIdleAnim.Get();
	LadderClimbAnim = AnimSet->LadderClimb ? AnimSet->LadderClimb.Get() : IdleAnim.Get();
	ZiplineAnim = AnimSet->ZiplineHang ? AnimSet->ZiplineHang.Get() : LadderClimbAnim.Get();
	StunnedLoopAnim = AnimSet->StunnedLoop ? AnimSet->StunnedLoop.Get() : IdleAnim.Get();
	AsleepLoopAnim = AnimSet->SleepLoop ? AnimSet->SleepLoop.Get() : StunnedLoopAnim.Get();
	CarryBodyAnim = AnimSet->CarryBodyPose ? AnimSet->CarryBodyPose.Get() : IdleAnim.Get();
	AimOffsetBlendSpace = AnimSet->AimOffset;
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
	UpdateProceduralLayer(DeltaSeconds);
}

void UHitmenAnimInstance::UpdateProceduralLayer(float DeltaSeconds)
{
	// Crouch / slide pose (everyone sees it, your own shadow too).
	const bool bProcedural = bProceduralCrouch && !(AnimSet && AnimSet->CrouchIdle) && !bIsDead;
	CrouchBlend = FMath::FInterpTo(CrouchBlend, bProcedural && (bIsCrouching || bIsSliding) ? 1.f : 0.f, DeltaSeconds, PoseBlendSpeed);
	SlideBlend = FMath::FInterpTo(SlideBlend, bProcedural && bIsSliding ? 1.f : 0.f, DeltaSeconds, PoseBlendSpeed);

	// Ledge climbs: the hands reach the ledge top, push while the body rises, let go; the legs tuck mid-climb.
	if (bIsMantling && !bWasMantling)
	{
		MantleElapsed = 0.f;
		bLedgeValid = HitmenCharacter->FindLedgeInFront(LedgePoint, LedgeNormal);
	}
	bWasMantling = bIsMantling;
	float Tuck = 0.f;
	float Reach = 0.f;
	if (bIsMantling)
	{
		MantleElapsed += DeltaSeconds;
		const float Owned = HitmenMovement->GetMantleAlpha();
		const float Alpha = Owned > 0.f ? Owned : FMath::Clamp(MantleElapsed / 0.5f, 0.f, 1.f);
		Tuck = FMath::Sin(Alpha * PI) * MantleTuck;
		Reach = bLedgeValid ? FMath::SmoothStep(0.f, 0.15f, Alpha) * (1.f - FMath::SmoothStep(0.7f, 0.95f, Alpha)) : 0.f;
	}

	BodyIK.CrouchAlpha = FMath::Max(CrouchBlend, Tuck);
	BodyIK.PelvisDrop = FMath::Lerp(CrouchPelvisDrop, SlidePelvisDrop, SlideBlend);
	BodyIK.SpineLean = FMath::Lerp(CrouchSpineLean, SlideSpineLean, SlideBlend);
	BodyIK.FeetForward = SlideFeetForward * SlideBlend;
	const FVector Along = FVector::CrossProduct(FVector::UpVector, LedgeNormal).GetSafeNormal();
	for (int32 Side = 0; Side < 2; ++Side)
	{
		const float Sign = Side == 0 ? -1.f : 1.f;
		BodyIK.HandAlpha[Side] = Reach;
		BodyIK.HandTarget[Side] = LedgePoint - LedgeNormal * 8.f + Along * (LedgeHandSpacing * Sign) + FVector::UpVector * 3.f;
		BodyIK.ElbowPole[Side] = LedgeNormal * 40.f + Along * (30.f * Sign) - FVector::UpVector * 20.f;
	}
	BodyIK.FistAlpha = 0.f;
}
