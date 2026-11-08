// Hitmen — single source of truth for "who is hostile to whom"

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenTeamLibrary.generated.h"

/** Team id ranges. Lobby teams use 0..N. */
namespace HitmenTeams
{
	/** Never hostile, never targeted (watchers, props). */
	constexpr int32 Neutral = -1;
	/** Every NPC belongs to this faction unless charmed. */
	constexpr int32 NPC = 1000;
	/** Solo players get SoloBase + PlayerId so each is its own faction. */
	constexpr int32 SoloBase = 2000;
}

/** Implemented by anything that has a faction (characters, turrets, vehicles). */
UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenTeamMember : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenTeamMember
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|Team")
	int32 GetHitmenTeamId() const;
};

UCLASS()
class HITMEN_API UHitmenTeamLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/** Team id of an actor (its instigator/owner chain is followed for projectiles). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Team")
	static int32 GetTeamId(const AActor* Actor);

	UFUNCTION(BlueprintPure, Category="Hitmen|Team")
	static bool AreHostile(const AActor* A, const AActor* B);

	UFUNCTION(BlueprintPure, Category="Hitmen|Team")
	static bool AreTeamIdsHostile(int32 A, int32 B);
};
