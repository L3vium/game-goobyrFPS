// Hitmen — search EQS pieces

#include "AI/HitmenEQS.h"
#include "AI/HitmenAIController.h"
#include "AI/HitmenAISubsystem.h"
#include "EnvironmentQuery/Items/EnvQueryItemType_Point.h"
#include "BehaviorTree/BlackboardComponent.h"
#include "NavigationSystem.h"
#include "GameFramework/Pawn.h"
#include "AIController.h"
#include "Engine/World.h"

// ---------------------------------------------------------------------------------------------
// Context
// ---------------------------------------------------------------------------------------------

FVector UEnvQueryContext_HitmenSearchCenter::GetCenter(const UObject* Querier)
{
	const APawn* Pawn = Cast<APawn>(Querier);
	const AAIController* Controller = Pawn ? Cast<AAIController>(Pawn->GetController()) : nullptr;
	const UBlackboardComponent* BB = Controller ? Controller->GetBlackboardComponent() : nullptr;
	if (BB)
	{
		const FVector Center = BB->GetValueAsVector(HitmenBBKeys::LastKnownLocation);
		if (FAISystem::IsValidLocation(Center) && !Center.IsNearlyZero())
		{
			return Center;
		}
	}
	return Pawn ? Pawn->GetActorLocation() : FVector::ZeroVector;
}

void UEnvQueryContext_HitmenSearchCenter::ProvideContext(FEnvQueryInstance& QueryInstance, FEnvQueryContextData& ContextData) const
{
	UEnvQueryItemType_Point::SetContextHelper(ContextData, GetCenter(QueryInstance.Owner.Get()));
}

// ---------------------------------------------------------------------------------------------
// Generator
// ---------------------------------------------------------------------------------------------

UEnvQueryGenerator_HitmenSearchSpots::UEnvQueryGenerator_HitmenSearchSpots()
{
	ItemType = UEnvQueryItemType_Point::StaticClass();
}

FText UEnvQueryGenerator_HitmenSearchSpots::GetDescriptionTitle() const
{
	return NSLOCTEXT("Hitmen", "SearchSpotsGen", "Hitmen search spots around the search center");
}

void UEnvQueryGenerator_HitmenSearchSpots::GenerateItems(FEnvQueryInstance& QueryInstance) const
{
	const UObject* Querier = QueryInstance.Owner.Get();
	UWorld* World = Querier ? Querier->GetWorld() : nullptr;
	const UNavigationSystemV1* Nav = World ? FNavigationSystem::GetCurrent<UNavigationSystemV1>(World) : nullptr;
	if (!World || !Nav)
	{
		return;
	}
	const FVector Center = UEnvQueryContext_HitmenSearchCenter::GetCenter(Querier);
	const FVector Extent(NavProjectionRadius);
	TArray<FNavLocation> Points;
	auto AddProjected = [&](const FVector& Raw)
	{
		FNavLocation Projected;
		if (Nav->ProjectPointToNavigation(Raw, Projected, Extent))
		{
			Points.Add(Projected);
		}
	};
	if (const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(World); AI && MaxHidingSpots > 0)
	{
		TArray<AActor*> Spots;
		AI->GetHidingSpotsNear(Center, SearchRadius, Spots);
		Spots.Sort([&Center](const AActor& A, const AActor& B) { return FVector::DistSquared(A.GetActorLocation(), Center) < FVector::DistSquared(B.GetActorLocation(), Center); });
		for (int32 Index = 0; Index < Spots.Num() && Index < MaxHidingSpots; ++Index)
		{
			AddProjected(Spots[Index]->GetActorLocation() + Spots[Index]->GetActorForwardVector() * HidingSpotStandOff);
		}
	}
	AddProjected(Center);
	for (int32 Index = 0; Index < RandomPoints; ++Index)
	{
		FNavLocation Random;
		if (Nav->GetRandomReachablePointInRadius(Center, SearchRadius, Random))
		{
			Points.Add(Random);
		}
	}
	for (const FNavLocation& Point : Points)
	{
		QueryInstance.AddItemData<UEnvQueryItemType_Point>(Point.Location);
	}
}

// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

UEnvQueryTest_HitmenSearchFree::UEnvQueryTest_HitmenSearchFree()
{
	Cost = EEnvTestCost::Low;
	ValidItemType = UEnvQueryItemType_Point::StaticClass();
	SetWorkOnFloatValues(false);
}

FText UEnvQueryTest_HitmenSearchFree::GetDescriptionTitle() const
{
	return NSLOCTEXT("Hitmen", "SearchFreeTest", "Not claimed by another searching guard");
}

void UEnvQueryTest_HitmenSearchFree::RunTest(FEnvQueryInstance& QueryInstance) const
{
	const UObject* Querier = QueryInstance.Owner.Get();
	const UHitmenAISubsystem* AI = Querier ? UHitmenAISubsystem::Get(Querier) : nullptr;
	BoolValue.BindData(Querier, QueryInstance.QueryID);
	const bool bWanted = BoolValue.GetValue();
	for (FEnvQueryInstance::ItemIterator It(this, QueryInstance); It; ++It)
	{
		const FVector Location = GetItemLocation(QueryInstance, It.GetIndex());
		const bool bFree = !AI || !AI->IsSearchSpotClaimed(Location);
		It.SetScore(TestPurpose, FilterType, bFree, bWanted);
	}
}

UEnvQueryTest_HitmenNearHidingSpot::UEnvQueryTest_HitmenNearHidingSpot()
{
	Cost = EEnvTestCost::Low;
	ValidItemType = UEnvQueryItemType_Point::StaticClass();
	SetWorkOnFloatValues(false);
}

FText UEnvQueryTest_HitmenNearHidingSpot::GetDescriptionTitle() const
{
	return NSLOCTEXT("Hitmen", "NearHidingTest", "In front of a hiding spot");
}

void UEnvQueryTest_HitmenNearHidingSpot::RunTest(FEnvQueryInstance& QueryInstance) const
{
	const UObject* Querier = QueryInstance.Owner.Get();
	const UHitmenAISubsystem* AI = Querier ? UHitmenAISubsystem::Get(Querier) : nullptr;
	BoolValue.BindData(Querier, QueryInstance.QueryID);
	const bool bWanted = BoolValue.GetValue();
	for (FEnvQueryInstance::ItemIterator It(this, QueryInstance); It; ++It)
	{
		const FVector Location = GetItemLocation(QueryInstance, It.GetIndex());
		TArray<AActor*> Spots;
		if (AI)
		{
			AI->GetHidingSpotsNear(Location, Reach, Spots);
		}
		It.SetScore(TestPurpose, FilterType, Spots.Num() > 0, bWanted);
	}
}
