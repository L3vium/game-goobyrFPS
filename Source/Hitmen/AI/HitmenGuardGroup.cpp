// Hitmen — guard group

#include "AI/HitmenGuardGroup.h"
#include "AI/HitmenAlertComponent.h"
#include "Characters/HitmenNPC.h"
#include "Components/BillboardComponent.h"

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
	for (AHitmenNPC* Member : Members)
	{
		if (!Member)
		{
			continue;
		}
		Member->SetGuardGroup(this);
		if (bShareAlerts && Member->GetAlert())
		{
			Member->GetAlert()->OnAlertStateChanged.AddDynamic(this, &AHitmenGuardGroup::HandleMemberAlert);
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
	const int32 Row = Slot / 2 + 1;
	const float Side = (Slot % 2 == 0 ? -0.5f : 0.5f) * FollowSpacing;
	const FVector Forward = Leader->GetActorForwardVector().GetSafeNormal2D();
	const FVector Right = FVector::CrossProduct(FVector::UpVector, Forward);
	return Leader->GetActorLocation() - Forward * FollowDistance * Row + Right * Side;
}

void AHitmenGuardGroup::HandleMemberAlert(EHitmenAlertState OldState, EHitmenAlertState NewState)
{
	if (bSharing || NewState == EHitmenAlertState::Unaware || NewState == EHitmenAlertState::Suspicious)
	{
		return;
	}
	// Find who raised it, then pull the others to the same place.
	const AHitmenNPC* Source = nullptr;
	for (const AHitmenNPC* Member : Members)
	{
		if (Member && Member->GetAlert() && Member->GetAlert()->GetAlertState() == NewState)
		{
			Source = Member;
			break;
		}
	}
	if (!Source)
	{
		return;
	}
	TGuardValue<bool> Guard(bSharing, true);
	const FVector Where = Source->GetAlert()->GetTarget() ? Source->GetAlert()->GetTarget()->GetActorLocation() : Source->GetActorLocation();
	for (AHitmenNPC* Member : Members)
	{
		if (Member && Member != Source && !Member->IsDead() && Member->GetAlert())
		{
			Member->GetAlert()->ReceiveReinforcementCall(Where, Source->GetAlert()->GetTarget());
		}
	}
}
