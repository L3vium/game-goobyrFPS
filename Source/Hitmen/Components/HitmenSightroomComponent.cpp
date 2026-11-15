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

void UHitmenSightroomComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	// Local render state only: never leave stencils on other actors when this pawn goes away.
	ClearHighlights();
	Super::EndPlay(EndPlayReason);
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
	// Keeps ticking after release to fade the look out; highlights stay until the fade ends (they fade with it).
	SetComponentTickEnabled(true);
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
			ClearHighlights();
			SetComponentTickEnabled(false);
		}
		return;
	}
	// The candidate set (custom depth on / off) follows at RefreshInterval; the shader fades every pixel each frame.
	RefreshAccumulator += DeltaTime;
	if (RefreshAccumulator >= RefreshInterval)
	{
		RefreshAccumulator = 0.f;
		Refresh();
	}
}

float UHitmenSightroomComponent::GetWaveRadius() const
{
	// Ease-out cubic, the same curve the post-process draws.
	return ScanVisualRadius * (1.f - FMath::Pow(1.f - ScanProgress, 3.f));
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
	PostProcessInstance->SetScalarParameterValue(TEXT("ScanProgress"), ScanProgress);
	PostProcessInstance->SetScalarParameterValue(TEXT("ScanRadius"), GetWaveRadius());
	PostProcessInstance->SetScalarParameterValue(TEXT("HeightWeight"), WaveHeightWeight);
	PostProcessInstance->SetScalarParameterValue(TEXT("Fade"), FadeAlpha);
	PostProcessInstance->SetVectorParameterValue(TEXT("ScanOrigin"), GetOwner()->GetActorLocation());
	// Per-pixel ranges around where you are now (enemies anywhere with bEnemiesMapWide, revealed by the wave).
	PostProcessInstance->SetScalarParameterValue(TEXT("EnemyRadius"), bEnemiesMapWide ? 1.e8f : EnemyRadius);
	PostProcessInstance->SetScalarParameterValue(TEXT("ObjectRadius"), InteractableRadius);
	PostProcessInstance->SetScalarParameterValue(TEXT("SoftEdge"), HighlightSoftEdge);
	if (UCameraComponent* Camera = GetCamera())
	{
		Camera->PostProcessSettings.AddBlendable(PostProcessInstance, FadeAlpha > 0.f ? 1.f : 0.f);
	}
}

void UHitmenSightroomComponent::Refresh()
{
	const UHitmenScanRegistry* Registry = GetWorld()->GetSubsystem<UHitmenScanRegistry>();
	if (!Registry)
	{
		return;
	}
	// Candidates only: whatever could be lit within its radius (plus a margin). PP_Sightroom fades each pixel by the
	// object's distance to you, the wave front and (interactables) the depth test, so this set may be generous.
	const AActor* Observer = GetOwner();
	const FVector Origin = Observer->GetActorLocation();

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
			// Through walls, near you (anywhere with bEnemiesMapWide).
			Stencil = (bEnemiesMapWide || WaveDistance <= EnemyRadius + CandidateMargin) ? HitmenStencil::Enemy : 0;
			break;
		case EHitmenScanCategory::Interactable:
			// Never through walls: the shader only lights the pixels where the object is the nearest surface.
			Stencil = WaveDistance <= InteractableRadius + CandidateMargin ? HitmenStencil::Interactable : 0;
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
