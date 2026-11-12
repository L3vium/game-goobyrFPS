// Hitmen — lobby game mode

#include "Framework/HitmenLobbyGameMode.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenGameInstance.h"
#include "Core/HitmenGameSettings.h"
#include "Data/HitmenMatchRules.h"
#include "Data/HitmenMapDefinition.h"
#include "Data/HitmenCatalog.h"
#include "Hitmen.h"
#include "Online/HitmenSessionSubsystem.h"
#include "Engine/World.h"
#include "TimerManager.h"

AHitmenLobbyGameMode::AHitmenLobbyGameMode()
{
	DefaultPawnClass = nullptr;
}

void AHitmenLobbyGameMode::InitGameState()
{
	Super::InitGameState();

	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS)
	{
		return;
	}
	UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>();
	const bool bReturningFromMatch = GI && GI->bHasPendingMatch;
	if (const UHitmenSessionSubsystem* Sessions = GI ? GI->GetSubsystem<UHitmenSessionSubsystem>() : nullptr)
	{
		GS->SetRoomCode(Sessions->GetRoomCode());
	}
	// The front end opens on the title unless it is already hosting (back from a match, joined).
	GS->SetMatchPhase(bStartOnTitle && !bReturningFromMatch && GetNetMode() == NM_Standalone ? EHitmenMatchPhase::Title : EHitmenMatchPhase::Lobby);

	if (bReturningFromMatch)
	{
		// Returning from a match: keep teams and options.
		GS->SetLobbySettings(GI->PendingLobbySettings);
		GS->SetTeams(GI->PendingTeams);
		return;
	}

	GS->SetLobbySettings(GetRules()->MakeDefaultLobbySettings());
}

bool AHitmenLobbyGameMode::IsHost(const APlayerController* PC) const
{
	return PC && PC->IsLocalController() && GetWorld()->GetFirstPlayerController() == PC;
}

void AHitmenLobbyGameMode::RequestCreateTeam(AHitmenPlayerController* Requester)
{
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS || !IsHost(Requester))
	{
		return;
	}
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
	FLinearColor Color = FLinearColor::MakeRandomColor();
	if (Catalog && !Catalog->TeamColors.IsEmpty())
	{
		Color = Catalog->TeamColors[TeamColorCursor++ % Catalog->TeamColors.Num()];
	}
	GS->CreateTeam(Color);
}

void AHitmenLobbyGameMode::RequestRemoveTeam(AHitmenPlayerController* Requester, int32 TeamId)
{
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>(); GS && IsHost(Requester))
	{
		GS->RemoveTeam(TeamId);
	}
}

void AHitmenLobbyGameMode::RequestAssignTeam(AHitmenPlayerController* Requester, AHitmenPlayerState* Target, int32 TeamId)
{
	const AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS || !Target || !IsHost(Requester))
	{
		return;
	}
	if (TeamId != INDEX_NONE && !GS->FindTeam(TeamId))
	{
		return;
	}
	Target->SetLobbyTeamId(TeamId);
}

void AHitmenLobbyGameMode::RequestSetTeamRole(AHitmenPlayerController* Requester, int32 TeamId, EHitmenTeamRole NewRole)
{
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>(); GS && IsHost(Requester))
	{
		GS->SetTeamRole(TeamId, NewRole);
	}
}

void AHitmenLobbyGameMode::RequestSetWatcher(AHitmenPlayerController* Requester, AHitmenPlayerState* Target, bool bWatcher)
{
	if (!Target)
	{
		return;
	}
	// Host can switch anyone; a player can switch themselves.
	if (IsHost(Requester) || Requester->PlayerState == Target)
	{
		Target->SetWatcher(bWatcher);
	}
}

void AHitmenLobbyGameMode::RequestSetLobbySettings(AHitmenPlayerController* Requester, const FHitmenLobbySettings& Settings)
{
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS || !IsHost(Requester))
	{
		return;
	}
	// Options hidden by design keep their designed value.
	FHitmenLobbySettings Clamped = GetRules()->SanitizeLobbySettings(Settings);
	Clamped.MapIndex = FMath::Clamp(Clamped.MapIndex, 0, FMath::Max(0, UHitmenGameSettings::Get()->GetMapCount() - 1));
	GS->SetLobbySettings(Clamped);
}

void AHitmenLobbyGameMode::RequestStart(AHitmenPlayerController* Requester)
{
	UE_LOG(LogHitmen, Log, TEXT("Lobby: start requested (host: %s)"), IsHost(Requester) ? TEXT("yes") : TEXT("no"));
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>(); GS && IsHost(Requester))
	{
		bool bAnyAgent = false;
		for (AHitmenPlayerState* PS : GS->GetHitmenPlayers())
		{
			PS->SetReady(false);
			bAnyAgent |= !PS->IsWatcher();
		}
		// Only watchers in the room: nobody has an agent to choose, the match starts at once.
		if (!bAnyAgent)
		{
			RequestLaunch(Requester);
			return;
		}
		GS->SetMatchPhase(EHitmenMatchPhase::Customization);
	}
}

void AHitmenLobbyGameMode::RequestBackToLobby(AHitmenPlayerController* Requester)
{
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>(); GS && IsHost(Requester))
	{
		GetWorldTimerManager().ClearTimer(LaunchTimer);
		GS->SetPhaseCountdown(0.f);
		GS->SetMatchPhase(EHitmenMatchPhase::Lobby);
	}
}

void AHitmenLobbyGameMode::RequestLaunch(AHitmenPlayerController* Requester)
{
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS || (Requester && !IsHost(Requester)))
	{
		return;
	}
	const UHitmenMapDefinition* Map = UHitmenGameSettings::Get()->LoadMap(GS->GetLobbySettings().MapIndex);
	if (!Map || Map->Level.IsNull())
	{
		UE_LOG(LogHitmen, Error, TEXT("Lobby: no level for map index %d"), GS->GetLobbySettings().MapIndex);
		return;
	}
	if (UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>())
	{
		GI->StorePendingMatch(GS->GetLobbySettings(), GS->GetTeams());
	}
	const FString URL = Map->Level.ToSoftObjectPath().GetLongPackageName() + TEXT("?listen");
	GetWorld()->ServerTravel(URL);
}

void AHitmenLobbyGameMode::NotifyReadyChanged()
{
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!bAutoLaunchWhenAllReady || !GS)
	{
		return;
	}
	bool bAllReady = GS->GetMatchPhase() == EHitmenMatchPhase::Customization;
	int32 Players = 0;
	for (const AHitmenPlayerState* PS : GS->GetHitmenPlayers())
	{
		if (!PS->IsWatcher())
		{
			++Players;
			bAllReady &= PS->IsReady();
		}
	}
	bAllReady &= Players > 0;
	int32 ReadyCount = 0;
	for (const AHitmenPlayerState* PS : GS->GetHitmenPlayers())
	{
		ReadyCount += !PS->IsWatcher() && PS->IsReady() ? 1 : 0;
	}
	UE_LOG(LogHitmen, Log, TEXT("Lobby: %d / %d ready (phase %s)%s"), ReadyCount, Players,
		*UEnum::GetValueAsString(GS->GetMatchPhase()), bAllReady ? TEXT(", launching") : TEXT(""));
	if (bAllReady && !GetWorldTimerManager().IsTimerActive(LaunchTimer))
	{
		// Everyone is ready: a short countdown, then the match.
		GS->SetPhaseCountdown(LaunchCountdown);
		GetWorldTimerManager().SetTimer(LaunchTimer, this, &AHitmenLobbyGameMode::HandleLaunchTimer, FMath::Max(0.01f, LaunchCountdown), false);
	}
	else if (!bAllReady && GetWorldTimerManager().IsTimerActive(LaunchTimer))
	{
		GetWorldTimerManager().ClearTimer(LaunchTimer);
		GS->SetPhaseCountdown(0.f);
	}
}

void AHitmenLobbyGameMode::HandleLaunchTimer()
{
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>())
	{
		GS->SetPhaseCountdown(0.f);
	}
	RequestLaunch(nullptr);
}

void AHitmenLobbyGameMode::OpenLobby()
{
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS || GS->GetMatchPhase() != EHitmenMatchPhase::Title)
	{
		return;
	}
	GS->SetLobbySettings(GetRules()->MakeDefaultLobbySettings());
	if (const UHitmenSessionSubsystem* Sessions = GetGameInstance() ? GetGameInstance()->GetSubsystem<UHitmenSessionSubsystem>() : nullptr)
	{
		GS->SetRoomCode(Sessions->GetRoomCode());
	}
	for (AHitmenPlayerState* PS : GS->GetHitmenPlayers())
	{
		PS->SetReady(false);
	}
	GS->SetMatchPhase(EHitmenMatchPhase::Lobby);
}

void AHitmenLobbyGameMode::ReturnToTitlePhase()
{
	GetWorldTimerManager().ClearTimer(LaunchTimer);
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>())
	{
		GS->SetPhaseCountdown(0.f);
		GS->SetTeams(TArray<FHitmenTeam>());
		GS->SetMatchPhase(EHitmenMatchPhase::Title);
	}
}
