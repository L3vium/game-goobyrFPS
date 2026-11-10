// Hitmen — AI coordination

#include "AI/HitmenAISubsystem.h"
#include "AI/HitmenAlertComponent.h"
#include "AI/HitmenAIController.h"
#include "Abilities/HitmenAbilityActors.h"
#include "Characters/HitmenCharacterBase.h"
#include "Characters/HitmenNPC.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Framework/HitmenGameModeBase.h"
#include "Framework/HitmenPlayerController.h"
#include "Interaction/HitmenScanRegistry.h"
#include "World/Interactables/HitmenStealth.h"
#include "Data/HitmenMatchRules.h"
#include "Core/HitmenTeamLibrary.h"
#include "GameFramework/PlayerState.h"
#include "NavigationSystem.h"
#include "NavigationPath.h"
#include "EngineUtils.h"
#include "Engine/World.h"

const FName UHitmenAISubsystem::HidingSpotTag = TEXT("HitmenHidingSpot");

namespace
{
	/** Vertical offsets of the hearing test (ear above the NPC pivot, source above the noise point). */
	constexpr float HearingEarHeight = 60.f;
	constexpr float HearingSourceHeight = 30.f;

	bool IsDeadCharacter(const AActor* Actor)
	{
		const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Actor);
		return Character && Character->IsDead();
	}

	APlayerState* GetTargetPlayerState(const AActor* Target)
	{
		const APawn* Pawn = Cast<APawn>(Target);
		return Pawn ? Pawn->GetPlayerState() : nullptr;
	}

	bool IsSniperActor(const AActor* Actor)
	{
		const AHitmenNPC* NPC = Cast<AHitmenNPC>(Actor);
		return NPC && NPC->IsSniper();
	}
}

UHitmenAISubsystem* UHitmenAISubsystem::Get(const UObject* WorldContext)
{
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	return World ? World->GetSubsystem<UHitmenAISubsystem>() : nullptr;
}

const UHitmenMatchRules& UHitmenAISubsystem::GetRules(const UObject* WorldContext)
{
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	const AHitmenGameModeBase* GM = World ? World->GetAuthGameMode<AHitmenGameModeBase>() : nullptr;
	const UHitmenMatchRules* Rules = GM ? GM->GetRules() : nullptr;
	return Rules ? *Rules : *GetDefault<UHitmenMatchRules>();
}

void UHitmenAISubsystem::OnWorldBeginPlay(UWorld& InWorld)
{
	Super::OnWorldBeginPlay(InWorld);
	// One pass at begin play (not per search): placed lockers, tall grass and tagged props.
	for (TActorIterator<AActor> It(&InWorld); It; ++It)
	{
		AActor* Actor = *It;
		if (Actor && (Actor->IsA<AHitmenTallGrass>() || Actor->IsA<AHitmenLocker>() || Actor->ActorHasTag(HidingSpotTag)))
		{
			HidingSpots.AddUnique(Actor);
		}
	}
}

void UHitmenAISubsystem::RegisterAlert(UHitmenAlertComponent* Alert)
{
	if (UHitmenAISubsystem* AI = Get(Alert)) { AI->Alerts.AddUnique(Alert); }
}

void UHitmenAISubsystem::UnregisterAlert(UHitmenAlertComponent* Alert)
{
	if (UHitmenAISubsystem* AI = Get(Alert))
	{
		AI->Alerts.Remove(Alert);
		AI->UpdateSearcher(Alert, nullptr, FVector::ZeroVector);
	}
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

void UHitmenAISubsystem::RegisterHidingSpot(AActor* Spot)
{
	if (UHitmenAISubsystem* AI = Get(Spot)) { AI->HidingSpots.AddUnique(Spot); }
}

void UHitmenAISubsystem::UnregisterHidingSpot(AActor* Spot)
{
	if (UHitmenAISubsystem* AI = Get(Spot)) { AI->HidingSpots.Remove(Spot); }
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
// UI
// ---------------------------------------------------------------------------------------------

TArray<FHitmenAlertSnapshot> UHitmenAISubsystem::GetAlertSnapshots(bool bOnlyAlerted) const
{
	TArray<FHitmenAlertSnapshot> Result;
	Result.Reserve(Alerts.Num());
	for (const TWeakObjectPtr<UHitmenAlertComponent>& Weak : Alerts)
	{
		const UHitmenAlertComponent* Alert = Weak.Get();
		const AActor* Owner = Alert ? Alert->GetOwner() : nullptr;
		if (!Owner || IsDeadCharacter(Owner))
		{
			continue;
		}
		if (bOnlyAlerted && Alert->GetAlertState() == EHitmenAlertState::Unaware)
		{
			continue;
		}
		FHitmenAlertSnapshot& Snapshot = Result.AddDefaulted_GetRef();
		Snapshot.Location = Owner->GetActorLocation();
		Snapshot.AlertState = Alert->GetAlertState();
		Snapshot.bCallingReinforcements = Alert->IsCallingReinforcements();
		Snapshot.Suspicion = Alert->GetSuspicion();
		Snapshot.TargetPlayerState = GetTargetPlayerState(Alert->GetTarget());
	}
	return Result;
}

void UHitmenAISubsystem::NotifyAlertChanged(const UHitmenAlertComponent* Alert)
{
	if (const AActor* Owner = Alert ? Alert->GetOwner() : nullptr)
	{
		OnAlertChanged.Broadcast(Owner->GetActorLocation(), Alert->GetAlertState(), GetTargetPlayerState(Alert->GetTarget()));
	}
}

void UHitmenAISubsystem::NotifyCallingChanged(const UHitmenAlertComponent* Alert)
{
	if (const AActor* Owner = Alert ? Alert->GetOwner() : nullptr)
	{
		OnReinforcementCallChanged.Broadcast(Owner->GetActorLocation(), Alert->IsCallingReinforcements());
	}
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
	const UHitmenMatchRules& Rules = GetRules(AI);
	const float Occlusion = Rules.OcclusionMultiplier;
	// Same cap for every noise type (gunshots and explosions included); designers may override per type.
	int32 MaxResponders = Rules.MaxNoiseResponders;
	if (const int32* Override = Rules.NoiseResponderOverrides.Find(Type))
	{
		MaxResponders = *Override;
	}
	MaxResponders = FMath::Max(0, MaxResponders);

	struct FListener { UHitmenAlertComponent* Alert; float Distance; };
	TArray<FListener> Listeners;
	for (UHitmenAlertComponent* Alert : AI->GetAlertComponents())
	{
		AActor* NPC = Alert->GetOwner();
		if (!NPC || NPC == Instigator || IsDeadCharacter(NPC))
		{
			continue;
		}
		// Deaf, asleep or busy shooting a visible target: must not use up a responder slot.
		if (!Alert->WouldReactToNoise())
		{
			continue;
		}
		// Friendly footsteps are not suspicious.
		if (Type == EHitmenNoiseType::Footstep && !UHitmenTeamLibrary::AreHostile(NPC, Instigator))
		{
			continue;
		}
		const FVector Ear = NPC->GetActorLocation() + FVector::UpVector * HearingEarHeight;
		const float Distance = FVector::Dist(Location, Ear);
		if (Distance > Radius)
		{
			continue;
		}
		const bool bOccluded = !AI->HasLineOfSight(Location + FVector::UpVector * HearingSourceHeight, Ear, Instigator, NPC);
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
	const float Radius = GetRules(AI).WitnessKillRadius;

	for (UHitmenAlertComponent* Alert : AI->GetAlertComponents())
	{
		const AHitmenCharacterBase* Witness = Cast<AHitmenCharacterBase>(Alert->GetOwner());
		if (!Witness || Witness == Victim || Witness == Killer || Witness->IsDead())
		{
			continue;
		}
		if (FVector::Dist(Witness->GetPawnViewLocation(), Victim->GetActorLocation()) > Radius)
		{
			continue;
		}
		// Same test as live sight: range, cone, walls, smoke, darkness, invisible / hidden killer.
		const AHitmenAIController* Brain = Cast<AHitmenAIController>(Witness->GetController());
		if (!Brain)
		{
			continue;
		}
		const float Range = Brain->GetEffectiveSightRadius();
		const float HalfAngle = Brain->GetPeripheralVisionHalfAngle();
		if (AI->CanObserverSee(Witness, Killer, Range, HalfAngle, true))
		{
			Alert->WitnessKill(Killer);
		}
		else if (Victim->IsDiscoverableBody() && AI->CanObserverSee(Witness, Victim, Range, HalfAngle, true))
		{
			// Saw someone drop without seeing who did it: investigate the body.
			Alert->SeeBody(Victim);
		}
	}
}

// ---------------------------------------------------------------------------------------------
// Reinforcements
// ---------------------------------------------------------------------------------------------

void UHitmenAISubsystem::CallReinforcements(AActor* Caller, const FVector& Location, AActor* Target)
{
	const UHitmenMatchRules& Rules = GetRules(this);
	const float Radius = Rules.ReinforcementRadius;
	const int32 MaxResponders = Rules.ReinforcementMaxResponders;
	const FVector Origin = Caller ? Caller->GetActorLocation() : Location;

	struct FResponder { UHitmenAlertComponent* Alert; float Distance; };
	TArray<FResponder> Responders;
	for (UHitmenAlertComponent* Alert : GetAlertComponents())
	{
		const AHitmenCharacterBase* NPC = Cast<AHitmenCharacterBase>(Alert->GetOwner());
		if (!NPC || NPC == Caller || NPC->IsDead() || !Alert->WouldAnswerCall())
		{
			continue;
		}
		// Only NPCs that would fight that target answer (charmed allies are not sent after their side).
		if (Target && !UHitmenTeamLibrary::AreHostile(NPC, Target))
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
// Sight
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

bool UHitmenAISubsystem::CanObserverSee(const APawn* Observer, const AActor* Target, float MaxRange, float HalfAngleDegrees, bool bTraceOcclusion) const
{
	if (!Observer || !Target)
	{
		return false;
	}
	// Players dropping in (and a moment after landing) are never spotted.
	if (const AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(Target); Player && Player->IsIntroProtected())
	{
		return false;
	}
	FVector Eye;
	FRotator ViewRotation;
	Observer->GetActorEyesViewPoint(Eye, ViewRotation);
	const FVector Point = Target->GetActorLocation();
	const FVector ToTarget = Point - Eye;
	if (MaxRange > 0.f && ToTarget.SizeSquared() > FMath::Square(MaxRange))
	{
		return false;
	}
	if (HalfAngleDegrees < 180.f)
	{
		const float CosHalf = FMath::Cos(FMath::DegreesToRadians(HalfAngleDegrees));
		if (FVector::DotProduct(ViewRotation.Vector(), ToTarget.GetSafeNormal()) < CosHalf)
		{
			return false;
		}
	}
	// Smoke between, blackout at either end.
	if (IsSightBlocked(Eye, Point) || IsLocationDark(Point) || IsLocationDark(Eye))
	{
		return false;
	}
	if (const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Target))
	{
		const UHitmenStatusEffectComponent* Status = Character->GetStatusEffects();
		if (!Character->IsDead() && Status && Status->IsHiddenFromSight())
		{
			return false;
		}
	}
	return !bTraceOcclusion || HasLineOfSight(Eye, Point, Observer, Target);
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
	const UHitmenMatchRules& Rules = GetRules(this);
	const bool bSniper = IsSniperActor(NPC);

	// Everyone fighting this target shapes the reference direction; slots are dealt per role
	// (close fighters and snipers separately) so a sniper never takes the direct-push slot.
	TArray<const AActor*> Engaged;
	TArray<const AActor*> SameRole;
	for (UHitmenAlertComponent* Alert : GetAlertComponents())
	{
		const AActor* Owner = Alert->GetOwner();
		if (Owner && !IsDeadCharacter(Owner) && Alert->GetAlertState() == EHitmenAlertState::Combat && Alert->GetTarget() == Target)
		{
			Engaged.Add(Owner);
			if (IsSniperActor(Owner) == bSniper)
			{
				SameRole.Add(Owner);
			}
		}
	}
	SameRole.AddUnique(NPC);
	SameRole.Sort([](const AActor& A, const AActor& B) { return A.GetName() < B.GetName(); });
	const int32 Slot = FMath::Max(0, SameRole.IndexOfByKey(NPC));
	static const float SlotAngles[] = { 0.f, 70.f, -70.f, 140.f, -140.f, 180.f };
	const float Angle = SlotAngles[Slot % UE_ARRAY_COUNT(SlotAngles)];

	// Reference direction: from the target toward the group's average position.
	const FVector TargetLocation = Target->GetActorLocation();
	FVector Average = FVector::ZeroVector;
	for (const AActor* Actor : Engaged)
	{
		Average += Actor->GetActorLocation();
	}
	Average = Engaged.Num() > 0 ? Average / Engaged.Num() : NPC->GetActorLocation();
	FVector Reference = (Average - TargetLocation).GetSafeNormal2D();
	if (Reference.IsNearlyZero())
	{
		Reference = (NPC->GetActorLocation() - TargetLocation).GetSafeNormal2D();
	}
	if (Reference.IsNearlyZero())
	{
		Reference = Target->GetActorForwardVector().GetSafeNormal2D();
	}
	const FVector Direction = Reference.RotateAngleAxis(Angle, FVector::UpVector);
	const FVector Desired = TargetLocation + Direction * PreferredDistance;

	UNavigationSystemV1* Nav = FNavigationSystem::GetCurrent<UNavigationSystemV1>(GetWorld());
	if (!Nav)
	{
		return Desired;
	}

	// ---- Floors: where the target and this NPC stand on the navmesh ----
	const float FloorHeight = FMath::Max(1.f, Rules.FlankFloorHeight);
	const float SameFloorTolerance = FloorHeight * 0.5f;
	auto FloorZ = [Nav, SameFloorTolerance](const AActor* Actor)
	{
		FNavLocation Projected;
		const FVector Location = Actor->GetActorLocation();
		return Nav->ProjectPointToNavigation(Location, Projected, FVector(1.f, 1.f, SameFloorTolerance * 2.f))
			? Projected.Location.Z : Location.Z;
	};
	const float TargetFloor = FloorZ(Target);
	const float SelfFloor = FloorZ(NPC);

	// ---- Candidates: the slot point snapped to every floor probed above / below the target ----
	struct FCandidate { FVector Location; float LevelDelta; };
	TArray<FCandidate> Candidates;
	const int32 Levels = Rules.bFlankUseOtherLevels ? FMath::Max(0, Rules.FlankHeightLevels) : 0;
	const FVector Extent(Rules.FlankProjectionRadius, Rules.FlankProjectionRadius, SameFloorTolerance);
	for (int32 Level = -Levels; Level <= Levels; ++Level)
	{
		FVector Probe = Desired;
		Probe.Z = TargetFloor + Level * FloorHeight;
		FNavLocation Projected;
		if (!Nav->ProjectPointToNavigation(Probe, Projected, Extent))
		{
			continue;
		}
		const float Delta = Projected.Location.Z - TargetFloor;
		const bool bDuplicate = Candidates.ContainsByPredicate([Delta, SameFloorTolerance](const FCandidate& C)
		{
			return FMath::Abs(C.LevelDelta - Delta) < SameFloorTolerance;
		});
		if (!bDuplicate)
		{
			Candidates.Add({ Projected.Location, Delta });
		}
	}
	if (Candidates.Num() == 0)
	{
		FNavLocation Projected;
		const FVector Fallback(Rules.FlankProjectionRadius, Rules.FlankProjectionRadius, FloorHeight);
		return Nav->ProjectPointToNavigation(Desired, Projected, Fallback) ? Projected.Location : Desired;
	}

	const bool bMultiLevel = Candidates.Num() > 1 || FMath::Abs(SelfFloor - TargetFloor) >= SameFloorTolerance;
	const bool bTakeOtherLevel = !bSniper && bMultiLevel && Slot == 1;
	const APawn* SelfPawn = Cast<APawn>(NPC);
	const float EyeAboveFloor = (NPC->GetActorLocation().Z - SelfFloor) + (SelfPawn ? SelfPawn->BaseEyeHeight : 0.f);

	// Score: direct push / normal slots stay on the target's floor; the second flanker takes another
	// floor (route via stairs); snipers want a line of sight and like high ground.
	auto Score = [&](const FCandidate& C)
	{
		const bool bOtherFloor = FMath::Abs(C.LevelDelta) >= SameFloorTolerance;
		float Value = -FMath::Abs(C.LevelDelta) / FloorHeight;
		if (bTakeOtherLevel)
		{
			Value = bOtherFloor ? 2.f - FMath::Abs(C.LevelDelta) / FloorHeight : 0.f;
		}
		if (bSniper)
		{
			const bool bSees = HasLineOfSight(C.Location + FVector::UpVector * EyeAboveFloor, TargetLocation, NPC, Target);
			Value = (bSees ? 2.f : 0.f) + FMath::Max(0.f, C.LevelDelta) / FloorHeight;
		}
		return Value;
	};
	Candidates.Sort([&Score](const FCandidate& A, const FCandidate& B) { return Score(A) > Score(B); });

	// First reachable candidate wins (a slot on a floor without a route is useless).
	for (const FCandidate& Candidate : Candidates)
	{
		const UNavigationPath* Path = UNavigationSystemV1::FindPathToLocationSynchronously(GetWorld(), NPC->GetActorLocation(), Candidate.Location, const_cast<AActor*>(NPC));
		if (Path && Path->IsValid() && !Path->IsPartial())
		{
			return Candidate.Location;
		}
	}
	return Candidates[0].Location;
}

// ---------------------------------------------------------------------------------------------
// Search coordination
// ---------------------------------------------------------------------------------------------

void UHitmenAISubsystem::GetHidingSpotsNear(const FVector& Center, float Radius, TArray<AActor*>& OutSpots) const
{
	const float RadiusSquared = FMath::Square(Radius);
	auto Consider = [&OutSpots, &Center, RadiusSquared](AActor* Actor)
	{
		if (Actor && FVector::DistSquared(Actor->GetActorLocation(), Center) <= RadiusSquared)
		{
			OutSpots.AddUnique(Actor);
		}
	};
	for (const TWeakObjectPtr<AActor>& Weak : HidingSpots)
	{
		Consider(Weak.Get());
	}
	// Lockers (and tagged interactables) spawned after begin play live in the scan registry.
	if (const UHitmenScanRegistry* Registry = GetWorld()->GetSubsystem<UHitmenScanRegistry>())
	{
		for (const TWeakObjectPtr<AActor>& Weak : Registry->GetScannables())
		{
			AActor* Actor = Weak.Get();
			if (Actor && (Actor->IsA<AHitmenLocker>() || Actor->IsA<AHitmenTallGrass>() || Actor->ActorHasTag(HidingSpotTag)))
			{
				Consider(Actor);
			}
		}
	}
}

void UHitmenAISubsystem::PruneClaims() const
{
	const float Now = GetWorld()->GetTimeSeconds();
	const float Lifetime = GetRules(this).SearchSpotClaimLifetime;
	SearchClaims.RemoveAll([Now, Lifetime](const FSearchClaim& Claim)
	{
		return Now - Claim.Time > Lifetime;
	});
}

void UHitmenAISubsystem::ClaimSearchSpot(const AActor* Claimer, const FVector& Location)
{
	PruneClaims();
	SearchClaims.Add({ Claimer, Location, static_cast<float>(GetWorld()->GetTimeSeconds()) });
}

bool UHitmenAISubsystem::IsSearchSpotClaimed(const FVector& Location) const
{
	PruneClaims();
	const float RadiusSquared = FMath::Square(GetRules(this).SearchSpotClaimRadius);
	return SearchClaims.ContainsByPredicate([&Location, RadiusSquared](const FSearchClaim& Claim)
	{
		return FVector::DistSquared(Claim.Location, Location) <= RadiusSquared;
	});
}

// ---------------------------------------------------------------------------------------------
// Last known position marker
// ---------------------------------------------------------------------------------------------

void UHitmenAISubsystem::UpdateSearcher(UHitmenAlertComponent* Alert, AActor* SearchedTarget, const FVector& LastKnownLocation)
{
	if (!Alert)
	{
		return;
	}
	const TWeakObjectPtr<UHitmenAlertComponent> AlertKey(Alert);
	const TWeakObjectPtr<AActor> NextKey(SearchedTarget);
	if (const TWeakObjectPtr<AActor>* Found = SearcherTargets.Find(AlertKey))
	{
		const TWeakObjectPtr<AActor> PreviousKey = *Found;
		if (PreviousKey != NextKey || !SearchedTarget)
		{
			SearcherTargets.Remove(AlertKey);
			if (FSearchMarker* Previous = SearchMarkers.Find(PreviousKey))
			{
				Previous->Searchers.Remove(AlertKey);
			}
			RefreshMarker(PreviousKey);
		}
	}
	if (SearchedTarget)
	{
		SearcherTargets.Add(AlertKey, NextKey);
		FSearchMarker& Marker = SearchMarkers.FindOrAdd(NextKey);
		Marker.Searchers.Add(AlertKey);
		Marker.Location = LastKnownLocation;
		RefreshMarker(NextKey);
	}
}

void UHitmenAISubsystem::RefreshMarker(const TWeakObjectPtr<AActor>& InKey)
{
	const TWeakObjectPtr<AActor> Key = InKey; // copy: the entry may be removed below
	FSearchMarker* Marker = SearchMarkers.Find(Key);
	if (!Marker)
	{
		return;
	}
	for (auto It = Marker->Searchers.CreateIterator(); It; ++It)
	{
		if (!It->IsValid())
		{
			It.RemoveCurrent();
		}
	}
	const AActor* Target = Key.Get();
	const bool bAlive = Target && !IsDeadCharacter(Target);
	const APawn* Pawn = Cast<APawn>(Target);
	AHitmenPlayerController* PC = Pawn ? Cast<AHitmenPlayerController>(Pawn->GetController()) : nullptr;
	const bool bShow = bAlive && PC && Marker->Searchers.Num() > 0;

	// Hide on the controller that last showed it if it is no longer the right one.
	AHitmenPlayerController* Shown = Marker->Controller.Get();
	if (Marker->bShown && (!bShow || Shown != PC))
	{
		if (Shown)
		{
			Shown->Client_ShowLastKnownPosition(Marker->SentLocation, false);
		}
		Marker->bShown = false;
	}
	if (bShow)
	{
		if (!Marker->bShown || !Marker->SentLocation.Equals(Marker->Location, 1.f))
		{
			PC->Client_ShowLastKnownPosition(Marker->Location, true);
			Marker->SentLocation = Marker->Location;
		}
		Marker->Controller = PC;
		Marker->bShown = true;
		return;
	}
	if (!bAlive || Marker->Searchers.Num() == 0)
	{
		SearchMarkers.Remove(Key);
	}
}
