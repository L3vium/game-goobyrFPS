// Hitmen — contract for everything usable with F (doors, switches, pickups, bodies, vehicles)

#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "HitmenInteractable.generated.h"

UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenInteractable : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenInteractable
{
	GENERATED_BODY()

public:
	/** Checked on the client for the prompt and again on the server before Interact. */
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|Interaction")
	bool CanInteract(AActor* Interactor) const;

	/** Centre-screen prompt ("Open", "Swap", "Hack"...). */
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|Interaction")
	FText GetInteractionText(AActor* Interactor) const;

	/** Server only. */
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|Interaction")
	void Interact(AActor* Interactor);

	/** Seconds F must be held (hacking a panel...): 0 = instant. A bar fills under the crosshair. */
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|Interaction")
	float GetInteractHoldTime(AActor* Interactor) const;
	virtual float GetInteractHoldTime_Implementation(AActor* Interactor) const { return 0.f; }
};
