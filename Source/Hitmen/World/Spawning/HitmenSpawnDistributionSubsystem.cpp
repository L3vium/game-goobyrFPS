// Hitmen — spawn distribution

#include "World/Spawning/HitmenSpawnDistributionSubsystem.h"
#include "GameFramework/Actor.h"

TArray<AActor*> UHitmenSpawnDistributionSubsystem::SelectSpread(const TArray<AActor*>& Candidates, int32 Count, float Randomness, FRandomStream& Stream)
{
	TArray<AActor*> Pool;
	for (AActor* Candidate : Candidates)
	{
		// Skip nulls and duplicates (a duplicate would be picked twice and break the count).
		if (IsValid(Candidate))
		{
			Pool.AddUnique(Candidate);
		}
	}
	TArray<AActor*> Selected;
	Count = FMath::Clamp(Count, 0, Pool.Num());
	if (Count == 0)
	{
		return Selected;
	}

	Selected.Add(Pool[Stream.RandRange(0, Pool.Num() - 1)]);
	Pool.Remove(Selected[0]);

	while (Selected.Num() < Count && Pool.Num() > 0)
	{
		// Score = distance to the nearest already selected point. Linear distance (not squared):
		// the Randomness band below is a fraction of the best distance, as documented.
		TArray<TPair<AActor*, float>> Scored;
		float MaxScore = 0.f;
		for (AActor* Candidate : Pool)
		{
			float NearestSq = TNumericLimits<float>::Max();
			for (const AActor* Picked : Selected)
			{
				NearestSq = FMath::Min(NearestSq, FVector::DistSquared(Candidate->GetActorLocation(), Picked->GetActorLocation()));
			}
			const float Nearest = FMath::Sqrt(NearestSq);
			Scored.Emplace(Candidate, Nearest);
			MaxScore = FMath::Max(MaxScore, Nearest);
		}
		// Keep the candidates within the top band, then pick randomly inside it.
		const float Threshold = MaxScore * (1.f - FMath::Clamp(Randomness, 0.f, 1.f));
		TArray<AActor*> Band;
		for (const TPair<AActor*, float>& Pair : Scored)
		{
			if (Pair.Value >= Threshold)
			{
				Band.Add(Pair.Key);
			}
		}
		AActor* Pick = Band[Stream.RandRange(0, Band.Num() - 1)];
		Selected.Add(Pick);
		Pool.Remove(Pick);
	}
	return Selected;
}
