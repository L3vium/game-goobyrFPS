// Hitmen — HUD elements

#include "UI/HitmenHUDElements.h"
#include "Data/HitmenMapDefinition.h"
#include "Framework/HitmenGameState.h"
#include "UI/HitmenUIFactory.h"
#include "Data/HitmenSoundSet.h"
#include "Blueprint/WidgetLayoutLibrary.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/Pawn.h"
#include "Rendering/DrawElements.h"
#include "Engine/World.h"
#include "UI/HitmenMenuKit.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"

namespace
{
	/** Splits "Open  [Locked - Keycard 1]" into the action and the bracketed reason. */
	void SplitPrompt(const FString& Full, FString& OutAction, FString& OutReason)
	{
		int32 Bracket = INDEX_NONE;
		if (Full.FindChar(TEXT('['), Bracket))
		{
			OutAction = Full.Left(Bracket).TrimStartAndEnd();
			OutReason = Full.Mid(Bracket).TrimStartAndEnd();
		}
		else
		{
			OutAction = Full.TrimStartAndEnd();
			OutReason.Reset();
		}
	}
}

// =============================================================================================
// Action prompt
// =============================================================================================

void UHitmenActionPromptWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnInteractionPromptChanged.AddUniqueDynamic(this, &UHitmenActionPromptWidget::HandlePrompt);
		UI->OnTraversalHintChanged.AddUniqueDynamic(this, &UHitmenActionPromptWidget::HandleTraversal);
		HandleTraversal(UI->GetTraversalHint());
	}
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenActionPromptWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnInteractionPromptChanged.RemoveAll(this);
		UI->OnTraversalHintChanged.RemoveAll(this);
	}
}

void UHitmenActionPromptWidget::HandlePrompt(const FText& Prompt, bool bVisible)
{
	bPromptVisible = bVisible && !Prompt.IsEmpty();
	if (bPromptVisible)
	{
		SplitPrompt(Prompt.ToString(), Action, Reason);
	}
}

void UHitmenActionPromptWidget::HandleTraversal(const FText& Hint)
{
	if (!Hint.IsEmpty())
	{
		Traversal = Hint.ToString();
	}
}

void UHitmenActionPromptWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	PromptAlpha = FMath::FInterpConstantTo(PromptAlpha, bPromptVisible ? 1.f : 0.f, InDeltaTime, FadeSpeed);
	const UHitmenUIEventSubsystem* UI = GetUIEvents();
	const bool bTraversal = UI && !UI->GetTraversalHint().IsEmpty() && !bPromptVisible;
	TraversalAlpha = FMath::FInterpConstantTo(TraversalAlpha, bTraversal ? 1.f : 0.f, InDeltaTime, FadeSpeed);
}

int32 UHitmenActionPromptWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	auto PaintRow = [&](const FString& Key, const FString& Label, const FString& Extra, float Y, float Alpha)
	{
		const FSlateFontInfo LabelFont = HitmenUI::SpacedFont(19, TEXT("SemiBold"), 60);
		const FSlateFontInfo ExtraFont = HitmenUI::SpacedFont(17, TEXT("Italic"), 30);
		const FString Upper = Label.ToUpper();
		const FVector2D LabelSize = HitmenUI::MeasureText(Upper, LabelFont);
		const FVector2D ExtraSize = Extra.IsEmpty() ? FVector2D::ZeroVector : HitmenUI::MeasureText(Extra, ExtraFont);
		const float KeyWidth = 30.f;
		const float Total = KeyWidth + 10.f + LabelSize.X + (Extra.IsEmpty() ? 0.f : 12.f + ExtraSize.X);
		float X = (Size.X - Total) * 0.5f;
		// Soft backing so the prompt reads on bright walls.
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(X - 40.f, Y - 6.f), FVector2D(Total * 0.5f + 40.f, 42.f), FLinearColor(0, 0, 0, 0), FLinearColor(0, 0, 0, 0.45f * Alpha), true);
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(X + Total * 0.5f, Y - 6.f), FVector2D(Total * 0.5f + 40.f, 42.f), FLinearColor(0, 0, 0, 0.45f * Alpha), FLinearColor(0, 0, 0, 0), true);
		const FVector2D KeySize = HitmenUI::PaintKey(OutDrawElements, LayerId + 1, AllottedGeometry, Key, FVector2D(X, Y), 17, Alpha);
		X += KeySize.X + 10.f;
		HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Upper, FVector2D(X, Y + (KeySize.Y - LabelSize.Y) * 0.5f), LabelFont, HitmenUI::Text() * FLinearColor(1, 1, 1, Alpha));
		if (!Extra.IsEmpty())
		{
			X += LabelSize.X + 12.f;
			HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Extra, FVector2D(X, Y + (KeySize.Y - ExtraSize.Y) * 0.5f), ExtraFont, HitmenUI::Accent() * FLinearColor(1.2f, 1.2f, 1.2f, Alpha));
		}
	};
	if (PromptAlpha > 0.01f && !Action.IsEmpty())
	{
		PaintRow(TEXT("F"), Action, Reason, 0.f, PromptAlpha);
	}
	if (TraversalAlpha > 0.01f && !Traversal.IsEmpty())
	{
		PaintRow(TEXT("SPACE"), Traversal, FString(), 0.f, TraversalAlpha * 0.85f);
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Damage vignette
// =============================================================================================

void UHitmenDamageVignetteWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnDamageTaken.AddUniqueDynamic(this, &UHitmenDamageVignetteWidget::HandleDamage);
		UI->OnVitalsChanged.AddUniqueDynamic(this, &UHitmenDamageVignetteWidget::HandleVitals);
		UI->OnPawnChanged.AddUniqueDynamic(this, &UHitmenDamageVignetteWidget::HandlePawnChanged);
		HandleVitals(UI->GetVitals());
	}
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenDamageVignetteWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnDamageTaken.RemoveAll(this);
		UI->OnVitalsChanged.RemoveAll(this);
		UI->OnPawnChanged.RemoveAll(this);
	}
}

void UHitmenDamageVignetteWidget::HandleDamage(float DirectionYaw)
{
	Pulse = 1.f;
	LastDamageYaw = DirectionYaw;
}

void UHitmenDamageVignetteWidget::HandleVitals(const FHitmenHUDVitalsView& View)
{
	HealthRatio = View.HealthNormalized;
}

void UHitmenDamageVignetteWidget::HandlePawnChanged(APawn* NewPawn)
{
	// A new body starts healthy.
	Pulse = 0.f;
	HealthRatio = 1.f;
}

void UHitmenDamageVignetteWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	Pulse = FMath::Max(0.f, Pulse - InDeltaTime * PulseDecay);
	const AHitmenCharacterBase* Character = GetOwningPlayer() ? Cast<AHitmenCharacterBase>(GetOwningPlayer()->GetPawn()) : nullptr;
	const bool bConcealed = Character && Character->GetStatusEffects() && Character->GetStatusEffects()->HasStatus(EHitmenStatusEffect::Concealed);
	HiddenAlpha = FMath::FInterpTo(HiddenAlpha, bConcealed ? 1.f : 0.f, InDeltaTime, 6.f);
	const float TargetLow = HealthRatio < LowHealthThreshold && HealthRatio > 0.f ? 1.f - HealthRatio / LowHealthThreshold : 0.f;
	LowHealth = FMath::FInterpTo(LowHealth, TargetLow, InDeltaTime, 3.f);
	if (TargetLow > 0.f)
	{
		HeartbeatTimer -= InDeltaTime;
		if (HeartbeatTimer <= 0.f)
		{
			HeartbeatTimer = FMath::Lerp(HeartbeatInterval, HeartbeatInterval * 0.6f, TargetLow);
			UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::Heartbeat);
		}
	}
	else
	{
		HeartbeatTimer = 0.f;
	}
}

int32 UHitmenDamageVignetteWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	// Low health breathes; hits flash on top.
	const float Breath = LowHealth > 0.f ? 0.75f + 0.25f * FMath::Sin(GetWorld() ? GetWorld()->GetTimeSeconds() * 5.f : 0.f) : 0.f;
	const float Strength = FMath::Clamp(Pulse * 0.8f + LowHealth * 0.65f * Breath, 0.f, 1.f);
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	if (HiddenAlpha > 0.01f)
	{
		const FLinearColor Dark = HiddenVignetteColor * FLinearColor(1.f, 1.f, 1.f, HiddenAlpha);
		const FLinearColor DarkClear(HiddenVignetteColor.R, HiddenVignetteColor.G, HiddenVignetteColor.B, 0.f);
		const float HideX = Size.X * 0.26f;
		const float HideY = Size.Y * 0.3f;
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, FVector2D(HideX, Size.Y), Dark, DarkClear, true);
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(Size.X - HideX, 0.f), FVector2D(HideX, Size.Y), DarkClear, Dark, true);
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, FVector2D(Size.X, HideY), Dark, DarkClear, false);
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(0.f, Size.Y - HideY), FVector2D(Size.X, HideY), DarkClear, Dark, false);
		UHitmenIconButton::PaintGlyph(OutDrawElements, LayerId + 1, AllottedGeometry, EHitmenIconGlyph::EyeOff, FVector2D(Size.X * 0.5f, Size.Y * 0.5f + 70.f), 12.f,
			FLinearColor(1.f, 1.f, 1.f, 0.7f * HiddenAlpha), 2.f);
	}
	if (Strength <= 0.01f)
	{
		return HiddenAlpha > 0.01f ? Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled) : LayerId;
	}
	const FLinearColor Red(0.55f, 0.f, 0.f, 0.85f * Strength);
	const FLinearColor Clear(0.55f, 0.f, 0.f, 0.f);
	const float EdgeX = Size.X * (0.18f + 0.1f * Strength);
	const float EdgeY = Size.Y * (0.18f + 0.1f * Strength);
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, FVector2D(EdgeX, Size.Y), Red, Clear, true);
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(Size.X - EdgeX, 0.f), FVector2D(EdgeX, Size.Y), Clear, Red, true);
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, FVector2D(Size.X, EdgeY), Red, Clear, false);
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D(0.f, Size.Y - EdgeY), FVector2D(Size.X, EdgeY), Clear, Red, false);
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 1, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Scope
// =============================================================================================

void UHitmenScopeWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnScopeToggled.AddUniqueDynamic(this, &UHitmenScopeWidget::HandleScope);
		HandleScope(UI->IsScoped());
	}
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenScopeWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnScopeToggled.RemoveAll(this);
	}
}

void UHitmenScopeWidget::HandleScope(bool bScoped)
{
	bScopedNow = bScoped;
}

void UHitmenScopeWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	Alpha = FMath::FInterpConstantTo(Alpha, bScopedNow ? 1.f : 0.f, InDeltaTime, 7.f);
	SwayTime += InDeltaTime;
}

int32 UHitmenScopeWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	if (Alpha <= 0.01f)
	{
		return LayerId;
	}
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	// Slight breathing sway of the whole scope.
	const FVector2D Sway(FMath::Sin(SwayTime * 0.9f) * 3.f, FMath::Sin(SwayTime * 1.3f) * 2.f);
	const FVector2D Center = Size * 0.5f + Sway;
	const float Radius = Size.Y * 0.44f;
	const FLinearColor Black(0.f, 0.f, 0.f, Alpha);
	// Outside the lens: black (sides as boxes, the ring as thick arcs).
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(-10.f, -10.f), FVector2D(Center.X - Radius + 12.f, Size.Y + 20.f), Black);
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(Center.X + Radius - 2.f, -10.f), FVector2D(Size.X - Center.X - Radius + 12.f, Size.Y + 20.f), Black);
	const float Outer = FMath::Max(Size.X, Size.Y);
	for (float R = Radius + 8.f; R < Outer; R += 22.f)
	{
		HitmenUI::PaintArc(OutDrawElements, LayerId, AllottedGeometry, Center, R, 0.f, 360.f, Black, 26.f, 96);
	}
	// Lens rim and inner vignette.
	HitmenUI::PaintArc(OutDrawElements, LayerId + 1, AllottedGeometry, Center, Radius, 0.f, 360.f, FLinearColor(0.f, 0.f, 0.f, Alpha), 6.f, 128);
	for (int32 Ring = 0; Ring < 6; ++Ring)
	{
		HitmenUI::PaintArc(OutDrawElements, LayerId + 1, AllottedGeometry, Center, Radius - 4.f - Ring * 8.f, 0.f, 360.f, FLinearColor(0.f, 0.f, 0.f, Alpha * (0.35f - Ring * 0.055f)), 9.f, 96);
	}
	// Reticle: thin cross with thick outer posts and range dots.
	const FLinearColor Ink(0.02f, 0.02f, 0.02f, 0.95f * Alpha);
	HitmenUI::PaintLine(OutDrawElements, LayerId + 2, AllottedGeometry, Center - FVector2D(Radius, 0.f), Center - FVector2D(Radius * 0.35f, 0.f), Ink, 5.f);
	HitmenUI::PaintLine(OutDrawElements, LayerId + 2, AllottedGeometry, Center + FVector2D(Radius * 0.35f, 0.f), Center + FVector2D(Radius, 0.f), Ink, 5.f);
	HitmenUI::PaintLine(OutDrawElements, LayerId + 2, AllottedGeometry, Center + FVector2D(0.f, Radius * 0.35f), Center + FVector2D(0.f, Radius), Ink, 5.f);
	HitmenUI::PaintLine(OutDrawElements, LayerId + 2, AllottedGeometry, Center - FVector2D(Radius * 0.35f, 0.f), Center + FVector2D(Radius * 0.35f, 0.f), Ink, 1.2f);
	HitmenUI::PaintLine(OutDrawElements, LayerId + 2, AllottedGeometry, Center - FVector2D(0.f, Radius * 0.35f), Center + FVector2D(0.f, Radius * 0.35f), Ink, 1.2f);
	for (int32 Dot = 1; Dot <= 4; ++Dot)
	{
		const float Offset = Radius * 0.07f * Dot;
		HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, Center + FVector2D(-2.f, Offset - 2.f), FVector2D(4.f), Ink);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, Center + FVector2D(Offset - 2.f, -2.f), FVector2D(4.f), Ink);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, Center + FVector2D(-Offset - 2.f, -2.f), FVector2D(4.f), Ink);
	}
	HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, Center - FVector2D(1.5f), FVector2D(3.f), FLinearColor(0.9f, 0.05f, 0.05f, Alpha));
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Inventory bar
// =============================================================================================

void UHitmenInventoryBarWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnInventoryViewChanged.AddUniqueDynamic(this, &UHitmenInventoryBarWidget::HandleInventory);
		UI->OnAbilityViewsChanged.AddUniqueDynamic(this, &UHitmenInventoryBarWidget::HandleAbilities);
		UI->OnVitalsChanged.AddUniqueDynamic(this, &UHitmenInventoryBarWidget::HandleVitals);
		HandleInventory(UI->GetInventoryView());
		HandleAbilities(UI->GetMoveAbilityView(), UI->GetManipulationAbilityView());
		HandleVitals(UI->GetVitals());
	}
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenInventoryBarWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnInventoryViewChanged.RemoveAll(this);
		UI->OnAbilityViewsChanged.RemoveAll(this);
		UI->OnVitalsChanged.RemoveAll(this);
	}
}

void UHitmenInventoryBarWidget::HandleInventory(const FHitmenHUDInventoryView& View)
{
	int32 NewActive = ActiveSlot;
	for (int32 Index = 0; Index < View.Slots.Num(); ++Index)
	{
		if (View.Slots[Index].bActive)
		{
			NewActive = Index;
		}
	}
	if (NewActive != ActiveSlot)
	{
		// Cards slide toward the newly drawn weapon.
		SwitchDirection = NewActive > ActiveSlot ? 1.f : -1.f;
		SwitchSlide = 1.f;
		ActiveSlot = NewActive;
	}
	Inventory = View;
}

void UHitmenInventoryBarWidget::HandleAbilities(const FHitmenHUDAbilityView& Move, const FHitmenHUDAbilityView& Manipulation)
{
	MoveAbility = Move;
	ManipulationAbility = Manipulation;
}

void UHitmenInventoryBarWidget::HandleVitals(const FHitmenHUDVitalsView& View)
{
	HealthRatio = View.HealthNormalized;
}

void UHitmenInventoryBarWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	SwitchSlide = FMath::FInterpTo(SwitchSlide, 0.f, InDeltaTime, 10.f);
}

int32 UHitmenInventoryBarWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const FVector2D WeaponCard(230.f, 66.f);
	const FVector2D AbilityCard(196.f, 66.f);
	const float Gap = 8.f;
	const float DividerGap = 26.f;
	const float Total = WeaponCard.X * 2.f + Gap + DividerGap * 2.f + AbilityCard.X * 2.f + Gap;
	float X = (Size.X - Total) * 0.5f;
	const float Y = Size.Y - WeaponCard.Y;
	const FSlateFontInfo NameFont = HitmenUI::SpacedFont(20, TEXT("SemiBold"), 50);
	const FSlateFontInfo SubFont = HitmenUI::Font(16, TEXT("Italic"));
	const FSlateFontInfo SmallFont = HitmenUI::SpacedFont(13, TEXT("Medium"), 120);

	auto PaintCardBase = [&](FVector2D Position, FVector2D CardSize, bool bActive, float Alpha)
	{
		HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, Position, CardSize,
			FLinearColor(0.f, 0.f, 0.f, (bActive ? 0.72f : 0.5f) * Alpha), FLinearColor(0.f, 0.f, 0.f, (bActive ? 0.45f : 0.28f) * Alpha), true);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, Position, FVector2D(CardSize.X, bActive ? 3.f : 1.f),
			bActive ? HitmenUI::Text() * FLinearColor(1, 1, 1, Alpha) : HitmenUI::Hairline() * FLinearColor(1, 1, 1, Alpha));
	};

	// ---- Weapon cards: the drawn weapon first, sliding in after a switch ----
	const float Slide = SwitchSlide * SwitchDirection * (WeaponCard.X + Gap) * 0.5f;
	for (int32 Order = 0; Order < 2; ++Order)
	{
		const int32 SlotIndex = Order == 0 ? ActiveSlot : 1 - ActiveSlot;
		const bool bActive = Order == 0;
		const FVector2D Position(X + Order * (WeaponCard.X + Gap) + (bActive ? Slide : -Slide), Y + (bActive ? 0.f : 8.f));
		const FVector2D CardSize = bActive ? WeaponCard : FVector2D(WeaponCard.X, WeaponCard.Y - 8.f);
		const float Alpha = bActive ? 1.f : 0.7f;
		PaintCardBase(Position, CardSize, bActive, Alpha);
		const bool bValid = Inventory.Slots.IsValidIndex(SlotIndex) && !Inventory.Slots[SlotIndex].bEmpty;
		const FHitmenHUDSlotView* View = bValid ? &Inventory.Slots[SlotIndex] : nullptr;
		const FString Name = View ? View->Name.ToString().ToUpper() : (SlotIndex == 0 && !bValid ? FString(TEXT("FISTS")) : FString(TEXT("EMPTY")));
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Name, Position + FVector2D(14.f, 9.f), NameFont,
			(View || SlotIndex == 0 ? HitmenUI::Text() : HitmenUI::TextDim()) * FLinearColor(1, 1, 1, Alpha));
		FString Sub;
		if (View)
		{
			Sub = View->Ammo >= 0 ? FString::Printf(TEXT("%d rounds"), View->Ammo) : FString(TEXT("melee"));
		}
		else if (SlotIndex == 0)
		{
			Sub = TEXT("punch");
		}
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Sub, Position + FVector2D(14.f, 36.f), SubFont,
			(View && View->Ammo == 0 ? HitmenUI::Accent() : HitmenUI::TextDim()) * FLinearColor(1, 1, 1, Alpha));
	}
	X += WeaponCard.X * 2.f + Gap + DividerGap;

	// ---- Divider ----
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(X - DividerGap * 0.5f, Y + 10.f), FVector2D(1.f, WeaponCard.Y - 20.f), HitmenUI::Hairline());

	// ---- Ability cards: [A] manipulation, [E] move ----
	auto PaintAbility = [&](const FHitmenHUDAbilityView& View, const FString& Key, float CardX)
	{
		const FVector2D Position(CardX, Y);
		PaintCardBase(Position, AbilityCard, false, View.bValid ? 1.f : 0.6f);
		const FVector2D KeySize = HitmenUI::PaintKey(OutDrawElements, LayerId + 2, AllottedGeometry, Key, Position + FVector2D(12.f, 14.f), 15, View.bValid ? 1.f : 0.4f);
		const FString Name = View.bValid ? View.Name.ToString().ToUpper() : FString(TEXT("NONE"));
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Name, Position + FVector2D(22.f + KeySize.X, 9.f), NameFont,
			View.bValid ? HitmenUI::Text() : HitmenUI::TextDim());
		FString Sub;
		if (View.bValid)
		{
			Sub = View.CooldownRemaining > 0.f ? FString::Printf(TEXT("%.1fs"), View.CooldownRemaining)
				: (View.MaxCharges > 0 ? FString::Printf(TEXT("%d / %d"), View.Charges, View.MaxCharges) : FString(TEXT("ready")));
		}
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Sub, Position + FVector2D(22.f + KeySize.X, 36.f), SubFont, HitmenUI::TextDim());
		// Cooldown bar along the bottom edge.
		if (View.bValid && View.CooldownRemaining > 0.f)
		{
			HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, Position + FVector2D(0.f, AbilityCard.Y - 3.f), FVector2D(AbilityCard.X, 3.f), FLinearColor(1, 1, 1, 0.12f));
		}
	};
	PaintAbility(ManipulationAbility, TEXT("A"), X);
	PaintAbility(MoveAbility, TEXT("E"), X + AbilityCard.X + Gap);

	// ---- Keycards: chips above the weapon cards ----
	float ChipX = (Size.X - Total) * 0.5f;
	for (const int32 Card : Inventory.Keycards)
	{
		const FString Label = FString::Printf(TEXT("KEYCARD %d"), Card);
		const FVector2D LabelSize = HitmenUI::MeasureText(Label, SmallFont);
		const FVector2D ChipSize(LabelSize.X + 22.f, 22.f);
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D(ChipX, Y - 30.f), ChipSize, FLinearColor(0.08f, 0.32f, 0.85f, 0.85f));
		HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Label, FVector2D(ChipX + 11.f, Y - 30.f + (22.f - LabelSize.Y) * 0.5f), SmallFont, HitmenUI::Text());
		ChipX += ChipSize.X + 6.f;
	}

	// ---- Health: a thin line above the ability cards, only when hurt ----
	if (HealthRatio < 0.999f)
	{
		const FVector2D BarPos(X, Y - 12.f);
		const float Width = AbilityCard.X * 2.f + Gap;
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, BarPos, FVector2D(Width, 4.f), FLinearColor(1, 1, 1, 0.12f));
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, BarPos, FVector2D(Width * FMath::Clamp(HealthRatio, 0.f, 1.f), 4.f),
			HealthRatio < 0.35f ? HitmenUI::Accent() : HitmenUI::Text());
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 3, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// World hint
// =============================================================================================

void UHitmenWorldHintWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnAbilityHintChanged.AddUniqueDynamic(this, &UHitmenWorldHintWidget::HandleHint);
		HandleHint(UI->GetAbilityHint());
	}
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenWorldHintWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnAbilityHintChanged.RemoveAll(this);
	}
}

void UHitmenWorldHintWidget::HandleHint(const FHitmenWorldHint& InHint)
{
	Hint = InHint;
}

int32 UHitmenWorldHintWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const AActor* Target = Hint.Target.Get();
	APlayerController* PC = GetOwningPlayer();
	if (!Target || !PC)
	{
		return LayerId;
	}
	// Anchor under the target's bounds.
	FVector Origin, Extent;
	Target->GetActorBounds(true, Origin, Extent);
	FVector2D Screen;
	if (!UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, Origin - FVector(0.f, 0.f, Extent.Z), Screen, true))
	{
		return LayerId;
	}
	const FSlateFontInfo LabelFont = HitmenUI::SpacedFont(17, TEXT("SemiBold"), 60);
	const FString Label = Hint.Action.ToString().ToUpper();
	const FVector2D LabelSize = HitmenUI::MeasureText(Label, LabelFont);
	const float Total = 30.f + 8.f + LabelSize.X;
	const FVector2D Position(Screen.X - Total * 0.5f, Screen.Y + 10.f);
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, Position - FVector2D(8.f, 5.f), FVector2D(Total + 16.f, 36.f), FLinearColor(0.f, 0.f, 0.f, 0.55f));
	const FVector2D KeySize = HitmenUI::PaintKey(OutDrawElements, LayerId + 1, AllottedGeometry, Hint.Key.ToString(), Position, 15);
	HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Label, Position + FVector2D(KeySize.X + 8.f, (KeySize.Y - LabelSize.Y) * 0.5f), LabelFont,
		FLinearColor(0.55f, 0.85f, 1.f, 1.f));
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Intro countdown
// =============================================================================================

void UHitmenIntroWidget::NativeConstruct()
{
	Super::NativeConstruct();
	const UHitmenMapDefinition* Map = HitmenUI::FindCurrentMap(this);
	MapName = Map ? Map->DisplayName : FText::GetEmpty();
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenIntroWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	const bool bIntro = GS && GS->GetMatchPhase() == EHitmenMatchPhase::Intro;
	Alpha = FMath::FInterpConstantTo(Alpha, bIntro ? 1.f : 0.f, InDeltaTime, 2.5f);
	// No countdown and no sting: the drop just shows the map name.
}

int32 UHitmenIntroWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	if (Alpha <= 0.01f)
	{
		return LayerId;
	}
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	HitmenUI::PaintGradient(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, FVector2D(Size.X, Size.Y * 0.4f), FLinearColor(0, 0, 0, 0.75f * Alpha), FLinearColor(0, 0, 0, 0), false);
	const FSlateFontInfo Overline = HitmenUI::SpacedFont(18, TEXT("Medium"), 300);
	const FSlateFontInfo Title = HitmenUI::SpacedFont(64, TEXT("Bold"), 40);
	const FString Over = TEXT("DROPPING IN");
	const FString Name = MapName.ToString().ToUpper();
	const FVector2D OverSize = HitmenUI::MeasureText(Over, Overline);
	const FVector2D NameSize = HitmenUI::MeasureText(Name, Title);
	const float Y = Size.Y * 0.08f;
	HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Over, FVector2D((Size.X - OverSize.X) * 0.5f, Y), Overline, HitmenUI::TextDim() * FLinearColor(1, 1, 1, Alpha));
	HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Name, FVector2D((Size.X - NameSize.X) * 0.5f, Y + 26.f), Title, HitmenUI::Text() * FLinearColor(1, 1, 1, Alpha));
	HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(Size.X * 0.5f - 50.f, Y + 26.f + NameSize.Y + 6.f), FVector2D(100.f, 4.f), HitmenUI::Accent() * FLinearColor(1, 1, 1, Alpha));
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}
