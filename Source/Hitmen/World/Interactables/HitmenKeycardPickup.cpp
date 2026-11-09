// Hitmen — keycard pickup

#include "World/Interactables/HitmenKeycardPickup.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/StaticMesh.h"
#include "UObject/ConstructorHelpers.h"

AHitmenKeycardPickup::AHitmenKeycardPickup()
{
	// Visible and traceable even without a Blueprint (keycards dropped by NPCs).
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CardMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));
	if (CardMesh.Succeeded())
	{
		Mesh->SetStaticMesh(CardMesh.Object);
		Mesh->SetRelativeScale3D(FVector(0.15f, 0.1f, 0.02f));
		Mesh->SetRelativeLocation(FVector(0.f, 0.f, 10.f));
	}
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
