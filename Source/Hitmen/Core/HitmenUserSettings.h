// Hitmen — player options saved in GameUserSettings.ini (title Options and pause Options)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameUserSettings.h"
#include "HitmenUserSettings.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenUserSettingsChanged);

UCLASS(Config=GameUserSettings, configdonotcheckdefaults)
class HITMEN_API UHitmenUserSettings : public UGameUserSettings
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintPure, Category="Hitmen|Settings", meta=(DisplayName="Get Hitmen User Settings"))
	static UHitmenUserSettings* GetHitmenUserSettings();

	/** Crouch is held by default; true = press to toggle. */
	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Controls")
	bool bToggleCrouch = false;

	/** Sprint is held by default; true = press to toggle. */
	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Controls")
	bool bToggleSprint = false;

	/** Ladder/zipline direction follows the camera pitch instead of forward = up. */
	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Controls")
	bool bClimbRelativeToCamera = false;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Controls")
	float MouseSensitivity = 0.25f;

	/** Extra multiplier while aiming; the zoom itself is already compensated (FOV ratio). */
	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Controls")
	float AimSensitivityMultiplier = 1.f;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Controls")
	bool bInvertY = false;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Camera")
	float FieldOfView = 90.f;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Audio")
	float MasterVolume = 1.f;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Audio")
	float MusicVolume = 0.8f;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Audio")
	float SFXVolume = 1.f;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Audio")
	float VoiceVolume = 1.f;

	/** Name typed by the player; empty = Steam name or a random one. */
	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Profile")
	FString PreferredName;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Profile")
	int32 PreferredIconIndex = 0;

	/** Broadcast after ApplyHitmenSettings so live systems refresh. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Settings")
	FOnHitmenUserSettingsChanged OnHitmenSettingsChanged;

	/** Saves and notifies listeners. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Settings")
	void ApplyHitmenSettings();

	virtual void SetToDefaults() override;
	virtual void LoadSettings(bool bForceReload = false) override;

	/** Saved settings older than this get the new control defaults (sensitivity rebalance). */
	static constexpr int32 CurrentSettingsVersion = 3;

	UPROPERTY(Config)
	int32 HitmenSettingsVersion = 0;
};
