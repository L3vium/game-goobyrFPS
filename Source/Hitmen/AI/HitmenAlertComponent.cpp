// Hitmen — alert state machine

#include "AI/HitmenAlertComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "Characters/HitmenCharacterBase.h"
#include "Framework/HitmenGameModeBase.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenPlayerController.h"
#include "Data/HitmenMatchRules.h"
#include "Core/HitmenTeamLibrary.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

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
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	return GM ? GM->GetRules()->LoseTargetTime : 6.f;
}

float UHitmenAlertComponent::GetRuleSearchTime() const
{
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	return GM ? GM->GetRules()->SearchDuration : 25.f;
}

float UHitmenAlertComponent::GetRuleInvestigateTime() const
{
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	return GM ? GM->GetRules()->InvestigateDuration : 8.f;
}

float UHitmenAlertComponent::GetDifficultySuspicionMultiplier() const
{
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	if (!GM || !GS)
	{
		return 1.f;
	}
	return GM->GetRules()->GetPreset(GS->GetLobbySettings().Difficulty).NPCSuspicionRateMultiplier;
}

// ---- State ----

void UHitmenAlertComponent::SetAlertState(EHitmenAlertState NewState)
{
	if (!GetOwner()->HasAuthority() || AlertState == NewState)
	{
		return;
	}
	const EHitmenAlertState Old = AlertState;
	AlertState = NewState;
	StateEnterTime = GetWorld()->GetTimeSeconds();
	if (NewState == EHitmenAlertState::Unaware)
	{
		Suspicion = 0.f;
		Target = nullptr;
	}
	if (NewState == EHitmenAlertState::Combat)
	{
		Suspicion = 1.f;
	}
	UpdateLastKnownMarker(NewState == EHitmenAlertState::Searching);
	if (Old == EHitmenAlertState::Searching && NewState != EHitmenAlertState::Searching)
	{
		UpdateLastKnownMarker(false);
	}
	OnRep_AlertState();
}

void UHitmenAlertComponent::OnRep_AlertState()
{
	const EHitmenAlertState Old = PreviousRepState;
	PreviousRepState = AlertState;
	Juice_OnAlertStateChanged(Old, AlertState);
	OnAlertStateChanged.Broadcast(Old, AlertState);
}

void UHitmenAlertComponent::SetCallingReinforcements(bool bCalling)
{
	if (GetOwner()->HasAuthority() && bCallingReinforcements != bCalling)
	{
		bCallingReinforcements = bCalling;
		OnRep_Calling();
	}
}

void UHitmenAlertComponent::OnRep_Calling()
{
	OnCallingChanged.Broadcast(bCallingReinforcements);
}

void UHitmenAlertComponent::SetSuppressed(bool bInSuppressed)
{
	bSuppressed = bInSuppressed;
	if (bSuppressed)
	{
		SetCallingReinforcements(false);
	}
}

void UHitmenAlertComponent::SetInvestigate(const FVector& Location)
{
	InvestigateLocation = Location;
	if (AlertState != EHitmenAlertState::Combat)
	{
		SetAlertState(EHitmenAlertState::Investigating);
		StateEnterTime = GetWorld()->GetTimeSeconds();
	}
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
	SetAlertState(EHitmenAlertState::Combat);
}

void UHitmenAlertComponent::UpdateLastKnownMarker(bool bShow) const
{
	const APawn* TargetPawn = Cast<APawn>(Target);
	if (AHitmenPlayerController* PC = TargetPawn ? Cast<AHitmenPlayerController>(TargetPawn->GetController()) : nullptr)
	{
		PC->Client_ShowLastKnownPosition(LastKnownLocation, bShow);
	}
}

// ---- Stimuli ----

void UHitmenAlertComponent::SeeHostile(AActor* Seen, float DeltaTime, bool bAlarming)
{
	if (bSuppressed || !Seen)
	{
		return;
	}
	const float Distance = FVector::Dist(GetOwner()->GetActorLocation(), Seen->GetActorLocation());
	LastSeenTime = GetWorld()->GetTimeSeconds();
	LastKnownLocation = Seen->GetActorLocation();

	if (AlertState == EHitmenAlertState::Combat || AlertState == EHitmenAlertState::Searching
		|| Distance <= InstantDetectionDistance)
	{
		const bool bWasSearching = AlertState == EHitmenAlertState::Searching;
		Target = Seen;
		bTargetVisible = true;
		SetAlertState(EHitmenAlertState::Combat);
		if (bWasSearching)
		{
			UpdateLastKnownMarker(false);
		}
		return;
	}

	// Gradual detection: white fill, then red.
	const float DistanceAlpha = 1.f - FMath::Clamp(Distance / SuspicionFalloffDistance, 0.f, 0.85f);
	const float Rate = SuspicionRiseRate * DistanceAlpha * (bAlarming ? AlarmingMultiplier : 1.f) * GetDifficultySuspicionMultiplier();
	Suspicion = FMath::Clamp(Suspicion + Rate * DeltaTime, 0.f, 1.f);
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
	if (bSuppressed)
	{
		return;
	}
	// Vision primes over sound: in combat with a visible target, noises are ignored.
	if (AlertState == EHitmenAlertState::Combat && bTargetVisible)
	{
		return;
	}
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	const float CombatRadius = GM ? GM->GetRules()->GunshotCombatRadius : 900.f;
	const bool bHostileSource = UHitmenTeamLibrary::AreHostile(GetOwner(), NoiseInstigator);

	if ((Type == EHitmenNoiseType::Gunshot || Type == EHitmenNoiseType::Explosion) && bHostileSource && Distance <= CombatRadius)
	{
		EngageTarget(NoiseInstigator);
		return;
	}
	if (AlertState == EHitmenAlertState::Combat)
	{
		LastKnownLocation = Location;
		return;
	}
	if (AlertState == EHitmenAlertState::Searching)
	{
		LastKnownLocation = Location;
		StateEnterTime = GetWorld()->GetTimeSeconds();
		return;
	}
	SetInvestigate(Location);
}

void UHitmenAlertComponent::SeeBody(AHitmenCharacterBase* Body)
{
	if (bSuppressed || !Body || KnownBodies.Contains(Body))
	{
		return;
	}
	KnownBodies.Add(Body);
	if (AlertState != EHitmenAlertState::Combat)
	{
		SetInvestigate(Body->GetActorLocation());
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
		EngageTarget(DamageInstigator);
	}
	else if (AlertState != EHitmenAlertState::Combat)
	{
		SetInvestigate(GetOwner()->GetActorLocation());
	}
}

void UHitmenAlertComponent::ReceiveReinforcementCall(const FVector& Location, AActor* CallTarget)
{
	if (bSuppressed || AlertState == EHitmenAlertState::Combat)
	{
		return;
	}
	if (CallTarget)
	{
		Target = CallTarget;
		LastKnownLocation = Location;
		bTargetVisible = false;
		LastSeenTime = GetWorld()->GetTimeSeconds();
		SetAlertState(EHitmenAlertState::Searching);
		return;
	}
	SetInvestigate(Location);
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

	if (const AHitmenCharacterBase* TargetCharacter = Cast<AHitmenCharacterBase>(Target); TargetCharacter && TargetCharacter->IsDead())
	{
		SetAlertState(EHitmenAlertState::Searching);
		Target = nullptr;
	}

	switch (AlertState)
	{
	case EHitmenAlertState::Suspicious:
		if (Now - LastSeenTime > 0.2f)
		{
			Suspicion = FMath::Max(0.f, Suspicion - SuspicionDecayRate * DeltaTime);
			if (Suspicion <= 0.f)
			{
				SetAlertState(EHitmenAlertState::Unaware);
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
			UpdateLastKnownMarker(false);
			SetAlertState(EHitmenAlertState::Unaware);
		}
		break;
	case EHitmenAlertState::Investigating:
		if (Now - StateEnterTime > GetRuleInvestigateTime())
		{
			SetAlertState(EHitmenAlertState::Unaware);
		}
		break;
	default:
		break;
	}
}
