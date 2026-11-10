// Hitmen — container

#include "World/Interactables/HitmenContainer.h"
#include "World/HitmenGeometry.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Abilities/HitmenAbilityActors.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenAbilityData.h"
#include "Data/HitmenCatalog.h"
#include "Core/HitmenGameSettings.h"
#include "Hitmen.h"
#include "UObject/ConstructorHelpers.h"
#include "Engine/World.h"

AHitmenContainer::AHitmenContainer()
{
	OpenText = NSLOCTEXT("Hitmen", "OpenContainer", "Open");
	CloseText = NSLOCTEXT("Hitmen", "CloseContainer", "Close");

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(HitmenGeometry::CubePath);
	auto MakePanel = [this](const TCHAR* Name, USceneComponent* Parent)
	{
		UStaticMeshComponent* Panel = CreateDefaultSubobject<UStaticMeshComponent>(Name);
		Panel->SetupAttachment(Parent);
		if (Cube.Succeeded())
		{
			Panel->SetStaticMesh(Cube.Object);
		}
		Panel->SetCollisionProfileName(TEXT("BlockAllDynamic"));
		Panel->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
		return Panel;
	};
	if (Cube.Succeeded())
	{
		Mesh->SetStaticMesh(Cube.Object);
	}
	PanelLeft = MakePanel(TEXT("PanelLeft"), Root);
	PanelRight = MakePanel(TEXT("PanelRight"), Root);
	PanelTop = MakePanel(TEXT("PanelTop"), Root);
	PanelBottom = MakePanel(TEXT("PanelBottom"), Root);
	Shelf = MakePanel(TEXT("Shelf"), Root);
	// The shelf never hides what lies on it from the F trace.
	Shelf->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Ignore);
	PanelBottom->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Ignore);
	LeftHinge = CreateDefaultSubobject<USceneComponent>(TEXT("LeftHinge"));
	LeftHinge->SetupAttachment(Root);
	RightHinge = CreateDefaultSubobject<USceneComponent>(TEXT("RightHinge"));
	RightHinge->SetupAttachment(Root);
	LeftDoor = MakePanel(TEXT("LeftDoor"), LeftHinge);
	RightDoor = MakePanel(TEXT("RightDoor"), RightHinge);
}

void AHitmenContainer::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	const float T = WallThickness;
	const float HalfD = Depth * 0.5f;
	const float HalfW = Width * 0.5f;
	HitmenGeometry::SetBox(Mesh, FVector(-HalfD + T * 0.5f, 0.f, Height * 0.5f), FVector(T, Width, Height));
	HitmenGeometry::SetBox(PanelLeft, FVector(0.f, -HalfW + T * 0.5f, Height * 0.5f), FVector(Depth, T, Height));
	HitmenGeometry::SetBox(PanelRight, FVector(0.f, HalfW - T * 0.5f, Height * 0.5f), FVector(Depth, T, Height));
	HitmenGeometry::SetBox(PanelTop, FVector(0.f, 0.f, Height - T * 0.5f), FVector(Depth, Width, T));
	HitmenGeometry::SetBox(PanelBottom, FVector(0.f, 0.f, T * 0.5f), FVector(Depth, Width, T));
	Shelf->SetVisibility(ShelfHeight > 0.f);
	Shelf->SetCollisionEnabled(ShelfHeight > 0.f ? ECollisionEnabled::QueryAndPhysics : ECollisionEnabled::NoCollision);
	HitmenGeometry::SetBox(Shelf, FVector(0.f, 0.f, FMath::Max(ShelfHeight, T)), FVector(Depth - T, Width - 2.f * T, T));
	// Doors hinged on the front outer edges, each covering half the front.
	LeftHinge->SetRelativeLocation(FVector(HalfD, -HalfW, 0.f));
	RightHinge->SetRelativeLocation(FVector(HalfD, HalfW, 0.f));
	HitmenGeometry::SetBox(LeftDoor, FVector(0.f, HalfW * 0.5f, Height * 0.5f), FVector(T, HalfW - 1.f, Height - 1.f));
	HitmenGeometry::SetBox(RightDoor, FVector(0.f, -HalfW * 0.5f, Height * 0.5f), FVector(T, HalfW - 1.f, Height - 1.f));
	for (UStaticMeshComponent* Panel : { PanelLeft.Get(), PanelRight.Get(), PanelTop.Get(), PanelBottom.Get(), Shelf.Get(), LeftDoor.Get(), RightDoor.Get() })
	{
		HitmenGeometry::InheritLook(Panel, Mesh);
	}
	ApplyOpenAlpha(bOpen ? 1.f : 0.f);
}

void AHitmenContainer::ApplyOpenAlpha(float Alpha)
{
	// Doors always swing out to the front.
	const float Angle = DoorOpenAngle * Alpha;
	LeftHinge->SetRelativeRotation(FRotator(0.f, -Angle, 0.f));
	RightHinge->SetRelativeRotation(FRotator(0.f, Angle, 0.f));
	// Nothing inside shows through closed doors (hidden state replicates to clients).
	if (HasAuthority())
	{
		const bool bReveal = Alpha > 0.15f;
		const float Reach = FMath::Max(Width, Height) + 50.f;
		for (int32 Index = SpawnedContents.Num() - 1; Index >= 0; --Index)
		{
			AActor* Content = SpawnedContents[Index];
			// Taken out (or destroyed): no longer the container's business.
			if (!IsValid(Content) || Content->GetAttachParentActor() || FVector::Dist(Content->GetActorLocation(), GetActorLocation()) > Reach)
			{
				if (IsValid(Content))
				{
					Content->SetActorHiddenInGame(false);
				}
				SpawnedContents.RemoveAt(Index);
				continue;
			}
			Content->SetActorHiddenInGame(!bReveal);
		}
	}
}

FTransform AHitmenContainer::GetSlotTransform(int32 SlotIndex) const
{
	const int32 PerShelf = FMath::Max(1, SlotsPerShelf);
	const int32 Level = SlotIndex / PerShelf;
	const int32 Column = SlotIndex % PerShelf;
	const float Usable = Width - 2.f * WallThickness;
	const float Y = -Usable * 0.5f + Usable * (Column + 0.5f) / PerShelf;
	const float Floor = Level == 0 ? WallThickness : ShelfHeight + WallThickness * 0.5f;
	const FVector Local(0.f, Y, Floor + SlotHoverHeight);
	// Weapons lie across the cabinet (barrel along the width).
	return FTransform(FRotator(0.f, 90.f, 0.f), Local) * GetActorTransform();
}

void AHitmenContainer::BeginPlay()
{
	Super::BeginPlay();
	if (HasAuthority())
	{
		SpawnContents();
	}
}

void AHitmenContainer::SpawnContents()
{
	TArray<FHitmenContainerEntry> Chosen;
	if (ContentsMode == EHitmenContainerContents::Fixed)
	{
		Chosen = Contents;
	}
	else
	{
		TArray<FHitmenContainerEntry> Pool = RandomPool;
		if (Pool.IsEmpty())
		{
			if (const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog())
			{
				for (UHitmenWeaponData* Weapon : Catalog->LootWeapons)
				{
					FHitmenContainerEntry& Entry = Pool.AddDefaulted_GetRef();
					Entry.Weapon = Weapon;
					Entry.Weight = Weapon ? Weapon->LootWeight : 0.f;
				}
				for (const EHitmenAbilitySlot Slot : { EHitmenAbilitySlot::Move, EHitmenAbilitySlot::Manipulation })
				{
					for (UHitmenAbilityData* Item : Catalog->GetAbilities(Slot))
					{
						FHitmenContainerEntry& Entry = Pool.AddDefaulted_GetRef();
						Entry.Item = Item;
						Entry.Weight = Item ? Item->LootWeight : 0.f;
					}
				}
			}
		}
		const int32 Count = FMath::Clamp(FMath::RandRange(FMath::Min(RandomCount.X, RandomCount.Y), FMath::Max(RandomCount.X, RandomCount.Y)), 0, GetSlotCount());
		for (int32 Index = 0; Index < Count && !Pool.IsEmpty(); ++Index)
		{
			float Total = 0.f;
			for (const FHitmenContainerEntry& Entry : Pool)
			{
				Total += FMath::Max(0.f, Entry.Weight);
			}
			float Roll = FMath::FRand() * Total;
			int32 Picked = Pool.Num() - 1;
			for (int32 Candidate = 0; Candidate < Pool.Num(); ++Candidate)
			{
				Roll -= FMath::Max(0.f, Pool[Candidate].Weight);
				if (Roll <= 0.f)
				{
					Picked = Candidate;
					break;
				}
			}
			Chosen.Add(Pool[Picked]);
			Pool.RemoveAt(Picked); // No duplicates in one container.
		}
	}
	for (int32 Index = 0; Index < Chosen.Num() && Index < GetSlotCount(); ++Index)
	{
		SpawnEntry(Chosen[Index], Index);
	}
	// Closed doors hide what was just put inside.
	ApplyOpenAlpha(bOpen ? 1.f : 0.f);
}

void AHitmenContainer::SpawnEntry(const FHitmenContainerEntry& Entry, int32 SlotIndex)
{
	const FTransform Slot = GetSlotTransform(SlotIndex);
	if (Entry.Weapon)
	{
		if (AHitmenWeaponBase* Weapon = AHitmenWeaponBase::SpawnDropped(this, Entry.Weapon, -1, Slot))
		{
			Weapon->StartHoveringAt(Slot.GetLocation(), Slot.Rotator());
			SpawnedContents.Add(Weapon);
		}
		return;
	}
	if (Entry.Item)
	{
		FActorSpawnParameters Params;
		Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		const TSubclassOf<AHitmenAbilityPickup> PickupClass = Entry.Item->PickupClass ? Entry.Item->PickupClass : TSubclassOf<AHitmenAbilityPickup>(AHitmenAbilityPickup::StaticClass());
		// Ability pickups float their mesh above their origin: put the origin on the shelf.
		const FVector Location = Slot.GetLocation() - FVector(0.f, 0.f, SlotHoverHeight);
		if (AHitmenAbilityPickup* Pickup = GetWorld()->SpawnActor<AHitmenAbilityPickup>(PickupClass, Location, Slot.Rotator(), Params))
		{
			Pickup->InitPickup(Entry.Item);
			SpawnedContents.Add(Pickup);
		}
	}
}
