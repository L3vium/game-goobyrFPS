// Hitmen — VIP safe spot marker

#include "AI/HitmenVIPSafeSpot.h"
#include "Components/SceneComponent.h"
#include "Components/BillboardComponent.h"

AHitmenVIPSafeSpot::AHitmenVIPSafeSpot()
{
	PrimaryActorTick.bCanEverTick = false;
	SetRootComponent(CreateDefaultSubobject<USceneComponent>(TEXT("Root")));
	SetHidden(true);
#if WITH_EDITORONLY_DATA
	if (UBillboardComponent* Sprite = CreateEditorOnlyDefaultSubobject<UBillboardComponent>(TEXT("Sprite")))
	{
		Sprite->SetupAttachment(GetRootComponent());
	}
#endif
}
