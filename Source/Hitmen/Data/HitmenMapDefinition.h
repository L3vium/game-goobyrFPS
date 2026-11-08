// Hitmen — a playable map entry for the lobby carousel

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HitmenMapDefinition.generated.h"

class UHitmenMatchRules;

UCLASS(BlueprintType)
class HITMEN_API UHitmenMapDefinition : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map")
	FText DisplayName;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map", meta=(MultiLine=true))
	FText Description;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map")
	TSoftObjectPtr<UTexture2D> Thumbnail;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map")
	TSoftObjectPtr<UWorld> Level;

	/** Hidden maps are not shown in the lobby carousel (gym, test maps). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map")
	bool bShowInCarousel = true;

	/** Rules override for this map; falls back to the project default rules. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map")
	TObjectPtr<UHitmenMatchRules> RulesOverride;

	/** Map bounds used by the minimap and pause map (world XY). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map")
	FBox2D WorldBounds = FBox2D(FVector2D(-20000.f, -20000.f), FVector2D(20000.f, 20000.f));

	/** Top-down texture for the minimap / pause map (optional, team-made). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map")
	TSoftObjectPtr<UTexture2D> MapTexture;

	virtual FPrimaryAssetId GetPrimaryAssetId() const override
	{
		return FPrimaryAssetId(TEXT("HitmenMap"), GetFName());
	}
};
