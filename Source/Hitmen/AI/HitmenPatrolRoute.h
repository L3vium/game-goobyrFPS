// Hitmen — patrol route placed by level designers (points, wait times, optional usable actors)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenPatrolRoute.generated.h"

class UBillboardComponent;

USTRUCT(BlueprintType)
struct FHitmenPatrolPoint
{
	GENERATED_BODY()

	/** Relative to the route actor; drag the widget in the viewport. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol", meta=(MakeEditWidget=true))
	FVector Location = FVector::ZeroVector;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol", meta=(ClampMin="0"))
	float WaitTime = 2.f;

	/** Something the NPC uses at this point (drink, terminal...). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol")
	TObjectPtr<AActor> UseActor = nullptr;
};

UCLASS(Blueprintable)
class HITMEN_API AHitmenPatrolRoute : public AActor
{
	GENERATED_BODY()

public:
	AHitmenPatrolRoute();

	UFUNCTION(BlueprintPure, Category="Hitmen|Patrol")
	int32 GetPointCount() const { return Points.Num(); }

	UFUNCTION(BlueprintPure, Category="Hitmen|Patrol")
	FVector GetPointWorldLocation(int32 Index) const;

	const FHitmenPatrolPoint* GetPoint(int32 Index) const { return Points.IsValidIndex(Index) ? &Points[Index] : nullptr; }

protected:
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UBillboardComponent> Billboard;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol")
	TArray<FHitmenPatrolPoint> Points;
};
