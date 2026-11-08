// Hitmen — registry of scannable actors for the Sightroom (avoids world iteration)

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/WorldSubsystem.h"
#include "HitmenScanRegistry.generated.h"

UCLASS()
class HITMEN_API UHitmenScanRegistry : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	static void Register(AActor* Actor);
	static void Unregister(AActor* Actor);

	const TArray<TWeakObjectPtr<AActor>>& GetScannables() const { return Scannables; }

private:
	TArray<TWeakObjectPtr<AActor>> Scannables;
};
