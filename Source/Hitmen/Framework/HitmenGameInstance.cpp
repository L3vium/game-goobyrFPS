// Hitmen — game instance

#include "Framework/HitmenGameInstance.h"
#include "Engine/World.h"
#include "Framework/HitmenLobbyGameMode.h"
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
	// Packaged builds (Shipping and Dev) never print engine messages on screen (shadow map warnings and the like).
	if (!GIsEditor)
	{
		GAreScreenMessagesEnabled = false;
	}
	if (GEngine)
	{
		GEngine->OnNetworkFailure().AddUObject(this, &UHitmenGameInstance::HandleNetworkFailure);
		GEngine->OnTravelFailure().AddUObject(this, &UHitmenGameInstance::HandleTravelFailure);
	}
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->OnHitmenSettingsChanged.AddUniqueDynamic(this, &UHitmenGameInstance::ApplyAudioSettings);
		// First launch of a packaged game: pick the graphics preset for this machine (Options can run it again).
		if (!GIsEditor && !Settings->bAutoDetectDone)
		{
			Settings->RunAutoDetect();
		}
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
	// Host in the front end: stop listening and go back to the title shot, no reload.
	UWorld* World = GetWorld();
	if (AHitmenLobbyGameMode* LobbyMode = World ? World->GetAuthGameMode<AHitmenLobbyGameMode>() : nullptr)
	{
		if (World->GetNetDriver())
		{
			GEngine->ShutdownWorldNetDriver(World);
		}
		LobbyMode->ReturnToTitlePhase();
		return;
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
	// GEngine broadcasts every world's failures: with several game instances in one process (PIE players) another
	// player's dropped connection must not close this player's room.
	if (World && World->GetGameInstance() != this)
	{
		return;
	}
	UE_LOG(LogHitmen, Warning, TEXT("Network failure (%s): %s"), ENetworkFailure::ToString(FailureType), *ErrorString);
	ReturnToTitle();
}

void UHitmenGameInstance::HandleTravelFailure(UWorld* World, ETravelFailure::Type FailureType, const FString& ErrorString)
{
	if (World && World->GetGameInstance() != this)
	{
		return;
	}
	UE_LOG(LogHitmen, Warning, TEXT("Travel failure (%s): %s"), ETravelFailure::ToString(FailureType), *ErrorString);
	ReturnToTitle();
}
