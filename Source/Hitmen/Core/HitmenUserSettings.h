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
	float MouseSensitivity = 1.f;

	UPROPERTY(Config, BlueprintReadWrite, Category="Hitmen|Controls")
	float AimSensitivityMultiplier = 0.7f;

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
};
