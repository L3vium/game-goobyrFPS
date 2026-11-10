// Hitmen — HUD elements painted in code: action prompt, damage vignette, sniper scope, inventory
// bar (weapon / ability cards), world-anchored hints

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "HitmenHUDElements.generated.h"

/**
 * Under the crosshair: "[F] Open" (key in a box, action, an optional bracketed reason dimmed),
 * and below it the traversal hint "[SPACE] Vault". Fades in / out.
 */
UCLASS()
class HITMEN_API UHitmenActionPromptWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandlePrompt(const FText& Prompt, bool bVisible);
	UFUNCTION() void HandleTraversal(const FText& Hint);

	/** Fade speed (alpha per second). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Prompt")
	float FadeSpeed = 9.f;

	FString Action;
	FString Reason;
	FString Traversal;
	float PromptAlpha = 0.f;
	float TraversalAlpha = 0.f;
	bool bPromptVisible = false;
};

/** Red edges when hurt: a pulse on every hit, a steady glow (and heartbeat) when health is low.
 * Dark edges and an eye-off mark while concealed (tall grass). */
UCLASS()
class HITMEN_API UHitmenDamageVignetteWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleDamage(float DirectionYaw);
	UFUNCTION() void HandleVitals(const FHitmenHUDVitalsView& View);
	UFUNCTION() void HandlePawnChanged(APawn* NewPawn);

	/** Below this health ratio the edges stay red and the heart beats. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Damage")
	float LowHealthThreshold = 0.35f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Damage")
	float PulseDecay = 1.6f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Damage")
	float HeartbeatInterval = 1.1f;

	/** Hidden in tall grass (or any concealment): a dark soft vignette and an eye-off mark. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Damage")
	FLinearColor HiddenVignetteColor = FLinearColor(0.01f, 0.03f, 0.015f, 0.8f);

	float Pulse = 0.f;
	float LowHealth = 0.f;
	float HiddenAlpha = 0.f;
	float HealthRatio = 1.f;
	float HeartbeatTimer = 0.f;
	float LastDamageYaw = 0.f;
};

/** Full-screen sniper scope (circle, reticle, vignette) while scoped. */
UCLASS()
class HITMEN_API UHitmenScopeWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleScope(bool bScoped);

	float Alpha = 0.f;
	bool bScopedNow = false;
	float SwayTime = 0.f;
};

/**
 * Bottom bar: [weapon 1][weapon 2] | [A ability][E ability]. Weapon cards show a name and the
 * ammo in dimmed italics; switching weapons slides the cards. Keycards sit above as chips.
 */
UCLASS()
class HITMEN_API UHitmenInventoryBarWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleInventory(const FHitmenHUDInventoryView& View);
	UFUNCTION() void HandleAbilities(const FHitmenHUDAbilityView& Move, const FHitmenHUDAbilityView& Manipulation);
	UFUNCTION() void HandleVitals(const FHitmenHUDVitalsView& View);

	FHitmenHUDInventoryView Inventory;
	FHitmenHUDAbilityView MoveAbility;
	FHitmenHUDAbilityView ManipulationAbility;
	float HealthRatio = 1.f;
	int32 ActiveSlot = 0;
	/** 1 right after a switch, eases to 0 (cards slide into place). */
	float SwitchSlide = 0.f;
	float SwitchDirection = 1.f;
};

/** Match intro: map name, "dropping in" and the countdown while players parachute in. */
UCLASS()
class HITMEN_API UHitmenIntroWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void NativeConstruct() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	FText MapName;
	float Alpha = 0.f;
	int32 LastSecond = -1;
};

/** World-anchored hint: "[A] Charm" under the remote-ability target, following it on screen. */
UCLASS()
class HITMEN_API UHitmenWorldHintWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleHint(const FHitmenWorldHint& InHint);

	FHitmenWorldHint Hint;
};
