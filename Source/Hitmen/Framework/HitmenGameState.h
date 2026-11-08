// Hitmen — replicated match state: teams, lobby settings, VIP, retrieval points, sonar, result

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameStateBase.h"
#include "Core/HitmenTypes.h"
#include "HitmenGameState.generated.h"

class AHitmenPlayerState;
class AHitmenCharacterBase;

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenGameStateEvent);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenMatchPhaseChanged, EHitmenMatchPhase, NewPhase);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSonarPingEvent, const FHitmenSonarPing&, Ping);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenMatchResultEvent, const FHitmenMatchResult&, Result);

UCLASS()
class HITMEN_API AHitmenGameState : public AGameStateBase
{
	GENERATED_BODY()

public:
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void AddPlayerState(APlayerState* PlayerState) override;
	virtual void RemovePlayerState(APlayerState* PlayerState) override;

	// ---- Events (bind in BeginPlay / NativeConstruct) ----
	/** Teams, players or lobby settings changed. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Events")
	FOnHitmenGameStateEvent OnLobbyChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Events")
	FOnHitmenMatchPhaseChanged OnMatchPhaseChanged;

	/** VIP picked, killed, picked up, dropped or delivered. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Events")
	FOnHitmenGameStateEvent OnVIPStateChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Events")
	FOnHitmenGameStateEvent OnRetrievalPointsChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Events")
	FOnHitmenSonarPingEvent OnSonarPing;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Events")
	FOnHitmenMatchResultEvent OnMatchEnded;

	// ---- Teams ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Teams")
	const TArray<FHitmenTeam>& GetTeams() const { return Teams; }

	const FHitmenTeam* FindTeam(int32 TeamId) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Teams")
	TArray<AHitmenPlayerState*> GetPlayersInTeam(int32 TeamId) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Teams")
	TArray<AHitmenPlayerState*> GetPlayersWithoutTeam() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Teams")
	TArray<AHitmenPlayerState*> GetHitmenPlayers() const;

	/** Server only. Returns the new team id. */
	int32 CreateTeam(const FLinearColor& Color);
	void RemoveTeam(int32 TeamId);
	void SetTeamRole(int32 TeamId, EHitmenTeamRole NewRole);
	void SetTeams(const TArray<FHitmenTeam>& InTeams);

	// ---- Lobby ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Lobby")
	const FHitmenLobbySettings& GetLobbySettings() const { return LobbySettings; }

	void SetLobbySettings(const FHitmenLobbySettings& InSettings);

	// ---- Match ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	EHitmenMatchPhase GetMatchPhase() const { return MatchPhase; }

	void SetMatchPhase(EHitmenMatchPhase NewPhase);

	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	AHitmenCharacterBase* GetVIP() const { return VIP; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	bool IsVIPDead() const { return bVIPDead; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	APawn* GetVIPCarrier() const { return VIPCarrier; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	const TArray<AActor*>& GetActiveRetrievalPoints() const { return ActiveRetrievalPoints; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	const FHitmenSonarPing& GetLastSonarPing() const { return SonarPing; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	const FHitmenMatchResult& GetMatchResult() const { return MatchResult; }

	void SetVIP(AHitmenCharacterBase* InVIP);
	void SetVIPDead(bool bDead);
	void SetVIPCarrier(APawn* Carrier);
	void SetActiveRetrievalPoints(const TArray<AActor*>& Points);
	void SetSonarPing(const FHitmenSonarPing& Ping);
	void SetMatchResult(const FHitmenMatchResult& Result);

	/** Debug grants allowed (gym, development builds) — mirrored for the client UI. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Debug")
	bool AreDebugCommandsAllowed() const { return bDebugCommandsAllowed; }

	void SetDebugCommandsAllowed(bool bAllowed) { bDebugCommandsAllowed = bAllowed; }

	/** Broadcast OnLobbyChanged locally (and to the UI event hub). */
	void NotifyLobbyChanged();

protected:
	UPROPERTY(ReplicatedUsing=OnRep_Lobby)
	TArray<FHitmenTeam> Teams;

	UPROPERTY(ReplicatedUsing=OnRep_Lobby)
	FHitmenLobbySettings LobbySettings;

	UPROPERTY(ReplicatedUsing=OnRep_MatchPhase)
	EHitmenMatchPhase MatchPhase = EHitmenMatchPhase::Lobby;

	UPROPERTY(ReplicatedUsing=OnRep_VIP)
	TObjectPtr<AHitmenCharacterBase> VIP;

	UPROPERTY(ReplicatedUsing=OnRep_VIP)
	bool bVIPDead = false;

	UPROPERTY(ReplicatedUsing=OnRep_VIP)
	TObjectPtr<APawn> VIPCarrier;

	UPROPERTY(ReplicatedUsing=OnRep_RetrievalPoints)
	TArray<TObjectPtr<AActor>> ActiveRetrievalPointsRep;

	/** Raw mirror of ActiveRetrievalPointsRep for BlueprintPure const-ref access. */
	UPROPERTY(Transient)
	TArray<AActor*> ActiveRetrievalPoints;

	UPROPERTY(ReplicatedUsing=OnRep_Sonar)
	FHitmenSonarPing SonarPing;

	UPROPERTY(ReplicatedUsing=OnRep_MatchResult)
	FHitmenMatchResult MatchResult;

	UPROPERTY(Replicated)
	bool bDebugCommandsAllowed = false;

	int32 NextTeamId = 0;

	UFUNCTION() void OnRep_Lobby();
	UFUNCTION() void OnRep_MatchPhase();
	UFUNCTION() void OnRep_VIP();
	UFUNCTION() void OnRep_RetrievalPoints();
	UFUNCTION() void OnRep_Sonar();
	UFUNCTION() void OnRep_MatchResult();

	void ForEachLocalUI(TFunctionRef<void(class UHitmenUIEventSubsystem&)> Fn) const;
};
