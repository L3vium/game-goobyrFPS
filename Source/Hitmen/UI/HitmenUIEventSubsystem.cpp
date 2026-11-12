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

FString UHitmenUIEventSubsystem::GetKeyLabel(EHitmenKeyLabel Label) const
{
	const int32 Index = static_cast<int32>(Label);
	if (KeyLabels.IsValidIndex(Index) && !KeyLabels[Index].IsEmpty())
	{
		return KeyLabels[Index];
	}
	// Default layout (AZERTY) until the controller pushes the real bindings.
	static const TCHAR* const Defaults[] = { TEXT("F"), TEXT("SPACE"), TEXT("E"), TEXT("A"), TEXT("TAB"), TEXT("ENTER") };
	return Index >= 0 && Index < UE_ARRAY_COUNT(Defaults) ? FString(Defaults[Index]) : FString();
}

FString UHitmenUIEventSubsystem::ShortKeyName(const FKey& Key)
{
	if (!Key.IsValid())
	{
		return TEXT("-");
	}
	if (Key == EKeys::SpaceBar) { return TEXT("SPACE"); }
	if (Key == EKeys::LeftShift || Key == EKeys::RightShift) { return TEXT("SHIFT"); }
	if (Key == EKeys::LeftControl || Key == EKeys::RightControl) { return TEXT("CTRL"); }
	if (Key == EKeys::LeftAlt || Key == EKeys::RightAlt) { return TEXT("ALT"); }
	if (Key == EKeys::Escape) { return TEXT("ESC"); }
	if (Key == EKeys::Enter) { return TEXT("ENTER"); }
	if (Key == EKeys::Tab) { return TEXT("TAB"); }
	if (Key == EKeys::LeftMouseButton) { return TEXT("LMB"); }
	if (Key == EKeys::RightMouseButton) { return TEXT("RMB"); }
	if (Key == EKeys::MiddleMouseButton) { return TEXT("MMB"); }
	if (Key == EKeys::ThumbMouseButton) { return TEXT("M4"); }
	if (Key == EKeys::ThumbMouseButton2) { return TEXT("M5"); }
	if (Key == EKeys::Mouse2D) { return TEXT("MOUSE"); }
	if (Key == EKeys::MouseWheelAxis) { return TEXT("WHEEL"); }
	return Key.GetDisplayName(false).ToString().ToUpper();
}

void UHitmenUIEventSubsystem::SetKeyLabels(const TArray<FString>& InLabels)
{
	if (InLabels != KeyLabels)
	{
		KeyLabels = InLabels;
		OnKeyLabelsChanged.Broadcast();
	}
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
