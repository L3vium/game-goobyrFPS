// Hitmen — UI factory

#include "UI/HitmenUIFactory.h"
#include "Engine/Font.h"
#include "Engine/World.h"
#include "Data/HitmenMapDefinition.h"
#include "Core/HitmenGameSettings.h"
#include "Rendering/SlateRenderer.h"
#include "Fonts/FontMeasure.h"
#include "Framework/Application/SlateApplication.h"
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
	FLinearColor Background() { return FLinearColor(0.012f, 0.013f, 0.016f, 0.88f); }
	FLinearColor Panel()      { return FLinearColor(0.03f, 0.033f, 0.04f, 0.9f); }
	FLinearColor PanelLight() { return FLinearColor(0.085f, 0.09f, 0.1f, 0.92f); }
	FLinearColor Text()       { return FLinearColor(0.95f, 0.95f, 0.95f, 1.f); }
	FLinearColor TextDim()    { return FLinearColor(0.52f, 0.54f, 0.58f, 1.f); }
	FLinearColor Accent()     { return FLinearColor(0.80f, 0.06f, 0.08f, 1.f); }
	FLinearColor Hairline()   { return FLinearColor(1.f, 1.f, 1.f, 0.16f); }
	FLinearColor Warning()    { return FLinearColor(1.f, 0.74f, 0.08f, 1.f); }

	static const UObject* GetUIFont()
	{
		static TWeakObjectPtr<const UObject> Cached;
		static bool bTried = false;
		if (!Cached.IsValid() && !bTried)
		{
			bTried = true;
			Cached = LoadObject<UObject>(nullptr, TEXT("/Game/Hitmen/UI/Fonts/FNT_Barlow.FNT_Barlow"), nullptr, LOAD_NoWarn | LOAD_Quiet);
		}
		return Cached.Get();
	}

	FSlateFontInfo Font(int32 Size, FName Typeface)
	{
		if (const UObject* FontObject = GetUIFont())
		{
			const FName Face = Typeface == TEXT("Black") ? FName(TEXT("Bold")) : Typeface;
			return FSlateFontInfo(FontObject, Size, Face);
		}
		return FCoreStyle::GetDefaultFontStyle(Typeface == TEXT("SemiBold") || Typeface == TEXT("Medium") ? FName(TEXT("Bold")) : Typeface, Size);
	}

	FSlateFontInfo SpacedFont(int32 Size, FName Typeface, int32 LetterSpacing)
	{
		FSlateFontInfo Info = Font(Size, Typeface);
		Info.LetterSpacing = LetterSpacing;
		return Info;
	}

	FVector2D MeasureText(const FString& InText, const FSlateFontInfo& FontInfo)
	{
		const TSharedRef<FSlateFontMeasure> Measure = FSlateApplication::Get().GetRenderer()->GetFontMeasureService();
		return Measure->Measure(InText, FontInfo);
	}

	void PaintLine(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D A, FVector2D B, const FLinearColor& Color, float Thickness)
	{
		TArray<FVector2D> Points = { A, B };
		FSlateDrawElement::MakeLines(Out, Layer, Geo.ToPaintGeometry(), Points, ESlateDrawEffect::None, Color, true, Thickness);
	}

	void PaintLines(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, const TArray<FVector2D>& Points, const FLinearColor& Color, float Thickness)
	{
		FSlateDrawElement::MakeLines(Out, Layer, Geo.ToPaintGeometry(), Points, ESlateDrawEffect::None, Color, true, Thickness);
	}

	void PaintBox(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D Position, FVector2D Size, const FLinearColor& Color)
	{
		FSlateDrawElement::MakeBox(Out, Layer, Geo.ToPaintGeometry(FVector2f(Size), FSlateLayoutTransform(FVector2f(Position))), FCoreStyle::Get().GetBrush("WhiteBrush"), ESlateDrawEffect::None, Color);
	}

	void PaintFrame(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D Position, FVector2D Size, const FLinearColor& Color, float Thickness)
	{
		const FVector2D A = Position;
		const FVector2D B = Position + FVector2D(Size.X, 0.f);
		const FVector2D C = Position + Size;
		const FVector2D D = Position + FVector2D(0.f, Size.Y);
		PaintLines(Out, Layer, Geo, { A, B, C, D, A }, Color, Thickness);
	}

	void PaintArc(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D Center, float Radius, float StartDeg, float EndDeg, const FLinearColor& Color, float Thickness, int32 Segments)
	{
		TArray<FVector2D> Points;
		for (int32 Index = 0; Index <= Segments; ++Index)
		{
			const float Angle = FMath::DegreesToRadians(FMath::Lerp(StartDeg, EndDeg, static_cast<float>(Index) / Segments));
			Points.Add(Center + FVector2D(FMath::Sin(Angle), -FMath::Cos(Angle)) * Radius);
		}
		PaintLines(Out, Layer, Geo, Points, Color, Thickness);
	}

	FVector2D PaintText(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, const FString& InText, FVector2D Position, const FSlateFontInfo& FontInfo, const FLinearColor& Color)
	{
		const FVector2D Size = MeasureText(InText, FontInfo);
		FSlateDrawElement::MakeText(Out, Layer, Geo.ToPaintGeometry(FVector2f(Size + FVector2D(4.f, 2.f)), FSlateLayoutTransform(FVector2f(Position))), InText, FontInfo, ESlateDrawEffect::None, Color);
		return Size;
	}

	void PaintGradient(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D Position, FVector2D Size, const FLinearColor& From, const FLinearColor& To, bool bHorizontal)
	{
		TArray<FSlateGradientStop> Stops;
		Stops.Add(FSlateGradientStop(FVector2D::ZeroVector, From));
		Stops.Add(FSlateGradientStop(bHorizontal ? FVector2D(Size.X, 0.f) : FVector2D(0.f, Size.Y), To));
		FSlateDrawElement::MakeGradient(Out, Layer, Geo.ToPaintGeometry(FVector2f(Size), FSlateLayoutTransform(FVector2f(Position))), Stops,
			bHorizontal ? Orient_Vertical : Orient_Horizontal, ESlateDrawEffect::None);
	}

	FVector2D PaintKey(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, const FString& Key, FVector2D Position, int32 FontSize, float Alpha)
	{
		const FSlateFontInfo KeyFont = Font(FontSize, TEXT("SemiBold"));
		const FVector2D TextSize = MeasureText(Key, KeyFont);
		const float Height = FontSize * 1.55f;
		const FVector2D Size(FMath::Max(Height, TextSize.X + FontSize * 0.9f), Height);
		PaintBox(Out, Layer, Geo, Position, Size, FLinearColor(1.f, 1.f, 1.f, 0.92f * Alpha));
		PaintText(Out, Layer + 1, Geo, Key, Position + (Size - TextSize) * 0.5f, KeyFont, FLinearColor(0.02f, 0.02f, 0.025f, Alpha));
		return Size;
	}

	void PaintImage(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, const FSlateBrush* Brush, FVector2D Position, FVector2D Size, const FLinearColor& Tint)
	{
		if (Brush)
		{
			FSlateDrawElement::MakeBox(Out, Layer, Geo.ToPaintGeometry(FVector2f(Size), FSlateLayoutTransform(FVector2f(Position))), Brush, ESlateDrawEffect::None, Tint);
		}
	}

	const UHitmenMapDefinition* FindCurrentMap(const UObject* WorldContext)
	{
		const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
		if (!World)
		{
			return nullptr;
		}
		const FString Current = UWorld::RemovePIEPrefix(World->GetOutermost()->GetName());
		const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
		for (int32 Index = 0; Index < Settings->Maps.Num(); ++Index)
		{
			const UHitmenMapDefinition* Map = Settings->LoadMap(Index);
			if (Map && Map->Level.ToSoftObjectPath().GetLongPackageName() == Current)
			{
				return Map;
			}
		}
		return nullptr;
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
