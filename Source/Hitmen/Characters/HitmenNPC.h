// Hitmen — NPC: hostile guard / civilian, optional VIP candidate, carries a weapon and keycard

#pragma once

#include "CoreMinimal.h"
#include "Characters/HitmenCharacterBase.h"
#include "HitmenNPC.generated.h"

class UHitmenAlertComponent;
class UWidgetComponent;
class UHitmenWeaponData;
class AHitmenWeaponBase;
class AHitmenPatrolRoute;
class AHitmenKeycardPickup;

UCLASS(Blueprintable)
class HITMEN_API AHitmenNPC : public AHitmenCharacterBase
{
	GENERATED_BODY()

public:
	AHitmenNPC(const FObjectInitializer& ObjectInitializer);

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	UHitmenAlertComponent* GetAlert() const { return AlertComponent; }

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	bool IsVIPCandidate() const { return bVIPCandidate; }

	void SetIsVIP(bool bVIP) { bIsVIP = bVIP; }

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	bool IsSniper() const { return bIsSniper; }

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	AHitmenWeaponBase* GetWeapon() const { return Weapon; }

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	AHitmenPatrolRoute* GetPatrolRoute() const { return PatrolRoute; }

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	EHitmenNPCBehavior GetBehavior() const { return Behavior; }

	/** Stationary NPC that never leaves its post, even in combat or while searching. */
	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	bool HoldsPosition() const { return Behavior == EHitmenNPCBehavior::Stationary && bHoldPosition; }

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	bool CanHearNoises() const { return bReactsToNoises; }

	/** Where a stationary NPC stands guard and which way it faces (its placement). */
	FVector GetPostLocation() const { return PostLocation; }
	FRotator GetPostRotation() const { return PostRotation; }

	virtual void PostInitializeComponents() override;

	/** Next patrol index (owned by the NPC so routes can be shared). */
	int32 AdvancePatrolIndex(int32 PointCount);

	/** Server: shoot toward a point (aim error from difficulty), paced by the match rules: a raise delay,
	 *  short bursts at NPCFireIntervalMultiplier x the weapon interval, pauses between bursts. */
	void FireAt(const FVector& Location);

	virtual int32 GetHitmenTeamId_Implementation() const override;
	virtual bool IsUnawareOf(const AActor* Other) const override;
	virtual bool CanBeTakenDownBy(const AActor* Attacker) const override;
	virtual void Tick(float DeltaSeconds) override;

	/** NPCs in combat raise their gun (weapon AimMontage) while standing or walking slowly. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Combat", meta=(ClampMin="0", Units="cm/s"))
	float AimPoseMaxSpeed = 140.f;

	/** Searching guards also hold their gun ready while they stand and look around (Hitman-like). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Combat")
	bool bReadyGunWhileSearching = true;

	/** Guards only shoot within this distance; farther away they walk closer until in range (snipers ignore it). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Combat", meta=(ClampMin="0", Units="cm"))
	float ShootingRange = 1500.f;

	/** In combat the guard turns toward its target (strafing) at this rate instead of facing where it walks. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Combat", meta=(ClampMin="0", Units="deg"))
	float CombatTurnRate = 600.f;

	/** Shooting range, or no limit for snipers. */
	float GetEffectiveShootingRange() const;

	/** Every machine: the gun is drawn (weapon EquipMontage played when it became ready). */
	bool bGunReady = false;

	// F: pickpocket the keycard from behind (alive, unaware); otherwise carry the body.
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

	/** Alive, unaware of the player, carrying a keycard, player behind it. */
	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	bool CanBePickpocketedBy(const AActor* Thief) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	int32 GetCarriedKeycard() const { return CarriedKeycard; }

	void SetGuardGroup(class AHitmenGuardGroup* Group);
	class AHitmenGuardGroup* GetGuardGroup() const;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenAlertComponent> AlertComponent;

	/** Overhead alert indicator (WBP_AlertIndicator). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UWidgetComponent> AlertWidget;

	/** This NPC may be picked as the VIP at match start (max 15 per map). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	bool bVIPCandidate = false;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	TObjectPtr<UHitmenWeaponData> DefaultWeapon;

	/** Patrol walks PatrolRoute; Stationary stands at its placement, facing its placement direction. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Behavior")
	EHitmenNPCBehavior Behavior = EHitmenNPCBehavior::Patrol;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Behavior", meta=(EditCondition="Behavior==EHitmenNPCBehavior::Patrol", EditConditionHides))
	TObjectPtr<AHitmenPatrolRoute> PatrolRoute;

	/** Off = only reacts to what it sees and to being hurt: ignores footsteps, gunshots,
	 *  distractions and radio calls. Bodies are still investigated, except by sentries
	 *  (Stationary + Hold Position + this off). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Behavior")
	bool bReactsToNoises = true;

	/** Never leaves its post: shoots what it sees, turns toward attackers, searches by looking
	 *  around. Combined with React To Noises off, this is a sentry. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Behavior", meta=(EditCondition="Behavior==EHitmenNPCBehavior::Stationary", EditConditionHides))
	bool bHoldPosition = false;

	FVector PostLocation = FVector::ZeroVector;
	FRotator PostRotation = FRotator::ZeroRotator;

	/** Snipers keep their distance instead of closing in. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	bool bIsSniper = false;

	/** Keycard carried on the hip (-1 = none): pickpocketed from behind or dropped on death. */
	UPROPERTY(EditAnywhere, ReplicatedUsing=OnRep_CarriedKeycard, BlueprintReadOnly, Category="Hitmen|NPC")
	int32 CarriedKeycard = INDEX_NONE;

	/** The keycard visible on the hip while carried. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> KeycardMesh;

	/** Bone / socket the keycard hangs from and its offset. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC")
	FName KeycardBone = TEXT("pelvis");

	/** Red aiming line from the weapon to the target while a sniper aims. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> AimLaser;

	/** Pickpocket only works from behind (dot of the NPC forward and the direction to the thief). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC", meta=(ClampMin="-1", ClampMax="1"))
	float PickpocketMaxDot = -0.2f;

	UFUNCTION()
	void OnRep_CarriedKeycard();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnPickpocketed(AActor* Thief);

	TWeakObjectPtr<class AHitmenGuardGroup> GuardGroup;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC")
	TSubclassOf<AHitmenKeycardPickup> KeycardPickupClass;

	/** Walk speed per awareness: patrols stroll, searches walk briskly, combat runs. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Movement", meta=(ClampMin="0", Units="cm/s"))
	float CalmWalkSpeed = 170.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Movement", meta=(ClampMin="0", Units="cm/s"))
	float AlertWalkSpeed = 320.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Movement", meta=(ClampMin="0", Units="cm/s"))
	float CombatWalkSpeed = 480.f;

	UFUNCTION()
	void HandleAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState);

	UFUNCTION()
	void HandleAlertCallingChanged(bool bCalling);

	UFUNCTION()
	void HandleAlertSuspicionChanged(float Suspicion);

	/** Pushes colour, white fill and call icon to the overhead widget (no reference held by the widget). */
	void PushAlertView();

	/** Ammo left in the weapon dropped on death. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC", meta=(ClampMin="0"))
	int32 DroppedWeaponAmmo = 10;

	UPROPERTY(Replicated, BlueprintReadOnly, Category="Hitmen|NPC")
	TObjectPtr<AHitmenWeaponBase> Weapon;

	int32 PatrolIndex = INDEX_NONE;

	virtual void OnDamaged(float Damage, AActor* InstigatorActor, bool bHeadshot) override;
	virtual void HandleDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot) override;
	virtual void HandleStatusAdded(EHitmenStatusEffect Status, AActor* StatusInstigator) override;
	virtual void HandleStatusRemoved(EHitmenStatusEffect Status, AActor* StatusInstigator) override;

	void RefreshBrainPause();

	/** Asleep (sleep dart): the body goes limp (ragdoll) with "Z z z" over the head, then stands up. */
	void SetSleepRagdoll(bool bAsleep);
	bool bSleepRagdoll = false;
	FTransform SleepMeshRelative;

private:
	/** Cosmetic: keeps the weapon's aim montage looping while fighting. */
	void UpdateAimPose();

	UPROPERTY(Transient)
	TObjectPtr<UAnimMontage> ActiveAimMontage;

	// Fire pacing (FireAt).
	float NextShotTime = 0.f;
	float LastShotRequestTime = -100.f;
	int32 ShotsLeftInBurst = 0;
};
