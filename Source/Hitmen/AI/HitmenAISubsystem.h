// Hitmen — AI coordination: registry, graded/occluded/capped hearing, kill witnesses,
// reinforcement calls, flanking slots, smoke and blackout sight blockers

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/WorldSubsystem.h"
#include "Core/HitmenTypes.h"
#include "HitmenAISubsystem.generated.h"

class UHitmenAlertComponent;
class AHitmenCharacterBase;
class AHitmenSmokeCloud;

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

UCLASS()
class HITMEN_API UHitmenAISubsystem : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	static UHitmenAISubsystem* Get(const UObject* WorldContext);

	// ---- Registry ----
	static void RegisterAlert(UHitmenAlertComponent* Alert);
	static void UnregisterAlert(UHitmenAlertComponent* Alert);
	static void RegisterSmoke(AHitmenSmokeCloud* Smoke);
	static void UnregisterSmoke(AHitmenSmokeCloud* Smoke);
	static void RegisterDarkZone(UObject* Zone);
	static void UnregisterDarkZone(UObject* Zone);

	UFUNCTION(BlueprintPure, Category="Hitmen|AI")
	TArray<UHitmenAlertComponent*> GetAlertComponents() const;

	// ---- Stimuli (server) ----
	/** Graded, occluded, only the N nearest NPCs react. */
	static void ReportNoise(const UObject* WorldContext, const FVector& Location, float Radius, AActor* Instigator, EHitmenNoiseType Type);

	/** NPCs near the victim that see the killer go straight to combat. */
	static void ReportKill(const UObject* WorldContext, AHitmenCharacterBase* Victim, AActor* Killer);

	/** A caller (NPC or camera) alerts nearby NPCs toward a location / target. */
	void CallReinforcements(AActor* Caller, const FVector& Location, AActor* Target);

	// ---- Sight blockers ----
	bool IsSightBlocked(const FVector& Start, const FVector& End) const;
	bool IsLocationDark(const FVector& Location) const;

	// ---- Squad tactics ----
	/** A position around the target, spread between all NPCs engaging it (flank, encircle). */
	FVector GetFlankLocation(const AActor* NPC, const AActor* Target, float PreferredDistance) const;

private:
	TArray<TWeakObjectPtr<UHitmenAlertComponent>> Alerts;
	TArray<TWeakObjectPtr<AHitmenSmokeCloud>> Smokes;
	TArray<TWeakObjectPtr<UObject>> DarkZones;

	bool HasLineOfSight(const FVector& From, const FVector& To, const AActor* IgnoreA, const AActor* IgnoreB) const;
};
