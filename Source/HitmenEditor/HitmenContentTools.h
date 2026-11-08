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

UCLASS()
class HITMENEDITOR_API UHitmenContentTools : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * Bakes a top-down schematic of the level open in the editor over Map->WorldBounds (north = +X
	 * at the top, east = +Y to the right): floors light grey (darker higher up), walls and
	 * anything above CutHeight dark, outlines where heights change, void near black. Geometry above
	 * CutHeight (roofs) is cut away so building interiors show. The texture is created / replaced at
	 * TexturePath, assigned to Map->MapTexture, and both are marked dirty (save them after).
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static UTexture2D* BakeMapTexture(UHitmenMapDefinition* Map, const FString& TexturePath, int32 Resolution = 768, float CutHeight = 260.f);

	/** Creates (or refills) a composite font asset whose default typeface lists the given faces. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Editor")
	static UFont* BuildCompositeFont(const FString& FontPath, const TArray<UFontFace*>& Faces, const TArray<FName>& TypefaceNames);
};
