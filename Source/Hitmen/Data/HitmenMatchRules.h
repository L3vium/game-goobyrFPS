// Hitmen — GD tool: match rules, lobby option defaults and global gameplay tuning

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Core/HitmenTypes.h"
#include "HitmenMatchRules.generated.h"

class UCurveFloat;

/** Values driven by the difficulty option. */
USTRUCT(BlueprintType)
struct FHitmenDifficultyPreset
{
	GENERATED_BODY()

	/** Weapon spawn candidates activated at match start. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Difficulty", meta=(ClampMin="0"))
	int32 WeaponSpawnCount = 6;

	/** Item (move / manipulation) spawn candidates activated at match start. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Difficulty", meta=(ClampMin="0"))
	int32 ItemSpawnCount = 6;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Difficulty", meta=(ClampMin="0"))
	float NPCSightRangeMultiplier = 1.f;

	/** How fast NPCs go from noticing to engaging. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Difficulty", meta=(ClampMin="0"))
	float NPCSuspicionRateMultiplier = 1.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Difficulty", meta=(ClampMin="0"))
	float NPCDamageMultiplier = 1.f;

	/** Extra aim error in degrees added to NPC shots. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Difficulty", meta=(ClampMin="0"))
	float NPCAimErrorDegrees = 3.f;
};

/**
 * One data asset holds every match-level number. The project default is set in
 * Project Settings -> Hitmen; a map definition may override it.
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenMatchRules : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	// ---- Lobby options (defaults + visibility) ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options")
	FHitmenBoolOption ProximityChat = { true, true };

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options")
	FHitmenIntOption RetrievalPointCount = { 3, 1, 8, true };

	/** Value is an EHitmenDifficulty index. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options")
	FHitmenIntOption Difficulty = { 1, 0, 2, true };

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Difficulty")
	TMap<EHitmenDifficulty, FHitmenDifficultyPreset> DifficultyPresets;

	// ---- Match ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Match", meta=(ClampMin="1", ClampMax="15"))
	int32 MaxVIPCandidates = 15;

	/** Seconds on the results screen before returning to the lobby. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Match", meta=(ClampMin="0"))
	float ResultsDuration = 8.f;

	/** Minimum distance between activated spawn candidates is maximized; this adds randomness (0 = strict farthest point). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Match", meta=(ClampMin="0", ClampMax="1"))
	float SpawnSpreadRandomness = 0.25f;

	// ---- VIP body / carry ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Carry", meta=(ClampMin="0.05", ClampMax="1"))
	float CarrySpeedMultiplier = 0.65f;

	/** Seconds between sonar pings when carrying starts. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sonar", meta=(ClampMin="0.1"))
	float SonarStartInterval = 12.f;

	/** Seconds between pings once the ramp is over. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sonar", meta=(ClampMin="0.1"))
	float SonarEndInterval = 2.f;

	/** Seconds of carrying to go from start to end interval. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sonar", meta=(ClampMin="0.1"))
	float SonarRampDuration = 90.f;

	/** Optional shaping of the ramp (0..1 -> 0..1). Linear when null. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sonar")
	TObjectPtr<UCurveFloat> SonarRampCurve;

	// ---- Noise ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float SprintFootstepRadius = 1200.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float WalkFootstepRadius = 350.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float CrouchFootstepRadius = 0.f;

	/** Radius multiplier when a wall blocks the line between noise and listener. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", ClampMax="1"))
	float OcclusionMultiplier = 0.45f;

	/** Only the N nearest NPCs react to one noise. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="1"))
	int32 MaxNoiseResponders = 2;

	/** Gunshot closer than this sends a hearing NPC straight to combat. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float GunshotCombatRadius = 900.f;

	// ---- AI ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI", meta=(ClampMin="0"))
	float ReinforcementCallDuration = 3.5f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI", meta=(ClampMin="0", Units="cm"))
	float ReinforcementRadius = 3000.f;

	/** Max NPCs alerted by one reinforcement call. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI", meta=(ClampMin="1"))
	int32 ReinforcementMaxResponders = 5;

	/** Seconds without seeing the target before Combat falls back to Searching. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI", meta=(ClampMin="0"))
	float LoseTargetTime = 6.f;

	/** Seconds spent searching before giving up. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI", meta=(ClampMin="0"))
	float SearchDuration = 25.f;

	/** Seconds spent investigating a point. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI", meta=(ClampMin="0"))
	float InvestigateDuration = 8.f;

	/** Distance around a kill in which a witness goes straight to combat. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI", meta=(ClampMin="0", Units="cm"))
	float WitnessKillRadius = 2000.f;

	const FHitmenDifficultyPreset& GetPreset(EHitmenDifficulty InDifficulty) const;

	/** Sonar interval after CarryTime seconds of carrying. */
	float GetSonarInterval(float CarryTime) const;

	virtual FPrimaryAssetId GetPrimaryAssetId() const override
	{
		return FPrimaryAssetId(TEXT("HitmenRules"), GetFName());
	}
};
