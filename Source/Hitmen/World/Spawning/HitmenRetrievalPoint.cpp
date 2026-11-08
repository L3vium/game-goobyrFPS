// Hitmen — retrieval point

#include "World/Spawning/HitmenRetrievalPoint.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Framework/HitmenMatchGameMode.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

AHitmenRetrievalPoint::AHitmenRetrievalPoint()
{
	bReplicates = true;
	DeliveryZone = CreateDefaultSubobject<UBoxComponent>(TEXT("DeliveryZone"));
	DeliveryZone->SetBoxExtent(FVector(300.f, 300.f, 150.f));
	DeliveryZone->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	SetRootComponent(DeliveryZone);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(DeliveryZone);
}

void AHitmenRetrievalPoint::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenRetrievalPoint, bActive);
}

void AHitmenRetrievalPoint::BeginPlay()
{
	Super::BeginPlay();
	if (HasAuthority())
	{
		DeliveryZone->OnComponentBeginOverlap.AddDynamic(this, &AHitmenRetrievalPoint::HandleOverlap);
	}
}

void AHitmenRetrievalPoint::SetActive(bool bInActive)
{
	bActive = bInActive;
	OnRep_Active();
}

void AHitmenRetrievalPoint::OnRep_Active()
{
	if (bHideWhenInactive)
	{
		SetActorHiddenInGame(!bActive);
	}
	Juice_OnActiveChanged(bActive);
}

void AHitmenRetrievalPoint::HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	if (!bActive)
	{
		return;
	}
	APawn* Pawn = Cast<APawn>(OtherActor);
	const UHitmenBodyCarryComponent* Carry = Pawn ? Pawn->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	if (!Carry || !Carry->IsCarrying() || !Carry->GetCarriedBody()->IsVIP())
	{
		return;
	}
	if (AHitmenMatchGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenMatchGameMode>())
	{
		GM->OnVIPDelivered(Pawn, this);
	}
}
