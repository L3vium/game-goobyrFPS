// Hitmen — footstep anim notify

#include "Characters/AnimNotify_HitmenFootstep.h"
#include "Components/HitmenFootstepComponent.h"
#include "Components/SkeletalMeshComponent.h"

void UAnimNotify_HitmenFootstep::Notify(USkeletalMeshComponent* MeshComp, UAnimSequenceBase* Animation, const FAnimNotifyEventReference& EventReference)
{
	Super::Notify(MeshComp, Animation, EventReference);
	if (AActor* Owner = MeshComp ? MeshComp->GetOwner() : nullptr)
	{
		if (UHitmenFootstepComponent* Footsteps = Owner->FindComponentByClass<UHitmenFootstepComponent>())
		{
			Footsteps->HandleFootstep(FootBone);
		}
	}
}
