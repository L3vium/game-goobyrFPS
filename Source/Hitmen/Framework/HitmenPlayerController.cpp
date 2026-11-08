// Hitmen — player controller

#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenLobbyGameMode.h"
#include "Framework/HitmenGameModeBase.h"
#include "Core/HitmenInterfaces.h"
#include "Core/HitmenUserSettings.h"
#include "Core/HitmenTeamLibrary.h"
#include "Online/HitmenSessionSubsystem.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Components/HitmenHealthComponent.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Abilities/HitmenAbilityComponent.h"
#include "Data/HitmenAbilityData.h"
#include "Hitmen.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "UserSettings/EnhancedInputUserSettings.h"
#include "InputMappingContext.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"
#include "GameFramework/SpectatorPawn.h"
#include "Kismet/KismetMathLibrary.h"

AHitmenPlayerController::AHitmenPlayerController()
{
	bShowMouseCursor = false;
}

void AHitmenPlayerController::BeginPlay()
{
	Super::BeginPlay();
	if (IsLocalController())
	{
		RefreshMappingContexts();
		SendPreferredProfile();
		SetMenuInputMode(false);
	}
}

void AHitmenPlayerController::SendPreferredProfile()
{
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	FString Name = Settings ? Settings->PreferredName : FString();
	if (Name.IsEmpty())
	{
		if (const UHitmenSessionSubsystem* Sessions = GetGameInstance() ? GetGameInstance()->GetSubsystem<UHitmenSessionSubsystem>() : nullptr)
		{
			Name = Sessions->GetLocalOnlineName();
		}
	}
	// Empty name = the server keeps the random one it assigned on login.
	if (!Name.IsEmpty())
	{
		Server_SetPlayerName(Name);
	}
	if (Settings)
	{
		Server_SetIcon(Settings->PreferredIconIndex);
	}
}

void AHitmenPlayerController::RefreshMappingContexts()
{
	UEnhancedInputLocalPlayerSubsystem* Subsystem = GetLocalPlayer() ? GetLocalPlayer()->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr;
	if (!Subsystem)
	{
		return;
	}
	Subsystem->ClearAllMappings();
	if (bSpectating && SpectatorMappingContext)
	{
		Subsystem->AddMappingContext(SpectatorMappingContext, 1);
	}
	UEnhancedInputUserSettings* UserSettings = Subsystem->GetUserSettings();
	for (UInputMappingContext* Context : GameplayMappingContexts)
	{
		if (Context)
		{
			Subsystem->AddMappingContext(Context, 0);
			if (UserSettings)
			{
				// Makes player-mappable keys appear in Options and applies saved rebinds.
				UserSettings->RegisterInputMappingContext(Context);
			}
		}
	}
}

void AHitmenPlayerController::SetupInputComponent()
{
	Super::SetupInputComponent();

	UEnhancedInputComponent* Input = Cast<UEnhancedInputComponent>(InputComponent);
	if (!Input)
	{
		UE_LOG(LogHitmen, Error, TEXT("%s: Enhanced Input component missing."), *GetName());
		return;
	}

	auto BindIfSet = [Input, this](UInputAction* Action, ETriggerEvent Event, void (AHitmenPlayerController::*Func)())
	{
		if (Action)
		{
			Input->BindAction(Action, Event, this, Func);
		}
	};

	if (MoveAction)       { Input->BindAction(MoveAction, ETriggerEvent::Triggered, this, &AHitmenPlayerController::HandleMove);
	                        Input->BindAction(MoveAction, ETriggerEvent::Completed, this, &AHitmenPlayerController::HandleMove); }
	if (LookAction)       { Input->BindAction(LookAction, ETriggerEvent::Triggered, this, &AHitmenPlayerController::HandleLook); }
	if (SwitchSlotAction) { Input->BindAction(SwitchSlotAction, ETriggerEvent::Triggered, this, &AHitmenPlayerController::HandleSwitchSlot); }

	BindIfSet(JumpAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleJumpPressed);
	BindIfSet(JumpAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleJumpReleased);
	BindIfSet(CrouchAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleCrouchPressed);
	BindIfSet(CrouchAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleCrouchReleased);
	BindIfSet(SprintAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSprintPressed);
	BindIfSet(SprintAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleSprintReleased);
	BindIfSet(FireAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleFirePressed);
	BindIfSet(FireAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleFireReleased);
	BindIfSet(AimAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleAimPressed);
	BindIfSet(AimAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleAimReleased);
	BindIfSet(InteractAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleInteract);
	BindIfSet(MoveAbilityAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleMoveAbilityPressed);
	BindIfSet(MoveAbilityAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleMoveAbilityReleased);
	BindIfSet(ManipulationAbilityAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleManipulationPressed);
	BindIfSet(ManipulationAbilityAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleManipulationReleased);
	BindIfSet(SightroomAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSightroomPressed);
	BindIfSet(SightroomAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleSightroomReleased);
	BindIfSet(EmoteWheelAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleEmoteWheelPressed);
	BindIfSet(EmoteWheelAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleEmoteWheelReleased);
	BindIfSet(PauseAction, ETriggerEvent::Started, &AHitmenPlayerController::HandlePause);
	BindIfSet(SpectatorModeAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSpectatorMode);
}

void AHitmenPlayerController::SetPawn(APawn* InPawn)
{
	Super::SetPawn(InPawn);
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		UI->BroadcastPawnChanged(InPawn);
	}
	// A new body restarts toggles.
	bCrouchToggled = false;
	bSprintToggled = false;
}

void AHitmenPlayerController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);
	if (InPawn && !InPawn->IsA<ASpectatorPawn>())
	{
		bSpectating = false;
		if (IsLocalController())
		{
			RefreshMappingContexts();
		}
	}
}

IHitmenInputReceiver* AHitmenPlayerController::GetReceiver() const
{
	return Cast<IHitmenInputReceiver>(GetPawn());
}

bool AHitmenPlayerController::IsGameplayInputBlocked() const
{
	const UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	return UI && (UI->IsPauseMenuOpen() || UI->IsOptionsOpen());
}

// ---------------------------------------------------------------------------------------------
// Input handlers
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerController::HandleMove(const FInputActionValue& Value)
{
	if (IHitmenInputReceiver* R = GetReceiver())
	{
		R->Input_Move(IsGameplayInputBlocked() ? FVector2D::ZeroVector : Value.Get<FVector2D>());
	}
}

void AHitmenPlayerController::HandleLook(const FInputActionValue& Value)
{
	if (IsGameplayInputBlocked())
	{
		return;
	}
	FVector2D Axis = Value.Get<FVector2D>();
	if (const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Axis *= Settings->MouseSensitivity;
		if (Settings->bInvertY)
		{
			Axis.Y *= -1.f;
		}
	}
	if (IHitmenInputReceiver* R = GetReceiver())
	{
		R->Input_Look(Axis);
	}
}

void AHitmenPlayerController::HandleJumpPressed()
{
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_JumpPressed(); }
}

void AHitmenPlayerController::HandleJumpReleased()
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_JumpReleased(); }
}

void AHitmenPlayerController::HandleCrouchPressed()
{
	IHitmenInputReceiver* R = GetReceiver();
	if (!R || IsGameplayInputBlocked())
	{
		return;
	}
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (Settings && Settings->bToggleCrouch)
	{
		bCrouchToggled = !bCrouchToggled;
		R->Input_CrouchChanged(bCrouchToggled);
	}
	else
	{
		R->Input_CrouchChanged(true);
	}
}

void AHitmenPlayerController::HandleCrouchReleased()
{
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (Settings && Settings->bToggleCrouch)
	{
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_CrouchChanged(false); }
}

void AHitmenPlayerController::HandleSprintPressed()
{
	IHitmenInputReceiver* R = GetReceiver();
	if (!R || IsGameplayInputBlocked())
	{
		return;
	}
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (Settings && Settings->bToggleSprint)
	{
		bSprintToggled = !bSprintToggled;
		R->Input_SprintChanged(bSprintToggled);
	}
	else
	{
		R->Input_SprintChanged(true);
	}
}

void AHitmenPlayerController::HandleSprintReleased()
{
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (Settings && Settings->bToggleSprint)
	{
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_SprintChanged(false); }
}

void AHitmenPlayerController::HandleFirePressed()
{
	if (bSpectating)
	{
		SpectateNext();
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_FirePressed(); }
}

void AHitmenPlayerController::HandleFireReleased()
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_FireReleased(); }
}

void AHitmenPlayerController::HandleAimPressed()
{
	if (bSpectating)
	{
		SpectatePrevious();
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_AimChanged(true); }
}

void AHitmenPlayerController::HandleAimReleased()
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_AimChanged(false); }
}

void AHitmenPlayerController::HandleInteract()
{
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_Interact(); }
}

void AHitmenPlayerController::HandleMoveAbilityPressed()
{
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_MoveAbilityPressed(); }
}

void AHitmenPlayerController::HandleMoveAbilityReleased()
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_MoveAbilityReleased(); }
}

void AHitmenPlayerController::HandleManipulationPressed()
{
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_ManipulationAbilityPressed(); }
}

void AHitmenPlayerController::HandleManipulationReleased()
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_ManipulationAbilityReleased(); }
}

void AHitmenPlayerController::HandleSwitchSlot(const FInputActionValue& Value)
{
	const float Direction = Value.Get<float>();
	if (FMath::IsNearlyZero(Direction))
	{
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_SwitchSlot(Direction); }
}

void AHitmenPlayerController::HandleSightroomPressed()
{
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked())
	{
		R->Input_SightroomChanged(true);
		if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this)) { UI->SetSightroomActive(true); }
	}
}

void AHitmenPlayerController::HandleSightroomReleased()
{
	if (IHitmenInputReceiver* R = GetReceiver())
	{
		R->Input_SightroomChanged(false);
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this)) { UI->SetSightroomActive(false); }
}

void AHitmenPlayerController::HandleEmoteWheelPressed()
{
	if (IsGameplayInputBlocked() || bSpectating)
	{
		return;
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this)) { UI->SetEmoteWheelOpen(true); }
}

void AHitmenPlayerController::HandleEmoteWheelReleased()
{
	// The wheel widget reads the hovered slot on close and calls SelectEmote.
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this)) { UI->SetEmoteWheelOpen(false); }
}

void AHitmenPlayerController::HandlePause()
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	if (!UI)
	{
		return;
	}
	if (UI->IsOptionsOpen())
	{
		UI->SetOptionsOpen(false);
		return;
	}
	const bool bOpen = !UI->IsPauseMenuOpen();
	UI->SetPauseMenuOpen(bOpen);
	SetMenuInputMode(bOpen);
}

void AHitmenPlayerController::HandleSpectatorMode()
{
	if (bSpectating)
	{
		CycleSpectatorMode();
	}
}

void AHitmenPlayerController::SelectEmote(int32 EmoteIndex)
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_Emote(EmoteIndex); }
}

void AHitmenPlayerController::SetMenuInputMode(bool bMenu)
{
	if (bMenu)
	{
		FInputModeGameAndUI Mode;
		Mode.SetHideCursorDuringCapture(false);
		Mode.SetLockMouseToViewportBehavior(EMouseLockMode::DoNotLock);
		SetInputMode(Mode);
		SetShowMouseCursor(true);
	}
	else
	{
		SetInputMode(FInputModeGameOnly());
		SetShowMouseCursor(false);
	}
}

// ---------------------------------------------------------------------------------------------
// Lobby RPCs
// ---------------------------------------------------------------------------------------------

bool AHitmenPlayerController::IsHost() const
{
	return GetNetMode() != NM_Client && IsLocalController();
}

void AHitmenPlayerController::Server_SetPlayerName_Implementation(const FString& NewName)
{
	const FString Clean = NewName.TrimStartAndEnd().Left(24);
	if (!Clean.IsEmpty() && PlayerState)
	{
		PlayerState->SetPlayerName(Clean);
		if (AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>())
		{
			GS->NotifyLobbyChanged();
		}
	}
}

void AHitmenPlayerController::Server_SetIcon_Implementation(int32 IconIndex)
{
	if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>()) { PS->SetIconIndex(IconIndex); }
}

void AHitmenPlayerController::Server_SetCosmetics_Implementation(const FHitmenCosmeticLoadout& Cosmetics)
{
	if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>()) { PS->SetCosmetics(Cosmetics); }
}

void AHitmenPlayerController::Server_SetLoadout_Implementation(const FHitmenLoadout& Loadout)
{
	if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>()) { PS->SetLoadout(Loadout); }
}

void AHitmenPlayerController::Server_SetReady_Implementation(bool bReady)
{
	if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>()) { PS->SetReady(bReady); }
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->NotifyReadyChanged(); }
}

void AHitmenPlayerController::Server_CreateTeam_Implementation()
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestCreateTeam(this); }
}

void AHitmenPlayerController::Server_RemoveTeam_Implementation(int32 TeamId)
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestRemoveTeam(this, TeamId); }
}

void AHitmenPlayerController::Server_AssignTeam_Implementation(AHitmenPlayerState* Target, int32 TeamId)
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestAssignTeam(this, Target, TeamId); }
}

void AHitmenPlayerController::Server_SetTeamRole_Implementation(int32 TeamId, EHitmenTeamRole NewRole)
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestSetTeamRole(this, TeamId, NewRole); }
}

void AHitmenPlayerController::Server_SetWatcher_Implementation(AHitmenPlayerState* Target, bool bWatcher)
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestSetWatcher(this, Target, bWatcher); }
}

void AHitmenPlayerController::Server_SetLobbySettings_Implementation(const FHitmenLobbySettings& Settings)
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestSetLobbySettings(this, Settings); }
}

void AHitmenPlayerController::Server_StartCustomization_Implementation()
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestStart(this); }
}

void AHitmenPlayerController::Server_LaunchMatch_Implementation()
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestLaunch(this); }
}

void AHitmenPlayerController::Server_BackToLobby_Implementation()
{
	if (AHitmenLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenLobbyGameMode>()) { GM->RequestBackToLobby(this); }
}

// ---------------------------------------------------------------------------------------------
// Client feedback
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerController::Client_HitConfirmed_Implementation(bool bKill, bool bHeadshot)
{
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this)) { UI->BroadcastHitMarker(bKill, bHeadshot); }
	Juice_OnHitConfirmed(bKill, bHeadshot);
}

void AHitmenPlayerController::Client_DamageTaken_Implementation(FVector_NetQuantize SourceLocation)
{
	float Yaw = 0.f;
	if (const APawn* P = GetPawn())
	{
		const FVector ToSource = (FVector(SourceLocation) - P->GetActorLocation()).GetSafeNormal2D();
		const FRotator Delta = UKismetMathLibrary::NormalizedDeltaRotator(ToSource.Rotation(), GetControlRotation());
		Yaw = Delta.Yaw;
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this)) { UI->BroadcastDamageTaken(Yaw); }
	Juice_OnDamageTaken(Yaw);
}

void AHitmenPlayerController::Client_ShowLastKnownPosition_Implementation(FVector_NetQuantize Location, bool bShow)
{
	if (!LastKnownPositionMarkerClass)
	{
		return;
	}
	if (!LastKnownPositionMarker)
	{
		FActorSpawnParameters Params;
		Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		LastKnownPositionMarker = GetWorld()->SpawnActor<AActor>(LastKnownPositionMarkerClass, FTransform(FVector(Location)), Params);
	}
	if (LastKnownPositionMarker)
	{
		LastKnownPositionMarker->SetActorLocation(Location);
		LastKnownPositionMarker->SetActorHiddenInGame(!bShow);
	}
}

void AHitmenPlayerController::Client_EnterSpectatorMode_Implementation(bool bWatcher)
{
	bSpectating = true;
	bIsWatcher = bWatcher;
	RefreshMappingContexts();
	SetSpectatorMode(bWatcher ? EHitmenSpectatorMode::FreeFly : EHitmenSpectatorMode::Follow);
}

// ---------------------------------------------------------------------------------------------
// Debug
// ---------------------------------------------------------------------------------------------

namespace
{
	bool DebugAllowed(const UWorld* World)
	{
		const AHitmenGameModeBase* GM = World ? World->GetAuthGameMode<AHitmenGameModeBase>() : nullptr;
		return GM && GM->AllowsDebugCommands();
	}
}

void AHitmenPlayerController::Server_DebugGiveWeapon_Implementation(UHitmenWeaponData* Weapon)
{
	if (!DebugAllowed(GetWorld()) || !GetPawn()) { return; }
	if (UHitmenInventoryComponent* Inventory = GetPawn()->FindComponentByClass<UHitmenInventoryComponent>())
	{
		Inventory->GiveWeapon(Weapon, -1, true);
	}
}

void AHitmenPlayerController::Server_DebugGiveAbility_Implementation(UHitmenAbilityData* Ability)
{
	if (!DebugAllowed(GetWorld()) || !GetPawn()) { return; }
	if (UHitmenAbilityComponent* Abilities = GetPawn()->FindComponentByClass<UHitmenAbilityComponent>())
	{
		Abilities->SetAbility(Ability);
	}
}

void AHitmenPlayerController::Server_DebugGiveKeycard_Implementation(int32 KeycardId)
{
	if (!DebugAllowed(GetWorld()) || !GetPawn()) { return; }
	if (UHitmenInventoryComponent* Inventory = GetPawn()->FindComponentByClass<UHitmenInventoryComponent>())
	{
		Inventory->AddKeycard(KeycardId);
	}
}

void AHitmenPlayerController::Server_DebugSetGodMode_Implementation(bool bEnabled)
{
	if (!DebugAllowed(GetWorld()) || !GetPawn()) { return; }
	if (UHitmenHealthComponent* Health = GetPawn()->FindComponentByClass<UHitmenHealthComponent>())
	{
		Health->SetGodMode(bEnabled);
	}
}

// ---------------------------------------------------------------------------------------------
// Spectator
// ---------------------------------------------------------------------------------------------

TArray<APawn*> AHitmenPlayerController::GetSpectatableTargets() const
{
	TArray<APawn*> Result;
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	if (!GS)
	{
		return Result;
	}
	const AHitmenPlayerState* Self = GetPlayerState<AHitmenPlayerState>();
	const bool bLimitToTeam = !bIsWatcher && Self && Self->IsInTeam();
	for (const AHitmenPlayerState* PS : GS->GetHitmenPlayers())
	{
		if (PS == Self || PS->IsWatcher() || !PS->IsAlive())
		{
			continue;
		}
		if (bLimitToTeam && PS->GetLobbyTeamId() != Self->GetLobbyTeamId())
		{
			continue;
		}
		if (APawn* P = PS->GetPawn())
		{
			Result.Add(P);
		}
	}
	// Dead team players with no teammate left can watch everyone.
	if (bLimitToTeam && Result.IsEmpty())
	{
		for (const AHitmenPlayerState* PS : GS->GetHitmenPlayers())
		{
			if (PS != Self && !PS->IsWatcher() && PS->IsAlive() && PS->GetPawn())
			{
				Result.Add(PS->GetPawn());
			}
		}
	}
	return Result;
}

void AHitmenPlayerController::SetSpectatorMode(EHitmenSpectatorMode NewMode)
{
	// Dead players cannot free-fly unless they are watchers.
	if (!bIsWatcher && NewMode == EHitmenSpectatorMode::FreeFly)
	{
		NewMode = EHitmenSpectatorMode::Follow;
	}
	SpectatorMode = NewMode;
	ApplySpectatorView();
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		UI->OnSpectatorModeChanged.Broadcast(static_cast<uint8>(SpectatorMode));
	}
}

void AHitmenPlayerController::CycleSpectatorMode()
{
	const uint8 Next = (static_cast<uint8>(SpectatorMode) + 1) % 3;
	SetSpectatorMode(static_cast<EHitmenSpectatorMode>(Next));
}

void AHitmenPlayerController::SpectateNext()
{
	const TArray<APawn*> Targets = GetSpectatableTargets();
	if (Targets.IsEmpty())
	{
		return;
	}
	const int32 Current = Targets.IndexOfByKey(SpectatorTarget.Get());
	SpectatorTarget = Targets[(Current + 1) % Targets.Num()];
	if (SpectatorMode == EHitmenSpectatorMode::FreeFly)
	{
		SpectatorMode = EHitmenSpectatorMode::Follow;
	}
	ApplySpectatorView();
}

void AHitmenPlayerController::SpectatePrevious()
{
	const TArray<APawn*> Targets = GetSpectatableTargets();
	if (Targets.IsEmpty())
	{
		return;
	}
	int32 Current = Targets.IndexOfByKey(SpectatorTarget.Get());
	Current = Current == INDEX_NONE ? 0 : Current;
	SpectatorTarget = Targets[(Current - 1 + Targets.Num()) % Targets.Num()];
	if (SpectatorMode == EHitmenSpectatorMode::FreeFly)
	{
		SpectatorMode = EHitmenSpectatorMode::Follow;
	}
	ApplySpectatorView();
}

void AHitmenPlayerController::ApplySpectatorView()
{
	if (!bSpectating)
	{
		return;
	}
	if (SpectatorMode == EHitmenSpectatorMode::Follow)
	{
		if (!SpectatorTarget.IsValid())
		{
			const TArray<APawn*> Targets = GetSpectatableTargets();
			SpectatorTarget = Targets.IsEmpty() ? nullptr : Targets[0];
		}
		if (APawn* Target = SpectatorTarget.Get())
		{
			SetViewTargetWithBlend(Target, 0.25f);
		}
	}
	else
	{
		// Free fly and grid both use the spectator pawn camera underneath.
		SetViewTargetWithBlend(GetPawnOrSpectator(), 0.25f);
	}
	OnSpectatorTargetChanged.Broadcast(SpectatorTarget.Get());
}
