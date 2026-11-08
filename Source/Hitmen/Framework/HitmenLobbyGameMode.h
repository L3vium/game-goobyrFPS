// Hitmen — lobby: teams, roles, watchers, map and options, customization, launch

#pragma once

#include "CoreMinimal.h"
#include "Framework/HitmenGameModeBase.h"
#include "Core/HitmenTypes.h"
#include "HitmenLobbyGameMode.generated.h"

class AHitmenPlayerController;
class AHitmenPlayerState;

UCLASS()
class HITMEN_API AHitmenLobbyGameMode : public AHitmenGameModeBase
{
	GENERATED_BODY()

public:
	AHitmenLobbyGameMode();

	virtual void InitGameState() override;

	/** Host = first local player controller on the listen server. */
	bool IsHost(const APlayerController* PC) const;

	// ---- Host operations (validated) ----
	void RequestCreateTeam(AHitmenPlayerController* Requester);
	void RequestRemoveTeam(AHitmenPlayerController* Requester, int32 TeamId);
	void RequestAssignTeam(AHitmenPlayerController* Requester, AHitmenPlayerState* Target, int32 TeamId);
	void RequestSetTeamRole(AHitmenPlayerController* Requester, int32 TeamId, EHitmenTeamRole NewRole);
	void RequestSetWatcher(AHitmenPlayerController* Requester, AHitmenPlayerState* Target, bool bWatcher);
	void RequestSetLobbySettings(AHitmenPlayerController* Requester, const FHitmenLobbySettings& Settings);
	/** Lobby -> customization screen. */
	void RequestStart(AHitmenPlayerController* Requester);
	/** Customization -> travel to the match map. */
	void RequestLaunch(AHitmenPlayerController* Requester);
	/** Customization -> back to the lobby. */
	void RequestBackToLobby(AHitmenPlayerController* Requester);

	/** Called when a player toggles ready; launches automatically when everyone is ready. */
	void NotifyReadyChanged();

protected:
	/** Launch automatically once every non-watcher is ready in the customization screen. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Lobby")
	bool bAutoLaunchWhenAllReady = false;

	int32 TeamColorCursor = 0;
};
