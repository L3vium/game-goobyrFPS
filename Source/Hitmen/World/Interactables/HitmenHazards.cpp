// Hitmen — hazards

#include "World/Interactables/HitmenHazards.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "TimerManager.h"

// ---------------------------------------------------------------------------------------------
// Room
// ---------------------------------------------------------------------------------------------

AHitmenRoomVolume::AHitmenRoomVolume()
{
	bReplicates = true;
	Bounds = CreateDefaultSubobject<UBoxComponent>(TEXT("Bounds"));
	Bounds->SetBoxExtent(FVector(400.f, 400.f, 150.f));
	Bounds->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	Bounds->SetGenerateOverlapEvents(true);
	SetRootComponent(Bounds);
}

bool AHitmenRoomVolume::IsSealed() const
{
	for (const AActor* Opening : Openings)
	{
		if (Opening && Opening->GetClass()->ImplementsInterface(UHitmenSealable::StaticClass()) && !IHitmenSealable::Execute_IsSealed(Opening))
		{
			return false;
		}
	}
	return true;
}

bool AHitmenRoomVolume::TryPoison(AActor* Poisoner)
{
	if (!HasAuthority() || !IsSealed())
	{
		return false;
	}
	TArray<AActor*> Inside;
	Bounds->GetOverlappingActors(Inside, AHitmenCharacterBase::StaticClass());
	for (AActor* Actor : Inside)
	{
		if (AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Actor); Character && !Character->IsDead())
		{
			Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Poisoned, PoisonDuration, PoisonMagnitude, Poisoner);
		}
	}
	Multicast_Gassed();
	return true;
}

void AHitmenRoomVolume::Multicast_Gassed_Implementation()
{
	Juice_OnGassed();
}

// ---------------------------------------------------------------------------------------------
// Drink
// ---------------------------------------------------------------------------------------------

AHitmenPoisonableDrink::AHitmenPoisonableDrink()
{
	bUsableWithInteract = false;
}

void AHitmenPoisonableDrink::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenPoisonableDrink, bPoisoned);
}

void AHitmenPoisonableDrink::ApplyPoison_Implementation(AActor* Poisoner)
{
	if (HasAuthority())
	{
		bPoisoned = true;
		PoisonedBy = Poisoner;
	}
}

void AHitmenPoisonableDrink::NPCUse_Implementation(AHitmenCharacterBase* NPC)
{
	if (!HasAuthority() || !NPC || !bPoisoned)
	{
		return;
	}
	NPC->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Poisoned, PoisonDuration, PoisonMagnitude, PoisonedBy);
	bPoisoned = false;
	PoisonedBy = nullptr;
}

// ---------------------------------------------------------------------------------------------
// Barrel
// ---------------------------------------------------------------------------------------------

AHitmenExplosiveBarrel::AHitmenExplosiveBarrel()
{
	bUsableWithInteract = false;
	Mesh->SetCollisionProfileName(TEXT("BlockAll"));
	Mesh->SetCollisionResponseToChannel(ECC_GameTraceChannel2, ECR_Block);
}

float AHitmenExplosiveBarrel::TakeDamage(float DamageAmount, FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser)
{
	if (!HasAuthority() || bExploded)
	{
		return 0.f;
	}
	Health -= DamageAmount;
	if (Health <= 0.f)
	{
		Explode(EventInstigator);
	}
	return DamageAmount;
}

void AHitmenExplosiveBarrel::ReceiveExplosion_Implementation(const FVector& Origin, float Radius, AActor* InstigatorActor)
{
	if (!HasAuthority() || bExploded || GetWorldTimerManager().IsTimerActive(ChainTimer))
	{
		return;
	}
	// Weak: the instigating player may leave before the chain delay ends.
	TWeakObjectPtr<AController> InstigatorController = InstigatorActor ? InstigatorActor->GetInstigatorController() : nullptr;
	GetWorldTimerManager().SetTimer(ChainTimer, FTimerDelegate::CreateWeakLambda(this, [this, InstigatorController]() { Explode(InstigatorController.Get()); }), ChainDelay, false);
}

void AHitmenExplosiveBarrel::Explode(AController* InstigatorController)
{
	if (bExploded)
	{
		return;
	}
	bExploded = true;
	Multicast_Exploded();
	UHitmenCombatLibrary::Explode(this, GetActorLocation(), ExplosionRadius, ExplosionDamage, this, InstigatorController);
	SetLifeSpan(0.2f);
}

void AHitmenExplosiveBarrel::Multicast_Exploded_Implementation()
{
	Juice_OnExploded();
	SetActorHiddenInGame(true);
	SetActorEnableCollision(false);
}

// ---------------------------------------------------------------------------------------------
// Breakable wall
// ---------------------------------------------------------------------------------------------

AHitmenBreakableWall::AHitmenBreakableWall()
{
	bReplicates = true;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	SetRootComponent(Mesh);
}

void AHitmenBreakableWall::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenBreakableWall, bBroken);
}

void AHitmenBreakableWall::ReceiveExplosion_Implementation(const FVector& Origin, float Radius, AActor* InstigatorActor)
{
	Break();
}

void AHitmenBreakableWall::Break()
{
	if (HasAuthority() && !bBroken)
	{
		bBroken = true;
		OnRep_Broken();
	}
}

void AHitmenBreakableWall::OnRep_Broken()
{
	if (bBroken)
	{
		Mesh->SetVisibility(false);
		Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Juice_OnBroken();
	}
}
