// Hitmen — interactable base

#include "Interaction/HitmenInteractableBase.h"
#include "Interaction/HitmenScanRegistry.h"
#include "Components/StaticMeshComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "Hitmen.h"

AHitmenInteractableBase::AHitmenInteractableBase()
{
	PrimaryActorTick.bCanEverTick = false;
	bReplicates = true;

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Root);
	Mesh->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);

	InteractionText = NSLOCTEXT("Hitmen", "Use", "Use");
}

void AHitmenInteractableBase::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenInteractableBase, bInteractionEnabled);
}

void AHitmenInteractableBase::BeginPlay()
{
	Super::BeginPlay();
	if (bShowInSightroom)
	{
		UHitmenScanRegistry::Register(this);
	}
}

void AHitmenInteractableBase::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UHitmenScanRegistry::Unregister(this);
	Super::EndPlay(EndPlayReason);
}

bool AHitmenInteractableBase::CanInteract_Implementation(AActor* Interactor) const
{
	return bUsableWithInteract && bInteractionEnabled && GetWorld()->GetTimeSeconds() - LastInteractTime >= InteractionCooldown;
}

void AHitmenInteractableBase::Interact_Implementation(AActor* Interactor)
{
	if (!HasAuthority() || !IHitmenInteractable::Execute_CanInteract(this, Interactor))
	{
		return;
	}
	LastInteractTime = GetWorld()->GetTimeSeconds();
	OnInteracted(Interactor);
	Multicast_Interacted(Interactor);
}

void AHitmenInteractableBase::Multicast_Interacted_Implementation(AActor* Interactor)
{
	Juice_OnInteracted(Interactor);
}

void AHitmenInteractableBase::SetInteractionEnabled(bool bEnabled)
{
	bInteractionEnabled = bEnabled;
	OnRep_InteractionEnabled();
}

EHitmenScanCategory AHitmenInteractableBase::GetScanCategory_Implementation(const AActor* Observer) const
{
	return bShowInSightroom ? EHitmenScanCategory::Interactable : EHitmenScanCategory::None;
}

void AHitmenInteractableBase::SetScanHighlight_Implementation(bool bEnabled, int32 StencilValue)
{
	TArray<UPrimitiveComponent*> Primitives;
	GetComponents(Primitives);
	for (UPrimitiveComponent* Primitive : Primitives)
	{
		Primitive->SetRenderCustomDepth(bEnabled);
		Primitive->SetCustomDepthStencilValue(bEnabled ? StencilValue : 0);
	}
}
