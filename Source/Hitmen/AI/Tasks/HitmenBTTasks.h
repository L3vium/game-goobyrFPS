// Hitmen — C++ behavior tree tasks used by BT_NPC and its subtrees

#pragma once

#include "CoreMinimal.h"
#include "BehaviorTree/BTTaskNode.h"
#include "BehaviorTree/Tasks/BTTask_BlackboardBase.h"
#include "Core/HitmenTypes.h"
#include "HitmenBTTasks.generated.h"

/** Next patrol point (or a random spot around home) into MoveLocation + WaitTime. */
UCLASS(meta=(DisplayName="Hitmen Find Patrol Point"))
class HITMEN_API UBTTask_HitmenFindPatrolPoint : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenFindPatrolPoint();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;

protected:
	/** Wander radius around home when the NPC has no route (0 = stand guard). */
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float WanderRadius = 0.f;

	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float DefaultWaitTime = 4.f;
};

/** Turns a stationary NPC back to the direction it was placed facing. */
UCLASS(meta=(DisplayName="Hitmen Face Post"))
class HITMEN_API UBTTask_HitmenFacePost : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenFacePost();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;
	virtual void TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds) override;

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="1", Units="deg"))
	float TurnSpeedDegrees = 180.f;

	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0", Units="deg"))
	float Tolerance = 3.f;
};

/** Shoots at TargetActor for a burst while it is visible. */
UCLASS(meta=(DisplayName="Hitmen Shoot Target"))
class HITMEN_API UBTTask_HitmenShoot : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenShoot();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;
	virtual void TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds) override;
	virtual uint16 GetInstanceMemorySize() const override { return sizeof(float); }

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float BurstDuration = 1.5f;

	/** Aim at the chest rather than the pivot. */
	UPROPERTY(EditAnywhere, Category="Hitmen")
	float AimHeightOffset = 40.f;

	/** Aim error of a charmed player shooting with its own weapon (NPCs use the difficulty's). */
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0", Units="deg"))
	float CharmedAimErrorDegrees = 4.f;
};

/** Picks a flank / encircle slot around the target into MoveLocation. */
UCLASS(meta=(DisplayName="Hitmen Find Flank Position"))
class HITMEN_API UBTTask_HitmenFindFlankPosition : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenFindFlankPosition();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float PreferredDistance = 700.f;

	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float SniperDistance = 2500.f;
};

/**
 * Picks the next place to search into MoveLocation (C++ search scoring, no EQS asset):
 * candidates are hiding spots (lockers, tall grass, tagged props) and reachable navmesh points
 * around the last known position. Each is scored by proximity to that position, being out of
 * sight from it and from the NPC, being a hiding spot, and not already searched by any NPC
 * (spots are claimed through the AI subsystem). BT_Search loops it with a look-around per spot.
 */
UCLASS(meta=(DisplayName="Hitmen Find Search Point"))
class HITMEN_API UBTTask_HitmenFindSearchPoint : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenFindSearchPoint();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0", Units="cm"))
	float SearchRadius = 1200.f;

	/** Investigate the noise / body location instead of the last seen location. */
	UPROPERTY(EditAnywhere, Category="Hitmen")
	bool bUseInvestigateLocation = false;

	/** Random reachable navmesh points sampled around the center. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search", meta=(ClampMin="0", ClampMax="32"))
	int32 NavCandidateCount = 10;

	/** Hiding spots considered at most (nearest first). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search", meta=(ClampMin="0", ClampMax="32"))
	int32 MaxHidingSpotCandidates = 6;

	/** Where to stand in front of a hiding spot (along its forward axis). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search", meta=(ClampMin="0", Units="cm"))
	float HidingSpotStandOff = 100.f;

	/** Horizontal tolerance when snapping a hiding spot to the navmesh. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search", meta=(ClampMin="0", Units="cm"))
	float NavProjectionRadius = 250.f;

	/** Spots closer than this to the NPC are not worth walking to (unless hiding spots). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search", meta=(ClampMin="0", Units="cm"))
	float MinStepDistance = 250.f;

	/** Score for being close to the center (1 at the center, 0 at SearchRadius). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search|Weights", meta=(ClampMin="0"))
	float ProximityWeight = 1.f;

	/** Score for being out of line of sight from the last known position (where you could have gone). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search|Weights", meta=(ClampMin="0"))
	float HiddenFromLastKnownWeight = 1.f;

	/** Score for being out of the NPC's current view (no need to walk where it already sees). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search|Weights", meta=(ClampMin="0"))
	float HiddenFromSelfWeight = 0.75f;

	/** Score for lockers, tall grass and tagged hiding spots. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search|Weights", meta=(ClampMin="0"))
	float HidingSpotWeight = 1.5f;

	/** Penalty for a spot already searched by any NPC (claims expire, see the match rules). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search|Weights", meta=(ClampMin="0"))
	float ClaimedPenalty = 3.f;

	/** Penalty for a spot too close to the NPC (see MinStepDistance). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search|Weights", meta=(ClampMin="0"))
	float TooClosePenalty = 2.f;

	/** Random spread so several searchers do not walk the same pattern. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Search|Weights", meta=(ClampMin="0"))
	float RandomWeight = 0.25f;
};

/** Channels a reinforcement call; interrupted if the NPC takes damage. */
UCLASS(meta=(DisplayName="Hitmen Call Reinforcements"))
class HITMEN_API UBTTask_HitmenCallReinforcements : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenCallReinforcements();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;
	virtual void TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds) override;
	virtual EBTNodeResult::Type AbortTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;
	virtual uint16 GetInstanceMemorySize() const override { return sizeof(float) * 2; }
};

/** Looks around for a while (montage from the anim set + slow rotation). */
UCLASS(meta=(DisplayName="Hitmen Look Around"))
class HITMEN_API UBTTask_HitmenLookAround : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenLookAround();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;
	virtual void TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds) override;
	virtual uint16 GetInstanceMemorySize() const override { return sizeof(float); }

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float Duration = 3.f;

	UPROPERTY(EditAnywhere, Category="Hitmen")
	float TurnSpeedDegrees = 60.f;
};

/** Uses the UseActor of the current patrol point (IHitmenNPCUsable). */
UCLASS(meta=(DisplayName="Hitmen Use Smart Actor"))
class HITMEN_API UBTTask_HitmenUseActor : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenUseActor();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;
	virtual void TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds) override;
	virtual uint16 GetInstanceMemorySize() const override { return sizeof(float); }
};

/** Sets the alert state (e.g. back to Unaware after an investigation). */
UCLASS(meta=(DisplayName="Hitmen Set Alert State"))
class HITMEN_API UBTTask_HitmenSetAlertState : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenSetAlertState();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen")
	EHitmenAlertState NewState = EHitmenAlertState::Unaware;
};
