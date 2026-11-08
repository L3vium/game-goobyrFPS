// Hitmen — AI coordination

#include "AI/HitmenAISubsystem.h"
#include "AI/HitmenAlertComponent.h"
#include "Abilities/HitmenAbilityActors.h"
#include "Characters/HitmenCharacterBase.h"
#include "Framework/HitmenGameModeBase.h"
#include "Data/HitmenMatchRules.h"
#include "Core/HitmenTeamLibrary.h"
#include "NavigationSystem.h"
#include "Engine/World.h"

UHitmenAISubsystem* UHitmenAISubsystem::Get(const UObject* WorldContext)
{
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	return World ? World->GetSubsystem<UHitmenAISubsystem>() : nullptr;
}

void UHitmenAISubsystem::RegisterAlert(UHitmenAlertComponent* Alert)
{
	if (UHitmenAISubsystem* AI = Get(Alert)) { AI->Alerts.AddUnique(Alert); }
}

void UHitmenAISubsystem::UnregisterAlert(UHitmenAlertComponent* Alert)
{
	if (UHitmenAISubsystem* AI = Get(Alert)) { AI->Alerts.Remove(Alert); }
}

void UHitmenAISubsystem::RegisterSmoke(AHitmenSmokeCloud* Smoke)
{
	if (UHitmenAISubsystem* AI = Get(Smoke)) { AI->Smokes.AddUnique(Smoke); }
}

void UHitmenAISubsystem::UnregisterSmoke(AHitmenSmokeCloud* Smoke)
{
	if (UHitmenAISubsystem* AI = Get(Smoke)) { AI->Smokes.Remove(Smoke); }
}

void UHitmenAISubsystem::RegisterDarkZone(UObject* Zone)
{
	if (UHitmenAISubsystem* AI = Get(Zone)) { AI->DarkZones.AddUnique(Zone); }
}

void UHitmenAISubsystem::UnregisterDarkZone(UObject* Zone)
{
	if (UHitmenAISubsystem* AI = Get(Zone)) { AI->DarkZones.Remove(Zone); }
}

TArray<UHitmenAlertComponent*> UHitmenAISubsystem::GetAlertComponents() const
{
	TArray<UHitmenAlertComponent*> Result;
	for (const TWeakObjectPtr<UHitmenAlertComponent>& Weak : Alerts)
	{
		if (UHitmenAlertComponent* Alert = Weak.Get())
		{
			Result.Add(Alert);
		}
	}
	return Result;
}

bool UHitmenAISubsystem::HasLineOfSight(const FVector& From, const FVector& To, const AActor* IgnoreA, const AActor* IgnoreB) const
{
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenAILOS), false);
	Params.AddIgnoredActor(IgnoreA);
	Params.AddIgnoredActor(IgnoreB);
	FHitResult Hit;
	return !GetWorld()->LineTraceSingleByChannel(Hit, From, To, ECC_Visibility, Params);
}

// ---------------------------------------------------------------------------------------------
// Noise
// ---------------------------------------------------------------------------------------------

void UHitmenAISubsystem::ReportNoise(const UObject* WorldContext, const FVector& Location, float Radius, AActor* Instigator, EHitmenNoiseType Type)
{
	UHitmenAISubsystem* AI = Get(WorldContext);
	if (!AI || Radius <= 0.f || AI->GetWorld()->GetNetMode() == NM_Client)
	{
		return;
	}
	const AHitmenGameModeBase* GM = AI->GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	const UHitmenMatchRules* Rules = GM ? GM->GetRules() : nullptr;
	const float Occlusion = Rules ? Rules->OcclusionMultiplier : 0.45f;
	int32 MaxResponders = Rules ? Rules->MaxNoiseResponders : 2;
	if (Type == EHitmenNoiseType::Explosion || Type == EHitmenNoiseType::Gunshot)
	{
		MaxResponders *= 2;
	}

	struct FListener { UHitmenAlertComponent* Alert; float Distance; };
	TArray<FListener> Listeners;
	for (UHitmenAlertComponent* Alert : AI->GetAlertComponents())
	{
		AActor* NPC = Alert->GetOwner();
		if (!NPC || NPC == Instigator)
		{
			continue;
		}
		if (const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(NPC); Character && Character->IsDead())
		{
			continue;
		}
		// Friendly footsteps are not suspicious.
		if (Type == EHitmenNoiseType::Footstep && !UHitmenTeamLibrary::AreHostile(NPC, Instigator))
		{
			continue;
		}
		const FVector Ear = NPC->GetActorLocation() + FVector::UpVector * 60.f;
		const float Distance = FVector::Dist(Location, Ear);
		if (Distance > Radius)
		{
			continue;
		}
		const bool bOccluded = !AI->HasLineOfSight(Location + FVector::UpVector * 30.f, Ear, Instigator, NPC);
		if (bOccluded && Distance > Radius * Occlusion)
		{
			continue;
		}
		Listeners.Add({ Alert, Distance });
	}
	Listeners.Sort([](const FListener& A, const FListener& B) { return A.Distance < B.Distance; });
	for (int32 Index = 0; Index < Listeners.Num() && Index < MaxResponders; ++Index)
	{
		Listeners[Index].Alert->HearNoise(Location, Type, Instigator, Listeners[Index].Distance);
	}
}

// ---------------------------------------------------------------------------------------------
// Kills
// ---------------------------------------------------------------------------------------------

void UHitmenAISubsystem::ReportKill(const UObject* WorldContext, AHitmenCharacterBase* Victim, AActor* Killer)
{
	UHitmenAISubsystem* AI = Get(WorldContext);
	if (!AI || !Victim || !Killer || AI->GetWorld()->GetNetMode() == NM_Client)
	{
		return;
	}
	const AHitmenGameModeBase* GM = AI->GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	const float Radius = GM ? GM->GetRules()->WitnessKillRadius : 2000.f;

	for (UHitmenAlertComponent* Alert : AI->GetAlertComponents())
	{
		const AHitmenCharacterBase* Witness = Cast<AHitmenCharacterBase>(Alert->GetOwner());
		if (!Witness || Witness == Victim || Witness->IsDead())
		{
			continue;
		}
		const FVector Eye = Witness->GetPawnViewLocation();
		if (FVector::Dist(Eye, Victim->GetActorLocation()) > Radius)
		{
			continue;
		}
		// Must face and see the killer.
		const FVector ToKiller = (Killer->GetActorLocation() - Eye).GetSafeNormal();
		if (FVector::DotProduct(Witness->GetActorForwardVector(), ToKiller) < 0.2f)
		{
			continue;
		}
		if (AI->HasLineOfSight(Eye, Killer->GetActorLocation(), Witness, Killer) && !AI->IsSightBlocked(Eye, Killer->GetActorLocation()))
		{
			Alert->WitnessKill(Killer);
		}
	}
}

// ---------------------------------------------------------------------------------------------
// Reinforcements
// ---------------------------------------------------------------------------------------------

void UHitmenAISubsystem::CallReinforcements(AActor* Caller, const FVector& Location, AActor* Target)
{
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	const float Radius = GM ? GM->GetRules()->ReinforcementRadius : 3000.f;
	const int32 MaxResponders = GM ? GM->GetRules()->ReinforcementMaxResponders : 5;
	const FVector Origin = Caller ? Caller->GetActorLocation() : Location;

	struct FResponder { UHitmenAlertComponent* Alert; float Distance; };
	TArray<FResponder> Responders;
	for (UHitmenAlertComponent* Alert : GetAlertComponents())
	{
		const AHitmenCharacterBase* NPC = Cast<AHitmenCharacterBase>(Alert->GetOwner());
		if (!NPC || NPC == Caller || NPC->IsDead())
		{
			continue;
		}
		const float Distance = FVector::Dist(Origin, NPC->GetActorLocation());
		if (Distance <= Radius)
		{
			Responders.Add({ Alert, Distance });
		}
	}
	Responders.Sort([](const FResponder& A, const FResponder& B) { return A.Distance < B.Distance; });
	for (int32 Index = 0; Index < Responders.Num() && Index < MaxResponders; ++Index)
	{
		Responders[Index].Alert->ReceiveReinforcementCall(Location, Target);
	}
}

// ---------------------------------------------------------------------------------------------
// Sight blockers
// ---------------------------------------------------------------------------------------------

bool UHitmenAISubsystem::IsSightBlocked(const FVector& Start, const FVector& End) const
{
	for (const TWeakObjectPtr<AHitmenSmokeCloud>& Weak : Smokes)
	{
		if (const AHitmenSmokeCloud* Smoke = Weak.Get(); Smoke && Smoke->BlocksSegment(Start, End))
		{
			return true;
		}
	}
	return false;
}

bool UHitmenAISubsystem::IsLocationDark(const FVector& Location) const
{
	for (const TWeakObjectPtr<UObject>& Weak : DarkZones)
	{
		if (const IHitmenDarkZone* Zone = Cast<IHitmenDarkZone>(Weak.Get()); Zone && Zone->IsLocationDark(Location))
		{
			return true;
		}
	}
	return false;
}

// ---------------------------------------------------------------------------------------------
// Flanking
// ---------------------------------------------------------------------------------------------

FVector UHitmenAISubsystem::GetFlankLocation(const AActor* NPC, const AActor* Target, float PreferredDistance) const
{
	if (!NPC || !Target)
	{
		return NPC ? NPC->GetActorLocation() : FVector::ZeroVector;
	}
	// Every NPC fighting this target gets a slot: front, left, right, wide left, wide right, behind.
	TArray<const AActor*> Engaged;
	for (UHitmenAlertComponent* Alert : GetAlertComponents())
	{
		if (Alert->GetAlertState() == EHitmenAlertState::Combat && Alert->GetTarget() == Target)
		{
			Engaged.Add(Alert->GetOwner());
		}
	}
	Engaged.Sort([](const AActor& A, const AActor& B) { return A.GetName() < B.GetName(); });
	const int32 Slot = FMath::Max(0, Engaged.IndexOfByKey(NPC));
	static const float SlotAngles[] = { 0.f, 70.f, -70.f, 140.f, -140.f, 180.f };
	const float Angle = SlotAngles[Slot % UE_ARRAY_COUNT(SlotAngles)];

	// Reference direction: from the target toward the group's average position.
	FVector Average = FVector::ZeroVector;
	for (const AActor* Actor : Engaged)
	{
		Average += Actor->GetActorLocation();
	}
	Average = Engaged.Num() > 0 ? Average / Engaged.Num() : NPC->GetActorLocation();
	const FVector Reference = (Average - Target->GetActorLocation()).GetSafeNormal2D();
	const FVector Direction = Reference.RotateAngleAxis(Angle, FVector::UpVector);
	const FVector Desired = Target->GetActorLocation() + Direction * PreferredDistance;

	if (const UNavigationSystemV1* Nav = FNavigationSystem::GetCurrent<UNavigationSystemV1>(GetWorld()))
	{
		FNavLocation Projected;
		if (Nav->ProjectPointToNavigation(Desired, Projected, FVector(400.f, 400.f, 400.f)))
		{
			return Projected.Location;
		}
	}
	return Desired;
}
