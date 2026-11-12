// Hitmen — ability actors

#include "Abilities/HitmenAbilityActors.h"
#include "Abilities/HitmenAbility.h"
#include "Data/HitmenSoundSet.h"
#include "AI/HitmenAlertComponent.h"
#include "Characters/HitmenNPC.h"
#include "NiagaraFunctionLibrary.h"
#include "EngineUtils.h"
#include "Abilities/HitmenAbilityComponent.h"
#include "Data/HitmenAbilityData.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "AI/HitmenAISubsystem.h"
#include "Core/HitmenTeamLibrary.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "NiagaraComponent.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInterface.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "TimerManager.h"
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
	PuffMesh = TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(TEXT("/Engine/BasicShapes/Sphere.Sphere")));
	PuffMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Hitmen/Materials/M_Smoke.M_Smoke")));
}

void AHitmenSmokeCloud::BeginPlay()
{
	Super::BeginPlay();
	SetLifeSpan(Lifetime);
	UHitmenAISubsystem::RegisterSmoke(this);
	SpawnTime = GetWorld()->GetTimeSeconds();

	const bool bHasNiagara = Effect && Effect->GetAsset() != nullptr;
	if (bHasNiagara)
	{
		// The cloud fills its radius quickly (the sight block is there at once).
		Effect->SetCustomTimeDilation(EffectTimeScale);
		Effect->SetRelativeScale3D(FVector(EffectScale));
	}
	if (GetNetMode() != NM_DedicatedServer && PuffCount > 0 && !(bHasNiagara && bHidePuffsWhenNiagaraAssigned))
	{
		BuildPuffs();
	}
}

void AHitmenSmokeCloud::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	GetWorld()->GetTimerManager().ClearTimer(PuffTimer);
	UHitmenAISubsystem::UnregisterSmoke(this);
	Super::EndPlay(EndPlayReason);
}

void AHitmenSmokeCloud::BuildPuffs()
{
	UStaticMesh* Mesh = PuffMesh.LoadSynchronous();
	if (!Mesh)
	{
		return;
	}
	if (UMaterialInterface* Material = PuffMaterial.IsNull() ? nullptr : PuffMaterial.LoadSynchronous())
	{
		PuffMaterialInstance = UMaterialInstanceDynamic::Create(Material, this);
	}
	const float MeshRadius = FMath::Max(1.f, Mesh->GetBounds().SphereRadius);

	// Seeded from the replicated spawn point so every machine builds a similar cloud.
	const FIntVector Cell(FMath::RoundToInt32(GetActorLocation().X), FMath::RoundToInt32(GetActorLocation().Y), FMath::RoundToInt32(GetActorLocation().Z));
	FRandomStream Random(static_cast<int32>(GetTypeHash(Cell)));

	for (int32 Index = 0; Index < PuffCount; ++Index)
	{
		FVector Offset = Random.GetUnitVector() * FMath::Pow(Random.GetFraction(), 1.f / 3.f) * Radius * PuffSpread;
		Offset.Z = FMath::Abs(Offset.Z) * PuffVerticalSpread;
		const float PuffRadius = Random.FRandRange(PuffMinRadius, FMath::Max(PuffMinRadius, PuffMaxRadius));

		UStaticMeshComponent* Puff = NewObject<UStaticMeshComponent>(this);
		Puff->SetStaticMesh(Mesh);
		Puff->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Puff->SetCastShadow(false);
		Puff->SetCanEverAffectNavigation(false);
		Puff->SetupAttachment(GetRootComponent());
		Puff->SetRelativeLocation(Offset);
		Puff->SetRelativeScale3D(FVector(KINDA_SMALL_NUMBER));
		if (PuffMaterialInstance)
		{
			for (int32 MaterialIndex = 0; MaterialIndex < Puff->GetNumMaterials(); ++MaterialIndex)
			{
				Puff->SetMaterial(MaterialIndex, PuffMaterialInstance);
			}
		}
		Puff->RegisterComponent();
		Puffs.Add(Puff);
		PuffScales.Add(FVector(PuffRadius / MeshRadius));
	}
	UpdatePuffs();
	GetWorld()->GetTimerManager().SetTimer(PuffTimer, this, &AHitmenSmokeCloud::UpdatePuffs, PuffUpdateInterval, true);
}

void AHitmenSmokeCloud::UpdatePuffs()
{
	const float Age = GetWorld()->GetTimeSeconds() - SpawnTime;
	const float Grow = PuffGrowTime > 0.f ? FMath::Clamp(Age / PuffGrowTime, 0.f, 1.f) : 1.f;
	const float FadeStart = FMath::Max(0.f, Lifetime - PuffFadeOutTime);
	const float Fade = PuffFadeOutTime > 0.f ? FMath::Clamp((Age - FadeStart) / PuffFadeOutTime, 0.f, 1.f) : 0.f;
	// Ease out on the way in, linear on the way out.
	const float ScaleAlpha = FMath::InterpEaseOut(0.f, 1.f, Grow, 2.f) * FMath::Lerp(1.f, PuffFadeOutScale, Fade);

	for (int32 Index = 0; Index < Puffs.Num(); ++Index)
	{
		if (UStaticMeshComponent* Puff = Puffs[Index])
		{
			Puff->SetRelativeScale3D(PuffScales[Index] * FMath::Max(ScaleAlpha, KINDA_SMALL_NUMBER));
		}
	}
	if (PuffMaterialInstance)
	{
		PuffMaterialInstance->SetScalarParameterValue(PuffOpacityParameter, 1.f - Fade);
	}
	if (Grow >= 1.f && Fade <= 0.f && Age < FadeStart - PuffUpdateInterval)
	{
		// Fully grown: idle until the fade starts.
		GetWorld()->GetTimerManager().SetTimer(PuffTimer, this, &AHitmenSmokeCloud::UpdatePuffs, PuffUpdateInterval, true, FadeStart - Age);
	}
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
	ArmTime = GetWorld()->GetTimeSeconds() + ArmDelay;
	if (HasAuthority())
	{
		Trigger->OnComponentBeginOverlap.AddDynamic(this, &AHitmenTrap::HandleOverlap);
	}
}

void AHitmenTrap::HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	// Anyone steps in: guards, other players, and whoever placed it (once it is armed).
	AHitmenCharacterBase* Victim = Cast<AHitmenCharacterBase>(OtherActor);
	if (bTriggered || !Victim || Victim->IsDead() || GetWorld()->GetTimeSeconds() < ArmTime)
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
	if (!Ability)
	{
		return FText::GetEmpty();
	}
	const UHitmenAbilityComponent* Abilities = Interactor ? Interactor->FindComponentByClass<UHitmenAbilityComponent>() : nullptr;
	const UHitmenAbility* Current = Abilities ? Abilities->GetAbility(Ability->Slot) : nullptr;
	if (Current && Current->GetData() && Current->GetData() != Ability)
	{
		return FText::Format(NSLOCTEXT("Hitmen", "SwapAbility", "Swap for {0}"), Ability->DisplayName);
	}
	return FText::Format(NSLOCTEXT("Hitmen", "TakeAbility", "Take {0}"), Ability->DisplayName);
}

void AHitmenAbilityPickup::OnInteracted(AActor* Interactor)
{
	UHitmenAbilityComponent* Abilities = Interactor ? Interactor->FindComponentByClass<UHitmenAbilityComponent>() : nullptr;
	if (!Abilities || !Ability)
	{
		return;
	}
	// A swap: the ability you had stays here in its place.
	const UHitmenAbility* Current = Abilities->GetAbility(Ability->Slot);
	UHitmenAbilityData* Previous = Current ? Current->GetData() : nullptr;
	Abilities->SetAbility(Ability);
	if (Previous && Previous != Ability)
	{
		Ability = Previous;
		ForceNetUpdate();
		return;
	}
	Destroy();
}

// ---------------------------------------------------------------------------------------------
// Stone: lands, rests, makes a noise; a guard who comes to look picks it up
// ---------------------------------------------------------------------------------------------

AHitmenStone::AHitmenStone()
{
	PrimaryActorTick.bCanEverTick = true;
	bExplodeOnImpact = false;
	bStickOnImpact = false;
	Movement->bShouldBounce = true;
	Movement->Bounciness = 0.25f;
	Movement->Friction = 0.6f;
	ImpactNoiseType = EHitmenNoiseType::Distraction;
}

bool AHitmenStone::OnImpact(const FHitResult& Hit)
{
	// Only the first impact makes the noise; it then bounces and rests where it stops.
	if (!bNoiseMade)
	{
		bNoiseMade = true;
		if (ImpactNoiseRadius > 0.f)
		{
			UHitmenAISubsystem::ReportNoise(this, Hit.ImpactPoint, ImpactNoiseRadius, GetInstigator(), EHitmenNoiseType::Distraction);
		}
		Multicast_StoneNoise(Hit.ImpactPoint);
		SetLifeSpan(RestLifetime);
	}
	return false;
}

void AHitmenStone::Multicast_StoneNoise_Implementation(FVector_NetQuantize Location)
{
	if (GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	if (NoiseFX)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, NoiseFX, Location, FRotator::ZeroRotator, FVector(0.6f));
	}
	if (LandSound)
	{
		UHitmenSoundLibrary::PlaySoundInSpace(this, LandSound, Location, EHitmenSoundSpace::Impact);
	}
}

void AHitmenStone::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!HasAuthority() || !bNoiseMade || GetWorld()->GetTimeSeconds() < NextPickupCheck)
	{
		return;
	}
	NextPickupCheck = GetWorld()->GetTimeSeconds() + 0.4f;
	// A guard that came to look (investigating / searching) and stands next to it picks it up.
	for (TActorIterator<AHitmenNPC> It(GetWorld()); It; ++It)
	{
		AHitmenNPC* NPC = *It;
		const UHitmenAlertComponent* Alert = NPC->GetAlert();
		if (NPC->IsDead() || !Alert || FVector::Dist2D(NPC->GetActorLocation(), GetActorLocation()) > PickupRadius)
		{
			continue;
		}
		const EHitmenAlertState State = Alert->GetAlertState();
		if (State == EHitmenAlertState::Investigating || State == EHitmenAlertState::Searching)
		{
			NPC->PlayAnimSetMontage(EHitmenAnimMontage::SearchLookAround);
			Destroy();
			return;
		}
	}
}

// ---------------------------------------------------------------------------------------------
// Wind grenade
// ---------------------------------------------------------------------------------------------

AHitmenProjectile_Wind::AHitmenProjectile_Wind()
{
	bExplodeOnImpact = true;
	ExplosionDamage = 0.f;
	ExplosionRadius = BlastRadius;
	ExplosionNoiseRadius = 1500.f;
}

void AHitmenProjectile_Wind::OnExplode(const FVector& Origin)
{
	// Everyone around is blown away from the burst, the thrower included; loose physics objects too.
	TArray<FOverlapResult> Overlaps;
	GetWorld()->OverlapMultiByObjectType(Overlaps, Origin, FQuat::Identity, FCollisionObjectQueryParams(FCollisionObjectQueryParams::AllDynamicObjects), FCollisionShape::MakeSphere(BlastRadius));
	TSet<AActor*> Done;
	for (const FOverlapResult& Overlap : Overlaps)
	{
		AActor* Actor = Overlap.GetActor();
		if (!Actor || Done.Contains(Actor))
		{
			continue;
		}
		Done.Add(Actor);
		const FVector Away = ((Actor->GetActorLocation() - Origin).GetSafeNormal2D() + FVector::UpVector * 0.6f).GetSafeNormal();
		if (ACharacter* Character = Cast<ACharacter>(Actor))
		{
			Character->LaunchCharacter(Away * KnockbackSpeed, true, true);
		}
		else if (UPrimitiveComponent* Primitive = Overlap.GetComponent(); Primitive && Primitive->IsSimulatingPhysics())
		{
			Primitive->AddImpulse(Away * KnockbackSpeed * Primitive->GetMass());
		}
	}
	UHitmenAISubsystem::ReportNoise(this, Origin, ExplosionNoiseRadius, GetInstigator(), EHitmenNoiseType::Explosion);
}
