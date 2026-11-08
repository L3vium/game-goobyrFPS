// Hitmen — match HUD: compass (N/S/E/W, VIP / retrieval markers), crosshair (spread ring, alert
// arcs, hit marker, red cross on headshot kill), centred prompt, inventory + keycards, abilities,
// square minimap with alert colours, and the overhead NPC alert indicator.

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "Core/HitmenTypes.h"
#include "HitmenHUDWidgets.generated.h"

class UTextBlock;
class UHorizontalBox;
class UHitmenInventoryComponent;
class UHitmenAbilityComponent;
class UHitmenAlertComponent;
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

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FVector2D GetDesiredSizeForPaint() const { return FVector2D(600.f, 40.f); }
};

/** Crosshair with spread ring, NPC alert arcs and hit markers. */
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

	/** Pixels per degree of weapon spread. */
	UPROPERTY(EditAnywhere, Category="Hitmen|Crosshair")
	float SpreadScale = 8.f;

	float HitMarkerTime = 0.f;
	float KillCrossTime = 0.f;
	bool bLastHitWasKill = false;
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

	UFUNCTION() void HandlePawnChanged(APawn* NewPawn);
	UFUNCTION() void HandlePrompt(const FText& Prompt, bool bVisible);
	UFUNCTION() void HandleInventoryChanged();
	UFUNCTION() void HandleActiveSlotChanged(int32 NewSlot);
	UFUNCTION() void HandleAbilitiesChanged();

	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

	void BindToPawn(APawn* Pawn);
	void UnbindFromPawn();

	UPROPERTY(Transient) TObjectPtr<UHitmenInventoryComponent> BoundInventory;
	UPROPERTY(Transient) TObjectPtr<UHitmenAbilityComponent> BoundAbilities;
};

/** Diamond above an NPC's head, filled by suspicion, coloured by alert state. */
UCLASS()
class HITMEN_API UHitmenAlertIndicatorWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Alert")
	void SetAlertSource(UHitmenAlertComponent* InAlert);

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual void BuildDefaultLayout() override;

	UPROPERTY(Transient)
	TObjectPtr<UHitmenAlertComponent> Alert;
};
