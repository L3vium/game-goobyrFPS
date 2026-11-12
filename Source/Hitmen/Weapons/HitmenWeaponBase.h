// Hitmen — weapon base: data-driven, predicted firing; one actor for its whole life (in hand,
// holstered, or lying in the world as its own pickup)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Core/HitmenTypes.h"
#include "Core/HitmenInterfaces.h"
#include "Interaction/HitmenInteractable.h"
#include "HitmenWeaponBase.generated.h"

class UHitmenWeaponData;
class UHitmenInventoryComponent;
class UStaticMeshComponent;
class USkeletalMeshComponent;
class UStaticMesh;
class UBoxComponent;
class USphereComponent;

/** Where the weapon actor currently is. Replicated; every machine derives visuals from it. */
UENUM(BlueprintType)
enum class EHitmenWeaponState : uint8
{
	/** Carried, hidden (other slot). */
	Holstered,
	/** Carried, shown and usable. */
	InHand,
	/** Lying in the world: physics, pickup prompt, auto-ammo. */
	Dropped
};

/**
 * Firing flow: the owning client predicts cosmetics and sends Server_Fire with a seed; the
 * server validates rate and ammo, resolves hits (FireServer) and multicasts effects to others.
 * NPCs call FireAt on the server. Children: hitscan, projectile, melee.
 *
 * Lifetime: spawned once (loadout, loot, level placement). Throwing or swapping puts the same
 * actor in the Dropped state (it becomes the world pickup); picking it up re-attaches it. Only a
 * same-type ammo merge destroys it (its ammo moved into the held weapon).
 */
UCLASS(Abstract, Blueprintable)
class HITMEN_API AHitmenWeaponBase : public AActor, public IHitmenInteractable, public IHitmenScannable
{
	GENERATED_BODY()

public:
	AHitmenWeaponBase();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void Tick(float DeltaSeconds) override;
	/** Attachment differs per machine (first-person mesh locally): never take the server's. */
	virtual void OnRep_AttachmentReplication() override;

	/** Server: called right after spawn. */
	void InitWeapon(UHitmenWeaponData* InData, APawn* InOwner);

	/**
	 * Server: spawns a weapon lying in the world (loot, level placement, legacy pickups).
	 * Ammo < 0 = the weapon's start ammo.
	 */
	static AHitmenWeaponBase* SpawnDropped(UObject* WorldContextObject, UHitmenWeaponData* InData, int32 InAmmo, const FTransform& Transform);

	/** Server: shows/attaches when in hand, hides when holstered. No effect while dropped. */
	void SetEquipped(bool bEquipped);

	/**
	 * Server: hand (or world) -> world. The actor detaches, simulates physics and becomes the
	 * pickup. A thrower makes it a thrown weapon (hurts the first hostile pawn it hits).
	 * Ammo < 0 = the weapon's start ammo.
	 */
	void Drop(int32 InAmmo, const FVector& Location, const FRotator& Rotation, const FVector& Velocity, APawn* Thrower);

	/** Server: world -> hand. The inventory then sets the slot and calls SetEquipped. */
	void PickUp(APawn* NewOwner);

	/** Server: a dropped weapon floats at this exact point (container shelves, racks). */
	void StartHoveringAt(const FVector& Location, const FRotator& Rotation);

	/** Any machine: re-applies the current state (the attach parent may have changed, e.g. on possession). */
	void RefreshAttachment() { ApplyWeaponState(); }

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	bool IsEquipped() const { return WeaponState == EHitmenWeaponState::InHand; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	bool IsDropped() const { return WeaponState == EHitmenWeaponState::Dropped; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	EHitmenWeaponState GetWeaponState() const { return WeaponState; }

	/** Server: ammo this weapon carries while dropped (start ammo if never set). */
	int32 GetDroppedAmmo() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	UHitmenWeaponData* GetWeaponData() const { return WeaponData; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	APawn* GetOwnerPawn() const { return OwnerPawn; }

	/** Spread half-angle (hip or aim). Fixed: firing never widens it. */
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

	/** Minigun barrels: 0 at rest, 1 at full speed (drives the spin animation). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Weapon")
	float GetSpinAlpha() const;

	/** Seconds between shots right now (the minigun ramps up while the trigger is held). */
	float GetCurrentFireInterval() const;

	// IHitmenInteractable (dropped weapons: pick up / swap)
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

	// IHitmenScannable (dropped weapons show as interactables in the Sightroom)
	virtual EHitmenScanCategory GetScanCategory_Implementation(const AActor* Observer) const override;
	virtual void SetScanHighlight_Implementation(bool bEnabled, int32 StencilValue) override;

protected:
	/** Physics body of the dropped weapon (actor root). No collision while carried. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> DropCollision;

	/** Grip pivot: the visuals hang under it. Offset to centre the mesh on the box when dropped. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Root;

	/** Static visual (set from data if empty). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> StaticMesh;

	/** Skeletal visual (set from data if empty). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USkeletalMeshComponent> SkeletalMesh;

	/** Walking over a dropped weapon of a type you hold takes its ammo. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USphereComponent> AutoPickupSphere;

	UPROPERTY(ReplicatedUsing=OnRep_WeaponData, BlueprintReadOnly, Category="Hitmen|Weapon")
	TObjectPtr<UHitmenWeaponData> WeaponData;

	UPROPERTY(ReplicatedUsing=OnRep_OwnerPawn, BlueprintReadOnly, Category="Hitmen|Weapon")
	TObjectPtr<APawn> OwnerPawn;

	UPROPERTY(ReplicatedUsing=OnRep_WeaponState, BlueprintReadOnly, Category="Hitmen|Weapon")
	EHitmenWeaponState WeaponState = EHitmenWeaponState::Holstered;

	/** Dropped and settled: physics off, floating and turning (every machine bobs locally). */
	UPROPERTY(ReplicatedUsing=OnRep_Hovering, BlueprintReadOnly, Category="Hitmen|Weapon")
	bool bHovering = false;

	/** Server: ammo carried while dropped (-1 = start ammo). */
	UPROPERTY(Transient)
	int32 DroppedAmmo = -1;

	// ---- Dropped tuning ----
	/** Seconds after a drop before it can be picked up (avoids re-grabbing a weapon just thrown). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="s"))
	float ThrownPickupDelay = 0.6f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="cm"))
	float AutoPickupRadius = 70.f;

	/** Fit the drop box to the mesh bounds; otherwise DropCollisionExtent is used. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup")
	bool bFitDropCollisionToMesh = true;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(Units="cm"))
	FVector DropCollisionExtent = FVector(30.f, 8.f, 10.f);

	/** Minimum half-size of the fitted box (thin meshes must not tunnel through floors). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="cm"))
	float MinDropCollisionExtent = 4.f;

	/** Spin given to a thrown weapon (deg/s; X gets a random sign). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup")
	FVector ThrowAngularVelocity = FVector(360.f, 720.f, 0.f);

	/** Shown when the data has no mesh (placeholder until the team assigns one). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Visuals")
	TSoftObjectPtr<UStaticMesh> PlaceholderMesh = TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(TEXT("/Engine/BasicShapes/Cube.Cube")));

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Visuals")
	FVector PlaceholderScale = FVector(0.5f, 0.12f, 0.15f);

	// ---- Lying in the world: once it lands it floats and turns (no physics) ----
	/** A thrown weapon lands as a floating pickup at once, this far ahead of the thrower (or before a wall). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Weapon|Drop", meta=(ClampMin="0", Units="cm"))
	float ThrowPlaceDistance = 180.f;

	/** Height of the weapon centre above the ground while floating. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="cm"))
	float HoverHeight = 45.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="cm"))
	float HoverBobAmplitude = 6.f;

	/** Bob cycles per second. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0"))
	float HoverBobFrequency = 0.6f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(Units="deg"))
	float HoverSpinSpeed = 45.f;

	/** Below this speed for SettleTime the physics stops and the weapon floats. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="cm/s"))
	float SettleSpeed = 40.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="s"))
	float SettleTime = 0.25f;

	/** A drop that never settles (stuck on a slope) floats anyway after this long. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Pickup", meta=(ClampMin="0", Units="s"))
	float MaxFallTime = 3.f;

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

	/** Spawns muzzle / tracer / impact effects and sounds from data, plays the fire montage, then calls the juice hook. */
	virtual void PlayFireEffects(const FVector& Origin, const TArray<FVector>& Impacts, bool bLocallyPredicted);

	UFUNCTION() void OnRep_WeaponData();
	UFUNCTION() void OnRep_OwnerPawn();
	UFUNCTION() void OnRep_WeaponState();
	UFUNCTION() void OnRep_Hovering();

	/** Server: physics off, lifted to the hover height above the ground under it. */
	void StartHovering();
	void ApplyHoverState();
	void UpdateDropped(float DeltaSeconds);

	/** The mesh that is shown (skeletal when there is one, else static). */
	UPrimitiveComponent* GetVisualComponent() const;

	/** First-person FOV / scale rendering while on the local player's arms. */
	void SetFirstPersonVisuals(bool bFirstPerson);

	/** Applies WeaponState on this machine: attach/hide/physics/collision, equip juice on transitions. */
	void ApplyWeaponState();

	void ApplyVisualsFromData();
	void FireOnce();
	void HandleAutoFire();
	bool CanFireNow() const;
	int32 GetOwnerAmmo() const;
	bool IsLocallyOwned() const;
	USceneComponent* GetAttachParent() const;
	void ReportShotNoise(const FVector& Origin, const TArray<FVector>& Impacts) const;

	// ---- Dropped pickup (server) ----
	void ActivatePickup();
	void TryAutoAmmo(AActor* Other);
	void ScanOverlappingPawns();
	void WatchInventory(APawn* Pawn);
	void UnwatchInventory(APawn* Pawn);
	void UnwatchAllInventories();

	UFUNCTION()
	void HandleAutoPickupBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION()
	void HandleAutoPickupEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex);

	/** A pawn standing in the sphere changed its inventory (its weapon may now match). */
	UFUNCTION()
	void HandleWatchedInventoryChanged();

	UFUNCTION()
	void HandleDropHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult& Hit);

	// ---- Juice hooks (cosmetic only) ----
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnFired(bool bLocallyPredicted);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnImpact(FVector ImpactPoint);

	/** Fires on transitions only (holstered <-> in hand). */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEquipped(bool bEquipped);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDryFire();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSpinUp(bool bStarted);

	/** The weapon became a world pickup (true) or was picked up (false). */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDropped(bool bDropped);

	/** Muzzle flash light (every weapon; lit for a few hundredths of a second per shot). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UPointLightComponent> MuzzleLight;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Juice", meta=(ClampMin="0", Units="s"))
	float MuzzleFlashTime = 0.05f;

	float MuzzleLightUntil = 0.f;

	/** Thrown weapons fly with physics and spin; one on the head stuns a pawn this long. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Throw", meta=(ClampMin="0", Units="deg"))
	float ThrowSpinSpeed = 900.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Throw", meta=(ClampMin="0", Units="s"))
	float ThrowStunDuration = 3.f;

	/** Lob added to a throw: upward speed as a share of the throw speed. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Throw", meta=(ClampMin="0"))
	float ThrowLift = 0.15f;

	/** Puff where an empty weapon vanishes after its throw. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Throw")
	TObjectPtr<class UNiagaraSystem> ThrownVanishFX;

	/** An empty gun is gone after its throw (whatever it hits); a loaded one stays a pickup. */
	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_ThrownVanish(FVector_NetQuantize Location);
	void VanishAfterThrow();

	/** Empty (and using ammo): thrown, it vanishes. */
	bool IsEmptyThrow() const;

	/** Silenced weapons: a bullet into a body is heard this far at most. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Weapon|Noise", meta=(ClampMin="0", Units="cm"))
	float SilencedFleshNoiseRadius = 150.f;

	UPROPERTY(Transient)
	TObjectPtr<APawn> LastThrower;

	TArray<TWeakObjectPtr<UHitmenInventoryComponent>> WatchedInventories;

	FTimerHandle FireTimer;
	FTimerHandle SpinUpTimer;
	FTimerHandle PickupActivationTimer;
	EHitmenWeaponState AppliedState = EHitmenWeaponState::Holstered;
	bool bStateApplied = false;
	bool bTriggerHeld = false;
	bool bSpunUp = false;
	/** Minigun ramp: when the barrels started turning, and when the trigger was last released. */
	float SpinStartTime = -100.f;
	float SpinReleaseTime = -100.f;
	bool bPickupActive = false;
	bool bThrowDamageDone = false;
	bool bConsumed = false;
	int32 BurstRemaining = 0;
	float LastFireTime = -100.f;
	float LastServerFireTime = -100.f;
	float SlowTime = 0.f;
	float DropTime = 0.f;
	float HoverPhase = 0.f;
	float ChargeStartTime = -1.f;
	int32 ShotCounter = 0;
	FVector DropCenter = FVector::ZeroVector;
};
