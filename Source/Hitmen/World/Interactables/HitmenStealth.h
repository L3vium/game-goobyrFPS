// Hitmen — stealth fixtures: lockers, tall grass, noisy distractions (radio)

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "World/Interactables/HitmenHidingSpot.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenStealth.generated.h"

class UBoxComponent;
class AHitmenCharacterBase;

/**
 * A locker: hide inside standing, looking out through the slit of the door (F again to get out); carrying a body, F
 * stows it (one body by default). Hiding, the view arc and the bodies are AHitmenHidingSpot's.
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenLocker : public AHitmenHidingSpot
{
	GENERATED_BODY()

public:
	AHitmenLocker();
	virtual void OnConstruction(const FTransform& Transform) override;

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

	/** Door hinged on the front-right edge (the default since 0.7.0); off = front-left. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Locker")
	bool bDoorHingeOnRight = true;

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

	virtual void ApplyOpenAlpha(float Alpha) override;
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
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Distraction")
	bool IsOn() const { return bOn; }

	/** Server: turns it on or off (players with F or remotely, guards switch it off). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Distraction")
	void SetOn(bool bNewOn, AActor* SwitchedBy);

protected:
	/** Guards within this radius hear it (walls cut it, the nearest two come). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction", meta=(ClampMin="0", Units="cm"))
	float NoiseRadius = 1500.f;

	/** Seconds between two uses. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction", meta=(ClampMin="0"))
	float Cooldown = 1.f;

	/** Once on, it plays until someone turns it off (0) or this long. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction", meta=(ClampMin="0", Units="s"))
	float PlayTime = 0.f;

	/** A guard who came to look and stands this close switches it off. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction", meta=(ClampMin="0", Units="cm"))
	float SwitchOffRadius = 180.f;

	/** What it is: sound loop, effect on each pulse, light colour (radio, TV, alarm clock...). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction|Juice")
	TObjectPtr<USoundBase> LoopSound;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction|Juice")
	TObjectPtr<class UNiagaraSystem> PulseFX;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction|Juice")
	FLinearColor LightColor = FLinearColor(1.f, 0.55f, 0.15f);

	UPROPERTY(ReplicatedUsing=OnRep_On)
	bool bOn = false;

	UFUNCTION()
	void OnRep_On();

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction", meta=(ClampMin="0.2", Units="s"))
	float PulseInterval = 1.f;

	/** Glows while it plays (Juice: colour and intensity). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UPointLightComponent> PlayingLight;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Distraction|Juice", meta=(ClampMin="0"))
	float PlayingLightIntensity = 3000.f;

	/** The radio loop (sound set RadioLoop) while it plays. */
	UPROPERTY(Transient)
	TObjectPtr<class UAudioComponent> PlayingAudio;

	float PlayingUntil = -1.f;
	FTimerHandle PulseTimer;
	void Pulse();
	virtual void Tick(float DeltaSeconds) override;

	virtual void OnInteracted(AActor* Interactor) override;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnTriggered();

	float LastTriggerTime = -1000.f;
};
