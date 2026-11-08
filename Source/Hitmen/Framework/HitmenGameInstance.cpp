// Hitmen — game instance

#include "Framework/HitmenGameInstance.h"
#include "Core/HitmenGameSettings.h"
#include "Online/HitmenSessionSubsystem.h"
#include "Hitmen.h"
#include "Engine/Engine.h"
#include "Kismet/GameplayStatics.h"

void UHitmenGameInstance::Init()
{
	Super::Init();
	if (GEngine)
	{
		GEngine->OnNetworkFailure().AddUObject(this, &UHitmenGameInstance::HandleNetworkFailure);
		GEngine->OnTravelFailure().AddUObject(this, &UHitmenGameInstance::HandleTravelFailure);
	}
}

void UHitmenGameInstance::Shutdown()
{
	if (GEngine)
	{
		GEngine->OnNetworkFailure().RemoveAll(this);
		GEngine->OnTravelFailure().RemoveAll(this);
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
