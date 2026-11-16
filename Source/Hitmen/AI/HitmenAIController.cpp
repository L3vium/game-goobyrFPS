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
#include "DrawDebugHelpers.h"
#include "HAL/IConsoleManager.h"

static TAutoConsoleVariable<int32> CVarHitmenAICones(TEXT("HitmenAI.Cones"), 0,
	TEXT("1: draw every NPC's focal (white) and peripheral (grey) view cones and its sixth sense ring (dev builds)."));

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
	const FName TakeCover(TEXT("TakeCover"));
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
	else
	{
		EvaluateSixthSense();
	}
	if (CVarHitmenAICones.GetValueOnGameThread() > 0)
	{
		DrawDebugCones();
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
	// Hiding from an unseen shooter comes first: the radio call waits until it stops hiding.
	Blackboard->SetValueAsBool(HitmenBBKeys::CanCallReinforcements, !IsPawnCharmed() && Alert->CanCallReinforcementsNow() && !Alert->IsTakingCover());
	Blackboard->SetValueAsBool(HitmenBBKeys::IsOnEdge, Alert->IsOnEdge());
	// An unarmed VIP runs to safety from real danger (Hitman's evacuation), not from a footstep.
	const AHitmenNPC* NPC = Cast<AHitmenNPC>(GetPawn());
	const EHitmenAlertState State = Alert->GetAlertState();
	const bool bDanger = State == EHitmenAlertState::Combat || State == EHitmenAlertState::Searching
		|| (State == EHitmenAlertState::Investigating && Alert->IsThreatInvestigation());
	Blackboard->SetValueAsBool(HitmenBBKeys::Evacuate, NPC && ((NPC->IsVIP() && !NPC->GetWeapon()) || NPC->IsGuest()) && bDanger);
	Blackboard->SetValueAsBool(HitmenBBKeys::TakeCover, Alert->IsTakingCover() && !Alert->IsTargetVisible());
	Blackboard->SetValueAsObject(HitmenBBKeys::DisposeBody, Alert->GetDisposeBody());
	Blackboard->SetValueAsObject(HitmenBBKeys::DisposeContainer, Alert->GetDisposeContainer());
	Blackboard->SetValueAsVector(HitmenBBKeys::DisposeLocation, Alert->GetDisposeLocation());
}

void AHitmenAIController::EvaluateSixthSense()
{
	const AHitmenCharacterBase* Self = Cast<AHitmenCharacterBase>(GetPawn());
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	if (!Self || !AI || !Alert || SixthSenseRadius <= 0.f || SixthSenseRate <= 0.f)
	{
		return;
	}
	const EHitmenAlertState State = Alert->GetAlertState();
	if (State == EHitmenAlertState::Combat || State == EHitmenAlertState::Searching)
	{
		return;
	}
	// A hostile right next to the NPC, out of its view: felt (a presence, a breath), never through a wall.
	for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
	{
		const APlayerController* PC = It->Get();
		AHitmenCharacterBase* Other = PC ? Cast<AHitmenCharacterBase>(PC->GetPawn()) : nullptr;
		if (!Other || Other->IsDead() || !UHitmenTeamLibrary::AreHostile(Self, Other))
		{
			continue;
		}
		if (FVector::DistSquared(Self->GetActorLocation(), Other->GetActorLocation()) > FMath::Square(SixthSenseRadius))
		{
			continue;
		}
		// Hidden players (invisible, inside a hiding spot, cheats) are not felt either: the range-free sight test with a
		// 180 degree cone keeps walls, smoke and hidden status.
		if (!AI->CanObserverSee(Self, Other, SixthSenseRadius + 50.f, 180.f, true))
		{
			continue;
		}
		// Heard walking or running right behind it; crouched (moving or not) or standing still, never.
		const UHitmenCharacterMovementComponent* Movement = Other->GetHitmenMovement();
		const bool bCrouched = Movement && Movement->IsCrouching();
		const bool bMoving = Other->GetVelocity().Size2D() > 30.f;
		const bool bSprinting = Movement && Movement->IsSprinting();
		const float Scale = !bMoving ? 0.f : (bCrouched ? SixthSenseCrouchScale : (bSprinting ? SixthSenseSprintScale : 1.f));
		if (Scale > 0.f)
		{
			Alert->SenseNearby(Other, SightEvaluationInterval, SixthSenseRate * Scale);
			return;
		}
	}
}

void AHitmenAIController::DrawDebugCones() const
{
#if ENABLE_DRAW_DEBUG
	const APawn* Self = GetPawn();
	if (!Self)
	{
		return;
	}
	FVector Eye;
	FRotator View;
	Self->GetActorEyesViewPoint(Eye, View);
	const float Range = GetEffectiveSightRadius();
	const float Life = SightEvaluationInterval * 1.1f;
	const FVector Direction = View.Vector();
	DrawDebugCone(GetWorld(), Eye, Direction, Range, FMath::DegreesToRadians(FocalVisionHalfAngle), FMath::DegreesToRadians(8.f), 16, FColor::White, false, Life, 0, 1.5f);
	DrawDebugCone(GetWorld(), Eye, Direction, Range, FMath::DegreesToRadians(PeripheralVisionHalfAngle), FMath::DegreesToRadians(8.f), 24, FColor(120, 120, 120), false, Life, 0, 1.f);
	DrawDebugCircle(GetWorld(), Self->GetActorLocation() - FVector(0.f, 0.f, 85.f), SixthSenseRadius, 32, FColor::Cyan, false, Life, 0, 1.f, FVector::ForwardVector, FVector::RightVector, false);
#endif
}

void AHitmenAIController::UpdateControlRotation(float DeltaTime, bool bUpdatePawn)
{
	APawn* const MyPawn = GetPawn();
	if (!MyPawn)
	{
		return;
	}
	// Where it wants to look (focus), unless it walks: then never more than MaxStrafeAngle away from the way it walks
	// (no backward walking: the legs only have forward and side cycles).
	FRotator Desired = GetControlRotation();
	FVector FocalPoint = GetFocalPoint();
	// Watching a target it no longer sees: where it was last known to be, never where it really is (it may have
	// blinked or teleported away).
	if (Alert && GetFocusActor() && GetFocusActor() == Alert->GetTarget() && !Alert->IsTargetVisible())
	{
		FocalPoint = Alert->GetLastKnownLocation() + FVector(0.f, 0.f, 60.f);
	}
	const FVector Velocity = MyPawn->GetVelocity();
	const bool bWalking = Velocity.Size2D() > FaceMovementMinSpeed;
	const bool bHasFocus = FAISystem::IsValidLocation(FocalPoint);
	if (bHasFocus)
	{
		Desired = (FocalPoint - MyPawn->GetPawnViewLocation()).Rotation();
	}
	if (bWalking)
	{
		const float WalkYaw = Velocity.Rotation().Yaw;
		if (!bHasFocus || FMath::Abs(FRotator::NormalizeAxis(Desired.Yaw - WalkYaw)) > MaxStrafeAngle)
		{
			Desired = FRotator(0.f, WalkYaw, 0.f);
		}
	}
	else if (!bHasFocus)
	{
		Desired = MyPawn->GetActorRotation();
	}
	Desired.Yaw = FRotator::NormalizeAxis(Desired.Yaw);
	Desired.Roll = 0.f;
	SetControlRotation(Desired);
	if (bUpdatePawn)
	{
		const FRotator CurrentPawnRotation = MyPawn->GetActorRotation();
		if (!CurrentPawnRotation.Equals(Desired, 1e-3f))
		{
			// The character movement turns the body toward the control rotation at its rotation rate.
			MyPawn->FaceRotation(Desired, DeltaTime);
		}
	}
}
