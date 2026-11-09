// Hitmen — player character

#include "Characters/HitmenPlayerCharacter.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "UObject/ConstructorHelpers.h"
#include "Engine/StaticMesh.h"
#include "Data/HitmenSoundSet.h"
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
#include "UI/HitmenUIEventSubsystem.h"
#include "Weapons/HitmenWeaponBase.h"
#include "World/Interactables/HitmenTraversal.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
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

	// The eye follows the capsule (smoothed stance height), never an animated bone: crouch and
	// slide lower the view even without crouch animations, and nothing shakes it.
	FirstPersonCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FirstPersonCamera"));
	FirstPersonCamera->SetupAttachment(GetCapsuleComponent());
	FirstPersonCamera->SetRelativeLocation(FVector(0.f, 0.f, StandingViewHeight));
	FirstPersonCamera->bUsePawnControlRotation = true;

	// Arms only (head and legs hidden), attached to the view; shown with a weapon or while acting.
	FirstPersonMesh = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("FirstPersonMesh"));
	FirstPersonMesh->SetupAttachment(FirstPersonCamera);
	FirstPersonMesh->SetRelativeLocationAndRotation(ArmsHipOffset, FRotator(0.f, ArmsYawOffset, 0.f));
	FirstPersonMesh->SetOnlyOwnerSee(true);
	FirstPersonMesh->FirstPersonPrimitiveType = EFirstPersonPrimitiveType::FirstPerson;
	FirstPersonMesh->SetCollisionProfileName(TEXT("NoCollision"));
	FirstPersonMesh->CastShadow = false;
	FirstPersonMesh->VisibilityBasedAnimTickOption = EVisibilityBasedAnimTickOption::AlwaysTickPoseAndRefreshBones;
	FirstPersonMesh->SetVisibility(false, true);
	FirstPersonCamera->bEnableFirstPersonFieldOfView = true;
	FirstPersonCamera->bEnableFirstPersonScale = true;
	FirstPersonCamera->FirstPersonFieldOfView = 70.f;
	FirstPersonCamera->FirstPersonScale = 0.6f;

	SpectatorCapture = CreateDefaultSubobject<USceneCaptureComponent2D>(TEXT("SpectatorCapture"));
	SpectatorCapture->SetupAttachment(FirstPersonCamera);
	SpectatorCapture->bCaptureEveryFrame = false;
	SpectatorCapture->bCaptureOnMovement = false;
	SpectatorCapture->SetAutoActivate(false);

	// The body is invisible to its owner but still casts its shadow for them.
	GetMesh()->SetOwnerNoSee(true);
	GetMesh()->bCastHiddenShadow = true;
	GetMesh()->FirstPersonPrimitiveType = EFirstPersonPrimitiveType::WorldSpaceRepresentation;

	InventoryComponent = CreateDefaultSubobject<UHitmenInventoryComponent>(TEXT("Inventory"));
	AbilityComponent = CreateDefaultSubobject<UHitmenAbilityComponent>(TEXT("Abilities"));
	InteractionComponent = CreateDefaultSubobject<UHitmenInteractionComponent>(TEXT("Interaction"));
	MeleeComponent = CreateDefaultSubobject<UHitmenMeleeComponent>(TEXT("Melee"));
	BodyCarryComponent = CreateDefaultSubobject<UHitmenBodyCarryComponent>(TEXT("BodyCarry"));
	SightroomComponent = CreateDefaultSubobject<UHitmenSightroomComponent>(TEXT("Sightroom"));
	EmoteComponent = CreateDefaultSubobject<UHitmenEmoteComponent>(TEXT("Emote"));

	ParachuteCanopy = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("ParachuteCanopy"));
	ParachuteCanopy->SetupAttachment(GetCapsuleComponent());
	ParachuteCanopy->SetRelativeLocation(FVector(0.f, 0.f, 330.f));
	ParachuteCanopy->SetRelativeScale3D(FVector(4.2f, 3.2f, 0.7f));
	ParachuteCanopy->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	ParachuteCanopy->SetVisibility(false);
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CanopyMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	if (CanopyMesh.Succeeded())
	{
		ParachuteCanopy->SetStaticMesh(CanopyMesh.Object);
	}

	CharmedControllerClass = AHitmenAIController::StaticClass();
}

void AHitmenPlayerCharacter::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME_CONDITION(AHitmenPlayerCharacter, bIsAiming, COND_SkipOwner);
	DOREPLIFETIME(AHitmenPlayerCharacter, bParachuting);
}

void AHitmenPlayerCharacter::BeginPlay()
{
	Super::BeginPlay();
	CurrentEyeHeight = StandingViewHeight;
	FirstPersonCamera->SetRelativeLocation(FVector(0.f, 0.f, CurrentEyeHeight));
	CurrentArmsOffset = ArmsHipOffset;
	for (const FName& Bone : FirstPersonHiddenBones)
	{
		FirstPersonMesh->HideBoneByName(Bone, EPhysBodyOp::PBO_None);
	}
	if (IsLocallyControlled())
	{
		BindLocalSettings();
	}
	SetupProximityVoice();
}

void AHitmenPlayerCharacter::NotifyControllerChanged()
{
	Super::NotifyControllerChanged();
	if (IsLocallyControlled())
	{
		BindLocalSettings();
	}
}

void AHitmenPlayerCharacter::BindLocalSettings()
{
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings(); Settings && !bLocalSettingsBound)
	{
		Settings->OnHitmenSettingsChanged.AddUniqueDynamic(this, &AHitmenPlayerCharacter::ApplyUserSettings);
		bLocalSettingsBound = true;
	}
	ApplyUserSettings();
}

void AHitmenPlayerCharacter::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->OnHitmenSettingsChanged.RemoveAll(this);
	}
	Super::EndPlay(EndPlayReason);
}

void AHitmenPlayerCharacter::GetMontageMeshes(TArray<USkeletalMeshComponent*>& OutMeshes) const
{
	Super::GetMontageMeshes(OutMeshes);
	if (IsLocallyControlled() && FirstPersonMesh)
	{
		OutMeshes.AddUnique(FirstPersonMesh);
	}
}

void AHitmenPlayerCharacter::ApplyUserSettings()
{
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	if (!Settings || !IsLocallyControlled())
	{
		return;
	}
	BaseFOV = Settings->FieldOfView;
	if (!bIsAiming)
	{
		FirstPersonCamera->SetFieldOfView(BaseFOV);
	}
	if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		Movement->SetClimbRelativeToCamera(Settings->bClimbRelativeToCamera);
	}
	Server_SetClimbRelative(Settings->bClimbRelativeToCamera);
}

void AHitmenPlayerCharacter::PossessedBy(AController* NewController)
{
	Super::PossessedBy(NewController);
	if (const AHitmenPlayerState* PS = GetPlayerState<AHitmenPlayerState>())
	{
		CosmeticComponent->SetCosmetics(PS->GetCosmetics());
	}
}

void AHitmenPlayerCharacter::StartParachute()
{
	if (!HasAuthority())
	{
		return;
	}
	bParachuting = true;
	OnRep_Parachuting();
}

void AHitmenPlayerCharacter::OnRep_Parachuting()
{
	ParachuteCanopy->SetVisibility(bParachuting);
	if (UCharacterMovementComponent* Movement = GetCharacterMovement())
	{
		Movement->AirControl = bParachuting ? 0.f : 0.35f;
		if (bParachuting)
		{
			Movement->SetMovementMode(MOVE_Falling);
		}
	}
	if (bParachuting)
	{
		UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::Parachute, GetActorLocation());
	}
	Juice_OnParachute(bParachuting);
}

void AHitmenPlayerCharacter::Landed(const FHitResult& Hit)
{
	Super::Landed(Hit);
	if (bParachuting)
	{
		if (HasAuthority())
		{
			bParachuting = false;
			OnRep_Parachuting();
			PlayAnimSetMontage(EHitmenAnimMontage::ParachuteLand);
		}
		UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::Landing, GetActorLocation());
		if (IsLocallyControlled())
		{
			AddCameraShake(1.2f, 0.35f);
		}
	}
}

void AHitmenPlayerCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (bParachuting)
	{
		// Slow, steady descent with a little drift; the canopy sways.
		if (UCharacterMovementComponent* Movement = GetCharacterMovement(); Movement && (HasAuthority() || IsLocallyControlled()))
		{
			const float Time = GetWorld()->GetTimeSeconds();
			Movement->Velocity.Z = FMath::Max(Movement->Velocity.Z, -ParachuteFallSpeed);
			Movement->Velocity.X = FMath::Sin(Time * 0.7f) * ParachuteDrift;
			Movement->Velocity.Y = FMath::Cos(Time * 0.5f) * ParachuteDrift;
		}
		ParachuteCanopy->SetRelativeRotation(FRotator(FMath::Sin(GetWorld()->GetTimeSeconds() * 1.3f) * 6.f, 0.f, FMath::Cos(GetWorld()->GetTimeSeconds()) * 5.f));
	}
	if (bDeathCameraActive)
	{
		UpdateDeathCamera(DeltaSeconds);
		return;
	}
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
	UpdateEyeHeight(DeltaSeconds);
	UpdateRecoil(DeltaSeconds);
	UpdateFirstPersonArms(DeltaSeconds);
	UpdateHUDHints(DeltaSeconds);
}

void AHitmenPlayerCharacter::UpdateHUDHints(float DeltaSeconds)
{
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(this);
	if (!UI)
	{
		return;
	}
	const UHitmenWeaponData* Data = InventoryComponent->GetActiveWeaponData();
	UI->SetScoped(bIsAiming && Data && Data->bScopeOverlay && FirstPersonCamera->FieldOfView < BaseFOV - 5.f);

	HintAccumulator += DeltaSeconds;
	if (HintAccumulator < 0.1f)
	{
		return;
	}
	HintAccumulator = 0.f;
	FText Hint;
	if (const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement(); Movement && CanAct() && StatusEffectComponent->CanMove())
	{
		switch (Movement->QueryTraversalAction())
		{
		case EHitmenTraversalAction::Vault:     Hint = NSLOCTEXT("Hitmen", "HintVault", "Vault"); break;
		case EHitmenTraversalAction::Climb:     Hint = NSLOCTEXT("Hitmen", "HintClimb", "Climb"); break;
		case EHitmenTraversalAction::ClimbOver: Hint = NSLOCTEXT("Hitmen", "HintClimbOver", "Climb over"); break;
		default: break;
		}
	}
	UI->SetTraversalHint(Hint);
}

// ---------------------------------------------------------------------------------------------
// First-person view: eye height, recoil, arms, death camera (local only)
// ---------------------------------------------------------------------------------------------

float AHitmenPlayerCharacter::GetTargetEyeHeight() const
{
	if (const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		if (Movement->IsSliding())
		{
			return SlideViewHeight;
		}
		if (Movement->IsMantling())
		{
			// Dip in the middle of the pull-up, back up when landing.
			const float Alpha = Movement->GetMantleAlpha();
			return FMath::Lerp(StandingViewHeight, CrouchedViewHeight, FMath::Sin(Alpha * PI) * 0.8f);
		}
	}
	return bIsCrouched ? CrouchedViewHeight : StandingViewHeight;
}

void AHitmenPlayerCharacter::OnStartCrouch(float HalfHeightAdjust, float ScaledHalfHeightAdjust)
{
	Super::OnStartCrouch(HalfHeightAdjust, ScaledHalfHeightAdjust);
	// The capsule centre just dropped: keep the eye where it was, then ease it down.
	CurrentEyeHeight += ScaledHalfHeightAdjust;
	FirstPersonCamera->SetRelativeLocation(FVector(0.f, 0.f, CurrentEyeHeight));
}

void AHitmenPlayerCharacter::OnEndCrouch(float HalfHeightAdjust, float ScaledHalfHeightAdjust)
{
	Super::OnEndCrouch(HalfHeightAdjust, ScaledHalfHeightAdjust);
	CurrentEyeHeight -= ScaledHalfHeightAdjust;
	FirstPersonCamera->SetRelativeLocation(FVector(0.f, 0.f, CurrentEyeHeight));
}

void AHitmenPlayerCharacter::UpdateEyeHeight(float DeltaSeconds)
{
	CurrentEyeHeight = FMath::FInterpTo(CurrentEyeHeight, GetTargetEyeHeight(), DeltaSeconds, EyeHeightInterpSpeed);
	FirstPersonCamera->SetRelativeLocation(FVector(0.f, 0.f, CurrentEyeHeight));
}

void AHitmenPlayerCharacter::AddRecoil(float PitchDegrees, float YawDegrees)
{
	const FVector2D Kick(PitchDegrees, YawDegrees);
	RecoilTarget += Kick;
	RecoilSettled += Kick * (1.f - RecoilRecoveryRatio);
}

void AHitmenPlayerCharacter::AddCameraShake(float Intensity, float Duration)
{
	if (Intensity >= ShakeIntensity * (ShakeDuration > 0.f ? ShakeTimeLeft / ShakeDuration : 0.f))
	{
		ShakeIntensity = Intensity;
		ShakeDuration = FMath::Max(Duration, 0.05f);
		ShakeTimeLeft = ShakeDuration;
	}
}

void AHitmenPlayerCharacter::UpdateRecoil(float DeltaSeconds)
{
	// Shake: random kicks through the recoil spring, so the view always settles back.
	if (ShakeTimeLeft > 0.f)
	{
		ShakeTimeLeft = FMath::Max(0.f, ShakeTimeLeft - DeltaSeconds);
		const float Strength = ShakeIntensity * (ShakeTimeLeft / ShakeDuration) * DeltaSeconds * 30.f;
		AddRecoil(FMath::FRandRange(-Strength, Strength), FMath::FRandRange(-Strength, Strength));
		RecoilSettled = FVector2D::ZeroVector;
	}
	AController* PC = GetController();
	if (!PC || (RecoilTarget.IsNearlyZero(0.001f) && RecoilCurrent.IsNearlyZero(0.001f)))
	{
		return;
	}
	const FVector2D Previous = RecoilCurrent;
	RecoilCurrent.X = FMath::FInterpTo(RecoilCurrent.X, RecoilTarget.X, DeltaSeconds, RecoilKickInterpSpeed);
	RecoilCurrent.Y = FMath::FInterpTo(RecoilCurrent.Y, RecoilTarget.Y, DeltaSeconds, RecoilKickInterpSpeed);
	RecoilTarget.X = FMath::FInterpTo(RecoilTarget.X, RecoilSettled.X, DeltaSeconds, RecoilRecoveryInterpSpeed);
	RecoilTarget.Y = FMath::FInterpTo(RecoilTarget.Y, RecoilSettled.Y, DeltaSeconds, RecoilRecoveryInterpSpeed);

	// Only the change is applied, so the player's own mouse movement is never overridden.
	const FVector2D Delta = RecoilCurrent - Previous;
	FRotator Control = PC->GetControlRotation();
	Control.Pitch = FRotator::NormalizeAxis(Control.Pitch + Delta.X);
	Control.Yaw += Delta.Y;
	PC->SetControlRotation(Control);

	// Settled: forget the history (the view stays where it is).
	if ((RecoilTarget - RecoilSettled).IsNearlyZero(0.01f) && (RecoilCurrent - RecoilTarget).IsNearlyZero(0.01f))
	{
		RecoilTarget = RecoilCurrent = RecoilSettled = FVector2D::ZeroVector;
	}
}

void AHitmenPlayerCharacter::ShowFirstPersonArms(float Duration)
{
	ArmsVisibleUntil = FMath::Max(ArmsVisibleUntil, GetWorld()->GetTimeSeconds() + Duration);
}

void AHitmenPlayerCharacter::UpdateFirstPersonArms(float DeltaSeconds)
{
	const UHitmenWeaponData* Data = InventoryComponent->GetActiveWeaponData();
	const bool bScoped = bIsAiming && Data && Data->bScopeOverlay && FirstPersonCamera->FieldOfView < BaseFOV - 1.f;
	const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement();
	const bool bBusy = Movement && Movement->IsClimbing();
	const bool bWantArms = !IsDead() && !bScoped && !bBusy
		&& (InventoryComponent->GetActiveWeapon() != nullptr || GetWorld()->GetTimeSeconds() < ArmsVisibleUntil);
	if (bWantArms != bArmsVisible || !bArmsInitialised)
	{
		bArmsVisible = bWantArms;
		bArmsInitialised = true;
		FirstPersonMesh->SetVisibility(bArmsVisible, true);
	}
	if (!bArmsVisible)
	{
		LastControlRotation = GetControlRotation();
		return;
	}

	// Hip / aim offset.
	CurrentArmsOffset = FMath::VInterpTo(CurrentArmsOffset, bIsAiming ? ArmsAimOffset : ArmsHipOffset, DeltaSeconds, ArmsOffsetInterpSpeed);

	// Sway: the arms lag behind fast view turns, then catch up.
	const FRotator Control = GetControlRotation();
	const FRotator Turn = (Control - LastControlRotation).GetNormalized();
	LastControlRotation = Control;
	const float SafeDelta = FMath::Max(DeltaSeconds, KINDA_SMALL_NUMBER);
	const float Scale = bIsAiming ? 0.35f : 1.f;
	const FRotator SwayTarget(
		FMath::Clamp(-Turn.Pitch / SafeDelta * ArmsSwayAmount * Scale, -ArmsSwayMax, ArmsSwayMax),
		FMath::Clamp(-Turn.Yaw / SafeDelta * ArmsSwayAmount * Scale, -ArmsSwayMax, ArmsSwayMax), 0.f);
	ArmsSway = FMath::RInterpTo(ArmsSway, SwayTarget, DeltaSeconds, 10.f);

	// Bob with ground speed.
	const float Speed = GetVelocity().Size2D();
	const float WalkSpeed = Movement ? FMath::Max(1.f, Movement->WalkSpeed) : 480.f;
	const bool bGrounded = Movement && Movement->IsMovingOnGround();
	const float BobAlpha = bGrounded ? FMath::Clamp(Speed / WalkSpeed, 0.f, 1.6f) * (bIsAiming ? 0.25f : 1.f) : 0.f;
	if (BobAlpha > 0.05f)
	{
		ArmsBobPhase += DeltaSeconds * (6.f + 4.f * BobAlpha);
	}
	const FVector Bob(0.f, FMath::Cos(ArmsBobPhase) * ArmsBobAmount * BobAlpha, -FMath::Abs(FMath::Sin(ArmsBobPhase)) * ArmsBobAmount * BobAlpha);

	// Offsets are in camera space (X forward, Y right, Z up); the mesh is turned by ArmsYawOffset.
	FirstPersonMesh->SetRelativeLocationAndRotation(CurrentArmsOffset + Bob,
		FRotator(ArmsSway.Pitch, ArmsYawOffset + ArmsSway.Yaw, 0.f));
}

void AHitmenPlayerCharacter::UpdateDeathCamera(float DeltaSeconds)
{
	const FVector Focus = GetMesh()->GetBoneLocation(TEXT("pelvis"));
	DeathCameraYaw += DeathCameraOrbitSpeed * DeltaSeconds;
	const FVector Offset = FRotator(0.f, DeathCameraYaw, 0.f).Vector() * -DeathCameraDistance + FVector(0.f, 0.f, DeathCameraHeight);
	FVector Desired = Focus + Offset;

	// Never through walls: pull in toward the body.
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenDeathCamera), false, this);
	if (GetWorld()->SweepSingleByChannel(Hit, Focus + FVector(0.f, 0.f, 40.f), Desired, FQuat::Identity, ECC_Camera, FCollisionShape::MakeSphere(12.f), Params))
	{
		Desired = Hit.Location;
	}
	const FVector Location = FMath::VInterpTo(FirstPersonCamera->GetComponentLocation(), Desired, DeltaSeconds, DeathCameraInterpSpeed);
	const FRotator LookAt = (Focus - Location).Rotation();
	FirstPersonCamera->SetWorldLocationAndRotation(Location, FMath::RInterpTo(FirstPersonCamera->GetComponentRotation(), LookAt, DeltaSeconds, DeathCameraInterpSpeed * 2.f));
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
	if (!CanAct() || !StatusEffectComponent->CanMove() || bParachuting)
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
	// Zooming keeps the same feel: sensitivity follows the field of view (tangent ratio).
	float Scale = 1.f;
	const float CurrentFOV = FirstPersonCamera->FieldOfView;
	if (CurrentFOV < BaseFOV - 0.5f)
	{
		Scale = FMath::Tan(FMath::DegreesToRadians(CurrentFOV * 0.5f)) / FMath::Tan(FMath::DegreesToRadians(BaseFOV * 0.5f));
	}
	if (bIsAiming)
	{
		if (const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
		{
			Scale *= Settings->AimSensitivityMultiplier;
		}
	}
	AddControllerYawInput(Axis.X * Scale);
	AddControllerPitchInput(-Axis.Y * Scale);
}

void AHitmenPlayerCharacter::Input_JumpPressed()
{
	if (CanAct() && !bParachuting)
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
	// Zipline: the ride starts here at once (client prediction); the server starts the same ride.
	if (AHitmenZipline* Zipline = Cast<AHitmenZipline>(InteractionComponent->GetFocusedActor()))
	{
		if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement(); Movement && !HasAuthority())
		{
			const float RideDirection = Zipline->GetRideDirectionFrom(GetActorLocation());
			if (!FMath::IsNearlyZero(RideDirection))
			{
				Movement->StartZiplineRide(Zipline, RideDirection);
			}
		}
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

	// The local view leaves the head and watches the body fall.
	if (IsLocallyControlled() && !bDeathCameraActive)
	{
		bDeathCameraActive = true;
		DeathCameraYaw = GetControlRotation().Yaw;
		FirstPersonMesh->SetVisibility(false, true);
		GetMesh()->SetOwnerNoSee(false);
		FirstPersonCamera->bUsePawnControlRotation = false;
		FirstPersonCamera->DetachFromComponent(FDetachmentTransformRules::KeepWorldTransform);
		SetActorTickEnabled(true);
		Juice_OnDeathCamera();
	}
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
