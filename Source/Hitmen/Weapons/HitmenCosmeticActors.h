// Hitmen — short-lived cosmetic actors (local only, never replicated): bullet tracers and light bursts

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenCosmeticActors.generated.h"

class UStaticMeshComponent;
class UPointLightComponent;
class UMaterialInterface;
class UMaterialInstanceDynamic;

/**
 * A bright streak that runs from the muzzle to the impact (shotgun pellets, machine guns, turrets).
 * Spawn with AHitmenTracer::SpawnTracer; it destroys itself once it reached the end.
 */
UCLASS(NotBlueprintable)
class HITMEN_API AHitmenTracer : public AActor
{
	GENERATED_BODY()

public:
	AHitmenTracer();

	/** Local cosmetic: a tracer from Start to End (nothing on a dedicated server). */
	static void SpawnTracer(UWorld* World, const FVector& Start, const FVector& End, UMaterialInterface* Material, const FLinearColor& Color);

	virtual void Tick(float DeltaSeconds) override;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	/** cm/s of the streak, its length and thickness (cm). */
	float Speed = 28000.f;
	float Length = 220.f;
	float Thickness = 1.4f;

private:
	FVector Start = FVector::ZeroVector;
	FVector End = FVector::ZeroVector;
	float Travelled = 0.f;
};

/**
 * A glowing sphere that grows and fades with a point light: muzzle glows, flashbang flashes.
 * Uses a translucent unlit material with a vector "Color" and a scalar "Opacity" parameter.
 */
UCLASS(NotBlueprintable)
class HITMEN_API AHitmenFlashBurst : public AActor
{
	GENERATED_BODY()

public:
	AHitmenFlashBurst();

	/** Local cosmetic burst (nothing on a dedicated server). Radii in cm; LightIntensity in candelas (0 = no light). */
	static void SpawnBurst(UWorld* World, const FVector& Location, UMaterialInterface* Material, const FLinearColor& Color,
		float StartRadius, float EndRadius, float Duration, float LightIntensity, float LightRadius);

	virtual void Tick(float DeltaSeconds) override;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UPointLightComponent> Light;

private:
	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> Material;

	FLinearColor Color = FLinearColor::White;
	float StartRadius = 10.f;
	float EndRadius = 100.f;
	float Duration = 0.1f;
	float LightIntensity = 0.f;
	float Elapsed = 0.f;
};
