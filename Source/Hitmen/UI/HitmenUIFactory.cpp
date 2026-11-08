// Hitmen — UI factory

#include "UI/HitmenUIFactory.h"
#include "Blueprint/WidgetTree.h"
#include "Components/TextBlock.h"
#include "Components/Button.h"
#include "Components/ButtonSlot.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Border.h"
#include "Components/Overlay.h"
#include "Components/OverlaySlot.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Styling/CoreStyle.h"
#include "Rendering/DrawElements.h"

namespace HitmenUI
{
	FLinearColor Background() { return FLinearColor(0.02f, 0.02f, 0.025f, 0.85f); }
	FLinearColor Panel()      { return FLinearColor(0.06f, 0.06f, 0.07f, 0.92f); }
	FLinearColor PanelLight() { return FLinearColor(0.14f, 0.14f, 0.16f, 0.95f); }
	FLinearColor Text()       { return FLinearColor(0.96f, 0.96f, 0.96f, 1.f); }
	FLinearColor TextDim()    { return FLinearColor(0.6f, 0.6f, 0.62f, 1.f); }
	FLinearColor Accent()     { return FLinearColor(0.84f, 0.11f, 0.17f, 1.f); }

	FSlateFontInfo Font(int32 Size, FName Typeface)
	{
		return FCoreStyle::GetDefaultFontStyle(Typeface, Size);
	}

	UTextBlock* MakeText(UWidgetTree* Tree, const FText& InText, int32 Size, FName Typeface, FLinearColor Color)
	{
		UTextBlock* Block = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
		Block->SetText(InText);
		Block->SetFont(Font(Size, Typeface));
		Block->SetColorAndOpacity(FSlateColor(Color));
		Block->SetJustification(ETextJustify::Left);
		return Block;
	}

	static FSlateBrush ColorBrush(const FLinearColor& Color)
	{
		FSlateBrush Brush;
		Brush.DrawAs = ESlateBrushDrawType::Image;
		Brush.TintColor = FSlateColor(Color);
		return Brush;
	}

	void StyleButton(UButton* Button, EHitmenButtonStyle Style)
	{
		FButtonStyle ButtonStyle;
		FLinearColor Normal = PanelLight();
		FLinearColor Hovered = FLinearColor(0.28f, 0.28f, 0.3f, 1.f);
		FLinearColor Pressed = Accent();
		switch (Style)
		{
		case EHitmenButtonStyle::Dashed:
		case EHitmenButtonStyle::Ghost:
			Normal = FLinearColor::Transparent;
			Hovered = FLinearColor(1.f, 1.f, 1.f, 0.08f);
			Pressed = FLinearColor(1.f, 1.f, 1.f, 0.16f);
			break;
		case EHitmenButtonStyle::Accent:
			Normal = Accent();
			Hovered = Accent() * 1.25f;
			Pressed = Accent() * 0.8f;
			break;
		default:
			break;
		}
		ButtonStyle.Normal = ColorBrush(Normal);
		ButtonStyle.Hovered = ColorBrush(Hovered);
		ButtonStyle.Pressed = ColorBrush(Pressed);
		ButtonStyle.Disabled = ColorBrush(Normal * FLinearColor(1.f, 1.f, 1.f, 0.4f));
		ButtonStyle.NormalPadding = FMargin(0.f);
		ButtonStyle.PressedPadding = FMargin(0.f);
		Button->SetStyle(ButtonStyle);
	}

	UButton* MakeButton(UWidgetTree* Tree, const FText& Label, int32 Size, EHitmenButtonStyle Style, UTextBlock** OutLabel)
	{
		UButton* Button = Tree->ConstructWidget<UButton>(UButton::StaticClass());
		StyleButton(Button, Style);

		UOverlay* Overlay = Tree->ConstructWidget<UOverlay>(UOverlay::StaticClass());
		if (Style == EHitmenButtonStyle::Dashed)
		{
			UHitmenDashedFrame* Frame = Tree->ConstructWidget<UHitmenDashedFrame>(UHitmenDashedFrame::StaticClass());
			Frame->SetVisibility(ESlateVisibility::HitTestInvisible);
			if (UOverlaySlot* FrameSlot = Overlay->AddChildToOverlay(Frame))
			{
				FrameSlot->SetHorizontalAlignment(HAlign_Fill);
				FrameSlot->SetVerticalAlignment(VAlign_Fill);
			}
		}
		UTextBlock* Text = MakeText(Tree, Label, Size, TEXT("Bold"));
		if (UOverlaySlot* TextSlot = Overlay->AddChildToOverlay(Text))
		{
			TextSlot->SetHorizontalAlignment(HAlign_Left);
			TextSlot->SetVerticalAlignment(VAlign_Center);
			TextSlot->SetPadding(FMargin(Size * 0.9f, Size * 0.55f));
		}
		Button->SetContent(Overlay);
		if (UButtonSlot* ContentSlot = Cast<UButtonSlot>(Overlay->Slot))
		{
			ContentSlot->SetHorizontalAlignment(HAlign_Fill);
			ContentSlot->SetVerticalAlignment(VAlign_Fill);
			ContentSlot->SetPadding(FMargin(0.f));
		}
		if (OutLabel)
		{
			*OutLabel = Text;
		}
		return Button;
	}

	UBorder* MakePanel(UWidgetTree* Tree, FLinearColor Color, FMargin Padding)
	{
		UBorder* Border = Tree->ConstructWidget<UBorder>(UBorder::StaticClass());
		Border->SetBrush(ColorBrush(Color));
		Border->SetPadding(Padding);
		return Border;
	}

	UCanvasPanelSlot* AddToCanvas(UCanvasPanel* Canvas, UWidget* Widget, const FAnchors& Anchors, FVector2D Position, FVector2D Alignment, FVector2D Size)
	{
		UCanvasPanelSlot* CanvasSlot = Canvas->AddChildToCanvas(Widget);
		CanvasSlot->SetAnchors(Anchors);
		CanvasSlot->SetAlignment(Alignment);
		CanvasSlot->SetPosition(Position);
		if (Size.IsZero())
		{
			CanvasSlot->SetAutoSize(true);
		}
		else
		{
			CanvasSlot->SetSize(Size);
		}
		return CanvasSlot;
	}

	void AddToVBox(UVerticalBox* Box, UWidget* Widget, FMargin Padding, bool bFill, EHorizontalAlignment HAlign)
	{
		UVerticalBoxSlot* BoxSlot = Box->AddChildToVerticalBox(Widget);
		BoxSlot->SetPadding(Padding);
		BoxSlot->SetHorizontalAlignment(HAlign);
		BoxSlot->SetSize(FSlateChildSize(bFill ? ESlateSizeRule::Fill : ESlateSizeRule::Automatic));
	}

	void AddToHBox(UHorizontalBox* Box, UWidget* Widget, FMargin Padding, bool bFill, EVerticalAlignment VAlign)
	{
		UHorizontalBoxSlot* BoxSlot = Box->AddChildToHorizontalBox(Widget);
		BoxSlot->SetPadding(Padding);
		BoxSlot->SetVerticalAlignment(VAlign);
		BoxSlot->SetSize(FSlateChildSize(bFill ? ESlateSizeRule::Fill : ESlateSizeRule::Automatic));
	}
}

// ---------------------------------------------------------------------------------------------
// Dashed frame
// ---------------------------------------------------------------------------------------------

int32 UHitmenDashedFrame::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const float Half = Thickness * 0.5f;
	const FVector2D Corners[] = { { Half, Half }, { Size.X - Half, Half }, { Size.X - Half, Size.Y - Half }, { Half, Size.Y - Half } };
	for (int32 Edge = 0; Edge < 4; ++Edge)
	{
		const FVector2D Start = Corners[Edge];
		const FVector2D End = Corners[(Edge + 1) % 4];
		const float Length = FVector2D::Distance(Start, End);
		const FVector2D Dir = (End - Start).GetSafeNormal();
		for (float Offset = 0.f; Offset < Length; Offset += DashLength + GapLength)
		{
			TArray<FVector2D> Points = { Start + Dir * Offset, Start + Dir * FMath::Min(Offset + DashLength, Length) };
			FSlateDrawElement::MakeLines(OutDrawElements, LayerId, AllottedGeometry.ToPaintGeometry(), Points, ESlateDrawEffect::None, Color, true, Thickness);
		}
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 1, InWidgetStyle, bParentEnabled);
}

// ---------------------------------------------------------------------------------------------
// List button
// ---------------------------------------------------------------------------------------------

void UHitmenListButton::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	EnsureLayout();
}

void UHitmenListButton::EnsureLayout()
{
	if (!WidgetTree->RootWidget)
	{
		UTextBlock* Text = nullptr;
		Button = HitmenUI::MakeButton(WidgetTree, FText::GetEmpty(), FontSize, Style, &Text);
		Label = Text;
		WidgetTree->RootWidget = Button;
	}
	if (Button && !Button->OnClicked.IsAlreadyBound(this, &UHitmenListButton::HandleClicked))
	{
		Button->OnClicked.AddDynamic(this, &UHitmenListButton::HandleClicked);
	}
}

void UHitmenListButton::Setup(const FText& InLabel, int32 InIndex, EHitmenButtonStyle InStyle, int32 InFontSize)
{
	Index = InIndex;
	Style = InStyle;
	FontSize = InFontSize;
	EnsureLayout();
	if (Button)
	{
		HitmenUI::StyleButton(Button, InStyle);
	}
	if (Label)
	{
		Label->SetFont(HitmenUI::Font(InFontSize));
	}
	SetLabel(InLabel);
}

void UHitmenListButton::SetLabel(const FText& InLabel)
{
	if (Label)
	{
		Label->SetText(InLabel);
	}
}

void UHitmenListButton::HandleClicked()
{
	OnItemClicked.Broadcast(Index);
}
