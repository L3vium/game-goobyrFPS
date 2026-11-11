// Hitmen — match HUD: compass (N/S/E/W, VIP / retrieval markers), crosshair (spread ring, alert
// arcs, hit marker, red cross on headshot kill), centred prompt, inventory + keycards, abilities,
// square minimap with alert colours, and the overhead NPC alert indicator.

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "Core/HitmenTypes.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "HitmenHUDWidgets.generated.h"

class UTextBlock;
class UHorizontalBox;
class APawn;

/** Heading strip at the top of the screen. */
UCLASS()
class HITMEN_API UHitmenCompassWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	/** Show the VIP direction on the compass (off by default: no waypoint). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Compass")
	bool bShowVIPMarker = false;

	/** Degrees visible across the strip. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Compass")
	float VisibleAngle = 120.f;

	/** Also letter NE / SE / SW / NW (the brief asks for N / S / E / W only). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Compass")
	bool bShowIntercardinals = false;

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FVector2D GetDesiredSizeForPaint() const { return FVector2D(600.f, 40.f); }
};

/**
 * Crosshair: a thin "+" (no spread ring: firing never widens it), a diagonal cross on a kill (red
 * on a headshot), and arrows around it toward NPCs that are onto you.
 */
UCLASS()
class HITMEN_API UHitmenCrosshairWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	void ShowHitMarker(bool bKill, bool bHeadshot);

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleHitMarker(bool bKill, bool bHeadshot);

	UPROPERTY(EditAnywhere, Category="Hitmen|Crosshair")
	float HitMarkerDuration = 0.25f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Crosshair")
	float KillCrossDuration = 0.6f;

	/** Half length of each arm of the cross and the gap at its centre (px). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Crosshair")
	float ArmLength = 9.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Crosshair")
	float ArmGap = 4.f;

	/** Radius of the awareness arcs around the crosshair (px). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Crosshair")
	float ThreatRadius = 74.f;

	/** Angular width of one awareness arc (degrees). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Crosshair", meta=(ClampMin="5", ClampMax="90"))
	float AwarenessArcWidth = 34.f;

	float HitMarkerTime = 0.f;
	float KillCrossTime = 0.f;
	bool bLastHitWasKill = false;
	bool bLastHitHeadshot = false;
	bool bLastKillHeadshot = false;
};

/** Square minimap: you, NPCs coloured by alert, sonar pings. */
UCLASS()
class HITMEN_API UHitmenMinimapWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	/** World radius shown around the player. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Minimap")
	float WorldRadius = 3500.f;

	/** Only NPCs with an alert state are drawn (Hitman-like: no radar for calm NPCs). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Minimap")
	bool bOnlyShowAlertedNPCs = true;

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeConstruct() override;

	/** Baked map of the level (DA_Map MapTexture) and its world bounds. UPROPERTY so GC keeps the texture. */
	UPROPERTY(Transient)
	FSlateBrush MapBrush;
	FBox2D MapBounds = FBox2D(ForceInit);
	bool bHasMap = false;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleSonar(const FHitmenSonarPing& Ping);

	FHitmenSonarPing LastPing;
	float LastPingLocalTime = -100.f;
};

/** Root HUD: composes the parts above and the text elements. */
UCLASS()
class HITMEN_API UHitmenHUDWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenCompassWidget> Compass;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenCrosshairWidget> Crosshair;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenMinimapWidget> Minimap;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> PromptText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> SlotOneText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> SlotTwoText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> KeycardText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> MoveAbilityText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> ManipulationAbilityText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> HealthText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<class UHitmenActionPromptWidget> ActionPrompt;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<class UHitmenDamageVignetteWidget> DamageVignette;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<class UHitmenScopeWidget> Scope;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<class UHitmenInventoryBarWidget> InventoryBar;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<class UHitmenWorldHintWidget> WorldHint;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<class UHitmenIntroWidget> Intro;

	UFUNCTION() void HandlePawnChanged(APawn* NewPawn);
	UFUNCTION() void HandlePrompt(const FText& Prompt, bool bVisible);
	UFUNCTION() void HandleInventoryView(const FHitmenHUDInventoryView& View);
	UFUNCTION() void HandleAbilityViews(const FHitmenHUDAbilityView& Move, const FHitmenHUDAbilityView& Manipulation);
	UFUNCTION() void HandleVitals(const FHitmenHUDVitalsView& View);
};

/** Diamond above an NPC's head, filled by suspicion, coloured by alert state. */
UCLASS()
class HITMEN_API UHitmenAlertIndicatorWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	/** Pushed by the NPC from its alert component events (the widget holds no gameplay reference). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Alert")
	void SetAlertView(EHitmenAlertColor InColor, float InSuspicion, bool bInCallingReinforcements);

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual void BuildDefaultLayout() override;

	EHitmenAlertColor Color = EHitmenAlertColor::None;
	float Suspicion = 0.f;
	bool bCallingReinforcements = false;
};
