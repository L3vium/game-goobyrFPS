// Hitmen — HUD widget manager

#include "Framework/HitmenHUD.h"
#include "Framework/HitmenPlayerController.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Core/HitmenTypes.h"
#include "Blueprint/UserWidget.h"

void AHitmenHUD::BeginPlay()
{
	Super::BeginPlay();

	APlayerController* PC = GetOwningPlayerController();
	if (!PC || !PC->IsLocalController())
	{
		return;
	}

	if (PrimaryWidgetClass)
	{
		PrimaryWidget = CreateWidget<UUserWidget>(PC, PrimaryWidgetClass);
		if (PrimaryWidget)
		{
			PrimaryWidget->AddToPlayerScreen(0);
		}
	}
	PauseMenuWidget = CreateHidden(PauseMenuClass, 50);
	OptionsWidget = CreateHidden(OptionsClass, 60);
	EmoteWheelWidget = CreateHidden(EmoteWheelClass, 40);
	SpectatorWidget = CreateHidden(SpectatorClass, 10);
	ResultsWidget = CreateHidden(ResultsClass, 70);

	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(PC))
	{
		UI->OnPauseMenuToggled.AddDynamic(this, &AHitmenHUD::HandlePauseToggled);
		UI->OnOptionsToggled.AddDynamic(this, &AHitmenHUD::HandleOptionsToggled);
		UI->OnEmoteWheelToggled.AddDynamic(this, &AHitmenHUD::HandleEmoteWheelToggled);
		UI->OnSpectatorModeChanged.AddDynamic(this, &AHitmenHUD::HandleSpectatorMode);
		UI->OnMatchEnded.AddDynamic(this, &AHitmenHUD::HandleMatchEnded);
		// Menus start closed on a new level.
		UI->SetPauseMenuOpen(false);
		UI->SetOptionsOpen(false);
	}
	RefreshInputMode();
}

void AHitmenHUD::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(GetOwningPlayerController()))
	{
		UI->OnPauseMenuToggled.RemoveAll(this);
		UI->OnOptionsToggled.RemoveAll(this);
		UI->OnEmoteWheelToggled.RemoveAll(this);
		UI->OnSpectatorModeChanged.RemoveAll(this);
		UI->OnMatchEnded.RemoveAll(this);
	}
	Super::EndPlay(EndPlayReason);
}

UUserWidget* AHitmenHUD::CreateHidden(TSubclassOf<UUserWidget> Class, int32 ZOrder)
{
	if (!Class)
	{
		return nullptr;
	}
	UUserWidget* Widget = CreateWidget<UUserWidget>(GetOwningPlayerController(), Class);
	if (Widget)
	{
		Widget->AddToPlayerScreen(ZOrder);
		Widget->SetVisibility(ESlateVisibility::Collapsed);
	}
	return Widget;
}

void AHitmenHUD::SetShown(UUserWidget* Widget, bool bShown)
{
	if (Widget)
	{
		Widget->SetVisibility(bShown ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	}
}

void AHitmenHUD::HandlePauseToggled(bool bOpen)
{
	SetShown(PauseMenuWidget, bOpen);
	RefreshInputMode();
}

void AHitmenHUD::HandleOptionsToggled(bool bOpen)
{
	SetShown(OptionsWidget, bOpen);
	RefreshInputMode();
}

void AHitmenHUD::HandleEmoteWheelToggled(bool bOpen)
{
	SetShown(EmoteWheelWidget, bOpen);
	RefreshInputMode();
}

void AHitmenHUD::HandleSpectatorMode(uint8 Mode)
{
	SetShown(SpectatorWidget, true);
}

void AHitmenHUD::HandleMatchEnded(const FHitmenMatchResult& Result)
{
	SetShown(ResultsWidget, true);
	RefreshInputMode();
}

void AHitmenHUD::RefreshInputMode()
{
	AHitmenPlayerController* PC = Cast<AHitmenPlayerController>(GetOwningPlayerController());
	const UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(PC);
	if (!PC || !UI)
	{
		return;
	}
	const bool bResults = ResultsWidget && ResultsWidget->IsVisible();
	const bool bMenu = bPrimaryIsMenu || UI->IsPauseMenuOpen() || UI->IsOptionsOpen() || UI->IsEmoteWheelOpen() || bResults;
	PC->SetMenuInputMode(bMenu);
}
