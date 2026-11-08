// Hitmen — cosmetics

#include "Components/HitmenCosmeticComponent.h"
#include "Data/HitmenCosmeticItem.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/StaticMesh.h"
#include "GameFramework/Character.h"
#include "Net/UnrealNetwork.h"

namespace
{
	const EHitmenCosmeticSlot AllSlots[] = { EHitmenCosmeticSlot::Head, EHitmenCosmeticSlot::Body, EHitmenCosmeticSlot::Legs };
}

UHitmenCosmeticComponent::UHitmenCosmeticComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenCosmeticComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenCosmeticComponent, Cosmetics);
}

USkeletalMeshComponent* UHitmenCosmeticComponent::GetTargetMesh() const
{
	if (TargetMeshComponentName != NAME_None)
	{
		TArray<USkeletalMeshComponent*> Meshes;
		GetOwner()->GetComponents(Meshes);
		for (USkeletalMeshComponent* Mesh : Meshes)
		{
			if (Mesh->GetFName() == TargetMeshComponentName)
			{
				return Mesh;
			}
		}
	}
	const ACharacter* Character = Cast<ACharacter>(GetOwner());
	return Character ? Character->GetMesh() : GetOwner()->FindComponentByClass<USkeletalMeshComponent>();
}

void UHitmenCosmeticComponent::BeginPlay()
{
	Super::BeginPlay();

	USkeletalMeshComponent* Target = GetTargetMesh();
	if (!Target)
	{
		return;
	}
	for (const EHitmenCosmeticSlot Slot : AllSlots)
	{
		const FName SlotName = *UEnum::GetValueAsName(Slot).ToString().Replace(TEXT("EHitmenCosmeticSlot::"), TEXT(""));

		USkeletalMeshComponent* Skeletal = NewObject<USkeletalMeshComponent>(GetOwner(), *FString::Printf(TEXT("Cosmetic_Skel_%s"), *SlotName.ToString()));
		Skeletal->SetupAttachment(Target);
		Skeletal->SetLeaderPoseComponent(Target);
		Skeletal->SetOwnerNoSee(bOwnerNoSee);
		Skeletal->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Skeletal->SetVisibility(false);
		Skeletal->RegisterComponent();
		SkeletalSlots.Add(Skeletal);

		UStaticMeshComponent* Static = NewObject<UStaticMeshComponent>(GetOwner(), *FString::Printf(TEXT("Cosmetic_Static_%s"), *SlotName.ToString()));
		Static->SetupAttachment(Target);
		Static->SetOwnerNoSee(bOwnerNoSee);
		Static->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Static->SetVisibility(false);
		Static->RegisterComponent();
		StaticSlots.Add(Static);
	}
	ApplyCosmetics();
}

void UHitmenCosmeticComponent::SetCosmetics(const FHitmenCosmeticLoadout& NewCosmetics)
{
	Cosmetics = NewCosmetics;
	ApplyCosmetics();
}

void UHitmenCosmeticComponent::OnRep_Cosmetics()
{
	ApplyCosmetics();
}

void UHitmenCosmeticComponent::ApplyCosmetics()
{
	USkeletalMeshComponent* Target = GetTargetMesh();
	if (!Target || SkeletalSlots.Num() != UE_ARRAY_COUNT(AllSlots))
	{
		return;
	}
	for (int32 Index = 0; Index < UE_ARRAY_COUNT(AllSlots); ++Index)
	{
		const UHitmenCosmeticItem* Item = Cosmetics.GetSlot(AllSlots[Index]);
		USkeletalMeshComponent* Skeletal = SkeletalSlots[Index];
		UStaticMeshComponent* Static = StaticSlots[Index];

		USkeletalMesh* SkelMesh = Item ? Item->SkeletalMesh.LoadSynchronous() : nullptr;
		UStaticMesh* StaticMesh = (Item && !SkelMesh) ? Item->StaticMesh.LoadSynchronous() : nullptr;

		Skeletal->SetSkeletalMesh(SkelMesh);
		Skeletal->SetVisibility(SkelMesh != nullptr);

		Static->SetStaticMesh(StaticMesh);
		Static->SetVisibility(StaticMesh != nullptr);
		if (StaticMesh)
		{
			Static->AttachToComponent(Target, FAttachmentTransformRules::SnapToTargetNotIncludingScale, Item->AttachSocket);
			Static->SetRelativeTransform(Item->AttachOffset);
		}
		if (Item && Item->BodyMaterialOverride.IsValid())
		{
			// Tinted outfits are applied on the slot mesh, never on the shared body material.
			if (UMaterialInterface* Material = Item->BodyMaterialOverride.LoadSynchronous())
			{
				(SkelMesh ? static_cast<UMeshComponent*>(Skeletal) : static_cast<UMeshComponent*>(Static))->SetMaterial(0, Material);
			}
		}
	}
	OnCosmeticsApplied.Broadcast();
}

TArray<UPrimitiveComponent*> UHitmenCosmeticComponent::GetCosmeticPrimitives() const
{
	TArray<UPrimitiveComponent*> Result;
	for (USkeletalMeshComponent* Mesh : SkeletalSlots) { Result.Add(Mesh); }
	for (UStaticMeshComponent* Mesh : StaticSlots) { Result.Add(Mesh); }
	return Result;
}
