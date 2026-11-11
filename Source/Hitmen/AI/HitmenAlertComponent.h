// Hitmen — NPC awareness state machine (unaware -> white -> yellow -> red -> search), replicated
// for overhead indicators, minimap and HUD.
//
// White (Suspicious) = saw you, not alerted. Losing sight after a long enough look turns it yellow
// (Investigating the last seen spot); a short glimpse just cools down. Red (Combat) only when the
// white fill completes while seeing you, when attacked, when witnessing a kill or hearing close
// gunfire. Yellow-search (Searching) = lost you, scores likely hiding places.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Core/HitmenTypes.h"
#include "HitmenAlertComponent.generated.h"

class AHitmenCharacterBase;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenAlertStateChanged, EHitmenAlertState, OldState, EHitmenAlertState, NewState);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenCallingChanged, bool, bCalling);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSuspicionChanged, float, Suspicion);

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

	/** White fill changed (server and clients): overhead indicators push it to their widget. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Alert")
	FOnHitmenSuspicionChanged OnSuspicionChanged;

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

	// ---- Server queries ----
	/** Would react to a noise now (not deaf, not asleep, not busy shooting a visible target). */
	bool WouldReactToNoise() const;

	/** Would answer a reinforcement call now (hears radio calls, not already fighting). */
	bool WouldAnswerCall() const;

	/**
	 * May start (or keep) a reinforcement call: alerted but not fighting (searching, investigating a
	 * real threat, or red without line of sight), past the cooldown and the minimum alert time, and
	 * not itself alerted by a call (unless relays are allowed). Never while shooting a visible target.
	 */
	bool CanCallReinforcementsNow() const;

	// ---- Stimuli (server) ----
	/** A hostile is visible this frame (called repeatedly while seen). */
	void SeeHostile(AActor* Seen, float DeltaTime, bool bAlarming);
	void LoseSightOf(AActor* Seen);
	void HearNoise(const FVector& Location, EHitmenNoiseType Type, AActor* NoiseInstigator, float Distance);
	void SeeBody(AHitmenCharacterBase* Body);
	void WitnessKill(AActor* Killer);
	void OnDamagedBy(AActor* DamageInstigator);
	void ReceiveReinforcementCall(const FVector& Location, AActor* CallTarget);

	/** Searches around a point without a known target (charmed pawns hunting for their new side). */
	void StartSearchAround(const FVector& Location);

	/** Forces a state (BT tasks, charm, debug). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Alert")
	void SetAlertState(EHitmenAlertState NewState);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Alert")
	void SetCallingReinforcements(bool bCalling);

	/** Off = deaf: noises, distractions and radio calls are ignored (sight and damage only). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Alert")
	void SetReactsToNoises(bool bReacts) { bReactsToNoises = bReacts; }

	/** Off = bodies are ignored (sentries: Hold Position + React To Noises off). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Alert")
	void SetReactsToBodies(bool bReacts) { bReactsToBodies = bReacts; }

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
	float InstantDetectionDistance = 300.f;

	/** Distance at which the rise rate reaches its minimum. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0", Units="cm"))
	float SuspicionFalloffDistance = 1600.f;

	/** Multiplier when the hostile is doing something alarming (armed, sprinting, in a restricted act). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="1"))
	float AlarmingMultiplier = 2.5f;

	/** White fill reached when sight is lost that turns white into yellow (investigate the last seen
	 *  spot). Below it, the glimpse just cools down. 0 = any glimpse investigates; 1 = never. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0", ClampMax="1"))
	float InvestigateSuspicionThreshold = 0.35f;

	/** Seconds without seeing the hostile before a white NPC counts it as lost. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0", Units="s"))
	float LoseSightGraceTime = 0.5f;

	/** Shot by someone it cannot see: the NPC goes on alert toward a point this far in the shot's direction
	 *  (it never learns the shooter's real position without seeing them). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="0", Units="cm"))
	float UnseenAttackerSearchDistance = 600.f;

	/** Rise rate multiplier for an NPC already alerted (yellow): it fills faster when it sees you again. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Alert", meta=(ClampMin="1"))
	float AlertedSuspicionMultiplier = 2.f;

	// ---- Replicated state ----
	UPROPERTY(ReplicatedUsing=OnRep_AlertState)
	EHitmenAlertState AlertState = EHitmenAlertState::Unaware;

	UPROPERTY(ReplicatedUsing=OnRep_Suspicion)
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
	bool bReactsToBodies = true;
	/** The current investigation comes from a real threat (sighting, body, damage, gunfire), not a
	 *  footstep or a distraction: such an investigator may call reinforcements. */
	bool bThreatInvestigation = false;
	/** Alerted by someone else's radio call (does not relay it unless the rules allow it). */
	bool bAlertedByCall = false;
	float LastSeenTime = -100.f;
	float StateEnterTime = 0.f;
	float LastDamageTime = -100.f;
	/** When the NPC last went from calm (unaware / white) to alerted (yellow / red / search). */
	float AlertedSinceTime = 0.f;
	float LastCallStartTime = -UE_BIG_NUMBER;
	TSet<TWeakObjectPtr<AHitmenCharacterBase>> KnownBodies;
	EHitmenAlertState PreviousRepState = EHitmenAlertState::Unaware;

	UFUNCTION() void OnRep_AlertState();
	UFUNCTION() void OnRep_Calling();
	UFUNCTION() void OnRep_Suspicion();

	/** Server: writes Suspicion and fires OnSuspicionChanged when it moved. */
	void SetSuspicionValue(float NewSuspicion);

	void SetInvestigate(const FVector& Location, bool bThreat);
	float GetRuleLoseTime() const;
	float GetRuleSearchTime() const;
	float GetRuleInvestigateTime() const;
	float GetDifficultySuspicionMultiplier() const;
	bool IsOwnerCharmed() const;

	/** Tells the AI subsystem which target this NPC is searching (ref-counted last known position marker). */
	void RefreshSearchMarker();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState);
};
