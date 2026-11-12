// Hitmen — vehicles

#include "World/Vehicles/HitmenVehicles.h"
#include "NiagaraFunctionLibrary.h"
#include "Core/HitmenGameSettings.h"
#include "Weapons/HitmenCosmeticActors.h"
#include "Data/HitmenSoundSet.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "Components/PointLightComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenHealthComponent.h"
#include "Framework/HitmenGameModeBase.h"
#include "Weapons/HitmenProjectile.h"
#include "AI/HitmenAISubsystem.h"
#include "Hitmen.h"
#include "Camera/CameraComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "GameFramework/FloatingPawnMovement.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/Controller.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/CapsuleComponent.h"
#include "Engine/DamageEvents.h"
#include "Kismet/GameplayStatics.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

// ---------------------------------------------------------------------------------------------
// Base
// ---------------------------------------------------------------------------------------------

AHitmenVehiclePawn::AHitmenVehiclePawn()
{
	// Fixtures are never vaulted, climbed or hung from.
	Tags.AddUnique(HitmenTags::NoTraversal);
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = true;
	SetReplicatingMovement(true);
	AutoPossessAI = EAutoPossessAI::Disabled;

	Collision = CreateDefaultSubobject<UBoxComponent>(TEXT("Collision"));
	Collision->SetBoxExtent(FVector(100.f, 80.f, 60.f));
	Collision->SetCollisionProfileName(TEXT("Pawn"));
	Collision->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	// The Pawn profile ignores weapon traces (characters are hit on their mesh); vehicles are hit on the hull.
	Collision->SetCollisionResponseToChannel(HITMEN_TRACE_WEAPON, ECR_Block);
	SetRootComponent(Collision);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Collision);
	Mesh->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);

	Health = CreateDefaultSubobject<UHitmenHealthComponent>(TEXT("Health"));
}

void AHitmenVehiclePawn::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenVehiclePawn, Driver);
	DOREPLIFETIME(AHitmenVehiclePawn, bVehicleDestroyed);
}

void AHitmenVehiclePawn::BeginPlay()
{
	Super::BeginPlay();
	Health->OnHealthChanged.AddDynamic(this, &AHitmenVehiclePawn::HandleHealthChanged);
	if (HasAuthority())
	{
		Health->OnDeath.AddDynamic(this, &AHitmenVehiclePawn::HandleHealthDeath);
	}
}

bool AHitmenVehiclePawn::CanInteract_Implementation(AActor* Interactor) const
{
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Interactor);
	return !Driver && !bVehicleDestroyed && Character && Character->IsPlayerControlled() && !Character->IsDead();
}

FText AHitmenVehiclePawn::GetInteractionText_Implementation(AActor* Interactor) const
{
	return NSLOCTEXT("Hitmen", "UseVehicle", "Use");
}

void AHitmenVehiclePawn::Interact_Implementation(AActor* Interactor)
{
	if (HasAuthority())
	{
		Enter(Cast<AHitmenCharacterBase>(Interactor));
	}
}

int32 AHitmenVehiclePawn::GetHitmenTeamId_Implementation() const
{
	return Driver ? UHitmenTeamLibrary::GetTeamId(Driver) : HitmenTeams::Neutral;
}

void AHitmenVehiclePawn::SetDriverStowed(AHitmenCharacterBase* Character, bool bStowed)
{
	if (Character)
	{
		Character->SetActorHiddenInGame(bStowed);
		Character->SetActorEnableCollision(!bStowed);
	}
}

void AHitmenVehiclePawn::Enter(AHitmenCharacterBase* Character)
{
	if (!Character || Driver || bVehicleDestroyed || Character->IsDead())
	{
		return;
	}
	AController* DriverController = Character->GetController();
	if (!DriverController)
	{
		return;
	}
	Driver = Character;
	StowedDriver = Character;
	Character->GetCharacterMovement()->StopMovementImmediately();
	SetDriverStowed(Character, true);
	Character->AttachToActor(this, FAttachmentTransformRules::SnapToTargetNotIncludingScale);
	if (UHitmenHealthComponent* DriverHealth = Character->GetHealth())
	{
		DriverHealth->OnHealthChanged.AddUniqueDynamic(this, &AHitmenVehiclePawn::HandleDriverHealthChanged);
	}
	DriverController->Possess(this);
	Juice_OnEntered(Character);
}

void AHitmenVehiclePawn::Exit()
{
	AHitmenCharacterBase* Character = Driver;
	if (!Character)
	{
		return;
	}
	AController* PlayerController = GetController();
	bFiring = false;
	Driver = nullptr;
	StowedDriver.Reset();
	if (UHitmenHealthComponent* DriverHealth = Character->GetHealth())
	{
		DriverHealth->OnHealthChanged.RemoveDynamic(this, &AHitmenVehiclePawn::HandleDriverHealthChanged);
	}
	Character->DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
	SetDriverStowed(Character, false);
	PlaceExitingDriver(Character);
	if (PlayerController)
	{
		PlayerController->Possess(Character);
	}
	Juice_OnExited();
}

void AHitmenVehiclePawn::PlaceExitingDriver(AHitmenCharacterBase* Character) const
{
	const FVector Candidates[] = {
		ExitOffset,
		FVector(ExitOffset.X, -ExitOffset.Y, ExitOffset.Z),
		FVector(-ExitOffset.X, ExitOffset.Y, ExitOffset.Z),
		FVector(0.f, 0.f, ExitOffset.Z + Collision->GetScaledBoxExtent().Z * 2.f)
	};
	const FTransform& VehicleTransform = GetActorTransform();
	for (const FVector& Offset : Candidates)
	{
		// TeleportTo checks encroachment and nudges to a free spot; false = no room there.
		if (Character->TeleportTo(VehicleTransform.TransformPosition(Offset), GetActorRotation()))
		{
			return;
		}
	}
	Character->SetActorLocation(VehicleTransform.TransformPosition(Candidates[0]));
}

void AHitmenVehiclePawn::UnPossessed()
{
	Super::UnPossessed();
	// Exit() clears Driver before re-possessing the character, so this only runs on a disconnect.
	if (HasAuthority() && Driver)
	{
		Exit();
	}
}

void AHitmenVehiclePawn::OnRep_Driver()
{
	// Collision is not replicated: mirror the server's stow / unstow on every client.
	AHitmenCharacterBase* Previous = StowedDriver.Get();
	if (Previous == Driver)
	{
		return;
	}
	if (Previous)
	{
		SetDriverStowed(Previous, false);
		Juice_OnExited();
	}
	if (Driver)
	{
		SetDriverStowed(Driver, true);
		Juice_OnEntered(Driver);
	}
	StowedDriver = Driver;
}

void AHitmenVehiclePawn::Input_Look(const FVector2D& Axis)
{
	AddControllerYawInput(Axis.X);
	AddControllerPitchInput(-Axis.Y);
}

void AHitmenVehiclePawn::Input_Interact()
{
	Server_Exit();
}

void AHitmenVehiclePawn::Server_Exit_Implementation()
{
	Exit();
}

void AHitmenVehiclePawn::Input_FirePressed()
{
	Server_SetFiring(true);
}

void AHitmenVehiclePawn::Input_FireReleased()
{
	Server_SetFiring(false);
}

void AHitmenVehiclePawn::Server_SetFiring_Implementation(bool bNewFiring)
{
	bFiring = bNewFiring && !bVehicleDestroyed;
	if (bFiring)
	{
		FireServer();
	}
}

// ---- Damage ----

float AHitmenVehiclePawn::TakeDamage(float DamageAmount, FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser)
{
	if (!HasAuthority() || bVehicleDestroyed || DamageAmount <= 0.f || !Health)
	{
		return 0.f;
	}
	AActor* InstigatorActor = EventInstigator ? EventInstigator->GetPawn() : DamageCauser;
	if (Driver)
	{
		const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
		if (GM && !GM->CanDamage(InstigatorActor, this))
		{
			return 0.f;
		}
	}
	else if (!bEmptyVehicleTakesDamage)
	{
		return 0.f;
	}

	// Super applies the radial falloff (explosions route through ApplyRadialDamageWithFalloff).
	const float Incoming = Super::TakeDamage(DamageAmount, DamageEvent, EventInstigator, DamageCauser);
	if (Incoming <= 0.f)
	{
		return 0.f;
	}
	float VehicleDamage = Incoming;
	if (DamageEvent.IsOfType(FPointDamageEvent::ClassID))
	{
		VehicleDamage *= PointDamageMultiplier;
	}
	else if (DamageEvent.IsOfType(FRadialDamageEvent::ClassID))
	{
		VehicleDamage *= RadialDamageMultiplier;
	}

	// Cache: a lethal hit ejects the driver before the forward.
	AHitmenCharacterBase* HitDriver = Driver;
	const float Dealt = Health->ApplyDamage(VehicleDamage, InstigatorActor, DamageCauser, false);

	if (HitDriver && DriverDamageShare > 0.f)
	{
		// Generic event: the falloff is already applied, the driver must not re-scale it.
		const FDamageEvent Forwarded(DamageEvent.DamageTypeClass);
		HitDriver->TakeDamage(Incoming * DriverDamageShare, Forwarded, EventInstigator, DamageCauser);
	}
	return Dealt;
}

void AHitmenVehiclePawn::HandleHealthChanged(UHitmenHealthComponent* HealthComponent, float NewHealth, float Delta, AActor* DamageInstigator)
{
	if (Delta < 0.f)
	{
		Juice_OnDamaged(-Delta, HealthComponent ? HealthComponent->GetHealthNormalized() : 0.f);
	}
}

void AHitmenVehiclePawn::HandleHealthDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot)
{
	if (HasAuthority() && !bVehicleDestroyed)
	{
		OnVehicleDestroyed();
	}
}

void AHitmenVehiclePawn::OnVehicleDestroyed()
{
	bVehicleDestroyed = true;
	bFiring = false;
	Exit();
	Juice_OnDestroyed();
}

void AHitmenVehiclePawn::OnRep_VehicleDestroyed()
{
	if (bVehicleDestroyed)
	{
		Juice_OnDestroyed();
	}
}

void AHitmenVehiclePawn::HandleDriverHealthChanged(UHitmenHealthComponent* HealthComponent, float NewHealth, float Delta, AActor* DamageInstigator)
{
	if (HasAuthority() && NewHealth <= 0.f && Driver && HealthComponent == Driver->GetHealth())
	{
		Exit();
	}
}

// ---------------------------------------------------------------------------------------------
// Mounted gun
// ---------------------------------------------------------------------------------------------

AHitmenMountedGun::AHitmenMountedGun()
{
	GunPivot = CreateDefaultSubobject<USceneComponent>(TEXT("GunPivot"));
	GunPivot->SetupAttachment(Collision);
	GunPivot->SetRelativeLocation(FVector(0.f, 0.f, 120.f));
	DisplayName = NSLOCTEXT("Hitmen", "MountedGun", "Mounted gun");

	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(GunPivot);
	Camera->SetRelativeLocation(FVector(-85.f, 0.f, 55.f));

	Mesh->SetupAttachment(GunPivot);

	Tripod = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Tripod"));
	Tripod->SetupAttachment(Collision);
	Tripod->SetCollisionResponseToChannel(ECC_GameTraceChannel3, ECR_Block);
	Shield = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Shield"));
	Shield->SetupAttachment(GunPivot);
	MuzzleLight = CreateDefaultSubobject<UPointLightComponent>(TEXT("MuzzleLight"));
	MuzzleLight->SetupAttachment(GunPivot);
	MuzzleLight->SetRelativeLocation(FVector(150.f, 0.f, 0.f));
	MuzzleLight->SetIntensity(0.f);
	MuzzleLight->SetAttenuationRadius(500.f);
	MuzzleLight->SetLightColor(FLinearColor(1.f, 0.7f, 0.35f));
	MuzzleLight->SetCastShadows(false);
	Shield->SetCollisionResponseToChannel(ECC_GameTraceChannel3, ECR_Block);

	// The gunner stands exposed behind the gun; the gun itself shrugs off most bullets.
	DriverDamageShare = 0.6f;
	PointDamageMultiplier = 0.15f;
	RadialDamageMultiplier = 0.75f;
	UseFromBehindText = NSLOCTEXT("Hitmen", "UseFromBehind", "Use from behind");
}

void AHitmenMountedGun::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME_CONDITION(AHitmenMountedGun, AimRotation, COND_SkipOwner);
}

bool AHitmenMountedGun::IsUserBehind(const AActor* User) const
{
	if (!User)
	{
		return false;
	}
	const FVector ToUser = (User->GetActorLocation() - GetActorLocation()).GetSafeNormal2D();
	return FVector::DotProduct(GetActorForwardVector().GetSafeNormal2D(), ToUser) <= UseFromBehindMaxDot;
}

FText AHitmenMountedGun::GetInteractionText_Implementation(AActor* Interactor) const
{
	// The prompt stays visible from the front so the player knows where to stand.
	return bUsableFromAnySide || IsUserBehind(Interactor) ? NSLOCTEXT("Hitmen", "UseMountedGun", "Use mounted gun") : UseFromBehindText;
}

void AHitmenMountedGun::Interact_Implementation(AActor* Interactor)
{
	if (bUsableFromAnySide || IsUserBehind(Interactor))
	{
		Super::Interact_Implementation(Interactor);
	}
}

void AHitmenMountedGun::Input_Look(const FVector2D& Axis)
{
	AimRotation.Yaw = FMath::Clamp(AimRotation.Yaw + Axis.X * LookSensitivity, -MaxYaw, MaxYaw);
	AimRotation.Pitch = FMath::Clamp(AimRotation.Pitch + Axis.Y * LookSensitivity, MinPitch, MaxPitch);
	Server_SetAim(AimRotation);
}

void AHitmenMountedGun::Server_SetAim_Implementation(FRotator NewAim)
{
	AimRotation = FRotator(FMath::Clamp(NewAim.Pitch, MinPitch, MaxPitch), FMath::Clamp(NewAim.Yaw, -MaxYaw, MaxYaw), 0.f);
}

void AHitmenMountedGun::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	GunPivot->SetRelativeRotation(AimRotation);
	MuzzleLight->SetIntensity(GetWorld()->GetTimeSeconds() < MuzzleFlashUntil ? 30000.f : 0.f);
	if (HasAuthority() && bFiring && !bVehicleDestroyed && GetWorld()->GetTimeSeconds() - LastFireTime >= FireInterval)
	{
		FireServer();
	}
}

void AHitmenMountedGun::FireServer()
{
	if (bVehicleDestroyed || GetWorld()->GetTimeSeconds() - LastFireTime < FireInterval)
	{
		return;
	}
	LastFireTime = GetWorld()->GetTimeSeconds();
	const FVector Start = GunPivot->GetComponentLocation() + GunPivot->GetForwardVector() * 100.f;
	const FVector Direction = FMath::VRandCone(GunPivot->GetForwardVector(), FMath::DegreesToRadians(SpreadDegrees));
	FVector Impact = Start + Direction * Range;
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenMountedGun), false, this);
	if (Driver)
	{
		Params.AddIgnoredActor(Driver);
	}
	if (GetWorld()->LineTraceSingleByChannel(Hit, Start, Impact, HITMEN_TRACE_WEAPON, Params))
	{
		Impact = Hit.ImpactPoint;
		UGameplayStatics::ApplyPointDamage(Hit.GetActor(), Damage, Direction, Hit, GetController(), this, UDamageType::StaticClass());
	}
	UHitmenAISubsystem::ReportNoise(this, Start, 900.f, Driver ? static_cast<AActor*>(Driver) : this, EHitmenNoiseType::Gunshot);
	Multicast_Fire(Impact);
}

void AHitmenMountedGun::Multicast_Fire_Implementation(FVector_NetQuantize Impact)
{
	// Every shot is felt: sound, a muzzle light flash, a view shake for the gunner.
	if (GetNetMode() != NM_DedicatedServer)
	{
		UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::MountedGunFire, GunPivot->GetComponentLocation());
		MuzzleFlashUntil = GetWorld()->GetTimeSeconds() + 0.04f;
		if (AHitmenPlayerCharacter* Gunner = Cast<AHitmenPlayerCharacter>(Driver); Gunner && IsLocallyControlled())
		{
			Gunner->AddCameraShake(FireShake, 0.12f);
		}
	}
	Juice_OnFire(Impact);
}

// ---------------------------------------------------------------------------------------------
// Tank
// ---------------------------------------------------------------------------------------------

AHitmenTank::AHitmenTank()
{
	Collision->SetBoxExtent(FVector(300.f, 180.f, 100.f));

	DisplayName = NSLOCTEXT("Hitmen", "Tank", "Tank");
	Turret = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Turret"));
	Turret->SetupAttachment(Collision);
	Barrel = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Barrel"));
	Barrel->SetupAttachment(Turret);
	Barrel->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	MuzzleLight = CreateDefaultSubobject<UPointLightComponent>(TEXT("MuzzleLight"));
	MuzzleLight->SetupAttachment(Turret);
	MuzzleLight->SetIntensity(0.f);
	MuzzleLight->SetAttenuationRadius(1500.f);
	MuzzleLight->SetLightColor(FLinearColor(1.f, 0.6f, 0.25f));
	MuzzleLight->SetCastShadows(false);
	Turret->SetRelativeLocation(FVector(0.f, 0.f, 120.f));

	SpringArm = CreateDefaultSubobject<USpringArmComponent>(TEXT("SpringArm"));
	SpringArm->SetupAttachment(Collision);
	SpringArm->TargetArmLength = 900.f;
	SpringArm->SocketOffset = FVector(0.f, 0.f, 250.f);
	SpringArm->bUsePawnControlRotation = true;

	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(SpringArm);

	Movement = CreateDefaultSubobject<UFloatingPawnMovement>(TEXT("Movement"));
	Movement->MaxSpeed = 700.f;
	Movement->Acceleration = 800.f;
	Movement->Deceleration = 1200.f;
	// The tank integrates its own motion on the server (the floating movement only moves local controllers).
	Movement->bAutoUpdateTickRegistration = false;
	Movement->PrimaryComponentTick.bStartWithTickEnabled = false;

	bUseControllerRotationYaw = false;

	// Armored hull: bullets barely scratch it, the crew is well protected.
	DriverDamageShare = 0.05f;
	PointDamageMultiplier = 0.05f;
	RadialDamageMultiplier = 0.35f;
}

void AHitmenTank::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME_CONDITION(AHitmenTank, AimRotation, COND_SkipOwner);
	DOREPLIFETIME_CONDITION(AHitmenTank, DriverStateLocation, COND_AutonomousOnly);
	DOREPLIFETIME_CONDITION(AHitmenTank, DriverStateYaw, COND_AutonomousOnly);
}

void AHitmenTank::BeginPlay()
{
	Super::BeginPlay();
	Movement->SetComponentTickEnabled(false);
	if (HasAuthority())
	{
		AimRotation = FRotator(0.f, GetActorRotation().Yaw, 0.f);
		DriverStateLocation = GetActorLocation();
		DriverStateYaw = GetActorRotation().Yaw;
	}
}

FRotator AHitmenTank::ClampAim(const FRotator& Aim) const
{
	return FRotator(FMath::Clamp(FRotator::NormalizeAxis(Aim.Pitch), MinAimPitch, MaxAimPitch), FRotator::NormalizeAxis(Aim.Yaw), 0.f);
}

void AHitmenTank::Input_Move(const FVector2D& Axis)
{
	// W/S drive the hull, A/D (or Q/D) turn it. The server simulates; resend unchanged input
	// periodically so the server-side timeout only fires when the driver really let go.
	const FVector2D Clamped(FMath::Clamp(Axis.X, -1.f, 1.f), FMath::Clamp(Axis.Y, -1.f, 1.f));
	const float Now = GetWorld()->GetTimeSeconds();
	if (HasAuthority() || !Clamped.Equals(LastSentDriveInput, KINDA_SMALL_NUMBER) || Now - LastDriveSendTime >= DriveInputResendInterval)
	{
		LastSentDriveInput = Clamped;
		LastDriveSendTime = Now;
		Server_SetDriveInput(Clamped);
	}
}

void AHitmenTank::Server_SetDriveInput_Implementation(FVector2D Axis)
{
	DriveInput = bVehicleDestroyed ? FVector2D::ZeroVector : FVector2D(FMath::Clamp(Axis.X, -1.f, 1.f), FMath::Clamp(Axis.Y, -1.f, 1.f));
	LastDriveInputTime = GetWorld()->GetTimeSeconds();
}

void AHitmenTank::Input_Look(const FVector2D& Axis)
{
	Super::Input_Look(Axis);
	if (!HasAuthority())
	{
		Server_SetAim(ClampAim(GetControlRotation()));
	}
}

void AHitmenTank::Input_FirePressed()
{
	// Make sure the server shoots where the driver looks.
	if (!HasAuthority())
	{
		Server_SetAim(ClampAim(GetControlRotation()));
	}
	Super::Input_FirePressed();
}

void AHitmenTank::Server_SetAim_Implementation(FRotator NewAim)
{
	AimRotation = ClampAim(NewAim);
}

void AHitmenTank::OnRep_DriverState()
{
	bHasDriverState = true;
}

void AHitmenTank::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (HasAuthority())
	{
		if (IsLocallyControlled())
		{
			AimRotation = ClampAim(GetControlRotation());
		}
		SimulateDrive(DeltaSeconds);
		StickToGround(DeltaSeconds);
		DriverStateLocation = GetActorLocation();
		DriverStateYaw = GetActorRotation().Yaw;
	}
	else if (IsLocallyControlled() && bHasDriverState)
	{
		// The driving client does not receive replicated movement: follow the server transform.
		const FVector Target = DriverStateLocation;
		const FRotator TargetRotation(0.f, DriverStateYaw, 0.f);
		if (FVector::DistSquared(GetActorLocation(), Target) > FMath::Square(DriverSnapDistance))
		{
			SetActorLocationAndRotation(Target, TargetRotation);
		}
		else
		{
			SetActorLocationAndRotation(FMath::VInterpTo(GetActorLocation(), Target, DeltaSeconds, DriverSmoothingSpeed),
				FMath::RInterpTo(GetActorRotation(), TargetRotation, DeltaSeconds, DriverSmoothingSpeed));
		}
	}

	// Turret follows the view: the driver's own camera, the replicated aim for everyone else.
	const float ViewYaw = IsLocallyControlled() ? GetControlRotation().Yaw : AimRotation.Yaw;
	Turret->SetWorldRotation(FRotator(0.f, ViewYaw, 0.f));
	// The cannon follows the aim pitch and kicks back on a shot; the muzzle light flashes.
	const float ViewPitch = FMath::Clamp(IsLocallyControlled() ? FRotator::NormalizeAxis(GetControlRotation().Pitch) : AimRotation.Pitch, MinAimPitch, MaxAimPitch);
	BarrelKick = FMath::FInterpTo(BarrelKick, 0.f, DeltaSeconds, 6.f);
	const FRotator BarrelRotation(ViewPitch, 0.f, 0.f);
	Barrel->SetRelativeLocationAndRotation(BarrelRotation.RotateVector(FVector(MuzzleOffset * 0.5f - BarrelKickDistance * BarrelKick, 0.f, 0.f)) + FVector(0.f, 0.f, 10.f), BarrelRotation + FRotator(-90.f, 0.f, 0.f));
	// The hull rocks back on a shot and settles.
	if (!bHullRestSet)
	{
		HullRestRotation = Mesh->GetRelativeRotation();
		bHullRestSet = true;
	}
	HullKick = FMath::FInterpTo(HullKick, 0.f, DeltaSeconds, 5.f);
	const float Rock = FMath::Sin(HullKick * PI) * HullKickPitch;
	Mesh->SetRelativeRotation(HullRestRotation + FRotator(Rock, 0.f, 0.f));
	MuzzleLight->SetRelativeLocation(BarrelRotation.RotateVector(FVector(MuzzleOffset + 20.f, 0.f, 0.f)) + FVector(0.f, 0.f, 10.f));
	MuzzleLight->SetIntensity(GetWorld()->GetTimeSeconds() < MuzzleFlashUntil ? 200000.f : 0.f);
}

void AHitmenTank::SimulateDrive(float DeltaSeconds)
{
	if (bVehicleDestroyed || !Driver || GetWorld()->GetTimeSeconds() - LastDriveInputTime > DriveInputTimeout)
	{
		DriveInput = FVector2D::ZeroVector;
	}

	if (!FMath::IsNearlyZero(DriveInput.X))
	{
		AddActorWorldRotation(FRotator(0.f, DriveInput.X * TurnRate * DeltaSeconds, 0.f));
	}

	const float TargetSpeed = DriveInput.Y * Movement->GetMaxSpeed();
	const bool bAccelerating = !FMath::IsNearlyZero(TargetSpeed) && TargetSpeed * CurrentSpeed >= 0.f && FMath::Abs(TargetSpeed) >= FMath::Abs(CurrentSpeed);
	CurrentSpeed = FMath::FInterpConstantTo(CurrentSpeed, TargetSpeed, DeltaSeconds, bAccelerating ? Movement->Acceleration : Movement->Deceleration);

	const FVector Forward = GetActorForwardVector();
	if (!FMath::IsNearlyZero(CurrentSpeed))
	{
		const FVector Delta = Forward * CurrentSpeed * DeltaSeconds;
		FHitResult Hit;
		AddActorWorldOffset(Delta, true, &Hit);
		if (Hit.IsValidBlockingHit())
		{
			// Slide along walls and up slopes; lose the speed pushed into the obstacle.
			AddActorWorldOffset(FVector::VectorPlaneProject(Delta * (1.f - Hit.Time), Hit.Normal), true);
			CurrentSpeed *= FMath::Clamp(1.f - FMath::Abs(FVector::DotProduct(Hit.Normal, Forward)), 0.f, 1.f);
		}
	}
	Movement->Velocity = Forward * CurrentSpeed;
	Movement->UpdateComponentVelocity();
}

void AHitmenTank::StickToGround(float DeltaSeconds)
{
	FHitResult Ground;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenTankGround), false, this);
	if (Driver)
	{
		Params.AddIgnoredActor(Driver);
	}
	const FVector Location = GetActorLocation();
	if (GetWorld()->LineTraceSingleByChannel(Ground, Location, Location - FVector::UpVector * GroundTraceDistance, ECC_Visibility, Params))
	{
		const float TargetZ = Ground.ImpactPoint.Z + Collision->GetScaledBoxExtent().Z + HoverHeight;
		SetActorLocation(FVector(Location.X, Location.Y, FMath::FInterpTo(Location.Z, TargetZ, DeltaSeconds, GroundFollowSpeed)), true);
	}
}

void AHitmenTank::OnVehicleDestroyed()
{
	DriveInput = FVector2D::ZeroVector;
	CurrentSpeed = 0.f;
	Movement->Velocity = FVector::ZeroVector;
	Super::OnVehicleDestroyed();
}

void AHitmenTank::FireServer()
{
	if (bVehicleDestroyed || !ShellClass || GetWorld()->GetTimeSeconds() - LastShotTime < ReloadTime)
	{
		return;
	}
	LastShotTime = GetWorld()->GetTimeSeconds();
	const FRotator Aim = AimRotation;
	const FVector Start = Turret->GetComponentLocation() + Aim.Vector() * MuzzleOffset;
	FActorSpawnParameters SpawnParams;
	SpawnParams.Owner = this;
	SpawnParams.Instigator = this;
	SpawnParams.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	if (AHitmenProjectile* Shell = GetWorld()->SpawnActor<AHitmenProjectile>(ShellClass, Start, Aim, SpawnParams))
	{
		Shell->Launch(Aim.Vector() * ShellSpeed, 0.f);
	}
	Multicast_TankFired();
	UHitmenAISubsystem::ReportNoise(this, Start, CannonNoiseRadius, Driver ? static_cast<AActor*>(Driver) : this, EHitmenNoiseType::Explosion);
}

void AHitmenTank::Multicast_TankFired_Implementation()
{
	if (GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::TankFire, Turret->GetComponentLocation());
	MuzzleFlashUntil = GetWorld()->GetTimeSeconds() + 0.08f;
	BarrelKick = 1.f;
	HullKick = 1.f;
	const FVector Muzzle = MuzzleLight->GetComponentLocation();
	if (MuzzleBlastFX)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, MuzzleBlastFX, Muzzle, Turret->GetComponentRotation(), FVector(0.6f));
	}
	if (HullDustFX)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, HullDustFX, GetActorLocation() - FVector(0.f, 0.f, 60.f), FRotator::ZeroRotator, FVector(1.2f));
	}
	AHitmenFlashBurst::SpawnBurst(GetWorld(), Muzzle, UHitmenGameSettings::Get()->FlashMaterial.LoadSynchronous(), FLinearColor(1.f, 0.6f, 0.25f), 20.f, 90.f, 0.1f, 0.f, 0.f);
	if (AHitmenPlayerCharacter* Gunner = Cast<AHitmenPlayerCharacter>(Driver); Gunner && IsLocallyControlled())
	{
		Gunner->AddCameraShake(FireShake, 0.35f);
	}
}

float AHitmenVehiclePawn::GetHealthNormalized() const
{
	return Health && Health->GetMaxHealth() > 0.f ? FMath::Clamp(Health->GetHealth() / Health->GetMaxHealth(), 0.f, 1.f) : 1.f;
}

float AHitmenTank::GetReloadAlpha() const
{
	return ReloadTime > 0.f ? FMath::Clamp((GetWorld()->GetTimeSeconds() - LastShotTime) / ReloadTime, 0.f, 1.f) : 1.f;
}
