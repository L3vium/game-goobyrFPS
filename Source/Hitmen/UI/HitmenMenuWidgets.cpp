// Hitmen — overlays

#include "UI/HitmenMenuWidgets.h"
#include "UI/HitmenUIFactory.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenGameState.h"
#include "Characters/HitmenCharacterBase.h"  // GameState::GetVIP() type only
#include "Framework/HitmenGameInstance.h"
#include "Framework/HitmenPlayerState.h"
#include "Core/HitmenGameSettings.h"
#include "Core/HitmenUserSettings.h"
#include "Data/HitmenCatalog.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenAbilityData.h"
#include "Data/HitmenMapDefinition.h"
#include "Blueprint/WidgetTree.h"
#include "Components/OverlaySlot.h"
#include "Components/Overlay.h"
#include "Blueprint/WidgetLayoutLibrary.h"
#include "Components/Button.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/HorizontalBox.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Border.h"
#include "Components/SizeBox.h"
#include "Components/Slider.h"
#include "Components/ScrollBox.h"
#include "Components/Image.h"
#include "Components/UniformGridPanel.h"
#include "Engine/TextureRenderTarget2D.h"
#include "EnhancedInputSubsystems.h"
#include "UserSettings/EnhancedInputUserSettings.h"
#include "Rendering/DrawElements.h"
#include "Styling/CoreStyle.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"

#define LOCTEXT_NAMESPACE "HitmenMenus"

namespace
{
	void PaintBox(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D Position, FVector2D Size, const FLinearColor& Color)
	{
		FSlateDrawElement::MakeBox(Out, Layer, Geo.ToPaintGeometry(FVector2f(Size), FSlateLayoutTransform(FVector2f(Position))), FCoreStyle::Get().GetBrush("WhiteBrush"), ESlateDrawEffect::None, Color);
	}

	void PaintText(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, const FString& Text, FVector2D Position, int32 Size, const FLinearColor& Color)
	{
		FSlateDrawElement::MakeText(Out, Layer, Geo.ToPaintGeometry(FVector2f(300.f, 40.f), FSlateLayoutTransform(FVector2f(Position))), Text, HitmenUI::Font(Size), ESlateDrawEffect::None, Color);
	}

	UEnhancedInputUserSettings* GetInputUserSettings(const APlayerController* PC)
	{
		const ULocalPlayer* LP = PC ? PC->GetLocalPlayer() : nullptr;
		const UEnhancedInputLocalPlayerSubsystem* Subsystem = LP ? LP->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr;
		return Subsystem ? Subsystem->GetUserSettings() : nullptr;
	}
}

// =============================================================================================
// Pause map
// =============================================================================================

int32 UHitmenPauseMapWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, HitmenUI::Panel());

	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	const UHitmenMapDefinition* Map = GS ? UHitmenGameSettings::Get()->LoadMap(GS->GetLobbySettings().MapIndex) : nullptr;
	const FBox2D Bounds = Map ? Map->WorldBounds : FBox2D(FVector2D(-20000.f), FVector2D(20000.f));
	const FVector2D Extent = Bounds.GetSize();
	auto ToMap = [&](const FVector& World)
	{
		const FVector2D Alpha((World.Y - Bounds.Min.Y) / FMath::Max(1.f, Extent.Y), 1.f - (World.X - Bounds.Min.X) / FMath::Max(1.f, Extent.X));
		return FVector2D(FMath::Clamp(Alpha.X, 0.f, 1.f) * Size.X, FMath::Clamp(Alpha.Y, 0.f, 1.f) * Size.Y);
	};

	if (GS)
	{
		for (const AActor* Point : GS->GetActiveRetrievalPoints())
		{
			const FVector2D P = ToMap(Point->GetActorLocation());
			PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, P - FVector2D(6.f), FVector2D(12.f), FLinearColor(1.f, 0.78f, 0.1f));
		}
		if (const AActor* VIP = GS->GetVIP())
		{
			const FVector2D P = ToMap(VIP->GetActorLocation());
			PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, P - FVector2D(6.f), FVector2D(12.f), GS->IsVIPDead() ? FLinearColor(1.f, 0.78f, 0.1f) : HitmenUI::Accent());
			PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, TEXT("VIP"), P + FVector2D(8.f, -8.f), 12, HitmenUI::Text());
		}
	}
	if (const APawn* Pawn = GetOwningPlayerPawn())
	{
		const FVector2D P = ToMap(Pawn->GetActorLocation());
		PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, P - FVector2D(5.f), FVector2D(10.f), HitmenUI::Text());
		PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, TEXT("YOU"), P + FVector2D(8.f, -8.f), 12, HitmenUI::Text());
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Debug panel
// =============================================================================================

void UHitmenDebugPanelWidget::BuildDefaultLayout()
{
	UBorder* Panel = HitmenUI::MakePanel(WidgetTree, HitmenUI::Panel(), FMargin(12.f));
	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(Column);
	Panel->SetContent(Scroll);
	WidgetTree->RootWidget = Panel;

	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Debug", "DEBUG"), 22, TEXT("Black")));
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
	// 0 weapon, 1 ability, 2 keycard, 3 god mode.
	auto AddButton = [this, Column](const FText& Label, int32 Index, int32 Kind)
	{
		UHitmenListButton* Button = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Button->Setup(Label, Index, EHitmenButtonStyle::Solid, 13);
		switch (Kind)
		{
		case 0: Button->OnItemClicked.AddUniqueDynamic(this, &UHitmenDebugPanelWidget::HandleWeaponClicked); break;
		case 1: Button->OnItemClicked.AddUniqueDynamic(this, &UHitmenDebugPanelWidget::HandleAbilityClicked); break;
		case 2: Button->OnItemClicked.AddUniqueDynamic(this, &UHitmenDebugPanelWidget::HandleKeycardClicked); break;
		default: Button->OnItemClicked.AddUniqueDynamic(this, &UHitmenDebugPanelWidget::HandleGodClicked); break;
		}
		HitmenUI::AddToVBox(Column, Button, FMargin(0.f, 1.f));
	};
	if (Catalog)
	{
		HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Weapons", "WEAPONS"), 14, TEXT("Bold"), HitmenUI::TextDim()), FMargin(0.f, 8.f, 0.f, 2.f));
		for (int32 Index = 0; Index < Catalog->Weapons.Num(); ++Index)
		{
			if (Catalog->Weapons[Index]) { AddButton(Catalog->Weapons[Index]->DisplayName, Index, 0); }
		}
		HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Abilities", "ABILITIES"), 14, TEXT("Bold"), HitmenUI::TextDim()), FMargin(0.f, 8.f, 0.f, 2.f));
		const TArray<UHitmenAbilityData*> Move = Catalog->GetAbilities(EHitmenAbilitySlot::Move);
		const TArray<UHitmenAbilityData*> Manip = Catalog->GetAbilities(EHitmenAbilitySlot::Manipulation);
		for (int32 Index = 0; Index < Move.Num(); ++Index)
		{
			AddButton(FText::Format(LOCTEXT("MoveEntry", "[E] {0}"), Move[Index]->DisplayName), Index, 1);
		}
		for (int32 Index = 0; Index < Manip.Num(); ++Index)
		{
			AddButton(FText::Format(LOCTEXT("ManipEntry", "[A] {0}"), Manip[Index]->DisplayName), 1000 + Index, 1);
		}
	}
	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Keycards", "KEYCARDS"), 14, TEXT("Bold"), HitmenUI::TextDim()), FMargin(0.f, 8.f, 0.f, 2.f));
	for (int32 Card = 0; Card < 4; ++Card)
	{
		AddButton(FText::Format(LOCTEXT("Keycard", "Keycard {0}"), FText::AsNumber(Card)), Card, 2);
	}
	AddButton(LOCTEXT("God", "Toggle god mode"), 0, 3);
}

void UHitmenDebugPanelWidget::HandleWeaponClicked(int32 Index)
{
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
	if (AHitmenPlayerController* PC = GetHitmenPC(); PC && Catalog && Catalog->Weapons.IsValidIndex(Index))
	{
		PC->Server_DebugGiveWeapon(Catalog->Weapons[Index]);
	}
}

void UHitmenDebugPanelWidget::HandleAbilityClicked(int32 Index)
{
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
	AHitmenPlayerController* PC = GetHitmenPC();
	if (!PC || !Catalog)
	{
		return;
	}
	const bool bManip = Index >= 1000;
	const TArray<UHitmenAbilityData*> List = Catalog->GetAbilities(bManip ? EHitmenAbilitySlot::Manipulation : EHitmenAbilitySlot::Move);
	const int32 ListIndex = bManip ? Index - 1000 : Index;
	if (List.IsValidIndex(ListIndex))
	{
		PC->Server_DebugGiveAbility(List[ListIndex]);
	}
}

void UHitmenDebugPanelWidget::HandleKeycardClicked(int32 Index)
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_DebugGiveKeycard(Index);
	}
}

void UHitmenDebugPanelWidget::HandleGodClicked(int32 Index)
{
	bGodMode = !bGodMode;
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_DebugSetGodMode(bGodMode);
	}
}

// =============================================================================================
// Pause menu
// =============================================================================================

void UHitmenPauseMenuWidget::BuildDefaultLayout()
{
	UBorder* Dim = HitmenUI::MakePanel(WidgetTree, HitmenUI::Background(), FMargin(60.f));
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	Dim->SetContent(Canvas);
	WidgetTree->RootWidget = Dim;

	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Paused", "PAUSE"), 64, TEXT("Black")));
	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("StillRunning", "The match keeps running."), 14, TEXT("Regular"), HitmenUI::TextDim()), FMargin(0.f, 0.f, 0.f, 20.f));
	ResumeButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Resume", "RESUME"), 22, EHitmenButtonStyle::Solid);
	OptionsButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Options", "OPTIONS"), 22, EHitmenButtonStyle::Solid);
	LeaveButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Leave", "LEAVE MATCH"), 22, EHitmenButtonStyle::Dashed);
	for (UButton* Button : { ResumeButton.Get(), OptionsButton.Get(), LeaveButton.Get() })
	{
		USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		Size->SetWidthOverride(320.f);
		Size->SetContent(Button);
		HitmenUI::AddToVBox(Column, Size, FMargin(0.f, 5.f), false, HAlign_Left);
	}
	HitmenUI::AddToCanvas(Canvas, Column, FAnchors(0.f, 0.f), FVector2D::ZeroVector, FVector2D::ZeroVector);

	Map = WidgetTree->ConstructWidget<UHitmenPauseMapWidget>(UHitmenPauseMapWidget::StaticClass());
	HitmenUI::AddToCanvas(Canvas, Map, FAnchors(0.5f, 0.5f), FVector2D(80.f, 0.f), FVector2D(0.5f, 0.5f), FVector2D(560.f, 560.f));

	DebugPanel = WidgetTree->ConstructWidget<UHitmenDebugPanelWidget>(UHitmenDebugPanelWidget::StaticClass());
	UCanvasPanelSlot* DebugSlot = HitmenUI::AddToCanvas(Canvas, DebugPanel, FAnchors(1.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D(1.f, 0.f));
	DebugSlot->SetOffsets(FMargin(-300.f, 0.f, 300.f, 0.f));
}

void UHitmenPauseMenuWidget::BindEvents()
{
	if (ResumeButton) { ResumeButton->OnClicked.AddUniqueDynamic(this, &UHitmenPauseMenuWidget::Resume); }
	if (OptionsButton) { OptionsButton->OnClicked.AddUniqueDynamic(this, &UHitmenPauseMenuWidget::OpenOptions); }
	if (LeaveButton) { LeaveButton->OnClicked.AddUniqueDynamic(this, &UHitmenPauseMenuWidget::LeaveMatch); }
	if (DebugPanel)
	{
		const AHitmenGameState* GS = GetHitmenGameState();
		DebugPanel->SetVisibility(GS && GS->AreDebugCommandsAllowed() ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	}
}

void UHitmenPauseMenuWidget::Resume()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->SetPauseMenuOpen(false);
	}
}

void UHitmenPauseMenuWidget::OpenOptions()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->SetOptionsOpen(true);
	}
}

void UHitmenPauseMenuWidget::LeaveMatch()
{
	if (UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>())
	{
		GI->ReturnToTitle();
	}
}

// =============================================================================================
// Key binding row
// =============================================================================================

void UHitmenKeyBindingRow::BuildDefaultLayout()
{
	UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	USizeBox* NameSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	NameSize->SetWidthOverride(240.f);
	NameText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 15, TEXT("Regular"));
	NameSize->SetContent(NameText);
	HitmenUI::AddToHBox(Row, NameSize);
	UTextBlock* Label = nullptr;
	KeyButton = HitmenUI::MakeButton(WidgetTree, FText::GetEmpty(), 15, EHitmenButtonStyle::Solid, &Label);
	KeyText = Label;
	USizeBox* KeySize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	KeySize->SetWidthOverride(180.f);
	KeySize->SetContent(KeyButton);
	HitmenUI::AddToHBox(Row, KeySize);
	WidgetTree->RootWidget = Row;
	KeyButton->OnClicked.AddDynamic(this, &UHitmenKeyBindingRow::HandleKeyClicked);
	SetIsFocusable(true);
}

void UHitmenKeyBindingRow::Setup(FName InMappingName, const FText& InDisplayName, const FKey& InKey)
{
	MappingName = InMappingName;
	CurrentKey = InKey;
	if (NameText) { NameText->SetText(InDisplayName); }
	if (KeyText) { KeyText->SetText(InKey.GetDisplayName()); }
}

void UHitmenKeyBindingRow::HandleKeyClicked()
{
	bListening = true;
	if (KeyText) { KeyText->SetText(LOCTEXT("PressKey", "Press a key...")); }
	SetKeyboardFocus();
}

FReply UHitmenKeyBindingRow::NativeOnKeyDown(const FGeometry& InGeometry, const FKeyEvent& InKeyEvent)
{
	if (!bListening)
	{
		return Super::NativeOnKeyDown(InGeometry, InKeyEvent);
	}
	ApplyKey(InKeyEvent.GetKey() == EKeys::Escape ? CurrentKey : InKeyEvent.GetKey());
	return FReply::Handled();
}

FReply UHitmenKeyBindingRow::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (!bListening)
	{
		return Super::NativeOnMouseButtonDown(InGeometry, InMouseEvent);
	}
	ApplyKey(InMouseEvent.GetEffectingButton());
	return FReply::Handled();
}

void UHitmenKeyBindingRow::ApplyKey(const FKey& Key)
{
	bListening = false;
	if (UEnhancedInputUserSettings* Settings = GetInputUserSettings(GetOwningPlayer()))
	{
		FMapPlayerKeyArgs Args;
		Args.MappingName = MappingName;
		Args.Slot = EPlayerMappableKeySlot::First;
		Args.NewKey = Key;
		FGameplayTagContainer Failure;
		Settings->MapPlayerKey(Args, Failure);
		Settings->SaveSettings();
		CurrentKey = Key;
	}
	if (KeyText) { KeyText->SetText(CurrentKey.GetDisplayName()); }
}

// =============================================================================================
// Options
// =============================================================================================

void UHitmenOptionsWidget::BuildDefaultLayout()
{
	UBorder* Dim = HitmenUI::MakePanel(WidgetTree, HitmenUI::Background(), FMargin(60.f, 50.f));
	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(Column);
	Dim->SetContent(Scroll);
	WidgetTree->RootWidget = Dim;

	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("OptionsTitle", "OPTIONS"), 54, TEXT("Black")), FMargin(0.f, 0.f, 0.f, 16.f));

	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Controls", "CONTROLS"), 18, TEXT("Bold"), HitmenUI::TextDim()));
	ToggleBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Column, ToggleBox);

	auto AddSlider = [this, Column](const FText& Label, float Min, float Max) -> USlider*
	{
		UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		USizeBox* LabelSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		LabelSize->SetWidthOverride(240.f);
		LabelSize->SetContent(HitmenUI::MakeText(WidgetTree, Label, 15, TEXT("Regular")));
		HitmenUI::AddToHBox(Row, LabelSize);
		USlider* Slider = WidgetTree->ConstructWidget<USlider>(USlider::StaticClass());
		Slider->SetMinValue(Min);
		Slider->SetMaxValue(Max);
		USizeBox* SliderSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		SliderSize->SetWidthOverride(300.f);
		SliderSize->SetContent(Slider);
		HitmenUI::AddToHBox(Row, SliderSize);
		HitmenUI::AddToVBox(Column, Row, FMargin(0.f, 4.f), false, HAlign_Left);
		return Slider;
	};
	SensitivitySlider = AddSlider(LOCTEXT("Sensitivity", "Mouse sensitivity"), 0.1f, 3.f);
	FOVSlider = AddSlider(LOCTEXT("FOV", "Field of view"), 70.f, 110.f);
	VolumeSlider = AddSlider(LOCTEXT("Volume", "Master volume"), 0.f, 1.f);

	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Bindings", "KEY BINDINGS"), 18, TEXT("Bold"), HitmenUI::TextDim()), FMargin(0.f, 18.f, 0.f, 4.f));
	BindingBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Column, BindingBox);

	UHorizontalBox* Buttons = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	CloseButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Back", "BACK"), 20, EHitmenButtonStyle::Solid);
	ResetButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Reset", "RESET"), 20, EHitmenButtonStyle::Dashed);
	HitmenUI::AddToHBox(Buttons, CloseButton, FMargin(0.f, 0.f, 12.f, 0.f));
	HitmenUI::AddToHBox(Buttons, ResetButton);
	HitmenUI::AddToVBox(Column, Buttons, FMargin(0.f, 20.f), false, HAlign_Left);
}

void UHitmenOptionsWidget::BindEvents()
{
	if (CloseButton) { CloseButton->OnClicked.AddUniqueDynamic(this, &UHitmenOptionsWidget::Close); }
	if (ResetButton) { ResetButton->OnClicked.AddUniqueDynamic(this, &UHitmenOptionsWidget::ResetToDefaults); }
	if (SensitivitySlider) { SensitivitySlider->OnValueChanged.AddUniqueDynamic(this, &UHitmenOptionsWidget::HandleSensitivityChanged); }
	if (FOVSlider) { FOVSlider->OnValueChanged.AddUniqueDynamic(this, &UHitmenOptionsWidget::HandleFOVChanged); }
	if (VolumeSlider) { VolumeSlider->OnValueChanged.AddUniqueDynamic(this, &UHitmenOptionsWidget::HandleMasterVolumeChanged); }
	RefreshToggles();
	RebuildBindings();
}

void UHitmenOptionsWidget::RefreshToggles()
{
	UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (!Settings || !ToggleBox)
	{
		return;
	}
	ToggleBox->ClearChildren();
	auto OnOff = [](bool b) { return b ? LOCTEXT("On", "ON") : LOCTEXT("Off", "OFF"); };
	const TArray<FText> Labels = {
		FText::Format(LOCTEXT("ToggleCrouch", "Crouch toggle: {0}"), OnOff(Settings->bToggleCrouch)),
		FText::Format(LOCTEXT("ToggleSprint", "Sprint toggle: {0}"), OnOff(Settings->bToggleSprint)),
		FText::Format(LOCTEXT("ClimbCamera", "Ladder direction follows the camera: {0}"), OnOff(Settings->bClimbRelativeToCamera)),
		FText::Format(LOCTEXT("InvertY", "Invert Y: {0}"), OnOff(Settings->bInvertY))
	};
	for (int32 Index = 0; Index < Labels.Num(); ++Index)
	{
		UHitmenListButton* Button = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Button->Setup(Labels[Index], Index, EHitmenButtonStyle::Solid, 15);
		Button->OnItemClicked.AddUniqueDynamic(this, &UHitmenOptionsWidget::HandleToggleClicked);
		HitmenUI::AddToVBox(ToggleBox, Button, FMargin(0.f, 2.f), false, HAlign_Left);
	}
	if (SensitivitySlider) { SensitivitySlider->SetValue(Settings->MouseSensitivity); }
	if (FOVSlider) { FOVSlider->SetValue(Settings->FieldOfView); }
	if (VolumeSlider) { VolumeSlider->SetValue(Settings->MasterVolume); }
}

void UHitmenOptionsWidget::RebuildBindings()
{
	UEnhancedInputUserSettings* InputSettings = GetInputUserSettings(GetOwningPlayer());
	if (!BindingBox)
	{
		return;
	}
	BindingBox->ClearChildren();
	const UEnhancedPlayerMappableKeyProfile* Profile = InputSettings ? InputSettings->GetActiveKeyProfile() : nullptr;
	if (!Profile)
	{
		HitmenUI::AddToVBox(BindingBox, HitmenUI::MakeText(WidgetTree, LOCTEXT("NoBindings", "Key bindings are available in game."), 14, TEXT("Regular"), HitmenUI::TextDim()));
		return;
	}
	for (const TPair<FName, FKeyMappingRow>& Pair : Profile->GetPlayerMappingRows())
	{
		for (const FPlayerKeyMapping& Mapping : Pair.Value.Mappings)
		{
			if (Mapping.GetSlot() != EPlayerMappableKeySlot::First)
			{
				continue;
			}
			UHitmenKeyBindingRow* Row = WidgetTree->ConstructWidget<UHitmenKeyBindingRow>(UHitmenKeyBindingRow::StaticClass());
			Row->Setup(Mapping.GetMappingName(), Mapping.GetDisplayName(), Mapping.GetCurrentKey());
			HitmenUI::AddToVBox(BindingBox, Row, FMargin(0.f, 2.f), false, HAlign_Left);
		}
	}
}

void UHitmenOptionsWidget::HandleToggleClicked(int32 Index)
{
	UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (!Settings)
	{
		return;
	}
	switch (Index)
	{
	case 0: Settings->bToggleCrouch = !Settings->bToggleCrouch; break;
	case 1: Settings->bToggleSprint = !Settings->bToggleSprint; break;
	case 2: Settings->bClimbRelativeToCamera = !Settings->bClimbRelativeToCamera; break;
	case 3: Settings->bInvertY = !Settings->bInvertY; break;
	default: break;
	}
	Settings->ApplyHitmenSettings();
	RefreshToggles();
}

void UHitmenOptionsWidget::HandleSensitivityChanged(float Value)
{
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->MouseSensitivity = Value;
		Settings->ApplyHitmenSettings();
	}
}

void UHitmenOptionsWidget::HandleFOVChanged(float Value)
{
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->FieldOfView = Value;
		Settings->ApplyHitmenSettings();
	}
}

void UHitmenOptionsWidget::HandleMasterVolumeChanged(float Value)
{
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->MasterVolume = Value;
		Settings->ApplyHitmenSettings();
	}
}

void UHitmenOptionsWidget::ResetToDefaults()
{
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->SetToDefaults();
		Settings->ApplyHitmenSettings();
	}
	if (UEnhancedInputUserSettings* InputSettings = GetInputUserSettings(GetOwningPlayer()))
	{
		if (UEnhancedPlayerMappableKeyProfile* Profile = InputSettings->GetActiveKeyProfile())
		{
			Profile->ResetToDefault();
		}
		InputSettings->SaveSettings();
	}
	RefreshToggles();
	RebuildBindings();
}

void UHitmenOptionsWidget::Close()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->SetOptionsOpen(false);
	}
}

// =============================================================================================
// Emote wheel
// =============================================================================================

void UHitmenEmoteWheelWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnEmoteWheelToggled.AddUniqueDynamic(this, &UHitmenEmoteWheelWidget::HandleWheelToggled);
	}
}

void UHitmenEmoteWheelWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnEmoteWheelToggled.RemoveAll(this);
	}
}

TArray<FText> UHitmenEmoteWheelWidget::GetEmoteNames() const
{
	TArray<FText> Names;
	if (const UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		Names = UI->GetEmoteNames();
	}
	if (Names.IsEmpty())
	{
		Names = { LOCTEXT("Wave", "Wave"), LOCTEXT("Point", "Point"), LOCTEXT("Bow", "Bow"), LOCTEXT("Dance", "Dance") };
	}
	return Names;
}

void UHitmenEmoteWheelWidget::HandleWheelToggled(bool bOpen)
{
	if (!bOpen && HoveredIndex != INDEX_NONE)
	{
		if (AHitmenPlayerController* PC = GetHitmenPC())
		{
			PC->SelectEmote(HoveredIndex);
		}
	}
	HoveredIndex = INDEX_NONE;
}

void UHitmenEmoteWheelWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const int32 Count = GetEmoteNames().Num();
	const FVector2D Mouse = UWidgetLayoutLibrary::GetMousePositionOnViewport(this);
	const FVector2D Center = UWidgetLayoutLibrary::GetViewportSize(this) * 0.5f / UWidgetLayoutLibrary::GetViewportScale(this);
	const FVector2D Delta = Mouse - Center;
	if (Count == 0 || Delta.Size() < 40.f)
	{
		HoveredIndex = INDEX_NONE;
		return;
	}
	float Angle = FMath::RadiansToDegrees(FMath::Atan2(Delta.X, -Delta.Y));
	Angle = FMath::Fmod(Angle + 360.f + 180.f / Count, 360.f);
	HoveredIndex = FMath::Clamp(FMath::FloorToInt(Angle / (360.f / Count)), 0, Count - 1);
}

int32 UHitmenEmoteWheelWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Center = AllottedGeometry.GetLocalSize() * 0.5f;
	const TArray<FText> Names = GetEmoteNames();
	const float Radius = 170.f;
	for (int32 Index = 0; Index < Names.Num(); ++Index)
	{
		const float Angle = FMath::DegreesToRadians(Index * 360.f / Names.Num());
		const FVector2D Point = Center + FVector2D(FMath::Sin(Angle), -FMath::Cos(Angle)) * Radius;
		const bool bHovered = Index == HoveredIndex;
		PaintBox(OutDrawElements, LayerId, AllottedGeometry, Point - FVector2D(70.f, 22.f), FVector2D(140.f, 44.f), bHovered ? HitmenUI::Accent() : HitmenUI::Panel());
		PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Names[Index].ToString(), Point - FVector2D(60.f, 12.f), 16, HitmenUI::Text());
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Spectator
// =============================================================================================

void UHitmenSpectatorWidget::BuildDefaultLayout()
{
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;
	ModeText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 18);
	HitmenUI::AddToCanvas(Canvas, ModeText, FAnchors(0.5f, 1.f), FVector2D(0.f, -30.f), FVector2D(0.5f, 1.f));
	Grid = WidgetTree->ConstructWidget<UUniformGridPanel>(UUniformGridPanel::StaticClass());
	Grid->SetSlotPadding(FMargin(4.f));
	UCanvasPanelSlot* GridSlot = HitmenUI::AddToCanvas(Canvas, Grid, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	GridSlot->SetOffsets(FMargin(20.f, 20.f, 20.f, 80.f));
	Grid->SetVisibility(ESlateVisibility::Collapsed);
}

void UHitmenSpectatorWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnSpectatorModeChanged.AddUniqueDynamic(this, &UHitmenSpectatorWidget::HandleModeChanged);
		UI->OnSpectatorViewsChanged.AddUniqueDynamic(this, &UHitmenSpectatorWidget::HandleViewsChanged);
		HandleViewsChanged(UI->GetSpectatorViews());
	}
}

void UHitmenSpectatorWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnSpectatorModeChanged.RemoveAll(this);
		UI->OnSpectatorViewsChanged.RemoveAll(this);
	}
}

void UHitmenSpectatorWidget::HandleModeChanged(uint8 Mode)
{
	CurrentMode = Mode;
	const EHitmenSpectatorMode SpectatorMode = static_cast<EHitmenSpectatorMode>(Mode);
	if (ModeText)
	{
		const FText ModeName = SpectatorMode == EHitmenSpectatorMode::FreeFly ? LOCTEXT("FreeFly", "FREE FLY")
			: SpectatorMode == EHitmenSpectatorMode::Follow ? LOCTEXT("Follow", "FOLLOW") : LOCTEXT("Grid", "MULTI-SCREEN");
		const FText Hint = SpectatorMode == EHitmenSpectatorMode::Grid
			? LOCTEXT("GridHint", "[Wheel] number of screens     [LMB/RMB] switch player     [V] change view")
			: LOCTEXT("SpectatorHint", "[LMB/RMB] switch player     [V] change view");
		ModeText->SetText(FText::Format(LOCTEXT("SpectatorLine", "{0}     {1}"), ModeName, Hint));
	}
	if (Grid && SpectatorMode != EHitmenSpectatorMode::Grid)
	{
		Grid->SetVisibility(ESlateVisibility::Collapsed);
	}
}

void UHitmenSpectatorWidget::HandleViewsChanged(const TArray<FHitmenSpectatorView>& Views)
{
	if (!Grid)
	{
		return;
	}
	Grid->ClearChildren();
	const int32 Columns = FMath::Max(1, FMath::CeilToInt(FMath::Sqrt(static_cast<float>(Views.Num()))));
	for (int32 Index = 0; Index < Views.Num(); ++Index)
	{
		const FHitmenSpectatorView& View = Views[Index];
		if (!View.RenderTarget)
		{
			continue;
		}
		UOverlay* Cell = WidgetTree->ConstructWidget<UOverlay>(UOverlay::StaticClass());
		UImage* Image = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
		FSlateBrush Brush;
		Brush.SetResourceObject(View.RenderTarget);
		Brush.ImageSize = FVector2D(View.RenderTarget->SizeX, View.RenderTarget->SizeY);
		Image->SetBrush(Brush);
		Cell->AddChildToOverlay(Image);
		UTextBlock* Name = HitmenUI::MakeText(WidgetTree, View.PlayerName, 14);
		if (UOverlaySlot* NameSlot = Cell->AddChildToOverlay(Name))
		{
			NameSlot->SetHorizontalAlignment(HAlign_Left);
			NameSlot->SetVerticalAlignment(VAlign_Top);
			NameSlot->SetPadding(FMargin(8.f, 6.f));
		}
		Grid->AddChildToUniformGrid(Cell, Index / Columns, Index % Columns);
	}
	const bool bGrid = static_cast<EHitmenSpectatorMode>(CurrentMode) == EHitmenSpectatorMode::Grid && !Views.IsEmpty();
	Grid->SetVisibility(bGrid ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
}

// =============================================================================================
// Results
// =============================================================================================

void UHitmenResultsWidget::BuildDefaultLayout()
{
	UBorder* Dim = HitmenUI::MakePanel(WidgetTree, HitmenUI::Background(), FMargin(80.f));
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	WinnerText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 72, TEXT("Black"));
	ReasonText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 22, TEXT("Regular"), HitmenUI::TextDim());
	HitmenUI::AddToVBox(Column, WinnerText);
	HitmenUI::AddToVBox(Column, ReasonText);
	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("BackToLobby", "Back to the lobby in a few seconds..."), 16, TEXT("Regular"), HitmenUI::TextDim()), FMargin(0.f, 30.f));
	Dim->SetContent(Column);
	WidgetTree->RootWidget = Dim;
}

void UHitmenResultsWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnMatchEnded.AddUniqueDynamic(this, &UHitmenResultsWidget::HandleMatchEnded);
	}
	if (const AHitmenGameState* GS = GetHitmenGameState(); GS && GS->GetMatchPhase() == EHitmenMatchPhase::Ended)
	{
		HandleMatchEnded(GS->GetMatchResult());
	}
}

void UHitmenResultsWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnMatchEnded.RemoveAll(this);
	}
}

void UHitmenResultsWidget::HandleMatchEnded(const FHitmenMatchResult& Result)
{
	const AHitmenGameState* GS = GetHitmenGameState();
	FText Winner = LOCTEXT("Draw", "DRAW");
	if (!Result.bIsDraw)
	{
		const FHitmenTeam* Team = GS ? GS->FindTeam(Result.WinningTeamId) : nullptr;
		Winner = Team ? FText::Format(LOCTEXT("TeamWins", "{0} WINS"), Team->DisplayName)
			: FText::Format(LOCTEXT("PlayerWins", "{0} WINS"), FText::FromString(Result.WinningPlayerName.ToUpper()));
	}
	if (WinnerText) { WinnerText->SetText(Winner); }
	if (ReasonText) { ReasonText->SetText(Result.Reason); }
}

#undef LOCTEXT_NAMESPACE
