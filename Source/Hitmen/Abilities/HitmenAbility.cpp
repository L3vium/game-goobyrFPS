// Hitmen — ability base

#include "Abilities/HitmenAbility.h"
#include "TimerManager.h"
#include "NiagaraComponent.h"
#include "NiagaraFunctionLibrary.h"
#include "EngineUtils.h"
#include "Abilities/HitmenAbilityComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Hitmen.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "GameFramework/GameStateBase.h"

UWorld* UHitmenAbility::GetWorld() const
{
	if (HasAnyFlags(RF_ClassDefaultObject))
	{
		return nullptr;
	}
	const UObject* Outer = GetOuter();
	return Outer ? Outer->GetWorld() : nullptr;
}

void UHitmenAbility::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAbility, Charges);
	DOREPLIFETIME(UHitmenAbility, CooldownEndTime);
	DOREPLIFETIME(UHitmenAbility, Data);
}

void UHitmenAbility::InitAbility(UHitmenAbilityComponent* InComponent, UHitmenAbilityData* InData)
{
	OwnerComponent = InComponent;
	Data = InData;
	Charges = MaxCharges;
	CooldownEndTime = 0.f;
}

float UHitmenAbility::GetServerTime() const
{
	const UWorld* World = GetWorld();
	const AGameStateBase* GS = World ? World->GetGameState() : nullptr;
	return GS ? GS->GetServerWorldTimeSeconds() : (World ? World->GetTimeSeconds() : 0.f);
}

bool UHitmenAbility::CanActivate() const
{
	const AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character || Character->IsDead())
	{
		return false;
	}
	if (MaxCharges > 0 && Charges <= 0)
	{
		return false;
	}
	return GetCooldownRemaining() <= 0.f;
}

float UHitmenAbility::GetCooldownRemaining() const
{
	return FMath::Max(0.f, CooldownEndTime - GetServerTime());
}

float UHitmenAbility::GetCooldownNormalized() const
{
	return Cooldown > 0.f ? FMath::Clamp(GetCooldownRemaining() / Cooldown, 0.f, 1.f) : 0.f;
}

void UHitmenAbility::CommitUse()
{
	if (MaxCharges > 0)
	{
		Charges = FMath::Max(0, Charges - 1);
	}
	CooldownEndTime = GetServerTime() + Cooldown;
}

void UHitmenAbility::ServerPress(const FVector& AimOrigin, const FVector& AimDirection)
{
	if (bActivateOnRelease || !CanActivate())
	{
		return;
	}
	if (Activate(AimOrigin, AimDirection))
	{
		CommitUse();
	}
}

void UHitmenAbility::ServerRelease(const FVector& AimOrigin, const FVector& AimDirection)
{
	if (bActivateOnRelease)
	{
		if (CanActivate() && Activate(AimOrigin, AimDirection))
		{
			CommitUse();
		}
		return;
	}
	Release(AimOrigin, AimDirection);
}

AHitmenCharacterBase* UHitmenAbility::GetOwnerCharacter() const
{
	return OwnerComponent ? Cast<AHitmenCharacterBase>(OwnerComponent->GetOwner()) : nullptr;
}

APawn* UHitmenAbility::GetOwnerPawn() const
{
	return OwnerComponent ? Cast<APawn>(OwnerComponent->GetOwner()) : nullptr;
}

bool UHitmenAbility::AimTrace(const FVector& Origin, const FVector& Direction, FHitResult& OutHit, float TraceRadius) const
{
	const UWorld* World = GetWorld();
	if (!World)
	{
		return false;
	}
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenAbilityAim), false, GetOwnerPawn());
	const FVector End = Origin + Direction.GetSafeNormal() * Range;
	if (TraceRadius > 0.f)
	{
		return World->SweepSingleByChannel(OutHit, Origin, End, FQuat::Identity, HITMEN_TRACE_WEAPON, FCollisionShape::MakeSphere(TraceRadius), Params);
	}
	return World->LineTraceSingleByChannel(OutHit, Origin, End, HITMEN_TRACE_WEAPON, Params);
}

AHitmenCharacterBase* UHitmenAbility::AimCharacter(const FVector& Origin, const FVector& Direction, bool bAllowDead) const
{
	FHitResult Hit;
	if (!AimTrace(Origin, Direction, Hit, 25.f))
	{
		return nullptr;
	}
	AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Hit.GetActor());
	if (!Character || Character == GetOwnerCharacter() || (!bAllowDead && Character->IsDead()))
	{
		return nullptr;
	}
	return Character;
}

AActor* UHitmenAbility::SpawnOwnedActor(TSubclassOf<AActor> Class, const FTransform& Transform) const
{
	UWorld* World = GetWorld();
	if (!World || !Class)
	{
		return nullptr;
	}
	FActorSpawnParameters Params;
	Params.Owner = GetOwnerPawn();
	Params.Instigator = GetOwnerPawn();
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	return World->SpawnActor<AActor>(Class, Transform, Params);
}

void UHitmenAbility::NotifyFailed(const FText& Reason) const
{
	if (OwnerComponent)
	{
		OwnerComponent->Client_AbilityFailed(const_cast<UHitmenAbility*>(this), Reason);
	}
}

void UHitmenAbility::PlayActivatedJuice(const FVector& Location) const
{
	if (OwnerComponent)
	{
		OwnerComponent->Multicast_AbilityActivated(const_cast<UHitmenAbility*>(this), Location);
	}
}

// ---------------------------------------------------------------------------------------------
// Cone targeting (remote abilities): forgiving at range, still needs a line of sight
// ---------------------------------------------------------------------------------------------

bool UHitmenAbility::HasLineOfSightTo(const FVector& Origin, const AActor* Target) const
{
	const UWorld* World = GetWorld();
	if (!World || !Target)
	{
		return false;
	}
	FVector Center, Extent;
	Target->GetActorBounds(true, Center, Extent);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenAbilitySight), false, GetOwnerPawn());
	FHitResult Hit;
	if (!World->LineTraceSingleByChannel(Hit, Origin, Center, ECC_Visibility, Params))
	{
		return true;
	}
	const AActor* HitActor = Hit.GetActor();
	return HitActor == Target || (HitActor && HitActor->IsAttachedTo(Target)) || Hit.Distance >= FVector::Dist(Origin, Center) - Extent.GetMax() * 0.5f;
}

AActor* UHitmenAbility::PickConeTarget(const FVector& Origin, const FVector& Direction, const TArray<AActor*>& Candidates, float ConeAngle) const
{
	const FVector Aim = Direction.GetSafeNormal();
	const float MinDot = FMath::Cos(FMath::DegreesToRadians(ConeAngle));
	AActor* Best = nullptr;
	float BestScore = -1.f;
	for (AActor* Candidate : Candidates)
	{
		if (!Candidate)
		{
			continue;
		}
		FVector Center, Extent;
		Candidate->GetActorBounds(true, Center, Extent);
		const FVector To = Center - Origin;
		const float Distance = To.Size();
		if (Distance > Range + Extent.GetMax() || Distance < KINDA_SMALL_NUMBER)
		{
			continue;
		}
		const float Dot = FVector::DotProduct(To / Distance, Aim);
		// A big actor counts as soon as its bounds touch the cone (angle widened by its apparent size).
		const float SizeAngle = FMath::Atan2(Extent.GetMax() * 0.6f, Distance);
		if (FMath::Acos(FMath::Clamp(Dot, -1.f, 1.f)) - SizeAngle > FMath::DegreesToRadians(ConeAngle) && Dot < MinDot)
		{
			continue;
		}
		// Closest to the crosshair first, nearer breaks ties.
		const float Score = Dot - Distance * 0.00001f;
		if (Score > BestScore && HasLineOfSightTo(Origin, Candidate))
		{
			BestScore = Score;
			Best = Candidate;
		}
	}
	return Best;
}

AHitmenCharacterBase* UHitmenAbility::AimCharacterInCone(const FVector& Origin, const FVector& Direction, bool bAllowDead, bool bOnlyDead) const
{
	const UWorld* World = GetWorld();
	const AHitmenCharacterBase* Self = GetOwnerCharacter();
	if (!World)
	{
		return nullptr;
	}
	TArray<AActor*> Candidates;
	for (TActorIterator<AHitmenCharacterBase> It(World); It; ++It)
	{
		AHitmenCharacterBase* Character = *It;
		const bool bDead = Character->IsDead();
		if (Character == Self || (bDead && (!bAllowDead || !Character->IsDiscoverableBody())) || (bOnlyDead && !bDead))
		{
			continue;
		}
		Candidates.Add(Character);
	}
	return Cast<AHitmenCharacterBase>(PickConeTarget(Origin, Direction, Candidates, TargetConeAngle));
}

void UHitmenAbility::PlayActivatedFX(const FVector& Location) const
{
	UWorld* World = GetWorld();
	if (!ActivatedFX || !World || World->GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	UNiagaraComponent* Effect = UNiagaraFunctionLibrary::SpawnSystemAtLocation(World, ActivatedFX, Location, FRotator::ZeroRotator, FVector(ActivatedFXScale), true);
	if (Effect && ActivatedFXDuration > 0.f)
	{
		// Looping effects (fire) stop after their duration.
		TWeakObjectPtr<UNiagaraComponent> Weak(Effect);
		FTimerHandle Stop;
		World->GetTimerManager().SetTimer(Stop, FTimerDelegate::CreateLambda([Weak]()
		{
			if (Weak.IsValid())
			{
				Weak->Deactivate();
			}
		}), ActivatedFXDuration, false);
	}
}
