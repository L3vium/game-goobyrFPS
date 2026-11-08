// Hitmen — family base of the manipulation abilities (A slot)

#pragma once

#include "CoreMinimal.h"
#include "Abilities/HitmenAbility.h"
#include "HitmenManipulationAbility.generated.h"

/**
 * Every manipulation ability (throwables, charm, devices, poison...) derives from this class.
 * Shared manipulation-only helpers belong here; the slot is fixed to EHitmenAbilitySlot::Manipulation.
 */
UCLASS(Abstract, Blueprintable)
class HITMEN_API UHitmenManipulationAbility : public UHitmenAbility
{
	GENERATED_BODY()

public:
	UHitmenManipulationAbility();
};
