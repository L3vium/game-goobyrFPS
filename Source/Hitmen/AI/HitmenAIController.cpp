// Hitmen — AI controller

#include "AI/HitmenAIController.h"
#include "AI/HitmenAlertComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "Characters/HitmenCharacterBase.h"
#include "Characters/HitmenNPC.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Core/HitmenTeamLibrary.h"
#include "Perception/AIPerceptionComponent.h"
#include "Perception/AISenseConfig_Sight.h"
#include "BehaviorTree/BehaviorTree.h"
#include "BehaviorTree/BlackboardComponent.h"
#include "BrainComponent.h"
#include "Engine/World.h"
#include "TimerManager.h"

namespace HitmenBBKeys
{
	const FName AlertState = TEXT("AlertState");
	const FName TargetActor = TEXT("TargetActor");
	const FName LastKnownLocation = TEXT("LastKnownLocation");
	const FName InvestigateLocation = TEXT("InvestigateLocation");
	const FName MoveLocation = TEXT("MoveLocation");
	const FName WaitTime = TEXT("WaitTime");
	const FName TargetVisible = TEXT("TargetVisible");
	const FName IsSniper = TEXT("IsSniper");
	const FName HomeLocation = TEXT("HomeLocation");
	const FName UseActor = TEXT("UseActor");
	const FName CanCallReinforcements = TEXT("CanCallReinforcements");
	const FName IsStationary = TEXT("IsStationary");
	const FName HoldPosition = TEXT("HoldPosition");
}

AHitmenAIController::AHitmenAIController()
{
	PerceptionComponent = CreateDefaultSubobject<UAIPerceptionComponent>(TEXT("Perception"));
	SightConfig = CreateDefaultSubobject<UAISenseConfig_Sight>(TEXT("SightConfig"));
	SightConfig->SightRadius = SightRadius;
	SightConfig->LoseSightRadius = LoseSightRadius;
	SightConfig->PeripheralVisionAngleDegrees = PeripheralVisionHalfAngle;
	SightConfig->DetectionByAffiliation.bDetectEnemies = true;
	SightConfig->DetectionByAffiliation.bDetectNeutrals = true;
	SightConfig->DetectionByAffiliation.bDetectFriendlies = true;
	SightConfig->SetMaxAge(1.f);
	PerceptionComponent->ConfigureSense(*SightConfig);
	PerceptionComponent->SetDominantSense(SightConfig->GetSenseImplementation());
	bWantsPlayerState = false;
}

void AHitmenAIController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);
	if (!InPawn)
	{
		return;
	}

	// Apply editable sight values (constructor ran before BP defaults).
	SightConfig->SightRadius = SightRadius;
	SightConfig->LoseSightRadius = LoseSightRadius;
	SightConfig->PeripheralVisionAngleDegrees = PeripheralVisionHalfAngle;
	PerceptionComponent->RequestStimuliListenerUpdate();

	Alert = InPawn->FindComponentByClass<UHitmenAlertComponent>();
	if (!Alert)
	{
		// Charmed players get a temporary alert component.
		Alert = NewObject<UHitmenAlertComponent>(InPawn, TEXT("RuntimeAlert"));
		Alert->RegisterComponent();
	}
	Alert->OnAlertStateChanged.AddDynamic(this, &AHitmenAIController::HandleAlertStateChanged);
	PerceptionComponent->OnTargetPerceptionUpdated.AddDynamic(this, &AHitmenAIController::HandleTargetPerceptionUpdated);

	if (BehaviorTree)
	{
		RunBehaviorTree(BehaviorTree);
	}
	if (Blackboard)
	{
		Blackboard->SetValueAsVector(HitmenBBKeys::HomeLocation, InPawn->GetActorLocation());
		if (const AHitmenNPC* NPC = Cast<AHitmenNPC>(InPawn))
		{
			Blackboard->SetValueAsVector(HitmenBBKeys::HomeLocation, NPC->GetPostLocation());
			Blackboard->SetValueAsBool(HitmenBBKeys::IsSniper, NPC->IsSniper());
			Blackboard->SetValueAsBool(HitmenBBKeys::IsStationary, NPC->GetBehavior() == EHitmenNPCBehavior::Stationary);
			Blackboard->SetValueAsBool(HitmenBBKeys::HoldPosition, NPC->HoldsPosition());
		}
	}

	// Charmed pawns go straight for the nearest hostile.
	if (const AHitmenCharacterBase* HitmenCharacter = Cast<AHitmenCharacterBase>(InPawn);
		HitmenCharacter && HitmenCharacter->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Charmed))
	{
		Alert->SetAlertState(EHitmenAlertState::Searching);
	}

	GetWorldTimerManager().SetTimer(SightTimer, this, &AHitmenAIController::EvaluateSight, SightEvaluationInterval, true);
	SyncBlackboard();
}

void AHitmenAIController::OnUnPossess()
{
	GetWorldTimerManager().ClearTimer(SightTimer);
	if (Alert)
	{
		Alert->OnAlertStateChanged.RemoveAll(this);
		if (Alert->GetFName() == TEXT("RuntimeAlert"))
		{
			Alert->DestroyComponent();
		}
	}
	Alert = nullptr;
	PerceivedActors.Reset();
	Super::OnUnPossess();
}

ETeamAttitude::Type AHitmenAIController::GetTeamAttitudeTowards(const AActor& Other) const
{
	return UHitmenTeamLibrary::AreHostile(GetPawn(), &Other) ? ETeamAttitude::Hostile : ETeamAttitude::Neutral;
}

void AHitmenAIController::SetBrainPaused(bool bPaused)
{
	if (UBrainComponent* Brain = GetBrainComponent())
	{
		if (bPaused)
		{
			Brain->PauseLogic(TEXT("Status"));
			StopMovement();
		}
		else
		{
			Brain->ResumeLogic(TEXT("Status"));
		}
	}
	if (Alert)
	{
		Alert->SetSuppressed(bPaused);
	}
}

void AHitmenAIController::HandleTargetPerceptionUpdated(AActor* Actor, FAIStimulus Stimulus)
{
	if (Stimulus.WasSuccessfullySensed())
	{
		PerceivedActors.Add(Actor);
	}
	else
	{
		PerceivedActors.Remove(Actor);
		if (Alert)
		{
			Alert->LoseSightOf(Actor);
		}
	}
}

bool AHitmenAIController::CanReallySee(const AActor* Actor) const
{
	const APawn* Self = GetPawn();
	if (!Self || !Actor)
	{
		return false;
	}
	const FVector Eye = Self->GetPawnViewLocation();
	const FVector Point = Actor->GetActorLocation();
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	if (AI && AI->IsSightBlocked(Eye, Point))
	{
		return false;
	}
	// Blackout: nobody is seen inside (or from inside) a dark zone.
	if (AI && (AI->IsLocationDark(Point) || AI->IsLocationDark(Eye)))
	{
		return false;
	}
	if (const AHitmenCharacterBase* HitmenCharacter = Cast<AHitmenCharacterBase>(Actor))
	{
		if (!HitmenCharacter->IsDead() && HitmenCharacter->GetStatusEffects()->IsHiddenFromSight())
		{
			return false;
		}
	}
	return true;
}

bool AHitmenAIController::IsAlarming(const AActor* Actor) const
{
	const AHitmenCharacterBase* HitmenCharacter = Cast<AHitmenCharacterBase>(Actor);
	if (!HitmenCharacter)
	{
		return false;
	}
	if (const UHitmenInventoryComponent* Inventory = HitmenCharacter->FindComponentByClass<UHitmenInventoryComponent>(); Inventory && Inventory->GetActiveWeapon())
	{
		return true;
	}
	const UHitmenCharacterMovementComponent* Movement = HitmenCharacter->GetHitmenMovement();
	return Movement && (Movement->IsSprinting() || Movement->IsClimbing() || Movement->IsMantling());
}

void AHitmenAIController::EvaluateSight()
{
	if (!Alert)
	{
		return;
	}
	const AHitmenCharacterBase* Self = Cast<AHitmenCharacterBase>(GetPawn());
	if (!Self || Self->IsDead())
	{
		return;
	}

	bool bSawTarget = false;
	for (auto It = PerceivedActors.CreateIterator(); It; ++It)
	{
		AActor* Actor = It->Get();
		if (!Actor)
		{
			It.RemoveCurrent();
			continue;
		}
		if (!CanReallySee(Actor))
		{
			Alert->LoseSightOf(Actor);
			continue;
		}
		if (AHitmenCharacterBase* HitmenCharacter = Cast<AHitmenCharacterBase>(Actor))
		{
			if (HitmenCharacter->IsDead())
			{
				if (HitmenCharacter->IsDiscoverableBody())
				{
					Alert->SeeBody(HitmenCharacter);
				}
				continue;
			}
		}
		if (UHitmenTeamLibrary::AreHostile(Self, Actor) && Cast<APawn>(Actor))
		{
			// Prefer the current target; otherwise the first hostile seen.
			if (!Alert->GetTarget() || Alert->GetTarget() == Actor || !bSawTarget)
			{
				Alert->SeeHostile(Actor, SightEvaluationInterval, IsAlarming(Actor));
				bSawTarget |= Alert->GetTarget() == Actor;
			}
		}
	}
	SyncBlackboard();
}

void AHitmenAIController::HandleAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState)
{
	SyncBlackboard();
}

void AHitmenAIController::SyncBlackboard()
{
	if (!Blackboard || !Alert)
	{
		return;
	}
	Blackboard->SetValueAsEnum(HitmenBBKeys::AlertState, static_cast<uint8>(Alert->GetAlertState()));
	Blackboard->SetValueAsObject(HitmenBBKeys::TargetActor, Alert->GetTarget());
	Blackboard->SetValueAsVector(HitmenBBKeys::LastKnownLocation, Alert->GetLastKnownLocation());
	Blackboard->SetValueAsVector(HitmenBBKeys::InvestigateLocation, Alert->GetInvestigateLocation());
	Blackboard->SetValueAsBool(HitmenBBKeys::TargetVisible, Alert->IsTargetVisible());
	// An NPC fighting cannot call; one that is alerted but not engaged can.
	Blackboard->SetValueAsBool(HitmenBBKeys::CanCallReinforcements, Alert->GetAlertState() == EHitmenAlertState::Combat && !Alert->IsTargetVisible());
}
