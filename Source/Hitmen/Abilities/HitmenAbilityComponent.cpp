// Hitmen — ability component

#include "Abilities/HitmenAbilityComponent.h"
#include "Abilities/Manipulation/HitmenManipulationAbilities.h"
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
#include "Components/StaticMeshComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "UObject/ConstructorHelpers.h"
#include "GameFramework/Character.h"
#include "GameFramework/PlayerController.h"
#include "Camera/PlayerCameraManager.h"

UHitmenAbilityComponent::UHitmenAbilityComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.bStartWithTickEnabled = true;
	SetIsReplicatedByDefault(true);
	bReplicateUsingRegisteredSubObjectList = true;

	// Hook cable and aim marker: engine shapes and the tintable basic material (override in the Blueprint).
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UMaterialInterface> BasicMaterial(TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial"));
	CableMesh = CylinderMesh.Object;
	AimMarkerMesh = SphereMesh.Object;
	CableMaterial = BasicMaterial.Object;
	AimMarkerMaterial = BasicMaterial.Object;
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
			const UHitmenUIEventSubsystem* KeyUI = UHitmenUIEventSubsystem::GetFromPawn(Cast<APawn>(GetOwner()));
			const EHitmenKeyLabel KeyLabel = Slot == EHitmenAbilitySlot::Manipulation ? EHitmenKeyLabel::Manipulation : EHitmenKeyLabel::MoveAbility;
			Hint.Key = FText::FromString(KeyUI ? KeyUI->GetKeyLabel(KeyLabel) : FString(Slot == EHitmenAbilitySlot::Manipulation ? TEXT("A") : TEXT("E")));
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
	UpdateAbilityVisuals();
	if (bLocal)
	{
		UpdateMarkLinks();
	}
}

UStaticMeshComponent* UHitmenAbilityComponent::MakeVisual(UStaticMesh* Mesh, UMaterialInterface* Material, const FLinearColor& Color)
{
	AActor* Owner = GetOwner();
	if (!Owner || !Mesh)
	{
		return nullptr;
	}
	UStaticMeshComponent* Visual = NewObject<UStaticMeshComponent>(Owner, NAME_None, RF_Transient);
	Visual->SetStaticMesh(Mesh);
	Visual->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Visual->SetCastShadow(false);
	Visual->SetCanEverAffectNavigation(false);
	Visual->SetUsingAbsoluteLocation(true);
	Visual->SetUsingAbsoluteRotation(true);
	Visual->SetUsingAbsoluteScale(true);
	Visual->SetupAttachment(Owner->GetRootComponent());
	Visual->RegisterComponent();
	if (Material)
	{
		if (UMaterialInstanceDynamic* Dynamic = Visual->CreateDynamicMaterialInstance(0, Material))
		{
			Dynamic->SetVectorParameterValue(TEXT("Color"), Color);
		}
	}
	Visual->SetVisibility(false);
	return Visual;
}

void UHitmenAbilityComponent::UpdateAbilityVisuals()
{
	if (GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	const APawn* Pawn = Cast<APawn>(GetOwner());
	if (!Pawn)
	{
		return;
	}

	// Cable: an equipped ability pulling the owner (hook), from the hand to the anchor, thin, on every machine.
	FVector Anchor = FVector::ZeroVector;
	bool bCable = false;
	for (const UHitmenAbility* Ability : { MoveAbility.Get(), ManipulationAbility.Get() })
	{
		if (Ability && Ability->GetCableAnchor(Anchor))
		{
			bCable = true;
			break;
		}
	}
	if (bCable && !CableComponent)
	{
		CableComponent = MakeVisual(CableMesh, CableMaterial, CableColor);
	}
	if (CableComponent)
	{
		CableComponent->SetVisibility(bCable);
		if (bCable)
		{
			FVector Start = Pawn->GetActorLocation();
			const APlayerController* PC = Cast<APlayerController>(Pawn->GetController());
			const ACharacter* Character = Cast<ACharacter>(Pawn);
			if (Pawn->IsLocallyControlled() && PC && PC->PlayerCameraManager)
			{
				// First person: low right in view, like a launcher held in the right hand.
				Start = PC->PlayerCameraManager->GetCameraLocation() + PC->PlayerCameraManager->GetCameraRotation().RotateVector(FirstPersonCableOffset);
			}
			else if (Character && Character->GetMesh() && Character->GetMesh()->DoesSocketExist(CableSocket))
			{
				Start = Character->GetMesh()->GetSocketLocation(CableSocket);
			}
			const FVector Span = Anchor - Start;
			// Engine cylinder: 100 cm tall along Z and 100 cm wide, pivot in the middle.
			CableComponent->SetWorldLocationAndRotation(Start + Span * 0.5f, FRotationMatrix::MakeFromZ(Span.GetSafeNormal()).Rotator());
			CableComponent->SetWorldScale3D(FVector(CableThickness / 100.f, CableThickness / 100.f, Span.Size() / 100.f));
		}
	}

	// Aim marker: the local player's move ability is ready and would land here (hook in range).
	FVector MarkerPoint = FVector::ZeroVector;
	bool bMarker = false;
	if (!bCable && Pawn->IsLocallyControlled() && Pawn->IsPlayerControlled())
	{
		const UHitmenAbility* Move = MoveAbility.Get();
		if (Move && Move->CanActivate())
		{
			FVector Origin, Direction;
			GetAim(Origin, Direction);
			bMarker = Move->GetAimMarker(Origin, Direction, MarkerPoint);
		}
	}
	if (bMarker && !AimMarkerComponent)
	{
		AimMarkerComponent = MakeVisual(AimMarkerMesh, AimMarkerMaterial, AimMarkerColor);
	}
	if (AimMarkerComponent)
	{
		AimMarkerComponent->SetVisibility(bMarker);
		if (bMarker)
		{
			AimMarkerComponent->SetWorldLocation(MarkerPoint);
			AimMarkerComponent->SetWorldScale3D(FVector(AimMarkerSize / 100.f));
		}
	}
}

void UHitmenAbilityComponent::Multicast_AbilityActivated_Implementation(UHitmenAbility* Ability, FVector_NetQuantize Location)
{
	if (Ability)
	{
		Ability->PlayActivatedFX(Location);
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

void UHitmenAbilityComponent::UpdateMarkLinks()
{
	// Multi-kill marks (owner only): a red glow on each marked target and a red line from one to the next.
	TArray<AActor*> Marked;
	if (const UHitmenAbility_MultiKill* MultiKill = Cast<UHitmenAbility_MultiKill>(GetAbility(EHitmenAbilitySlot::Manipulation)))
	{
		for (AHitmenCharacterBase* Target : MultiKill->GetMarkedTargets())
		{
			if (Target && !Target->IsDead())
			{
				Marked.Add(Target);
			}
		}
	}
	if (!MarkHighlightMaterial && HintOverlayMaterial)
	{
		MarkHighlightMaterial = UMaterialInstanceDynamic::Create(HintOverlayMaterial, this);
		MarkHighlightMaterial->SetVectorParameterValue(TEXT("Color"), MarkColor);
	}
	for (int32 Index = MarkHighlighted.Num() - 1; Index >= 0; --Index)
	{
		AActor* Actor = MarkHighlighted[Index].Get();
		if (!Actor || !Marked.Contains(Actor))
		{
			UHitmenInteractionComponent::SetActorHighlight(Actor, MarkHighlightMaterial, false);
			MarkHighlighted.RemoveAt(Index);
		}
	}
	for (AActor* Actor : Marked)
	{
		if (!MarkHighlighted.Contains(Actor))
		{
			UHitmenInteractionComponent::SetActorHighlight(Actor, MarkHighlightMaterial, true);
			MarkHighlighted.Add(Actor);
		}
	}
	const int32 Links = FMath::Max(0, Marked.Num() - 1);
	while (MarkLinks.Num() < Links)
	{
		UStaticMeshComponent* Link = MakeVisual(CableMesh, CableMaterial, MarkColor);
		if (!Link)
		{
			break;
		}
		Link->SetOnlyOwnerSee(true);
		MarkLinks.Add(Link);
	}
	for (int32 Index = 0; Index < MarkLinks.Num(); ++Index)
	{
		UStaticMeshComponent* Link = MarkLinks[Index];
		if (!Link)
		{
			continue;
		}
		const bool bShown = Index < Links;
		Link->SetVisibility(bShown);
		if (!bShown)
		{
			continue;
		}
		const FVector A = Marked[Index]->GetActorLocation() + FVector::UpVector * 40.f;
		const FVector B = Marked[Index + 1]->GetActorLocation() + FVector::UpVector * 40.f;
		const FVector Dir = (B - A).GetSafeNormal();
		const float Length = FVector::Dist(A, B);
		Link->SetWorldLocationAndRotation((A + B) * 0.5f, FRotationMatrix::MakeFromZ(Dir).Rotator());
		Link->SetWorldScale3D(FVector(MarkLinkThickness / 100.f, MarkLinkThickness / 100.f, Length / 100.f));
	}
}
