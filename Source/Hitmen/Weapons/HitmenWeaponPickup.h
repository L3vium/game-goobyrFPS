// Hitmen — weapon in the world: walking over the same type takes its ammo, F swaps or picks up

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "HitmenWeaponPickup.generated.h"

class UHitmenWeaponData;
class USphereComponent;

UCLASS(Blueprintable)
class HITMEN_API AHitmenWeaponPickup : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenWeaponPickup();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;

	/** Server: what this pickup contains. Ammo < 0 = start ammo. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Pickup")
	void InitPickup(UHitmenWeaponData* InWeapon, int32 InAmmo);

	/** Server: physics throw; hitting a pawn deals the weapon's throw damage once. */
	void Throw(const FVector& Velocity, APawn* Thrower);

	UFUNCTION(BlueprintPure, Category="Hitmen|Pickup")
	UHitmenWeaponData* GetWeapon() const { return Weapon; }

	// IHitmenInteractable
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

protected:
	/** Weapon placed by a level designer (overridden by InitPickup). */
	UPROPERTY(EditAnywhere, ReplicatedUsing=OnRep_Weapon, BlueprintReadOnly, Category="Hitmen|Pickup")
	TObjectPtr<UHitmenWeaponData> Weapon;

	UPROPERTY(EditAnywhere, Replicated, BlueprintReadOnly, Category="Hitmen|Pickup")
	int32 Ammo = -1;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USphereComponent> AutoPickupSphere;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Pickup", meta=(ClampMin="0"))
	float ThrownPickupDelay = 0.6f;

	UFUNCTION() void OnRep_Weapon();

	UFUNCTION()
	void HandleAutoPickupOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION()
	void HandleThrownHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult& Hit);

	void ApplyWeaponVisual();
	void TryAutoAmmo(AActor* Other);

	UPROPERTY(Transient)
	TObjectPtr<APawn> LastThrower;

	float PickupEnabledTime = 0.f;
	bool bThrowDamageDone = false;
};
