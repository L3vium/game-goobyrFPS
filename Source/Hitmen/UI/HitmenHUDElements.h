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
	/** What the focused thing is (a line under the prompt, or alone when it cannot be used). */
	FString Info;
	float InfoAlpha = 0.f;
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

/** Bottom centre: what nearby NPCs say (Hitman-style subtitles), the newest line at the bottom. */
UCLASS()
class HITMEN_API UHitmenSubtitlesWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleBark(const FHitmenBarkView& Bark);

	UPROPERTY(EditAnywhere, Category="Hitmen|Subtitles", meta=(ClampMin="1"))
	int32 MaxLines = 2;

	UPROPERTY(EditAnywhere, Category="Hitmen|Subtitles", meta=(ClampMin="0.01", Units="s"))
	float FadeTime = 0.25f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Subtitles", meta=(ClampMin="8"))
	int32 FontSize = 19;

	struct FSubtitleLine
	{
		FString Speaker;
		FString Text;
		FLinearColor Color = FLinearColor::White;
		float Age = 0.f;
		float Duration = 3.f;
	};
	TArray<FSubtitleLine> Lines;
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
 * Bottom bar: [held weapon][other weapon] | [A ability][E ability]. Weapon cards show a name and the ammo; switching
 * weapons makes the two cards trade places. Ability cards show the key that triggers them.
 */
/**
 * Vehicles and the bottom-right stack: the vehicle's health bar at the top, its reload ring around the crosshair,
 * keycards stacked bottom right and "[F] Exit" under them while you drive (nothing overlaps the vehicle UI).
 */
UCLASS()
class HITMEN_API UHitmenStatusCornerWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION()
	void HandleInventory(const FHitmenHUDInventoryView& View);

	TArray<int32> Keycards;
};

/** Right, centred vertically: the F1-F8 cheats and which are on (gym always, other maps in dev builds). */
UCLASS()
class HITMEN_API UHitmenCheatPanelWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
};

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

	/** UPROPERTY: the views carry icon textures. */
	UPROPERTY(Transient) FHitmenHUDInventoryView Inventory;
	UPROPERTY(Transient) FHitmenHUDAbilityView MoveAbility;
	UPROPERTY(Transient) FHitmenHUDAbilityView ManipulationAbility;
	int32 ActiveSlot = 0;

	/** The held weapon is always the left card: switching makes the two cards trade places (0 = just switched, 1 =
	 *  settled). */
	float SwapAlpha = 1.f;
	int32 LastActiveSlot = -1;

	/** Seconds the two weapon cards take to trade places. */
	UPROPERTY(EditAnywhere, Category="Hitmen|HUD", meta=(ClampMin="0.01", Units="s"))
	float SwapTime = 0.12f;
};

/** Match intro: "dropping in" and the map name while players parachute in. */
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
