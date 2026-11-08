// Hitmen — every tunable value of a weapon

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Core/HitmenTypes.h"
#include "HitmenWeaponData.generated.h"

class AHitmenWeaponBase;
class AHitmenProjectile;
class AHitmenWeaponPickup;
class UStaticMesh;
class USkeletalMesh;
class UNiagaraSystem;
class USoundBase;
class UAnimMontage;
class UCameraShakeBase;

/**
 * Data of a weapon type. Two pickups with the same data are the same type (ammo merges).
 * Every gameplay number lives here so designers balance without code.
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenWeaponData : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	// ---- Identity ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon")
	FText DisplayName;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon")
	TSoftObjectPtr<UTexture2D> Icon;

	/** Actor class spawned when equipped (hitscan / projectile / melee child BP). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon")
	TSubclassOf<AHitmenWeaponBase> WeaponClass;

	/** Pickup spawned in the world (loot, thrown weapons). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon")
	TSubclassOf<AHitmenWeaponPickup> PickupClass;

	/** Weight in loot tables. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon", meta=(ClampMin="0"))
	float LootWeight = 1.f;

	// ---- Ammo ----
	/** Melee weapons ignore ammo. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Ammo")
	bool bUsesAmmo = true;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Ammo", meta=(EditCondition="bUsesAmmo", ClampMin="0"))
	int32 StartAmmo = 30;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Ammo", meta=(EditCondition="bUsesAmmo", ClampMin="1"))
	int32 MaxAmmo = 120;

	/** Ammo granted when walking over a pickup of the same type. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Ammo", meta=(EditCondition="bUsesAmmo", ClampMin="0"))
	int32 AmmoPerPickup = 15;

	/** Ammo consumed per trigger pull / shot. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Ammo", meta=(EditCondition="bUsesAmmo", ClampMin="1"))
	int32 AmmoPerShot = 1;

	// ---- Firing ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing")
	EHitmenFireMode FireMode = EHitmenFireMode::Single;

	/** Shots per minute. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing", meta=(ClampMin="1"))
	float RoundsPerMinute = 300.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing", meta=(EditCondition="FireMode==EHitmenFireMode::Burst", ClampMin="1"))
	int32 BurstCount = 3;

	/** Seconds of spin-up before the first shot (minigun). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing", meta=(ClampMin="0"))
	float SpinUpTime = 0.f;

	/** Bow: hold to charge, release to fire. Damage/speed scale with charge. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing")
	bool bChargeToFire = false;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing", meta=(EditCondition="bChargeToFire", ClampMin="0.05"))
	float FullChargeTime = 1.f;

	// ---- Damage ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Damage", meta=(ClampMin="0"))
	float Damage = 25.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Damage", meta=(ClampMin="1"))
	float HeadshotMultiplier = 2.f;

	/** Bone names counting as head. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Damage")
	TArray<FName> HeadBones = { TEXT("head"), TEXT("neck_01") };

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Damage", meta=(ClampMin="0", Units="cm"))
	float Range = 10000.f;

	/** Damage multiplier at max range (linear falloff from 50% range). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Damage", meta=(ClampMin="0", ClampMax="1"))
	float FalloffAtMaxRange = 0.5f;

	/** Pellets per shot (shotgun). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Damage", meta=(ClampMin="1"))
	int32 PelletCount = 1;

	/** Damage dealt by the weapon when thrown and hitting a pawn. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Damage", meta=(ClampMin="0"))
	float ThrowDamage = 10.f;

	// ---- Accuracy ----
	/** Half-angle cone in degrees, hip fire. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0", Units="deg"))
	float HipSpread = 2.5f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0", Units="deg"))
	float AimSpread = 0.5f;

	/** Added per shot while firing continuously, recovers over time. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0", Units="deg"))
	float SpreadPerShot = 0.3f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0", Units="deg"))
	float MaxBloom = 4.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0"))
	float BloomRecoveryPerSecond = 6.f;

	/** Camera field of view while aiming. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="5", ClampMax="170"))
	float AimFOV = 70.f;

	/** Recoil pitch kick per shot (degrees, applied to the owning controller). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0"))
	float RecoilPitch = 0.6f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0"))
	float RecoilYawRandom = 0.25f;

	// ---- Stealth ----
	/** Silenced weapons make no gunshot noise (bodies are still seen). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Stealth")
	bool bSilenced = false;

	/** Radius in which NPCs hear the shot (occluded by walls). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Stealth", meta=(ClampMin="0", Units="cm"))
	float NoiseRadius = 2500.f;

	// ---- Projectile ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Projectile")
	TSubclassOf<AHitmenProjectile> ProjectileClass;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Projectile", meta=(ClampMin="0"))
	float ProjectileSpeed = 4000.f;

	// ---- Melee ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Melee", meta=(ClampMin="0", Units="cm"))
	float MeleeRange = 180.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Melee", meta=(ClampMin="0", Units="cm"))
	float MeleeRadius = 45.f;

	// ---- Visuals (swappable by the team) ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	TSoftObjectPtr<UStaticMesh> StaticMesh;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	TSoftObjectPtr<USkeletalMesh> SkeletalMesh;

	/** Socket on the character mesh the weapon attaches to. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FName AttachSocket = TEXT("HandGrip_R");

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FTransform AttachOffset;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FName MuzzleSocket = TEXT("Muzzle");

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> MuzzleFX;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> TracerFX;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> ImpactFX;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<USoundBase> FireSound;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<USoundBase> DryFireSound;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<USoundBase> ImpactSound;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UAnimMontage> FireMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UAnimMontage> EquipMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSubclassOf<UCameraShakeBase> FireCameraShake;

	float GetFireInterval() const { return 60.f / FMath::Max(1.f, RoundsPerMinute); }

	virtual FPrimaryAssetId GetPrimaryAssetId() const override
	{
		return FPrimaryAssetId(TEXT("HitmenWeapon"), GetFName());
	}
};
