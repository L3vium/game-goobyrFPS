// Hitmen — base widget

#include "UI/HitmenUserWidget.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenGameState.h"
#include "Blueprint/WidgetTree.h"
#include "Engine/World.h"

void UHitmenUserWidget::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	if (WidgetTree && !WidgetTree->RootWidget)
	{
		bUsingDefaultLayout = true;
		BuildDefaultLayout();
	}
}

void UHitmenUserWidget::NativeConstruct()
{
	Super::NativeConstruct();
	BindEvents();
}

void UHitmenUserWidget::NativeDestruct()
{
	UnbindEvents();
	Super::NativeDestruct();
}

UHitmenUIEventSubsystem* UHitmenUserWidget::GetUIEvents() const
{
	return UHitmenUIEventSubsystem::Get(GetOwningPlayer());
}

AHitmenPlayerController* UHitmenUserWidget::GetHitmenPC() const
{
	return Cast<AHitmenPlayerController>(GetOwningPlayer());
}

AHitmenGameState* UHitmenUserWidget::GetHitmenGameState() const
{
	return GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
}
