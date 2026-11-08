// Hitmen — inventory

#include "Inventory/HitmenInventoryComponent.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Weapons/HitmenWeaponPickup.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Data/HitmenWeaponData.h"
#include "Net/UnrealNetwork.h"
#include "GameFramework/Pawn.h"
#include "Engine/World.h"

UHitmenInventoryComponent::UHitmenInventoryComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
	Slots.SetNum(SlotCount);
}

void UHitmenInventoryComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenInventoryComponent, Slots);
	DOREPLIFETIME(UHitmenInventoryComponent, ActiveSlot);
	DOREPLIFETIME(UHitmenInventoryComponent, Keycards);
}

void UHitmenInventoryComponent::BeginPlay()
{
	Super::BeginPlay();
	if (Slots.Num() != SlotCount)
	{
		Slots.SetNum(SlotCount);
	}
}

void UHitmenInventoryComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (GetOwner()->HasAuthority())
	{
		for (FHitmenInventorySlot& Slot : Slots)
		{
			if (Slot.Actor)
			{
				Slot.Actor->Destroy();
			}
		}
	}
	Super::EndPlay(EndPlayReason);
}

// ---- Queries ----

AHitmenWeaponBase* UHitmenInventoryComponent::GetActiveWeapon() const
{
	return Slots.IsValidIndex(ActiveSlot) ? Slots[ActiveSlot].Actor.Get() : nullptr;
}

UHitmenWeaponData* UHitmenInventoryComponent::GetActiveWeaponData() const
{
	return Slots.IsValidIndex(ActiveSlot) ? Slots[ActiveSlot].Weapon.Get() : nullptr;
}

int32 UHitmenInventoryComponent::GetActiveAmmo() const
{
	return Slots.IsValidIndex(ActiveSlot) ? Slots[ActiveSlot].Ammo : 0;
}

bool UHitmenInventoryComponent::HasWeaponType(const UHitmenWeaponData* Weapon) const
{
	return Weapon && Slots.ContainsByPredicate([Weapon](const FHitmenInventorySlot& S) { return S.Weapon == Weapon; });
}

bool UHitmenInventoryComponent::HasFreeSlot() const
{
	return Slots.ContainsByPredicate([](const FHitmenInventorySlot& S) { return S.IsEmpty(); });
}

// ---- Server operations ----

AHitmenWeaponBase* UHitmenInventoryComponent::SpawnWeaponActor(UHitmenWeaponData* Weapon)
{
	if (!Weapon || !Weapon->WeaponClass)
	{
		return nullptr;
	}
	APawn* OwnerPawn = Cast<APawn>(GetOwner());
	FActorSpawnParameters Params;
	Params.Owner = GetOwner();
	Params.Instigator = OwnerPawn;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	AHitmenWeaponBase* WeaponActor = GetWorld()->SpawnActor<AHitmenWeaponBase>(Weapon->WeaponClass, GetOwner()->GetActorTransform(), Params);
	if (WeaponActor)
	{
		WeaponActor->InitWeapon(Weapon, OwnerPawn);
		WeaponActor->SetEquipped(false);
	}
	return WeaponActor;
}

bool UHitmenInventoryComponent::GiveWeapon(UHitmenWeaponData* Weapon, int32 Ammo, bool bForceReplace)
{
	if (!GetOwner()->HasAuthority() || !Weapon)
	{
		return false;
	}
	const int32 Amount = Ammo < 0 ? Weapon->StartAmmo : Ammo;
	if (TryAddAmmo(Weapon, Amount))
	{
		return true;
	}

	int32 Target = Slots.IndexOfByPredicate([](const FHitmenInventorySlot& S) { return S.IsEmpty(); });
	if (Target == INDEX_NONE)
	{
		if (!bForceReplace)
		{
			return false;
		}
		Target = Slots.IsValidIndex(ActiveSlot) ? ActiveSlot : 0;
		FVector Origin, Direction;
		UHitmenCombatLibrary::GetAimPoint(Cast<APawn>(GetOwner()), Origin, Direction);
		SpawnThrownPickup(Slots[Target].Weapon, Slots[Target].Ammo, Origin, Direction * 0.2f);
		RemoveSlot(Target, true);
	}

	FHitmenInventorySlot& Slot = Slots[Target];
	Slot.Weapon = Weapon;
	Slot.Ammo = Weapon->bUsesAmmo ? FMath::Clamp(Amount, 0, Weapon->MaxAmmo) : 0;
	Slot.Actor = SpawnWeaponActor(Weapon);
	SetActiveSlotInternal(Target);
	OnRep_Slots();
	return true;
}

bool UHitmenInventoryComponent::TryAddAmmo(UHitmenWeaponData* Weapon, int32 Amount)
{
	if (!GetOwner()->HasAuthority() || !Weapon)
	{
		return false;
	}
	for (FHitmenInventorySlot& Slot : Slots)
	{
		if (Slot.Weapon == Weapon)
		{
			if (Weapon->bUsesAmmo)
			{
				Slot.Ammo = FMath::Clamp(Slot.Ammo + Amount, 0, Weapon->MaxAmmo);
			}
			Juice_OnAmmoPickedUp(Weapon, Amount);
			OnRep_Slots();
			return true;
		}
	}
	return false;
}

bool UHitmenInventoryComponent::ConsumeActiveAmmo(int32 Amount)
{
	if (!Slots.IsValidIndex(ActiveSlot) || Slots[ActiveSlot].IsEmpty())
	{
		return false;
	}
	FHitmenInventorySlot& Slot = Slots[ActiveSlot];
	if (!Slot.Weapon->bUsesAmmo)
	{
		return true;
	}
	if (Slot.Ammo < Amount)
	{
		return false;
	}
	Slot.Ammo -= Amount;
	OnRep_Slots();
	return true;
}

void UHitmenInventoryComponent::AddKeycard(int32 KeycardId)
{
	if (GetOwner()->HasAuthority() && KeycardId >= 0 && !Keycards.Contains(KeycardId))
	{
		Keycards.Add(KeycardId);
		Keycards.Sort();
		OnRep_Slots();
	}
}

bool UHitmenInventoryComponent::TakeActive(UHitmenWeaponData*& OutWeapon, int32& OutAmmo)
{
	if (!GetOwner()->HasAuthority() || !Slots.IsValidIndex(ActiveSlot) || Slots[ActiveSlot].IsEmpty())
	{
		return false;
	}
	OutWeapon = Slots[ActiveSlot].Weapon;
	OutAmmo = Slots[ActiveSlot].Ammo;
	RemoveSlot(ActiveSlot, true);
	return true;
}

void UHitmenInventoryComponent::RemoveSlot(int32 SlotIndex, bool bDestroyActor)
{
	if (!Slots.IsValidIndex(SlotIndex))
	{
		return;
	}
	if (bDestroyActor && Slots[SlotIndex].Actor)
	{
		Slots[SlotIndex].Actor->Destroy();
	}
	Slots[SlotIndex] = FHitmenInventorySlot();

	// Throw rules: slot 2 always slides up to slot 1, and slot 1 becomes the hand.
	if (SlotIndex == 0 && !Slots[1].IsEmpty())
	{
		Slots[0] = Slots[1];
		Slots[1] = FHitmenInventorySlot();
	}
	SetActiveSlotInternal(Slots[0].IsEmpty() ? INDEX_NONE : 0);
	OnRep_Slots();
}

void UHitmenInventoryComponent::SpawnThrownPickup(UHitmenWeaponData* Weapon, int32 Ammo, const FVector& Origin, const FVector& Direction)
{
	if (!Weapon)
	{
		return;
	}
	const TSubclassOf<AHitmenWeaponPickup> PickupClass = Weapon->PickupClass ? Weapon->PickupClass : TSubclassOf<AHitmenWeaponPickup>(AHitmenWeaponPickup::StaticClass());
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
	Params.Instigator = Cast<APawn>(GetOwner());
	Params.Owner = GetOwner();
	const FVector SpawnLocation = Origin + Direction.GetSafeNormal() * 60.f;
	if (AHitmenWeaponPickup* Pickup = GetWorld()->SpawnActor<AHitmenWeaponPickup>(PickupClass, SpawnLocation, Direction.Rotation(), Params))
	{
		Pickup->InitPickup(Weapon, Ammo);
		Pickup->Throw(Direction.GetSafeNormal() * ThrowSpeed, Params.Instigator);
	}
}

// ---- Client requests ----

void UHitmenInventoryComponent::SelectSlot(int32 SlotIndex)
{
	Server_SelectSlot(SlotIndex);
}

void UHitmenInventoryComponent::CycleSlot(float Direction)
{
	// Only two slots: the wheel toggles between them (if both are filled).
	if (Slots[0].IsEmpty() || Slots[1].IsEmpty())
	{
		return;
	}
	SelectSlot(ActiveSlot == 0 ? 1 : 0);
}

void UHitmenInventoryComponent::Server_SelectSlot_Implementation(int32 SlotIndex)
{
	if (Slots.IsValidIndex(SlotIndex) && !Slots[SlotIndex].IsEmpty())
	{
		if (AHitmenWeaponBase* Current = GetActiveWeapon())
		{
			Current->StopFire();
		}
		SetActiveSlotInternal(SlotIndex);
	}
}

void UHitmenInventoryComponent::ThrowActive()
{
	if (!Slots.IsValidIndex(ActiveSlot) || Slots[ActiveSlot].IsEmpty())
	{
		return;
	}
	FVector Origin, Direction;
	UHitmenCombatLibrary::GetAimPoint(Cast<APawn>(GetOwner()), Origin, Direction);
	Server_ThrowActive(Origin, Direction);
}

void UHitmenInventoryComponent::Server_ThrowActive_Implementation(FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction)
{
	if (!Slots.IsValidIndex(ActiveSlot) || Slots[ActiveSlot].IsEmpty())
	{
		return;
	}
	// Never trust a client origin far from the pawn.
	const FVector SafeOrigin = FVector::Dist(Origin, GetOwner()->GetActorLocation()) < 300.f ? FVector(Origin) : GetOwner()->GetActorLocation();
	UHitmenWeaponData* Weapon = Slots[ActiveSlot].Weapon;
	const int32 Ammo = Slots[ActiveSlot].Ammo;
	Juice_OnThrow(Weapon);
	SpawnThrownPickup(Weapon, Ammo, SafeOrigin, Direction);
	RemoveSlot(ActiveSlot, true);
}

void UHitmenInventoryComponent::StartFire()
{
	if (AHitmenWeaponBase* Weapon = GetActiveWeapon())
	{
		Weapon->StartFire();
	}
}

void UHitmenInventoryComponent::StopFire()
{
	if (AHitmenWeaponBase* Weapon = GetActiveWeapon())
	{
		Weapon->StopFire();
	}
}

// ---- Replication ----

void UHitmenInventoryComponent::SetActiveSlotInternal(int32 NewSlot)
{
	ActiveSlot = NewSlot;
	OnRep_ActiveSlot();
}

void UHitmenInventoryComponent::RefreshWeaponVisibility()
{
	for (int32 Index = 0; Index < Slots.Num(); ++Index)
	{
		if (AHitmenWeaponBase* Weapon = Slots[Index].Actor)
		{
			Weapon->SetEquipped(Index == ActiveSlot);
		}
	}
}

void UHitmenInventoryComponent::OnRep_Slots()
{
	RefreshWeaponVisibility();
	OnInventoryChanged.Broadcast();
}

void UHitmenInventoryComponent::OnRep_ActiveSlot()
{
	RefreshWeaponVisibility();
	if (LastActiveSlot != ActiveSlot)
	{
		LastActiveSlot = ActiveSlot;
		Juice_OnSlotChanged(ActiveSlot);
		OnActiveSlotChanged.Broadcast(ActiveSlot);
	}
}
