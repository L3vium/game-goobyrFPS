// Hitmen — ability component

#include "Abilities/HitmenAbilityComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Abilities/HitmenAbility.h"
#include "Data/HitmenAbilityData.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Interaction/HitmenInteractionComponent.h"
#include "Interaction/HitmenInteractable.h"
#include "Hitmen.h"
#include "Net/UnrealNetwork.h"
#include "GameFramework/Pawn.h"
#include "Engine/World.h"
#include "TimerManager.h"

UHitmenAbilityComponent::UHitmenAbilityComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.bStartWithTickEnabled = true;
	SetIsReplicatedByDefault(true);
	bReplicateUsingRegisteredSubObjectList = true;
}

void UHitmenAbilityComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAbilityComponent, MoveAbility);
	DOREPLIFETIME(UHitmenAbilityComponent, ManipulationAbility);
}

UHitmenAbility* UHitmenAbilityComponent::GetAbility(EHitmenAbilitySlot Slot) const
{
	return Slot == EHitmenAbilitySlot::Move ? MoveAbility.Get() : ManipulationAbility.Get();
}

void UHitmenAbilityComponent::SetAbility(UHitmenAbilityData* AbilityData)
{
	if (!GetOwner()->HasAuthority() || !AbilityData || !AbilityData->AbilityClass)
	{
		return;
	}
	TObjectPtr<UHitmenAbility>& SlotRef = AbilityData->Slot == EHitmenAbilitySlot::Move ? MoveAbility : ManipulationAbility;
	if (SlotRef)
	{
		SlotRef->OnUnequipped();
		RemoveReplicatedSubObject(SlotRef);
	}
	UHitmenAbility* NewAbility = NewObject<UHitmenAbility>(GetOwner(), AbilityData->AbilityClass);
	if (NewAbility->GetSlot() != AbilityData->Slot)
	{
		UE_LOG(LogHitmen, Warning, TEXT("%s: data slot does not match the ability family of %s (fix the data asset)."),
			*GetNameSafe(AbilityData), *GetNameSafe(AbilityData->AbilityClass.Get()));
	}
	NewAbility->InitAbility(this, AbilityData);
	AddReplicatedSubObject(NewAbility);
	SlotRef = NewAbility;
	NewAbility->OnEquipped();
	OnRep_Abilities();
}

void UHitmenAbilityComponent::OnRep_Abilities()
{
	// Clients: run equip/unequip side effects for changes.
	TArray<UHitmenAbility*> Current;
	if (MoveAbility) { Current.Add(MoveAbility); }
	if (ManipulationAbility) { Current.Add(ManipulationAbility); }

	if (!GetOwner()->HasAuthority())
	{
		for (UHitmenAbility* Old : KnownAbilities)
		{
			if (Old && !Current.Contains(Old))
			{
				Old->OnUnequipped();
			}
		}
		for (UHitmenAbility* New : Current)
		{
			if (!KnownAbilities.Contains(New))
			{
				New->SetOwnerComponent(this);
				New->OnEquipped();
			}
		}
	}
	KnownAbilities.Reset();
	for (UHitmenAbility* Ability : Current)
	{
		KnownAbilities.Add(Ability);
	}
	OnAbilitiesChanged.Broadcast();
}

void UHitmenAbilityComponent::BeginPlay()
{
	Super::BeginPlay();
	// Local player only (checked each scan: a respawned pawn is possessed after BeginPlay).
	if (GetNetMode() != NM_DedicatedServer)
	{
		GetWorld()->GetTimerManager().SetTimer(HintTimer, this, &UHitmenAbilityComponent::UpdateTargetHint, HintScanInterval, true);
	}
}

void UHitmenAbilityComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	GetWorld()->GetTimerManager().ClearTimer(HintTimer);
	ClearTargetHint();
	Super::EndPlay(EndPlayReason);
}

void UHitmenAbilityComponent::ClearTargetHint()
{
	UHitmenInteractionComponent::SetActorHighlight(HintedActor.Get(), HintOverlayMaterial, false);
	HintedActor.Reset();
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(Cast<APawn>(GetOwner())))
	{
		UI->SetAbilityHint(FHitmenWorldHint());
	}
}

void UHitmenAbilityComponent::UpdateTargetHint()
{
	const APawn* Pawn = Cast<APawn>(GetOwner());
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Pawn);
	if (!Pawn || !Pawn->IsLocallyControlled() || !Pawn->IsPlayerControlled() || (Character && Character->IsDead()))
	{
		if (HintedActor.IsValid())
		{
			ClearTargetHint();
		}
		return;
	}
	FVector Origin, Direction;
	GetAim(Origin, Direction);
	FHitmenWorldHint Hint;
	// Manipulation (A) first, then move (E).
	for (const EHitmenAbilitySlot Slot : { EHitmenAbilitySlot::Manipulation, EHitmenAbilitySlot::Move })
	{
		const UHitmenAbility* Ability = GetAbility(Slot);
		FText Action;
		AActor* Target = Ability && Ability->CanActivate() ? Ability->FindHintTarget(Origin, Direction, Action) : nullptr;
		if (Target)
		{
			Hint.Target = Target;
			Hint.Action = Action;
			Hint.Key = Slot == EHitmenAbilitySlot::Manipulation ? NSLOCTEXT("Hitmen", "KeyManipulation", "A") : NSLOCTEXT("Hitmen", "KeyMove", "E");
			break;
		}
	}
	if (HintedActor.Get() != Hint.Target.Get())
	{
		UHitmenInteractionComponent::SetActorHighlight(HintedActor.Get(), HintOverlayMaterial, false);
		UHitmenInteractionComponent::SetActorHighlight(Hint.Target.Get(), HintOverlayMaterial, true);
		HintedActor = Hint.Target;
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(Pawn))
	{
		UI->SetAbilityHint(Hint);
	}
}

void UHitmenAbilityComponent::GetAim(FVector& OutOrigin, FVector& OutDirection) const
{
	UHitmenCombatLibrary::GetAimPoint(Cast<APawn>(GetOwner()), OutOrigin, OutDirection);
}

void UHitmenAbilityComponent::PressSlot(EHitmenAbilitySlot Slot)
{
	UHitmenAbility* Ability = GetAbility(Slot);
	if (!Ability)
	{
		return;
	}
	FVector Origin, Direction;
	GetAim(Origin, Direction);
	if (Ability->CanActivate() && !GetOwner()->HasAuthority())
	{
		Ability->LocalPress(Origin, Direction);
	}
	Server_Press(Slot, Origin, Direction);
}

void UHitmenAbilityComponent::ReleaseSlot(EHitmenAbilitySlot Slot)
{
	UHitmenAbility* Ability = GetAbility(Slot);
	if (!Ability)
	{
		return;
	}
	FVector Origin, Direction;
	GetAim(Origin, Direction);
	if (!GetOwner()->HasAuthority())
	{
		Ability->LocalRelease();
	}
	Server_Release(Slot, Origin, Direction);
}

void UHitmenAbilityComponent::Server_Press_Implementation(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction)
{
	if (UHitmenAbility* Ability = GetAbility(Slot))
	{
		const FVector SafeOrigin = FVector::Dist(Origin, GetOwner()->GetActorLocation()) < 400.f ? FVector(Origin) : GetOwner()->GetActorLocation();
		if (GetOwner()->HasAuthority() && Cast<APawn>(GetOwner())->IsLocallyControlled())
		{
			// Listen-server host: run the local half too.
			Ability->LocalPress(SafeOrigin, Direction);
		}
		Ability->ServerPress(SafeOrigin, Direction);
	}
}

void UHitmenAbilityComponent::Server_Release_Implementation(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction)
{
	if (UHitmenAbility* Ability = GetAbility(Slot))
	{
		const FVector SafeOrigin = FVector::Dist(Origin, GetOwner()->GetActorLocation()) < 400.f ? FVector(Origin) : GetOwner()->GetActorLocation();
		if (GetOwner()->HasAuthority() && Cast<APawn>(GetOwner())->IsLocallyControlled())
		{
			Ability->LocalRelease();
		}
		Ability->ServerRelease(SafeOrigin, Direction);
	}
}

void UHitmenAbilityComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	const APawn* Pawn = Cast<APawn>(GetOwner());
	const bool bAuthority = GetOwner()->HasAuthority();
	const bool bLocal = Pawn && Pawn->IsLocallyControlled();
	for (UHitmenAbility* Ability : { MoveAbility.Get(), ManipulationAbility.Get() })
	{
		if (!Ability || !Ability->WantsTick())
		{
			continue;
		}
		if (bAuthority)
		{
			Ability->TickAbility(DeltaTime);
		}
		if (bLocal && !bAuthority)
		{
			Ability->LocalTick(DeltaTime);
		}
	}
}

void UHitmenAbilityComponent::Multicast_AbilityActivated_Implementation(UHitmenAbility* Ability, FVector_NetQuantize Location)
{
	if (Ability)
	{
		Ability->Juice_OnActivated(Location);
	}
}

void UHitmenAbilityComponent::Client_AbilityFailed_Implementation(UHitmenAbility* Ability, const FText& Reason)
{
	if (Ability)
	{
		Ability->Juice_OnFailed(Reason);
	}
	OnAbilityFailed.Broadcast(Ability, Reason);

	if (!bShowFailureAsPrompt || Reason.IsEmpty())
	{
		return;
	}
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(Cast<APawn>(GetOwner())))
	{
		UI->BroadcastPrompt(Reason, true);
		GetWorld()->GetTimerManager().SetTimer(FailurePromptTimer, this, &UHitmenAbilityComponent::ClearFailurePrompt, FailurePromptDuration, false);
	}
}

void UHitmenAbilityComponent::ClearFailurePrompt()
{
	APawn* Pawn = Cast<APawn>(GetOwner());
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(Pawn);
	if (!UI)
	{
		return;
	}
	// Give the prompt back to whatever the player is focusing.
	const UHitmenInteractionComponent* Interaction = Pawn ? Pawn->FindComponentByClass<UHitmenInteractionComponent>() : nullptr;
	AActor* Focused = Interaction ? Interaction->GetFocusedActor() : nullptr;
	if (Focused && Focused->GetClass()->ImplementsInterface(UHitmenInteractable::StaticClass()))
	{
		UI->BroadcastPrompt(IHitmenInteractable::Execute_GetInteractionText(Focused, Pawn), true);
		return;
	}
	UI->BroadcastPrompt(FText::GetEmpty(), false);
}
