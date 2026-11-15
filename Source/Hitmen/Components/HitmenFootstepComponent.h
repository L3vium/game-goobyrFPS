// Hitmen — footsteps: per-surface sound/VFX (juice) and gait-based noise for the AI

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Chaos/ChaosEngineInterface.h"
#include "Data/HitmenSoundSet.h"
#include "HitmenFootstepComponent.generated.h"

class USoundBase;
class UNiagaraSystem;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FOnHitmenFootstep, TEnumAsByte<EPhysicalSurface>, Surface, FVector, Location, float, NoiseRadius);

/**
 * Call HandleFootstep from the "Hitmen Footstep" anim notify (UAnimNotify_HitmenFootstep, property
 * FootBone = foot_l / foot_r). Until animations have notifies, a distance-based fallback fires
 * steps from movement so the system works out of the box; a notify suppresses the fallback.
 *
 * Surfaces come from physical materials (DefaultEngine.ini): SurfaceType1 Grass, 2 Stone, 3 Sand,
 * 4 Wood, 5 Metal, 6 Tile. What each sounds like is the surface table of DA_SoundSet (one place for every character);
 * SurfaceFeedback here only overrides a surface for this character when its entry has a sound.
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

	/**
	 * The ground under From (complex trace, physical material). Terrain tiles report grass / earth / rock from their
	 * slope, like their colour. Steps, landings and slides all read the surface here, so they always agree.
	 */
	bool QuerySurface(const FVector& From, FHitResult& OutHit, EPhysicalSurface& OutSurface) const;

	/** A landing on the surface under the feet: that surface's step plus a body thump (hard: heavier, louder). */
	void PlayLanding(float FallSpeed, bool bHard);

protected:
	/** Step volume per gait (the AI noise radius is separate, in the match rules). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float WalkVolume = 0.5f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float SprintVolume = 0.8f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float CrouchVolume = 0.22f;

	/** Your own steps are played in 2D, scaled by this. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0"))
	float LocalPlayerVolume = 0.55f;

	/** Landings: body thumps (one picked at random) under the surface's step. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Landing")
	TArray<TObjectPtr<USoundBase>> LandThumps;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Landing")
	TArray<TObjectPtr<USoundBase>> HardLandThumps;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Landing", meta=(ClampMin="0"))
	float LandStepVolume = 0.9f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Landing", meta=(ClampMin="0"))
	float LandThumpVolume = 0.55f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Landing", meta=(ClampMin="0"))
	float HardLandThumpVolume = 0.85f;

	/** Terrain tiles (HitmenTerrain tag): grass above this normal Z, rock below TerrainRockMaxNormalZ, earth between
	 *  (the terrain material's colour bands). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Terrain", meta=(ClampMin="0", ClampMax="1"))
	float TerrainGrassMinNormalZ = 0.86f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Terrain", meta=(ClampMin="0", ClampMax="1"))
	float TerrainRockMaxNormalZ = 0.59f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Terrain")
	TEnumAsByte<EPhysicalSurface> TerrainGrassSurface = SurfaceType1;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Terrain")
	TEnumAsByte<EPhysicalSurface> TerrainEarthSurface = SurfaceType3;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Terrain")
	TEnumAsByte<EPhysicalSurface> TerrainRockSurface = SurfaceType2;

	/** Random pitch around 1 (+-) so repeated steps do not sound identical. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep|Audio", meta=(ClampMin="0", ClampMax="0.5"))
	float PitchJitter = 0.06f;

	bool bExternalStepTiming = false;

	void HandleFootstepInternal(FName FootBone);

	/** Per-character overrides of the DA_SoundSet surface table (an entry counts only when it has a sound). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	TMap<TEnumAsByte<EPhysicalSurface>, FHitmenSurfaceFeedback> SurfaceFeedback;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	FHitmenSurfaceFeedback DefaultFeedback;

	/** Fire steps from travelled distance when no anim notify arrived recently. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep")
	bool bUseDistanceFallback = true;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="10", Units="cm"))
	float WalkStrideLength = 380.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="10", Units="cm"))
	float SprintStrideLength = 500.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Footstep", meta=(ClampMin="10", Units="cm"))
	float CrouchStrideLength = 240.f;

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
	int32 LastVariation = INDEX_NONE;

	float ComputeNoiseRadius(float SurfaceMultiplier) const;
	const FHitmenSurfaceFeedback& GetFeedback(EPhysicalSurface Surface) const;
	/** The surface puff, only when Project Settings -> Hitmen -> Juice -> bFootstepFX. */
	void SpawnStepEffect(const FHitmenSurfaceFeedback& Feedback, const FVector& Location) const;
	/** A random variation of the surface's step (never the last one played). */
	USoundBase* PickStepSound(const FHitmenSurfaceFeedback& Feedback);
	/** Your own sounds in 2D, everyone else's in the footstep sound space. */
	void PlayStepSound(USoundBase* Sound, const FVector& Location, float Volume, float Pitch) const;
};
