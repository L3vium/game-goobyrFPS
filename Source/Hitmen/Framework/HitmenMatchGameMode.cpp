// Hitmen — match game mode

#include "Framework/HitmenMatchGameMode.h"
#include "UObject/UnrealType.h"
#include "Components/HitmenHealthComponent.h"
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
#include "GameFramework/SpectatorPawn.h"
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
	GS->SetMatchPhase(bParachuteIntro ? EHitmenMatchPhase::Intro : EHitmenMatchPhase::Playing);
}

void AHitmenMatchGameMode::StartPlay()
{
	Super::StartPlay();
	SetupMatch();
	if (AllowsDebugCommands())
	{
		SnapshotNPCs();
	}
	if (bParachuteIntro)
	{
		if (AHitmenGameState* GS = GetGameState<AHitmenGameState>())
		{
			GS->SetPhaseCountdown(IntroDuration);
		}
		GetWorldTimerManager().SetTimer(IntroTimer, this, &AHitmenMatchGameMode::HandleIntroEnd, FMath::Max(0.01f, IntroDuration), false);
	}
}

void AHitmenMatchGameMode::HandleIntroEnd()
{
	if (AHitmenGameState* GS = GetGameState<AHitmenGameState>(); GS && GS->GetMatchPhase() == EHitmenMatchPhase::Intro)
	{
		GS->SetPhaseCountdown(0.f);
		GS->SetMatchPhase(EHitmenMatchPhase::Playing);
	}
}

APawn* AHitmenMatchGameMode::SpawnDefaultPawnAtTransform_Implementation(AController* NewPlayer, const FTransform& SpawnTransform)
{
	const AHitmenGameState* GS = GetGameState<AHitmenGameState>();
	const bool bDrop = bParachuteIntro && GS && GS->GetMatchPhase() == EHitmenMatchPhase::Intro;
	FTransform Transform = SpawnTransform;
	if (bDrop)
	{
		Transform.AddToTranslation(FVector(0.f, 0.f, IntroDropHeight));
	}
	APawn* Pawn = Super::SpawnDefaultPawnAtTransform_Implementation(NewPlayer, Transform);
	if (AHitmenPlayerCharacter* Character = Cast<AHitmenPlayerCharacter>(Pawn); Character && bDrop)
	{
		Character->StartParachute();
	}
	return Pawn;
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
	TArray<AActor*> Starts;
	for (TActorIterator<AHitmenTeamStart> It(GetWorld()); It; ++It)
	{
		Starts.Add(*It);
	}
	if (Starts.IsEmpty())
	{
		return Super::ChoosePlayerStart_Implementation(Player);
	}
	TArray<AActor*> Free = Starts.FilterByPredicate([this](AActor* Start) { return !TakenStarts.Contains(Start); });
	if (Free.IsEmpty())
	{
		// Respawns (gym): every start is usable again.
		TakenStarts.Reset();
		Free = Starts;
	}
	// One random anchor per team (or solo player); teammates take the free starts closest to it.
	const int32 Group = GetSpawnGroup(Player);
	AActor* Anchor = SpawnAnchors.FindRef(Group).Get();
	AActor* Chosen = nullptr;
	if (!Anchor)
	{
		Chosen = Free[FMath::RandRange(0, Free.Num() - 1)];
		SpawnAnchors.Add(Group, Chosen);
	}
	else
	{
		float Best = BIG_NUMBER;
		for (AActor* Start : Free)
		{
			const float Distance = FVector::DistSquared(Start->GetActorLocation(), Anchor->GetActorLocation());
			if (Distance < Best)
			{
				Best = Distance;
				Chosen = Start;
			}
		}
	}
	TakenStarts.Add(Chosen);
	return Chosen;
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

	// Watchers only (watching the NPCs): the match never ends by itself.
	bool bAnyAgent = false;
	for (const AHitmenPlayerState* PS : GS->GetHitmenPlayers())
	{
		bAnyAgent |= !PS->IsWatcher();
	}
	if (!bAnyAgent)
	{
		return;
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
	// No return timer: after a beat on the result everyone flies free; the host sends everyone back.
	GetWorldTimerManager().SetTimer(EndTimer, this, &AHitmenMatchGameMode::MoveEveryoneToSpectators, FMath::Max(0.01f, EndSpectateDelay), false);
}

void AHitmenMatchGameMode::MoveEveryoneToSpectators()
{
	for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
	{
		AHitmenPlayerController* PC = Cast<AHitmenPlayerController>(It->Get());
		if (!PC)
		{
			continue;
		}
		// Watchers and dead players already spectate; they switch to free fly too. Living agents leave their body
		// where it stands.
		if (PC->GetPawn() && !PC->GetPawn()->IsA<ASpectatorPawn>())
		{
			PC->UnPossess();
		}
		if (!PC->IsInState(NAME_Spectating))
		{
			PC->ChangeState(NAME_Spectating);
			PC->ClientGotoState(NAME_Spectating);
		}
		PC->Client_EnterSpectatorMode(true);
	}
}

void AHitmenMatchGameMode::SnapshotNPCs()
{
	NPCSnapshots.Reset();
	NPCSnapshotClasses.Reset();
	for (TActorIterator<AHitmenNPC> It(GetWorld()); It; ++It)
	{
		AHitmenNPC* NPC = *It;
		FNPCSnapshot& Snapshot = NPCSnapshots.AddDefaulted_GetRef();
		Snapshot.Class = NPC->GetClass();
		Snapshot.Transform = NPC->GetActorTransform();
		NPCSnapshotClasses.AddUnique(NPC->GetClass());
		// Everything a designer can set on a placed NPC (C++ and Blueprint variables), as text: behaviour, route,
		// guard group, keycard... Object references (routes) resolve again in this world.
		for (TFieldIterator<FProperty> PropIt(NPC->GetClass()); PropIt; ++PropIt)
		{
			const FProperty* Property = *PropIt;
			const UClass* DeclaringClass = Property->GetOwnerClass();
			if (!DeclaringClass || !DeclaringClass->IsChildOf(AHitmenNPC::StaticClass()) || !Property->HasAnyPropertyFlags(CPF_Edit)
				|| Property->HasAnyPropertyFlags(CPF_Transient | CPF_EditConst | CPF_DisableEditOnInstance))
			{
				continue;
			}
			FString Value;
			Property->ExportTextItem_Direct(Value, Property->ContainerPtrToValuePtr<void>(NPC), nullptr, NPC, PPF_None);
			Snapshot.Properties.Emplace(Property->GetFName(), MoveTemp(Value));
		}
	}
	UE_LOG(LogHitmen, Log, TEXT("Debug: %d NPCs recorded for respawn"), NPCSnapshots.Num());
}

void AHitmenMatchGameMode::DebugRespawnNPCs()
{
	// Remove every NPC (alive or not) and its controller, then place the recorded ones again.
	for (TActorIterator<AHitmenNPC> It(GetWorld()); It; ++It)
	{
		if (AController* Controller = It->GetController())
		{
			Controller->Destroy();
		}
		It->Destroy();
	}
	for (const FNPCSnapshot& Snapshot : NPCSnapshots)
	{
		if (!Snapshot.Class)
		{
			continue;
		}
		AHitmenNPC* NPC = GetWorld()->SpawnActorDeferred<AHitmenNPC>(Snapshot.Class, Snapshot.Transform, nullptr, nullptr,
			ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn);
		if (!NPC)
		{
			continue;
		}
		for (const TPair<FName, FString>& Pair : Snapshot.Properties)
		{
			if (const FProperty* Property = NPC->GetClass()->FindPropertyByName(Pair.Key))
			{
				Property->ImportText_Direct(*Pair.Value, Property->ContainerPtrToValuePtr<void>(NPC), NPC, PPF_None);
			}
		}
		NPC->FinishSpawning(Snapshot.Transform);
		if (!NPC->GetController())
		{
			NPC->SpawnDefaultController();
		}
	}
	UE_LOG(LogHitmen, Log, TEXT("Debug: %d NPCs respawned"), NPCSnapshots.Num());
}

void AHitmenMatchGameMode::DebugKillNPCs(AController* Killer)
{
	for (TActorIterator<AHitmenNPC> It(GetWorld()); It; ++It)
	{
		if (UHitmenHealthComponent* Health = It->GetHealth(); Health && !It->IsDead())
		{
			Health->Kill(Killer ? Killer->GetPawn() : nullptr, nullptr);
		}
	}
}

void AHitmenMatchGameMode::DebugResetMap()
{
	GetWorld()->ServerTravel(TEXT("?Restart"), false);
}

void AHitmenMatchGameMode::RequestReturnToLobby(APlayerController* Requester)
{
	// The listen-server host only, once the match is over.
	if (!bMatchOver || !Requester || !Requester->IsLocalController() || GetWorld()->GetFirstPlayerController() != Requester)
	{
		return;
	}
	ReturnToLobby();
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
