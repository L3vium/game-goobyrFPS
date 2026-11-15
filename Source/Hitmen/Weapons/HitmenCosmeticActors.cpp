// Hitmen — tracers and light bursts

#include "Weapons/HitmenCosmeticActors.h"
#include "Components/StaticMeshComponent.h"
#include "Components/PointLightComponent.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	const FName ColorParameter(TEXT("Color"));
	const FName OpacityParameter(TEXT("Opacity"));
}

// ---------------------------------------------------------------------------------------------
// Tracer
// ---------------------------------------------------------------------------------------------

AHitmenTracer::AHitmenTracer()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = false;
	SetCanBeDamaged(false);
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	SetRootComponent(Mesh);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Mesh->SetCastShadow(false);
	Mesh->SetGenerateOverlapEvents(false);
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cylinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	if (Cylinder.Succeeded())
	{
		Mesh->SetStaticMesh(Cylinder.Object);
	}
}

void AHitmenTracer::SpawnTracer(UWorld* World, const FVector& Start, const FVector& End, UMaterialInterface* Material, const FLinearColor& Color,
	const FHitmenTracerStyle& Style)
{
	if (!World || World->GetNetMode() == NM_DedicatedServer || FVector::DistSquared(Start, End) < 100.f)
	{
		return;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	if (AHitmenTracer* Tracer = World->SpawnActor<AHitmenTracer>(Start, FRotator::ZeroRotator, Params))
	{
		Tracer->Start = Start;
		Tracer->End = End;
		Tracer->Style = Style;
		if (Material)
		{
			Tracer->Instance = UMaterialInstanceDynamic::Create(Material, Tracer);
			Tracer->Instance->SetVectorParameterValue(ColorParameter, Color);
			Tracer->Instance->SetScalarParameterValue(OpacityParameter, 1.f);
			Tracer->Mesh->SetMaterial(0, Tracer->Instance);
		}
		Tracer->SetLifeSpan(2.f);
		Tracer->Tick(0.f);
	}
}

void AHitmenTracer::SpawnSmokeTrail(UWorld* World, const FVector& Start, const FVector& End, UMaterialInterface* Material, const FHitmenTracerStyle& Style)
{
	if (!World || World->GetNetMode() == NM_DedicatedServer || Style.SmokeLifetime <= 0.f || !Material || FVector::DistSquared(Start, End) < 100.f)
	{
		return;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	if (AHitmenTracer* Smoke = World->SpawnActor<AHitmenTracer>(Start, FRotator::ZeroRotator, Params))
	{
		// Starts a little out of the barrel, so it never fills the shooter's own view.
		const FVector Direction = (End - Start).GetSafeNormal();
		Smoke->Start = Start + Direction * 60.f;
		Smoke->End = End;
		Smoke->Style = Style;
		Smoke->bSmoke = true;
		Smoke->Instance = UMaterialInstanceDynamic::Create(Material, Smoke);
		Smoke->Instance->SetScalarParameterValue(OpacityParameter, Style.SmokeOpacity);
		Smoke->Mesh->SetMaterial(0, Smoke->Instance);
		Smoke->Mesh->SetTranslucentSortPriority(-1);
		Smoke->SetLifeSpan(Style.SmokeLifetime + 0.5f);
		Smoke->Tick(0.f);
	}
}

void AHitmenTracer::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const FVector Direction = (End - Start).GetSafeNormal();
	const float Total = FVector::Dist(Start, End);
	if (bSmoke)
	{
		// Smoke: the whole line, widening and thinning out (ease-out), then gone.
		Age += DeltaSeconds;
		const float Life = FMath::Clamp(1.f - Age / FMath::Max(Style.SmokeLifetime, 0.01f), 0.f, 1.f);
		if (Life <= 0.f)
		{
			Destroy();
			return;
		}
		if (Instance)
		{
			Instance->SetScalarParameterValue(OpacityParameter, Style.SmokeOpacity * Life * Life);
		}
		const float Width = Style.SmokeThickness + Style.SmokeGrowth * Age;
		SetActorLocationAndRotation(Start + Direction * Total * 0.5f, FRotationMatrix::MakeFromZ(Direction).Rotator());
		SetActorScale3D(FVector(Width / 100.f, Width / 100.f, FMath::Max(Total, 1.f) / 100.f));
		return;
	}
	if (Style.Speed <= 0.f)
	{
		// Trail: the whole line at once, thinning and fading out.
		Age += DeltaSeconds;
		const float Life = FMath::Clamp(1.f - Age / FMath::Max(Style.FadeTime, 0.01f), 0.f, 1.f);
		if (Life <= 0.f)
		{
			Destroy();
			return;
		}
		if (Instance)
		{
			Instance->SetScalarParameterValue(OpacityParameter, Life);
		}
		const float Width = Style.Thickness * (0.4f + 0.6f * Life);
		SetActorLocationAndRotation(Start + Direction * Total * 0.5f, FRotationMatrix::MakeFromZ(Direction).Rotator());
		SetActorScale3D(FVector(Width / 100.f, Width / 100.f, FMath::Max(Total, 1.f) / 100.f));
		return;
	}
	Travelled += Style.Speed * DeltaSeconds;
	const float Head = FMath::Min(Travelled, Total);
	const float Tail = FMath::Clamp(Travelled - Style.Length, 0.f, Total);
	if (Tail >= Total)
	{
		Destroy();
		return;
	}
	// The basic cylinder is 100 cm tall along Z: lay it along the shot, from tail to head.
	const float Segment = FMath::Max(Head - Tail, 1.f);
	SetActorLocationAndRotation(Start + Direction * (Tail + Segment * 0.5f), FRotationMatrix::MakeFromZ(Direction).Rotator());
	SetActorScale3D(FVector(Style.Thickness / 100.f, Style.Thickness / 100.f, Segment / 100.f));
}

// ---------------------------------------------------------------------------------------------
// Flash burst
// ---------------------------------------------------------------------------------------------

AHitmenFlashBurst::AHitmenFlashBurst()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = false;
	SetCanBeDamaged(false);
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	SetRootComponent(Mesh);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Mesh->SetCastShadow(false);
	Mesh->SetGenerateOverlapEvents(false);
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	if (Sphere.Succeeded())
	{
		Mesh->SetStaticMesh(Sphere.Object);
	}
	Light = CreateDefaultSubobject<UPointLightComponent>(TEXT("Light"));
	Light->SetupAttachment(Mesh);
	Light->SetCastShadows(false);
	Light->SetIntensityUnits(ELightUnits::Candelas);
	Light->SetIntensity(0.f);
}

void AHitmenFlashBurst::SpawnBurst(UWorld* World, const FVector& Location, UMaterialInterface* InMaterial, const FLinearColor& InColor,
	float InStartRadius, float InEndRadius, float InDuration, float InLightIntensity, float InLightRadius)
{
	if (!World || World->GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	if (AHitmenFlashBurst* Burst = World->SpawnActor<AHitmenFlashBurst>(Location, FRotator::ZeroRotator, Params))
	{
		Burst->Color = InColor;
		Burst->StartRadius = InStartRadius;
		Burst->EndRadius = InEndRadius;
		Burst->Duration = FMath::Max(0.02f, InDuration);
		Burst->LightIntensity = InLightIntensity;
		Burst->Light->SetAttenuationRadius(InLightRadius);
		Burst->Light->SetLightColor(InColor);
		Burst->Light->SetVisibility(InLightIntensity > 0.f);
		if (InMaterial)
		{
			Burst->Material = UMaterialInstanceDynamic::Create(InMaterial, Burst);
			Burst->Mesh->SetMaterial(0, Burst->Material);
		}
		else
		{
			Burst->Mesh->SetVisibility(false);
		}
		Burst->SetLifeSpan(Burst->Duration + 1.f);
		Burst->Tick(0.f);
	}
}

void AHitmenFlashBurst::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Elapsed += DeltaSeconds;
	const float T = FMath::Clamp(Elapsed / Duration, 0.f, 1.f);
	if (T >= 1.f)
	{
		Destroy();
		return;
	}
	// Grows fast, fades out (ease-out size, quadratic fade).
	const float Radius = FMath::Lerp(StartRadius, EndRadius, 1.f - FMath::Square(1.f - T));
	const float Fade = FMath::Square(1.f - T);
	SetActorScale3D(FVector(Radius / 50.f));
	if (Material)
	{
		Material->SetVectorParameterValue(ColorParameter, Color);
		Material->SetScalarParameterValue(OpacityParameter, Fade);
	}
	Light->SetIntensity(LightIntensity * Fade);
}
