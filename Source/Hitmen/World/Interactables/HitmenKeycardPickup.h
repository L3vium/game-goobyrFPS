// Hitmen — keycard lying in the world or dropped by an NPC

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "HitmenKeycardPickup.generated.h"

UCLASS(Blueprintable)
class HITMEN_API AHitmenKeycardPickup : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenKeycardPickup();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Keycard")
	void SetKeycardId(int32 InId) { KeycardId = InId; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Keycard")
	int32 GetKeycardId() const { return KeycardId; }

	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;

protected:
	/** Level of the keycard (doors with the same id open). */
	UPROPERTY(EditAnywhere, Replicated, BlueprintReadOnly, Category="Hitmen|Keycard", meta=(ClampMin="0"))
	int32 KeycardId = 0;

	virtual void OnInteracted(AActor* Interactor) override;
};
