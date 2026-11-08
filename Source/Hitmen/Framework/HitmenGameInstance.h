// Hitmen — game instance: carries lobby data across travel and handles network failures

#pragma once

#include "CoreMinimal.h"
#include "Engine/GameInstance.h"
#include "Core/HitmenTypes.h"
#include "HitmenGameInstance.generated.h"

class USoundMix;
class USoundClass;

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

	/** Client: the local player's chosen loadout and look, re-sent after every travel (a
	 *  non-seamless travel or a reconnect gives the player a fresh PlayerState). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Travel")
	FHitmenLoadout LocalLoadout;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Travel")
	FHitmenCosmeticLoadout LocalCosmetics;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Travel")
	bool bHasLocalLoadout = false;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Travel")
	bool bHasLocalCosmetics = false;

	void StorePendingMatch(const FHitmenLobbySettings& Settings, const TArray<FHitmenTeam>& Teams);
	void ClearPendingMatch();

	/** Returns to the title screen and tears down the session. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Travel")
	void ReturnToTitle();

	/** Opens the debug gym locally (title screen debug button). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Travel")
	void OpenGym();

protected:
	/** Mix pushed at startup; Options volumes override its classes (set in BP_GameInstance). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Audio")
	TSoftObjectPtr<USoundMix> VolumeSoundMix;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Audio")
	TSoftObjectPtr<USoundClass> MasterSoundClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Audio")
	TSoftObjectPtr<USoundClass> MusicSoundClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Audio")
	TSoftObjectPtr<USoundClass> SFXSoundClass;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Audio")
	TSoftObjectPtr<USoundClass> VoiceSoundClass;

	/** Applies the Options volumes to the sound mix. */
	UFUNCTION()
	void ApplyAudioSettings();

	void HandlePostLoadMap(UWorld* LoadedWorld);

	void HandleNetworkFailure(UWorld* World, UNetDriver* NetDriver, ENetworkFailure::Type FailureType, const FString& ErrorString);
	void HandleTravelFailure(UWorld* World, ETravelFailure::Type FailureType, const FString& ErrorString);
};
