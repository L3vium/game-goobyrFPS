// Hitmen — inventory

#include "Inventory/HitmenInventoryComponent.h"
#include "Weapons/HitmenWeaponBase.h"
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
		// The old weapon actor goes to the world (dropped in front), never destroyed.
		FVector Origin, Direction;
		UHitmenCombatLibrary::GetAimPoint(Cast<APawn>(GetOwner()), Origin, Direction);
		DropSlot(Slots.IsValidIndex(ActiveSlot) ? ActiveSlot : 0, Origin + Direction * ThrowSpawnDistance, Direction.Rotation(), Direction * ReplaceDropSpeed, nullptr);
		Target = Slots.IndexOfByPredicate([](const FHitmenInventorySlot& S) { return S.IsEmpty(); });
		if (Target == INDEX_NONE)
		{
			return false;
		}
	}

	FillSlot(Target, Weapon, Amount, SpawnWeaponActor(Weapon));
	return true;
}

bool UHitmenInventoryComponent::PickUpWeapon(AHitmenWeaponBase* WorldWeapon, bool bAllowSwap)
{
	if (!GetOwner()->HasAuthority() || !IsValid(WorldWeapon) || !WorldWeapon->IsDropped() || !WorldWeapon->GetWeaponData())
	{
		return false;
	}
	UHitmenWeaponData* Weapon = WorldWeapon->GetWeaponData();
	const int32 Amount = WorldWeapon->GetDroppedAmmo();

	// Same type: merge its ammo; the world copy is redundant.
	if (HasWeaponType(Weapon))
	{
		TryAddAmmo(Weapon, Amount);
		WorldWeapon->Destroy();
		return true;
	}

	int32 Target = Slots.IndexOfByPredicate([](const FHitmenInventorySlot& S) { return S.IsEmpty(); });
	if (Target == INDEX_NONE)
	{
		if (!bAllowSwap)
		{
			return false;
		}
		// Swap: the held weapon takes the picked weapon's place in the world.
		const FTransform PlaceAt = WorldWeapon->GetActorTransform();
		DropSlot(Slots.IsValidIndex(ActiveSlot) ? ActiveSlot : 0, PlaceAt.GetLocation(), PlaceAt.Rotator(), FVector::ZeroVector, nullptr);
		Target = Slots.IndexOfByPredicate([](const FHitmenInventorySlot& S) { return S.IsEmpty(); });
		if (Target == INDEX_NONE)
		{
			return false;
		}
	}

	WorldWeapon->PickUp(Cast<APawn>(GetOwner()));
	FillSlot(Target, Weapon, Amount, WorldWeapon);
	return true;
}

void UHitmenInventoryComponent::FillSlot(int32 SlotIndex, UHitmenWeaponData* Weapon, int32 Ammo, AHitmenWeaponBase* Actor)
{
	FHitmenInventorySlot& Slot = Slots[SlotIndex];
	Slot.Weapon = Weapon;
	Slot.Ammo = Weapon->bUsesAmmo ? FMath::Clamp(Ammo, 0, Weapon->MaxAmmo) : 0;
	Slot.Actor = Actor;
	SetActiveSlotInternal(SlotIndex);
	OnRep_Slots();
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

void UHitmenInventoryComponent::DropSlot(int32 SlotIndex, const FVector& Location, const FRotator& Rotation, const FVector& Velocity, APawn* Thrower)
{
	if (!Slots.IsValidIndex(SlotIndex) || Slots[SlotIndex].IsEmpty())
	{
		return;
	}
	const FHitmenInventorySlot Dropped = Slots[SlotIndex];
	RemoveSlot(SlotIndex, false);
	if (Dropped.Actor)
	{
		Dropped.Actor->Drop(Dropped.Ammo, Location, Rotation, Velocity, Thrower);
	}
}

void UHitmenInventoryComponent::RefreshWeaponAttachments()
{
	for (const FHitmenInventorySlot& Slot : Slots)
	{
		if (Slot.Actor)
		{
			Slot.Actor->RefreshAttachment();
		}
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
	const FVector SafeOrigin = FVector::Dist(Origin, GetOwner()->GetActorLocation()) < ThrowOriginTolerance ? FVector(Origin) : GetOwner()->GetActorLocation();
	const FVector Dir = FVector(Direction).GetSafeNormal();
	UHitmenWeaponData* Weapon = Slots[ActiveSlot].Weapon;
	// The weapon actor itself flies and becomes the world pickup.
	DropSlot(ActiveSlot, SafeOrigin + Dir * ThrowSpawnDistance, Dir.Rotation(), Dir * ThrowSpeed, Cast<APawn>(GetOwner()));
	Multicast_Thrown(Weapon);
}

void UHitmenInventoryComponent::Multicast_Thrown_Implementation(UHitmenWeaponData* Weapon)
{
	Juice_OnThrow(Weapon);
	OnWeaponThrown.Broadcast(Weapon);
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
	// Server only: the weapon state replicates and every machine applies it.
	if (!GetOwner()->HasAuthority())
	{
		return;
	}
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
