// Hitmen — game instance: carries lobby data across travel and handles network failures

#pragma once

#include "CoreMinimal.h"
#include "Engine/GameInstance.h"
#include "Core/HitmenTypes.h"
#include "HitmenGameInstance.generated.h"

UCLASS()
class HITMEN_API UHitmenGameInstance : public UGameInstance
{
	GENERATED_BODY()

public:
	virtual void Init() override;
	virtual void Shutdown() override;

	/** Server: lobby state captured before travelling to the match map (GameState is not kept). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Travel")
	FHitmenLobbySettings PendingLobbySettings;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Travel")
	TArray<FHitmenTeam> PendingTeams;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Travel")
	bool bHasPendingMatch = false;

	void StorePendingMatch(const FHitmenLobbySettings& Settings, const TArray<FHitmenTeam>& Teams);
	void ClearPendingMatch();

	/** Returns to the title screen and tears down the session. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Travel")
	void ReturnToTitle();

	/** Opens the debug gym locally (title screen debug button). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Travel")
	void OpenGym();

protected:
	void HandleNetworkFailure(UWorld* World, UNetDriver* NetDriver, ENetworkFailure::Type FailureType, const FString& ErrorString);
	void HandleTravelFailure(UWorld* World, ETravelFailure::Type FailureType, const FString& ErrorString);
};
