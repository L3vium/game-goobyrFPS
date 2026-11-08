// Hitmen — family base of the move abilities (E slot)

#pragma once

#include "CoreMinimal.h"
#include "Abilities/HitmenAbility.h"
#include "HitmenMoveAbility.generated.h"

/**
 * Every move ability (blink, jetpack, dash, self statuses, C4...) derives from this class.
 * Shared move-only helpers belong here; the slot is fixed to EHitmenAbilitySlot::Move.
 */
UCLASS(Abstract, Blueprintable)
class HITMEN_API UHitmenMoveAbility : public UHitmenAbility
{
	GENERATED_BODY()

public:
	UHitmenMoveAbility();
};
