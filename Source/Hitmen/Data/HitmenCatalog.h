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

	/** Player icons shown in the lobby. When empty, the coloured glyph icons below are used. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<TSoftObjectPtr<UTexture2D>> PlayerIcons;

	/** Placeholder icons (no texture needed): one colour + glyph per icon. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<FLinearColor> FallbackIconColors = {
		FLinearColor(0.85f, 0.10f, 0.10f), FLinearColor(0.95f, 0.55f, 0.05f), FLinearColor(0.95f, 0.85f, 0.10f), FLinearColor(0.20f, 0.75f, 0.25f),
		FLinearColor(0.10f, 0.65f, 0.85f), FLinearColor(0.25f, 0.30f, 0.90f), FLinearColor(0.65f, 0.25f, 0.85f), FLinearColor(0.90f, 0.90f, 0.90f) };

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Catalog")
	TArray<FString> FallbackIconGlyphs = { TEXT("A"), TEXT("B"), TEXT("C"), TEXT("D"), TEXT("E"), TEXT("F"), TEXT("G"), TEXT("H") };

	/** Number of icons a player can cycle through. */
	int32 GetIconCount() const { return PlayerIcons.Num() > 0 ? PlayerIcons.Num() : FMath::Max(1, FallbackIconColors.Num()); }

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
