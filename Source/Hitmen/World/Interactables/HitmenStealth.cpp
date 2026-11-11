// Hitmen — stealth fixtures

#include "World/Interactables/HitmenStealth.h"
#include "Data/HitmenAnimSet.h"
#include "GameFramework/PlayerController.h"
#include "Hitmen.h"
#include "UObject/ConstructorHelpers.h"
#include "World/HitmenGeometry.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "AI/HitmenAlertComponent.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "TimerManager.h"

// ---------------------------------------------------------------------------------------------
// Locker
// ---------------------------------------------------------------------------------------------

AHitmenLocker::AHitmenLocker()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	InteractionText = NSLOCTEXT("Hitmen", "Hide", "Hide");

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(HitmenGeometry::CubePath);
	auto MakePanel = [this](const TCHAR* Name, USceneComponent* Parent)
	{
		UStaticMeshComponent* Panel = CreateDefaultSubobject<UStaticMeshComponent>(Name);
		Panel->SetupAttachment(Parent);
		if (Cube.Succeeded())
		{
			Panel->SetStaticMesh(Cube.Object);
		}
		Panel->SetCollisionProfileName(TEXT("BlockAllDynamic"));
		Panel->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
		return Panel;
	};
	if (Cube.Succeeded())
	{
		Mesh->SetStaticMesh(Cube.Object);
	}
	PanelLeft = MakePanel(TEXT("PanelLeft"), Root);
	PanelRight = MakePanel(TEXT("PanelRight"), Root);
	PanelTop = MakePanel(TEXT("PanelTop"), Root);
	PanelBottom = MakePanel(TEXT("PanelBottom"), Root);
	DoorHinge = CreateDefaultSubobject<USceneComponent>(TEXT("DoorHinge"));
	DoorHinge->SetupAttachment(Root);
	DoorUpper = MakePanel(TEXT("DoorUpper"), DoorHinge);
	DoorLower = MakePanel(TEXT("DoorLower"), DoorHinge);
}

void AHitmenLocker::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenLocker, Occupant);
}

void AHitmenLocker::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	const float T = WallThickness;
	const float HalfD = Depth * 0.5f;
	const float HalfW = Width * 0.5f;
	HitmenGeometry::SetBox(Mesh, FVector(-HalfD + T * 0.5f, 0.f, Height * 0.5f), FVector(T, Width, Height));
	HitmenGeometry::SetBox(PanelLeft, FVector(0.f, -HalfW + T * 0.5f, Height * 0.5f), FVector(Depth, T, Height));
	HitmenGeometry::SetBox(PanelRight, FVector(0.f, HalfW - T * 0.5f, Height * 0.5f), FVector(Depth, T, Height));
	HitmenGeometry::SetBox(PanelTop, FVector(0.f, 0.f, Height - T * 0.5f), FVector(Depth, Width, T));
	HitmenGeometry::SetBox(PanelBottom, FVector(0.f, 0.f, T * 0.5f), FVector(Depth, Width, T));
	// Door hinged on the front-left edge, split by the eye slit.
	DoorHinge->SetRelativeLocation(FVector(HalfD - T * 0.5f, -HalfW, 0.f));
	const float SlitBottom = SlitHeight - SlitGap * 0.5f;
	const float SlitTop = SlitHeight + SlitGap * 0.5f;
	HitmenGeometry::SetBox(DoorLower, FVector(0.f, HalfW, SlitBottom * 0.5f), FVector(T, Width, SlitBottom));
	HitmenGeometry::SetBox(DoorUpper, FVector(0.f, HalfW, (SlitTop + Height) * 0.5f), FVector(T, Width, Height - SlitTop));
	for (UStaticMeshComponent* Panel : { PanelLeft.Get(), PanelRight.Get(), PanelTop.Get(), PanelBottom.Get(), DoorUpper.Get(), DoorLower.Get() })
	{
		HitmenGeometry::InheritLook(Panel, Mesh);
	}
	DoorHinge->SetRelativeRotation(FRotator::ZeroRotator);
}

bool AHitmenLocker::CanInteract_Implementation(AActor* Interactor) const
{
	return Super::CanInteract_Implementation(Interactor) && (!Occupant || Occupant == Interactor);
}

FText AHitmenLocker::GetInteractionText_Implementation(AActor* Interactor) const
{
	return Occupant == Interactor ? NSLOCTEXT("Hitmen", "LeaveLocker", "Get out") : InteractionText;
}

void AHitmenLocker::OnInteracted(AActor* Interactor)
{
	AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Interactor);
	if (!Character)
	{
		return;
	}
	if (Occupant == Character)
	{
		Character->GetStatusEffects()->RemoveStatus(EHitmenStatusEffect::Hidden);
		Occupant = nullptr;
	}
	else
	{
		Occupant = Character;
		Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Hidden, 0.f, 1.f, this);
	}
	OnRep_Occupant();
}

void AHitmenLocker::OnRep_Occupant()
{
	if (AHitmenCharacterBase* Previous = AppliedOccupant.Get(); Previous && Previous != Occupant)
	{
		ApplyOccupant(Previous, false);
	}
	if (Occupant && AppliedOccupant.Get() != Occupant)
	{
		ApplyOccupant(Occupant, true);
	}
	AppliedOccupant = Occupant;
	StartDoorCycle();
	Juice_OnOccupantChanged(Occupant != nullptr);
}

void AHitmenLocker::ApplyOccupant(AHitmenCharacterBase* Character, bool bInside)
{
	UCharacterMovementComponent* Movement = Character->GetCharacterMovement();
	if (bInside)
	{
		// Inside the hollow cabinet, facing out through the slit, unable to walk.
		Character->MoveIgnoreActorAdd(this);
		Movement->StopMovementImmediately();
		Movement->DisableMovement();
		const FRotator Facing(0.f, GetActorRotation().Yaw, 0.f);
		Character->SetActorLocationAndRotation(GetActorTransform().TransformPosition(InsideOffset), Facing, false, nullptr, ETeleportType::TeleportPhysics);
		if (APlayerController* PC = Cast<APlayerController>(Character->GetController()); PC && PC->IsLocalController())
		{
			PC->SetControlRotation(Facing);
		}
		Character->PlayCosmeticMontage(Character->GetAnimSet() ? Character->GetAnimSet()->GetMontage(EHitmenAnimMontage::LockerEnter) : nullptr);
	}
	else
	{
		Character->SetActorLocation(GetActorTransform().TransformPosition(ExitOffset), false, nullptr, ETeleportType::TeleportPhysics);
		Character->MoveIgnoreActorRemove(this);
		if (!Character->IsDead())
		{
			Movement->SetMovementMode(MOVE_Falling);
		}
		Character->PlayCosmeticMontage(Character->GetAnimSet() ? Character->GetAnimSet()->GetMontage(EHitmenAnimMontage::LockerExit) : nullptr);
	}
}

void AHitmenLocker::StartDoorCycle()
{
	DoorCycleStart = GetWorld()->GetTimeSeconds();
	SetActorTickEnabled(true);
}

void AHitmenLocker::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const float Alpha = FMath::Clamp((GetWorld()->GetTimeSeconds() - DoorCycleStart) / DoorCycleTime, 0.f, 1.f);
	// Opens, lets the occupant through, closes again (smooth).
	const float Open = FMath::Sin(Alpha * PI);
	DoorHinge->SetRelativeRotation(FRotator(0.f, -DoorOpenAngle * FMath::SmoothStep(0.f, 1.f, Open), 0.f));
	if (Alpha >= 1.f)
	{
		DoorHinge->SetRelativeRotation(FRotator::ZeroRotator);
		SetActorTickEnabled(false);
	}
}

// ---------------------------------------------------------------------------------------------
// Tall grass
// ---------------------------------------------------------------------------------------------

AHitmenTallGrass::AHitmenTallGrass()
{
	Volume = CreateDefaultSubobject<UBoxComponent>(TEXT("Volume"));
	Volume->SetBoxExtent(FVector(300.f, 300.f, 60.f));
	Volume->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	SetRootComponent(Volume);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Volume);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void AHitmenTallGrass::BeginPlay()
{
	Super::BeginPlay();
	if (HasAuthority())
	{
		GetWorldTimerManager().SetTimer(CheckTimer, this, &AHitmenTallGrass::UpdateOccupants, CheckInterval, true);
	}
}

void AHitmenTallGrass::UpdateOccupants()
{
	TArray<AActor*> Inside;
	Volume->GetOverlappingActors(Inside, AHitmenCharacterBase::StaticClass());
	for (AActor* Actor : Inside)
	{
		AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Actor);
		if (Character && !Character->IsDead() && Character->bIsCrouched)
		{
			// Short refreshed status: leaving the grass or standing up removes it quickly.
			Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Concealed, CheckInterval * 2.f, 1.f, this);
		}
	}
}

// ---------------------------------------------------------------------------------------------
// Distraction
// ---------------------------------------------------------------------------------------------

AHitmenDistraction::AHitmenDistraction()
{
	InteractionText = NSLOCTEXT("Hitmen", "Distract", "Turn on");
}

bool AHitmenDistraction::CanRemoteActivate_Implementation() const
{
	return GetWorld()->GetTimeSeconds() - LastTriggerTime >= Cooldown;
}

void AHitmenDistraction::RemoteActivate_Implementation(AActor* Activator)
{
	if (HasAuthority() && CanRemoteActivate_Implementation())
	{
		Trigger(Activator);
	}
}

void AHitmenDistraction::OnInteracted(AActor* Interactor)
{
	if (CanRemoteActivate_Implementation())
	{
		Trigger(Interactor);
	}
}

void AHitmenDistraction::Trigger(AActor* Activator)
{
	LastTriggerTime = GetWorld()->GetTimeSeconds();
	// The noise comes from the device, not from the player, again on every pulse while it plays.
	Multicast_Triggered();
}

void AHitmenDistraction::Multicast_Triggered_Implementation()
{
	// Every machine: plays for PlayTime with a pulsing light and a sound each PulseInterval.
	PlayingUntil = GetWorld()->GetTimeSeconds() + PlayTime;
	SetActorTickEnabled(true);
	if (!PlayingAudio && GetNetMode() != NM_DedicatedServer)
	{
		if (USoundBase* Loop = UHitmenSoundLibrary::GetHitmenSound(EHitmenSound::RadioLoop))
		{
			PlayingAudio = UGameplayStatics::SpawnSoundAttached(Loop, Root);
		}
	}
	Pulse();
	GetWorldTimerManager().SetTimer(PulseTimer, this, &AHitmenDistraction::Pulse, PulseInterval, true);
	Juice_OnTriggered();
}
