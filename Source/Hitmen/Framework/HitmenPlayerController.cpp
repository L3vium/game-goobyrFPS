// Hitmen — player controller

#include "Framework/HitmenPlayerController.h"
#include "Core/HitmenText.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/Character.h"
#include "World/Vehicles/HitmenVehicles.h"
#include "Data/HitmenSoundSet.h"
#include "Sound/SoundAttenuation.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenLobbyGameMode.h"
#include "Framework/HitmenGameModeBase.h"
#include "Framework/HitmenHUD.h"
#include "Framework/HitmenGameInstance.h"
#include "Framework/HitmenMatchGameMode.h"
#include "Spectator/HitmenSpectatorPawn.h"
#include "Blueprint/UserWidget.h"
#include "Core/HitmenInterfaces.h"
#include "Core/HitmenUserSettings.h"
#include "Core/HitmenTeamLibrary.h"
#include "Online/HitmenSessionSubsystem.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Components/HitmenHealthComponent.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Abilities/HitmenAbilityComponent.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Data/HitmenAbilityData.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenCatalog.h"
#include "Data/HitmenCosmeticItem.h"
#include "Core/HitmenGameSettings.h"
#include "AI/HitmenAIController.h"
#include "Data/HitmenAnimSet.h"
#include "Abilities/HitmenAbility.h"
#include "Interaction/HitmenInteractionComponent.h"
#include "EngineUtils.h"
#include "World/Interactables/HitmenSecurity.h"
#include "Abilities/Move/HitmenMoveAbilities.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "Components/HitmenSightroomComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "AI/HitmenAISubsystem.h"
#include "AI/HitmenAlertComponent.h"
#include "Framework/HitmenVIPTracker.h"
#include "Framework/HitmenEventCamera.h"
#include "World/Interactables/HitmenStealth.h"
#include "Components/SceneCaptureComponent2D.h"
#include "Engine/TextureRenderTarget2D.h"
#include "Kismet/KismetRenderingLibrary.h"
#include "TimerManager.h"
#include "Hitmen.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "UserSettings/EnhancedInputUserSettings.h"
#include "InputMappingContext.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"
#include "GameFramework/SpectatorPawn.h"
#include "Spectator/HitmenWatcherAvatar.h"
#include "Camera/PlayerCameraManager.h"
#include "Engine/GameViewportClient.h"
#include "Engine/Engine.h"
#include "Core/HitmenInterfaces.h"
#include "UI/HitmenUIFactory.h"
#include "Kismet/KismetMathLibrary.h"

AHitmenPlayerController::AHitmenPlayerController()
{
	bShowMouseCursor = false;
	VIPTracker = CreateDefaultSubobject<UHitmenVIPTrackerComponent>(TEXT("VIPTracker"));
}

void AHitmenPlayerController::BeginPlay()
{
	Super::BeginPlay();
	if (IsLocalController())
	{
		RefreshMappingContexts();
		SendPreferredProfile();
		// After a travel the PlayerState may be fresh: re-send the agent chosen in the front end.
		if (const UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>())
		{
			if (GI->bHasLocalLoadout) { Server_SetLoadout(GI->LocalLoadout); }
			if (GI->bHasLocalCosmetics) { Server_SetCosmetics(GI->LocalCosmetics); }
		}
		// The HUD owns the input mode (menu screens keep the cursor); it may not exist yet.
		RefreshInputMode();
		// Watchers around (avatars), players outlined for watchers.
		GetWorldTimerManager().SetTimer(WatcherPresenceTimer, this, &AHitmenPlayerController::UpdateWatcherPresence, 0.1f, true);
		PushWatcherOverlay();
	}
}

void AHitmenPlayerController::RefreshInputMode()
{
	if (AHitmenHUD* HitmenHUD = Cast<AHitmenHUD>(GetHUD()))
	{
		HitmenHUD->RefreshInputMode();
	}
	else
	{
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
	if (UserSettings)
	{
		// The spectator keys are listed (and rebindable) even while playing.
		if (SpectatorMappingContext)
		{
			UserSettings->RegisterInputMappingContext(SpectatorMappingContext);
		}
		UserSettings->OnSettingsChanged.AddUniqueDynamic(this, &AHitmenPlayerController::HandleInputUserSettingsChanged);
		ApplyKeyboardLayout(UserSettings);
	}
	// The subsystem rebuilds its mappings on the next tick: read the keys after that.
	FTimerHandle LabelTimer;
	GetWorldTimerManager().SetTimer(LabelTimer, this, &AHitmenPlayerController::RefreshKeyLabels, 0.1f, false);
}

void AHitmenPlayerController::HandleInputUserSettingsChanged(UEnhancedInputUserSettings* Settings)
{
	FTimerHandle LabelTimer;
	GetWorldTimerManager().SetTimer(LabelTimer, this, &AHitmenPlayerController::RefreshKeyLabels, 0.1f, false);
}

void AHitmenPlayerController::RefreshKeyLabels()
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	const UEnhancedInputLocalPlayerSubsystem* Subsystem = GetLocalPlayer() ? GetLocalPlayer()->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr;
	if (!UI || !Subsystem)
	{
		return;
	}
	// Same order as EHitmenKeyLabel.
	const UInputAction* Actions[] = { InteractAction, JumpAction, MoveAbilityAction, ManipulationAbilityAction, SightroomAction, ConfirmAction, SprintAction };
	static_assert(UE_ARRAY_COUNT(Actions) == static_cast<int32>(EHitmenKeyLabel::Count), "One action per key label");
	TArray<FString> Labels;
	for (const UInputAction* Action : Actions)
	{
		FString Label;
		if (Action)
		{
			for (const FKey& Key : Subsystem->QueryKeysMappedToAction(Action))
			{
				if (!Key.IsGamepadKey())
				{
					Label = UHitmenUIEventSubsystem::ShortKeyName(Key);
					break;
				}
			}
		}
		Labels.Add(Label);
	}
	UI->SetKeyLabels(Labels);
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
	BindIfSet(InteractAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleInteractReleased);
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
	BindIfSet(SpectatorUpAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSpectatorUpPressed);
	BindIfSet(SpectatorUpAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleSpectatorUpReleased);
	BindIfSet(SpectatorDownAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSpectatorDownPressed);
	BindIfSet(SpectatorDownAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleSpectatorDownReleased);
	BindIfSet(SpectatorFastAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSpectatorFastPressed);
	BindIfSet(SpectatorFastAction, ETriggerEvent::Completed, &AHitmenPlayerController::HandleSpectatorFastReleased);
	BindIfSet(SpectatorPlayerViewAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSpectatorPlayerView);
	BindIfSet(SpectatorSplitViewAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSpectatorSplitView);
	BindIfSet(SpectatorFreeViewAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleSpectatorFreeView);
	BindIfSet(WatcherTogglePlayersAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleWatcherTogglePlayers);
	BindIfSet(WatcherToggleWatchersAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleWatcherToggleWatchers);
	BindIfSet(ConfirmAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleConfirm);

	// Cheats (gym / dev builds): fixed F-keys, not rebindable (the engine's F1-F5 view modes and F9 screenshot are
	// cleared in DefaultInput.ini).
	const FKey CheatKeys[] = { EKeys::F1, EKeys::F2, EKeys::F3, EKeys::F4, EKeys::F5, EKeys::F6, EKeys::F7, EKeys::F8, EKeys::F9 };
	for (const FKey& Key : CheatKeys)
	{
		InputComponent->BindKey(Key, IE_Pressed, this, &AHitmenPlayerController::HandleCheatKey);
	}
	// Watching: on AZERTY the number row types & e-acute " without Shift; Windows reports those keys, never One / Two /
	// Three, so the views also listen to them (and to the numpad).
	const FKey DigitKeys[] = { EKeys::Ampersand, EKeys::E_AccentAigu, EKeys::Quote, EKeys::NumPadOne, EKeys::NumPadTwo, EKeys::NumPadThree };
	for (const FKey& Key : DigitKeys)
	{
		InputComponent->BindKey(Key, IE_Pressed, this, &AHitmenPlayerController::HandleSpectatorDigitKey);
	}
}

void AHitmenPlayerController::HandleSpectatorDigitKey(FKey Key)
{
	if (Key == EKeys::Ampersand || Key == EKeys::NumPadOne)
	{
		HandleSpectatorPlayerView();
	}
	else if (Key == EKeys::E_AccentAigu || Key == EKeys::NumPadTwo)
	{
		HandleSpectatorSplitView();
	}
	else
	{
		HandleSpectatorFreeView();
	}
}

void AHitmenPlayerController::HandleCheatKey(FKey Key)
{
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	const FKey CheatKeys[] = { EKeys::F1, EKeys::F2, EKeys::F3, EKeys::F4, EKeys::F5, EKeys::F6, EKeys::F7, EKeys::F8, EKeys::F9 };
	int32 Index = INDEX_NONE;
	for (int32 It = 0; It < UE_ARRAY_COUNT(CheatKeys); ++It)
	{
		Index = CheatKeys[It] == Key ? It : Index;
	}
	if (!GS || !GS->AreDebugCommandsAllowed() || Index == INDEX_NONE || IsGameplayInputBlocked())
	{
		return;
	}
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
	Server_DebugCheat(static_cast<EHitmenCheat>(Index));
}

bool AHitmenPlayerController::IsCheatFlying() const
{
	const AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>();
	const ACharacter* Flyer = Cast<ACharacter>(GetPawn());
	return PS && PS->HasCheat(EHitmenCheat::Fly) && Flyer && Flyer->GetCharacterMovement() && Flyer->GetCharacterMovement()->MovementMode == MOVE_Flying;
}

void AHitmenPlayerController::ApplyCheatsToPawn()
{
	ACharacter* Flyer = Cast<ACharacter>(GetPawn());
	UCharacterMovementComponent* Movement = Flyer ? Flyer->GetCharacterMovement() : nullptr;
	if (!Movement)
	{
		return;
	}
	const AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>();
	const bool bFly = PS && PS->HasCheat(EHitmenCheat::Fly);
	const bool bFlying = Movement->MovementMode == MOVE_Flying && !Flyer->GetActorEnableCollision();
	if (bFly == bFlying)
	{
		return;
	}
	// Fly: through walls, in the look direction (the controller drives it); off: fall back to the ground.
	Flyer->SetActorEnableCollision(!bFly);
	Movement->MaxFlySpeed = CheatFlySpeed;
	Movement->BrakingDecelerationFlying = CheatFlySpeed * 3.f;
	Movement->SetMovementMode(bFly ? MOVE_Flying : MOVE_Falling);
	bFlyUpHeld = false;
	bFlyDownHeld = false;
}

// ---- Free fly (IMC_Spectator) ----

void AHitmenPlayerController::HandleSpectatorUpPressed() { bSpectatorUpHeld = true; PushSpectatorVertical(); }
void AHitmenPlayerController::HandleSpectatorUpReleased() { bSpectatorUpHeld = false; PushSpectatorVertical(); }
void AHitmenPlayerController::HandleSpectatorDownPressed() { bSpectatorDownHeld = true; PushSpectatorVertical(); }
void AHitmenPlayerController::HandleSpectatorDownReleased() { bSpectatorDownHeld = false; PushSpectatorVertical(); }

void AHitmenPlayerController::PushSpectatorVertical()
{
	if (AHitmenSpectatorPawn* Spectator = Cast<AHitmenSpectatorPawn>(GetPawnOrSpectator()))
	{
		Spectator->SetVerticalInput(IsGameplayInputBlocked() ? 0.f : (bSpectatorUpHeld ? 1.f : 0.f) - (bSpectatorDownHeld ? 1.f : 0.f));
	}
}

void AHitmenPlayerController::HandleSpectatorFastPressed()
{
	if (AHitmenSpectatorPawn* Spectator = Cast<AHitmenSpectatorPawn>(GetPawnOrSpectator()))
	{
		Spectator->SetFast(true);
	}
}

void AHitmenPlayerController::HandleSpectatorFastReleased()
{
	if (AHitmenSpectatorPawn* Spectator = Cast<AHitmenSpectatorPawn>(GetPawnOrSpectator()))
	{
		Spectator->SetFast(false);
	}
}

void AHitmenPlayerController::HandleConfirm()
{
	if (IsMatchOver() && !IsGameplayInputBlocked())
	{
		RequestReturnToLobby();
	}
}

bool AHitmenPlayerController::IsMatchOver() const
{
	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	return GS && GS->GetMatchPhase() == EHitmenMatchPhase::Ended;
}

void AHitmenPlayerController::RequestReturnToLobby()
{
	if (IsHost() && IsMatchOver())
	{
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIConfirm);
		Server_ReturnToLobby();
	}
}

void AHitmenPlayerController::Server_ReturnToLobby_Implementation()
{
	if (AHitmenMatchGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenMatchGameMode>())
	{
		GM->RequestReturnToLobby(this);
	}
}

void AHitmenPlayerController::SetPawn(APawn* InPawn)
{
	Super::SetPawn(InPawn);
	if (IsLocalController())
	{
		BindHUDSources(InPawn);
		// The controller owns the Sightroom radii so a 3C swap keeps them.
		if (UHitmenSightroomComponent* Sightroom = InPawn ? InPawn->FindComponentByClass<UHitmenSightroomComponent>() : nullptr)
		{
			Sightroom->SetScanRadii(SightroomEnemyRadius, SightroomInteractableRadius);
		}
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		// The previous pawn can no longer reach this UI to clear its prompt (entering a vehicle left
		// "Use" on screen): a new pawn always starts without one.
		UI->BroadcastPrompt(FText::GetEmpty(), false);
		UI->BroadcastPawnChanged(InPawn);
	}
	// A new body restarts toggles; a flying cheat carries over to it.
	bCrouchToggled = false;
	bSprintToggled = false;
	ApplyCheatsToPawn();
}

void AHitmenPlayerController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);
	if (InPawn && !InPawn->IsA<ASpectatorPawn>())
	{
		if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>())
		{
			PS->ClearWatcherView();
		}
		// Alive again (respawn, revive): the owner leaves its spectator views (remote owners never ran OnPossess).
		if (IsLocalController())
		{
			Client_ExitSpectatorMode_Implementation();
		}
		else if (bSpectating)
		{
			bSpectating = false;
			Client_ExitSpectatorMode();
		}
	}
}

void AHitmenPlayerController::Client_ExitSpectatorMode_Implementation()
{
	bSpectating = false;
	bIsWatcher = false;
	bSpectatorUpHeld = false;
	bSpectatorDownHeld = false;
	GetWorldTimerManager().ClearTimer(SpectatorGridTimer);
	StopSpectatorCaptures();
	SpectatorTarget = nullptr;
	RefreshMappingContexts();
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		UI->SetSpectatorTargetName(FText::GetEmpty());
		UI->OnSpectatorModeChanged.Broadcast(static_cast<uint8>(SpectatorMode));
	}
}

APawn* AHitmenPlayerController::GetSpectatorViewPawn() const
{
	if (APawn* Spectator = GetSpectatorPawn())
	{
		return Spectator;
	}
	return GetPawnOrSpectator();
}

IHitmenInputReceiver* AHitmenPlayerController::GetReceiver() const
{
	// While spectating, the free-fly pawn is the spectator pawn (the dead body can still be the replicated pawn for a
	// moment after the server switched us to spectating: it must not swallow the keys).
	return Cast<IHitmenInputReceiver>(bSpectating ? GetSpectatorViewPawn() : GetPawnOrSpectator());
}

void AHitmenPlayerController::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UnbindHUDSources();
	StopSpectatorCaptures();
	ClearWatcherPresence();
	Super::EndPlay(EndPlayReason);
}

// ---------------------------------------------------------------------------------------------
// HUD publisher: the only place that reads gameplay components for the HUD
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerController::BindHUDSources(APawn* InPawn)
{
	UnbindHUDSources();
	if (!InPawn || InPawn->IsA<ASpectatorPawn>())
	{
		return;
	}
	HUDInventory = InPawn->FindComponentByClass<UHitmenInventoryComponent>();
	HUDAbilities = InPawn->FindComponentByClass<UHitmenAbilityComponent>();
	HUDHealth = InPawn->FindComponentByClass<UHitmenHealthComponent>();
	HUDCarry = InPawn->FindComponentByClass<UHitmenBodyCarryComponent>();
	if (UHitmenInventoryComponent* Inventory = HUDInventory.Get())
	{
		Inventory->OnInventoryChanged.AddUniqueDynamic(this, &AHitmenPlayerController::HandleHUDInventoryChanged);
		Inventory->OnActiveSlotChanged.AddUniqueDynamic(this, &AHitmenPlayerController::HandleHUDActiveSlotChanged);
	}
	if (UHitmenHealthComponent* Health = HUDHealth.Get())
	{
		Health->OnHealthChanged.AddUniqueDynamic(this, &AHitmenPlayerController::HandleHUDHealthChanged);
	}
	if (UHitmenBodyCarryComponent* Carry = HUDCarry.Get())
	{
		Carry->OnCarryChanged.AddUniqueDynamic(this, &AHitmenPlayerController::HandleHUDCarryChanged);
	}
	HandleHUDInventoryChanged();
	HandleHUDHealthChanged(HUDHealth.Get(), 0.f, 0.f, nullptr);
	HandleHUDCarryChanged(nullptr);
	PublishEmotes(InPawn);
	RefreshHUDViews();
	GetWorldTimerManager().SetTimer(HUDRefreshTimer, this, &AHitmenPlayerController::RefreshHUDViews, HUDRefreshInterval, true);
}

void AHitmenPlayerController::UnbindHUDSources()
{
	if (UHitmenInventoryComponent* Inventory = HUDInventory.Get())
	{
		Inventory->OnInventoryChanged.RemoveAll(this);
		Inventory->OnActiveSlotChanged.RemoveAll(this);
	}
	if (UHitmenHealthComponent* Health = HUDHealth.Get())
	{
		Health->OnHealthChanged.RemoveAll(this);
	}
	if (UHitmenBodyCarryComponent* Carry = HUDCarry.Get())
	{
		Carry->OnCarryChanged.RemoveAll(this);
	}
	HUDInventory = nullptr;
	HUDAbilities = nullptr;
	HUDHealth = nullptr;
	HUDCarry = nullptr;
	if (UWorld* World = GetWorld())
	{
		World->GetTimerManager().ClearTimer(HUDRefreshTimer);
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		UI->SetCarryingVIP(false);
		UI->SetAlertBlips(TArray<FHitmenHUDAlertBlip>());
	}
}

void AHitmenPlayerController::HandleHUDActiveSlotChanged(int32 NewSlot)
{
	HandleHUDInventoryChanged();
}

void AHitmenPlayerController::HandleHUDInventoryChanged()
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	const UHitmenInventoryComponent* Inventory = HUDInventory.Get();
	if (!UI || !Inventory)
	{
		return;
	}
	FHitmenHUDInventoryView View;
	const TArray<FHitmenInventorySlot>& Slots = Inventory->GetSlots();
	for (int32 Index = 0; Index < Slots.Num(); ++Index)
	{
		FHitmenHUDSlotView& SlotView = View.Slots.AddDefaulted_GetRef();
		SlotView.bActive = Inventory->GetActiveSlot() == Index;
		SlotView.bEmpty = Slots[Index].IsEmpty();
		if (!SlotView.bEmpty && Slots[Index].Weapon)
		{
			SlotView.Name = UHitmenTextSubsystem::Translate(Slots[Index].Weapon->DisplayName);
			SlotView.Ammo = Slots[Index].Weapon->bUsesAmmo ? Slots[Index].Ammo : -1;
			SlotView.MaxAmmo = Slots[Index].Weapon->bUsesAmmo ? Slots[Index].Weapon->MaxAmmo : -1;
			SlotView.Icon = Slots[Index].Weapon->Icon.LoadSynchronous();
		}
	}
	View.Keycards = Inventory->GetKeycards();
	if (const UHitmenBodyCarryComponent* Carry = HUDCarry.Get(); Carry && Carry->IsCarrying())
	{
		View.bCarryingBody = true;
		View.bCarryingVIP = Carry->GetCarriedBody() && Carry->GetCarriedBody()->IsVIP();
	}
	UI->SetInventoryView(View);
}

void AHitmenPlayerController::HandleHUDHealthChanged(UHitmenHealthComponent* HealthComponent, float NewHealth, float Delta, AActor* InstigatorActor)
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	const UHitmenHealthComponent* Health = HUDHealth.Get();
	if (!UI || !Health)
	{
		return;
	}
	FHitmenHUDVitalsView View;
	View.Health = Health->GetHealth();
	View.Armor = Health->GetArmor();
	View.HealthNormalized = Health->GetHealthNormalized();
	UI->SetVitals(View);
}

void AHitmenPlayerController::HandleHUDCarryChanged(AHitmenCharacterBase* Body)
{
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		const UHitmenBodyCarryComponent* Carry = HUDCarry.Get();
		UI->SetCarryingVIP(Carry && Carry->IsCarrying() && Carry->GetCarriedBody() && Carry->GetCarriedBody()->IsVIP());
	}
	HandleHUDInventoryChanged();
}

void AHitmenPlayerController::PublishEmotes(APawn* InPawn)
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	if (!UI)
	{
		return;
	}
	TArray<FText> Names;
	const AHitmenCharacterBase* HitmenCharacter = Cast<AHitmenCharacterBase>(InPawn);
	if (const UHitmenAnimSet* AnimSet = HitmenCharacter ? HitmenCharacter->GetAnimSet() : nullptr)
	{
		for (const FHitmenEmote& Emote : AnimSet->Emotes)
		{
			Names.Add(UHitmenTextSubsystem::Translate(Emote.DisplayName));
		}
	}
	UI->SetEmoteNames(Names);
}

void AHitmenPlayerController::RefreshHUDViews()
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	const APawn* ViewPawn = GetPawn();
	if (!UI || !ViewPawn)
	{
		return;
	}

	// Abilities (cooldowns tick down, so they are polled at the HUD refresh rate).
	auto MakeAbilityView = [](const UHitmenAbility* Ability)
	{
		FHitmenHUDAbilityView View;
		if (Ability && Ability->GetData())
		{
			View.bValid = true;
			View.Name = UHitmenTextSubsystem::Translate(Ability->GetData()->DisplayName);
			View.Charges = Ability->GetCharges();
			View.MaxCharges = Ability->GetMaxCharges();
			View.CooldownRemaining = Ability->GetCooldownRemaining();
			View.CooldownAlpha = 1.f - Ability->GetCooldownNormalized();
			View.Icon = Ability->GetData()->Icon.LoadSynchronous();
			View.Key = Ability->GetHUDKey();
			if (const UHitmenAbility_Jetpack* Jetpack = Cast<UHitmenAbility_Jetpack>(Ability))
			{
				View.Meter = Jetpack->GetFuelNormalized();
			}
		}
		return View;
	};
	const UHitmenAbilityComponent* Abilities = HUDAbilities.Get();
	FHitmenHUDAbilityView MoveView = MakeAbilityView(Abilities ? Abilities->GetAbility(EHitmenAbilitySlot::Move) : nullptr);
	FHitmenHUDAbilityView ManipulationView = MakeAbilityView(Abilities ? Abilities->GetAbility(EHitmenAbilitySlot::Manipulation) : nullptr);
	if (!MoveView.bValid)
	{
		MoveView.Key = EHitmenKeyLabel::MoveAbility;
	}
	if (!ManipulationView.bValid)
	{
		ManipulationView.Key = EHitmenKeyLabel::Manipulation;
	}
	UI->SetAbilityViews(MoveView, ManipulationView);

	// Timed effects on you (invisibility, pill...): a ring above the crosshair empties as each runs out.
	TArray<FHitmenHUDStatusTimer> Timers;
	if (const AHitmenCharacterBase* ViewCharacter = Cast<AHitmenCharacterBase>(ViewPawn); ViewCharacter && ViewCharacter->GetStatusEffects())
	{
		const UHitmenStatusEffectComponent* Statuses = ViewCharacter->GetStatusEffects();
		const float Now = Statuses->GetServerTime();
		for (const FHitmenActiveStatus& Status : Statuses->GetActiveStatuses())
		{
			const bool bShown = Status.Type == EHitmenStatusEffect::Invisible || Status.Type == EHitmenStatusEffect::Boosted || Status.Type == EHitmenStatusEffect::Armored;
			if (bShown && Status.EndTime > 0.f && Status.StartTime >= 0.f && Status.EndTime > Now)
			{
				FHitmenHUDStatusTimer& Timer = Timers.AddDefaulted_GetRef();
				Timer.Status = Status.Type;
				Timer.Remaining = Status.EndTime - Now;
				Timer.Duration = FMath::Max(Status.EndTime - Status.StartTime, 0.1f);
			}
		}
	}
	UI->SetStatusTimers(Timers);

	// Crosshair spread.
	const UHitmenInventoryComponent* Inventory = HUDInventory.Get();
	const AHitmenWeaponBase* Weapon = Inventory ? Inventory->GetActiveWeapon() : nullptr;
	UI->SetCrosshairSpread(Weapon ? Weapon->GetCurrentSpread() : 1.f);

	// NPC alert blips (minimap colours, crosshair arcs).
	TArray<FHitmenHUDAlertBlip> Blips;
	if (const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this))
	{
		for (const UHitmenAlertComponent* Alert : AI->GetAlertComponents())
		{
			const AHitmenCharacterBase* NPC = Alert ? Cast<AHitmenCharacterBase>(Alert->GetOwner()) : nullptr;
			if (!NPC || NPC->IsDead())
			{
				continue;
			}
			FHitmenHUDAlertBlip& Blip = Blips.AddDefaulted_GetRef();
			Blip.Location = NPC->GetActorLocation();
			// A fight against someone it never saw (shot at, colleagues dropping) reads as a search: red and UNDER FIRE
			// only once it has seen you.
			Blip.Color = Alert->GetDisplayAlertColor();
			Blip.State = Alert->GetDisplayAlertState();
			Blip.Suspicion = Alert->GetSuspicion();
			Blip.bTargetsViewer = Alert->GetTarget() == ViewPawn;
			Blip.bCallingReinforcements = Alert->IsCallingReinforcements();
			// Vision cone: the live controller on the server; clients have none and read the controller class default.
			Blip.Yaw = NPC->GetActorRotation().Yaw;
			if (const AHitmenAIController* NPCAI = Cast<AHitmenAIController>(NPC->GetController()))
			{
				Blip.SightRadius = NPCAI->GetEffectiveSightRadius();
				Blip.SightHalfAngle = NPCAI->GetPeripheralVisionHalfAngle();
			}
			else if (const AHitmenAIController* Default = NPC->AIControllerClass ? Cast<AHitmenAIController>(NPC->AIControllerClass->GetDefaultObject()) : nullptr)
			{
				Blip.SightRadius = Default->GetBaseSightRadius();
				Blip.SightHalfAngle = Default->GetPeripheralVisionHalfAngle();
			}
		}
	}
	// Security cameras that are watching an intruder and have you in clear view (awareness arcs, camera mark).
	if (UWorld* World = GetWorld())
	{
		const FVector Eye = ViewPawn->GetPawnViewLocation();
		for (TActorIterator<AHitmenSecurityCamera> It(World); It; ++It)
		{
			if (It->IsCameraDisabled() || (!It->IsSeeingIntruder() && It->GetDetectionMeter() <= 0.f))
			{
				continue;
			}
			FHitResult Hit;
			FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenCameraBlip), false, *It);
			Params.AddIgnoredActor(ViewPawn);
			if (World->LineTraceSingleByChannel(Hit, It->GetActorLocation(), Eye, ECC_Visibility, Params))
			{
				continue;
			}
			// The meter fills a white arc; once it raised the alarm it reads as a search (yellow), never a fight.
			const bool bAlarm = It->HasRecentlyAlarmed() && It->IsSeeingIntruder();
			FHitmenHUDAlertBlip& Blip = Blips.AddDefaulted_GetRef();
			Blip.Location = It->GetActorLocation();
			Blip.Color = bAlarm ? EHitmenAlertColor::Yellow : EHitmenAlertColor::White;
			Blip.State = bAlarm ? EHitmenAlertState::Searching : EHitmenAlertState::Suspicious;
			Blip.Suspicion = It->GetDetectionMeter();
			Blip.bTargetsViewer = true;
			Blip.bIsCamera = true;
		}
	}
	UI->SetAlertBlips(MoveTemp(Blips));
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
	if (IsCheatFlying())
	{
		// Fly cheat: forward follows the view pitch.
		if (APawn* P = GetPawn(); P && !IsGameplayInputBlocked())
		{
			const FRotator View = GetControlRotation();
			const FVector2D Axis = Value.Get<FVector2D>();
			P->AddMovementInput(View.Vector(), Axis.Y);
			P->AddMovementInput(FRotationMatrix(View).GetUnitAxis(EAxis::Y), Axis.X);
		}
		return;
	}
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
	// The emote wheel is open: the mouse picks a slot, the view stays.
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this); UI && UI->IsEmoteWheelOpen())
	{
		UI->AddEmoteWheelCursor(FVector2D(Axis.X, -Axis.Y) * EmoteWheelMouseScale);
		return;
	}
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
	if (IsCheatFlying())
	{
		bFlyUpHeld = true;
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_JumpPressed(); }
}

void AHitmenPlayerController::HandleJumpReleased()
{
	if (IsCheatFlying())
	{
		bFlyUpHeld = false;
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_JumpReleased(); }
}

void AHitmenPlayerController::HandleCrouchPressed()
{
	if (IsCheatFlying())
	{
		bFlyDownHeld = true;
		return;
	}
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
	if (IsCheatFlying())
	{
		bFlyDownHeld = false;
		return;
	}
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (Settings && Settings->bToggleCrouch)
	{
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_CrouchChanged(false); }
}

void AHitmenPlayerController::HandleSprintPressed()
{
	// Fly cheat: Shift flies faster.
	if (IsCheatFlying())
	{
		bFlyFastHeld = true;
		ApplyCheatFlySpeed(true);
		Server_SetCheatFlyFast(true);
		return;
	}
	IHitmenInputReceiver* R = GetReceiver();
	if (!R || IsGameplayInputBlocked())
	{
		return;
	}
	SprintPressTime = GetWorld()->GetTimeSeconds();
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
	if (bFlyFastHeld)
	{
		bFlyFastHeld = false;
		ApplyCheatFlySpeed(false);
		Server_SetCheatFlyFast(false);
		return;
	}
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	// Toggle sprint: the release ends nothing, but a quick tap still dashes.
	if (Settings && Settings->bToggleSprint)
	{
		if (IHitmenInputReceiver* R = GetReceiver(); R && GetWorld()->GetTimeSeconds() - SprintPressTime <= SprintTapTime && !IsGameplayInputBlocked())
		{
			R->Input_SprintTapped();
		}
		return;
	}
	IHitmenInputReceiver* R = GetReceiver();
	if (!R)
	{
		return;
	}
	R->Input_SprintChanged(false);
	// A quick tap of Shift dashes (when the dash is equipped); holding it sprints.
	if (GetWorld()->GetTimeSeconds() - SprintPressTime <= SprintTapTime && !IsGameplayInputBlocked())
	{
		R->Input_SprintTapped();
	}
}

void AHitmenPlayerController::HandleFirePressed()
{
	if (bSpectating)
	{
		SpectateNext();
		return;
	}
	// Aiming a blink: left click blinks now.
	if (UHitmenAbilityComponent* Abilities = GetPawn() ? GetPawn()->FindComponentByClass<UHitmenAbilityComponent>() : nullptr; Abilities && Abilities->IsAimingAny())
	{
		Abilities->ConfirmAim(Abilities->IsAimingSlot(EHitmenAbilitySlot::Move) ? EHitmenAbilitySlot::Move : EHitmenAbilitySlot::Manipulation);
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_FirePressed(); }
}

bool AHitmenPlayerController::CancelAbilityAim()
{
	UHitmenAbilityComponent* Abilities = GetPawn() ? GetPawn()->FindComponentByClass<UHitmenAbilityComponent>() : nullptr;
	if (!Abilities || !Abilities->IsAimingAny())
	{
		return false;
	}
	Abilities->CancelAim(EHitmenAbilitySlot::Move);
	Abilities->CancelAim(EHitmenAbilitySlot::Manipulation);
	return true;
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
	// Aiming a blink: right click drops it.
	if (CancelAbilityAim())
	{
		return;
	}
	if (IHitmenInputReceiver* R = GetReceiver(); R && !IsGameplayInputBlocked()) { R->Input_AimChanged(true); }
}

void AHitmenPlayerController::HandleAimReleased()
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_AimChanged(false); }
}

void AHitmenPlayerController::HandleInteractReleased()
{
	if (const APawn* ControlledPawn = GetPawn())
	{
		if (UHitmenInteractionComponent* Interaction = ControlledPawn->FindComponentByClass<UHitmenInteractionComponent>())
		{
			Interaction->ReleaseInteract();
		}
	}
}

void AHitmenPlayerController::HandleInteract()
{
	// Aiming a blink: F drops it.
	if (CancelAbilityAim())
	{
		return;
	}
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
	if (bSpectating)
	{
		// Mouse wheel while flying free: fly speed (shown briefly by the spectator widget).
		if (AHitmenSpectatorPawn* Spectator = Cast<AHitmenSpectatorPawn>(GetSpectatorPawn()); Spectator && SpectatorMode == EHitmenSpectatorMode::FreeFly && !IsGameplayInputBlocked())
		{
			const float Scale = Spectator->AdjustSpeed(Direction > 0.f ? 1.f : -1.f);
			if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
			{
				UI->OnWatcherSpeedChanged.Broadcast(Scale);
			}
		}
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
	// R during an emote ends it (no wheel).
	if (IHitmenInputReceiver* R = GetReceiver(); R && R->Input_IsEmoting())
	{
		R->Input_StopEmote();
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
	// Aiming a blink: Esc drops it, and the pause menu still opens.
	CancelAbilityAim();
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
	// Front-end screens have no pause menu (Esc is handled by their own Back buttons).
	if (const AHitmenHUD* HitmenHUD = Cast<AHitmenHUD>(GetHUD()); HitmenHUD && HitmenHUD->IsMenuHUD())
	{
		return;
	}
	// The HUD reacts to the toggle and sets the input mode.
	UI->SetPauseMenuOpen(!UI->IsPauseMenuOpen());
}

void AHitmenPlayerController::HandleSpectatorMode()
{
	if (!bSpectating || IsGameplayInputBlocked())
	{
		return;
	}
	// V: first / third person in player view (from another view: player view).
	if (SpectatorMode == EHitmenSpectatorMode::Follow)
	{
		SetSpectatorThirdPerson(!bSpectatorThirdPerson);
	}
	else
	{
		SetSpectatorMode(EHitmenSpectatorMode::Follow);
	}
}

void AHitmenPlayerController::SetSpectatorThirdPerson(bool bThirdPerson)
{
	bSpectatorThirdPerson = bThirdPerson;
	if (bSpectatorThirdPerson)
	{
		// Start behind the player, looking where they look.
		if (const APawn* Target = SpectatorTarget.Get())
		{
			SetControlRotation(FRotator(-12.f, Target->GetActorRotation().Yaw, 0.f));
		}
	}
	ApplySpectatorView();
}

void AHitmenPlayerController::HandleSpectatorPlayerView()
{
	if (bSpectating && !IsGameplayInputBlocked()) { SetSpectatorMode(EHitmenSpectatorMode::Follow); }
}

void AHitmenPlayerController::HandleSpectatorSplitView()
{
	if (bSpectating && !IsGameplayInputBlocked()) { SetSpectatorMode(EHitmenSpectatorMode::Grid); }
}

void AHitmenPlayerController::HandleSpectatorFreeView()
{
	if (bSpectating && !IsGameplayInputBlocked()) { SetSpectatorMode(EHitmenSpectatorMode::FreeFly); }
}

bool AHitmenPlayerController::IsWatcherPlayersShown() const
{
	const EHitmenWatcherVisibility Rule = UHitmenGameSettings::Get()->WatcherSeePlayers;
	return Rule == EHitmenWatcherVisibility::Shown || (Rule == EHitmenWatcherVisibility::ShownToggleable && bWatcherShowPlayers);
}

bool AHitmenPlayerController::IsWatcherWatchersShown() const
{
	const EHitmenWatcherVisibility Rule = UHitmenGameSettings::Get()->WatcherSeeWatchers;
	return Rule == EHitmenWatcherVisibility::Shown || (Rule == EHitmenWatcherVisibility::ShownToggleable && bWatcherShowWatchers);
}

void AHitmenPlayerController::HandleWatcherTogglePlayers()
{
	if (bSpectating && UHitmenGameSettings::Get()->WatcherSeePlayers == EHitmenWatcherVisibility::ShownToggleable)
	{
		bWatcherShowPlayers = !bWatcherShowPlayers;
		PushWatcherOverlay();
		UpdateWatcherPresence();
	}
}

void AHitmenPlayerController::HandleWatcherToggleWatchers()
{
	if (bSpectating && UHitmenGameSettings::Get()->WatcherSeeWatchers == EHitmenWatcherVisibility::ShownToggleable)
	{
		bWatcherShowWatchers = !bWatcherShowWatchers;
		PushWatcherOverlay();
		UpdateWatcherPresence();
	}
}

void AHitmenPlayerController::PushWatcherOverlay()
{
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		UI->SetWatcherOverlay(bWatcherShowPlayers, bWatcherShowWatchers);
		UI->OnSpectatorModeChanged.Broadcast(static_cast<uint8>(SpectatorMode));
	}
}

void AHitmenPlayerController::SelectEmote(int32 EmoteIndex)
{
	if (IHitmenInputReceiver* R = GetReceiver()) { R->Input_Emote(EmoteIndex); }
}

void AHitmenPlayerController::SetMenuInputMode(bool bMenu, bool bUIOnly)
{
	if (bMenu && bUIOnly)
	{
		// Front end: the viewport never captures the mouse, so the cursor can't vanish on a click.
		FInputModeUIOnly Mode;
		if (const AHitmenHUD* HitmenHUD = Cast<AHitmenHUD>(GetHUD()); HitmenHUD && HitmenHUD->GetPrimaryWidget())
		{
			UUserWidget* Primary = HitmenHUD->GetPrimaryWidget();
			Primary->SetIsFocusable(true);
			Mode.SetWidgetToFocus(Primary->TakeWidget());
		}
		Mode.SetLockMouseToViewportBehavior(EMouseLockMode::DoNotLock);
		SetInputMode(Mode);
		SetShowMouseCursor(true);
		bEnableClickEvents = false;
	}
	else if (bMenu)
	{
		// In-match overlays (pause, options, results): game input keeps running (Esc closes).
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

namespace
{
	/** Only what DA_Catalog offers (a modified client could send any weapon or ability asset). */
	bool IsLoadoutAllowed(const FHitmenLoadout& Loadout)
	{
		const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
		if (!Catalog)
		{
			return true;
		}
		return (!Loadout.Weapon || Catalog->Weapons.Contains(Loadout.Weapon))
			&& (!Loadout.MoveAbility || Catalog->MoveAbilities.Contains(Loadout.MoveAbility))
			&& (!Loadout.ManipulationAbility || Catalog->ManipulationAbilities.Contains(Loadout.ManipulationAbility));
	}

	bool IsCosmeticsAllowed(const FHitmenCosmeticLoadout& Cosmetics)
	{
		const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
		if (!Catalog)
		{
			return true;
		}
		for (const EHitmenCosmeticSlot Slot : { EHitmenCosmeticSlot::Head, EHitmenCosmeticSlot::Body, EHitmenCosmeticSlot::Legs })
		{
			const UHitmenCosmeticItem* Item = Cosmetics.GetSlot(Slot);
			if (Item && (!Catalog->Cosmetics.Contains(Item) || Item->Slot != Slot))
			{
				return false;
			}
		}
		return true;
	}
}

void AHitmenPlayerController::Server_SetCosmetics_Implementation(const FHitmenCosmeticLoadout& Cosmetics)
{
	if (!IsCosmeticsAllowed(Cosmetics))
	{
		return;
	}
	if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>()) { PS->SetCosmetics(Cosmetics); }
	if (AHitmenPlayerCharacter* Agent = Cast<AHitmenPlayerCharacter>(GetPawn()))
	{
		Agent->ApplyCosmetics(Cosmetics);
	}
}

void AHitmenPlayerController::Server_SetLoadout_Implementation(const FHitmenLoadout& Loadout)
{
	AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>();
	if (!PS || !IsLoadoutAllowed(Loadout))
	{
		return;
	}
	const FHitmenLoadout Previous = PS->GetLoadout();
	const bool bChanged = Previous.Weapon != Loadout.Weapon || Previous.MoveAbility != Loadout.MoveAbility || Previous.ManipulationAbility != Loadout.ManipulationAbility;
	PS->SetLoadout(Loadout);
	// The pawn may have spawned before this arrived (non-seamless travel): equip it during the intro.
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	AHitmenPlayerCharacter* Agent = Cast<AHitmenPlayerCharacter>(GetPawn());
	if (bChanged && Agent && GS && GS->GetMatchPhase() == EHitmenMatchPhase::Intro)
	{
		Agent->ApplyLoadout(Loadout);
	}
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
	if (bHeadshot)
	{
		// Only the shooter hears it: a crisp confirmation on top of the red hit ticks.
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::HeadshotHit);
	}
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
		LastKnownPositionMarker->SetActorLocation(FVector(Location) + LastKnownPositionMarkerOffset);
		LastKnownPositionMarker->SetActorHiddenInGame(!bShow);
	}
}

void AHitmenPlayerController::ShowEventCamera(AActor* Subject, const FText& Title)
{
	if (!IsLocalController() || !Subject)
	{
		return;
	}
	if (!EventCamera)
	{
		FActorSpawnParameters Params;
		Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		Params.ObjectFlags |= RF_Transient;
		EventCamera = GetWorld()->SpawnActor<AHitmenEventCamera>(Params);
	}
	if (EventCamera)
	{
		EventCamera->Show(Subject, Title, UHitmenGameSettings::Get()->EventCameraDuration, this);
	}
}

void AHitmenPlayerController::StopEventCamera()
{
	if (EventCamera)
	{
		EventCamera->Stop();
	}
}

void AHitmenPlayerController::Client_ShowDistractionCamera_Implementation(AActor* Guard)
{
	if (UHitmenGameSettings::Get()->bEventCameraOnDistraction)
	{
		ShowEventCamera(Guard, HITMEN_TEXT("Hitmen.EventDistraction", "Distraction"));
	}
}

void AHitmenPlayerController::NotifyDistractionNoticed(AActor* Guard, AActor* NoiseInstigator)
{
	if (!Guard || !NoiseInstigator || !Guard->HasAuthority())
	{
		return;
	}
	// Who caused it: whoever switched the distraction on, or the thrower of a stone / dart.
	AActor* Source = NoiseInstigator;
	if (AHitmenDistraction* Distraction = Cast<AHitmenDistraction>(NoiseInstigator))
	{
		if (!Distraction->ConsumeEventCamera())
		{
			return;
		}
		Source = Distraction->GetSwitchedOnBy();
	}
	const APawn* Pawn = Cast<APawn>(Source);
	if (!Pawn && Source)
	{
		Pawn = Source->GetInstigator();
	}
	AHitmenPlayerController* PC = Pawn ? Cast<AHitmenPlayerController>(Pawn->GetController()) : nullptr;
	const float Now = Guard->GetWorld()->GetTimeSeconds();
	if (PC && Now - PC->LastDistractionCameraTime >= PC->DistractionCameraCooldown)
	{
		PC->LastDistractionCameraTime = Now;
		PC->Client_ShowDistractionCamera(Guard);
	}
}

void AHitmenPlayerController::Client_EnterSpectatorMode_Implementation(bool bWatcher)
{
	if (VIPTracker)
	{
		VIPTracker->EndCinematic();
	}
	bSpectating = true;
	bIsWatcher = bWatcher;
	bSpectatorUpHeld = false;
	bSpectatorDownHeld = false;
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

void AHitmenPlayerController::HitmenWatch()
{
	Server_DebugWatch();
}

void AHitmenPlayerController::Revive()
{
	Server_DebugCheat(EHitmenCheat::Revive);
}

void AHitmenPlayerController::Server_DebugWatch_Implementation()
{
	AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>();
	if (!DebugAllowed(GetWorld()) || !PS || bSpectating)
	{
		return;
	}
	// Out of the match for good: no body left behind, never a target to follow.
	if (APawn* Body = GetPawn(); Body && !Body->IsA<ASpectatorPawn>())
	{
		UnPossess();
		Body->Destroy();
	}
	PS->SetWatcher(true);
	PS->SetAlive(false);
	ChangeState(NAME_Spectating);
	ClientGotoState(NAME_Spectating);
	Client_EnterSpectatorMode(true);
}

void AHitmenPlayerController::Server_DebugCheat_Implementation(EHitmenCheat Cheat)
{
	AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>();
	AHitmenMatchGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenMatchGameMode>();
	if (!DebugAllowed(GetWorld()) || !PS)
	{
		return;
	}
	switch (Cheat)
	{
	case EHitmenCheat::RespawnEnemies:
		if (GM) { GM->DebugRespawnNPCs(); }
		break;
	case EHitmenCheat::KillEnemies:
		if (GM) { GM->DebugKillNPCs(this); }
		break;
	case EHitmenCheat::ResetGym:
		if (GM) { GM->DebugResetMap(); }
		break;
	case EHitmenCheat::Revive:
		if (GM) { GM->DebugRevivePlayer(this); }
		break;
	default:
		PS->SetCheat(Cheat, !PS->HasCheat(Cheat));
		break;
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
	// Nobody alive to watch: player view and split screen have nothing to show, free fly it is (always allowed then).
	const bool bNobodyToWatch = GetSpectatableTargets().IsEmpty();
	if (bNobodyToWatch)
	{
		NewMode = EHitmenSpectatorMode::FreeFly;
	}
	// Dead players free fly too unless Project Settings -> Hitmen -> Watchers -> bSpectatorsCanFreeFly is off.
	else if (!bIsWatcher && NewMode == EHitmenSpectatorMode::FreeFly && !UHitmenGameSettings::Get()->bSpectatorsCanFreeFly)
	{
		NewMode = EHitmenSpectatorMode::Follow;
	}
	// Free fly starts where the camera was (a player's eyes, the third-person camera).
	if (NewMode == EHitmenSpectatorMode::FreeFly && SpectatorMode != EHitmenSpectatorMode::FreeFly && PlayerCameraManager)
	{
		if (AHitmenSpectatorPawn* Spectator = Cast<AHitmenSpectatorPawn>(GetSpectatorPawn()))
		{
			Spectator->TeleportToView(PlayerCameraManager->GetCameraLocation(), PlayerCameraManager->GetCameraRotation());
			SetControlRotation(PlayerCameraManager->GetCameraRotation());
		}
	}
	SpectatorMode = NewMode;
	ApplySpectatorView();
	if (SpectatorMode == EHitmenSpectatorMode::Grid)
	{
		RebuildSpectatorGrid();
		GetWorldTimerManager().SetTimer(SpectatorGridTimer, this, &AHitmenPlayerController::RebuildSpectatorGrid, SpectatorGridRefreshInterval, true);
	}
	else
	{
		GetWorldTimerManager().ClearTimer(SpectatorGridTimer);
		StopSpectatorCaptures();
	}
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

void AHitmenPlayerController::StopSpectatorCaptures()
{
	for (const TWeakObjectPtr<APawn>& CapturedPawn : CapturedPawns)
	{
		if (const AHitmenPlayerCharacter* Captured = Cast<AHitmenPlayerCharacter>(CapturedPawn.Get()))
		{
			if (USceneCaptureComponent2D* Capture = Captured->GetSpectatorCapture())
			{
				Capture->bCaptureEveryFrame = false;
				Capture->TextureTarget = nullptr;
				Capture->Deactivate();
			}
		}
	}
	const bool bHadViews = !CapturedPawns.IsEmpty();
	CapturedPawns.Reset();
	if (bHadViews)
	{
		if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
		{
			UI->SetSpectatorViews(TArray<FHitmenSpectatorView>());
		}
	}
}

void AHitmenPlayerController::RebuildSpectatorGrid()
{
	TArray<APawn*> Targets = GetSpectatableTargets();
	if (Targets.Num() > MaxSplitViews)
	{
		// Keep the followed player first so the cut never drops them.
		if (APawn* Followed = SpectatorTarget.Get(); Followed && Targets.Contains(Followed))
		{
			Targets.Remove(Followed);
			Targets.Insert(Followed, 0);
		}
		Targets.SetNum(MaxSplitViews);
	}

	// Unchanged set: nothing to rebuild (the timer calls this every few seconds).
	bool bSame = Targets.Num() == CapturedPawns.Num();
	for (int32 Index = 0; bSame && Index < Targets.Num(); ++Index)
	{
		bSame = CapturedPawns[Index].Get() == Targets[Index];
	}
	if (bSame && !Targets.IsEmpty())
	{
		return;
	}

	StopSpectatorCaptures();
	// Pictures shaped like their cell on screen (1, 2 side by side, 2x2, 3x2, 4x2), smaller above 4.
	int32 Columns = 1;
	int32 Rows = 1;
	HitmenUI::GetSplitLayout(Targets.Num(), Columns, Rows);
	FVector2D ViewportSize(1920.f, 1080.f);
	if (GEngine && GEngine->GameViewport)
	{
		GEngine->GameViewport->GetViewportSize(ViewportSize);
	}
	const float CellAspect = FMath::Clamp((ViewportSize.X / Columns) / FMath::Max(ViewportSize.Y / Rows, 1.f), 0.3f, 4.f);
	const int32 Width = Targets.Num() > 4 ? SpectatorSmallCaptureWidth : SpectatorCaptureWidth;
	const int32 Height = FMath::Max(64, FMath::RoundToInt(Width / CellAspect));
	TArray<FHitmenSpectatorView> Views;
	for (int32 Index = 0; Index < Targets.Num(); ++Index)
	{
		AHitmenPlayerCharacter* Captured = Cast<AHitmenPlayerCharacter>(Targets[Index]);
		USceneCaptureComponent2D* Capture = Captured ? Captured->GetSpectatorCapture() : nullptr;
		if (!Capture)
		{
			continue;
		}
		if (!SpectatorRenderTargets.IsValidIndex(Index) || !SpectatorRenderTargets[Index])
		{
			SpectatorRenderTargets.SetNum(FMath::Max(SpectatorRenderTargets.Num(), Index + 1));
			// Opaque LDR pictures (scene colour HDR would carry an inverted alpha and show see-through in the grid).
			SpectatorRenderTargets[Index] = UKismetRenderingLibrary::CreateRenderTarget2D(this, Width, Height, RTF_RGBA8_SRGB);
		}
		else if (SpectatorRenderTargets[Index]->SizeX != Width || SpectatorRenderTargets[Index]->SizeY != Height)
		{
			UKismetRenderingLibrary::ResizeRenderTarget2D(SpectatorRenderTargets[Index], Width, Height);
		}
		// Same vertical field as a 90 degree 16:9 view, whatever the cell's shape.
		const float HalfVertical = FMath::Atan(FMath::Tan(FMath::DegreesToRadians(45.f)) * 9.f / 16.f);
		Capture->FOVAngle = FMath::RadiansToDegrees(2.f * FMath::Atan(FMath::Tan(HalfVertical) * CellAspect));
		Capture->CaptureSource = ESceneCaptureSource::SCS_FinalColorLDR;
		// Through their eyes: their own body, arms, weapons and cosmetics stay out of the picture.
		Capture->HiddenActors.Reset();
		Capture->HiddenActors.Add(Captured);
		TArray<AActor*> Attached;
		Captured->GetAttachedActors(Attached, true, true);
		for (AActor* Child : Attached)
		{
			Capture->HiddenActors.Add(Child);
		}
		Capture->TextureTarget = SpectatorRenderTargets[Index];
		Capture->bCaptureEveryFrame = false;
		Capture->Activate();
		Capture->CaptureScene();
		CapturedPawns.Add(Captured);

		FHitmenSpectatorView& View = Views.AddDefaulted_GetRef();
		View.RenderTarget = SpectatorRenderTargets[Index];
		View.PlayerName = Captured->GetPlayerState() ? FText::FromString(Captured->GetPlayerState()->GetPlayerName()) : FText::GetEmpty();
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		UI->SetSpectatorViews(Views);
	}
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
			// First person: through their eyes; third person: the spectator camera behind them (PlayerTick).
			if (bSpectatorThirdPerson)
			{
				UpdateSpectatorThirdPerson();
				SetViewTargetWithBlend(GetSpectatorViewPawn(), 0.2f);
			}
			else
			{
				SetViewTargetWithBlend(Target, 0.25f);
			}
		}
	}
	else
	{
		// Free fly and grid both use the spectator pawn camera underneath.
		SetViewTargetWithBlend(GetSpectatorViewPawn(), 0.25f);
	}
	if (AHitmenSpectatorPawn* Spectator = Cast<AHitmenSpectatorPawn>(GetSpectatorPawn()))
	{
		Spectator->SetFreeFly(SpectatorMode == EHitmenSpectatorMode::FreeFly);
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this))
	{
		const APawn* Followed = SpectatorMode == EHitmenSpectatorMode::Follow ? SpectatorTarget.Get() : nullptr;
		UI->SetSpectatorTargetName(Followed && Followed->GetPlayerState() ? FText::FromString(Followed->GetPlayerState()->GetPlayerName()) : FText::GetEmpty());
		UI->OnSpectatorModeChanged.Broadcast(static_cast<uint8>(SpectatorMode));
	}
	OnSpectatorTargetChanged.Broadcast(SpectatorTarget.Get());
}

void AHitmenPlayerController::PlayerTick(float DeltaTime)
{
	Super::PlayerTick(DeltaTime);
	if (IsCheatFlying() && !IsGameplayInputBlocked())
	{
		GetPawn()->AddMovementInput(FVector::UpVector, (bFlyUpHeld ? 1.f : 0.f) - (bFlyDownHeld ? 1.f : 0.f));
	}
	PushVehicleView();
	if (IsLocalController())
	{
		UpdateSecurityFeed(DeltaTime);
	}
	// QA scripted move input (console HitmenPress Move <forward> <right>).
	if (!QAMoveInput.IsNearlyZero() && GetPawn())
	{
		const FRotator Yaw(0.f, GetControlRotation().Yaw, 0.f);
		GetPawn()->AddMovementInput(Yaw.Vector(), QAMoveInput.X);
		GetPawn()->AddMovementInput(FRotationMatrix(Yaw).GetUnitAxis(EAxis::Y), QAMoveInput.Y);
	}
	if (bSpectating && IsLocalController())
	{
		UpdateSpectatorThirdPerson();
		TickSpectatorCaptures(DeltaTime);
		ReportWatcherView();
	}
}

void AHitmenPlayerController::UpdateSpectatorThirdPerson()
{
	if (SpectatorMode != EHitmenSpectatorMode::Follow || !bSpectatorThirdPerson)
	{
		return;
	}
	const APawn* Target = SpectatorTarget.Get();
	AHitmenSpectatorPawn* Spectator = Cast<AHitmenSpectatorPawn>(GetSpectatorPawn());
	if (!Target || !Spectator)
	{
		return;
	}
	// Over the shoulder, orbiting with the mouse; pulled in front of walls.
	const FRotator View = GetControlRotation();
	const FVector Pivot = Target->GetPawnViewLocation() + FVector(0.f, 0.f, 15.f);
	FVector Camera = Pivot - View.Vector() * SpectatorThirdPersonDistance + FRotationMatrix(View).GetUnitAxis(EAxis::Y) * 45.f;
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSpectatorThirdPerson), false, Target);
	Params.AddIgnoredActor(Spectator);
	if (GetWorld()->SweepSingleByChannel(Hit, Pivot, Camera, FQuat::Identity, ECC_Camera, FCollisionShape::MakeSphere(12.f), Params))
	{
		Camera = Hit.Location;
	}
	Spectator->TeleportToView(Camera, View);
}

void AHitmenPlayerController::TickSpectatorCaptures(float DeltaTime)
{
	if (SpectatorMode != EHitmenSpectatorMode::Grid || CapturedPawns.IsEmpty())
	{
		return;
	}
	// Round robin: each picture about SpectatorCaptureRate times a second, a few captures per frame at most.
	SpectatorCaptureBudget += DeltaTime * SpectatorCaptureRate * CapturedPawns.Num();
	for (int32 Done = 0; SpectatorCaptureBudget >= 1.f && Done < CapturedPawns.Num(); ++Done)
	{
		SpectatorCaptureCursor = (SpectatorCaptureCursor + 1) % CapturedPawns.Num();
		if (const AHitmenPlayerCharacter* Captured = Cast<AHitmenPlayerCharacter>(CapturedPawns[SpectatorCaptureCursor].Get()))
		{
			if (USceneCaptureComponent2D* Capture = Captured->GetSpectatorCapture())
			{
				Capture->CaptureScene();
			}
		}
		SpectatorCaptureBudget -= 1.f;
	}
	SpectatorCaptureBudget = FMath::Min(SpectatorCaptureBudget, 1.f);
}

void AHitmenPlayerController::ReportWatcherView()
{
	if (!PlayerCameraManager)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	const float Interval = 1.f / FMath::Max(1.f, UHitmenGameSettings::Get()->WatcherViewRate);
	if (Now - LastWatcherReportTime < Interval)
	{
		return;
	}
	const FVector Location = PlayerCameraManager->GetCameraLocation();
	const FRotator Rotation = PlayerCameraManager->GetCameraRotation();
	const APawn* Followed = SpectatorMode == EHitmenSpectatorMode::Follow ? SpectatorTarget.Get() : nullptr;
	AHitmenPlayerState* Watched = Followed ? Followed->GetPlayerState<AHitmenPlayerState>() : nullptr;
	// Only when something changed (a heartbeat every 2 s).
	if (Location.Equals(LastReportedLocation, 20.f) && Rotation.Equals(LastReportedRotation, 2.f) && Watched == LastReportedWatched.Get()
		&& Now - LastWatcherReportTime < 2.f)
	{
		return;
	}
	LastWatcherReportTime = Now;
	LastReportedLocation = Location;
	LastReportedRotation = Rotation;
	LastReportedWatched = Watched;
	Server_ReportWatcherView(Location, Rotation.Yaw, Rotation.Pitch, Watched);
}

void AHitmenPlayerController::Server_ReportWatcherView_Implementation(FVector_NetQuantize10 Location, float Yaw, float Pitch, AHitmenPlayerState* Watched)
{
	// Only a player without a gameplay pawn watches.
	if (const APawn* Current = GetPawn(); Current && !Current->IsA<ASpectatorPawn>())
	{
		return;
	}
	if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>())
	{
		FHitmenWatcherView View;
		View.Location = Location;
		View.Yaw = Yaw;
		View.Pitch = FMath::ClampAngle(Pitch, -89.f, 89.f);
		View.Watched = Watched != PS ? Watched : nullptr;
		PS->SetWatcherView(View);
	}
}

void AHitmenPlayerController::UpdateWatcherPresence()
{
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	const AGameStateBase* GS = GetWorld() ? GetWorld()->GetGameState() : nullptr;
	const AHitmenPlayerState* Self = GetPlayerState<AHitmenPlayerState>();
	const bool bShowWatchers = bSpectating ? IsWatcherWatchersShown() : Settings->bPlayersSeeWatchers;
	const bool bOutlinePlayers = bSpectating && IsWatcherPlayersShown();

	// Nobody left to watch in player view or split screen: free fly.
	if (bSpectating && SpectatorMode != EHitmenSpectatorMode::FreeFly && GetSpectatableTargets().IsEmpty())
	{
		SetSpectatorMode(EHitmenSpectatorMode::FreeFly);
	}
	UpdateVoiceRules();

	// Avatars of the watchers flying free (one following a player in player view is with that player). They always
	// exist (proximity voice comes from them) and are drawn when this player may see them: watchers see orbs, players
	// (bPlayersSeeWatchers) ghosts.
	TArray<AHitmenPlayerState*> Wanted;
	if (GS)
	{
		for (APlayerState* Base : GS->PlayerArray)
		{
			AHitmenPlayerState* PS = Cast<AHitmenPlayerState>(Base);
			if (PS && PS != Self && PS->GetWatcherView().bWatching && !PS->GetWatcherView().Watched)
			{
				Wanted.Add(PS);
			}
		}
	}
	for (int32 Index = WatcherAvatars.Num() - 1; Index >= 0; --Index)
	{
		AHitmenWatcherAvatar* Avatar = WatcherAvatars[Index];
		if (Avatar && Wanted.Contains(Avatar->GetWatcher()))
		{
			Wanted.Remove(Avatar->GetWatcher());
			continue;
		}
		if (Avatar)
		{
			Avatar->Destroy();
		}
		WatcherAvatars.RemoveAt(Index);
	}
	for (AHitmenPlayerState* PS : Wanted)
	{
		FActorSpawnParameters Params;
		Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		Params.ObjectFlags |= RF_Transient;
		if (AHitmenWatcherAvatar* Avatar = GetWorld()->SpawnActor<AHitmenWatcherAvatar>(FVector(PS->GetWatcherView().Location), FRotator::ZeroRotator, Params))
		{
			Avatar->SetWatcher(PS);
			WatcherAvatars.Add(Avatar);
		}
	}
	const AHitmenGameState* HitmenGS = Cast<AHitmenGameState>(GS);
	const bool bProximityVoice = HitmenGS && HitmenGS->GetLobbySettings().bProximityChat && Settings->bWatcherProximityVoice;
	USoundAttenuation* VoiceAttenuation = bProximityVoice ? Settings->VoiceAttenuation.LoadSynchronous() : nullptr;
	for (AHitmenWatcherAvatar* Avatar : WatcherAvatars)
	{
		if (Avatar)
		{
			Avatar->SetPresentation(bShowWatchers, !bSpectating);
			Avatar->SetVoice(bProximityVoice, VoiceAttenuation);
		}
	}

	// Players outlined through walls for watchers (PP_Markers, stencil WatchedPlayer).
	TSet<TWeakObjectPtr<AActor>> Outlined;
	if (GS && bOutlinePlayers)
	{
		for (APlayerState* Base : GS->PlayerArray)
		{
			const AHitmenPlayerState* PS = Cast<AHitmenPlayerState>(Base);
			AHitmenPlayerCharacter* OutlinedPawn = PS && PS->IsAlive() ? Cast<AHitmenPlayerCharacter>(PS->GetPawn()) : nullptr;
			if (OutlinedPawn)
			{
				Outlined.Add(OutlinedPawn);
			}
		}
	}
	for (const TWeakObjectPtr<AActor>& Old : WatcherHighlightedPlayers)
	{
		if (AActor* Actor = Old.Get(); Actor && !Outlined.Contains(Old))
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, false, 0);
		}
	}
	for (const TWeakObjectPtr<AActor>& New : Outlined)
	{
		if (AActor* Actor = New.Get(); Actor && !WatcherHighlightedPlayers.Contains(New))
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, true, HitmenStencil::WatchedPlayer);
		}
	}
	WatcherHighlightedPlayers = MoveTemp(Outlined);
}

void AHitmenPlayerController::UpdateVoiceRules()
{
	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	if (!GS || !IsLocalController())
	{
		return;
	}
	// Only during a match: in the lobby everybody talks.
	const EHitmenMatchPhase Phase = GS->GetMatchPhase();
	const bool bInMatch = Phase == EHitmenMatchPhase::Intro || Phase == EHitmenMatchPhase::Playing;
	const bool bDeadTalkToLiving = UHitmenGameSettings::Get()->bDeadCanTalkToLiving;
	const APlayerState* Self = GetPlayerState<APlayerState>();
	for (APlayerState* Base : GS->PlayerArray)
	{
		const AHitmenPlayerState* PS = Cast<AHitmenPlayerState>(Base);
		if (!PS || Base == Self || !Base->GetUniqueId().IsValid())
		{
			continue;
		}
		// The living never hear the dead or the watchers (unless allowed); the dead and watchers hear everyone.
		const bool bOut = PS->IsWatcher() || !PS->IsAlive();
		const bool bHear = !bInMatch || bSpectating || !bOut || bDeadTalkToLiving;
		const bool bMuted = VoiceMutedPlayers.Contains(Base);
		if (!bHear && !bMuted)
		{
			GameplayMutePlayer(Base->GetUniqueId());
			VoiceMutedPlayers.Add(Base);
		}
		else if (bHear && bMuted)
		{
			GameplayUnmutePlayer(Base->GetUniqueId());
			VoiceMutedPlayers.Remove(Base);
		}
	}
}

void AHitmenPlayerController::ClearWatcherPresence()
{
	GetWorldTimerManager().ClearTimer(WatcherPresenceTimer);
	for (AHitmenWatcherAvatar* Avatar : WatcherAvatars)
	{
		if (Avatar)
		{
			Avatar->Destroy();
		}
	}
	WatcherAvatars.Reset();
	for (const TWeakObjectPtr<AActor>& Old : WatcherHighlightedPlayers)
	{
		if (AActor* Actor = Old.Get())
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, false, 0);
		}
	}
	WatcherHighlightedPlayers.Reset();
}

void AHitmenPlayerController::PushVehicleView()
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(this);
	if (!UI)
	{
		return;
	}
	const AHitmenVehiclePawn* Vehicle = Cast<AHitmenVehiclePawn>(GetPawn());
	if (!Vehicle && !bVehicleViewActive)
	{
		return;
	}
	FHitmenHUDVehicleView View;
	if (Vehicle)
	{
		View.bActive = true;
		View.Name = UHitmenTextSubsystem::Translate(Vehicle->DisplayName);
		View.Health = Vehicle->GetHealthNormalized();
		View.Reload = Vehicle->GetReloadAlpha();
	}
	bVehicleViewActive = View.bActive;
	UI->SetVehicleView(View);
}

void AHitmenPlayerController::UpdateSecurityFeed(float DeltaTime)
{
	SecurityFeedAccumulator += DeltaTime;
	if (SecurityFeedAccumulator < 0.15f)
	{
		return;
	}
	SecurityFeedAccumulator = 0.f;
	// A camera whose meter fills on this player: its feed top left while it sees them (off by default).
	AHitmenSecurityCamera* Seeing = nullptr;
	const APawn* MyPawn = GetPawn();
	if (UHitmenGameSettings::Get()->bCameraFeedWhenSeen && MyPawn && !bSpectating)
	{
		for (TActorIterator<AHitmenSecurityCamera> It(GetWorld()); It; ++It)
		{
			if (It->IsSeeingIntruder() && It->GetSeenIntruder() == MyPawn && !It->IsCameraDisabled())
			{
				Seeing = *It;
				break;
			}
		}
	}
	if (Seeing)
	{
		if (!EventCamera)
		{
			FActorSpawnParameters Params;
			Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
			Params.ObjectFlags |= RF_Transient;
			EventCamera = GetWorld()->SpawnActor<AHitmenEventCamera>(Params);
		}
		if (EventCamera)
		{
			EventCamera->ShowFrom(Seeing->GetFeedView(), Seeing->GetFeedFOV(), HITMEN_TEXT("Hitmen.EventCameraFeed", "Camera"), 0.f, this);
		}
		SecurityFeedCamera = Seeing;
	}
	else if (SecurityFeedCamera.IsValid())
	{
		SecurityFeedCamera.Reset();
		if (EventCamera && EventCamera->IsShowingView())
		{
			EventCamera->Stop();
		}
	}
}

void AHitmenPlayerController::ApplyCheatFlySpeed(bool bFast)
{
	ACharacter* Flyer = Cast<ACharacter>(GetPawn());
	if (UCharacterMovementComponent* Movement = Flyer ? Flyer->GetCharacterMovement() : nullptr; Movement && IsCheatFlying())
	{
		const float Speed = CheatFlySpeed * (bFast ? CheatFlyFastMultiplier : 1.f);
		Movement->MaxFlySpeed = Speed;
		Movement->BrakingDecelerationFlying = Speed * 3.f;
	}
}

void AHitmenPlayerController::Server_SetCheatFlyFast_Implementation(bool bFast)
{
	ApplyCheatFlySpeed(bFast);
}

void AHitmenPlayerController::HitmenTraverse()
{
	const AHitmenCharacterBase* Traverser = Cast<AHitmenCharacterBase>(GetPawn());
	UHitmenCharacterMovementComponent* Movement = Traverser ? Traverser->GetHitmenMovement() : nullptr;
	if (Movement)
	{
		const bool bStarted = Movement->DebugTryMantle(FRotator(0.f, GetControlRotation().Yaw, 0.f).Vector());
		UE_LOG(LogHitmen, Display, TEXT("HitmenTraverse: %s"), bStarted ? TEXT("started") : TEXT("nothing to traverse"));
	}
}

void AHitmenPlayerController::HitmenPress(const FString& Action, float X, float Y)
{
	if (!DebugAllowed(GetWorld()))
	{
		return;
	}
	const FString A = Action.ToLower();
	if (A == TEXT("fire")) { HandleFirePressed(); }
	else if (A == TEXT("aim")) { HandleAimPressed(); }
	else if (A == TEXT("jump")) { HandleJumpPressed(); }
	else if (A == TEXT("crouch")) { HandleCrouchPressed(); }
	else if (A == TEXT("sprint")) { HandleSprintPressed(); }
	else if (A == TEXT("interact")) { HandleInteract(); }
	else if (A == TEXT("moveability")) { HandleMoveAbilityPressed(); }
	else if (A == TEXT("manipulation")) { HandleManipulationPressed(); }
	else if (A == TEXT("pause")) { HandlePause(); }
	else if (A == TEXT("switchslot")) { HandleSwitchSlot(FInputActionValue(1.f)); }
	else if (A == TEXT("move")) { QAMoveInput = FVector2D(X, Y); }
	else if (A == TEXT("emote")) { HandleEmoteWheelPressed(); }
	else if (A == TEXT("view1")) { HandleSpectatorPlayerView(); }
	else if (A == TEXT("view2")) { HandleSpectatorSplitView(); }
	else if (A == TEXT("view3")) { HandleSpectatorFreeView(); }
	UE_LOG(LogHitmen, Display, TEXT("HitmenPress %s"), *Action);
}

void AHitmenPlayerController::HitmenRelease(const FString& Action)
{
	if (!DebugAllowed(GetWorld()))
	{
		return;
	}
	const FString A = Action.ToLower();
	if (A == TEXT("fire")) { HandleFireReleased(); }
	else if (A == TEXT("aim")) { HandleAimReleased(); }
	else if (A == TEXT("jump")) { HandleJumpReleased(); }
	else if (A == TEXT("crouch")) { HandleCrouchReleased(); }
	else if (A == TEXT("sprint")) { HandleSprintReleased(); }
	else if (A == TEXT("interact")) { HandleInteractReleased(); }
	else if (A == TEXT("moveability")) { HandleMoveAbilityReleased(); }
	else if (A == TEXT("manipulation")) { HandleManipulationReleased(); }
	else if (A == TEXT("move")) { QAMoveInput = FVector2D::ZeroVector; }
	else if (A == TEXT("emote")) { HandleEmoteWheelReleased(); }
	UE_LOG(LogHitmen, Display, TEXT("HitmenRelease %s"), *Action);
}

void AHitmenPlayerController::ApplyKeyboardLayout(UEnhancedInputUserSettings* UserSettings)
{
	UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	UEnhancedPlayerMappableKeyProfile* Profile = UserSettings ? UserSettings->GetActiveKeyProfile() : nullptr;
	if (!Settings || !Profile)
	{
		return;
	}
	// The default keys are authored for AZERTY (Z Q S D, A, E). On a QWERTY keyboard the same places are W A S D, Q, E:
	// moved there once, only for keys the player never rebound.
	const int32 Wanted = HitmenUI::IsAzertyLayout() ? 1 : 2;
	if (Settings->AppliedKeyboardLayout == Wanted)
	{
		return;
	}
	const TMap<FKey, FKey> AzertyToQwerty = { { EKeys::Z, EKeys::W }, { EKeys::Q, EKeys::A }, { EKeys::A, EKeys::Q }, { EKeys::W, EKeys::Z } };
	int32 Moved = 0;
	for (const TPair<FName, FKeyMappingRow>& Row : Profile->GetPlayerMappingRows())
	{
		for (const FPlayerKeyMapping& Mapping : Row.Value.Mappings)
		{
			const FKey Default = Mapping.GetDefaultKey();
			const FKey* OnQwerty = AzertyToQwerty.Find(Default);
			if (!OnQwerty)
			{
				continue;
			}
			// Untouched by the player: on its default (AZERTY) or on the QWERTY place this code gave it.
			const FKey Current = Mapping.GetCurrentKey();
			if (Current != Default && Current != *OnQwerty)
			{
				continue;
			}
			const FKey Target = Wanted == 2 ? *OnQwerty : Default;
			if (Current == Target)
			{
				continue;
			}
			FMapPlayerKeyArgs Args;
			Args.MappingName = Mapping.GetMappingName();
			Args.Slot = Mapping.GetSlot();
			Args.NewKey = Target;
			FGameplayTagContainer Failure;
			UserSettings->MapPlayerKey(Args, Failure);
			++Moved;
		}
	}
	Settings->AppliedKeyboardLayout = Wanted;
	Settings->SaveSettings();
	UserSettings->ApplySettings();
	UserSettings->SaveSettings();
	UE_LOG(LogHitmen, Log, TEXT("Keyboard layout %s: %d default keys placed."), Wanted == 2 ? TEXT("QWERTY") : TEXT("AZERTY"), Moved);
}
