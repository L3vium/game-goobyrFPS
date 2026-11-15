// Hitmen — weapon families

#include "Weapons/HitmenWeaponTypes.h"
#include "Characters/HitmenCharacterBase.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "TimerManager.h"
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
	const AHitmenCharacterBase* Shooter = Cast<AHitmenCharacterBase>(OwnerPawn);
	const float Spread = WeaponData->GetPelletSpread(Shooter && Shooter->IsAiming());
	for (int32 Index = 0; Index < Pellets; ++Index)
	{
		FVector Dir = Direction;
		if (Pellets > 1 && WeaponData->bFixedPelletPattern)
		{
			// One pellet in the middle, the others on two rings (golden-angle steps), a little seeded jitter.
			if (Index > 0)
			{
				const float Ring = Index <= (Pellets - 1) / 2 ? 0.55f : 1.f;
				const float Angle = Index * 137.5f;
				const FVector Right = FVector::CrossProduct(Direction, FMath::Abs(Direction.Z) < 0.99f ? FVector::UpVector : FVector::ForwardVector).GetSafeNormal();
				const FVector Up = FVector::CrossProduct(Right, Direction);
				const FVector Offset = (Right * FMath::Cos(FMath::DegreesToRadians(Angle)) + Up * FMath::Sin(FMath::DegreesToRadians(Angle)))
					* FMath::Tan(FMath::DegreesToRadians(Spread * Ring));
				Dir = (Direction + Offset).GetSafeNormal();
			}
			Dir = UHitmenCombatLibrary::RandomConeDirection(Dir, Spread * 0.1f, Seed, Index + 1);
		}
		else if (Pellets > 1)
		{
			Dir = UHitmenCombatLibrary::RandomConeDirection(Direction, Spread, Seed, Index + 1);
		}
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
		float Damage = WeaponData->GetDamageAtDistance(FVector::Dist(Origin, Hit.ImpactPoint));
		const AHitmenCharacterBase* HitCharacter = Cast<AHitmenCharacterBase>(HitActor);
		const TArray<FName>& HeadBones = HitCharacter ? HitCharacter->GetHeadBones() : WeaponData->HeadBones;
		if (HeadBones.Contains(Hit.BoneName))
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
		Projectile->SetHeadshotMultiplier(WeaponData->HeadshotMultiplier);
		Projectile->Launch(Direction * WeaponData->ProjectileSpeed * SpeedScale, WeaponData->Damage * DamageScale);
	}
}

// ---------------------------------------------------------------------------------------------
// Melee
// ---------------------------------------------------------------------------------------------

void AHitmenWeapon_Melee::SweepHostiles(const FVector& Origin, const FVector& Direction, TArray<FHitResult>& OutHits) const
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
	TSet<AActor*> Seen;
	for (const FHitResult& Hit : Hits)
	{
		AActor* HitActor = Hit.GetActor();
		if (!HitActor || Seen.Contains(HitActor) || !UHitmenTeamLibrary::AreHostile(OwnerPawn, HitActor))
		{
			continue;
		}
		Seen.Add(HitActor);
		OutHits.Add(Hit);
	}
}

void AHitmenWeapon_Melee::FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts)
{
	TArray<FHitResult> Hits;
	SweepHostiles(Origin, Direction, Hits);
	for (const FHitResult& Hit : Hits)
	{
		UGameplayStatics::ApplyPointDamage(Hit.GetActor(), WeaponData->Damage, Direction, Hit, OwnerPawn->GetController(), this, UDamageType::StaticClass());
		OutImpacts.Add(Hit.ImpactPoint);
	}
}

void AHitmenWeapon_Melee::SimulateImpacts(const FVector& Origin, const FVector& Direction, int32 Seed, TArray<FVector>& OutImpacts) const
{
	TArray<FHitResult> Hits;
	SweepHostiles(Origin, Direction, Hits);
	for (const FHitResult& Hit : Hits)
	{
		OutImpacts.Add(Hit.ImpactPoint);
	}
	// The blade lands a little into the slash: shake, view kick and hit stop then.
	AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(OwnerPawn);
	if (Hits.Num() > 0 && Player && Player->IsLocallyControlled() && WeaponData)
	{
		const float Shake = WeaponData->HitShake;
		const float Kick = WeaponData->HitFOVKick;
		const float Stop = WeaponData->HitStop;
		const float Delay = FMath::Clamp(WeaponData->GetFireInterval() * 0.9f, 0.3f, 0.55f) * 0.4f;
		TWeakObjectPtr<AHitmenPlayerCharacter> WeakPlayer(Player);
		FTimerHandle Handle;
		GetWorldTimerManager().SetTimer(Handle, FTimerDelegate::CreateWeakLambda(Player, [WeakPlayer, Shake, Kick, Stop]()
		{
			if (WeakPlayer.IsValid())
			{
				WeakPlayer->PlayMeleeHitFeedback(Shake, Kick, Stop);
			}
		}), Delay, false);
	}
}
