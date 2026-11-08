// Hitmen — a group of guards that patrol together (leader + followers in formation, shared alerts)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Core/HitmenTypes.h"
#include "HitmenGuardGroup.generated.h"

class AHitmenNPC;
class UBillboardComponent;

/**
 * Members[0] leads (its own patrol route or post); the others follow it in a loose formation
 * while calm. When one member gets alerted, the others are alerted toward the same spot.
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenGuardGroup : public AActor
{
	GENERATED_BODY()

public:
	AHitmenGuardGroup();
	virtual void BeginPlay() override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Group")
	AHitmenNPC* GetLeader() const;

	/** Where a follower should stand behind the leader (world). */
	FVector GetFormationLocation(const AHitmenNPC* Follower) const;

protected:
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UBillboardComponent> Billboard;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Group")
	TArray<TObjectPtr<AHitmenNPC>> Members;

	/** Distance behind the leader. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Group", meta=(ClampMin="0", Units="cm"))
	float FollowDistance = 170.f;

	/** Side spacing between followers. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Group", meta=(ClampMin="0", Units="cm"))
	float FollowSpacing = 150.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Group")
	bool bShareAlerts = true;

	UFUNCTION()
	void HandleMemberAlert(EHitmenAlertState OldState, EHitmenAlertState NewState);

	bool bSharing = false;
};
