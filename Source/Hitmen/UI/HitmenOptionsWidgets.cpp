// Hitmen — options screen

#include "UI/HitmenOptionsWidgets.h"
#include "UI/HitmenUIFactory.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Core/HitmenUserSettings.h"
#include "Data/HitmenSoundSet.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Border.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/VerticalBoxSlot.h"
#include "TimerManager.h"
#include "Engine/World.h"
#include "Components/ScrollBox.h"
#include "Components/SizeBox.h"
#include "Components/Spacer.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "EnhancedInputSubsystems.h"
#include "UserSettings/EnhancedInputUserSettings.h"
#include "Engine/LocalPlayer.h"
#include "GameFramework/PlayerController.h"
#include "Kismet/KismetSystemLibrary.h"

#define LOCTEXT_NAMESPACE "HitmenOptions"

namespace
{
	UEnhancedInputUserSettings* OptionsInputSettings(const APlayerController* PC)
	{
		const ULocalPlayer* LP = PC ? PC->GetLocalPlayer() : nullptr;
		const UEnhancedInputLocalPlayerSubsystem* Subsystem = LP ? LP->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr;
		return Subsystem ? Subsystem->GetUserSettings() : nullptr;
	}

	const FLinearColor RowText(0.95f, 0.95f, 0.95f, 1.f);

	/** Order of the Controls sections (categories come from each mapping's DisplayCategory). */
	const TCHAR* const CategoryOrder[] = { TEXT("Movement"), TEXT("Combat"), TEXT("Abilities"), TEXT("Interaction"), TEXT("Spectator"), TEXT("Menus") };

	/** Controls rows in play order; mappings not listed follow alphabetically. */
	const TCHAR* const MappingOrder[] = {
		TEXT("MoveForward"), TEXT("MoveBackward"), TEXT("MoveLeft"), TEXT("MoveRight"), TEXT("Jump"), TEXT("Crouch"), TEXT("Sprint"),
		TEXT("Fire"), TEXT("Aim"), TEXT("SwitchSlot"), TEXT("ManipulationAbility"), TEXT("MoveAbility"), TEXT("Sightroom"),
		TEXT("Interact"), TEXT("EmoteWheel"), TEXT("SpectatorMode"), TEXT("SpectatorUp"), TEXT("SpectatorUpAlt"), TEXT("SpectatorDown"),
		TEXT("SpectatorDownAlt"), TEXT("SpectatorFast"), TEXT("Pause"), TEXT("PauseAlt"), TEXT("Confirm") };

	int32 MappingRank(const FName& Name)
	{
		for (int32 Index = 0; Index < UE_ARRAY_COUNT(MappingOrder); ++Index)
		{
			if (Name == FName(MappingOrder[Index]))
			{
				return Index;
			}
		}
		return UE_ARRAY_COUNT(MappingOrder);
	}

	TArray<FText> QualityNames()
	{
		return { LOCTEXT("Low", "Low"), LOCTEXT("Medium", "Medium"), LOCTEXT("High", "High"), LOCTEXT("Epic", "Epic") };
	}

	TArray<FText> OffOn()
	{
		return { LOCTEXT("Off", "Off"), LOCTEXT("On", "On") };
	}

	FText Percent(float Value) { return FText::FromString(FString::Printf(TEXT("%d %%"), FMath::RoundToInt(Value * 100.f))); }
}

// =============================================================================================
// Row
// =============================================================================================

void UHitmenOptionRow::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	SetIsFocusable(true);
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenOptionRow::SetupHeader(const FText& InLabel)
{
	Kind = EHitmenOptionRowKind::Header;
	Label = InLabel;
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenOptionRow::SetupChoice(const FText& InLabel, const FText& InDescription, const TArray<FText>& InChoices, int32 InIndex, TFunction<void(int32)> InOnChoice)
{
	Kind = EHitmenOptionRowKind::Choice;
	Label = InLabel;
	Description = InDescription;
	Choices = InChoices;
	ChoiceIndex = FMath::Clamp(InIndex, 0, FMath::Max(0, Choices.Num() - 1));
	OnChoice = MoveTemp(InOnChoice);
}

void UHitmenOptionRow::SetupSlider(const FText& InLabel, const FText& InDescription, float InMin, float InMax, float InStep, float InValue,
	TFunction<FText(float)> InFormat, TFunction<void(float)> InOnValue)
{
	Kind = EHitmenOptionRowKind::Slider;
	Label = InLabel;
	Description = InDescription;
	SliderMin = InMin;
	SliderMax = FMath::Max(InMin + KINDA_SMALL_NUMBER, InMax);
	SliderStep = InStep;
	SliderValue = FMath::Clamp(InValue, SliderMin, SliderMax);
	FormatValue = MoveTemp(InFormat);
	OnValue = MoveTemp(InOnValue);
}

void UHitmenOptionRow::SetupKey(const FText& InLabel, const FText& InDescription, FName InMappingName, const FKey& InKey)
{
	Kind = EHitmenOptionRowKind::Key;
	Label = InLabel;
	Description = InDescription;
	MappingName = InMappingName;
	CurrentKey = InKey;
}

void UHitmenOptionRow::SetupAction(const FText& InLabel, const FText& InDescription, const FText& InButtonText, TFunction<void()> InOnAction)
{
	Kind = EHitmenOptionRowKind::Action;
	Label = InLabel;
	Description = InDescription;
	ActionText = InButtonText;
	OnAction = MoveTemp(InOnAction);
}

FText UHitmenOptionRow::GetDescription() const
{
	if (!ConflictWith.IsEmpty())
	{
		return FText::Format(LOCTEXT("ConflictDesc", "{0}\n\nThis key is also used by {1}."), Description, ConflictWith);
	}
	return Description;
}

void UHitmenOptionRow::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HoverAlpha = FMath::FInterpTo(HoverAlpha, (bHovered || bDragging || bListening) ? 1.f : 0.f, InDeltaTime, 16.f);
}

int32 UHitmenOptionRow::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	if (Kind == EHitmenOptionRowKind::Header)
	{
		// Section title: spaced dim capitals with a hairline running to the right edge.
		const FSlateFontInfo HeaderFont = HitmenUI::SpacedFont(15, TEXT("SemiBold"), 240);
		const FString Text = Label.ToString().ToUpper();
		const FVector2D TextSize = HitmenUI::MeasureText(Text, HeaderFont);
		const float Y = Size.Y - TextSize.Y - 6.f;
		HitmenUI::PaintText(OutDrawElements, LayerId, AllottedGeometry, Text, FVector2D(24.f, Y), HeaderFont, HitmenUI::TextDim());
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(36.f + TextSize.X, Y + TextSize.Y * 0.55f), FVector2D(FMath::Max(0.f, Size.X - 60.f - TextSize.X), 1.f), HitmenUI::Hairline());
		return LayerId + 1;
	}

	// Hover: a soft band and the red bar on the left, like the menu buttons.
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size,
		FLinearColor(1.f, 1.f, 1.f, 0.085f * HoverAlpha), FLinearColor(1.f, 1.f, 1.f, 0.02f * HoverAlpha), true);
	HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(0.f, 6.f), FVector2D(4.f * HoverAlpha, Size.Y - 12.f), HitmenUI::Accent());
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(16.f, Size.Y - 1.f), FVector2D(Size.X - 32.f, 1.f), FLinearColor(1.f, 1.f, 1.f, 0.06f));

	const FSlateFontInfo LabelFont = HitmenUI::Font(21, TEXT("SemiBold"));
	const FVector2D LabelSize = HitmenUI::MeasureText(Label.ToString(), LabelFont);
	const bool bConflict = !ConflictWith.IsEmpty();
	HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Label.ToString(), FVector2D(24.f + 6.f * HoverAlpha, (Size.Y - LabelSize.Y) * 0.5f), LabelFont,
		bConflict ? HitmenUI::Accent() : RowText);

	const float ValueW = GetValueWidth();
	const float X0 = Size.X - ValueW - 18.f;
	const float CY = Size.Y * 0.5f;
	const FSlateFontInfo ValueFont = HitmenUI::Font(20, TEXT("Medium"));
	switch (Kind)
	{
	case EHitmenOptionRowKind::Choice:
	{
		const FString Value = Choices.IsValidIndex(ChoiceIndex) ? Choices[ChoiceIndex].ToString().ToUpper() : FString();
		const FVector2D ValueSize = HitmenUI::MeasureText(Value, ValueFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Value, FVector2D(X0 + (ValueW - ValueSize.X) * 0.5f, CY - ValueSize.Y * 0.5f), ValueFont, RowText);
		// Arrows at both ends, brighter under the mouse.
		const float A = 7.f;
		for (int32 Side = -1; Side <= 1; Side += 2)
		{
			const float AX = Side < 0 ? X0 + 14.f : X0 + ValueW - 14.f;
			const float Alpha = HoveredArrow == Side ? 1.f : 0.35f + 0.3f * HoverAlpha;
			const FLinearColor Color(1.f, 1.f, 1.f, Alpha);
			HitmenUI::PaintTriangle(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(AX + Side * A, CY), FVector2D(AX - Side * A, CY - A * 1.2f), FVector2D(AX - Side * A, CY + A * 1.2f), Color);
		}
		break;
	}
	case EHitmenOptionRowKind::Slider:
	{
		const float TextRoom = 92.f;
		const float TrackX = X0 + 6.f;
		const float TrackW = ValueW - TextRoom - 12.f;
		const float Alpha = (SliderValue - SliderMin) / (SliderMax - SliderMin);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(TrackX, CY - 2.f), FVector2D(TrackW, 4.f), FLinearColor(1.f, 1.f, 1.f, 0.16f));
		HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, FVector2D(TrackX, CY - 2.f), FVector2D(TrackW * Alpha, 4.f), FLinearColor(1.f, 1.f, 1.f, 0.85f));
		const float Knob = 14.f + 4.f * HoverAlpha;
		HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, FVector2D(TrackX + TrackW * Alpha - Knob * 0.5f, CY - Knob * 0.5f), FVector2D(Knob), bDragging ? HitmenUI::Accent() : RowText);
		const FString Value = FormatValue ? FormatValue(SliderValue).ToString() : FString::Printf(TEXT("%.2f"), SliderValue);
		const FVector2D ValueSize = HitmenUI::MeasureText(Value, ValueFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Value, FVector2D(X0 + ValueW - ValueSize.X - 4.f, CY - ValueSize.Y * 0.5f), ValueFont, RowText);
		break;
	}
	case EHitmenOptionRowKind::Key:
	{
		if (bListening)
		{
			const FString Prompt = LOCTEXT("PressKey", "PRESS A KEY  (ESC CANCELS)").ToString();
			const FSlateFontInfo PromptFont = HitmenUI::Font(17, TEXT("SemiBoldItalic"));
			const FVector2D PromptSize = HitmenUI::MeasureText(Prompt, PromptFont);
			const float Blink = 0.55f + 0.45f * FMath::Sin(GetWorld() ? GetWorld()->GetRealTimeSeconds() * 8.f : 0.f);
			HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Prompt, FVector2D(X0 + (ValueW - PromptSize.X) * 0.5f, CY - PromptSize.Y * 0.5f), PromptFont,
				FLinearColor(1.f, 1.f, 1.f, Blink));
			break;
		}
		const FString KeyName = UHitmenUIEventSubsystem::ShortKeyName(CurrentKey);
		const FSlateFontInfo KeyFont = HitmenUI::Font(18, TEXT("SemiBold"));
		const FVector2D TextSize = HitmenUI::MeasureText(KeyName, KeyFont);
		const FVector2D Cap(FMath::Max(40.f, TextSize.X + 22.f), 34.f);
		const FVector2D CapPos(X0 + (ValueW - Cap.X) * 0.5f, CY - Cap.Y * 0.5f);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, CapPos, Cap, bConflict ? HitmenUI::Accent() : FLinearColor(1.f, 1.f, 1.f, 0.9f));
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, KeyName, CapPos + (Cap - TextSize) * 0.5f, KeyFont, bConflict ? RowText : HitmenUI::Ink());
		break;
	}
	case EHitmenOptionRowKind::Action:
	{
		const FString Text = ActionText.ToString().ToUpper();
		const FSlateFontInfo ActionFont = HitmenUI::SpacedFont(17, TEXT("SemiBold"), 120);
		const FVector2D TextSize = HitmenUI::MeasureText(Text, ActionFont);
		const FVector2D Box(FMath::Max(160.f, TextSize.X + 40.f), 36.f);
		const FVector2D BoxPos(X0 + (ValueW - Box.X) * 0.5f, CY - Box.Y * 0.5f);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, BoxPos, Box, FLinearColor(1.f, 1.f, 1.f, 0.08f + 0.85f * HoverAlpha));
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 2, AllottedGeometry, BoxPos, Box, FLinearColor(1.f, 1.f, 1.f, 0.6f), 1.f);
		HitmenUI::PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, Text, BoxPos + (Box - TextSize) * 0.5f, ActionFont, FLinearColor::LerpUsingHSV(RowText, HitmenUI::Ink(), HoverAlpha));
		break;
	}
	default:
		break;
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

void UHitmenOptionRow::StepChoice(int32 Direction)
{
	if (Kind == EHitmenOptionRowKind::Choice && Choices.Num() > 0)
	{
		ChoiceIndex = (ChoiceIndex + Direction + Choices.Num()) % Choices.Num();
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		if (OnChoice)
		{
			OnChoice(ChoiceIndex);
		}
	}
	else if (Kind == EHitmenOptionRowKind::Slider)
	{
		const float Step = SliderStep > 0.f ? SliderStep : (SliderMax - SliderMin) * 0.05f;
		SliderValue = FMath::Clamp(SliderValue + Direction * Step, SliderMin, SliderMax);
		if (OnValue)
		{
			OnValue(SliderValue);
		}
	}
}

void UHitmenOptionRow::SetSliderFromLocalX(float LocalX, float RowWidth)
{
	const float ValueW = GetValueWidth();
	const float TrackX = RowWidth - ValueW - 18.f + 6.f;
	const float TrackW = ValueW - 92.f - 12.f;
	float Value = FMath::Lerp(SliderMin, SliderMax, FMath::Clamp((LocalX - TrackX) / TrackW, 0.f, 1.f));
	if (SliderStep > 0.f)
	{
		Value = SliderMin + FMath::RoundToFloat((Value - SliderMin) / SliderStep) * SliderStep;
	}
	Value = FMath::Clamp(Value, SliderMin, SliderMax);
	if (!FMath::IsNearlyEqual(Value, SliderValue))
	{
		SliderValue = Value;
		if (OnValue)
		{
			OnValue(SliderValue);
		}
	}
}

void UHitmenOptionRow::ChooseKey(const FKey& Key)
{
	bListening = false;
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIConfirm);
	if (OnKeyChosen)
	{
		OnKeyChosen(this, Key);
	}
}

FReply UHitmenOptionRow::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	const FKey Button = InMouseEvent.GetEffectingButton();
	if (bListening)
	{
		ChooseKey(Button);
		return FReply::Handled();
	}
	if (Button != EKeys::LeftMouseButton)
	{
		return FReply::Unhandled();
	}
	const FVector2D Local = InGeometry.AbsoluteToLocal(InMouseEvent.GetScreenSpacePosition());
	const float Width = InGeometry.GetLocalSize().X;
	const float X0 = Width - GetValueWidth() - 18.f;
	switch (Kind)
	{
	case EHitmenOptionRowKind::Choice:
		// Left half of the value steps back; anywhere else steps forward (the label too).
		StepChoice(Local.X >= X0 && Local.X < X0 + GetValueWidth() * 0.5f ? -1 : 1);
		return FReply::Handled();
	case EHitmenOptionRowKind::Slider:
		if (Local.X >= X0)
		{
			bDragging = true;
			SetSliderFromLocalX(Local.X, Width);
			return FReply::Handled().CaptureMouse(TakeWidget());
		}
		return FReply::Handled();
	case EHitmenOptionRowKind::Key:
		bListening = true;
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		return FReply::Handled().SetUserFocus(TakeWidget(), EFocusCause::Mouse);
	case EHitmenOptionRowKind::Action:
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIConfirm);
		if (OnAction)
		{
			OnAction();
		}
		return FReply::Handled();
	default:
		return FReply::Unhandled();
	}
}

FReply UHitmenOptionRow::NativeOnMouseButtonUp(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (bDragging)
	{
		bDragging = false;
		return FReply::Handled().ReleaseMouseCapture();
	}
	return Super::NativeOnMouseButtonUp(InGeometry, InMouseEvent);
}

FReply UHitmenOptionRow::NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	const FVector2D Local = InGeometry.AbsoluteToLocal(InMouseEvent.GetScreenSpacePosition());
	const float Width = InGeometry.GetLocalSize().X;
	if (bDragging)
	{
		SetSliderFromLocalX(Local.X, Width);
		return FReply::Handled();
	}
	if (Kind == EHitmenOptionRowKind::Choice)
	{
		const float X0 = Width - GetValueWidth() - 18.f;
		HoveredArrow = Local.X < X0 ? 0 : (Local.X < X0 + GetValueWidth() * 0.5f ? -1 : 1);
	}
	return Super::NativeOnMouseMove(InGeometry, InMouseEvent);
}

FReply UHitmenOptionRow::NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (bListening)
	{
		ChooseKey(InMouseEvent.GetWheelDelta() > 0.f ? EKeys::MouseScrollUp : EKeys::MouseScrollDown);
		return FReply::Handled();
	}
	// The wheel scrolls the list; it only edits a value while its row is being dragged.
	return FReply::Unhandled();
}

FReply UHitmenOptionRow::NativeOnKeyDown(const FGeometry& InGeometry, const FKeyEvent& InKeyEvent)
{
	if (!bListening)
	{
		return Super::NativeOnKeyDown(InGeometry, InKeyEvent);
	}
	if (InKeyEvent.GetKey() == EKeys::Escape)
	{
		bListening = false;
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIBack);
		return FReply::Handled();
	}
	ChooseKey(InKeyEvent.GetKey());
	return FReply::Handled();
}

void UHitmenOptionRow::NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseEnter(InGeometry, InMouseEvent);
	bHovered = true;
	if (OnHovered)
	{
		OnHovered(this);
	}
}

void UHitmenOptionRow::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	bHovered = false;
	HoveredArrow = 0;
}

void UHitmenOptionRow::NativeOnFocusLost(const FFocusEvent& InFocusEvent)
{
	Super::NativeOnFocusLost(InFocusEvent);
	bListening = false;
}

// =============================================================================================
// Tab bar
// =============================================================================================

namespace
{
	FSlateFontInfo TabFont() { return HitmenUI::SpacedFont(22, TEXT("SemiBold"), 200); }
	constexpr float TabPadding = 26.f;
}

void UHitmenTabBar::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenTabBar::SetTabs(const TArray<FText>& InTabs, int32 InSelected)
{
	Tabs = InTabs;
	Selected = InSelected;
}

float UHitmenTabBar::GetTabWidth(int32 Tab) const
{
	return HitmenUI::MeasureText(Tabs[Tab].ToString().ToUpper(), TabFont()).X + TabPadding * 2.f;
}

int32 UHitmenTabBar::TabAt(const FGeometry& Geometry, const FVector2D& ScreenPosition) const
{
	const FVector2D Local = Geometry.AbsoluteToLocal(ScreenPosition);
	float X = 0.f;
	for (int32 Tab = 0; Tab < Tabs.Num(); ++Tab)
	{
		const float W = GetTabWidth(Tab);
		if (Local.X >= X && Local.X < X + W && Local.Y >= 0.f && Local.Y <= Geometry.GetLocalSize().Y)
		{
			return Tab;
		}
		X += W;
	}
	return INDEX_NONE;
}

int32 UHitmenTabBar::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(0.f, Size.Y - 1.f), FVector2D(Size.X, 1.f), HitmenUI::Hairline());
	float X = 0.f;
	for (int32 Tab = 0; Tab < Tabs.Num(); ++Tab)
	{
		const float W = GetTabWidth(Tab);
		const bool bSelected = Tab == Selected;
		const bool bHover = Tab == Hovered;
		if (bSelected)
		{
			HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(X, 0.f), FVector2D(W, Size.Y), FLinearColor(1.f, 1.f, 1.f, 0.f), FLinearColor(1.f, 1.f, 1.f, 0.1f), false);
			HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(X, Size.Y - 4.f), FVector2D(W, 4.f), HitmenUI::Accent());
		}
		const FString Text = Tabs[Tab].ToString().ToUpper();
		const FVector2D TextSize = HitmenUI::MeasureText(Text, TabFont());
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Text, FVector2D(X + TabPadding, (Size.Y - TextSize.Y) * 0.5f - 2.f), TabFont(),
			bSelected ? HitmenUI::Text() : FLinearColor(1.f, 1.f, 1.f, bHover ? 0.85f : 0.45f));
		X += W;
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

FReply UHitmenTabBar::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	const int32 Tab = TabAt(InGeometry, InMouseEvent.GetScreenSpacePosition());
	if (Tab == INDEX_NONE || InMouseEvent.GetEffectingButton() != EKeys::LeftMouseButton)
	{
		return FReply::Unhandled();
	}
	if (Tab != Selected)
	{
		Selected = Tab;
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		if (OnTabChosen)
		{
			OnTabChosen(Tab);
		}
	}
	return FReply::Handled();
}

FReply UHitmenTabBar::NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Hovered = TabAt(InGeometry, InMouseEvent.GetScreenSpacePosition());
	return Super::NativeOnMouseMove(InGeometry, InMouseEvent);
}

void UHitmenTabBar::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	Hovered = INDEX_NONE;
}

// =============================================================================================
// Key hint
// =============================================================================================

void UHitmenKeyHint::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenKeyHint::Setup(const FString& InKey, const FText& InLabel)
{
	Key = InKey;
	Label = InLabel;
}

int32 UHitmenKeyHint::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const FVector2D Cap = HitmenUI::PaintKey(OutDrawElements, LayerId, AllottedGeometry, Key, FVector2D(0.f, (Size.Y - 18.f * 1.55f) * 0.5f), 18, bHovered ? 1.f : 0.85f);
	const FSlateFontInfo LabelFont = HitmenUI::SpacedFont(19, TEXT("SemiBold"), 120);
	const FString Text = Label.ToString().ToUpper();
	const FVector2D TextSize = HitmenUI::MeasureText(Text, LabelFont);
	HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Text, FVector2D(Cap.X + 12.f, (Size.Y - TextSize.Y) * 0.5f), LabelFont,
		FLinearColor(1.f, 1.f, 1.f, bHovered ? 1.f : 0.7f));
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

FReply UHitmenKeyHint::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (InMouseEvent.GetEffectingButton() == EKeys::LeftMouseButton && OnClicked)
	{
		OnClicked();
		return FReply::Handled();
	}
	return FReply::Unhandled();
}

void UHitmenKeyHint::NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseEnter(InGeometry, InMouseEvent);
	bHovered = true;
}

void UHitmenKeyHint::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	bHovered = false;
}

// =============================================================================================
// Options
// =============================================================================================

void UHitmenOptionsWidget::BuildDefaultLayout()
{
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;
	SetIsFocusable(true);

	// The game (or the title) stays visible, darkened.
	UBorder* Dim = HitmenUI::MakePanel(WidgetTree, FLinearColor(0.f, 0.f, 0.f, 0.72f), FMargin(0.f));
	UCanvasPanelSlot* DimSlot = HitmenUI::AddToCanvas(Canvas, Dim, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	DimSlot->SetOffsets(FMargin(0.f));

	// Centred panel.
	PanelBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	PanelBox->SetWidthOverride(PanelWidth);
	PanelBox->SetHeightOverride(PanelHeight);
	UCanvasPanelSlot* PanelSlot = HitmenUI::AddToCanvas(Canvas, PanelBox, FAnchors(0.5f, 0.5f), FVector2D::ZeroVector, FVector2D(0.5f, 0.5f));
	PanelSlot->SetAutoSize(true);
	UBorder* Panel = HitmenUI::MakePanel(WidgetTree, FLinearColor(0.02f, 0.022f, 0.027f, 0.97f), FMargin(0.f));
	PanelBox->SetContent(Panel);
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Panel->SetContent(Column);

	// Title and tabs.
	UTextBlock* Title = HitmenUI::MakeText(WidgetTree, LOCTEXT("OptionsTitle", "OPTIONS"), 46, TEXT("Bold"));
	Title->SetFont(HitmenUI::SpacedFont(46, TEXT("Bold"), 80));
	HitmenUI::AddToVBox(Column, Title, FMargin(40.f, 28.f, 40.f, 6.f));
	TabBar = WidgetTree->ConstructWidget<UHitmenTabBar>(UHitmenTabBar::StaticClass());
	TabBar->SetTabs({ LOCTEXT("TabGameplay", "Gameplay"), LOCTEXT("TabControls", "Controls"), LOCTEXT("TabGraphics", "Graphics"), LOCTEXT("TabAudio", "Audio") }, 0);
	TabBar->OnTabChosen = [this](int32 Tab) { ShowTab(static_cast<EHitmenOptionsTab>(Tab)); };
	USizeBox* TabSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	TabSize->SetHeightOverride(56.f);
	TabSize->SetContent(TabBar);
	HitmenUI::AddToVBox(Column, TabSize, FMargin(40.f, 0.f, 40.f, 0.f));

	// Rows on the left, the description of the hovered row on the right.
	UHorizontalBox* Body = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UVerticalBoxSlot* BodySlot = Column->AddChildToVerticalBox(Body);
	BodySlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	BodySlot->SetPadding(FMargin(24.f, 14.f, 40.f, 0.f));
	RowScroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	RowBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	RowScroll->AddChild(RowBox);
	UHorizontalBoxSlot* ScrollSlot = Body->AddChildToHorizontalBox(RowScroll);
	ScrollSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	UVerticalBox* DescriptionColumn = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	USizeBox* DescriptionSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	DescriptionSize->SetWidthOverride(300.f);
	DescriptionSize->SetContent(DescriptionColumn);
	UHorizontalBoxSlot* DescriptionSlot = Body->AddChildToHorizontalBox(DescriptionSize);
	DescriptionSlot->SetPadding(FMargin(28.f, 8.f, 0.f, 0.f));
	DescriptionSlot->SetVerticalAlignment(VAlign_Top);
	UBorder* Accent = HitmenUI::MakePanel(WidgetTree, HitmenUI::Accent(), FMargin(0.f));
	USizeBox* AccentSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	AccentSize->SetWidthOverride(48.f);
	AccentSize->SetHeightOverride(4.f);
	AccentSize->SetContent(Accent);
	HitmenUI::AddToVBox(DescriptionColumn, AccentSize, FMargin(0.f, 0.f, 0.f, 12.f), false, HAlign_Left);
	DescriptionTitle = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 24, TEXT("SemiBold"));
	DescriptionTitle->SetAutoWrapText(true);
	HitmenUI::AddToVBox(DescriptionColumn, DescriptionTitle, FMargin(0.f, 0.f, 0.f, 10.f));
	DescriptionText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 18, TEXT("Regular"), FLinearColor(0.72f, 0.74f, 0.78f, 1.f));
	DescriptionText->SetAutoWrapText(true);
	HitmenUI::AddToVBox(DescriptionColumn, DescriptionText);

	// Key hints at the bottom right.
	UHorizontalBox* Footer = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	auto AddHint = [this, Footer](const FString& Key, const FText& Text, TFunction<void()> Handler)
	{
		UHitmenKeyHint* Hint = WidgetTree->ConstructWidget<UHitmenKeyHint>(UHitmenKeyHint::StaticClass());
		Hint->Setup(Key, Text);
		Hint->OnClicked = MoveTemp(Handler);
		USizeBox* HintSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		HintSize->SetHeightOverride(40.f);
		HintSize->SetWidthOverride(HitmenUI::MeasureText(Text.ToString().ToUpper(), HitmenUI::SpacedFont(19, TEXT("SemiBold"), 120)).X + 70.f);
		HintSize->SetContent(Hint);
		HitmenUI::AddToHBox(Footer, HintSize, FMargin(28.f, 0.f, 0.f, 0.f));
	};
	AddHint(TEXT("R"), LOCTEXT("Defaults", "Defaults"), [this]() { ResetToDefaults(); });
	AddHint(TEXT("ESC"), LOCTEXT("Back", "Back"), [this]() { Close(); });
	HitmenUI::AddToVBox(Column, Footer, FMargin(40.f, 12.f, 40.f, 22.f), false, HAlign_Right);
}

void UHitmenOptionsWidget::BindEvents()
{
	ShowTab(CurrentTab);
}

void UHitmenOptionsWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const bool bVisible = IsVisible();
	if (bVisible && !bWasVisible)
	{
		// Opened: values may have changed elsewhere (benchmark, rebinding in another screen).
		ShowTab(CurrentTab);
		SetKeyboardFocus();
	}
	bWasVisible = bVisible;
	// Keep the keyboard here (Esc / R) unless a key row is listening.
	if (bVisible && !HasAnyUserFocus() && !HasFocusedDescendants())
	{
		SetKeyboardFocus();
	}
}

FReply UHitmenOptionsWidget::NativeOnKeyDown(const FGeometry& InGeometry, const FKeyEvent& InKeyEvent)
{
	const FKey Key = InKeyEvent.GetKey();
	if (Key == EKeys::Escape)
	{
		Close();
		return FReply::Handled();
	}
	if (Key == EKeys::R && !InKeyEvent.IsRepeat())
	{
		ResetToDefaults();
		return FReply::Handled();
	}
	// Q / E switch tabs (like the shoulder buttons).
	if ((Key == EKeys::Q || Key == EKeys::E) && !InKeyEvent.IsRepeat())
	{
		const int32 Next = (static_cast<int32>(CurrentTab) + (Key == EKeys::E ? 1 : 3)) % 4;
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		ShowTab(static_cast<EHitmenOptionsTab>(Next));
		return FReply::Handled();
	}
	return Super::NativeOnKeyDown(InGeometry, InKeyEvent);
}

UHitmenOptionRow* UHitmenOptionsWidget::AddRow()
{
	UHitmenOptionRow* Row = WidgetTree->ConstructWidget<UHitmenOptionRow>(UHitmenOptionRow::StaticClass());
	Row->OnHovered = [this](UHitmenOptionRow* Hovered) { HandleRowHovered(Hovered); };
	Row->OnKeyChosen = [this](UHitmenOptionRow* Chosen, const FKey& Key) { HandleKeyChosen(Chosen, Key); };
	USizeBox* RowSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	RowSize->SetHeightOverride(UHitmenOptionRow::RowHeight);
	RowSize->SetContent(Row);
	HitmenUI::AddToVBox(RowBox, RowSize, FMargin(0.f), false, HAlign_Fill);
	Rows.Add(Row);
	return Row;
}

void UHitmenOptionsWidget::AddHeader(const FText& Text)
{
	UHitmenOptionRow* Row = WidgetTree->ConstructWidget<UHitmenOptionRow>(UHitmenOptionRow::StaticClass());
	Row->SetupHeader(Text);
	USizeBox* RowSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	RowSize->SetHeightOverride(Rows.IsEmpty() ? 34.f : 52.f);
	RowSize->SetContent(Row);
	HitmenUI::AddToVBox(RowBox, RowSize, FMargin(0.f), false, HAlign_Fill);
}

void UHitmenOptionsWidget::HandleRowHovered(UHitmenOptionRow* Row)
{
	if (Row && DescriptionTitle && DescriptionText)
	{
		DescriptionTitle->SetText(Row->GetLabel());
		DescriptionText->SetText(Row->GetDescription());
	}
}

void UHitmenOptionsWidget::ShowTab(EHitmenOptionsTab Tab)
{
	CurrentTab = Tab;
	if (TabBar)
	{
		TabBar->SetSelected(static_cast<int32>(Tab));
	}
	if (!RowBox)
	{
		return;
	}
	RowBox->ClearChildren();
	Rows.Reset();
	switch (Tab)
	{
	case EHitmenOptionsTab::Gameplay: BuildGameplay(); break;
	case EHitmenOptionsTab::Controls: BuildControls(); break;
	case EHitmenOptionsTab::Graphics: BuildGraphics(); break;
	case EHitmenOptionsTab::Audio: BuildAudio(); break;
	}
	if (RowScroll)
	{
		RowScroll->ScrollToStart();
	}
	// The first real row describes itself until the mouse picks another.
	for (UHitmenOptionRow* Row : Rows)
	{
		if (Row->GetKind() != EHitmenOptionRowKind::Header)
		{
			HandleRowHovered(Row);
			break;
		}
	}
}

void UHitmenOptionsWidget::BuildGameplay()
{
	UHitmenUserSettings* S = UHitmenUserSettings::GetHitmenUserSettings();
	if (!S)
	{
		return;
	}
	AddHeader(LOCTEXT("Mouse", "Mouse"));
	AddRow()->SetupSlider(LOCTEXT("Sensitivity", "Mouse sensitivity"), LOCTEXT("SensitivityDesc", "How fast the view turns with the mouse."),
		0.05f, 1.5f, 0.01f, S->MouseSensitivity, [](float V) { return FText::FromString(FString::Printf(TEXT("%.2f"), V)); },
		[S](float V) { S->MouseSensitivity = V; S->ApplyHitmenSettings(); });
	AddRow()->SetupSlider(LOCTEXT("AimSensitivity", "Aiming sensitivity"), LOCTEXT("AimSensitivityDesc", "Multiplies the sensitivity while aiming. The zoom is already compensated: 1.00 feels the same as the hip."),
		0.25f, 2.f, 0.05f, S->AimSensitivityMultiplier, [](float V) { return FText::FromString(FString::Printf(TEXT("x %.2f"), V)); },
		[S](float V) { S->AimSensitivityMultiplier = V; S->ApplyHitmenSettings(); });
	AddRow()->SetupChoice(LOCTEXT("InvertY", "Invert vertical look"), LOCTEXT("InvertYDesc", "Moving the mouse forward looks down."),
		OffOn(), S->bInvertY ? 1 : 0, [S](int32 I) { S->bInvertY = I == 1; S->ApplyHitmenSettings(); });
	AddHeader(LOCTEXT("Movement", "Movement"));
	const TArray<FText> HoldToggle = { LOCTEXT("Hold", "Hold"), LOCTEXT("Toggle", "Toggle") };
	AddRow()->SetupChoice(LOCTEXT("CrouchMode", "Crouch"), LOCTEXT("CrouchModeDesc", "Hold the key to stay crouched, or press it once to switch."),
		HoldToggle, S->bToggleCrouch ? 1 : 0, [S](int32 I) { S->bToggleCrouch = I == 1; S->ApplyHitmenSettings(); });
	AddRow()->SetupChoice(LOCTEXT("SprintMode", "Sprint"), LOCTEXT("SprintModeDesc", "Hold the key to sprint, or press it once to switch. A quick tap dashes when you have the dash."),
		HoldToggle, S->bToggleSprint ? 1 : 0, [S](int32 I) { S->bToggleSprint = I == 1; S->ApplyHitmenSettings(); });
	AddRow()->SetupChoice(LOCTEXT("ClimbMode", "Ladders and ziplines"), LOCTEXT("ClimbModeDesc", "Forward always climbs up, or the direction follows where you look."),
		{ LOCTEXT("ForwardUp", "Forward is up"), LOCTEXT("FollowCamera", "Follow the camera") }, S->bClimbRelativeToCamera ? 1 : 0,
		[S](int32 I) { S->bClimbRelativeToCamera = I == 1; S->ApplyHitmenSettings(); });
}

void UHitmenOptionsWidget::BuildControls()
{
	UEnhancedInputUserSettings* InputSettings = OptionsInputSettings(GetOwningPlayer());
	const UEnhancedPlayerMappableKeyProfile* Profile = InputSettings ? InputSettings->GetActiveKeyProfile() : nullptr;
	if (!Profile)
	{
		AddHeader(LOCTEXT("NoBindings", "Key bindings are not available yet"));
		return;
	}
	// Group the first slot of every mapping by its category.
	TMap<FString, TArray<const FPlayerKeyMapping*>> ByCategory;
	for (const TPair<FName, FKeyMappingRow>& Pair : Profile->GetPlayerMappingRows())
	{
		for (const FPlayerKeyMapping& Mapping : Pair.Value.Mappings)
		{
			if (Mapping.GetSlot() == EPlayerMappableKeySlot::First)
			{
				ByCategory.FindOrAdd(Mapping.GetDisplayCategory().ToString()).Add(&Mapping);
			}
		}
	}
	TArray<FString> Categories;
	for (const TCHAR* Category : CategoryOrder)
	{
		if (ByCategory.Contains(Category))
		{
			Categories.Add(Category);
		}
	}
	TArray<FString> Others;
	ByCategory.GetKeys(Others);
	Others.Sort();
	for (const FString& Category : Others)
	{
		Categories.AddUnique(Category);
	}
	for (const FString& Category : Categories)
	{
		AddHeader(FText::FromString(Category));
		TArray<const FPlayerKeyMapping*>& Mappings = ByCategory[Category];
		// Play order (the key profile is a map and loses the mapping context's order).
		Mappings.Sort([](const FPlayerKeyMapping& A, const FPlayerKeyMapping& B)
		{
			const int32 RankA = MappingRank(A.GetMappingName());
			const int32 RankB = MappingRank(B.GetMappingName());
			return RankA != RankB ? RankA < RankB : A.GetDisplayName().ToString() < B.GetDisplayName().ToString();
		});
		for (const FPlayerKeyMapping* Mapping : Mappings)
		{
			const FText Description = Category == TEXT("Spectator")
				? LOCTEXT("SpectatorKeyDesc", "Free fly while you watch or after the match. Only used while spectating, so it may share keys with the game.")
				: LOCTEXT("KeyDesc", "Click, then press a key or a mouse button. Escape cancels.");
			AddRow()->SetupKey(Mapping->GetDisplayName(), Description, Mapping->GetMappingName(), Mapping->GetCurrentKey());
		}
	}
	RefreshKeyConflicts();
}

void UHitmenOptionsWidget::RefreshKeyConflicts()
{
	// Spectator keys live in their own context (it overrides the game while you watch): they only clash with
	// each other. Everything else shares one context.
	TMap<FKey, TArray<UHitmenOptionRow*>> Groups[2];
	TArray<UHitmenOptionRow*> KeyRows;
	FString Section;
	TMap<UHitmenOptionRow*, int32> RowGroup;
	for (int32 Index = 0; Index < RowBox->GetChildrenCount(); ++Index)
	{
		const USizeBox* Box = Cast<USizeBox>(RowBox->GetChildAt(Index));
		UHitmenOptionRow* Row = Box ? Cast<UHitmenOptionRow>(Box->GetContent()) : nullptr;
		if (!Row)
		{
			continue;
		}
		if (Row->GetKind() == EHitmenOptionRowKind::Header)
		{
			Section = Row->GetLabel().ToString();
			continue;
		}
		if (Row->GetKind() == EHitmenOptionRowKind::Key && Row->GetKey().IsValid())
		{
			const int32 Group = Section == TEXT("Spectator") ? 1 : 0;
			Groups[Group].FindOrAdd(Row->GetKey()).Add(Row);
			KeyRows.Add(Row);
		}
	}
	for (UHitmenOptionRow* Row : KeyRows)
	{
		Row->SetConflict(FText::GetEmpty());
	}
	for (TMap<FKey, TArray<UHitmenOptionRow*>>& Group : Groups)
	{
		for (TPair<FKey, TArray<UHitmenOptionRow*>>& Pair : Group)
		{
			if (Pair.Value.Num() < 2)
			{
				continue;
			}
			for (UHitmenOptionRow* Row : Pair.Value)
			{
				TArray<FString> OtherNames;
				for (const UHitmenOptionRow* Other : Pair.Value)
				{
					if (Other != Row)
					{
						OtherNames.Add(Other->GetLabel().ToString());
					}
				}
				Row->SetConflict(FText::FromString(FString::Join(OtherNames, TEXT(", "))));
			}
		}
	}
}

void UHitmenOptionsWidget::HandleKeyChosen(UHitmenOptionRow* Row, const FKey& Key)
{
	UEnhancedInputUserSettings* InputSettings = OptionsInputSettings(GetOwningPlayer());
	if (!Row || !InputSettings || !Key.IsValid())
	{
		return;
	}
	FMapPlayerKeyArgs Args;
	Args.MappingName = Row->GetMappingName();
	Args.Slot = EPlayerMappableKeySlot::First;
	Args.NewKey = Key;
	FGameplayTagContainer Failure;
	InputSettings->MapPlayerKey(Args, Failure);
	InputSettings->SaveSettings();
	Row->SetKey(Key);
	RefreshKeyConflicts();
	HandleRowHovered(Row);
	SetKeyboardFocus();
}

void UHitmenOptionsWidget::BuildGraphics()
{
	UHitmenUserSettings* S = UHitmenUserSettings::GetHitmenUserSettings();
	if (!S)
	{
		return;
	}
	auto ApplyAndRefresh = [this, S]()
	{
		S->ApplySettings(false);
		// The preset row turns Custom (or back) when one quality changes: rebuild after this click is handled.
		if (UWorld* World = GetWorld())
		{
			World->GetTimerManager().SetTimerForNextTick(FTimerDelegate::CreateWeakLambda(this, [this]() { RefreshCurrentTab(); }));
		}
	};

	AddHeader(LOCTEXT("Quality", "Quality"));
	TArray<FText> Presets = QualityNames();
	const int32 Overall = S->GetOverallScalabilityLevel();
	int32 PresetIndex = Overall;
	if (Overall < 0 || Overall > 3)
	{
		Presets.Add(LOCTEXT("Custom", "Custom"));
		PresetIndex = Presets.Num() - 1;
	}
	AddRow()->SetupChoice(LOCTEXT("Preset", "Quality preset"), LOCTEXT("PresetDesc", "Sets every quality below at once. Changing one of them makes the preset Custom."),
		Presets, PresetIndex, [S, ApplyAndRefresh](int32 I) { if (I <= 3) { S->SetOverallScalabilityLevel(I); ApplyAndRefresh(); } });
	AddRow()->SetupAction(LOCTEXT("AutoDetect", "Detect the best settings"), LOCTEXT("AutoDetectDesc", "Measures this computer (a second or two) and picks the qualities. It runs once by itself on the first launch."),
		LOCTEXT("Run", "Run"), [this, S]() { S->RunAutoDetect(); RefreshCurrentTab(); });

	AddHeader(LOCTEXT("Display", "Display"));
	{
		TArray<FIntPoint> Resolutions;
		UKismetSystemLibrary::GetSupportedFullscreenResolutions(Resolutions);
		const FIntPoint Current = S->GetScreenResolution();
		Resolutions.AddUnique(Current);
		Resolutions.Sort([](const FIntPoint& A, const FIntPoint& B) { return A.X * A.Y < B.X * B.Y; });
		TArray<FText> Names;
		for (const FIntPoint& R : Resolutions)
		{
			Names.Add(FText::FromString(FString::Printf(TEXT("%d x %d"), R.X, R.Y)));
		}
		AddRow()->SetupChoice(LOCTEXT("Resolution", "Resolution"), LOCTEXT("ResolutionDesc", "Size of the game image on screen."),
			Names, Resolutions.IndexOfByKey(Current), [S, Resolutions](int32 I)
			{
				if (Resolutions.IsValidIndex(I)) { S->SetScreenResolution(Resolutions[I]); S->ApplyResolutionSettings(false); S->SaveSettings(); }
			});
	}
	{
		const EWindowMode::Type Modes[3] = { EWindowMode::Fullscreen, EWindowMode::WindowedFullscreen, EWindowMode::Windowed };
		const EWindowMode::Type Mode = S->GetFullscreenMode();
		const int32 ModeIndex = Mode == EWindowMode::Fullscreen ? 0 : (Mode == EWindowMode::WindowedFullscreen ? 1 : 2);
		AddRow()->SetupChoice(LOCTEXT("WindowMode", "Window"), LOCTEXT("WindowModeDesc", "Fullscreen, a borderless window over the desktop, or a normal window."),
			{ LOCTEXT("Fullscreen", "Fullscreen"), LOCTEXT("Borderless", "Borderless"), LOCTEXT("Windowed", "Windowed") }, ModeIndex,
			[S, Modes](int32 I) { S->SetFullscreenMode(Modes[FMath::Clamp(I, 0, 2)]); S->ApplyResolutionSettings(false); S->SaveSettings(); });
	}
	AddRow()->SetupChoice(LOCTEXT("VSync", "Vertical sync"), LOCTEXT("VSyncDesc", "Removes tearing; adds a little input latency."),
		OffOn(), S->IsVSyncEnabled() ? 1 : 0, [S](int32 I) { S->SetVSyncEnabled(I == 1); S->ApplySettings(false); });
	{
		const TArray<int32> Limits = { 0, 30, 60, 90, 120, 144, 165, 240 };
		TArray<FText> Names;
		for (int32 Limit : Limits)
		{
			Names.Add(Limit == 0 ? LOCTEXT("Unlimited", "Unlimited") : FText::AsNumber(Limit));
		}
		const int32 Current = FMath::RoundToInt(S->GetFrameRateLimit());
		AddRow()->SetupChoice(LOCTEXT("FrameLimit", "Frame rate limit"), LOCTEXT("FrameLimitDesc", "Caps the frames per second (less heat and noise)."),
			Names, FMath::Max(0, Limits.IndexOfByKey(Current)), [S, Limits](int32 I) { S->SetFrameRateLimit(static_cast<float>(Limits[I])); S->ApplySettings(false); });
	}
	{
		float Normalized = 1.f, Value = 100.f, Min = 50.f, Max = 100.f;
		S->GetResolutionScaleInformationEx(Normalized, Value, Min, Max);
		AddRow()->SetupSlider(LOCTEXT("RenderScale", "Render scale"), LOCTEXT("RenderScaleDesc", "Renders fewer pixels and upscales them (TSR). Lower is faster and softer."),
			FMath::Max(50.f, Min), FMath::Min(100.f, Max), 5.f, Value, [](float V) { return FText::FromString(FString::Printf(TEXT("%d %%"), FMath::RoundToInt(V))); },
			[S](float V) { S->SetResolutionScaleValueEx(V); S->ApplySettings(false); });
	}

	AddHeader(LOCTEXT("Details", "Details"));
	struct FQualityRow { FText Label; FText Description; int32 Value; TFunction<void(int32)> Set; };
	const FQualityRow Qualities[] = {
		{ LOCTEXT("Shadows", "Shadows"), LOCTEXT("ShadowsDesc", "Sharpness and reach of the shadows."), S->GetShadowQuality(), [S](int32 I) { S->SetShadowQuality(I); } },
		{ LOCTEXT("GI", "Global illumination"), LOCTEXT("GIDesc", "Light bouncing in rooms (Lumen). Low and Medium turn Lumen off: much faster, flatter rooms."), S->GetGlobalIlluminationQuality(), [S](int32 I) { S->SetGlobalIlluminationQuality(I); } },
		{ LOCTEXT("Reflections", "Reflections"), LOCTEXT("ReflectionsDesc", "Reflections on floors, glass and metal."), S->GetReflectionQuality(), [S](int32 I) { S->SetReflectionQuality(I); } },
		{ LOCTEXT("Effects", "Effects"), LOCTEXT("EffectsDesc", "Particles, smoke and fire."), S->GetVisualEffectQuality(), [S](int32 I) { S->SetVisualEffectQuality(I); } },
		{ LOCTEXT("ViewDistance", "View distance"), LOCTEXT("ViewDistanceDesc", "How far small objects and details are drawn."), S->GetViewDistanceQuality(), [S](int32 I) { S->SetViewDistanceQuality(I); } },
		{ LOCTEXT("PostProcess", "Post-processing"), LOCTEXT("PostProcessDesc", "Bloom, ambient occlusion and other screen effects."), S->GetPostProcessingQuality(), [S](int32 I) { S->SetPostProcessingQuality(I); } },
		{ LOCTEXT("Textures", "Textures"), LOCTEXT("TexturesDesc", "Texture resolution (video memory)."), S->GetTextureQuality(), [S](int32 I) { S->SetTextureQuality(I); } },
		{ LOCTEXT("AntiAliasing", "Anti-aliasing"), LOCTEXT("AntiAliasingDesc", "Smooths edges (TSR quality)."), S->GetAntiAliasingQuality(), [S](int32 I) { S->SetAntiAliasingQuality(I); } },
	};
	for (const FQualityRow& Quality : Qualities)
	{
		TFunction<void(int32)> Set = Quality.Set;
		AddRow()->SetupChoice(Quality.Label, Quality.Description, QualityNames(), FMath::Clamp(Quality.Value, 0, 3),
			[Set, ApplyAndRefresh](int32 I) { Set(I); ApplyAndRefresh(); });
	}

	AddHeader(LOCTEXT("Camera", "Camera"));
	AddRow()->SetupSlider(LOCTEXT("FOV", "Field of view"), LOCTEXT("FOVDesc", "Horizontal field of view of the first-person camera."),
		70.f, 110.f, 1.f, S->FieldOfView, [](float V) { return FText::FromString(FString::Printf(TEXT("%d"), FMath::RoundToInt(V))); },
		[S](float V) { S->FieldOfView = V; S->ApplyHitmenSettings(); });
	AddRow()->SetupSlider(LOCTEXT("HeadBob", "Head bob"), LOCTEXT("HeadBobDesc", "How much the view bobs with your steps. 0 % keeps it still."),
		0.f, 1.f, 0.05f, S->HeadBobScale, Percent, [S](float V) { S->HeadBobScale = V; S->ApplyHitmenSettings(); });
}

void UHitmenOptionsWidget::BuildAudio()
{
	UHitmenUserSettings* S = UHitmenUserSettings::GetHitmenUserSettings();
	if (!S)
	{
		return;
	}
	AddHeader(LOCTEXT("Volume", "Volume"));
	struct FVolumeRow { FText Label; FText Description; float* Value; };
	const FVolumeRow Volumes[] = {
		{ LOCTEXT("Master", "Master"), LOCTEXT("MasterDesc", "Everything."), &S->MasterVolume },
		{ LOCTEXT("Music", "Music"), LOCTEXT("MusicDesc", "Menus and match music."), &S->MusicVolume },
		{ LOCTEXT("SFX", "Effects"), LOCTEXT("SFXDesc", "Shots, steps, doors, abilities."), &S->SFXVolume },
		{ LOCTEXT("Voice", "Voice"), LOCTEXT("VoiceDesc", "Voices and proximity chat."), &S->VoiceVolume },
		{ LOCTEXT("UIVolume", "Interface"), LOCTEXT("UIVolumeDesc", "Menu clicks and hovers."), &S->UIVolume },
	};
	for (const FVolumeRow& Volume : Volumes)
	{
		float* Value = Volume.Value;
		AddRow()->SetupSlider(Volume.Label, Volume.Description, 0.f, 1.f, 0.05f, *Value, Percent, [S, Value](float V) { *Value = V; S->ApplyHitmenSettings(); });
	}
}

void UHitmenOptionsWidget::ResetToDefaults()
{
	// Defaults for the tab on screen only.
	UHitmenUserSettings* S = UHitmenUserSettings::GetHitmenUserSettings();
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIBack);
	switch (CurrentTab)
	{
	case EHitmenOptionsTab::Gameplay:
		if (S)
		{
			const UHitmenUserSettings* D = GetDefault<UHitmenUserSettings>();
			S->MouseSensitivity = D->MouseSensitivity;
			S->AimSensitivityMultiplier = D->AimSensitivityMultiplier;
			S->bInvertY = D->bInvertY;
			S->bToggleCrouch = D->bToggleCrouch;
			S->bToggleSprint = D->bToggleSprint;
			S->bClimbRelativeToCamera = D->bClimbRelativeToCamera;
			S->ApplyHitmenSettings();
		}
		break;
	case EHitmenOptionsTab::Controls:
		if (UEnhancedInputUserSettings* InputSettings = OptionsInputSettings(GetOwningPlayer()))
		{
			if (UEnhancedPlayerMappableKeyProfile* Profile = InputSettings->GetActiveKeyProfile())
			{
				Profile->ResetToDefault();
			}
			InputSettings->SaveSettings();
			InputSettings->ApplySettings();
		}
		break;
	case EHitmenOptionsTab::Graphics:
		if (S)
		{
			const UHitmenUserSettings* D = GetDefault<UHitmenUserSettings>();
			S->FieldOfView = D->FieldOfView;
			S->HeadBobScale = D->HeadBobScale;
			S->SetVSyncEnabled(true);
			S->SetFrameRateLimit(0.f);
			S->RunAutoDetect();
			S->ApplyHitmenSettings();
		}
		break;
	case EHitmenOptionsTab::Audio:
		if (S)
		{
			const UHitmenUserSettings* D = GetDefault<UHitmenUserSettings>();
			S->MasterVolume = D->MasterVolume;
			S->MusicVolume = D->MusicVolume;
			S->SFXVolume = D->SFXVolume;
			S->VoiceVolume = D->VoiceVolume;
			S->UIVolume = D->UIVolume;
			S->ApplyHitmenSettings();
		}
		break;
	}
	ShowTab(CurrentTab);
}

void UHitmenOptionsWidget::SelectTab(int32 TabIndex)
{
	ShowTab(static_cast<EHitmenOptionsTab>(FMath::Clamp(TabIndex, 0, 3)));
}

void UHitmenOptionsWidget::Close()
{
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIBack);
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->SetOptionsOpen(false);
	}
}

#undef LOCTEXT_NAMESPACE
