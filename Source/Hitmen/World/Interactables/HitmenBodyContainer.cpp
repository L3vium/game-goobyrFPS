// Hitmen — body container

#include "World/Interactables/HitmenBodyContainer.h"
#include "World/HitmenGeometry.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "UObject/ConstructorHelpers.h"
#include "Hitmen.h"

AHitmenBodyContainer::AHitmenBodyContainer()
{
	DisplayName = NSLOCTEXT("Hitmen", "BodyContainer", "Body container");
	BodyCapacity = 2;
	// Two bodies lying side by side along the bin (rolled like in the arms).
	BodySlots = {
		FTransform(FRotator(0.f, 0.f, 90.f), FVector(-22.f, 0.f, 32.f)),
		FTransform(FRotator(0.f, 180.f, 90.f), FVector(22.f, 0.f, 32.f)) };
	// Crouched inside: the eye at the slit under the rim (capsule centre = eye - 64).
	InsideOffset = FVector(-8.f, 0.f, 24.f);
	ExitOffset = FVector(120.f, 0.f, 96.f);
	OpenCycleTime = 0.8f;
	ViewPitchMin = -30.f;
	ViewPitchMax = 25.f;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(HitmenGeometry::CubePath);
	auto MakePanel = [this](const TCHAR* Name, USceneComponent* Parent)
	{
		UStaticMeshComponent* Panel = CreateDefaultSubobject<UStaticMeshComponent>(Name);
		Panel->SetupAttachment(Parent);
		if (Cube.Succeeded())
		{
			Panel->SetStaticMesh(Cube.Object);
		}
		Panel->SetCollisionProfileName(TEXT("BlockAllDynamic"));
		Panel->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
		return Panel;
	};
	if (Cube.Succeeded())
	{
		Mesh->SetStaticMesh(Cube.Object);
	}
	WallLeft = MakePanel(TEXT("WallLeft"), Root);
	WallRight = MakePanel(TEXT("WallRight"), Root);
	WallFrontLower = MakePanel(TEXT("WallFrontLower"), Root);
	WallFrontUpper = MakePanel(TEXT("WallFrontUpper"), Root);
	Floor = MakePanel(TEXT("Floor"), Root);
	LidPivot = CreateDefaultSubobject<USceneComponent>(TEXT("LidPivot"));
	LidPivot->SetupAttachment(Root);
	Lid = MakePanel(TEXT("Lid"), LidPivot);
}

void AHitmenBodyContainer::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	const float T = WallThickness;
	const float HalfD = Depth * 0.5f;
	const float HalfW = Width * 0.5f;
	// Back wall (Mesh), sides, floor, the front split by the peek slit; actor origin = floor centre, front at +X.
	HitmenGeometry::SetBox(Mesh, FVector(-HalfD + T * 0.5f, 0.f, Height * 0.5f), FVector(T, Width, Height));
	HitmenGeometry::SetBox(WallLeft, FVector(0.f, -HalfW + T * 0.5f, Height * 0.5f), FVector(Depth, T, Height));
	HitmenGeometry::SetBox(WallRight, FVector(0.f, HalfW - T * 0.5f, Height * 0.5f), FVector(Depth, T, Height));
	HitmenGeometry::SetBox(Floor, FVector(0.f, 0.f, T * 0.5f), FVector(Depth, Width, T));
	const float SlitBottom = FMath::Clamp(SlitHeight - SlitGap * 0.5f, T, Height - T);
	const float SlitTop = FMath::Clamp(SlitHeight + SlitGap * 0.5f, SlitBottom, Height);
	HitmenGeometry::SetBox(WallFrontLower, FVector(HalfD - T * 0.5f, 0.f, SlitBottom * 0.5f), FVector(T, Width, SlitBottom));
	HitmenGeometry::SetBox(WallFrontUpper, FVector(HalfD - T * 0.5f, 0.f, (SlitTop + Height) * 0.5f), FVector(T, Width, FMath::Max(Height - SlitTop, 1.f)));
	// The lid lies on top, hinged on the back edge.
	LidPivot->SetRelativeLocationAndRotation(FVector(-HalfD, 0.f, Height), FRotator::ZeroRotator);
	HitmenGeometry::SetBox(Lid, FVector(HalfD, 0.f, T * 0.5f), FVector(Depth + 4.f, Width + 4.f, T));
	for (UStaticMeshComponent* Panel : { WallLeft.Get(), WallRight.Get(), WallFrontLower.Get(), WallFrontUpper.Get(), Floor.Get() })
	{
		HitmenGeometry::InheritLook(Panel, Mesh);
	}
}

void AHitmenBodyContainer::ApplyOpenAlpha(float Alpha)
{
	LidPivot->SetRelativeRotation(FRotator(LidOpenAngle * Alpha, 0.f, 0.f));
}
