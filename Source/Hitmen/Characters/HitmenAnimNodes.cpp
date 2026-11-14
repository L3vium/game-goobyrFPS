// Hitmen — procedural body layer (anim graph node)

#include "Characters/HitmenAnimNodes.h"
#include "Characters/HitmenAnimInstance.h"
#include "Characters/HitmenFirstPersonAnimInstance.h"
#include "Animation/AnimInstanceProxy.h"
#include "Animation/AnimSequence.h"
#include "Animation/Skeleton.h"
#include "TwoBoneIK.h"

namespace HitmenBodyIK
{
	/** Every finger bone of the mannequin, parents first (missing ones are skipped). */
	TArray<FName> FingerNames()
	{
		TArray<FName> Names;
		for (const TCHAR* Side : { TEXT("l"), TEXT("r") })
		{
			for (const TCHAR* Finger : { TEXT("thumb"), TEXT("index"), TEXT("middle"), TEXT("ring"), TEXT("pinky") })
			{
				if (FCString::Strcmp(Finger, TEXT("thumb")) != 0)
				{
					Names.Add(*FString::Printf(TEXT("%s_metacarpal_%s"), Finger, Side));
				}
				for (int32 Segment = 1; Segment <= 3; ++Segment)
				{
					Names.Add(*FString::Printf(TEXT("%s_%02d_%s"), Finger, Segment, Side));
				}
			}
		}
		return Names;
	}
}

void FAnimNode_HitmenBodyIK::PreUpdate(const UAnimInstance* InAnimInstance)
{
	// Game thread: copy the driving state, and cache the fist pose once.
	if (const UHitmenAnimInstance* Body = Cast<UHitmenAnimInstance>(InAnimInstance))
	{
		State = Body->GetBodyIKState();
	}
	else if (const UHitmenFirstPersonAnimInstance* Arms = Cast<UHitmenFirstPersonAnimInstance>(InAnimInstance))
	{
		State = Arms->GetBodyIKState();
	}
	else
	{
		State = FHitmenBodyIKState();
	}

	if (FistPose && CachedFistPose.Get() != FistPose)
	{
		CachedFistPose = FistPose;
		FistLocal.Reset();
		if (const USkeleton* Skeleton = FistPose->GetSkeleton())
		{
			const FReferenceSkeleton& Reference = Skeleton->GetReferenceSkeleton();
			const FAnimExtractContext Context(static_cast<double>(FistPoseTime));
			for (const FName& Name : HitmenBodyIK::FingerNames())
			{
				const int32 Index = Reference.FindBoneIndex(Name);
				if (Index != INDEX_NONE)
				{
					FTransform Local;
					FistPose->GetBoneTransform(Local, FSkeletonPoseBoneIndex(Index), Context, false);
					FistLocal.Add(Name, Local);
				}
			}
		}
	}
}

bool FAnimNode_HitmenBodyIK::IsValidToEvaluate(const USkeleton* Skeleton, const FBoneContainer& RequiredBones)
{
	return Pelvis.IsValidToEvaluate(RequiredBones);
}

void FAnimNode_HitmenBodyIK::InitializeBoneReferences(const FBoneContainer& RequiredBones)
{
	for (FBoneReference* Bone : { &Pelvis, &Spine, &ThighL, &CalfL, &FootL, &ThighR, &CalfR, &FootR, &UpperArmL, &LowerArmL, &HandL, &UpperArmR, &LowerArmR, &HandR })
	{
		Bone->Initialize(RequiredBones);
	}
	FingerBones.Reset();
	FingerNames.Reset();
	for (const FName& Name : HitmenBodyIK::FingerNames())
	{
		FBoneReference Finger(Name);
		Finger.Initialize(RequiredBones);
		if (Finger.IsValidToEvaluate(RequiredBones))
		{
			FingerBones.Add(Finger.GetCompactPoseIndex(RequiredBones));
			FingerNames.Add(Name);
		}
	}
}

void FAnimNode_HitmenBodyIK::SolveLimb(FComponentSpacePoseContext& Output, const FBoneReference& Root, const FBoneReference& Joint, const FBoneReference& End,
	const FVector& Effector, const FVector& JointTarget, bool bKeepEndRotation, const FTransform* EndRotationFrom) const
{
	const FBoneContainer& Bones = Output.Pose.GetPose().GetBoneContainer();
	if (!Root.IsValidToEvaluate(Bones) || !Joint.IsValidToEvaluate(Bones) || !End.IsValidToEvaluate(Bones))
	{
		return;
	}
	const FCompactPoseBoneIndex RootIndex = Root.GetCompactPoseIndex(Bones);
	const FCompactPoseBoneIndex JointIndex = Joint.GetCompactPoseIndex(Bones);
	const FCompactPoseBoneIndex EndIndex = End.GetCompactPoseIndex(Bones);
	FTransform RootTransform = Output.Pose.GetComponentSpaceTransform(RootIndex);
	FTransform JointTransform = Output.Pose.GetComponentSpaceTransform(JointIndex);
	FTransform EndTransform = Output.Pose.GetComponentSpaceTransform(EndIndex);
	const FQuat EndRotation = EndRotationFrom ? EndRotationFrom->GetRotation() : EndTransform.GetRotation();
	AnimationCore::SolveTwoBoneIK(RootTransform, JointTransform, EndTransform, JointTarget, Effector, false, 1.0, 1.0);
	if (bKeepEndRotation)
	{
		EndTransform.SetRotation(EndRotation);
	}
	TArray<FBoneTransform> Transforms = { FBoneTransform(RootIndex, RootTransform), FBoneTransform(JointIndex, JointTransform), FBoneTransform(EndIndex, EndTransform) };
	Transforms.Sort(FCompareBoneTransformIndex());
	Output.Pose.SafeSetCSBoneTransforms(Transforms);
}

void FAnimNode_HitmenBodyIK::EvaluateSkeletalControl_AnyThread(FComponentSpacePoseContext& Output, TArray<FBoneTransform>& OutBoneTransforms)
{
	const FBoneContainer& Bones = Output.Pose.GetPose().GetBoneContainer();
	const FVector Forward = ComponentForward.GetSafeNormal();

	// ---- Crouch / slide: pelvis down, spine leaned, feet kept where the animation put them (knees bend) ----
	if (State.CrouchAlpha > KINDA_SMALL_NUMBER)
	{
		const float Crouch = FMath::Clamp(State.CrouchAlpha, 0.f, 1.f);
		const bool bLegs = FootL.IsValidToEvaluate(Bones) && FootR.IsValidToEvaluate(Bones) && CalfL.IsValidToEvaluate(Bones) && CalfR.IsValidToEvaluate(Bones);
		FTransform FootLBefore, FootRBefore;
		if (bLegs)
		{
			FootLBefore = Output.Pose.GetComponentSpaceTransform(FootL.GetCompactPoseIndex(Bones));
			FootRBefore = Output.Pose.GetComponentSpaceTransform(FootR.GetCompactPoseIndex(Bones));
		}
		const FCompactPoseBoneIndex PelvisIndex = Pelvis.GetCompactPoseIndex(Bones);
		FTransform PelvisTransform = Output.Pose.GetComponentSpaceTransform(PelvisIndex);
		PelvisTransform.AddToTranslation(FVector(0.f, 0.f, -State.PelvisDrop * Crouch));
		Output.Pose.SafeSetCSBoneTransforms({ FBoneTransform(PelvisIndex, PelvisTransform) });

		if (Spine.IsValidToEvaluate(Bones) && !FMath::IsNearlyZero(State.SpineLean))
		{
			const FCompactPoseBoneIndex SpineIndex = Spine.GetCompactPoseIndex(Bones);
			FTransform SpineTransform = Output.Pose.GetComponentSpaceTransform(SpineIndex);
			const FQuat Lean(LeanAxis.GetSafeNormal(), FMath::DegreesToRadians(State.SpineLean * Crouch));
			SpineTransform.SetRotation(Lean * SpineTransform.GetRotation());
			Output.Pose.SafeSetCSBoneTransforms({ FBoneTransform(SpineIndex, SpineTransform) });
		}
		if (bLegs)
		{
			const FVector Push = Forward * State.FeetForward * Crouch;
			const FVector KneeL = Output.Pose.GetComponentSpaceTransform(CalfL.GetCompactPoseIndex(Bones)).GetLocation() + Forward * 60.f;
			SolveLimb(Output, ThighL, CalfL, FootL, FootLBefore.GetLocation() + Push, KneeL, true, &FootLBefore);
			const FVector KneeR = Output.Pose.GetComponentSpaceTransform(CalfR.GetCompactPoseIndex(Bones)).GetLocation() + Forward * 60.f;
			SolveLimb(Output, ThighR, CalfR, FootR, FootRBefore.GetLocation() + Push, KneeR, true, &FootRBefore);
		}
	}

	// ---- Hands: wrists toward world targets (ledge, punch), elbows bent toward the pole ----
	const FTransform& ComponentToWorld = Output.AnimInstanceProxy->GetComponentTransform();
	const FBoneReference* Uppers[2] = { &UpperArmL, &UpperArmR };
	const FBoneReference* Lowers[2] = { &LowerArmL, &LowerArmR };
	const FBoneReference* Hands[2] = { &HandL, &HandR };
	for (int32 Side = 0; Side < 2; ++Side)
	{
		const float Weight = FMath::Clamp(State.HandAlpha[Side], 0.f, 1.f);
		if (Weight <= KINDA_SMALL_NUMBER || !Hands[Side]->IsValidToEvaluate(Bones) || !Lowers[Side]->IsValidToEvaluate(Bones))
		{
			continue;
		}
		const FVector Current = Output.Pose.GetComponentSpaceTransform(Hands[Side]->GetCompactPoseIndex(Bones)).GetLocation();
		const FVector Target = ComponentToWorld.InverseTransformPosition(State.HandTarget[Side]);
		const FVector Elbow = Output.Pose.GetComponentSpaceTransform(Lowers[Side]->GetCompactPoseIndex(Bones)).GetLocation();
		const FVector Pole = Elbow + ComponentToWorld.InverseTransformVector(State.ElbowPole[Side]);
		SolveLimb(Output, *Uppers[Side], *Lowers[Side], *Hands[Side], FMath::Lerp(Current, Target, Weight), Pole, true, nullptr);
	}

	// ---- Fist: fingers toward the fist pose (local rotations), parents first ----
	if (State.FistAlpha > KINDA_SMALL_NUMBER && FistLocal.Num() > 0)
	{
		const float FistWeight = FMath::Clamp(State.FistAlpha, 0.f, 1.f);
		for (int32 Index = 0; Index < FingerBones.Num(); ++Index)
		{
			const FTransform* Fist = FistLocal.Find(FingerNames[Index]);
			if (!Fist)
			{
				continue;
			}
			const FCompactPoseBoneIndex Bone = FingerBones[Index];
			const FCompactPoseBoneIndex Parent = Bones.GetParentBoneIndex(Bone);
			if (Parent == INDEX_NONE)
			{
				continue;
			}
			FTransform Local = Output.Pose.GetLocalSpaceTransform(Bone);
			Local.SetRotation(FQuat::Slerp(Local.GetRotation(), Fist->GetRotation(), FistWeight).GetNormalized());
			const FTransform Component = Local * Output.Pose.GetComponentSpaceTransform(Parent);
			Output.Pose.SafeSetCSBoneTransforms({ FBoneTransform(Bone, Component) });
		}
	}
}
