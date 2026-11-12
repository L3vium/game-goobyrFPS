// Hitmen — match: VIP pick, retrieval points, loot, team starts, win conditions, sonar

#pragma once

#include "CoreMinimal.h"
#include "Framework/HitmenGameModeBase.h"
#include "Core/HitmenTypes.h"
#include "HitmenMatchGameMode.generated.h"

class AHitmenPlayerCharacter;
class AHitmenPlayerState;
class AHitmenRetrievalPoint;

UCLASS()
class HITMEN_API AHitmenMatchGameMode : public AHitmenGameModeBase
{
	GENERATED_BODY()

public:
	AHitmenMatchGameMode();

	virtual void InitGameState() override;
	virtual void StartPlay() override;
	virtual void HandleStartingNewPlayer_Implementation(APlayerController* NewPlayer) override;
	virtual AActor* ChoosePlayerStart_Implementation(AController* Player) override;
	virtual APawn* SpawnDefaultPawnAtTransform_Implementation(AController* NewPlayer, const FTransform& SpawnTransform) override;
	virtual void SetPlayerDefaults(APawn* PlayerPawn) override;
	virtual bool PlayerCanRestart_Implementation(APlayerController* Player) override;

	virtual void OnCharacterKilled(AHitmenCharacterBase* Victim, AController* Killer, AActor* DamageCauser, bool bHeadshot) override;

	/** A player carrying the VIP body reached an active retrieval point. */
	virtual void OnVIPDelivered(APawn* Carrier, AHitmenRetrievalPoint* Point);

	/** VIP body picked up / dropped (drives the sonar). */
	virtual void OnVIPCarrierChanged(APawn* NewCarrier);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Match")
	void EndMatch(const FHitmenMatchResult& Result);

protected:
	/** Gym and test maps can disable win conditions. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Match")
	bool bWinConditionsEnabled = true;

	/** Activate every loot / retrieval candidate (gym showcase). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Match")
	bool bActivateAllCandidates = false;

	/** Dead players respawn after this delay (0 = no respawn, spectate instead). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Match", meta=(ClampMin="0"))
	float RespawnDelay = 0.f;

	/** Seconds after death before a dead player switches to spectating teammates. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Match", meta=(ClampMin="0"))
	float DeathSpectateDelay = 3.f;

	/** Match start: players drop from the sky under a parachute, without control, then play. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Match|Intro")
	bool bParachuteIntro = true;

	/** Height above the spawn point where the drop starts. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Match|Intro", meta=(ClampMin="0", Units="cm", EditCondition="bParachuteIntro"))
	float IntroDropHeight = 2600.f;

	/** Intro phase length (countdown on screen; landing gives control earlier). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Match|Intro", meta=(ClampMin="0", Units="s", EditCondition="bParachuteIntro"))
	float IntroDuration = 3.f;

	FTimerHandle IntroTimer;
	void HandleIntroEnd();

	/** Spawn clusters: one random anchor per team / solo player; teammates take the nearest free starts. */
	TMap<int32, TWeakObjectPtr<AActor>> SpawnAnchors;
	TSet<TWeakObjectPtr<AActor>> TakenStarts;

	virtual void SetupMatch();
	void SelectVIP(FRandomStream& Stream);
	void ActivateRetrievalPoints(FRandomStream& Stream);
	void SpawnLoot(FRandomStream& Stream);
	void CheckWinConditions();
	void TickSonar();
	void ReturnToLobby();
	void HandlePlayerDeath(AController* VictimController);

	/** Spawn group per team (solo players get their own group index). */
	int32 GetSpawnGroup(const AController* Player) const;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnMatchStarted();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnMatchEnded(const FHitmenMatchResult& Result);

	FTimerHandle SonarTimer;
	FTimerHandle EndTimer;
	float CarryStartTime = 0.f;
	bool bMatchSetup = false;
	bool bMatchOver = false;
};
