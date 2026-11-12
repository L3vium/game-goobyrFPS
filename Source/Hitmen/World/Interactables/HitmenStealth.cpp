// Hitmen — stealth fixtures

#include "World/Interactables/HitmenStealth.h"
#include "Characters/HitmenNPC.h"
#include "NiagaraFunctionLibrary.h"
#include "EngineUtils.h"
#include "Kismet/GameplayStatics.h"
#include "Components/AudioComponent.h"
#include "Data/HitmenSoundSet.h"
#include "Components/PointLightComponent.h"
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
	// Door hinged on a front edge (right by default), split by the eye slit.
	const float Side = bDoorHingeOnRight ? 1.f : -1.f;
	DoorHinge->SetRelativeLocation(FVector(HalfD - T * 0.5f, Side * HalfW, 0.f));
	const float SlitBottom = SlitHeight - SlitGap * 0.5f;
	const float SlitTop = SlitHeight + SlitGap * 0.5f;
	HitmenGeometry::SetBox(DoorLower, FVector(0.f, -Side * HalfW, SlitBottom * 0.5f), FVector(T, Width, SlitBottom));
	HitmenGeometry::SetBox(DoorUpper, FVector(0.f, -Side * HalfW, (SlitTop + Height) * 0.5f), FVector(T, Width, Height - SlitTop));
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
	// The door opens, the character steps in (or out) turning to face out, the door closes: UpdateStep moves it
	// on every machine while the door cycles.
	const FRotator Facing(0.f, GetActorRotation().Yaw, 0.f);
	Character->MoveIgnoreActorAdd(this);
	Movement->StopMovementImmediately();
	Movement->DisableMovement();
	SteppingCharacter = Character;
	bSteppingIn = bInside;
	StepFrom = Character->GetActorLocation();
	StepTo = GetActorTransform().TransformPosition(bInside ? InsideOffset : ExitOffset);
	StepFromRotation = Character->GetControlRotation();
	StepToRotation = bInside ? Facing : FRotator(0.f, Facing.Yaw, 0.f);
	Character->PlayCosmeticMontage(Character->GetAnimSet() ? Character->GetAnimSet()->GetMontage(bInside ? EHitmenAnimMontage::LockerEnter : EHitmenAnimMontage::LockerExit) : nullptr);
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, bInside ? EHitmenSound::LockerEnter : EHitmenSound::LockerExit, GetActorLocation());
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
	DoorHinge->SetRelativeRotation(FRotator(0.f, (bDoorHingeOnRight ? 1.f : -1.f) * DoorOpenAngle * FMath::SmoothStep(0.f, 1.f, Open), 0.f));
	UpdateStep();
	if (Alpha >= 1.f && !SteppingCharacter.IsValid())
	{
		DoorHinge->SetRelativeRotation(FRotator::ZeroRotator);
		SetActorTickEnabled(false);
	}
}

void AHitmenLocker::UpdateStep()
{
	AHitmenCharacterBase* Character = SteppingCharacter.Get();
	if (!Character)
	{
		return;
	}
	const float T = FMath::Clamp((GetWorld()->GetTimeSeconds() - DoorCycleStart - StepDelay) / StepTime, 0.f, 1.f);
	const float Ease = FMath::InterpEaseInOut(0.f, 1.f, T, 2.f);
	Character->SetActorLocation(FMath::Lerp(StepFrom, StepTo, Ease), false, nullptr, ETeleportType::TeleportPhysics);
	if (APlayerController* PC = Cast<APlayerController>(Character->GetController()); PC && PC->IsLocalController())
	{
		PC->SetControlRotation(FMath::Lerp(StepFromRotation, StepToRotation, Ease));
	}
	if (T >= 1.f)
	{
		SteppingCharacter.Reset();
		if (!bSteppingIn)
		{
			// Out: walk again.
			Character->MoveIgnoreActorRemove(this);
			if (!Character->IsDead())
			{
				Character->GetCharacterMovement()->SetMovementMode(MOVE_Falling);
			}
		}
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
	// Fixtures are never vaulted, climbed or hung from.
	Tags.AddUnique(HitmenTags::NoTraversal);
	InteractionText = NSLOCTEXT("Hitmen", "Distract", "Turn on");
	NoiseRadius = 1000.f;
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	PlayingLight = CreateDefaultSubobject<UPointLightComponent>(TEXT("PlayingLight"));
	PlayingLight->SetupAttachment(Root);
	PlayingLight->SetRelativeLocation(FVector(0.f, 0.f, 60.f));
	PlayingLight->SetIntensity(0.f);
	PlayingLight->SetAttenuationRadius(350.f);
	PlayingLight->SetCastShadows(false);
	PlayingLight->SetVolumetricScatteringIntensity(0.f);
}

void AHitmenDistraction::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenDistraction, bOn);
}

FText AHitmenDistraction::GetInteractionText_Implementation(AActor* Interactor) const
{
	return bOn ? NSLOCTEXT("Hitmen", "DistractOff", "Turn off") : InteractionText;
}

void AHitmenDistraction::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!bOn)
	{
		PlayingLight->SetIntensity(0.f);
		SetActorTickEnabled(false);
		return;
	}
	// Pulses with the beat while it plays.
	const float Beat = 0.5f + 0.5f * FMath::Sin(GetWorld()->GetTimeSeconds() * PI * 4.f);
	PlayingLight->SetIntensity(PlayingLightIntensity * (0.35f + 0.65f * Beat));
}

void AHitmenDistraction::Pulse()
{
	if (!bOn)
	{
		GetWorldTimerManager().ClearTimer(PulseTimer);
		return;
	}
	if (GetNetMode() != NM_DedicatedServer && PulseFX)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, PulseFX, GetActorLocation() + FVector::UpVector * 40.f, FRotator::ZeroRotator, FVector(0.35f));
	}
	if (!HasAuthority())
	{
		return;
	}
	if (PlayTime > 0.f && GetWorld()->GetTimeSeconds() >= PlayingUntil)
	{
		SetOn(false, nullptr);
		return;
	}
	// Heard only by guards in range (walls cut it, the nearest come); a guard who came to look switches it off.
	UHitmenAISubsystem::ReportNoise(this, GetActorLocation(), NoiseRadius, this, EHitmenNoiseType::Distraction);
	for (TActorIterator<AHitmenNPC> It(GetWorld()); It; ++It)
	{
		AHitmenNPC* NPC = *It;
		const UHitmenAlertComponent* Alert = NPC->GetAlert();
		if (NPC->IsDead() || !Alert || FVector::Dist2D(NPC->GetActorLocation(), GetActorLocation()) > SwitchOffRadius)
		{
			continue;
		}
		const EHitmenAlertState State = Alert->GetAlertState();
		if (State == EHitmenAlertState::Investigating || State == EHitmenAlertState::Searching)
		{
			NPC->PlayAnimSetMontage(EHitmenAnimMontage::SearchLookAround);
			SetOn(false, NPC);
			return;
		}
	}
}

bool AHitmenDistraction::CanRemoteActivate_Implementation() const
{
	return GetWorld()->GetTimeSeconds() - LastTriggerTime >= Cooldown;
}

void AHitmenDistraction::RemoteActivate_Implementation(AActor* Activator)
{
	if (HasAuthority() && CanRemoteActivate_Implementation())
	{
		SetOn(!bOn, Activator);
	}
}

void AHitmenDistraction::OnInteracted(AActor* Interactor)
{
	if (CanRemoteActivate_Implementation())
	{
		SetOn(!bOn, Interactor);
	}
}

void AHitmenDistraction::SetOn(bool bNewOn, AActor* SwitchedBy)
{
	if (!HasAuthority() || bOn == bNewOn)
	{
		return;
	}
	LastTriggerTime = GetWorld()->GetTimeSeconds();
	bOn = bNewOn;
	OnRep_On();
}

void AHitmenDistraction::OnRep_On()
{
	// Every machine: on = the loop, a pulsing light and an effect on each pulse; off = silence.
	PlayingLight->SetLightColor(LightColor);
	if (bOn)
	{
		PlayingUntil = GetWorld()->GetTimeSeconds() + PlayTime;
		SetActorTickEnabled(true);
		if (!PlayingAudio && GetNetMode() != NM_DedicatedServer)
		{
			USoundBase* Loop = LoopSound ? LoopSound.Get() : UHitmenSoundLibrary::GetHitmenSound(EHitmenSound::RadioLoop);
			if (Loop)
			{
				PlayingAudio = UGameplayStatics::SpawnSoundAttached(Loop, Root, NAME_None, FVector::ZeroVector, EAttachLocation::KeepRelativeOffset, false, 1.f, 1.f, 0.f,
					UHitmenSoundLibrary::GetSpaceAttenuation(EHitmenSoundSpace::World));
			}
		}
		Pulse();
		GetWorldTimerManager().SetTimer(PulseTimer, this, &AHitmenDistraction::Pulse, PulseInterval, true);
	}
	else
	{
		GetWorldTimerManager().ClearTimer(PulseTimer);
		if (PlayingAudio)
		{
			PlayingAudio->FadeOut(0.3f, 0.f);
			PlayingAudio = nullptr;
		}
	}
	Juice_OnTriggered();
}
