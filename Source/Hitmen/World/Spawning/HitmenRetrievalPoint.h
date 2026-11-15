// Hitmen — retrieval point candidate: where the VIP's body is extracted (a helicopter, a car, a crate... the team sets
// the model)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Interaction/HitmenInteractable.h"
#include "HitmenRetrievalPoint.generated.h"

class UBoxComponent;
class UStaticMeshComponent;
class UCameraComponent;
class UMaterialInterface;
class AHitmenCharacterBase;

/** How the extraction leaves in the end cinematic. */
UENUM(BlueprintType)
enum class EHitmenDepartureStyle : uint8
{
	/** Stays where it is (a crate, a drop point). */
	None,
	/** Rises and flies off (helicopter): parts with bSpins turn. */
	LiftOff,
	/** Drives away forward (car, van, boat). */
	DriveAway
};

/** One piece of the extraction model (built from basic shapes until the team drops its own mesh in). */
USTRUCT(BlueprintType)
struct FHitmenModelPart
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	TObjectPtr<UStaticMesh> Mesh = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	TObjectPtr<UMaterialInterface> Material = nullptr;

	/** Relative to the model root (the point's origin, X forward). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	FTransform Transform;

	/** Turns around its own Z axis while the model departs (rotors). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	bool bSpins = false;
};

/**
 * Up to N candidates are activated at match start. Once the VIP is dead every active point shows gold through walls
 * (for everyone). Carrying the VIP's body (Attack role), look at it and press F: the body is extracted, everyone
 * watches the extraction leave (CinematicCamera, DepartureStyle) and the match ends.
 *
 * The model: ModelParts (meshes, materials, transforms; built from basic shapes by the content builder) under Model;
 * the team replaces them with their own mesh (one part) or adds components under Model in the Blueprint.
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenRetrievalPoint : public AActor, public IHitmenInteractable
{
	GENERATED_BODY()

public:
	AHitmenRetrievalPoint();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Retrieval")
	void SetActive(bool bInActive);

	UFUNCTION(BlueprintPure, Category="Hitmen|Retrieval")
	bool IsActive() const { return bActive; }

	// IHitmenInteractable
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionInfo_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

	/** Every machine: the extraction leaves (the end cinematic plays on it). */
	void PlayDeparture();

	float GetCinematicDuration() const { return CinematicDuration; }

	/** Gold through walls (local render state; the VIP tracker turns it on once the VIP is dead). */
	void SetMarkerHighlight(bool bEnabled);

protected:
	/** Interaction volume (blocks only the interaction trace) and the point's origin. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> DeliveryZone;

	/** The landing pad / marking on the ground. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	/** Root of the extraction model (moves when it departs). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Model;

	/** The end cinematic's camera (everyone's view blends to it on extraction). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCameraComponent> CinematicCamera;

	/** Shows the point on the minimap and the pause map while it is active. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UHitmenMapMarkerComponent> MapMarker;

	/** The extraction model, piece by piece (helicopter, car, crate...). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval|Model")
	TArray<FHitmenModelPart> ModelParts;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval|Model")
	EHitmenDepartureStyle DepartureStyle = EHitmenDepartureStyle::None;

	/** Name shown under the prompt ("Helicopter", "Car"...). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	FText DisplayName;

	/** How long everyone watches the extraction before becoming watchers. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval|Cinematic", meta=(ClampMin="1", Units="s"))
	float CinematicDuration = 6.f;

	/** Lift off: climb acceleration (cm/s^2) after the spool-up, and the rotor speed (deg/s). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval|Cinematic", meta=(ClampMin="0"))
	float DepartureAcceleration = 260.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval|Cinematic", meta=(ClampMin="0", Units="s"))
	float DepartureDelay = 1.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval|Cinematic", meta=(ClampMin="0"))
	float SpinSpeed = 1440.f;

	/** Hide the model and pad when this candidate is not selected for the match. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	bool bHideWhenInactive = true;

	UPROPERTY(ReplicatedUsing=OnRep_Active)
	bool bActive = true;

	UFUNCTION() void OnRep_Active();

	/** Part components built from ModelParts (by OnConstruction; saved with placed actors). */
	UPROPERTY()
	TArray<TObjectPtr<UStaticMeshComponent>> PartComponents;

	void BuildModel();

	TArray<TObjectPtr<UStaticMeshComponent>> SpinningParts;
	float DepartureStart = -1.f;
	float LastInteractTime = -100.f;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnActiveChanged(bool bIsActive);

	/** The extraction starts (every machine): sounds, effects of the team. */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnExtraction();
};
