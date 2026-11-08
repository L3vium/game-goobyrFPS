// Hitmen — agent preview

#include "Framework/HitmenAgentPreview.h"
#include "Components/HitmenCosmeticComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Animation/AnimSequenceBase.h"

AHitmenAgentPreview::AHitmenAgentPreview()
{
	PrimaryActorTick.bCanEverTick = false;
	bReplicates = false;
	Body = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("Body"));
	SetRootComponent(Body);
	Body->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Cosmetics = CreateDefaultSubobject<UHitmenCosmeticComponent>(TEXT("Cosmetics"));
	Cosmetics->SetIsReplicated(false);
}

void AHitmenAgentPreview::BeginPlay()
{
	Super::BeginPlay();
	if (IdleAnimation)
	{
		Body->PlayAnimation(IdleAnimation, true);
	}
}

void AHitmenAgentPreview::ShowCosmetics(const FHitmenCosmeticLoadout& InCosmetics)
{
	Cosmetics->SetCosmetics(InCosmetics);
	Juice_OnLookChanged();
}
