// Hitmen — level-placed weapon marker: hands off to a dropped weapon actor at BeginPlay

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "HitmenWeaponPickup.generated.h"

class UHitmenWeaponData;
class AHitmenWeaponBase;

/**
 * Weapons live as one actor for their whole life (AHitmenWeaponBase has a Dropped state that is
 * its own world pickup). This marker only exists so level designers can place a weapon: at
 * BeginPlay (or InitPickup when spawned by code) the server spawns the weapon actor in the
 * Dropped state at this transform and destroys the marker. It shows the weapon mesh in the editor.
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenWeaponPickup : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenWeaponPickup();

	virtual void OnConstruction(const FTransform& Transform) override;
	virtual void BeginPlay() override;

	/** Server: what to spawn. Ammo < 0 = start ammo. Hands off immediately if already playing. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Pickup")
	void InitPickup(UHitmenWeaponData* InWeapon, int32 InAmmo);

	/** Server: legacy throw (NPC drops) — forwarded to the spawned weapon actor. */
	void Throw(const FVector& Velocity, APawn* Thrower);

	UFUNCTION(BlueprintPure, Category="Hitmen|Pickup")
	UHitmenWeaponData* GetWeapon() const { return Weapon; }

	/** The weapon actor this marker handed off to (server). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Pickup")
	AHitmenWeaponBase* GetSpawnedWeapon() const { return SpawnedWeapon; }

protected:
	/** Weapon placed by a level designer (overridden by InitPickup). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Pickup")
	TObjectPtr<UHitmenWeaponData> Weapon;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Pickup")
	int32 Ammo = -1;

	UPROPERTY(Transient)
	TObjectPtr<AHitmenWeaponBase> SpawnedWeapon;

	void HandOff();
	void ApplyEditorVisual();
};
