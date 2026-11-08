// Hitmen — stealth fixtures: lockers, tall grass, noisy distractions, silent hackable distractions

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenStealth.generated.h"

class UBoxComponent;
class AHitmenCharacterBase;

/** Hide inside; F again to get out. NPCs cannot see you inside. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenLocker : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenLocker();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;

protected:
	/** Inside point, relative to the locker. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(MakeEditWidget=true))
	FVector InsideOffset = FVector(0.f, 0.f, 95.f);

	/** Exit point, relative to the locker. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(MakeEditWidget=true))
	FVector ExitOffset = FVector(90.f, 0.f, 95.f);

	UPROPERTY(Replicated, BlueprintReadOnly, Category="Hitmen|Locker")
	TObjectPtr<AHitmenCharacterBase> Occupant;

	virtual void OnInteracted(AActor* Interactor) override;
};

/** Crouching inside hides you from NPC sight. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenTallGrass : public AActor
{
	GENERATED_BODY()

public:
	AHitmenTallGrass();
	virtual void BeginPlay() override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> Volume;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Grass", meta=(ClampMin="0.05"))
	float CheckInterval = 0.2f;

	void UpdateOccupants();

	FTimerHandle CheckTimer;
};

/** Makes a noise that attracts NPCs (radio, alarm clock, sprinkler...). */
UCLASS(Blueprintable)
class HITMEN_API AHitmenDistraction : public AHitmenInteractableBase, public IHitmenRemoteActivatable
{
	GENERATED_BODY()

public:
	AHitmenDistraction();

	virtual bool CanRemoteActivate_Implementation() const override;
	virtual void RemoteActivate_Implementation(AActor* Activator) override;

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction", meta=(ClampMin="0", Units="cm"))
	float NoiseRadius = 1500.f;

	/** Seconds before it can be used again. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction", meta=(ClampMin="0"))
	float Cooldown = 15.f;

	virtual void OnInteracted(AActor* Interactor) override;
	virtual void Trigger(AActor* Activator);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Triggered();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnTriggered();

	float LastTriggerTime = -1000.f;
};

/** Silent: draws NPCs that can see it (screens, lights). Hack it with F or remotely. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenHackableDistraction : public AHitmenDistraction
{
	GENERATED_BODY()

public:
	AHitmenHackableDistraction();

protected:
	virtual void Trigger(AActor* Activator) override;
};
