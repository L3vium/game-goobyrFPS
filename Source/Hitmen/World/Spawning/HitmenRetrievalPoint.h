// Hitmen — retrieval point candidate (helicopter, boat, van... the team sets the mesh)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenRetrievalPoint.generated.h"

class UBoxComponent;
class UStaticMeshComponent;

UCLASS(Blueprintable)
class HITMEN_API AHitmenRetrievalPoint : public AActor
{
	GENERATED_BODY()

public:
	AHitmenRetrievalPoint();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Retrieval")
	void SetActive(bool bInActive);

	UFUNCTION(BlueprintPure, Category="Hitmen|Retrieval")
	bool IsActive() const { return bActive; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> DeliveryZone;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	/** Hide the mesh when this candidate is not selected for the match. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	bool bHideWhenInactive = true;

	UPROPERTY(ReplicatedUsing=OnRep_Active)
	bool bActive = true;

	UFUNCTION() void OnRep_Active();

	UFUNCTION()
	void HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnActiveChanged(bool bIsActive);
};
