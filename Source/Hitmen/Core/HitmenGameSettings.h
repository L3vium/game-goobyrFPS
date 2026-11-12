// Hitmen — GD tool: Project Settings -> Game -> Hitmen

#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "HitmenGameSettings.generated.h"

class UHitmenMatchRules;
class UHitmenMapDefinition;
class UHitmenCatalog;
class UHitmenSoundSet;
class UBehaviorTree;

/**
 * Project-wide defaults editable from Project Settings. Gameplay code reads these through
 * UHitmenGameSettings::Get(); no class should hard-code an asset path.
 */
UCLASS(Config=Game, DefaultConfig, meta=(DisplayName="Hitmen"))
class HITMEN_API UHitmenGameSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	UHitmenGameSettings();

	static const UHitmenGameSettings* Get() { return GetDefault<UHitmenGameSettings>(); }

	virtual FName GetCategoryName() const override { return TEXT("Game"); }

	// ---- Data ----
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Data")
	TSoftObjectPtr<UHitmenMatchRules> DefaultRules;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Data")
	TSoftObjectPtr<UHitmenCatalog> Catalog;

	/** Every placeholder sound (UI, fixtures, explosions, heartbeat...). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Data")
	TSoftObjectPtr<UHitmenSoundSet> SoundSet;

	/** Maps in carousel order. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Data")
	TArray<TSoftObjectPtr<UHitmenMapDefinition>> Maps;

	// ---- AI ----
	/** Tree run by an AHitmenAIController that has none of its own (charmed players possessed by the
	 *  bare C++ controller). NPC controller Blueprints set their own tree. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="AI")
	TSoftObjectPtr<UBehaviorTree> DefaultBehaviorTree;

	// ---- Combat ----
	/** A bullet or arrow in the head always kills (the multiplier no longer matters). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Combat")
	bool bHeadshotKills = true;

	/** Also for NPC shots (off: guards would one-shot players through their aim error). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Combat", meta=(EditCondition="bHeadshotKills"))
	bool bHeadshotKillsFromNPCs = false;

	// ---- Levels ----
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Levels")
	TSoftObjectPtr<UWorld> TitleLevel;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Levels")
	TSoftObjectPtr<UWorld> LobbyLevel;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Levels")
	TSoftObjectPtr<UWorld> GymLevel;

	// ---- Online ----
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Online", meta=(ClampMin="1", ClampMax="16"))
	int32 MaxPlayers = 8;

	/** Session search result cap. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Online", meta=(ClampMin="1"))
	int32 MaxSearchResults = 50;

	// ---- Debug ----
	/** Shows the debug button on the title screen. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Debug")
	bool bShowDebugButton = true;

	/** Allows cheat commands in Shipping builds (keep off for releases). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Debug")
	bool bAllowCheatsInShipping = false;

	UHitmenMatchRules* LoadDefaultRules() const;
	UHitmenCatalog* LoadCatalog() const;
	UHitmenMapDefinition* LoadMap(int32 Index) const;
	int32 GetMapCount() const { return Maps.Num(); }
};
