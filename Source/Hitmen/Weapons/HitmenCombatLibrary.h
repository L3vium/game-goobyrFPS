// Hitmen — shared combat helpers (explosions, aim, hostility-aware damage)

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "Core/HitmenTypes.h"
#include "HitmenCombatLibrary.generated.h"

UCLASS()
class HITMEN_API UHitmenCombatLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * Server. Radial damage with falloff, impulse on physics, notifies IHitmenExplosionReceiver
	 * actors (breakable walls, barrels, mines) and makes an explosion noise for the AI.
	 */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Combat", meta=(WorldContext="WorldContextObject"))
	static void Explode(const UObject* WorldContextObject, FVector Origin, float Radius, float Damage, AActor* DamageCauser, AController* InstigatorController, float NoiseRadius = 4000.f, float Impulse = 150000.f);

	/** Eye location and view direction of a pawn (camera for players, eyes for NPCs). */
	/** Make a noise NPCs can hear: graded, occluded, only the nearest responders react. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|AI", meta=(WorldContext="WorldContextObject"))
	static void ReportNoise(const UObject* WorldContextObject, FVector Location, float Radius, AActor* NoiseInstigator, EHitmenNoiseType Type);

	UFUNCTION(BlueprintPure, Category="Hitmen|Combat")
	static void GetAimPoint(const APawn* Pawn, FVector& OutLocation, FVector& OutDirection);

	/** Random direction inside a cone, deterministic from a seed (client/server agree). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Combat")
	static FVector RandomConeDirection(const FVector& Direction, float HalfAngleDegrees, int32 Seed, int32 Index);
};
