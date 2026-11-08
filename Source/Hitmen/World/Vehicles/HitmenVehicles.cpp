// Hitmen — vehicles

#include "World/Vehicles/HitmenVehicles.h"
#include "Characters/HitmenCharacterBase.h"
#include "Weapons/HitmenProjectile.h"
#include "AI/HitmenAISubsystem.h"
#include "Hitmen.h"
#include "Camera/CameraComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "GameFramework/FloatingPawnMovement.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/CapsuleComponent.h"
#include "Kismet/GameplayStatics.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

// ---------------------------------------------------------------------------------------------
// Base
// ---------------------------------------------------------------------------------------------

AHitmenVehiclePawn::AHitmenVehiclePawn()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = true;
	SetReplicatingMovement(true);
	AutoPossessAI = EAutoPossessAI::Disabled;

	Collision = CreateDefaultSubobject<UBoxComponent>(TEXT("Collision"));
	Collision->SetBoxExtent(FVector(100.f, 80.f, 60.f));
	Collision->SetCollisionProfileName(TEXT("Pawn"));
	Collision->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	SetRootComponent(Collision);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Collision);
	Mesh->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
}

void AHitmenVehiclePawn::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenVehiclePawn, Driver);
}

bool AHitmenVehiclePawn::CanInteract_Implementation(AActor* Interactor) const
{
	return !Driver && Cast<AHitmenCharacterBase>(Interactor) && Cast<APawn>(Interactor)->IsPlayerControlled();
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

void AHitmenVehiclePawn::Enter(AHitmenCharacterBase* Character)
{
	if (!Character || Driver)
	{
		return;
	}
	AController* DriverController = Character->GetController();
	if (!DriverController)
	{
		return;
	}
	Driver = Character;
	Character->GetCharacterMovement()->StopMovementImmediately();
	Character->SetActorEnableCollision(false);
	Character->SetActorHiddenInGame(true);
	Character->AttachToActor(this, FAttachmentTransformRules::SnapToTargetNotIncludingScale);
	DriverController->Possess(this);
	Juice_OnEntered(Character);
}

void AHitmenVehiclePawn::Exit()
{
	AHitmenCharacterBase* Character = Driver;
	AController* PlayerController = GetController();
	if (!Character)
	{
		return;
	}
	bFiring = false;
	Driver = nullptr;
	Character->DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
	Character->SetActorHiddenInGame(false);
	Character->SetActorEnableCollision(true);
	Character->TeleportTo(GetActorTransform().TransformPosition(ExitOffset), GetActorRotation());
	if (PlayerController)
	{
		PlayerController->Possess(Character);
	}
	Juice_OnExited();
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
	bFiring = bNewFiring;
	if (bFiring)
	{
		FireServer();
	}
}

// ---------------------------------------------------------------------------------------------
// Mounted gun
// ---------------------------------------------------------------------------------------------

AHitmenMountedGun::AHitmenMountedGun()
{
	GunPivot = CreateDefaultSubobject<USceneComponent>(TEXT("GunPivot"));
	GunPivot->SetupAttachment(Collision);
	GunPivot->SetRelativeLocation(FVector(0.f, 0.f, 80.f));

	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(GunPivot);
	Camera->SetRelativeLocation(FVector(-90.f, 0.f, 30.f));

	Mesh->SetupAttachment(GunPivot);
}

void AHitmenMountedGun::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME_CONDITION(AHitmenMountedGun, AimRotation, COND_SkipOwner);
}

void AHitmenMountedGun::Input_Look(const FVector2D& Axis)
{
	AimRotation.Yaw = FMath::Clamp(AimRotation.Yaw + Axis.X, -MaxYaw, MaxYaw);
	AimRotation.Pitch = FMath::Clamp(AimRotation.Pitch + Axis.Y, MinPitch, MaxPitch);
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
	if (HasAuthority() && bFiring && GetWorld()->GetTimeSeconds() - LastFireTime >= FireInterval)
	{
		FireServer();
	}
}

void AHitmenMountedGun::FireServer()
{
	if (GetWorld()->GetTimeSeconds() - LastFireTime < FireInterval)
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
	UHitmenAISubsystem::ReportNoise(this, Start, 4000.f, Driver ? static_cast<AActor*>(Driver) : this, EHitmenNoiseType::Gunshot);
	Multicast_Fire(Impact);
}

void AHitmenMountedGun::Multicast_Fire_Implementation(FVector_NetQuantize Impact)
{
	Juice_OnFire(Impact);
}

// ---------------------------------------------------------------------------------------------
// Tank
// ---------------------------------------------------------------------------------------------

AHitmenTank::AHitmenTank()
{
	Collision->SetBoxExtent(FVector(300.f, 180.f, 100.f));

	Turret = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Turret"));
	Turret->SetupAttachment(Collision);
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

	bUseControllerRotationYaw = false;
}

void AHitmenTank::Input_Move(const FVector2D& Axis)
{
	// W/S drive the hull, A/D (or Q/D) turn it.
	AddMovementInput(GetActorForwardVector(), Axis.Y);
	AddActorWorldRotation(FRotator(0.f, Axis.X * TurnRate * GetWorld()->GetDeltaSeconds(), 0.f));
}

void AHitmenTank::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	// Turret follows the camera yaw.
	const float ViewYaw = GetControlRotation().Yaw;
	Turret->SetWorldRotation(FRotator(0.f, ViewYaw, 0.f));

	// Stick to the ground.
	FHitResult Ground;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenTankGround), false, this);
	const FVector Location = GetActorLocation();
	if (GetWorld()->LineTraceSingleByChannel(Ground, Location, Location - FVector::UpVector * 2000.f, ECC_Visibility, Params))
	{
		const float TargetZ = Ground.ImpactPoint.Z + Collision->GetScaledBoxExtent().Z + HoverHeight * 0.f;
		SetActorLocation(FVector(Location.X, Location.Y, FMath::FInterpTo(Location.Z, TargetZ, DeltaSeconds, 10.f)), true);
	}
}

void AHitmenTank::FireServer()
{
	if (!ShellClass || GetWorld()->GetTimeSeconds() - LastShotTime < ReloadTime)
	{
		return;
	}
	LastShotTime = GetWorld()->GetTimeSeconds();
	const FRotator Aim = GetControlRotation();
	const FVector Start = Turret->GetComponentLocation() + Aim.Vector() * 350.f;
	FActorSpawnParameters SpawnParams;
	SpawnParams.Owner = this;
	SpawnParams.Instigator = this;
	SpawnParams.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	if (AHitmenProjectile* Shell = GetWorld()->SpawnActor<AHitmenProjectile>(ShellClass, Start, Aim, SpawnParams))
	{
		Shell->Launch(Aim.Vector() * ShellSpeed, 0.f);
	}
	UHitmenAISubsystem::ReportNoise(this, Start, 6000.f, Driver ? static_cast<AActor*>(Driver) : this, EHitmenNoiseType::Explosion);
}
