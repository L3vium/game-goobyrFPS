// Hitmen — player start belonging to a spawn group (one group per team / solo player)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerStart.h"
#include "HitmenTeamStart.generated.h"

UCLASS(Blueprintable)
class HITMEN_API AHitmenTeamStart : public APlayerStart
{
	GENERATED_BODY()

public:
	AHitmenTeamStart(const FObjectInitializer& ObjectInitializer);

	/** Starts with the same group are used by the same team (sheltered spawn per team). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Spawn")
	int32 SpawnGroup = 0;
};
