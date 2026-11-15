// Hitmen — owns every input binding, lobby RPCs, client feedback RPCs and spectator modes

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "Core/HitmenTypes.h"
#include "HitmenPlayerController.generated.h"

class UInputMappingContext;
class UInputAction;
class AHitmenPlayerState;
class UHitmenWeaponData;
class UHitmenAbilityData;
class UHitmenInventoryComponent;
class UHitmenAbilityComponent;
class UHitmenHealthComponent;
class UHitmenBodyCarryComponent;
class UTextureRenderTarget2D;
class AHitmenCharacterBase;
class UHitmenVIPTrackerComponent;
struct FInputActionValue;

/** How a watcher (or a dead player) looks at the match: keys 1 (player view), 2 (split screen), 3 (free fly). */
UENUM(BlueprintType)
enum class EHitmenSpectatorMode : uint8
{
	FreeFly,
	/** Player view: one player, first or third person (V); LMB / RMB switch player. */
	Follow,
	/** Split screen: every living player at once (up to 8, render targets). */
	Grid
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSpectatorTargetChanged, APawn*, Target);

/**
 * Input lives here, not in the pawn: swapping the 3C Blueprint keeps every binding.
 * Gameplay inputs are forwarded to the possessed pawn through IHitmenInputReceiver.
 */
UCLASS()
class HITMEN_API AHitmenPlayerController : public APlayerController
{
	GENERATED_BODY()

public:
	AHitmenPlayerController();

	virtual void BeginPlay() override;
	virtual void SetupInputComponent() override;
	virtual void SetPawn(APawn* InPawn) override;
	virtual void OnPossess(APawn* InPawn) override;

	// ---- Lobby (client -> server) ----
	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetPlayerName(const FString& NewName);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetIcon(int32 IconIndex);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetCosmetics(const FHitmenCosmeticLoadout& Cosmetics);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetLoadout(const FHitmenLoadout& Loadout);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetReady(bool bReady);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_CreateTeam();

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_RemoveTeam(int32 TeamId);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_AssignTeam(AHitmenPlayerState* Target, int32 TeamId);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetTeamRole(int32 TeamId, EHitmenTeamRole NewRole);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetWatcher(AHitmenPlayerState* Target, bool bWatcher);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_SetLobbySettings(const FHitmenLobbySettings& Settings);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_StartCustomization();

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_LaunchMatch();

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Lobby")
	void Server_BackToLobby();

	/** Match over: the host sends everyone back to the lobby. */
	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Match")
	void Server_ReturnToLobby();

	/** Results banner / pause menu / Enter: asks the server when this is the host and the match is over. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Match")
	void RequestReturnToLobby();

	/** The match on this map has ended (results banner, everyone spectating). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Match")
	bool IsMatchOver() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Lobby")
	bool IsHost() const;

	// ---- Feedback (server -> owning client) ----
	UFUNCTION(Client, Unreliable)
	void Client_HitConfirmed(bool bKill, bool bHeadshot);

	UFUNCTION(Client, Unreliable)
	void Client_DamageTaken(FVector_NetQuantize SourceLocation);

	/** Shows / hides the last-known-position silhouette where NPCs think you are. */
	UFUNCTION(Client, Reliable)
	void Client_ShowLastKnownPosition(FVector_NetQuantize Location, bool bShow);

	UFUNCTION(Client, Reliable)
	void Client_EnterSpectatorMode(bool bWatcher);

	/** Owner -> server while spectating: where this watcher looks and whom it follows (avatars, tags, watched line). */
	UFUNCTION(Server, Unreliable)
	void Server_ReportWatcherView(FVector_NetQuantize10 Location, float Yaw, float Pitch, AHitmenPlayerState* Watched);

	// ---- Event camera (picture in picture, top left) ----
	/** Local: films Subject for a few seconds (Project Settings -> Hitmen -> HUD -> EventCameraDuration). */
	void ShowEventCamera(AActor* Subject, const FText& Title);
	void StopEventCamera();

	/** Server -> the player who caused it: a guard comes to look at your distraction. */
	UFUNCTION(Client, Reliable)
	void Client_ShowDistractionCamera(AActor* Guard);

	/** Server: a guard starts investigating a distraction; the player who switched it on (or threw it) sees it. */
	static void NotifyDistractionNoticed(AActor* Guard, AActor* NoiseInstigator);

	UHitmenVIPTrackerComponent* GetVIPTracker() const { return VIPTracker; }

	// ---- Debug grants (gym / development) ----
	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugGiveWeapon(UHitmenWeaponData* Weapon);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugGiveAbility(UHitmenAbilityData* Ability);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugGiveKeycard(int32 KeycardId);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugSetGodMode(bool bEnabled);

	/** F1-F8: toggles a cheat (invulnerable, ammo, cooldowns, invisible, fly) or runs one (respawn, kill, reset). */
	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugCheat(EHitmenCheat Cheat);

	/** Console, gym and dev builds: leave your body and watch as a watcher (QA of the watcher mode). */
	UFUNCTION(Exec)
	void HitmenWatch();

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugWatch();

	/** Fly cheat on the current pawn (flying, no collision) or back to walking; server and owning client. */
	void ApplyCheatsToPawn();

	// ---- Emotes / UI ----
	/** Called by the emote wheel when the player picks an emote. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SelectEmote(int32 EmoteIndex);

	/**
	 * Switches input between game and UI (cursor visible). bUIOnly: front-end screens, where no
	 * click ever reaches the game viewport (it would capture and hide the cursor).
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetMenuInputMode(bool bMenu, bool bUIOnly = false);

	/** Asks the HUD to re-evaluate the input mode (menus, pause, wheel...). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void RefreshInputMode();

	// ---- Spectator ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Spectator")
	bool IsSpectating() const { return bSpectating; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Spectator")
	EHitmenSpectatorMode GetSpectatorMode() const { return SpectatorMode; }

	UFUNCTION(BlueprintCallable, Category="Hitmen|Spectator")
	void SetSpectatorMode(EHitmenSpectatorMode NewMode);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Spectator")
	void CycleSpectatorMode();

	UFUNCTION(BlueprintCallable, Category="Hitmen|Spectator")
	void SpectateNext();

	UFUNCTION(BlueprintCallable, Category="Hitmen|Spectator")
	void SpectatePrevious();

	/** Pawns this spectator may look at (watchers: everyone; dead players: teammates first). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Spectator")
	TArray<APawn*> GetSpectatableTargets() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Spectator")
	APawn* GetSpectatorTarget() const { return SpectatorTarget.Get(); }

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Spectator")
	FOnHitmenSpectatorTargetChanged OnSpectatorTargetChanged;

	/** Player view in third person (V). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Spectator")
	bool IsSpectatorThirdPerson() const { return bSpectatorThirdPerson; }

	/** Player view: first (false) or third person (true, over the shoulder, the mouse orbits); V toggles it. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Spectator")
	void SetSpectatorThirdPerson(bool bThirdPerson);

	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

protected:
	// ---- Input assets (set in BP_PlayerController) ----
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Contexts")
	TArray<TObjectPtr<UInputMappingContext>> GameplayMappingContexts;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Contexts")
	TObjectPtr<UInputMappingContext> SpectatorMappingContext;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> MoveAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> LookAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> JumpAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> CrouchAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SprintAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> FireAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> AimAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> InteractAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> MoveAbilityAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> ManipulationAbilityAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SwitchSlotAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SightroomAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> EmoteWheelAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> PauseAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SpectatorModeAction;
	/** Free fly (IMC_Spectator): up / down / faster, rebindable in Options > Controls > Spectator. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SpectatorUpAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SpectatorDownAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SpectatorFastAction;
	/** Watcher views (IMC_Spectator): 1 player view, 2 split screen, 3 free fly; N / B show or hide players / watchers. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SpectatorPlayerViewAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SpectatorSplitViewAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> SpectatorFreeViewAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> WatcherTogglePlayersAction;
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> WatcherToggleWatchersAction;
	/** Enter: confirm (match over: the host goes back to the lobby). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input|Actions") TObjectPtr<UInputAction> ConfirmAction;

	/** Local-only silhouette spawned where NPCs last saw this player. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Feedback")
	TSubclassOf<AActor> LastKnownPositionMarkerClass;

	/** Added to the reported position (the capsule centre); -90 Z puts a mannequin's feet on the floor. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Feedback")
	FVector LastKnownPositionMarkerOffset = FVector(0.f, 0.f, -90.f);

	UPROPERTY(Transient)
	TObjectPtr<AActor> LastKnownPositionMarker;

	/** The dead VIP for this player: toast, kill cam, yellow body / ghost, gold retrieval points, end cinematic. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UHitmenVIPTrackerComponent> VIPTracker;

	UPROPERTY(Transient)
	TObjectPtr<class AHitmenEventCamera> EventCamera;

	/** Server: distraction event cameras for this player at most this often. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD", meta=(ClampMin="0", Units="s"))
	float DistractionCameraCooldown = 8.f;

	float LastDistractionCameraTime = -100.f;

	// ---- Sightroom (Tab) ----
	/** Enemies inside this radius are shown through walls while the Sightroom is on. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float SightroomEnemyRadius = 2000.f;

	/** Interactables inside this radius are highlighted when directly visible. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float SightroomInteractableRadius = 1800.f;

	// ---- HUD publishing ----
	/** Seconds between ability cooldown / spread / alert refreshes pushed to the HUD. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD", meta=(ClampMin="0.02", Units="s"))
	float HUDRefreshInterval = 0.1f;

	// ---- Spectator: split screen, third person ----
	/** Width of each split screen picture (up to 4 views); its height follows the cell's shape on screen. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="128"))
	int32 SpectatorCaptureWidth = 960;

	/** Width of each picture above 4 views. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="128"))
	int32 SpectatorSmallCaptureWidth = 640;

	/** Each split screen picture is redrawn this often (captures are staggered across frames). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="1", ClampMax="60", Units="Hz"))
	float SpectatorCaptureRate = 20.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="1", ClampMax="8"))
	int32 MaxSplitViews = 8;

	/** Player view in third person: camera distance behind the player. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="50", Units="cm"))
	float SpectatorThirdPersonDistance = 320.f;

	/** Seconds between grid refreshes (players joining / dying). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0.25", Units="s"))
	float SpectatorGridRefreshInterval = 2.f;

	// ---- Juice hooks (local) ----
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnHitConfirmed(bool bKill, bool bHeadshot);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDamageTaken(float DirectionYaw);

	// ---- Input handlers ----
	void HandleMove(const FInputActionValue& Value);
	void HandleLook(const FInputActionValue& Value);
	void HandleJumpPressed();
	void HandleJumpReleased();
	void HandleCrouchPressed();
	void HandleCrouchReleased();
	void HandleSprintPressed();
	void HandleSprintReleased();
	void HandleFirePressed();
	void HandleFireReleased();
	void HandleAimPressed();
	void HandleAimReleased();
	void HandleInteract();
	void HandleInteractReleased();
	void HandleMoveAbilityPressed();
	void HandleMoveAbilityReleased();
	void HandleManipulationPressed();
	void HandleManipulationReleased();
	void HandleSwitchSlot(const FInputActionValue& Value);
	void HandleSightroomPressed();
	void HandleSightroomReleased();
	void HandleEmoteWheelPressed();
	void HandleEmoteWheelReleased();
	void HandlePause();
	void HandleSpectatorMode();
	void HandleSpectatorUpPressed();
	void HandleSpectatorUpReleased();
	void HandleSpectatorDownPressed();
	void HandleSpectatorDownReleased();
	void HandleSpectatorFastPressed();
	void HandleSpectatorFastReleased();
	void HandleConfirm();
	void HandleCheatKey(FKey Key);
	bool bFlyUpHeld = false;
	bool bFlyDownHeld = false;
	bool IsCheatFlying() const;

	/** Fly cheat speed. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Debug", meta=(ClampMin="0", Units="cm/s"))
	float CheatFlySpeed = 1800.f;
	void PushSpectatorVertical();
	bool bSpectatorUpHeld = false;
	bool bSpectatorDownHeld = false;

	/** Pushes the current key of each prompt action to the UI hub (after rebinding too). */
	void RefreshKeyLabels();
	UFUNCTION() void HandleInputUserSettingsChanged(class UEnhancedInputUserSettings* Settings);

	// ---- HUD publisher: binds to the pawn's components and pushes view models to the UI hub ----
	void BindHUDSources(APawn* InPawn);
	void UnbindHUDSources();
	void RefreshHUDViews();
	void PublishEmotes(APawn* InPawn);
	UFUNCTION() void HandleHUDInventoryChanged();
	UFUNCTION() void HandleHUDActiveSlotChanged(int32 NewSlot);
	UFUNCTION() void HandleHUDHealthChanged(UHitmenHealthComponent* HealthComponent, float NewHealth, float Delta, AActor* InstigatorActor);
	UFUNCTION() void HandleHUDCarryChanged(AHitmenCharacterBase* Body);

	UPROPERTY(Transient) TWeakObjectPtr<UHitmenInventoryComponent> HUDInventory;
	UPROPERTY(Transient) TWeakObjectPtr<UHitmenAbilityComponent> HUDAbilities;
	UPROPERTY(Transient) TWeakObjectPtr<UHitmenHealthComponent> HUDHealth;
	UPROPERTY(Transient) TWeakObjectPtr<UHitmenBodyCarryComponent> HUDCarry;
	FTimerHandle HUDRefreshTimer;

	// ---- Spectator grid captures (owned here, shown by the spectator widget through the UI hub) ----
	void RebuildSpectatorGrid();
	void StopSpectatorCaptures();

	UPROPERTY(Transient) TArray<TObjectPtr<UTextureRenderTarget2D>> SpectatorRenderTargets;
	TArray<TWeakObjectPtr<APawn>> CapturedPawns;
	FTimerHandle SpectatorGridTimer;
	int32 SpectatorCaptureCursor = 0;
	float SpectatorCaptureBudget = 0.f;
	void TickSpectatorCaptures(float DeltaTime);
	void UpdateSpectatorThirdPerson();
	bool bSpectatorThirdPerson = false;

	// ---- Watcher presence (local): view reports, other watchers' avatars, players outlined for watchers ----
	void ReportWatcherView();
	void UpdateWatcherPresence();
	void ClearWatcherPresence();
	bool IsWatcherPlayersShown() const;
	bool IsWatcherWatchersShown() const;
	void HandleSpectatorPlayerView();
	void HandleSpectatorSplitView();
	void HandleSpectatorFreeView();
	void HandleWatcherTogglePlayers();
	void HandleWatcherToggleWatchers();
	void PushWatcherOverlay();
	UPROPERTY(Transient) TArray<TObjectPtr<class AHitmenWatcherAvatar>> WatcherAvatars;
	TSet<TWeakObjectPtr<AActor>> WatcherHighlightedPlayers;
	FTimerHandle WatcherPresenceTimer;
	float LastWatcherReportTime = -100.f;
	FVector LastReportedLocation = FVector::ZeroVector;
	FRotator LastReportedRotation = FRotator::ZeroRotator;
	TWeakObjectPtr<AHitmenPlayerState> LastReportedWatched;
	bool bWatcherShowPlayers = true;
	bool bWatcherShowWatchers = true;

	class IHitmenInputReceiver* GetReceiver() const;
	bool IsGameplayInputBlocked() const;
	void RefreshMappingContexts();
	void SendPreferredProfile();
	void ApplySpectatorView();

	UPROPERTY(Transient)
	TWeakObjectPtr<APawn> SpectatorTarget;

	EHitmenSpectatorMode SpectatorMode = EHitmenSpectatorMode::FreeFly;
	bool bSpectating = false;
	bool bIsWatcher = false;
	bool bCrouchToggled = false;
	bool bSprintToggled = false;

	/** Shift released within this time of the press = a tap (dash when equipped); held = sprint. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Input", meta=(ClampMin="0", Units="s"))
	float SprintTapTime = 0.18f;

	float SprintPressTime = -100.f;

	virtual void PlayerTick(float DeltaTime) override;

	/** Camera feed top left while a security camera sees you (Project Settings -> Hitmen -> HUD -> bCameraFeedWhenSeen). */
	void UpdateSecurityFeed(float DeltaTime);
	float SecurityFeedAccumulator = 0.f;
	TWeakObjectPtr<AActor> SecurityFeedCamera;

	/** Fly cheat: Shift multiplies the fly speed by this. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Cheats", meta=(ClampMin="1"))
	float CheatFlyFastMultiplier = 3.f;
	bool bFlyFastHeld = false;

	/** Fly cheat speed on the server too (the movement is predicted: both sides must agree). */
	UFUNCTION(Server, Reliable)
	void Server_SetCheatFlyFast(bool bFast);
	void ApplyCheatFlySpeed(bool bFast);

	/** Vehicle HUD (health, reload, exit): pushed every frame while driving. */
	void PushVehicleView();
	bool bVehicleViewActive = false;
};
