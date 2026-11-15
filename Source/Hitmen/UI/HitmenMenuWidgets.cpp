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
#include "Components/UniformGridSlot.h"
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
	default: return LOCTEXT("LegendNPCs", "Guards");
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

bool UHitmenPauseMapWidget::IsCategoryDrawn(int32 Category) const
{
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	switch (Category)
	{
	case 1:
	{
		if (!Settings->bPauseMapShowTeammates)
		{
			return false;
		}
		const APawn* Pawn = GetOwningPlayerPawn();
		const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
		const int32 MyTeam = Pawn ? UHitmenTeamLibrary::GetTeamId(Pawn) : HitmenTeams::Neutral;
		if (!GS || MyTeam == HitmenTeams::Neutral)
		{
			return false;
		}
		for (const APlayerState* PS : GS->PlayerArray)
		{
			const APawn* Mate = PS ? PS->GetPawn() : nullptr;
			if (Mate && Mate != Pawn && UHitmenTeamLibrary::GetTeamId(Mate) == MyTeam)
			{
				return true;
			}
		}
		return false;
	}
	case AlertedCategory: return Settings->bPauseMapShowNPCs;
	default: return true;
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

void UHitmenPauseMapWidget::GetMapSquare(const FVector2D& Area, FVector2D& OutOrigin, float& OutSide)
{
	OutSide = FMath::Min(Area.X, Area.Y);
	OutOrigin = (Area - FVector2D(OutSide)) * 0.5f;
}

FVector2D UHitmenPauseMapWidget::GetViewCenter() const
{
	return bViewCenterSet ? ViewCenter : Bounds.GetCenter();
}

float UHitmenPauseMapWidget::GetWorldSide() const
{
	const FVector2D Extent = Bounds.GetSize();
	return FMath::Max(Extent.X, Extent.Y) / FMath::Max(Zoom, 1.f);
}

void UHitmenPauseMapWidget::ClampViewCenter()
{
	const FVector2D Center = Bounds.GetCenter();
	const FVector2D Extent = Bounds.GetSize();
	const float Full = FMath::Max(Extent.X, Extent.Y);
	const float Slack = FMath::Max(0.f, (Full - GetWorldSide()) * 0.5f);
	ViewCenter = FVector2D(FMath::Clamp(GetViewCenter().X, Center.X - Slack, Center.X + Slack), FMath::Clamp(GetViewCenter().Y, Center.Y - Slack, Center.Y + Slack));
	bViewCenterSet = true;
}

FReply UHitmenPauseMapWidget::NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	// Zoom toward the point under the cursor: it stays under the cursor (north up: screen X is world Y, screen Y is
	// world -X).
	FVector2D Origin;
	float Side = 0.f;
	GetMapSquare(InGeometry.GetLocalSize(), Origin, Side);
	const FVector2D Local = InGeometry.AbsoluteToLocal(InMouseEvent.GetScreenSpacePosition());
	const FVector2D Alpha = (Local - Origin) / FMath::Max(Side, 1.f) - FVector2D(0.5f);
	const FVector2D Center = GetViewCenter();
	const float OldSide = GetWorldSide();
	const FVector2D Under(Center.X - Alpha.Y * OldSide, Center.Y + Alpha.X * OldSide);
	Zoom = FMath::Clamp(Zoom * (InMouseEvent.GetWheelDelta() > 0.f ? 1.25f : 0.8f), 1.f, 5.f);
	const float NewSide = GetWorldSide();
	ViewCenter = FVector2D(Under.X + Alpha.Y * NewSide, Under.Y - Alpha.X * NewSide);
	bViewCenterSet = true;
	ClampViewCenter();
	return FReply::Handled();
}

FReply UHitmenPauseMapWidget::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (InMouseEvent.GetEffectingButton() == EKeys::LeftMouseButton && Zoom > 1.f)
	{
		bDragging = true;
		LastDragPosition = InMouseEvent.GetScreenSpacePosition();
		return FReply::Handled().CaptureMouse(TakeWidget());
	}
	return Super::NativeOnMouseButtonDown(InGeometry, InMouseEvent);
}

FReply UHitmenPauseMapWidget::NativeOnMouseButtonUp(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (bDragging && InMouseEvent.GetEffectingButton() == EKeys::LeftMouseButton)
	{
		bDragging = false;
		return FReply::Handled().ReleaseMouseCapture();
	}
	return Super::NativeOnMouseButtonUp(InGeometry, InMouseEvent);
}

FReply UHitmenPauseMapWidget::NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (!bDragging)
	{
		return Super::NativeOnMouseMove(InGeometry, InMouseEvent);
	}
	FVector2D Origin;
	float Side = 0.f;
	GetMapSquare(InGeometry.GetLocalSize(), Origin, Side);
	// Screen pixels to local units, then to world: the map follows the cursor.
	const FVector2D Delta = InGeometry.AbsoluteToLocal(InMouseEvent.GetScreenSpacePosition()) - InGeometry.AbsoluteToLocal(LastDragPosition);
	LastDragPosition = InMouseEvent.GetScreenSpacePosition();
	const float WorldPerUnit = GetWorldSide() / FMath::Max(Side, 1.f);
	const FVector2D Center = GetViewCenter();
	ViewCenter = FVector2D(Center.X + Delta.Y * WorldPerUnit, Center.Y - Delta.X * WorldPerUnit);
	bViewCenterSet = true;
	ClampViewCenter();
	return FReply::Handled();
}

int32 UHitmenPauseMapWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	FVector2D Origin;
	float Side = 0.f;
	GetMapSquare(AllottedGeometry.GetLocalSize(), Origin, Side);
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, Origin, FVector2D(Side), FLinearColor(0.03f, 0.035f, 0.04f, 0.95f));

	const APawn* Pawn = GetOwningPlayerPawn();
	// North (+X) up; the view centre follows the wheel (zoom toward the cursor) and left drags.
	const FVector2D Focus = Zoom > 1.f ? GetViewCenter() : Bounds.GetCenter();
	const float WorldSide = GetWorldSide();
	const FVector2D ViewMin(Focus.X - WorldSide * 0.5f, Focus.Y - WorldSide * 0.5f);
	auto ToMap = [&](const FVector& World)
	{
		const FVector2D Alpha((World.Y - ViewMin.Y) / WorldSide, 1.f - (World.X - ViewMin.X) / WorldSide);
		return Origin + Alpha * Side;
	};
	// Everything stays inside the map square (never over the legend or the menu).
	OutDrawElements.PushClip(FSlateClippingZone(AllottedGeometry.ToPaintGeometry(FVector2f(Side, Side), FSlateLayoutTransform(FVector2f(Origin)))));
	if (bHasMap)
	{
		const FVector2D TopLeft = ToMap(FVector(Bounds.Max.X, Bounds.Min.Y, 0.f));
		const FVector2D BottomRight = ToMap(FVector(Bounds.Min.X, Bounds.Max.Y, 0.f));
		// Dimmed so white markers (you, cameras, suspicious guards) read on the light schematic.
		HitmenUI::PaintImage(OutDrawElements, LayerId + 1, AllottedGeometry, &MapBrush, TopLeft, BottomRight - TopLeft, FLinearColor(0.42f, 0.44f, 0.47f, 1.f));
	}
	auto Marker = [&](int32 Category, const FVector& World, const FString& Label, float Size)
	{
		if (!bCategoryVisible[Category] || !IsCategoryDrawn(Category))
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
	// NPCs (Project Settings -> Hitmen -> Maps, off by default): diamonds as on the minimap, cones under them.
	const UHitmenGameSettings* MapSettings = UHitmenGameSettings::Get();
	const UHitmenUIEventSubsystem* BlipSource = bCategoryVisible[AlertedCategory] && IsCategoryDrawn(AlertedCategory) ? GetUIEvents() : nullptr;
	if (BlipSource)
	{
		const float PixelsPerCm = Side / FMath::Max(WorldSide, 1.f);
		for (const FHitmenHUDAlertBlip& Blip : BlipSource->GetAlertBlips())
		{
			if (Blip.bIsCamera)
			{
				continue;
			}
			const FVector2D P = ToMap(Blip.Location);
			const FLinearColor Tint = HitmenUI::GetMapNPCColor(Blip.Color);
			if (MapSettings->bPauseMapShowVisionCones && Blip.SightRadius > 0.f)
			{
				HitmenUI::PaintVisionCone(OutDrawElements, LayerId + 2, AllottedGeometry, P, Blip.SightRadius * PixelsPerCm, Blip.Yaw,
					Blip.SightHalfAngle, HitmenUI::GetMapConeColor(Blip.Color) * FLinearColor(1.f, 1.f, 1.f, MapSettings->MapVisionConeOpacity));
			}
			HitmenUI::PaintDiamond(OutDrawElements, LayerId + 3, AllottedGeometry, P, MapSettings->PauseMapNPCSize * 0.5f, Tint);
		}
	}
	// Map markers (retrieval points, cameras, anything with a UHitmenMapMarkerComponent), drawn by their kind.
	if (const UHitmenMapMarkerSubsystem* Markers = UHitmenMapMarkerSubsystem::Get(this))
	{
		for (const TWeakObjectPtr<UHitmenMapMarkerComponent>& MapMarker : Markers->GetMarkers())
		{
			const UHitmenMapMarkerType* Type = MapMarker.IsValid() ? MapMarker->GetType() : nullptr;
			if (!Type || !MapMarker->IsMarkerVisible() || !Type->IsShownOn(true) || HiddenMarkerTypes.Contains(const_cast<UHitmenMapMarkerType*>(Type)))
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
	const FString ZoomText = Zoom > 1.f ? FString::Printf(TEXT("WHEEL: ZOOM AT THE CURSOR  -  DRAG: MOVE  -  x%.1f"), Zoom)
		: FString(TEXT("WHEEL: ZOOM AT THE CURSOR"));
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
	// Only what the map draws gets a row (teammates come and go with the players).
	TArray<int32> Categories;
	for (int32 Category = 0; Category < UHitmenPauseMapWidget::CategoryCount; ++Category)
	{
		if (!Map || Map->IsCategoryDrawn(Category))
		{
			Categories.Add(Category);
		}
	}
	if (LegendToggles.Num() > 0 && Types == Current && Categories == LegendCategories)
	{
		return;
	}
	// Markers register as the map starts, possibly after this menu was built: rebuild, everything shown again.
	LegendBox->ClearChildren();
	LegendToggles.Reset();
	LegendMarkerTypes.Reset();
	LegendMarkerTypes.Append(Types);
	LegendCategories = Categories;
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
	const int32 RowCount = LegendCategories.Num() + LegendMarkerTypes.Num();
	for (int32 RowIndex = 0; RowIndex < RowCount; ++RowIndex)
	{
		const UHitmenMapMarkerType* MarkerType = RowIndex >= LegendCategories.Num() ? LegendMarkerTypes[RowIndex - LegendCategories.Num()].Get() : nullptr;
		const int32 Category = MarkerType ? INDEX_NONE : LegendCategories[RowIndex];
		UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		UHorizontalBox* Swatches = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		TArray<FLinearColor> Colors = { MarkerType ? MarkerType->Color : UHitmenPauseMapWidget::GetCategoryColor(Category) };
		if (Category == UHitmenPauseMapWidget::AlertedCategory)
		{
			Colors = { HitmenUI::GetMapNPCColor(EHitmenAlertColor::None), HitmenAlert::ToLinearColor(EHitmenAlertColor::White),
				HitmenAlert::ToLinearColor(EHitmenAlertColor::Yellow), HitmenAlert::ToLinearColor(EHitmenAlertColor::Red) };
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
		SwatchColumn->SetWidthOverride(68.f);
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
	for (int32 RowIndex = 0; RowIndex < LegendToggles.Num(); ++RowIndex)
	{
		const int32 MarkerIndex = RowIndex - LegendCategories.Num();
		if (MarkerIndex < 0)
		{
			Map->SetCategoryVisible(LegendCategories[RowIndex], LegendToggles[RowIndex]->IsChecked());
		}
		else if (LegendMarkerTypes.IsValidIndex(MarkerIndex))
		{
			Map->SetMarkerTypeVisible(LegendMarkerTypes[MarkerIndex], LegendToggles[RowIndex]->IsChecked());
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
	// Split screen: the whole screen but a strip at the bottom for the keys.
	Grid = WidgetTree->ConstructWidget<UUniformGridPanel>(UUniformGridPanel::StaticClass());
	Grid->SetSlotPadding(FMargin(3.f));
	UCanvasPanelSlot* GridSlot = HitmenUI::AddToCanvas(Canvas, Grid, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	GridSlot->SetOffsets(FMargin(6.f, 6.f, 6.f, 64.f));
	Grid->SetVisibility(ESlateVisibility::Collapsed);
	// Bottom: the view (and who), then its keys.
	ModeText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 19, TEXT("Bold"));
	ModeText->SetJustification(ETextJustify::Center);
	UCanvasPanelSlot* ModeSlot = HitmenUI::AddToCanvas(Canvas, ModeText, FAnchors(0.5f, 1.f), FVector2D(0.f, -34.f), FVector2D(0.5f, 1.f));
	ModeSlot->SetAutoSize(true);
	KeysText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 14, TEXT("Medium"), HitmenUI::TextDim());
	KeysText->SetJustification(ETextJustify::Center);
	UCanvasPanelSlot* KeysSlot = HitmenUI::AddToCanvas(Canvas, KeysText, FAnchors(0.5f, 1.f), FVector2D(0.f, -12.f), FVector2D(0.5f, 1.f));
	KeysSlot->SetAutoSize(true);
	// The fly speed, briefly, after a wheel notch.
	SpeedText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 22, TEXT("Bold"));
	SpeedText->SetJustification(ETextJustify::Center);
	UCanvasPanelSlot* SpeedSlot = HitmenUI::AddToCanvas(Canvas, SpeedText, FAnchors(0.5f, 0.5f), FVector2D(0.f, 80.f), FVector2D(0.5f, 0.5f));
	SpeedSlot->SetAutoSize(true);
	SpeedText->SetRenderOpacity(0.f);
	InfoText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 16, TEXT("Medium"), FLinearColor(0.88f, 0.88f, 0.88f, 1.f));
	InfoText->SetJustification(ETextJustify::Center);
	InfoText->SetShadowColorAndOpacity(FLinearColor(0.f, 0.f, 0.f, 0.8f));
	InfoText->SetShadowOffset(FVector2D(1.f, 1.f));
	UCanvasPanelSlot* InfoSlot = HitmenUI::AddToCanvas(Canvas, InfoText, FAnchors(0.5f, 0.5f), FVector2D(0.f, 36.f), FVector2D(0.5f, 0.f));
	InfoSlot->SetAutoSize(true);
}

void UHitmenSpectatorWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	if (SpeedText && GetWorld())
	{
		const float Age = GetWorld()->GetRealTimeSeconds() - SpeedShownAt;
		SpeedText->SetRenderOpacity(FMath::Clamp((SpeedShowTime - Age) / 0.3f, 0.f, 1.f));
	}
	if (InfoText)
	{
		const UHitmenUIEventSubsystem* UI = GetUIEvents();
		const FText Info = UI && static_cast<EHitmenSpectatorMode>(CurrentMode) != EHitmenSpectatorMode::Grid ? UI->GetInteractionInfo() : FText::GetEmpty();
		if (!Info.EqualTo(InfoText->GetText()))
		{
			InfoText->SetText(Info);
		}
	}
}

void UHitmenSpectatorWidget::HandleWatcherSpeed(float SpeedScale)
{
	if (SpeedText && GetWorld())
	{
		SpeedText->SetText(FText::FromString(FString::Printf(TEXT("FLY SPEED  x%.2g"), SpeedScale)));
		SpeedShownAt = GetWorld()->GetRealTimeSeconds();
	}
}

void UHitmenSpectatorWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnSpectatorModeChanged.AddUniqueDynamic(this, &UHitmenSpectatorWidget::HandleModeChanged);
		UI->OnSpectatorViewsChanged.AddUniqueDynamic(this, &UHitmenSpectatorWidget::HandleViewsChanged);
		UI->OnWatcherSpeedChanged.AddUniqueDynamic(this, &UHitmenSpectatorWidget::HandleWatcherSpeed);
		HandleViewsChanged(UI->GetSpectatorViews());
	}
}

void UHitmenSpectatorWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnSpectatorModeChanged.RemoveAll(this);
		UI->OnSpectatorViewsChanged.RemoveAll(this);
		UI->OnWatcherSpeedChanged.RemoveAll(this);
	}
}

void UHitmenSpectatorWidget::HandleModeChanged(uint8 Mode)
{
	CurrentMode = Mode;
	const EHitmenSpectatorMode SpectatorMode = static_cast<EHitmenSpectatorMode>(Mode);
	const UHitmenUIEventSubsystem* UI = GetUIEvents();
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	const AHitmenPlayerState* Self = GetOwningPlayerState<AHitmenPlayerState>();
	const bool bWatcher = !Self || Self->IsWatcher() || Self->IsAlive() == false;
	const bool bCanFreeFly = Self && Self->IsWatcher();
	if (ModeText)
	{
		FString Line;
		switch (SpectatorMode)
		{
		case EHitmenSpectatorMode::Follow:
		{
			const FString Name = UI ? UI->GetSpectatorTargetName().ToString().ToUpper() : FString();
			Line = Name.IsEmpty() ? FString(TEXT("PLAYER VIEW")) : FString::Printf(TEXT("PLAYER VIEW  -  %s"), *Name);
			break;
		}
		case EHitmenSpectatorMode::Grid:
			Line = ViewCount > 0 ? FString(TEXT("SPLIT SCREEN")) : FString(TEXT("SPLIT SCREEN  -  NOBODY TO WATCH"));
			break;
		default:
			Line = TEXT("FREE FLY");
			break;
		}
		ModeText->SetText(FText::FromString(Line));
	}
	if (KeysText)
	{
		// The view's own keys, then the views and the switches this server allows.
		TArray<FString> Keys;
		switch (SpectatorMode)
		{
		case EHitmenSpectatorMode::Follow:
			Keys = { TEXT("[LMB / RMB] switch player"), TEXT("[V] first / third person") };
			break;
		case EHitmenSpectatorMode::Grid:
			Keys = { TEXT("[LMB / RMB] switch player") };
			break;
		default:
			Keys = { TEXT("[ZQSD] move"), TEXT("[E / SPACE] up"), TEXT("[A / CTRL] down"), TEXT("[SHIFT] faster"), TEXT("[WHEEL] speed") };
			break;
		}
		Keys.Add(TEXT("[1] player view"));
		Keys.Add(TEXT("[2] split screen"));
		if (bCanFreeFly)
		{
			Keys.Add(TEXT("[3] free fly"));
		}
		if (bWatcher && Settings->WatcherSeePlayers == EHitmenWatcherVisibility::ShownToggleable)
		{
			Keys.Add(FString::Printf(TEXT("[N] players %s"), UI && UI->AreWatcherPlayersShown() ? TEXT("on") : TEXT("off")));
		}
		if (bWatcher && Settings->WatcherSeeWatchers == EHitmenWatcherVisibility::ShownToggleable)
		{
			Keys.Add(FString::Printf(TEXT("[B] watchers %s"), UI && UI->AreWatcherWatchersShown() ? TEXT("on") : TEXT("off")));
		}
		if (Settings->bWatchersCanScan && SpectatorMode != EHitmenSpectatorMode::Grid)
		{
			Keys.Add(TEXT("[TAB] scan"));
		}
		KeysText->SetText(FText::FromString(FString::Join(Keys, TEXT("     "))));
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
	ViewCount = Views.Num();
	// 1, 2 side by side, 2x2, 3x2, 4x2: each picture fills its cell (the controller shapes it like the cell).
	int32 Columns = 1;
	int32 Rows = 1;
	HitmenUI::GetSplitLayout(Views.Num(), Columns, Rows);
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
		if (UOverlaySlot* ImageSlot = Cell->AddChildToOverlay(Image))
		{
			ImageSlot->SetHorizontalAlignment(HAlign_Fill);
			ImageSlot->SetVerticalAlignment(VAlign_Fill);
		}
		UTextBlock* Name = HitmenUI::MakeText(WidgetTree, FText::FromString(View.PlayerName.ToString().ToUpper()), 15, TEXT("Bold"));
		if (UOverlaySlot* NameSlot = Cell->AddChildToOverlay(Name))
		{
			NameSlot->SetHorizontalAlignment(HAlign_Left);
			NameSlot->SetVerticalAlignment(VAlign_Top);
			NameSlot->SetPadding(FMargin(8.f, 6.f));
		}
		if (UUniformGridSlot* CellSlot = Grid->AddChildToUniformGrid(Cell, Index / Columns, Index % Columns))
		{
			CellSlot->SetHorizontalAlignment(HAlign_Fill);
			CellSlot->SetVerticalAlignment(VAlign_Fill);
		}
	}
	const bool bGrid = static_cast<EHitmenSpectatorMode>(CurrentMode) == EHitmenSpectatorMode::Grid && !Views.IsEmpty();
	Grid->SetVisibility(bGrid ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
	HandleModeChanged(CurrentMode);
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
	HitmenUI::AddToVBox(Column, ReasonText, FMargin(0.f, 2.f, 0.f, 4.f), false, HAlign_Center);
	// Bottom right: host, [Enter] Back to the lobby (also in the pause menu); others, waiting for the host.
	UVerticalBox* Corner = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UCanvasPanelSlot* CornerSlot = HitmenUI::AddToCanvas(Canvas, Corner, FAnchors(1.f, 1.f), FVector2D(-40.f, -36.f), FVector2D(1.f, 1.f));
	CornerSlot->SetAutoSize(true);
	BackHint = WidgetTree->ConstructWidget<UHitmenKeyHint>(UHitmenKeyHint::StaticClass());
	BackHint->Setup(TEXT("ENTER"), LOCTEXT("BackToLobbyHint", "Back to the lobby"));
	BackHint->OnClicked = [this]() { HandleBackClicked(); };
	USizeBox* HintSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	HintSize->SetHeightOverride(40.f);
	HintSize->SetWidthOverride(330.f);
	HintSize->SetContent(BackHint);
	HitmenUI::AddToVBox(Corner, HintSize, FMargin(0.f), false, HAlign_Right);
	WaitText = HitmenUI::MakeText(WidgetTree, LOCTEXT("WaitingForHost", "Waiting for the host to go back to the lobby"), 19, TEXT("Italic"), HitmenUI::TextDim());
	HitmenUI::AddToVBox(Corner, WaitText, FMargin(0.f), false, HAlign_Right);
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
