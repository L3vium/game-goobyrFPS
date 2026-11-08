// Hitmen — status effects

#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Components/HitmenHealthComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "GameFramework/GameStateBase.h"
#include "TimerManager.h"

namespace
{
	constexpr float StatusUpdateInterval = 0.25f;
}

UHitmenStatusEffectComponent::UHitmenStatusEffectComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenStatusEffectComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenStatusEffectComponent, ActiveStatuses);
}

float UHitmenStatusEffectComponent::GetServerTime() const
{
	const UWorld* World = GetWorld();
	const AGameStateBase* GS = World ? World->GetGameState() : nullptr;
	return GS ? GS->GetServerWorldTimeSeconds() : (World ? World->GetTimeSeconds() : 0.f);
}

void UHitmenStatusEffectComponent::ApplyStatus(EHitmenStatusEffect Type, float Duration, float Magnitude, AActor* Instigator)
{
	if (!GetOwner()->HasAuthority() || Type == EHitmenStatusEffect::None)
	{
		return;
	}
	const float EndTime = Duration > 0.f ? GetServerTime() + Duration : -1.f;
	if (FHitmenActiveStatus* Existing = ActiveStatuses.FindByPredicate([Type](const FHitmenActiveStatus& S) { return S.Type == Type; }))
	{
		Existing->EndTime = (Existing->EndTime < 0.f || EndTime < 0.f) ? -1.f : FMath::Max(Existing->EndTime, EndTime);
		Existing->Magnitude = FMath::Max(Existing->Magnitude, Magnitude);
		Existing->Instigator = Instigator ? Instigator : Existing->Instigator.Get();
	}
	else
	{
		FHitmenActiveStatus& Status = ActiveStatuses.AddDefaulted_GetRef();
		Status.Type = Type;
		Status.EndTime = EndTime;
		Status.Magnitude = Magnitude;
		Status.Instigator = Instigator;
	}
	RaiseDiffEvents();

	if (!UpdateTimer.IsValid())
	{
		GetWorld()->GetTimerManager().SetTimer(UpdateTimer, this, &UHitmenStatusEffectComponent::UpdateStatuses, StatusUpdateInterval, true);
	}
}

void UHitmenStatusEffectComponent::RemoveStatus(EHitmenStatusEffect Type)
{
	if (!GetOwner()->HasAuthority())
	{
		return;
	}
	if (ActiveStatuses.RemoveAll([Type](const FHitmenActiveStatus& S) { return S.Type == Type; }) > 0)
	{
		RaiseDiffEvents();
	}
}

void UHitmenStatusEffectComponent::ClearAll()
{
	if (GetOwner()->HasAuthority() && !ActiveStatuses.IsEmpty())
	{
		ActiveStatuses.Reset();
		RaiseDiffEvents();
	}
}

void UHitmenStatusEffectComponent::UpdateStatuses()
{
	const float Now = GetServerTime();

	// Poison damage over time.
	if (HasStatus(EHitmenStatusEffect::Poisoned))
	{
		if (UHitmenHealthComponent* Health = GetOwner()->FindComponentByClass<UHitmenHealthComponent>())
		{
			Health->ApplyDamage(PoisonDamagePerSecond * GetMagnitude(EHitmenStatusEffect::Poisoned) * StatusUpdateInterval,
				GetStatusInstigator(EHitmenStatusEffect::Poisoned), nullptr, false);
		}
	}

	const int32 Removed = ActiveStatuses.RemoveAll([Now](const FHitmenActiveStatus& S) { return S.EndTime >= 0.f && S.EndTime <= Now; });
	if (Removed > 0)
	{
		RaiseDiffEvents();
	}
	if (ActiveStatuses.IsEmpty())
	{
		GetWorld()->GetTimerManager().ClearTimer(UpdateTimer);
	}
}

void UHitmenStatusEffectComponent::OnRep_ActiveStatuses()
{
	RaiseDiffEvents();
}

void UHitmenStatusEffectComponent::RaiseDiffEvents()
{
	TArray<EHitmenStatusEffect> Current;
	for (const FHitmenActiveStatus& Status : ActiveStatuses)
	{
		Current.Add(Status.Type);
	}
	for (EHitmenStatusEffect Type : Current)
	{
		if (!PreviousTypes.Contains(Type))
		{
			OnStatusAdded.Broadcast(Type, GetStatusInstigator(Type));
		}
	}
	for (EHitmenStatusEffect Type : PreviousTypes)
	{
		if (!Current.Contains(Type))
		{
			OnStatusRemoved.Broadcast(Type, nullptr);
		}
	}
	PreviousTypes = MoveTemp(Current);
}

bool UHitmenStatusEffectComponent::HasStatus(EHitmenStatusEffect Type) const
{
	return ActiveStatuses.ContainsByPredicate([Type](const FHitmenActiveStatus& S) { return S.Type == Type; });
}

float UHitmenStatusEffectComponent::GetMagnitude(EHitmenStatusEffect Type) const
{
	const FHitmenActiveStatus* Found = ActiveStatuses.FindByPredicate([Type](const FHitmenActiveStatus& S) { return S.Type == Type; });
	return Found ? Found->Magnitude : 0.f;
}

AActor* UHitmenStatusEffectComponent::GetStatusInstigator(EHitmenStatusEffect Type) const
{
	const FHitmenActiveStatus* Found = ActiveStatuses.FindByPredicate([Type](const FHitmenActiveStatus& S) { return S.Type == Type; });
	return Found ? Found->Instigator.Get() : nullptr;
}

bool UHitmenStatusEffectComponent::IsIncapacitated() const
{
	return HasStatus(EHitmenStatusEffect::Stunned) || HasStatus(EHitmenStatusEffect::Asleep);
}

bool UHitmenStatusEffectComponent::CanMove() const
{
	return !IsIncapacitated() && !HasStatus(EHitmenStatusEffect::Trapped) && !HasStatus(EHitmenStatusEffect::Hidden);
}

bool UHitmenStatusEffectComponent::IsHiddenFromSight() const
{
	return HasStatus(EHitmenStatusEffect::Invisible) || HasStatus(EHitmenStatusEffect::Hidden) || HasStatus(EHitmenStatusEffect::Concealed);
}

float UHitmenStatusEffectComponent::GetSpeedMultiplier() const
{
	if (!CanMove())
	{
		return 0.f;
	}
	float Multiplier = 1.f;
	if (HasStatus(EHitmenStatusEffect::Boosted))
	{
		Multiplier += BoostSpeedBonus * GetMagnitude(EHitmenStatusEffect::Boosted);
	}
	return Multiplier;
}

float UHitmenStatusEffectComponent::GetOutgoingDamageMultiplier() const
{
	return HasStatus(EHitmenStatusEffect::Boosted) ? 1.f + BoostDamageBonus * GetMagnitude(EHitmenStatusEffect::Boosted) : 1.f;
}

float UHitmenStatusEffectComponent::GetIncomingDamageMultiplier() const
{
	float Multiplier = 1.f;
	if (HasStatus(EHitmenStatusEffect::Boosted))
	{
		Multiplier *= 1.f - BoostResistance;
	}
	if (HasStatus(EHitmenStatusEffect::Armored))
	{
		Multiplier *= 1.f - ArmoredResistance;
	}
	return Multiplier;
}
