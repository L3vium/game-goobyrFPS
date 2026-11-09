// Hitmen — patrol route

#include "AI/HitmenPatrolRoute.h"
#include "Components/BillboardComponent.h"
#include "Components/SplineComponent.h"

AHitmenPatrolRoute::AHitmenPatrolRoute()
{
	PrimaryActorTick.bCanEverTick = false;
	Billboard = CreateDefaultSubobject<UBillboardComponent>(TEXT("Billboard"));
	SetRootComponent(Billboard);
	Spline = CreateDefaultSubobject<USplineComponent>(TEXT("Spline"));
	Spline->SetupAttachment(Billboard);
	Spline->SetHiddenInGame(true);
	Spline->bDrawDebug = true;
}

void AHitmenPatrolRoute::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	RebuildPath();
}

void AHitmenPatrolRoute::BeginPlay()
{
	Super::BeginPlay();
	RebuildPath();
}

void AHitmenPatrolRoute::RebuildPath()
{
	Spline->ClearSplinePoints(false);
	for (const FHitmenPatrolPoint& Point : Points)
	{
		Spline->AddSplinePoint(Point.Location, ESplineCoordinateSpace::Local, false);
	}
	const ESplinePointType::Type Type = bCurved ? ESplinePointType::Curve : ESplinePointType::Linear;
	for (int32 Index = 0; Index < Points.Num(); ++Index)
	{
		Spline->SetSplinePointType(Index, Type, false);
	}
	Spline->SetClosedLoop(bClosedLoop && Points.Num() > 2, false);
	Spline->UpdateSpline();

	CurvedPath.Reset();
	if (!bCurved || Points.Num() < 2)
	{
		return;
	}
	// Sample each segment; the segment's end key point keeps its wait time and use actor.
	const int32 Segments = Spline->IsClosedLoop() ? Points.Num() : Points.Num() - 1;
	for (int32 Segment = 0; Segment < Segments; ++Segment)
	{
		const float Start = Spline->GetDistanceAlongSplineAtSplinePoint(Segment);
		const float End = Segment + 1 < Points.Num() ? Spline->GetDistanceAlongSplineAtSplinePoint(Segment + 1) : Spline->GetSplineLength();
		const int32 Steps = FMath::Max(1, FMath::CeilToInt((End - Start) / CurveStep));
		for (int32 Step = 1; Step <= Steps; ++Step)
		{
			const float Distance = FMath::Lerp(Start, End, static_cast<float>(Step) / Steps);
			FHitmenPatrolPoint Sample;
			Sample.Location = Spline->GetLocationAtDistanceAlongSpline(Distance, ESplineCoordinateSpace::Local);
			Sample.WaitTime = 0.f;
			if (Step == Steps)
			{
				const FHitmenPatrolPoint& Key = Points[(Segment + 1) % Points.Num()];
				Sample.WaitTime = Key.WaitTime;
				Sample.UseActor = Key.UseActor;
			}
			CurvedPath.Add(Sample);
		}
	}
}

FVector AHitmenPatrolRoute::GetPointWorldLocation(int32 Index) const
{
	const TArray<FHitmenPatrolPoint>& Path = GetPath();
	return Path.IsValidIndex(Index) ? GetActorTransform().TransformPosition(Path[Index].Location) : GetActorLocation();
}
