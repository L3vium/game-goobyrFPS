// Hitmen — menu kit: the building blocks of every front-end screen (menu buttons with an animated
// accent bar, arrow buttons, check toggles, square cards, a choice carousel, a gradient backdrop).
// All code-drawn so the look lives in one place; the team can still replace any screen with a WBP.

#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "HitmenMenuKit.generated.h"

class UButton;
class UTextBlock;

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenMenuClicked);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenMenuToggled, bool, bChecked);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenMenuChosen, int32, Index);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenMenuHovered, bool, bHovered);

UENUM(BlueprintType)
enum class EHitmenMenuButtonKind : uint8
{
	/** Big title-screen entry. */
	Primary,
	/** Smaller list entry. */
	Secondary,
	/** Filled red call to action (Ready, Create). */
	Accent,
	/** Small dimmed text (Back). */
	Back
};

/**
 * Text button: uppercase label (and optional dimmed subtitle). On hover a red bar grows on the
 * left, a soft gradient fades in behind and the label slides right; clicks play UI sounds.
 */
UCLASS()
class HITMEN_API UHitmenMenuButton : public UUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void Setup(const FText& InLabel, EHitmenMenuButtonKind InKind = EHitmenMenuButtonKind::Primary, const FText& InSubtitle = FText());

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetLabel(const FText& InLabel);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetSubtitle(const FText& InSubtitle);

	/** Disabled buttons are dimmed and ignore clicks. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetButtonEnabled(bool bEnabled);

	/** Selected = drawn like hovered (toggles such as Ready). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetSelected(bool bInSelected) { bSelected = bInSelected; }

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuClicked OnClicked;

	/** Index reported by OnIndexClicked (lists). */
	UPROPERTY(BlueprintReadWrite, Category="Hitmen|UI")
	int32 Index = INDEX_NONE;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuChosen OnIndexClicked;

protected:
	virtual void NativeOnInitialized() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleClicked();
	UFUNCTION() void HandleHovered();
	UFUNCTION() void HandleUnhovered();

	void EnsureLayout();
	void ApplyStyle();

	UPROPERTY(Transient) TObjectPtr<UButton> Button;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> LabelText;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> SubtitleText;

	FText Label;
	FText Subtitle;
	EHitmenMenuButtonKind Kind = EHitmenMenuButtonKind::Primary;
	float HoverAlpha = 0.f;
	bool bHovered = false;
	bool bSelected = false;
	bool bEnabledState = true;
};

/** Square arrow button (carousel prev / next, cosmetic arrows). */
UCLASS()
class HITMEN_API UHitmenArrowButton : public UUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void Setup(bool bInPointsRight, float InSize = 52.f);

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuClicked OnClicked;

protected:
	virtual void NativeOnInitialized() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;

	bool bPointsRight = true;
	float Size = 52.f;
	float HoverAlpha = 0.f;
	bool bHovered = false;
};

/** Check box + label; the whole row is clickable. */
UCLASS()
class HITMEN_API UHitmenToggle : public UUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void Setup(const FText& InLabel, bool bInChecked, int32 InFontSize = 20);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetChecked(bool bInChecked) { bChecked = bInChecked; }

	UFUNCTION(BlueprintPure, Category="Hitmen|UI")
	bool IsChecked() const { return bChecked; }

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetInteractable(bool bInInteractable) { bInteractable = bInInteractable; }

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuToggled OnToggled;

protected:
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;
	virtual FVector2D GetDesiredSizeForPaint() const;

	FText Label;
	int32 FontSize = 20;
	bool bChecked = false;
	bool bInteractable = true;
	bool bHovered = false;
	float HoverAlpha = 0.f;
};

/**
 * Square card: category on top, a big glyph (or an image), the title at the bottom. Hover lifts it
 * and fires OnHovered (loadout carousels open from it).
 */
UCLASS()
class HITMEN_API UHitmenCardTile : public UUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void Setup(const FText& InCategory, const FText& InTitle, const FText& InKey = FText(), float InSize = 170.f);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetTitle(const FText& InTitle) { Title = InTitle; }

	/** Optional picture (icon texture). */
	void SetImage(UTexture2D* Texture);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetSelected(bool bInSelected) { bSelected = bInSelected; }

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuClicked OnClicked;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuHovered OnHovered;

	/** Mouse wheel over the card: -1 / +1. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuChosen OnWheel;

	UPROPERTY(BlueprintReadWrite, Category="Hitmen|UI")
	int32 Index = INDEX_NONE;

protected:
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;

	FText Category;
	FText Title;
	FText Key;
	float Size = 170.f;
	FSlateBrush ImageBrush;
	bool bHasImage = false;
	bool bSelected = false;
	bool bHovered = false;
	float HoverAlpha = 0.f;
};

/**
 * A horizontal strip of choices (small square cards) that pops above a card: hover highlights,
 * click picks, the wheel steps through them.
 */
UCLASS()
class HITMEN_API UHitmenChoiceCarousel : public UUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetChoices(const TArray<FText>& InChoices, int32 InSelected);

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenMenuChosen OnChosen;

	/** Width needed for every choice. */
	FVector2D GetStripSize() const;

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;

	int32 ChoiceAt(const FGeometry& Geometry, const FVector2D& ScreenPosition) const;

	TArray<FText> Choices;
	int32 Selected = 0;
	int32 Hovered = INDEX_NONE;
	float ChoiceWidth = 132.f;
	float ChoiceHeight = 74.f;
	float Gap = 6.f;
};

/** Dark gradient over the live 3D scene behind front-end screens (left side, bottom edge). */
UCLASS()
class HITMEN_API UHitmenBackdrop : public UUserWidget
{
	GENERATED_BODY()

public:
	/** Share of the screen width darkened from the left (0..1). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|UI")
	float LeftCoverage = 0.62f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|UI")
	float Strength = 0.92f;

	/** Also darken from the right (lobby screens with columns on both sides). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|UI")
	float RightCoverage = 0.f;

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
};
