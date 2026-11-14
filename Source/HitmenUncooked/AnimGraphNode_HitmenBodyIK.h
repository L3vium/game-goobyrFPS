// Hitmen — editor node for FAnimNode_HitmenBodyIK (procedural crouch / slide pose, hand IK, fists)

#pragma once

#include "CoreMinimal.h"
#include "AnimGraphNode_SkeletalControlBase.h"
#include "Characters/HitmenAnimNodes.h"
#include "AnimGraphNode_HitmenBodyIK.generated.h"

UCLASS(meta=(Keywords="Hitmen Crouch IK Hands Fist"))
class HITMENUNCOOKED_API UAnimGraphNode_HitmenBodyIK : public UAnimGraphNode_SkeletalControlBase
{
	GENERATED_BODY()

public:
	UPROPERTY(EditAnywhere, Category="Settings")
	FAnimNode_HitmenBodyIK Node;

	virtual FText GetNodeTitle(ENodeTitleType::Type TitleType) const override;
	virtual FText GetTooltipText() const override;

protected:
	virtual FText GetControllerDescription() const override;
	virtual const FAnimNode_SkeletalControlBase* GetNode() const override { return &Node; }
};
