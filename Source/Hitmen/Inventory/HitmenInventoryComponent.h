// Hitmen — two weapon slots, throw rules, ammo merge on pickup, keycards

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenInventoryComponent.generated.h"

class UHitmenWeaponData;
class AHitmenWeaponBase;

USTRUCT(BlueprintType)
struct FHitmenInventorySlot
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Inventory")
	TObjectPtr<UHitmenWeaponData> Weapon = nullptr;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Inventory")
	int32 Ammo = 0;

	/** Spawned once when acquired, hidden while not in hand. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Inventory")
	TObjectPtr<AHitmenWeaponBase> Actor = nullptr;

	bool IsEmpty() const { return Weapon == nullptr; }
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenInventoryChanged);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenActiveSlotChanged, int32, NewSlot);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenInventoryComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	static constexpr int32 SlotCount = 2;

	UHitmenInventoryComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	// ---- Events ----
	/** Slots, ammo or keycards changed. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Inventory")
	FOnHitmenInventoryChanged OnInventoryChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Inventory")
	FOnHitmenActiveSlotChanged OnActiveSlotChanged;

	// ---- Queries ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	const TArray<FHitmenInventorySlot>& GetSlots() const { return Slots; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	int32 GetActiveSlot() const { return ActiveSlot; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	AHitmenWeaponBase* GetActiveWeapon() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	UHitmenWeaponData* GetActiveWeaponData() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	int32 GetActiveAmmo() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	bool HasWeaponType(const UHitmenWeaponData* Weapon) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	bool HasFreeSlot() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	const TArray<int32>& GetKeycards() const { return Keycards; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	bool HasKeycard(int32 KeycardId) const { return KeycardId < 0 || Keycards.Contains(KeycardId); }

	// ---- Server operations ----
	/**
	 * Adds a weapon: same type merges ammo, else fills a free slot; bForceReplace swaps the
	 * active slot (the old weapon is dropped). Ammo < 0 = the weapon's start ammo.
	 */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Inventory")
	bool GiveWeapon(UHitmenWeaponData* Weapon, int32 Ammo = -1, bool bForceReplace = false);

	/** Merges ammo if the same type is held. Returns true when consumed. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Inventory")
	bool TryAddAmmo(UHitmenWeaponData* Weapon, int32 Amount);

	/** Consumes ammo of the active slot. Returns false when empty. */
	bool ConsumeActiveAmmo(int32 Amount);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Inventory")
	void AddKeycard(int32 KeycardId);

	/** Removes the active weapon without dropping it (swap pickups). Returns its data/ammo. */
	bool TakeActive(UHitmenWeaponData*& OutWeapon, int32& OutAmmo);

	// ---- Client requests ----
	UFUNCTION(BlueprintCallable, Category="Hitmen|Inventory")
	void SelectSlot(int32 SlotIndex);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Inventory")
	void CycleSlot(float Direction);

	/** Throws the item in hand (F with nothing to interact, or firing empty). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Inventory")
	void ThrowActive();

	UFUNCTION(BlueprintCallable, Category="Hitmen|Inventory")
	void StartFire();

	UFUNCTION(BlueprintCallable, Category="Hitmen|Inventory")
	void StopFire();

protected:
	/** Speed of a thrown weapon. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Inventory", meta=(ClampMin="0"))
	float ThrowSpeed = 1400.f;

	UPROPERTY(ReplicatedUsing=OnRep_Slots)
	TArray<FHitmenInventorySlot> Slots;

	UPROPERTY(ReplicatedUsing=OnRep_ActiveSlot)
	int32 ActiveSlot = INDEX_NONE;

	UPROPERTY(ReplicatedUsing=OnRep_Slots)
	TArray<int32> Keycards;

	UFUNCTION(Server, Reliable)
	void Server_SelectSlot(int32 SlotIndex);

	UFUNCTION(Server, Reliable)
	void Server_ThrowActive(FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction);

	UFUNCTION() void OnRep_Slots();
	UFUNCTION() void OnRep_ActiveSlot();

	void SetActiveSlotInternal(int32 NewSlot);
	void RefreshWeaponVisibility();
	AHitmenWeaponBase* SpawnWeaponActor(UHitmenWeaponData* Weapon);
	void SpawnThrownPickup(UHitmenWeaponData* Weapon, int32 Ammo, const FVector& Origin, const FVector& Direction);
	void RemoveSlot(int32 SlotIndex, bool bDestroyActor);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnThrow(UHitmenWeaponData* Weapon);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSlotChanged(int32 NewSlot);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnAmmoPickedUp(UHitmenWeaponData* Weapon, int32 Amount);

	int32 LastActiveSlot = INDEX_NONE;
};
