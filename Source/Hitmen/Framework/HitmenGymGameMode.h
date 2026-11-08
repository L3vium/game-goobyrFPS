// Hitmen — debug gym: every candidate active, no win condition, respawn, debug grants

#pragma once

#include "CoreMinimal.h"
#include "Framework/HitmenMatchGameMode.h"
#include "HitmenGymGameMode.generated.h"

UCLASS()
class HITMEN_API AHitmenGymGameMode : public AHitmenMatchGameMode
{
	GENERATED_BODY()

public:
	AHitmenGymGameMode();

	virtual bool AllowsDebugCommands() const override { return true; }
};
