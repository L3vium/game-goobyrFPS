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

	void SetIsVIP(bool bVIP) { bIsVIP = bVIP; bAlwaysRelevant = bVIP || bAlwaysRelevant; }

	/** Server: a VIP candidate that was not picked becomes a plain guard (armed, named "Guard"). */
	void BecomeGuard();

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	bool IsGuest() const { return bGuest; }

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

	/** Next patrol index (owned by the NPC so routes can be shared); open routes go back and forth. */
	int32 AdvancePatrolIndex(int32 PointCount, bool bClosedLoop = true);
	int32 GetPatrolIndex() const { return PatrolIndex; }
	/** Re-targeting the point an interrupted walk was heading to (gives up after a few tries: unreachable point). */
	bool TryResumePatrol() { return ++PatrolResumeCount <= MaxPatrolResumes; }
	void ResetPatrolResume() { PatrolResumeCount = 0; }

	// ---- Cover (server) ----
	void SetCoverLocation(const FVector& Location) { CoverLocation = Location; bHasCover = true; }
	void ClearCover() { bHasCover = false; }
	bool HasCover() const { return bHasCover; }
	FVector GetCoverLocation() const { return CoverLocation; }
	/** Standing at its cover point: crouches behind it between bursts. */
	bool IsInCover() const;

	/** VIP evacuation: when its escorts were last called (server). */
	float LastEscortCallTime = -1000.f;
	/** Crouched in cover it still watches over it (it peeks between bursts). */
	virtual FVector GetPawnViewLocation() const override;

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
	/** "Guard  -  Shotgun  -  Keycard 2": what you see when you look at it up close. */
	virtual FText GetInteractionInfo_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

	/** Alive, not looking at the thief, something on a hip (keycard, holstered pistol), the thief behind it (crouched:
	 *  beside it is enough). */
	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	bool CanBePickpocketedBy(const AActor* Thief) const;

	/** What F would take from its hips now (the hip nearest where the thief looks when both carry something). */
	EHitmenPickpocketItem GetPickpocketItem(const AActor* Thief) const;

	virtual bool GetHolsterAttachment(FName& OutBone, FTransform& OutRelative) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|NPC")
	int32 GetCarriedKeycard() const { return CarriedKeycard; }

	void SetGuardGroup(class AHitmenGuardGroup* Group);

	/** Server: walks Route (from the route's AssignedNPCs); makes it a patroller. */
	void AssignPatrolRoute(AHitmenPatrolRoute* Route);

	/** Calm walking pace multiplier (a group leader waiting for its followers). */
	void SetPaceMultiplier(float Multiplier);
	class AHitmenGuardGroup* GetGuardGroup() const;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenAlertComponent> AlertComponent;

	/** Overhead alert indicator (WBP_AlertIndicator). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UWidgetComponent> AlertWidget;

	/** This NPC may be picked as the VIP at match start (max 15 per map). The candidates not picked become guards. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	bool bVIPCandidate = false;

	/** A VIP candidate that is not picked becomes a guard with this weapon (none = it keeps DefaultWeapon). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	TObjectPtr<UHitmenWeaponData> CandidateGuardWeapon;

	/** A guest: neutral (never hostile, never attacked by guards), unarmed, runs from danger. Off in every map: the
	 *  map is a hostile area. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Behavior")
	bool bGuest = false;

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

	/** Width of the aim line per cm of distance from your eye (0.0016 = about 2 px at 1080p, 90 degrees): it reads at
	 *  any distance. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Sniper", meta=(ClampMin="0"))
	float AimLaserScreenWidth = 0.0016f;

	/** A small red star at a sniper's scope, the same size on screen at any distance (seen only in line of sight). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UMaterialBillboardComponent> ScopeGlint;

	/** Glint sprite material (scalar "Aiming": 0 calm pulse, 1 bright fast pulse while aiming). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Sniper")
	TObjectPtr<UMaterialInterface> GlintMaterial;

	/** Glint size as a fraction of the screen. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Sniper", meta=(ClampMin="0"))
	float GlintScreenSize = 0.03f;

	/** The red star only shows while the sniper aims at someone it sees (off: always, pulsing slowly when calm). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Sniper")
	bool bGlintOnlyWhileAiming = true;

	/** Server: the sniper aims at a target it sees (glint and red line on every machine). */
	UPROPERTY(Replicated)
	bool bSniperAiming = false;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> GlintInstance;

	/** Name shown when you look at it (VIP candidates are "Guest", whichever one is the VIP). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	FText DisplayName;

	/** Where the keycard sits on the right hip, in the NPC's own space (X forward, Y right, Z up from the capsule
	 *  centre); on the left hip Y is mirrored. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Hips")
	FVector KeycardHipOffset = FVector(4.f, 21.f, 2.f);

	/** Pickpocketing also needs the thief to be crouched. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	bool bPickpocketRequiresCrouch = false;

	/**
	 * Three places to carry things: the hands, the left hip and the right hip. Holstered (default): a pistol rides in
	 * the hip holster (HolsterSide) while the guard is calm, is drawn when it is alerted (yellow threat, search, fight,
	 * on edge) and put back when it calms down; from behind it can be pickpocketed. InHands: always in the hands (never
	 * pickpocketed). Long guns (weapon data bCanHolster off) are always in the hands.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Hips")
	EHitmenNPCWeaponCarry WeaponCarry = EHitmenNPCWeaponCarry::Holstered;

	/** Hip of the holster; a keycard rides on the other hip. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC|Hips")
	EHitmenHipSlot HolsterSide = EHitmenHipSlot::Right;

	/** The holstered weapon on the right hip, in the NPC's own space (X forward, Y right, Z up from the capsule centre).
	 *  Weapon actors point their barrel along their +Y (the pistol's VisualTransform): this rotation hangs it barrel
	 *  down, the slide toward the front, the grip up and back. The left hip mirrors it. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Hips")
	FTransform HolsterTransform = FTransform(FRotator(0.f, -90.f, 80.f), FVector(-3.f, 24.f, 4.f));

	/** Crouched thieves may pickpocket from beside the NPC too (dot of its forward and the way to the thief). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC", meta=(ClampMin="-1", ClampMax="1"))
	float CrouchedPickpocketMaxDot = 0.3f;

	/** Pickpocketing reach from the NPC's centre. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC", meta=(ClampMin="0", Units="cm"))
	float PickpocketReach = 220.f;

	/** Hip of the keycard: the other side of the holster. */
	EHitmenHipSlot GetKeycardSide() const { return HolsterSide == EHitmenHipSlot::Right ? EHitmenHipSlot::Left : EHitmenHipSlot::Right; }

	/** A transform in the NPC's own space (actor space) relative to the hip bone of the reference pose (the same on
	 *  every machine, whatever the animation does when it is computed). */
	FTransform ActorSpaceToHipBone(const FTransform& ActorSpace) const;

	/** Server: holster or draw the pistol from the alert state. */
	void RefreshWeaponCarry();

	/** Carries bodies to a container after a body search (the VIP's body never). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UHitmenBodyCarryComponent> BodyCarryComponent;

	UFUNCTION()
	void HandleCarryChanged(AHitmenCharacterBase* Body);

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

	/** Back to routine but on edge after an alert: brisker than the calm stroll. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Movement", meta=(ClampMin="0", Units="cm/s"))
	float OnEdgeWalkSpeed = 240.f;

	/** Without a gun the NPC fights only this close (it used to swing punches from its shooting range). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Combat", meta=(ClampMin="0", Units="cm"))
	float UnarmedReach = 160.f;

	/** Counts as in cover this close to its cover point. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Combat", meta=(ClampMin="0", Units="cm"))
	float CoverArrivalRadius = 90.f;

	/** In cover, stands up this long before the next burst (the crouch pose blends back first). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Combat", meta=(ClampMin="0", Units="s"))
	float CoverRiseLead = 0.35f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|NPC|Patrol", meta=(ClampMin="0"))
	int32 MaxPatrolResumes = 2;

	/** Walk speed from the alert state (and on edge). */
	void RefreshWalkSpeed();
	float PaceMultiplier = 1.f;

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

	/** Server: spawns DefaultWeapon (or GuardWeapon) in the hands or the holster. */
	void SpawnWeapon(UHitmenWeaponData* Data);

	int32 PatrolIndex = INDEX_NONE;
	int32 PatrolDirection = 1;
	int32 PatrolResumeCount = 0;
	FVector CoverLocation = FVector::ZeroVector;
	bool bHasCover = false;
	bool bInBurstPause = false;
	bool bWasOnEdge = false;

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
