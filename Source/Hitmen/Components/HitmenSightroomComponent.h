// Hitmen — Sightroom (Tab): scan sweep, enemies through walls in a radius, interactables in
// line of sight, the target in red at any distance. Local only.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenSightroomComponent.generated.h"

class UMaterialInterface;
class UMaterialInstanceDynamic;
class UCameraComponent;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSightroomChanged, bool, bActive);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenSightroomComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenSightroomComponent();

	virtual void BeginPlay() override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Sightroom")
	FOnHitmenSightroomChanged OnSightroomChanged;

	UFUNCTION(BlueprintCallable, Category="Hitmen|Sightroom")
	void SetSightroomActive(bool bActive);

	UFUNCTION(BlueprintPure, Category="Hitmen|Sightroom")
	bool IsSightroomActive() const { return bSightroomActive; }

	/** Radii come from the player controller (Hitmen|Sightroom) when it possesses the pawn. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Sightroom")
	void SetScanRadii(float InEnemyRadius, float InInteractableRadius) { EnemyRadius = FMath::Max(0.f, InEnemyRadius); InteractableRadius = FMath::Max(0.f, InInteractableRadius); }

	/** 0..1 progress of the expanding scan wave. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Sightroom")
	float GetScanProgress() const { return ScanProgress; }

protected:
	/** Enemies inside this radius are shown through walls. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float EnemyRadius = 2500.f;

	/** Interactables inside this radius are shown if directly visible. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float InteractableRadius = 1800.f;

	/** Seconds for the scan wave to reach full radius. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0.05", Units="s"))
	float ScanDuration = 0.8f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0.02", Units="s"))
	float RefreshInterval = 0.15f;

	/** Post-process material reading custom depth stencils (PP_Sightroom). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom|Juice")
	TObjectPtr<UMaterialInterface> PostProcessMaterial;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> PostProcessInstance;

	UPROPERTY(Transient)
	TArray<TWeakObjectPtr<AActor>> Highlighted;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSightroomChanged(bool bActive);

	void Refresh();
	void ClearHighlights();
	UCameraComponent* GetCamera() const;
	bool HasLineOfSight(const FVector& From, const AActor* Target) const;

	bool bSightroomActive = false;
	float ScanProgress = 0.f;
	float RefreshAccumulator = 0.f;
};
