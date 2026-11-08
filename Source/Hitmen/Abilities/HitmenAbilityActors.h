// Hitmen — actors spawned by abilities: smoke cloud, trap, wind mine, C4 charge, ability pickup

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Weapons/HitmenProjectile.h"
#include "Interaction/HitmenInteractableBase.h"
#include "HitmenAbilityActors.generated.h"

class USphereComponent;
class UNiagaraComponent;
class UHitmenAbilityData;

/** Blocks NPC sight through its sphere (players are blocked by the visual). */
UCLASS(Blueprintable)
class HITMEN_API AHitmenSmokeCloud : public AActor
{
	GENERATED_BODY()

public:
	AHitmenSmokeCloud();

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Smoke")
	float GetRadius() const { return Radius; }

	/** True when the segment crosses the cloud. */
	bool BlocksSegment(const FVector& Start, const FVector& End) const;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UNiagaraComponent> Effect;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke", meta=(ClampMin="0", Units="cm"))
	float Radius = 450.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke", meta=(ClampMin="0", Units="s"))
	float Lifetime = 12.f;
};

/** Stepping on it traps NPCs (long) and players (short). */
UCLASS(Blueprintable)
class HITMEN_API AHitmenTrap : public AActor
{
	GENERATED_BODY()

public:
	AHitmenTrap();
	virtual void BeginPlay() override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USphereComponent> Trigger;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Trap", meta=(ClampMin="0"))
	float NPCTrapDuration = 30.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Trap", meta=(ClampMin="0"))
	float PlayerTrapDuration = 4.f;

	UFUNCTION()
	void HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Triggered(AActor* Victim);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnTriggered(AActor* Victim);

	bool bTriggered = false;
};

/** Proximity mine that knocks back everyone around. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenWindMine : public AActor
{
	GENERATED_BODY()

public:
	AHitmenWindMine();
	virtual void BeginPlay() override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USphereComponent> Trigger;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|WindMine", meta=(ClampMin="0", Units="cm"))
	float BlastRadius = 600.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|WindMine", meta=(ClampMin="0"))
	float KnockbackSpeed = 1800.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|WindMine", meta=(ClampMin="0"))
	float ArmDelay = 1.f;

	UFUNCTION()
	void HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	void Blast();

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Blast();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnBlast();

	float ArmTime = 0.f;
	bool bDone = false;
};

/** Sticky charge detonated by the C4 ability. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenC4Charge : public AHitmenProjectile
{
	GENERATED_BODY()

public:
	AHitmenC4Charge();
};

/** World pickup that equips an ability in its slot. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenAbilityPickup : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenAbilityPickup();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Pickup")
	void InitPickup(UHitmenAbilityData* InAbility);

	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;

protected:
	UPROPERTY(EditAnywhere, Replicated, BlueprintReadOnly, Category="Hitmen|Pickup")
	TObjectPtr<UHitmenAbilityData> Ability;

	virtual void OnInteracted(AActor* Interactor) override;
};
