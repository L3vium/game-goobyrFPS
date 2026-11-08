// Hitmen — every selectable thing (loadout, cosmetics, icons, names) in one place

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Core/HitmenTypes.h"
#include "HitmenCatalog.generated.h"

class UHitmenWeaponData;
class UHitmenAbilityData;
class UHitmenCosmeticItem;

UCLASS(BlueprintType)
class HITMEN_API UHitmenCatalog : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	/** Weapons offered in the loadout and the debug panel. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<TObjectPtr<UHitmenWeaponData>> Weapons;

	/** Weapons that can appear on loot spawn points (may differ from the loadout). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<TObjectPtr<UHitmenWeaponData>> LootWeapons;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<TObjectPtr<UHitmenAbilityData>> MoveAbilities;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<TObjectPtr<UHitmenAbilityData>> ManipulationAbilities;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<TObjectPtr<UHitmenCosmeticItem>> Cosmetics;

	/** Player icons shown in the lobby. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<TSoftObjectPtr<UTexture2D>> PlayerIcons;

	/** Random names used when no Steam name is available. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<FString> RandomNames = {
		TEXT("Agent Crow"), TEXT("Silent Moth"), TEXT("Velvet Fox"), TEXT("Grey Heron"),
		TEXT("Paper Wolf"), TEXT("Quiet Lynx"), TEXT("Agent Juniper"), TEXT("Iron Finch"),
		TEXT("Night Otter"), TEXT("Agent Copper"), TEXT("Cold Badger"), TEXT("Agent Sable")
	};

	/** Default team colours, cycled when teams are created. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<FLinearColor> TeamColors = {
		FLinearColor(0.85f, 0.2f, 0.2f), FLinearColor(0.2f, 0.45f, 0.9f),
		FLinearColor(0.25f, 0.75f, 0.3f), FLinearColor(0.9f, 0.7f, 0.15f),
		FLinearColor(0.65f, 0.3f, 0.85f), FLinearColor(0.2f, 0.8f, 0.8f)
	};

	UFUNCTION(BlueprintPure, Category="Hitmen|Catalog")
	TArray<UHitmenCosmeticItem*> GetCosmeticsForSlot(EHitmenCosmeticSlot Slot) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Catalog")
	TArray<UHitmenAbilityData*> GetAbilities(EHitmenAbilitySlot Slot) const;

	UFUNCTION(BlueprintCallable, Category="Hitmen|Catalog")
	FHitmenCosmeticLoadout MakeRandomCosmetics() const;

	UFUNCTION(BlueprintCallable, Category="Hitmen|Catalog")
	FString MakeRandomName() const;

	virtual FPrimaryAssetId GetPrimaryAssetId() const override
	{
		return FPrimaryAssetId(TEXT("HitmenCatalog"), GetFName());
	}
};
