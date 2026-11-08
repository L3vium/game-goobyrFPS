// Hitmen — carry the VIP body on the back (slower), drop it, deliver it to a retrieval point

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

	/** Only the VIP can be carried (other bodies are hidden with the lighter). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Carry")
	bool bOnlyVIP = true;

	/** Socket of the carrier's mesh the body attaches to. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Carry")
	FName CarrySocket = TEXT("spine_03");

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
