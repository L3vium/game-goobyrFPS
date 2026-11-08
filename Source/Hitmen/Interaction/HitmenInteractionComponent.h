// Hitmen — focus trace, centre-screen prompt, F routing (stealth kill > interactable > drop/throw)

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenInteractionComponent.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenFocusChanged, AActor*, FocusedActor, const FText&, Prompt);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenInteractionComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenInteractionComponent();

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Interaction")
	FOnHitmenFocusChanged OnFocusChanged;

	/** F pressed. Returns true if something consumed it. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Interaction")
	bool TryInteract();

	UFUNCTION(BlueprintPure, Category="Hitmen|Interaction")
	AActor* GetFocusedActor() const { return FocusedActor.Get(); }

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Interaction", meta=(ClampMin="0", Units="cm"))
	float TraceDistance = 260.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Interaction", meta=(ClampMin="0", Units="cm"))
	float TraceRadius = 14.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Interaction", meta=(ClampMin="0.01"))
	float TraceInterval = 0.08f;

	/** Server tolerance on the distance check. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Interaction", meta=(ClampMin="0", Units="cm"))
	float ServerDistanceTolerance = 150.f;

	UFUNCTION(Server, Reliable)
	void Server_Interact(AActor* Target);

	void UpdateFocus();
	void SetFocus(AActor* NewFocus, const FText& Prompt);

	TWeakObjectPtr<AActor> FocusedActor;
	FText FocusedPrompt;
	FTimerHandle TraceTimer;
};
