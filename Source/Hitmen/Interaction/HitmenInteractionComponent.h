// Hitmen — focus trace, centre-screen prompt, F routing (stealth kill > interactable > drop/throw)

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenInteractionComponent.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenFocusChanged, AActor*, FocusedActor, const FText&, Prompt);

class UMaterialInterface;

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

	/**
	 * Adds (or removes) an overlay material on every mesh of an actor: the white fresnel shown on
	 * whatever you look at, and on remote-ability targets. Local, cosmetic.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Interaction")
	static void SetActorHighlight(AActor* Actor, UMaterialInterface* OverlayMaterial, bool bEnabled);

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

	/** Fresnel overlay on the focused actor (M_FocusFresnel). Empty = no highlight. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Interaction")
	TObjectPtr<UMaterialInterface> FocusOverlayMaterial;

	UFUNCTION(Server, Reliable)
	void Server_Interact(AActor* Target);

	void UpdateFocus();
	void SetFocus(AActor* NewFocus, const FText& Prompt);

	void StartFocusTrace();
	void StopFocusTrace();

	TWeakObjectPtr<AActor> FocusedActor;
	TWeakObjectPtr<AActor> HighlightedActor;
	FText FocusedPrompt;

	/** Time the trace first lost the focused interactable: it stays focused a moment (no flicker on thin frames). */
	float FocusLostTime = -1.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Interaction", meta=(ClampMin="0", Units="s"))
	float FocusGraceTime = 0.2f;
	FTimerHandle TraceTimer;
};
