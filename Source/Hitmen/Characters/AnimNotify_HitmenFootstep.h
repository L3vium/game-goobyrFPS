// Hitmen — AN_Footstep: place on walk/run animations at foot contacts

#pragma once

#include "CoreMinimal.h"
#include "Animation/AnimNotifies/AnimNotify.h"
#include "AnimNotify_HitmenFootstep.generated.h"

UCLASS(meta=(DisplayName="Hitmen Footstep"))
class HITMEN_API UAnimNotify_HitmenFootstep : public UAnimNotify
{
	GENERATED_BODY()

public:
	/** Foot bone used as trace origin (foot_l / foot_r). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen")
	FName FootBone = TEXT("foot_l");

	virtual void Notify(USkeletalMeshComponent* MeshComp, UAnimSequenceBase* Animation, const FAnimNotifyEventReference& EventReference) override;
	virtual FString GetNotifyName_Implementation() const override { return TEXT("Footstep"); }
};
