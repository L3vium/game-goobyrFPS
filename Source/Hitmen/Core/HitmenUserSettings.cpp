// Hitmen — player options

#include "Core/HitmenUserSettings.h"
#include "Engine/Engine.h"

UHitmenUserSettings* UHitmenUserSettings::GetHitmenUserSettings()
{
	return GEngine ? Cast<UHitmenUserSettings>(GEngine->GetGameUserSettings()) : nullptr;
}

void UHitmenUserSettings::ApplyHitmenSettings()
{
	SaveSettings();
	OnHitmenSettingsChanged.Broadcast();
}

void UHitmenUserSettings::SetToDefaults()
{
	Super::SetToDefaults();
	bToggleCrouch = false;
	bToggleSprint = false;
	bClimbRelativeToCamera = false;
	MouseSensitivity = 0.45f;
	AimSensitivityMultiplier = 1.f;
	HitmenSettingsVersion = CurrentSettingsVersion;
	bInvertY = false;
	FieldOfView = 90.f;
	MasterVolume = 1.f;
	MusicVolume = 0.8f;
	SFXVolume = 1.f;
	VoiceVolume = 1.f;
}

void UHitmenUserSettings::LoadSettings(bool bForceReload)
{
	Super::LoadSettings(bForceReload);
	if (HitmenSettingsVersion < CurrentSettingsVersion)
	{
		// Version 2: lower default sensitivity; aiming compensates the zoom by itself.
		MouseSensitivity = 0.45f;
		AimSensitivityMultiplier = 1.f;
		HitmenSettingsVersion = CurrentSettingsVersion;
		SaveSettings();
	}
}
