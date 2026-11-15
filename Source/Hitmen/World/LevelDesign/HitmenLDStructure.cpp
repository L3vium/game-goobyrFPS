// Hitmen — level design structures

#include "World/LevelDesign/HitmenLDStructure.h"
#include "Hitmen.h"
#include "Components/InstancedStaticMeshComponent.h"
#include "Components/PointLightComponent.h"
#include "Components/SplineComponent.h"
#include "Containers/Ticker.h"
#include "Engine/StaticMesh.h"
#include "Engine/StaticMeshActor.h"
#include "Engine/World.h"
#include "Materials/MaterialInterface.h"
#include "PCGComponent.h"
#include "PCGGraph.h"
#include "UObject/ConstructorHelpers.h"

namespace HitmenLD
{
	/** The builders' numbers (hitmen_levels.py): ledges, vents, stair lanes. */
	constexpr float LedgeDepth = 70.f;
	constexpr float LedgeDrop = 15.f;
	constexpr float VentSize = 120.f;
	constexpr float StairLane = 280.f;
	constexpr float WallThickness = 25.f;

	const TCHAR* CubePath = TEXT("/Engine/BasicShapes/Cube.Cube");

	TSoftClassPtr<AActor> SoftClass(const TCHAR* Path)
	{
		return TSoftClassPtr<AActor>(FSoftObjectPath(Path));
	}

	/** Sets a fixture's tunable before its construction (the members are protected; the builders set them the same
	 *  way through Python). */
	void SetFloat(UObject* Object, FName Name, float Value)
	{
		if (FFloatProperty* Property = FindFProperty<FFloatProperty>(Object->GetClass(), Name))
		{
			Property->SetPropertyValue_InContainer(Object, Value);
		}
	}

	void SetBool(UObject* Object, FName Name, bool bValue)
	{
		if (FBoolProperty* Property = FindFProperty<FBoolProperty>(Object->GetClass(), Name))
		{
			Property->SetPropertyValue_InContainer(Object, bValue);
		}
	}
}

// =============================================================================================
// Openings
// =============================================================================================

FVector FHitmenLDOpening::Resolve() const
{
	FVector Default;
	switch (Type)
	{
	case EHitmenLDOpeningType::Door:   Default = FVector(110.f, 0.f, 230.f); break;
	case EHitmenLDOpeningType::Vent:   Default = FVector(HitmenLD::VentSize, 0.f, HitmenLD::VentSize); break;
	case EHitmenLDOpeningType::Window: Default = FVector(120.f, 100.f, 220.f); break;
	default:                           Default = FVector(150.f, 0.f, 230.f); break;
	}
	return FVector(Width > 0.f ? Width : Default.X, Sill >= 0.f ? Sill : Default.Y, Top > 0.f ? Top : Default.Z);
}

// =============================================================================================
// Base
// =============================================================================================

AHitmenLDStructure::AHitmenLDStructure()
{
	PrimaryActorTick.bCanEverTick = false;
	bReplicates = false;

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Static);
	RootComponent = Root;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(HitmenLD::CubePath);
	auto MakePieces = [this](const TCHAR* Name, const TCHAR* Tag)
	{
		UInstancedStaticMeshComponent* Pieces = CreateDefaultSubobject<UInstancedStaticMeshComponent>(Name);
		Pieces->SetupAttachment(Root);
		Pieces->SetMobility(EComponentMobility::Static);
		Pieces->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);
		Pieces->ComponentTags.Add(Tag);
		return Pieces;
	};
	WallPieces = MakePieces(TEXT("Walls"), TEXT("HitmenLD_Wall"));
	FloorPieces = MakePieces(TEXT("Floors"), TEXT("HitmenLD_Floor"));
	TrimPieces = MakePieces(TEXT("Trim"), TEXT("HitmenLD_Trim"));
	if (Cube.Succeeded())
	{
		WallPieces->SetStaticMesh(Cube.Object);
		FloorPieces->SetStaticMesh(Cube.Object);
		TrimPieces->SetStaticMesh(Cube.Object);
	}

	static ConstructorHelpers::FObjectFinder<UMaterialInterface> WallLook(TEXT("/Game/LevelPrototyping/Materials/MI_PrototypeGrid_TopDark.MI_PrototypeGrid_TopDark"));
	static ConstructorHelpers::FObjectFinder<UMaterialInterface> FloorLook(TEXT("/Game/LevelPrototyping/Materials/MI_PrototypeGrid_Gray.MI_PrototypeGrid_Gray"));
	static ConstructorHelpers::FObjectFinder<UMaterialInterface> TrimLook(TEXT("/Game/LevelPrototyping/Materials/MI_PrototypeGrid_Gray_02.MI_PrototypeGrid_Gray_02"));
	WallMaterial = WallLook.Object;
	FloorMaterial = FloorLook.Object;
	TrimMaterial = TrimLook.Object;

	DoorClass = HitmenLD::SoftClass(TEXT("/Game/Hitmen/World/Interactables/Door/BP_Door.BP_Door_C"));
	WindowClass = HitmenLD::SoftClass(TEXT("/Game/Hitmen/World/Interactables/Window/BP_Window.BP_Window_C"));
	VentHatchClass = HitmenLD::SoftClass(TEXT("/Game/Hitmen/World/Interactables/Vent/BP_VentHatch.BP_VentHatch_C"));
	LadderClass = HitmenLD::SoftClass(TEXT("/Game/Hitmen/World/Interactables/Ladder/BP_Ladder.BP_Ladder_C"));
	GutterClass = HitmenLD::SoftClass(TEXT("/Game/Hitmen/World/Interactables/Ladder/BP_Gutter.BP_Gutter_C"));
	LampClass = HitmenLD::SoftClass(TEXT("/Game/Hitmen/World/Interactables/Power/BP_LightFixture.BP_LightFixture_C"));
}

float AHitmenLDStructure::GetMetricHeight(EHitmenLDMetric Metric)
{
	switch (Metric)
	{
	case EHitmenLDMetric::Step:       return 50.f;
	case EHitmenLDMetric::Vault:      return 110.f;
	case EHitmenLDMetric::Climb:      return 240.f;
	case EHitmenLDMetric::OutOfReach: return 300.f;
	default:                          return 0.f;
	}
}

float AHitmenLDStructure::Snap(float Value) const
{
	return GridSnap > 0.f ? FMath::Max(GridSnap, FMath::GridSnap(Value, GridSnap)) : Value;
}

void AHitmenLDStructure::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	const TPair<UInstancedStaticMeshComponent*, UMaterialInterface*> Looks[] = { { WallPieces, WallMaterial }, { FloorPieces, FloorMaterial }, { TrimPieces, TrimMaterial } };
	for (const TPair<UInstancedStaticMeshComponent*, UMaterialInterface*>& Look : Looks)
	{
		Look.Key->ClearInstances();
		if (Look.Value)
		{
			Look.Key->SetMaterial(0, Look.Value);
		}
	}
	BuildBoxes();
#if WITH_EDITOR
	// First placement (or a duplicate): the fixtures come right after construction (never spawn inside it).
	if (CanSpawnFixtures() && Fixtures.IsEmpty() && !bFixturesDirty)
	{
		bFixturesDirty = true;
		TWeakObjectPtr<AHitmenLDStructure> WeakThis(this);
		FTSTicker::GetCoreTicker().AddTicker(FTickerDelegate::CreateLambda([WeakThis](float)
		{
			if (AHitmenLDStructure* Structure = WeakThis.Get())
			{
				if (Structure->bFixturesDirty && Structure->Fixtures.IsEmpty() && Structure->CanSpawnFixtures())
				{
					Structure->RebuildFixtures();
				}
				Structure->bFixturesDirty = false;
			}
			return false;
		}));
	}
#endif
}

bool AHitmenLDStructure::CanSpawnFixtures() const
{
	const UWorld* World = GetWorld();
	return World && World->WorldType == EWorldType::Editor && !IsTemplate() && !HasAnyFlags(RF_Transient | RF_ClassDefaultObject) && IsValid(this);
}

void AHitmenLDStructure::ClearFixtures()
{
	for (AActor* Fixture : Fixtures)
	{
		// Only ours: a copy of this structure still lists the original's fixtures until it spawns its own.
		if (IsValid(Fixture) && Fixture->GetAttachParentActor() == this)
		{
			Fixture->Destroy();
		}
	}
	Fixtures.Reset();
}

void AHitmenLDStructure::RebuildFixtures()
{
	if (!CanSpawnFixtures())
	{
		return;
	}
	Modify();
	ClearFixtures();
	BuildFixtures();
}

AActor* AHitmenLDStructure::AddFixture(const TSoftClassPtr<AActor>& Class, const FVector& Location, float Yaw)
{
	return AddFixture(Class, Location, Yaw, [](AActor*) {});
}

AActor* AHitmenLDStructure::AddFixture(const TSoftClassPtr<AActor>& Class, const FVector& Location, float Yaw, TFunctionRef<void(AActor*)> Configure)
{
	UClass* Loaded = Class.LoadSynchronous();
	UWorld* World = GetWorld();
	if (!Loaded || !World)
	{
		return nullptr;
	}
	const FTransform Local(FRotator(0.f, Yaw, 0.f), Location);
	const FTransform WorldTransform = Local * GetActorTransform();
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	Params.bDeferConstruction = true;
	Params.OverrideLevel = GetLevel();
	AActor* Fixture = World->SpawnActor(Loaded, &WorldTransform, Params);
	if (!Fixture)
	{
		return nullptr;
	}
	Configure(Fixture);
	Fixture->FinishSpawning(WorldTransform);
	Fixture->AttachToActor(this, FAttachmentTransformRules::KeepWorldTransform);
#if WITH_EDITOR
	Fixture->SetFolderPath(GetFolderPath().IsNone() ? FName(*GetActorLabel()) : FName(*(GetFolderPath().ToString() / GetActorLabel())));
#endif
	Fixtures.Add(Fixture);
	return Fixture;
}

void AHitmenLDStructure::AddBox(EPiece Piece, const FVector& Center, const FVector& Size, float Yaw)
{
	if (Size.X < 1.f || Size.Y < 1.f || Size.Z < 1.f)
	{
		return;
	}
	UInstancedStaticMeshComponent* Pieces = Piece == EPiece::Wall ? WallPieces : (Piece == EPiece::Floor ? FloorPieces : TrimPieces);
	Pieces->AddInstance(FTransform(FRotator(0.f, Yaw, 0.f), Center, Size / 100.f));
}

void AHitmenLDStructure::AddWall(EPiece Piece, const FVector2D& Start, const FVector2D& End, float Z0, float Height, float Thickness, TArray<FVector4> Openings)
{
	const float Length = FVector2D::Distance(Start, End);
	if (Length < 1.f)
	{
		return;
	}
	const FVector2D Dir = (End - Start) / Length;
	const float Yaw = FMath::RadiansToDegrees(FMath::Atan2(Dir.Y, Dir.X));
	auto Segment = [&](float T0, float T1, float ZB, float ZT)
	{
		if (T1 - T0 < 1.f || ZT - ZB < 1.f)
		{
			return;
		}
		const FVector2D Mid = Start + Dir * ((T0 + T1) * 0.5f);
		AddBox(Piece, FVector(Mid, (ZB + ZT) * 0.5f), FVector(T1 - T0, Thickness, ZT - ZB), Yaw);
	};
	Openings.Sort([](const FVector4& A, const FVector4& B) { return A.X < B.X; });
	float Cursor = 0.f;
	for (const FVector4& Opening : Openings)
	{
		const float A = Opening.X - Opening.Y * 0.5f;
		const float B = Opening.X + Opening.Y * 0.5f;
		Segment(Cursor, A, Z0, Z0 + Height);
		Segment(A, B, Z0, Z0 + Opening.Z);
		Segment(A, B, Z0 + Opening.W, Z0 + Height);
		Cursor = B;
	}
	Segment(Cursor, Length, Z0, Z0 + Height);
}

void AHitmenLDStructure::AddStairs(const FVector& Start, float Yaw, float Rise, float Run, float Width, int32 Steps)
{
	Steps = Steps > 0 ? Steps : FMath::Max(4, FMath::FloorToInt(Rise / 20.f));
	const float StepRise = Rise / Steps;
	const float StepRun = Run / Steps;
	const FVector Dir = FRotator(0.f, Yaw, 0.f).Vector();
	for (int32 Index = 0; Index < Steps; ++Index)
	{
		const float H = StepRise * (Index + 1);
		const FVector Center = Start + Dir * (StepRun * (Index + 0.5f));
		AddBox(EPiece::Floor, FVector(Center.X, Center.Y, Start.Z + H * 0.5f), FVector(StepRun, Width, H), Yaw);
	}
}

void AHitmenLDStructure::BakeToStaticMeshes()
{
#if WITH_EDITOR
	UWorld* World = GetWorld();
	if (!CanSpawnFixtures() || !World)
	{
		return;
	}
	Modify();
	const FName Folder = GetFolderPath().IsNone() ? FName(*GetActorLabel()) : FName(*(GetFolderPath().ToString() / GetActorLabel()));
	for (UInstancedStaticMeshComponent* Pieces : { WallPieces.Get(), FloorPieces.Get(), TrimPieces.Get() })
	{
		for (int32 Index = 0; Index < Pieces->GetInstanceCount(); ++Index)
		{
			FTransform Transform;
			Pieces->GetInstanceTransform(Index, Transform, true);
			FActorSpawnParameters Params;
			Params.OverrideLevel = GetLevel();
			AStaticMeshActor* Box = World->SpawnActor<AStaticMeshActor>(Transform.GetLocation(), Transform.Rotator(), Params);
			if (!Box)
			{
				continue;
			}
			Box->SetActorScale3D(Transform.GetScale3D());
			Box->GetStaticMeshComponent()->SetStaticMesh(Pieces->GetStaticMesh());
			Box->GetStaticMeshComponent()->SetMaterial(0, Pieces->GetMaterial(0));
			Box->SetFolderPath(Folder);
		}
	}
	for (AActor* Fixture : Fixtures)
	{
		if (IsValid(Fixture) && Fixture->GetAttachParentActor() == this)
		{
			Fixture->DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
		}
	}
	Fixtures.Reset();
	World->EditorDestroyActor(this, true);
#endif
}

#if WITH_EDITOR
void AHitmenLDStructure::PostEditChangeProperty(FPropertyChangedEvent& PropertyChangedEvent)
{
	Super::PostEditChangeProperty(PropertyChangedEvent);
	if (PropertyChangedEvent.ChangeType != EPropertyChangeType::Interactive && PropertyChangedEvent.GetPropertyName() != GET_MEMBER_NAME_CHECKED(AHitmenLDStructure, Fixtures))
	{
		RebuildFixtures();
	}
}

void AHitmenLDStructure::PostDuplicate(bool bDuplicateForPIE)
{
	Super::PostDuplicate(bDuplicateForPIE);
	if (!bDuplicateForPIE)
	{
		// A copy spawns its own fixtures (the list still names the original's).
		Fixtures.Reset();
	}
}

void AHitmenLDStructure::PostEditImport()
{
	Super::PostEditImport();
	Fixtures.Reset();
}

void AHitmenLDStructure::Destroyed()
{
	if (CanSpawnFixtures() || (GetWorld() && GetWorld()->WorldType == EWorldType::Editor))
	{
		ClearFixtures();
	}
	Super::Destroyed();
}
#endif

// =============================================================================================
// Block, wall, stairs
// =============================================================================================

void AHitmenLDBlock::BuildBoxes()
{
	const float H = Height == EHitmenLDMetric::Custom ? CustomHeight : GetMetricHeight(Height);
	const FVector Size(Snap(Footprint.X), Snap(Footprint.Y), FMath::Max(H, 1.f));
	AddBox(EPiece::Wall, FVector(0.f, 0.f, Size.Z * 0.5f), Size);
}

float AHitmenLDWall::GetHeight() const
{
	return Height == EHitmenLDMetric::Custom ? CustomHeight : GetMetricHeight(Height);
}

void AHitmenLDWall::BuildBoxes()
{
	TArray<FVector4> Holes;
	for (const FHitmenLDOpening& Opening : Openings)
	{
		const FVector Size = Opening.Resolve();
		Holes.Add(FVector4(Opening.Offset, Size.X, Size.Y, Size.Z));
	}
	AddWall(EPiece::Wall, FVector2D::ZeroVector, FVector2D(Snap(Length), 0.f), 0.f, GetHeight(), Thickness, Holes);
}

void AHitmenLDWall::BuildFixtures()
{
	for (const FHitmenLDOpening& Opening : Openings)
	{
		if (!Opening.bFixture)
		{
			continue;
		}
		const FVector Size = Opening.Resolve();
		switch (Opening.Type)
		{
		case EHitmenLDOpeningType::Door:
			AddFixture(DoorClass, FVector(Opening.Offset - Size.X * 0.5f, 0.f, 0.f), 0.f);
			break;
		case EHitmenLDOpeningType::Window:
			AddFixture(WindowClass, FVector(Opening.Offset, 0.f, Size.Y - 100.f), 90.f, [&Opening](AActor* Fixture)
			{
				HitmenLD::SetBool(Fixture, TEXT("bOpen"), Opening.bOpen);
			});
			break;
		case EHitmenLDOpeningType::Vent:
			AddFixture(VentHatchClass, FVector(Opening.Offset, 0.f, Size.Y), 0.f);
			break;
		default:
			break;
		}
	}
}

void AHitmenLDStairs::BuildBoxes()
{
	AddStairs(FVector::ZeroVector, 0.f, Rise, Run, Width, Steps);
}

// =============================================================================================
// Building
// =============================================================================================

AHitmenLDBuilding::AHitmenLDBuilding()
{
	RoofOutline = CreateDefaultSubobject<USplineComponent>(TEXT("RoofOutline"));
	RoofOutline->SetupAttachment(Root);
	RoofOutline->ComponentTags.Add(TEXT("HitmenLD_Roof"));
	RoofOutline->SetClosedLoop(true);
	RoofDressing = CreateDefaultSubobject<UPCGComponent>(TEXT("RoofDressing"));
	RoofDressing->GenerationTrigger = EPCGComponentGenerationTrigger::GenerateOnDemand;
	RoofDressingGraph = TSoftObjectPtr<UPCGGraphInterface>(FSoftObjectPath(TEXT("/Game/Hitmen/PCG/PCG_Hitmen_RoofDressing.PCG_Hitmen_RoofDressing")));
}

void AHitmenLDBuilding::GetFace(EHitmenLDFace Face, FVector2D& OutStart, FVector2D& OutEnd, FVector2D& OutNormal) const
{
	const float X0 = -Snap(Width) * 0.5f, X1 = Snap(Width) * 0.5f, Y0 = -Snap(Depth) * 0.5f, Y1 = Snap(Depth) * 0.5f;
	switch (Face)
	{
	case EHitmenLDFace::South: OutStart = FVector2D(X0, Y0); OutEnd = FVector2D(X1, Y0); OutNormal = FVector2D(0.f, -1.f); break;
	case EHitmenLDFace::North: OutStart = FVector2D(X0, Y1); OutEnd = FVector2D(X1, Y1); OutNormal = FVector2D(0.f, 1.f); break;
	case EHitmenLDFace::West:  OutStart = FVector2D(X0, Y0); OutEnd = FVector2D(X0, Y1); OutNormal = FVector2D(-1.f, 0.f); break;
	default:                   OutStart = FVector2D(X1, Y0); OutEnd = FVector2D(X1, Y1); OutNormal = FVector2D(1.f, 0.f); break;
	}
}

TArray<float> AHitmenLDBuilding::WindowPlaces(EHitmenLDFace Face) const
{
	TArray<float> Places;
	if (!bWindows || IsBlind(Face))
	{
		return Places;
	}
	const bool bAlongX = Face == EHitmenLDFace::South || Face == EHitmenLDFace::North;
	const float Length = bAlongX ? Snap(Width) : Snap(Depth);
	if (bAlongX)
	{
		Places = { Length * 0.25f, Length * 0.75f };
	}
	else if (Length < 1300.f)
	{
		Places = { Length * 0.5f };
	}
	else
	{
		Places = { Length * 0.3f, Length * 0.7f };
	}
	return Places;
}

FVector2D AHitmenLDBuilding::GutterSpot(float& OutYaw) const
{
	const float W = Snap(Width), D = Snap(Depth);
	switch (GutterFace)
	{
	case EHitmenLDFace::West:  OutYaw = 180.f; return FVector2D(-W * 0.5f - 15.f, D * 0.3f);
	case EHitmenLDFace::East:  OutYaw = 0.f;   return FVector2D(W * 0.5f + 15.f, D * 0.3f);
	case EHitmenLDFace::South: OutYaw = -90.f; return FVector2D(W * 0.3f, -D * 0.5f - 15.f);
	default:                   OutYaw = 90.f;  return FVector2D(W * 0.3f, D * 0.5f + 15.f);
	}
}

void AHitmenLDBuilding::BuildBoxes()
{
	using namespace HitmenLD;
	const float W = Snap(Width), D = Snap(Depth), FH = FloorHeight, H = Floors * FH;
	const float X0 = -W * 0.5f, X1 = W * 0.5f, Y0 = -D * 0.5f, Y1 = D * 0.5f;
	const float SY0 = Y0 + 120.f, SY1 = 60.f;
	const EHitmenLDFace Faces[] = { EHitmenLDFace::South, EHitmenLDFace::North, EHitmenLDFace::West, EHitmenLDFace::East };
	float GutterYaw = 0.f;
	const FVector2D Gutter = GutterSpot(GutterYaw);
	for (int32 F = 0; F < Floors; ++F)
	{
		const float Z = F * FH;
		for (const EHitmenLDFace Face : Faces)
		{
			FVector2D Start, End, Normal;
			GetFace(Face, Start, End, Normal);
			const float Length = FVector2D::Distance(Start, End);
			TArray<FVector4> Holes;
			const bool bDoorHere = F == 0 && Face == DoorFace;
			for (const float At : WindowPlaces(Face))
			{
				if (!bDoorHere || FMath::Abs(At - Length * 0.5f) > 200.f)
				{
					Holes.Add(FVector4(At, 120.f, 100.f, 220.f));
				}
			}
			if (bDoorHere)
			{
				Holes.Add(FVector4(Length * 0.5f, 110.f, 0.f, 230.f));
			}
			for (const FHitmenLDVent& Vent : Vents)
			{
				if (Vent.Face == Face && Vent.Floor == F)
				{
					Holes.Add(FVector4(Length * Vent.Along, VentSize, 0.f, VentSize));
				}
			}
			AddWall(EPiece::Wall, Start, End, Z, FH, WallThickness, Holes);
			// Ledges all around the upper floors (none on a blind face), with a gap where the gutter climbs.
			if (bLedges && bWindows && F > 0 && !IsBlind(Face))
			{
				const float Out = WallThickness * 0.5f + LedgeDepth * 0.5f;
				const float LZ = Z - LedgeDrop - 6.f;
				const bool bAlongX = Face == EHitmenLDFace::South || Face == EHitmenLDFace::North;
				const float A = bAlongX ? X0 - WallThickness * 0.5f - LedgeDepth : Y0 - WallThickness * 0.5f;
				const float B = bAlongX ? X1 + WallThickness * 0.5f + LedgeDepth : Y1 + WallThickness * 0.5f;
				TArray<FVector2D> Spans;
				if (bGutter && Face == GutterFace)
				{
					const float Gap = bAlongX ? Gutter.X : Gutter.Y;
					Spans = { FVector2D(A, Gap - 70.f), FVector2D(Gap + 70.f, B) };
				}
				else
				{
					Spans = { FVector2D(A, B) };
				}
				for (const FVector2D& Span : Spans)
				{
					if (Span.Y - Span.X <= 20.f)
					{
						continue;
					}
					const float Mid = (Span.X + Span.Y) * 0.5f;
					const FVector2D Line = Start + Normal * Out;
					AddBox(EPiece::Trim, bAlongX ? FVector(Mid, Line.Y, LZ) : FVector(Line.X, Mid, LZ),
						bAlongX ? FVector(Span.Y - Span.X, LedgeDepth, 12.f) : FVector(LedgeDepth, Span.Y - Span.X, 12.f));
				}
			}
		}
		if (F > 0)
		{
			// The slab, with a hole over the flight that comes up here (a 1.2 m landing at each end of its lane).
			AddBox(EPiece::Floor, FVector((X0 + X1 - 2.f * StairLane) * 0.5f, 0.f, Z), FVector(W - 2.f * StairLane, D, 25.f));
			for (int32 Lane = 0; Lane < 2; ++Lane)
			{
				const float LX = X1 - StairLane * (Lane + 0.5f);
				if (Lane == (F - 1) % 2)
				{
					AddBox(EPiece::Floor, FVector(LX, (Y0 + SY0) * 0.5f, Z), FVector(StairLane, SY0 - Y0, 25.f));
					AddBox(EPiece::Floor, FVector(LX, (SY1 + Y1) * 0.5f, Z), FVector(StairLane, Y1 - SY1, 25.f));
				}
				else
				{
					AddBox(EPiece::Floor, FVector(LX, 0.f, Z), FVector(StairLane, D, 25.f));
				}
			}
		}
	}
	AddBox(EPiece::Floor, FVector(0.f, 0.f, H), FVector(W, D, 25.f));
	// Switchback: even flights rise north on the east lane, odd ones south on the next lane.
	for (int32 F = 0; F < Floors - 1; ++F)
	{
		if (F % 2 == 0)
		{
			AddStairs(FVector(X1 - StairLane * 0.5f, SY0, F * FH), 90.f, FH, SY1 - SY0, 260.f);
		}
		else
		{
			AddStairs(FVector(X1 - StairLane * 1.5f, SY1, F * FH), -90.f, FH, SY1 - SY0, 260.f);
		}
	}
	// Vent ducts: from the hatch in the wall straight into the room, a hatch at the far end.
	for (const FHitmenLDVent& Vent : Vents)
	{
		if (Vent.DuctLength <= 0.f)
		{
			continue;
		}
		FVector2D Start, End, Normal;
		GetFace(Vent.Face, Start, End, Normal);
		const FVector2D Hatch = Start + (End - Start) * Vent.Along;
		const FVector2D Inward = -Normal;
		const FVector2D Mid = Hatch + Inward * (WallThickness * 0.5f + Vent.DuctLength * 0.5f);
		const float Z = Vent.Floor * FH;
		const float Yaw = FMath::RadiansToDegrees(FMath::Atan2(Inward.Y, Inward.X));
		const FVector2D Side(-Inward.Y, Inward.X);
		AddBox(EPiece::Trim, FVector(Mid, Z - 5.f), FVector(Vent.DuctLength, VentSize + 20.f, 10.f), Yaw);
		AddBox(EPiece::Trim, FVector(Mid, Z + VentSize + 5.f), FVector(Vent.DuctLength, VentSize + 20.f, 10.f), Yaw);
		for (const float Sign : { -1.f, 1.f })
		{
			AddBox(EPiece::Trim, FVector(Mid + Side * Sign * (VentSize * 0.5f + 5.f), Z + VentSize * 0.5f), FVector(Vent.DuctLength, 10.f, VentSize), Yaw);
		}
	}
}

void AHitmenLDBuilding::BuildFixtures()
{
	using namespace HitmenLD;
	const float W = Snap(Width), D = Snap(Depth), FH = FloorHeight, H = Floors * FH;
	const float Y0 = -D * 0.5f, Y1 = D * 0.5f;
	if (DoorFace == EHitmenLDFace::North)
	{
		AddFixture(DoorClass, FVector(55.f, Y1, 0.f), 180.f);
	}
	else
	{
		AddFixture(DoorClass, FVector(-55.f, Y0, 0.f), 0.f);
	}
	const EHitmenLDFace Faces[] = { EHitmenLDFace::South, EHitmenLDFace::North, EHitmenLDFace::West, EHitmenLDFace::East };
	auto FaceYaw = [](EHitmenLDFace Face)
	{
		switch (Face)
		{
		case EHitmenLDFace::South: return 90.f;
		case EHitmenLDFace::North: return -90.f;
		case EHitmenLDFace::West:  return 180.f;
		default:                   return 0.f;
		}
	};
	for (int32 F = 0; F < Floors; ++F)
	{
		const float Z = F * FH;
		for (const EHitmenLDFace Face : Faces)
		{
			FVector2D Start, End, Normal;
			GetFace(Face, Start, End, Normal);
			const float Length = FVector2D::Distance(Start, End);
			const FVector2D Dir = (End - Start) / Length;
			for (const float At : WindowPlaces(Face))
			{
				if (F == 0 && Face == DoorFace && FMath::Abs(At - Length * 0.5f) <= 200.f)
				{
					continue;
				}
				const FVector2D Spot = Start + Dir * At;
				// About one window in three starts open (fixed by its place).
				const bool bOpen = bSomeWindowsOpen && (FMath::FloorToInt(FMath::Abs(Spot.X) / 50.f) + FMath::FloorToInt(FMath::Abs(Spot.Y) / 50.f) + F) % 3 == 0;
				AddFixture(WindowClass, FVector(Spot, Z), FaceYaw(Face), [bOpen](AActor* Fixture)
				{
					HitmenLD::SetBool(Fixture, TEXT("bOpen"), bOpen);
				});
			}
		}
	}
	for (const FHitmenLDVent& Vent : Vents)
	{
		FVector2D Start, End, Normal;
		GetFace(Vent.Face, Start, End, Normal);
		const FVector2D Hatch = Start + (End - Start) * Vent.Along;
		const bool bAlongX = Vent.Face == EHitmenLDFace::South || Vent.Face == EHitmenLDFace::North;
		const float Z = Vent.Floor * FH;
		AddFixture(VentHatchClass, FVector(Hatch, Z), bAlongX ? 0.f : 90.f);
		if (Vent.DuctLength > 0.f)
		{
			AddFixture(VentHatchClass, FVector(Hatch - Normal * (WallThickness * 0.5f + Vent.DuctLength), Z), bAlongX ? 0.f : 90.f);
		}
	}
	if (bLamps)
	{
		for (int32 F = 0; F < Floors; ++F)
		{
			const float LZ = F * FH + FH - 45.f;
			const FVector Spots[] = { FVector(-W * 0.22f, -D * 0.15f, LZ), FVector(W * 0.12f, D * 0.2f, LZ) };
			for (int32 Index = 0; Index < 2; ++Index)
			{
				if (AActor* Lamp = AddFixture(LampClass, Spots[Index], 0.f))
				{
					if (UPointLightComponent* Light = Lamp->FindComponentByClass<UPointLightComponent>())
					{
						Light->SetIntensityUnits(ELightUnits::Lumens);
						Light->SetIntensity(Index == 0 ? 420.f : 260.f);
						Light->SetAttenuationRadius(1100.f);
						Light->SetCastShadows(false);
					}
				}
			}
		}
	}
	if (bGutter)
	{
		float Yaw = 0.f;
		const FVector2D Spot = GutterSpot(Yaw);
		const float GutterHeight = H + 20.f;
		AddFixture(GutterClass, FVector(Spot, 0.f), Yaw, [GutterHeight](AActor* Fixture)
		{
			HitmenLD::SetFloat(Fixture, TEXT("Height"), GutterHeight);
			TArray<UStaticMeshComponent*> Meshes;
			Fixture->GetComponents(Meshes);
			for (UStaticMeshComponent* Mesh : Meshes)
			{
				if (Mesh->GetFName() == TEXT("Mesh"))
				{
					const FVector Scale = Mesh->GetRelativeScale3D();
					Mesh->SetRelativeScale3D(FVector(Scale.X, Scale.Y, GutterHeight / 100.f));
					Mesh->SetRelativeLocation(FVector(5.f, 0.f, GutterHeight * 0.5f));
				}
			}
		});
	}
}

void AHitmenLDBuilding::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	// The roof outline: 1 m inside the roof edge, on top of the roof slab.
	const float HX = Snap(Width) * 0.5f - 100.f, HY = Snap(Depth) * 0.5f - 100.f, Z = Floors * FloorHeight + 12.5f;
	RoofOutline->ClearSplinePoints(false);
	for (const FVector2D& Corner : { FVector2D(-HX, -HY), FVector2D(HX, -HY), FVector2D(HX, HY), FVector2D(-HX, HY) })
	{
		RoofOutline->AddSplinePoint(FVector(Corner, Z), ESplineCoordinateSpace::Local, false);
	}
	for (int32 Index = 0; Index < RoofOutline->GetNumberOfSplinePoints(); ++Index)
	{
		RoofOutline->SetSplinePointType(Index, ESplinePointType::Linear, false);
	}
	RoofOutline->SetClosedLoop(true, false);
	RoofOutline->UpdateSpline();
	if (UPCGGraphInterface* Graph = bDressRoof ? RoofDressingGraph.LoadSynchronous() : nullptr)
	{
		if (RoofDressing->GetGraph() != Graph)
		{
			RoofDressing->SetGraph(Graph);
		}
	}
	else if (RoofDressing->GetGraph())
	{
		RoofDressing->Cleanup();
		RoofDressing->SetGraph(nullptr);
	}
}

void AHitmenLDBuilding::GenerateRoofDressing()
{
	if (bDressRoof && RoofDressing->GetGraph())
	{
		RoofDressing->Generate();
	}
}

// =============================================================================================
// Vent duct
// =============================================================================================

AHitmenLDVentDuct::AHitmenLDVentDuct()
{
	Path = CreateDefaultSubobject<USplineComponent>(TEXT("Path"));
	Path->SetupAttachment(Root);
	Path->ClearSplinePoints(false);
	Path->AddSplinePoint(FVector::ZeroVector, ESplineCoordinateSpace::Local, false);
	Path->AddSplinePoint(FVector(600.f, 0.f, 0.f), ESplineCoordinateSpace::Local, false);
	Path->SetSplinePointType(0, ESplinePointType::Linear, false);
	Path->SetSplinePointType(1, ESplinePointType::Linear, true);
}

void AHitmenLDVentDuct::BuildBoxes()
{
	const int32 Count = Path->GetNumberOfSplinePoints();
	const float Half = InsideSize * 0.5f;
	for (int32 Index = 0; Index + 1 < Count; ++Index)
	{
		const FVector A = Path->GetLocationAtSplinePoint(Index, ESplineCoordinateSpace::Local);
		const FVector B = Path->GetLocationAtSplinePoint(Index + 1, ESplineCoordinateSpace::Local);
		const FVector Flat(B.X - A.X, B.Y - A.Y, 0.f);
		float Length = Flat.Size();
		if (Length < 1.f)
		{
			continue;
		}
		const FVector Dir = Flat / Length;
		const float Yaw = Dir.Rotation().Yaw;
		// Joints overlap by half the duct so corners stay closed.
		const float Extend = (Index > 0 ? Half + 10.f : 0.f);
		const FVector Mid = (A + B) * 0.5f - Dir * (Extend * 0.5f);
		Length += Extend;
		const FVector Side(-Dir.Y, Dir.X, 0.f);
		const float Z = Mid.Z;
		AddBox(EPiece::Trim, FVector(Mid.X, Mid.Y, Z - 5.f), FVector(Length, InsideSize + 20.f, 10.f), Yaw);
		AddBox(EPiece::Trim, FVector(Mid.X, Mid.Y, Z + InsideSize + 5.f), FVector(Length, InsideSize + 20.f, 10.f), Yaw);
		for (const float Sign : { -1.f, 1.f })
		{
			const FVector Wall = Mid + Side * Sign * (Half + 5.f);
			AddBox(EPiece::Trim, FVector(Wall.X, Wall.Y, Z + Half), FVector(Length, 10.f, InsideSize), Yaw);
		}
	}
}

void AHitmenLDVentDuct::BuildFixtures()
{
	const int32 Count = Path->GetNumberOfSplinePoints();
	if (Count < 2)
	{
		return;
	}
	auto HatchAt = [this](int32 Point, int32 Toward)
	{
		const FVector At = Path->GetLocationAtSplinePoint(Point, ESplineCoordinateSpace::Local);
		const FVector Next = Path->GetLocationAtSplinePoint(Toward, ESplineCoordinateSpace::Local);
		const float Yaw = FVector(Next.X - At.X, Next.Y - At.Y, 0.f).Rotation().Yaw;
		// The hatch's opening runs across the duct.
		AddFixture(VentHatchClass, At, Yaw + 90.f);
	};
	if (bHatchAtStart)
	{
		HatchAt(0, 1);
	}
	if (bHatchAtEnd)
	{
		HatchAt(Count - 1, Count - 2);
	}
}

// =============================================================================================
// PCG line
// =============================================================================================

AHitmenLDPCGLine::AHitmenLDPCGLine()
{
	PrimaryActorTick.bCanEverTick = false;
	Line = CreateDefaultSubobject<USplineComponent>(TEXT("Line"));
	Line->ComponentTags.Add(TEXT("HitmenLD_Line"));
	RootComponent = Line;
	Line->SetMobility(EComponentMobility::Static);
	Line->ClearSplinePoints(false);
	Line->AddSplinePoint(FVector::ZeroVector, ESplineCoordinateSpace::Local, false);
	Line->AddSplinePoint(FVector(1500.f, 0.f, 0.f), ESplineCoordinateSpace::Local, true);
	PCG = CreateDefaultSubobject<UPCGComponent>(TEXT("PCG"));
	PCG->GenerationTrigger = EPCGComponentGenerationTrigger::GenerateOnDemand;
	Graph = TSoftObjectPtr<UPCGGraphInterface>(FSoftObjectPath(TEXT("/Game/Hitmen/PCG/PCG_Hitmen_CoverLine.PCG_Hitmen_CoverLine")));
}

void AHitmenLDPCGLine::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	UPCGGraphInterface* Loaded = Graph.LoadSynchronous();
	if (PCG->GetGraph() != Loaded)
	{
		PCG->SetGraph(Loaded);
	}
}

void AHitmenLDPCGLine::Generate()
{
	if (PCG->GetGraph())
	{
		PCG->Generate();
	}
}
