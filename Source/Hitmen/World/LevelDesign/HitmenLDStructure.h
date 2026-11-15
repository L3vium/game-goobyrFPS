// Hitmen — level design structures: parametric blocks, walls, stairs, buildings, vent ducts and PCG lines, built from
// boxes at the gym metrics. Place them from the "Hitmen LD" tab (Window menu) or the Place Actors panel, set the
// numbers in Details, then keep them live or bake them to plain static mesh actors.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenLDStructure.generated.h"

class UInstancedStaticMeshComponent;
class UMaterialInterface;
class USplineComponent;
class UPCGComponent;
class UPCGGraphInterface;

/** Heights the player's moves are tuned for (the gym's metric walls). */
UENUM(BlueprintType)
enum class EHitmenLDMetric : uint8
{
	Custom,
	/** 50 cm: walked or jumped onto. */
	Step,
	/** 110 cm: vaulted (Space). */
	Vault,
	/** 240 cm: grabbed and climbed onto. */
	Climb,
	/** 300 cm: out of reach (needs a ladder, a crate or an ability). */
	OutOfReach
};

UENUM(BlueprintType)
enum class EHitmenLDOpeningType : uint8
{
	Door,
	Window,
	Vent,
	/** A plain hole (no fixture). */
	Gap
};

UENUM(BlueprintType)
enum class EHitmenLDFace : uint8
{
	South,
	North,
	West,
	East
};

USTRUCT(BlueprintType)
struct FHitmenLDOpening
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	EHitmenLDOpeningType Type = EHitmenLDOpeningType::Window;

	/** Centre of the opening along the wall, from its start (cm). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm"))
	float Offset = 200.f;

	/** 0 = the type's size (door 110 x 230, window 120 from 100 to 220, vent 120 x 120). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm"))
	float Width = 0.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm"))
	float Sill = -1.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm"))
	float Top = 0.f;

	/** Places the door / window / hatch actor in it. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bFixture = true;

	/** Windows: starts open. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bOpen = false;

	/** Resolved size: X width, Y sill, Z top. */
	FVector Resolve() const;
};

USTRUCT(BlueprintType)
struct FHitmenLDVent
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	EHitmenLDFace Face = EHitmenLDFace::East;

	/** Along the face, 0..1. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(ClampMin="0", ClampMax="1"))
	float Along = 0.2f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(ClampMin="0"))
	int32 Floor = 0;

	/** A crawl duct from the hatch into the room this long, with a second hatch at its end (0 = a hatch in the wall
	 *  only). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(ClampMin="0", Units="cm"))
	float DuctLength = 300.f;
};

/** Base of every LD structure: boxes in three instanced meshes (walls, floors, trim) rebuilt from the numbers, and
 *  gameplay fixtures (doors, windows, hatches, ladders, lamps) spawned as real actors attached to it. */
UCLASS(Abstract, HideCategories=(Replication, Networking, Input, HLOD, Cooking, DataLayers))
class HITMEN_API AHitmenLDStructure : public AActor
{
	GENERATED_BODY()

public:
	AHitmenLDStructure();

	virtual void OnConstruction(const FTransform& Transform) override;
#if WITH_EDITOR
	virtual void PostEditChangeProperty(FPropertyChangedEvent& PropertyChangedEvent) override;
	virtual void PostDuplicate(bool bDuplicateForPIE) override;
	virtual void PostEditImport() override;
	virtual void Destroyed() override;
#endif

	/** Spawns the fixtures again (doors, windows, hatches, ladders, lamps) after hand edits. */
	UFUNCTION(CallInEditor, BlueprintCallable, Category="Hitmen|LD")
	void RebuildFixtures();

	/** Replaces this structure by plain static mesh actors (one per box) and keeps its fixtures where they are. */
	UFUNCTION(CallInEditor, BlueprintCallable, Category="Hitmen|LD")
	void BakeToStaticMeshes();

	/** The gym height of a metric (0 for Custom). */
	UFUNCTION(BlueprintPure, Category="Hitmen|LD")
	static float GetMetricHeight(EHitmenLDMetric Metric);

	/** Sizes snap to this grid (0 = off). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(ClampMin="0", Units="cm"))
	float GridSnap = 50.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Look")
	TObjectPtr<UMaterialInterface> WallMaterial;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Look")
	TObjectPtr<UMaterialInterface> FloorMaterial;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Look")
	TObjectPtr<UMaterialInterface> TrimMaterial;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Fixtures")
	TSoftClassPtr<AActor> DoorClass;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Fixtures")
	TSoftClassPtr<AActor> WindowClass;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Fixtures")
	TSoftClassPtr<AActor> VentHatchClass;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Fixtures")
	TSoftClassPtr<AActor> LadderClass;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Fixtures")
	TSoftClassPtr<AActor> GutterClass;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|Fixtures")
	TSoftClassPtr<AActor> LampClass;

	/** The fixtures this structure spawned (destroyed and spawned again on a change). */
	UPROPERTY(VisibleInstanceOnly, Category="Hitmen|LD|Fixtures")
	TArray<TObjectPtr<AActor>> Fixtures;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Root;

	/** Tagged HitmenLD_Wall (PCG reads surfaces by these tags). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UInstancedStaticMeshComponent> WallPieces;

	/** Tagged HitmenLD_Floor. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UInstancedStaticMeshComponent> FloorPieces;

	/** Tagged HitmenLD_Trim (ledges, ducts, rims). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UInstancedStaticMeshComponent> TrimPieces;

	enum class EPiece : uint8 { Wall, Floor, Trim };

	/** Adds the boxes (actor space). */
	virtual void BuildBoxes() {}
	/** Spawns the fixtures through AddFixture (editor world only). */
	virtual void BuildFixtures() {}

	/** A box from its centre and full size (cm, actor space), turned by Yaw around its centre. */
	void AddBox(EPiece Piece, const FVector& Center, const FVector& Size, float Yaw = 0.f);
	/** A wall from Start to End (actor-space XY) from Z0, Height high, with openings (X centre along, Y width,
	 *  Z sill, W top). */
	void AddWall(EPiece Piece, const FVector2D& Start, const FVector2D& End, float Z0, float Height, float Thickness, TArray<FVector4> Openings);
	/** Steps rising along Yaw from Start (actor space). */
	void AddStairs(const FVector& Start, float Yaw, float Rise, float Run, float Width, int32 Steps = 0);
	/** Spawns a fixture at an actor-space location and yaw, attached to this structure. Configure runs before its
	 *  construction (set Height, bOpen...). */
	AActor* AddFixture(const TSoftClassPtr<AActor>& Class, const FVector& Location, float Yaw, TFunctionRef<void(AActor*)> Configure);
	AActor* AddFixture(const TSoftClassPtr<AActor>& Class, const FVector& Location, float Yaw);

	float Snap(float Value) const;

private:
	void ClearFixtures();
	bool CanSpawnFixtures() const;
	bool bFixturesDirty = false;
};

/** A box at a metric height: a step, a vault block, a climb block, a wall out of reach. */
UCLASS(meta=(DisplayName="Hitmen LD Block"))
class HITMEN_API AHitmenLDBlock : public AHitmenLDStructure
{
	GENERATED_BODY()

public:
	/** Width (X) and depth (Y), cm. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm"))
	FVector2D Footprint = FVector2D(200.f, 200.f);

	/** Its height from the gym metrics, or CustomHeight. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	EHitmenLDMetric Height = EHitmenLDMetric::Vault;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", EditCondition="Height == EHitmenLDMetric::Custom"))
	float CustomHeight = 150.f;

protected:
	virtual void BuildBoxes() override;
};

/** A straight wall along X with doors, windows, vents and gaps. */
UCLASS(meta=(DisplayName="Hitmen LD Wall"))
class HITMEN_API AHitmenLDWall : public AHitmenLDStructure
{
	GENERATED_BODY()

public:
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="10"))
	float Length = 600.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	EHitmenLDMetric Height = EHitmenLDMetric::Custom;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", EditCondition="Height == EHitmenLDMetric::Custom"))
	float CustomHeight = 350.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="5"))
	float Thickness = 25.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	TArray<FHitmenLDOpening> Openings;

protected:
	virtual void BuildBoxes() override;
	virtual void BuildFixtures() override;
	float GetHeight() const;
};

/** A straight flight rising along X. */
UCLASS(meta=(DisplayName="Hitmen LD Stairs"))
class HITMEN_API AHitmenLDStairs : public AHitmenLDStructure
{
	GENERATED_BODY()

public:
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="20"))
	float Rise = 350.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="40"))
	float Run = 600.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="60"))
	float Width = 260.f;

	/** 0 = one step per 20 cm of rise. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(ClampMin="0"))
	int32 Steps = 0;

protected:
	virtual void BuildBoxes() override;
};

/** A hollow building like the builders' (hitmen_levels.building): floors, a switchback stair, a door, vaultable
 *  windows on every face and floor, walkable ledges, a gutter, crawl vents with ducts, lamps, and a roof outline for
 *  PCG dressing. The origin is the centre of its ground floor. */
UCLASS(meta=(DisplayName="Hitmen LD Building"))
class HITMEN_API AHitmenLDBuilding : public AHitmenLDStructure
{
	GENERATED_BODY()

public:
	AHitmenLDBuilding();

	/** X, cm. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="700"))
	float Width = 1200.f;

	/** Y, cm. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="700"))
	float Depth = 1000.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(ClampMin="1", ClampMax="6"))
	int32 Floors = 2;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="300"))
	float FloorHeight = 350.f;

	/** South (-Y) or North (+Y). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	EHitmenLDFace DoorFace = EHitmenLDFace::South;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bWindows = true;

	/** About one window in three starts open. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(EditCondition="bWindows"))
	bool bSomeWindowsOpen = true;

	/** Faces with no window at all. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	TArray<EHitmenLDFace> BlindFaces;

	/** A walkable ledge around every upper floor, just under the room floor (out of a window, along to the next). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bLedges = true;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bGutter = true;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(EditCondition="bGutter"))
	EHitmenLDFace GutterFace = EHitmenLDFace::West;

	/** Crawl hatches at floor level, each with its duct into the room. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	TArray<FHitmenLDVent> Vents;

	/** Two ceiling lamps per floor (fixed exposure maps need them). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bLamps = true;

	/** Generates RoofDressingGraph on the roof (crates, vents, antennas). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|PCG")
	bool bDressRoof = false;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|PCG", meta=(EditCondition="bDressRoof"))
	TSoftObjectPtr<UPCGGraphInterface> RoofDressingGraph;

	/** Runs the roof dressing graph again. */
	UFUNCTION(CallInEditor, BlueprintCallable, Category="Hitmen|LD|PCG")
	void GenerateRoofDressing();

	virtual void OnConstruction(const FTransform& Transform) override;

protected:
	/** A closed loop just inside the roof edge, tagged HitmenLD_Roof (PCG samples its inside). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USplineComponent> RoofOutline;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UPCGComponent> RoofDressing;

	virtual void BuildBoxes() override;
	virtual void BuildFixtures() override;

private:
	bool IsBlind(EHitmenLDFace Face) const { return BlindFaces.Contains(Face); }
	/** Opening centres along a face (window places). */
	TArray<float> WindowPlaces(EHitmenLDFace Face) const;
	FVector2D GutterSpot(float& OutYaw) const;
	/** Face geometry: start, end (actor XY, along the face), the outward normal. */
	void GetFace(EHitmenLDFace Face, FVector2D& OutStart, FVector2D& OutEnd, FVector2D& OutNormal) const;
};

/** A crawl duct (120 x 120 inside) along a spline, straight between points, with a hatch at each open end. */
UCLASS(meta=(DisplayName="Hitmen LD Vent Duct"))
class HITMEN_API AHitmenLDVentDuct : public AHitmenLDStructure
{
	GENERATED_BODY()

public:
	AHitmenLDVentDuct();

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD", meta=(Units="cm", ClampMin="80"))
	float InsideSize = 120.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bHatchAtStart = true;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD")
	bool bHatchAtEnd = true;

protected:
	/** The duct's floor line (points at the inside floor). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USplineComponent> Path;

	virtual void BuildBoxes() override;
	virtual void BuildFixtures() override;
};

/** A spline that runs a PCG graph: cover blocks, rock lines (PCG_Hitmen_CoverLine, PCG_Hitmen_Rocks). */
UCLASS(meta=(DisplayName="Hitmen LD PCG Line"))
class HITMEN_API AHitmenLDPCGLine : public AActor
{
	GENERATED_BODY()

public:
	AHitmenLDPCGLine();

	virtual void OnConstruction(const FTransform& Transform) override;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|LD|PCG")
	TSoftObjectPtr<UPCGGraphInterface> Graph;

	/** Runs the graph again. */
	UFUNCTION(CallInEditor, BlueprintCallable, Category="Hitmen|LD|PCG")
	void Generate();

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USplineComponent> Line;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UPCGComponent> PCG;
};
