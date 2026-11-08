// Hitmen — picks N of M candidates spread over the map (retrieval points, loot, VIP)

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/WorldSubsystem.h"
#include "HitmenSpawnDistributionSubsystem.generated.h"

UCLASS()
class HITMEN_API UHitmenSpawnDistributionSubsystem : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	/**
	 * Randomized farthest-point sampling: the first pick is random, each next pick maximizes
	 * the distance to the already picked ones (Randomness 0..1 blends in random choices), so
	 * points are never clustered in one corner.
	 */
	static TArray<AActor*> SelectSpread(const TArray<AActor*>& Candidates, int32 Count, float Randomness, FRandomStream& Stream);
};
