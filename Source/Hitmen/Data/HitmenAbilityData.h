// Hitmen — UI description of an ability; the gameplay values live on the ability class

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Core/HitmenTypes.h"
#include "HitmenAbilityData.generated.h"

class UHitmenAbility;
class AHitmenAbilityPickup;

/**
 * Selectable ability (loadout, loot, debug panel). Gameplay values (cooldown, range, duration,
 * specific parameters) are EditDefaultsOnly properties of AbilityClass, set in its BP child.
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenAbilityData : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability")
	FText DisplayName;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability", meta=(MultiLine=true))
	FText Description;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability")
	TSoftObjectPtr<UTexture2D> Icon;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability")
	EHitmenAbilitySlot Slot = EHitmenAbilitySlot::Move;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability")
	TSubclassOf<UHitmenAbility> AbilityClass;

	/** Pickup spawned in the world by loot spawn points. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability")
	TSubclassOf<AHitmenAbilityPickup> PickupClass;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability", meta=(ClampMin="0"))
	float LootWeight = 1.f;

	virtual FPrimaryAssetId GetPrimaryAssetId() const override
	{
		return FPrimaryAssetId(TEXT("HitmenAbility"), GetFName());
	}
};
