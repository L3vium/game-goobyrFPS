// Hitmen — ability base

#include "Abilities/HitmenAbility.h"
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

void UHitmenAbility::PlayActivatedJuice(const FVector& Location) const
{
	if (OwnerComponent)
	{
		OwnerComponent->Multicast_AbilityActivated(const_cast<UHitmenAbility*>(this), Location);
	}
}
