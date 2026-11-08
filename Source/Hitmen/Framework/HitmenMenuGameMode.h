// Hitmen — title screen game mode (no pawn, menu HUD)

#pragma once

#include "CoreMinimal.h"
#include "Framework/HitmenGameModeBase.h"
#include "HitmenMenuGameMode.generated.h"

UCLASS()
class HITMEN_API AHitmenMenuGameMode : public AHitmenGameModeBase
{
	GENERATED_BODY()

public:
	AHitmenMenuGameMode();
};
