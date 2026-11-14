// Hitmen — procedural body layer (anim graph node): crouch / slide pose with planted feet, hand IK (ledges,
// punches) and a closed fist, driven by the owning anim instance (no animation asset needed)

#pragma once

#include "CoreMinimal.h"
#include "BoneControllers/AnimNode_SkeletalControlBase.h"
#include "HitmenAnimNodes.generated.h"

class UAnimSequence;

/** What the procedural layer does this frame; filled on the game thread by the anim instance. */
struct FHitmenBodyIKState
{
	/** 0..1: lower the pelvis by PelvisDrop, lean the spine by SpineLean and keep the feet where they were. */
	float CrouchAlpha = 0.f;
	float PelvisDrop = 0.f;
	float SpineLean = 0.f;
	/** Feet pushed forward (cm, slide: legs out in front). */
	float FeetForward = 0.f;

	/** Per hand (0 left, 1 right): IK weight and world target of the wrist. */
	float HandAlpha[2] = { 0.f, 0.f };
	FVector HandTarget[2] = { FVector::ZeroVector, FVector::ZeroVector };
	/** World offset from the elbow toward which it bends (down and out by default). */
	FVector ElbowPole[2] = { FVector(0.f, 0.f, -50.f), FVector(0.f, 0.f, -50.f) };

	/** 0..1 blend of the fingers toward the fist pose. */
	float FistAlpha = 0.f;
};

USTRUCT(BlueprintInternalUseOnly)
struct HITMEN_API FAnimNode_HitmenBodyIK : public FAnimNode_SkeletalControlBase
{
	GENERATED_BODY()

	/** Fingers take this sequence's pose at FistPoseTime when FistAlpha > 0 (a frame with closed fists). */
	UPROPERTY(EditAnywhere, Category="Hitmen")
	TObjectPtr<UAnimSequence> FistPose;

	UPROPERTY(EditAnywhere, Category="Hitmen", meta=(ClampMin="0"))
	float FistPoseTime = 0.3f;

	/** Component-space axis the spine turns around to lean forward (mannequin: forward is +Y, so -X). */
	UPROPERTY(EditAnywhere, Category="Hitmen")
	FVector LeanAxis = FVector(-1.f, 0.f, 0.f);

	/** Component-space forward (knees bend toward it, feet slide along it). */
	UPROPERTY(EditAnywhere, Category="Hitmen")
	FVector ComponentForward = FVector(0.f, 1.f, 0.f);

	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference Pelvis = FBoneReference(TEXT("pelvis"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference Spine = FBoneReference(TEXT("spine_01"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference ThighL = FBoneReference(TEXT("thigh_l"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference CalfL = FBoneReference(TEXT("calf_l"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference FootL = FBoneReference(TEXT("foot_l"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference ThighR = FBoneReference(TEXT("thigh_r"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference CalfR = FBoneReference(TEXT("calf_r"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference FootR = FBoneReference(TEXT("foot_r"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference UpperArmL = FBoneReference(TEXT("upperarm_l"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference LowerArmL = FBoneReference(TEXT("lowerarm_l"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference HandL = FBoneReference(TEXT("hand_l"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference UpperArmR = FBoneReference(TEXT("upperarm_r"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference LowerArmR = FBoneReference(TEXT("lowerarm_r"));
	UPROPERTY(EditAnywhere, Category="Hitmen") FBoneReference HandR = FBoneReference(TEXT("hand_r"));

	// FAnimNode_Base
	virtual bool HasPreUpdate() const override { return true; }
	virtual void PreUpdate(const UAnimInstance* InAnimInstance) override;

	// FAnimNode_SkeletalControlBase
	virtual void EvaluateSkeletalControl_AnyThread(FComponentSpacePoseContext& Output, TArray<FBoneTransform>& OutBoneTransforms) override;
	virtual bool IsValidToEvaluate(const USkeleton* Skeleton, const FBoneContainer& RequiredBones) override;

private:
	virtual void InitializeBoneReferences(const FBoneContainer& RequiredBones) override;

	void SolveLimb(FComponentSpacePoseContext& Output, const FBoneReference& Root, const FBoneReference& Joint, const FBoneReference& End,
		const FVector& Effector, const FVector& JointTarget, bool bKeepEndRotation, const FTransform* EndRotationFrom) const;

	/** Copy of the source's state for this frame (taken on the game thread). */
	FHitmenBodyIKState State;
	/** Local transforms of the finger bones in the fist pose, by bone name (cached once on the game thread). */
	TMap<FName, FTransform> FistLocal;
	TWeakObjectPtr<UAnimSequence> CachedFistPose;
	/** Finger bones present in the skeleton, parents first (compact indices). */
	TArray<FCompactPoseBoneIndex> FingerBones;
	TArray<FName> FingerNames;
};
