// Hitmen — projectile base (arrows, rockets, grenades, darts, thrown items) and its families

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Core/HitmenTypes.h"
#include "HitmenProjectile.generated.h"

class USphereComponent;
class UStaticMeshComponent;
class UProjectileMovementComponent;
class UNiagaraSystem;
class USoundBase;

/**
 * Server-authoritative projectile. Impact behaviour is data on the BP child: direct damage,
 * explosion, status effect, noise. Specialised families override OnImpact.
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenProjectile : public AActor
{
	GENERATED_BODY()

public:
	AHitmenProjectile();

	virtual void BeginPlay() override;

	/** Server: sets velocity and base damage (from the weapon or ability). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Projectile")
	virtual void Launch(const FVector& Velocity, float InDamage);

	/** Server: explode now (fuse, C4 detonation, shot barrel). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Projectile")
	virtual void Detonate();

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USphereComponent> Collision;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UProjectileMovementComponent> Movement;

	// ---- Impact behaviour ----
	/** Damage applied to the actor hit directly (0 = none). Overridden by Launch. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Impact", meta=(ClampMin="0"))
	float Damage = 0.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Impact")
	bool bExplodeOnImpact = false;

	/** Explode after this many seconds (grenades); 0 = no fuse. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Impact", meta=(ClampMin="0"))
	float FuseTime = 0.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Explosion", meta=(ClampMin="0", Units="cm"))
	float ExplosionRadius = 450.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Explosion", meta=(ClampMin="0"))
	float ExplosionDamage = 120.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Explosion", meta=(ClampMin="0", Units="cm"))
	float ExplosionNoiseRadius = 4500.f;

	/** Status applied to a pawn hit directly (sleep dart). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Status")
	EHitmenStatusEffect StatusOnHit = EHitmenStatusEffect::None;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Status", meta=(ClampMin="0"))
	float StatusDuration = 10.f;

	/** Noise made at impact (stones, bottles). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Noise", meta=(ClampMin="0", Units="cm"))
	float ImpactNoiseRadius = 0.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Noise")
	EHitmenNoiseType ImpactNoiseType = EHitmenNoiseType::Impact;

	/** Stick to what it hits instead of being destroyed (arrows, C4). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Impact")
	bool bStickOnImpact = false;

	/** Seconds the projectile lives at most. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile", meta=(ClampMin="0"))
	float MaxLifetime = 10.f;

	// ---- Juice ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice")
	TObjectPtr<UNiagaraSystem> ImpactEffect;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice")
	TObjectPtr<UNiagaraSystem> ExplosionEffect;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice")
	TObjectPtr<USoundBase> ImpactSound;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice")
	TObjectPtr<USoundBase> ExplosionSound;

	/** Attached for the whole flight (rocket smoke, arrow streak). Empty = none. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice")
	TObjectPtr<UNiagaraSystem> TrailEffect;

	/** Scale of the spawned explosion effect. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice", meta=(ClampMin="0"))
	float ExplosionEffectScale = 1.f;

	/** Local players within this radius get a view shake (strength falls off with distance). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice", meta=(ClampMin="0", Units="cm"))
	float ExplosionShakeRadius = 2200.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Projectile|Juice", meta=(ClampMin="0"))
	float ExplosionShakeIntensity = 2.5f;

	UFUNCTION()
	void HandleHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult& Hit);

	/** Server: behaviour on impact. Return true if the projectile is consumed. */
	virtual bool OnImpact(const FHitResult& Hit);

	/** Server: explosion behaviour (default radial damage). */
	virtual void OnExplode(const FVector& Origin);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Impact(FVector_NetQuantize Location, bool bExploded);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnImpact(FVector Location);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnExplode(FVector Location);

	AController* GetInstigatorControllerSafe() const;

	FTimerHandle FuseTimer;
	bool bConsumed = false;
};

/** Pulls pawns and physics objects to a point for a few seconds. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenProjectile_BlackHole : public AHitmenProjectile
{
	GENERATED_BODY()

public:
	AHitmenProjectile_BlackHole();
	virtual void Tick(float DeltaSeconds) override;

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|BlackHole", meta=(ClampMin="0", Units="cm"))
	float PullRadius = 1200.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|BlackHole", meta=(ClampMin="0"))
	float PullStrength = 1800.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|BlackHole", meta=(ClampMin="0"))
	float Duration = 4.f;

	/** Damage per second to pawns near the core. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|BlackHole", meta=(ClampMin="0"))
	float CoreDamagePerSecond = 25.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|BlackHole", meta=(ClampMin="0", Units="cm"))
	float CoreRadius = 200.f;

	/** Activates after this delay in flight even without impact. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|BlackHole", meta=(ClampMin="0"))
	float ArmDelay = 1.2f;

	virtual bool OnImpact(const FHitResult& Hit) override;
	void Activate();

	UFUNCTION(NetMulticast, Reliable)
	void Multicast_Activated(FVector_NetQuantize Location);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnActivated();

	bool bActive = false;
	float ActiveTime = 0.f;
	FTimerHandle ArmTimer;
};

/** Stuns everyone around (NPCs and players) and blinds players with line of sight. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenProjectile_Flashbang : public AHitmenProjectile
{
	GENERATED_BODY()

public:
	AHitmenProjectile_Flashbang();

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Flashbang", meta=(ClampMin="0"))
	float StunDuration = 5.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Flashbang", meta=(ClampMin="0"))
	float BlindDuration = 3.f;

	virtual void OnExplode(const FVector& Origin) override;
};

/** Spawns a smoke cloud that blocks NPC and player sight. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenProjectile_Smoke : public AHitmenProjectile
{
	GENERATED_BODY()

public:
	AHitmenProjectile_Smoke();

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Smoke")
	TSubclassOf<class AHitmenSmokeCloud> SmokeClass;

	virtual void OnExplode(const FVector& Origin) override;
};
