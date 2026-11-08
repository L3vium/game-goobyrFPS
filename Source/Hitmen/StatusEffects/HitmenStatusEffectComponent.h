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

class UCameraComponent;
class UMaterialInterface;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenStatusEvent, EHitmenStatusEffect, Status, AActor*, Instigator);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenStatusEffectComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenStatusEffectComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

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

	// ---- Screen effects: post-process blendables on the locally controlled owner's camera ----
	/** Distorted view while PillStatus is active. Missing asset = no effect. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice")
	TSoftObjectPtr<UMaterialInterface> PillPostProcessMaterial;

	/** White-out while Blinded (flashbang). Missing asset = no effect. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice")
	TSoftObjectPtr<UMaterialInterface> BlindPostProcessMaterial;

	/** Status that drives the pill view (the Pill ability applies Boosted). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice")
	EHitmenStatusEffect PillStatus = EHitmenStatusEffect::Boosted;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice", meta=(ClampMin="0", Units="s"))
	float PillFadeInTime = 1.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice", meta=(ClampMin="0", Units="s"))
	float PillFadeOutTime = 1.5f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice", meta=(ClampMin="0", Units="s"))
	float BlindFadeInTime = 0.05f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice", meta=(ClampMin="0", Units="s"))
	float BlindFadeOutTime = 1.5f;

	/** Timer step while a screen effect fades (no Tick). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Juice", meta=(ClampMin="0.005", Units="s"))
	float ScreenEffectUpdateInterval = 0.033f;

	UPROPERTY(ReplicatedUsing=OnRep_ActiveStatuses)
	TArray<FHitmenActiveStatus> ActiveStatuses;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInterface> LoadedPillMaterial;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInterface> LoadedBlindMaterial;

	TWeakObjectPtr<UCameraComponent> EffectCamera;
	float PillWeight = 0.f;
	float BlindWeight = 0.f;
	float LastScreenEffectUpdateTime = 0.f;
	bool bPillMaterialLoadTried = false;
	bool bBlindMaterialLoadTried = false;
	FTimerHandle ScreenEffectTimer;

	/** Previous replicated set, to raise add/remove events on clients. */
	TArray<EHitmenStatusEffect> PreviousTypes;

	FTimerHandle UpdateTimer;

	UFUNCTION() void OnRep_ActiveStatuses();

	void UpdateStatuses();
	void RaiseDiffEvents();
	float GetServerTime() const;

	/** True on the machine that renders this pawn's camera (owning client or listen host). */
	bool IsLocalViewer() const;
	/** Starts the fade timer when a screen effect weight must move toward its status. */
	void RefreshScreenEffects();
	void UpdateScreenEffects();
	void ApplyScreenEffectWeight(UMaterialInterface* Material, float Weight);
	UMaterialInterface* ResolveMaterial(const TSoftObjectPtr<UMaterialInterface>& Soft, TObjectPtr<UMaterialInterface>& Loaded, bool& bLoadTried) const;
};
