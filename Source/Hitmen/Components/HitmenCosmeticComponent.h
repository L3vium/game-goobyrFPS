// Hitmen — applies the head / top / body / bottom cosmetics to a character

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Core/HitmenTypes.h"
#include "HitmenCosmeticComponent.generated.h"

class USkeletalMeshComponent;
class UStaticMeshComponent;

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenCosmeticsApplied);

/**
 * Creates one skeletal and one static mesh component per slot at BeginPlay (hidden) and only
 * swaps meshes afterwards — nothing is spawned or destroyed when the look changes.
 */
UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenCosmeticComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenCosmeticComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Cosmetics")
	FOnHitmenCosmeticsApplied OnCosmeticsApplied;

	/** Server (replicates) or local preview (customization screen). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Cosmetics")
	void SetCosmetics(const FHitmenCosmeticLoadout& NewCosmetics);

	UFUNCTION(BlueprintPure, Category="Hitmen|Cosmetics")
	const FHitmenCosmeticLoadout& GetCosmetics() const { return Cosmetics; }

	/** All mesh components created for cosmetics (sightroom highlight, owner-no-see). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Cosmetics")
	TArray<UPrimitiveComponent*> GetCosmeticPrimitives() const;

protected:
	/** Mesh the cosmetics follow; defaults to the character mesh. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Cosmetics")
	FName TargetMeshComponentName = NAME_None;

	/** Hide cosmetics from the owning first-person camera. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Cosmetics")
	bool bOwnerNoSee = true;

	UPROPERTY(ReplicatedUsing=OnRep_Cosmetics)
	FHitmenCosmeticLoadout Cosmetics;

	UPROPERTY(Transient)
	TArray<TObjectPtr<USkeletalMeshComponent>> SkeletalSlots;

	UPROPERTY(Transient)
	TArray<TObjectPtr<UStaticMeshComponent>> StaticSlots;

	/** Item shown in each slot: unchanged slots are skipped (fast look cycling). */
	UPROPERTY(Transient)
	TArray<TObjectPtr<const UHitmenCosmeticItem>> AppliedItems;
	bool bAppliedOnce = false;

	UFUNCTION() void OnRep_Cosmetics();

	void ApplyCosmetics();
	USkeletalMeshComponent* GetTargetMesh() const;
};
