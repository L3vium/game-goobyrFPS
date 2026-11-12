// Hitmen — overlays

#include "UI/HitmenMenuWidgets.h"
#include "Data/HitmenMapMarkerType.h"
#include "Components/HitmenMapMarkerComponent.h"
#include "GameFramework/PlayerState.h"
#include "Engine/Texture2D.h"
#include "EngineUtils.h"
#include "Core/HitmenTeamLibrary.h"
#include "World/Interactables/HitmenSecurity.h"
#include "UI/HitmenMenuKit.h"
#include "UI/HitmenOptionsWidgets.h"
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
	default: return LOCTEXT("LegendAlerted", "Guards on alert");
	}
}

FLinearColor UHitmenPauseMapWidget::GetCategoryColor(int32 Category)
{
	switch (Category)
	{
	case 0: return HitmenUI::Text();
	case 1: return FLinearColor(0.3f, 0.7f, 1.f);
	case 2: return HitmenUI::Accent();
	default: return HitmenAlert::ToLinearColor(EHitmenAlertColor::Yellow);
	}
}

void UHitmenPauseMapWidget::SetMarkerTypeVisible(UHitmenMapMarkerType* Type, bool bVisible)
{
	if (bVisible)
	{
		HiddenMarkerTypes.Remove(Type);
	}
	else if (Type)
	{
		HiddenMarkerTypes.Add(Type);
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
		// Dimmed so white markers (you, cameras, suspicious guards) read on the light schematic.
		HitmenUI::PaintImage(OutDrawElements, LayerId + 1, AllottedGeometry, &MapBrush, TopLeft, BottomRight - TopLeft, FLinearColor(0.42f, 0.44f, 0.47f, 1.f));
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
			if (Blip.Color != EHitmenAlertColor::None && bCategoryVisible[AlertedCategory])
			{
				// Same colours as the minimap: white (suspicious), yellow (alerted / searching), red (combat).
				const FVector2D P = ToMap(Blip.Location);
				HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, P - FVector2D(6.5f), FVector2D(13.f), FLinearColor(0.f, 0.f, 0.f, 0.7f));
				HitmenUI::PaintBox(OutDrawElements, LayerId + 4, AllottedGeometry, P - FVector2D(4.5f), FVector2D(9.f), HitmenAlert::ToLinearColor(Blip.Color));
			}
		}
	}
	// Map markers (retrieval points, cameras, anything with a UHitmenMapMarkerComponent), drawn by their kind.
	if (const UHitmenMapMarkerSubsystem* Markers = UHitmenMapMarkerSubsystem::Get(this))
	{
		for (const TWeakObjectPtr<UHitmenMapMarkerComponent>& MapMarker : Markers->GetMarkers())
		{
			const UHitmenMapMarkerType* Type = MapMarker.IsValid() ? MapMarker->GetType() : nullptr;
			if (!Type || !MapMarker->IsMarkerVisible() || !Type->bShowOnPauseMap || HiddenMarkerTypes.Contains(const_cast<UHitmenMapMarkerType*>(Type)))
			{
				continue;
			}
			const FVector2D P = ToMap(MapMarker->GetMarkerLocation());
			HitmenUI::PaintMapMarker(OutDrawElements, LayerId + 3, AllottedGeometry, P, Type->Size, Type, Markers->GetIconBrush(Type));
			if (!MapMarker->GetLabel().IsEmpty())
			{
				HitmenUI::PaintText(OutDrawElements, LayerId + 4, AllottedGeometry, MapMarker->GetLabel().ToString().ToUpper(), P + FVector2D(Type->Size, -Type->Size - 4.f), HitmenUI::SpacedFont(14, TEXT("SemiBold"), 100), Type->Color);
			}
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

	// Left: PAUSE and three big square blocks.
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToCanvas(Canvas, Column, FAnchors(0.f, 0.f), FVector2D(96.f, 84.f), FVector2D::ZeroVector);
	HitmenUI::AddToVBox(Column, HitmenUI::MakeText(WidgetTree, LOCTEXT("Paused", "PAUSE"), 84, TEXT("Bold")), FMargin(0.f, 0.f, 0.f, 28.f));
	struct FEntry { FText Label; FName Handler; };
	const FEntry Entries[] = {
		{ LOCTEXT("Resume", "Resume"), GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, Resume) },
		{ LOCTEXT("Options", "Options"), GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, OpenOptions) },
		{ LOCTEXT("Leave", "Leave match"), GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, LeaveMatch) },
		{ LOCTEXT("BackToLobbyEntry", "Back to the lobby"), GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, BackToLobby) } };
	for (const FEntry& Entry : Entries)
	{
		UHitmenMenuButton* Button = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
		Button->Setup(Entry.Label, EHitmenMenuButtonKind::Block);
		FScriptDelegate Delegate;
		Delegate.BindUFunction(this, Entry.Handler);
		Button->OnClicked.Add(Delegate);
		USizeBox* ButtonSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		ButtonSize->SetWidthOverride(360.f);
		ButtonSize->SetHeightOverride(92.f);
		ButtonSize->SetContent(Button);
		HitmenUI::AddToVBox(Column, ButtonSize, FMargin(0.f, 0.f, 0.f, 12.f), false, HAlign_Left);
		if (Entry.Handler == GET_FUNCTION_NAME_CHECKED(UHitmenPauseMenuWidget, BackToLobby))
		{
			// Only the host, once the match is over (RefreshLegend runs on every open).
			BackToLobbyEntry = ButtonSize;
			ButtonSize->SetVisibility(ESlateVisibility::Collapsed);
		}
	}

	// The map in the middle.
	Map = WidgetTree->ConstructWidget<UHitmenPauseMapWidget>(UHitmenPauseMapWidget::StaticClass());
	UCanvasPanelSlot* MapSlot = HitmenUI::AddToCanvas(Canvas, Map, FAnchors(0.26f, 0.05f, 0.76f, 0.95f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	MapSlot->SetOffsets(FMargin(0.f));

	// Right: the legend, each row with the marker colours as they are drawn on the map.
	LegendBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UCanvasPanelSlot* LegendSlot = HitmenUI::AddToCanvas(Canvas, LegendBox, FAnchors(1.f, 0.f), FVector2D(-64.f, 96.f), FVector2D(1.f, 0.f));
	LegendSlot->SetAutoSize(true);
	RefreshLegend();

	// Debug tools: hidden behind a small button; they open over the legend.
	DebugPanel = WidgetTree->ConstructWidget<UHitmenDebugPanelWidget>(UHitmenDebugPanelWidget::StaticClass());
	UCanvasPanelSlot* DebugSlot = HitmenUI::AddToCanvas(Canvas, DebugPanel, FAnchors(1.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	DebugSlot->SetOffsets(FMargin(-360.f, 64.f, 360.f, 120.f));
	DebugSlot->SetZOrder(10);
	DebugToggleButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	DebugToggleButton->Setup(LOCTEXT("DebugTools", "Debug tools"), EHitmenMenuButtonKind::Chip);
	DebugToggleButton->OnClicked.AddDynamic(this, &UHitmenPauseMenuWidget::ToggleDebugPanel);
	UCanvasPanelSlot* ToggleSlot = HitmenUI::AddToCanvas(Canvas, DebugToggleButton, FAnchors(1.f, 1.f), FVector2D(-64.f, -48.f), FVector2D(1.f, 1.f));
	ToggleSlot->SetAutoSize(true);
	ToggleSlot->SetZOrder(11);
}

void UHitmenPauseMenuWidget::RefreshLegend()
{
	if (BackToLobbyEntry)
	{
		const AHitmenPlayerController* PC = GetHitmenPC();
		BackToLobbyEntry->SetVisibility(PC && PC->IsHost() && PC->IsMatchOver() ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	}
	if (!LegendBox)
	{
		return;
	}
	TArray<UHitmenMapMarkerType*> Types;
	if (const UHitmenMapMarkerSubsystem* Markers = UHitmenMapMarkerSubsystem::Get(this))
	{
		Types = Markers->GetLegendTypes();
	}
	TArray<UHitmenMapMarkerType*> Current;
	for (const TObjectPtr<UHitmenMapMarkerType>& Type : LegendMarkerTypes)
	{
		Current.Add(Type.Get());
	}
	if (LegendToggles.Num() > 0 && Types == Current)
	{
		return;
	}
	// Markers register as the map starts, possibly after this menu was built: rebuild, everything shown again.
	LegendBox->ClearChildren();
	LegendToggles.Reset();
	LegendMarkerTypes.Reset();
	LegendMarkerTypes.Append(Types);
	if (Map)
	{
		for (int32 Category = 0; Category < UHitmenPauseMapWidget::CategoryCount; ++Category)
		{
			Map->SetCategoryVisible(Category, true);
		}
		for (UHitmenMapMarkerType* Type : Types)
		{
			Map->SetMarkerTypeVisible(Type, true);
		}
	}
	UVerticalBox* Legend = LegendBox;
	UTextBlock* LegendTitle = HitmenUI::MakeText(WidgetTree, LOCTEXT("Legend", "MAP LEGEND"), 15, TEXT("Medium"), HitmenUI::TextDim());
	LegendTitle->SetFont(HitmenUI::SpacedFont(15, TEXT("Medium"), 260));
	HitmenUI::AddToVBox(Legend, LegendTitle, FMargin(0.f, 0.f, 0.f, 12.f));
	const int32 RowCount = UHitmenPauseMapWidget::CategoryCount + LegendMarkerTypes.Num();
	for (int32 Category = 0; Category < RowCount; ++Category)
	{
		const UHitmenMapMarkerType* MarkerType = Category >= UHitmenPauseMapWidget::CategoryCount ? LegendMarkerTypes[Category - UHitmenPauseMapWidget::CategoryCount].Get() : nullptr;
		UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		UHorizontalBox* Swatches = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		TArray<FLinearColor> Colors = { MarkerType ? MarkerType->Color : UHitmenPauseMapWidget::GetCategoryColor(Category) };
		if (Category == UHitmenPauseMapWidget::AlertedCategory)
		{
			Colors = { HitmenAlert::ToLinearColor(EHitmenAlertColor::White), HitmenAlert::ToLinearColor(EHitmenAlertColor::Yellow), HitmenAlert::ToLinearColor(EHitmenAlertColor::Red) };
		}
		for (const FLinearColor& Color : Colors)
		{
			UBorder* Swatch = HitmenUI::MakePanel(WidgetTree, Color, FMargin(0.f));
			USizeBox* SwatchSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
			SwatchSize->SetWidthOverride(12.f);
			SwatchSize->SetHeightOverride(12.f);
			SwatchSize->SetContent(Swatch);
			HitmenUI::AddToHBox(Swatches, SwatchSize, FMargin(0.f, 0.f, 4.f, 0.f));
		}
		USizeBox* SwatchColumn = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		SwatchColumn->SetWidthOverride(52.f);
		SwatchColumn->SetContent(Swatches);
		HitmenUI::AddToHBox(Row, SwatchColumn, FMargin(0.f));
		UHitmenToggle* Toggle = WidgetTree->ConstructWidget<UHitmenToggle>(UHitmenToggle::StaticClass());
		Toggle->Setup(MarkerType ? MarkerType->DisplayName : UHitmenPauseMapWidget::GetCategoryName(Category), true, 18);
		Toggle->OnToggled.AddDynamic(this, &UHitmenPauseMenuWidget::HandleLegendToggled);
		HitmenUI::AddToHBox(Row, Toggle);
		HitmenUI::AddToVBox(Legend, Row, FMargin(0.f, 4.f));
		LegendToggles.Add(Toggle);
	}
}

void UHitmenPauseMenuWidget::HandleLegendToggled(bool bChecked)
{
	if (!Map)
	{
		return;
	}
	for (int32 Category = 0; Category < LegendToggles.Num(); ++Category)
	{
		const int32 MarkerIndex = Category - UHitmenPauseMapWidget::CategoryCount;
		if (MarkerIndex < 0)
		{
			Map->SetCategoryVisible(Category, LegendToggles[Category]->IsChecked());
		}
		else if (LegendMarkerTypes.IsValidIndex(MarkerIndex))
		{
			Map->SetMarkerTypeVisible(LegendMarkerTypes[MarkerIndex], LegendToggles[Category]->IsChecked());
		}
	}
}

void UHitmenPauseMenuWidget::ToggleDebugPanel()
{
	if (DebugPanel)
	{
		DebugPanel->SetVisibility(DebugPanel->IsVisible() ? ESlateVisibility::Collapsed : ESlateVisibility::Visible);
	}
}

void UHitmenPauseMenuWidget::BindEvents()
{
	if (DebugPanel)
	{
		// Closed by default; the toggle only exists where debug commands are allowed.
		const AHitmenGameState* GS = GetHitmenGameState();
		DebugPanel->SetVisibility(ESlateVisibility::Collapsed);
		if (DebugToggleButton)
		{
			DebugToggleButton->SetVisibility(GS && GS->AreDebugCommandsAllowed() ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
		}
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

void UHitmenPauseMenuWidget::BackToLobby()
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->RequestReturnToLobby();
	}
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->SetPauseMenuOpen(false);
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
			: SpectatorMode == EHitmenSpectatorMode::FreeFly
			? LOCTEXT("FreeFlyHint", "[ZQSD] move   [SPACE / A] up   [CTRL / E] down   [SHIFT] faster     [LMB/RMB] follow a player     [V] change view")
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
	// A banner at the top: the match is over but everyone keeps watching (free fly).
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;
	UBorder* Banner = HitmenUI::MakePanel(WidgetTree, FLinearColor(0.f, 0.f, 0.f, 0.72f), FMargin(56.f, 22.f, 56.f, 20.f));
	UCanvasPanelSlot* BannerSlot = HitmenUI::AddToCanvas(Canvas, Banner, FAnchors(0.5f, 0.f), FVector2D(0.f, 48.f), FVector2D(0.5f, 0.f));
	BannerSlot->SetAutoSize(true);
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Banner->SetContent(Column);
	WinnerText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 58, TEXT("Bold"));
	WinnerText->SetJustification(ETextJustify::Center);
	ReasonText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 22, TEXT("Regular"), HitmenUI::TextDim());
	ReasonText->SetJustification(ETextJustify::Center);
	HitmenUI::AddToVBox(Column, WinnerText, FMargin(0.f), false, HAlign_Center);
	HitmenUI::AddToVBox(Column, ReasonText, FMargin(0.f, 2.f, 0.f, 14.f), false, HAlign_Center);
	// Host: [Enter] Back to the lobby (also in the pause menu). Others: waiting for the host.
	BackHint = WidgetTree->ConstructWidget<UHitmenKeyHint>(UHitmenKeyHint::StaticClass());
	BackHint->Setup(TEXT("ENTER"), LOCTEXT("BackToLobbyHint", "Back to the lobby"));
	BackHint->OnClicked = [this]() { HandleBackClicked(); };
	USizeBox* HintSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	HintSize->SetHeightOverride(40.f);
	HintSize->SetWidthOverride(330.f);
	HintSize->SetContent(BackHint);
	HitmenUI::AddToVBox(Column, HintSize, FMargin(0.f), false, HAlign_Center);
	WaitText = HitmenUI::MakeText(WidgetTree, LOCTEXT("WaitingForHost", "Waiting for the host to go back to the lobby"), 19, TEXT("Italic"), HitmenUI::TextDim());
	HitmenUI::AddToVBox(Column, WaitText, FMargin(0.f), false, HAlign_Center);
	SetVisibility(ESlateVisibility::SelfHitTestInvisible);
}

void UHitmenResultsWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnMatchEnded.AddUniqueDynamic(this, &UHitmenResultsWidget::HandleMatchEnded);
		UI->OnKeyLabelsChanged.AddUniqueDynamic(this, &UHitmenResultsWidget::HandleKeyLabelsChanged);
		HandleKeyLabelsChanged();
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
		UI->OnKeyLabelsChanged.RemoveAll(this);
	}
}

void UHitmenResultsWidget::HandleKeyLabelsChanged()
{
	if (const UHitmenUIEventSubsystem* UI = GetUIEvents(); UI && BackHint)
	{
		BackHint->Setup(UI->GetKeyLabel(EHitmenKeyLabel::Confirm), LOCTEXT("BackToLobbyHint", "Back to the lobby"));
	}
}

void UHitmenResultsWidget::HandleBackClicked()
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->RequestReturnToLobby();
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
	const AHitmenPlayerController* PC = GetHitmenPC();
	const bool bHost = PC && PC->IsHost();
	if (BackHint && BackHint->GetParent()) { BackHint->GetParent()->SetVisibility(bHost ? ESlateVisibility::Visible : ESlateVisibility::Collapsed); }
	if (WaitText) { WaitText->SetVisibility(bHost ? ESlateVisibility::Collapsed : ESlateVisibility::HitTestInvisible); }
}

#undef LOCTEXT_NAMESPACE
