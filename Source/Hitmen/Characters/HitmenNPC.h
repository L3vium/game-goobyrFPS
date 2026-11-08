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

	/** Server: shoot toward a point (aim error from difficulty). */
	void FireAt(const FVector& Location);

	virtual int32 GetHitmenTeamId_Implementation() const override;
	virtual bool IsUnawareOf(const AActor* Other) const override;

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

	/** Keycard dropped on death (-1 = none). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|NPC")
	int32 CarriedKeycard = INDEX_NONE;

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
};
