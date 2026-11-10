// Hitmen — menu kit: icon buttons and the always-visible choice strip

#include "UI/HitmenMenuKit.h"
#include "UI/HitmenUIFactory.h"
#include "Data/HitmenSoundSet.h"
#include "Blueprint/WidgetTree.h"
#include "Components/SizeBox.h"
#include "Engine/Texture2D.h"
#include "Rendering/DrawElements.h"

namespace
{
	USizeBox* EnsureSizeRoot(UWidgetTree* Tree)
	{
		if (!Tree)
		{
			return nullptr;
		}
		USizeBox* Box = Cast<USizeBox>(Tree->RootWidget);
		if (!Box)
		{
			Box = Tree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
			Tree->RootWidget = Box;
		}
		return Box;
	}
}

// =============================================================================================
// Icon button
// =============================================================================================

void UHitmenIconButton::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	// The sized root must exist before Slate builds the widget, or later sizes are ignored.
	EnsureSizeRoot(WidgetTree);
	UpdateSize();
}

void UHitmenIconButton::Setup(EHitmenIconGlyph InGlyph, float InSize, bool bInFramed)
{
	Glyph = InGlyph;
	Size = InSize;
	bFramed = bInFramed;
	SetVisibility(ESlateVisibility::Visible);
	UpdateSize();
}

void UHitmenIconButton::SetLabel(const FText& InLabel)
{
	Label = InLabel;
	UpdateSize();
}

void UHitmenIconButton::UpdateSize()
{
	if (USizeBox* Box = EnsureSizeRoot(WidgetTree))
	{
		float Width = Size;
		if (!Label.IsEmpty())
		{
			Width += 4.f + HitmenUI::MeasureText(Label.ToString().ToUpper(), HitmenUI::SpacedFont(FMath::RoundToInt(Size * 0.42f), TEXT("SemiBold"), 120)).X + 18.f;
		}
		Box->SetWidthOverride(Width);
		Box->SetHeightOverride(Size);
	}
}

void UHitmenIconButton::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HoverAlpha = FMath::FInterpTo(HoverAlpha, bHovered ? 1.f : 0.f, InDeltaTime, 18.f);
}

FReply UHitmenIconButton::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (InMouseEvent.GetEffectingButton() != EKeys::LeftMouseButton)
	{
		return FReply::Unhandled();
	}
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
	OnClicked.Broadcast();
	return FReply::Handled();
}

FReply UHitmenIconButton::NativeOnMouseButtonDoubleClick(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	return NativeOnMouseButtonDown(InGeometry, InMouseEvent);
}

void UHitmenIconButton::NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseEnter(InGeometry, InMouseEvent);
	bHovered = true;
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
}

void UHitmenIconButton::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	bHovered = false;
}

void UHitmenIconButton::PaintGlyph(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, EHitmenIconGlyph InGlyph, FVector2D C, float E, const FLinearColor& Color, float T)
{
	switch (InGlyph)
	{
	case EHitmenIconGlyph::Close:
		HitmenUI::PaintLine(Out, Layer, Geo, C + FVector2D(-E, -E), C + FVector2D(E, E), Color, T);
		HitmenUI::PaintLine(Out, Layer, Geo, C + FVector2D(-E, E), C + FVector2D(E, -E), Color, T);
		break;
	case EHitmenIconGlyph::Refresh:
	{
		// Most of a circle with an arrow head at its open end.
		HitmenUI::PaintArc(Out, Layer, Geo, C, E, -50.f, 250.f, Color, T, 28);
		const float End = FMath::DegreesToRadians(-50.f);
		const FVector2D Tip = C + FVector2D(FMath::Cos(End), FMath::Sin(End)) * E;
		const float Head = E * 0.62f;
		HitmenUI::PaintLines(Out, Layer, Geo, { Tip + FVector2D(-Head, -Head * 0.1f), Tip, Tip + FVector2D(Head * 0.05f, Head) }, Color, T);
		break;
	}
	case EHitmenIconGlyph::Eye:
	case EHitmenIconGlyph::EyeOff:
	{
		// Almond outline and a round iris.
		TArray<FVector2D> Upper;
		TArray<FVector2D> Lower;
		for (int32 Step = 0; Step <= 16; ++Step)
		{
			const float X = FMath::Lerp(-E, E, Step / 16.f);
			const float Y = E * 0.62f * (1.f - FMath::Square(X / E));
			Upper.Add(C + FVector2D(X, -Y));
			Lower.Add(C + FVector2D(X, Y));
		}
		HitmenUI::PaintLines(Out, Layer, Geo, Upper, Color, T);
		HitmenUI::PaintLines(Out, Layer, Geo, Lower, Color, T);
		HitmenUI::PaintArc(Out, Layer, Geo, C, E * 0.3f, 0.f, 360.f, Color, T, 16);
		if (InGlyph == EHitmenIconGlyph::EyeOff)
		{
			HitmenUI::PaintLine(Out, Layer, Geo, C + FVector2D(-E, E * 0.85f), C + FVector2D(E, -E * 0.85f), Color, T * 1.2f);
		}
		break;
	}
	case EHitmenIconGlyph::Dice:
	{
		HitmenUI::PaintFrame(Out, Layer, Geo, C - FVector2D(E), FVector2D(E * 2.f), Color, T);
		const float Pip = FMath::Max(2.f, E * 0.32f);
		for (const FVector2D& Offset : { FVector2D(-0.45f, -0.45f), FVector2D(0.f, 0.f), FVector2D(0.45f, 0.45f) })
		{
			HitmenUI::PaintBox(Out, Layer, Geo, C + Offset * E - FVector2D(Pip * 0.5f), FVector2D(Pip), Color);
		}
		break;
	}
	}
}

int32 UHitmenIconButton::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Local = AllottedGeometry.GetLocalSize();
	if (bFramed)
	{
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Local, FMath::Lerp(FLinearColor(0.05f, 0.052f, 0.06f, 0.85f), FLinearColor(0.18f, 0.185f, 0.2f, 0.95f), HoverAlpha));
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, Local, FLinearColor(1.f, 1.f, 1.f, 0.2f + 0.5f * HoverAlpha), 1.f);
	}
	const FLinearColor Color = FMath::Lerp(FLinearColor(0.85f, 0.86f, 0.88f, bFramed ? 1.f : 0.75f), FLinearColor::White, HoverAlpha);
	PaintGlyph(OutDrawElements, LayerId + 2, AllottedGeometry, Glyph, FVector2D(Size * 0.5f, Local.Y * 0.5f), Size * 0.24f, Color, FMath::Max(1.5f, Size * 0.06f));
	if (!Label.IsEmpty())
	{
		const FSlateFontInfo LabelFont = HitmenUI::SpacedFont(FMath::RoundToInt(Size * 0.42f), TEXT("SemiBold"), 120);
		const FString Text = Label.ToString().ToUpper();
		const FVector2D TextSize = HitmenUI::MeasureText(Text, LabelFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Text, FVector2D(Size + 2.f, (Local.Y - TextSize.Y) * 0.5f), LabelFont, Color);
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Choice strip
// =============================================================================================

void UHitmenChoiceStrip::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	EnsureSizeRoot(WidgetTree);
	UpdateSize();
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenChoiceStrip::SetChoices(const TArray<FHitmenChoiceItem>& InChoices, int32 InSelected, float InTileSize, int32 InColumns)
{
	Choices = InChoices;
	Selected = InSelected;
	TileSize = InTileSize;
	Columns = InColumns;
	Brushes.SetNum(Choices.Num());
	for (int32 Index = 0; Index < Choices.Num(); ++Index)
	{
		Brushes[Index] = FSlateBrush();
		if (UTexture2D* Icon = Choices[Index].Icon)
		{
			Brushes[Index].SetResourceObject(Icon);
			Brushes[Index].ImageSize = FVector2D(Icon->GetSizeX(), Icon->GetSizeY());
		}
	}
	SetVisibility(ESlateVisibility::Visible);
	UpdateSize();
}

void UHitmenChoiceStrip::UpdateSize()
{
	if (USizeBox* Box = EnsureSizeRoot(WidgetTree))
	{
		const int32 ColumnCount = FMath::Min(GetColumnCount(), FMath::Max(1, Choices.Num()));
		const int32 Rows = FMath::DivideAndRoundUp(FMath::Max(1, Choices.Num()), GetColumnCount());
		Box->SetWidthOverride(ColumnCount * (TileSize + Gap) - Gap);
		Box->SetHeightOverride(Rows * (TileSize + Gap) - Gap);
	}
}

int32 UHitmenChoiceStrip::ChoiceAt(const FGeometry& Geometry, const FVector2D& ScreenPosition) const
{
	const FVector2D Local = Geometry.AbsoluteToLocal(ScreenPosition);
	if (Local.Y < 0.f || Local.X < 0.f)
	{
		return INDEX_NONE;
	}
	const int32 Column = FMath::FloorToInt(Local.X / (TileSize + Gap));
	const int32 Row = FMath::FloorToInt(Local.Y / (TileSize + Gap));
	if (Column >= GetColumnCount() || Local.X - Column * (TileSize + Gap) > TileSize || Local.Y - Row * (TileSize + Gap) > TileSize)
	{
		return INDEX_NONE;
	}
	const int32 Choice = Row * GetColumnCount() + Column;
	return Choices.IsValidIndex(Choice) ? Choice : INDEX_NONE;
}

FVector2D UHitmenChoiceStrip::GetTilePosition(int32 Choice) const
{
	const int32 ColumnCount = GetColumnCount();
	return FVector2D((Choice % ColumnCount) * (TileSize + Gap), (Choice / ColumnCount) * (TileSize + Gap));
}

FReply UHitmenChoiceStrip::NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	const int32 NewHovered = ChoiceAt(InGeometry, InMouseEvent.GetScreenSpacePosition());
	if (NewHovered != Hovered && NewHovered != INDEX_NONE)
	{
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
	}
	Hovered = NewHovered;
	return FReply::Handled();
}

FReply UHitmenChoiceStrip::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	const int32 Choice = ChoiceAt(InGeometry, InMouseEvent.GetScreenSpacePosition());
	if (Choice != INDEX_NONE && InMouseEvent.GetEffectingButton() == EKeys::LeftMouseButton)
	{
		Selected = Choice;
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		OnChosen.Broadcast(Choice);
		return FReply::Handled();
	}
	return FReply::Unhandled();
}

FReply UHitmenChoiceStrip::NativeOnMouseButtonDoubleClick(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	return NativeOnMouseButtonDown(InGeometry, InMouseEvent);
}

void UHitmenChoiceStrip::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	Hovered = INDEX_NONE;
}

int32 UHitmenChoiceStrip::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FSlateFontInfo NameFont = HitmenUI::SpacedFont(FMath::Max(11, FMath::RoundToInt(TileSize * 0.125f)), TEXT("SemiBold"), 30);
	const float NameBand = TileSize * 0.26f;
	for (int32 Choice = 0; Choice < Choices.Num(); ++Choice)
	{
		const FVector2D Position = GetTilePosition(Choice);
		const FVector2D Tile(TileSize);
		const bool bIsSelected = Choice == Selected;
		const bool bIsHovered = Choice == Hovered;
		const bool bLit = bIsSelected || bIsHovered;
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, Position, Tile,
			bIsSelected ? FLinearColor(0.2f, 0.205f, 0.22f, 0.96f) : (bIsHovered ? FLinearColor(0.13f, 0.135f, 0.15f, 0.95f) : FLinearColor(0.045f, 0.047f, 0.055f, 0.9f)));
		// Icon in the upper part of the tile.
		const FVector2D IconArea(TileSize - 16.f, TileSize - NameBand - 12.f);
		const float Fit = FMath::Min(IconArea.X, IconArea.Y);
		if (Brushes.IsValidIndex(Choice) && Brushes[Choice].GetResourceObject())
		{
			HitmenUI::PaintImage(OutDrawElements, LayerId + 1, AllottedGeometry, &Brushes[Choice], Position + FVector2D((TileSize - Fit) * 0.5f, 8.f + (IconArea.Y - Fit) * 0.5f), FVector2D(Fit),
				FLinearColor(1.f, 1.f, 1.f, bLit ? 1.f : 0.7f));
		}
		else
		{
			// "None": a struck-through ring.
			const FVector2D Center = Position + FVector2D(TileSize * 0.5f, 8.f + IconArea.Y * 0.5f);
			const float Radius = Fit * 0.3f;
			const FLinearColor Ring(1.f, 1.f, 1.f, bLit ? 0.8f : 0.4f);
			HitmenUI::PaintArc(OutDrawElements, LayerId + 1, AllottedGeometry, Center, Radius, 0.f, 360.f, Ring, 2.f, 24);
			HitmenUI::PaintLine(OutDrawElements, LayerId + 1, AllottedGeometry, Center + FVector2D(-Radius, Radius) * 0.7f, Center + FVector2D(Radius, -Radius) * 0.7f, Ring, 2.f);
		}
		// Name, shortened so it always stays inside the tile.
		FString Name = Choices[Choice].Name.ToString().ToUpper();
		const float Room = TileSize - 10.f;
		if (HitmenUI::MeasureText(Name, NameFont).X > Room)
		{
			while (Name.Len() > 1 && HitmenUI::MeasureText(Name + TEXT("."), NameFont).X > Room)
			{
				Name.LeftChopInline(1);
			}
			Name += TEXT(".");
		}
		const FVector2D NameSize = HitmenUI::MeasureText(Name, NameFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Name, Position + FVector2D((TileSize - NameSize.X) * 0.5f, TileSize - NameBand + (NameBand - NameSize.Y) * 0.5f - 2.f),
			NameFont, bLit ? HitmenUI::Text() : HitmenUI::TextDim());
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 3, AllottedGeometry, Position, Tile,
			bIsSelected ? FLinearColor(1.f, 1.f, 1.f, 0.85f) : FLinearColor(1.f, 1.f, 1.f, bIsHovered ? 0.45f : 0.14f), bIsSelected ? 2.f : 1.f);
		if (bIsSelected)
		{
			HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, Position + FVector2D(0.f, TileSize - 4.f), FVector2D(TileSize, 4.f), HitmenUI::Accent());
		}
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}
