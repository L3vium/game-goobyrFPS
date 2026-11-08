// Hitmen — footsteps: per-surface sound/VFX (juice) and gait-based noise for the AI

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Chaos/ChaosEngineInterface.h"
#include "HitmenFootstepComponent.generated.h"

class USoundBase;
class UNiagaraSystem;

USTRUCT(BlueprintType)
struct FHitmenSurfaceFeedback
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	TObjectPtr<USoundBase> Sound = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	TObjectPtr<UNiagaraSystem> Effect = nullptr;

	/** Multiplies the noise radius heard by NPCs (sand quieter, metal louder). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep", meta=(ClampMin="0"))
	float NoiseMultiplier = 1.f;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FOnHitmenFootstep, TEnumAsByte<EPhysicalSurface>, Surface, FVector, Location, float, NoiseRadius);

/**
 * Call HandleFootstep from the AN_Footstep anim notify. Until animations have notifies,
 * a distance-based fallback fires steps from movement so the system works out of the box.
 */
UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenFootstepComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenFootstepComponent();

	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Footstep")
	FOnHitmenFootstep OnFootstep;

	UFUNCTION(BlueprintCallable, Category="Hitmen|Footstep")
	void HandleFootstep(FName FootBone = NAME_None);

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	TMap<TEnumAsByte<EPhysicalSurface>, FHitmenSurfaceFeedback> SurfaceFeedback;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	FHitmenSurfaceFeedback DefaultFeedback;

	/** Fire steps from travelled distance when no anim notify arrived recently. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	bool bUseDistanceFallback = true;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="10", Units="cm"))
	float WalkStrideLength = 140.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="10", Units="cm"))
	float SprintStrideLength = 190.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="10", Units="cm"))
	float CrouchStrideLength = 90.f;

	/** Only players make noise for NPCs (NPC steps are purely cosmetic). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	bool bReportNoiseToAI = true;

	float DistanceAccumulator = 0.f;
	float LastNotifyTime = -100.f;
	FVector LastLocation = FVector::ZeroVector;

	float ComputeNoiseRadius(float SurfaceMultiplier) const;
};
