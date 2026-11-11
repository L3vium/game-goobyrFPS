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
struct FInputActionValue;

/** How a watcher (or a dead player) looks at the match. */
UENUM(BlueprintType)
enum class EHitmenSpectatorMode : uint8
{
	FreeFly,
	/** First-person view of one player; switch with next / previous. */
	Follow,
	/** Every player's view at once (render targets). */
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

	// ---- Debug grants (gym / development) ----
	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugGiveWeapon(UHitmenWeaponData* Weapon);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugGiveAbility(UHitmenAbilityData* Ability);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugGiveKeycard(int32 KeycardId);

	UFUNCTION(Server, Reliable, BlueprintCallable, Category="Hitmen|Debug")
	void Server_DebugSetGodMode(bool bEnabled);

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

	/** Grid mode: how many screens are shown (0 = every player). Mouse wheel cycles it. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Spectator")
	int32 GetSpectatorGridCount() const { return SpectatorGridCount; }

	UFUNCTION(BlueprintCallable, Category="Hitmen|Spectator")
	void CycleSpectatorGridCount(int32 Direction);

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

	/** Local-only silhouette spawned where NPCs last saw this player. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Feedback")
	TSubclassOf<AActor> LastKnownPositionMarkerClass;

	/** Added to the reported position (the capsule centre); -90 Z puts a mannequin's feet on the floor. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Feedback")
	FVector LastKnownPositionMarkerOffset = FVector(0.f, 0.f, -90.f);

	UPROPERTY(Transient)
	TObjectPtr<AActor> LastKnownPositionMarker;

	// ---- Sightroom (Tab) ----
	/** Enemies inside this radius are shown through walls while the Sightroom is on. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float SightroomEnemyRadius = 2500.f;

	/** Interactables inside this radius are highlighted when directly visible. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Sightroom", meta=(ClampMin="0", Units="cm"))
	float SightroomInteractableRadius = 1800.f;

	// ---- HUD publishing ----
	/** Seconds between ability cooldown / spread / alert refreshes pushed to the HUD. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|HUD", meta=(ClampMin="0.02", Units="s"))
	float HUDRefreshInterval = 0.1f;

	// ---- Spectator grid ----
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator")
	FIntPoint SpectatorCaptureResolution = FIntPoint(640, 360);

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
	int32 SpectatorGridCount = 0;

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
};
