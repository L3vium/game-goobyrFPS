// Hitmen — body container

#include "World/Interactables/HitmenBodyContainer.h"
#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "Data/HitmenSoundSet.h"
#include "Components/StaticMeshComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

AHitmenBodyContainer::AHitmenBodyContainer()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	bReplicates = true;
	InteractionText = NSLOCTEXT("Hitmen", "HideBody", "Hide body");

	LidPivot = CreateDefaultSubobject<USceneComponent>(TEXT("LidPivot"));
	LidPivot->SetupAttachment(Root);
	LidPivot->SetRelativeLocation(FVector(-60.f, 0.f, 110.f));
	Lid = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Lid"));
	Lid->SetupAttachment(LidPivot);
	Lid->SetRelativeLocation(FVector(60.f, 0.f, 4.f));
}

void AHitmenBodyContainer::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenBodyContainer, HiddenCount);
}

bool AHitmenBodyContainer::CanInteract_Implementation(AActor* Interactor) const
{
	// Only while carrying a body (the prompt then says whether it fits).
	const UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	return Carry && Carry->IsCarrying() && Super::CanInteract_Implementation(Interactor);
}

FText AHitmenBodyContainer::GetInteractionText_Implementation(AActor* Interactor) const
{
	const UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	const AHitmenCharacterBase* Body = Carry ? Carry->GetCarriedBody() : nullptr;
	if (Body && Body->IsVIP())
	{
		return NSLOCTEXT("Hitmen", "HideBodyVIP", "The target can't be hidden");
	}
	if (IsFull())
	{
		return NSLOCTEXT("Hitmen", "HideBodyFull", "Hide body  [Full]");
	}
	return FText::Format(NSLOCTEXT("Hitmen", "HideBodyCount", "Hide body  ({0}/{1})"), HiddenCount, Capacity);
}

void AHitmenBodyContainer::OnInteracted(AActor* Interactor)
{
	UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	AHitmenCharacterBase* Body = Carry ? Carry->GetCarriedBody() : nullptr;
	if (!Body || Body->IsVIP() || IsFull())
	{
		return;
	}
	// The body goes in and is gone for good (guards never find it).
	Carry->Drop();
	Body->VanishBody();
	++HiddenCount;
	Multicast_PlayHide();
}

void AHitmenBodyContainer::Multicast_PlayHide_Implementation()
{
	HideStartTime = GetWorld()->GetTimeSeconds();
	SetActorTickEnabled(true);
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::ContainerOpen, GetActorLocation());
	Juice_OnBodyHidden(HiddenCount);
}

void AHitmenBodyContainer::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	// Lid: opens in the first third, stays open, closes in the last third.
	const float T = FMath::Clamp((GetWorld()->GetTimeSeconds() - HideStartTime) / HideDuration, 0.f, 1.f);
	const float Open = T < 0.33f ? FMath::SmoothStep(0.f, 0.33f, T) : (T > 0.66f ? 1.f - FMath::SmoothStep(0.66f, 1.f, T) : 1.f);
	LidPivot->SetRelativeRotation(FRotator(Open * LidOpenAngle, 0.f, 0.f));
	if (T >= 1.f)
	{
		LidPivot->SetRelativeRotation(FRotator::ZeroRotator);
		SetActorTickEnabled(false);
	}
}
