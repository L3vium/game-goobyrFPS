// Hitmen — loot spawn candidate (weapon or item); activated with spatial spread by difficulty

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenLootSpawnPoint.generated.h"

class UHitmenCatalog;
class UHitmenWeaponData;
class UHitmenAbilityData;
class UBillboardComponent;

UENUM(BlueprintType)
enum class EHitmenLootCategory : uint8
{
	Weapon,
	MoveItem,
	ManipulationItem,
	AnyItem
};

UCLASS(Blueprintable)
class HITMEN_API AHitmenLootSpawnPoint : public AActor
{
	GENERATED_BODY()

public:
	AHitmenLootSpawnPoint();

	UFUNCTION(BlueprintPure, Category="Hitmen|Loot")
	EHitmenLootCategory GetCategory() const { return Category; }

	/** Server: spawns one weighted-random entry of its category. */
	void SpawnLoot(const UHitmenCatalog* Catalog, FRandomStream& Stream);

protected:
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UBillboardComponent> Billboard;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Loot")
	EHitmenLootCategory Category = EHitmenLootCategory::Weapon;

	/** Restrict to these weapons (empty = catalog loot list). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Loot")
	TArray<TObjectPtr<UHitmenWeaponData>> WeaponOverrides;

	/** Restrict to these items (empty = catalog lists). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Loot")
	TArray<TObjectPtr<UHitmenAbilityData>> ItemOverrides;

	UPROPERTY(Transient)
	TObjectPtr<AActor> SpawnedLoot;
};
