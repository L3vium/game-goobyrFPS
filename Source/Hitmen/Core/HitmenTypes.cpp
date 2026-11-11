// Hitmen — shared enums and structs

#include "Core/HitmenTypes.h"
#include "Data/HitmenCosmeticItem.h"

UHitmenCosmeticItem* FHitmenCosmeticLoadout::GetSlot(EHitmenCosmeticSlot Slot) const
{
	switch (Slot)
	{
	case EHitmenCosmeticSlot::Head: return Head;
	case EHitmenCosmeticSlot::Body: return Body;
	case EHitmenCosmeticSlot::Legs: return Legs;
	}
	return nullptr;
}

void FHitmenCosmeticLoadout::SetSlot(EHitmenCosmeticSlot Slot, UHitmenCosmeticItem* Item)
{
	switch (Slot)
	{
	case EHitmenCosmeticSlot::Head: Head = Item; break;
	case EHitmenCosmeticSlot::Body: Body = Item; break;
	case EHitmenCosmeticSlot::Legs: Legs = Item; break;
	}
}

namespace HitmenAlert
{
	EHitmenAlertColor ToColor(EHitmenAlertState State)
	{
		switch (State)
		{
		case EHitmenAlertState::Suspicious:    return EHitmenAlertColor::White;
		case EHitmenAlertState::Investigating: return EHitmenAlertColor::Yellow;
		case EHitmenAlertState::Searching:     return EHitmenAlertColor::Yellow;
		case EHitmenAlertState::Combat:        return EHitmenAlertColor::Red;
		default:                               return EHitmenAlertColor::None;
		}
	}

	FLinearColor ToLinearColor(EHitmenAlertColor Color)
	{
		switch (Color)
		{
		case EHitmenAlertColor::White:  return FLinearColor(0.95f, 0.95f, 0.95f);
		case EHitmenAlertColor::Yellow: return FLinearColor(1.0f, 0.78f, 0.1f);
		case EHitmenAlertColor::Red:    return FLinearColor::FromSRGBColor(FColor(215, 0, 0));
		default:                        return FLinearColor::Transparent;
		}
	}
}
