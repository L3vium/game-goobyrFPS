// Hitmen — emotes

#include "Components/HitmenEmoteComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Data/HitmenAnimSet.h"
#include "Animation/AnimInstance.h"
#include "Animation/AnimMontage.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "Net/UnrealNetwork.h"

UHitmenEmoteComponent::UHitmenEmoteComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenEmoteComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenEmoteComponent, ActiveEmote);
}

void UHitmenEmoteComponent::PlayEmote(int32 EmoteIndex)
{
	if (EmoteIndex >= 0)
	{
		Server_PlayEmote(EmoteIndex);
	}
}

void UHitmenEmoteComponent::StopEmote()
{
	if (IsEmoting())
	{
		Server_StopEmote();
	}
}

UAnimMontage* UHitmenEmoteComponent::GetEmoteMontage(int32 EmoteIndex) const
{
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(GetOwner());
	const UHitmenAnimSet* AnimSet = Character ? Character->GetAnimSet() : nullptr;
	return AnimSet && AnimSet->Emotes.IsValidIndex(EmoteIndex) ? AnimSet->Emotes[EmoteIndex].Montage.Get() : nullptr;
}

void UHitmenEmoteComponent::Server_PlayEmote_Implementation(int32 EmoteIndex)
{
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(GetOwner());
	const float Now = GetWorld()->GetTimeSeconds();
	if (!Character || Character->IsDead() || Now - LastEmoteTime < EmoteCooldown || !GetEmoteMontage(EmoteIndex))
	{
		return;
	}
	LastEmoteTime = Now;
	// A new pick while one runs restarts on the new one.
	if (ActiveEmote != INDEX_NONE)
	{
		ActiveEmote = INDEX_NONE;
		OnRep_ActiveEmote();
	}
	ActiveEmote = EmoteIndex;
	OnRep_ActiveEmote();
	GetWorld()->GetTimerManager().ClearTimer(EndTimer);
	if (!bLoopEmotes)
	{
		// Played once: the emote ends with its montage.
		const float Length = GetEmoteMontage(EmoteIndex)->GetPlayLength();
		GetWorld()->GetTimerManager().SetTimer(EndTimer, this, &UHitmenEmoteComponent::ServerStopEmote, FMath::Max(Length, 0.1f), false);
	}
}

void UHitmenEmoteComponent::Server_StopEmote_Implementation()
{
	ServerStopEmote();
}

void UHitmenEmoteComponent::ServerStopEmote()
{
	if (GetOwner() && GetOwner()->HasAuthority() && ActiveEmote != INDEX_NONE)
	{
		GetWorld()->GetTimerManager().ClearTimer(EndTimer);
		ActiveEmote = INDEX_NONE;
		OnRep_ActiveEmote();
	}
}

void UHitmenEmoteComponent::OnRep_ActiveEmote()
{
	ApplyEmote();
}

void UHitmenEmoteComponent::ApplyEmote()
{
	if (AppliedEmote == ActiveEmote)
	{
		return;
	}
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(GetOwner());
	UAnimInstance* Anim = Character && Character->GetMesh() ? Character->GetMesh()->GetAnimInstance() : nullptr;
	// Stop what ran before (cleared first: its end notification must not loop it again).
	UAnimMontage* Previous = PlayingMontage;
	PlayingMontage = nullptr;
	if (Anim && Previous)
	{
		Anim->Montage_Stop(StopBlendOutTime, Previous);
	}
	// An emote is played on the spot: whatever root motion its animation carries is ignored while it runs.
	if (Anim)
	{
		Anim->SetRootMotionMode(ActiveEmote != INDEX_NONE ? ERootMotionMode::IgnoreRootMotion : ERootMotionMode::RootMotionFromMontagesOnly);
	}
	AppliedEmote = ActiveEmote;
	if (ActiveEmote != INDEX_NONE)
	{
		PlayMontageNow();
		Juice_OnEmote(ActiveEmote);
	}
	else
	{
		Juice_OnEmoteEnded();
	}
	OnEmoteChanged.Broadcast(ActiveEmote);
}

void UHitmenEmoteComponent::PlayMontageNow()
{
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(GetOwner());
	UAnimInstance* Anim = Character && Character->GetMesh() ? Character->GetMesh()->GetAnimInstance() : nullptr;
	UAnimMontage* Montage = GetEmoteMontage(AppliedEmote);
	if (!Anim || !Montage)
	{
		return;
	}
	if (Anim->Montage_Play(Montage) > 0.f)
	{
		PlayingMontage = Montage;
		FOnMontageEnded Ended;
		Ended.BindUObject(this, &UHitmenEmoteComponent::HandleMontageEnded);
		Anim->Montage_SetEndDelegate(Ended, Montage);
	}
}

void UHitmenEmoteComponent::HandleMontageEnded(UAnimMontage* Montage, bool bInterrupted)
{
	if (Montage != PlayingMontage)
	{
		return;
	}
	PlayingMontage = nullptr;
	if (bInterrupted)
	{
		// Another montage took over (a hit, a punch): the emote is over.
		ServerStopEmote();
		return;
	}
	// Still emoting: play it again (every machine loops its own copy, nothing goes over the network).
	if (bLoopEmotes && AppliedEmote != INDEX_NONE)
	{
		PlayMontageNow();
	}
}
