// Hitmen — match events over everything (with or without a pawn): toasts at the top, the event camera top left,
// letterbox bars during the end cinematic

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "HitmenMatchEventsWidget.generated.h"

class UImage;
class UBorder;

UCLASS()
class HITMEN_API UHitmenMatchEventsWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleToast(const FHitmenToast& Toast);

	/** Event camera picture size on screen (px at 1080p). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Events")
	FVector2D EventCameraSize = FVector2D(400.f, 225.f);

	UPROPERTY(EditAnywhere, Category="Hitmen|Events")
	FVector2D EventCameraOffset = FVector2D(28.f, 74.f);

	/** Toast fade in / out (s). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Events", meta=(ClampMin="0.05"))
	float ToastFade = 0.35f;

	/** Letterbox bar height as a share of the screen during the end cinematic. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Events", meta=(ClampMin="0", ClampMax="0.3"))
	float LetterboxHeight = 0.11f;

	UPROPERTY(Transient) TObjectPtr<UImage> EventImage;
	UPROPERTY(Transient) TObjectPtr<UBorder> EventFrame;

	/** The picture shown (a UPROPERTY: the brush resource must stay referenced). */
	UPROPERTY(Transient) TObjectPtr<class UTextureRenderTarget2D> ShownTarget;

	FHitmenToast CurrentToast;
	float ToastStart = -100.f;
	float EventAlpha = 0.f;
	float LetterboxAlpha = 0.f;
	FString EventTitle;
};
