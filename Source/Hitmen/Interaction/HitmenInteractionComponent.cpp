// Hitmen — interaction component

#include "Interaction/HitmenInteractionComponent.h"
#include "Interaction/HitmenInteractable.h"
#include "Components/HitmenMeleeComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Hitmen.h"
#include "GameFramework/Pawn.h"
#include "Engine/World.h"
#include "TimerManager.h"

UHitmenInteractionComponent::UHitmenInteractionComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenInteractionComponent::BeginPlay()
{
	Super::BeginPlay();
	const APawn* Pawn = Cast<APawn>(GetOwner());
	if (Pawn && Pawn->IsLocallyControlled())
	{
		GetWorld()->GetTimerManager().SetTimer(TraceTimer, this, &UHitmenInteractionComponent::UpdateFocus, TraceInterval, true);
	}
}

void UHitmenInteractionComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	GetWorld()->GetTimerManager().ClearTimer(TraceTimer);
	SetFocus(nullptr, FText::GetEmpty());
	Super::EndPlay(EndPlayReason);
}

void UHitmenInteractionComponent::UpdateFocus()
{
	APawn* Pawn = Cast<APawn>(GetOwner());
	if (!Pawn || !Pawn->IsLocallyControlled())
	{
		return;
	}

	// Stealth kill takes priority.
	if (const UHitmenMeleeComponent* Melee = Pawn->FindComponentByClass<UHitmenMeleeComponent>())
	{
		if (AActor* Victim = Melee->FindStealthKillTarget())
		{
			SetFocus(Victim, NSLOCTEXT("Hitmen", "Eliminate", "Eliminate"));
			return;
		}
	}

	FVector Origin, Direction;
	UHitmenCombatLibrary::GetAimPoint(Pawn, Origin, Direction);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenInteract), false, Pawn);
	FHitResult Hit;
	AActor* Candidate = nullptr;
	if (GetWorld()->SweepSingleByChannel(Hit, Origin, Origin + Direction * TraceDistance, FQuat::Identity, HITMEN_TRACE_INTERACT, FCollisionShape::MakeSphere(TraceRadius), Params))
	{
		Candidate = Hit.GetActor();
	}
	if (Candidate && Candidate->GetClass()->ImplementsInterface(UHitmenInteractable::StaticClass())
		&& IHitmenInteractable::Execute_CanInteract(Candidate, Pawn))
	{
		SetFocus(Candidate, IHitmenInteractable::Execute_GetInteractionText(Candidate, Pawn));
		return;
	}

	// Carrying a body with nothing focused: F drops it.
	if (const UHitmenBodyCarryComponent* Carry = Pawn->FindComponentByClass<UHitmenBodyCarryComponent>(); Carry && Carry->IsCarrying())
	{
		SetFocus(nullptr, NSLOCTEXT("Hitmen", "DropBody", "Drop body"));
		return;
	}
	SetFocus(nullptr, FText::GetEmpty());
}

void UHitmenInteractionComponent::SetFocus(AActor* NewFocus, const FText& Prompt)
{
	if (FocusedActor.Get() == NewFocus && FocusedPrompt.EqualTo(Prompt))
	{
		return;
	}
	FocusedActor = NewFocus;
	FocusedPrompt = Prompt;
	OnFocusChanged.Broadcast(NewFocus, Prompt);
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(Cast<APawn>(GetOwner())))
	{
		UI->BroadcastPrompt(Prompt, !Prompt.IsEmpty());
	}
}

bool UHitmenInteractionComponent::TryInteract()
{
	APawn* Pawn = Cast<APawn>(GetOwner());
	if (!Pawn)
	{
		return false;
	}
	if (UHitmenMeleeComponent* Melee = Pawn->FindComponentByClass<UHitmenMeleeComponent>())
	{
		if (Melee->TryStealthKill())
		{
			return true;
		}
	}
	if (AActor* Target = FocusedActor.Get())
	{
		Server_Interact(Target);
		return true;
	}
	if (UHitmenBodyCarryComponent* Carry = Pawn->FindComponentByClass<UHitmenBodyCarryComponent>(); Carry && Carry->IsCarrying())
	{
		Carry->RequestDrop();
		return true;
	}
	return false;
}

void UHitmenInteractionComponent::Server_Interact_Implementation(AActor* Target)
{
	APawn* Pawn = Cast<APawn>(GetOwner());
	if (!Pawn || !Target || !Target->GetClass()->ImplementsInterface(UHitmenInteractable::StaticClass()))
	{
		return;
	}
	if (FVector::Dist(Pawn->GetActorLocation(), Target->GetActorLocation()) > TraceDistance + ServerDistanceTolerance + Target->GetSimpleCollisionRadius())
	{
		return;
	}
	if (IHitmenInteractable::Execute_CanInteract(Target, Pawn))
	{
		IHitmenInteractable::Execute_Interact(Target, Pawn);
	}
}
