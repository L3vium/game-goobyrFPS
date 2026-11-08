// Hitmen — behavior tree decorators

#include "AI/Decorators/HitmenBTDecorators.h"
#include "AI/HitmenAIController.h"
#include "BehaviorTree/BlackboardComponent.h"
#include "BehaviorTree/Blackboard/BlackboardKeyType_Enum.h"

UBTDecorator_HitmenAlertState::UBTDecorator_HitmenAlertState()
{
	NodeName = TEXT("Alert State Is");
	BlackboardKey.SelectedKeyName = HitmenBBKeys::AlertState;
	BlackboardKey.AddEnumFilter(this, GET_MEMBER_NAME_CHECKED(UBTDecorator_HitmenAlertState, BlackboardKey), StaticEnum<EHitmenAlertState>());
	FlowAbortMode = EBTFlowAbortMode::Both;
}

bool UBTDecorator_HitmenAlertState::CalculateRawConditionValue(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory) const
{
	const UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	if (!BB)
	{
		return false;
	}
	const EHitmenAlertState State = static_cast<EHitmenAlertState>(BB->GetValueAsEnum(BlackboardKey.SelectedKeyName));
	return AllowedStates.Contains(State);
}

FString UBTDecorator_HitmenAlertState::GetStaticDescription() const
{
	FString States;
	for (const EHitmenAlertState State : AllowedStates)
	{
		States += (States.IsEmpty() ? TEXT("") : TEXT(", ")) + StaticEnum<EHitmenAlertState>()->GetDisplayNameTextByValue(static_cast<int64>(State)).ToString();
	}
	return FString::Printf(TEXT("Alert state in [%s]"), *States);
}
