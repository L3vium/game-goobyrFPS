// Hitmen — weapon base: data-driven, predicted firing, spawned once and shown/hidden

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenWeaponBase.generated.h"

class UHitmenWeaponData;
class UStaticMeshComponent;
class USkeletalMeshComponent;

/**
 * Firing flow: the owning client predicts cosmetics and sends Server_Fire with a seed; the
 * server validates rate and ammo, resolves hits (FireServer) and multicasts effects to others.
 * NPCs call FireAt on the server. Children: hitscan, projectile, melee.
 */
UCLASS(Abstract, Blueprintable)
class HITMEN_API AHitmenWeaponBase : public AActor
{
	GENERATED_BODY()

public:
	AHitmenWeaponBase();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void Tick(float DeltaSeconds) override;

	/** Server: called right after spawn. */
	void InitWeapon(UHitmenWeaponData* InData, APawn* InOwner);

	/** Shows/attaches when in hand, hides when holstered. Runs on every machine. */
	void SetEquipped(bool bEquipped);

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	bool IsEquipped() const { return bIsEquipped; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	UHitmenWeaponData* GetWeaponData() const { return WeaponData; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	APawn* GetOwnerPawn() const { return OwnerPawn; }

	/** Current spread half-angle (hip/aim + bloom), for the crosshair ring. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	float GetCurrentSpread() const;

	/** 0..1 bow charge. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	float GetCharge() const;

	// ---- Owner input (local) ----
	virtual void StartFire();
	virtual void StopFire();

	/** Server: NPC fire toward a point with aim error in degrees. NPC weapons have unlimited ammo. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Weapon")
	virtual void FireAt(const FVector& TargetLocation, float AimErrorDegrees);

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	FVector GetMuzzleLocation() const;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Root;

	/** Static visual (set from data if empty). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> StaticMesh;

	/** Skeletal visual (set from data if empty). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USkeletalMeshComponent> SkeletalMesh;

	UPROPERTY(ReplicatedUsing=OnRep_WeaponData, BlueprintReadOnly, Category="Hitmen|Weapon")
	TObjectPtr<UHitmenWeaponData> WeaponData;

	UPROPERTY(Replicated, BlueprintReadOnly, Category="Hitmen|Weapon")
	TObjectPtr<APawn> OwnerPawn;

	/**
	 * Server: resolve one trigger pull. OutImpacts receives hit points for effects.
	 * Charge is 0..1 for charge weapons, 1 otherwise.
	 */
	virtual void FireServer(const FVector& Origin, const FVector& Direction, int32 Seed, float Charge, TArray<FVector>& OutImpacts) {}

	/** Local prediction of impact points for effects (no damage). */
	virtual void SimulateImpacts(const FVector& Origin, const FVector& Direction, int32 Seed, TArray<FVector>& OutImpacts) const {}

	UFUNCTION(Server, Reliable)
	void Server_Fire(FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction, int32 Seed, float Charge);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_FireEffects(FVector_NetQuantize Origin, const TArray<FVector_NetQuantize>& Impacts);

	/** Spawns muzzle / tracer / impact effects and sounds from data, then calls the juice hook. */
	virtual void PlayFireEffects(const FVector& Origin, const TArray<FVector>& Impacts, bool bLocallyPredicted);

	UFUNCTION() void OnRep_WeaponData();

	void ApplyVisualsFromData();
	void FireOnce();
	void HandleAutoFire();
	bool CanFireNow() const;
	int32 GetOwnerAmmo() const;
	bool IsLocallyOwned() const;
	USceneComponent* GetAttachParent() const;
	void ReportShotNoise(const FVector& Origin) const;

	// ---- Juice hooks (cosmetic only) ----
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnFired(bool bLocallyPredicted);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnImpact(FVector ImpactPoint);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEquipped(bool bEquipped);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDryFire();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSpinUp(bool bStarted);

	FTimerHandle FireTimer;
	FTimerHandle SpinUpTimer;
	bool bIsEquipped = false;
	bool bTriggerHeld = false;
	bool bSpunUp = false;
	int32 BurstRemaining = 0;
	float LastFireTime = -100.f;
	float LastServerFireTime = -100.f;
	float CurrentBloom = 0.f;
	float ChargeStartTime = -1.f;
	int32 ShotCounter = 0;
};
