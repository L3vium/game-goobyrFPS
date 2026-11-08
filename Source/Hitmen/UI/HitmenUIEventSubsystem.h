// Hitmen — event hub between gameplay and UI (no widget holds a hard reference to gameplay)

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/LocalPlayerSubsystem.h"
#include "Core/HitmenTypes.h"
#include "HitmenUIEventSubsystem.generated.h"

class APawn;
class APlayerController;

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
	bool bPauseMenuOpen = false;
	bool bOptionsOpen = false;
	bool bEmoteWheelOpen = false;
	bool bSightroomActive = false;
};
