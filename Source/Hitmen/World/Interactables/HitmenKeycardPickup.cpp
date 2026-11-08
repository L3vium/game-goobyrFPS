// Hitmen — keycard pickup

#include "World/Interactables/HitmenKeycardPickup.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Net/UnrealNetwork.h"

AHitmenKeycardPickup::AHitmenKeycardPickup()
{
	Mesh->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	Mesh->SetCollisionResponseToChannel(ECC_GameTraceChannel3, ECR_Block);
}

void AHitmenKeycardPickup::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenKeycardPickup, KeycardId);
}

FText AHitmenKeycardPickup::GetInteractionText_Implementation(AActor* Interactor) const
{
	return FText::Format(NSLOCTEXT("Hitmen", "TakeKeycard", "Take keycard {0}"), FText::AsNumber(KeycardId));
}

void AHitmenKeycardPickup::OnInteracted(AActor* Interactor)
{
	if (UHitmenInventoryComponent* Inventory = Interactor ? Interactor->FindComponentByClass<UHitmenInventoryComponent>() : nullptr)
	{
		Inventory->AddKeycard(KeycardId);
		Destroy();
	}
}
