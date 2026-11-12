// Hitmen — a container (dumpster, chest, freezer) to hide carried bodies in

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "HitmenBodyContainer.generated.h"

class AHitmenCharacterBase;

/**
 * Carrying a body, F: the lid opens, the body goes in, the lid closes. Hidden bodies are never found by guards.
 * Holds up to Capacity bodies; the VIP is never hidden (it must be delivered).
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenBodyContainer : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenBodyContainer();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void Tick(float DeltaSeconds) override;

	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|BodyContainer")
	int32 GetHiddenCount() const { return HiddenCount; }

	UFUNCTION(BlueprintPure, Category="Hitmen|BodyContainer")
	bool IsFull() const { return HiddenCount >= Capacity; }

protected:
	/** The lid (hinged on its back edge); the team swaps the mesh in the Blueprint. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> LidPivot;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Lid;

	/** Bodies it can hold. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(ClampMin="1"))
	int32 Capacity = 2;

	/** Open, body in, closed (seconds). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(ClampMin="0.1", Units="s"))
	float HideDuration = 0.8f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(Units="deg"))
	float LidOpenAngle = 105.f;

	UPROPERTY(Replicated)
	int32 HiddenCount = 0;

	virtual void OnInteracted(AActor* Interactor) override;

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_PlayHide();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnBodyHidden(int32 Count);

	float HideStartTime = -100.f;
};
