// Hitmen — Sightroom (Tab): a fast scan wave around you, then the whole map in the scan look; enemies through
// walls near you, interactables near you where you really see them, the target in red at any distance. Local only.
// C++ only keeps custom depth on the candidates (a little beyond the radii); PP_Sightroom decides per pixel from your
// current position, the wave front and the depth test, with soft edges, so nothing pops in or out.

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

	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

protected:
	/** Like Hitman's Instinct: enemies are seen through walls only this close (they light up as the wave passes
	 *  them). The target is seen at any distance. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float EnemyRadius = 2000.f;

	/** Enemies anywhere on the map once the wave ends (off: only those inside EnemyRadius). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom")
	bool bEnemiesMapWide = false;

	/** How far the drawn wave travels; behind it, and everywhere once it ends, the map takes the cool scan look. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float ScanVisualRadius = 9000.f;

	/** Height counts this much in the wave distance (horizontal distance + |height| x this), in C++ and in
	 *  PP_Sightroom ("HeightWeight"), so a highlight appears exactly when the wave front reaches it. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0"))
	float WaveHeightWeight = 0.35f;

	/** Interactables inside this radius are shown where directly visible (depth test per pixel in PP_Sightroom). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float InteractableRadius = 1800.f;

	/** Highlights fade in over this distance inside EnemyRadius / InteractableRadius (and behind the wave front), so an
	 *  enemy walking into range or an object you walk toward appears progressively. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="1", Units="cm"))
	float HighlightSoftEdge = 350.f;

	/** Candidates keep custom depth this far beyond their radius (the shader fades them; covers the refresh delay). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float CandidateMargin = 400.f;

	/** Seconds for the scan wave to reach full radius (the shader shows highlights as the front reaches them). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0.05", Units="s"))
	float ScanDuration = 0.5f;

	/** Seconds between refreshes of the candidate set (who keeps custom depth); the look itself updates every frame. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0.02", Units="s"))
	float RefreshInterval = 0.1f;

	/** Seconds for the world to fade into / out of the scan look. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0.01", Units="s"))
	float FadeDuration = 0.35f;

	/** Post-process material reading custom depth stencils (PP_Sightroom). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom|Juice")
	TObjectPtr<UMaterialInterface> PostProcessMaterial;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> PostProcessInstance;

	/** Highlighted actors and the stencil each was given (only changes touch the render state). Weak, not GC roots. */
	TMap<TWeakObjectPtr<AActor>, int32> Highlighted;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSightroomChanged(bool bActive);

	void Refresh();
	void ClearHighlights();
	UCameraComponent* GetCamera() const;
	/** Wave radius now (eased like the drawn wave). */
	float GetWaveRadius() const;
	/** Distance used by the wave: horizontal + |height| x WaveHeightWeight. */
	float GetWaveDistance(const FVector& Origin, const FVector& Location) const;

	bool bSightroomActive = false;
	float ScanProgress = 0.f;
	/** 0..1 blend of the post-process (fades out after release). */
	float FadeAlpha = 0.f;
	void ApplyPostProcess();
	float RefreshAccumulator = 0.f;
};
