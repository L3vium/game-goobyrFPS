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
	if (FVector::DotProduct(Victim->GetActorForwardVector(), VictimToAttacker.GetSafeNormal2D()) > StealthKillBehindDot)
	{
		return false;
	}
	// The attacker must face the victim.
	if (FVector::DotProduct(Owner->GetActorForwardVector(), -VictimToAttacker.GetSafeNormal2D()) < 0.4f)
	{
		return false;
	}
	return Victim->IsUnawareOf(Owner);
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

void UHitmenMeleeComponent::Server_StealthKill_Implementation(AHitmenCharacterBase* Victim)
{
	if (!IsValidStealthKillVictim(Victim))
	{
		return;
	}
	Multicast_PlayStealthKill(Victim);
	Victim->GetHealth()->Kill(GetOwner(), GetOwner(), false);
}

void UHitmenMeleeComponent::Multicast_PlayStealthKill_Implementation(AHitmenCharacterBase* Victim)
{
	const AHitmenCharacterBase* Attacker = Cast<AHitmenCharacterBase>(GetOwner());
	const UHitmenAnimSet* AnimSet = Attacker ? Attacker->GetAnimSet() : nullptr;
	if (AnimSet && AnimSet->StealthKillAttackerMontage && Attacker->GetMesh()->GetAnimInstance())
	{
		Attacker->GetMesh()->GetAnimInstance()->Montage_Play(AnimSet->StealthKillAttackerMontage);
	}
	const UHitmenAnimSet* VictimSet = Victim ? Victim->GetAnimSet() : nullptr;
	if (VictimSet && VictimSet->StealthKillVictimMontage && Victim->GetMesh()->GetAnimInstance())
	{
		Victim->GetMesh()->GetAnimInstance()->Montage_Play(VictimSet->StealthKillVictimMontage);
	}
	Juice_OnStealthKill(Victim);
}
