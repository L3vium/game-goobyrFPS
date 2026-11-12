// Hitmen — player state

#include "Framework/HitmenPlayerState.h"
#include "GameFramework/Pawn.h"
#include "Framework/HitmenPlayerController.h"
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
	DOREPLIFETIME(AHitmenPlayerState, CheatFlags);
}

void AHitmenPlayerState::SetCheat(EHitmenCheat Cheat, bool bEnabled)
{
	const uint8 Bit = static_cast<uint8>(1u << static_cast<uint32>(Cheat));
	const uint8 NewFlags = bEnabled ? (CheatFlags | Bit) : (CheatFlags & ~Bit);
	if (NewFlags != CheatFlags)
	{
		CheatFlags = NewFlags;
		ForceNetUpdate();
		OnRep_Cheats();
	}
}

void AHitmenPlayerState::OnRep_Cheats()
{
	if (AHitmenPlayerController* PC = Cast<AHitmenPlayerController>(GetOwningController()))
	{
		PC->ApplyCheatsToPawn();
	}
}

bool AHitmenPlayerState::ActorHasCheat(const UObject* Context, EHitmenCheat Cheat)
{
	// Pawn, weapon (owned by its pawn), component or ability (outer): walk up to the pawn or controller.
	const AActor* Actor = Cast<AActor>(Context);
	if (!Actor)
	{
		if (const UActorComponent* Component = Cast<UActorComponent>(Context))
		{
			Actor = Component->GetOwner();
		}
		else if (Context)
		{
			Actor = Context->GetTypedOuter<AActor>();
		}
	}
	while (Actor && !Actor->IsA<APawn>() && !Actor->IsA<AController>())
	{
		Actor = Actor->GetOwner();
	}
	const AHitmenPlayerState* PS = nullptr;
	if (const APawn* Pawn = Cast<APawn>(Actor))
	{
		PS = Pawn->GetPlayerState<AHitmenPlayerState>();
	}
	else if (const AController* Controller = Cast<AController>(Actor))
	{
		PS = Controller->GetPlayerState<AHitmenPlayerState>();
	}
	return PS && PS->HasCheat(Cheat);
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
		// Cheats survive the gym reset (F7 reloads the map).
		Target->CheatFlags = CheatFlags;
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
