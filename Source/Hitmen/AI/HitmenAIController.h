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
	bool IsPawnCharmed() const;

	/** Applies the sight values and the difficulty multiplier to the sight sense. */
	void ApplySightConfig();

	TSet<TWeakObjectPtr<AActor>> PerceivedActors;
	FTimerHandle SightTimer;

	/** The alert component was created by this controller (charmed player) and is destroyed with it. */
	bool bOwnsRuntimeAlert = false;
};
