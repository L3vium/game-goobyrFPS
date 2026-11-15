// Hitmen — hiding spots

#include "World/Interactables/HitmenHidingSpot.h"
#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Data/HitmenAnimSet.h"
#include "Data/HitmenSoundSet.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "Camera/PlayerCameraManager.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

AHitmenHidingSpot::AHitmenHidingSpot()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	bReplicates = true;
	InteractionText = NSLOCTEXT("Hitmen", "Hide", "Hide");
}

void AHitmenHidingSpot::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenHidingSpot, Occupant);
	DOREPLIFETIME(AHitmenHidingSpot, Bodies);
}

void AHitmenHidingSpot::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	// Never leave someone's view locked to an arc.
	if (AHitmenCharacterBase* Inside = AppliedOccupant.Get())
	{
		SetViewLimits(Inside, false);
	}
	Super::EndPlay(EndPlayReason);
}

bool AHitmenHidingSpot::CanStowBody(const AHitmenCharacterBase* Body) const
{
	return Body && !Body->IsVIP() && HasRoomForBody();
}

bool AHitmenHidingSpot::CanHide(const AActor* Who) const
{
	return bCanHideInside && !Occupant && Who && !(bBlockHidingWhenBodyInside && Bodies.Num() > 0);
}

bool AHitmenHidingSpot::CanInteract_Implementation(AActor* Interactor) const
{
	if (!Super::CanInteract_Implementation(Interactor) || !Interactor)
	{
		return false;
	}
	if (Occupant)
	{
		// Only the one inside can use it (to get out).
		return Occupant == Interactor;
	}
	// Carrying a body: F stows it (or nothing, when it does not fit). Otherwise F hides you, or opens it to look at the
	// bodies inside when there is no room to hide.
	if (const UHitmenBodyCarryComponent* Carry = Interactor->FindComponentByClass<UHitmenBodyCarryComponent>(); Carry && Carry->IsCarrying())
	{
		return CanStowBody(Carry->GetCarriedBody());
	}
	return CanHide(Interactor) || Bodies.Num() > 0;
}

FText AHitmenHidingSpot::GetInteractionText_Implementation(AActor* Interactor) const
{
	if (Occupant && Occupant == Interactor)
	{
		return NSLOCTEXT("Hitmen", "LeaveHidingSpot", "Get out");
	}
	const UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	if (Carry && Carry->IsCarrying())
	{
		return NSLOCTEXT("Hitmen", "HideBody", "Hide body");
	}
	return CanHide(Interactor) ? InteractionText : NSLOCTEXT("Hitmen", "LookInside", "Look inside");
}

FText AHitmenHidingSpot::GetInteractionInfo_Implementation(AActor* Interactor) const
{
	// "Body container  1 / 2 bodies", plus why it cannot be used right now.
	FText State;
	const UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	const AHitmenCharacterBase* Carried = Carry ? Carry->GetCarriedBody() : nullptr;
	if (Carried && Carried->IsVIP())
	{
		State = NSLOCTEXT("Hitmen", "SpotTarget", "the target can't be hidden");
	}
	else if (Carried && !HasRoomForBody())
	{
		State = NSLOCTEXT("Hitmen", "SpotFull", "full");
	}
	else if (!Carried && Occupant && Occupant != Interactor)
	{
		State = NSLOCTEXT("Hitmen", "SpotTaken", "someone is inside");
	}
	else if (!Carried && !Occupant && bCanHideInside && bBlockHidingWhenBodyInside && Bodies.Num() > 0)
	{
		State = NSLOCTEXT("Hitmen", "SpotNoRoom", "no room to hide");
	}
	const FText Name = DisplayName.IsEmpty() ? NSLOCTEXT("Hitmen", "HidingSpot", "Hiding spot") : DisplayName;
	if (BodyCapacity <= 0)
	{
		return State.IsEmpty() ? Name : FText::Format(NSLOCTEXT("Hitmen", "SpotInfoState", "{0}  -  {1}"), Name, State);
	}
	const FText Count = FText::Format(NSLOCTEXT("Hitmen", "SpotBodies", "{0} / {1} {1}|plural(one=body,other=bodies)"), Bodies.Num(), BodyCapacity);
	return State.IsEmpty()
		? FText::Format(NSLOCTEXT("Hitmen", "SpotInfo", "{0}  -  {1}"), Name, Count)
		: FText::Format(NSLOCTEXT("Hitmen", "SpotInfoFull", "{0}  -  {1}  -  {2}"), Name, Count, State);
}

FTransform AHitmenHidingSpot::GetBodySlotTransform(int32 Index) const
{
	if (BodySlots.Num() == 0)
	{
		return FTransform(FRotator(0.f, 0.f, 90.f), FVector(0.f, 0.f, 30.f + 22.f * Index));
	}
	if (BodySlots.IsValidIndex(Index))
	{
		return BodySlots[Index];
	}
	// More bodies than slots: stacked on the last slot.
	FTransform Last = BodySlots.Last();
	Last.AddToTranslation(FVector(0.f, 0.f, 22.f * (Index - BodySlots.Num() + 1)));
	return Last;
}

bool AHitmenHidingSpot::StowCarriedBody(AActor* Carrier)
{
	UHitmenBodyCarryComponent* Carry = Carrier ? Carrier->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	AHitmenCharacterBase* Body = Carry ? Carry->GetCarriedBody() : nullptr;
	if (!HasAuthority() || !CanStowBody(Body))
	{
		return false;
	}
	Carry->Drop();
	Body->SetStowedIn(this, Bodies.Num());
	Bodies.Add(Body);
	Multicast_OpenCycle();
	Juice_OnBodyStowed(Bodies.Num());
	return true;
}

void AHitmenHidingSpot::OnInteracted(AActor* Interactor)
{
	AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Interactor);
	if (!Character)
	{
		return;
	}
	if (Occupant == Character)
	{
		Character->GetStatusEffects()->RemoveStatus(EHitmenStatusEffect::Hidden);
		Occupant = nullptr;
		OnRep_Occupant();
		return;
	}
	if (const UHitmenBodyCarryComponent* Carry = Character->FindComponentByClass<UHitmenBodyCarryComponent>(); Carry && Carry->IsCarrying())
	{
		StowCarriedBody(Character);
		return;
	}
	if (CanHide(Character))
	{
		Occupant = Character;
		Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Hidden, 0.f, 1.f, this);
		OnRep_Occupant();
	}
	else if (Bodies.Num() > 0)
	{
		// No room to hide: it opens and closes again, showing the bodies inside.
		Multicast_OpenCycle();
	}
}

void AHitmenHidingSpot::OnRep_Occupant()
{
	if (AHitmenCharacterBase* Previous = AppliedOccupant.Get(); Previous && Previous != Occupant)
	{
		ApplyOccupant(Previous, false);
	}
	if (Occupant && AppliedOccupant.Get() != Occupant)
	{
		ApplyOccupant(Occupant, true);
	}
	AppliedOccupant = Occupant;
	StartOpenCycle();
	Juice_OnOccupantChanged(Occupant != nullptr);
}

void AHitmenHidingSpot::Multicast_OpenCycle_Implementation()
{
	StartOpenCycle();
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::ContainerOpen, GetActorLocation());
}

void AHitmenHidingSpot::ApplyOccupant(AHitmenCharacterBase* Character, bool bInside)
{
	UCharacterMovementComponent* Movement = Character->GetCharacterMovement();
	// It opens, the character steps in (or out) turning to face out, it closes: UpdateStep moves it on every machine
	// while the opening cycles.
	const FRotator Facing(0.f, GetActorRotation().Yaw, 0.f);
	Character->MoveIgnoreActorAdd(this);
	Movement->StopMovementImmediately();
	Movement->DisableMovement();
	SteppingCharacter = Character;
	bSteppingIn = bInside;
	StepFrom = Character->GetActorLocation();
	StepTo = GetActorTransform().TransformPosition(bInside ? InsideOffset : ExitOffset);
	StepFromRotation = Character->GetControlRotation();
	StepToRotation = Facing;
	if (!bInside)
	{
		SetViewLimits(Character, false);
	}
	Character->PlayCosmeticMontage(Character->GetAnimSet() ? Character->GetAnimSet()->GetMontage(bInside ? EHitmenAnimMontage::LockerEnter : EHitmenAnimMontage::LockerExit) : nullptr);
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, bInside ? EHitmenSound::LockerEnter : EHitmenSound::LockerExit, GetActorLocation());
}

void AHitmenHidingSpot::SetViewLimits(AHitmenCharacterBase* Character, bool bInside) const
{
	APlayerController* PC = Character ? Cast<APlayerController>(Character->GetController()) : nullptr;
	APlayerCameraManager* Camera = PC && PC->IsLocalController() ? PC->PlayerCameraManager.Get() : nullptr;
	if (!Camera)
	{
		return;
	}
	const APlayerCameraManager* Defaults = Camera->GetClass()->GetDefaultObject<APlayerCameraManager>();
	if (bInside && ViewArcDegrees < 359.f)
	{
		const float Yaw = FRotator::ClampAxis(GetActorRotation().Yaw);
		Camera->ViewYawMin = Yaw - ViewArcDegrees * 0.5f;
		Camera->ViewYawMax = Yaw + ViewArcDegrees * 0.5f;
	}
	else
	{
		Camera->ViewYawMin = Defaults->ViewYawMin;
		Camera->ViewYawMax = Defaults->ViewYawMax;
	}
	Camera->ViewPitchMin = bInside ? ViewPitchMin : Defaults->ViewPitchMin;
	Camera->ViewPitchMax = bInside ? ViewPitchMax : Defaults->ViewPitchMax;
}

void AHitmenHidingSpot::StartOpenCycle()
{
	OpenCycleStart = GetWorld()->GetTimeSeconds();
	SetActorTickEnabled(true);
}

void AHitmenHidingSpot::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const float Alpha = FMath::Clamp((GetWorld()->GetTimeSeconds() - OpenCycleStart) / OpenCycleTime, 0.f, 1.f);
	// Opens, lets the occupant / the body through, closes again (smooth).
	ApplyOpenAlpha(FMath::SmoothStep(0.f, 1.f, FMath::Sin(Alpha * PI)));
	UpdateStep();
	if (Alpha >= 1.f && !SteppingCharacter.IsValid())
	{
		ApplyOpenAlpha(0.f);
		SetActorTickEnabled(false);
	}
}

void AHitmenHidingSpot::UpdateStep()
{
	AHitmenCharacterBase* Character = SteppingCharacter.Get();
	if (!Character)
	{
		return;
	}
	const float T = FMath::Clamp((GetWorld()->GetTimeSeconds() - OpenCycleStart - StepDelay) / StepTime, 0.f, 1.f);
	const float Ease = FMath::InterpEaseInOut(0.f, 1.f, T, 2.f);
	Character->SetActorLocation(FMath::Lerp(StepFrom, StepTo, Ease), false, nullptr, ETeleportType::TeleportPhysics);
	if (APlayerController* PC = Cast<APlayerController>(Character->GetController()); PC && PC->IsLocalController())
	{
		PC->SetControlRotation(FMath::Lerp(StepFromRotation, StepToRotation, Ease));
	}
	if (T >= 1.f)
	{
		SteppingCharacter.Reset();
		if (bSteppingIn)
		{
			// Inside: the view turns only within the arc.
			SetViewLimits(Character, true);
		}
		else
		{
			// Out: walk again.
			Character->MoveIgnoreActorRemove(this);
			if (!Character->IsDead())
			{
				Character->GetCharacterMovement()->SetMovementMode(MOVE_Falling);
			}
		}
	}
}
