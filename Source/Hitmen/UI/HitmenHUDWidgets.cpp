// Hitmen — match HUD

#include "UI/HitmenHUDWidgets.h"
#include "UI/HitmenUIFactory.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Framework/HitmenGameState.h"
#include "Characters/HitmenCharacterBase.h"  // GameState::GetVIP() type only
#include "GameFramework/Pawn.h"
#include "Blueprint/WidgetTree.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/SizeBox.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/HorizontalBox.h"
#include "Components/Border.h"
#include "Rendering/DrawElements.h"
#include "Styling/CoreStyle.h"
#include "GameFramework/PlayerController.h"
#include "Engine/World.h"

#define LOCTEXT_NAMESPACE "HitmenHUD"

namespace
{
	void DrawLine(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D A, FVector2D B, const FLinearColor& Color, float Thickness = 2.f)
	{
		TArray<FVector2D> Points = { A, B };
		FSlateDrawElement::MakeLines(Out, Layer, Geo.ToPaintGeometry(), Points, ESlateDrawEffect::None, Color, true, Thickness);
	}

	void DrawArc(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D Center, float Radius, float StartDeg, float EndDeg, const FLinearColor& Color, float Thickness, int32 Segments = 12)
	{
		TArray<FVector2D> Points;
		for (int32 Index = 0; Index <= Segments; ++Index)
		{
			const float Angle = FMath::DegreesToRadians(FMath::Lerp(StartDeg, EndDeg, static_cast<float>(Index) / Segments));
			Points.Add(Center + FVector2D(FMath::Sin(Angle), -FMath::Cos(Angle)) * Radius);
		}
		FSlateDrawElement::MakeLines(Out, Layer, Geo.ToPaintGeometry(), Points, ESlateDrawEffect::None, Color, true, Thickness);
	}

	void DrawText(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, const FString& Text, FVector2D Position, int32 Size, const FLinearColor& Color)
	{
		FSlateDrawElement::MakeText(Out, Layer, Geo.ToPaintGeometry(FVector2f(200.f, 40.f), FSlateLayoutTransform(FVector2f(Position))), Text, HitmenUI::Font(Size), ESlateDrawEffect::None, Color);
	}

	void DrawBox(FSlateWindowElementList& Out, int32 Layer, const FGeometry& Geo, FVector2D Position, FVector2D Size, const FLinearColor& Color)
	{
		FSlateDrawElement::MakeBox(Out, Layer, Geo.ToPaintGeometry(FVector2f(Size), FSlateLayoutTransform(FVector2f(Position))), FCoreStyle::Get().GetBrush("WhiteBrush"), ESlateDrawEffect::None, Color);
	}

	/** Yaw of a world direction relative to the view (degrees, 0 = ahead, clockwise). */
	float RelativeYaw(const FVector& Direction, float ViewYaw)
	{
		return FRotator::NormalizeAxis(Direction.Rotation().Yaw - ViewYaw);
	}
}

// =============================================================================================
// Compass
// =============================================================================================

int32 UHitmenCompassWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const APlayerController* PC = GetOwningPlayer();
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	if (!PC)
	{
		return LayerId;
	}
	const float Heading = PC->GetControlRotation().Yaw;
	const float HalfAngle = VisibleAngle * 0.5f;
	const float CenterX = Size.X * 0.5f;

	auto ToX = [&](float WorldYaw, bool& bVisible)
	{
		const float Delta = FRotator::NormalizeAxis(WorldYaw - Heading);
		bVisible = FMath::Abs(Delta) <= HalfAngle;
		return CenterX + Delta / HalfAngle * CenterX;
	};

	// Ticks and cardinal letters (north = +X world).
	static const TCHAR* Letters[] = { TEXT("N"), TEXT("NE"), TEXT("E"), TEXT("SE"), TEXT("S"), TEXT("SW"), TEXT("W"), TEXT("NW") };
	for (int32 Step = 0; Step < 24; ++Step)
	{
		const float Yaw = Step * 15.f;
		bool bVisible = false;
		const float X = ToX(Yaw, bVisible);
		if (!bVisible)
		{
			continue;
		}
		const bool bCardinal = Step % 3 == 0;
		const bool bMainCardinal = Step % 6 == 0;
		DrawLine(OutDrawElements, LayerId, AllottedGeometry, FVector2D(X, Size.Y - (bCardinal ? 12.f : 6.f)), FVector2D(X, Size.Y), HitmenUI::Text() * FLinearColor(1.f, 1.f, 1.f, 0.6f), 1.5f);
		if (bCardinal && (bMainCardinal || bShowIntercardinals))
		{
			const TCHAR* Letter = Letters[(Step / 3) % 8];
			DrawText(OutDrawElements, LayerId + 1, AllottedGeometry, Letter, FVector2D(X - 8.f, 0.f), Step % 6 == 0 ? 16 : 11, HitmenUI::Text());
		}
	}

	// Markers.
	const APawn* Pawn = PC->GetPawn();
	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	if (Pawn && GS)
	{
		const UHitmenUIEventSubsystem* UI = GetUIEvents();
		if (UI && UI->IsCarryingVIP())
		{
			for (const AActor* Point : GS->GetActiveRetrievalPoints())
			{
				bool bVisible = false;
				const float X = ToX((Point->GetActorLocation() - Pawn->GetActorLocation()).Rotation().Yaw, bVisible);
				if (bVisible)
				{
					DrawBox(OutDrawElements, LayerId + 2, AllottedGeometry, FVector2D(X - 4.f, Size.Y - 20.f), FVector2D(8.f, 8.f), FLinearColor(1.f, 0.78f, 0.1f));
				}
			}
		}
		else if (bShowVIPMarker && GS->GetVIP())
		{
			bool bVisible = false;
			const float X = ToX((GS->GetVIP()->GetActorLocation() - Pawn->GetActorLocation()).Rotation().Yaw, bVisible);
			if (bVisible)
			{
				const FLinearColor Color = GS->IsVIPDead() ? FLinearColor(1.f, 0.78f, 0.1f) : HitmenUI::Accent();
				DrawBox(OutDrawElements, LayerId + 2, AllottedGeometry, FVector2D(X - 4.f, Size.Y - 20.f), FVector2D(8.f, 8.f), Color);
			}
		}
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Crosshair
// =============================================================================================

void UHitmenCrosshairWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnHitMarker.AddUniqueDynamic(this, &UHitmenCrosshairWidget::HandleHitMarker);
	}
}

void UHitmenCrosshairWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnHitMarker.RemoveAll(this);
	}
}

void UHitmenCrosshairWidget::HandleHitMarker(bool bKill, bool bHeadshot)
{
	ShowHitMarker(bKill, bHeadshot);
}

void UHitmenCrosshairWidget::ShowHitMarker(bool bKill, bool bHeadshot)
{
	HitMarkerTime = HitMarkerDuration;
	bLastHitWasKill = bKill;
	if (bKill && bHeadshot)
	{
		KillCrossTime = KillCrossDuration;
	}
}

void UHitmenCrosshairWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HitMarkerTime = FMath::Max(0.f, HitMarkerTime - InDeltaTime);
	KillCrossTime = FMath::Max(0.f, KillCrossTime - InDeltaTime);
}

int32 UHitmenCrosshairWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Center = AllottedGeometry.GetLocalSize() * 0.5f;
	const APlayerController* PC = GetOwningPlayer();
	const APawn* Pawn = PC ? PC->GetPawn() : nullptr;
	const FLinearColor White = HitmenUI::Text();

	// Centre dot.
	DrawBox(OutDrawElements, LayerId, AllottedGeometry, Center - FVector2D(1.5f), FVector2D(3.f), White);

	// Ring sized by weapon spread.
	const UHitmenUIEventSubsystem* UI = GetUIEvents();
	const float Spread = UI ? UI->GetCrosshairSpread() : 1.f;
	const float RingRadius = 10.f + Spread * SpreadScale;
	DrawArc(OutDrawElements, LayerId, AllottedGeometry, Center, RingRadius, 0.f, 360.f, White * FLinearColor(1.f, 1.f, 1.f, 0.55f), 1.5f, 32);

	// Alert arcs: NPCs aware of you, placed toward them.
	if (Pawn && PC && UI)
	{
		const float ViewYaw = PC->GetControlRotation().Yaw;
		for (const FHitmenHUDAlertBlip& Blip : UI->GetAlertBlips())
		{
			if (Blip.Color == EHitmenAlertColor::None || !Blip.bTargetsViewer)
			{
				continue;
			}
			const float Yaw = RelativeYaw(Blip.Location - Pawn->GetActorLocation(), ViewYaw);
			FLinearColor Tint = HitmenAlert::ToLinearColor(Blip.Color);
			if (Blip.Color == EHitmenAlertColor::White)
			{
				Tint.A = FMath::Lerp(0.3f, 1.f, Blip.Suspicion);
			}
			DrawArc(OutDrawElements, LayerId + 1, AllottedGeometry, Center, RingRadius + 26.f, Yaw - 14.f, Yaw + 14.f, Tint, 4.f);
		}
	}

	// Hit marker.
	if (HitMarkerTime > 0.f)
	{
		const float Alpha = HitMarkerTime / HitMarkerDuration;
		const FLinearColor Color = (bLastHitWasKill ? HitmenUI::Accent() : White) * FLinearColor(1.f, 1.f, 1.f, Alpha);
		for (const FVector2D& Dir : { FVector2D(1, 1), FVector2D(-1, 1), FVector2D(1, -1), FVector2D(-1, -1) })
		{
			DrawLine(OutDrawElements, LayerId + 2, AllottedGeometry, Center + Dir * 8.f, Center + Dir * 15.f, Color, 2.f);
		}
	}
	// Big red cross on a headshot kill.
	if (KillCrossTime > 0.f)
	{
		const float Alpha = KillCrossTime / KillCrossDuration;
		const FLinearColor Red = HitmenUI::Accent() * FLinearColor(1.f, 1.f, 1.f, Alpha);
		DrawLine(OutDrawElements, LayerId + 3, AllottedGeometry, Center + FVector2D(-24.f, -24.f), Center + FVector2D(24.f, 24.f), Red, 5.f);
		DrawLine(OutDrawElements, LayerId + 3, AllottedGeometry, Center + FVector2D(24.f, -24.f), Center + FVector2D(-24.f, 24.f), Red, 5.f);
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Minimap
// =============================================================================================

void UHitmenMinimapWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnSonarPing.AddUniqueDynamic(this, &UHitmenMinimapWidget::HandleSonar);
	}
}

void UHitmenMinimapWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnSonarPing.RemoveAll(this);
	}
}

void UHitmenMinimapWidget::HandleSonar(const FHitmenSonarPing& Ping)
{
	LastPing = Ping;
	LastPingLocalTime = GetWorld() ? GetWorld()->GetTimeSeconds() : 0.f;
}

int32 UHitmenMinimapWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const FVector2D Center = Size * 0.5f;
	DrawBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, HitmenUI::Background());
	const FLinearColor Frame = HitmenUI::Text() * FLinearColor(1.f, 1.f, 1.f, 0.35f);
	DrawLine(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(0, 0), FVector2D(Size.X, 0), Frame, 1.f);
	DrawLine(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(Size.X, 0), Size, Frame, 1.f);
	DrawLine(OutDrawElements, LayerId + 1, AllottedGeometry, Size, FVector2D(0, Size.Y), Frame, 1.f);
	DrawLine(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(0, Size.Y), FVector2D(0, 0), Frame, 1.f);

	const APlayerController* PC = GetOwningPlayer();
	const APawn* Pawn = PC ? PC->GetPawn() : nullptr;
	if (!Pawn)
	{
		return LayerId + 2;
	}
	const FVector Origin = Pawn->GetActorLocation();
	const float ViewYaw = PC->GetControlRotation().Yaw;
	const float Scale = (Size.X * 0.5f) / WorldRadius;

	// Rotating map: up = where you look.
	auto ToMap = [&](const FVector& World, bool& bInside)
	{
		const FVector Delta = World - Origin;
		const FVector Rotated = Delta.RotateAngleAxis(-ViewYaw, FVector::UpVector);
		const FVector2D Point = Center + FVector2D(Rotated.Y, -Rotated.X) * Scale;
		bInside = Point.X >= 0.f && Point.Y >= 0.f && Point.X <= Size.X && Point.Y <= Size.Y;
		return Point;
	};

	if (const UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		for (const FHitmenHUDAlertBlip& Blip : UI->GetAlertBlips())
		{
			if (bOnlyShowAlertedNPCs && Blip.Color == EHitmenAlertColor::None)
			{
				continue;
			}
			bool bInside = false;
			const FVector2D Point = ToMap(Blip.Location, bInside);
			if (bInside)
			{
				const FLinearColor Tint = Blip.Color == EHitmenAlertColor::None ? HitmenUI::TextDim() : HitmenAlert::ToLinearColor(Blip.Color);
				DrawBox(OutDrawElements, LayerId + 2, AllottedGeometry, Point - FVector2D(3.f), FVector2D(6.f), Tint);
			}
		}
	}

	// Sonar ping of the carried VIP body.
	if (LastPing.IsValid() && LastPing.NextInterval > 0.f)
	{
		const float Age = GetWorld()->GetTimeSeconds() - LastPingLocalTime;
		const float Alpha = 1.f - FMath::Clamp(Age / LastPing.NextInterval, 0.f, 1.f);
		bool bInside = false;
		const FVector2D Point = ToMap(LastPing.Location, bInside);
		if (bInside && Alpha > 0.f)
		{
			DrawArc(OutDrawElements, LayerId + 3, AllottedGeometry, Point, 6.f + (1.f - Alpha) * 18.f, 0.f, 360.f, HitmenUI::Accent() * FLinearColor(1.f, 1.f, 1.f, Alpha), 2.f, 20);
		}
	}

	// You: arrow pointing up.
	const FLinearColor Me = HitmenUI::Text();
	DrawLine(OutDrawElements, LayerId + 4, AllottedGeometry, Center + FVector2D(0.f, -8.f), Center + FVector2D(-6.f, 6.f), Me, 2.f);
	DrawLine(OutDrawElements, LayerId + 4, AllottedGeometry, Center + FVector2D(0.f, -8.f), Center + FVector2D(6.f, 6.f), Me, 2.f);
	DrawLine(OutDrawElements, LayerId + 4, AllottedGeometry, Center + FVector2D(-6.f, 6.f), Center + FVector2D(6.f, 6.f), Me, 2.f);
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 5, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// HUD root
// =============================================================================================

void UHitmenHUDWidget::BuildDefaultLayout()
{
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;

	Compass = WidgetTree->ConstructWidget<UHitmenCompassWidget>(UHitmenCompassWidget::StaticClass());
	HitmenUI::AddToCanvas(Canvas, Compass, FAnchors(0.5f, 0.f), FVector2D(0.f, 20.f), FVector2D(0.5f, 0.f), FVector2D(640.f, 44.f));

	Crosshair = WidgetTree->ConstructWidget<UHitmenCrosshairWidget>(UHitmenCrosshairWidget::StaticClass());
	Crosshair->SetVisibility(ESlateVisibility::HitTestInvisible);
	HitmenUI::AddToCanvas(Canvas, Crosshair, FAnchors(0.5f, 0.5f), FVector2D::ZeroVector, FVector2D(0.5f, 0.5f), FVector2D(220.f, 220.f));

	PromptText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 20);
	PromptText->SetJustification(ETextJustify::Center);
	HitmenUI::AddToCanvas(Canvas, PromptText, FAnchors(0.5f, 0.5f), FVector2D(0.f, 70.f), FVector2D(0.5f, 0.f));

	// Inventory, bottom centre; keycards next to it.
	UBorder* InventoryPanel = HitmenUI::MakePanel(WidgetTree, HitmenUI::Panel(), FMargin(14.f, 8.f));
	UHorizontalBox* InventoryRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	SlotOneText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 16);
	SlotTwoText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 16);
	KeycardText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 14, TEXT("Regular"), HitmenUI::TextDim());
	HitmenUI::AddToHBox(InventoryRow, SlotOneText, FMargin(0.f, 0.f, 22.f, 0.f));
	HitmenUI::AddToHBox(InventoryRow, SlotTwoText, FMargin(0.f, 0.f, 22.f, 0.f));
	HitmenUI::AddToHBox(InventoryRow, KeycardText);
	InventoryPanel->SetContent(InventoryRow);
	HitmenUI::AddToCanvas(Canvas, InventoryPanel, FAnchors(0.5f, 1.f), FVector2D(0.f, -24.f), FVector2D(0.5f, 1.f));

	// Square minimap, bottom left.
	const float MinimapSize = 220.f;
	const float Margin = 24.f;
	Minimap = WidgetTree->ConstructWidget<UHitmenMinimapWidget>(UHitmenMinimapWidget::StaticClass());
	Minimap->SetVisibility(ESlateVisibility::HitTestInvisible);
	HitmenUI::AddToCanvas(Canvas, Minimap, FAnchors(0.f, 1.f), FVector2D(Margin, -Margin), FVector2D(0.f, 1.f), FVector2D(MinimapSize, MinimapSize));

	// Abilities and health, bottom right (the minimap owns the bottom left).
	UVerticalBox* Right = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	MoveAbilityText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 15);
	ManipulationAbilityText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 15);
	HealthText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 15, TEXT("Regular"), HitmenUI::TextDim());
	MoveAbilityText->SetJustification(ETextJustify::Right);
	ManipulationAbilityText->SetJustification(ETextJustify::Right);
	HealthText->SetJustification(ETextJustify::Right);
	HitmenUI::AddToVBox(Right, MoveAbilityText, FMargin(0.f), false, HAlign_Right);
	HitmenUI::AddToVBox(Right, ManipulationAbilityText, FMargin(0.f), false, HAlign_Right);
	HitmenUI::AddToVBox(Right, HealthText, FMargin(0.f), false, HAlign_Right);
	HitmenUI::AddToCanvas(Canvas, Right, FAnchors(1.f, 1.f), FVector2D(-Margin - 4.f, -Margin), FVector2D(1.f, 1.f));
}

void UHitmenHUDWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnPawnChanged.AddUniqueDynamic(this, &UHitmenHUDWidget::HandlePawnChanged);
		UI->OnInteractionPromptChanged.AddUniqueDynamic(this, &UHitmenHUDWidget::HandlePrompt);
		UI->OnInventoryViewChanged.AddUniqueDynamic(this, &UHitmenHUDWidget::HandleInventoryView);
		UI->OnAbilityViewsChanged.AddUniqueDynamic(this, &UHitmenHUDWidget::HandleAbilityViews);
		UI->OnVitalsChanged.AddUniqueDynamic(this, &UHitmenHUDWidget::HandleVitals);
		// Catch up with views published before this widget existed.
		HandleInventoryView(UI->GetInventoryView());
		HandleAbilityViews(UI->GetMoveAbilityView(), UI->GetManipulationAbilityView());
		HandleVitals(UI->GetVitals());
	}
	HandlePawnChanged(GetOwningPlayerPawn());
}

void UHitmenHUDWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnPawnChanged.RemoveAll(this);
		UI->OnInteractionPromptChanged.RemoveAll(this);
		UI->OnInventoryViewChanged.RemoveAll(this);
		UI->OnAbilityViewsChanged.RemoveAll(this);
		UI->OnVitalsChanged.RemoveAll(this);
	}
}

void UHitmenHUDWidget::HandlePawnChanged(APawn* NewPawn)
{
	SetVisibility(NewPawn ? ESlateVisibility::SelfHitTestInvisible : ESlateVisibility::Collapsed);
}

void UHitmenHUDWidget::HandlePrompt(const FText& Prompt, bool bVisible)
{
	if (PromptText)
	{
		PromptText->SetText(bVisible ? FText::Format(LOCTEXT("Prompt", "[F]  {0}"), Prompt) : FText::GetEmpty());
	}
}

void UHitmenHUDWidget::HandleInventoryView(const FHitmenHUDInventoryView& View)
{
	auto SlotLabel = [&View](int32 Index) -> FText
	{
		if (!View.Slots.IsValidIndex(Index) || View.Slots[Index].bEmpty)
		{
			return FText::Format(LOCTEXT("EmptySlot", "{0}  -"), FText::AsNumber(Index + 1));
		}
		const FHitmenHUDSlotView& SlotView = View.Slots[Index];
		const FText Ammo = SlotView.Ammo >= 0 ? FText::AsNumber(SlotView.Ammo) : FText::FromString(TEXT("-"));
		const FText Marker = SlotView.bActive ? FText::FromString(TEXT("> ")) : FText::GetEmpty();
		return FText::Format(LOCTEXT("Slot", "{0}{1}  {2}  {3}"), Marker, FText::AsNumber(Index + 1), SlotView.Name, Ammo);
	};
	if (SlotOneText) { SlotOneText->SetText(SlotLabel(0)); }
	if (SlotTwoText) { SlotTwoText->SetText(SlotLabel(1)); }
	if (KeycardText)
	{
		FString Cards;
		for (const int32 Card : View.Keycards)
		{
			Cards += FString::Printf(TEXT(" [K%d]"), Card);
		}
		KeycardText->SetText(FText::FromString(Cards));
	}
}

void UHitmenHUDWidget::HandleAbilityViews(const FHitmenHUDAbilityView& Move, const FHitmenHUDAbilityView& Manipulation)
{
	auto AbilityLabel = [](const FHitmenHUDAbilityView& View, const TCHAR* Key) -> FText
	{
		if (!View.bValid)
		{
			return FText::FromString(FString::Printf(TEXT("[%s]  -"), Key));
		}
		FString Text = FString::Printf(TEXT("[%s]  %s"), Key, *View.Name.ToString());
		if (View.MaxCharges > 0)
		{
			Text += FString::Printf(TEXT("  x%d"), View.Charges);
		}
		if (View.CooldownRemaining > 0.f)
		{
			Text += FString::Printf(TEXT("  %.1fs"), View.CooldownRemaining);
		}
		return FText::FromString(Text);
	};
	if (MoveAbilityText) { MoveAbilityText->SetText(AbilityLabel(Move, TEXT("E"))); }
	if (ManipulationAbilityText) { ManipulationAbilityText->SetText(AbilityLabel(Manipulation, TEXT("A"))); }
}

void UHitmenHUDWidget::HandleVitals(const FHitmenHUDVitalsView& View)
{
	if (!HealthText)
	{
		return;
	}
	// Health only shows when hurt (minimal HUD).
	if (View.HealthNormalized < 0.999f)
	{
		HealthText->SetText(FText::Format(LOCTEXT("Health", "HEALTH {0}  ARMOR {1}"), FText::AsNumber(FMath::RoundToInt(View.Health)), FText::AsNumber(FMath::RoundToInt(View.Armor))));
	}
	else
	{
		HealthText->SetText(FText::GetEmpty());
	}
}

// =============================================================================================
// Alert indicator
// =============================================================================================

void UHitmenAlertIndicatorWidget::BuildDefaultLayout()
{
	USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	Size->SetWidthOverride(28.f);
	Size->SetHeightOverride(28.f);
	WidgetTree->RootWidget = Size;
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenAlertIndicatorWidget::SetAlertView(EHitmenAlertColor InColor, float InSuspicion, bool bInCallingReinforcements)
{
	Color = InColor;
	Suspicion = FMath::Clamp(InSuspicion, 0.f, 1.f);
	bCallingReinforcements = bInCallingReinforcements;
}

int32 UHitmenAlertIndicatorWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	if (Color == EHitmenAlertColor::None)
	{
		return LayerId;
	}
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const FVector2D C = Size * 0.5f;
	const float R = FMath::Min(Size.X, Size.Y) * 0.45f;
	const FLinearColor Tint = HitmenAlert::ToLinearColor(Color);
	const TArray<FVector2D> Diamond = { C + FVector2D(0, -R), C + FVector2D(R, 0), C + FVector2D(0, R), C + FVector2D(-R, 0), C + FVector2D(0, -R) };
	FSlateDrawElement::MakeLines(OutDrawElements, LayerId, AllottedGeometry.ToPaintGeometry(), Diamond, ESlateDrawEffect::None, Tint, true, 2.5f);

	// Fill from the bottom by suspicion (white) or fully (yellow / red).
	const float Fill = Color == EHitmenAlertColor::White ? Suspicion : 1.f;
	const int32 Lines = 10;
	for (int32 Index = 0; Index < Lines; ++Index)
	{
		const float T = (Index + 0.5f) / Lines;
		if (T > Fill)
		{
			break;
		}
		const float Y = C.Y + R - T * 2.f * R;
		const float HalfWidth = R - FMath::Abs(Y - C.Y);
		DrawLine(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(C.X - HalfWidth, Y), FVector2D(C.X + HalfWidth, Y), Tint * FLinearColor(1.f, 1.f, 1.f, 0.8f), 2.f);
	}
	if (bCallingReinforcements)
	{
		DrawText(OutDrawElements, LayerId + 2, AllottedGeometry, TEXT("!"), FVector2D(C.X + R + 2.f, C.Y - R), 14, HitmenUI::Accent());
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

#undef LOCTEXT_NAMESPACE
