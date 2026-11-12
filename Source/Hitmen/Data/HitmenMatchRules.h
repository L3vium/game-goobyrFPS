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
	/** Retrieval points activated at match start (the lobby no longer edits this directly). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Difficulty", meta=(ClampMin="1"))
	int32 RetrievalPointCount = 3;

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

	/** Fallback only: the count comes from the difficulty preset. Hidden from the lobby. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options")
	FHitmenIntOption RetrievalPointCount = { 3, 1, 8, false };

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

	/** Sonar timing (seconds). Lobby options: hide one to fix its value by design. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options")
	FHitmenFloatOption SonarStartInterval = { 12.f, 2.f, 30.f, true };

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options")
	FHitmenFloatOption SonarEndInterval = { 2.f, 0.5f, 10.f, true };

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options")
	FHitmenFloatOption SonarRampDuration = { 90.f, 10.f, 300.f, true };

	/** Step used by the lobby +/- buttons for the sonar options. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Lobby Options", meta=(ClampMin="0.1"))
	float SonarLobbyStep = 1.f;

	/** Optional shaping of the ramp (0..1 -> 0..1). Linear when null. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sonar")
	TObjectPtr<UCurveFloat> SonarRampCurve;

	// ---- Noise ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float SprintFootstepRadius = 900.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float WalkFootstepRadius = 400.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float CrouchFootstepRadius = 0.f;

	/** Radius multiplier for each wall between the noise and the listener (two walls: squared...). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", ClampMax="1"))
	float OcclusionMultiplier = 0.45f;

	/** Walls counted by the occlusion test; more walls than this = not heard. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="1", ClampMax="6"))
	int32 MaxOccludingWalls = 3;

	/** Only the N nearest NPCs that can hear react to one noise, for every noise type (gunshots and
	 *  explosions included). Keep it at 1-2; per-type exceptions in NoiseResponderOverrides. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="1"))
	int32 MaxNoiseResponders = 2;

	/** Hearing a hostile this close in the dark (cannot see) is enough to attack. Elsewhere hearing only
	 *  sends NPCs to the sound (or the bullet impact); only sight reveals where a shooter is. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Noise", meta=(ClampMin="0", Units="cm"))
	float DarkCloseEngageDistance = 250.f;

	// ---- NPC fire (Hitman-like: few, slow shots) ----
	/** NPC shots are this many times slower than the weapon's own rate. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Fire", meta=(ClampMin="1"))
	float NPCFireIntervalMultiplier = 2.5f;

	/** Shots per burst before a pause. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Fire", meta=(ClampMin="1"))
	int32 NPCBurstShots = 3;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Fire", meta=(ClampMin="0", Units="s"))
	float NPCBurstPause = 1.2f;

	/** Time to raise the weapon before the first shot of an engagement. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Fire", meta=(ClampMin="0", Units="s"))
	float NPCFirstShotDelay = 0.6f;

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

	// ---- AI: hearing caps, reinforcement calls, search, flanking (owned by the AI code) ----
	/** Per noise type replacement for MaxNoiseResponders (a missing type uses MaxNoiseResponders). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Hearing")
	TMap<EHitmenNoiseType, int32> NoiseResponderOverrides;

	/** Seconds between two reinforcement calls of one NPC, counted from the call start. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Reinforcements", meta=(ClampMin="0", Units="s"))
	float ReinforcementCallCooldown = 15.f;

	/** Seconds an NPC must have been alerted (yellow / red / search) before it may call. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Reinforcements", meta=(ClampMin="0", Units="s"))
	float ReinforcementCallMinAlertTime = 1.5f;

	/** Yellow NPCs investigating a real threat (sighting, body, gunfire) may call, not only searching
	 *  ones and red ones without line of sight. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Reinforcements")
	bool bInvestigatingNPCsCallReinforcements = true;

	/** NPCs alerted by a call may call in turn (off = no chain reaction across the map). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Reinforcements")
	bool bReinforcementCallsRelay = false;

	/** Seconds a searched spot stays "already searched" for every NPC. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Search", meta=(ClampMin="0", Units="s"))
	float SearchSpotClaimLifetime = 20.f;

	/** Two search spots closer than this are the same spot. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Search", meta=(ClampMin="0", Units="cm"))
	float SearchSpotClaimRadius = 300.f;

	/** Probe flank slots on other floors so one flanker takes another level / route (stairs). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Flanking")
	bool bFlankUseOtherLevels = true;

	/** Typical height between two floors of the maps. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Flanking", meta=(ClampMin="1", Units="cm"))
	float FlankFloorHeight = 300.f;

	/** Floors probed above and below the target. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Flanking", meta=(ClampMin="0", ClampMax="3"))
	int32 FlankHeightLevels = 1;

	/** Horizontal tolerance when snapping a flank slot to the navmesh. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|AI|Flanking", meta=(ClampMin="0", Units="cm"))
	float FlankProjectionRadius = 300.f;

	const FHitmenDifficultyPreset& GetPreset(EHitmenDifficulty InDifficulty) const;

	/** Sonar interval after CarryTime seconds of carrying. */
	/** Interval for the given carry time using the lobby's sonar timing. */
	float GetSonarInterval(float CarryTime, const FHitmenLobbySettings& Settings) const;

	/** Lobby settings filled with every option's default value. */
	FHitmenLobbySettings MakeDefaultLobbySettings() const;

	/** Clamps to each option's range and forces hidden options back to their designed value. */
	FHitmenLobbySettings SanitizeLobbySettings(const FHitmenLobbySettings& Requested) const;

	virtual FPrimaryAssetId GetPrimaryAssetId() const override
	{
		return FPrimaryAssetId(TEXT("HitmenRules"), GetFName());
	}
};
