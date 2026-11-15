// Hitmen — Goobyr gun hamster

#include "Characters/HitmenHamster.h"
#include "Characters/HitmenCharacterBase.h"
#include "Core/HitmenTeamLibrary.h"
#include "Data/HitmenSoundSet.h"
#include "AIController.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Kismet/GameplayStatics.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Net/UnrealNetwork.h"
#include "NiagaraFunctionLibrary.h"
#include "UObject/ConstructorHelpers.h"
#include "Engine/World.h"
#include "EngineUtils.h"

namespace
{
	UStaticMeshComponent* MakePart(AHitmenHamster* Owner, USceneComponent* Parent, const TCHAR* Name, UStaticMesh* Mesh, const FVector& Location, const FVector& Scale)
	{
		UStaticMeshComponent* Part = Owner->CreateDefaultSubobject<UStaticMeshComponent>(Name);
		Part->SetupAttachment(Parent);
		Part->SetStaticMesh(Mesh);
		Part->SetRelativeLocation(Location);
		Part->SetRelativeScale3D(Scale);
		Part->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Part->SetCanEverAffectNavigation(false);
		return Part;
	}
}

AHitmenHamster::AHitmenHamster(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = true;
	SetReplicateMovement(true);
	AIControllerClass = AAIController::StaticClass();
	AutoPossessAI = EAutoPossessAI::Spawned;

	GetCapsuleComponent()->InitCapsuleSize(12.f, 12.f);
	GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	GetCharacterMovement()->MaxWalkSpeed = RunSpeed;
	GetCharacterMovement()->bOrientRotationToMovement = true;
	GetCharacterMovement()->RotationRate = FRotator(0.f, 900.f, 0.f);
	GetCharacterMovement()->bUseControllerDesiredRotation = false;
	bUseControllerRotationYaw = false;
	GetMesh()->SetVisibility(false);

	// A round brown body, a head, two pink ears and a little tail: basic shapes, swappable in the Blueprint.
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cylinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	UStaticMesh* Ball = Sphere.Succeeded() ? Sphere.Object : nullptr;
	USceneComponent* RootPart = GetCapsuleComponent();
	Body = MakePart(this, RootPart, TEXT("Body"), Ball, FVector(0.f, 0.f, -2.f), FVector(0.3f, 0.22f, 0.2f));
	Head = MakePart(this, RootPart, TEXT("Head"), Ball, FVector(13.f, 0.f, 3.f), FVector(0.15f, 0.15f, 0.14f));
	EarLeft = MakePart(this, RootPart, TEXT("EarLeft"), Ball, FVector(13.f, -5.f, 10.f), FVector(0.05f, 0.03f, 0.06f));
	EarRight = MakePart(this, RootPart, TEXT("EarRight"), Ball, FVector(13.f, 5.f, 10.f), FVector(0.05f, 0.03f, 0.06f));
	Tail = MakePart(this, RootPart, TEXT("Tail"), Cylinder.Succeeded() ? Cylinder.Object : nullptr, FVector(-15.f, 0.f, 0.f), FVector(0.02f, 0.02f, 0.06f));
	Tail->SetRelativeRotation(FRotator(-70.f, 0.f, 0.f));
}

void AHitmenHamster::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenHamster, Master);
}

void AHitmenHamster::BeginPlay()
{
	Super::BeginPlay();
	GetCharacterMovement()->MaxWalkSpeed = RunSpeed;
	// Fur and belly colours on the basic shapes (BasicShapeMaterial's "Color").
	auto Paint = [](UStaticMeshComponent* Part, const FLinearColor& Color)
	{
		if (Part)
		{
			if (UMaterialInstanceDynamic* Material = Part->CreateDynamicMaterialInstance(0))
			{
				Material->SetVectorParameterValue(TEXT("Color"), Color);
			}
		}
	};
	Paint(Body, FurColor);
	Paint(Head, BellyColor);
	Paint(EarLeft, FLinearColor(1.f, 0.6f, 0.65f));
	Paint(EarRight, FLinearColor(1.f, 0.6f, 0.65f));
	Paint(Tail, FurColor);
	if (HasAuthority())
	{
		SetLifeSpan(0.f);
		FTimerHandle Handle;
		GetWorldTimerManager().SetTimer(Handle, this, &AHitmenHamster::Vanish, Lifetime, false);
	}
	NextSqueak = GetWorld()->GetTimeSeconds() + FMath::FRandRange(0.2f, 1.2f);
	if (PuffFX && GetNetMode() != NM_DedicatedServer)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, PuffFX, GetActorLocation(), FRotator::ZeroRotator, FVector(0.3f));
	}
}

void AHitmenHamster::SetMaster(AActor* InMaster)
{
	Master = InMaster;
}

int32 AHitmenHamster::GetHitmenTeamId_Implementation() const
{
	return Master ? UHitmenTeamLibrary::GetTeamId(Master) : HitmenTeams::Neutral;
}

AActor* AHitmenHamster::FindPrey() const
{
	// The nearest living enemy of whoever fired it, within SeekRadius.
	const int32 MasterTeam = Master ? UHitmenTeamLibrary::GetTeamId(Master) : HitmenTeams::Neutral;
	AActor* Best = nullptr;
	float BestDistance = SeekRadius * SeekRadius;
	for (TActorIterator<AHitmenCharacterBase> It(GetWorld()); It; ++It)
	{
		AHitmenCharacterBase* Candidate = *It;
		if (!Candidate || Candidate == Master || Candidate->IsDead() || !UHitmenTeamLibrary::AreTeamIdsHostile(MasterTeam, UHitmenTeamLibrary::GetTeamId(Candidate)))
		{
			continue;
		}
		const float Distance = FVector::DistSquared(GetActorLocation(), Candidate->GetActorLocation());
		if (Distance < BestDistance)
		{
			BestDistance = Distance;
			Best = Candidate;
		}
	}
	return Best;
}

void AHitmenHamster::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const float Now = GetWorld()->GetTimeSeconds();
	if (Now >= NextSqueak && GetNetMode() != NM_DedicatedServer)
	{
		NextSqueak = Now + FMath::FRandRange(0.8f, 2.2f);
		if (USoundBase* Squeak = UHitmenSoundLibrary::GetHitmenSound(EHitmenSound::HamsterSqueak))
		{
			UHitmenSoundLibrary::PlaySoundInSpace(this, Squeak, GetActorLocation(), EHitmenSoundSpace::Footstep,
				UHitmenSoundLibrary::GetHitmenSoundVolume(EHitmenSound::HamsterSqueak), FMath::FRandRange(0.85f, 1.25f));
		}
	}
	if (!HasAuthority() || Now < NextThink)
	{
		return;
	}
	NextThink = Now + 0.2f;
	AActor* Target = Prey.Get();
	const AHitmenCharacterBase* TargetCharacter = Cast<AHitmenCharacterBase>(Target);
	if (!Target || (TargetCharacter && TargetCharacter->IsDead()))
	{
		Target = FindPrey();
		Prey = Target;
	}
	AAIController* Brain = Cast<AAIController>(GetController());
	if (!Target)
	{
		if (Brain)
		{
			Brain->StopMovement();
		}
		return;
	}
	const float Distance = FVector::Dist2D(GetActorLocation(), Target->GetActorLocation());
	if (Distance <= BiteRange + Target->GetSimpleCollisionRadius())
	{
		if (Brain)
		{
			Brain->StopMovement();
		}
		if (Now >= NextBite)
		{
			NextBite = Now + BiteInterval;
			Bite(Target);
		}
	}
	else if (Brain)
	{
		Brain->MoveToActor(Target, BiteRange * 0.5f, true, true, true);
	}
}

void AHitmenHamster::Bite(AActor* Target)
{
	// No instigator: the bitten guard is alarmed where it stands, it never learns who fired the hamsters.
	FHitResult Hit;
	Hit.ImpactPoint = Target->GetActorLocation() - FVector(0.f, 0.f, 60.f);
	Hit.Location = Hit.ImpactPoint;
	UGameplayStatics::ApplyPointDamage(Target, BiteDamage, (Target->GetActorLocation() - GetActorLocation()).GetSafeNormal(), Hit, nullptr, this, UDamageType::StaticClass());
	Multicast_Bite(Hit.ImpactPoint);
}

void AHitmenHamster::Multicast_Bite_Implementation(FVector_NetQuantize Location)
{
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::HamsterBite, Location);
	// A little hop with each bite.
	if (Body)
	{
		Body->AddRelativeLocation(FVector(0.f, 0.f, 4.f));
		TWeakObjectPtr<UStaticMeshComponent> WeakBody(Body);
		FTimerHandle Handle;
		GetWorldTimerManager().SetTimer(Handle, FTimerDelegate::CreateWeakLambda(this, [WeakBody]()
		{
			if (WeakBody.IsValid())
			{
				WeakBody->AddRelativeLocation(FVector(0.f, 0.f, -4.f));
			}
		}), 0.12f, false);
	}
}

float AHitmenHamster::TakeDamage(float DamageAmount, FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser)
{
	const float Taken = Super::TakeDamage(DamageAmount, DamageEvent, EventInstigator, DamageCauser);
	if (HasAuthority() && DamageAmount > 0.f)
	{
		DamageTaken += DamageAmount;
		if (DamageTaken >= Health)
		{
			Vanish();
		}
	}
	return Taken;
}

void AHitmenHamster::Vanish()
{
	if (!HasAuthority() || IsActorBeingDestroyed())
	{
		return;
	}
	Multicast_Gone(GetActorLocation());
	Destroy();
}

void AHitmenHamster::Multicast_Gone_Implementation(FVector_NetQuantize Location)
{
	if (PuffFX && GetNetMode() != NM_DedicatedServer)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, PuffFX, Location, FRotator::ZeroRotator, FVector(0.35f));
	}
}
