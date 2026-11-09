// Hitmen — patrol route placed by level designers (points, wait times, optional usable actors)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenPatrolRoute.generated.h"

class UBillboardComponent;
class USplineComponent;

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
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual void BeginPlay() override;

	/** Points the NPC walks through (the curve samples when Curved). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Patrol")
	int32 GetPointCount() const { return GetPath().Num(); }

	UFUNCTION(BlueprintPure, Category="Hitmen|Patrol")
	FVector GetPointWorldLocation(int32 Index) const;

	const FHitmenPatrolPoint* GetPoint(int32 Index) const { return GetPath().IsValidIndex(Index) ? &GetPath()[Index] : nullptr; }

protected:
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UBillboardComponent> Billboard;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol")
	TArray<FHitmenPatrolPoint> Points;

	/** Walk a smooth curve through the points instead of straight lines (the spline shows it). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol")
	bool bCurved = false;

	/** The route goes back to the first point after the last one. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol")
	bool bClosedLoop = true;

	/** Distance between two steps along the curve. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol", meta=(ClampMin="50", Units="cm", EditCondition="bCurved"))
	float CurveStep = 220.f;

	/** Visualises the route (curved or not); rebuilt from Points. */
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<USplineComponent> Spline;

	/** Curve samples (relative): key points keep their wait time / use actor, in-between steps do not wait. */
	UPROPERTY(Transient)
	TArray<FHitmenPatrolPoint> CurvedPath;

	void RebuildPath();
	const TArray<FHitmenPatrolPoint>& GetPath() const { return bCurved && CurvedPath.Num() > 0 ? CurvedPath : Points; }
};
