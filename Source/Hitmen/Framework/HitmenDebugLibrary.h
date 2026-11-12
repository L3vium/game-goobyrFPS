// Hitmen — test helpers for editor scripts (QA): drive a local player's Enhanced Input like real keys.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenDebugLibrary.generated.h"

class APlayerController;
class UInputAction;

UCLASS()
class HITMEN_API UHitmenDebugLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/** Holds an input action at Value every frame until StopInput (a held key / stick). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Debug")
	static void StartInput(APlayerController* PlayerController, UInputAction* Action, FVector Value);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Debug")
	static void StopInput(APlayerController* PlayerController, UInputAction* Action);

	/** One frame of input (a key press and release next frame). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Debug")
	static void InjectInput(APlayerController* PlayerController, UInputAction* Action, FVector Value);
};
