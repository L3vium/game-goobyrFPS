// Hitmen — Sightroom (Tab): a fast scan wave around you, then the whole map in the scan look; enemies through
// walls everywhere, interactables near you in line of sight, the target in red at any distance. Local only.

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
	/** Reach of the scan wave drawn around you. Enemies inside it light up as the wave passes them; when it ends,
	 *  the whole map fades into the scan look and every enemy lights up (bEnemiesMapWide). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float EnemyRadius = 2500.f;

	/** When the wave ends, enemies anywhere on the map are shown (otherwise only those inside EnemyRadius). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom")
	bool bEnemiesMapWide = true;

	/** Height counts this much in the wave distance (horizontal distance + |height| x this), in C++ and in
	 *  PP_Sightroom ("HeightWeight"), so a highlight appears exactly when the wave front reaches it. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0"))
	float WaveHeightWeight = 0.35f;

	/** Interactables inside this radius are shown if directly visible. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float InteractableRadius = 1800.f;

	/** Seconds for the scan wave to reach full radius. Highlights follow the wave every frame meanwhile. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0.05", Units="s"))
	float ScanDuration = 0.35f;

	/** Seconds between highlight refreshes once the wave is done (moving enemies, line of sight). */
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
	bool HasLineOfSight(const FVector& From, const AActor* Target) const;
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
