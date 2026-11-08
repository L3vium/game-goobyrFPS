// Hitmen — UI event hub

#include "UI/HitmenUIEventSubsystem.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/Pawn.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"
#include "Kismet/GameplayStatics.h"

UHitmenUIEventSubsystem* UHitmenUIEventSubsystem::Get(const APlayerController* PC)
{
	if (!PC || !PC->IsLocalController())
	{
		return nullptr;
	}
	const ULocalPlayer* LP = PC->GetLocalPlayer();
	return LP ? LP->GetSubsystem<UHitmenUIEventSubsystem>() : nullptr;
}

UHitmenUIEventSubsystem* UHitmenUIEventSubsystem::GetFromPawn(const APawn* Pawn)
{
	return Pawn ? Get(Cast<APlayerController>(Pawn->GetController())) : nullptr;
}

UHitmenUIEventSubsystem* UHitmenUIEventSubsystem::GetUIEvents(const UObject* WorldContextObject)
{
	return Get(UGameplayStatics::GetPlayerController(WorldContextObject, 0));
}

void UHitmenUIEventSubsystem::SetPauseMenuOpen(bool bOpen)
{
	if (bPauseMenuOpen != bOpen)
	{
		bPauseMenuOpen = bOpen;
		OnPauseMenuToggled.Broadcast(bOpen);
	}
}

void UHitmenUIEventSubsystem::SetOptionsOpen(bool bOpen)
{
	if (bOptionsOpen != bOpen)
	{
		bOptionsOpen = bOpen;
		OnOptionsToggled.Broadcast(bOpen);
	}
}

void UHitmenUIEventSubsystem::SetEmoteWheelOpen(bool bOpen)
{
	if (bEmoteWheelOpen != bOpen)
	{
		bEmoteWheelOpen = bOpen;
		OnEmoteWheelToggled.Broadcast(bOpen);
	}
}

void UHitmenUIEventSubsystem::SetSightroomActive(bool bActive)
{
	if (bSightroomActive != bActive)
	{
		bSightroomActive = bActive;
		OnSightroomToggled.Broadcast(bActive);
	}
}
