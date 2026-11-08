// Hitmen — game instance

#include "Framework/HitmenGameInstance.h"
#include "Core/HitmenGameSettings.h"
#include "Online/HitmenSessionSubsystem.h"
#include "Hitmen.h"
#include "Engine/Engine.h"
#include "Kismet/GameplayStatics.h"
#include "Core/HitmenUserSettings.h"
#include "Sound/SoundMix.h"
#include "Sound/SoundClass.h"
#include "UObject/UObjectGlobals.h"

void UHitmenGameInstance::Init()
{
	Super::Init();
	if (GEngine)
	{
		GEngine->OnNetworkFailure().AddUObject(this, &UHitmenGameInstance::HandleNetworkFailure);
		GEngine->OnTravelFailure().AddUObject(this, &UHitmenGameInstance::HandleTravelFailure);
	}
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->OnHitmenSettingsChanged.AddUniqueDynamic(this, &UHitmenGameInstance::ApplyAudioSettings);
	}
	// Sound mixes live in the world's audio device: push the volumes again after each map load.
	FCoreUObjectDelegates::PostLoadMapWithWorld.AddUObject(this, &UHitmenGameInstance::HandlePostLoadMap);
}

void UHitmenGameInstance::HandlePostLoadMap(UWorld* LoadedWorld)
{
	ApplyAudioSettings();
}

void UHitmenGameInstance::ApplyAudioSettings()
{
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	USoundMix* Mix = VolumeSoundMix.LoadSynchronous();
	UWorld* World = GetWorld();
	if (!Settings || !Mix || !World)
	{
		return;
	}
	auto Override = [&](const TSoftObjectPtr<USoundClass>& Class, float Volume)
	{
		if (USoundClass* Loaded = Class.LoadSynchronous())
		{
			UGameplayStatics::SetSoundMixClassOverride(World, Mix, Loaded, FMath::Clamp(Volume, 0.f, 1.f), 1.f, 0.f, true);
		}
	};
	Override(MasterSoundClass, Settings->MasterVolume);
	Override(MusicSoundClass, Settings->MusicVolume);
	Override(SFXSoundClass, Settings->SFXVolume);
	Override(VoiceSoundClass, Settings->VoiceVolume);
	UGameplayStatics::PushSoundMixModifier(World, Mix);
}

void UHitmenGameInstance::Shutdown()
{
	if (GEngine)
	{
		GEngine->OnNetworkFailure().RemoveAll(this);
		GEngine->OnTravelFailure().RemoveAll(this);
	}
	FCoreUObjectDelegates::PostLoadMapWithWorld.RemoveAll(this);
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->OnHitmenSettingsChanged.RemoveAll(this);
	}
	Super::Shutdown();
}

void UHitmenGameInstance::StorePendingMatch(const FHitmenLobbySettings& Settings, const TArray<FHitmenTeam>& Teams)
{
	PendingLobbySettings = Settings;
	PendingTeams = Teams;
	bHasPendingMatch = true;
}

void UHitmenGameInstance::ClearPendingMatch()
{
	PendingTeams.Reset();
	PendingLobbySettings = FHitmenLobbySettings();
	bHasPendingMatch = false;
}

void UHitmenGameInstance::ReturnToTitle()
{
	ClearPendingMatch();
	if (UHitmenSessionSubsystem* Sessions = GetSubsystem<UHitmenSessionSubsystem>())
	{
		Sessions->DestroySession();
	}
	const TSoftObjectPtr<UWorld>& Title = UHitmenGameSettings::Get()->TitleLevel;
	if (!Title.IsNull())
	{
		UGameplayStatics::OpenLevelBySoftObjectPtr(this, Title, true);
	}
}

void UHitmenGameInstance::OpenGym()
{
	const TSoftObjectPtr<UWorld>& Gym = UHitmenGameSettings::Get()->GymLevel;
	if (!Gym.IsNull())
	{
		UGameplayStatics::OpenLevelBySoftObjectPtr(this, Gym, true, TEXT("listen"));
	}
}

void UHitmenGameInstance::HandleNetworkFailure(UWorld* World, UNetDriver* NetDriver, ENetworkFailure::Type FailureType, const FString& ErrorString)
{
	UE_LOG(LogHitmen, Warning, TEXT("Network failure (%s): %s"), ENetworkFailure::ToString(FailureType), *ErrorString);
	ReturnToTitle();
}

void UHitmenGameInstance::HandleTravelFailure(UWorld* World, ETravelFailure::Type FailureType, const FString& ErrorString)
{
	UE_LOG(LogHitmen, Warning, TEXT("Travel failure (%s): %s"), ETravelFailure::ToString(FailureType), *ErrorString);
	ReturnToTitle();
}
