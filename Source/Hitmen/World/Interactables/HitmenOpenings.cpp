// Hitmen — openings

#include "World/Interactables/HitmenOpenings.h"
#include "Engine/StaticMesh.h"
#include "Hitmen.h"
#include "UObject/ConstructorHelpers.h"
#include "World/HitmenGeometry.h"
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
		// Swing away from whoever opens it: the side is measured across the opening (its swing normal).
		const FVector ToOpener = (Opener->GetActorLocation() - GetActorLocation()).GetSafeNormal2D();
		OpenDirection = FVector::DotProduct(ToOpener, GetSwingNormal()) > 0.f ? -1.f : 1.f;
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
	NPCTrigger->SetBoxExtent(FVector(110.f, 110.f, 100.f));
	NPCTrigger->SetRelativeLocation(FVector(55.f, 0.f, 100.f));
	NPCTrigger->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	Mesh->SetCanEverAffectNavigation(false);
}

void AHitmenOpeningBase::CacheHinge(const FVector& EdgeDirection)
{
	HingeClosedRotation = Mesh->GetRelativeRotation();
	HingeClosedLocation = Mesh->GetRelativeLocation();
	FVector HalfSize = FVector::ZeroVector;
	if (const UStaticMesh* StaticMesh = Mesh->GetStaticMesh())
	{
		const FBoxSphereBounds Bounds = StaticMesh->GetBounds();
		HalfSize = Bounds.BoxExtent * Mesh->GetRelativeScale3D();
		// Mesh pivot may not be at its centre: the hinge is on the bounds' edge.
		HalfSize += Bounds.Origin * Mesh->GetRelativeScale3D() * EdgeDirection.GetAbs();
	}
	// The mesh origin sits HalfSize away from its edge along -EdgeDirection.
	HingeToCenter = -EdgeDirection * HalfSize;
}

void AHitmenOpeningBase::RotateAroundHinge(const FRotator& Delta)
{
	const FQuat Closed = HingeClosedRotation.Quaternion();
	const FVector Hinge = HingeClosedLocation - Closed.RotateVector(HingeToCenter);
	const FQuat Open = Closed * Delta.Quaternion();
	Mesh->SetRelativeLocationAndRotation(Hinge + Open.RotateVector(HingeToCenter), Open.Rotator());
}

void AHitmenDoor::BeginPlay()
{
	Super::BeginPlay();
	ClosedRotation = Mesh->GetRelativeRotation();
	// Doors swing on their left edge (local -X), like a real door.
	CacheHinge(FVector(-1.f, 0.f, 0.f));
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
		// The action stays readable; the reason sits in brackets after it.
		return FText::Format(NSLOCTEXT("Hitmen", "LockedDoor", "{0}  [Locked - Keycard {1}]"), OpenText, FText::AsNumber(RequiredKeycard));
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
	RotateAroundHinge(FRotator(0.f, OpenAngle * OpenDirection * Alpha, 0.f));
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

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(HitmenGeometry::CubePath);
	auto MakePiece = [this](const TCHAR* Name, USceneComponent* Parent)
	{
		UStaticMeshComponent* Piece = CreateDefaultSubobject<UStaticMeshComponent>(Name);
		Piece->SetupAttachment(Parent);
		if (Cube.Succeeded())
		{
			Piece->SetStaticMesh(Cube.Object);
		}
		Piece->SetCollisionProfileName(TEXT("BlockAllDynamic"));
		Piece->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
		return Piece;
	};
	FrameTop = MakePiece(TEXT("FrameTop"), Root);
	FrameLeft = MakePiece(TEXT("FrameLeft"), Root);
	FrameRight = MakePiece(TEXT("FrameRight"), Root);
	LeftHinge = CreateDefaultSubobject<USceneComponent>(TEXT("LeftHinge"));
	LeftHinge->SetupAttachment(Root);
	RightHinge = CreateDefaultSubobject<USceneComponent>(TEXT("RightHinge"));
	RightHinge->SetupAttachment(Root);
	LeftShutter = MakePiece(TEXT("LeftShutter"), LeftHinge);
	RightShutter = MakePiece(TEXT("RightShutter"), RightHinge);
	if (Cube.Succeeded())
	{
		Mesh->SetStaticMesh(Cube.Object);
	}
}

void AHitmenWindow::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	// Origin = floor; the opening spans [SillHeight, SillHeight + Height] and [-Width/2, Width/2].
	// The frame sits just inside the wall's opening (Inset) so none of its faces is coplanar with the wall's
	// opening faces (that flickered); the clear opening is the inner frame.
	constexpr float Inset = 0.3f;
	const float T = FrameThickness;
	const float Z0 = SillHeight;
	const float Z1 = SillHeight + Height;
	const float InnerHalf = Width * 0.5f - T - Inset;
	HitmenGeometry::SetBox(Mesh, FVector(0.f, 0.f, Z0 + Inset + T * 0.5f), FVector(FrameDepth, Width - 2.f * Inset, T));
	HitmenGeometry::SetBox(FrameTop, FVector(0.f, 0.f, Z1 - Inset - T * 0.5f), FVector(FrameDepth, Width - 2.f * Inset, T));
	HitmenGeometry::SetBox(FrameLeft, FVector(0.f, -(InnerHalf + T * 0.5f), (Z0 + Z1) * 0.5f), FVector(FrameDepth, T, Height - 2.f * (T + Inset)));
	HitmenGeometry::SetBox(FrameRight, FVector(0.f, InnerHalf + T * 0.5f, (Z0 + Z1) * 0.5f), FVector(FrameDepth, T, Height - 2.f * (T + Inset)));
	for (UStaticMeshComponent* Piece : { FrameTop.Get(), FrameLeft.Get(), FrameRight.Get() })
	{
		HitmenGeometry::InheritLook(Piece, Mesh);
	}
	// Hinges on the inner frame edges; each shutter covers half the clear opening. They overlap a little in the
	// middle (no gap a body or a probe could pass) at two different depths, so they never share a plane.
	const float ShutterHeight = Height - 2.f * (T + Inset) - 1.f;
	LeftHinge->SetRelativeLocation(FVector(-ShutterThickness * 0.5f - 0.1f, -InnerHalf, (Z0 + Z1) * 0.5f));
	RightHinge->SetRelativeLocation(FVector(ShutterThickness * 0.5f + 0.1f, InnerHalf, (Z0 + Z1) * 0.5f));
	HitmenGeometry::SetBox(LeftShutter, FVector(0.f, InnerHalf * 0.5f + 0.5f, 0.f), FVector(ShutterThickness, InnerHalf + 1.f, ShutterHeight));
	HitmenGeometry::SetBox(RightShutter, FVector(0.f, -InnerHalf * 0.5f - 0.5f, 0.f), FVector(ShutterThickness, InnerHalf + 1.f, ShutterHeight));
	ApplyOpenAlpha(bOpen ? 1.f : 0.f);
}

void AHitmenWindow::ApplyOpenAlpha(float Alpha)
{
	// Both shutters swing toward the side away from the opener (OpenDirection).
	const float Angle = ShutterOpenAngle * Alpha * OpenDirection;
	LeftHinge->SetRelativeRotation(FRotator(0.f, -Angle, 0.f));
	RightHinge->SetRelativeRotation(FRotator(0.f, Angle, 0.f));
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
	// Hatches swing up on their top edge.
	CacheHinge(FVector(0.f, 0.f, 1.f));
}

void AHitmenVentHatch::ApplyOpenAlpha(float Alpha)
{
	// The panel lies in X-Z (thin along Y): it swings out of its plane around the top edge, like a small door.
	RotateAroundHinge(FRotator(0.f, 0.f, -OpenAngle * OpenDirection * Alpha));
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
