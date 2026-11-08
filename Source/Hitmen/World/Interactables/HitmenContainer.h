// Hitmen — openable container (cabinet with two doors) holding weapons / items on its shelves

#pragma once

#include "CoreMinimal.h"
#include "World/Interactables/HitmenOpenings.h"
#include "HitmenContainer.generated.h"

class UHitmenWeaponData;
class UHitmenAbilityData;

/** One possible content: a weapon or an item (ability pickup). */
USTRUCT(BlueprintType)
struct FHitmenContainerEntry
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container")
	TObjectPtr<UHitmenWeaponData> Weapon;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container")
	TObjectPtr<UHitmenAbilityData> Item;

	/** Random mode: relative chance of this entry. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container", meta=(ClampMin="0"))
	float Weight = 1.f;
};

UENUM(BlueprintType)
enum class EHitmenContainerContents : uint8
{
	/** Exactly the Contents list. */
	Fixed,
	/** RandomCount entries drawn from RandomPool (empty pool = the catalog's loot). */
	Random
};

/**
 * Cabinet built from cubes (Mesh = back panel) with a shelf and two doors that open sideways.
 * Contents spawn at match start, each snapped to a slot on the floor or the shelf, and are picked
 * up with F once the doors are open. Actor origin = floor, doors on the +X face.
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenContainer : public AHitmenOpeningBase
{
	GENERATED_BODY()

public:
	AHitmenContainer();
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual void BeginPlay() override;

	/** World transform of a content slot (0 .. SlotsPerShelf * 2 - 1). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Container")
	FTransform GetSlotTransform(int32 SlotIndex) const;

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Contents")
	EHitmenContainerContents ContentsMode = EHitmenContainerContents::Fixed;

	/** Fixed mode: what is inside, slot by slot. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Contents")
	TArray<FHitmenContainerEntry> Contents;

	/** Random mode: candidates (weighted). Empty = the catalog's loot weapons and items. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Contents")
	TArray<FHitmenContainerEntry> RandomPool;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Contents", meta=(ClampMin="0"))
	FIntPoint RandomCount = FIntPoint(1, 3);

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(ClampMin="40", Units="cm"))
	float Width = 130.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(ClampMin="30", Units="cm"))
	float Depth = 60.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(ClampMin="40", Units="cm"))
	float Height = 160.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(ClampMin="1", Units="cm"))
	float WallThickness = 4.f;

	/** Height of the middle shelf (0 = no shelf). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(ClampMin="0", Units="cm"))
	float ShelfHeight = 80.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(ClampMin="1", ClampMax="6"))
	int32 SlotsPerShelf = 3;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(Units="deg"))
	float DoorOpenAngle = 110.f;

	/** Height above a shelf where contents float. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Container|Shape", meta=(ClampMin="0", Units="cm"))
	float SlotHoverHeight = 28.f;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelLeft;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelRight;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelTop;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> PanelBottom;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Shelf;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> LeftHinge;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> RightHinge;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> LeftDoor;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> RightDoor;

	UPROPERTY(Transient)
	TArray<TObjectPtr<AActor>> SpawnedContents;

	virtual void ApplyOpenAlpha(float Alpha) override;

	/** Server: spawns the contents into the slots. */
	void SpawnContents();
	void SpawnEntry(const FHitmenContainerEntry& Entry, int32 SlotIndex);
	int32 GetSlotCount() const { return ShelfHeight > 0.f ? SlotsPerShelf * 2 : SlotsPerShelf; }
};
