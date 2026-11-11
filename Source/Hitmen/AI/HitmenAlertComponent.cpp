// Hitmen — alert state machine

#include "AI/HitmenAlertComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Framework/HitmenGameState.h"
#include "Data/HitmenMatchRules.h"
#include "Core/HitmenTeamLibrary.h"
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
}

void UHitmenAlertComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAlertComponent, AlertState);
	DOREPLIFETIME(UHitmenAlertComponent, Suspicion);
	DOREPLIFETIME(UHitmenAlertComponent, bCallingReinforcements);
	DOREPLIFETIME(UHitmenAlertComponent, Target);
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
	return UHitmenAISubsystem::GetRules(this).SearchDuration;
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

// ---- State ----

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

void UHitmenAlertComponent::SetInvestigate(const FVector& Location, bool bThreat)
{
	InvestigateLocation = Location;
	if (AlertState == EHitmenAlertState::Combat)
	{
		return;
	}
	const bool bAlreadyInvestigating = AlertState == EHitmenAlertState::Investigating;
	SetAlertState(EHitmenAlertState::Investigating);
	bThreatInvestigation = (bAlreadyInvestigating && bThreatInvestigation) || bThreat;
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
	LastSeenTime = GetWorld()->GetTimeSeconds();
	bTargetVisible = true;
	bAlertedByCall = false;
	SetAlertState(EHitmenAlertState::Combat);
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
	SetAlertState(EHitmenAlertState::Searching);
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
		AActor* Searched = AlertState == EHitmenAlertState::Searching && !bSuppressed && !IsDeadActor(Target) ? Target.Get() : nullptr;
		AI->UpdateSearcher(this, Searched, LastKnownLocation);
	}
}

// ---- Stimuli ----

void UHitmenAlertComponent::SeeHostile(AActor* Seen, float DeltaTime, bool bAlarming)
{
	if (bSuppressed || !Seen)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	const float Distance = FVector::Dist(GetOwner()->GetActorLocation(), Seen->GetActorLocation());
	LastSeenTime = Now;
	LastKnownLocation = Seen->GetActorLocation();
	bAlertedByCall = false;

	// Already red, or searching for you (found you), or point blank: red.
	if (AlertState == EHitmenAlertState::Combat || AlertState == EHitmenAlertState::Searching
		|| Distance <= InstantDetectionDistance)
	{
		Target = Seen;
		bTargetVisible = true;
		SetAlertState(EHitmenAlertState::Combat);
		return;
	}

	// Gradual detection: the white fill must complete while seeing you to turn red.
	const float DistanceAlpha = 1.f - FMath::Clamp(Distance / SuspicionFalloffDistance, 0.f, 0.85f);
	const float AlertedMultiplier = AlertState == EHitmenAlertState::Investigating ? AlertedSuspicionMultiplier : 1.f;
	const float Rate = SuspicionRiseRate * DistanceAlpha * (bAlarming ? AlarmingMultiplier : 1.f) * AlertedMultiplier * GetDifficultySuspicionMultiplier();
	SetSuspicionValue(FMath::Clamp(Suspicion + Rate * DeltaTime, 0.f, 1.f));
	Target = Seen;
	bTargetVisible = true;
	if (Suspicion >= 1.f)
	{
		SetAlertState(EHitmenAlertState::Combat);
	}
	else if (AlertState == EHitmenAlertState::Unaware)
	{
		SetAlertState(EHitmenAlertState::Suspicious);
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
	// Gunfire, explosions and fighting are threats; footsteps and distractions are only curiosities.
	const bool bThreat = bLoud || Type == EHitmenNoiseType::Combat;
	SetInvestigate(Location, bThreat);
}

void UHitmenAlertComponent::SeeBody(AHitmenCharacterBase* Body)
{
	if (bSuppressed || !bReactsToBodies || !Body || KnownBodies.Contains(Body))
	{
		return;
	}
	KnownBodies.Add(Body);
	if (AlertState != EHitmenAlertState::Combat)
	{
		SetInvestigate(Body->GetActorLocation(), true);
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
			// Hit by someone it cannot see: alarmed, it turns toward where the shot came from and searches.
			const FVector Here = GetOwner()->GetActorLocation();
			const FVector Toward = (DamageInstigator->GetActorLocation() - Here).GetSafeNormal2D();
			Target = DamageInstigator;
			bTargetVisible = false;
			bAlertedByCall = false;
			LastKnownLocation = Here + Toward * UnseenAttackerSearchDistance;
			LastSeenTime = GetWorld()->GetTimeSeconds();
			SetAlertState(EHitmenAlertState::Combat);
		}
	}
	else if (AlertState != EHitmenAlertState::Combat)
	{
		bAlertedByCall = false;
		SetInvestigate(GetOwner()->GetActorLocation(), true);
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
		SetAlertState(EHitmenAlertState::Searching);
		StateEnterTime = GetWorld()->GetTimeSeconds();
		RefreshSearchMarker();
		return;
	}
	SetInvestigate(Location, true);
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
			SetAlertState(EHitmenAlertState::Searching);
		}
		else if (AlertState == EHitmenAlertState::Suspicious)
		{
			SetAlertState(EHitmenAlertState::Unaware);
		}
		RefreshSearchMarker();
	}

	const bool bLostSight = Now - LastSeenTime > LoseSightGraceTime;
	switch (AlertState)
	{
	case EHitmenAlertState::Suspicious:
		if (bLostSight)
		{
			if (Target && Suspicion >= InvestigateSuspicionThreshold)
			{
				// Looked long enough: white turns yellow and comes to check the last seen spot.
				SetInvestigate(LastKnownLocation, true);
			}
			else
			{
				SetSuspicionValue(FMath::Max(0.f, Suspicion - SuspicionDecayRate * DeltaTime));
				if (Suspicion <= 0.f)
				{
					SetAlertState(EHitmenAlertState::Unaware);
				}
			}
		}
		break;
	case EHitmenAlertState::Combat:
		if (!bTargetVisible && Now - LastSeenTime > GetRuleLoseTime())
		{
			SetAlertState(EHitmenAlertState::Searching);
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
				SetAlertState(EHitmenAlertState::Unaware);
			}
		}
		break;
	case EHitmenAlertState::Investigating:
		if (bLostSight)
		{
			SetSuspicionValue(FMath::Max(0.f, Suspicion - SuspicionDecayRate * DeltaTime));
		}
		if (Now - StateEnterTime > GetRuleInvestigateTime())
		{
			SetAlertState(EHitmenAlertState::Unaware);
		}
		break;
	default:
		break;
	}
}
