// Hitmen — event camera

#include "Framework/HitmenEventCamera.h"
#include "Core/HitmenGameSettings.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Components/SceneCaptureComponent2D.h"
#include "Engine/TextureRenderTarget2D.h"
#include "Kismet/KismetRenderingLibrary.h"
#include "GameFramework/PlayerController.h"
#include "Camera/PlayerCameraManager.h"
#include "Engine/World.h"

AHitmenEventCamera::AHitmenEventCamera()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	bReplicates = false;
	SetCanBeDamaged(false);
	Capture = CreateDefaultSubobject<USceneCaptureComponent2D>(TEXT("Capture"));
	SetRootComponent(Capture);
	Capture->bCaptureEveryFrame = false;
	Capture->bCaptureOnMovement = false;
	Capture->CaptureSource = ESceneCaptureSource::SCS_FinalColorLDR;
	Capture->FOVAngle = 55.f;
}

void AHitmenEventCamera::ShowFrom(USceneComponent* View, float FOV, const FText& Title, float Duration, APlayerController* InViewer)
{
	if (!View || !InViewer)
	{
		return;
	}
	if (!Target)
	{
		const FIntPoint Resolution = UHitmenGameSettings::Get()->EventCameraResolution;
		Target = UKismetRenderingLibrary::CreateRenderTarget2D(this, FMath::Max(64, Resolution.X), FMath::Max(36, Resolution.Y), RTF_RGBA8_SRGB);
		Capture->TextureTarget = Target;
	}
	const bool bSameView = FixedView.Get() == View && EndTime != -1.f;
	Subject.Reset();
	FixedView = View;
	Viewer = InViewer;
	Capture->FOVAngle = FOV;
	EndTime = Duration > 0.f ? GetWorld()->GetTimeSeconds() + Duration : TNumericLimits<float>::Max();
	SetActorLocationAndRotation(View->GetComponentLocation(), View->GetComponentRotation());
	Capture->bCaptureEveryFrame = true;
	SetActorTickEnabled(true);
	if (!bSameView)
	{
		if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(InViewer))
		{
			UI->SetEventCamera(Target, Title, Duration > 0.f ? EndTime : -1.f);
		}
	}
}

void AHitmenEventCamera::Show(AActor* InSubject, const FText& Title, float Duration, APlayerController* InViewer)
{
	if (!InSubject || !InViewer)
	{
		return;
	}
	FixedView.Reset();
	Capture->FOVAngle = 55.f;
	if (!Target)
	{
		const FIntPoint Resolution = UHitmenGameSettings::Get()->EventCameraResolution;
		Target = UKismetRenderingLibrary::CreateRenderTarget2D(this, FMath::Max(64, Resolution.X), FMath::Max(36, Resolution.Y), RTF_RGBA8_SRGB);
		Capture->TextureTarget = Target;
	}
	Subject = InSubject;
	Viewer = InViewer;
	EndTime = GetWorld()->GetTimeSeconds() + Duration;
	// Start beside the subject, on the side facing the viewer, then turn slowly around it.
	const FVector ToViewer = InViewer->PlayerCameraManager ? (InViewer->PlayerCameraManager->GetCameraLocation() - InSubject->GetActorLocation()).GetSafeNormal2D() : FVector::ForwardVector;
	OrbitYaw = ToViewer.Rotation().Yaw + 35.f;
	Frame(0.f);
	Capture->bCaptureEveryFrame = true;
	SetActorTickEnabled(true);
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(InViewer))
	{
		UI->SetEventCamera(Target, Title, EndTime);
	}
}

void AHitmenEventCamera::Stop()
{
	EndTime = -1.f;
	FixedView.Reset();
	Capture->bCaptureEveryFrame = false;
	SetActorTickEnabled(false);
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(Viewer.Get()))
	{
		UI->SetEventCamera(nullptr, FText::GetEmpty(), -1.f);
	}
}

void AHitmenEventCamera::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (const USceneComponent* View = FixedView.Get())
	{
		if (GetWorld()->GetTimeSeconds() >= EndTime)
		{
			Stop();
			return;
		}
		// A fixed camera's view (security feed): follows its head as it sweeps.
		SetActorLocationAndRotation(View->GetComponentLocation(), View->GetComponentRotation());
		return;
	}
	if (!Subject.IsValid() || GetWorld()->GetTimeSeconds() >= EndTime)
	{
		Stop();
		return;
	}
	Frame(DeltaSeconds);
}

void AHitmenEventCamera::Frame(float DeltaSeconds)
{
	const AActor* Watched = Subject.Get();
	if (!Watched)
	{
		return;
	}
	FVector Origin, Extent;
	Watched->GetActorBounds(true, Origin, Extent);
	const float Radius = FMath::Clamp(Extent.Size(), 60.f, 400.f);
	const float Distance = FMath::Clamp(Radius * 3.2f, 260.f, 900.f);
	OrbitYaw += 10.f * DeltaSeconds;
	const FRotator View(-18.f, OrbitYaw + 180.f, 0.f);
	FVector Wanted = Origin - View.Vector() * Distance;
	// Never behind a wall: pull in toward the subject.
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenEventCamera), false, Watched);
	if (const AActor* Carrier = Watched->GetAttachParentActor())
	{
		Params.AddIgnoredActor(Carrier);
	}
	if (GetWorld()->SweepSingleByChannel(Hit, Origin, Wanted, FQuat::Identity, ECC_Visibility, FCollisionShape::MakeSphere(15.f), Params))
	{
		Wanted = Hit.Location;
	}
	SetActorLocationAndRotation(Wanted, (Origin - Wanted).Rotation());
}
