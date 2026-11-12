// Hitmen — test helpers

#include "Framework/HitmenDebugLibrary.h"
#include "EnhancedInputSubsystems.h"
#include "InputAction.h"
#include "GameFramework/PlayerController.h"
#include "Engine/LocalPlayer.h"

namespace
{
	UEnhancedInputLocalPlayerSubsystem* GetInputSubsystem(APlayerController* PlayerController)
	{
		const ULocalPlayer* LocalPlayer = PlayerController ? PlayerController->GetLocalPlayer() : nullptr;
		return LocalPlayer ? LocalPlayer->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr;
	}
}

void UHitmenDebugLibrary::StartInput(APlayerController* PlayerController, UInputAction* Action, FVector Value)
{
	if (UEnhancedInputLocalPlayerSubsystem* Input = GetInputSubsystem(PlayerController); Input && Action)
	{
		Input->StartContinuousInputInjectionForAction(Action, FInputActionValue(Action->ValueType, Value), {}, {});
	}
}

void UHitmenDebugLibrary::StopInput(APlayerController* PlayerController, UInputAction* Action)
{
	if (UEnhancedInputLocalPlayerSubsystem* Input = GetInputSubsystem(PlayerController); Input && Action)
	{
		Input->StopContinuousInputInjectionForAction(Action);
	}
}

void UHitmenDebugLibrary::InjectInput(APlayerController* PlayerController, UInputAction* Action, FVector Value)
{
	if (UEnhancedInputLocalPlayerSubsystem* Input = GetInputSubsystem(PlayerController); Input && Action)
	{
		Input->InjectInputForAction(Action, FInputActionValue(Action->ValueType, Value), {}, {});
	}
}
