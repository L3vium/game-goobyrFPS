// Hitmen — stealth fixtures

#include "World/Interactables/HitmenStealth.h"
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
	InteractionText = NSLOCTEXT("Hitmen", "Hide", "Hide");
}

void AHitmenLocker::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenLocker, Occupant);
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
		Character->TeleportTo(GetActorTransform().TransformPosition(ExitOffset), Character->GetActorRotation());
		Character->GetCharacterMovement()->SetMovementMode(MOVE_Falling);
		Occupant = nullptr;
		return;
	}
	Occupant = Character;
	Character->GetCharacterMovement()->StopMovementImmediately();
	Character->TeleportTo(GetActorTransform().TransformPosition(InsideOffset), GetActorRotation());
	Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Hidden, 0.f, 1.f, this);
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
	// The noise comes from the device, not from the player.
	UHitmenAISubsystem::ReportNoise(this, GetActorLocation(), NoiseRadius, this, EHitmenNoiseType::Distraction);
	Multicast_Triggered();
}

void AHitmenDistraction::Multicast_Triggered_Implementation()
{
	Juice_OnTriggered();
}

AHitmenHackableDistraction::AHitmenHackableDistraction()
{
	InteractionText = NSLOCTEXT("Hitmen", "Hack", "Hack");
	NoiseRadius = 1200.f;
}

void AHitmenHackableDistraction::Trigger(AActor* Activator)
{
	LastTriggerTime = GetWorld()->GetTimeSeconds();
	// Silent: only NPCs that can see the device react.
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	if (AI)
	{
		FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenHackable), false, this);
		for (UHitmenAlertComponent* Alert : AI->GetAlertComponents())
		{
			const APawn* NPC = Cast<APawn>(Alert->GetOwner());
			if (!NPC)
			{
				continue;
			}
			const FVector Eye = NPC->GetPawnViewLocation();
			const float Distance = FVector::Dist(Eye, GetActorLocation());
			FHitResult Hit;
			Params.ClearIgnoredSourceObjects();
			Params.AddIgnoredActor(this);
			Params.AddIgnoredActor(NPC);
			if (Distance <= NoiseRadius && !GetWorld()->LineTraceSingleByChannel(Hit, Eye, GetActorLocation(), ECC_Visibility, Params))
			{
				Alert->HearNoise(GetActorLocation(), EHitmenNoiseType::Distraction, this, Distance);
			}
		}
	}
	Multicast_Triggered();
}
