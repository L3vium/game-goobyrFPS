// Hitmen — Goobyr gun hamster: a tiny critter that runs to the nearest enemy of whoever fired it and bites

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "Core/HitmenTeamLibrary.h"
#include "HitmenHamster.generated.h"

class UStaticMeshComponent;

UCLASS(Blueprintable)
class HITMEN_API AHitmenHamster : public ACharacter, public IHitmenTeamMember
{
	GENERATED_BODY()

public:
	AHitmenHamster(const FObjectInitializer& ObjectInitializer);

	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
	virtual float TakeDamage(float DamageAmount, struct FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser) override;

	/** Server: whose side it fights for (the shooter). */
	void SetMaster(AActor* InMaster);

	/** Its master's side: it bites their enemies, and guards may shoot it. */
	virtual int32 GetHitmenTeamId_Implementation() const override;

protected:
	// ---- Model (basic shapes; swap the meshes / materials in the Blueprint) ----
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Body;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Head;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> EarLeft;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> EarRight;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Tail;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster|Look")
	FLinearColor FurColor = FLinearColor(0.78f, 0.52f, 0.28f);

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster|Look")
	FLinearColor BellyColor = FLinearColor(0.95f, 0.88f, 0.78f);

	// ---- Behaviour ----
	/** Looks for enemies of its master this far. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster", meta=(ClampMin="0", Units="cm"))
	float SeekRadius = 2000.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster", meta=(ClampMin="0"))
	float BiteDamage = 12.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster", meta=(ClampMin="0.05", Units="s"))
	float BiteInterval = 0.6f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster", meta=(ClampMin="0", Units="cm"))
	float BiteRange = 75.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster", meta=(ClampMin="1"))
	float Health = 10.f;

	/** Seconds before it scurries off for good (gone). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster", meta=(ClampMin="1", Units="s"))
	float Lifetime = 15.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster", meta=(ClampMin="0", Units="cm/s"))
	float RunSpeed = 620.f;

	/** Puff when it lands, bites or goes. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Hamster|Juice")
	TObjectPtr<class UNiagaraSystem> PuffFX;

	UPROPERTY(Replicated)
	TObjectPtr<AActor> Master;

	TWeakObjectPtr<AActor> Prey;
	float NextThink = 0.f;
	float NextBite = 0.f;
	float NextSqueak = 0.f;
	float DamageTaken = 0.f;

	AActor* FindPrey() const;
	void Bite(AActor* Target);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Bite(FVector_NetQuantize Location);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Gone(FVector_NetQuantize Location);

	void Vanish();

public:
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
};
