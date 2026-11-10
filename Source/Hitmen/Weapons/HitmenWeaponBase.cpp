// Hitmen — weapon base

#include "Weapons/HitmenWeaponBase.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Data/HitmenWeaponData.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "Core/HitmenTeamLibrary.h"
#include "Interaction/HitmenScanRegistry.h"
#include "AI/HitmenAISubsystem.h"
#include "Hitmen.h"
#include "Components/BoxComponent.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/CollisionProfile.h"
#include "Animation/AnimMontage.h"
#include "Sound/SoundBase.h"
#include "NiagaraFunctionLibrary.h"
#include "NiagaraComponent.h"
#include "Kismet/GameplayStatics.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/DamageType.h"
#include "Net/UnrealNetwork.h"
#include "TimerManager.h"
#include "Engine/World.h"

AHitmenWeaponBase::AHitmenWeaponBase()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	bReplicates = true;
	// Movement only replicates while dropped (physics); in hand every machine attaches locally.
	SetReplicatingMovement(false);
	bNetUseOwnerRelevancy = true;

	DropCollision = CreateDefaultSubobject<UBoxComponent>(TEXT("DropCollision"));
	DropCollision->InitBoxExtent(DropCollisionExtent);
	DropCollision->SetCollisionProfileName(UCollisionProfile::PhysicsActor_ProfileName);
	DropCollision->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore);
	DropCollision->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	// A gun on the floor must not block sight traces (AI, Sightroom, footsteps).
	DropCollision->SetCollisionResponseToChannel(ECC_Visibility, ECR_Ignore);
	DropCollision->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	DropCollision->SetCollisionResponseToChannel(HITMEN_TRACE_WEAPON, ECR_Ignore);
	DropCollision->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	DropCollision->SetSimulatePhysics(false);
	DropCollision->SetNotifyRigidBodyCollision(true);
	DropCollision->SetGenerateOverlapEvents(false);
	DropCollision->SetCanEverAffectNavigation(false);
	SetRootComponent(DropCollision);

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetupAttachment(DropCollision);

	StaticMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("StaticMesh"));
	StaticMesh->SetupAttachment(Root);
	StaticMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	SkeletalMesh = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("SkeletalMesh"));
	SkeletalMesh->SetupAttachment(Root);
	SkeletalMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	AutoPickupSphere = CreateDefaultSubobject<USphereComponent>(TEXT("AutoPickupSphere"));
	AutoPickupSphere->SetupAttachment(DropCollision);
	AutoPickupSphere->InitSphereRadius(AutoPickupRadius);
	AutoPickupSphere->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	AutoPickupSphere->SetGenerateOverlapEvents(true);
	AutoPickupSphere->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	AutoPickupSphere->SetCanEverAffectNavigation(false);
}

void AHitmenWeaponBase::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenWeaponBase, WeaponData);
	DOREPLIFETIME(AHitmenWeaponBase, OwnerPawn);
	DOREPLIFETIME(AHitmenWeaponBase, WeaponState);
	DOREPLIFETIME(AHitmenWeaponBase, bHovering);
}

void AHitmenWeaponBase::BeginPlay()
{
	Super::BeginPlay();
	AutoPickupSphere->SetSphereRadius(AutoPickupRadius);
	if (HasAuthority())
	{
		AutoPickupSphere->OnComponentBeginOverlap.AddDynamic(this, &AHitmenWeaponBase::HandleAutoPickupBeginOverlap);
		AutoPickupSphere->OnComponentEndOverlap.AddDynamic(this, &AHitmenWeaponBase::HandleAutoPickupEndOverlap);
		DropCollision->OnComponentHit.AddDynamic(this, &AHitmenWeaponBase::HandleDropHit);
	}
	UHitmenScanRegistry::Register(this);
}

void AHitmenWeaponBase::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UHitmenScanRegistry::Unregister(this);
	UnwatchAllInventories();
	GetWorldTimerManager().ClearAllTimersForObject(this);
	Super::EndPlay(EndPlayReason);
}

void AHitmenWeaponBase::InitWeapon(UHitmenWeaponData* InData, APawn* InOwner)
{
	WeaponData = InData;
	OwnerPawn = InOwner;
	ApplyVisualsFromData();
}

AHitmenWeaponBase* AHitmenWeaponBase::SpawnDropped(UObject* WorldContextObject, UHitmenWeaponData* InData, int32 InAmmo, const FTransform& Transform)
{
	UWorld* World = WorldContextObject ? WorldContextObject->GetWorld() : nullptr;
	if (!World || World->GetNetMode() == NM_Client || !InData || !InData->WeaponClass)
	{
		return nullptr;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	AHitmenWeaponBase* Weapon = World->SpawnActor<AHitmenWeaponBase>(InData->WeaponClass, Transform, Params);
	if (Weapon)
	{
		Weapon->InitWeapon(InData, nullptr);
		Weapon->Drop(InAmmo, Transform.GetLocation(), Transform.Rotator(), FVector::ZeroVector, nullptr);
	}
	return Weapon;
}

void AHitmenWeaponBase::OnRep_WeaponData()
{
	ApplyVisualsFromData();
	ApplyWeaponState();
}

void AHitmenWeaponBase::OnRep_OwnerPawn()
{
	// The attach parent depends on the owner (first-person mesh for the local player).
	ApplyWeaponState();
}

void AHitmenWeaponBase::OnRep_WeaponState()
{
	ApplyWeaponState();
}

void AHitmenWeaponBase::OnRep_AttachmentReplication()
{
	// Intentionally not calling Super: the server's attach parent (its own first-person mesh for
	// the host) is wrong on other machines. Every machine attaches from the replicated state.
	ApplyWeaponState();
}

void AHitmenWeaponBase::ApplyVisualsFromData()
{
	if (!WeaponData)
	{
		return;
	}
	// BP children may already set meshes; data only fills empty components.
	if (!StaticMesh->GetStaticMesh() && !WeaponData->StaticMesh.IsNull())
	{
		StaticMesh->SetStaticMesh(WeaponData->StaticMesh.LoadSynchronous());
	}
	if (!SkeletalMesh->GetSkeletalMeshAsset() && !WeaponData->SkeletalMesh.IsNull())
	{
		SkeletalMesh->SetSkeletalMesh(WeaponData->SkeletalMesh.LoadSynchronous());
	}
	// The mesh pack's orientation / scale (data), under the grip pivot.
	StaticMesh->SetRelativeTransform(WeaponData->VisualTransform);
	SkeletalMesh->SetRelativeTransform(WeaponData->VisualTransform);
	// Only one visual is ever shown: the skeletal mesh when there is one (animated parts).
	if (SkeletalMesh->GetSkeletalMeshAsset())
	{
		StaticMesh->SetStaticMesh(nullptr);
		StaticMesh->SetVisibility(false);
	}
	if (!StaticMesh->GetStaticMesh() && !SkeletalMesh->GetSkeletalMeshAsset())
	{
		// Placeholder until the team assigns a mesh, so dropped weapons stay visible.
		if (UStaticMesh* Placeholder = PlaceholderMesh.LoadSynchronous())
		{
			StaticMesh->SetStaticMesh(Placeholder);
			StaticMesh->SetRelativeScale3D(PlaceholderScale);
		}
	}

	// Fit the drop box to the visuals (in grip space) and remember the offset that centres them.
	FBox Bounds(ForceInit);
	if (bFitDropCollisionToMesh)
	{
		if (SkeletalMesh->GetSkeletalMeshAsset())
		{
			Bounds += SkeletalMesh->CalcBounds(SkeletalMesh->GetRelativeTransform()).GetBox();
		}
		else if (StaticMesh->GetStaticMesh())
		{
			Bounds += StaticMesh->CalcBounds(StaticMesh->GetRelativeTransform()).GetBox();
		}
	}
	if (Bounds.IsValid)
	{
		DropCenter = Bounds.GetCenter();
		DropCollision->SetBoxExtent(Bounds.GetExtent().ComponentMax(FVector(MinDropCollisionExtent)), true);
	}
	else
	{
		DropCenter = FVector::ZeroVector;
		DropCollision->SetBoxExtent(DropCollisionExtent, true);
	}
}

bool AHitmenWeaponBase::IsLocallyOwned() const
{
	return OwnerPawn && OwnerPawn->IsLocallyControlled();
}

USceneComponent* AHitmenWeaponBase::GetAttachParent() const
{
	if (const AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(OwnerPawn))
	{
		return Player->GetWeaponAttachComponent();
	}
	if (const ACharacter* Character = Cast<ACharacter>(OwnerPawn))
	{
		return Character->GetMesh();
	}
	return OwnerPawn ? OwnerPawn->GetRootComponent() : nullptr;
}

// ---- State ----

void AHitmenWeaponBase::SetEquipped(bool bEquipped)
{
	if (!HasAuthority() || WeaponState == EHitmenWeaponState::Dropped)
	{
		return;
	}
	const EHitmenWeaponState NewState = bEquipped ? EHitmenWeaponState::InHand : EHitmenWeaponState::Holstered;
	if (NewState != WeaponState || !bStateApplied)
	{
		WeaponState = NewState;
		ApplyWeaponState();
	}
}

void AHitmenWeaponBase::ApplyWeaponState()
{
	const bool bFirstApply = !bStateApplied;
	const EHitmenWeaponState Previous = AppliedState;
	bStateApplied = true;
	AppliedState = WeaponState;

	SetFirstPersonVisuals(false);
	if (WeaponState == EHitmenWeaponState::Dropped)
	{
		StopFire();
		if (GetAttachParentActor())
		{
			DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
		}
		// First-person arms are scaled: a dropped weapon is always back to its real size.
		SetActorScale3D(FVector::OneVector);
		Root->SetRelativeLocationAndRotation(-DropCenter, FRotator::ZeroRotator);
		SetActorHiddenInGame(false);
		AutoPickupSphere->SetCollisionEnabled(HasAuthority() ? ECollisionEnabled::QueryOnly : ECollisionEnabled::NoCollision);
		SetActorTickEnabled(true);
		if (bHovering)
		{
			ApplyHoverState();
		}
		else
		{
			DropCollision->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
			// Clients simulate too; replicated movement corrects them.
			DropCollision->SetSimulatePhysics(true);
		}
	}
	else
	{
		DropCollision->SetSimulatePhysics(false);
		DropCollision->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		AutoPickupSphere->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Root->SetRelativeLocationAndRotation(FVector::ZeroVector, FRotator::ZeroRotator);
		SetActorTickEnabled(false);

		const bool bInHand = WeaponState == EHitmenWeaponState::InHand;
		SetActorHiddenInGame(!bInHand);
		if (!bInHand)
		{
			StopFire();
		}
		if (USceneComponent* Parent = GetAttachParent())
		{
			const FName Socket = WeaponData ? WeaponData->AttachSocket : NAME_None;
			AttachToComponent(Parent, FAttachmentTransformRules::SnapToTargetNotIncludingScale, Socket);
			// The local player's first-person arms use their own offset and FOV / scale rendering.
			const AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(OwnerPawn);
			const bool bFirstPerson = Player && Parent == Player->GetFirstPersonMesh();
			SetFirstPersonVisuals(bFirstPerson);
			if (WeaponData)
			{
				SetActorRelativeTransform(bFirstPerson ? WeaponData->FirstPersonAttachOffset : WeaponData->AttachOffset);
			}
		}
	}

	// Transitions only: ammo updates re-apply the same state without replaying juice.
	const bool bWasInHand = !bFirstApply && Previous == EHitmenWeaponState::InHand;
	const bool bNowInHand = WeaponState == EHitmenWeaponState::InHand;
	if (bWasInHand != bNowInHand)
	{
		Juice_OnEquipped(bNowInHand);
		if (bNowInHand && !bFirstApply && WeaponData && GetNetMode() != NM_DedicatedServer)
		{
			if (AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerPawn))
			{
				Character->PlayCosmeticMontage(WeaponData->EquipMontage.LoadSynchronous());
			}
		}
	}
	const bool bWasDropped = !bFirstApply && Previous == EHitmenWeaponState::Dropped;
	const bool bNowDropped = WeaponState == EHitmenWeaponState::Dropped;
	if (bWasDropped != bNowDropped)
	{
		Juice_OnDropped(bNowDropped);
	}
}

void AHitmenWeaponBase::Drop(int32 InAmmo, const FVector& Location, const FRotator& Rotation, const FVector& Velocity, APawn* Thrower)
{
	if (!HasAuthority())
	{
		return;
	}
	StopFire();
	DroppedAmmo = InAmmo;
	LastThrower = Thrower;
	bThrowDamageDone = Thrower == nullptr;
	bConsumed = false;
	bPickupActive = false;
	UnwatchAllInventories();

	OwnerPawn = nullptr;
	SetOwner(nullptr);
	SetInstigator(nullptr);
	bHovering = false;
	SlowTime = 0.f;
	DropTime = 0.f;
	WeaponState = EHitmenWeaponState::Dropped;
	SetReplicatingMovement(true);
	ApplyWeaponState();

	SetActorLocationAndRotation(Location, Rotation, false, nullptr, ETeleportType::TeleportPhysics);
	if (!Thrower)
	{
		// Swapped or dropped (not thrown): it becomes a floating pickup at once, no tumbling.
		StartHovering();
		GetWorldTimerManager().ClearTimer(PickupActivationTimer);
		GetWorldTimerManager().SetTimer(PickupActivationTimer, this, &AHitmenWeaponBase::ActivatePickup, FMath::Max(0.05f, ThrownPickupDelay), false);
		return;
	}
	// A thrown weapon hits pawns until its first impact; then pawns walk through it.
	DropCollision->SetCollisionResponseToChannel(ECC_Pawn, Thrower ? ECR_Block : ECR_Ignore);
	DropCollision->SetPhysicsLinearVelocity(Velocity);
	const FVector Spin = Thrower
		? FVector(ThrowAngularVelocity.X * (FMath::RandBool() ? 1.f : -1.f), ThrowAngularVelocity.Y, ThrowAngularVelocity.Z)
		: FVector::ZeroVector;
	DropCollision->SetPhysicsAngularVelocityInDegrees(Spin);
	ForceNetUpdate();

	GetWorldTimerManager().ClearTimer(PickupActivationTimer);
	if (ThrownPickupDelay > 0.f)
	{
		GetWorldTimerManager().SetTimer(PickupActivationTimer, this, &AHitmenWeaponBase::ActivatePickup, ThrownPickupDelay, false);
	}
	else
	{
		ActivatePickup();
	}
}

void AHitmenWeaponBase::PickUp(APawn* NewOwner)
{
	if (!HasAuthority() || !NewOwner)
	{
		return;
	}
	GetWorldTimerManager().ClearTimer(PickupActivationTimer);
	bPickupActive = false;
	UnwatchAllInventories();
	LastThrower = nullptr;
	DropCollision->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore);

	SetReplicatingMovement(false);
	bHovering = false;
	OwnerPawn = NewOwner;
	SetOwner(NewOwner);
	SetInstigator(NewOwner);
	WeaponState = EHitmenWeaponState::Holstered;
	ApplyWeaponState();
	ForceNetUpdate();
}

int32 AHitmenWeaponBase::GetDroppedAmmo() const
{
	if (DroppedAmmo >= 0)
	{
		return DroppedAmmo;
	}
	return WeaponData ? WeaponData->StartAmmo : 0;
}

// ---- Dropped pickup (server) ----

void AHitmenWeaponBase::ActivatePickup()
{
	if (!HasAuthority() || !IsDropped())
	{
		return;
	}
	bPickupActive = true;
	ScanOverlappingPawns();
}

void AHitmenWeaponBase::ScanOverlappingPawns()
{
	TArray<AActor*> Overlapping;
	AutoPickupSphere->GetOverlappingActors(Overlapping, APawn::StaticClass());
	for (AActor* Actor : Overlapping)
	{
		WatchInventory(Cast<APawn>(Actor));
		TryAutoAmmo(Actor);
		if (!IsValid(this) || bConsumed)
		{
			return;
		}
	}
}

void AHitmenWeaponBase::HandleAutoPickupBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	if (!IsDropped())
	{
		return;
	}
	WatchInventory(Cast<APawn>(OtherActor));
	TryAutoAmmo(OtherActor);
}

void AHitmenWeaponBase::HandleAutoPickupEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex)
{
	UnwatchInventory(Cast<APawn>(OtherActor));
}

void AHitmenWeaponBase::HandleWatchedInventoryChanged()
{
	if (bPickupActive && IsDropped() && !bConsumed)
	{
		ScanOverlappingPawns();
	}
}

void AHitmenWeaponBase::WatchInventory(APawn* Pawn)
{
	UHitmenInventoryComponent* Inventory = Pawn && Pawn->IsPlayerControlled() ? Pawn->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	if (Inventory && !WatchedInventories.Contains(TWeakObjectPtr<UHitmenInventoryComponent>(Inventory)))
	{
		WatchedInventories.Add(TWeakObjectPtr<UHitmenInventoryComponent>(Inventory));
		Inventory->OnInventoryChanged.AddUniqueDynamic(this, &AHitmenWeaponBase::HandleWatchedInventoryChanged);
	}
}

void AHitmenWeaponBase::UnwatchInventory(APawn* Pawn)
{
	UHitmenInventoryComponent* Inventory = Pawn ? Pawn->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	if (Inventory && WatchedInventories.Remove(TWeakObjectPtr<UHitmenInventoryComponent>(Inventory)) > 0)
	{
		Inventory->OnInventoryChanged.RemoveDynamic(this, &AHitmenWeaponBase::HandleWatchedInventoryChanged);
	}
}

void AHitmenWeaponBase::UnwatchAllInventories()
{
	for (const TWeakObjectPtr<UHitmenInventoryComponent>& Weak : WatchedInventories)
	{
		if (UHitmenInventoryComponent* Inventory = Weak.Get())
		{
			Inventory->OnInventoryChanged.RemoveDynamic(this, &AHitmenWeaponBase::HandleWatchedInventoryChanged);
		}
	}
	WatchedInventories.Reset();
}

void AHitmenWeaponBase::TryAutoAmmo(AActor* Other)
{
	if (!HasAuthority() || !bPickupActive || bConsumed || !IsDropped() || !WeaponData || !WeaponData->bUsesAmmo)
	{
		return;
	}
	APawn* Pawn = Cast<APawn>(Other);
	if (!Pawn || !Pawn->IsPlayerControlled())
	{
		return;
	}
	if (const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Pawn); Character && Character->IsDead())
	{
		return;
	}
	UHitmenInventoryComponent* Inventory = Pawn->FindComponentByClass<UHitmenInventoryComponent>();
	if (!Inventory || !Inventory->HasWeaponType(WeaponData))
	{
		return;
	}
	// Same weapon type: take its ammunition and remove it, no key press. Guard first: TryAddAmmo
	// broadcasts OnInventoryChanged, which re-enters HandleWatchedInventoryChanged.
	bConsumed = true;
	UnwatchAllInventories();
	const int32 Amount = DroppedAmmo >= 0 ? FMath::Max(DroppedAmmo, WeaponData->AmmoPerPickup) : WeaponData->AmmoPerPickup;
	if (Inventory->TryAddAmmo(WeaponData, Amount))
	{
		Destroy();
		return;
	}
	bConsumed = false;
}

void AHitmenWeaponBase::HandleDropHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult& Hit)
{
	if (!IsDropped() || bThrowDamageDone)
	{
		return;
	}
	if (OtherActor == LastThrower)
	{
		return;
	}
	// First impact of a throw: hurt a hostile pawn once, then become a plain pickup.
	bThrowDamageDone = true;
	DropCollision->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore);
	if (WeaponData && LastThrower && Cast<APawn>(OtherActor) && UHitmenTeamLibrary::AreHostile(LastThrower, OtherActor))
	{
		UGameplayStatics::ApplyPointDamage(OtherActor, WeaponData->ThrowDamage, DropCollision->GetPhysicsLinearVelocity().GetSafeNormal(), Hit, LastThrower->GetController(), this, UDamageType::StaticClass());
	}
}

// ---- Interaction / Sightroom ----

bool AHitmenWeaponBase::CanInteract_Implementation(AActor* Interactor) const
{
	if (!IsDropped() || !WeaponData || bConsumed)
	{
		return false;
	}
	// Server: respect the post-throw delay (clients show the prompt; the server decides).
	if (HasAuthority() && !bPickupActive)
	{
		return false;
	}
	return Interactor && Interactor->FindComponentByClass<UHitmenInventoryComponent>() != nullptr;
}

FText AHitmenWeaponBase::GetInteractionText_Implementation(AActor* Interactor) const
{
	if (!WeaponData)
	{
		return FText::GetEmpty();
	}
	const UHitmenInventoryComponent* Inventory = Interactor ? Interactor->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	if (Inventory && !Inventory->HasFreeSlot() && !Inventory->HasWeaponType(WeaponData))
	{
		return FText::Format(NSLOCTEXT("Hitmen", "SwapWeapon", "Swap for {0}"), WeaponData->DisplayName);
	}
	return FText::Format(NSLOCTEXT("Hitmen", "PickUpWeapon", "Pick up {0}"), WeaponData->DisplayName);
}

void AHitmenWeaponBase::Interact_Implementation(AActor* Interactor)
{
	if (!HasAuthority() || !IHitmenInteractable::Execute_CanInteract(this, Interactor))
	{
		return;
	}
	if (UHitmenInventoryComponent* Inventory = Interactor->FindComponentByClass<UHitmenInventoryComponent>())
	{
		Inventory->PickUpWeapon(this, true);
	}
}

EHitmenScanCategory AHitmenWeaponBase::GetScanCategory_Implementation(const AActor* Observer) const
{
	return IsDropped() ? EHitmenScanCategory::Interactable : EHitmenScanCategory::None;
}

void AHitmenWeaponBase::SetScanHighlight_Implementation(bool bEnabled, int32 StencilValue)
{
	for (UPrimitiveComponent* Primitive : { static_cast<UPrimitiveComponent*>(StaticMesh.Get()), static_cast<UPrimitiveComponent*>(SkeletalMesh.Get()) })
	{
		if (Primitive)
		{
			Primitive->SetRenderCustomDepth(bEnabled);
			Primitive->SetCustomDepthStencilValue(bEnabled ? StencilValue : 0);
		}
	}
}

// ---- Firing ----

FVector AHitmenWeaponBase::GetMuzzleLocation() const
{
	const FName Socket = WeaponData ? WeaponData->MuzzleSocket : NAME_None;
	if (SkeletalMesh->GetSkeletalMeshAsset() && SkeletalMesh->DoesSocketExist(Socket))
	{
		return SkeletalMesh->GetSocketLocation(Socket);
	}
	if (StaticMesh->GetStaticMesh() && StaticMesh->DoesSocketExist(Socket))
	{
		return StaticMesh->GetSocketLocation(Socket);
	}
	const FVector Offset = WeaponData ? WeaponData->MuzzleOffset : FVector(40.f, 0.f, 0.f);
	return GetActorTransform().TransformPosition(Offset);
}

int32 AHitmenWeaponBase::GetOwnerAmmo() const
{
	if (!WeaponData || !WeaponData->bUsesAmmo)
	{
		return MAX_int32;
	}
	const UHitmenInventoryComponent* Inventory = OwnerPawn ? OwnerPawn->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	return Inventory ? Inventory->GetActiveAmmo() : MAX_int32;
}

float AHitmenWeaponBase::GetCurrentSpread() const
{
	if (!WeaponData)
	{
		return 0.f;
	}
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerPawn);
	return (Character && Character->IsAiming()) ? WeaponData->AimSpread : WeaponData->HipSpread;
}

float AHitmenWeaponBase::GetCharge() const
{
	if (!WeaponData || !WeaponData->bChargeToFire || ChargeStartTime < 0.f)
	{
		return 0.f;
	}
	return FMath::Clamp((GetWorld()->GetTimeSeconds() - ChargeStartTime) / WeaponData->FullChargeTime, 0.f, 1.f);
}

void AHitmenWeaponBase::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (IsDropped())
	{
		UpdateDropped(DeltaSeconds);
	}
	else
	{
		SetActorTickEnabled(false);
	}
}

// ---- Lying in the world: fall, settle, float ----

void AHitmenWeaponBase::UpdateDropped(float DeltaSeconds)
{
	if (bHovering)
	{
		// Cosmetic bob and turn on every machine (the actor itself stays still).
		HoverPhase += DeltaSeconds;
		const float Bob = FMath::Sin(HoverPhase * HoverBobFrequency * UE_TWO_PI) * HoverBobAmplitude;
		Root->SetRelativeLocationAndRotation(FRotator(0.f, HoverPhase * HoverSpinSpeed, 0.f).RotateVector(-DropCenter) + FVector(0.f, 0.f, Bob),
			FRotator(0.f, HoverPhase * HoverSpinSpeed, 0.f));
		return;
	}
	if (!HasAuthority())
	{
		return;
	}
	DropTime += DeltaSeconds;
	const bool bSlow = DropCollision->GetPhysicsLinearVelocity().Size() < SettleSpeed;
	SlowTime = bSlow ? SlowTime + DeltaSeconds : 0.f;
	if ((SlowTime >= SettleTime && DropTime > ThrownPickupDelay * 0.5f) || DropTime >= MaxFallTime)
	{
		StartHovering();
	}
}

void AHitmenWeaponBase::StartHovering()
{
	if (!HasAuthority() || !IsDropped() || bHovering)
	{
		return;
	}
	// Float at a fixed height above whatever is under it, upright.
	FVector Location = GetActorLocation();
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenWeaponHover), false, this);
	if (GetWorld()->LineTraceSingleByChannel(Hit, Location + FVector(0.f, 0.f, 50.f), Location - FVector(0.f, 0.f, 400.f), ECC_Visibility, Params))
	{
		Location = Hit.Location + FVector(0.f, 0.f, HoverHeight);
	}
	bHovering = true;
	HoverPhase = FMath::FRand() * 10.f;
	DropCollision->SetSimulatePhysics(false);
	SetActorLocationAndRotation(Location, FRotator(0.f, GetActorRotation().Yaw, 0.f), false, nullptr, ETeleportType::TeleportPhysics);
	ApplyHoverState();
	ForceNetUpdate();
}

void AHitmenWeaponBase::StartHoveringAt(const FVector& Location, const FRotator& Rotation)
{
	if (!HasAuthority() || !IsDropped())
	{
		return;
	}
	bHovering = true;
	HoverPhase = FMath::FRand() * 10.f;
	DropCollision->SetSimulatePhysics(false);
	SetActorLocationAndRotation(Location, Rotation, false, nullptr, ETeleportType::TeleportPhysics);
	ApplyHoverState();
	ForceNetUpdate();
}

void AHitmenWeaponBase::OnRep_Hovering()
{
	if (bHovering && IsDropped())
	{
		ApplyHoverState();
	}
}

void AHitmenWeaponBase::ApplyHoverState()
{
	DropCollision->SetSimulatePhysics(false);
	// Still traceable (F to pick up) and overlappable (auto ammo); no more physics.
	DropCollision->SetCollisionEnabled(ECollisionEnabled::QueryOnly);
	DropCollision->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore);
	SetActorTickEnabled(true);
}

UPrimitiveComponent* AHitmenWeaponBase::GetVisualComponent() const
{
	if (SkeletalMesh->GetSkeletalMeshAsset())
	{
		return SkeletalMesh;
	}
	return StaticMesh;
}

void AHitmenWeaponBase::SetFirstPersonVisuals(bool bFirstPerson)
{
	const EFirstPersonPrimitiveType Type = bFirstPerson ? EFirstPersonPrimitiveType::FirstPerson : EFirstPersonPrimitiveType::None;
	StaticMesh->FirstPersonPrimitiveType = Type;
	SkeletalMesh->FirstPersonPrimitiveType = Type;
	// The local player's own weapon does not cast a shadow from the view (the body's shadow does).
	StaticMesh->SetCastShadow(!bFirstPerson);
	SkeletalMesh->SetCastShadow(!bFirstPerson);
	StaticMesh->MarkRenderStateDirty();
	SkeletalMesh->MarkRenderStateDirty();
}

bool AHitmenWeaponBase::CanFireNow() const
{
	if (!WeaponData || !IsEquipped() || !OwnerPawn)
	{
		return false;
	}
	if (const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerPawn))
	{
		if (Character->IsDead())
		{
			return false;
		}
	}
	return GetWorld()->GetTimeSeconds() - LastFireTime >= WeaponData->GetFireInterval() * 0.95f;
}

void AHitmenWeaponBase::StartFire()
{
	if (!WeaponData || !IsLocallyOwned())
	{
		return;
	}
	bTriggerHeld = true;

	if (WeaponData->bChargeToFire)
	{
		ChargeStartTime = GetWorld()->GetTimeSeconds();
		return;
	}

	if (WeaponData->SpinUpTime > 0.f && !bSpunUp)
	{
		Juice_OnSpinUp(true);
		GetWorldTimerManager().SetTimer(SpinUpTimer, FTimerDelegate::CreateWeakLambda(this, [this]()
		{
			bSpunUp = true;
			if (bTriggerHeld)
			{
				HandleAutoFire();
			}
		}), WeaponData->SpinUpTime, false);
		return;
	}

	BurstRemaining = WeaponData->FireMode == EHitmenFireMode::Burst ? WeaponData->BurstCount : 0;
	HandleAutoFire();
}

void AHitmenWeaponBase::StopFire()
{
	const bool bWasHeld = bTriggerHeld;
	bTriggerHeld = false;
	if (WeaponData && WeaponData->bChargeToFire && bWasHeld && ChargeStartTime >= 0.f)
	{
		FireOnce();
		ChargeStartTime = -1.f;
	}
	if (WeaponData && WeaponData->FireMode != EHitmenFireMode::Burst)
	{
		GetWorldTimerManager().ClearTimer(FireTimer);
	}
	if (bSpunUp || GetWorldTimerManager().IsTimerActive(SpinUpTimer))
	{
		GetWorldTimerManager().ClearTimer(SpinUpTimer);
		bSpunUp = false;
		Juice_OnSpinUp(false);
	}
}

void AHitmenWeaponBase::HandleAutoFire()
{
	if (!WeaponData)
	{
		return;
	}
	const float Interval = WeaponData->GetFireInterval();
	const float Wait = FMath::Max(0.f, LastFireTime + Interval - GetWorld()->GetTimeSeconds());
	if (Wait > 0.f)
	{
		GetWorldTimerManager().SetTimer(FireTimer, this, &AHitmenWeaponBase::HandleAutoFire, Wait, false);
		return;
	}

	FireOnce();

	bool bContinue = false;
	switch (WeaponData->FireMode)
	{
	case EHitmenFireMode::Automatic: bContinue = bTriggerHeld; break;
	case EHitmenFireMode::Burst:     bContinue = --BurstRemaining > 0; break;
	default: break;
	}
	if (bContinue)
	{
		GetWorldTimerManager().SetTimer(FireTimer, this, &AHitmenWeaponBase::HandleAutoFire, Interval, false);
	}
}

void AHitmenWeaponBase::FireOnce()
{
	if (!CanFireNow())
	{
		return;
	}

	// Empty (one ammo pool, no reserve): dry click, then throw the weapon where we look.
	if (GetOwnerAmmo() < WeaponData->AmmoPerShot)
	{
		if (USoundBase* DrySound = WeaponData->DryFireSound.LoadSynchronous())
		{
			UGameplayStatics::PlaySoundAtLocation(this, DrySound, GetMuzzleLocation());
		}
		Juice_OnDryFire();
		if (UHitmenInventoryComponent* Inventory = OwnerPawn->FindComponentByClass<UHitmenInventoryComponent>())
		{
			bTriggerHeld = false;
			GetWorldTimerManager().ClearTimer(FireTimer);
			Inventory->ThrowActive();
		}
		return;
	}

	LastFireTime = GetWorld()->GetTimeSeconds();
	FVector Origin, Direction;
	UHitmenCombatLibrary::GetAimPoint(OwnerPawn, Origin, Direction);

	const int32 Seed = FMath::Rand();
	const FVector SpreadDirection = UHitmenCombatLibrary::RandomConeDirection(Direction, GetCurrentSpread(), Seed, 0);
	const float Charge = WeaponData->bChargeToFire ? FMath::Max(0.2f, GetCharge()) : 1.f;

	// Local prediction of cosmetics.
	TArray<FVector> Impacts;
	SimulateImpacts(Origin, SpreadDirection, Seed, Impacts);
	PlayFireEffects(GetMuzzleLocation(), Impacts, true);

	// The view kicks up, then comes back down smoothly (spring on the player character).
	if (AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(OwnerPawn))
	{
		Player->AddRecoil(WeaponData->RecoilPitch, FMath::FRandRange(-WeaponData->RecoilYawRandom, WeaponData->RecoilYawRandom));
	}
	if (APlayerController* PC = Cast<APlayerController>(OwnerPawn->GetController()); PC && WeaponData->FireCameraShake)
	{
		PC->ClientStartCameraShake(WeaponData->FireCameraShake);
	}

	Server_Fire(Origin, SpreadDirection, Seed, Charge);
}

void AHitmenWeaponBase::Server_Fire_Implementation(FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction, int32 Seed, float Charge)
{
	if (!WeaponData || !OwnerPawn || !IsEquipped())
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	// Rate validation with a little tolerance for jitter.
	if (Now - LastServerFireTime < WeaponData->GetFireInterval() * 0.7f)
	{
		return;
	}
	// Origin must be near the shooter.
	if (FVector::Dist(Origin, OwnerPawn->GetActorLocation()) > 400.f)
	{
		return;
	}
	UHitmenInventoryComponent* Inventory = OwnerPawn->FindComponentByClass<UHitmenInventoryComponent>();
	if (Inventory && !Inventory->ConsumeActiveAmmo(WeaponData->AmmoPerShot))
	{
		return;
	}
	LastServerFireTime = Now;

	TArray<FVector> Impacts;
	FireServer(Origin, Direction, Seed, FMath::Clamp(Charge, 0.f, 1.f), Impacts);
	ReportShotNoise(Origin);

	TArray<FVector_NetQuantize> NetImpacts;
	for (const FVector& Impact : Impacts)
	{
		NetImpacts.Add(Impact);
	}
	Multicast_FireEffects(GetMuzzleLocation(), NetImpacts);
}

void AHitmenWeaponBase::FireAt(const FVector& TargetLocation, float AimErrorDegrees)
{
	if (!HasAuthority() || !WeaponData || !OwnerPawn)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	if (Now - LastServerFireTime < WeaponData->GetFireInterval())
	{
		return;
	}
	LastServerFireTime = Now;

	FVector Origin, Unused;
	UHitmenCombatLibrary::GetAimPoint(OwnerPawn, Origin, Unused);
	const FVector Direction = (TargetLocation - Origin).GetSafeNormal();
	const int32 Seed = FMath::Rand();
	const FVector Spread = UHitmenCombatLibrary::RandomConeDirection(Direction, AimErrorDegrees + WeaponData->HipSpread, Seed, 0);

	TArray<FVector> Impacts;
	FireServer(Origin, Spread, Seed, 1.f, Impacts);
	ReportShotNoise(Origin);

	TArray<FVector_NetQuantize> NetImpacts;
	for (const FVector& Impact : Impacts)
	{
		NetImpacts.Add(Impact);
	}
	Multicast_FireEffects(GetMuzzleLocation(), NetImpacts);
}

void AHitmenWeaponBase::ReportShotNoise(const FVector& Origin) const
{
	if (WeaponData && !WeaponData->bSilenced && WeaponData->NoiseRadius > 0.f)
	{
		UHitmenAISubsystem::ReportNoise(this, Origin, WeaponData->NoiseRadius, OwnerPawn, EHitmenNoiseType::Gunshot);
	}
}

void AHitmenWeaponBase::Multicast_FireEffects_Implementation(FVector_NetQuantize Origin, const TArray<FVector_NetQuantize>& Impacts)
{
	// The owning client already played predicted effects.
	if (IsLocallyOwned() && OwnerPawn && OwnerPawn->IsPlayerControlled())
	{
		return;
	}
	TArray<FVector> Points;
	for (const FVector_NetQuantize& Impact : Impacts)
	{
		Points.Add(Impact);
	}
	PlayFireEffects(Origin, Points, false);
}

void AHitmenWeaponBase::PlayFireEffects(const FVector& Origin, const TArray<FVector>& Impacts, bool bLocallyPredicted)
{
	if (GetNetMode() == NM_DedicatedServer || !WeaponData)
	{
		return;
	}
	if (UNiagaraSystem* Muzzle = WeaponData->MuzzleFX.LoadSynchronous())
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Muzzle, Origin, GetActorRotation());
	}
	if (UNiagaraSystem* BackBlast = WeaponData->BackBlastFX.LoadSynchronous())
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, BackBlast, GetActorTransform().TransformPosition(WeaponData->BackBlastOffset), (-GetActorForwardVector()).Rotation());
	}
	if (USoundBase* Sound = WeaponData->FireSound.LoadSynchronous())
	{
		UGameplayStatics::PlaySoundAtLocation(this, Sound, Origin);
	}
	// Same cosmetic path as the FX: predicted on the shooter, multicast to everyone else.
	if (AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerPawn))
	{
		Character->PlayCosmeticMontage(WeaponData->FireMontage.LoadSynchronous());
	}
	UNiagaraSystem* Tracer = WeaponData->TracerFX.LoadSynchronous();
	UNiagaraSystem* ImpactFX = WeaponData->ImpactFX.LoadSynchronous();
	UNiagaraSystem* SurfaceFX = WeaponData->SurfaceImpactFX.LoadSynchronous();
	UNiagaraSystem* FleshFX = WeaponData->FleshImpactFX.LoadSynchronous();
	USoundBase* ImpactSound = WeaponData->ImpactSound.LoadSynchronous();
	FCollisionQueryParams FleshParams(SCENE_QUERY_STAT(HitmenImpactFlesh), false, this);
	FleshParams.AddIgnoredActor(OwnerPawn);
	for (const FVector& Impact : Impacts)
	{
		// Cosmetic: a character right at the impact point bleeds, anything else throws dust and sparks.
		const bool bFlesh = FleshFX && GetWorld()->OverlapAnyTestByChannel(Impact, FQuat::Identity, ECC_Pawn, FCollisionShape::MakeSphere(12.f), FleshParams);
		const FRotator BackToShooter = (Origin - Impact).GetSafeNormal().Rotation();
		if (bFlesh)
		{
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, FleshFX, Impact, BackToShooter);
			if (Tracer)
			{
				if (UNiagaraComponent* TracerComp = UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Tracer, Origin, (Impact - Origin).Rotation()))
				{
					TracerComp->SetVectorParameter(TEXT("TracerEnd"), Impact);
				}
			}
			Juice_OnImpact(Impact);
			continue;
		}
		if (SurfaceFX)
		{
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, SurfaceFX, Impact, BackToShooter);
		}
		if (Tracer)
		{
			if (UNiagaraComponent* TracerComp = UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Tracer, Origin, (Impact - Origin).Rotation()))
			{
				TracerComp->SetVectorParameter(TEXT("TracerEnd"), Impact);
			}
		}
		if (ImpactFX)
		{
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, ImpactFX, Impact);
		}
		if (ImpactSound)
		{
			UGameplayStatics::PlaySoundAtLocation(this, ImpactSound, Impact);
		}
		Juice_OnImpact(Impact);
	}
	Juice_OnFired(bLocallyPredicted);
}
