// Hitmen — overlays: pause menu (match keeps running) with map + debug grants, options with
// key rebinding, emote wheel, spectator HUD with multi-view grid, results screen

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

/** Top-down map: you, the VIP, retrieval points (no other information). */
UCLASS()
class HITMEN_API UHitmenPauseMapWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
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

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> ResumeButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> OptionsButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> LeaveButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenPauseMapWidget> Map;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenDebugPanelWidget> DebugPanel;
};

/** One rebindable action: name + key button; click then press a key. */
UCLASS()
class HITMEN_API UHitmenKeyBindingRow : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	void Setup(FName InMappingName, const FText& InDisplayName, const FKey& InKey);

protected:
	virtual void BuildDefaultLayout() override;
	virtual FReply NativeOnKeyDown(const FGeometry& InGeometry, const FKeyEvent& InKeyEvent) override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;

	UFUNCTION() void HandleKeyClicked();

	void ApplyKey(const FKey& Key);

	UPROPERTY(Transient) TObjectPtr<UTextBlock> NameText;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> KeyText;
	UPROPERTY(Transient) TObjectPtr<UButton> KeyButton;

	FName MappingName;
	FKey CurrentKey;
	bool bListening = false;
};

UCLASS()
class HITMEN_API UHitmenOptionsWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Options") void Close();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Options") void ResetToDefaults();

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;

	UFUNCTION() void HandleToggleClicked(int32 Index);
	UFUNCTION() void HandleSensitivityChanged(float Value);
	UFUNCTION() void HandleFOVChanged(float Value);
	UFUNCTION() void HandleMasterVolumeChanged(float Value);

	void RefreshToggles();
	void RebuildBindings();

	UPROPERTY(Transient) TObjectPtr<UVerticalBox> ToggleBox;
	UPROPERTY(Transient) TObjectPtr<UVerticalBox> BindingBox;
	UPROPERTY(Transient) TObjectPtr<USlider> SensitivitySlider;
	UPROPERTY(Transient) TObjectPtr<USlider> FOVSlider;
	UPROPERTY(Transient) TObjectPtr<USlider> VolumeSlider;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> CloseButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> ResetButton;
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

/** Winner + reason, shown until the return to the lobby. */
UCLASS()
class HITMEN_API UHitmenResultsWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;

	UFUNCTION() void HandleMatchEnded(const FHitmenMatchResult& Result);

	UPROPERTY(Transient) TObjectPtr<UTextBlock> WinnerText;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> ReasonText;
};
