// Hitmen — base game mode

#include "Framework/HitmenGameModeBase.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenHUD.h"
#include "Core/HitmenGameSettings.h"
#include "Core/HitmenTeamLibrary.h"
#include "Data/HitmenMatchRules.h"
#include "Data/HitmenMapDefinition.h"
#include "Data/HitmenCatalog.h"
#include "Spectator/HitmenSpectatorPawn.h"

AHitmenGameModeBase::AHitmenGameModeBase()
{
	GameStateClass = AHitmenGameState::StaticClass();
	PlayerStateClass = AHitmenPlayerState::StaticClass();
	PlayerControllerClass = AHitmenPlayerController::StaticClass();
	HUDClass = AHitmenHUD::StaticClass();
	SpectatorClass = AHitmenSpectatorPawn::StaticClass();
	bUseSeamlessTravel = true;
}

void AHitmenGameModeBase::PostLogin(APlayerController* NewPlayer)
{
	Super::PostLogin(NewPlayer);
	EnsurePlayerName(NewPlayer ? NewPlayer->GetPlayerState<AHitmenPlayerState>() : nullptr);
}

UHitmenMatchRules* AHitmenGameModeBase::GetRules() const
{
	if (CachedRules)
	{
		return CachedRules;
	}
	if (RulesOverride)
	{
		CachedRules = RulesOverride;
		return CachedRules;
	}
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	if (const AHitmenGameState* GS = GetGameState<AHitmenGameState>())
	{
		if (const UHitmenMapDefinition* Map = Settings->LoadMap(GS->GetLobbySettings().MapIndex))
		{
			if (Map->RulesOverride)
			{
				CachedRules = Map->RulesOverride;
				return CachedRules;
			}
		}
	}
	CachedRules = Settings->LoadDefaultRules();
	if (!CachedRules)
	{
		// Never return null: fall back to code defaults.
		CachedRules = NewObject<UHitmenMatchRules>(const_cast<AHitmenGameModeBase*>(this));
	}
	return CachedRules;
}

bool AHitmenGameModeBase::AllowsDebugCommands() const
{
#if UE_BUILD_SHIPPING
	return UHitmenGameSettings::Get()->bAllowCheatsInShipping;
#else
	return true;
#endif
}

bool AHitmenGameModeBase::CanDamage(const AActor* DamageInstigator, const AActor* Victim) const
{
	if (!Victim)
	{
		return false;
	}
	if (!DamageInstigator || DamageInstigator == Victim)
	{
		return true;
	}
	return UHitmenTeamLibrary::AreHostile(DamageInstigator, Victim);
}

void AHitmenGameModeBase::OnCharacterKilled(AHitmenCharacterBase* Victim, AController* Killer, AActor* DamageCauser, bool bHeadshot)
{
	if (Killer)
	{
		if (AHitmenPlayerState* KillerPS = Killer->GetPlayerState<AHitmenPlayerState>())
		{
			KillerPS->AddKill();
		}
	}
	Juice_OnCharacterKilled(Victim, Killer, bHeadshot);
}

void AHitmenGameModeBase::EnsurePlayerName(AHitmenPlayerState* PlayerState) const
{
	if (!PlayerState || !PlayerState->GetPlayerName().IsEmpty())
	{
		return;
	}
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
	PlayerState->SetPlayerName(Catalog ? Catalog->MakeRandomName() : FString::Printf(TEXT("Agent %d"), PlayerState->GetPlayerId()));
}
