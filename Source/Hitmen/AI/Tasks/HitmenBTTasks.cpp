// Hitmen — behavior tree tasks

#include "AI/Tasks/HitmenBTTasks.h"
#include "AI/HitmenAIController.h"
#include "AI/HitmenAlertComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "AI/HitmenPatrolRoute.h"
#include "Characters/HitmenNPC.h"
#include "Characters/HitmenCharacterBase.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Components/HitmenMeleeComponent.h"
#include "Core/HitmenInterfaces.h"
#include "Data/HitmenAnimSet.h"
#include "Data/HitmenMatchRules.h"
#include "Framework/HitmenGameModeBase.h"
#include "BehaviorTree/BlackboardComponent.h"
#include "GameFramework/Character.h"
#include "Components/CapsuleComponent.h"
#include "NavigationSystem.h"
#include "Animation/AnimInstance.h"
#include "Engine/World.h"

namespace
{
	UHitmenAlertComponent* GetAlert(UBehaviorTreeComponent& OwnerComp)
	{
		const AHitmenAIController* AI = Cast<AHitmenAIController>(OwnerComp.GetAIOwner());
		return AI ? AI->GetAlert() : nullptr;
	}

	bool RandomReachablePoint(const UWorld* World, const FVector& Origin, float Radius, FVector& OutPoint)
	{
		const UNavigationSystemV1* Nav = FNavigationSystem::GetCurrent<UNavigationSystemV1>(World);
		FNavLocation Result;
		if (Nav && Nav->GetRandomReachablePointInRadius(Origin, Radius, Result))
		{
			OutPoint = Result.Location;
			return true;
		}
		return false;
	}
}

// ---------------------------------------------------------------------------------------------
// Patrol
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenFacePost::UBTTask_HitmenFacePost()
{
	NodeName = TEXT("Face Post");
	bNotifyTick = true;
}

EBTNodeResult::Type UBTTask_HitmenFacePost::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	const AHitmenNPC* NPC = Cast<AHitmenNPC>(OwnerComp.GetAIOwner() ? OwnerComp.GetAIOwner()->GetPawn() : nullptr);
	if (!NPC)
	{
		return EBTNodeResult::Failed;
	}
	OwnerComp.GetAIOwner()->ClearFocus(EAIFocusPriority::Gameplay);
	return EBTNodeResult::InProgress;
}

void UBTTask_HitmenFacePost::TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds)
{
	AHitmenNPC* NPC = Cast<AHitmenNPC>(OwnerComp.GetAIOwner() ? OwnerComp.GetAIOwner()->GetPawn() : nullptr);
	if (!NPC)
	{
		FinishLatentTask(OwnerComp, EBTNodeResult::Failed);
		return;
	}
	const float Current = NPC->GetActorRotation().Yaw;
	const float Wanted = NPC->GetPostRotation().Yaw;
	const float Delta = FMath::FindDeltaAngleDegrees(Current, Wanted);
	if (FMath::Abs(Delta) <= Tolerance)
	{
		NPC->SetActorRotation(NPC->GetPostRotation());
		OwnerComp.GetAIOwner()->SetControlRotation(NPC->GetPostRotation());
		FinishLatentTask(OwnerComp, EBTNodeResult::Succeeded);
		return;
	}
	const float Step = FMath::Clamp(Delta, -TurnSpeedDegrees * DeltaSeconds, TurnSpeedDegrees * DeltaSeconds);
	const FRotator NewRotation(0.f, Current + Step, 0.f);
	NPC->SetActorRotation(NewRotation);
	OwnerComp.GetAIOwner()->SetControlRotation(NewRotation);
}

UBTTask_HitmenFindPatrolPoint::UBTTask_HitmenFindPatrolPoint()
{
	NodeName = TEXT("Find Patrol Point");
}

EBTNodeResult::Type UBTTask_HitmenFindPatrolPoint::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	AHitmenNPC* NPC = Cast<AHitmenNPC>(OwnerComp.GetAIOwner() ? OwnerComp.GetAIOwner()->GetPawn() : nullptr);
	if (!BB || !NPC)
	{
		return EBTNodeResult::Failed;
	}

	// Stationary NPCs ignore any route: their patrol is their post.
	const AHitmenPatrolRoute* Route = NPC->GetBehavior() == EHitmenNPCBehavior::Patrol ? NPC->GetPatrolRoute() : nullptr;
	if (Route && Route->GetPointCount() > 0)
	{
		const int32 Index = NPC->AdvancePatrolIndex(Route->GetPointCount());
		const FHitmenPatrolPoint* Point = Route->GetPoint(Index);
		BB->SetValueAsVector(HitmenBBKeys::MoveLocation, Route->GetPointWorldLocation(Index));
		BB->SetValueAsFloat(HitmenBBKeys::WaitTime, Point ? Point->WaitTime : DefaultWaitTime);
		BB->SetValueAsObject(HitmenBBKeys::UseActor, Point ? Point->UseActor.Get() : nullptr);
		return EBTNodeResult::Succeeded;
	}

	const FVector Home = BB->GetValueAsVector(HitmenBBKeys::HomeLocation);
	FVector Target = Home;
	if (WanderRadius > 0.f)
	{
		RandomReachablePoint(NPC->GetWorld(), Home, WanderRadius, Target);
	}
	BB->SetValueAsVector(HitmenBBKeys::MoveLocation, Target);
	BB->SetValueAsFloat(HitmenBBKeys::WaitTime, DefaultWaitTime);
	BB->SetValueAsObject(HitmenBBKeys::UseActor, nullptr);
	return EBTNodeResult::Succeeded;
}

// ---------------------------------------------------------------------------------------------
// Shoot
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenShoot::UBTTask_HitmenShoot()
{
	NodeName = TEXT("Shoot Target");
	bNotifyTick = true;
}

EBTNodeResult::Type UBTTask_HitmenShoot::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	*reinterpret_cast<float*>(NodeMemory) = 0.f;
	const UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	AActor* Target = BB ? Cast<AActor>(BB->GetValueAsObject(HitmenBBKeys::TargetActor)) : nullptr;
	if (!Target)
	{
		return EBTNodeResult::Failed;
	}
	OwnerComp.GetAIOwner()->SetFocus(Target);
	return EBTNodeResult::InProgress;
}

void UBTTask_HitmenShoot::TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds)
{
	float& Elapsed = *reinterpret_cast<float*>(NodeMemory);
	Elapsed += DeltaSeconds;

	AAIController* AI = OwnerComp.GetAIOwner();
	const UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	AActor* Target = BB ? Cast<AActor>(BB->GetValueAsObject(HitmenBBKeys::TargetActor)) : nullptr;
	APawn* Pawn = AI ? AI->GetPawn() : nullptr;
	const AHitmenCharacterBase* TargetCharacter = Cast<AHitmenCharacterBase>(Target);
	if (!Pawn || !Target || (TargetCharacter && TargetCharacter->IsDead()) || !BB->GetValueAsBool(HitmenBBKeys::TargetVisible))
	{
		if (AI)
		{
			AI->ClearFocus(EAIFocusPriority::Gameplay);
		}
		FinishLatentTask(OwnerComp, EBTNodeResult::Failed);
		return;
	}

	const FVector AimPoint = Target->GetActorLocation() + FVector::UpVector * AimHeightOffset;
	if (AHitmenNPC* NPC = Cast<AHitmenNPC>(Pawn))
	{
		if (NPC->GetWeapon())
		{
			NPC->FireAt(AimPoint);
		}
		else if (UHitmenMeleeComponent* Melee = NPC->FindComponentByClass<UHitmenMeleeComponent>())
		{
			Melee->Punch();
		}
	}
	else
	{
		// Charmed players (bare AHitmenAIController + default tree) shoot with whatever they hold,
		// or punch when unarmed.
		UHitmenInventoryComponent* Inventory = Pawn->FindComponentByClass<UHitmenInventoryComponent>();
		if (AHitmenWeaponBase* Weapon = Inventory ? Inventory->GetActiveWeapon() : nullptr)
		{
			Weapon->FireAt(AimPoint, CharmedAimErrorDegrees);
		}
		else if (UHitmenMeleeComponent* Melee = Pawn->FindComponentByClass<UHitmenMeleeComponent>())
		{
			Melee->Punch();
		}
	}

	if (Elapsed >= BurstDuration)
	{
		FinishLatentTask(OwnerComp, EBTNodeResult::Succeeded);
	}
}

// ---------------------------------------------------------------------------------------------
// Flank
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenFindFlankPosition::UBTTask_HitmenFindFlankPosition()
{
	NodeName = TEXT("Find Flank Position");
}

EBTNodeResult::Type UBTTask_HitmenFindFlankPosition::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	const APawn* Pawn = OwnerComp.GetAIOwner() ? OwnerComp.GetAIOwner()->GetPawn() : nullptr;
	const AActor* Target = BB ? Cast<AActor>(BB->GetValueAsObject(HitmenBBKeys::TargetActor)) : nullptr;
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(Pawn);
	if (!Pawn || !Target || !AI)
	{
		return EBTNodeResult::Failed;
	}
	const float Distance = BB->GetValueAsBool(HitmenBBKeys::IsSniper) ? SniperDistance : PreferredDistance;
	BB->SetValueAsVector(HitmenBBKeys::MoveLocation, AI->GetFlankLocation(Pawn, Target, Distance));
	return EBTNodeResult::Succeeded;
}

// ---------------------------------------------------------------------------------------------
// Search
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenFindSearchPoint::UBTTask_HitmenFindSearchPoint()
{
	NodeName = TEXT("Find Search Point");
}

EBTNodeResult::Type UBTTask_HitmenFindSearchPoint::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	const APawn* Pawn = OwnerComp.GetAIOwner() ? OwnerComp.GetAIOwner()->GetPawn() : nullptr;
	UWorld* World = Pawn ? Pawn->GetWorld() : nullptr;
	if (!BB || !Pawn || !World)
	{
		return EBTNodeResult::Failed;
	}
	FVector Center = BB->GetValueAsVector(bUseInvestigateLocation ? HitmenBBKeys::InvestigateLocation : HitmenBBKeys::LastKnownLocation);
	if (!FAISystem::IsValidLocation(Center) || Center.IsNearlyZero())
	{
		Center = Pawn->GetActorLocation();
	}
	const UNavigationSystemV1* Nav = FNavigationSystem::GetCurrent<UNavigationSystemV1>(World);
	UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(Pawn);

	// ---- Candidates ----
	struct FCandidate { FVector Location; bool bHidingSpot; };
	TArray<FCandidate> Candidates;
	const FVector ProjectExtent(NavProjectionRadius, NavProjectionRadius, NavProjectionRadius);
	auto AddProjected = [&Candidates, Nav, &ProjectExtent](const FVector& Raw, bool bHidingSpot)
	{
		FNavLocation Projected;
		if (Nav && Nav->ProjectPointToNavigation(Raw, Projected, ProjectExtent))
		{
			Candidates.Add({ Projected.Location, bHidingSpot });
		}
	};
	if (AI && MaxHidingSpotCandidates > 0)
	{
		TArray<AActor*> Spots;
		AI->GetHidingSpotsNear(Center, SearchRadius, Spots);
		Spots.Sort([&Center](const AActor& A, const AActor& B)
		{
			return FVector::DistSquared(A.GetActorLocation(), Center) < FVector::DistSquared(B.GetActorLocation(), Center);
		});
		for (int32 Index = 0; Index < Spots.Num() && Index < MaxHidingSpotCandidates; ++Index)
		{
			const AActor* Spot = Spots[Index];
			AddProjected(Spot->GetActorLocation() + Spot->GetActorForwardVector() * HidingSpotStandOff, true);
		}
	}
	AddProjected(Center, false);
	for (int32 Index = 0; Index < NavCandidateCount; ++Index)
	{
		FVector Point;
		if (RandomReachablePoint(World, Center, SearchRadius, Point))
		{
			Candidates.Add({ Point, false });
		}
	}
	if (Candidates.Num() == 0)
	{
		BB->SetValueAsVector(HitmenBBKeys::MoveLocation, Center);
		return EBTNodeResult::Succeeded;
	}

	// ---- Scoring ----
	const ACharacter* Character = Cast<ACharacter>(Pawn);
	const float EyeAboveFloor = (Character && Character->GetCapsuleComponent() ? Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight() : 0.f) + Pawn->BaseEyeHeight;
	const FVector SelfEye = Pawn->GetPawnViewLocation();
	// The last known position is a capsule center: lift it by the eye height above that center.
	const FVector CenterEye = Center + FVector::UpVector * Pawn->BaseEyeHeight;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSearchLOS), false, Pawn);
	auto IsVisibleFrom = [World, &Params](const FVector& From, const FVector& To)
	{
		FHitResult Hit;
		return !World->LineTraceSingleByChannel(Hit, From, To, ECC_Visibility, Params);
	};

	int32 BestIndex = 0;
	float BestScore = -TNumericLimits<float>::Max();
	for (int32 Index = 0; Index < Candidates.Num(); ++Index)
	{
		const FCandidate& Candidate = Candidates[Index];
		const FVector Eye = Candidate.Location + FVector::UpVector * EyeAboveFloor;
		float Score = ProximityWeight * (1.f - FMath::Clamp(FVector::Dist(Candidate.Location, Center) / FMath::Max(SearchRadius, 1.f), 0.f, 1.f));
		if (!IsVisibleFrom(CenterEye, Eye))
		{
			Score += HiddenFromLastKnownWeight;
		}
		if (!IsVisibleFrom(SelfEye, Eye))
		{
			Score += HiddenFromSelfWeight;
		}
		if (Candidate.bHidingSpot)
		{
			Score += HidingSpotWeight;
		}
		else if (FVector::Dist2D(Candidate.Location, Pawn->GetActorLocation()) < MinStepDistance)
		{
			Score -= TooClosePenalty;
		}
		if (AI && AI->IsSearchSpotClaimed(Candidate.Location))
		{
			Score -= ClaimedPenalty;
		}
		Score += FMath::FRand() * RandomWeight;
		if (Score > BestScore)
		{
			BestScore = Score;
			BestIndex = Index;
		}
	}

	const FVector Chosen = Candidates[BestIndex].Location;
	if (AI)
	{
		AI->ClaimSearchSpot(Pawn, Chosen);
	}
	BB->SetValueAsVector(HitmenBBKeys::MoveLocation, Chosen);
	return EBTNodeResult::Succeeded;
}

// ---------------------------------------------------------------------------------------------
// Reinforcements
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenCallReinforcements::UBTTask_HitmenCallReinforcements()
{
	NodeName = TEXT("Call Reinforcements");
	bNotifyTick = true;
	bNotifyTaskFinished = true;
}

EBTNodeResult::Type UBTTask_HitmenCallReinforcements::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	UHitmenAlertComponent* Alert = GetAlert(OwnerComp);
	if (!Alert || Alert->IsCallingReinforcements())
	{
		return EBTNodeResult::Failed;
	}
	float* Memory = reinterpret_cast<float*>(NodeMemory);
	Memory[0] = 0.f;
	Memory[1] = Alert->GetLastDamageTime();
	Alert->SetCallingReinforcements(true);
	OwnerComp.GetAIOwner()->StopMovement();

	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerComp.GetAIOwner()->GetPawn());
	if (Character && Character->GetAnimSet() && Character->GetAnimSet()->CallReinforcementsMontage)
	{
		Character->GetMesh()->GetAnimInstance()->Montage_Play(Character->GetAnimSet()->CallReinforcementsMontage);
	}
	return EBTNodeResult::InProgress;
}

void UBTTask_HitmenCallReinforcements::TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds)
{
	UHitmenAlertComponent* Alert = GetAlert(OwnerComp);
	float* Memory = reinterpret_cast<float*>(NodeMemory);
	if (!Alert)
	{
		FinishLatentTask(OwnerComp, EBTNodeResult::Failed);
		return;
	}
	// Interrupted by damage.
	if (Alert->GetLastDamageTime() > Memory[1])
	{
		Alert->SetCallingReinforcements(false);
		FinishLatentTask(OwnerComp, EBTNodeResult::Failed);
		return;
	}
	Memory[0] += DeltaSeconds;
	const float Duration = UHitmenAISubsystem::GetRules(Alert).ReinforcementCallDuration;
	if (Memory[0] >= Duration)
	{
		Alert->SetCallingReinforcements(false);
		if (UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(Alert))
		{
			AI->CallReinforcements(Alert->GetOwner(), Alert->GetLastKnownLocation(), Alert->GetTarget());
		}
		FinishLatentTask(OwnerComp, EBTNodeResult::Succeeded);
	}
}

EBTNodeResult::Type UBTTask_HitmenCallReinforcements::AbortTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	if (UHitmenAlertComponent* Alert = GetAlert(OwnerComp))
	{
		Alert->SetCallingReinforcements(false);
	}
	return EBTNodeResult::Aborted;
}

// ---------------------------------------------------------------------------------------------
// Look around
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenLookAround::UBTTask_HitmenLookAround()
{
	NodeName = TEXT("Look Around");
	bNotifyTick = true;
}

EBTNodeResult::Type UBTTask_HitmenLookAround::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	*reinterpret_cast<float*>(NodeMemory) = 0.f;
	OwnerComp.GetAIOwner()->ClearFocus(EAIFocusPriority::Gameplay);
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerComp.GetAIOwner()->GetPawn());
	if (Character && Character->GetAnimSet() && Character->GetAnimSet()->SearchLookAroundMontage)
	{
		Character->GetMesh()->GetAnimInstance()->Montage_Play(Character->GetAnimSet()->SearchLookAroundMontage);
	}
	return EBTNodeResult::InProgress;
}

void UBTTask_HitmenLookAround::TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds)
{
	float& Elapsed = *reinterpret_cast<float*>(NodeMemory);
	Elapsed += DeltaSeconds;
	if (APawn* Pawn = OwnerComp.GetAIOwner()->GetPawn())
	{
		// Sweep left and right.
		const float Direction = FMath::Sin(Elapsed * 1.5f) >= 0.f ? 1.f : -1.f;
		Pawn->AddActorWorldRotation(FRotator(0.f, Direction * TurnSpeedDegrees * DeltaSeconds, 0.f));
	}
	if (Elapsed >= Duration)
	{
		FinishLatentTask(OwnerComp, EBTNodeResult::Succeeded);
	}
}

// ---------------------------------------------------------------------------------------------
// Use actor
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenUseActor::UBTTask_HitmenUseActor()
{
	NodeName = TEXT("Use Smart Actor");
	bNotifyTick = true;
}

EBTNodeResult::Type UBTTask_HitmenUseActor::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	const UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	AActor* UseActor = BB ? Cast<AActor>(BB->GetValueAsObject(HitmenBBKeys::UseActor)) : nullptr;
	if (!UseActor || !UseActor->GetClass()->ImplementsInterface(UHitmenNPCUsable::StaticClass()))
	{
		return EBTNodeResult::Succeeded;
	}
	*reinterpret_cast<float*>(NodeMemory) = IHitmenNPCUsable::Execute_GetUseDuration(UseActor);
	return EBTNodeResult::InProgress;
}

void UBTTask_HitmenUseActor::TickTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory, float DeltaSeconds)
{
	float& Remaining = *reinterpret_cast<float*>(NodeMemory);
	Remaining -= DeltaSeconds;
	if (Remaining > 0.f)
	{
		return;
	}
	const UBlackboardComponent* BB = OwnerComp.GetBlackboardComponent();
	AActor* UseActor = BB ? Cast<AActor>(BB->GetValueAsObject(HitmenBBKeys::UseActor)) : nullptr;
	AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerComp.GetAIOwner()->GetPawn());
	if (UseActor && Character)
	{
		IHitmenNPCUsable::Execute_NPCUse(UseActor, Character);
	}
	FinishLatentTask(OwnerComp, EBTNodeResult::Succeeded);
}

// ---------------------------------------------------------------------------------------------
// Set alert state
// ---------------------------------------------------------------------------------------------

UBTTask_HitmenSetAlertState::UBTTask_HitmenSetAlertState()
{
	NodeName = TEXT("Set Alert State");
}

EBTNodeResult::Type UBTTask_HitmenSetAlertState::ExecuteTask(UBehaviorTreeComponent& OwnerComp, uint8* NodeMemory)
{
	if (UHitmenAlertComponent* Alert = GetAlert(OwnerComp))
	{
		Alert->SetAlertState(NewState);
		return EBTNodeResult::Succeeded;
	}
	return EBTNodeResult::Failed;
}
