// Hitmen — stealth fixtures: lockers, tall grass, noisy distractions (radio)

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
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual void Tick(float DeltaSeconds) override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;

protected:
	/**
	 * Hollow cabinet built from cubes (Mesh is the back panel): you really stand inside and look
	 * out through the slit of the door. Actor origin = floor, door on the +X face.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="40", Units="cm"))
	float Width = 84.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="40", Units="cm"))
	float Depth = 76.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="150", Units="cm"))
	float Height = 215.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="1", Units="cm"))
	float WallThickness = 4.f;

	/** Centre height of the eye slit in the door (match the player's eye). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(Units="cm"))
	float SlitHeight = 164.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="1", Units="cm"))
	float SlitGap = 7.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(Units="deg"))
	float DoorOpenAngle = 100.f;

	/** Door opens and closes again over this time when someone gets in or out. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="0.1", Units="s"))
	float DoorCycleTime = 0.8f;

	/** The occupant steps in (or out) over this time while the door is open, turning to face out. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="0.05", Units="s"))
	float StepTime = 0.5f;

	/** Delay after the door starts opening before the occupant moves. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(ClampMin="0", Units="s"))
	float StepDelay = 0.12f;

	/** Inside point (capsule centre), relative to the locker. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(MakeEditWidget=true))
	FVector InsideOffset = FVector(-4.f, 0.f, 96.f);

	/** Exit point, relative to the locker. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker", meta=(MakeEditWidget=true))
	FVector ExitOffset = FVector(95.f, 0.f, 96.f);

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelLeft;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelRight;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelTop;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelBottom;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> DoorHinge;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> DoorUpper;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> DoorLower;

	UPROPERTY(ReplicatedUsing=OnRep_Occupant, BlueprintReadOnly, Category="Hitmen|Locker")
	TObjectPtr<AHitmenCharacterBase> Occupant;

	virtual void OnInteracted(AActor* Interactor) override;

	UFUNCTION()
	void OnRep_Occupant();

	/** Every machine: collision, movement and view for whoever gets in / out. */
	void ApplyOccupant(AHitmenCharacterBase* Character, bool bInside);
	void StartDoorCycle();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnOccupantChanged(bool bOccupied);

	TWeakObjectPtr<AHitmenCharacterBase> AppliedOccupant;
	float DoorCycleStart = -100.f;

	/** The character stepping in or out (every machine moves it the same way). */
	TWeakObjectPtr<AHitmenCharacterBase> SteppingCharacter;
	bool bSteppingIn = false;
	FVector StepFrom = FVector::ZeroVector;
	FVector StepTo = FVector::ZeroVector;
	FRotator StepFromRotation = FRotator::ZeroRotator;
	FRotator StepToRotation = FRotator::ZeroRotator;
	void UpdateStep();
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
