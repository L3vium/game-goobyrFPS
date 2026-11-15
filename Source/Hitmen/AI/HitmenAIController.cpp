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
#include "Core/HitmenGameSettings.h"
#include "Data/HitmenMatchRules.h"
#include "Framework/HitmenGameState.h"
#include "Perception/AIPerceptionComponent.h"
#include "Perception/AISenseConfig_Sight.h"
#include "BehaviorTree/BehaviorTree.h"
#include "BehaviorTree/BlackboardComponent.h"
#include "BrainComponent.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "Weapons/HitmenWeaponBase.h"
#include "EngineUtils.h"

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
	const FName IsOnEdge(TEXT("IsOnEdge"));
	const FName Evacuate(TEXT("Evacuate"));
	const FName DisposeBody(TEXT("DisposeBody"));
	const FName DisposeContainer(TEXT("DisposeContainer"));
	const FName DisposeLocation(TEXT("DisposeLocation"));
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

float AHitmenAIController::GetEffectiveSightRadius() const
{
	// The sense config holds the radius already scaled by the difficulty.
	return SightConfig ? SightConfig->SightRadius : SightRadius;
}

void AHitmenAIController::ApplySightConfig()
{
	// Difficulty scales both radii (the lobby difficulty is only known once the game state exists).
	float Multiplier = 1.f;
	if (const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr)
	{
		Multiplier = UHitmenAISubsystem::GetRules(this).GetPreset(GS->GetLobbySettings().Difficulty).NPCSightRangeMultiplier;
	}
	SightConfig->SightRadius = SightRadius * Multiplier;
	SightConfig->LoseSightRadius = LoseSightRadius * Multiplier;
	SightConfig->PeripheralVisionAngleDegrees = PeripheralVisionHalfAngle;
	PerceptionComponent->RequestStimuliListenerUpdate();
}

void AHitmenAIController::BeginPlay()
{
	Super::BeginPlay();
	// Placed NPCs are possessed before the game state carries the lobby difficulty: apply again.
	ApplySightConfig();
}

void AHitmenAIController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);
	if (!InPawn)
	{
		return;
	}

	// Apply editable sight values (constructor ran before BP defaults).
	ApplySightConfig();

	Alert = InPawn->FindComponentByClass<UHitmenAlertComponent>();
	bOwnsRuntimeAlert = false;
	if (!Alert)
	{
		// Charmed players get a temporary alert component (unique name: a player can be charmed twice).
		Alert = NewObject<UHitmenAlertComponent>(InPawn, MakeUniqueObjectName(InPawn, UHitmenAlertComponent::StaticClass(), TEXT("RuntimeAlert")));
		Alert->RegisterComponent();
		bOwnsRuntimeAlert = true;
	}
	Alert->OnAlertStateChanged.AddUniqueDynamic(this, &AHitmenAIController::HandleAlertStateChanged);
	PerceptionComponent->OnTargetPerceptionUpdated.AddUniqueDynamic(this, &AHitmenAIController::HandleTargetPerceptionUpdated);

	// Bare controllers (charmed players) fall back to the project's default NPC tree.
	UBehaviorTree* Tree = BehaviorTree;
	if (!Tree)
	{
		Tree = UHitmenGameSettings::Get()->DefaultBehaviorTree.LoadSynchronous();
	}
	if (Tree)
	{
		RunBehaviorTree(Tree);
	}
	RefreshNPCKeys();

	// Charmed pawns hunt hostiles of their new side, starting where they stand.
	if (IsPawnCharmed())
	{
		Alert->StartSearchAround(InPawn->GetActorLocation());
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
		if (bOwnsRuntimeAlert)
		{
			Alert->SetAlertState(EHitmenAlertState::Unaware);
			Alert->SetSuppressed(true);
			Alert->DestroyComponent();
		}
	}
	Alert = nullptr;
	bOwnsRuntimeAlert = false;
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
	// Perception runs on its own timer and lags: a target that just left sight (or teleported away) would still read as
	// seen, at its new position. Range (the lose-sight radius), cone and walls are checked again here, with smoke,
	// darkness and hidden status.
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	const float LoseRadius = SightConfig ? SightConfig->LoseSightRadius : LoseSightRadius;
	return AI && AI->CanObserverSee(GetPawn(), Actor, LoseRadius, PeripheralVisionHalfAngle, true);
}

bool AHitmenAIController::CanSeeActorNow(const AActor* Actor) const
{
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	return AI && AI->CanObserverSee(GetPawn(), Actor, GetEffectiveSightRadius(), PeripheralVisionHalfAngle, true);
}

bool AHitmenAIController::IsPawnCharmed() const
{
	const AHitmenCharacterBase* HitmenCharacter = Cast<AHitmenCharacterBase>(GetPawn());
	return HitmenCharacter && HitmenCharacter->GetStatusEffects() && HitmenCharacter->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Charmed);
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
	// Illegal-looking actions in view (Hitman): running, climbing, vaulting, hanging, sneaking (crouch-walking), sliding.
	const UHitmenCharacterMovementComponent* Movement = HitmenCharacter->GetHitmenMovement();
	return Movement && (Movement->IsSprinting() || Movement->IsClimbing() || Movement->IsMantling() || Movement->IsHanging() || Movement->IsSliding()
		|| (Movement->IsCrouching() && HitmenCharacter->GetVelocity().Size2D() > 50.f));
}

float AHitmenAIController::GetSightRateScale(const AActor* Actor) const
{
	const APawn* Self = GetPawn();
	if (!Self || !Actor)
	{
		return 1.f;
	}
	FVector Eye;
	FRotator View;
	Self->GetActorEyesViewPoint(Eye, View);
	// Focal vision fills at full rate, the edge of the view slower.
	const FVector ToActor = (Actor->GetActorLocation() - Eye).GetSafeNormal();
	const float Angle = FMath::RadiansToDegrees(FMath::Acos(FMath::Clamp(FVector::DotProduct(View.Vector(), ToActor), -1.f, 1.f)));
	const float AngleScale = Angle <= FocalVisionHalfAngle ? 1.f
		: FMath::GetMappedRangeValueClamped(FVector2D(FocalVisionHalfAngle, FMath::Max(PeripheralVisionHalfAngle, FocalVisionHalfAngle + 1.f)), FVector2D(1.f, PeripheralRateMultiplier), Angle);
	// How much of the body is in view: head, chest and pelvis.
	float Visible = 1.f;
	if (const ACharacter* SeenCharacter = Cast<ACharacter>(Actor); SeenCharacter && SeenCharacter->GetMesh())
	{
		int32 Seen = 0;
		FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSightParts), false, Self);
		Params.AddIgnoredActor(Actor);
		for (const FName Bone : { FName(TEXT("head")), FName(TEXT("spine_03")), FName(TEXT("pelvis")) })
		{
			if (!GetWorld()->LineTraceTestByChannel(Eye, SeenCharacter->GetMesh()->GetBoneLocation(Bone), ECC_Visibility, Params))
			{
				++Seen;
			}
		}
		Visible = Seen >= 3 ? 1.f : FMath::Lerp(PartialCoverRateMultiplier, 1.f, FMath::Max(0, Seen - 1) / 2.f);
	}
	return AngleScale * Visible;
}

void AHitmenAIController::EvaluateEvidence()
{
	const APawn* Self = GetPawn();
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	if (!Self || !AI || !Alert || Alert->GetAlertState() != EHitmenAlertState::Unaware)
	{
		return;
	}
	for (TActorIterator<AHitmenWeaponBase> It(GetWorld()); It; ++It)
	{
		AHitmenWeaponBase* Weapon = *It;
		if (Weapon && Weapon->IsEvidence() && FVector::DistSquared(Self->GetActorLocation(), Weapon->GetActorLocation()) <= FMath::Square(EvidenceSightRadius)
			&& AI->CanObserverSee(Self, Weapon, EvidenceSightRadius, PeripheralVisionHalfAngle, true))
		{
			Alert->SeeEvidence(Weapon);
			return;
		}
	}
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

	// Keep the current target while it is visible; otherwise take the nearest visible hostile.
	AActor* CurrentTarget = Alert->GetTarget();
	bool bCurrentTargetVisible = false;
	AActor* NearestHostile = nullptr;
	float NearestDistanceSquared = TNumericLimits<float>::Max();
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
			// A colleague knocked out (sleep dart): go and wake it.
			if (!UHitmenTeamLibrary::AreHostile(Self, HitmenCharacter) && HitmenCharacter->GetStatusEffects()
				&& HitmenCharacter->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Asleep))
			{
				Alert->SeeSleeper(HitmenCharacter);
				continue;
			}
		}
		if (UHitmenTeamLibrary::AreHostile(Self, Actor) && Cast<APawn>(Actor))
		{
			if (Actor == CurrentTarget)
			{
				bCurrentTargetVisible = true;
			}
			const float DistanceSquared = FVector::DistSquared(Self->GetActorLocation(), Actor->GetActorLocation());
			if (DistanceSquared < NearestDistanceSquared)
			{
				NearestDistanceSquared = DistanceSquared;
				NearestHostile = Actor;
			}
		}
	}
	if (AActor* Seen = bCurrentTargetVisible ? CurrentTarget : NearestHostile)
	{
		Alert->SeeHostile(Seen, SightEvaluationInterval, IsAlarming(Seen), GetSightRateScale(Seen));
	}
	EvidenceAccumulator += SightEvaluationInterval;
	if (EvidenceAccumulator >= EvidenceCheckInterval)
	{
		EvidenceAccumulator = 0.f;
		EvaluateEvidence();
	}
	SyncBlackboard();
}

void AHitmenAIController::HandleAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState)
{
	SyncBlackboard();
}

void AHitmenAIController::RefreshNPCKeys()
{
	APawn* InPawn = GetPawn();
	if (!Blackboard || !InPawn)
	{
		return;
	}
	Blackboard->SetValueAsVector(HitmenBBKeys::HomeLocation, InPawn->GetActorLocation());
	if (const AHitmenNPC* NPC = Cast<AHitmenNPC>(InPawn))
	{
		Blackboard->SetValueAsVector(HitmenBBKeys::HomeLocation, NPC->GetPostLocation());
		Blackboard->SetValueAsBool(HitmenBBKeys::IsSniper, NPC->IsSniper());
		Blackboard->SetValueAsBool(HitmenBBKeys::IsStationary, NPC->GetBehavior() == EHitmenNPCBehavior::Stationary);
		Blackboard->SetValueAsBool(HitmenBBKeys::HoldPosition, NPC->HoldsPosition());
	}
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
	// An NPC fighting cannot call; one that is alerted but not engaged can (charmed pawns never call).
	Blackboard->SetValueAsBool(HitmenBBKeys::CanCallReinforcements, !IsPawnCharmed() && Alert->CanCallReinforcementsNow());
	Blackboard->SetValueAsBool(HitmenBBKeys::IsOnEdge, Alert->IsOnEdge());
	// An unarmed VIP runs to safety from real danger (Hitman's evacuation), not from a footstep.
	const AHitmenNPC* NPC = Cast<AHitmenNPC>(GetPawn());
	const EHitmenAlertState State = Alert->GetAlertState();
	const bool bDanger = State == EHitmenAlertState::Combat || State == EHitmenAlertState::Searching
		|| (State == EHitmenAlertState::Investigating && Alert->IsThreatInvestigation());
	Blackboard->SetValueAsBool(HitmenBBKeys::Evacuate, NPC && NPC->IsVIP() && !NPC->GetWeapon() && bDanger);
	Blackboard->SetValueAsObject(HitmenBBKeys::DisposeBody, Alert->GetDisposeBody());
	Blackboard->SetValueAsObject(HitmenBBKeys::DisposeContainer, Alert->GetDisposeContainer());
	Blackboard->SetValueAsVector(HitmenBBKeys::DisposeLocation, Alert->GetDisposeLocation());
}
