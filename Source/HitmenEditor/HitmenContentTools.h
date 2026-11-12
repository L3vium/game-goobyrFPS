// Hitmen — editor tools exposed to Python: map schematic baker (minimap / pause map / room
// preview) and composite font assembly (UI fonts)

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenContentTools.generated.h"

class UHitmenMapDefinition;
class UTexture2D;
class UFont;
class UFontFace;
class UStaticMesh;
class UMaterialInterface;

UCLASS()
class HITMENEDITOR_API UHitmenContentTools : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * Bakes a top-down schematic of the level open in the editor over Map->WorldBounds (north = +X
	 * at the top, east = +Y to the right): floors light grey (darker higher up), walls and
	 * anything above CutHeight dark, outlines where heights change, void near black. Geometry above
	 * CutHeight (roofs) is cut away so building interiors show; on terrain (actors tagged HitmenTerrain) the cut follows
	 * the ground under each point. The texture is created / replaced at
	 * TexturePath, assigned to Map->MapTexture, and both are marked dirty (save them after).
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static UTexture2D* BakeMapTexture(UHitmenMapDefinition* Map, const FString& TexturePath, int32 Resolution = 768, float CutHeight = 260.f);

	/**
	 * Builds (or rebuilds) a terrain tile from a height grid: NumX x NumY vertices Spacing cm apart, vertex (IX, IY) at
	 * (IX * Spacing, IY * Spacing, Heights[IY * NumX + IX]) relative to the actor placed at Origin. Quads whose centre
	 * (in world XY) falls in one of Holes are left out (stair wells, slabs of their own). The mesh is its own collision
	 * (complex as simple) so the navmesh and footsteps follow it. Saved at AssetPath.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static UStaticMesh* BuildHeightfieldMesh(const FString& AssetPath, const TArray<float>& Heights, int32 NumX, int32 NumY, float Spacing,
		FVector2D Origin, const TArray<FBox2D>& Holes, UMaterialInterface* Material, bool bFlipWinding = false);

	/** Actor tag of terrain tiles: the map bake cuts buildings at CutHeight above them (hills keep their rooms). */
	static const FName TerrainTag;

	/** Creates (or refills) a composite font asset whose default typeface lists the given faces. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static UFont* BuildCompositeFont(const FString& FontPath, const TArray<UFontFace*>& Faces, const TArray<FName>& TypefaceNames);
};
