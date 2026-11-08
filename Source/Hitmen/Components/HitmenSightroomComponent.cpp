// Hitmen — sightroom

#include "Components/HitmenSightroomComponent.h"
#include "Data/HitmenSoundSet.h"
#include "Interaction/HitmenScanRegistry.h"
#include "Characters/HitmenCharacterBase.h"
#include "Framework/HitmenGameState.h"
#include "Core/HitmenInterfaces.h"
#include "Hitmen.h"
#include "Camera/CameraComponent.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Engine/World.h"

UHitmenSightroomComponent::UHitmenSightroomComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.bStartWithTickEnabled = false;
}

void UHitmenSightroomComponent::BeginPlay()
{
	Super::BeginPlay();
	if (PostProcessMaterial)
	{
		PostProcessInstance = UMaterialInstanceDynamic::Create(PostProcessMaterial, this);
		if (UCameraComponent* Camera = GetCamera())
		{
			Camera->PostProcessSettings.AddBlendable(PostProcessInstance, 0.f);
		}
	}
}

UCameraComponent* UHitmenSightroomComponent::GetCamera() const
{
	return GetOwner()->FindComponentByClass<UCameraComponent>();
}

void UHitmenSightroomComponent::SetSightroomActive(bool bActive)
{
	const APawn* Pawn = Cast<APawn>(GetOwner());
	if (!Pawn || !Pawn->IsLocallyControlled() || bSightroomActive == bActive)
	{
		return;
	}
	bSightroomActive = bActive;
	if (bActive)
	{
		ScanProgress = 0.f;
	}
	RefreshAccumulator = RefreshInterval;
	// Keeps ticking after release to fade the look out.
	SetComponentTickEnabled(true);
	if (!bActive)
	{
		ClearHighlights();
	}
	UHitmenSoundLibrary::PlayHitmenSound2D(this, bActive ? EHitmenSound::SightroomOn : EHitmenSound::SightroomOff);
	Juice_OnSightroomChanged(bActive);
	OnSightroomChanged.Broadcast(bActive);
}

void UHitmenSightroomComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	FadeAlpha = FMath::FInterpConstantTo(FadeAlpha, bSightroomActive ? 1.f : 0.f, DeltaTime, 1.f / FadeDuration);
	if (bSightroomActive)
	{
		ScanProgress = FMath::Min(1.f, ScanProgress + DeltaTime / ScanDuration);
	}
	ApplyPostProcess();
	if (!bSightroomActive)
	{
		if (FadeAlpha <= 0.f)
		{
			SetComponentTickEnabled(false);
		}
		return;
	}
	RefreshAccumulator += DeltaTime;
	if (RefreshAccumulator >= RefreshInterval)
	{
		RefreshAccumulator = 0.f;
		Refresh();
	}
}

void UHitmenSightroomComponent::ApplyPostProcess()
{
	if (!PostProcessInstance)
	{
		return;
	}
	// The wave keeps travelling to the edge of the scan; the grid and the cool tint fade with FadeAlpha.
	const float Eased = 1.f - FMath::Pow(1.f - ScanProgress, 3.f);
	PostProcessInstance->SetScalarParameterValue(TEXT("ScanProgress"), ScanProgress);
	PostProcessInstance->SetScalarParameterValue(TEXT("ScanRadius"), EnemyRadius * Eased);
	PostProcessInstance->SetScalarParameterValue(TEXT("Fade"), FadeAlpha);
	PostProcessInstance->SetVectorParameterValue(TEXT("ScanOrigin"), GetOwner()->GetActorLocation());
	if (UCameraComponent* Camera = GetCamera())
	{
		Camera->PostProcessSettings.AddBlendable(PostProcessInstance, FadeAlpha > 0.f ? 1.f : 0.f);
	}
}

bool UHitmenSightroomComponent::HasLineOfSight(const FVector& From, const AActor* Target) const
{
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSightroom), false, GetOwner());
	FHitResult Hit;
	if (!GetWorld()->LineTraceSingleByChannel(Hit, From, Target->GetActorLocation(), ECC_Visibility, Params))
	{
		return true;
	}
	return Hit.GetActor() == Target;
}

void UHitmenSightroomComponent::Refresh()
{
	const UHitmenScanRegistry* Registry = GetWorld()->GetSubsystem<UHitmenScanRegistry>();
	if (!Registry)
	{
		return;
	}
	const AActor* Observer = GetOwner();
	const FVector Origin = Observer->GetActorLocation();
	const UCameraComponent* Camera = GetCamera();
	const FVector Eye = Camera ? Camera->GetComponentLocation() : Origin;
	const float WaveRadius = EnemyRadius * ScanProgress;

	TArray<TWeakObjectPtr<AActor>> NewHighlights;
	for (const TWeakObjectPtr<AActor>& Weak : Registry->GetScannables())
	{
		AActor* Actor = Weak.Get();
		if (!Actor || Actor == Observer || !Actor->GetClass()->ImplementsInterface(UHitmenScannable::StaticClass()))
		{
			continue;
		}
		const EHitmenScanCategory Category = IHitmenScannable::Execute_GetScanCategory(Actor, Observer);
		const float Distance = FVector::Dist(Origin, Actor->GetActorLocation());
		int32 Stencil = 0;
		switch (Category)
		{
		case EHitmenScanCategory::Target:
		{
			const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Actor);
			Stencil = (Character && Character->IsDead()) ? HitmenStencil::TargetDead : HitmenStencil::Target;
			break;
		}
		case EHitmenScanCategory::Enemy:
			Stencil = Distance <= WaveRadius ? HitmenStencil::Enemy : 0;
			break;
		case EHitmenScanCategory::Interactable:
			Stencil = (Distance <= FMath::Min(WaveRadius, InteractableRadius) && HasLineOfSight(Eye, Actor)) ? HitmenStencil::Interactable : 0;
			break;
		default:
			break;
		}
		if (Stencil != 0)
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, true, Stencil);
			NewHighlights.Add(Actor);
		}
	}

	for (const TWeakObjectPtr<AActor>& Old : Highlighted)
	{
		if (AActor* Actor = Old.Get(); Actor && !NewHighlights.Contains(Old))
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, false, 0);
		}
	}
	Highlighted = MoveTemp(NewHighlights);
}

void UHitmenSightroomComponent::ClearHighlights()
{
	for (const TWeakObjectPtr<AActor>& Weak : Highlighted)
	{
		if (AActor* Actor = Weak.Get())
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, false, 0);
		}
	}
	Highlighted.Reset();
}
