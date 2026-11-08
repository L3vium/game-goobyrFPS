// Hitmen — ability component

#include "Abilities/HitmenAbilityComponent.h"
#include "Abilities/HitmenAbility.h"
#include "Data/HitmenAbilityData.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Net/UnrealNetwork.h"
#include "GameFramework/Pawn.h"

UHitmenAbilityComponent::UHitmenAbilityComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.bStartWithTickEnabled = true;
	SetIsReplicatedByDefault(true);
	bReplicateUsingRegisteredSubObjectList = true;
}

void UHitmenAbilityComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAbilityComponent, MoveAbility);
	DOREPLIFETIME(UHitmenAbilityComponent, ManipulationAbility);
}

UHitmenAbility* UHitmenAbilityComponent::GetAbility(EHitmenAbilitySlot Slot) const
{
	return Slot == EHitmenAbilitySlot::Move ? MoveAbility.Get() : ManipulationAbility.Get();
}

void UHitmenAbilityComponent::SetAbility(UHitmenAbilityData* AbilityData)
{
	if (!GetOwner()->HasAuthority() || !AbilityData || !AbilityData->AbilityClass)
	{
		return;
	}
	TObjectPtr<UHitmenAbility>& SlotRef = AbilityData->Slot == EHitmenAbilitySlot::Move ? MoveAbility : ManipulationAbility;
	if (SlotRef)
	{
		SlotRef->OnUnequipped();
		RemoveReplicatedSubObject(SlotRef);
	}
	UHitmenAbility* NewAbility = NewObject<UHitmenAbility>(GetOwner(), AbilityData->AbilityClass);
	NewAbility->InitAbility(this, AbilityData);
	AddReplicatedSubObject(NewAbility);
	SlotRef = NewAbility;
	NewAbility->OnEquipped();
	OnRep_Abilities();
}

void UHitmenAbilityComponent::OnRep_Abilities()
{
	// Clients: run equip/unequip side effects for changes.
	TArray<UHitmenAbility*> Current;
	if (MoveAbility) { Current.Add(MoveAbility); }
	if (ManipulationAbility) { Current.Add(ManipulationAbility); }

	if (!GetOwner()->HasAuthority())
	{
		for (UHitmenAbility* Old : KnownAbilities)
		{
			if (Old && !Current.Contains(Old))
			{
				Old->OnUnequipped();
			}
		}
		for (UHitmenAbility* New : Current)
		{
			if (!KnownAbilities.Contains(New))
			{
				New->SetOwnerComponent(this);
				New->OnEquipped();
			}
		}
	}
	KnownAbilities.Reset();
	for (UHitmenAbility* Ability : Current)
	{
		KnownAbilities.Add(Ability);
	}
	OnAbilitiesChanged.Broadcast();
}

void UHitmenAbilityComponent::GetAim(FVector& OutOrigin, FVector& OutDirection) const
{
	UHitmenCombatLibrary::GetAimPoint(Cast<APawn>(GetOwner()), OutOrigin, OutDirection);
}

void UHitmenAbilityComponent::PressSlot(EHitmenAbilitySlot Slot)
{
	UHitmenAbility* Ability = GetAbility(Slot);
	if (!Ability)
	{
		return;
	}
	FVector Origin, Direction;
	GetAim(Origin, Direction);
	if (Ability->CanActivate() && !GetOwner()->HasAuthority())
	{
		Ability->LocalPress(Origin, Direction);
	}
	Server_Press(Slot, Origin, Direction);
}

void UHitmenAbilityComponent::ReleaseSlot(EHitmenAbilitySlot Slot)
{
	UHitmenAbility* Ability = GetAbility(Slot);
	if (!Ability)
	{
		return;
	}
	FVector Origin, Direction;
	GetAim(Origin, Direction);
	if (!GetOwner()->HasAuthority())
	{
		Ability->LocalRelease();
	}
	Server_Release(Slot, Origin, Direction);
}

void UHitmenAbilityComponent::Server_Press_Implementation(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction)
{
	if (UHitmenAbility* Ability = GetAbility(Slot))
	{
		const FVector SafeOrigin = FVector::Dist(Origin, GetOwner()->GetActorLocation()) < 400.f ? FVector(Origin) : GetOwner()->GetActorLocation();
		if (GetOwner()->HasAuthority() && Cast<APawn>(GetOwner())->IsLocallyControlled())
		{
			// Listen-server host: run the local half too.
			Ability->LocalPress(SafeOrigin, Direction);
		}
		Ability->ServerPress(SafeOrigin, Direction);
	}
}

void UHitmenAbilityComponent::Server_Release_Implementation(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction)
{
	if (UHitmenAbility* Ability = GetAbility(Slot))
	{
		const FVector SafeOrigin = FVector::Dist(Origin, GetOwner()->GetActorLocation()) < 400.f ? FVector(Origin) : GetOwner()->GetActorLocation();
		if (GetOwner()->HasAuthority() && Cast<APawn>(GetOwner())->IsLocallyControlled())
		{
			Ability->LocalRelease();
		}
		Ability->ServerRelease(SafeOrigin, Direction);
	}
}

void UHitmenAbilityComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	const APawn* Pawn = Cast<APawn>(GetOwner());
	const bool bAuthority = GetOwner()->HasAuthority();
	const bool bLocal = Pawn && Pawn->IsLocallyControlled();
	for (UHitmenAbility* Ability : { MoveAbility.Get(), ManipulationAbility.Get() })
	{
		if (!Ability || !Ability->WantsTick())
		{
			continue;
		}
		if (bAuthority)
		{
			Ability->TickAbility(DeltaTime);
		}
		if (bLocal && !bAuthority)
		{
			Ability->LocalTick(DeltaTime);
		}
	}
}

void UHitmenAbilityComponent::Multicast_AbilityActivated_Implementation(UHitmenAbility* Ability, FVector_NetQuantize Location)
{
	if (Ability)
	{
		Ability->Juice_OnActivated(Location);
	}
}
