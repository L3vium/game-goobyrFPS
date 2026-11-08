// Hitmen — traversal fixtures

#include "World/Interactables/HitmenTraversal.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Components/BoxComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/Character.h"
#include "Engine/World.h"

// ---------------------------------------------------------------------------------------------
// Ladder
// ---------------------------------------------------------------------------------------------

AHitmenLadder::AHitmenLadder()
{
	bShowInSightroom = true;
	ClimbVolume = CreateDefaultSubobject<UBoxComponent>(TEXT("ClimbVolume"));
	ClimbVolume->SetupAttachment(Root);
	ClimbVolume->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	ClimbVolume->SetGenerateOverlapEvents(true);
	Mesh->SetCollisionProfileName(TEXT("NoCollision"));
	Mesh->SetCollisionResponseToChannel(ECC_GameTraceChannel3, ECR_Ignore);
}

void AHitmenLadder::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	// Volume in front of the ladder, from the floor to a bit above the top ledge.
	ClimbVolume->SetBoxExtent(FVector(55.f, 45.f, Height * 0.5f + 50.f));
	ClimbVolume->SetRelativeLocation(FVector(40.f, 0.f, Height * 0.5f + 50.f));
}

FVector AHitmenLadder::GetClimbStart() const
{
	return GetActorLocation();
}

FVector AHitmenLadder::GetClimbEnd() const
{
	return GetActorLocation() + FVector::UpVector * Height;
}

// ---------------------------------------------------------------------------------------------
// Zipline
// ---------------------------------------------------------------------------------------------

AHitmenZipline::AHitmenZipline()
{
	GrabVolume = CreateDefaultSubobject<UCapsuleComponent>(TEXT("GrabVolume"));
	GrabVolume->SetupAttachment(Root);
	GrabVolume->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	GrabVolume->SetGenerateOverlapEvents(true);
	Mesh->SetCollisionProfileName(TEXT("NoCollision"));
}

void AHitmenZipline::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	// Capsule along the cable, hanging height below it.
	const FVector Axis = EndPoint;
	const float Length = Axis.Size();
	GrabVolume->SetCapsuleSize(70.f, FMath::Max(80.f, Length * 0.5f));
	GrabVolume->SetRelativeLocation(Axis * 0.5f - FVector::UpVector * 90.f);
	GrabVolume->SetRelativeRotation(FRotationMatrix::MakeFromZ(Axis.GetSafeNormal()).Rotator());
	// Visual cable: the mesh is stretched along the axis (a cylinder by default).
	Mesh->SetRelativeLocation(Axis * 0.5f);
	Mesh->SetRelativeRotation(FRotationMatrix::MakeFromZ(Axis.GetSafeNormal()).Rotator());
	Mesh->SetRelativeScale3D(FVector(0.05f, 0.05f, Length / 100.f));
}

// ---------------------------------------------------------------------------------------------
// Jump pad
// ---------------------------------------------------------------------------------------------

AHitmenJumpPad::AHitmenJumpPad()
{
	Trigger = CreateDefaultSubobject<UBoxComponent>(TEXT("Trigger"));
	Trigger->SetupAttachment(Root);
	Trigger->SetBoxExtent(FVector(80.f, 80.f, 30.f));
	Trigger->SetRelativeLocation(FVector(0.f, 0.f, 30.f));
	Trigger->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
}

void AHitmenJumpPad::BeginPlay()
{
	Super::BeginPlay();
	Trigger->OnComponentBeginOverlap.AddDynamic(this, &AHitmenJumpPad::HandleOverlap);
}

void AHitmenJumpPad::HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	if (ACharacter* Character = Cast<ACharacter>(OtherActor))
	{
		// Runs on the server and the owning client (both overlap), so the launch is predicted.
		Character->LaunchCharacter(FVector::UpVector * LaunchSpeed + GetActorForwardVector() * ForwardSpeed, true, true);
		Juice_OnLaunch(Character);
	}
}

// ---------------------------------------------------------------------------------------------
// Teleporter
// ---------------------------------------------------------------------------------------------

AHitmenTeleporter::AHitmenTeleporter()
{
	Trigger = CreateDefaultSubobject<UBoxComponent>(TEXT("Trigger"));
	Trigger->SetupAttachment(Root);
	Trigger->SetBoxExtent(FVector(70.f, 70.f, 100.f));
	Trigger->SetRelativeLocation(FVector(0.f, 0.f, 100.f));
	Trigger->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
}

void AHitmenTeleporter::BeginPlay()
{
	Super::BeginPlay();
	if (HasAuthority())
	{
		Trigger->OnComponentBeginOverlap.AddDynamic(this, &AHitmenTeleporter::HandleOverlap);
	}
}

FVector AHitmenTeleporter::GetExitLocation() const
{
	return GetActorLocation() + GetActorForwardVector() * 150.f + FVector::UpVector * 100.f;
}

void AHitmenTeleporter::MarkArrival(AActor* Actor)
{
	RecentArrivals.Add(Actor, GetWorld()->GetTimeSeconds());
}

void AHitmenTeleporter::HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	ACharacter* Character = Cast<ACharacter>(OtherActor);
	if (!Character || !Destination)
	{
		return;
	}
	if (const float* Arrived = RecentArrivals.Find(Character); Arrived && GetWorld()->GetTimeSeconds() - *Arrived < ArrivalCooldown)
	{
		return;
	}
	if (UHitmenCharacterMovementComponent* Movement = Cast<UHitmenCharacterMovementComponent>(Character->GetCharacterMovement()))
	{
		Movement->StopClimbing(false);
	}
	Destination->MarkArrival(Character);
	Character->TeleportTo(Destination->GetExitLocation(), Destination->GetActorRotation());
	Multicast_Teleported(Character);
}

void AHitmenTeleporter::Multicast_Teleported_Implementation(AActor* Actor)
{
	Juice_OnTeleported(Actor);
}
