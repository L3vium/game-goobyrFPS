// Hitmen — match game mode

#include "Framework/HitmenMatchGameMode.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenGameInstance.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "Characters/HitmenNPC.h"
#include "World/Spawning/HitmenRetrievalPoint.h"
#include "World/Spawning/HitmenLootSpawnPoint.h"
#include "World/Spawning/HitmenTeamStart.h"
#include "World/Spawning/HitmenSpawnDistributionSubsystem.h"
#include "Core/HitmenGameSettings.h"
#include "Data/HitmenMatchRules.h"
#include "Data/HitmenCatalog.h"
#include "Hitmen.h"
#include "EngineUtils.h"
#include "TimerManager.h"
#include "Engine/World.h"

AHitmenMatchGameMode::AHitmenMatchGameMode()
{
	DefaultPawnClass = AHitmenPlayerCharacter::StaticClass();
}

void AHitmenMatchGameMode::InitGameState()
{
	Super::InitGameState();
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS)
	{
		return;
	}
	if (const UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>(); GI && GI->bHasPendingMatch)
	{
		GS->SetLobbySettings(GI->PendingLobbySettings);
		GS->SetTeams(GI->PendingTeams);
	}
	else
	{
		// Launched directly in PIE: use rule defaults.
		GS->SetLobbySettings(GetRules()->MakeDefaultLobbySettings());
	}
	GS->SetMatchPhase(EHitmenMatchPhase::Playing);
}

void AHitmenMatchGameMode::StartPlay()
{
	Super::StartPlay();
	SetupMatch();
}

void AHitmenMatchGameMode::SetupMatch()
{
	if (bMatchSetup)
	{
		return;
	}
	bMatchSetup = true;

	FRandomStream Stream(FMath::Rand());
	SelectVIP(Stream);
	ActivateRetrievalPoints(Stream);
	SpawnLoot(Stream);
	Juice_OnMatchStarted();
}

void AHitmenMatchGameMode::SelectVIP(FRandomStream& Stream)
{
	TArray<AHitmenNPC*> Candidates;
	for (TActorIterator<AHitmenNPC> It(GetWorld()); It; ++It)
	{
		if (It->IsVIPCandidate())
		{
			Candidates.Add(*It);
		}
	}
	const int32 MaxCandidates = GetRules()->MaxVIPCandidates;
	if (Candidates.Num() > MaxCandidates)
	{
		// Too many candidates placed: keep a random subset so every placement can be picked.
		UE_LOG(LogHitmen, Log, TEXT("Match: %d VIP candidates placed, %d drawn at random."), Candidates.Num(), MaxCandidates);
		for (int32 Index = Candidates.Num() - 1; Index > 0; --Index)
		{
			Candidates.Swap(Index, Stream.RandRange(0, Index));
		}
		Candidates.SetNum(MaxCandidates);
	}
	if (Candidates.IsEmpty())
	{
		UE_LOG(LogHitmen, Warning, TEXT("Match: no VIP candidate in this map."));
		return;
	}
	AHitmenNPC* VIP = Candidates[Stream.RandRange(0, Candidates.Num() - 1)];
	VIP->SetIsVIP(true);
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>())
	{
		GS->SetVIP(VIP);
	}
}

void AHitmenMatchGameMode::ActivateRetrievalPoints(FRandomStream& Stream)
{
	TArray<AActor*> Candidates;
	for (TActorIterator<AHitmenRetrievalPoint> It(GetWorld()); It; ++It)
	{
		It->SetActive(false);
		Candidates.Add(*It);
	}
	const AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	const int32 Count = bActivateAllCandidates ? Candidates.Num() : (GS ? GS->GetLobbySettings().RetrievalPointCount : 1);
	const TArray<AActor*> Selected = UHitmenSpawnDistributionSubsystem::SelectSpread(Candidates, Count, GetRules()->SpawnSpreadRandomness, Stream);
	for (AActor* Point : Selected)
	{
		CastChecked<AHitmenRetrievalPoint>(Point)->SetActive(true);
	}
	if (AHitmenGameState* MutableGS = GetGameState<AHitmenGameState>())
	{
		MutableGS->SetActiveRetrievalPoints(Selected);
	}
}

void AHitmenMatchGameMode::SpawnLoot(FRandomStream& Stream)
{
	const AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	const FHitmenDifficultyPreset& Preset = GetRules()->GetPreset(GS ? GS->GetLobbySettings().Difficulty : EHitmenDifficulty::Normal);
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();

	TArray<AActor*> WeaponPoints;
	TArray<AActor*> ItemPoints;
	for (TActorIterator<AHitmenLootSpawnPoint> It(GetWorld()); It; ++It)
	{
		(It->GetCategory() == EHitmenLootCategory::Weapon ? WeaponPoints : ItemPoints).Add(*It);
	}

	const float Randomness = GetRules()->SpawnSpreadRandomness;
	const TArray<AActor*> Weapons = bActivateAllCandidates ? WeaponPoints : UHitmenSpawnDistributionSubsystem::SelectSpread(WeaponPoints, Preset.WeaponSpawnCount, Randomness, Stream);
	const TArray<AActor*> Items = bActivateAllCandidates ? ItemPoints : UHitmenSpawnDistributionSubsystem::SelectSpread(ItemPoints, Preset.ItemSpawnCount, Randomness, Stream);
	for (AActor* Point : Weapons)
	{
		CastChecked<AHitmenLootSpawnPoint>(Point)->SpawnLoot(Catalog, Stream);
	}
	for (AActor* Point : Items)
	{
		CastChecked<AHitmenLootSpawnPoint>(Point)->SpawnLoot(Catalog, Stream);
	}
}

void AHitmenMatchGameMode::HandleStartingNewPlayer_Implementation(APlayerController* NewPlayer)
{
	const AHitmenPlayerState* PS = NewPlayer ? NewPlayer->GetPlayerState<AHitmenPlayerState>() : nullptr;
	if (PS && PS->IsWatcher())
	{
		NewPlayer->StartSpectatingOnly();
		if (AHitmenPlayerController* HPC = Cast<AHitmenPlayerController>(NewPlayer))
		{
			HPC->Client_EnterSpectatorMode(true);
		}
		return;
	}
	Super::HandleStartingNewPlayer_Implementation(NewPlayer);
}

bool AHitmenMatchGameMode::PlayerCanRestart_Implementation(APlayerController* Player)
{
	const AHitmenPlayerState* PS = Player ? Player->GetPlayerState<AHitmenPlayerState>() : nullptr;
	if (PS && PS->IsWatcher())
	{
		return false;
	}
	return Super::PlayerCanRestart_Implementation(Player);
}

int32 AHitmenMatchGameMode::GetSpawnGroup(const AController* Player) const
{
	const AHitmenPlayerState* PS = Player ? Player->GetPlayerState<AHitmenPlayerState>() : nullptr;
	if (!PS)
	{
		return INDEX_NONE;
	}
	// Teams share a group; solo players each take their own group after the teams.
	return PS->IsInTeam() ? PS->GetLobbyTeamId() : 100 + PS->GetPlayerId();
}

AActor* AHitmenMatchGameMode::ChoosePlayerStart_Implementation(AController* Player)
{
	const int32 Group = GetSpawnGroup(Player);
	const AHitmenGameState* GS = GetGameState<AHitmenGameState>();

	// Map lobby teams onto start groups: team order -> group index 0..N, solos after.
	int32 DesiredGroupIndex = 0;
	if (GS)
	{
		const AHitmenPlayerState* PS = Player->GetPlayerState<AHitmenPlayerState>();
		if (PS && PS->IsInTeam())
		{
			DesiredGroupIndex = GS->GetTeams().IndexOfByPredicate([PS](const FHitmenTeam& T) { return T.TeamId == PS->GetLobbyTeamId(); });
		}
		else if (PS)
		{
			const TArray<AHitmenPlayerState*> Solos = GS->GetPlayersWithoutTeam();
			DesiredGroupIndex = GS->GetTeams().Num() + Solos.IndexOfByKey(PS);
		}
	}

	TArray<AHitmenTeamStart*> Starts;
	TSet<int32> Groups;
	for (TActorIterator<AHitmenTeamStart> It(GetWorld()); It; ++It)
	{
		Starts.Add(*It);
		Groups.Add(It->SpawnGroup);
	}
	if (Starts.IsEmpty())
	{
		return Super::ChoosePlayerStart_Implementation(Player);
	}

	TArray<int32> SortedGroups = Groups.Array();
	SortedGroups.Sort();
	const int32 TargetGroup = SortedGroups[FMath::Max(0, DesiredGroupIndex) % SortedGroups.Num()];

	TArray<AActor*>& Used = UsedStarts.FindOrAdd(Group);
	AHitmenTeamStart* Fallback = nullptr;
	for (AHitmenTeamStart* Start : Starts)
	{
		if (Start->SpawnGroup != TargetGroup)
		{
			continue;
		}
		Fallback = Fallback ? Fallback : Start;
		if (!Used.Contains(Start))
		{
			Used.Add(Start);
			return Start;
		}
	}
	return Fallback ? Fallback : Starts[0];
}

void AHitmenMatchGameMode::SetPlayerDefaults(APawn* PlayerPawn)
{
	Super::SetPlayerDefaults(PlayerPawn);
	AHitmenPlayerCharacter* Character = Cast<AHitmenPlayerCharacter>(PlayerPawn);
	const AHitmenPlayerState* PS = PlayerPawn ? PlayerPawn->GetPlayerState<AHitmenPlayerState>() : nullptr;
	if (Character && PS)
	{
		Character->ApplyLoadout(PS->GetLoadout());
		Character->ApplyCosmetics(PS->GetCosmetics());
	}
}

void AHitmenMatchGameMode::OnCharacterKilled(AHitmenCharacterBase* Victim, AController* Killer, AActor* DamageCauser, bool bHeadshot)
{
	Super::OnCharacterKilled(Victim, Killer, DamageCauser, bHeadshot);
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS || !Victim)
	{
		return;
	}

	if (Victim == GS->GetVIP())
	{
		GS->SetVIPDead(true);
	}

	if (AController* VictimController = Victim->GetController())
	{
		if (AHitmenPlayerState* VictimPS = VictimController->GetPlayerState<AHitmenPlayerState>())
		{
			VictimPS->SetAlive(false);
			HandlePlayerDeath(VictimController);
		}
	}
	CheckWinConditions();
}

void AHitmenMatchGameMode::HandlePlayerDeath(AController* VictimController)
{
	APlayerController* PC = Cast<APlayerController>(VictimController);
	if (!PC)
	{
		return;
	}
	FTimerHandle Handle;
	TWeakObjectPtr<APlayerController> WeakPC = PC;
	if (RespawnDelay > 0.f)
	{
		GetWorldTimerManager().SetTimer(Handle, FTimerDelegate::CreateWeakLambda(this, [this, WeakPC]()
		{
			if (APlayerController* P = WeakPC.Get())
			{
				if (AHitmenPlayerState* PS = P->GetPlayerState<AHitmenPlayerState>())
				{
					PS->SetAlive(true);
				}
				if (APawn* Old = P->GetPawn())
				{
					P->UnPossess();
					Old->SetLifeSpan(10.f);
				}
				RestartPlayer(P);
			}
		}), RespawnDelay, false);
		return;
	}
	GetWorldTimerManager().SetTimer(Handle, FTimerDelegate::CreateWeakLambda(this, [WeakPC]()
	{
		if (AHitmenPlayerController* P = Cast<AHitmenPlayerController>(WeakPC.Get()))
		{
			P->UnPossess();
			P->ChangeState(NAME_Spectating);
			P->ClientGotoState(NAME_Spectating);
			P->Client_EnterSpectatorMode(false);
		}
	}), DeathSpectateDelay, false);
}

void AHitmenMatchGameMode::OnVIPCarrierChanged(APawn* NewCarrier)
{
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!GS)
	{
		return;
	}
	GS->SetVIPCarrier(NewCarrier);
	GetWorldTimerManager().ClearTimer(SonarTimer);
	if (NewCarrier)
	{
		CarryStartTime = GetWorld()->GetTimeSeconds();
		TickSonar();
	}
	else
	{
		GS->SetSonarPing(FHitmenSonarPing());
	}
}

void AHitmenMatchGameMode::TickSonar()
{
	AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	APawn* Carrier = GS ? GS->GetVIPCarrier() : nullptr;
	if (!Carrier || bMatchOver)
	{
		return;
	}
	const float CarryTime = GetWorld()->GetTimeSeconds() - CarryStartTime;
	const float Interval = GetRules()->GetSonarInterval(CarryTime, GS->GetLobbySettings());

	FHitmenSonarPing Ping;
	Ping.Location = Carrier->GetActorLocation();
	Ping.ServerTime = GS->GetServerWorldTimeSeconds();
	Ping.NextInterval = Interval;
	GS->SetSonarPing(Ping);

	GetWorldTimerManager().SetTimer(SonarTimer, this, &AHitmenMatchGameMode::TickSonar, Interval, false);
}

void AHitmenMatchGameMode::OnVIPDelivered(APawn* Carrier, AHitmenRetrievalPoint* Point)
{
	if (!bWinConditionsEnabled || bMatchOver || !Carrier)
	{
		return;
	}
	const AHitmenPlayerState* PS = Carrier->GetPlayerState<AHitmenPlayerState>();
	if (!PS || PS->GetTeamRole() != EHitmenTeamRole::Attack)
	{
		return;
	}
	FHitmenMatchResult Result;
	Result.WinningTeamId = PS->GetLobbyTeamId();
	Result.WinningPlayerName = PS->GetPlayerName();
	Result.Reason = NSLOCTEXT("Hitmen", "WinDelivered", "The VIP was extracted");
	EndMatch(Result);
}

void AHitmenMatchGameMode::CheckWinConditions()
{
	const AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	if (!bWinConditionsEnabled || bMatchOver || !GS)
	{
		return;
	}

	bool bAnyProtectTeam = false;
	int32 AliveAttackers = 0;
	int32 AlivePlayers = 0;
	const AHitmenPlayerState* AnyProtector = nullptr;
	for (const AHitmenPlayerState* PS : GS->GetHitmenPlayers())
	{
		if (PS->IsWatcher())
		{
			continue;
		}
		const bool bProtect = PS->IsInTeam() && PS->GetTeamRole() == EHitmenTeamRole::Protect;
		bAnyProtectTeam |= bProtect;
		if (!PS->IsAlive())
		{
			continue;
		}
		++AlivePlayers;
		if (bProtect)
		{
			AnyProtector = PS;
		}
		else
		{
			++AliveAttackers;
		}
	}

	if (AlivePlayers == 0)
	{
		FHitmenMatchResult Result;
		Result.bIsDraw = true;
		Result.Reason = NSLOCTEXT("Hitmen", "Draw", "No agent survived");
		EndMatch(Result);
		return;
	}

	// Protect wins when every non-protect player is dead.
	if (bAnyProtectTeam && AliveAttackers == 0 && AnyProtector)
	{
		FHitmenMatchResult Result;
		Result.WinningTeamId = AnyProtector->GetLobbyTeamId();
		Result.Reason = NSLOCTEXT("Hitmen", "WinProtect", "Every intruder was eliminated");
		EndMatch(Result);
	}
}

void AHitmenMatchGameMode::EndMatch(const FHitmenMatchResult& Result)
{
	if (bMatchOver)
	{
		return;
	}
	bMatchOver = true;
	GetWorldTimerManager().ClearTimer(SonarTimer);
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>())
	{
		GS->SetMatchResult(Result);
		GS->SetMatchPhase(EHitmenMatchPhase::Ended);
	}
	Juice_OnMatchEnded(Result);
	GetWorldTimerManager().SetTimer(EndTimer, this, &AHitmenMatchGameMode::ReturnToLobby, GetRules()->ResultsDuration, false);
}

void AHitmenMatchGameMode::ReturnToLobby()
{
	const TSoftObjectPtr<UWorld>& Lobby = UHitmenGameSettings::Get()->LobbyLevel;
	if (Lobby.IsNull())
	{
		return;
	}
	GetWorld()->ServerTravel(Lobby.ToSoftObjectPath().GetLongPackageName() + TEXT("?listen"));
}
