// Hitmen — loot spawn point

#include "World/Spawning/HitmenLootSpawnPoint.h"
#include "Data/HitmenCatalog.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenAbilityData.h"
#include "Weapons/HitmenWeaponPickup.h"
#include "Abilities/HitmenAbilityActors.h"
#include "Components/BillboardComponent.h"
#include "Engine/World.h"

namespace
{
	template <typename T>
	T* PickWeighted(const TArray<T*>& Items, FRandomStream& Stream)
	{
		float Total = 0.f;
		for (const T* Item : Items)
		{
			Total += Item ? Item->LootWeight : 0.f;
		}
		if (Total <= 0.f)
		{
			return Items.IsEmpty() ? nullptr : Items[Stream.RandRange(0, Items.Num() - 1)];
		}
		float Roll = Stream.FRandRange(0.f, Total);
		for (T* Item : Items)
		{
			if (!Item)
			{
				continue;
			}
			Roll -= Item->LootWeight;
			if (Roll <= 0.f)
			{
				return Item;
			}
		}
		return Items.Last();
	}
}

AHitmenLootSpawnPoint::AHitmenLootSpawnPoint()
{
	Billboard = CreateDefaultSubobject<UBillboardComponent>(TEXT("Billboard"));
	SetRootComponent(Billboard);
}

void AHitmenLootSpawnPoint::SpawnLoot(const UHitmenCatalog* Catalog, FRandomStream& Stream)
{
	if (!HasAuthority() || SpawnedLoot)
	{
		return;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;

	if (Category == EHitmenLootCategory::Weapon)
	{
		TArray<UHitmenWeaponData*> Options;
		for (UHitmenWeaponData* Weapon : (WeaponOverrides.Num() > 0 ? WeaponOverrides : (Catalog ? Catalog->LootWeapons : TArray<TObjectPtr<UHitmenWeaponData>>())))
		{
			Options.Add(Weapon);
		}
		if (UHitmenWeaponData* Weapon = PickWeighted(Options, Stream))
		{
			const TSubclassOf<AHitmenWeaponPickup> PickupClass = Weapon->PickupClass ? Weapon->PickupClass : TSubclassOf<AHitmenWeaponPickup>(AHitmenWeaponPickup::StaticClass());
			if (AHitmenWeaponPickup* Pickup = GetWorld()->SpawnActor<AHitmenWeaponPickup>(PickupClass, GetActorTransform(), Params))
			{
				Pickup->InitPickup(Weapon, -1);
				SpawnedLoot = Pickup;
			}
		}
		return;
	}

	TArray<UHitmenAbilityData*> Options;
	if (ItemOverrides.Num() > 0)
	{
		for (UHitmenAbilityData* Item : ItemOverrides) { Options.Add(Item); }
	}
	else if (Catalog)
	{
		if (Category != EHitmenLootCategory::ManipulationItem) { Options.Append(Catalog->GetAbilities(EHitmenAbilitySlot::Move)); }
		if (Category != EHitmenLootCategory::MoveItem) { Options.Append(Catalog->GetAbilities(EHitmenAbilitySlot::Manipulation)); }
	}
	if (UHitmenAbilityData* Item = PickWeighted(Options, Stream))
	{
		const TSubclassOf<AHitmenAbilityPickup> PickupClass = Item->PickupClass ? Item->PickupClass : TSubclassOf<AHitmenAbilityPickup>(AHitmenAbilityPickup::StaticClass());
		if (AHitmenAbilityPickup* Pickup = GetWorld()->SpawnActor<AHitmenAbilityPickup>(PickupClass, GetActorTransform(), Params))
		{
			Pickup->InitPickup(Item);
			SpawnedLoot = Pickup;
		}
	}
}
