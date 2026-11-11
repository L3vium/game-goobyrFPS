// Hitmen — map marker kinds (minimap, pause map, legend)

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HitmenMapMarkerType.generated.h"

class UTexture2D;

UENUM(BlueprintType)
enum class EHitmenMapMarkerShape : uint8
{
	Square,
	Diamond,
	Triangle,
	Ring
};

/**
 * A kind of map marker (retrieval point, camera, ...): how it is drawn on the minimap and the pause map and what the
 * legend calls it. One data asset per kind in /Game/Hitmen/Data/MapMarkers. To show an actor on the maps, add a
 * UHitmenMapMarkerComponent to it and pick its kind (docs/Pipelines.md).
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenMapMarkerType : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	/** Legend row (pause menu). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	FText DisplayName;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	FLinearColor Color = FLinearColor::White;

	/** Drawn when there is no icon. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	EHitmenMapMarkerShape Shape = EHitmenMapMarkerShape::Square;

	/** Optional icon (white on transparent, tinted by Color); replaces the shape. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	TObjectPtr<UTexture2D> Icon;

	/** Size on the pause map, in pixels. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker", meta=(ClampMin="2"))
	float Size = 12.f;

	/** The minimap draws the marker at Size times this. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker", meta=(ClampMin="0.1"))
	float MinimapScale = 0.7f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	bool bShowOnMinimap = true;

	/** Minimap: out of range, the marker sticks to the edge (objectives) instead of disappearing. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	bool bClampToMinimapEdge = false;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	bool bShowOnPauseMap = true;

	/** A legend row with a toggle (only for kinds present on the map). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	bool bShowInLegend = true;

	/** Legend rows are sorted by this, then by name. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Map Marker")
	int32 LegendOrder = 0;
};
