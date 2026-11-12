// Hitmen — carry a body in your arms (slower, hands busy: it takes the left weapon slot), drop it, hide it in a
// container or deliver the VIP to a retrieval point

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenBodyCarryComponent.generated.h"

class AHitmenCharacterBase;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenCarryChanged, AHitmenCharacterBase*, Body);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenBodyCarryComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenBodyCarryComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Carry")
	FOnHitmenCarryChanged OnCarryChanged;

	UFUNCTION(BlueprintPure, Category="Hitmen|Carry")
	bool IsCarrying() const { return CarriedBody != nullptr; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Carry")
	AHitmenCharacterBase* GetCarriedBody() const { return CarriedBody; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Carry")
	bool CanCarry(const AHitmenCharacterBase* Body) const;

	/** Server. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Carry")
	void PickUp(AHitmenCharacterBase* Body);

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Carry")
	void Drop();

	/** Client request to drop. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Carry")
	void RequestDrop();

	float GetSpeedMultiplier() const;

protected:
	/** Designer toggle: allow carrying bodies at all. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Carry")
	bool bCarryEnabled = true;

	/** Only the VIP can be carried (off: any body, to hide it in a container). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Carry")
	bool bOnlyVIP = false;

	/** Where the body lies in the arms, relative to the carrier (X forward, Z up; rolled to lie across). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Carry")
	FTransform CarryTransform = FTransform(FRotator(0.f, 0.f, 90.f), FVector(55.f, 0.f, -5.f));

public:
	const FTransform& GetCarryTransform() const { return CarryTransform; }

protected:

	UPROPERTY(ReplicatedUsing=OnRep_CarriedBody)
	TObjectPtr<AHitmenCharacterBase> CarriedBody;

	UFUNCTION(Server, Reliable)
	void Server_Drop();

	UFUNCTION() void OnRep_CarriedBody();

	UFUNCTION()
	void HandleOwnerDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnCarryChanged(bool bCarrying);

	void NotifyGameMode() const;
};
