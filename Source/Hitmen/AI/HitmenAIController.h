// Hitmen — NPC brain: sight perception feeding the alert component, blackboard sync, BT runner

#pragma once

#include "CoreMinimal.h"
#include "AIController.h"
#include "GenericTeamAgentInterface.h"
#include "Core/HitmenTypes.h"
#include "HitmenAIController.generated.h"

class UBehaviorTree;
class UAISenseConfig_Sight;
class UHitmenAlertComponent;
struct FAIStimulus;

/** Blackboard key names shared by BB_NPC and the C++ BT nodes. */
namespace HitmenBBKeys
{
	extern HITMEN_API const FName AlertState;
	extern HITMEN_API const FName TargetActor;
	extern HITMEN_API const FName LastKnownLocation;
	extern HITMEN_API const FName InvestigateLocation;
	extern HITMEN_API const FName MoveLocation;
	extern HITMEN_API const FName WaitTime;
	extern HITMEN_API const FName TargetVisible;
	extern HITMEN_API const FName IsSniper;
	extern HITMEN_API const FName HomeLocation;
	extern HITMEN_API const FName UseActor;
	extern HITMEN_API const FName CanCallReinforcements;
	extern HITMEN_API const FName IsStationary;
	extern HITMEN_API const FName HoldPosition;
	extern HITMEN_API const FName IsOnEdge;
	extern HITMEN_API const FName Evacuate;
	/** Body disposal: the body to carry and the container to put it in (set after a body search). */
	extern HITMEN_API const FName DisposeBody;
	extern HITMEN_API const FName DisposeContainer;
	extern HITMEN_API const FName DisposeLocation;
}

UCLASS(Blueprintable)
class HITMEN_API AHitmenAIController : public AAIController
{
	GENERATED_BODY()

public:
	AHitmenAIController();

	virtual void OnPossess(APawn* InPawn) override;
	virtual void OnUnPossess() override;
	virtual ETeamAttitude::Type GetTeamAttitudeTowards(const AActor& Other) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|AI")
	UHitmenAlertComponent* GetAlert() const { return Alert; }

	/** Pauses/resumes thinking (stun, sleep, trap). */
	void SetBrainPaused(bool bPaused);

	/** Sight radius after the difficulty's NPCSightRangeMultiplier. */
	UFUNCTION(BlueprintPure, Category="Hitmen|AI")
	float GetEffectiveSightRadius() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|AI")
	float GetPeripheralVisionHalfAngle() const { return PeripheralVisionHalfAngle; }

	/** Full sight test (range, cone, walls, smoke, darkness, hidden status) against any actor. */
	bool CanSeeActorNow(const AActor* Actor) const;

	/** Pushes the alert state into the blackboard (also called on a timer). */
	void SyncBlackboard();

	/** The NPC's fixed keys (post, sniper, stationary, hold position): at possession and when its role changes. */
	void RefreshNPCKeys();

protected:
	virtual void BeginPlay() override;

	/** Root tree (BT_NPC) with its subtrees. Empty = Project Settings -> Hitmen -> Default Behavior Tree
	 *  (bare AHitmenAIController, e.g. a charmed player). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI")
	TObjectPtr<UBehaviorTree> BehaviorTree;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0", Units="cm"))
	float SightRadius = 1950.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0", Units="cm"))
	float LoseSightRadius = 2250.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0", ClampMax="180", Units="deg"))
	float PeripheralVisionHalfAngle = 65.f;

	/** Hitman-like vision: full rate inside this cone, slower in the peripheral vision up to PeripheralVisionHalfAngle. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0", ClampMax="180", Units="deg"))
	float FocalVisionHalfAngle = 30.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0", ClampMax="1"))
	float PeripheralRateMultiplier = 0.35f;

	/** Only part of the body in view (head, chest and pelvis are tested) fills the meter slower: this at one in three. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0", ClampMax="1"))
	float PartialCoverRateMultiplier = 0.4f;

	/** Weapons left lying around are noticed this far (they are smaller than a person). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0", Units="cm"))
	float EvidenceSightRadius = 1200.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0.1", Units="s"))
	float EvidenceCheckInterval = 0.5f;

	/** How often visible actors are evaluated (seconds). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|AI|Sight", meta=(ClampMin="0.02"))
	float SightEvaluationInterval = 0.1f;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|AI")
	TObjectPtr<UAISenseConfig_Sight> SightConfig;

	UPROPERTY(Transient)
	TObjectPtr<UHitmenAlertComponent> Alert;

	UFUNCTION()
	void HandleTargetPerceptionUpdated(AActor* Actor, FAIStimulus Stimulus);

	UFUNCTION()
	void HandleAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState);

	void EvaluateSight();
	bool CanReallySee(const AActor* Actor) const;
	bool IsAlarming(const AActor* Actor) const;
	/** 0..1 how fast the meter fills for this actor: focal or peripheral vision, how much of the body is visible. */
	float GetSightRateScale(const AActor* Actor) const;
	/** Weapons lying around in view (evidence to pick up). */
	void EvaluateEvidence();
	float EvidenceAccumulator = 0.f;
	bool IsPawnCharmed() const;

	/** Applies the sight values and the difficulty multiplier to the sight sense. */
	void ApplySightConfig();

	TSet<TWeakObjectPtr<AActor>> PerceivedActors;
	FTimerHandle SightTimer;

	/** The alert component was created by this controller (charmed player) and is destroyed with it. */
	bool bOwnsRuntimeAlert = false;
};
