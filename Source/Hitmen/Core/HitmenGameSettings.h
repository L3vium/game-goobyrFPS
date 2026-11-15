// Hitmen — GD tool: Project Settings -> Game -> Hitmen

#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "HitmenGameSettings.generated.h"

class UHitmenMatchRules;
class UHitmenMapDefinition;
class UHitmenCatalog;
class UHitmenSoundSet;
class UHitmenBarkSet;
class UBehaviorTree;
class UMaterialInterface;

/**
 * Project-wide defaults editable from Project Settings. Gameplay code reads these through
 * UHitmenGameSettings::Get(); no class should hard-code an asset path.
 */
/** What watchers see of players / other watchers (Project Settings -> Hitmen -> Watchers). */
UENUM(BlueprintType)
enum class EHitmenWatcherVisibility : uint8
{
	Hidden,
	Shown,
	/** Shown, and the watcher can hide them (N players, B watchers). */
	ShownToggleable
};

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

	/** What NPCs say when their awareness changes (subtitles for nearby players, optional voices). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="AI")
	TSoftObjectPtr<UHitmenBarkSet> BarkSet;

	// ---- Visuals ----
	/** Unlit additive material for bullet tracers (vector "Color", scalar "Opacity"). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Visuals")
	TSoftObjectPtr<UMaterialInterface> TracerMaterial;

	/** Unlit additive material for muzzle glows and flashes (vector "Color", scalar "Opacity"). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Visuals")
	TSoftObjectPtr<UMaterialInterface> FlashMaterial;

	/** Translucent material for the smoke trail a shot leaves (weapon TracerStyle.SmokeLifetime; scalar "Opacity"). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Visuals")
	TSoftObjectPtr<UMaterialInterface> SmokeTrailMaterial;

	// ---- Juice (every effect keeps its asset slot; these switches decide whether it plays) ----
	/** A puff at the hands at each step up a ladder or gutter (the grab sound always plays). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Juice")
	bool bLadderClimbFX = false;

	/** A puff while going down a ladder or gutter (LadderDescendFX on the character). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Juice")
	bool bLadderDescendFX = false;

	/** Dust along a slide. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Juice")
	bool bSlideFX = false;

	/** The scrape loop while sliding (EHitmenSound::Slide). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Juice")
	bool bSlideSound = true;

	/** A puff under the foot on each step and landing (the Effect of each surface in DA_SoundSet). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Juice")
	bool bFootstepFX = false;

	// ---- HUD ----
	/** A small camera top left shows the VIP's body for a few seconds when it dies (everyone). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="HUD")
	bool bEventCameraOnTargetKill = true;

	/** A small camera top left shows the guard who comes to look at a distraction you switched on (you only). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="HUD")
	bool bEventCameraOnDistraction = true;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="HUD", meta=(ClampMin="1", Units="s"))
	float EventCameraDuration = 4.f;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="HUD")
	FIntPoint EventCameraResolution = FIntPoint(480, 270);

	/** Once the VIP is dead, active retrieval points show gold through walls for everyone. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="HUD")
	bool bShowRetrievalPointsAfterVIPDeath = true;

	/** Always-on post process drawing the VIP marker (yellow) and retrieval points (gold) through walls. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="HUD")
	TSoftObjectPtr<UMaterialInterface> MarkerPostProcessMaterial;

	// ---- Maps (minimap and pause map layers; the pause map legend lists only what is drawn) ----
	/** Minimap: every living NPC as a diamond (grey calm, white suspicious, yellow alerted, red in combat) and the
	 *  security cameras (marker kinds on the NPCs layer). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps")
	bool bMinimapShowNPCs = true;

	/** Minimap: a vision cone in front of each NPC drawn, fading out toward its sight radius. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps", meta=(EditCondition="bMinimapShowNPCs"))
	bool bMinimapShowVisionCones = false;

	/** Minimap: marker kinds on the Interactables layer (hiding spots, distractions, containers, keycards, devices,
	 *  hazards). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps")
	bool bMinimapShowInteractables = false;

	/** Pause map: NPCs and cameras, as on the minimap. Off: the pause map shows you, the VIP and retrieval points. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps")
	bool bPauseMapShowNPCs = false;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps", meta=(EditCondition="bPauseMapShowNPCs"))
	bool bPauseMapShowVisionCones = false;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps")
	bool bPauseMapShowInteractables = false;

	/** Pause map: your teammates (only when you have some). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps")
	bool bPauseMapShowTeammates = true;

	/** Vision cone opacity at the NPC; it fades to nothing at the NPC's sight radius. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps", meta=(ClampMin="0", ClampMax="1"))
	float MapVisionConeOpacity = 0.35f;

	/** NPC diamond size on the minimap and the pause map (pixels, tip to tip). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps", meta=(ClampMin="2"))
	float MinimapNPCSize = 9.f;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Maps", meta=(ClampMin="2"))
	float PauseMapNPCSize = 13.f;

	// ---- Watchers (spectating: watchers and dead players) ----
	/** Free fly goes through walls and objects (still inside the map's box). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bWatchersFlyThroughWalls = true;

	/** Free fly goes under the ground too (down to WatcherDepthBelowGround); off: never lower than just above the
	 *  surface under the camera. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bWatchersFlyThroughTerrain = true;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers", meta=(ClampMin="0", Units="cm", EditCondition="bWatchersFlyThroughTerrain"))
	float WatcherDepthBelowGround = 2500.f;

	/** Tab scans for watchers too (free fly and third-person player view). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bWatchersCanScan = true;

	/** Watchers see the players outlined through walls with their name: hidden, shown, or shown with N to hide them. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	EHitmenWatcherVisibility WatcherSeePlayers = EHitmenWatcherVisibility::ShownToggleable;

	/** Watchers see the other watchers (a glowing orb with their name): hidden, shown, or shown with B to hide them. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	EHitmenWatcherVisibility WatcherSeeWatchers = EHitmenWatcherVisibility::ShownToggleable;

	/** What a watcher looks at shows its info line (name and state: "Locker - Body inside", "Light - On"), no key. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bWatchersSeeInteractableInfo = true;

	/** What a watcher looks at glows like the player's focus. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bWatchersHighlightInteractables = true;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers", meta=(ClampMin="100", Units="cm"))
	float WatcherFocusRange = 3000.f;

	/** Players see other players' names over their heads (in view, within PlayerNameTagRange). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bPlayersSeeNameTags = false;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers", meta=(ClampMin="0", Units="cm", EditCondition="bPlayersSeeNameTags"))
	float PlayerNameTagRange = 4000.f;

	/** Players see the watchers flying around (orb and name). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bPlayersSeeWatchers = false;

	/** Players see "WATCHED BY n PLAYERS" while watchers follow them in player view. */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers")
	bool bPlayersSeeWatchedIndicator = false;

	/** A watcher's view goes to the server this often while it moves (avatars, tags, the watched line). */
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category="Watchers", meta=(ClampMin="1", ClampMax="30", Units="Hz"))
	float WatcherViewRate = 10.f;

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
