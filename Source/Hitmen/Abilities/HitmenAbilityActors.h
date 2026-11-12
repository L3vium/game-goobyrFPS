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
class UStaticMesh;
class UMaterialInterface;
class UMaterialInstanceDynamic;

/**
 * Blocks NPC sight through its sphere (players are blocked by the visual).
 * Visual: the Niagara slot (team VFX) or, while none is assigned, a fallback cloud of basic-shape
 * puffs that grow in and fade out over the lifetime (cosmetic, built on every machine).
 */
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

	/** The smoke effect plays this much faster (it fills the radius quicker). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke", meta=(ClampMin="0.1"))
	float EffectTimeScale = 1.8f;

	/** Uniform scale of the smoke effect so it covers Radius. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke", meta=(ClampMin="0.01"))
	float EffectScale = 1.f;

	// ---- Fallback puffs (shown while no Niagara system is assigned) ----
	/** Hide the puffs when the Effect component has a Niagara system. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback")
	bool bHidePuffsWhenNiagaraAssigned = true;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0", ClampMax="64"))
	int32 PuffCount = 14;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback")
	TSoftObjectPtr<UStaticMesh> PuffMesh;

	/** Translucent material; its PuffOpacityParameter scalar is driven for the fade. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback")
	TSoftObjectPtr<UMaterialInterface> PuffMaterial;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback")
	FName PuffOpacityParameter = TEXT("Opacity");

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0", Units="cm"))
	float PuffMinRadius = 120.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0", Units="cm"))
	float PuffMaxRadius = 240.f;

	/** Puff centres are spread within Radius times this. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0", ClampMax="1"))
	float PuffSpread = 0.7f;

	/** Vertical squash of the spread (smoke hugs the ground); puffs stay above the origin. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0", ClampMax="1"))
	float PuffVerticalSpread = 0.5f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0", Units="s"))
	float PuffGrowTime = 1.2f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0", Units="s"))
	float PuffFadeOutTime = 2.5f;

	/** Puff scale reached at the end of the fade (also hides it when the material has no opacity parameter). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0"))
	float PuffFadeOutScale = 0.2f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Smoke|Fallback", meta=(ClampMin="0.01", Units="s"))
	float PuffUpdateInterval = 0.05f;

	UPROPERTY(Transient)
	TArray<TObjectPtr<UStaticMeshComponent>> Puffs;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> PuffMaterialInstance;

	/** Full scale of each puff (parallel to Puffs). */
	TArray<FVector> PuffScales;
	float SpawnTime = 0.f;
	FTimerHandle PuffTimer;

	void BuildPuffs();
	void UpdatePuffs();
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
