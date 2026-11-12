// Hitmen — per-player data that survives travel (name, icon, team, look, loadout)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerState.h"
#include "Core/HitmenTypes.h"
#include "Core/HitmenTeamLibrary.h"
#include "HitmenPlayerState.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenPlayerInfoChanged, AHitmenPlayerState*, PlayerState);

UCLASS()
class HITMEN_API AHitmenPlayerState : public APlayerState, public IHitmenTeamMember
{
	GENERATED_BODY()

public:
	AHitmenPlayerState();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void CopyProperties(APlayerState* PlayerState) override;
	virtual void OnRep_PlayerName() override;

	// IHitmenTeamMember
	virtual int32 GetHitmenTeamId_Implementation() const override;

	/** Any info changed (name, icon, team, cosmetics, loadout, alive). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Player")
	FOnHitmenPlayerInfoChanged OnInfoChanged;

	// ---- Getters ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	int32 GetLobbyTeamId() const { return LobbyTeamId; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	bool IsInTeam() const { return LobbyTeamId != INDEX_NONE; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	bool IsWatcher() const { return bIsWatcher; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	int32 GetIconIndex() const { return IconIndex; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	const FHitmenCosmeticLoadout& GetCosmetics() const { return Cosmetics; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	const FHitmenLoadout& GetLoadout() const { return Loadout; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	bool IsAlive() const { return bIsAlive; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	bool IsReady() const { return bIsReady; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	int32 GetKills() const { return Kills; }

	/** Cheats this player switched on (F1-F8 in the gym / dev builds). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Debug")
	bool HasCheat(EHitmenCheat Cheat) const { return (CheatFlags & (1u << static_cast<uint32>(Cheat))) != 0; }

	/** Server. */
	void SetCheat(EHitmenCheat Cheat, bool bEnabled);

	/** True when the player owning this pawn, weapon, component or ability has the cheat on. */
	static bool ActorHasCheat(const UObject* Context, EHitmenCheat Cheat);

	/** Role of this player's team (solo players attack). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	EHitmenTeamRole GetTeamRole() const;

	// ---- Server setters (called by the GameMode / controller RPCs) ----
	void SetLobbyTeamId(int32 NewTeamId);
	void SetWatcher(bool bNewWatcher);
	void SetIconIndex(int32 NewIndex);
	void SetCosmetics(const FHitmenCosmeticLoadout& NewCosmetics);
	void SetLoadout(const FHitmenLoadout& NewLoadout);
	void SetAlive(bool bNewAlive);
	void SetReady(bool bNewReady);
	void AddKill() { ++Kills; }

protected:
	UPROPERTY(ReplicatedUsing=OnRep_Info)
	int32 LobbyTeamId = INDEX_NONE;

	UPROPERTY(ReplicatedUsing=OnRep_Info)
	bool bIsWatcher = false;

	UPROPERTY(ReplicatedUsing=OnRep_Info)
	int32 IconIndex = 0;

	UPROPERTY(ReplicatedUsing=OnRep_Info)
	FHitmenCosmeticLoadout Cosmetics;

	UPROPERTY(ReplicatedUsing=OnRep_Info)
	FHitmenLoadout Loadout;

	UPROPERTY(ReplicatedUsing=OnRep_Info)
	bool bIsAlive = true;

	UPROPERTY(ReplicatedUsing=OnRep_Info)
	bool bIsReady = false;

	UPROPERTY(Replicated)
	int32 Kills = 0;

	UPROPERTY(ReplicatedUsing=OnRep_Cheats)
	uint8 CheatFlags = 0;

	/** The owning client applies the fly cheat to its own pawn (movement mode, collision). */
	UFUNCTION()
	void OnRep_Cheats();

	UFUNCTION()
	void OnRep_Info();

	void NotifyInfoChanged();
};
