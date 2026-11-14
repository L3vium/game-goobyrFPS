// Hitmen — where the VIP runs when the place gets dangerous (Hitman's safe room). Placed by LD; without any, the VIP
// flees away from the threat, out of its sight.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenVIPSafeSpot.generated.h"

UCLASS(Blueprintable, meta=(DisplayName="Hitmen VIP Safe Spot"))
class HITMEN_API AHitmenVIPSafeSpot : public AActor
{
	GENERATED_BODY()

public:
	AHitmenVIPSafeSpot();
};
