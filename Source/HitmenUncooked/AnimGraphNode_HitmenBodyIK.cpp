// Hitmen — editor node for FAnimNode_HitmenBodyIK

#include "AnimGraphNode_HitmenBodyIK.h"

#define LOCTEXT_NAMESPACE "HitmenBodyIK"

FText UAnimGraphNode_HitmenBodyIK::GetControllerDescription() const
{
	return LOCTEXT("Description", "Hitmen Body IK");
}

FText UAnimGraphNode_HitmenBodyIK::GetNodeTitle(ENodeTitleType::Type TitleType) const
{
	return GetControllerDescription();
}

FText UAnimGraphNode_HitmenBodyIK::GetTooltipText() const
{
	return LOCTEXT("Tooltip", "Procedural layer driven by the Hitmen anim instance: crouch / slide pose with planted feet, hand IK (ledges, punches) and closed fists.");
}

#undef LOCTEXT_NAMESPACE
