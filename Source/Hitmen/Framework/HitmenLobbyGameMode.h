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

	/** Title -> Lobby once the room is hosted (the world listens in place, no level load). */
	void OpenLobby();

	/** Lobby -> Title on the host leaving the room (the world stops listening). */
	void ReturnToTitlePhase();

protected:
	/** Launch automatically once every non-watcher is ready in the customization screen. */
	/** The match starts by itself when every non-watcher is ready (after LaunchCountdown). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Lobby")
	bool bAutoLaunchWhenAllReady = true;

	/** Seconds between "everyone ready" and the travel; un-readying cancels. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Lobby", meta=(ClampMin="0", Units="s"))
	float LaunchCountdown = 5.f;

	/** A standalone front end starts on the title screen (the lobby opens when hosting). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Lobby")
	bool bStartOnTitle = true;

	FTimerHandle LaunchTimer;
	void HandleLaunchTimer();

	int32 TeamColorCursor = 0;
};
