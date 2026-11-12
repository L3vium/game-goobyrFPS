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
#include "Components/HitmenFootstepComponent.h"
#include "Components/HitmenCosmeticComponent.h"
#include "Components/HitmenMeleeComponent.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "Components/HitmenSightroomComponent.h"
#include "Components/HitmenEmoteComponent.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Abilities/HitmenAbilityComponent.h"
#include "Abilities/Move/HitmenMoveAbilities.h"
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
	// Players last much longer than guards (default of the Health component's MaxHealth on the
	// player; BP_PlayerCharacter can still change it).
	static constexpr float DefaultPlayerMaxHealth = 150.f;
	if (UHitmenHealthComponent* PlayerHealth = GetHealth())
	{
		PlayerHealth->SetDefaultMaxHealth(DefaultPlayerMaxHealth);
		// Out of a fight for a while, players heal back (Hitman-like).
		PlayerHealth->SetRegenerates(true);
	}

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
	FirstPersonCamera->FirstPersonScale = FirstPersonViewScale;

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
	FirstPersonCamera->FirstPersonScale = FirstPersonViewScale;
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
	ReleaseProximityVoice();
	Super::EndPlay(EndPlayReason);
}

void AHitmenPlayerCharacter::GetMontageMeshes(TArray<USkeletalMeshComponent*>& OutMeshes) const
{
	// Body montages (fire, equip, punch, takedown, throw) play on the body only: the first-person arms move
	// procedurally (UpdateFirstPersonArms), so you never see a third-person motion (the body leaning into a punch).
	Super::GetMontageMeshes(OutMeshes);
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
		// Steering is done in Tick (ParachuteSteerSpeed); plain air control would fight it.
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

bool AHitmenPlayerCharacter::IsIntroProtected() const
{
	return bParachuting || (GetWorld() && GetWorld()->GetTimeSeconds() - ParachuteLandTime < IntroSightGraceTime);
}

void AHitmenPlayerCharacter::Landed(const FHitResult& Hit)
{
	const float FallSpeed = -GetVelocity().Z;
	Super::Landed(Hit);
	if (!bParachuting && FallSpeed >= LandingSoundSpeed)
	{
		// Every landing is heard (soft or hard); a hard one dips the view.
		const bool bHard = FallSpeed >= HardLandingSpeed;
		UHitmenSoundLibrary::PlayHitmenSoundAt(this, bHard ? EHitmenSound::LandingHard : EHitmenSound::Landing, GetActorLocation());
		if (bHard && IsLocallyControlled())
		{
			AddCameraShake(FMath::GetMappedRangeValueClamped(FVector2D(HardLandingSpeed, HardLandingSpeed * 2.f), FVector2D(0.8f, 2.f), FallSpeed), 0.3f);
		}
	}
	if (bParachuting)
	{
		ParachuteLandTime = GetWorld()->GetTimeSeconds();
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
	if (IsLocallyControlled())
	{
		UpdateJetpackFromSpace();
	}
	if (bParachuting)
	{
		// Slow, steady descent steered with the move keys (a little drift when no key is held).
		if (UCharacterMovementComponent* Movement = GetCharacterMovement(); Movement && (HasAuthority() || IsLocallyControlled()))
		{
			const float Time = GetWorld()->GetTimeSeconds();
			Movement->Velocity.Z = FMath::Max(Movement->Velocity.Z, -ParachuteFallSpeed);
			const FVector Steer = Movement->GetCurrentAcceleration().GetSafeNormal2D();
			const FVector Drift(FMath::Sin(Time * 0.7f) * ParachuteDrift, FMath::Cos(Time * 0.5f) * ParachuteDrift, 0.f);
			const FVector Target = Steer.IsNearlyZero() ? Drift : Steer * ParachuteSteerSpeed;
			const FVector Horizontal = FMath::VInterpTo(FVector(Movement->Velocity.X, Movement->Velocity.Y, 0.f), Target, DeltaSeconds, ParachuteSteerResponse);
			Movement->Velocity.X = Horizontal.X;
			Movement->Velocity.Y = Horizontal.Y;
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
	if (FootstepComponent)
	{
		// Your own steps fall on the head bob (UpdateHeadBob); everyone else's come from their animation.
		FootstepComponent->SetExternalStepTiming(true);
	}
	AimHeldTime = bIsAiming ? AimHeldTime + DeltaSeconds : 0.f;
	float TargetFOV = BaseFOV;
	const UHitmenWeaponData* AimData = InventoryComponent->GetActiveWeaponData();
	if (bIsAiming && AimData)
	{
		// Scoped weapons first come up to the eye and steady themselves, then the zoom comes in.
		const float ScopeDelay = AimData->bScopeOverlay ? AimData->FPMotion.ScopeRaiseTime + AimData->FPMotion.ScopeWobbleTime : 0.f;
		if (AimHeldTime >= ScopeDelay)
		{
			TargetFOV = AimData->AimFOV;
		}
	}
	FirstPersonCamera->SetFieldOfView(FMath::FInterpTo(FirstPersonCamera->FieldOfView, TargetFOV, DeltaSeconds, AimInterpSpeed));
	// Zoomed in, the aim breathes a little (a real offset of the view, so the shot follows it).
	FVector2D ScopeSway = FVector2D::ZeroVector;
	if (bIsAiming && AimData && AimData->bScopeOverlay && FirstPersonCamera->FieldOfView < BaseFOV - 5.f)
	{
		const float Time = GetWorld()->GetTimeSeconds();
		const float Amount = AimData->FPMotion.ScopeSway;
		ScopeSway = FVector2D(FMath::Sin(Time * 0.9f) * Amount, FMath::Sin(Time * 1.3f + 1.f) * Amount * 0.7f);
	}
	if (AController* ViewController = GetController(); ViewController && !ScopeSway.Equals(ScopeSwayApplied, 0.0001f))
	{
		const FVector2D Delta = ScopeSway - ScopeSwayApplied;
		ViewController->SetControlRotation(ViewController->GetControlRotation() + FRotator(Delta.X, Delta.Y, 0.f));
		ScopeSwayApplied = ScopeSway;
	}
	UpdateHeadBob(DeltaSeconds);
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
		case EHitmenTraversalAction::Hang:      Hint = NSLOCTEXT("Hitmen", "HintHang", "Climb out"); break;
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
	FirstPersonCamera->SetRelativeLocation(FVector(0.f, HeadBobOffset.Y, CurrentEyeHeight + HeadBobOffset.Z));
}

void AHitmenPlayerCharacter::UpdateHeadBob(float DeltaSeconds)
{
	const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement();
	const bool bGrounded = Movement && Movement->IsMovingOnGround() && !Movement->IsSliding();
	const float Speed = GetVelocity().Size2D();
	const bool bSprint = Movement && Movement->IsSprinting();
	const bool bCrouch = bIsCrouched;
	const bool bWalking = bGrounded && Speed > 40.f;
	HeadBobAlpha = FMath::FInterpTo(HeadBobAlpha, bWalking ? 1.f : 0.f, DeltaSeconds, 8.f);
	if (bWalking)
	{
		// One step per half turn of the phase: the view is lowest (the foot lands) at every multiple of PI.
		const float StepLength = bCrouch ? CrouchStepLength : (bSprint ? SprintStepLength : WalkStepLength);
		HeadBobPhase += Speed * DeltaSeconds / StepLength * PI;
		const int32 Step = FMath::FloorToInt(HeadBobPhase / PI);
		if (Step != HeadBobStep)
		{
			HeadBobStep = Step;
			if (FootstepComponent)
			{
				FootstepComponent->HandleTimedFootstep();
			}
		}
	}
	else
	{
		// Standing still: settle on the next low point so the first step starts there.
		const float Rest = FMath::CeilToFloat(HeadBobPhase / PI) * PI;
		HeadBobPhase = FMath::FInterpTo(HeadBobPhase, Rest, DeltaSeconds, 6.f);
		HeadBobStep = FMath::FloorToInt(HeadBobPhase / PI);
	}
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	const float UserScale = Settings ? FMath::Clamp(Settings->HeadBobScale, 0.f, 2.f) : 1.f;
	const float GaitScale = bSprint ? HeadBobSprintScale : (bCrouch ? HeadBobCrouchScale : 1.f);
	const float Scale = UserScale * GaitScale * HeadBobAlpha * (bIsAiming ? 0.3f : 1.f);
	HeadBobOffset = FVector(0.f,
		FMath::Sin(HeadBobPhase * 0.5f) * HeadBobSide * Scale,
		(FMath::Abs(FMath::Sin(HeadBobPhase)) - 0.5f) * HeadBobHeight * Scale);
}

void AHitmenPlayerCharacter::PlayFirstPersonAction(EHitmenFPAction Action, float Duration)
{
	FPAction = Action;
	FPActionStart = GetWorld()->GetTimeSeconds();
	FPActionDuration = FMath::Max(0.05f, Duration);
	if (Action == EHitmenFPAction::Punch)
	{
		// Left, right, left...
		FPActionSide = -FPActionSide;
	}
	ShowFirstPersonArms(FPActionDuration + 0.1f);
}

void AHitmenPlayerCharacter::OnFirstPersonFired(const UHitmenWeaponData* Weapon)
{
	if (!Weapon || !IsLocallyControlled())
	{
		return;
	}
	if (!Weapon->bUsesAmmo && Weapon->MeleeRange > 0.f)
	{
		PlayFirstPersonAction(EHitmenFPAction::KnifeSlash, FMath::Clamp(Weapon->GetFireInterval() * 0.9f, 0.3f, 0.55f));
		return;
	}
	// A spring kick per shot: an initial speed of Amount * sqrt(K) peaks at about Amount.
	const FHitmenFPMotion& Motion = Weapon->FPMotion;
	KickStiffness = Motion.KickStiffness;
	const float Omega = FMath::Sqrt(KickStiffness);
	const float AimScale = bIsAiming ? 0.6f : 1.f;
	KickOffsetVelocity += FVector(-Motion.KickBack, 0.f, Motion.KickBack * 0.15f) * Omega * AimScale;
	KickRotationVelocity += FVector(Motion.KickUp, FMath::FRandRange(-Motion.KickSide, Motion.KickSide), FMath::FRandRange(-Motion.KickSide, Motion.KickSide) * 1.5f) * Omega * AimScale;
}

void AHitmenPlayerCharacter::HandleWeaponThrown(UHitmenWeaponData* Weapon)
{
	Super::HandleWeaponThrown(Weapon);
	if (IsLocallyControlled())
	{
		PlayFirstPersonAction(EHitmenFPAction::Throw, 0.5f);
	}
}

void AHitmenPlayerCharacter::EvaluateFirstPersonAction(FVector& OutOffset, FRotator& OutRotation) const
{
	OutOffset = FVector::ZeroVector;
	OutRotation = FRotator::ZeroRotator;
	const float Elapsed = GetWorld()->GetTimeSeconds() - FPActionStart;
	if (FPAction == EHitmenFPAction::None || Elapsed < 0.f || Elapsed > FPActionDuration)
	{
		return;
	}
	struct FKey { float T; FVector Offset; FRotator Rotation; };
	// Keys in camera space (X forward, Y right, Z up; pitch up, yaw right, roll right), eased between keys.
	const float S = FPActionSide;
	TArray<FKey> Keys;
	switch (FPAction)
	{
	case EHitmenFPAction::Punch:
		Keys = { { 0.f, FVector::ZeroVector, FRotator::ZeroRotator },
				 { 0.3f, FVector(26.f, -7.f * S, 5.f), FRotator(4.f, -10.f * S, 14.f * S) },
				 { 1.f, FVector::ZeroVector, FRotator::ZeroRotator } };
		break;
	case EHitmenFPAction::KnifeSlash:
		Keys = { { 0.f, FVector::ZeroVector, FRotator::ZeroRotator },
				 { 0.25f, FVector(-6.f, 7.f, 5.f), FRotator(10.f, 30.f, 25.f) },
				 { 0.55f, FVector(18.f, -12.f, -4.f), FRotator(-12.f, -40.f, -20.f) },
				 { 1.f, FVector::ZeroVector, FRotator::ZeroRotator } };
		break;
	case EHitmenFPAction::Throw:
		Keys = { { 0.f, FVector::ZeroVector, FRotator::ZeroRotator },
				 { 0.4f, FVector(-10.f, -2.f, 14.f), FRotator(35.f, 5.f, -15.f) },
				 { 0.6f, FVector(28.f, -6.f, -10.f), FRotator(-30.f, -8.f, 10.f) },
				 { 1.f, FVector::ZeroVector, FRotator::ZeroRotator } };
		break;
	case EHitmenFPAction::Takedown:
		Keys = { { 0.f, FVector::ZeroVector, FRotator::ZeroRotator },
				 { 0.25f, FVector(24.f, -4.f, -14.f), FRotator(-22.f, -6.f, 10.f) },
				 { 0.7f, FVector(20.f, -3.f, -12.f), FRotator(-18.f, -4.f, 8.f) },
				 { 1.f, FVector::ZeroVector, FRotator::ZeroRotator } };
		break;
	default:
		return;
	}
	const float T = FMath::Clamp(Elapsed / FPActionDuration, 0.f, 1.f);
	for (int32 Index = 1; Index < Keys.Num(); ++Index)
	{
		if (T <= Keys[Index].T)
		{
			const FKey& A = Keys[Index - 1];
			const FKey& B = Keys[Index];
			const float Alpha = FMath::SmoothStep(0.f, 1.f, (T - A.T) / FMath::Max(B.T - A.T, KINDA_SMALL_NUMBER));
			OutOffset = FMath::Lerp(A.Offset, B.Offset, Alpha);
			OutRotation = FRotator(FMath::Lerp(A.Rotation.Pitch, B.Rotation.Pitch, Alpha), FMath::Lerp(A.Rotation.Yaw, B.Rotation.Yaw, Alpha),
				FMath::Lerp(A.Rotation.Roll, B.Rotation.Roll, Alpha));
			return;
		}
	}
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
	AHitmenWeaponBase* Weapon = InventoryComponent->GetActiveWeapon();
	const float Now = GetWorld()->GetTimeSeconds();
	const bool bScoped = bIsAiming && Data && Data->bScopeOverlay && FirstPersonCamera->FieldOfView < BaseFOV - 1.f;
	const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement();
	const bool bBusy = Movement && (Movement->IsClimbing() || Movement->IsHanging());
	const bool bActing = FPAction != EHitmenFPAction::None && Now < FPActionStart + FPActionDuration;
	const bool bWantArms = !IsDead() && !bScoped && !bBusy && (Weapon != nullptr || Now < ArmsVisibleUntil || bActing);
	if (bWantArms != bArmsVisible || !bArmsInitialised)
	{
		bArmsVisible = bWantArms;
		bArmsInitialised = true;
		FirstPersonMesh->SetVisibility(bArmsVisible, true);
	}
	if (!bArmsVisible)
	{
		LastControlRotation = GetControlRotation();
		LastArmsWeapon = Weapon;
		return;
	}
	const FHitmenFPMotion Motion = Data ? Data->FPMotion : FHitmenFPMotion();
	const float SafeDelta = FMath::Max(DeltaSeconds, KINDA_SMALL_NUMBER);

	// Equip: every weapon that comes into hand rises from below the view (fast, eased out).
	if (Weapon != LastArmsWeapon.Get())
	{
		LastArmsWeapon = Weapon;
		EquipStartTime = Weapon ? Now : -100.f;
	}
	const float EquipAlpha = FMath::Clamp((Now - EquipStartTime) / FMath::Max(Motion.EquipTime, 0.01f), 0.f, 1.f);
	const float EquipLeft = FMath::Pow(1.f - EquipAlpha, 3.f);
	const FVector EquipOffset = Motion.EquipFrom * EquipLeft;
	const FRotator EquipRotation = Motion.EquipFromRotation * EquipLeft;

	// Hip <-> aim: a spring toward the target (the gun travels to the centre, overshoots a hair and settles).
	const FVector TargetArmsOffset = !Weapon ? ArmsUnarmedOffset : (bIsAiming ? ArmsAimOffset + Motion.AimOffset : ArmsHipOffset + Motion.HipOffset);
	const int32 Steps = FMath::Clamp(FMath::CeilToInt(SafeDelta / 0.005f), 1, 40);
	const float H = SafeDelta / Steps;
	if (!Weapon)
	{
		CurrentArmsOffset = TargetArmsOffset;
		ArmsOffsetVelocity = FVector::ZeroVector;
	}
	const float AimDampingC = 2.f * FMath::Sqrt(ArmsAimStiffness) * ArmsAimDamping;
	const float KickDampingC = 2.f * FMath::Sqrt(KickStiffness) * 0.6f;
	for (int32 Step = 0; Step < Steps; ++Step)
	{
		if (Weapon)
		{
			ArmsOffsetVelocity += (ArmsAimStiffness * (TargetArmsOffset - CurrentArmsOffset) - AimDampingC * ArmsOffsetVelocity) * H;
			CurrentArmsOffset += ArmsOffsetVelocity * H;
		}
		// Fire kick springs back to rest.
		KickOffsetVelocity += (-KickStiffness * KickOffset - KickDampingC * KickOffsetVelocity) * H;
		KickOffset += KickOffsetVelocity * H;
		KickRotationVelocity += (-KickStiffness * KickRotation - KickDampingC * KickRotationVelocity) * H;
		KickRotation += KickRotationVelocity * H;
	}

	// Sway: the arms lag behind fast view turns, then catch up (and lean into the turn).
	const FRotator Control = GetControlRotation();
	const FRotator Turn = (Control - LastControlRotation).GetNormalized();
	LastControlRotation = Control;
	const float SwayScale = bIsAiming ? 0.35f : 1.f;
	const float SwayPitch = FMath::Clamp(-Turn.Pitch / SafeDelta * ArmsSwayAmount * SwayScale, -ArmsSwayMax, ArmsSwayMax);
	const float SwayYaw = FMath::Clamp(-Turn.Yaw / SafeDelta * ArmsSwayAmount * SwayScale, -ArmsSwayMax, ArmsSwayMax);
	ArmsSway = FMath::RInterpTo(ArmsSway, FRotator(SwayPitch, SwayYaw, SwayYaw * 0.6f), DeltaSeconds, 10.f);

	// Moving: walking forward / back pushes the arms a little, strafing tilts them.
	const float WalkSpeed = Movement ? FMath::Max(1.f, Movement->WalkSpeed) : 480.f;
	const FVector LocalVelocity = FRotator(0.f, Control.Yaw, 0.f).UnrotateVector(GetVelocity());
	const FVector2D MoveTarget(-LocalVelocity.X / WalkSpeed * ArmsMoveLag, LocalVelocity.Y / WalkSpeed * ArmsStrafeTilt);
	ArmsMoveState = FMath::Vector2DInterpTo(ArmsMoveState, MoveTarget * (bIsAiming ? 0.3f : 1.f), DeltaSeconds, 8.f);

	// Bob on the same phase as the head bob (one dip per step).
	const float Speed = GetVelocity().Size2D();
	const bool bGrounded = Movement && Movement->IsMovingOnGround();
	const float BobAlpha = HeadBobAlpha * FMath::Clamp(Speed / WalkSpeed, 0.f, 1.6f) * (bIsAiming ? 0.25f : 1.f);
	const FVector Bob(0.f, FMath::Sin(HeadBobPhase * 0.5f) * ArmsBobAmount * BobAlpha, -FMath::Abs(FMath::Sin(HeadBobPhase)) * ArmsBobAmount * BobAlpha);

	// Sprinting with a weapon: lowered and tilted, still in view.
	const bool bSprinting = Movement && Movement->IsSprinting() && bGrounded && Speed > WalkSpeed * 1.05f && !bIsAiming && Weapon;
	ArmsSprintAlpha = FMath::FInterpTo(ArmsSprintAlpha, bSprinting ? 1.f : 0.f, DeltaSeconds, 9.f);
	const FVector SprintShift = (ArmsSprintOffset - ArmsHipOffset) * ArmsSprintAlpha;
	const FRotator SprintTilt = ArmsSprintRotation * ArmsSprintAlpha;

	// Scoped weapons: once raised, the gun wobbles and steadies before the zoom.
	FRotator Wobble = FRotator::ZeroRotator;
	if (bIsAiming && Data && Data->bScopeOverlay)
	{
		const float T = AimHeldTime - Motion.ScopeRaiseTime;
		if (T > 0.f && T < Motion.ScopeWobbleTime + 0.2f)
		{
			const float Decay = FMath::Exp(-T * 9.f) * Motion.ScopeWobble;
			Wobble = FRotator(FMath::Sin(T * 31.f) * Decay, FMath::Cos(T * 23.f) * Decay * 0.7f, FMath::Sin(T * 17.f) * Decay * 0.5f);
		}
	}

	// Minigun: the faster the barrels turn, the harder it shakes.
	FVector Jitter = FVector::ZeroVector;
	FRotator JitterRotation = FRotator::ZeroRotator;
	if (Weapon && Data && Data->SpinUpTime > 0.f)
	{
		const float Spin = Weapon->GetSpinAlpha();
		Jitter = FVector(FMath::FRandRange(-0.25f, 0.25f), FMath::FRandRange(-0.25f, 0.25f), FMath::FRandRange(-0.25f, 0.25f)) * Spin;
		JitterRotation = FRotator(0.f, 0.f, FMath::Sin(Now * 55.f) * 1.5f * Spin);
	}

	// Bow: drawing pulls the arms toward you; full draw trembles.
	FVector Draw = FVector::ZeroVector;
	FRotator DrawRotation = FRotator::ZeroRotator;
	if (Weapon && Data && Data->bChargeToFire)
	{
		const float Charge = Weapon->GetCharge();
		Draw = FVector(-9.f * Charge, -2.f * Charge, 1.f * Charge) + (Charge >= 0.99f ? FVector(0.f, 0.f, FMath::Sin(Now * 40.f) * 0.3f) : FVector::ZeroVector);
		DrawRotation = FRotator(2.f * Charge, 0.f, -5.f * Charge);
	}

	// Near a wall the gun is pulled back and lowered (never through the wall).
	float WallTarget = 0.f;
	if (Weapon)
	{
		FHitResult WallHit;
		FCollisionQueryParams WallParams(SCENE_QUERY_STAT(HitmenArmsWall), false, this);
		const FVector Eye = FirstPersonCamera->GetComponentLocation();
		if (GetWorld()->SweepSingleByChannel(WallHit, Eye, Eye + Control.Vector() * ArmsWallCheckDistance, FQuat::Identity, ECC_Visibility, FCollisionShape::MakeSphere(6.f), WallParams))
		{
			WallTarget = 1.f - WallHit.Distance / ArmsWallCheckDistance;
		}
	}
	WallPullAlpha = FMath::FInterpTo(WallPullAlpha, WallTarget, DeltaSeconds, 12.f);
	const FVector WallOffset = FVector(-16.f, 3.f, -9.f) * WallPullAlpha;
	const FRotator WallRotation = FRotator(-24.f, 6.f, 12.f) * WallPullAlpha;

	FVector ActionOffset;
	FRotator ActionRotation;
	EvaluateFirstPersonAction(ActionOffset, ActionRotation);

	// Compose in camera space (X forward, Y right, Z up); rotations turn around the hands (ArmsPivot).
	const FVector Location = CurrentArmsOffset + Bob + SprintShift + KickOffset + EquipOffset + ActionOffset + WallOffset + Jitter + Draw
		+ FVector(ArmsMoveState.X, 0.f, 0.f);
	const FRotator Rotation(
		ArmsSway.Pitch + SprintTilt.Pitch + KickRotation.X + EquipRotation.Pitch + ActionRotation.Pitch + WallRotation.Pitch + Wobble.Pitch + DrawRotation.Pitch,
		ArmsSway.Yaw + SprintTilt.Yaw + KickRotation.Y + EquipRotation.Yaw + ActionRotation.Yaw + WallRotation.Yaw + Wobble.Yaw,
		ArmsSway.Roll + SprintTilt.Roll + KickRotation.Z + EquipRotation.Roll + ActionRotation.Roll + WallRotation.Roll + Wobble.Roll
			+ JitterRotation.Roll + DrawRotation.Roll + ArmsMoveState.Y);
	const FQuat Turned = Rotation.Quaternion();
	const FVector Final = ArmsPivot + Turned.RotateVector(Location - ArmsPivot);
	// The mannequin faces +Y: turn it to the view first, then apply the camera-space rotation.
	FirstPersonMesh->SetRelativeLocationAndRotation(Final, Turned * FRotator(0.f, ArmsYawOffset, 0.f).Quaternion());
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
	// A takedown holds the attacker in place until the strike is over.
	return !IsDead() && !StatusEffectComponent->IsIncapacitated() && !(MeleeComponent && MeleeComponent->IsPerformingTakedown());
}

// ---------------------------------------------------------------------------------------------
// Input
// ---------------------------------------------------------------------------------------------

void AHitmenPlayerCharacter::Input_Move(const FVector2D& Axis)
{
	// Under the canopy the same keys steer the drop.
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
	bJumpInputHeld = true;
	JumpInputPressTime = GetWorld()->GetTimeSeconds();
	if (CanAct() && !bParachuting)
	{
		Jump();
	}
	UpdateJetpackFromSpace();
}

void AHitmenPlayerCharacter::Input_JumpReleased()
{
	bJumpInputHeld = false;
	StopJumping();
	UpdateJetpackFromSpace();
}

bool AHitmenPlayerCharacter::HasMoveAbility(TSubclassOf<UHitmenAbility> AbilityClass) const
{
	const UHitmenAbility* Ability = AbilityComponent ? AbilityComponent->GetAbility(EHitmenAbilitySlot::Move) : nullptr;
	return Ability && AbilityClass && Ability->IsA(AbilityClass);
}

void AHitmenPlayerCharacter::UpdateJetpackFromSpace()
{
	// Space held in the air (a moment after the jump, so a jump stays a jump) thrusts; ledge grabs come first.
	const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement();
	const bool bAirborne = Movement && Movement->IsFalling();
	const bool bBusy = Movement && (Movement->IsMantling() || Movement->IsHanging() || Movement->IsClimbing());
	const bool bWant = bJumpInputHeld && CanAct() && !bParachuting && bAirborne && !bBusy
		&& GetWorld()->GetTimeSeconds() - JumpInputPressTime >= JetpackSpaceHoldDelay
		&& HasMoveAbility(UHitmenAbility_Jetpack::StaticClass());
	if (bWant && !bJetpackFromSpace)
	{
		bJetpackFromSpace = true;
		AbilityComponent->PressSlot(EHitmenAbilitySlot::Move);
	}
	else if (!bJumpInputHeld && bJetpackFromSpace)
	{
		bJetpackFromSpace = false;
		AbilityComponent->ReleaseSlot(EHitmenAbilitySlot::Move);
	}
}

void AHitmenPlayerCharacter::Input_SprintTapped()
{
	// A tap of Shift dashes (E still works).
	if (CanAct() && HasMoveAbility(UHitmenAbility_Dash::StaticClass()))
	{
		AbilityComponent->PressSlot(EHitmenAbilitySlot::Move);
		AbilityComponent->ReleaseSlot(EHitmenAbilitySlot::Move);
	}
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
	// One talker per player state: a respawned body replaces the old one (a talker left attached
	// to a destroyed body crashed when that player spoke).
	ReleaseProximityVoice();
	UVOIPStatics::ResetPlayerVoiceTalker(PS);
	VoiceTalker = UVOIPTalker::CreateTalkerForPlayer(PS);
	if (VoiceTalker)
	{
		VoiceTalker->Settings.ComponentToAttachTo = GetRootComponent();
		VoiceTalker->Settings.AttenuationSettings = VoiceAttenuation;
	}
}

void AHitmenPlayerCharacter::ReleaseProximityVoice()
{
	if (VoiceTalker)
	{
		VoiceTalker->Settings.ComponentToAttachTo = nullptr;
		VoiceTalker->DestroyComponent();
		VoiceTalker = nullptr;
	}
}
