// Hitmen — holds the move (E) and manipulation (A) abilities and routes input/RPCs to them

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Core/HitmenTypes.h"
#include "HitmenAbilityComponent.generated.h"

class UMaterialInterface;

class UHitmenAbility;
class UHitmenAbilityData;

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenAbilitiesChanged);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenAbilityFailed, UHitmenAbility*, Ability, const FText&, Reason);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenAbilityComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenAbilityComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Abilities")
	FOnHitmenAbilitiesChanged OnAbilitiesChanged;

	/** Owning client: a use was refused by the server, with the reason to show. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Abilities")
	FOnHitmenAbilityFailed OnAbilityFailed;

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

	/** Server -> owning client: the ability refused to activate (no charge spent). */
	UFUNCTION(Client, Reliable)
	void Client_AbilityFailed(UHitmenAbility* Ability, const FText& Reason);

protected:
	/** Also show failure reasons in the centre prompt (until the UI has a dedicated toast). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities")
	bool bShowFailureAsPrompt = true;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities", meta=(ClampMin="0.1", Units="s"))
	float FailurePromptDuration = 1.5f;

	/** Glow on remote-ability targets under the crosshair (overlay material). Empty = no glow. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities")
	TObjectPtr<UMaterialInterface> HintOverlayMaterial;

	/** Seconds between two target scans for the hint (local player only). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities", meta=(ClampMin="0.02", Units="s"))
	float HintScanInterval = 0.1f;

	/** Local: highlights the target the manipulation (then move) ability would act on. */
	void UpdateTargetHint();
	void ClearTargetHint();

	FTimerHandle HintTimer;
	TWeakObjectPtr<AActor> HintedActor;

	FTimerHandle FailurePromptTimer;

	void ClearFailurePrompt();

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
