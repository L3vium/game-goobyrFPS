// Hitmen — Environment Query pieces for the search (EQ_HitmenSearch, built by UHitmenEditorTools): where a searching
// guard looks next around the last known position (or a body it found)

#pragma once

#include "CoreMinimal.h"
#include "EnvironmentQuery/EnvQueryContext.h"
#include "EnvironmentQuery/EnvQueryGenerator.h"
#include "EnvironmentQuery/EnvQueryTest.h"
#include "HitmenEQS.generated.h"

/** The point the search is about: the blackboard's LastKnownLocation (the guard itself when unset). */
UCLASS(meta=(DisplayName="Hitmen Search Center"))
class HITMEN_API UEnvQueryContext_HitmenSearchCenter : public UEnvQueryContext
{
	GENERATED_BODY()

public:
	virtual void ProvideContext(FEnvQueryInstance& QueryInstance, FEnvQueryContextData& ContextData) const override;

	/** The search center of a querier (also used by the tests). */
	static FVector GetCenter(const UObject* Querier);
};

/**
 * Candidates around the search center, on the navmesh: hiding spots (lockers, body containers, tall grass, actors
 * tagged HitmenHidingSpot) seen from their front, the center itself, and random reachable points.
 */
UCLASS(meta=(DisplayName="Hitmen Search Spots"))
class HITMEN_API UEnvQueryGenerator_HitmenSearchSpots : public UEnvQueryGenerator
{
	GENERATED_BODY()

public:
	UEnvQueryGenerator_HitmenSearchSpots();
	virtual void GenerateItems(FEnvQueryInstance& QueryInstance) const override;
	virtual FText GetDescriptionTitle() const override;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen", meta=(ClampMin="100", Units="cm"))
	float SearchRadius = 1500.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen", meta=(ClampMin="0"))
	int32 MaxHidingSpots = 6;

	/** A hiding spot is checked from this far in front of it. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen", meta=(ClampMin="0", Units="cm"))
	float HidingSpotStandOff = 90.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen", meta=(ClampMin="0"))
	int32 RandomPoints = 10;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen", meta=(ClampMin="10", Units="cm"))
	float NavProjectionRadius = 200.f;
};

/** Scores 1 a spot no colleague has claimed for the search, 0 a claimed one (guards split up). */
UCLASS(meta=(DisplayName="Hitmen Search Spot Free"))
class HITMEN_API UEnvQueryTest_HitmenSearchFree : public UEnvQueryTest
{
	GENERATED_BODY()

public:
	UEnvQueryTest_HitmenSearchFree();
	virtual void RunTest(FEnvQueryInstance& QueryInstance) const override;
	virtual FText GetDescriptionTitle() const override;
};

/** Scores 1 a spot in front of a hiding spot (where someone could hide), 0 elsewhere. */
UCLASS(meta=(DisplayName="Hitmen Near Hiding Spot"))
class HITMEN_API UEnvQueryTest_HitmenNearHidingSpot : public UEnvQueryTest
{
	GENERATED_BODY()

public:
	UEnvQueryTest_HitmenNearHidingSpot();
	virtual void RunTest(FEnvQueryInstance& QueryInstance) const override;
	virtual FText GetDescriptionTitle() const override;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen", meta=(ClampMin="10", Units="cm"))
	float Reach = 160.f;
};
