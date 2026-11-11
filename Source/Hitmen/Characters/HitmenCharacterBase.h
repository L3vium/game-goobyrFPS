// Hitmen — shared base of players and NPCs: health, status, footsteps, look, death, body

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "Core/HitmenTypes.h"
#include "Core/HitmenInterfaces.h"
#include "Core/HitmenTeamLibrary.h"
#include "Interaction/HitmenInteractable.h"
#include "Data/HitmenAnimSet.h"
#include "HitmenCharacterBase.generated.h"

class UHitmenHealthComponent;
class UHitmenStatusEffectComponent;
class UHitmenFootstepComponent;
class UHitmenCosmeticComponent;
class UHitmenCharacterMovementComponent;
class UHitmenAnimSet;
class UHitmenWeaponData;
class UAnimMontage;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenBodyStateChanged, AHitmenCharacterBase*, Character);

UCLASS(Abstract)
class HITMEN_API AHitmenCharacterBase : public ACharacter, public IHitmenTeamMember, public IHitmenScannable, public IHitmenInteractable
{
	GENERATED_BODY()

public:
	AHitmenCharacterBase(const FObjectInitializer& ObjectInitializer);

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual float TakeDamage(float DamageAmount, FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser) override;
	virtual bool CanJumpInternal_Implementation() const override;
	/** Re-attaches carried weapons: the attach mesh depends on local control. */
	virtual void NotifyControllerChanged() override;

	// ---- Components ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	UHitmenHealthComponent* GetHealth() const { return HealthComponent; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	UHitmenStatusEffectComponent* GetStatusEffects() const { return StatusEffectComponent; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	UHitmenCosmeticComponent* GetCosmetics() const { return CosmeticComponent; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	UHitmenCharacterMovementComponent* GetHitmenMovement() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	UHitmenAnimSet* GetAnimSet() const { return AnimSet; }

	// ---- Montages (cosmetic) ----
	/** Local only: plays a montage on the body mesh of this machine. Returns its length (0 if not played). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Animation")
	float PlayCosmeticMontage(UAnimMontage* Montage, float PlayRate = 1.f);

	/** Server: plays an anim-set montage on every machine (unreliable multicast, cosmetic). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Animation")
	void PlayAnimSetMontage(EHitmenAnimMontage Montage, int32 Variant = 0);

	// ---- State ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	bool IsDead() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	virtual bool IsAiming() const { return false; }

	/** Combined multiplier read by the movement component (status, carrying...). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	virtual float GetMovementSpeedMultiplier() const;

	/** NPC alert state or "unaware from behind" for players; used by stealth kills. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	/** Silent takedown allowed by Attacker: default = from behind (players see what is in front of them);
	 *  NPCs: from any angle, standing or crouched, as long as they are not seeing the attacker. */
	virtual bool CanBeTakenDownBy(const AActor* Attacker) const { return IsUnawareOf(Attacker); }
	virtual bool IsUnawareOf(const AActor* Other) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Character")
	bool IsVIP() const { return bIsVIP; }

	// ---- Body ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Body")
	bool IsVanished() const { return bVanished; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Body")
	APawn* GetCarrier() const { return Carrier; }

	/** Server: lighter ability, removes the body so nobody can find it. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Body")
	void VanishBody();

	/** Server: attach this body to a carrier's back (null = drop). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Body")
	void SetCarriedBy(APawn* NewCarrier, FName AttachSocket);

	/** Bodies NPCs should react to: dead, not vanished, not carried away. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Body")
	bool IsDiscoverableBody() const;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Body")
	FOnHitmenBodyStateChanged OnBodyStateChanged;

	// IHitmenTeamMember
	virtual int32 GetHitmenTeamId_Implementation() const override { return HitmenTeams::Neutral; }

	// IHitmenInteractable (bodies can be carried)
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

	// IHitmenScannable
	virtual EHitmenScanCategory GetScanCategory_Implementation(const AActor* Observer) const override;
	virtual void SetScanHighlight_Implementation(bool bEnabled, int32 StencilValue) override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenHealthComponent> HealthComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenStatusEffectComponent> StatusEffectComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenFootstepComponent> FootstepComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenCosmeticComponent> CosmeticComponent;

	/** Every animation of this character (read by ABP_ through UHitmenAnimInstance). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Animation")
	TObjectPtr<UHitmenAnimSet> AnimSet;

	/** Bones that count as a headshot. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Damage")
	TArray<FName> HeadBones = { TEXT("head"), TEXT("neck_01"), TEXT("neck_02") };

	/** Ragdoll impulse applied on death along the last hit direction. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Damage", meta=(ClampMin="0"))
	float DeathImpulse = 6000.f;

	/** Play a death montage (anim set) before the ragdoll. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Animation")
	bool bPlayDeathMontage = true;

	/** Longest time a death montage may delay the ragdoll. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Animation", meta=(ClampMin="0", Units="s", EditCondition="bPlayDeathMontage"))
	float DeathRagdollMaxDelay = 1.5f;

	/** Hit reacts interrupt other montages (fire, mantle...). Off: only when nothing else plays. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Animation")
	bool bHitReactInterruptsMontages = false;

	UPROPERTY(Replicated)
	bool bIsVIP = false;

	UPROPERTY(ReplicatedUsing=OnRep_Body)
	bool bVanished = false;

	UPROPERTY(ReplicatedUsing=OnRep_Body)
	TObjectPtr<APawn> Carrier;

	UPROPERTY(Replicated)
	FName CarrySocket = NAME_None;

	/** Server-picked death montage (from the hit direction), replicated with the death. */
	UPROPERTY(Replicated)
	int32 DeathMontageIndex = INDEX_NONE;

	FTimerHandle DeathRagdollTimer;

	FVector LastHitDirection = FVector::ZeroVector;
	FName LastHitBone = NAME_None;

	UFUNCTION()
	virtual void HandleDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot);

	UFUNCTION()
	virtual void HandleStatusAdded(EHitmenStatusEffect Status, AActor* StatusInstigator);

	UFUNCTION()
	virtual void HandleStatusRemoved(EHitmenStatusEffect Status, AActor* StatusInstigator);

	/** Server-side reaction to damage (NPCs alert, sleepers wake). */
	virtual void OnDamaged(float Damage, AActor* InstigatorActor, bool bHeadshot);

	UFUNCTION()
	void OnRep_Body();

	void EnableRagdoll();
	void ApplyBodyState();

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_DamageFeedback(float Damage, bool bHeadshot, FVector_NetQuantize HitLocation);

	/**
	 * bOwnerPredicted: the owning client and the server already played it (movement events run
	 * on both), so only the other machines play it.
	 */
	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_PlayAnimSetMontage(EHitmenAnimMontage Montage, uint8 Variant, bool bOwnerPredicted);

	/** Plays an anim-set montage on this machine. Returns its length (0 if not played). */
	float PlayAnimSetMontageLocal(EHitmenAnimMontage Montage, int32 Variant = 0);

	/**
	 * Meshes that play cosmetic montages on this machine (body mesh by default). The player can
	 * add its first-person mesh when locally controlled.
	 */
	virtual void GetMontageMeshes(TArray<USkeletalMeshComponent*>& OutMeshes) const;

	/** CMC traversal event (owning client + server): mantle / vault montage. */
	UFUNCTION()
	void HandleMantleStarted(bool bIsVault);

	/** CMC traversal event (owning client + server): climb enter / ladder top exit / zipline. */
	UFUNCTION()
	void HandleTraversalMontage(EHitmenAnimMontage Montage);

	/** Inventory throw event (every machine): throw montage. */
	UFUNCTION()
	void HandleWeaponThrown(UHitmenWeaponData* Weapon);

	/** Server: death montage index from the last hit direction. */
	int32 ChooseDeathMontageIndex() const;

	/** Plays the death montage if any; returns the delay before the ragdoll (0 = ragdoll now). */
	float PlayDeathMontage();

	void HandleDeathRagdollTimer();

	// ---- Juice hooks (cosmetic only, every machine) ----
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDamaged(float Damage, bool bHeadshot, FVector HitLocation);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDeath(bool bHeadshot);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnStatusAdded(EHitmenStatusEffect Status);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnStatusRemoved(EHitmenStatusEffect Status);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnBodyStateChanged(bool bVanishedNow, bool bCarriedNow);
};
