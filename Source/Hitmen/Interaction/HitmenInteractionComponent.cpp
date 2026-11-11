// Hitmen — interaction component

#include "Interaction/HitmenInteractionComponent.h"
#include "Data/HitmenSoundSet.h"
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
		FocusLostTime = -1.f;
		SetFocus(Candidate, IHitmenInteractable::Execute_GetInteractionText(Candidate, Pawn));
		return;
	}
	// The sweep grazing a frame / the wall around it for a frame must not make the prompt blink.
	if (AActor* Current = FocusedActor.Get(); Current && Current->GetClass()->ImplementsInterface(UHitmenInteractable::StaticClass())
		&& IHitmenInteractable::Execute_CanInteract(Current, Pawn) && FVector::Dist(Current->GetActorLocation(), Origin) < TraceDistance + 150.f)
	{
		const float Now = GetWorld()->GetTimeSeconds();
		if (FocusLostTime < 0.f)
		{
			FocusLostTime = Now;
		}
		if (Now - FocusLostTime < FocusGraceTime)
		{
			return;
		}
	}
	FocusLostTime = -1.f;

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
		const float Hold = Target->GetClass()->ImplementsInterface(UHitmenInteractable::StaticClass())
			? IHitmenInteractable::Execute_GetInteractHoldTime(Target, Pawn) : 0.f;
		if (Hold > 0.f)
		{
			BeginHold(Target, Hold);
		}
		else
		{
			Server_Interact(Target);
		}
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
	// Held interactions (hacking) only go through after the hold time, timed on the server too.
	const float Hold = IHitmenInteractable::Execute_GetInteractHoldTime(Target, Pawn);
	if (Hold > 0.f)
	{
		const bool bHeldLongEnough = ServerHoldTarget.Get() == Target && GetWorld()->GetTimeSeconds() - ServerHoldStartTime >= Hold * 0.85f;
		ServerHoldTarget.Reset();
		if (!bHeldLongEnough)
		{
			return;
		}
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

// ---------------------------------------------------------------------------------------------
// Hold to interact
// ---------------------------------------------------------------------------------------------

void UHitmenInteractionComponent::BeginHold(AActor* Target, float Duration)
{
	HoldTarget = Target;
	HoldStartTime = GetWorld()->GetTimeSeconds();
	HoldDuration = Duration;
	Server_BeginHold(Target);
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::HackLoop);
	GetWorld()->GetTimerManager().SetTimer(HoldTimer, this, &UHitmenInteractionComponent::UpdateHold, 0.03f, true);
	UpdateHold();
}

void UHitmenInteractionComponent::UpdateHold()
{
	AActor* Target = HoldTarget.Get();
	// Looking away (or the target going away) cancels the hold.
	if (!Target || FocusedActor.Get() != Target)
	{
		EndHold(false);
		return;
	}
	const float Progress = FMath::Clamp((GetWorld()->GetTimeSeconds() - HoldStartTime) / FMath::Max(0.01f, HoldDuration), 0.f, 1.f);
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(Cast<APawn>(GetOwner())))
	{
		UI->SetInteractProgress(Progress);
	}
	if (Progress >= 1.f)
	{
		Server_Interact(Target);
		UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::HackComplete);
		EndHold(true);
	}
}

void UHitmenInteractionComponent::EndHold(bool bCompleted)
{
	HoldTarget.Reset();
	GetWorld()->GetTimerManager().ClearTimer(HoldTimer);
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::GetFromPawn(Cast<APawn>(GetOwner())))
	{
		UI->SetInteractProgress(-1.f);
	}
}

void UHitmenInteractionComponent::ReleaseInteract()
{
	if (HoldTarget.IsValid())
	{
		EndHold(false);
	}
}

void UHitmenInteractionComponent::Server_BeginHold_Implementation(AActor* Target)
{
	ServerHoldTarget = Target;
	ServerHoldStartTime = GetWorld()->GetTimeSeconds();
}
