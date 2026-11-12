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
 * Call HandleFootstep from the "Hitmen Footstep" anim notify (UAnimNotify_HitmenFootstep, property
 * FootBone = foot_l / foot_r). Until animations have notifies, a distance-based fallback fires
 * steps from movement so the system works out of the box; a notify suppresses the fallback.
 *
 * Surfaces come from physical materials (DefaultEngine.ini): SurfaceType1 Grass, 2 Stone, 3 Sand,
 * 4 Wood, 5 Metal. The default table has an entry for each (sound/VFX empty, noise multipliers set).
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

	/** The owner times its own steps (first-person head bob): notifies and the distance fallback stay silent. */
	void SetExternalStepTiming(bool bExternal) { bExternalStepTiming = bExternal; }

	/** A step from the external timer (same sound, noise and VFX as a notify). */
	void HandleTimedFootstep() { HandleFootstepInternal(NAME_None); }

protected:
	/** Step volume per gait (the AI noise radius is separate, in the match rules). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float WalkVolume = 0.55f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float SprintVolume = 0.9f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float CrouchVolume = 0.25f;

	/** Your own steps are played in 2D, scaled by this. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float LocalPlayerVolume = 0.6f;

	/** Random pitch around 1 (+-) so repeated steps do not sound identical. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0", ClampMax="0.5"))
	float PitchJitter = 0.06f;

	bool bExternalStepTiming = false;

	void HandleFootstepInternal(FName FootBone);

	/** Per surface type: sound, VFX and AI noise multiplier. Missing surfaces use DefaultFeedback. */
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

	/** Seconds after an anim notify step during which the distance fallback stays silent. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="0", Units="s"))
	float NotifySuppressesFallbackTime = 1.f;

	/** Ground trace: starts this far above the foot and goes this far down. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="0", Units="cm"))
	float TraceStartHeight = 20.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="0", Units="cm"))
	float TraceLength = 200.f;

	/** Only players make noise for NPCs (NPC steps are purely cosmetic). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	bool bReportNoiseToAI = true;

	float DistanceAccumulator = 0.f;
	float LastNotifyTime = -100.f;
	FVector LastLocation = FVector::ZeroVector;

	float ComputeNoiseRadius(float SurfaceMultiplier) const;
};
