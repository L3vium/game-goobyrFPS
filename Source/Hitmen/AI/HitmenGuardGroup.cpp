// Hitmen — guard group

#include "AI/HitmenGuardGroup.h"
#include "AI/HitmenAlertComponent.h"
#include "Characters/HitmenNPC.h"
#include "Components/BillboardComponent.h"
#include "TimerManager.h"
#include "Engine/World.h"

AHitmenGuardGroup::AHitmenGuardGroup()
{
	PrimaryActorTick.bCanEverTick = false;
	Billboard = CreateDefaultSubobject<UBillboardComponent>(TEXT("Billboard"));
	SetRootComponent(Billboard);
}

void AHitmenGuardGroup::BeginPlay()
{
	Super::BeginPlay();
	if (!HasAuthority())
	{
		return;
	}
	BindMembers();
}

void AHitmenGuardGroup::Configure(const TArray<AHitmenNPC*>& InMembers, float InFollowDistance, float InFollowSpacing, int32 InColumns)
{
	Members.Reset();
	for (AHitmenNPC* Member : InMembers)
	{
		Members.Add(Member);
	}
	FollowDistance = InFollowDistance;
	FollowSpacing = InFollowSpacing;
	Columns = FMath::Max(1, InColumns);
	if (HasActorBegunPlay() && HasAuthority())
	{
		BindMembers();
	}
}

void AHitmenGuardGroup::BindMembers()
{
	GetWorldTimerManager().SetTimer(PaceTimer, this, &AHitmenGuardGroup::UpdatePace, 0.3f, true);
	for (AHitmenNPC* Member : Members)
	{
		if (!Member)
		{
			continue;
		}
		Member->SetGuardGroup(this);
		if (bShareAlerts && Member->GetAlert())
		{
			Member->GetAlert()->OnAlertStateChanged.AddUniqueDynamic(this, &AHitmenGuardGroup::HandleMemberAlert);
		}
	}
}

AHitmenNPC* AHitmenGuardGroup::GetLeader() const
{
	for (AHitmenNPC* Member : Members)
	{
		if (Member && !Member->IsDead())
		{
			return Member;
		}
	}
	return nullptr;
}

FVector AHitmenGuardGroup::GetFormationLocation(const AHitmenNPC* Follower) const
{
	const AHitmenNPC* Leader = GetLeader();
	if (!Leader || !Follower)
	{
		return Follower ? Follower->GetActorLocation() : FVector::ZeroVector;
	}
	// Slot index among the living followers: two-wide rows behind the leader.
	int32 Slot = 0;
	for (const AHitmenNPC* Member : Members)
	{
		if (Member == Follower)
		{
			break;
		}
		if (Member && Member != Leader && !Member->IsDead())
		{
			++Slot;
		}
	}
	// Rows of Columns behind the leader, centred.
	const int32 PerRow = FMath::Max(1, Columns);
	const int32 Row = Slot / PerRow + 1;
	const int32 Column = Slot % PerRow;
	const float Side = (Column - (PerRow - 1) * 0.5f) * FollowSpacing;
	const FVector Forward = Leader->GetActorForwardVector().GetSafeNormal2D();
	const FVector Right = FVector::CrossProduct(FVector::UpVector, Forward);
	return Leader->GetActorLocation() - Forward * FollowDistance * Row + Right * Side;
}

void AHitmenGuardGroup::UpdatePace()
{
	AHitmenNPC* Leader = GetLeader();
	if (!Leader)
	{
		return;
	}
	// The group stays together: the leader slows down while someone is far from its slot (calm only).
	bool bLagging = false;
	for (const AHitmenNPC* Member : Members)
	{
		if (Member && Member != Leader && !Member->IsDead() && Member->GetAlert() && Member->GetAlert()->GetAlertState() == EHitmenAlertState::Unaware
			&& FVector::Dist2D(Member->GetActorLocation(), GetFormationLocation(Member)) > LagDistance)
		{
			bLagging = true;
			break;
		}
	}
	Leader->SetPaceMultiplier(bLagging ? LeaderWaitPace : 1.f);
}

void AHitmenGuardGroup::HandleMemberAlert(EHitmenAlertState OldState, EHitmenAlertState NewState)
{
	if (bSharing || NewState == EHitmenAlertState::Unaware || NewState == EHitmenAlertState::Suspicious)
	{
		return;
	}
	// Find who raised it, then pull the others to what it knows (never the target's real position). A footstep or a
	// distraction stays that guard's business: only real threats are shared.
	const AHitmenNPC* Source = nullptr;
	for (const AHitmenNPC* Member : Members)
	{
		if (Member && Member->GetAlert() && Member->GetAlert()->GetAlertState() == NewState)
		{
			Source = Member;
			break;
		}
	}
	if (!Source || (NewState == EHitmenAlertState::Investigating && !Source->GetAlert()->IsThreatInvestigation()))
	{
		return;
	}
	TGuardValue<bool> Guard(bSharing, true);
	const FVector Where = Source->GetAlert()->GetCallLocation();
	for (AHitmenNPC* Member : Members)
	{
		if (Member && Member != Source && !Member->IsDead() && Member->GetAlert())
		{
			Member->GetAlert()->ReceiveReinforcementCall(Where, Source->GetAlert()->GetTarget());
		}
	}
}
