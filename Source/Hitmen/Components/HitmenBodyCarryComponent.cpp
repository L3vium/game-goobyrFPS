// Hitmen — body carry

#include "Components/HitmenBodyCarryComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenHealthComponent.h"
#include "Framework/HitmenMatchGameMode.h"
#include "Data/HitmenMatchRules.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

UHitmenBodyCarryComponent::UHitmenBodyCarryComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenBodyCarryComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenBodyCarryComponent, CarriedBody);
}

void UHitmenBodyCarryComponent::BeginPlay()
{
	Super::BeginPlay();
	if (GetOwner()->HasAuthority())
	{
		if (UHitmenHealthComponent* Health = GetOwner()->FindComponentByClass<UHitmenHealthComponent>())
		{
			Health->OnDeath.AddDynamic(this, &UHitmenBodyCarryComponent::HandleOwnerDeath);
		}
	}
}

bool UHitmenBodyCarryComponent::CanCarry(const AHitmenCharacterBase* Body) const
{
	return bCarryEnabled && !CarriedBody && Body && Body->IsDiscoverableBody() && (!bOnlyVIP || Body->IsVIP());
}

void UHitmenBodyCarryComponent::PickUp(AHitmenCharacterBase* Body)
{
	if (!GetOwner()->HasAuthority() || !CanCarry(Body))
	{
		return;
	}
	// The body takes the hands (the left weapon slot); the other weapon stays slung.
	if (UHitmenInventoryComponent* Inventory = GetOwner()->FindComponentByClass<UHitmenInventoryComponent>())
	{
		Inventory->MakeRoomForBody();
	}
	CarriedBody = Body;
	// NAME_None: in the arms, at CarryTransform (read on every machine from this component).
	Body->SetCarriedBy(Cast<APawn>(GetOwner()), NAME_None);
	OnRep_CarriedBody();
	NotifyGameMode();
}

void UHitmenBodyCarryComponent::Drop()
{
	if (!GetOwner()->HasAuthority() || !CarriedBody)
	{
		return;
	}
	CarriedBody->SetCarriedBy(nullptr, NAME_None);
	CarriedBody = nullptr;
	OnRep_CarriedBody();
	NotifyGameMode();
	// Hands free again: the weapon you still have comes back to hand.
	if (UHitmenInventoryComponent* Inventory = GetOwner()->FindComponentByClass<UHitmenInventoryComponent>())
	{
		Inventory->OnBodyDropped();
	}
}

void UHitmenBodyCarryComponent::RequestDrop()
{
	Server_Drop();
}

void UHitmenBodyCarryComponent::Server_Drop_Implementation()
{
	Drop();
}

void UHitmenBodyCarryComponent::HandleOwnerDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot)
{
	Drop();
}

void UHitmenBodyCarryComponent::OnRep_CarriedBody()
{
	Juice_OnCarryChanged(CarriedBody != nullptr);
	OnCarryChanged.Broadcast(CarriedBody);
}

void UHitmenBodyCarryComponent::NotifyGameMode() const
{
	if (CarriedBody && !CarriedBody->IsVIP())
	{
		return;
	}
	if (AHitmenMatchGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenMatchGameMode>())
	{
		GM->OnVIPCarrierChanged(CarriedBody ? Cast<APawn>(GetOwner()) : nullptr);
	}
}

float UHitmenBodyCarryComponent::GetSpeedMultiplier() const
{
	if (!CarriedBody)
	{
		return 1.f;
	}
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	// Clients have no game mode: use the default rule value they all share.
	static const float DefaultMultiplier = GetDefault<UHitmenMatchRules>()->CarrySpeedMultiplier;
	return GM ? GM->GetRules()->CarrySpeedMultiplier : DefaultMultiplier;
}
