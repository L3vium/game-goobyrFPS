// Hitmen — alert state machine

#include "AI/HitmenAlertComponent.h"
#include "Hitmen.h"
#include "Characters/HitmenNPC.h"
#include "NavigationSystem.h"
#include "AI/HitmenAISubsystem.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Framework/HitmenGameState.h"
#include "Data/HitmenMatchRules.h"
#include "Core/HitmenTeamLibrary.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Framework/HitmenPlayerController.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "World/Interactables/HitmenHidingSpot.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

namespace
{
	/** Yellow, red or yellow-search: the NPC knows something is wrong. */
	bool IsAlertedState(EHitmenAlertState State)
	{
		return State == EHitmenAlertState::Investigating || State == EHitmenAlertState::Combat || State == EHitmenAlertState::Searching;
	}

	bool IsDeadActor(const AActor* Actor)
	{
		const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Actor);
		return Character && Character->IsDead();
	}
}

UHitmenAlertComponent::UHitmenAlertComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.TickInterval = 0.1f;
	SetIsReplicatedByDefault(true);
	// White fill per second by distance (cm): about 0.15 s point blank, 0.7 s at 5 m, 1.7 s at 10 m, 4 s at 20 m.
	FRichCurve* Curve = SuspicionRateByDistance.GetRichCurve();
	Curve->Reset();
	Curve->AddKey(0.f, 7.f);
	Curve->AddKey(200.f, 6.f);
	Curve->AddKey(500.f, 1.5f);
	Curve->AddKey(1000.f, 0.6f);
	Curve->AddKey(1500.f, 0.38f);
	Curve->AddKey(2000.f, 0.25f);
	Curve->AddKey(3000.f, 0.15f);
	for (auto It = Curve->GetKeyHandleIterator(); It; ++It)
	{
		Curve->SetKeyInterpMode(*It, RCIM_Cubic);
	}
}

void UHitmenAlertComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAlertComponent, AlertState);
	DOREPLIFETIME(UHitmenAlertComponent, Suspicion);
	DOREPLIFETIME(UHitmenAlertComponent, bCallingReinforcements);
	DOREPLIFETIME(UHitmenAlertComponent, Target);
	DOREPLIFETIME(UHitmenAlertComponent, bTargetVisible);
	DOREPLIFETIME(UHitmenAlertComponent, AlertReason);
	DOREPLIFETIME(UHitmenAlertComponent, bOnEdge);
	DOREPLIFETIME(UHitmenAlertComponent, ForcedBarkCount);
}

void UHitmenAlertComponent::ForceBark(EHitmenAlertReason Reason)
{
	if (!GetOwner()->HasAuthority())
	{
		return;
	}
	AlertReason = Reason;
	++ForcedBarkCount;
	OnRep_ForcedBark();
}

void UHitmenAlertComponent::OnRep_ForcedBark()
{
	UHitmenAISubsystem::PlayBark(this);
}

void UHitmenAlertComponent::BeginPlay()
{
	Super::BeginPlay();
	UHitmenAISubsystem::RegisterAlert(this);
	SetComponentTickEnabled(GetOwner()->HasAuthority());
}

void UHitmenAlertComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UHitmenAISubsystem::UnregisterAlert(this);
	Super::EndPlay(EndPlayReason);
}

// ---- Rules ----

float UHitmenAlertComponent::GetRuleLoseTime() const
{
	return UHitmenAISubsystem::GetRules(this).LoseTargetTime;
}

float UHitmenAlertComponent::GetRuleSearchTime() const
{
	return bBodySearch ? UHitmenAISubsystem::GetRules(this).BodySearchDuration : UHitmenAISubsystem::GetRules(this).SearchDuration;
}

float UHitmenAlertComponent::GetRuleInvestigateTime() const
{
	return UHitmenAISubsystem::GetRules(this).InvestigateDuration;
}

float UHitmenAlertComponent::GetDifficultySuspicionMultiplier() const
{
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	return GS ? UHitmenAISubsystem::GetRules(this).GetPreset(GS->GetLobbySettings().Difficulty).NPCSuspicionRateMultiplier : 1.f;
}

bool UHitmenAlertComponent::IsOwnerCharmed() const
{
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(GetOwner());
	return Character && Character->GetStatusEffects() && Character->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Charmed);
}

// ---- Server queries ----

bool UHitmenAlertComponent::WouldReactToNoise() const
{
	return !bSuppressed && bReactsToNoises && !(AlertState == EHitmenAlertState::Combat && bTargetVisible);
}

bool UHitmenAlertComponent::WouldAnswerCall() const
{
	return !bSuppressed && bReactsToNoises && AlertState != EHitmenAlertState::Combat;
}

bool UHitmenAlertComponent::CanCallReinforcementsNow() const
{
	if (bSuppressed)
	{
		return false;
	}
	// Fighting (shooting a visible target) never calls, and interrupts a call in progress.
	if (AlertState == EHitmenAlertState::Combat && bTargetVisible)
	{
		return false;
	}
	if (bCallingReinforcements)
	{
		return true;
	}
	const UHitmenMatchRules& Rules = UHitmenAISubsystem::GetRules(this);
	const float Now = GetWorld()->GetTimeSeconds();
	if (Now - LastCallStartTime < Rules.ReinforcementCallCooldown
		|| Now - AlertedSinceTime < Rules.ReinforcementCallMinAlertTime
		|| (bAlertedByCall && !Rules.bReinforcementCallsRelay))
	{
		return false;
	}
	switch (AlertState)
	{
	case EHitmenAlertState::Combat:        return Now - LastSeenTime > LoseSightGraceTime; // red, out of sight for real
	case EHitmenAlertState::Searching:     return true;
	case EHitmenAlertState::Investigating: return Rules.bInvestigatingNPCsCallReinforcements && bThreatInvestigation;
	default:                               return false;
	}
}

FVector UHitmenAlertComponent::GetCallLocation() const
{
	// A radio call sends others where this NPC has real knowledge: the target's last seen spot when it saw one, the
	// body / noise it investigates otherwise (never a stale or empty last known position).
	if (Target && LastSeenTime > 0.f && (AlertState == EHitmenAlertState::Combat || AlertState == EHitmenAlertState::Searching))
	{
		return LastKnownLocation;
	}
	return AlertState == EHitmenAlertState::Investigating ? InvestigateLocation : (LastSeenTime > 0.f ? LastKnownLocation : GetOwner()->GetActorLocation());
}

// ---- State ----

void UHitmenAlertComponent::SetAlertStateWithReason(EHitmenAlertState NewState, EHitmenAlertReason Reason)
{
	if (!GetOwner()->HasAuthority())
	{
		return;
	}
	if (AlertState != NewState)
	{
		AlertReason = Reason;
	}
	SetAlertState(NewState);
}

void UHitmenAlertComponent::EnterOnEdge()
{
	OnEdgeUntil = GetWorld()->GetTimeSeconds() + OnEdgeDuration;
	bOnEdge = OnEdgeDuration > 0.f;
}

void UHitmenAlertComponent::SetAlertState(EHitmenAlertState NewState)
{
	if (!GetOwner()->HasAuthority() || AlertState == NewState)
	{
		return;
	}
	const EHitmenAlertState Old = AlertState;
	const float Now = GetWorld()->GetTimeSeconds();
	AlertState = NewState;
	StateEnterTime = Now;
	if (!IsAlertedState(Old) && IsAlertedState(NewState))
	{
		AlertedSinceTime = Now;
	}
	if (NewState == EHitmenAlertState::Unaware)
	{
		SetSuspicionValue(0.f);
		Target = nullptr;
		bTargetVisible = false;
		bThreatInvestigation = false;
		bAlertedByCall = false;
	}
	if (NewState == EHitmenAlertState::Combat)
	{
		// Red means personal contact: no longer just following someone else's call.
		SetSuspicionValue(1.f);
		bAlertedByCall = false;
	}
	if (NewState != EHitmenAlertState::Investigating)
	{
		bThreatInvestigation = false;
	}
	RefreshSearchMarker();
	OnRep_AlertState();
}

void UHitmenAlertComponent::OnRep_AlertState()
{
	const EHitmenAlertState Old = PreviousRepState;
	PreviousRepState = AlertState;
	Juice_OnAlertStateChanged(Old, AlertState);
	OnAlertStateChanged.Broadcast(Old, AlertState);
	UHitmenAISubsystem::PlayBark(this);
	if (UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this))
	{
		AI->NotifyAlertChanged(this);
	}
}

void UHitmenAlertComponent::SetCallingReinforcements(bool bCalling)
{
	if (GetOwner()->HasAuthority() && bCallingReinforcements != bCalling)
	{
		bCallingReinforcements = bCalling;
		if (bCalling)
		{
			// Cooldown counts from the call start, so an interrupted call is not retried at once.
			LastCallStartTime = GetWorld()->GetTimeSeconds();
		}
		OnRep_Calling();
	}
}

void UHitmenAlertComponent::OnRep_Calling()
{
	OnCallingChanged.Broadcast(bCallingReinforcements);
	if (UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this))
	{
		AI->NotifyCallingChanged(this);
	}
}

void UHitmenAlertComponent::SetSuspicionValue(float NewSuspicion)
{
	if (Suspicion != NewSuspicion)
	{
		Suspicion = NewSuspicion;
		OnRep_Suspicion();
	}
}

void UHitmenAlertComponent::OnRep_Suspicion()
{
	OnSuspicionChanged.Broadcast(Suspicion);
}

void UHitmenAlertComponent::SetSuppressed(bool bInSuppressed)
{
	bSuppressed = bInSuppressed;
	if (bSuppressed)
	{
		SetCallingReinforcements(false);
	}
	RefreshSearchMarker();
}

void UHitmenAlertComponent::SetInvestigate(const FVector& Location, bool bThreat, EHitmenAlertReason Reason)
{
	const bool bSamePlace = AlertState == EHitmenAlertState::Investigating && FVector::Dist2D(Location, InvestigateLocation) < SameInvestigationRadius;
	InvestigateLocation = Location;
	if (AlertState == EHitmenAlertState::Combat)
	{
		return;
	}
	// Already looking into that very place (a repeated radio call, the same noise again): the running timer goes on,
	// so an alert always ends (repeated calls used to restart it for ever).
	if (bSamePlace)
	{
		bThreatInvestigation |= bThreat;
		return;
	}
	const bool bAlreadyInvestigating = AlertState == EHitmenAlertState::Investigating;
	SetAlertStateWithReason(EHitmenAlertState::Investigating, Reason);
	bThreatInvestigation = (bAlreadyInvestigating && bThreatInvestigation) || bThreat;
	// The timer starts on arrival (see TickComponent): the walk there does not count.
	bInvestigateArrived = false;
	StateEnterTime = GetWorld()->GetTimeSeconds();
}

void UHitmenAlertComponent::EngageTarget(AActor* NewTarget)
{
	if (!NewTarget)
	{
		return;
	}
	Target = NewTarget;
	LastKnownLocation = NewTarget->GetActorLocation();
	LastSeenLocation = LastKnownLocation;
	LastSeenTime = GetWorld()->GetTimeSeconds();
	bTargetVisible = true;
	bAlertedByCall = false;
	SetAlertStateWithReason(EHitmenAlertState::Combat, EHitmenAlertReason::Spotted);
}

void UHitmenAlertComponent::StartSearchAround(const FVector& Location)
{
	if (!GetOwner()->HasAuthority())
	{
		return;
	}
	Target = nullptr;
	bTargetVisible = false;
	LastKnownLocation = Location;
	SetAlertStateWithReason(EHitmenAlertState::Searching, EHitmenAlertReason::LostTarget);
	StateEnterTime = GetWorld()->GetTimeSeconds();
	RefreshSearchMarker();
}

void UHitmenAlertComponent::RefreshSearchMarker()
{
	if (!GetOwner() || !GetOwner()->HasAuthority())
	{
		return;
	}
	if (UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this))
	{
		// The ghost marks where the target was last seen (never a noise it went to check).
		AActor* Searched = AlertState == EHitmenAlertState::Searching && !bSuppressed && !IsDeadActor(Target) ? Target.Get() : nullptr;
		AI->UpdateSearcher(this, Searched, LastSeenTime > 0.f ? LastSeenLocation : LastKnownLocation);
	}
}

// ---- Stimuli ----

void UHitmenAlertComponent::SeeHostile(AActor* Seen, float DeltaTime, bool bAlarming, float RateScale)
{
	if (bSuppressed || !Seen)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	const float Distance = FVector::Dist(GetOwner()->GetActorLocation(), Seen->GetActorLocation());
	LastSeenTime = Now;
	LastKnownLocation = Seen->GetActorLocation();
	LastSeenLocation = LastKnownLocation;
	bAlertedByCall = false;
	// The "Hitman service": everyone hunting this target learns where it was just seen.
	if (UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this); AI && (AlertState == EHitmenAlertState::Combat || AlertState == EHitmenAlertState::Searching))
	{
		AI->ReportSighting(Seen, LastKnownLocation);
	}

	// Already red, or searching for you (found you), or point blank: red.
	if (AlertState == EHitmenAlertState::Combat || AlertState == EHitmenAlertState::Searching
		|| Distance <= InstantDetectionDistance)
	{
		Target = Seen;
		bTargetVisible = true;
		SetAlertStateWithReason(EHitmenAlertState::Combat, EHitmenAlertReason::Spotted);
		return;
	}

	// Gradual detection: the white fill must complete while seeing you to turn red. The distance decides how fast (near
	// instant up close, slow far away); peripheral vision and partial cover fill it slower (RateScale); an NPC on edge
	// or investigating fills it faster.
	const float DistanceRate = FMath::Max(0.f, SuspicionRateByDistance.GetRichCurveConst()->Eval(Distance, 0.25f));
	const float AlertedMultiplier = AlertState == EHitmenAlertState::Investigating ? AlertedSuspicionMultiplier : (bOnEdge ? OnEdgeSuspicionMultiplier : 1.f);
	const float Rate = SuspicionRiseRate * DistanceRate * (bAlarming ? AlarmingMultiplier : 1.f) * AlertedMultiplier * GetDifficultySuspicionMultiplier()
		* FMath::Clamp(RateScale, 0.f, 1.f);
	SetSuspicionValue(FMath::Clamp(Suspicion + Rate * DeltaTime, 0.f, 1.f));
	Target = Seen;
	bTargetVisible = true;
	if (Suspicion >= 1.f)
	{
		SetAlertStateWithReason(EHitmenAlertState::Combat, EHitmenAlertReason::Spotted);
	}
	else if (AlertState == EHitmenAlertState::Unaware)
	{
		SetAlertStateWithReason(EHitmenAlertState::Suspicious, EHitmenAlertReason::Glimpse);
	}
	else if (AlertState == EHitmenAlertState::Investigating)
	{
		// A yellow NPC follows what it keeps glimpsing.
		InvestigateLocation = LastKnownLocation;
		bThreatInvestigation = true;
		StateEnterTime = Now;
	}
}

void UHitmenAlertComponent::LoseSightOf(AActor* Seen)
{
	if (Seen == Target)
	{
		bTargetVisible = false;
	}
}

void UHitmenAlertComponent::HearNoise(const FVector& Location, EHitmenNoiseType Type, AActor* NoiseInstigator, float Distance)
{
	if (!WouldReactToNoise())
	{
		return;
	}
	const bool bHostileSource = UHitmenTeamLibrary::AreHostile(GetOwner(), NoiseInstigator);
	const bool bLoud = Type == EHitmenNoiseType::Gunshot || Type == EHitmenNoiseType::Explosion;

	// Hearing never reveals where a shooter is: the NPC goes to the sound (for gunfire, where the bullet
	// landed). Exception: in the dark, a hostile heard right next to it is attacked (nothing else to go on).
	if (bHostileSource && NoiseInstigator && Distance <= UHitmenAISubsystem::GetRules(this).DarkCloseEngageDistance)
	{
		const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
		if (AI && AI->IsLocationDark(GetOwner()->GetActorLocation()))
		{
			EngageTarget(NoiseInstigator);
			return;
		}
	}
	if (AlertState == EHitmenAlertState::Combat)
	{
		if (!bTargetVisible)
		{
			LastKnownLocation = Location;
		}
		return;
	}
	if (AlertState == EHitmenAlertState::Searching)
	{
		LastKnownLocation = Location;
		StateEnterTime = GetWorld()->GetTimeSeconds();
		RefreshSearchMarker();
		return;
	}
	// Gunfire, explosions and fighting are threats; footsteps and distractions are only curiosities. A gunshot (its
	// report and its impact alike) is heard where the bullet landed: the guard turns to the impact, never to the shooter.
	const bool bThreat = bLoud || Type == EHitmenNoiseType::Combat;
	if (Type == EHitmenNoiseType::Distraction && AlertState != EHitmenAlertState::Investigating)
	{
		AHitmenPlayerController::NotifyDistractionNoticed(GetOwner(), NoiseInstigator);
	}
	SetInvestigate(Location, bThreat, bThreat ? EHitmenAlertReason::Gunfire : EHitmenAlertReason::Noise);
}

void UHitmenAlertComponent::SeeBody(AHitmenCharacterBase* Body)
{
	if (bSuppressed || !bReactsToBodies || !Body || KnownBodies.Contains(Body))
	{
		return;
	}
	KnownBodies.Add(Body);
	// Like Hitman's dead-body service: a body is reported once; whoever finds it first calls it in, the others already
	// know (they come because of the call, not each on their own).
	UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	if (AI && !AI->TryReportBody(Body))
	{
		return;
	}
	if (AlertState != EHitmenAlertState::Combat)
	{
		SetInvestigate(Body->GetActorLocation(), true, EHitmenAlertReason::Body);
		BodyFound = Body;
		UE_LOG(LogHitmen, Log, TEXT("%s: found the body of %s"), *GetNameSafe(GetOwner()), *GetNameSafe(Body));
	}
}

void UHitmenAlertComponent::StartBodySearch(AHitmenCharacterBase* Body)
{
	UE_LOG(LogHitmen, Log, TEXT("%s: searching around the body of %s"), *GetNameSafe(GetOwner()), *GetNameSafe(Body));
	// Hitman: whoever finds a body looks around it for a while (EQS search points), then calms down.
	StartSearchAround(Body ? Body->GetActorLocation() : GetOwner()->GetActorLocation());
	bBodySearch = true;
}

void UHitmenAlertComponent::TryStartBodyDisposal(AHitmenCharacterBase* Body)
{
	const UHitmenMatchRules& Rules = UHitmenAISubsystem::GetRules(this);
	// The VIP's body is never carried away by a guard (it must stay for the players).
	if (!Rules.bNPCsDisposeBodies || !Body || Body->IsVIP() || !Body->IsDead() || Body->GetCarrier() || Body->GetStowedIn() || Body->IsVanished())
	{
		return;
	}
	const UHitmenBodyCarryComponent* Carry = GetOwner()->FindComponentByClass<UHitmenBodyCarryComponent>();
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	AHitmenHidingSpot* Spot = AI ? AI->FindContainerForBody(Body, Rules.MaxBodyDisposalDistance) : nullptr;
	if (!Spot)
	{
		return;
	}
	// A sniper on its tower or a guard holding its post cannot walk the body away: the nearest colleague who can does.
	const AHitmenNPC* Self = Cast<AHitmenNPC>(GetOwner());
	const bool bCanWalk = Carry && Self && !Self->IsSniper() && !Self->HoldsPosition();
	UHitmenAlertComponent* Handler = bCanWalk ? this : nullptr;
	if (!Handler && AI)
	{
		float Best = Rules.MaxBodyDisposalDistance;
		for (UHitmenAlertComponent* Other : const_cast<UHitmenAISubsystem*>(AI)->GetAlertComponents())
		{
			const AHitmenNPC* NPC = Other ? Cast<AHitmenNPC>(Other->GetOwner()) : nullptr;
			if (!NPC || NPC == Self || NPC->IsDead() || NPC->IsSniper() || NPC->HoldsPosition() || NPC->IsVIP()
				|| !NPC->FindComponentByClass<UHitmenBodyCarryComponent>() || Other->GetAlertState() == EHitmenAlertState::Combat || Other->GetDisposeBody())
			{
				continue;
			}
			const float Distance = FVector::Dist(NPC->GetActorLocation(), Body->GetActorLocation());
			if (Distance < Best)
			{
				Best = Distance;
				Handler = Other;
			}
		}
	}
	UE_LOG(LogHitmen, Log, TEXT("%s: body of %s -> %s by %s"), *GetNameSafe(GetOwner()), *GetNameSafe(Body), *GetNameSafe(Spot),
		Handler ? *GetNameSafe(Handler->GetOwner()) : TEXT("nobody"));
	if (Handler)
	{
		Handler->AssignBodyDisposal(Body, Spot);
	}
}

void UHitmenAlertComponent::AssignBodyDisposal(AHitmenCharacterBase* Body, AActor* Spot)
{
	DisposeBody = Body;
	DisposeContainer = Spot;
	BodyFound = Body;
	// Stand in front of it (its origin is inside it, off the navmesh).
	DisposeLocation = Spot ? Spot->GetActorLocation() + Spot->GetActorForwardVector() * 130.f : FVector::ZeroVector;
	if (const UNavigationSystemV1* Nav = FNavigationSystem::GetCurrent<UNavigationSystemV1>(GetWorld()))
	{
		FNavLocation Projected;
		if (Nav->ProjectPointToNavigation(DisposeLocation, Projected, FVector(150.f, 150.f, 250.f)))
		{
			DisposeLocation = Projected.Location;
		}
	}
}

AActor* UHitmenAlertComponent::GetDisposeBody() const
{
	return DisposeBody.Get();
}

void UHitmenAlertComponent::EndBodyDisposal()
{
	// Never left holding a body.
	if (UHitmenBodyCarryComponent* Carry = GetOwner() ? GetOwner()->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr; Carry && Carry->IsCarrying())
	{
		Carry->Drop();
	}
	DisposeBody.Reset();
	DisposeContainer.Reset();
	BodyFound.Reset();
}

void UHitmenAlertComponent::SeeSleeper(AHitmenCharacterBase* Sleeper)
{
	if (bSuppressed || !Sleeper || AlertState == EHitmenAlertState::Combat || AlertState == EHitmenAlertState::Searching || WakeTarget.IsValid())
	{
		return;
	}
	UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	if (AI && !AI->TryReportBody(Sleeper))
	{
		return;
	}
	WakeTarget = Sleeper;
	SetInvestigate(Sleeper->GetActorLocation(), true, EHitmenAlertReason::Sleeper);
}

void UHitmenAlertComponent::SeeEvidence(AActor* Evidence)
{
	if (bSuppressed || !Evidence || AlertState != EHitmenAlertState::Unaware || EvidenceTarget.IsValid())
	{
		return;
	}
	UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	if (AI && !AI->TryClaimEvidence(Evidence, GetOwner()))
	{
		return;
	}
	EvidenceTarget = Evidence;
	SetInvestigate(Evidence->GetActorLocation(), false, EHitmenAlertReason::Evidence);
}

void UHitmenAlertComponent::HandleInvestigateTarget()
{
	const FVector Here = GetOwner()->GetActorLocation();
	if (AHitmenCharacterBase* Sleeper = WakeTarget.Get(); Sleeper && FVector::Dist2D(Here, Sleeper->GetActorLocation()) <= HandleReach)
	{
		if (UHitmenStatusEffectComponent* Status = Sleeper->GetStatusEffects())
		{
			Status->RemoveStatus(EHitmenStatusEffect::Asleep);
		}
		// Darted again later, it can be found again.
		if (UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this))
		{
			AI->ForgetBody(Sleeper);
		}
		WakeTarget.Reset();
	}
	// Someone picked it up on the way: nothing to confiscate.
	if (const AHitmenWeaponBase* Weapon = Cast<AHitmenWeaponBase>(EvidenceTarget.Get()); Weapon && !Weapon->IsEvidence())
	{
		EvidenceTarget.Reset();
	}
	if (AActor* Evidence = EvidenceTarget.Get(); Evidence && FVector::Dist2D(Here, Evidence->GetActorLocation()) <= HandleReach)
	{
		// Confiscated: a weapon left lying around is taken away (Hitman's guards bag evidence).
		Evidence->Destroy();
		EvidenceTarget.Reset();
	}
}

void UHitmenAlertComponent::WitnessKill(AActor* Killer)
{
	if (!bSuppressed && UHitmenTeamLibrary::AreHostile(GetOwner(), Killer))
	{
		EngageTarget(Killer);
	}
}

void UHitmenAlertComponent::OnDamagedBy(AActor* DamageInstigator)
{
	LastDamageTime = GetWorld()->GetTimeSeconds();
	if (bSuppressed)
	{
		return;
	}
	if (DamageInstigator && UHitmenTeamLibrary::AreHostile(GetOwner(), DamageInstigator))
	{
		const APawn* OwnerPawn = Cast<APawn>(GetOwner());
		const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
		const bool bSeen = AI && OwnerPawn && AI->CanObserverSee(OwnerPawn, DamageInstigator, TNumericLimits<float>::Max(), 180.f, true);
		if (bSeen)
		{
			EngageTarget(DamageInstigator);
		}
		else if (!(AlertState == EHitmenAlertState::Combat && bTargetVisible))
		{
			// Hit by someone it cannot see: alarmed, it ducks into cover from where the shot came from (a guess along the
			// shot, never the shooter's real position), then searches there.
			const FVector Here = GetOwner()->GetActorLocation();
			const FVector Toward = (DamageInstigator->GetActorLocation() - Here).GetSafeNormal2D();
			const FVector Guess = Here + Toward * UnseenAttackerSearchDistance;
			if (bTakeCoverFromUnseenThreats)
			{
				TakeCoverFrom(DamageInstigator, Guess, UnseenThreatCoverDuration, EHitmenAlertReason::Damage);
			}
			else
			{
				Target = DamageInstigator;
				bTargetVisible = false;
				bAlertedByCall = false;
				LastKnownLocation = Guess;
				LastSeenTime = GetWorld()->GetTimeSeconds();
				SetAlertStateWithReason(EHitmenAlertState::Combat, EHitmenAlertReason::Damage);
			}
		}
	}
	else if (AlertState != EHitmenAlertState::Combat)
	{
		bAlertedByCall = false;
		SetInvestigate(GetOwner()->GetActorLocation(), true, EHitmenAlertReason::Damage);
	}
}

void UHitmenAlertComponent::ReceiveReinforcementCall(const FVector& Location, AActor* CallTarget)
{
	if (!WouldAnswerCall())
	{
		return;
	}
	// Only flag NPCs the call itself alerted: those already alerted keep their own right to call.
	if (!IsAlertedState(AlertState))
	{
		bAlertedByCall = true;
	}
	if (CallTarget)
	{
		Target = CallTarget;
		LastKnownLocation = Location;
		bTargetVisible = false;
		LastSeenTime = GetWorld()->GetTimeSeconds();
		SetAlertStateWithReason(EHitmenAlertState::Searching, EHitmenAlertReason::Call);
		StateEnterTime = GetWorld()->GetTimeSeconds();
		RefreshSearchMarker();
		return;
	}
	SetInvestigate(Location, true, EHitmenAlertReason::Call);
}

// ---- Timers ----

void UHitmenAlertComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	if (bSuppressed)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();

	// Target died: forget it first (no marker at a dead player's spot), then fall back.
	if (Target && IsDeadActor(Target))
	{
		Target = nullptr;
		bTargetVisible = false;
		if (AlertState == EHitmenAlertState::Combat)
		{
			// No bark: the threat is gone.
			SetAlertStateWithReason(EHitmenAlertState::Searching, EHitmenAlertReason::None);
		}
		else if (AlertState == EHitmenAlertState::Suspicious)
		{
			SetAlertStateWithReason(EHitmenAlertState::Unaware, EHitmenAlertReason::None);
		}
		RefreshSearchMarker();
	}

	if (bOnEdge && Now > OnEdgeUntil)
	{
		bOnEdge = false;
	}
	if (DisposeBody.IsValid() && AlertState != EHitmenAlertState::Unaware)
	{
		EndBodyDisposal();
	}
	// Shared knowledge: pull the latest sighting of the hunted target (a colleague may have seen it since).
	SharedKnowledgeAccumulator += DeltaTime;
	if (SharedKnowledgeAccumulator >= SharedKnowledgeInterval && Target && !bTargetVisible
		&& (AlertState == EHitmenAlertState::Searching || AlertState == EHitmenAlertState::Combat))
	{
		SharedKnowledgeAccumulator = 0.f;
		FVector Shared;
		float SharedTime = 0.f;
		if (const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this); AI && AI->GetSharedSighting(Target, Shared, SharedTime) && SharedTime > LastSeenTime)
		{
			LastKnownLocation = Shared;
			LastSeenLocation = Shared;
			LastSeenTime = SharedTime;
			if (AlertState == EHitmenAlertState::Searching)
			{
				StateEnterTime = Now;
			}
			RefreshSearchMarker();
		}
	}

	const bool bLostSight = Now - LastSeenTime > LoseSightGraceTime;
	switch (AlertState)
	{
	case EHitmenAlertState::Suspicious:
		if (bLostSight)
		{
			if (Target && Suspicion >= InvestigateSuspicionThreshold)
			{
				// "Hm?": it walks over to check the spot it glimpsed (yellow).
				SetInvestigate(LastKnownLocation, Suspicion >= 0.5f, EHitmenAlertReason::Approach);
			}
			else
			{
				SetSuspicionValue(FMath::Max(0.f, Suspicion - SuspicionDecayRate * DeltaTime));
				if (Suspicion <= 0.f)
				{
					SetAlertStateWithReason(EHitmenAlertState::Unaware, EHitmenAlertReason::AllClear);
				}
			}
		}
		break;
	case EHitmenAlertState::Combat:
		if (bTargetVisible)
		{
			// Sees the shooter: cover from a guess is over, it fights.
			CoverUntil = -1.f;
			bPanicking = false;
		}
		else if (CoverUntil > 0.f && Now >= CoverUntil)
		{
			// Done hiding: search where the threat seemed to come from.
			CoverUntil = -1.f;
			bPanicking = false;
			SetAlertStateWithReason(EHitmenAlertState::Searching, EHitmenAlertReason::LostTarget);
		}
		else if (!IsTakingCover() && Now - LastSeenTime > GetRuleLoseTime())
		{
			SetAlertStateWithReason(EHitmenAlertState::Searching, EHitmenAlertReason::LostTarget);
		}
		break;
	case EHitmenAlertState::Searching:
		if (Now - StateEnterTime > GetRuleSearchTime())
		{
			if (IsOwnerCharmed())
			{
				// Charmed pawns keep hunting for their new side until the charm ends.
				StartSearchAround(GetOwner()->GetActorLocation());
			}
			else
			{
				// Nothing found: back to routine, on edge for a while (after a body search: then carry it away).
				const bool bWasBodySearch = bBodySearch;
				bBodySearch = false;
				EnterOnEdge();
				SetAlertStateWithReason(EHitmenAlertState::Unaware, EHitmenAlertReason::SearchOver);
				if (bWasBodySearch)
				{
					TryStartBodyDisposal(BodyFound.Get());
				}
			}
		}
		break;
	case EHitmenAlertState::Investigating:
	{
		if (bLostSight)
		{
			SetSuspicionValue(FMath::Max(0.f, Suspicion - SuspicionDecayRate * DeltaTime));
		}
		// The look-around timer starts on arrival; a walk that never arrives gives up after a while.
		if (!bInvestigateArrived && FVector::Dist2D(GetOwner()->GetActorLocation(), InvestigateLocation) <= InvestigateArrivalRadius)
		{
			bInvestigateArrived = true;
			StateEnterTime = Now;
		}
		if (bInvestigateArrived)
		{
			HandleInvestigateTarget();
		}
		const float Limit = bInvestigateArrived ? GetRuleInvestigateTime() : InvestigateTravelTimeout;
		if (Now - StateEnterTime > Limit)
		{
			const bool bWasThreat = bThreatInvestigation;
			WakeTarget.Reset();
			EvidenceTarget.Reset();
			// The investigation was the body (a radio call or a noise may have changed the reason since, not the place).
			if (AHitmenCharacterBase* Body = BodyFound.Get(); Body && FVector::Dist2D(InvestigateLocation, Body->GetActorLocation()) < 400.f)
			{
				// A body: search around it before calming down.
				StartBodySearch(Body);
				break;
			}
			if (bWasThreat)
			{
				EnterOnEdge();
			}
			SetAlertStateWithReason(EHitmenAlertState::Unaware, EHitmenAlertReason::AllClear);
		}
		break;
	}
	default:
		break;
	}
}

// ---- Sixth sense, cover, panic ----

void UHitmenAlertComponent::SenseNearby(AActor* Hostile, float DeltaTime, float Rate)
{
	if (bSuppressed || !Hostile || AlertState == EHitmenAlertState::Combat || AlertState == EHitmenAlertState::Searching)
	{
		return;
	}
	// A presence felt behind: the white fill rises (never to red on its own) and the NPC turns toward it (Suspicious
	// faces its target), so it sees whoever is there.
	SetSuspicionValue(FMath::Clamp(Suspicion + Rate * DeltaTime, 0.f, 0.95f));
	Target = Hostile;
	LastKnownLocation = Hostile->GetActorLocation();
	LastSeenLocation = LastKnownLocation;
	LastSeenTime = GetWorld()->GetTimeSeconds();
	if (AlertState == EHitmenAlertState::Unaware)
	{
		SetAlertStateWithReason(EHitmenAlertState::Suspicious, EHitmenAlertReason::Glimpse);
	}
}

bool UHitmenAlertComponent::IsTakingCover() const
{
	return CoverUntil > 0.f && GetWorld() && GetWorld()->GetTimeSeconds() < CoverUntil;
}

void UHitmenAlertComponent::TakeCoverFrom(AActor* Threat, const FVector& ThreatLocation, float Duration, EHitmenAlertReason Reason)
{
	if (bSuppressed || !GetOwner()->HasAuthority())
	{
		return;
	}
	// Already fighting someone it sees: no hiding from a guess.
	if (AlertState == EHitmenAlertState::Combat && bTargetVisible)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	const bool bWasCombat = AlertState == EHitmenAlertState::Combat;
	if (Threat)
	{
		Target = Threat;
	}
	bTargetVisible = false;
	bAlertedByCall = false;
	LastKnownLocation = ThreatLocation;
	LastSeenTime = Now;
	CoverUntil = FMath::Max(CoverUntil, Now + Duration);
	bPanicking = bPanicking || Reason == EHitmenAlertReason::Panic;
	SetAlertStateWithReason(EHitmenAlertState::Combat, Reason);
	if (bWasCombat && (Reason == EHitmenAlertReason::Panic || Reason == EHitmenAlertReason::TakeCover))
	{
		ForceBark(Reason);
	}
}

void UHitmenAlertComponent::RecordAllyDeath(AActor* Killer, const FVector& DeathLocation)
{
	if (bSuppressed || !GetOwner()->HasAuthority() || !bTakeCoverFromUnseenThreats)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	AllyDeathTimes.RemoveAll([this, Now](float Time) { return Now - Time > AllyDeathWindow; });
	AllyDeathTimes.Add(Now);
	// Where the shot seemed to come from: from the body toward the killer, a short way out (never its real position).
	const FVector Here = GetOwner()->GetActorLocation();
	const FVector FromBody = Killer ? (Killer->GetActorLocation() - DeathLocation).GetSafeNormal2D() : (DeathLocation - Here).GetSafeNormal2D();
	const FVector Guess = DeathLocation + FromBody * FMath::Max(UnseenAttackerSearchDistance, 600.f);
	if (AllyDeathTimes.Num() >= PanicAllyDeaths)
	{
		TakeCoverFrom(Killer, Guess, PanicDuration, EHitmenAlertReason::Panic);
	}
	else
	{
		TakeCoverFrom(Killer, Guess, UnseenThreatCoverDuration, EHitmenAlertReason::TakeCover);
	}
}

void UHitmenAlertComponent::LoseTrackOf(AActor* Hostile, const FVector& LastPlace)
{
	if (bSuppressed || !Hostile || Target != Hostile || !GetOwner()->HasAuthority())
	{
		return;
	}
	if (AlertState != EHitmenAlertState::Combat && AlertState != EHitmenAlertState::Searching && AlertState != EHitmenAlertState::Suspicious)
	{
		return;
	}
	// Gone in a blink: it searches where the target was, as if it had lost sight of it there.
	bTargetVisible = false;
	CoverUntil = -1.f;
	bPanicking = false;
	LastKnownLocation = LastPlace;
	LastSeenLocation = LastPlace;
	if (AlertState == EHitmenAlertState::Suspicious)
	{
		SetInvestigate(LastPlace, false, EHitmenAlertReason::Approach);
		return;
	}
	SetAlertStateWithReason(EHitmenAlertState::Searching, EHitmenAlertReason::LostTarget);
	StateEnterTime = GetWorld()->GetTimeSeconds();
	RefreshSearchMarker();
}
