// Hitmen — manipulation abilities (A): throwables, remote distraction, charm, lighter,
// multi-kill, placed devices, poison. All work on NPCs and players.

#pragma once

#include "CoreMinimal.h"
#include "Abilities/Manipulation/HitmenManipulationAbility.h"
#include "HitmenManipulationAbilities.generated.h"

class AHitmenProjectile;
class AHitmenCharacterBase;

/** Throws a projectile class (stone, sleep dart, flashbang, smoke grenade). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Throwable : public UHitmenManipulationAbility
{
	GENERATED_BODY()

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Throwable")
	TSubclassOf<AHitmenProjectile> ProjectileClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Throwable", meta=(ClampMin="0"))
	float ThrowSpeed = 1500.f;

	/** Extra upward angle for lobbed throws (degrees). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Throwable", meta=(ClampMin="-30", ClampMax="60"))
	float LobAngle = 8.f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

UCLASS(Blueprintable) class HITMEN_API UHitmenAbility_Stone : public UHitmenAbility_Throwable { GENERATED_BODY() public: UHitmenAbility_Stone(); };
UCLASS(Blueprintable) class HITMEN_API UHitmenAbility_SleepDart : public UHitmenAbility_Throwable { GENERATED_BODY() public: UHitmenAbility_SleepDart(); };
UCLASS(Blueprintable) class HITMEN_API UHitmenAbility_Flashbang : public UHitmenAbility_Throwable { GENERATED_BODY() public: UHitmenAbility_Flashbang(); };
UCLASS(Blueprintable) class HITMEN_API UHitmenAbility_SmokeGrenade : public UHitmenAbility_Throwable { GENERATED_BODY() public: UHitmenAbility_SmokeGrenade(); };

/** Triggers a distraction, a panel or a console at range. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_RemoteDistraction : public UHitmenManipulationAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_RemoteDistraction();
	virtual AActor* FindHintTarget(const FVector& Origin, const FVector& Direction, FText& OutAction) const override;

protected:
	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

/** The aimed target fights for you for Duration (charmed players are driven by AI). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Charm : public UHitmenManipulationAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Charm();
	virtual AActor* FindHintTarget(const FVector& Origin, const FVector& Direction, FText& OutAction) const override;

protected:
	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

/** Lighter: burns a body away so nobody finds it (not the VIP). Look at a body nearby: [A] Lighter. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Lighter : public UHitmenManipulationAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Lighter();
	virtual AActor* FindHintTarget(const FVector& Origin, const FVector& Direction, FText& OutAction) const override;

protected:
	/** The body burns this long before it is gone. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Lighter", meta=(ClampMin="0", Units="s"))
	float BurnTime = 1.2f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

/** Hold A: every enemy you look at is marked, one after the other (up to MaxMarks); release to confirm.
 *  When one marked target dies, every marked target dies. Marks are linked by a red line (owner only). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_MultiKill : public UHitmenManipulationAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_MultiKill();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability|MultiKill")
	const TArray<AHitmenCharacterBase*>& GetMarkedTargets() const { return MarkedTargets; }

	virtual bool WantsTick() const override { return bMarking; }
	virtual void TickAbility(float DeltaTime) override;
	virtual AActor* FindHintTarget(const FVector& Origin, const FVector& Direction, FText& OutAction) const override;

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|MultiKill", meta=(ClampMin="2"))
	int32 MaxMarks = 5;

	/** While A is held, one more target every MarkInterval (the next in view, closest to the crosshair). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|MultiKill", meta=(ClampMin="0.02", Units="s"))
	float MarkInterval = 0.12f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|MultiKill", meta=(ClampMin="1", ClampMax="60", Units="deg"))
	float MarkConeAngle = 25.f;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
	virtual void Release(const FVector& AimOrigin, const FVector& AimDirection) override;

	/** Server: marks the best unmarked target in view (true when one was added). */
	bool MarkNext();

	UPROPERTY(Replicated)
	bool bMarking = false;

	float LastMarkTime = -100.f;

	UFUNCTION()
	void HandleMarkedDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot);

	void ClearMarks();

	UPROPERTY(Replicated)
	TArray<AHitmenCharacterBase*> MarkedTargets;

	FTimerHandle ExpireTimer;
	bool bResolving = false;
};

/** Places an actor on the ground in front (trap, wind mine). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_PlaceDevice : public UHitmenManipulationAbility
{
	GENERATED_BODY()

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Device")
	TSubclassOf<AActor> DeviceClass;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

UCLASS(Blueprintable) class HITMEN_API UHitmenAbility_Trap : public UHitmenAbility_PlaceDevice { GENERATED_BODY() public: UHitmenAbility_Trap(); };
/** Wind grenade: thrown, it bursts on impact and blows everyone around away, you included. */
UCLASS(Blueprintable) class HITMEN_API UHitmenAbility_WindMine : public UHitmenAbility_Throwable { GENERATED_BODY() public: UHitmenAbility_WindMine(); };

/**
 * Pour into a drink or a vent hatch (poisons sealed rooms on that vent network).
 * A vent whose rooms are all open refuses the vial: no charge is spent and the player is told why.
 */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_PoisonVial : public UHitmenManipulationAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_PoisonVial();

protected:
	/** Shown to the owning player when no room served by the vent is sealed. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|Poison")
	FText RoomNotSealedText;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};
