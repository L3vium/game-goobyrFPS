// Hitmen — editor content tools

#include "HitmenContentTools.h"
#include "Data/HitmenMapDefinition.h"

#include "Editor.h"
#include "Engine/World.h"
#include "Engine/Texture2D.h"
#include "Engine/Font.h"
#include "Engine/FontFace.h"
#include "Fonts/CompositeFont.h"
#include "ImageUtils.h"
#include "CollisionQueryParams.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "UObject/Package.h"

DEFINE_LOG_CATEGORY_STATIC(LogHitmenContentTools, Log, All);

namespace HitmenMapBake
{
	enum class ECell : uint8 { Void, Floor, Solid };

	struct FCell
	{
		ECell Kind = ECell::Void;
		float Height = 0.f;
	};

	const FLinearColor VoidColor(0.075f, 0.08f, 0.09f);
	const FLinearColor SolidColor(0.2f, 0.215f, 0.24f);
	const FLinearColor FloorLow(0.72f, 0.74f, 0.77f);
	const FLinearColor FloorHigh(0.5f, 0.53f, 0.58f);
	const FLinearColor Outline(0.3f, 0.32f, 0.36f);
}

UTexture2D* UHitmenContentTools::BakeMapTexture(UHitmenMapDefinition* Map, const FString& TexturePath, int32 Resolution, float CutHeight)
{
	using namespace HitmenMapBake;
	UWorld* World = GEditor ? GEditor->GetEditorWorldContext().World() : nullptr;
	if (!Map || !World || Resolution < 16)
	{
		UE_LOG(LogHitmenContentTools, Warning, TEXT("BakeMapTexture: no map / editor world."));
		return nullptr;
	}
	const FBox2D Bounds = Map->WorldBounds;
	const FVector2D Size = Bounds.GetSize();
	const int32 Res = Resolution;

	// Only level structure counts (static geometry), never pawns or props.
	const FCollisionObjectQueryParams Objects(ECC_WorldStatic);
	const FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenMapBake), false);
	TArray<FCell> Cells;
	Cells.SetNum(Res * Res);
	float MinFloor = BIG_NUMBER;
	float MaxFloor = -BIG_NUMBER;
	for (int32 V = 0; V < Res; ++V)
	{
		for (int32 U = 0; U < Res; ++U)
		{
			const float X = Bounds.Max.X - (V + 0.5f) / Res * Size.X;
			const float Y = Bounds.Min.Y + (U + 0.5f) / Res * Size.Y;
			FCell& Cell = Cells[V * Res + U];
			FHitResult Hit;
			if (!World->LineTraceSingleByObjectType(Hit, FVector(X, Y, 20000.f), FVector(X, Y, -20000.f), Objects, Params))
			{
				continue;
			}
			if (Hit.ImpactPoint.Z <= CutHeight)
			{
				Cell.Kind = ECell::Floor;
				Cell.Height = Hit.ImpactPoint.Z;
			}
			else if (World->OverlapAnyTestByObjectType(FVector(X, Y, CutHeight), FQuat::Identity, Objects, FCollisionShape::MakeSphere(2.f), Params))
			{
				Cell.Kind = ECell::Solid; // A wall crossing the cut plane.
			}
			else if (World->LineTraceSingleByObjectType(Hit, FVector(X, Y, CutHeight), FVector(X, Y, -20000.f), Objects, Params))
			{
				Cell.Kind = ECell::Floor; // Under a roof: the room inside.
				Cell.Height = Hit.ImpactPoint.Z;
			}
			if (Cell.Kind == ECell::Floor)
			{
				MinFloor = FMath::Min(MinFloor, Cell.Height);
				MaxFloor = FMath::Max(MaxFloor, Cell.Height);
			}
		}
	}

	auto At = [&](int32 U, int32 V) -> const FCell&
	{
		return Cells[FMath::Clamp(V, 0, Res - 1) * Res + FMath::Clamp(U, 0, Res - 1)];
	};
	TArray<FColor> Pixels;
	Pixels.SetNum(Res * Res);
	const float HeightRange = FMath::Max(300.f, MaxFloor - MinFloor);
	for (int32 V = 0; V < Res; ++V)
	{
		for (int32 U = 0; U < Res; ++U)
		{
			const FCell& Cell = At(U, V);
			FLinearColor Color = VoidColor;
			if (Cell.Kind == ECell::Solid)
			{
				Color = SolidColor;
			}
			else if (Cell.Kind == ECell::Floor)
			{
				const float Alpha = FMath::Clamp((Cell.Height - MinFloor) / HeightRange, 0.f, 1.f);
				Color = FMath::Lerp(FloorLow, FloorHigh, Alpha);
				// Outline where the floor meets a wall, the void or a step.
				for (const FIntPoint& Offset : { FIntPoint(1, 0), FIntPoint(-1, 0), FIntPoint(0, 1), FIntPoint(0, -1) })
				{
					const FCell& Other = At(U + Offset.X, V + Offset.Y);
					if (Other.Kind != ECell::Floor || FMath::Abs(Other.Height - Cell.Height) > 35.f)
					{
						Color = Outline;
						break;
					}
				}
			}
			Pixels[V * Res + U] = Color.ToFColor(true);
		}
	}

	// Create or replace the texture asset.
	const FString AssetName = FPackageName::GetLongPackageAssetName(TexturePath);
	UPackage* Package = CreatePackage(*TexturePath);
	Package->FullyLoad();
	if (UTexture2D* Existing = FindObject<UTexture2D>(Package, *AssetName))
	{
		Existing->Rename(nullptr, GetTransientPackage(), REN_DontCreateRedirectors | REN_NonTransactional);
	}
	FCreateTexture2DParameters TextureParams;
	TextureParams.bUseAlpha = false;
	TextureParams.bSRGB = true;
	TextureParams.CompressionSettings = TC_Default;
	TextureParams.TextureGroup = TEXTUREGROUP_UI;
	UTexture2D* Texture = FImageUtils::CreateTexture2D(Res, Res, Pixels, Package, AssetName, RF_Public | RF_Standalone, TextureParams);
	if (!Texture)
	{
		return nullptr;
	}
	Texture->PostEditChange();
	FAssetRegistryModule::AssetCreated(Texture);
	Package->MarkPackageDirty();

	Map->Modify();
	Map->MapTexture = Texture;
	Map->MarkPackageDirty();
	UE_LOG(LogHitmenContentTools, Log, TEXT("Baked %s (%dx%d, floors %.0f..%.0f)."), *TexturePath, Res, Res, MinFloor, MaxFloor);
	return Texture;
}

UFont* UHitmenContentTools::BuildCompositeFont(const FString& FontPath, const TArray<UFontFace*>& Faces, const TArray<FName>& TypefaceNames)
{
	if (Faces.Num() == 0 || Faces.Num() != TypefaceNames.Num())
	{
		UE_LOG(LogHitmenContentTools, Warning, TEXT("BuildCompositeFont: faces and names must match."));
		return nullptr;
	}
	const FString AssetName = FPackageName::GetLongPackageAssetName(FontPath);
	UPackage* Package = CreatePackage(*FontPath);
	Package->FullyLoad();
	UFont* Font = FindObject<UFont>(Package, *AssetName);
	if (!Font)
	{
		Font = NewObject<UFont>(Package, *AssetName, RF_Public | RF_Standalone);
		FAssetRegistryModule::AssetCreated(Font);
	}
	Font->Modify();
	Font->FontCacheType = EFontCacheType::Runtime;
	FTypeface& Typeface = Font->GetMutableInternalCompositeFont().DefaultTypeface;
	Typeface.Fonts.Reset();
	for (int32 Index = 0; Index < Faces.Num(); ++Index)
	{
		if (Faces[Index])
		{
			FTypefaceEntry& Entry = Typeface.Fonts.AddDefaulted_GetRef();
			Entry.Name = TypefaceNames[Index];
			Entry.Font = FFontData(Faces[Index]);
		}
	}
	Font->PostEditChange();
	Package->MarkPackageDirty();
	return Font;
}
