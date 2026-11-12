// Hitmen — two weapon slots, throw rules, ammo merge on pickup, keycards. Weapon actors are
// spawned once and move between hand and world (thrown / swapped / picked up), never respawned.

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

	/** Spawned once (loadout) or taken from the world; hidden while not in hand, dropped when thrown. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Inventory")
	TObjectPtr<AHitmenWeaponBase> Actor = nullptr;

	bool IsEmpty() const { return Weapon == nullptr; }
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenInventoryChanged);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenActiveSlotChanged, int32, NewSlot);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenWeaponThrown, UHitmenWeaponData*, Weapon);

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

	/** The item in hand was thrown (every machine; cosmetic: throw montage, juice). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Inventory")
	FOnHitmenWeaponThrown OnWeaponThrown;

	// ---- Queries ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Inventory")
	const TArray<FHitmenInventorySlot>& GetSlots() const { return Slots; }

	/** Server: picking up a body. Both slots full: the held weapon is dropped. The remaining weapon goes to the
	 *  right slot and nothing is in hand (the body is in the arms). */
	void MakeRoomForBody();

	/** Server: the body was put down; the weapon you still carry comes to hand. */
	void OnBodyDropped();

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
	 * Adds a new weapon (loadout, debug): same type merges ammo, else spawns its actor once into a
	 * free slot; bForceReplace swaps the active slot (the old weapon actor is dropped).
	 * Ammo < 0 = the weapon's start ammo.
	 */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Inventory")
	bool GiveWeapon(UHitmenWeaponData* Weapon, int32 Ammo = -1, bool bForceReplace = false);

	/**
	 * Takes a weapon lying in the world (the same actor goes to the hand). Same type: its ammo is
	 * merged and the world actor destroyed. Full: with bAllowSwap the held weapon is dropped where
	 * the picked one was. Returns true when the world weapon was consumed.
	 */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Inventory")
	bool PickUpWeapon(AHitmenWeaponBase* WorldWeapon, bool bAllowSwap = true);

	/** Merges ammo if the same type is held. Returns true when consumed. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Inventory")
	bool TryAddAmmo(UHitmenWeaponData* Weapon, int32 Amount);

	/** Consumes ammo of the active slot. Returns false when empty. */
	bool ConsumeActiveAmmo(int32 Amount);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Inventory")
	void AddKeycard(int32 KeycardId);

	/** Re-attaches carried weapon actors (e.g. after possession: first-person vs body mesh). Any machine. */
	void RefreshWeaponAttachments();

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

	/** Distance in front of the eyes where a thrown weapon appears. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Inventory", meta=(ClampMin="0", Units="cm"))
	float ThrowSpawnDistance = 60.f;

	/** Speed of the old weapon dropped by a forced replace (debug give). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Inventory", meta=(ClampMin="0"))
	float ReplaceDropSpeed = 300.f;

	/** Server tolerance on the client throw origin. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Inventory", meta=(ClampMin="0", Units="cm"))
	float ThrowOriginTolerance = 300.f;

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

	/** Cosmetic: throw montage / juice on every machine. */
	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Thrown(UHitmenWeaponData* Weapon);

	UFUNCTION() void OnRep_Slots();
	UFUNCTION() void OnRep_ActiveSlot();

	void SetActiveSlotInternal(int32 NewSlot);
	void RefreshWeaponVisibility();
	AHitmenWeaponBase* SpawnWeaponActor(UHitmenWeaponData* Weapon);
	/** Server: removes the slot and puts its actor in the world (thrown when Thrower is set). */
	void DropSlot(int32 SlotIndex, const FVector& Location, const FRotator& Rotation, const FVector& Velocity, APawn* Thrower);
	void RemoveSlot(int32 SlotIndex, bool bDestroyActor);
	void FillSlot(int32 SlotIndex, UHitmenWeaponData* Weapon, int32 Ammo, AHitmenWeaponBase* Actor);

	/** Every machine. */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnThrow(UHitmenWeaponData* Weapon);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSlotChanged(int32 NewSlot);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnAmmoPickedUp(UHitmenWeaponData* Weapon, int32 Amount);

	int32 LastActiveSlot = INDEX_NONE;
};
