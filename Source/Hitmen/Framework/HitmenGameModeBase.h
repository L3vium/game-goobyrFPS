// Hitmen — base game mode: default classes, rules access, damage policy

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "HitmenGameModeBase.generated.h"

class UHitmenMatchRules;
class AHitmenCharacterBase;
class AHitmenPlayerState;

UCLASS(Abstract)
class HITMEN_API AHitmenGameModeBase : public AGameModeBase
{
	GENERATED_BODY()

public:
	AHitmenGameModeBase();

	virtual void PostLogin(APlayerController* NewPlayer) override;

	/** Rules for the current map (map override or project default). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Rules")
	UHitmenMatchRules* GetRules() const;

	/** Cheats / debug grants (gym, development builds). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Debug")
	virtual bool AllowsDebugCommands() const;

	/** Friendly-fire policy. Self damage from explosions is allowed. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Damage")
	virtual bool CanDamage(const AActor* DamageInstigator, const AActor* Victim) const;

	/** Called by characters (server) when they die. */
	virtual void OnCharacterKilled(AHitmenCharacterBase* Victim, AController* Killer, AActor* DamageCauser, bool bHeadshot);

	/** Gives a joining player a name if the client has not sent one yet. */
	void EnsurePlayerName(AHitmenPlayerState* PlayerState) const;

protected:
	/** Rules asset used when neither the map nor the project settings provide one. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Rules")
	TObjectPtr<UHitmenMatchRules> RulesOverride;

	UPROPERTY(Transient)
	mutable TObjectPtr<UHitmenMatchRules> CachedRules;

	/** Juice hook: a character died (server). */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnCharacterKilled(AHitmenCharacterBase* Victim, AController* Killer, bool bHeadshot);
};
