// Hitmen — patrol route

#include "AI/HitmenPatrolRoute.h"
#include "Components/BillboardComponent.h"

AHitmenPatrolRoute::AHitmenPatrolRoute()
{
	PrimaryActorTick.bCanEverTick = false;
	Billboard = CreateDefaultSubobject<UBillboardComponent>(TEXT("Billboard"));
	SetRootComponent(Billboard);
	bIsEditorOnlyActor = false;
}

FVector AHitmenPatrolRoute::GetPointWorldLocation(int32 Index) const
{
	return Points.IsValidIndex(Index) ? GetActorTransform().TransformPosition(Points[Index].Location) : GetActorLocation();
}
