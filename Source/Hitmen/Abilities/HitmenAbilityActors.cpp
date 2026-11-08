// Hitmen — ability actors

#include "Abilities/HitmenAbilityActors.h"
#include "Abilities/HitmenAbilityComponent.h"
#include "Data/HitmenAbilityData.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "Core/HitmenTeamLibrary.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "NiagaraComponent.h"
#include "GameFramework/ProjectileMovementComponent.h"
#include "Engine/OverlapResult.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "Hitmen.h"

// ---------------------------------------------------------------------------------------------
// Smoke
// ---------------------------------------------------------------------------------------------

AHitmenSmokeCloud::AHitmenSmokeCloud()
{
	bReplicates = true;
	Effect = CreateDefaultSubobject<UNiagaraComponent>(TEXT("Effect"));
	SetRootComponent(Effect);
}

void AHitmenSmokeCloud::BeginPlay()
{
	Super::BeginPlay();
	SetLifeSpan(Lifetime);
	UHitmenAISubsystem::RegisterSmoke(this);
}

void AHitmenSmokeCloud::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UHitmenAISubsystem::UnregisterSmoke(this);
	Super::EndPlay(EndPlayReason);
}

bool AHitmenSmokeCloud::BlocksSegment(const FVector& Start, const FVector& End) const
{
	const FVector Closest = FMath::ClosestPointOnSegment(GetActorLocation(), Start, End);
	return FVector::DistSquared(Closest, GetActorLocation()) < Radius * Radius;
}

// ---------------------------------------------------------------------------------------------
// Trap
// ---------------------------------------------------------------------------------------------

AHitmenTrap::AHitmenTrap()
{
	bReplicates = true;
	Trigger = CreateDefaultSubobject<USphereComponent>(TEXT("Trigger"));
	Trigger->InitSphereRadius(60.f);
	Trigger->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	SetRootComponent(Trigger);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Trigger);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void AHitmenTrap::BeginPlay()
{
	Super::BeginPlay();
	if (HasAuthority())
	{
		Trigger->OnComponentBeginOverlap.AddDynamic(this, &AHitmenTrap::HandleOverlap);
	}
}

void AHitmenTrap::HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	AHitmenCharacterBase* Victim = Cast<AHitmenCharacterBase>(OtherActor);
	if (bTriggered || !Victim || Victim->IsDead() || !UHitmenTeamLibrary::AreHostile(GetInstigator(), Victim))
	{
		return;
	}
	bTriggered = true;
	const float TrapDuration = Victim->IsPlayerControlled() ? PlayerTrapDuration : NPCTrapDuration;
	Victim->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Trapped, TrapDuration, 1.f, GetInstigator());
	Multicast_Triggered(Victim);
	SetLifeSpan(TrapDuration + 0.5f);
}

void AHitmenTrap::Multicast_Triggered_Implementation(AActor* Victim)
{
	Juice_OnTriggered(Victim);
}

// ---------------------------------------------------------------------------------------------
// Wind mine
// ---------------------------------------------------------------------------------------------

AHitmenWindMine::AHitmenWindMine()
{
	bReplicates = true;
	Trigger = CreateDefaultSubobject<USphereComponent>(TEXT("Trigger"));
	Trigger->InitSphereRadius(150.f);
	Trigger->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	SetRootComponent(Trigger);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Trigger);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void AHitmenWindMine::BeginPlay()
{
	Super::BeginPlay();
	ArmTime = GetWorld()->GetTimeSeconds() + ArmDelay;
	if (HasAuthority())
	{
		Trigger->OnComponentBeginOverlap.AddDynamic(this, &AHitmenWindMine::HandleOverlap);
	}
}

void AHitmenWindMine::HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	if (bDone || GetWorld()->GetTimeSeconds() < ArmTime || !Cast<APawn>(OtherActor) || !UHitmenTeamLibrary::AreHostile(GetInstigator(), OtherActor))
	{
		return;
	}
	Blast();
}

void AHitmenWindMine::Blast()
{
	bDone = true;
	const FVector Center = GetActorLocation();
	TArray<FOverlapResult> Overlaps;
	GetWorld()->OverlapMultiByObjectType(Overlaps, Center, FQuat::Identity, FCollisionObjectQueryParams(FCollisionObjectQueryParams::AllDynamicObjects), FCollisionShape::MakeSphere(BlastRadius));
	TSet<AActor*> Done;
	for (const FOverlapResult& Overlap : Overlaps)
	{
		AActor* Actor = Overlap.GetActor();
		if (!Actor || Done.Contains(Actor))
		{
			continue;
		}
		Done.Add(Actor);
		const FVector Away = (Actor->GetActorLocation() - Center).GetSafeNormal2D() + FVector::UpVector * 0.6f;
		if (ACharacter* Character = Cast<ACharacter>(Actor))
		{
			Character->LaunchCharacter(Away.GetSafeNormal() * KnockbackSpeed, true, true);
		}
		else if (UPrimitiveComponent* Primitive = Overlap.GetComponent(); Primitive && Primitive->IsSimulatingPhysics())
		{
			Primitive->AddImpulse(Away.GetSafeNormal() * KnockbackSpeed * Primitive->GetMass());
		}
	}
	UHitmenAISubsystem::ReportNoise(this, Center, 1500.f, GetInstigator(), EHitmenNoiseType::Explosion);
	Multicast_Blast();
	SetLifeSpan(0.5f);
}

void AHitmenWindMine::Multicast_Blast_Implementation()
{
	Juice_OnBlast();
}

// ---------------------------------------------------------------------------------------------
// C4
// ---------------------------------------------------------------------------------------------

AHitmenC4Charge::AHitmenC4Charge()
{
	bStickOnImpact = true;
	bExplodeOnImpact = false;
	ExplosionRadius = 600.f;
	ExplosionDamage = 200.f;
	MaxLifetime = 600.f;
}

// ---------------------------------------------------------------------------------------------
// Ability pickup
// ---------------------------------------------------------------------------------------------

AHitmenAbilityPickup::AHitmenAbilityPickup()
{
	Mesh->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	Mesh->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
}

void AHitmenAbilityPickup::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenAbilityPickup, Ability);
}

void AHitmenAbilityPickup::InitPickup(UHitmenAbilityData* InAbility)
{
	Ability = InAbility;
}

bool AHitmenAbilityPickup::CanInteract_Implementation(AActor* Interactor) const
{
	return Ability && Interactor && Interactor->FindComponentByClass<UHitmenAbilityComponent>() && Super::CanInteract_Implementation(Interactor);
}

FText AHitmenAbilityPickup::GetInteractionText_Implementation(AActor* Interactor) const
{
	return Ability ? FText::Format(NSLOCTEXT("Hitmen", "TakeAbility", "Take {0}"), Ability->DisplayName) : FText::GetEmpty();
}

void AHitmenAbilityPickup::OnInteracted(AActor* Interactor)
{
	if (UHitmenAbilityComponent* Abilities = Interactor ? Interactor->FindComponentByClass<UHitmenAbilityComponent>() : nullptr)
	{
		Abilities->SetAbility(Ability);
		Destroy();
	}
}
