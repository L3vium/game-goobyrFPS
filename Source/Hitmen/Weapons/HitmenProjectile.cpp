// Hitmen — projectiles

#include "Weapons/HitmenProjectile.h"
#include "NiagaraComponent.h"
#include "GameFramework/PlayerController.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "Data/HitmenSoundSet.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Abilities/HitmenAbilityActors.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "Hitmen.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/ProjectileMovementComponent.h"
#include "GameFramework/Character.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Kismet/GameplayStatics.h"
#include "NiagaraFunctionLibrary.h"
#include "Engine/OverlapResult.h"
#include "Engine/World.h"
#include "TimerManager.h"

AHitmenProjectile::AHitmenProjectile()
{
	PrimaryActorTick.bCanEverTick = false;
	bReplicates = true;
	SetReplicatingMovement(true);

	Collision = CreateDefaultSubobject<USphereComponent>(TEXT("Collision"));
	Collision->InitSphereRadius(8.f);
	Collision->SetCollisionProfileName(TEXT("Projectile"));
	Collision->SetCollisionResponseToChannel(ECC_Pawn, ECR_Block);
	Collision->bReturnMaterialOnMove = true;
	Collision->CanCharacterStepUpOn = ECB_No;
	SetRootComponent(Collision);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Collision);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	Movement = CreateDefaultSubobject<UProjectileMovementComponent>(TEXT("Movement"));
	Movement->UpdatedComponent = Collision;
	Movement->InitialSpeed = 0.f;
	Movement->MaxSpeed = 0.f;
	Movement->bRotationFollowsVelocity = true;
	Movement->bShouldBounce = false;
	Movement->ProjectileGravityScale = 1.f;
}

void AHitmenProjectile::BeginPlay()
{
	Super::BeginPlay();
	if (APawn* InstigatorPawn = GetInstigator())
	{
		Collision->IgnoreActorWhenMoving(InstigatorPawn, true);
	}
	if (AActor* OwnerActor = GetOwner())
	{
		Collision->IgnoreActorWhenMoving(OwnerActor, true);
	}
	if (TrailEffect && GetNetMode() != NM_DedicatedServer)
	{
		UNiagaraFunctionLibrary::SpawnSystemAttached(TrailEffect, Mesh, NAME_None, FVector::ZeroVector, FRotator::ZeroRotator, EAttachLocation::SnapToTarget, true);
	}
	if (HasAuthority())
	{
		Collision->OnComponentHit.AddDynamic(this, &AHitmenProjectile::HandleHit);
		SetLifeSpan(MaxLifetime);
		if (FuseTime > 0.f)
		{
			GetWorldTimerManager().SetTimer(FuseTimer, this, &AHitmenProjectile::Detonate, FuseTime, false);
		}
	}
}

void AHitmenProjectile::Launch(const FVector& Velocity, float InDamage)
{
	if (InDamage > 0.f)
	{
		Damage = InDamage;
	}
	Movement->Velocity = Velocity;
	Movement->MaxSpeed = FMath::Max(Movement->MaxSpeed, Velocity.Size() * 2.f);
}

AController* AHitmenProjectile::GetInstigatorControllerSafe() const
{
	return GetInstigator() ? GetInstigator()->GetController() : nullptr;
}

void AHitmenProjectile::HandleHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult& Hit)
{
	if (bConsumed || OtherActor == GetInstigator())
	{
		return;
	}
	if (OnImpact(Hit))
	{
		bConsumed = true;
		Destroy();
	}
}

bool AHitmenProjectile::OnImpact(const FHitResult& Hit)
{
	AActor* OtherActor = Hit.GetActor();

	if (Damage > 0.f && OtherActor)
	{
		UGameplayStatics::ApplyPointDamage(OtherActor, Damage, GetVelocity().GetSafeNormal(), Hit, GetInstigatorControllerSafe(), this, UDamageType::StaticClass());
	}
	if (StatusOnHit != EHitmenStatusEffect::None)
	{
		if (AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OtherActor))
		{
			Character->GetStatusEffects()->ApplyStatus(StatusOnHit, StatusDuration, 1.f, GetInstigator());
		}
	}
	if (ImpactNoiseRadius > 0.f)
	{
		UHitmenAISubsystem::ReportNoise(this, Hit.ImpactPoint, ImpactNoiseRadius, GetInstigator(), ImpactNoiseType);
	}

	if (bExplodeOnImpact)
	{
		Detonate();
		return false; // Detonate destroys.
	}

	Multicast_Impact(Hit.ImpactPoint, false);

	if (bStickOnImpact)
	{
		Movement->StopMovementImmediately();
		Movement->ProjectileGravityScale = 0.f;
		Collision->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		if (USceneComponent* Target = Hit.GetComponent())
		{
			AttachToComponent(Target, FAttachmentTransformRules::KeepWorldTransform, Hit.BoneName);
		}
		return false;
	}
	// Bouncing grenades keep flying until the fuse ends.
	return !(FuseTime > 0.f && Movement->bShouldBounce);
}

void AHitmenProjectile::Detonate()
{
	if (!HasAuthority() || bConsumed)
	{
		return;
	}
	bConsumed = true;
	const FVector Origin = GetActorLocation();
	OnExplode(Origin);
	Multicast_Impact(Origin, true);
	SetLifeSpan(0.1f);
	SetActorHiddenInGame(true);
	SetActorEnableCollision(false);
}

void AHitmenProjectile::OnExplode(const FVector& Origin)
{
	UHitmenCombatLibrary::Explode(this, Origin, ExplosionRadius, ExplosionDamage, this, GetInstigatorControllerSafe(), ExplosionNoiseRadius);
}

void AHitmenProjectile::Multicast_Impact_Implementation(FVector_NetQuantize Location, bool bExploded)
{
	if (GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	UNiagaraSystem* Effect = bExploded ? ExplosionEffect.Get() : ImpactEffect.Get();
	USoundBase* Sound = bExploded ? ExplosionSound.Get() : ImpactSound.Get();
	if (Effect)
	{
		const FVector Scale(bExploded ? ExplosionEffectScale : 1.f);
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Effect, Location, FRotator::ZeroRotator, Scale);
	}
	if (Sound)
	{
		UGameplayStatics::PlaySoundAtLocation(this, Sound, Location);
	}
	else if (bExploded)
	{
		UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::Explosion, Location);
	}
	if (bExploded)
	{
		// Shake the view of nearby local players.
		for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
		{
			const APlayerController* PC = It->Get();
			AHitmenPlayerCharacter* Player = PC && PC->IsLocalController() ? Cast<AHitmenPlayerCharacter>(PC->GetPawn()) : nullptr;
			const float Distance = Player ? FVector::Dist(Player->GetActorLocation(), Location) : BIG_NUMBER;
			if (Player && Distance < ExplosionShakeRadius)
			{
				Player->AddCameraShake(ExplosionShakeIntensity * (1.f - Distance / ExplosionShakeRadius), 0.6f);
			}
		}
		Juice_OnExplode(Location);
	}
	else
	{
		Juice_OnImpact(Location);
	}
}

// ---------------------------------------------------------------------------------------------
// Black hole
// ---------------------------------------------------------------------------------------------

AHitmenProjectile_BlackHole::AHitmenProjectile_BlackHole()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	MaxLifetime = 15.f;
}

bool AHitmenProjectile_BlackHole::OnImpact(const FHitResult& Hit)
{
	Activate();
	return false;
}

void AHitmenProjectile_BlackHole::Activate()
{
	if (bActive || !HasAuthority())
	{
		return;
	}
	bActive = true;
	ActiveTime = 0.f;
	Movement->StopMovementImmediately();
	Movement->ProjectileGravityScale = 0.f;
	Collision->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	SetActorTickEnabled(true);
	UHitmenAISubsystem::ReportNoise(this, GetActorLocation(), 1500.f, GetInstigator(), EHitmenNoiseType::Explosion);
	Multicast_Activated(GetActorLocation());
}

void AHitmenProjectile_BlackHole::Multicast_Activated_Implementation(FVector_NetQuantize Location)
{
	Juice_OnActivated();
}

void AHitmenProjectile_BlackHole::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!HasAuthority())
	{
		return;
	}
	if (!bActive)
	{
		ActiveTime += DeltaSeconds;
		if (ActiveTime >= ArmDelay)
		{
			Activate();
		}
		return;
	}

	ActiveTime += DeltaSeconds;
	const FVector Center = GetActorLocation();
	TArray<FOverlapResult> Overlaps;
	GetWorld()->OverlapMultiByObjectType(Overlaps, Center, FQuat::Identity, FCollisionObjectQueryParams(FCollisionObjectQueryParams::AllDynamicObjects), FCollisionShape::MakeSphere(PullRadius));
	TSet<AActor*> Done;
	for (const FOverlapResult& Overlap : Overlaps)
	{
		AActor* Actor = Overlap.GetActor();
		if (!Actor || Done.Contains(Actor))
		{
			continue;
		}
		Done.Add(Actor);
		const FVector ToCenter = Center - Actor->GetActorLocation();
		const float Distance = ToCenter.Size();
		const float Strength = PullStrength * (1.f - FMath::Clamp(Distance / PullRadius, 0.f, 1.f));
		if (ACharacter* Character = Cast<ACharacter>(Actor))
		{
			Character->LaunchCharacter(ToCenter.GetSafeNormal() * Strength * DeltaSeconds * 10.f, false, false);
			if (Distance < CoreRadius)
			{
				UGameplayStatics::ApplyDamage(Character, CoreDamagePerSecond * DeltaSeconds, GetInstigatorControllerSafe(), this, UDamageType::StaticClass());
			}
		}
		else if (UPrimitiveComponent* Primitive = Overlap.GetComponent(); Primitive && Primitive->IsSimulatingPhysics())
		{
			Primitive->AddForce(ToCenter.GetSafeNormal() * Strength * Primitive->GetMass() * 3.f);
		}
	}
	if (ActiveTime >= Duration)
	{
		Detonate();
	}
}

// ---------------------------------------------------------------------------------------------
// Flashbang
// ---------------------------------------------------------------------------------------------

AHitmenProjectile_Flashbang::AHitmenProjectile_Flashbang()
{
	FuseTime = 1.5f;
	ExplosionDamage = 0.f;
	ExplosionRadius = 900.f;
	ExplosionNoiseRadius = 2500.f;
	Movement->bShouldBounce = true;
	Movement->Bounciness = 0.35f;
}

void AHitmenProjectile_Flashbang::OnExplode(const FVector& Origin)
{
	TArray<FOverlapResult> Overlaps;
	GetWorld()->OverlapMultiByChannel(Overlaps, Origin, FQuat::Identity, ECC_Pawn, FCollisionShape::MakeSphere(ExplosionRadius));
	TSet<AActor*> Done;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenFlash), false, this);
	for (const FOverlapResult& Overlap : Overlaps)
	{
		AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Overlap.GetActor());
		if (!Character || Done.Contains(Character) || Character->IsDead())
		{
			continue;
		}
		Done.Add(Character);
		FHitResult Block;
		const bool bVisible = !GetWorld()->LineTraceSingleByChannel(Block, Origin, Character->GetPawnViewLocation(), ECC_Visibility, Params) || Block.GetActor() == Character;
		if (!bVisible)
		{
			continue;
		}
		Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Stunned, StunDuration, 1.f, GetInstigator());
		if (Character->IsPlayerControlled())
		{
			Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Blinded, BlindDuration, 1.f, GetInstigator());
		}
	}
	UHitmenAISubsystem::ReportNoise(this, Origin, ExplosionNoiseRadius, GetInstigator(), EHitmenNoiseType::Explosion);
}

// ---------------------------------------------------------------------------------------------
// Smoke
// ---------------------------------------------------------------------------------------------

AHitmenProjectile_Smoke::AHitmenProjectile_Smoke()
{
	FuseTime = 1.2f;
	ExplosionDamage = 0.f;
	ExplosionNoiseRadius = 600.f;
	Movement->bShouldBounce = true;
	Movement->Bounciness = 0.3f;
	SmokeClass = AHitmenSmokeCloud::StaticClass();
}

void AHitmenProjectile_Smoke::OnExplode(const FVector& Origin)
{
	FActorSpawnParameters Params;
	Params.Instigator = GetInstigator();
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	GetWorld()->SpawnActor<AHitmenSmokeCloud>(SmokeClass ? SmokeClass.Get() : AHitmenSmokeCloud::StaticClass(), Origin, FRotator::ZeroRotator, Params);
}
