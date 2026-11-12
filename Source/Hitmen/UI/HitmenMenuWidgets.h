// Hitmen — overlays: pause menu (match keeps running) with map + debug grants, emote wheel, spectator HUD with
// multi-view grid, results banner. Options live in HitmenOptionsWidgets.h.

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "Core/HitmenTypes.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "HitmenMenuWidgets.generated.h"

class UButton;
class UTextBlock;
class UVerticalBox;
class UUniformGridPanel;
class UCheckBox;
class USlider;
class UTextureRenderTarget2D;
class APawn;
class UHitmenMapMarkerType;

/** Top-down map: you, the VIP, retrieval points (no other information). */
UCLASS()
class HITMEN_API UHitmenPauseMapWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	/** Legend categories read from the game state: 0 you, 1 teammates, 2 VIP, 3 alerted NPCs. Everything else is a map
	 *  marker (UHitmenMapMarkerComponent) with a legend row per kind. */
	static constexpr int32 CategoryCount = 4;
	static constexpr int32 AlertedCategory = 3;
	static FText GetCategoryName(int32 Category);
	static FLinearColor GetCategoryColor(int32 Category);

	void SetCategoryVisible(int32 Category, bool bVisible) { if (Category >= 0 && Category < CategoryCount) { bCategoryVisible[Category] = bVisible; } }
	bool IsCategoryVisible(int32 Category) const { return Category >= 0 && Category < CategoryCount && bCategoryVisible[Category]; }

	void SetMarkerTypeVisible(UHitmenMapMarkerType* Type, bool bVisible);

protected:
	virtual void NativeConstruct() override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;

	/** UPROPERTY so GC keeps the map texture. */
	UPROPERTY(Transient)
	FSlateBrush MapBrush;
	bool bHasMap = false;
	FBox2D Bounds = FBox2D(FVector2D(-20000.f), FVector2D(20000.f));
	FText MapName;
	bool bCategoryVisible[CategoryCount] = { true, true, true, true };

	/** Marker kinds switched off in the legend. */
	UPROPERTY(Transient)
	TSet<TObjectPtr<UHitmenMapMarkerType>> HiddenMarkerTypes;
	/** 1 = whole map; zooms around the player with the wheel. */
	float Zoom = 1.f;
};

/** Gym / development panel: grant every weapon, ability and keycard. */
UCLASS()
class HITMEN_API UHitmenDebugPanelWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BuildDefaultLayout() override;

	UFUNCTION() void HandleWeaponClicked(int32 Index);
	UFUNCTION() void HandleAbilityClicked(int32 Index);
	UFUNCTION() void HandleKeycardClicked(int32 Index);
	UFUNCTION() void HandleGodClicked(int32 Index);

	bool bGodMode = false;
};

UCLASS()
class HITMEN_API UHitmenPauseMenuWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Pause") void Resume();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Pause") void OpenOptions();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Pause") void LeaveMatch();
	/** Host, match over: everyone goes back to the lobby. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Pause") void BackToLobby();

	/** Legend rows: the built-in categories, then one per map marker kind on this map (rebuilt when they change). */
	void RefreshLegend();

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenPauseMapWidget> Map;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenDebugPanelWidget> DebugPanel;
	UPROPERTY(Transient) TArray<TObjectPtr<class UHitmenToggle>> LegendToggles;
	UPROPERTY(Transient) TObjectPtr<UVerticalBox> LegendBox;
	/** Marker kind of each legend toggle after the built-in categories. */
	UPROPERTY(Transient) TArray<TObjectPtr<UHitmenMapMarkerType>> LegendMarkerTypes;

	UFUNCTION() void HandleLegendToggled(bool bChecked);
	/** Shows / hides the debug tools (they open over the legend). */
	UFUNCTION() void ToggleDebugPanel();
	UPROPERTY(Transient) TObjectPtr<class UHitmenMenuButton> DebugToggleButton;
	/** "Back to the lobby" block, shown to the host once the match is over. */
	UPROPERTY(Transient) TObjectPtr<UWidget> BackToLobbyEntry;
};

/** Hold R: radial emote wheel; the hovered slot plays on release. */
UCLASS()
class HITMEN_API UHitmenEmoteWheelWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	UFUNCTION() void HandleWheelToggled(bool bOpen);

	TArray<FText> GetEmoteNames() const;

	int32 HoveredIndex = INDEX_NONE;
	FVector2D LastCenter = FVector2D::ZeroVector;
};

/** Watcher / dead player HUD: mode, target, and the multi-screen grid. */
UCLASS()
class HITMEN_API UHitmenSpectatorWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;

	UFUNCTION() void HandleModeChanged(uint8 Mode);
	UFUNCTION() void HandleViewsChanged(const TArray<FHitmenSpectatorView>& Views);

	UPROPERTY(Transient) TObjectPtr<UTextBlock> ModeText;
	UPROPERTY(Transient) TObjectPtr<UUniformGridPanel> Grid;

	uint8 CurrentMode = 0;
};

/** Match over: a banner at the top (winner + reason) while everyone spectates; the host gets "[Enter] Back to the
 *  lobby", the others "Waiting for the host". No timer. */
UCLASS()
class HITMEN_API UHitmenResultsWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;

	UFUNCTION() void HandleMatchEnded(const FHitmenMatchResult& Result);
	UFUNCTION() void HandleBackClicked();
	UFUNCTION() void HandleKeyLabelsChanged();

	UPROPERTY(Transient) TObjectPtr<UTextBlock> WinnerText;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> ReasonText;
	UPROPERTY(Transient) TObjectPtr<class UHitmenKeyHint> BackHint;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> WaitText;
};
