// Hitmen — weapon families: hitscan (pistol, SMG, rifles, shotgun, minigun, sniper),
// projectile (bow, rocket, grenade, black hole) and melee (sword)

#pragma once

#include "CoreMinimal.h"
#include "Weapons/HitmenWeaponBase.h"
#include "HitmenWeaponTypes.generated.h"

/** Instant traces; pellets for shotguns, falloff with range, headshot multiplier. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenWeapon_Hitscan : public AHitmenWeaponBase
{
	GENERATED_BODY()

protected:
	virtual void FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts) override;
	virtual void SimulateImpacts(const FVector& Origin, const FVector& Direction, int32 Seed, TArray<FVector>& OutImpacts) const override;

	/** Pellet spread for multi-pellet weapons (degrees, around the aimed direction). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Hitscan", meta=(ClampMin="0"))
	float PelletSpread = 5.f;

	void TracePellets(const FVector& Origin, const FVector& Direction, int32 Seed, TArray<FHitResult>& OutHits, TArray<FVector>& OutImpacts) const;
};

/** Spawns the data's projectile class (speed scales with charge for bows). */
UCLASS(Blueprintable)
class HITMEN_API AHitmenWeapon_Projectile : public AHitmenWeaponBase
{
	GENERATED_BODY()

protected:
	virtual void FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts) override;

	/** Minimum speed fraction at zero charge. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Projectile", meta=(ClampMin="0", ClampMax="1"))
	float MinChargeSpeed = 0.35f;
};

/** Sphere sweep in front of the owner. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenWeapon_Melee : public AHitmenWeaponBase
{
	GENERATED_BODY()

protected:
	virtual void FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts) override;
};
