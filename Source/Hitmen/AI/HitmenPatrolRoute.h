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

	/** Open routes are walked back and forth. */
	bool IsClosedLoop() const { return bClosedLoop; }

	/** Server: the NPCs that walk this route (assigned here, or that name it as their PatrolRoute). */
	TArray<class AHitmenNPC*> GatherWalkers() const;

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

	/**
	 * NPCs that walk this route (they need no PatrolRoute of their own). With bAssignedNPCsPatrolAsGroup, every NPC on
	 * this route walks as one group: the first leads along the route, the others follow in formation.
	 */
	UPROPERTY(EditInstanceOnly, BlueprintReadOnly, Category="Hitmen|Patrol|Group")
	TArray<TObjectPtr<class AHitmenNPC>> AssignedNPCs;

	/** NPCs sharing this route patrol together (a group of 3, 4...) instead of each on its own. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol|Group")
	bool bAssignedNPCsPatrolAsGroup = true;

	/** Formation: distance between rows behind the leader, side spacing, followers per row. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol|Group", meta=(ClampMin="50", Units="cm"))
	float GroupFollowDistance = 170.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol|Group", meta=(ClampMin="0", Units="cm"))
	float GroupFollowSpacing = 140.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Patrol|Group", meta=(ClampMin="1", ClampMax="4"))
	int32 GroupColumns = 2;

	/** The group made at BeginPlay (null when the route is walked alone). */
	UPROPERTY(Transient)
	TObjectPtr<class AHitmenGuardGroup> RouteGroup;

	void SetUpWalkers();

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
