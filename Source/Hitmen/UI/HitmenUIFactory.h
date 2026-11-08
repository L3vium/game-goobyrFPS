// Hitmen — UI style + builders used by the default C++ layouts, plus two reusable widgets:
// a dashed frame and an indexed list button.

#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "Components/SlateWrapperTypes.h"
#include "Layout/Margin.h"
#include "HitmenUIFactory.generated.h"

class UWidgetTree;
class UTextBlock;
class UButton;
class UCanvasPanel;
class UCanvasPanelSlot;
class UBorder;
class UVerticalBox;
class UHorizontalBox;

/** Visual style of a Hitmen button. */
UENUM(BlueprintType)
enum class EHitmenButtonStyle : uint8
{
	/** Filled square. */
	Solid,
	/** Dashed outline, no fill (Join, + Create team). */
	Dashed,
	/** Transparent, text only. */
	Ghost,
	/** Accent colour (Start, Launch). */
	Accent
};

/** Shared palette and fonts. The team restyles by editing these or by building WBP layouts. */
namespace HitmenUI
{
	HITMEN_API FLinearColor Background();
	HITMEN_API FLinearColor Panel();
	HITMEN_API FLinearColor PanelLight();
	HITMEN_API FLinearColor Text();
	HITMEN_API FLinearColor TextDim();
	HITMEN_API FLinearColor Accent();
	HITMEN_API FSlateFontInfo Font(int32 Size, FName Typeface = TEXT("Bold"));

	HITMEN_API UTextBlock* MakeText(UWidgetTree* Tree, const FText& InText, int32 Size, FName Typeface = TEXT("Bold"), FLinearColor Color = FLinearColor(0.96f, 0.96f, 0.96f, 1.f));
	/** Square button with a left-aligned label. OutLabel receives the label text block. */
	HITMEN_API UButton* MakeButton(UWidgetTree* Tree, const FText& Label, int32 Size, EHitmenButtonStyle Style, UTextBlock** OutLabel = nullptr);
	HITMEN_API UBorder* MakePanel(UWidgetTree* Tree, FLinearColor Color, FMargin Padding = FMargin(16.f));
	HITMEN_API void StyleButton(UButton* Button, EHitmenButtonStyle Style);

	HITMEN_API UCanvasPanelSlot* AddToCanvas(UCanvasPanel* Canvas, UWidget* Widget, const FAnchors& Anchors, FVector2D Position, FVector2D Alignment, FVector2D Size = FVector2D::ZeroVector);
	HITMEN_API void AddToVBox(UVerticalBox* Box, UWidget* Widget, FMargin Padding = FMargin(0.f, 4.f), bool bFill = false, EHorizontalAlignment HAlign = HAlign_Fill);
	HITMEN_API void AddToHBox(UHorizontalBox* Box, UWidget* Widget, FMargin Padding = FMargin(4.f, 0.f), bool bFill = false, EVerticalAlignment VAlign = VAlign_Center);
}

/** Paints a dashed rectangle around its geometry. */
UCLASS()
class HITMEN_API UHitmenDashedFrame : public UUserWidget
{
	GENERATED_BODY()

public:
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|UI")
	FLinearColor Color = FLinearColor(1.f, 1.f, 1.f, 0.8f);

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|UI")
	float Thickness = 2.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|UI")
	float DashLength = 10.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|UI")
	float GapLength = 6.f;

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenListButtonClicked, int32, Index);

/** A button that remembers an index and reports it when clicked (lists, carousels, catalogs). */
UCLASS()
class HITMEN_API UHitmenListButton : public UUserWidget
{
	GENERATED_BODY()

public:
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenListButtonClicked OnItemClicked;

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void Setup(const FText& InLabel, int32 InIndex, EHitmenButtonStyle InStyle = EHitmenButtonStyle::Solid, int32 InFontSize = 18);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetLabel(const FText& InLabel);

	UFUNCTION(BlueprintPure, Category="Hitmen|UI")
	int32 GetIndex() const { return Index; }

protected:
	virtual void NativeOnInitialized() override;

	UPROPERTY(meta=(BindWidgetOptional))
	TObjectPtr<UButton> Button;

	UPROPERTY(meta=(BindWidgetOptional))
	TObjectPtr<UTextBlock> Label;

	UFUNCTION()
	void HandleClicked();

	void EnsureLayout();

	int32 Index = INDEX_NONE;
	EHitmenButtonStyle Style = EHitmenButtonStyle::Solid;
	int32 FontSize = 18;
};
