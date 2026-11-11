// Hitmen — hazards: sealed rooms (vent poison), poisonable drinks, explosive barrels, breakable walls

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Interaction/HitmenInteractableBase.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenHazards.generated.h"

class UBoxComponent;
class UStaticMeshComponent;

/** A room closed by doors/windows/hatches; poison only spreads when every opening is sealed. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenRoomVolume : public AActor
{
	GENERATED_BODY()

public:
	AHitmenRoomVolume();

	UFUNCTION(BlueprintPure, Category="Hitmen|Room")
	bool IsSealed() const;

	/** Server: gas the room if sealed. Returns true if it worked. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Room")
	bool TryPoison(AActor* Poisoner);

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> Bounds;

	/** Doors, windows and vent hatches (IHitmenSealable) that close this room. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Room")
	TArray<TObjectPtr<AActor>> Openings;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Room", meta=(ClampMin="0"))
	float PoisonDuration = 12.f;

	/** Poison magnitude (damage per second multiplier). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Room", meta=(ClampMin="0"))
	float PoisonMagnitude = 2.f;

	UFUNCTION(NetMulticast, Reliable)
	void Multicast_Gassed();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnGassed();
};

/** A glass / bottle NPCs drink from during their routine; poison it with the vial. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenPoisonableDrink : public AHitmenInteractableBase, public IHitmenPoisonable, public IHitmenNPCUsable
{
	GENERATED_BODY()

public:
	AHitmenPoisonableDrink();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	virtual bool CanBePoisoned_Implementation() const override { return !bPoisoned; }
	virtual void ApplyPoison_Implementation(AActor* Poisoner) override;
	virtual float GetUseDuration_Implementation() const override { return UseDuration; }
	virtual void NPCUse_Implementation(AHitmenCharacterBase* NPC) override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return false; }

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Drink", meta=(ClampMin="0"))
	float UseDuration = 3.f;

	/** Poison applied to the drinker (lethal by default). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Drink", meta=(ClampMin="0"))
	float PoisonDuration = 8.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Drink", meta=(ClampMin="0"))
	float PoisonMagnitude = 5.f;

	UPROPERTY(Replicated, BlueprintReadOnly, Category="Hitmen|Drink")
	bool bPoisoned = false;

	UPROPERTY(Transient)
	TObjectPtr<AActor> PoisonedBy;
};

/** Red barrel: explodes when shot or caught in another explosion. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenExplosiveBarrel : public AHitmenInteractableBase, public IHitmenExplosionReceiver
{
	GENERATED_BODY()

public:
	AHitmenExplosiveBarrel();

	virtual float TakeDamage(float DamageAmount, FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser) override;
	virtual void ReceiveExplosion_Implementation(const FVector& Origin, float Radius, AActor* InstigatorActor) override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return false; }

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barrel", meta=(ClampMin="1"))
	float Health = 20.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barrel", meta=(ClampMin="0"))
	float ExplosionRadius = 600.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barrel", meta=(ClampMin="0"))
	float ExplosionDamage = 150.f;

	/** Delay when set off by another explosion (chain reactions). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barrel", meta=(ClampMin="0"))
	float ChainDelay = 0.25f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barrel|Juice")
	TObjectPtr<class UNiagaraSystem> ExplosionEffect;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barrel|Juice", meta=(ClampMin="0"))
	float ExplosionEffectScale = 1.5f;

	/** Camera shake strength for players near the blast. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barrel|Juice", meta=(ClampMin="0"))
	float ShakeIntensity = 2.5f;

	void Explode(AController* InstigatorController);

	UFUNCTION(NetMulticast, Reliable)
	void Multicast_Exploded();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnExploded();

	bool bExploded = false;
	FTimerHandle ChainTimer;
};

/** Wall section that breaks on any explosion (weapons, C4, tank). */
UCLASS(Blueprintable)
class HITMEN_API AHitmenBreakableWall : public AActor, public IHitmenExplosionReceiver
{
	GENERATED_BODY()

public:
	AHitmenBreakableWall();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void ReceiveExplosion_Implementation(const FVector& Origin, float Radius, AActor* InstigatorActor) override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Breakable")
	void Break();

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(ReplicatedUsing=OnRep_Broken, BlueprintReadOnly, Category="Hitmen|Breakable")
	bool bBroken = false;

	UFUNCTION() void OnRep_Broken();

	/** Juice: spawn debris / geometry collection here. */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnBroken();
};
