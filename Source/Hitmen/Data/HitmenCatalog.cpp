// Hitmen — catalog

#include "Data/HitmenCatalog.h"
#include "Data/HitmenCosmeticItem.h"
#include "Data/HitmenAbilityData.h"

TArray<UHitmenCosmeticItem*> UHitmenCatalog::GetCosmeticsForSlot(EHitmenCosmeticSlot Slot) const
{
	TArray<UHitmenCosmeticItem*> Result;
	for (UHitmenCosmeticItem* Item : Cosmetics)
	{
		if (Item && Item->Slot == Slot)
		{
			Result.Add(Item);
		}
	}
	return Result;
}

TArray<UHitmenAbilityData*> UHitmenCatalog::GetAbilities(EHitmenAbilitySlot Slot) const
{
	TArray<UHitmenAbilityData*> Result;
	for (UHitmenAbilityData* Data : (Slot == EHitmenAbilitySlot::Move ? MoveAbilities : ManipulationAbilities))
	{
		if (Data)
		{
			Result.Add(Data);
		}
	}
	return Result;
}

FHitmenCosmeticLoadout UHitmenCatalog::MakeRandomCosmetics() const
{
	FHitmenCosmeticLoadout Loadout;
	for (EHitmenCosmeticSlot Slot : { EHitmenCosmeticSlot::Head, EHitmenCosmeticSlot::Top, EHitmenCosmeticSlot::Body, EHitmenCosmeticSlot::Bottom })
	{
		TArray<UHitmenCosmeticItem*> Options = GetCosmeticsForSlot(Slot);
		// Index == Num means "nothing" for this slot.
		const int32 Pick = FMath::RandRange(0, Options.Num());
		Loadout.SetSlot(Slot, Options.IsValidIndex(Pick) ? Options[Pick] : nullptr);
	}
	return Loadout;
}

FString UHitmenCatalog::MakeRandomName() const
{
	if (RandomNames.IsEmpty())
	{
		return FString::Printf(TEXT("Agent %d"), FMath::RandRange(10, 99));
	}
	return RandomNames[FMath::RandRange(0, RandomNames.Num() - 1)];
}
