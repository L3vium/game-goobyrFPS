// Hitmen — melee

#include "Components/HitmenMeleeComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "Components/HitmenHealthComponent.h"
#include "Data/HitmenAnimSet.h"
#include "Core/HitmenTeamLibrary.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "AI/HitmenAISubsystem.h"
#include "Hitmen.h"
#include "Kismet/GameplayStatics.h"
#include "Engine/OverlapResult.h"
#include "Animation/AnimInstance.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/World.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/Controller.h"
#include "Net/UnrealNetwork.h"
#include "TimerManager.h"

UHitmenMeleeComponent::UHitmenMeleeComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenMeleeComponent::Punch()
{
	if (GetWorld()->GetTimeSeconds() - LastPunchTime < PunchCooldown)
	{
		return;
	}
	LastPunchTime = GetWorld()->GetTimeSeconds();
	// Predicted on the puncher: arms show at once, the server confirms the hit.
	PlayPunchLocal();
	Server_Punch();
}

void UHitmenMeleeComponent::PlayPunchLocal()
{
	AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(GetOwner());
	if (!Character || GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	const UHitmenAnimSet* Set = Character->GetAnimSet();
	const float Length = Character->PlayCosmeticMontage(Set ? Set->PunchMontage.Get() : nullptr, PunchPlayRate);
	if (AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(Character); Player && Player->IsLocallyControlled())
	{
		Player->ShowFirstPersonArms(FMath::Max(Length / FMath::Max(PunchPlayRate, 0.1f), PunchCooldown) + 0.2f);
	}
	Juice_OnPunch();
}

void UHitmenMeleeComponent::Server_Punch_Implementation()
{
	APawn* Pawn = Cast<APawn>(GetOwner());
	if (!Pawn)
	{
		return;
	}
	FVector Origin, Direction;
	UHitmenCombatLibrary::GetAimPoint(Pawn, Origin, Direction);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenPunch), false, Pawn);
	FHitResult Hit;
	if (GetWorld()->SweepSingleByChannel(Hit, Origin, Origin + Direction * PunchRange, FQuat::Identity, HITMEN_TRACE_WEAPON, FCollisionShape::MakeSphere(PunchRadius), Params)
		&& UHitmenTeamLibrary::AreHostile(Pawn, Hit.GetActor()))
	{
		UGameplayStatics::ApplyPointDamage(Hit.GetActor(), PunchDamage, Direction, Hit, Pawn->GetController(), Pawn, UDamageType::StaticClass());
	}
	if (PunchNoiseRadius > 0.f)
	{
		UHitmenAISubsystem::ReportNoise(this, Pawn->GetActorLocation(), PunchNoiseRadius, Pawn, EHitmenNoiseType::Combat);
	}
	Multicast_PlayPunch();
}

void UHitmenMeleeComponent::Multicast_PlayPunch_Implementation()
{
	// The puncher already played it (prediction).
	const APawn* Pawn = Cast<APawn>(GetOwner());
	if (Pawn && Pawn->IsLocallyControlled() && Pawn->IsPlayerControlled())
	{
		return;
	}
	PlayPunchLocal();
}

bool UHitmenMeleeComponent::IsValidStealthKillVictim(const AHitmenCharacterBase* Victim) const
{
	const AActor* Owner = GetOwner();
	if (!Victim || Victim == Owner || Victim->IsDead() || !UHitmenTeamLibrary::AreHostile(Owner, Victim))
	{
		return false;
	}
	const FVector VictimToAttacker = Owner->GetActorLocation() - Victim->GetActorLocation();
	if (VictimToAttacker.Size() > StealthKillRange)
	{
		return false;
	}
	// The attacker must face the victim; any side of the victim is fine as long as it is not onto the attacker.
	if (FVector::DotProduct(Owner->GetActorForwardVector(), -VictimToAttacker.GetSafeNormal2D()) < 0.4f)
	{
		return false;
	}
	return Victim->CanBeTakenDownBy(Owner);
}

AHitmenCharacterBase* UHitmenMeleeComponent::FindStealthKillTarget() const
{
	const AActor* Owner = GetOwner();
	TArray<FOverlapResult> Overlaps;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenStealthKill), false, Owner);
	GetWorld()->OverlapMultiByChannel(Overlaps, Owner->GetActorLocation(), FQuat::Identity, ECC_Pawn, FCollisionShape::MakeSphere(StealthKillRange), Params);
	AHitmenCharacterBase* Best = nullptr;
	float BestDistance = TNumericLimits<float>::Max();
	for (const FOverlapResult& Overlap : Overlaps)
	{
		AHitmenCharacterBase* Candidate = Cast<AHitmenCharacterBase>(Overlap.GetActor());
		if (IsValidStealthKillVictim(Candidate))
		{
			const float Distance = FVector::DistSquared(Owner->GetActorLocation(), Candidate->GetActorLocation());
			if (Distance < BestDistance)
			{
				BestDistance = Distance;
				Best = Candidate;
			}
		}
	}
	return Best;
}

bool UHitmenMeleeComponent::TryStealthKill()
{
	if (AHitmenCharacterBase* Victim = FindStealthKillTarget())
	{
		Server_StealthKill(Victim);
		return true;
	}
	return false;
}

void UHitmenMeleeComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenMeleeComponent, bTakedownActive);
}

void UHitmenMeleeComponent::Server_StealthKill_Implementation(AHitmenCharacterBase* Victim)
{
	if (bTakedownActive || !IsValidStealthKillVictim(Victim))
	{
		return;
	}
	ACharacter* Attacker = Cast<ACharacter>(GetOwner());
	if (!Attacker)
	{
		return;
	}
	// A takedown, not an instant kill: both bodies freeze, the attacker steps in behind the
	// victim facing it, the montages play, and the victim dies on the strike.
	bTakedownActive = true;
	// The attacker keeps its approach side and blends to the hold distance, turning to the victim: no snap.
	const FVector Approach = (Victim->GetActorLocation() - Attacker->GetActorLocation()).GetSafeNormal2D();
	const FVector Hold = Victim->GetActorLocation() - Approach * StealthKillHoldDistance;
	BlendVictim = Victim;
	BlendFrom = Attacker->GetActorLocation();
	BlendTo = FVector(Hold.X, Hold.Y, Attacker->GetActorLocation().Z);
	BlendStart = GetWorld()->GetTimeSeconds();
	GetWorld()->GetTimerManager().SetTimer(TakedownBlendTimer, this, &UHitmenMeleeComponent::UpdateTakedownBlend, 1.f / 60.f, true);
	const float TargetYaw = Approach.Rotation().Yaw;
	if (AController* AttackerController = Attacker->GetController())
	{
		AttackerController->SetIgnoreMoveInput(true);
		Client_BlendView(TargetYaw, -8.f, TakedownBlendTime);
	}
	Attacker->GetCharacterMovement()->StopMovementImmediately();
	if (AController* VictimController = Victim->GetController())
	{
		VictimController->StopMovement();
		VictimController->SetIgnoreMoveInput(true);
	}
	Victim->GetCharacterMovement()->StopMovementImmediately();
	Victim->GetCharacterMovement()->DisableMovement();
	Multicast_PlayStealthKill(Victim);

	const TWeakObjectPtr<AHitmenCharacterBase> WeakVictim(Victim);
	GetWorld()->GetTimerManager().SetTimer(TakedownStrikeTimer, FTimerDelegate::CreateUObject(this, &UHitmenMeleeComponent::FinishStealthKill, WeakVictim), FMath::Max(0.01f, StealthKillStrikeTime), false);
	GetWorld()->GetTimerManager().SetTimer(TakedownEndTimer, FTimerDelegate::CreateUObject(this, &UHitmenMeleeComponent::EndTakedown, WeakVictim), FMath::Max(StealthKillStrikeTime + 0.05f, StealthKillDuration), false);
}

void UHitmenMeleeComponent::FinishStealthKill(TWeakObjectPtr<AHitmenCharacterBase> Victim)
{
	if (Victim.IsValid() && !Victim->IsDead())
	{
		Victim->GetHealth()->Kill(GetOwner(), GetOwner(), false);
	}
}

void UHitmenMeleeComponent::EndTakedown(TWeakObjectPtr<AHitmenCharacterBase> Victim)
{
	bTakedownActive = false;
	if (const APawn* Attacker = Cast<APawn>(GetOwner()))
	{
		if (AController* AttackerController = Attacker->GetController())
		{
			AttackerController->SetIgnoreMoveInput(false);
		}
	}
	// A victim that somehow survived gets its legs back.
	if (Victim.IsValid() && !Victim->IsDead())
	{
		Victim->GetCharacterMovement()->SetMovementMode(MOVE_Walking);
		if (AController* VictimController = Victim->GetController())
		{
			VictimController->SetIgnoreMoveInput(false);
		}
	}
}

void UHitmenMeleeComponent::Multicast_PlayStealthKill_Implementation(AHitmenCharacterBase* Victim)
{
	AHitmenCharacterBase* Attacker = Cast<AHitmenCharacterBase>(GetOwner());
	const UHitmenAnimSet* AnimSet = Attacker ? Attacker->GetAnimSet() : nullptr;
	if (Attacker && AnimSet)
	{
		// Body and first-person arms (PlayCosmeticMontage covers both): the owner sees the strike.
		UAnimMontage* AttackMontage = AnimSet->StealthKillAttackerMontage ? AnimSet->StealthKillAttackerMontage.Get() : AnimSet->PunchMontage.Get();
		const float Length = Attacker->PlayCosmeticMontage(AttackMontage, 1.f);
		if (AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(Attacker); Player && Player->IsLocallyControlled())
		{
			Player->ShowFirstPersonArms(FMath::Max(Length, StealthKillDuration) + 0.2f);
			Player->AddCameraShake(0.6f, 0.25f);
		}
	}
	const UHitmenAnimSet* VictimSet = Victim ? Victim->GetAnimSet() : nullptr;
	if (Victim && VictimSet)
	{
		UAnimMontage* VictimMontage = VictimSet->StealthKillVictimMontage ? VictimSet->StealthKillVictimMontage.Get() : VictimSet->HitReactMontage.Get();
		Victim->PlayCosmeticMontage(VictimMontage, 1.f);
	}
	Juice_OnStealthKill(Victim);
}

void UHitmenMeleeComponent::UpdateTakedownBlend()
{
	ACharacter* Attacker = Cast<ACharacter>(GetOwner());
	const float T = TakedownBlendTime > 0.f ? FMath::Clamp((GetWorld()->GetTimeSeconds() - BlendStart) / TakedownBlendTime, 0.f, 1.f) : 1.f;
	if (Attacker)
	{
		const float Ease = FMath::InterpEaseInOut(0.f, 1.f, T, 2.f);
		const FVector Location = FMath::Lerp(BlendFrom, BlendTo, Ease);
		const FVector ToVictim = BlendVictim.IsValid() ? (BlendVictim->GetActorLocation() - Location).GetSafeNormal2D() : Attacker->GetActorForwardVector();
		const FRotator Current = Attacker->GetActorRotation();
		Attacker->SetActorLocationAndRotation(Location, FMath::Lerp(Current, FRotator(0.f, ToVictim.Rotation().Yaw, 0.f), Ease), true);
	}
	if (T >= 1.f)
	{
		GetWorld()->GetTimerManager().ClearTimer(TakedownBlendTimer);
	}
}

void UHitmenMeleeComponent::Client_BlendView_Implementation(float TargetYaw, float TargetPitch, float Duration)
{
	const APawn* Pawn = Cast<APawn>(GetOwner());
	AController* Controller = Pawn ? Pawn->GetController() : nullptr;
	if (!Controller)
	{
		return;
	}
	ViewBlendFrom = Controller->GetControlRotation();
	ViewBlendTo = FRotator(TargetPitch, TargetYaw, 0.f);
	ViewBlendStart = GetWorld()->GetTimeSeconds();
	ViewBlendDuration = Duration;
	GetWorld()->GetTimerManager().SetTimer(ViewBlendTimer, this, &UHitmenMeleeComponent::UpdateViewBlend, 1.f / 120.f, true);
	UpdateViewBlend();
}

void UHitmenMeleeComponent::UpdateViewBlend()
{
	const APawn* Pawn = Cast<APawn>(GetOwner());
	AController* Controller = Pawn ? Pawn->GetController() : nullptr;
	const float T = ViewBlendDuration > 0.f ? FMath::Clamp((GetWorld()->GetTimeSeconds() - ViewBlendStart) / ViewBlendDuration, 0.f, 1.f) : 1.f;
	if (Controller)
	{
		const FQuat Blended = FQuat::Slerp(ViewBlendFrom.Quaternion(), ViewBlendTo.Quaternion(), FMath::InterpEaseInOut(0.f, 1.f, T, 2.f));
		Controller->SetControlRotation(Blended.Rotator());
	}
	if (T >= 1.f)
	{
		GetWorld()->GetTimerManager().ClearTimer(ViewBlendTimer);
	}
}
