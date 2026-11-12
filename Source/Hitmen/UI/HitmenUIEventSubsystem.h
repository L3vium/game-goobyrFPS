// Hitmen — event hub between gameplay and UI (no widget holds a hard reference to gameplay)

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/LocalPlayerSubsystem.h"
#include "Core/HitmenTypes.h"
#include "HitmenUIEventSubsystem.generated.h"

class APawn;
class APlayerController;
class UTextureRenderTarget2D;
class UTexture2D;

/** One inventory slot as the HUD shows it. */
USTRUCT(BlueprintType)
struct FHitmenHUDSlotView
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") FText Name;
	/** Remaining ammo, -1 for weapons without ammo. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") int32 Ammo = -1;
	/** Ammo the weapon can hold (shown as "Ammo / MaxAmmo"). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") int32 MaxAmmo = -1;
	/** The weapon's icon (DA_Weapon_* Icon), null = name only. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") TObjectPtr<UTexture2D> Icon = nullptr;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bEmpty = true;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bActive = false;
};

/** Weapon slots and acquired keycards. */
USTRUCT(BlueprintType)
struct FHitmenHUDInventoryView
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") TArray<FHitmenHUDSlotView> Slots;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") TArray<int32> Keycards;
	/** A body is in the arms: it fills the left card ("BODY" / "TARGET"). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bCarryingBody = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bCarryingVIP = false;
};

/** One ability (E move / A manipulation) as the HUD shows it. */
USTRUCT(BlueprintType)
struct FHitmenHUDAbilityView
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bValid = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") FText Name;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") int32 Charges = 0;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") int32 MaxCharges = 0;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") float CooldownRemaining = 0.f;
	/** Cooldown progress 0..1 (1 = ready). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") float CooldownAlpha = 1.f;
	/** A resource gauge 0..1 (jetpack fuel), -1 = none. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") float Meter = -1.f;
	/** The ability's icon (DA_Ability_* Icon). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") TObjectPtr<UTexture2D> Icon = nullptr;
};

USTRUCT(BlueprintType)
struct FHitmenHUDVitalsView
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") float Health = 0.f;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") float Armor = 0.f;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") float HealthNormalized = 1.f;
};

/** An NPC as the minimap / crosshair alert arcs show it. */
USTRUCT(BlueprintType)
struct FHitmenHUDAlertBlip
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") FVector Location = FVector::ZeroVector;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") EHitmenAlertColor Color = EHitmenAlertColor::None;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") EHitmenAlertState State = EHitmenAlertState::Unaware;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") float Suspicion = 0.f;
	/** The NPC is aware of the viewing player (crosshair arcs only show these). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bTargetsViewer = false;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bCallingReinforcements = false;
	/** A security camera rather than an NPC (awareness arcs show a camera mark). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") bool bIsCamera = false;
};

/** A hint anchored on a world actor (remote-ability target: glow + action under it). */
USTRUCT(BlueprintType)
struct FHitmenWorldHint
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") TWeakObjectPtr<AActor> Target;
	/** Key label, e.g. "A" or "E". */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") FText Key;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|HUD") FText Action;

	bool IsSet() const { return Target.IsValid(); }
};

/** One player's live view for the spectator grid. */
USTRUCT(BlueprintType)
struct FHitmenSpectatorView
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Spectator") TObjectPtr<UTextureRenderTarget2D> RenderTarget = nullptr;
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Spectator") FText PlayerName;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenPromptChanged, const FText&, Prompt, bool, bVisible);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenHitMarker, bool, bKill, bool, bHeadshot);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenToggle, bool, bOpen);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenPawnChanged, APawn*, NewPawn);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSonar, const FHitmenSonarPing&, Ping);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenPhase, EHitmenMatchPhase, Phase);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenResult, const FHitmenMatchResult&, Result);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenDamageTaken, float, DirectionYawDegrees);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSpectatorMode, uint8, Mode);
DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenSimpleEvent);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenInventoryView, const FHitmenHUDInventoryView&, View);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenAbilityViews, const FHitmenHUDAbilityView&, Move, const FHitmenHUDAbilityView&, Manipulation);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenVitals, const FHitmenHUDVitalsView&, View);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSpectatorViews, const TArray<FHitmenSpectatorView>&, Views);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenWorldHint, const FHitmenWorldHint&, Hint);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenTextChanged, const FText&, Text);

/**
 * Local-player event hub. Gameplay code calls the Broadcast* helpers; widgets bind in
 * NativeConstruct. Lives on the local player, so it survives level travel and pawn changes.
 */
UCLASS()
class HITMEN_API UHitmenUIEventSubsystem : public ULocalPlayerSubsystem
{
	GENERATED_BODY()

public:
	/** Finds the subsystem for a local controller (null on servers for remote players). */
	static UHitmenUIEventSubsystem* Get(const APlayerController* PC);
	static UHitmenUIEventSubsystem* GetFromPawn(const APawn* Pawn);

	UFUNCTION(BlueprintPure, Category="Hitmen|UI", meta=(WorldContext="WorldContextObject", DisplayName="Get Hitmen UI Events"))
	static UHitmenUIEventSubsystem* GetUIEvents(const UObject* WorldContextObject);

	/** Possessed pawn changed (HUD rebinds to the new pawn's components). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenPawnChanged OnPawnChanged;

	/** Centre-screen interaction prompt ("Open", "Swap", "Climb"...). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenPromptChanged OnInteractionPromptChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenHitMarker OnHitMarker;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenDamageTaken OnDamageTaken;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenToggle OnPauseMenuToggled;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenToggle OnEmoteWheelToggled;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenToggle OnSightroomToggled;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenToggle OnOptionsToggled;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenSonar OnSonarPing;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenPhase OnMatchPhaseChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenResult OnMatchEnded;

	/** Lobby data (teams, players, options) changed: lobby widgets refresh. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenSimpleEvent OnLobbyChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenSpectatorMode OnSpectatorModeChanged;

	// ---- HUD view models (pushed by the player controller; widgets never touch gameplay) ----
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenInventoryView OnInventoryViewChanged;

	/** Refreshed a few times per second while abilities exist (cooldowns). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenAbilityViews OnAbilityViewsChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenVitals OnVitalsChanged;

	/** The local pawn started / stopped carrying the VIP body. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenToggle OnCarryingVIPChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenSimpleEvent OnEmotesChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenSpectatorViews OnSpectatorViewsChanged;

	void SetInventoryView(const FHitmenHUDInventoryView& View) { InventoryView = View; OnInventoryViewChanged.Broadcast(InventoryView); }
	void SetAbilityViews(const FHitmenHUDAbilityView& Move, const FHitmenHUDAbilityView& Manipulation) { MoveAbilityView = Move; ManipulationAbilityView = Manipulation; OnAbilityViewsChanged.Broadcast(Move, Manipulation); }
	void SetVitals(const FHitmenHUDVitalsView& View) { VitalsView = View; OnVitalsChanged.Broadcast(VitalsView); }
	void SetCarryingVIP(bool bCarrying) { if (bCarryingVIP != bCarrying) { bCarryingVIP = bCarrying; OnCarryingVIPChanged.Broadcast(bCarrying); } }
	void SetEmoteNames(const TArray<FText>& Names) { EmoteNames = Names; OnEmotesChanged.Broadcast(); }
	void SetSpectatorViews(const TArray<FHitmenSpectatorView>& Views) { SpectatorViews = Views; OnSpectatorViewsChanged.Broadcast(SpectatorViews); }
	void SetCrosshairSpread(float Degrees) { CrosshairSpread = Degrees; }

	/** Remote-ability target under the crosshair (or none). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenWorldHint OnAbilityHintChanged;

	/** What Space does in front of the player ("Vault", "Climb"...), empty = nothing. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenTextChanged OnTraversalHintChanged;

	/** Scoped aiming (sniper overlay). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|UI")
	FOnHitmenToggle OnScopeToggled;

	void SetAbilityHint(const FHitmenWorldHint& Hint)
	{
		if (Hint.Target != AbilityHint.Target || !Hint.Action.EqualTo(AbilityHint.Action))
		{
			AbilityHint = Hint;
			OnAbilityHintChanged.Broadcast(AbilityHint);
		}
	}
	void SetTraversalHint(const FText& Hint)
	{
		if (!Hint.EqualTo(TraversalHint))
		{
			TraversalHint = Hint;
			OnTraversalHintChanged.Broadcast(TraversalHint);
		}
	}
	void SetScoped(bool bInScoped) { if (bScoped != bInScoped) { bScoped = bInScoped; OnScopeToggled.Broadcast(bScoped); } }
	/** Hold-to-interact progress 0..1 (hacking a panel...), negative = no hold in progress. */
	void SetInteractProgress(float Progress) { InteractProgress = Progress; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") float GetInteractProgress() const { return InteractProgress; }

	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const FHitmenWorldHint& GetAbilityHint() const { return AbilityHint; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const FText& GetTraversalHint() const { return TraversalHint; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") bool IsScoped() const { return bScoped; }
	void SetAlertBlips(TArray<FHitmenHUDAlertBlip>&& Blips) { AlertBlips = MoveTemp(Blips); }

	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const FHitmenHUDInventoryView& GetInventoryView() const { return InventoryView; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const FHitmenHUDAbilityView& GetMoveAbilityView() const { return MoveAbilityView; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const FHitmenHUDAbilityView& GetManipulationAbilityView() const { return ManipulationAbilityView; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const FHitmenHUDVitalsView& GetVitals() const { return VitalsView; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") bool IsCarryingVIP() const { return bCarryingVIP; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const TArray<FText>& GetEmoteNames() const { return EmoteNames; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const TArray<FHitmenSpectatorView>& GetSpectatorViews() const { return SpectatorViews; }
	/** Current weapon spread in degrees (crosshair ring). */
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") float GetCrosshairSpread() const { return CrosshairSpread; }
	UFUNCTION(BlueprintPure, Category="Hitmen|UI") const TArray<FHitmenHUDAlertBlip>& GetAlertBlips() const { return AlertBlips; }

	// ---- Broadcast helpers (keep call sites one-liners) ----
	void BroadcastPawnChanged(APawn* NewPawn) { OnPawnChanged.Broadcast(NewPawn); }
	void BroadcastPrompt(const FText& Prompt, bool bVisible) { OnInteractionPromptChanged.Broadcast(Prompt, bVisible); }
	void BroadcastHitMarker(bool bKill, bool bHeadshot) { OnHitMarker.Broadcast(bKill, bHeadshot); }
	void BroadcastDamageTaken(float Yaw) { OnDamageTaken.Broadcast(Yaw); }
	void BroadcastSonar(const FHitmenSonarPing& Ping) { OnSonarPing.Broadcast(Ping); }
	void BroadcastPhase(EHitmenMatchPhase Phase) { OnMatchPhaseChanged.Broadcast(Phase); }
	void BroadcastResult(const FHitmenMatchResult& Result) { OnMatchEnded.Broadcast(Result); }
	void BroadcastLobbyChanged() { OnLobbyChanged.Broadcast(); }

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetPauseMenuOpen(bool bOpen);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetOptionsOpen(bool bOpen);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetEmoteWheelOpen(bool bOpen);

	UFUNCTION(BlueprintCallable, Category="Hitmen|UI")
	void SetSightroomActive(bool bActive);

	UFUNCTION(BlueprintPure, Category="Hitmen|UI")
	bool IsPauseMenuOpen() const { return bPauseMenuOpen; }

	UFUNCTION(BlueprintPure, Category="Hitmen|UI")
	bool IsOptionsOpen() const { return bOptionsOpen; }

	UFUNCTION(BlueprintPure, Category="Hitmen|UI")
	bool IsEmoteWheelOpen() const { return bEmoteWheelOpen; }

private:
	UPROPERTY(Transient) FHitmenHUDInventoryView InventoryView;
	UPROPERTY(Transient) FHitmenHUDAbilityView MoveAbilityView;
	UPROPERTY(Transient) FHitmenHUDAbilityView ManipulationAbilityView;
	UPROPERTY(Transient) FHitmenHUDVitalsView VitalsView;
	UPROPERTY(Transient) TArray<FText> EmoteNames;
	UPROPERTY(Transient) TArray<FHitmenSpectatorView> SpectatorViews;
	UPROPERTY(Transient) TArray<FHitmenHUDAlertBlip> AlertBlips;
	float CrosshairSpread = 1.f;
	bool bCarryingVIP = false;
	bool bPauseMenuOpen = false;
	bool bOptionsOpen = false;
	bool bEmoteWheelOpen = false;
	bool bSightroomActive = false;

	// ---- Hints (world-anchored ability target, traversal, scope) ----
	FHitmenWorldHint AbilityHint;
	FText TraversalHint;
	bool bScoped = false;
	float InteractProgress = -1.f;
};
