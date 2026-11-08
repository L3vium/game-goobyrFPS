// Hitmen — holds the move (E) and manipulation (A) abilities and routes input/RPCs to them

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Core/HitmenTypes.h"
#include "HitmenAbilityComponent.generated.h"

class UHitmenAbility;
class UHitmenAbilityData;

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenAbilitiesChanged);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenAbilityComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenAbilityComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Abilities")
	FOnHitmenAbilitiesChanged OnAbilitiesChanged;

	/** Server: equips an ability in its slot (replaces the previous one). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Abilities")
	void SetAbility(UHitmenAbilityData* AbilityData);

	UFUNCTION(BlueprintPure, Category="Hitmen|Abilities")
	UHitmenAbility* GetAbility(EHitmenAbilitySlot Slot) const;

	/** Owning client input. */
	void PressSlot(EHitmenAbilitySlot Slot);
	void ReleaseSlot(EHitmenAbilitySlot Slot);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_AbilityActivated(UHitmenAbility* Ability, FVector_NetQuantize Location);

protected:
	UPROPERTY(ReplicatedUsing=OnRep_Abilities)
	TObjectPtr<UHitmenAbility> MoveAbility;

	UPROPERTY(ReplicatedUsing=OnRep_Abilities)
	TObjectPtr<UHitmenAbility> ManipulationAbility;

	UFUNCTION(Server, Reliable)
	void Server_Press(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction);

	UFUNCTION(Server, Reliable)
	void Server_Release(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction);

	UFUNCTION() void OnRep_Abilities();

	void GetAim(FVector& OutOrigin, FVector& OutDirection) const;

	/** Remembers previously equipped abilities on clients to call OnUnequipped. */
	UPROPERTY(Transient)
	TArray<TObjectPtr<UHitmenAbility>> KnownAbilities;
};
