// Hitmen — behavior tree decorators

#pragma once

#include "CoreMinimal.h"
#include "BehaviorTree/Decorators/BTDecorator_BlackboardBase.h"
#include "Core/HitmenTypes.h"
#include "HitmenBTDecorators.generated.h"

/**
 * Passes when the AlertState blackboard key is one of AllowedStates. Observes the key, so
 * "Observer aborts" switches subtrees as soon as the NPC's awareness changes.
 */
UCLASS(meta=(DisplayName="Hitmen Alert State Is"))
class HITMEN_API UBTDecorator_HitmenAlertState : public UBTDecorator_BlackboardBase
{
	GENERATED_BODY()

public:
	UBTDecorator_HitmenAlertState();
	virtual bool CalculateRawConditionValue(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) const override;
	virtual FString GetStaticDescription() const override;

protected:
	UPROPERTY(EditAnywhere, Category="Hitmen")
	TArray<EHitmenAlertState> AllowedStates;
};
