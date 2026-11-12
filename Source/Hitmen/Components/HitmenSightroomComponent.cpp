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
	// Every frame while the wave travels, so a highlight appears the moment the front reaches it.
	RefreshAccumulator += DeltaTime;
	if (ScanProgress < 1.f || RefreshAccumulator >= RefreshInterval)
	{
		RefreshAccumulator = 0.f;
		Refresh();
	}
}

float UHitmenSightroomComponent::GetWaveRadius() const
{
	// Ease-out cubic, the same curve the post-process draws.
	return EnemyRadius * (1.f - FMath::Pow(1.f - ScanProgress, 3.f));
}

float UHitmenSightroomComponent::GetWaveDistance(const FVector& Origin, const FVector& Location) const
{
	return FVector::Dist2D(Origin, Location) + FMath::Abs(Location.Z - Origin.Z) * WaveHeightWeight;
}

void UHitmenSightroomComponent::ApplyPostProcess()
{
	if (!PostProcessInstance)
	{
		return;
	}
	// The wave keeps travelling to the edge of the scan; the grid and the cool tint fade with FadeAlpha.
	const float Eased = GetWaveRadius() / FMath::Max(EnemyRadius, 1.f);
	PostProcessInstance->SetScalarParameterValue(TEXT("ScanProgress"), ScanProgress);
	PostProcessInstance->SetScalarParameterValue(TEXT("ScanRadius"), EnemyRadius * Eased);
	PostProcessInstance->SetScalarParameterValue(TEXT("HeightWeight"), WaveHeightWeight);
	PostProcessInstance->SetScalarParameterValue(TEXT("Fade"), FadeAlpha);
	PostProcessInstance->SetVectorParameterValue(TEXT("ScanOrigin"), GetOwner()->GetActorLocation());
	if (UCameraComponent* Camera = GetCamera())
	{
		Camera->PostProcessSettings.AddBlendable(PostProcessInstance, FadeAlpha > 0.f ? 1.f : 0.f);
	}
}

bool UHitmenSightroomComponent::HasLineOfSight(const FVector& From, const AActor* Target) const
{
	// Aim at the middle of what you see (a window's origin sits on the sill, inside the wall), then its top.
	FVector Center;
	FVector Extent;
	Target->GetActorBounds(false, Center, Extent);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSightroom), false, GetOwner());
	for (const FVector& Point : { Center, Center + FVector(0.f, 0.f, Extent.Z * 0.8f) })
	{
		FHitResult Hit;
		if (!GetWorld()->LineTraceSingleByChannel(Hit, From, Point, ECC_Visibility, Params) || Hit.GetActor() == Target)
		{
			return true;
		}
	}
	return false;
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
	const float WaveRadius = GetWaveRadius();
	const bool bWaveDone = ScanProgress >= 1.f;

	TMap<TWeakObjectPtr<AActor>, int32> NewHighlights;
	for (const TWeakObjectPtr<AActor>& Weak : Registry->GetScannables())
	{
		AActor* Actor = Weak.Get();
		if (!Actor || Actor == Observer || !Actor->GetClass()->ImplementsInterface(UHitmenScannable::StaticClass()))
		{
			continue;
		}
		const EHitmenScanCategory Category = IHitmenScannable::Execute_GetScanCategory(Actor, Observer);
		const float WaveDistance = GetWaveDistance(Origin, Actor->GetActorLocation());
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
			Stencil = (WaveDistance <= WaveRadius || (bWaveDone && bEnemiesMapWide)) ? HitmenStencil::Enemy : 0;
			break;
		case EHitmenScanCategory::Interactable:
			Stencil = (WaveDistance <= WaveRadius && FVector::Dist(Origin, Actor->GetActorLocation()) <= InteractableRadius && HasLineOfSight(Eye, Actor))
				? HitmenStencil::Interactable : 0;
			break;
		default:
			break;
		}
		if (Stencil != 0)
		{
			const int32* Previous = Highlighted.Find(Actor);
			if (!Previous || *Previous != Stencil)
			{
				IHitmenScannable::Execute_SetScanHighlight(Actor, true, Stencil);
			}
			NewHighlights.Add(Actor, Stencil);
		}
	}

	for (const TPair<TWeakObjectPtr<AActor>, int32>& Old : Highlighted)
	{
		if (AActor* Actor = Old.Key.Get(); Actor && !NewHighlights.Contains(Old.Key))
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, false, 0);
		}
	}
	Highlighted = MoveTemp(NewHighlights);
}

void UHitmenSightroomComponent::ClearHighlights()
{
	for (const TPair<TWeakObjectPtr<AActor>, int32>& Pair : Highlighted)
	{
		if (AActor* Actor = Pair.Key.Get())
		{
			IHitmenScannable::Execute_SetScanHighlight(Actor, false, 0);
		}
	}
	Highlighted.Reset();
}
