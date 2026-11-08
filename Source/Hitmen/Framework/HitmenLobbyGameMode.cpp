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
#include "Engine/World.h"

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
	GS->SetMatchPhase(EHitmenMatchPhase::Lobby);

	UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>();
	if (GI && GI->bHasPendingMatch)
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
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>(); GS && IsHost(Requester))
	{
		for (AHitmenPlayerState* PS : GS->GetHitmenPlayers())
		{
			PS->SetReady(false);
		}
		GS->SetMatchPhase(EHitmenMatchPhase::Customization);
	}
}

void AHitmenLobbyGameMode::RequestBackToLobby(AHitmenPlayerController* Requester)
{
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>(); GS && IsHost(Requester))
	{
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
	const AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!bAutoLaunchWhenAllReady || !GS || GS->GetMatchPhase() != EHitmenMatchPhase::Customization)
	{
		return;
	}
	for (const AHitmenPlayerState* PS : GS->GetHitmenPlayers())
	{
		if (!PS->IsWatcher() && !PS->IsReady())
		{
			return;
		}
	}
	RequestLaunch(nullptr);
}
