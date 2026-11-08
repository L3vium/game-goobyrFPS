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

/** A reachable point around the last known location into MoveLocation (search pattern). */
UCLASS(meta=(DisplayName="Hitmen Find Search Point"))
class HITMEN_API UBTTask_HitmenFindSearchPoint : public UBTTaskNode
{
	GENERATED_BODY()

public:
	UBTTask_HitmenFindSearchPoint();
	virtual EBTNodeResult::Type ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) override;

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float SearchRadius = 1200.f;

	/** Investigate the noise / body location instead of the last seen location. */
	UPROPERTY(EditAnywhere, Category="Hitmen")
	bool bUseInvestigateLocation = false;
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
