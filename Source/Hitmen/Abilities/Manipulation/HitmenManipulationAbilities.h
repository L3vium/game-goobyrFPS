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

/** Lighter: vanishes a body so nobody finds it (not the VIP). */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_Lighter : public UHitmenManipulationAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_Lighter();

protected:
	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;
};

/** Mark several targets; when one dies, every marked target dies. */
UCLASS(Blueprintable)
class HITMEN_API UHitmenAbility_MultiKill : public UHitmenManipulationAbility
{
	GENERATED_BODY()

public:
	UHitmenAbility_MultiKill();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability|MultiKill")
	const TArray<AHitmenCharacterBase*>& GetMarkedTargets() const { return MarkedTargets; }

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Ability|MultiKill", meta=(ClampMin="2"))
	int32 MaxMarks = 4;

	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) override;

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
UCLASS(Blueprintable) class HITMEN_API UHitmenAbility_WindMine : public UHitmenAbility_PlaceDevice { GENERATED_BODY() public: UHitmenAbility_WindMine(); };

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
