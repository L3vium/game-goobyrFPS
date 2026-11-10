// Hitmen — menu kit

#include "UI/HitmenMenuKit.h"
#include "UI/HitmenUIFactory.h"
#include "Data/HitmenSoundSet.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Button.h"
#include "Components/ButtonSlot.h"
#include "Components/TextBlock.h"
#include "Components/SizeBox.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Engine/Texture2D.h"
#include "Rendering/DrawElements.h"

namespace
{
	FButtonStyle InvisibleButtonStyle()
	{
		FButtonStyle Style;
		FSlateBrush Clear;
		Clear.DrawAs = ESlateBrushDrawType::NoDrawType;
		Style.Normal = Clear;
		Style.Hovered = Clear;
		Style.Pressed = Clear;
		Style.Disabled = Clear;
		Style.NormalPadding = FMargin(0.f);
		Style.PressedPadding = FMargin(0.f);
		return Style;
	}
}

// =============================================================================================
// Menu button
// =============================================================================================

void UHitmenMenuButton::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	EnsureLayout();
}

void UHitmenMenuButton::EnsureLayout()
{
	if (Button || !WidgetTree)
	{
		return;
	}
	Button = WidgetTree->ConstructWidget<UButton>(UButton::StaticClass());
	// Never keyboard-focusable: Space / Enter must not re-press the last clicked button (Space = Back).
	PRAGMA_DISABLE_DEPRECATION_WARNINGS
	Button->IsFocusable = false;
	PRAGMA_ENABLE_DEPRECATION_WARNINGS
	Button->SetStyle(InvisibleButtonStyle());
	Button->OnClicked.AddDynamic(this, &UHitmenMenuButton::HandleClicked);
	Button->OnHovered.AddDynamic(this, &UHitmenMenuButton::HandleHovered);
	Button->OnUnhovered.AddDynamic(this, &UHitmenMenuButton::HandleUnhovered);
	WidgetTree->RootWidget = Button;

	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	LabelText = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	SubtitleText = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	Column->AddChildToVerticalBox(LabelText);
	if (UVerticalBoxSlot* SubSlot = Column->AddChildToVerticalBox(SubtitleText))
	{
		SubSlot->SetPadding(FMargin(0.f, 0.f, 0.f, 0.f));
	}
	Button->SetContent(Column);
	if (UButtonSlot* ContentSlot = Cast<UButtonSlot>(Column->Slot))
	{
		ContentSlot->SetHorizontalAlignment(HAlign_Left);
		ContentSlot->SetVerticalAlignment(VAlign_Center);
		ContentSlot->SetPadding(FMargin(22.f, 10.f, 28.f, 10.f));
	}
	ApplyStyle();
}

void UHitmenMenuButton::Setup(const FText& InLabel, EHitmenMenuButtonKind InKind, const FText& InSubtitle)
{
	Kind = InKind;
	Label = InLabel;
	Subtitle = InSubtitle;
	EnsureLayout();
	ApplyStyle();
}

void UHitmenMenuButton::SetLabel(const FText& InLabel)
{
	Label = InLabel;
	ApplyStyle();
}

void UHitmenMenuButton::SetSubtitle(const FText& InSubtitle)
{
	Subtitle = InSubtitle;
	ApplyStyle();
}

void UHitmenMenuButton::SetButtonEnabled(bool bEnabled)
{
	bEnabledState = bEnabled;
	if (Button)
	{
		Button->SetIsEnabled(bEnabled);
	}
	ApplyStyle();
}

void UHitmenMenuButton::ApplyStyle()
{
	if (!LabelText)
	{
		return;
	}
	int32 Size = 34;
	FName Face = TEXT("SemiBold");
	int32 Spacing = 60;
	switch (Kind)
	{
	case EHitmenMenuButtonKind::Secondary: Size = 22; Spacing = 70; break;
	case EHitmenMenuButtonKind::Accent:    Size = 24; Spacing = 90; break;
	case EHitmenMenuButtonKind::Back:      Size = 19; Face = TEXT("Medium"); Spacing = 120; break;
	case EHitmenMenuButtonKind::Block:     Size = 28; Spacing = 90; break;
	case EHitmenMenuButtonKind::Dashed:    Size = 28; Spacing = 90; break;
	case EHitmenMenuButtonKind::Chip:      Size = 15; Spacing = 140; break;
	default: break;
	}
	if (UButtonSlot* ContentSlot = Button && Button->GetContent() ? Cast<UButtonSlot>(Button->GetContent()->Slot) : nullptr)
	{
		const bool bBlock = Kind == EHitmenMenuButtonKind::Block || Kind == EHitmenMenuButtonKind::Dashed;
		ContentSlot->SetPadding(bBlock ? FMargin(26.f, 16.f, 26.f, 16.f)
			: (Kind == EHitmenMenuButtonKind::Chip ? FMargin(14.f, 7.f, 14.f, 7.f) : FMargin(22.f, 10.f, 28.f, 10.f)));
	}
	LabelText->SetText(FText::FromString(Label.ToString().ToUpper()));
	LabelText->SetFont(HitmenUI::SpacedFont(Size, Face, Spacing));
	const FLinearColor Color = !bEnabledState ? HitmenUI::TextDim() * FLinearColor(1, 1, 1, 0.6f)
		: (Kind == EHitmenMenuButtonKind::Back ? HitmenUI::TextDim() : HitmenUI::Text());
	LabelText->SetColorAndOpacity(FSlateColor(Color));
	SubtitleText->SetText(Subtitle);
	SubtitleText->SetFont(HitmenUI::Font(FMath::Max(14, Size / 2), TEXT("Italic")));
	SubtitleText->SetColorAndOpacity(FSlateColor(HitmenUI::TextDim()));
	SubtitleText->SetVisibility(Subtitle.IsEmpty() ? ESlateVisibility::Collapsed : ESlateVisibility::HitTestInvisible);
}

void UHitmenMenuButton::HandleClicked()
{
	if (!bEnabledState)
	{
		return;
	}
	UHitmenSoundLibrary::PlayHitmenSound2D(this, Kind == EHitmenMenuButtonKind::Back ? EHitmenSound::UIBack : EHitmenSound::UIClick);
	OnClicked.Broadcast();
	OnIndexClicked.Broadcast(Index);
}

void UHitmenMenuButton::HandleHovered()
{
	bHovered = true;
	if (bEnabledState)
	{
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
	}
}

void UHitmenMenuButton::HandleUnhovered()
{
	bHovered = false;
}

void UHitmenMenuButton::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const bool bLit = (bHovered || bSelected) && bEnabledState;
	HoverAlpha = FMath::FInterpTo(HoverAlpha, bLit ? 1.f : 0.f, InDeltaTime, 18.f);
	const bool bSlides = Kind == EHitmenMenuButtonKind::Primary || Kind == EHitmenMenuButtonKind::Secondary || Kind == EHitmenMenuButtonKind::Back;
	const float Slide = bSlides ? 12.f * HoverAlpha : 0.f;
	if (LabelText)
	{
		LabelText->SetRenderTranslation(FVector2D(Slide, 0.f));
	}
	if (SubtitleText)
	{
		SubtitleText->SetRenderTranslation(FVector2D(Slide, 0.f));
	}
}

int32 UHitmenMenuButton::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	if (Kind == EHitmenMenuButtonKind::Block || Kind == EHitmenMenuButtonKind::Dashed || Kind == EHitmenMenuButtonKind::Chip)
	{
		const float Dim = bEnabledState ? 1.f : 0.45f;
		if (Kind == EHitmenMenuButtonKind::Dashed)
		{
			// Dashed outline over a faint fill.
			HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, FMath::Lerp(FLinearColor(0.f, 0.f, 0.f, 0.35f), FLinearColor(0.1f, 0.1f, 0.11f, 0.85f), HoverAlpha));
			const FLinearColor DashColor = FMath::Lerp(FLinearColor(1.f, 1.f, 1.f, 0.55f * Dim), FLinearColor(1.f, 1.f, 1.f, 0.95f * Dim), HoverAlpha);
			const float Dash = 10.f;
			const float GapLength = 7.f;
			auto DashedEdge = [&](FVector2D From, FVector2D To)
			{
				const float Length = FVector2D::Distance(From, To);
				const FVector2D Dir = (To - From) / FMath::Max(1.f, Length);
				for (float At = 0.f; At < Length; At += Dash + GapLength)
				{
					HitmenUI::PaintLine(OutDrawElements, LayerId + 1, AllottedGeometry, From + Dir * At, From + Dir * FMath::Min(Length, At + Dash), DashColor, 2.f);
				}
			};
			DashedEdge(FVector2D(1.f, 1.f), FVector2D(Size.X - 1.f, 1.f));
			DashedEdge(FVector2D(Size.X - 1.f, 1.f), Size - FVector2D(1.f, 1.f));
			DashedEdge(Size - FVector2D(1.f, 1.f), FVector2D(1.f, Size.Y - 1.f));
			DashedEdge(FVector2D(1.f, Size.Y - 1.f), FVector2D(1.f, 1.f));
		}
		else
		{
			const bool bChip = Kind == EHitmenMenuButtonKind::Chip;
			const FLinearColor Base = bChip ? FLinearColor(0.16f, 0.17f, 0.19f, 0.95f) : FLinearColor(0.055f, 0.058f, 0.068f, 0.9f);
			const FLinearColor Lit = bChip ? FLinearColor(0.32f, 0.33f, 0.36f, 1.f) : FLinearColor(0.16f, 0.165f, 0.18f, 0.95f);
			HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, FMath::Lerp(Base, Lit, HoverAlpha) * FLinearColor(1.f, 1.f, 1.f, Dim));
			HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, Size, FLinearColor(1.f, 1.f, 1.f, (0.18f + 0.5f * HoverAlpha) * Dim), 1.f);
		}
		// Red edge on the left when lit.
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, FVector2D(Kind == EHitmenMenuButtonKind::Chip ? 3.f : 5.f, Size.Y * HoverAlpha), HitmenUI::Accent());
		return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
	}
	if (Kind == EHitmenMenuButtonKind::Accent)
	{
		const FLinearColor Fill = bEnabledState ? FMath::Lerp(HitmenUI::Accent(), HitmenUI::Accent() * 1.3f, HoverAlpha) : FLinearColor(0.2f, 0.2f, 0.22f, 0.8f);
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, Fill);
	}
	if (HoverAlpha > 0.01f)
	{
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size,
			FLinearColor(1.f, 1.f, 1.f, 0.14f * HoverAlpha), FLinearColor(1.f, 1.f, 1.f, 0.f), true);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(0.f, 0.f), FVector2D(4.f, Size.Y * HoverAlpha),
			Kind == EHitmenMenuButtonKind::Accent ? HitmenUI::Text() : HitmenUI::Accent());
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Arrow button
// =============================================================================================

void UHitmenArrowButton::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenArrowButton::Setup(bool bInPointsRight, float InSize)
{
	bPointsRight = bInPointsRight;
	Size = InSize;
}

void UHitmenArrowButton::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HoverAlpha = FMath::FInterpTo(HoverAlpha, bHovered ? 1.f : 0.f, InDeltaTime, 18.f);
	PressAlpha = FMath::FInterpTo(PressAlpha, 0.f, InDeltaTime, 12.f);
}

FReply UHitmenArrowButton::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (InMouseEvent.GetEffectingButton() == EKeys::LeftMouseButton)
	{
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		PressAlpha = 1.f;
		OnClicked.Broadcast();
		return FReply::Handled();
	}
	return FReply::Unhandled();
}

FReply UHitmenArrowButton::NativeOnMouseButtonDoubleClick(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	// Slate turns the second of two fast clicks into a double click: it is one more step.
	return NativeOnMouseButtonDown(InGeometry, InMouseEvent);
}

void UHitmenArrowButton::NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseEnter(InGeometry, InMouseEvent);
	bHovered = true;
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
}

void UHitmenArrowButton::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	bHovered = false;
}

int32 UHitmenArrowButton::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Local = AllottedGeometry.GetLocalSize();
	const float Side = FMath::Min(Local.X, Local.Y);
	const FVector2D Origin = (Local - FVector2D(Side)) * 0.5f;
	// Square block: dark, lights up on hover, flashes on press.
	const FLinearColor Fill = FMath::Lerp(FMath::Lerp(FLinearColor(0.04f, 0.042f, 0.05f, 0.85f), FLinearColor(0.16f, 0.165f, 0.18f, 0.95f), HoverAlpha), HitmenUI::Accent(), PressAlpha * 0.8f);
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, Origin, FVector2D(Side), Fill);
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, Origin, FVector2D(Side), FLinearColor(1.f, 1.f, 1.f, 0.22f + 0.5f * HoverAlpha), 1.f);
	// A real arrow: a shaft and a head.
	const float Dir = bPointsRight ? 1.f : -1.f;
	const FVector2D C = Origin + FVector2D(Side) * 0.5f + FVector2D(Dir * 2.f * HoverAlpha, 0.f);
	const float HalfShaft = Side * 0.24f;
	const float Head = Side * 0.17f;
	const float Thickness = FMath::Max(2.f, Side * 0.065f);
	const FVector2D Tip = C + FVector2D(Dir * HalfShaft, 0.f);
	HitmenUI::PaintLine(OutDrawElements, LayerId + 2, AllottedGeometry, C - FVector2D(Dir * HalfShaft, 0.f), Tip - FVector2D(Dir * Thickness * 0.5f, 0.f), HitmenUI::Text(), Thickness);
	HitmenUI::PaintLines(OutDrawElements, LayerId + 2, AllottedGeometry, { Tip + FVector2D(-Dir * Head, -Head), Tip, Tip + FVector2D(-Dir * Head, Head) }, HitmenUI::Text(), Thickness);
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Toggle
// =============================================================================================

void UHitmenToggle::Setup(const FText& InLabel, bool bInChecked, int32 InFontSize)
{
	Label = InLabel;
	bChecked = bInChecked;
	FontSize = InFontSize;
	SetVisibility(ESlateVisibility::Visible);
	// Painted widget: a sized root gives it a real desired size in boxes.
	if (WidgetTree)
	{
		USizeBox* Box = Cast<USizeBox>(WidgetTree->RootWidget);
		if (!Box)
		{
			Box = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
			WidgetTree->RootWidget = Box;
		}
		const FVector2D Desired = GetDesiredSizeForPaint();
		Box->SetWidthOverride(Desired.X + 4.f);
		Box->SetHeightOverride(Desired.Y + 4.f);
	}
}

FVector2D UHitmenToggle::GetDesiredSizeForPaint() const
{
	const FVector2D Text = HitmenUI::MeasureText(Label.ToString().ToUpper(), HitmenUI::SpacedFont(FontSize, TEXT("SemiBold"), 60));
	return FVector2D(FontSize * 1.3f + 12.f + Text.X, FMath::Max(Text.Y, FontSize * 1.3f));
}

void UHitmenToggle::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HoverAlpha = FMath::FInterpTo(HoverAlpha, bHovered && bInteractable ? 1.f : 0.f, InDeltaTime, 14.f);
}

FReply UHitmenToggle::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (InMouseEvent.GetEffectingButton() != EKeys::LeftMouseButton || !bInteractable)
	{
		return FReply::Unhandled();
	}
	bChecked = !bChecked;
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
	OnToggled.Broadcast(bChecked);
	return FReply::Handled();
}

void UHitmenToggle::NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseEnter(InGeometry, InMouseEvent);
	bHovered = true;
}

void UHitmenToggle::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	bHovered = false;
}

int32 UHitmenToggle::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Local = AllottedGeometry.GetLocalSize();
	const float Box = FontSize * 1.1f;
	const FVector2D BoxPos(0.f, (Local.Y - Box) * 0.5f);
	const float Alpha = bInteractable ? 1.f : 0.45f;
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, BoxPos, FVector2D(Box), FLinearColor(0.f, 0.f, 0.f, 0.5f));
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, BoxPos, FVector2D(Box), FMath::Lerp(FLinearColor(1, 1, 1, 0.5f * Alpha), HitmenUI::Accent(), HoverAlpha), 1.5f);
	if (bChecked)
	{
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, BoxPos + FVector2D(Box * 0.22f), FVector2D(Box * 0.56f), HitmenUI::Accent() * FLinearColor(1, 1, 1, Alpha));
	}
	const FSlateFontInfo LabelFont = HitmenUI::SpacedFont(FontSize, TEXT("SemiBold"), 60);
	const FString Text = Label.ToString().ToUpper();
	const FVector2D TextSize = HitmenUI::MeasureText(Text, LabelFont);
	HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Text, FVector2D(Box + 12.f, (Local.Y - TextSize.Y) * 0.5f), LabelFont, HitmenUI::Text() * FLinearColor(1, 1, 1, Alpha));
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Card tile
// =============================================================================================

void UHitmenCardTile::Setup(const FText& InCategory, const FText& InTitle, const FText& InKey, float InSize)
{
	Category = InCategory;
	Title = InTitle;
	Key = InKey;
	Size = InSize;
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenCardTile::SetImage(UTexture2D* Texture)
{
	bHasImage = Texture != nullptr;
	if (Texture)
	{
		ImageBrush.SetResourceObject(Texture);
		ImageBrush.ImageSize = FVector2D(Texture->GetSizeX(), Texture->GetSizeY());
	}
}

void UHitmenCardTile::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HoverAlpha = FMath::FInterpTo(HoverAlpha, bHovered || bSelected ? 1.f : 0.f, InDeltaTime, 12.f);
}

FReply UHitmenCardTile::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (InMouseEvent.GetEffectingButton() == EKeys::LeftMouseButton)
	{
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		OnClicked.Broadcast();
		return FReply::Handled();
	}
	return FReply::Unhandled();
}

FReply UHitmenCardTile::NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	OnWheel.Broadcast(InMouseEvent.GetWheelDelta() > 0.f ? -1 : 1);
	return FReply::Handled();
}

void UHitmenCardTile::NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseEnter(InGeometry, InMouseEvent);
	bHovered = true;
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
	OnHovered.Broadcast(true);
}

void UHitmenCardTile::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	bHovered = false;
	OnHovered.Broadcast(false);
}

int32 UHitmenCardTile::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Local = AllottedGeometry.GetLocalSize();
	const FVector2D Lift(0.f, -6.f * HoverAlpha);
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, Lift, Local, FLinearColor(0.07f, 0.075f, 0.085f, 0.92f), FLinearColor(0.02f, 0.02f, 0.025f, 0.95f), false);
	if (bHasImage)
	{
		HitmenUI::PaintImage(OutDrawElements, LayerId + 1, AllottedGeometry, &ImageBrush, Lift + FVector2D(10.f, 30.f), Local - FVector2D(20.f, 70.f));
	}
	else
	{
		// No icon yet: the initials, big and faint.
		FString Initials;
		TArray<FString> Words;
		Title.ToString().ParseIntoArrayWS(Words);
		for (int32 Word = 0; Word < Words.Num() && Initials.Len() < 2; ++Word)
		{
			Initials.AppendChar(FChar::ToUpper(Words[Word][0]));
		}
		const FSlateFontInfo GlyphFont = HitmenUI::Font(FMath::RoundToInt(Local.Y * 0.32f), TEXT("Bold"));
		const FVector2D GlyphSize = HitmenUI::MeasureText(Initials, GlyphFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Initials, Lift + FVector2D((Local.X - GlyphSize.X) * 0.5f, Local.Y * 0.26f), GlyphFont, FLinearColor(1.f, 1.f, 1.f, 0.12f + 0.1f * HoverAlpha));
	}
	const FSlateFontInfo SmallFont = HitmenUI::SpacedFont(13, TEXT("Medium"), 150);
	HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Category.ToString().ToUpper(), Lift + FVector2D(12.f, 10.f), SmallFont, HitmenUI::TextDim());
	if (!Key.IsEmpty())
	{
		HitmenUI::PaintKey(OutDrawElements, LayerId + 2, AllottedGeometry, Key.ToString(), Lift + FVector2D(Local.X - 34.f, 8.f), 13);
	}
	const FSlateFontInfo TitleFont = HitmenUI::SpacedFont(19, TEXT("SemiBold"), 40);
	const FString TitleText = Title.ToString().ToUpper();
	HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, TitleText, Lift + FVector2D(12.f, Local.Y - 34.f), TitleFont, HitmenUI::Text());
	// Red bar under the card when hovered / selected; hairline frame otherwise.
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 3, AllottedGeometry, Lift, Local, FLinearColor(1.f, 1.f, 1.f, 0.12f + 0.3f * HoverAlpha), 1.f);
	HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, Lift + FVector2D(0.f, Local.Y - 4.f), FVector2D(Local.X * HoverAlpha, 4.f), HitmenUI::Accent());
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Choice carousel
// =============================================================================================

void UHitmenChoiceCarousel::SetChoices(const TArray<FText>& InChoices, int32 InSelected)
{
	Choices = InChoices;
	Selected = InSelected;
	SetVisibility(ESlateVisibility::Visible);
}

FVector2D UHitmenChoiceCarousel::GetStripSize() const
{
	return FVector2D(FMath::Max(1, Choices.Num()) * (ChoiceWidth + Gap) - Gap + 16.f, ChoiceHeight + 16.f);
}

int32 UHitmenChoiceCarousel::ChoiceAt(const FGeometry& Geometry, const FVector2D& ScreenPosition) const
{
	const FVector2D Local = Geometry.AbsoluteToLocal(ScreenPosition) - FVector2D(8.f);
	if (Local.Y < 0.f || Local.Y > ChoiceHeight || Local.X < 0.f)
	{
		return INDEX_NONE;
	}
	const int32 Choice = FMath::FloorToInt(Local.X / (ChoiceWidth + Gap));
	return Choices.IsValidIndex(Choice) ? Choice : INDEX_NONE;
}

FReply UHitmenChoiceCarousel::NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	const int32 NewHovered = ChoiceAt(InGeometry, InMouseEvent.GetScreenSpacePosition());
	if (NewHovered != Hovered && NewHovered != INDEX_NONE)
	{
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
	}
	Hovered = NewHovered;
	return FReply::Handled();
}

FReply UHitmenChoiceCarousel::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
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

FReply UHitmenChoiceCarousel::NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (Choices.Num() > 0)
	{
		Selected = (Selected + (InMouseEvent.GetWheelDelta() > 0.f ? -1 : 1) + Choices.Num()) % Choices.Num();
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
		OnChosen.Broadcast(Selected);
	}
	return FReply::Handled();
}

void UHitmenChoiceCarousel::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	Hovered = INDEX_NONE;
}

int32 UHitmenChoiceCarousel::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Strip = GetStripSize();
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Strip, FLinearColor(0.f, 0.f, 0.f, 0.82f));
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(0.f, Strip.Y - 2.f), FVector2D(Strip.X, 2.f), HitmenUI::Accent());
	const FSlateFontInfo NameFont = HitmenUI::SpacedFont(16, TEXT("SemiBold"), 40);
	for (int32 Choice = 0; Choice < Choices.Num(); ++Choice)
	{
		const FVector2D Position(8.f + Choice * (ChoiceWidth + Gap), 8.f);
		const bool bIsSelected = Choice == Selected;
		const bool bIsHovered = Choice == Hovered;
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, Position, FVector2D(ChoiceWidth, ChoiceHeight),
			bIsSelected ? FLinearColor(1.f, 1.f, 1.f, 0.16f) : FLinearColor(1.f, 1.f, 1.f, bIsHovered ? 0.1f : 0.04f));
		if (bIsSelected)
		{
			HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, Position, FVector2D(4.f, ChoiceHeight), HitmenUI::Accent());
		}
		FString Name = Choices[Choice].ToString().ToUpper();
		const FVector2D NameSize = HitmenUI::MeasureText(Name, NameFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Name, Position + FVector2D(FMath::Max(10.f, (ChoiceWidth - NameSize.X) * 0.5f), (ChoiceHeight - NameSize.Y) * 0.5f),
			NameFont, bIsSelected || bIsHovered ? HitmenUI::Text() : HitmenUI::TextDim());
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Backdrop
// =============================================================================================

int32 UHitmenBackdrop::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const FLinearColor Dark(0.008f, 0.009f, 0.012f, Strength);
	const FLinearColor Clear(0.008f, 0.009f, 0.012f, 0.f);
	if (LeftCoverage > 0.f)
	{
		const float Solid = Size.X * LeftCoverage * 0.35f;
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, FVector2D(Solid, Size.Y), Dark);
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(Solid, 0.f), FVector2D(Size.X * LeftCoverage - Solid, Size.Y), Dark, Clear, true);
	}
	if (RightCoverage > 0.f)
	{
		const float Width = Size.X * RightCoverage;
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(Size.X - Width, 0.f), FVector2D(Width, Size.Y), Clear, Dark, true);
	}
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(0.f, Size.Y * 0.72f), FVector2D(Size.X, Size.Y * 0.28f), Clear, FLinearColor(0.f, 0.f, 0.f, 0.75f * Strength), false);
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 1, InWidgetStyle, bParentEnabled);
}
