// Hitmen — one cosmetic piece (hat, moustache, jacket, trousers...)

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Core/HitmenTypes.h"
#include "HitmenCosmeticItem.generated.h"

class USkeletalMesh;
class UStaticMesh;
class UMaterialInterface;

/**
 * A cosmetic item. Either a skeletal mesh following the body (leader pose) or a static mesh
 * attached to a socket. The team sets meshes; code only reads them.
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenCosmeticItem : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic")
	FText DisplayName;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic")
	TSoftObjectPtr<UTexture2D> Icon;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic")
	EHitmenCosmeticSlot Slot = EHitmenCosmeticSlot::Head;

	/** Skeletal piece driven by the body (leader pose). Takes priority over StaticMesh. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic|Mesh")
	TSoftObjectPtr<USkeletalMesh> SkeletalMesh;

	/** Rigid piece attached to AttachSocket (hat, glasses, moustache...). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic|Mesh")
	TSoftObjectPtr<UStaticMesh> StaticMesh;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic|Mesh")
	FName AttachSocket = TEXT("head");

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic|Mesh")
	FTransform AttachOffset;

	/** Optional material override for the body section of this slot (tinted outfits). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic|Mesh")
	TSoftObjectPtr<UMaterialInterface> BodyMaterialOverride;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Cosmetic|Mesh")
	FLinearColor Tint = FLinearColor::White;

	virtual FPrimaryAssetId GetPrimaryAssetId() const override
	{
		return FPrimaryAssetId(TEXT("HitmenCosmetic"), GetFName());
	}
};
