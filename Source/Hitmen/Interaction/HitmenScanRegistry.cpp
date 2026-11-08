// Hitmen — scan registry

#include "Interaction/HitmenScanRegistry.h"
#include "Engine/World.h"

void UHitmenScanRegistry::Register(AActor* Actor)
{
	if (UWorld* World = Actor ? Actor->GetWorld() : nullptr)
	{
		if (UHitmenScanRegistry* Registry = World->GetSubsystem<UHitmenScanRegistry>())
		{
			Registry->Scannables.AddUnique(Actor);
		}
	}
}

void UHitmenScanRegistry::Unregister(AActor* Actor)
{
	if (UWorld* World = Actor ? Actor->GetWorld() : nullptr)
	{
		if (UHitmenScanRegistry* Registry = World->GetSubsystem<UHitmenScanRegistry>())
		{
			Registry->Scannables.Remove(Actor);
		}
	}
}
