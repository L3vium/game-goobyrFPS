// Hitmen — base of every world fixture: swappable mesh, prompt, sightroom highlight, juice

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Interaction/HitmenInteractable.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenInteractableBase.generated.h"

class UStaticMeshComponent;

UCLASS(Abstract, Blueprintable)
class HITMEN_API AHitmenInteractableBase : public AActor, public IHitmenInteractable, public IHitmenScannable
{
	GENERATED_BODY()

public:
	AHitmenInteractableBase();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	// IHitmenInteractable
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override { return InteractionText; }
	virtual void Interact_Implementation(AActor* Interactor) override;

	// IHitmenScannable
	virtual EHitmenScanCategory GetScanCategory_Implementation(const AActor* Observer) const override;
	virtual void SetScanHighlight_Implementation(bool bEnabled, int32 StencilValue) override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Interaction")
	void SetInteractionEnabled(bool bEnabled);

	UFUNCTION(BlueprintPure, Category="Hitmen|Interaction")
	bool IsInteractionEnabled() const { return bInteractionEnabled; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Root;

	/** Main visual; the team replaces the mesh in the BP child. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Interaction")
	FText InteractionText;

	/** Seconds before it can be used again. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Interaction", meta=(ClampMin="0"))
	float InteractionCooldown = 0.25f;

	/** Not every fixture is usable with F (ladders, grass): they still show in the Sightroom. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Interaction")
	bool bUsableWithInteract = true;

	/** Shown by the Sightroom when in line of sight. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Interaction")
	bool bShowInSightroom = true;

	UPROPERTY(ReplicatedUsing=OnRep_InteractionEnabled, EditAnywhere, BlueprintReadOnly, Category="Hitmen|Interaction")
	bool bInteractionEnabled = true;

	/** Server: the actual behaviour of the fixture. */
	virtual void OnInteracted(AActor* Interactor) {}

	UFUNCTION() virtual void OnRep_InteractionEnabled() {}

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Interacted(AActor* Interactor);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnInteracted(AActor* Interactor);

	float LastInteractTime = -100.f;
};
