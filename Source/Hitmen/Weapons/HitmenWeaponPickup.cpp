// Hitmen — level-placed weapon marker

#include "Weapons/HitmenWeaponPickup.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Data/HitmenWeaponData.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"

AHitmenWeaponPickup::AHitmenWeaponPickup()
{
	// The marker is never used or scanned: the weapon actor it spawns is.
	bUsableWithInteract = false;
	bShowInSightroom = false;
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Mesh->SetSimulatePhysics(false);
	Mesh->SetHiddenInGame(true);
}

void AHitmenWeaponPickup::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	ApplyEditorVisual();
}

void AHitmenWeaponPickup::ApplyEditorVisual()
{
	// Editor preview of what will spawn (the mesh is hidden in game).
	if (Weapon && !Mesh->GetStaticMesh() && !Weapon->StaticMesh.IsNull())
	{
		Mesh->SetStaticMesh(Weapon->StaticMesh.LoadSynchronous());
	}
}

void AHitmenWeaponPickup::BeginPlay()
{
	Super::BeginPlay();
	Mesh->SetHiddenInGame(true);
	if (HasAuthority() && Weapon)
	{
		HandOff();
	}
}

void AHitmenWeaponPickup::InitPickup(UHitmenWeaponData* InWeapon, int32 InAmmo)
{
	Weapon = InWeapon;
	Ammo = InAmmo;
	if (HasAuthority() && HasActorBegunPlay())
	{
		HandOff();
	}
}

void AHitmenWeaponPickup::HandOff()
{
	if (SpawnedWeapon || !Weapon)
	{
		return;
	}
	SpawnedWeapon = AHitmenWeaponBase::SpawnDropped(this, Weapon, Ammo, GetActorTransform());
	// The marker's job is done. Members stay valid until GC, so a following Throw still forwards.
	Destroy();
}

void AHitmenWeaponPickup::Throw(const FVector& Velocity, APawn* Thrower)
{
	if (IsValid(SpawnedWeapon) && SpawnedWeapon->IsDropped())
	{
		SpawnedWeapon->Drop(SpawnedWeapon->GetDroppedAmmo(), SpawnedWeapon->GetActorLocation(), SpawnedWeapon->GetActorRotation(), Velocity, Thrower);
	}
}
