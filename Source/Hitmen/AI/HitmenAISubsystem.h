// Hitmen — AI coordination: registry, graded/occluded/capped hearing, kill witnesses,
// reinforcement calls, flanking slots (angle + floor aware), search spot claims, last-known-position
// marker ref-count, smoke and blackout sight blockers, UI-facing alert events and snapshots.

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/WorldSubsystem.h"
#include "Core/HitmenTypes.h"
#include "HitmenAISubsystem.generated.h"

class UHitmenAlertComponent;
class UHitmenMatchRules;
class AHitmenCharacterBase;
class AHitmenSmokeCloud;
class AHitmenPlayerController;
class APlayerState;

/** Something that can make an area dark (circuit breaker blackout volumes). */
UINTERFACE(MinimalAPI, meta=(CannotImplementInterfaceInBlueprint))
class UHitmenDarkZone : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenDarkZone
{
	GENERATED_BODY()

public:
	virtual bool IsLocationDark(const FVector& Location) const = 0;
};

/** Lightweight per-NPC view for the minimap, crosshair arcs and other widgets. */
USTRUCT(BlueprintType)
struct FHitmenAlertSnapshot
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|AI")
	FVector Location = FVector::ZeroVector;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|AI")
	EHitmenAlertState AlertState = EHitmenAlertState::Unaware;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|AI")
	bool bCallingReinforcements = false;

	/** 0..1 white fill. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|AI")
	float Suspicion = 0.f;

	/** Player the NPC is aware of (null for NPC targets or none). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|AI")
	TObjectPtr<APlayerState> TargetPlayerState = nullptr;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FOnHitmenAIAlertChanged, FVector, NPCLocation, EHitmenAlertState, AlertState, APlayerState*, TargetPlayerState);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenAIReinforcementCallChanged, FVector, NPCLocation, bool, bCalling);

UCLASS()
class HITMEN_API UHitmenAISubsystem : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	static UHitmenAISubsystem* Get(const UObject* WorldContext);

	/** Match rules of the running match (server), or the code defaults (clients, no game mode). */
	static const UHitmenMatchRules& GetRules(const UObject* WorldContext);

	virtual void OnWorldBeginPlay(UWorld& InWorld) override;

	// ---- Registry ----
	static void RegisterAlert(UHitmenAlertComponent* Alert);
	static void UnregisterAlert(UHitmenAlertComponent* Alert);
	static void RegisterSmoke(AHitmenSmokeCloud* Smoke);
	static void UnregisterSmoke(AHitmenSmokeCloud* Smoke);
	static void RegisterDarkZone(UObject* Zone);
	static void UnregisterDarkZone(UObject* Zone);

	/** Places a player could hide (tall grass, tagged props). Lockers come from the scan registry;
	 *  actors present at begin play (lockers, tall grass, actors tagged HidingSpotTag) are found automatically. */
	static void RegisterHidingSpot(AActor* Spot);
	static void UnregisterHidingSpot(AActor* Spot);

	/** Actor tag that marks any placed actor as a hiding spot for NPC searches. */
	static const FName HidingSpotTag;

	UFUNCTION(BlueprintPure, Category="Hitmen|AI")
	TArray<UHitmenAlertComponent*> GetAlertComponents() const;

	// ---- UI (all machines, no hard references from widgets to NPCs) ----
	/** Any NPC changed alert state (replicated: fires on clients too). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|AI")
	FOnHitmenAIAlertChanged OnAlertChanged;

	/** An NPC started / stopped a reinforcement call (icon above its head). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|AI")
	FOnHitmenAIReinforcementCallChanged OnReinforcementCallChanged;

	/** One entry per living NPC (and charmed player). bOnlyAlerted skips Unaware ones. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|AI")
	TArray<FHitmenAlertSnapshot> GetAlertSnapshots(bool bOnlyAlerted = false) const;

	/** Called by alert components (OnRep paths) to feed the delegates above. */
	void NotifyAlertChanged(const UHitmenAlertComponent* Alert);
	void NotifyCallingChanged(const UHitmenAlertComponent* Alert);

	// ---- Stimuli (server) ----
	/** Graded, occluded, only the N nearest NPCs that can hear react (MaxNoiseResponders, per type). */
	static void ReportNoise(const UObject* WorldContext, const FVector& Location, float Radius, AActor* Instigator, EHitmenNoiseType Type);

	/** NPCs near the victim that really see the killer go straight to combat; those that only see the
	 *  victim investigate the body. Uses the same sight test as the AI controller. */
	static void ReportKill(const UObject* WorldContext, AHitmenCharacterBase* Victim, AActor* Killer);

	/** A caller (NPC or camera) alerts nearby NPCs toward a location / target. */
	void CallReinforcements(AActor* Caller, const FVector& Location, AActor* Target);

	// ---- Sight ----
	bool IsSightBlocked(const FVector& Start, const FVector& End) const;
	bool IsLocationDark(const FVector& Location) const;

	/**
	 * Shared NPC sight test: range (0 = unlimited), view cone (>= 180 = all around), occlusion trace,
	 * smoke, darkness at either end, and the target's hidden status (invisible, locker, tall grass).
	 * Dead targets ignore the hidden status (bodies are props).
	 */
	bool CanObserverSee(const APawn* Observer, const AActor* Target, float MaxRange, float HalfAngleDegrees, bool bTraceOcclusion) const;

	// ---- Squad tactics ----
	/**
	 * A position around the target, spread between all NPCs engaging it. Angle slots (front, left,
	 * right, wide, behind) are probed on several floors: when the fight spans levels, the first
	 * flanker pushes directly and the second takes a slot on another floor / route (stairs).
	 * Snipers get their own far slots and prefer a line of sight.
	 */
	FVector GetFlankLocation(const AActor* NPC, const AActor* Target, float PreferredDistance) const;

	// ---- Search coordination ----
	/** Hiding spots (actors) within Radius of Center. */
	void GetHidingSpotsNear(const FVector& Center, float Radius, TArray<AActor*>& OutSpots) const;

	/** Marks a spot as searched by Claimer (other NPCs avoid it for SearchSpotClaimLifetime). */
	void ClaimSearchSpot(const AActor* Claimer, const FVector& Location);

	/** True when a claim (any NPC, Claimer included = already visited) lies within SearchSpotClaimRadius. */
	bool IsSearchSpotClaimed(const FVector& Location) const;

	// ---- Last known position marker (server) ----
	/** Alert reports which target it is searching (null = none). The target's marker is shown while
	 *  at least one NPC searches it, hidden at zero, never for dead targets. */
	void UpdateSearcher(UHitmenAlertComponent* Alert, AActor* SearchedTarget, const FVector& LastKnownLocation);

private:
	struct FSearchClaim
	{
		TWeakObjectPtr<const AActor> Claimer;
		FVector Location = FVector::ZeroVector;
		float Time = 0.f;
	};

	struct FSearchMarker
	{
		TSet<TWeakObjectPtr<UHitmenAlertComponent>> Searchers;
		TWeakObjectPtr<AHitmenPlayerController> Controller;
		FVector Location = FVector::ZeroVector;
		FVector SentLocation = FVector::ZeroVector;
		bool bShown = false;
	};

	TArray<TWeakObjectPtr<UHitmenAlertComponent>> Alerts;
	TArray<TWeakObjectPtr<AHitmenSmokeCloud>> Smokes;
	TArray<TWeakObjectPtr<UObject>> DarkZones;
	TArray<TWeakObjectPtr<AActor>> HidingSpots;
	mutable TArray<FSearchClaim> SearchClaims;
	TMap<TWeakObjectPtr<AActor>, FSearchMarker> SearchMarkers;
	TMap<TWeakObjectPtr<UHitmenAlertComponent>, TWeakObjectPtr<AActor>> SearcherTargets;

	bool HasLineOfSight(const FVector& From, const FVector& To, const AActor* IgnoreA, const AActor* IgnoreB) const;
	void RefreshMarker(const TWeakObjectPtr<AActor>& Key);
	void PruneClaims() const;
};
