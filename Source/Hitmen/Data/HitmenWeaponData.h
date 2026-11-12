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
class UAnimSequenceBase;
class UHitmenAnimSet;

/**
 * Data of a weapon type. Two pickups with the same data are the same type (ammo merges).
 * Every gameplay number lives here so designers balance without code.
 */
/** How the weapon moves in first person (procedural, per weapon). Offsets are camera space: X forward, Y right, Z up. */
USTRUCT(BlueprintType)
struct FHitmenFPMotion
{
	GENERATED_BODY()

	/** Added to the character's hip offset (machine guns sit lower). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP")
	FVector HipOffset = FVector::ZeroVector;

	/** Added to the character's aim offset (the gun comes toward the centre, still low). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP")
	FVector AimOffset = FVector::ZeroVector;

	/** Coming into hand: seconds, and where it starts (below the view, tipped). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0.01", Units="s"))
	float EquipTime = 0.2f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP")
	FVector EquipFrom = FVector(-4.f, 6.f, -26.f);

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP")
	FRotator EquipFromRotation = FRotator(-40.f, 10.f, -25.f);

	/** Kick per shot: back (cm), up (deg), random sideways (deg); stiffness of the spring that brings it back. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0", Units="cm"))
	float KickBack = 2.5f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0", Units="deg"))
	float KickUp = 3.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0", Units="deg"))
	float KickSide = 0.8f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="1"))
	float KickStiffness = 220.f;

	/** Scoped weapons: raise to the centre (s), then wobble before the zoom (deg, s); breathing sway while zoomed (deg). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0", Units="s"))
	float ScopeRaiseTime = 0.25f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0", Units="deg"))
	float ScopeWobble = 2.2f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0", Units="s"))
	float ScopeWobbleTime = 0.3f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|FP", meta=(ClampMin="0", Units="deg"))
	float ScopeSway = 0.12f;
};

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

	/**
	 * Actor class of the weapon (hitscan / projectile / melee child BP). Spawned once (loadout,
	 * loot, level placement); the same actor is held, holstered, thrown and lies in the world.
	 */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon")
	TSubclassOf<AHitmenWeaponBase> WeaponClass;

	/**
	 * Level-placement marker class (BP_WeaponPickup). It only hands off to a dropped weapon actor
	 * at BeginPlay; thrown weapons and loot no longer spawn it.
	 */
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

	/** Minigun: seconds to ramp from SpinUpStartRPM to RoundsPerMinute. It fires from the first press. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing", meta=(ClampMin="0"))
	float SpinUpTime = 0.f;

	/** Minigun: shots per minute at the start of the spin-up. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing", meta=(EditCondition="SpinUpTime>0", ClampMin="1"))
	float SpinUpStartRPM = 450.f;

	/** Minigun: releasing the trigger for less than this keeps the barrels spinning (no new ramp). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Firing", meta=(EditCondition="SpinUpTime>0", ClampMin="0"))
	float SpinDownGrace = 0.4f;

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

	/** Camera field of view while aiming. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="5", ClampMax="170"))
	float AimFOV = 70.f;

	/** Recoil pitch kick per shot (degrees, applied to the owning controller). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0"))
	float RecoilPitch = 0.6f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy", meta=(ClampMin="0"))
	float RecoilYawRandom = 0.25f;

	/** Aiming shows a full-screen scope (reticle + vignette) and hides the weapon. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Accuracy")
	bool bScopeOverlay = false;

	// ---- Stealth ----
	/** Silenced weapons make no gunshot noise (bodies are still seen). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Stealth")
	bool bSilenced = false;

	/** Radius in which NPCs hear the shot (occluded by walls). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Stealth", meta=(ClampMin="0", Units="cm"))
	float NoiseRadius = 600.f;

	/** Noise where a bullet lands (hitscan): this is what draws NPCs, silenced or not. 0 = none. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Noise", meta=(ClampMin="0", Units="cm"))
	float ImpactNoiseRadius = 500.f;

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

	/**
	 * Applied to the mesh inside the weapon actor: turns / scales a mesh pack to the hand socket's
	 * convention (forward = +Y for the mannequin's HandGrip_R) and real-world size.
	 */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FTransform VisualTransform;

	/** Socket on the character mesh the weapon attaches to. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FName AttachSocket = TEXT("HandGrip_R");

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FTransform AttachOffset;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FName MuzzleSocket = TEXT("Muzzle");

	/** Muzzle position in weapon space when the mesh has no muzzle socket. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FVector MuzzleOffset = FVector(40.f, 0.f, 4.f);

	/** Offset on the first-person arms (the local player's view); AttachOffset is used on bodies. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FTransform FirstPersonAttachOffset;

	/** Procedural first-person motion: hip / aim offsets, equip, kick, scope sequence. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	FHitmenFPMotion FPMotion;

	/** Looping arms pose while this weapon is in hand (first person). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	TSoftObjectPtr<UAnimSequenceBase> FirstPersonPose;

	/** Body animations while this weapon is in hand (armed locomotion, aim offset). Empty = character's set. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Visuals")
	TObjectPtr<UHitmenAnimSet> HeldAnimSet;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> MuzzleFX;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> TracerFX;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> ImpactFX;

	/** Puff thrown back toward the shooter where a bullet hits a surface (with ImpactFX). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> SurfaceImpactFX;

	/** Played instead of the surface effects when a bullet hits a character. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> FleshImpactFX;

	/** Smoke behind the weapon when it fires (rocket launcher back-blast). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UNiagaraSystem> BackBlastFX;

	/** Back-blast position in weapon space. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	FVector BackBlastOffset = FVector(-60.f, 0.f, 0.f);

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<USoundBase> FireSound;

	/** Fire sound pitch (random between X and Y) and volume: one recording gives every gun its own voice. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	FVector2D FireSoundPitch = FVector2D(0.97f, 1.03f);

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice", meta=(ClampMin="0"))
	float FireSoundVolume = 1.f;

	/** Share of shots drawn as a tracer (1 = every shot / pellet). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice", meta=(ClampMin="0", ClampMax="1"))
	float TracerChance = 0.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	FLinearColor TracerColor = FLinearColor(1.f, 0.75f, 0.35f);

	/** Muzzle flash light (candelas, 0 = none) and glow sphere radius (cm, 0 = none), like the mounted gun. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice", meta=(ClampMin="0"))
	float MuzzleLightIntensity = 4000.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice", meta=(ClampMin="0", Units="cm"))
	float MuzzleGlowRadius = 9.f;

	/** Uniform scale of MuzzleFX (the toon effects are sized for big stylised characters). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice", meta=(ClampMin="0.01"))
	float MuzzleFXScale = 0.25f;

	/** Played by the owner when firing with no ammo left, just before the weapon is thrown. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<USoundBase> DryFireSound;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<USoundBase> ImpactSound;

	/** Played on the owning character for each shot (predicted locally, multicast to others). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UAnimMontage> FireMontage;

	/** Aim pose an NPC holds (looped) while it fights with this weapon and stands still. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Juice")
	TSoftObjectPtr<UAnimMontage> AimMontage;

	/** Played on the owning character when the weapon comes into hand (every machine). */
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
