// Hitmen — openings

#include "World/Interactables/HitmenOpenings.h"
#include "World/Interactables/HitmenHazards.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Characters/HitmenNPC.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "TimerManager.h"

// ---------------------------------------------------------------------------------------------
// Base
// ---------------------------------------------------------------------------------------------

AHitmenOpeningBase::AHitmenOpeningBase()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	OpenText = NSLOCTEXT("Hitmen", "Open", "Open");
	CloseText = NSLOCTEXT("Hitmen", "Close", "Close");
}

void AHitmenOpeningBase::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenOpeningBase, bOpen);
	DOREPLIFETIME(AHitmenOpeningBase, OpenDirection);
}

FText AHitmenOpeningBase::GetInteractionText_Implementation(AActor* Interactor) const
{
	return bOpen ? CloseText : OpenText;
}

void AHitmenOpeningBase::OnInteracted(AActor* Interactor)
{
	SetOpen(!bOpen, Interactor);
}

void AHitmenOpeningBase::SetOpen(bool bNewOpen, const AActor* Opener)
{
	if (!HasAuthority() || bOpen == bNewOpen)
	{
		return;
	}
	if (bNewOpen && Opener)
	{
		// Swing away from whoever opens it.
		const FVector ToOpener = (Opener->GetActorLocation() - GetActorLocation()).GetSafeNormal2D();
		OpenDirection = FVector::DotProduct(ToOpener, GetActorForwardVector()) > 0.f ? -1.f : 1.f;
	}
	bOpen = bNewOpen;
	OnRep_Open();
}

void AHitmenOpeningBase::OnRep_Open()
{
	SetActorTickEnabled(true);
	Juice_OnOpenChanged(bOpen);
}

void AHitmenOpeningBase::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	OpenAlpha = FMath::FInterpConstantTo(OpenAlpha, bOpen ? 1.f : 0.f, DeltaSeconds, MoveSpeed);
	ApplyOpenAlpha(FMath::SmoothStep(0.f, 1.f, OpenAlpha));
	if (FMath::IsNearlyEqual(OpenAlpha, bOpen ? 1.f : 0.f))
	{
		SetActorTickEnabled(false);
	}
}

// ---------------------------------------------------------------------------------------------
// Door
// ---------------------------------------------------------------------------------------------

AHitmenDoor::AHitmenDoor()
{
	NPCTrigger = CreateDefaultSubobject<UBoxComponent>(TEXT("NPCTrigger"));
	NPCTrigger->SetupAttachment(Root);
	NPCTrigger->SetBoxExtent(FVector(120.f, 80.f, 100.f));
	NPCTrigger->SetRelativeLocation(FVector(0.f, 50.f, 100.f));
	NPCTrigger->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	Mesh->SetCanEverAffectNavigation(false);
}

void AHitmenDoor::BeginPlay()
{
	Super::BeginPlay();
	ClosedRotation = Mesh->GetRelativeRotation();
	if (HasAuthority())
	{
		NPCTrigger->OnComponentBeginOverlap.AddDynamic(this, &AHitmenDoor::HandleNPCOverlap);
	}
}

bool AHitmenDoor::HasKeycard(const AActor* Interactor) const
{
	if (RequiredKeycard < 0)
	{
		return true;
	}
	const UHitmenInventoryComponent* Inventory = Interactor ? Interactor->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	return Inventory && Inventory->HasKeycard(RequiredKeycard);
}

bool AHitmenDoor::CanInteract_Implementation(AActor* Interactor) const
{
	// Locked doors still show their prompt so the player learns which card is needed.
	return AHitmenInteractableBase::CanInteract_Implementation(Interactor);
}

FText AHitmenDoor::GetInteractionText_Implementation(AActor* Interactor) const
{
	if (!bOpen && !HasKeycard(Interactor))
	{
		return FText::Format(NSLOCTEXT("Hitmen", "Locked", "Locked - keycard {0}"), FText::AsNumber(RequiredKeycard));
	}
	return Super::GetInteractionText_Implementation(Interactor);
}

void AHitmenDoor::OnInteracted(AActor* Interactor)
{
	if (!bOpen && !HasKeycard(Interactor))
	{
		return;
	}
	SetOpen(!bOpen, Interactor);
}

void AHitmenDoor::ApplyOpenAlpha(float Alpha)
{
	Mesh->SetRelativeRotation(ClosedRotation + FRotator(0.f, OpenAngle * OpenDirection * Alpha, 0.f));
}

void AHitmenDoor::HandleNPCOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	const AHitmenNPC* NPC = Cast<AHitmenNPC>(OtherActor);
	if (!NPC || NPC->IsDead() || bOpen)
	{
		return;
	}
	SetOpen(true, NPC);
	GetWorldTimerManager().SetTimer(AutoCloseTimer, FTimerDelegate::CreateWeakLambda(this, [this]() { SetOpen(false); }), NPCAutoCloseDelay, false);
}

// ---------------------------------------------------------------------------------------------
// Window
// ---------------------------------------------------------------------------------------------

AHitmenWindow::AHitmenWindow()
{
	OpenText = NSLOCTEXT("Hitmen", "OpenWindow", "Open window");
	CloseText = NSLOCTEXT("Hitmen", "CloseWindow", "Close window");
}

void AHitmenWindow::BeginPlay()
{
	Super::BeginPlay();
	ClosedLocation = Mesh->GetRelativeLocation();
}

void AHitmenWindow::ApplyOpenAlpha(float Alpha)
{
	Mesh->SetRelativeLocation(ClosedLocation + FVector::UpVector * SlideDistance * Alpha);
}

// ---------------------------------------------------------------------------------------------
// Vent hatch
// ---------------------------------------------------------------------------------------------

AHitmenVentHatch::AHitmenVentHatch()
{
	OpenText = NSLOCTEXT("Hitmen", "OpenVent", "Open vent");
	CloseText = NSLOCTEXT("Hitmen", "CloseVent", "Close vent");
}

void AHitmenVentHatch::BeginPlay()
{
	Super::BeginPlay();
	ClosedRotation = Mesh->GetRelativeRotation();
}

void AHitmenVentHatch::ApplyOpenAlpha(float Alpha)
{
	Mesh->SetRelativeRotation(ClosedRotation + FRotator(OpenAngle * Alpha, 0.f, 0.f));
}

void AHitmenVentHatch::ApplyPoison_Implementation(AActor* Poisoner)
{
	if (!HasAuthority())
	{
		return;
	}
	for (AHitmenRoomVolume* Room : LinkedRooms)
	{
		if (Room)
		{
			Room->TryPoison(Poisoner);
		}
	}
}
