// Hitmen — watcher avatar

#include "Spectator/HitmenWatcherAvatar.h"
#include "Framework/HitmenPlayerState.h"
#include "Core/HitmenGameSettings.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "UObject/ConstructorHelpers.h"

AHitmenWatcherAvatar::AHitmenWatcherAvatar()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = false;
	SetActorEnableCollision(false);

	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> ConeMesh(TEXT("/Engine/BasicShapes/Cone.Cone"));

	Orb = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Orb"));
	SetRootComponent(Orb);
	Orb->SetStaticMesh(SphereMesh.Object);
	Orb->SetWorldScale3D(FVector(0.22f));
	// A cone lying along +X: the lens shows where the watcher looks.
	Lens = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Lens"));
	Lens->SetupAttachment(Orb);
	Lens->SetStaticMesh(ConeMesh.Object);
	Lens->SetRelativeLocation(FVector(55.f, 0.f, 0.f));
	Lens->SetRelativeRotation(FRotator(-90.f, 0.f, 0.f));
	Lens->SetRelativeScale3D(FVector(0.6f, 0.6f, 0.7f));
	for (UStaticMeshComponent* Mesh : { Orb.Get(), Lens.Get() })
	{
		Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Mesh->SetCastShadow(false);
		Mesh->SetGenerateOverlapEvents(false);
	}
}

void AHitmenWatcherAvatar::BeginPlay()
{
	Super::BeginPlay();
	if (UMaterialInterface* Flash = UHitmenGameSettings::Get()->FlashMaterial.LoadSynchronous())
	{
		Material = UMaterialInstanceDynamic::Create(Flash, this);
		Material->SetVectorParameterValue(TEXT("Color"), Color);
		Material->SetScalarParameterValue(TEXT("Opacity"), 0.85f);
		Orb->SetMaterial(0, Material);
		Lens->SetMaterial(0, Material);
	}
}

void AHitmenWatcherAvatar::SetWatcher(AHitmenPlayerState* InWatcher)
{
	Watcher = InWatcher;
	bPlaced = false;
}

void AHitmenWatcherAvatar::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	AHitmenPlayerState* PS = Watcher.Get();
	if (!PS)
	{
		return;
	}
	const FHitmenWatcherView& View = PS->GetWatcherView();
	const FVector TargetLocation = View.Location;
	const FRotator TargetRotation(View.Pitch, View.Yaw, 0.f);
	if (!bPlaced)
	{
		SetActorLocationAndRotation(TargetLocation, TargetRotation);
		bPlaced = true;
	}
	else
	{
		SetActorLocationAndRotation(FMath::VInterpTo(GetActorLocation(), TargetLocation, DeltaSeconds, InterpSpeed),
			FMath::RInterpTo(GetActorRotation(), TargetRotation, DeltaSeconds, InterpSpeed));
	}
	PS->SetSmoothedWatcherLocation(GetActorLocation());
}
