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
		DeliveryZone->OnComponentEndOverlap.AddDynamic(this, &AHitmenRetrievalPoint::HandleEndOverlap);
		ScanOverlappingPawns();
	}
}

void AHitmenRetrievalPoint::SetActive(bool bInActive)
{
	bActive = bInActive;
	OnRep_Active();
	if (HasAuthority() && bActive)
	{
		// Someone may already be standing here with the body.
		ScanOverlappingPawns();
	}
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
	APawn* Pawn = Cast<APawn>(OtherActor);
	WatchPawn(Pawn);
	TryDeliver(Pawn);
}

void AHitmenRetrievalPoint::HandleEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex)
{
	UnwatchPawn(Cast<APawn>(OtherActor));
}

void AHitmenRetrievalPoint::HandleCarryChanged(AHitmenCharacterBase* Body)
{
	// Picked up while already inside: the carrier is set before the event fires.
	if (Body && Body->IsVIP())
	{
		TryDeliver(Body->GetCarrier());
	}
}

void AHitmenRetrievalPoint::ScanOverlappingPawns()
{
	TArray<AActor*> Overlapping;
	DeliveryZone->GetOverlappingActors(Overlapping, APawn::StaticClass());
	for (AActor* Actor : Overlapping)
	{
		APawn* Pawn = Cast<APawn>(Actor);
		WatchPawn(Pawn);
		TryDeliver(Pawn);
	}
}

void AHitmenRetrievalPoint::WatchPawn(APawn* Pawn)
{
	UHitmenBodyCarryComponent* Carry = Pawn ? Pawn->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	if (Carry && !WatchedCarriers.Contains(TWeakObjectPtr<UHitmenBodyCarryComponent>(Carry)))
	{
		WatchedCarriers.Add(TWeakObjectPtr<UHitmenBodyCarryComponent>(Carry));
		Carry->OnCarryChanged.AddUniqueDynamic(this, &AHitmenRetrievalPoint::HandleCarryChanged);
	}
}

void AHitmenRetrievalPoint::UnwatchPawn(APawn* Pawn)
{
	UHitmenBodyCarryComponent* Carry = Pawn ? Pawn->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	if (Carry && WatchedCarriers.Remove(TWeakObjectPtr<UHitmenBodyCarryComponent>(Carry)) > 0)
	{
		Carry->OnCarryChanged.RemoveDynamic(this, &AHitmenRetrievalPoint::HandleCarryChanged);
	}
}

void AHitmenRetrievalPoint::TryDeliver(APawn* Pawn)
{
	if (!HasAuthority() || !bActive || !Pawn || !DeliveryZone->IsOverlappingActor(Pawn))
	{
		return;
	}
	const UHitmenBodyCarryComponent* Carry = Pawn->FindComponentByClass<UHitmenBodyCarryComponent>();
	const AHitmenCharacterBase* Body = Carry ? Carry->GetCarriedBody() : nullptr;
	if (!Body || !Body->IsVIP())
	{
		return;
	}
	if (AHitmenMatchGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenMatchGameMode>())
	{
		GM->OnVIPDelivered(Pawn, this);
	}
}
