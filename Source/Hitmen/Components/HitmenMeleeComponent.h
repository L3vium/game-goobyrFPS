// Hitmen — fists when nothing is in hand, and the silent stealth kill from behind (F)

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenMeleeComponent.generated.h"

class AHitmenCharacterBase;

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
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Melee|StealthKill", meta=(ClampMin="-1", ClampMax="1"))
	float StealthKillBehindDot = -0.35f;

	UFUNCTION(Server, Reliable)
	void Server_Punch();

	UFUNCTION(Server, Reliable)
	void Server_StealthKill(AHitmenCharacterBase* Victim);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_PlayPunch();

	/** Punch montage (body + first-person arms) and juice on this machine. */
	void PlayPunchLocal();

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

	float LastPunchTime = -100.f;
};
