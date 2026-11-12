// Hitmen — move abilities (E): blink, double jump, jetpack, dash, hook, transposition,
// pill, invisibility, armor, C4

#pragma once

#include "CoreMinimal.h"
#include "Abilities/Move/HitmenMoveAbility.h"
#include "HitmenMoveAbilities.generated.h"

class AHitmenProjectile;

/** Hold to aim, release to teleport to the aimed point (Dishonored-like). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Blink : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Blink();
	virtual bool WantsTick() const override { return bAiming; }
	virtual void LocalPress(const FVector& AimOrigin, const FVector& AimDirection) override;
	virtual void LocalRelease() override;
	virtual void LocalTick(float DeltaTime) override;

protected:
	/** Pulls the destination back from walls by this distance. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Blink", meta=(ClampMin="0"))
	float WallOffset = 50.f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
	bool ComputeDestination(const FVector& AimOrigin, const FVector& AimDirection, FVector& OutDestination) const;

	bool bAiming = false;

public:
	/** Holding E shows where you will land (the ability component draws the marker). */
	virtual bool GetAimMarker(const FVector& Origin, const FVector& Direction, FVector& OutPoint) const override;
};

/** Passive: one extra jump in the air. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_DoubleJump : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	virtual void OnEquipped() override;
	virtual void OnUnequipped() override;

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|DoubleJump", meta=(ClampMin="2"))
	int32 JumpCount = 2;

	/** The jumps after the first go this much higher. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|DoubleJump", meta=(ClampMin="0.5"))
	float ExtraJumpMultiplier = 1.35f;
};

/**
 * Hold to fly up while fuel lasts; fuel recharges on the ground.
 * Thrust and fuel are simulated by UHitmenCharacterMovementComponent from a saved-move flag, so
 * the owning client predicts them like sprint. The server copies its fuel here for the UI.
 */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Jetpack : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Jetpack();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual bool WantsTick() const override { return true; }
	virtual void TickAbility(float DeltaTime) override;
	virtual void LocalPress(const FVector& AimOrigin, const FVector& AimDirection) override;
	virtual void LocalRelease() override;
	virtual void LocalTick(float DeltaTime) override;
	virtual void OnEquipped() override;
	virtual void OnUnequipped() override;

	/** Predicted on the owning client, replicated for everyone else. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Ability|Jetpack")
	float GetFuelNormalized() const;

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Jetpack", meta=(ClampMin="0", Units="s"))
	float MaxFuel = 3.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Jetpack", meta=(ClampMin="0"))
	float FuelRechargePerSecond = 0.75f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Jetpack", meta=(ClampMin="0", Units="cm/s"))
	float LiftSpeed = 550.f;

	/** How fast the vertical speed converges to LiftSpeed. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Jetpack", meta=(ClampMin="0"))
	float LiftInterpSpeed = 6.f;

	/** Owning client snaps its predicted fuel to the server's when they differ by more (idle only). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Jetpack", meta=(ClampMin="0", Units="s"))
	float FuelReconcileTolerance = 0.3f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
	virtual void Release(const FVector& AimOrigin, const FVector& AimDirection) override;

	class UHitmenCharacterMovementComponent* GetMovement() const;

	UPROPERTY(Replicated)
	float Fuel = 3.f;

	/** Server: thrust state last tick, to fire Juice_OnEnded when the fuel runs out. */
	bool bWasThrusting = false;
};

/** Burst of speed in the input direction. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Dash : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Dash();
	virtual void LocalPress(const FVector& AimOrigin, const FVector& AimDirection) override;

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Dash", meta=(ClampMin="0"))
	float DashSpeed = 2200.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Dash", meta=(ClampMin="0"))
	float UpwardBoost = 150.f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
	FVector GetDashVelocity() const;
};

/** Grapple to a surface and get pulled toward it. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Hook : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Hook();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual bool WantsTick() const override { return bHooked; }
	virtual void TickAbility(float DeltaTime) override { Pull(DeltaTime); }
	virtual void LocalTick(float DeltaTime) override { Pull(DeltaTime); }

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability|Hook")
	FVector GetHookPoint() const { return HookPoint; }

	virtual bool GetAimMarker(const FVector& Origin, const FVector& Direction, FVector& OutPoint) const override;
	virtual bool GetCableAnchor(FVector& OutAnchor) const override { OutAnchor = HookPoint; return bHooked; }

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Hook", meta=(ClampMin="0"))
	float PullSpeed = 1800.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Hook", meta=(ClampMin="0"))
	float StopDistance = 150.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Hook", meta=(ClampMin="0"))
	float MaxPullTime = 2.5f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
	void Pull(float DeltaTime);

	/** Clients restart their local pull timer for every new hook (it is only reset on the server otherwise). */
	UFUNCTION()
	void OnRep_Hooked();

	UPROPERTY(ReplicatedUsing=OnRep_Hooked)
	bool bHooked = false;

	UPROPERTY(Replicated)
	FVector HookPoint = FVector::ZeroVector;

	float PullTime = 0.f;
};

/** Swap positions with the aimed character (NPC or player). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Transposition : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	virtual AActor* FindHintTarget(const FVector& Origin, const FVector& Direction, FText& OutAction) const override;

protected:
	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

/** Applies a status to the owner for Duration (pill boost, invisibility). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_SelfStatus : public UHitmenMoveAbility
{
	GENERATED_BODY()

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Status")
	EHitmenStatusEffect Status = EHitmenStatusEffect::Boosted;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Status", meta=(ClampMin="0"))
	float Magnitude = 1.f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

/** Pill: damage, speed and resistance boost with a distorted view (juice on the status). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Pill : public UHitmenAbility_SelfStatus
{
	GENERATED_BODY()

public:
	UHitmenAbility_Pill();
};

UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Invisibility : public UHitmenAbility_SelfStatus
{
	GENERATED_BODY()

public:
	UHitmenAbility_Invisibility();
};

/** Instant armor plates plus damage resistance for Duration. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Armor : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Armor();

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Armor", meta=(ClampMin="0"))
	float ArmorAmount = 75.f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

/** First press throws a sticky charge, second press detonates it. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_C4 : public UHitmenMoveAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_C4();

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|C4")
	TSubclassOf<AHitmenProjectile> ChargeClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|C4", meta=(ClampMin="0"))
	float ThrowSpeed = 1100.f;

	virtual bool CanActivate() const override;
	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;

	UPROPERTY(Transient)
	TWeakObjectPtr<AHitmenProjectile> PlacedCharge;
};
