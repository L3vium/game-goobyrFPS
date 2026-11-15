// Hitmen — creates the root widgets of a level and shows/hides overlays from UI events

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/HUD.h"
#include "Core/HitmenTypes.h"
#include "HitmenHUD.generated.h"

class UUserWidget;

/**
 * One BP_HUD_* per game mode sets the widget classes (title menu, lobby, match HUD...).
 * Widgets are created once at BeginPlay and toggled by UHitmenUIEventSubsystem events.
 */
UCLASS()
class HITMEN_API AHitmenHUD : public AHUD
{
	GENERATED_BODY()

public:
	/** Seconds the front-end camera takes to fly to a screen's shot (the agent page's arrows wait for it). */
	float GetShotBlendTime() const { return ShotBlendTime; }

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	UFUNCTION(BlueprintPure, Category="Hitmen|HUD")
	UUserWidget* GetPrimaryWidget() const { return PrimaryWidget; }

	/** Front-end HUD (title, lobby): the cursor is always shown and gameplay keys are ignored. */
	UFUNCTION(BlueprintPure, Category="Hitmen|HUD")
	bool IsMenuHUD() const { return bPrimaryIsMenu; }

	/** Single owner of the input mode: menu HUD, pause, options, emote wheel, results. */
	void RefreshInputMode();

protected:
	/** Always-on widget: title menu, lobby screen or match HUD. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	TSubclassOf<UUserWidget> PrimaryWidgetClass;

	/** Shows the mouse cursor while the primary widget is displayed (menus). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	bool bPrimaryIsMenu = false;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	TSubclassOf<UUserWidget> PauseMenuClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	TSubclassOf<UUserWidget> OptionsClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	TSubclassOf<UUserWidget> EmoteWheelClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	TSubclassOf<UUserWidget> SpectatorClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	TSubclassOf<UUserWidget> ResultsClass;

	/** Toasts, the event camera and the cinematic bars (match HUDs; the C++ default when unset). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD")
	TSubclassOf<UUserWidget> MatchEventsClass;

	UPROPERTY(Transient) TObjectPtr<UUserWidget> PrimaryWidget;
	UPROPERTY(Transient) TObjectPtr<UUserWidget> PauseMenuWidget;
	UPROPERTY(Transient) TObjectPtr<UUserWidget> OptionsWidget;
	UPROPERTY(Transient) TObjectPtr<UUserWidget> EmoteWheelWidget;
	UPROPERTY(Transient) TObjectPtr<UUserWidget> SpectatorWidget;
	UPROPERTY(Transient) TObjectPtr<UUserWidget> ResultsWidget;
	UPROPERTY(Transient) TObjectPtr<UUserWidget> MatchEventsWidget;

	UFUNCTION() void HandlePauseToggled(bool bOpen);
	UFUNCTION() void HandleOptionsToggled(bool bOpen);
	UFUNCTION() void HandleEmoteWheelToggled(bool bOpen);
	UFUNCTION() void HandleSpectatorMode(uint8 Mode);
	UFUNCTION() void HandleMatchEnded(const struct FHitmenMatchResult& Result);
	UFUNCTION() void HandlePhaseChanged(EHitmenMatchPhase Phase);

	/** Front end: blends the view to the camera tagged "Shot_<Phase>" (Shot_Title, Shot_Lobby...). */
	void ApplyCameraShot(EHitmenMatchPhase Phase, bool bBlend);

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD", meta=(ClampMin="0", Units="s"))
	float ShotBlendTime = 1.2f;

	UUserWidget* CreateHidden(TSubclassOf<UUserWidget> Class, int32 ZOrder);
	void SetShown(UUserWidget* Widget, bool bShown);
};
