// Hitmen — emotes

#include "Components/HitmenEmoteComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Data/HitmenAnimSet.h"
#include "Animation/AnimInstance.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/World.h"

UHitmenEmoteComponent::UHitmenEmoteComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenEmoteComponent::PlayEmote(int32 EmoteIndex)
{
	if (EmoteIndex >= 0)
	{
		Server_PlayEmote(EmoteIndex);
	}
}

void UHitmenEmoteComponent::Server_PlayEmote_Implementation(int32 EmoteIndex)
{
	const float Now = GetWorld()->GetTimeSeconds();
	if (Now - LastEmoteTime < EmoteCooldown)
	{
		return;
	}
	LastEmoteTime = Now;
	Multicast_PlayEmote(EmoteIndex);
}

void UHitmenEmoteComponent::Multicast_PlayEmote_Implementation(int32 EmoteIndex)
{
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(GetOwner());
	const UHitmenAnimSet* AnimSet = Character ? Character->GetAnimSet() : nullptr;
	if (AnimSet && AnimSet->Emotes.IsValidIndex(EmoteIndex) && AnimSet->Emotes[EmoteIndex].Montage)
	{
		if (UAnimInstance* Anim = Character->GetMesh()->GetAnimInstance())
		{
			Anim->Montage_Play(AnimSet->Emotes[EmoteIndex].Montage);
		}
	}
	Juice_OnEmote(EmoteIndex);
}
