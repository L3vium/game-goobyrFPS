// Hitmen — weapon families

#include "Weapons/HitmenWeaponTypes.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Weapons/HitmenProjectile.h"
#include "Data/HitmenWeaponData.h"
#include "Core/HitmenTeamLibrary.h"
#include "Hitmen.h"
#include "Kismet/GameplayStatics.h"
#include "Engine/World.h"
#include "GameFramework/Pawn.h"

// ---------------------------------------------------------------------------------------------
// Hitscan
// ---------------------------------------------------------------------------------------------

void AHitmenWeapon_Hitscan::TracePellets(const FVector& Origin, const FVector& Direction, int32 Seed, TArray<FHitResult>& OutHits, TArray<FVector>& OutImpacts) const
{
	if (!WeaponData)
	{
		return;
	}
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenHitscan), true, OwnerPawn);
	Params.AddIgnoredActor(this);
	Params.bReturnPhysicalMaterial = true;

	const int32 Pellets = FMath::Max(1, WeaponData->PelletCount);
	for (int32 Index = 0; Index < Pellets; ++Index)
	{
		const FVector Dir = Pellets > 1 ? UHitmenCombatLibrary::RandomConeDirection(Direction, PelletSpread, Seed, Index + 1) : Direction;
		const FVector End = Origin + Dir * WeaponData->Range;
		FHitResult Hit;
		if (GetWorld()->LineTraceSingleByChannel(Hit, Origin, End, HITMEN_TRACE_WEAPON, Params))
		{
			OutHits.Add(Hit);
			OutImpacts.Add(Hit.ImpactPoint);
		}
		else
		{
			OutImpacts.Add(End);
		}
	}
}

void AHitmenWeapon_Hitscan::SimulateImpacts(const FVector& Origin, const FVector& Direction, int32 Seed, TArray<FVector>& OutImpacts) const
{
	TArray<FHitResult> Hits;
	TracePellets(Origin, Direction, Seed, Hits, OutImpacts);
}

void AHitmenWeapon_Hitscan::FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts)
{
	TArray<FHitResult> Hits;
	TracePellets(Origin, Direction, Seed, Hits, OutImpacts);

	AController* InstigatorController = OwnerPawn ? OwnerPawn->GetController() : nullptr;
	for (const FHitResult& Hit : Hits)
	{
		AActor* HitActor = Hit.GetActor();
		if (!HitActor)
		{
			continue;
		}
		const float Distance = FVector::Dist(Origin, Hit.ImpactPoint);
		const float FalloffAlpha = FMath::Clamp((Distance / WeaponData->Range - 0.5f) * 2.f, 0.f, 1.f);
		float Damage = WeaponData->Damage * FMath::Lerp(1.f, WeaponData->FalloffAtMaxRange, FalloffAlpha);
		if (WeaponData->HeadBones.Contains(Hit.BoneName))
		{
			Damage *= WeaponData->HeadshotMultiplier;
		}
		UGameplayStatics::ApplyPointDamage(HitActor, Damage, Direction, Hit, InstigatorController, this, UDamageType::StaticClass());
	}
}

// ---------------------------------------------------------------------------------------------
// Projectile
// ---------------------------------------------------------------------------------------------

void AHitmenWeapon_Projectile::FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts)
{
	if (!WeaponData || !WeaponData->ProjectileClass)
	{
		UE_LOG(LogHitmen, Warning, TEXT("%s has no projectile class."), *GetNameSafe(WeaponData));
		return;
	}
	// Spawn slightly in front of the eyes so it never hits the shooter.
	const FVector SpawnLocation = Origin + Direction * 60.f;
	FActorSpawnParameters Params;
	Params.Owner = this;
	Params.Instigator = OwnerPawn;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	if (AHitmenProjectile* Projectile = GetWorld()->SpawnActor<AHitmenProjectile>(WeaponData->ProjectileClass, SpawnLocation, Direction.Rotation(), Params))
	{
		const float SpeedScale = WeaponData->bChargeToFire ? FMath::Lerp(MinChargeSpeed, 1.f, Charge) : 1.f;
		const float DamageScale = WeaponData->bChargeToFire ? FMath::Lerp(0.3f, 1.f, Charge) : 1.f;
		Projectile->Launch(Direction * WeaponData->ProjectileSpeed * SpeedScale, WeaponData->Damage * DamageScale);
	}
}

// ---------------------------------------------------------------------------------------------
// Melee
// ---------------------------------------------------------------------------------------------

void AHitmenWeapon_Melee::FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts)
{
	if (!WeaponData || !OwnerPawn)
	{
		return;
	}
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenMelee), false, OwnerPawn);
	Params.AddIgnoredActor(this);
	TArray<FHitResult> Hits;
	const FVector End = Origin + Direction * WeaponData->MeleeRange;
	GetWorld()->SweepMultiByChannel(Hits, Origin, End, FQuat::Identity, HITMEN_TRACE_WEAPON, FCollisionShape::MakeSphere(WeaponData->MeleeRadius), Params);

	TSet<AActor*> Damaged;
	for (const FHitResult& Hit : Hits)
	{
		AActor* HitActor = Hit.GetActor();
		if (!HitActor || Damaged.Contains(HitActor) || !UHitmenTeamLibrary::AreHostile(OwnerPawn, HitActor))
		{
			continue;
		}
		Damaged.Add(HitActor);
		UGameplayStatics::ApplyPointDamage(HitActor, WeaponData->Damage, Direction, Hit, OwnerPawn->GetController(), this, UDamageType::StaticClass());
		OutImpacts.Add(Hit.ImpactPoint);
	}
}
