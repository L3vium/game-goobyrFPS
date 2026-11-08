// Hitmen — interaction component

#include "Interaction/HitmenInteractionComponent.h"
#include "Characters/HitmenNPC.h"
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
#include "Components/MeshComponent.h"
#include "Materials/MaterialInterface.h"

UHitmenInteractionComponent::UHitmenInteractionComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenInteractionComponent::BeginPlay()
{
	Super::BeginPlay();
	// A respawned pawn begins play before it is possessed, so the trace cannot wait for local
	// control here: it always runs (cheap, 12 Hz) and does nothing unless the pawn is the local
	// player's (UpdateFocus).
	StartFocusTrace();
}

void UHitmenInteractionComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	StopFocusTrace();
	SetFocus(nullptr, FText::GetEmpty());
	Super::EndPlay(EndPlayReason);
}

void UHitmenInteractionComponent::StartFocusTrace()
{
	if (UWorld* World = GetWorld(); World && World->GetNetMode() != NM_DedicatedServer && !World->GetTimerManager().IsTimerActive(TraceTimer))
	{
		World->GetTimerManager().SetTimer(TraceTimer, this, &UHitmenInteractionComponent::UpdateFocus, TraceInterval, true);
	}
}

void UHitmenInteractionComponent::StopFocusTrace()
{
	if (UWorld* World = GetWorld())
	{
		World->GetTimerManager().ClearTimer(TraceTimer);
	}
}

void UHitmenInteractionComponent::SetActorHighlight(AActor* Actor, UMaterialInterface* OverlayMaterial, bool bEnabled)
{
	if (!Actor || !OverlayMaterial)
	{
		return;
	}
	TInlineComponentArray<UMeshComponent*> Meshes(Actor);
	for (UMeshComponent* Mesh : Meshes)
	{
		if (!Mesh || !Mesh->IsVisible())
		{
			continue;
		}
		if (bEnabled)
		{
			Mesh->SetOverlayMaterial(OverlayMaterial);
		}
		else if (Mesh->GetOverlayMaterial() == OverlayMaterial)
		{
			Mesh->SetOverlayMaterial(nullptr);
		}
	}
}

void UHitmenInteractionComponent::UpdateFocus()
{
	APawn* Pawn = Cast<APawn>(GetOwner());
	if (!Pawn || !Pawn->IsLocallyControlled() || !Pawn->IsPlayerControlled())
	{
		// Not (or no longer) the local player's pawn: nothing focused, no prompt.
		if (FocusedActor.IsValid() || !FocusedPrompt.IsEmpty())
		{
			SetFocus(nullptr, FText::GetEmpty());
		}
		return;
	}
	if (const AHitmenCharacterBase* Self = Cast<AHitmenCharacterBase>(Pawn); Self && Self->IsDead())
	{
		SetFocus(nullptr, FText::GetEmpty());
		return;
	}

	// Stealth kill takes priority (a keycard carrier is pickpocketed first: the kill comes after).
	if (const UHitmenMeleeComponent* Melee = Pawn->FindComponentByClass<UHitmenMeleeComponent>())
	{
		if (AActor* Victim = Melee->FindStealthKillTarget())
		{
			const AHitmenNPC* NPC = Cast<AHitmenNPC>(Victim);
			if (NPC && NPC->CanBePickpocketedBy(Pawn))
			{
				SetFocus(Victim, IHitmenInteractable::Execute_GetInteractionText(Victim, Pawn));
				return;
			}
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
	if (HighlightedActor.Get() != NewFocus)
	{
		SetActorHighlight(HighlightedActor.Get(), FocusOverlayMaterial, false);
		// Bodies and NPCs (stealth kill, carry, pickpocket) glow too: they are what F acts on.
		SetActorHighlight(NewFocus, FocusOverlayMaterial, true);
		HighlightedActor = NewFocus;
	}
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
	if (const AHitmenNPC* NPC = Cast<AHitmenNPC>(FocusedActor.Get()); NPC && NPC->CanBePickpocketedBy(Pawn))
	{
		Server_Interact(FocusedActor.Get());
		return true;
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
		// One interact montage for every interactable (doors, pickups, bodies, consoles...).
		if (AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Pawn))
		{
			Character->PlayAnimSetMontage(EHitmenAnimMontage::Interact);
		}
	}
}
