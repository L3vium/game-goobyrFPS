// Hitmen — game state

#include "Framework/HitmenGameState.h"
#include "Framework/HitmenPlayerState.h"
#include "Characters/HitmenCharacterBase.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "GameFramework/PlayerController.h"

void AHitmenGameState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenGameState, Teams);
	DOREPLIFETIME(AHitmenGameState, LobbySettings);
	DOREPLIFETIME(AHitmenGameState, RoomCode);
	DOREPLIFETIME(AHitmenGameState, MatchPhase);
	DOREPLIFETIME(AHitmenGameState, PhaseCountdownEnd);
	DOREPLIFETIME(AHitmenGameState, VIP);
	DOREPLIFETIME(AHitmenGameState, bVIPDead);
	DOREPLIFETIME(AHitmenGameState, VIPCarrier);
	DOREPLIFETIME(AHitmenGameState, ActiveRetrievalPointsRep);
	DOREPLIFETIME(AHitmenGameState, SonarPing);
	DOREPLIFETIME(AHitmenGameState, MatchResult);
	DOREPLIFETIME(AHitmenGameState, bDebugCommandsAllowed);
}

void AHitmenGameState::AddPlayerState(APlayerState* PlayerState)
{
	Super::AddPlayerState(PlayerState);
	NotifyLobbyChanged();
}

void AHitmenGameState::RemovePlayerState(APlayerState* PlayerState)
{
	Super::RemovePlayerState(PlayerState);
	NotifyLobbyChanged();
}

const FHitmenTeam* AHitmenGameState::FindTeam(int32 TeamId) const
{
	return Teams.FindByPredicate([TeamId](const FHitmenTeam& Team) { return Team.TeamId == TeamId; });
}

TArray<AHitmenPlayerState*> AHitmenGameState::GetHitmenPlayers() const
{
	TArray<AHitmenPlayerState*> Result;
	for (APlayerState* PS : PlayerArray)
	{
		if (AHitmenPlayerState* HPS = Cast<AHitmenPlayerState>(PS))
		{
			Result.Add(HPS);
		}
	}
	return Result;
}

TArray<AHitmenPlayerState*> AHitmenGameState::GetPlayersInTeam(int32 TeamId) const
{
	return GetHitmenPlayers().FilterByPredicate([TeamId](const AHitmenPlayerState* PS) { return PS->GetLobbyTeamId() == TeamId; });
}

TArray<AHitmenPlayerState*> AHitmenGameState::GetPlayersWithoutTeam() const
{
	return GetPlayersInTeam(INDEX_NONE);
}

int32 AHitmenGameState::CreateTeam(const FLinearColor& Color)
{
	FHitmenTeam& Team = Teams.AddDefaulted_GetRef();
	Team.TeamId = NextTeamId++;
	Team.DisplayName = FText::Format(NSLOCTEXT("Hitmen", "TeamName", "Team {0}"), FText::AsNumber(Team.TeamId + 1));
	Team.Color = Color;
	Team.Role = EHitmenTeamRole::Attack;
	NotifyLobbyChanged();
	return Team.TeamId;
}

void AHitmenGameState::RemoveTeam(int32 TeamId)
{
	Teams.RemoveAll([TeamId](const FHitmenTeam& Team) { return Team.TeamId == TeamId; });
	for (AHitmenPlayerState* PS : GetPlayersInTeam(TeamId))
	{
		PS->SetLobbyTeamId(INDEX_NONE);
	}
	NotifyLobbyChanged();
}

void AHitmenGameState::SetTeamRole(int32 TeamId, EHitmenTeamRole NewRole)
{
	for (FHitmenTeam& Team : Teams)
	{
		if (Team.TeamId == TeamId)
		{
			Team.Role = NewRole;
		}
	}
	NotifyLobbyChanged();
}

void AHitmenGameState::SetTeams(const TArray<FHitmenTeam>& InTeams)
{
	Teams = InTeams;
	NextTeamId = 0;
	for (const FHitmenTeam& Team : Teams)
	{
		NextTeamId = FMath::Max(NextTeamId, Team.TeamId + 1);
	}
	NotifyLobbyChanged();
}

void AHitmenGameState::SetLobbySettings(const FHitmenLobbySettings& InSettings)
{
	LobbySettings = InSettings;
	NotifyLobbyChanged();
}

void AHitmenGameState::SetPhaseCountdown(float Seconds)
{
	PhaseCountdownEnd = Seconds > 0.f ? GetServerWorldTimeSeconds() + Seconds : 0.f;
}

float AHitmenGameState::GetPhaseCountdownRemaining() const
{
	return PhaseCountdownEnd > 0.f ? FMath::Max(0.f, PhaseCountdownEnd - GetServerWorldTimeSeconds()) : 0.f;
}

void AHitmenGameState::SetMatchPhase(EHitmenMatchPhase NewPhase)
{
	MatchPhase = NewPhase;
	OnRep_MatchPhase();
}

void AHitmenGameState::SetVIP(AHitmenCharacterBase* InVIP)
{
	VIP = InVIP;
	OnRep_VIP();
}

void AHitmenGameState::SetVIPDead(bool bDead)
{
	bVIPDead = bDead;
	OnRep_VIP();
}

void AHitmenGameState::SetVIPCarrier(APawn* Carrier)
{
	VIPCarrier = Carrier;
	OnRep_VIP();
}

void AHitmenGameState::SetActiveRetrievalPoints(const TArray<AActor*>& Points)
{
	ActiveRetrievalPointsRep.Reset();
	for (AActor* Point : Points)
	{
		ActiveRetrievalPointsRep.Add(Point);
	}
	OnRep_RetrievalPoints();
}

void AHitmenGameState::SetSonarPing(const FHitmenSonarPing& Ping)
{
	SonarPing = Ping;
	OnRep_Sonar();
}

void AHitmenGameState::SetMatchResult(const FHitmenMatchResult& Result)
{
	MatchResult = Result;
	OnRep_MatchResult();
}

void AHitmenGameState::NotifyLobbyChanged()
{
	OnLobbyChanged.Broadcast();
	ForEachLocalUI([](UHitmenUIEventSubsystem& UI) { UI.BroadcastLobbyChanged(); });
}

void AHitmenGameState::OnRep_Lobby()
{
	NotifyLobbyChanged();
}

void AHitmenGameState::OnRep_MatchPhase()
{
	OnMatchPhaseChanged.Broadcast(MatchPhase);
	const EHitmenMatchPhase Phase = MatchPhase;
	ForEachLocalUI([Phase](UHitmenUIEventSubsystem& UI) { UI.BroadcastPhase(Phase); });
}

void AHitmenGameState::OnRep_VIP()
{
	OnVIPStateChanged.Broadcast();
}

void AHitmenGameState::OnRep_RetrievalPoints()
{
	ActiveRetrievalPoints.Reset();
	for (AActor* Point : ActiveRetrievalPointsRep)
	{
		if (Point)
		{
			ActiveRetrievalPoints.Add(Point);
		}
	}
	OnRetrievalPointsChanged.Broadcast();
}

void AHitmenGameState::OnRep_Sonar()
{
	OnSonarPing.Broadcast(SonarPing);
	const FHitmenSonarPing Ping = SonarPing;
	ForEachLocalUI([Ping](UHitmenUIEventSubsystem& UI) { UI.BroadcastSonar(Ping); });
}

void AHitmenGameState::OnRep_MatchResult()
{
	OnMatchEnded.Broadcast(MatchResult);
	const FHitmenMatchResult Result = MatchResult;
	ForEachLocalUI([Result](UHitmenUIEventSubsystem& UI) { UI.BroadcastResult(Result); });
}

void AHitmenGameState::ForEachLocalUI(TFunctionRef<void(UHitmenUIEventSubsystem&)> Fn) const
{
	UWorld* World = GetWorld();
	if (!World)
	{
		return;
	}
	for (FConstPlayerControllerIterator It = World->GetPlayerControllerIterator(); It; ++It)
	{
		if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(It->Get()))
		{
			Fn(*UI);
		}
	}
}
