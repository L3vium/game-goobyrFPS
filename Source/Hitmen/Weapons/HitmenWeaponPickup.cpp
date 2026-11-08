// Hitmen — weapon pickup

#include "Weapons/HitmenWeaponPickup.h"
#include "Data/HitmenWeaponData.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Core/HitmenTeamLibrary.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Kismet/GameplayStatics.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "Hitmen.h"

AHitmenWeaponPickup::AHitmenWeaponPickup()
{
	SetReplicatingMovement(true);
	Mesh->SetCollisionProfileName(TEXT("PhysicsActor"));
	Mesh->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore);
	Mesh->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	Mesh->SetSimulatePhysics(false);
	Mesh->SetNotifyRigidBodyCollision(true);

	AutoPickupSphere = CreateDefaultSubobject<USphereComponent>(TEXT("AutoPickupSphere"));
	AutoPickupSphere->SetupAttachment(Mesh);
	AutoPickupSphere->InitSphereRadius(70.f);
	AutoPickupSphere->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	AutoPickupSphere->SetGenerateOverlapEvents(true);
}

void AHitmenWeaponPickup::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenWeaponPickup, Weapon);
	DOREPLIFETIME(AHitmenWeaponPickup, Ammo);
}

void AHitmenWeaponPickup::BeginPlay()
{
	Super::BeginPlay();
	ApplyWeaponVisual();
	if (HasAuthority())
	{
		AutoPickupSphere->OnComponentBeginOverlap.AddDynamic(this, &AHitmenWeaponPickup::HandleAutoPickupOverlap);
		Mesh->OnComponentHit.AddDynamic(this, &AHitmenWeaponPickup::HandleThrownHit);
	}
}

void AHitmenWeaponPickup::InitPickup(UHitmenWeaponData* InWeapon, int32 InAmmo)
{
	Weapon = InWeapon;
	Ammo = InAmmo;
	ApplyWeaponVisual();
}

void AHitmenWeaponPickup::OnRep_Weapon()
{
	ApplyWeaponVisual();
}

void AHitmenWeaponPickup::ApplyWeaponVisual()
{
	if (!Weapon)
	{
		return;
	}
	if (!Mesh->GetStaticMesh())
	{
		UStaticMesh* Visual = Weapon->StaticMesh.LoadSynchronous();
		if (!Visual)
		{
			// Placeholder until the team assigns a mesh.
			Visual = LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cube.Cube"));
			Mesh->SetRelativeScale3D(FVector(0.5f, 0.12f, 0.15f));
		}
		Mesh->SetStaticMesh(Visual);
	}
	InteractionText = FText::Format(NSLOCTEXT("Hitmen", "PickUpWeapon", "Pick up {0}"), Weapon->DisplayName);
}

void AHitmenWeaponPickup::Throw(const FVector& Velocity, APawn* Thrower)
{
	LastThrower = Thrower;
	bThrowDamageDone = false;
	PickupEnabledTime = GetWorld()->GetTimeSeconds() + ThrownPickupDelay;
	Mesh->SetSimulatePhysics(true);
	Mesh->SetPhysicsLinearVelocity(Velocity);
	Mesh->SetPhysicsAngularVelocityInDegrees(FVector(FMath::FRandRange(-360.f, 360.f), 720.f, 0.f));
}

void AHitmenWeaponPickup::HandleThrownHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult& Hit)
{
	if (bThrowDamageDone || !Weapon || !LastThrower || OtherActor == LastThrower)
	{
		return;
	}
	if (Cast<APawn>(OtherActor) && UHitmenTeamLibrary::AreHostile(LastThrower, OtherActor))
	{
		bThrowDamageDone = true;
		UGameplayStatics::ApplyPointDamage(OtherActor, Weapon->ThrowDamage, Mesh->GetPhysicsLinearVelocity().GetSafeNormal(), Hit, LastThrower->GetController(), this, UDamageType::StaticClass());
	}
}

void AHitmenWeaponPickup::HandleAutoPickupOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	TryAutoAmmo(OtherActor);
}

void AHitmenWeaponPickup::TryAutoAmmo(AActor* Other)
{
	if (!Weapon || GetWorld()->GetTimeSeconds() < PickupEnabledTime || !Weapon->bUsesAmmo)
	{
		return;
	}
	APawn* Pawn = Cast<APawn>(Other);
	if (!Pawn || !Pawn->IsPlayerControlled())
	{
		return;
	}
	UHitmenInventoryComponent* Inventory = Pawn->FindComponentByClass<UHitmenInventoryComponent>();
	if (!Inventory || !Inventory->HasWeaponType(Weapon))
	{
		return;
	}
	// Same weapon type: take its ammunition and remove it, no key press.
	const int32 Amount = Ammo >= 0 ? FMath::Max(Ammo, Weapon->AmmoPerPickup) : Weapon->AmmoPerPickup;
	if (Inventory->TryAddAmmo(Weapon, Amount))
	{
		Destroy();
	}
}

bool AHitmenWeaponPickup::CanInteract_Implementation(AActor* Interactor) const
{
	return Weapon && Super::CanInteract_Implementation(Interactor) && GetWorld()->GetTimeSeconds() >= PickupEnabledTime;
}

FText AHitmenWeaponPickup::GetInteractionText_Implementation(AActor* Interactor) const
{
	const UHitmenInventoryComponent* Inventory = Interactor ? Interactor->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	if (Weapon && Inventory && !Inventory->HasFreeSlot() && !Inventory->HasWeaponType(Weapon))
	{
		return FText::Format(NSLOCTEXT("Hitmen", "SwapWeapon", "Swap for {0}"), Weapon->DisplayName);
	}
	return InteractionText;
}

void AHitmenWeaponPickup::Interact_Implementation(AActor* Interactor)
{
	if (!HasAuthority() || !Weapon)
	{
		return;
	}
	UHitmenInventoryComponent* Inventory = Interactor ? Interactor->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	if (!Inventory)
	{
		return;
	}
	if (Inventory->HasWeaponType(Weapon) || Inventory->HasFreeSlot())
	{
		if (Inventory->GiveWeapon(Weapon, Ammo))
		{
			Destroy();
		}
		return;
	}
	// Swap: the held weapon takes this pickup's place.
	UHitmenWeaponData* OldWeapon = nullptr;
	int32 OldAmmo = 0;
	if (Inventory->TakeActive(OldWeapon, OldAmmo))
	{
		UHitmenWeaponData* NewWeapon = Weapon;
		const int32 NewAmmo = Ammo;
		Inventory->GiveWeapon(NewWeapon, NewAmmo);
		InitPickup(OldWeapon, OldAmmo);
		Mesh->SetStaticMesh(nullptr);
		ApplyWeaponVisual();
	}
}
