// Hitmen — retrieval point candidate (helicopter, boat, van... the team sets the mesh)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenRetrievalPoint.generated.h"

class UBoxComponent;
class UStaticMeshComponent;
class AHitmenCharacterBase;
class UHitmenBodyCarryComponent;

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

	/** Shows the point on the minimap and the pause map while it is active. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UHitmenMapMarkerComponent> MapMarker;

	/** Hide the mesh when this candidate is not selected for the match. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Retrieval")
	bool bHideWhenInactive = true;

	UPROPERTY(ReplicatedUsing=OnRep_Active)
	bool bActive = true;

	UFUNCTION() void OnRep_Active();

	UFUNCTION()
	void HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION()
	void HandleEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex);

	/** A pawn standing inside started (or stopped) carrying a body: deliver if it is the VIP. */
	UFUNCTION()
	void HandleCarryChanged(AHitmenCharacterBase* Body);

	/** Server: delivers if the pawn carries the VIP, the point is active and the pawn is inside. */
	void TryDeliver(APawn* Pawn);

	/** Server: binds to the carry events of pawns inside (and delivers those already carrying). */
	void WatchPawn(APawn* Pawn);
	void UnwatchPawn(APawn* Pawn);
	void ScanOverlappingPawns();

	TArray<TWeakObjectPtr<UHitmenBodyCarryComponent>> WatchedCarriers;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnActiveChanged(bool bIsActive);
};
