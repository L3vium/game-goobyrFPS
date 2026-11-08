// Hitmen — player state

#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenGameState.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

AHitmenPlayerState::AHitmenPlayerState()
{
	SetNetUpdateFrequency(10.f);
}

void AHitmenPlayerState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenPlayerState, LobbyTeamId);
	DOREPLIFETIME(AHitmenPlayerState, bIsWatcher);
	DOREPLIFETIME(AHitmenPlayerState, IconIndex);
	DOREPLIFETIME(AHitmenPlayerState, Cosmetics);
	DOREPLIFETIME(AHitmenPlayerState, Loadout);
	DOREPLIFETIME(AHitmenPlayerState, bIsAlive);
	DOREPLIFETIME(AHitmenPlayerState, bIsReady);
	DOREPLIFETIME(AHitmenPlayerState, Kills);
}

void AHitmenPlayerState::CopyProperties(APlayerState* PlayerState)
{
	Super::CopyProperties(PlayerState);
	if (AHitmenPlayerState* Target = Cast<AHitmenPlayerState>(PlayerState))
	{
		Target->LobbyTeamId = LobbyTeamId;
		Target->bIsWatcher = bIsWatcher;
		Target->IconIndex = IconIndex;
		Target->Cosmetics = Cosmetics;
		Target->Loadout = Loadout;
		// Fresh match: alive, not ready, no kills.
		Target->bIsAlive = true;
		Target->bIsReady = false;
		Target->Kills = 0;
	}
}

void AHitmenPlayerState::OnRep_PlayerName()
{
	Super::OnRep_PlayerName();
	NotifyInfoChanged();
}

int32 AHitmenPlayerState::GetHitmenTeamId_Implementation() const
{
	if (bIsWatcher)
	{
		return HitmenTeams::Neutral;
	}
	return LobbyTeamId != INDEX_NONE ? LobbyTeamId : HitmenTeams::SoloBase + GetPlayerId();
}

EHitmenTeamRole AHitmenPlayerState::GetTeamRole() const
{
	if (const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr)
	{
		if (const FHitmenTeam* Team = GS->FindTeam(LobbyTeamId))
		{
			return Team->Role;
		}
	}
	return EHitmenTeamRole::Attack;
}

void AHitmenPlayerState::SetLobbyTeamId(int32 NewTeamId)
{
	LobbyTeamId = NewTeamId;
	if (NewTeamId != INDEX_NONE)
	{
		bIsWatcher = false;
	}
	NotifyInfoChanged();
}

void AHitmenPlayerState::SetWatcher(bool bNewWatcher)
{
	// Only players without a team can watch.
	bIsWatcher = bNewWatcher && LobbyTeamId == INDEX_NONE;
	NotifyInfoChanged();
}

void AHitmenPlayerState::SetIconIndex(int32 NewIndex)
{
	IconIndex = FMath::Max(0, NewIndex);
	NotifyInfoChanged();
}

void AHitmenPlayerState::SetCosmetics(const FHitmenCosmeticLoadout& NewCosmetics)
{
	Cosmetics = NewCosmetics;
	NotifyInfoChanged();
}

void AHitmenPlayerState::SetLoadout(const FHitmenLoadout& NewLoadout)
{
	Loadout = NewLoadout;
	NotifyInfoChanged();
}

void AHitmenPlayerState::SetAlive(bool bNewAlive)
{
	bIsAlive = bNewAlive;
	NotifyInfoChanged();
}

void AHitmenPlayerState::SetReady(bool bNewReady)
{
	bIsReady = bNewReady;
	NotifyInfoChanged();
}

void AHitmenPlayerState::OnRep_Info()
{
	NotifyInfoChanged();
}

void AHitmenPlayerState::NotifyInfoChanged()
{
	OnInfoChanged.Broadcast(this);
	if (AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr)
	{
		GS->NotifyLobbyChanged();
	}
}
