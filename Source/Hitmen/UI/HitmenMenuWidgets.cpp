// Hitmen — overlays

#include "UI/HitmenMenuWidgets.h"
#include "GameFramework/PlayerState.h"
#include "Engine/Texture2D.h"
#include "EngineUtils.h"
#include "Core/HitmenTeamLibrary.h"
#include "World/Interactables/HitmenSecurity.h"
#include "UI/HitmenMenuKit.h"
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

FText UHitmenPauseMapWidget::GetCategoryName(int32 Category)
{
	switch (Category)
	{
	case 0: return LOCTEXT("LegendYou", "You");
	case 1: return LOCTEXT("LegendTeam", "Teammates");
	case 2: return LOCTEXT("LegendVIP", "Target (VIP)");
	case 3: return LOCTEXT("LegendRetrieval", "Retrieval points");
	case 4: return LOCTEXT("LegendAlerted", "Alerted guards");
	default: return LOCTEXT("LegendCameras", "Cameras");
	}
}

FLinearColor UHitmenPauseMapWidget::GetCategoryColor(int32 Category)
{
	switch (Category)
	{
	case 0: return HitmenUI::Text();
	case 1: return FLinearColor(0.3f, 0.7f, 1.f);
	case 2: return HitmenUI::Accent();
	case 3: return HitmenUI::Warning();
	case 4: return FLinearColor(1.f, 0.45f, 0.1f);
	default: return FLinearColor(0.75f, 0.75f, 0.8f);
	}
}

void UHitmenPauseMapWidget::NativeConstruct()
{
	Super::NativeConstruct();
	bHasMap = false;
	if (const UHitmenMapDefinition* Map = HitmenUI::FindCurrentMap(this))
	{
		Bounds = Map->WorldBounds;
		MapName = Map->DisplayName;
		if (UTexture2D* Texture = Map->MapTexture.LoadSynchronous())
		{
			MapBrush.SetResourceObject(Texture);
			MapBrush.ImageSize = FVector2D(Texture->GetSizeX(), Texture->GetSizeY());
			bHasMap = true;
		}
	}
	SetVisibility(ESlateVisibility::Visible);
}

FReply UHitmenPauseMapWidget::NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Zoom = FMath::Clamp(Zoom * (InMouseEvent.GetWheelDelta() > 0.f ? 1.25f : 0.8f), 1.f, 5.f);
	return FReply::Handled();
}

int32 UHitmenPauseMapWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Area = AllottedGeometry.GetLocalSize();
	const float Side = FMath::Min(Area.X, Area.Y);
	const FVector2D Origin = (Area - FVector2D(Side)) * 0.5f;
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, Origin, FVector2D(Side), FLinearColor(0.03f, 0.035f, 0.04f, 0.95f));

	const FVector2D Extent = Bounds.GetSize();
	const APawn* Pawn = GetOwningPlayerPawn();
	// Zoom keeps the player in view; north (+X) up.
	FVector2D Focus = Bounds.GetCenter();
	if (Pawn && Zoom > 1.f)
	{
		Focus = FVector2D(Pawn->GetActorLocation().X, Pawn->GetActorLocation().Y);
	}
	const float WorldSide = FMath::Max(Extent.X, Extent.Y) / Zoom;
	const FVector2D ViewMin(Focus.X - WorldSide * 0.5f, Focus.Y - WorldSide * 0.5f);
	auto ToMap = [&](const FVector& World)
	{
		const FVector2D Alpha((World.Y - ViewMin.Y) / WorldSide, 1.f - (World.X - ViewMin.X) / WorldSide);
		return Origin + Alpha * Side;
	};
	OutDrawElements.PushClip(FSlateClippingZone(AllottedGeometry));
	if (bHasMap)
	{
		const FVector2D TopLeft = ToMap(FVector(Bounds.Max.X, Bounds.Min.Y, 0.f));
		const FVector2D BottomRight = ToMap(FVector(Bounds.Min.X, Bounds.Max.Y, 0.f));
		HitmenUI::PaintImage(OutDrawElements, LayerId + 1, AllottedGeometry, &MapBrush, TopLeft, BottomRight - TopLeft);
	}
	auto Marker = [&](int32 Category, const FVector& World, const FString& Label, float Size)
	{
		if (!bCategoryVisible[Category])
		{
			return;
		}
		const FVector2D P = ToMap(World);
		const FLinearColor Color = GetCategoryColor(Category);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, P - FVector2D(Size * 0.5f + 2.f), FVector2D(Size + 4.f), FLinearColor(0.f, 0.f, 0.f, 0.7f));
		HitmenUI::PaintBox(OutDrawElements, LayerId + 4, AllottedGeometry, P - FVector2D(Size * 0.5f), FVector2D(Size), Color);
		if (!Label.IsEmpty())
		{
			HitmenUI::PaintText(OutDrawElements, LayerId + 4, AllottedGeometry, Label, P + FVector2D(Size, -Size - 4.f), HitmenUI::SpacedFont(14, TEXT("SemiBold"), 100), Color);
		}
	};
	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	if (GS)
	{
		for (const AActor* Point : GS->GetActiveRetrievalPoints())
		{
			Marker(3, Point->GetActorLocation(), TEXT(""), 14.f);
		}
		if (const AActor* VIP = GS->GetVIP())
		{
			Marker(2, VIP->GetActorLocation(), TEXT("TARGET"), 14.f);
		}
		const int32 MyTeam = Pawn ? UHitmenTeamLibrary::GetTeamId(Pawn) : HitmenTeams::Neutral;
		for (const APlayerState* PS : GS->PlayerArray)
		{
			const APawn* Mate = PS ? PS->GetPawn() : nullptr;
			if (Mate && Mate != Pawn && MyTeam != HitmenTeams::Neutral && UHitmenTeamLibrary::GetTeamId(Mate) == MyTeam)
			{
				Marker(1, Mate->GetActorLocation(), PS->GetPlayerName().ToUpper(), 10.f);
			}
		}
	}
	if (const UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		for (const FHitmenHUDAlertBlip& Blip : UI->GetAlertBlips())
		{
			if (Blip.Color != EHitmenAlertColor::None)
			{
				Marker(4, Blip.Location, TEXT(""), 9.f);
			}
		}
	}
	for (TActorIterator<AHitmenSecurityCamera> It(GetWorld()); It; ++It)
	{
		if (!It->IsCameraDisabled())
		{
			Marker(5, It->GetActorLocation(), TEXT(""), 8.f);
		}
	}
	if (Pawn && bCategoryVisible[0])
	{
		// You: an arrow along your view.
		const FVector2D P = ToMap(Pawn->GetActorLocation());
		const float Yaw = FMath::DegreesToRadians(Pawn->GetControlRotation().Yaw);
		const FVector2D Dir(FMath::Sin(Yaw), -FMath::Cos(Yaw));
		const FVector2D SideDir(-Dir.Y, Dir.X);
		HitmenUI::PaintLines(OutDrawElements, LayerId + 5, AllottedGeometry, { P + Dir * 12.f, P - Dir * 8.f + SideDir * 8.f, P - Dir * 4.f, P - Dir * 8.f - SideDir * 8.f, P + Dir * 12.f }, HitmenUI::Text(), 2.5f);
		HitmenUI::PaintText(OutDrawElements, LayerId + 5, AllottedGeometry, TEXT("YOU"), P + FVector2D(14.f, -10.f), HitmenUI::SpacedFont(14, TEXT("SemiBold"), 120), HitmenUI::Text());
	}
	OutDrawElements.PopClip();
	// Frame, name and compass letter (dark bands keep the text readable on a light map).
	HitmenUI::PaintGradient(OutDrawElements, LayerId + 6, AllottedGeometry, Origin, FVector2D(Side, 70.f), FLinearColor(0.f, 0.f, 0.f, 0.75f), FLinearColor(0.f, 0.f, 0.f, 0.f), false);
	HitmenUI::PaintGradient(OutDrawElements, LayerId + 6, AllottedGeometry, Origin + FVector2D(0.f, Side - 50.f), FVector2D(Side, 50.f), FLinearColor(0.f, 0.f, 0.f, 0.f), FLinearColor(0.f, 0.f, 0.f, 0.7f), false);
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 7, AllottedGeometry, Origin, FVector2D(Side), HitmenUI::Hairline(), 1.f);
	HitmenUI::PaintText(OutDrawElements, LayerId + 7, AllottedGeometry, MapName.ToString().ToUpper(), Origin + FVector2D(18.f, 14.f), HitmenUI::SpacedFont(26, TEXT("Bold"), 60), HitmenUI::Text());
	HitmenUI::PaintText(OutDrawElements, LayerId + 7, AllottedGeometry, TEXT("N"), Origin + FVector2D(Side * 0.5f - 6.f, 8.f), HitmenUI::Font(20, TEXT("Bold")), HitmenUI::Accent());
	const FString ZoomText = FString::Printf(TEXT("WHEEL TO ZOOM  -  x%.1f"), Zoom);
	HitmenUI::PaintText(OutDrawElements, LayerId + 7, AllottedGeometry, ZoomText, Origin + FVector2D(18.f, Side - 30.f), HitmenUI::SpacedFont(13, TEXT("Medium"), 160), HitmenUI::TextDim());
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 8, InWidgetStyle, bParentEnabled);
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
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;
	UHitmenBackdrop* Backdrop = WidgetTree->ConstructWidget<UHitmenBackdrop>(UHitmenBackdrop::StaticClass());
	Backdrop->LeftCoverage = 0.75f;
	Backdrop->RightCoverage = 0.4f;
	Backdrop->SetVisibility(ESlateVisibility::HitTestInvisible);
	UCanvasPanelSlot* BackSlot = HitmenUI::AddToCanvas(Canvas, Backdrop, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	BackSlot->SetOffsets(FMargin(0.f));

	// Left: title, actions, then the map legend.
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToCanvas(Canvas, Column, FAnchors(0.f, 0.f), FVector2D(96.f, 84.f), FVector2D::ZeroVector);
	UTextBlock* Overline = HitmenUI::MakeText(WidgetTree, LOCTEXT("InProgress", "CONTRACT IN PROGRESS"), 15, TEXT("Medium"), HitmenUI::TextDim());
	Overline->SetFont(HitmenUI::SpacedFont(15, TEXT("Medium"), 260));
	HitmenUI::AddToVBox(Column, Overline);
	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Paused", "PAUSED"), 76, TEXT("Bold")), FMargin(0.f, -10.f, 0.f, 0.f));
	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("StillRunning", "The match keeps running."), 18, TEXT("Italic"), HitmenUI::TextDim()), FMargin(2.f, 0.f, 0.f, 26.f));
	struct FEntry { FText Label; EHitmenMenuButtonKind Kind; FName Handler; };
	const FEntry Entries[] = {
		{ LOCTEXT("Resume", "Resume"), EHitmenMenuButtonKind::Secondary, GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, Resume) },
		{ LOCTEXT("Options", "Options"), EHitmenMenuButtonKind::Secondary, GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, OpenOptions) },
		{ LOCTEXT("Leave", "Leave match"), EHitmenMenuButtonKind::Back, GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, LeaveMatch) } };
	for (const FEntry& Entry : Entries)
	{
		UHitmenMenuButton* Button = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
		Button->Setup(Entry.Label, Entry.Kind);
		FScriptDelegate Delegate;
		Delegate.BindUFunction(this, Entry.Handler);
		Button->OnClicked.Add(Delegate);
		HitmenUI::AddToVBox(Column, Button, FMargin(-22.f, 2.f, 0.f, 2.f), false, HAlign_Left);
	}
	UTextBlock* LegendTitle = HitmenUI::MakeText(WidgetTree, LOCTEXT("Legend", "MAP LEGEND"), 15, TEXT("Medium"), HitmenUI::TextDim());
	LegendTitle->SetFont(HitmenUI::SpacedFont(15, TEXT("Medium"), 260));
	HitmenUI::AddToVBox(Column, LegendTitle, FMargin(0.f, 40.f, 0.f, 10.f));
	for (int32 Category = 0; Category < UHitmenPauseMapWidget::CategoryCount; ++Category)
	{
		UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		UBorder* Swatch = HitmenUI::MakePanel(WidgetTree, UHitmenPauseMapWidget::GetCategoryColor(Category), FMargin(0.f));
		USizeBox* SwatchSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		SwatchSize->SetWidthOverride(10.f);
		SwatchSize->SetHeightOverride(10.f);
		SwatchSize->SetContent(Swatch);
		HitmenUI::AddToHBox(Row, SwatchSize, FMargin(0.f, 0.f, 12.f, 0.f));
		UHitmenToggle* Toggle = WidgetTree->ConstructWidget<UHitmenToggle>(UHitmenToggle::StaticClass());
		Toggle->Setup(UHitmenPauseMapWidget::GetCategoryName(Category), true, 18);
		Toggle->OnToggled.AddDynamic(this, &UHitmenPauseMenuWidget::HandleLegendToggled);
		HitmenUI::AddToHBox(Row, Toggle);
		HitmenUI::AddToVBox(Column, Row, FMargin(0.f, 3.f));
		LegendToggles.Add(Toggle);
	}

	// The map takes most of the screen.
	Map = WidgetTree->ConstructWidget<UHitmenPauseMapWidget>(UHitmenPauseMapWidget::StaticClass());
	UCanvasPanelSlot* MapSlot = HitmenUI::AddToCanvas(Canvas, Map, FAnchors(0.24f, 0.05f, 0.82f, 0.95f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	MapSlot->SetOffsets(FMargin(0.f));

	// Debug tools flush against the right edge.
	DebugPanel = WidgetTree->ConstructWidget<UHitmenDebugPanelWidget>(UHitmenDebugPanelWidget::StaticClass());
	UCanvasPanelSlot* DebugSlot = HitmenUI::AddToCanvas(Canvas, DebugPanel, FAnchors(1.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	DebugSlot->SetOffsets(FMargin(-280.f, 0.f, 280.f, 0.f));
}

void UHitmenPauseMenuWidget::HandleLegendToggled(bool bChecked)
{
	if (!Map)
	{
		return;
	}
	for (int32 Category = 0; Category < LegendToggles.Num(); ++Category)
	{
		Map->SetCategoryVisible(Category, LegendToggles[Category]->IsChecked());
	}
}

void UHitmenPauseMenuWidget::BindEvents()
{
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
