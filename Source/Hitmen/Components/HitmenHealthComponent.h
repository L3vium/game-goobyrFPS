// Hitmen — replicated health and armor

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenHealthComponent.generated.h"

class UHitmenHealthComponent;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_FourParams(FOnHitmenHealthChanged, UHitmenHealthComponent*, HealthComponent, float, NewHealth, float, Delta, AActor*, Instigator);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FOnHitmenDeath, AActor*, Killer, AActor*, DamageCauser, bool, bHeadshot);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenHealthComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenHealthComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Health")
	FOnHitmenHealthChanged OnHealthChanged;

	/** Fires on the server with killer info, and on clients when death replicates. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Health")
	FOnHitmenDeath OnDeath;

	/** Server. Applies damage to armor first, then health. Returns the damage dealt. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Health")
	float ApplyDamage(float Amount, AActor* Instigator, AActor* DamageCauser, bool bHeadshot);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Health")
	void Heal(float Amount);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Health")
	void AddArmor(float Amount);

	/** Kills instantly (stealth kill, multi-kill, poison). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Health")
	void Kill(AActor* Instigator, AActor* DamageCauser, bool bHeadshot = false);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Health")
	void SetGodMode(bool bEnabled) { bGodMode = bEnabled; }

	/** Constructor-time default (players and NPCs share the component with different pools). */
	void SetDefaultMaxHealth(float InMaxHealth) { MaxHealth = InMaxHealth; Health = InMaxHealth; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Health")
	float GetHealth() const { return Health; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Health")
	float GetMaxHealth() const { return MaxHealth; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Health")
	float GetArmor() const { return Armor; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Health")
	float GetHealthNormalized() const { return MaxHealth > 0.f ? Health / MaxHealth : 0.f; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Health")
	bool IsDead() const { return bIsDead; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Health")
	bool WasHeadshotDeath() const { return bHeadshotDeath; }

protected:
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Health", meta=(ClampMin="1"))
	float MaxHealth = 100.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Health", meta=(ClampMin="0"))
	float MaxArmor = 100.f;

	UPROPERTY(ReplicatedUsing=OnRep_Health)
	float Health = 100.f;

	UPROPERTY(Replicated)
	float Armor = 0.f;

	UPROPERTY(ReplicatedUsing=OnRep_Dead)
	bool bIsDead = false;

	UPROPERTY(Replicated)
	bool bHeadshotDeath = false;

	bool bGodMode = false;
	float LastReplicatedHealth = 100.f;

	UFUNCTION() void OnRep_Health();
	UFUNCTION() void OnRep_Dead();

	void Die(AActor* Instigator, AActor* DamageCauser, bool bHeadshot);
};
