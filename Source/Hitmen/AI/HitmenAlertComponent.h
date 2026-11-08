// Hitmen — NPC awareness state machine (unaware -> white -> yellow -> red -> search), replicated
// for overhead indicators, minimap and HUD.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Core/HitmenTypes.h"
#include "HitmenAlertComponent.generated.h"

class AHitmenCharacterBase;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenAlertStateChanged, EHitmenAlertState, OldState, EHitmenAlertState, NewState);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenCallingChanged, bool, bCalling);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenAlertComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenAlertComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Alert")
	FOnHitmenAlertStateChanged OnAlertStateChanged;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Alert")
	FOnHitmenCallingChanged OnCallingChanged;

	// ---- Queries (all machines) ----
	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	EHitmenAlertState GetAlertState() const { return AlertState; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	EHitmenAlertColor GetAlertColor() const { return HitmenAlert::ToColor(AlertState); }

	/** 0..1 fill of the white indicator before turning red. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	float GetSuspicion() const { return Suspicion; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	bool IsCallingReinforcements() const { return bCallingReinforcements; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	AActor* GetTarget() const { return Target; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	FVector GetLastKnownLocation() const { return LastKnownLocation; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	FVector GetInvestigateLocation() const { return InvestigateLocation; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	bool IsTargetVisible() const { return bTargetVisible; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Alert")
	float GetLastDamageTime() const { return LastDamageTime; }

	// ---- Stimuli (server) ----
	/** A hostile is visible this frame (called repeatedly while seen). */
	void SeeHostile(AActor* Seen, float DeltaTime, bool bAlarming);
	void LoseSightOf(AActor* Seen);
	void HearNoise(const FVector& Location, EHitmenNoiseType Type, AActor* NoiseInstigator, float Distance);
	void SeeBody(AHitmenCharacterBase* Body);
	void WitnessKill(AActor* Killer);
	void OnDamagedBy(AActor* DamageInstigator);
	void ReceiveReinforcementCall(const FVector& Location, AActor* CallTarget);

	/** Forces a state (BT tasks, charm, debug). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Alert")
	void SetAlertState(EHitmenAlertState NewState);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Alert")
	void SetCallingReinforcements(bool bCalling);

	/** Off = deaf: noises, distractions, radio calls and bodies are ignored (sight and damage only). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Alert")
	void SetReactsToNoises(bool bReacts) { bReactsToNoises = bReacts; }

	/** Puts the NPC into combat with a given target (charm, debug). */
	void EngageTarget(AActor* NewTarget);

	/** Stops reacting (death, sleep). */
	void SetSuppressed(bool bSuppressed);

protected:
	// ---- Tuning ----
	/** Suspicion per second at point-blank range; scales down with distance. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0"))
	float SuspicionRiseRate = 1.2f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0"))
	float SuspicionDecayRate = 0.25f;

	/** Seeing a hostile closer than this goes straight to combat. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0", Units="cm"))
	float InstantDetectionDistance = 350.f;

	/** Distance at which the rise rate reaches its minimum. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0", Units="cm"))
	float SuspicionFalloffDistance = 2500.f;

	/** Multiplier when the hostile is doing something alarming (armed, sprinting, in a restricted act). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="1"))
	float AlarmingMultiplier = 2.5f;

	// ---- Replicated state ----
	UPROPERTY(ReplicatedUsing=OnRep_AlertState)
	EHitmenAlertState AlertState = EHitmenAlertState::Unaware;

	UPROPERTY(Replicated)
	float Suspicion = 0.f;

	UPROPERTY(ReplicatedUsing=OnRep_Calling)
	bool bCallingReinforcements = false;

	UPROPERTY(Replicated)
	TObjectPtr<AActor> Target;

	FVector LastKnownLocation = FVector::ZeroVector;
	FVector InvestigateLocation = FVector::ZeroVector;
	bool bTargetVisible = false;
	bool bSuppressed = false;
	bool bReactsToNoises = true;
	float LastSeenTime = -100.f;
	float StateEnterTime = 0.f;
	float LastDamageTime = -100.f;
	TSet<TWeakObjectPtr<AHitmenCharacterBase>> KnownBodies;
	EHitmenAlertState PreviousRepState = EHitmenAlertState::Unaware;

	UFUNCTION() void OnRep_AlertState();
	UFUNCTION() void OnRep_Calling();

	void SetInvestigate(const FVector& Location);
	float GetRuleLoseTime() const;
	float GetRuleSearchTime() const;
	float GetRuleInvestigateTime() const;
	float GetDifficultySuspicionMultiplier() const;
	void UpdateLastKnownMarker(bool bShow) const;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState);
};
