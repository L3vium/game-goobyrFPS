// Hitmen — the agent standing in the front-end scene (title backdrop, "Your agent" screen): a
// mannequin wearing the local player's cosmetics, idling. Local only, never replicated.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Core/HitmenTypes.h"
#include "HitmenAgentPreview.generated.h"

class USkeletalMeshComponent;
class UHitmenCosmeticComponent;
class UAnimSequenceBase;

UCLASS(Blueprintable)
class HITMEN_API AHitmenAgentPreview : public AActor
{
	GENERATED_BODY()

public:
	AHitmenAgentPreview();
	virtual void BeginPlay() override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Agent")
	USkeletalMeshComponent* GetBody() const { return Body; }

	/** Shows a look on the mannequin (customization arrows apply live). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Agent")
	void ShowCosmetics(const FHitmenCosmeticLoadout& Cosmetics);

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USkeletalMeshComponent> Body;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenCosmeticComponent> Cosmetics;

	/** Looping idle (menu idles of the anim set by default). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Agent")
	TObjectPtr<UAnimSequenceBase> IdleAnimation;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnLookChanged();
};
