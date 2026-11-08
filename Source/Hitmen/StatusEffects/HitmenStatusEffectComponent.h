// Hitmen — status effects shared by NPCs and players (stun, sleep, charm, trap, poison...)

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Core/HitmenTypes.h"
#include "HitmenStatusEffectComponent.generated.h"

USTRUCT(BlueprintType)
struct FHitmenActiveStatus
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Status")
	EHitmenStatusEffect Type = EHitmenStatusEffect::None;

	/** Server world time when it ends; < 0 = until removed. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Status")
	float EndTime = -1.f;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Status")
	float Magnitude = 1.f;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Status")
	TObjectPtr<AActor> Instigator = nullptr;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenStatusEvent, EHitmenStatusEffect, Status, AActor*, Instigator);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenStatusEffectComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenStatusEffectComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Status")
	FOnHitmenStatusEvent OnStatusAdded;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Status")
	FOnHitmenStatusEvent OnStatusRemoved;

	/** Server. Duration <= 0 lasts until RemoveStatus. Re-applying refreshes to the longest end. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Status")
	void ApplyStatus(EHitmenStatusEffect Type, float Duration, float Magnitude = 1.f, AActor* Instigator = nullptr);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Status")
	void RemoveStatus(EHitmenStatusEffect Type);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Status")
	void ClearAll();

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	bool HasStatus(EHitmenStatusEffect Type) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	float GetMagnitude(EHitmenStatusEffect Type) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	AActor* GetStatusInstigator(EHitmenStatusEffect Type) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	const TArray<FHitmenActiveStatus>& GetActiveStatuses() const { return ActiveStatuses; }

	// ---- Derived queries ----
	/** Stunned, asleep: cannot act at all. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	bool IsIncapacitated() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	bool CanMove() const;

	/** Hidden from NPC sight (invisible, in a locker, crouched in tall grass). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	bool IsHiddenFromSight() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	float GetSpeedMultiplier() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	float GetOutgoingDamageMultiplier() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Status")
	float GetIncomingDamageMultiplier() const;

protected:
	/** Speed bonus per magnitude while Boosted. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Status|Boost", meta=(ClampMin="0"))
	float BoostSpeedBonus = 0.35f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Status|Boost", meta=(ClampMin="0"))
	float BoostDamageBonus = 0.5f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Status|Boost", meta=(ClampMin="0", ClampMax="1"))
	float BoostResistance = 0.3f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Status|Armor", meta=(ClampMin="0", ClampMax="1"))
	float ArmoredResistance = 0.4f;

	/** Poison damage per second per magnitude. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Status|Poison", meta=(ClampMin="0"))
	float PoisonDamagePerSecond = 20.f;

	UPROPERTY(ReplicatedUsing=OnRep_ActiveStatuses)
	TArray<FHitmenActiveStatus> ActiveStatuses;

	/** Previous replicated set, to raise add/remove events on clients. */
	TArray<EHitmenStatusEffect> PreviousTypes;

	FTimerHandle UpdateTimer;

	UFUNCTION() void OnRep_ActiveStatuses();

	void UpdateStatuses();
	void RaiseDiffEvents();
	float GetServerTime() const;
};
