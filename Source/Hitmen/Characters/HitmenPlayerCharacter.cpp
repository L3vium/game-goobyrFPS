// Hitmen — player character

#include "Characters/HitmenPlayerCharacter.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenGameState.h"
#include "AI/HitmenAIController.h"
#include "Core/HitmenTypes.h"
#include "Core/HitmenUserSettings.h"
#include "Core/HitmenTeamLibrary.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenAbilityData.h"
#include "Components/HitmenHealthComponent.h"
#include "Components/HitmenCosmeticComponent.h"
#include "Components/HitmenMeleeComponent.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "Components/HitmenSightroomComponent.h"
#include "Components/HitmenEmoteComponent.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Abilities/HitmenAbilityComponent.h"
#include "Interaction/HitmenInteractionComponent.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Camera/CameraComponent.h"
#include "Components/SceneCaptureComponent2D.h"
#include "Components/SkeletalMeshComponent.h"
#include "Net/VoiceConfig.h"
#include "Net/UnrealNetwork.h"
#include "AIController.h"
#include "Engine/World.h"

AHitmenPlayerCharacter::AHitmenPlayerCharacter(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	PrimaryActorTick.bCanEverTick = true;
	bUseControllerRotationYaw = true;

	FirstPersonMesh = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("FirstPersonMesh"));
	FirstPersonMesh->SetupAttachment(GetMesh());
	FirstPersonMesh->SetOnlyOwnerSee(true);
	FirstPersonMesh->FirstPersonPrimitiveType = EFirstPersonPrimitiveType::FirstPerson;
	FirstPersonMesh->SetCollisionProfileName(TEXT("NoCollision"));
	FirstPersonMesh->CastShadow = false;

	FirstPersonCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FirstPersonCamera"));
	FirstPersonCamera->SetupAttachment(FirstPersonMesh, TEXT("head"));
	FirstPersonCamera->SetRelativeLocationAndRotation(FVector(-2.8f, 5.89f, 0.f), FRotator(0.f, 90.f, -90.f));
	FirstPersonCamera->bUsePawnControlRotation = true;
	FirstPersonCamera->bEnableFirstPersonFieldOfView = true;
	FirstPersonCamera->bEnableFirstPersonScale = true;
	FirstPersonCamera->FirstPersonFieldOfView = 70.f;
	FirstPersonCamera->FirstPersonScale = 0.6f;

	SpectatorCapture = CreateDefaultSubobject<USceneCaptureComponent2D>(TEXT("SpectatorCapture"));
	SpectatorCapture->SetupAttachment(FirstPersonCamera);
	SpectatorCapture->bCaptureEveryFrame = false;
	SpectatorCapture->bCaptureOnMovement = false;
	SpectatorCapture->SetAutoActivate(false);

	GetMesh()->SetOwnerNoSee(true);
	GetMesh()->FirstPersonPrimitiveType = EFirstPersonPrimitiveType::WorldSpaceRepresentation;

	InventoryComponent = CreateDefaultSubobject<UHitmenInventoryComponent>(TEXT("Inventory"));
	AbilityComponent = CreateDefaultSubobject<UHitmenAbilityComponent>(TEXT("Abilities"));
	InteractionComponent = CreateDefaultSubobject<UHitmenInteractionComponent>(TEXT("Interaction"));
	MeleeComponent = CreateDefaultSubobject<UHitmenMeleeComponent>(TEXT("Melee"));
	BodyCarryComponent = CreateDefaultSubobject<UHitmenBodyCarryComponent>(TEXT("BodyCarry"));
	SightroomComponent = CreateDefaultSubobject<UHitmenSightroomComponent>(TEXT("Sightroom"));
	EmoteComponent = CreateDefaultSubobject<UHitmenEmoteComponent>(TEXT("Emote"));

	CharmedControllerClass = AHitmenAIController::StaticClass();
}

void AHitmenPlayerCharacter::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME_CONDITION(AHitmenPlayerCharacter, bIsAiming, COND_SkipOwner);
}

void AHitmenPlayerCharacter::BeginPlay()
{
	Super::BeginPlay();
	if (IsLocallyControlled())
	{
		if (const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
		{
			BaseFOV = Settings->FieldOfView;
			FirstPersonCamera->SetFieldOfView(BaseFOV);
			if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
			{
				Movement->SetClimbRelativeToCamera(Settings->bClimbRelativeToCamera);
			}
			Server_SetClimbRelative(Settings->bClimbRelativeToCamera);
		}
	}
	SetupProximityVoice();
}

void AHitmenPlayerCharacter::PossessedBy(AController* NewController)
{
	Super::PossessedBy(NewController);
	if (const AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>())
	{
		CosmeticComponent->SetCosmetics(PS->GetCosmetics());
	}
}

void AHitmenPlayerCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!IsLocallyControlled())
	{
		return;
	}
	float TargetFOV = BaseFOV;
	if (bIsAiming)
	{
		if (const UHitmenWeaponData* Data = InventoryComponent->GetActiveWeaponData())
		{
			TargetFOV = Data->AimFOV;
		}
	}
	FirstPersonCamera->SetFieldOfView(FMath::FInterpTo(FirstPersonCamera->FieldOfView, TargetFOV, DeltaSeconds, AimInterpSpeed));
}

// ---------------------------------------------------------------------------------------------
// Loadout / state
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerCharacter::ApplyLoadout(const FHitmenLoadout& Loadout)
{
	if (!HasAuthority())
	{
		return;
	}
	if (Loadout.Weapon)
	{
		InventoryComponent->GiveWeapon(Loadout.Weapon);
	}
	if (Loadout.MoveAbility)
	{
		AbilityComponent->SetAbility(Loadout.MoveAbility);
	}
	if (Loadout.ManipulationAbility)
	{
		AbilityComponent->SetAbility(Loadout.ManipulationAbility);
	}
}

void AHitmenPlayerCharacter::ApplyCosmetics(const FHitmenCosmeticLoadout& Cosmetics)
{
	CosmeticComponent->SetCosmetics(Cosmetics);
}

float AHitmenPlayerCharacter::GetMovementSpeedMultiplier() const
{
	return Super::GetMovementSpeedMultiplier() * BodyCarryComponent->GetSpeedMultiplier();
}

int32 AHitmenPlayerCharacter::GetHitmenTeamId_Implementation() const
{
	// Charmed players fight for the charmer.
	if (StatusEffectComponent->HasStatus(EHitmenStatusEffect::Charmed))
	{
		return UHitmenTeamLibrary::GetTeamId(StatusEffectComponent->GetStatusInstigator(EHitmenStatusEffect::Charmed));
	}
	if (const AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>())
	{
		return IHitmenTeamMember::Execute_GetHitmenTeamId(PS);
	}
	return HitmenTeams::Neutral;
}

USceneComponent* AHitmenPlayerCharacter::GetWeaponAttachComponent() const
{
	return IsLocallyControlled() && FirstPersonMesh->GetSkeletalMeshAsset() ? FirstPersonMesh.Get() : GetMesh();
}

bool AHitmenPlayerCharacter::CanAct() const
{
	return !IsDead() && !StatusEffectComponent->IsIncapacitated();
}

// ---------------------------------------------------------------------------------------------
// Input
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerCharacter::Input_Move(const FVector2D& Axis)
{
	if (!CanAct() || !StatusEffectComponent->CanMove())
	{
		return;
	}
	AddMovementInput(GetActorForwardVector(), Axis.Y);
	AddMovementInput(GetActorRightVector(), Axis.X);
}

void AHitmenPlayerCharacter::Input_Look(const FVector2D& Axis)
{
	if (IsDead())
	{
		return;
	}
	float Scale = 1.f;
	if (bIsAiming)
	{
		if (const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
		{
			Scale = Settings->AimSensitivityMultiplier;
		}
	}
	AddControllerYawInput(Axis.X * Scale);
	AddControllerPitchInput(-Axis.Y * Scale);
}

void AHitmenPlayerCharacter::Input_JumpPressed()
{
	if (CanAct())
	{
		Jump();
	}
}

void AHitmenPlayerCharacter::Input_JumpReleased()
{
	StopJumping();
}

void AHitmenPlayerCharacter::Input_CrouchChanged(bool bWantsCrouch)
{
	bWantsCrouchInput = bWantsCrouch;
	if (bWantsCrouch)
	{
		Crouch();
	}
	else
	{
		UnCrouch();
	}
}

void AHitmenPlayerCharacter::Input_SprintChanged(bool bWantsSprint)
{
	bWantsSprintInput = bWantsSprint;
	if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		Movement->SetSprinting(bWantsSprint);
	}
}

void AHitmenPlayerCharacter::Input_FirePressed()
{
	if (!CanAct())
	{
		return;
	}
	if (InventoryComponent->GetActiveWeapon())
	{
		InventoryComponent->StartFire();
	}
	else
	{
		MeleeComponent->Punch();
	}
}

void AHitmenPlayerCharacter::Input_FireReleased()
{
	InventoryComponent->StopFire();
}

void AHitmenPlayerCharacter::Input_AimChanged(bool bWantsAim)
{
	const bool bNewAim = bWantsAim && CanAct();
	if (bIsAiming != bNewAim)
	{
		bIsAiming = bNewAim;
		Server_SetAiming(bNewAim);
		Juice_OnAimChanged(bNewAim);
	}
}

void AHitmenPlayerCharacter::Server_SetAiming_Implementation(bool bAim)
{
	bIsAiming = bAim;
}

void AHitmenPlayerCharacter::Server_SetClimbRelative_Implementation(bool bRelative)
{
	if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		Movement->SetClimbRelativeToCamera(bRelative);
	}
}

void AHitmenPlayerCharacter::Input_Interact()
{
	if (!CanAct())
	{
		return;
	}
	// F: stealth kill > interactable > drop body > throw the held item.
	if (!InteractionComponent->TryInteract())
	{
		InventoryComponent->ThrowActive();
	}
}

void AHitmenPlayerCharacter::Input_MoveAbilityPressed()
{
	if (CanAct()) { AbilityComponent->PressSlot(EHitmenAbilitySlot::Move); }
}

void AHitmenPlayerCharacter::Input_MoveAbilityReleased()
{
	AbilityComponent->ReleaseSlot(EHitmenAbilitySlot::Move);
}

void AHitmenPlayerCharacter::Input_ManipulationAbilityPressed()
{
	if (CanAct()) { AbilityComponent->PressSlot(EHitmenAbilitySlot::Manipulation); }
}

void AHitmenPlayerCharacter::Input_ManipulationAbilityReleased()
{
	AbilityComponent->ReleaseSlot(EHitmenAbilitySlot::Manipulation);
}

void AHitmenPlayerCharacter::Input_SwitchSlot(float Direction)
{
	if (CanAct()) { InventoryComponent->CycleSlot(Direction); }
}

void AHitmenPlayerCharacter::Input_SightroomChanged(bool bActive)
{
	SightroomComponent->SetSightroomActive(bActive && !IsDead());
}

void AHitmenPlayerCharacter::Input_Emote(int32 EmoteIndex)
{
	if (CanAct()) { EmoteComponent->PlayEmote(EmoteIndex); }
}

bool AHitmenPlayerCharacter::Input_IsCrouchWanted() const
{
	return bWantsCrouchInput;
}

bool AHitmenPlayerCharacter::Input_IsSprintWanted() const
{
	return bWantsSprintInput;
}

// ---------------------------------------------------------------------------------------------
// Status reactions
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerCharacter::HandleStatusAdded(EHitmenStatusEffect Status, AActor* StatusInstigator)
{
	Super::HandleStatusAdded(Status, StatusInstigator);
	if (Status == EHitmenStatusEffect::Charmed && HasAuthority())
	{
		BeginCharm(StatusInstigator);
	}
	if (Status == EHitmenStatusEffect::Invisible)
	{
		ApplyInvisibilityVisuals(true);
	}
	if (Status == EHitmenStatusEffect::Stunned || Status == EHitmenStatusEffect::Asleep)
	{
		InventoryComponent->StopFire();
	}
}

void AHitmenPlayerCharacter::HandleStatusRemoved(EHitmenStatusEffect Status, AActor* StatusInstigator)
{
	Super::HandleStatusRemoved(Status, StatusInstigator);
	if (Status == EHitmenStatusEffect::Charmed && HasAuthority())
	{
		EndCharm();
	}
	if (Status == EHitmenStatusEffect::Invisible)
	{
		ApplyInvisibilityVisuals(false);
	}
}

void AHitmenPlayerCharacter::ApplyInvisibilityVisuals(bool bInvisible)
{
	// Default look: hidden for everyone but the owner; juice can replace with a shader.
	if (IsLocallyControlled())
	{
		return;
	}
	GetMesh()->SetVisibility(!bInvisible, true);
}

void AHitmenPlayerCharacter::BeginCharm(AActor* Charmer)
{
	if (!CharmedControllerClass || CharmedPreviousController || IsDead())
	{
		return;
	}
	CharmedPreviousController = GetController();
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	AAIController* AI = GetWorld()->SpawnActor<AAIController>(CharmedControllerClass, GetActorTransform(), Params);
	if (!AI)
	{
		CharmedPreviousController = nullptr;
		return;
	}
	if (APlayerController* PC = Cast<APlayerController>(CharmedPreviousController))
	{
		PC->UnPossess();
		PC->SetViewTarget(this);
	}
	AI->Possess(this);
}

void AHitmenPlayerCharacter::EndCharm()
{
	AController* Previous = CharmedPreviousController;
	CharmedPreviousController = nullptr;
	if (!Previous)
	{
		return;
	}
	if (AAIController* AI = Cast<AAIController>(GetController()))
	{
		AI->UnPossess();
		AI->Destroy();
	}
	if (!IsDead())
	{
		Previous->Possess(this);
	}
}

void AHitmenPlayerCharacter::HandleDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot)
{
	if (HasAuthority() && CharmedPreviousController)
	{
		EndCharm();
	}
	SightroomComponent->SetSightroomActive(false);
	InventoryComponent->StopFire();
	Super::HandleDeath(Killer, DamageCauser, bHeadshot);
}

// ---------------------------------------------------------------------------------------------
// Voice
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerCharacter::SetupProximityVoice()
{
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	APlayerState* PS = GetPlayerState();
	if (!GS || !GS->GetLobbySettings().bProximityChat || !PS || IsLocallyControlled() || GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	VoiceTalker = UVOIPTalker::CreateTalkerForPlayer(PS);
	if (VoiceTalker)
	{
		VoiceTalker->Settings.ComponentToAttachTo = GetRootComponent();
		VoiceTalker->Settings.AttenuationSettings = VoiceAttenuation;
	}
}
