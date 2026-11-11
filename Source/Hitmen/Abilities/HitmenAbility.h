// Hitmen — ability base: replicated sub-object with charges, cooldown and per-class tunables

#pragma once

#include "CoreMinimal.h"
#include "UObject/Object.h"
#include "Core/HitmenTypes.h"
#include "HitmenAbility.generated.h"

class UHitmenAbilityComponent;
class UHitmenAbilityData;
class AHitmenCharacterBase;

/**
 * Families: UHitmenMoveAbility (E) and UHitmenManipulationAbility (A) fix the slot; every concrete
 * ability derives from one of them.
 * Gameplay values are EditDefaultsOnly on each class and set in the BP_Ability_* child.
 * Server: Activate / Release / TickAbility. Owning client: LocalPress / LocalRelease / LocalTick
 * for responsive movement abilities (both sides apply the same motion).
 */
UCLASS(Abstract, Blueprintable, BlueprintType)
class HITMEN_API UHitmenAbility : public UObject
{
	GENERATED_BODY()

public:
	virtual bool IsSupportedForNetworking() const override { return true; }
	virtual UWorld* GetWorld() const override;
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	void InitAbility(UHitmenAbilityComponent* InComponent, UHitmenAbilityData* InData);

	/** Clients: bind the replicated copy to its component without touching replicated state. */
	void SetOwnerComponent(UHitmenAbilityComponent* InComponent) { OwnerComponent = InComponent; }

	// ---- Server flow ----
	void ServerPress(const FVector& AimOrigin, const FVector& AimDirection);
	void ServerRelease(const FVector& AimOrigin, const FVector& AimDirection);
	virtual void TickAbility(float DeltaTime) {}

	// ---- Owning client flow ----
	virtual void LocalPress(const FVector& AimOrigin, const FVector& AimDirection) {}

	/**
	 * Local hint: what this ability would act on from this aim (it glows at range and the action
	 * shows under it). Null = no hint (throwables, self abilities).
	 */
	virtual AActor* FindHintTarget(const FVector& Origin, const FVector& Direction, FText& OutAction) const { return nullptr; }
	virtual void LocalRelease() {}
	virtual void LocalTick(float DeltaTime) {}

	/** Equip / unequip side effects (double jump count, passive armor...). Server + clients. */
	virtual void OnEquipped() {}
	virtual void OnUnequipped() {}

	/** Abilities that need TickAbility / LocalTick while active. */
	virtual bool WantsTick() const { return false; }

	/** Local player, ability ready: a point to mark in the world from this aim (where a hook will land). */
	virtual bool GetAimMarker(const FVector& Origin, const FVector& Direction, FVector& OutPoint) const { return false; }

	/** Every machine: a cable is drawn from the owner's hand to OutAnchor while this returns true (hook pull). */
	virtual bool GetCableAnchor(FVector& OutAnchor) const { return false; }

	// ---- Queries ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	virtual bool CanActivate() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	float GetCooldownRemaining() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	float GetCooldownNormalized() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	int32 GetCharges() const { return Charges; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	int32 GetMaxCharges() const { return MaxCharges; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	UHitmenAbilityData* GetData() const { return Data; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	AHitmenCharacterBase* GetOwnerCharacter() const;

	/** Slot of the family (set by UHitmenMoveAbility / UHitmenManipulationAbility). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	EHitmenAbilitySlot GetSlot() const { return Slot; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Ability")
	APawn* GetOwnerPawn() const;

	/** Activation is used on release instead of press (aim then confirm, e.g. Blink). */
	bool ActivatesOnRelease() const { return bActivateOnRelease; }

	// ---- Juice (every machine, via the component multicast) ----
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnActivated(FVector Location);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEnded();

	/** Local only: aim preview while holding (blink target, hook point). */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnAimPreview(bool bVisible, FVector Location);

	/** Owning client only: the use was refused with a reason ("Room not sealed"). */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnFailed(const FText& Reason);

protected:
	/** Fixed by the family base class; the data asset's slot must match it. */
	UPROPERTY(VisibleDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability|Common")
	EHitmenAbilitySlot Slot = EHitmenAbilitySlot::Move;

	/** Seconds between uses. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability|Common", meta=(ClampMin="0"))
	float Cooldown = 8.f;

	/** Uses available (0 = unlimited, gated only by cooldown). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability|Common", meta=(ClampMin="0"))
	int32 MaxCharges = 0;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability|Common", meta=(ClampMin="0", Units="cm"))
	float Range = 1500.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability|Common", meta=(ClampMin="0", Units="s"))
	float Duration = 8.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Ability|Common")
	bool bActivateOnRelease = false;

	UPROPERTY(Replicated)
	int32 Charges = 0;

	UPROPERTY(Replicated)
	float CooldownEndTime = 0.f;

	UPROPERTY()
	TObjectPtr<UHitmenAbilityComponent> OwnerComponent;

	UPROPERTY(Replicated)
	TObjectPtr<UHitmenAbilityData> Data;

	/** Server: do the effect. Return true to consume a charge / start the cooldown. */
	virtual bool Activate(const FVector& AimOrigin, const FVector& AimDirection) { return false; }
	virtual void Release(const FVector& AimOrigin, const FVector& AimDirection) {}

	void CommitUse();
	float GetServerTime() const;

	/** Server: tells the owning player why the use was refused (no charge is spent). */
	void NotifyFailed(const FText& Reason) const;

	// ---- Helpers ----
	bool AimTrace(const FVector& Origin, const FVector& Direction, FHitResult& OutHit, float TraceRadius = 0.f) const;
	/** First living (or dead if bAllowDead) character under the crosshair. */
	AHitmenCharacterBase* AimCharacter(const FVector& Origin, const FVector& Direction, bool bAllowDead = false) const;
	AActor* SpawnOwnedActor(TSubclassOf<AActor> Class, const FTransform& Transform) const;
	void PlayActivatedJuice(const FVector& Location) const;
};
