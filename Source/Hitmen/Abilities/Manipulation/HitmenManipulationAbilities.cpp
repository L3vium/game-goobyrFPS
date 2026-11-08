// Hitmen — manipulation abilities

#include "Abilities/Manipulation/HitmenManipulationAbilities.h"
#include "Abilities/HitmenAbilityActors.h"
#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenHealthComponent.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Weapons/HitmenProjectile.h"
#include "Core/HitmenInterfaces.h"
#include "World/Interactables/HitmenHazards.h"
#include "World/Interactables/HitmenOpenings.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "TimerManager.h"

// ---------------------------------------------------------------------------------------------
// Throwables
// ---------------------------------------------------------------------------------------------

bool UHitmenAbility_Throwable::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	if (!ProjectileClass)
	{
		return false;
	}
	FVector Direction = AimDirection.GetSafeNormal();
	Direction = (Direction + FVector::UpVector * FMath::Tan(FMath::DegreesToRadians(LobAngle))).GetSafeNormal();
	AHitmenProjectile* Projectile = Cast<AHitmenProjectile>(SpawnOwnedActor(ProjectileClass, FTransform(Direction.Rotation(), AimOrigin + Direction * 60.f)));
	if (!Projectile)
	{
		return false;
	}
	Projectile->Launch(Direction * ThrowSpeed, 0.f);
	PlayActivatedJuice(AimOrigin);
	return true;
}

UHitmenAbility_Stone::UHitmenAbility_Stone()
{
	Cooldown = 2.f;
	MaxCharges = 0;
	ThrowSpeed = 1300.f;
}

UHitmenAbility_SleepDart::UHitmenAbility_SleepDart()
{
	Cooldown = 6.f;
	MaxCharges = 4;
	ThrowSpeed = 6000.f;
	LobAngle = 0.f;
}

UHitmenAbility_Flashbang::UHitmenAbility_Flashbang()
{
	Cooldown = 15.f;
	MaxCharges = 3;
	ProjectileClass = AHitmenProjectile_Flashbang::StaticClass();
}

UHitmenAbility_SmokeGrenade::UHitmenAbility_SmokeGrenade()
{
	Cooldown = 15.f;
	MaxCharges = 3;
	ProjectileClass = AHitmenProjectile_Smoke::StaticClass();
}

// ---------------------------------------------------------------------------------------------
// Remote distraction
// ---------------------------------------------------------------------------------------------

UHitmenAbility_RemoteDistraction::UHitmenAbility_RemoteDistraction()
{
	Cooldown = 5.f;
	Range = 3000.f;
}

bool UHitmenAbility_RemoteDistraction::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	FHitResult Hit;
	if (!AimTrace(AimOrigin, AimDirection, Hit, 30.f))
	{
		return false;
	}
	AActor* Target = Hit.GetActor();
	if (!Target || !Target->GetClass()->ImplementsInterface(UHitmenRemoteActivatable::StaticClass())
		|| !IHitmenRemoteActivatable::Execute_CanRemoteActivate(Target))
	{
		return false;
	}
	IHitmenRemoteActivatable::Execute_RemoteActivate(Target, GetOwnerPawn());
	PlayActivatedJuice(Hit.ImpactPoint);
	return true;
}

// ---------------------------------------------------------------------------------------------
// Charm
// ---------------------------------------------------------------------------------------------

UHitmenAbility_Charm::UHitmenAbility_Charm()
{
	Cooldown = 25.f;
	Duration = 12.f;
	Range = 2500.f;
}

bool UHitmenAbility_Charm::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Target = AimCharacter(AimOrigin, AimDirection);
	if (!Target)
	{
		return false;
	}
	Target->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Charmed, Duration, 1.f, GetOwnerPawn());
	PlayActivatedJuice(Target->GetActorLocation());
	return true;
}

// ---------------------------------------------------------------------------------------------
// Lighter
// ---------------------------------------------------------------------------------------------

UHitmenAbility_Lighter::UHitmenAbility_Lighter()
{
	Cooldown = 3.f;
	Range = 300.f;
}

bool UHitmenAbility_Lighter::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Body = AimCharacter(AimOrigin, AimDirection, true);
	if (!Body || !Body->IsDiscoverableBody() || Body->IsVIP())
	{
		return false;
	}
	PlayActivatedJuice(Body->GetActorLocation());
	Body->VanishBody();
	return true;
}

// ---------------------------------------------------------------------------------------------
// Multi-kill
// ---------------------------------------------------------------------------------------------

UHitmenAbility_MultiKill::UHitmenAbility_MultiKill()
{
	Cooldown = 2.f;
	Duration = 30.f;
	Range = 3000.f;
}

void UHitmenAbility_MultiKill::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAbility_MultiKill, MarkedTargets);
}

bool UHitmenAbility_MultiKill::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Target = AimCharacter(AimOrigin, AimDirection);
	if (!Target || MarkedTargets.Contains(Target) || MarkedTargets.Num() >= MaxMarks)
	{
		return false;
	}
	MarkedTargets.Add(Target);
	Target->GetHealth()->OnDeath.AddDynamic(this, &UHitmenAbility_MultiKill::HandleMarkedDeath);
	GetWorld()->GetTimerManager().SetTimer(ExpireTimer, FTimerDelegate::CreateUObject(this, &UHitmenAbility_MultiKill::ClearMarks), Duration, false);
	PlayActivatedJuice(Target->GetActorLocation());
	return true;
}

void UHitmenAbility_MultiKill::HandleMarkedDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot)
{
	if (bResolving)
	{
		return;
	}
	bResolving = true;
	APawn* OwnerPawn = GetOwnerPawn();
	const TArray<AHitmenCharacterBase*> Targets = MarkedTargets;
	for (AHitmenCharacterBase* Target : Targets)
	{
		if (Target && !Target->IsDead())
		{
			Target->GetHealth()->Kill(OwnerPawn, OwnerPawn, false);
		}
	}
	bResolving = false;
	ClearMarks();
	CommitUse();
}

void UHitmenAbility_MultiKill::ClearMarks()
{
	for (AHitmenCharacterBase* Target : MarkedTargets)
	{
		if (Target)
		{
			Target->GetHealth()->OnDeath.RemoveDynamic(this, &UHitmenAbility_MultiKill::HandleMarkedDeath);
		}
	}
	MarkedTargets.Reset();
	Juice_OnEnded();
}

// ---------------------------------------------------------------------------------------------
// Devices
// ---------------------------------------------------------------------------------------------

bool UHitmenAbility_PlaceDevice::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	const AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character || !DeviceClass)
	{
		return false;
	}
	FHitResult Hit;
	FVector Location = Character->GetActorLocation() + Character->GetActorForwardVector() * 120.f;
	if (AimTrace(AimOrigin, AimDirection, Hit) && FVector::Dist(Hit.ImpactPoint, Character->GetActorLocation()) < Range)
	{
		Location = Hit.ImpactPoint;
	}
	// Snap to the ground.
	FHitResult Ground;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenDevice), false, Character);
	if (GetWorld()->LineTraceSingleByChannel(Ground, Location + FVector::UpVector * 100.f, Location - FVector::UpVector * 500.f, ECC_Visibility, Params))
	{
		Location = Ground.ImpactPoint;
	}
	if (!SpawnOwnedActor(DeviceClass, FTransform(Character->GetActorRotation(), Location)))
	{
		return false;
	}
	PlayActivatedJuice(Location);
	return true;
}

UHitmenAbility_Trap::UHitmenAbility_Trap()
{
	Cooldown = 10.f;
	MaxCharges = 3;
	Range = 600.f;
	DeviceClass = AHitmenTrap::StaticClass();
}

UHitmenAbility_WindMine::UHitmenAbility_WindMine()
{
	Cooldown = 12.f;
	MaxCharges = 3;
	Range = 600.f;
	DeviceClass = AHitmenWindMine::StaticClass();
}

// ---------------------------------------------------------------------------------------------
// Poison
// ---------------------------------------------------------------------------------------------

UHitmenAbility_PoisonVial::UHitmenAbility_PoisonVial()
{
	Cooldown = 5.f;
	MaxCharges = 2;
	Range = 250.f;
	RoomNotSealedText = NSLOCTEXT("Hitmen", "RoomNotSealed", "Room not sealed");
}

namespace
{
	/** Rooms served by a poisonable vent; returns false for targets without rooms (drinks). */
	bool GetServedRooms(const AActor* Target, TArray<AHitmenRoomVolume*>& OutRooms)
	{
		const AHitmenVentHatch* Vent = Cast<AHitmenVentHatch>(Target);
		if (!Vent)
		{
			return false;
		}
		for (AHitmenRoomVolume* Room : Vent->GetLinkedRooms())
		{
			if (Room)
			{
				OutRooms.Add(Room);
			}
		}
		return true;
	}
}

bool UHitmenAbility_PoisonVial::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	FHitResult Hit;
	if (!AimTrace(AimOrigin, AimDirection, Hit, 20.f))
	{
		return false;
	}
	AActor* Target = Hit.GetActor();
	if (!Target || !Target->GetClass()->ImplementsInterface(UHitmenPoisonable::StaticClass()) || !IHitmenPoisonable::Execute_CanBePoisoned(Target))
	{
		return false;
	}
	// Vents only gas sealed rooms: check before the charge is consumed.
	TArray<AHitmenRoomVolume*> Rooms;
	if (GetServedRooms(Target, Rooms) && !Rooms.ContainsByPredicate([](const AHitmenRoomVolume* Room) { return Room->IsSealed(); }))
	{
		NotifyFailed(RoomNotSealedText);
		return false;
	}
	IHitmenPoisonable::Execute_ApplyPoison(Target, GetOwnerPawn());
	PlayActivatedJuice(Hit.ImpactPoint);
	return true;
}
