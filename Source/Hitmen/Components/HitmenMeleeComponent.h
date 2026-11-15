// Hitmen — fists when nothing is in hand, and the silent stealth kill from behind (F)

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenMeleeComponent.generated.h"

class AHitmenCharacterBase;
class UNiagaraSystem;

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenMeleeComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenMeleeComponent();

	/** Local: punch in front. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Melee")
	void Punch();

	/** Victim the owner could stealth kill right now (local prompt + server validation). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Melee")
	AHitmenCharacterBase* FindStealthKillTarget() const;

	/** Local: starts a stealth kill if possible. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Melee")
	bool TryStealthKill();

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Punch", meta=(ClampMin="0"))
	float PunchDamage = 20.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Punch", meta=(ClampMin="0", Units="cm"))
	float PunchRange = 150.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Punch", meta=(ClampMin="0", Units="cm"))
	float PunchRadius = 30.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Punch", meta=(ClampMin="0", Units="s"))
	float PunchCooldown = 0.55f;

	/** Noise radius of a punch (combat sounds attract nearby NPCs). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Punch", meta=(ClampMin="0", Units="cm"))
	float PunchNoiseRadius = 500.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|StealthKill", meta=(ClampMin="0", Units="cm"))
	float StealthKillRange = 170.f;

	/** Attacker must be behind the victim: dot(victim forward, victim->attacker) below this. */
	/** The attacker blends into the strike position (and the view turns) over this time: no snap. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|StealthKill", meta=(ClampMin="0", Units="s"))
	float TakedownBlendTime = 0.25f;

	UFUNCTION(Server, Reliable)
	void Server_Punch();

	UFUNCTION(Server, Reliable)
	void Server_StealthKill(AHitmenCharacterBase* Victim);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_PlayPunch(bool bHit, FVector_NetQuantize HitLocation);

	/** Punch montage (body + first-person arms) and juice on this machine. */
	void PlayPunchLocal();

	/** A punch that lands: impact puff and thump for everyone, shake, view kick and a hit stop for the puncher. */
	void PlayPunchHitLocal(const FVector& HitLocation);

	/** The hostile a punch from here would hit (same sweep on the puncher and the server). */
	bool SweepPunch(FHitResult& OutHit) const;

	/** Impact puff where a punch lands (everyone sees it). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Juice")
	TObjectPtr<UNiagaraSystem> PunchHitFX;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Juice", meta=(ClampMin="0.01"))
	float PunchHitFXScale = 0.45f;

	/** View shake of the puncher when a punch lands. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Juice", meta=(ClampMin="0"))
	float PunchHitShake = 0.9f;

	/** The puncher's view widens this much on contact, then settles. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Juice", meta=(ClampMin="0", Units="deg"))
	float PunchHitFOVKick = 3.f;

	/** The puncher's fist holds still this long on contact (local hit stop). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|Juice", meta=(ClampMin="0", Units="s"))
	float PunchHitStop = 0.07f;

	/** Punch montage speed (the mannequin attack clip is slow for a jab). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee", meta=(ClampMin="0.1"))
	float PunchPlayRate = 1.6f;

	UFUNCTION(NetMulticast, Reliable)
	void Multicast_PlayStealthKill(AHitmenCharacterBase* Victim);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnPunch();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnStealthKill(AHitmenCharacterBase* Victim);

	bool IsValidStealthKillVictim(const AHitmenCharacterBase* Victim) const;

	/** The takedown plays before the victim dies: both are held in place this long. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|StealthKill", meta=(ClampMin="0", Units="s"))
	float StealthKillDuration = 1.f;

	/** Moment of the strike in the takedown (the victim dies here). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|StealthKill", meta=(ClampMin="0", Units="s"))
	float StealthKillStrikeTime = 0.55f;

	/** The attacker is pulled to this distance behind the victim. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|StealthKill", meta=(ClampMin="0", Units="cm"))
	float StealthKillHoldDistance = 75.f;

	void FinishStealthKill(TWeakObjectPtr<AHitmenCharacterBase> Victim);
	void EndTakedown(TWeakObjectPtr<AHitmenCharacterBase> Victim);

	UPROPERTY(Replicated)
	bool bTakedownActive = false;

	FTimerHandle TakedownStrikeTimer;
	FTimerHandle TakedownEndTimer;

	float LastPunchTime = -100.f;

public:
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	/** True during a takedown (input ignored by the owner). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Melee")
	bool IsPerformingTakedown() const { return bTakedownActive; }

	// Takedown blend (server moves the attacker; the owning client turns its view).
	FTimerHandle TakedownBlendTimer;
	TWeakObjectPtr<AActor> BlendVictim;
	FVector BlendFrom = FVector::ZeroVector;
	FVector BlendTo = FVector::ZeroVector;
	float BlendStart = 0.f;
	void UpdateTakedownBlend();

	UFUNCTION(Client, Reliable)
	void Client_BlendView(float TargetYaw, float TargetPitch, float Duration);
	FTimerHandle ViewBlendTimer;
	FRotator ViewBlendFrom = FRotator::ZeroRotator;
	FRotator ViewBlendTo = FRotator::ZeroRotator;
	float ViewBlendStart = 0.f;
	float ViewBlendDuration = 0.f;
	void UpdateViewBlend();
};
