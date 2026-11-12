// Hitmen — player controller

#include "Framework/HitmenPlayerController.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/Character.h"
#include "World/Vehicles/HitmenVehicles.h"
#include "Data/HitmenSoundSet.h"
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
#include "Data/HitmenAbilityData.h"
#include "Data/HitmenWeaponData.h"
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
		// After a travel the PlayerState may be fresh: re-send the agent chosen in the front end.
		if (const UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>())
		{
			if (GI->bHasLocalLoadout) { Server_SetLoadout(GI->LocalLoadout); }
			if (GI->bHasLocalCosmetics) { Server_SetCosmetics(GI->LocalCosmetics); }
		}
		// The HUD owns the input mode (menu screens keep the cursor); it may not exist yet.
		RefreshInputMode();
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
	const UInputAction* Actions[] = { InteractAction, JumpAction, MoveAbilityAction, ManipulationAbilityAction, SightroomAction, ConfirmAction };
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
	BindIfSet(ConfirmAction, ETriggerEvent::Started, &AHitmenPlayerController::HandleConfirm);

	// Cheats (gym / dev builds): fixed F-keys, not rebindable (the engine's F1-F5 view modes are cleared in
	// DefaultInput.ini).
	const FKey CheatKeys[] = { EKeys::F1, EKeys::F2, EKeys::F3, EKeys::F4, EKeys::F5, EKeys::F6, EKeys::F7, EKeys::F8 };
	for (const FKey& Key : CheatKeys)
	{
		InputComponent->BindKey(Key, IE_Pressed, this, &AHitmenPlayerController::HandleCheatKey);
	}
}

void AHitmenPlayerController::HandleCheatKey(FKey Key)
{
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	const FKey CheatKeys[] = { EKeys::F1, EKeys::F2, EKeys::F3, EKeys::F4, EKeys::F5, EKeys::F6, EKeys::F7, EKeys::F8 };
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
		bSpectating = false;
		if (IsLocalController())
		{
			RefreshMappingContexts();
		}
	}
}

IHitmenInputReceiver* AHitmenPlayerController::GetReceiver() const
{
	// While spectating, GetPawn() is null and the free-fly pawn is the spectator pawn.
	return Cast<IHitmenInputReceiver>(GetPawnOrSpectator());
}

void AHitmenPlayerController::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UnbindHUDSources();
	StopSpectatorCaptures();
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
			SlotView.Name = Slots[Index].Weapon->DisplayName;
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
			Names.Add(Emote.DisplayName);
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
			View.Name = Ability->GetData()->DisplayName;
			View.Charges = Ability->GetCharges();
			View.MaxCharges = Ability->GetMaxCharges();
			View.CooldownRemaining = Ability->GetCooldownRemaining();
			View.CooldownAlpha = 1.f - Ability->GetCooldownNormalized();
			View.Icon = Ability->GetData()->Icon.LoadSynchronous();
			if (const UHitmenAbility_Jetpack* Jetpack = Cast<UHitmenAbility_Jetpack>(Ability))
			{
				View.Meter = Jetpack->GetFuelNormalized();
			}
		}
		return View;
	};
	const UHitmenAbilityComponent* Abilities = HUDAbilities.Get();
	UI->SetAbilityViews(MakeAbilityView(Abilities ? Abilities->GetAbility(EHitmenAbilitySlot::Move) : nullptr),
		MakeAbilityView(Abilities ? Abilities->GetAbility(EHitmenAbilitySlot::Manipulation) : nullptr));

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
			Blip.Color = Alert->GetAlertColor();
			Blip.State = Alert->GetAlertState();
			Blip.Suspicion = Alert->GetSuspicion();
			Blip.bTargetsViewer = Alert->GetTarget() == ViewPawn;
			Blip.bCallingReinforcements = Alert->IsCallingReinforcements();
		}
	}
	// Security cameras that are watching an intruder and have you in clear view (awareness arcs, camera mark).
	if (UWorld* World = GetWorld())
	{
		const FVector Eye = ViewPawn->GetPawnViewLocation();
		for (TActorIterator<AHitmenSecurityCamera> It(World); It; ++It)
		{
			if (It->IsCameraDisabled() || !It->IsSeeingIntruder())
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
			FHitmenHUDAlertBlip& Blip = Blips.AddDefaulted_GetRef();
			Blip.Location = It->GetActorLocation();
			Blip.Color = EHitmenAlertColor::Red;
			Blip.State = EHitmenAlertState::Combat;
			Blip.Suspicion = 1.f;
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
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (Settings && Settings->bToggleSprint)
	{
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
	if (bSpectating && SpectatorMode == EHitmenSpectatorMode::Grid)
	{
		// Mouse wheel in the grid: one, two, three... or every screen.
		CycleSpectatorGridCount(Direction > 0.f ? 1 : -1);
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
	if (bSpectating)
	{
		CycleSpectatorMode();
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

void AHitmenPlayerController::Server_SetCosmetics_Implementation(const FHitmenCosmeticLoadout& Cosmetics)
{
	if (AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>()) { PS->SetCosmetics(Cosmetics); }
	if (AHitmenPlayerCharacter* Agent = Cast<AHitmenPlayerCharacter>(GetPawn()))
	{
		Agent->ApplyCosmetics(Cosmetics);
	}
}

void AHitmenPlayerController::Server_SetLoadout_Implementation(const FHitmenLoadout& Loadout)
{
	AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>();
	if (!PS)
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

void AHitmenPlayerController::Client_EnterSpectatorMode_Implementation(bool bWatcher)
{
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
	// Dead players cannot free-fly unless they are watchers.
	if (!bIsWatcher && NewMode == EHitmenSpectatorMode::FreeFly)
	{
		NewMode = EHitmenSpectatorMode::Follow;
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

void AHitmenPlayerController::CycleSpectatorGridCount(int32 Direction)
{
	const int32 Available = GetSpectatableTargets().Num();
	if (Available <= 0)
	{
		return;
	}
	// 0 means "all": cycle 1, 2, ... Available-1, all.
	int32 Current = SpectatorGridCount <= 0 || SpectatorGridCount >= Available ? Available : SpectatorGridCount;
	Current = (Current - 1 + FMath::Sign(Direction) + Available) % Available + 1;
	SpectatorGridCount = Current >= Available ? 0 : Current;
	if (SpectatorMode == EHitmenSpectatorMode::Grid)
	{
		RebuildSpectatorGrid();
	}
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
	if (SpectatorGridCount > 0 && Targets.Num() > SpectatorGridCount)
	{
		// Keep the followed player first so a smaller grid still shows them.
		if (APawn* Followed = SpectatorTarget.Get(); Followed && Targets.Contains(Followed))
		{
			Targets.Remove(Followed);
			Targets.Insert(Followed, 0);
		}
		Targets.SetNum(SpectatorGridCount);
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
			SpectatorRenderTargets[Index] = UKismetRenderingLibrary::CreateRenderTarget2D(this, SpectatorCaptureResolution.X, SpectatorCaptureResolution.Y);
		}
		Capture->TextureTarget = SpectatorRenderTargets[Index];
		Capture->bCaptureEveryFrame = true;
		Capture->Activate();
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

void AHitmenPlayerController::PlayerTick(float DeltaTime)
{
	Super::PlayerTick(DeltaTime);
	if (IsCheatFlying() && !IsGameplayInputBlocked())
	{
		GetPawn()->AddMovementInput(FVector::UpVector, (bFlyUpHeld ? 1.f : 0.f) - (bFlyDownHeld ? 1.f : 0.f));
	}
	PushVehicleView();
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
		View.Name = Vehicle->DisplayName;
		View.Health = Vehicle->GetHealthNormalized();
		View.Reload = Vehicle->GetReloadAlpha();
	}
	bVehicleViewActive = View.bActive;
	UI->SetVehicleView(View);
}
