// Hitmen — name tags over the game (one painted layer, with or without a pawn): players for watchers (and for players
// when Project Settings -> Hitmen -> Watchers allows it), watchers, and the "watched by" line. Reads the GameState
// player array and the UI hub only.

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "HitmenNameTagsWidget.generated.h"

UCLASS()
class HITMEN_API UHitmenNameTagsWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	/** Tag height above a player's capsule top (cm). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Name Tags")
	float PlayerTagOffset = 35.f;

	/** Tag height above a watcher's orb (cm). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Name Tags")
	float WatcherTagOffset = 30.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Name Tags")
	FLinearColor PlayerTagColor = FLinearColor(0.55f, 0.92f, 1.f);

	UPROPERTY(EditAnywhere, Category="Hitmen|Name Tags")
	FLinearColor WatcherTagColor = FLinearColor(0.7f, 0.75f, 0.85f);
};
