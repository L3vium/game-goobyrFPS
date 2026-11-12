// Hitmen — security fixtures

#include "World/Interactables/HitmenSecurity.h"
#include "Engine/DamageEvents.h"
#include "Components/TextRenderComponent.h"
#include "Core/HitmenGameSettings.h"
#include "Weapons/HitmenCosmeticActors.h"
#include "Components/HitmenMapMarkerComponent.h"
#include "Components/SpotLightComponent.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "Engine/StaticMesh.h"
#include "UObject/ConstructorHelpers.h"
#include "NiagaraFunctionLibrary.h"
#include "Data/HitmenSoundSet.h"
#include "Characters/HitmenCharacterBase.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Hitmen.h"
#include "Components/SceneCaptureComponent2D.h"
#include "Components/StaticMeshComponent.h"
#include "Components/DecalComponent.h"
#include "Components/BoxComponent.h"
#include "Components/PointLightComponent.h"
#include "Engine/TextureRenderTarget2D.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Kismet/GameplayStatics.h"
#include "Kismet/KismetRenderingLibrary.h"
#include "GameFramework/PlayerController.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "TimerManager.h"

// ---------------------------------------------------------------------------------------------
// Camera
// ---------------------------------------------------------------------------------------------

AHitmenSecurityCamera::AHitmenSecurityCamera()
{
	// Fixtures are never vaulted, climbed or hung from.
	Tags.AddUnique(HitmenTags::NoTraversal);
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.05f;
	bReplicates = true;

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);

	Head = CreateDefaultSubobject<USceneComponent>(TEXT("Head"));
	Head->SetupAttachment(Root);

	Body = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Body"));
	Body->SetupAttachment(Head);

	ViewCone = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("ViewCone"));
	ViewCone->SetupAttachment(Head);
	ViewCone->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	ViewCone->CastShadow = false;

	FloorDecal = CreateDefaultSubobject<UDecalComponent>(TEXT("FloorDecal"));
	FloorDecal->SetupAttachment(Head);
	FloorDecal->DecalSize = FVector(200.f, 300.f, 300.f);

	MapMarker = CreateDefaultSubobject<UHitmenMapMarkerComponent>(TEXT("MapMarker"));

	ViewLight = CreateDefaultSubobject<USpotLightComponent>(TEXT("ViewLight"));
	ViewLight->SetupAttachment(Head);
	ViewLight->SetMobility(EComponentMobility::Movable);
	ViewLight->SetIntensityUnits(ELightUnits::Candelas);
	ViewLight->SetCastShadows(true);
	ViewLight->bAffectsWorld = true;

	Capture = CreateDefaultSubobject<USceneCaptureComponent2D>(TEXT("Capture"));
	Capture->SetupAttachment(Head);
	Capture->bCaptureEveryFrame = false;
	Capture->bCaptureOnMovement = false;
}

void AHitmenSecurityCamera::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenSecurityCamera, bDisabled);
	DOREPLIFETIME(AHitmenSecurityCamera, bSeeingIntruder);
	DOREPLIFETIME(AHitmenSecurityCamera, DetectionProgress);
	DOREPLIFETIME(AHitmenSecurityCamera, bBroken);
}

void AHitmenSecurityCamera::BeginPlay()
{
	Super::BeginPlay();
	// Cameras watch the floor: the cone ends in a round footprint where it meets the ground.
	BaseHeadRotation = Head->GetRelativeRotation() + FRotator(-LookDownAngle, 0.f, 0.f);
	Head->SetRelativeRotation(BaseHeadRotation);
	// The feed: wide, looking more ahead than the head, tonemapped at the match exposure (it was a white floor).
	Capture->FOVAngle = FeedFOV;
	Capture->SetRelativeRotation(FRotator(LookDownAngle - FeedLookDownAngle, 0.f, 0.f));
	Capture->CaptureSource = ESceneCaptureSource::SCS_FinalColorLDR;
	Capture->PostProcessBlendWeight = 1.f;
	Capture->PostProcessSettings.bOverride_AutoExposureMethod = true;
	Capture->PostProcessSettings.AutoExposureMethod = EAutoExposureMethod::AEM_Histogram;
	Capture->PostProcessSettings.bOverride_AutoExposureMinBrightness = true;
	Capture->PostProcessSettings.bOverride_AutoExposureMaxBrightness = true;
	Capture->PostProcessSettings.AutoExposureMinBrightness = FeedExposureEV100;
	Capture->PostProcessSettings.AutoExposureMaxBrightness = FeedExposureEV100;
	Capture->ShowFlags.SetEyeAdaptation(false);
	// The footage shows the room, not the camera's own zone pattern.
	Capture->ShowFlags.SetLightFunctions(false);
	Capture->ShowFlags.SetDecals(false);

	// Cone length / width follow the view settings (default mesh: engine cone pointing up).
	const float Radius = FMath::Tan(FMath::DegreesToRadians(ViewHalfAngle)) * ViewDistance;
	ViewCone->SetRelativeLocation(FVector(ViewDistance * 0.5f, 0.f, 0.f));
	ViewCone->SetRelativeRotation(FRotator(90.f, 0.f, 0.f));
	ViewCone->SetRelativeScale3D(FVector(Radius / 50.f, Radius / 50.f, ViewDistance / 100.f));

	FloorDecal->SetUsingAbsoluteRotation(true);
	UpdateFootprint();

	// The view is a light: shadowed (walls stop it), soft edge, reaching a bit past the view distance.
	ViewLight->SetIntensity(ViewLightIntensity);
	ViewLight->SetOuterConeAngle(ViewHalfAngle);
	ViewLight->SetInnerConeAngle(ViewHalfAngle * 0.7f);
	ViewLight->SetAttenuationRadius(ViewDistance * 1.35f);
	ViewLight->SetSourceRadius(4.f);
	ViewCone->SetHiddenInGame(!bShowViewCone);
	// The zone is projected by the light itself (walls cut it like the light), and the beam shows in the air.
	if (ProjectionMaterial)
	{
		ProjectionInstance = UMaterialInstanceDynamic::Create(ProjectionMaterial, this);
		ProjectionInstance->SetScalarParameterValue(TEXT("TanHalf"), FMath::Tan(FMath::DegreesToRadians(ViewHalfAngle)));
		ViewLight->SetLightFunctionMaterial(ProjectionInstance);
		ViewLight->SetLightFunctionScale(FVector(ViewDistance, ViewDistance, ViewDistance));
	}
	ViewLight->SetVolumetricScatteringIntensity(BeamScattering);
	ViewLight->bCastVolumetricShadow = true;

	for (UMeshComponent* MeshComp : { static_cast<UMeshComponent*>(ViewCone.Get()) })
	{
		if (UMaterialInterface* Material = MeshComp->GetMaterial(0))
		{
			ColorMaterials.Add(MeshComp->CreateDynamicMaterialInstance(0, Material));
		}
	}
	if (FloorDecal->GetDecalMaterial())
	{
		ColorMaterials.Add(FloorDecal->CreateDynamicMaterialInstance());
	}
	UpdateFootprint();  // again, now that the ring material can take the cone (static cameras never sweep)
	OnRep_Visuals();
	SetActorTickEnabled(HasAuthority() || SweepAngle > 0.f);
}

void AHitmenSecurityCamera::UpdateFootprint()
{
	// The light's footprint: where the cone axis and eight cone edges meet the level. The decal box covers them all
	// (projected straight down) and its material keeps only the pixels on the cone's edge, so the ring is exactly
	// where the light ends, on the floor and on walls.
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenCameraFloor), false, this);
	const FVector Eye = Head->GetComponentLocation();
	const FVector Forward = Head->GetForwardVector();
	FHitResult Ground;
	const FVector Along = Eye + Forward * ViewDistance;
	const bool bHit = GetWorld()->LineTraceSingleByChannel(Ground, Eye, Along, ECC_Visibility, Params);
	const FVector Center = bHit ? Ground.ImpactPoint : Along;
	FBox Footprint(Center, Center);
	const FQuat HeadRotation = Head->GetComponentQuat();
	for (int32 Index = 0; Index < 8; ++Index)
	{
		const FRotator Edge(ViewHalfAngle * FMath::Cos(Index * PI / 4.f), ViewHalfAngle * FMath::Sin(Index * PI / 4.f), 0.f);
		const FVector Direction = HeadRotation.RotateVector(Edge.Vector());
		FHitResult Hit;
		const FVector End = Eye + Direction * ViewDistance * 1.35f;
		Footprint += GetWorld()->LineTraceSingleByChannel(Hit, Eye, End, ECC_Visibility, Params) ? Hit.ImpactPoint : End;
	}
	const FVector Extent = Footprint.GetExtent();
	FloorDecal->SetVisibility(bHit && !bDisabled && bShowFloorRing);
	FloorDecal->SetWorldLocation(Footprint.GetCenter());
	FloorDecal->SetWorldRotation(FRotator(-90.f, 0.f, 0.f));
	// Decal box: X projects (down here), Y and Z span the footprint (Y along world Y, Z along world X once pitched).
	FloorDecal->DecalSize = FVector(FMath::Max(Extent.Z + 150.f, 150.f), Extent.Y + 60.f, Extent.X + 60.f);
	FloorDecal->MarkRenderStateDirty();
	for (UMaterialInstanceDynamic* Material : ColorMaterials)
	{
		if (Material)
		{
			Material->SetVectorParameterValue(TEXT("Eye"), FLinearColor(Eye));
			Material->SetVectorParameterValue(TEXT("Forward"), FLinearColor(Forward));
			Material->SetScalarParameterValue(TEXT("CosHalfAngle"), FMath::Cos(FMath::DegreesToRadians(ViewHalfAngle)));
		}
	}
}

FLinearColor AHitmenSecurityCamera::GetStateColor() const
{
	// One colour only (yellow): the zone stays yellow whatever happens; the meter fills inside it.
	return SuspiciousColor;
}

void AHitmenSecurityCamera::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (bDisabled)
	{
		return;
	}
	// Sweep (deterministic on every machine from time).
	if (SweepAngle > 0.f)
	{
		SweepTime += DeltaSeconds * SweepSpeed;
		const float Yaw = FMath::Sin(SweepTime) * SweepAngle * 0.5f;
		Head->SetRelativeRotation(BaseHeadRotation + FRotator(0.f, Yaw, 0.f));
		UpdateFootprint();
	}
	if (!HasAuthority())
	{
		return;
	}

	APawn* Intruder = FindIntruder();
	const bool bSeeing = Intruder != nullptr;
	if (bSeeing != bSeeingIntruder)
	{
		bSeeingIntruder = bSeeing;
		OnRep_Visuals();
	}
	SeenTime = bSeeing ? SeenTime + DeltaSeconds : 0.f;
	const float Meter = DetectionTime > 0.f ? SeenTime / DetectionTime : (bSeeing ? 1.f : 0.f);
	const uint8 Progress = static_cast<uint8>(FMath::Clamp(Meter, 0.f, 1.f) * 255.f);
	if (Progress != DetectionProgress)
	{
		DetectionProgress = Progress;
		OnRep_Visuals();
	}
	if (bSeeing && SeenTime >= DetectionTime && GetWorld()->GetTimeSeconds() - LastAlarmTime > AlarmCooldown)
	{
		RaiseAlarm(Intruder);
	}
}

APawn* AHitmenSecurityCamera::FindIntruder() const
{
	const FVector Eye = Head->GetComponentLocation();
	const FVector Forward = Head->GetForwardVector();
	const float CosHalf = FMath::Cos(FMath::DegreesToRadians(ViewHalfAngle));
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenCamera), false, this);

	for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
	{
		const APlayerController* PC = It->Get();
		AHitmenCharacterBase* Character = PC ? Cast<AHitmenCharacterBase>(PC->GetPawn()) : nullptr;
		if (!Character || Character->IsDead() || Character->GetStatusEffects()->IsHiddenFromSight())
		{
			continue;
		}
		if (const AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(Character); Player && Player->IsIntroProtected())
		{
			continue;
		}
		if (!UHitmenTeamLibrary::AreTeamIdsHostile(HitmenTeams::NPC, UHitmenTeamLibrary::GetTeamId(Character)))
		{
			continue;
		}
		const FVector Target = Character->GetActorLocation();
		const FVector ToTarget = Target - Eye;
		if (ToTarget.Size() > ViewDistance || FVector::DotProduct(ToTarget.GetSafeNormal(), Forward) < CosHalf)
		{
			continue;
		}
		if (AI && (AI->IsSightBlocked(Eye, Target) || AI->IsLocationDark(Target)))
		{
			continue;
		}
		// Seen only through open air: any wall (visibility or static geometry) between the lens and
		// the head or the chest hides the player.
		FCollisionQueryParams SightParams = Params;
		SightParams.AddIgnoredActor(Character);
		const FCollisionObjectQueryParams Static(FCollisionObjectQueryParams::InitType::AllStaticObjects);
		const float HalfHeight = Character->GetSimpleCollisionHalfHeight();
		for (const FVector& Point : { Target + FVector(0.f, 0.f, HalfHeight * 0.75f), Target })
		{
			if (!GetWorld()->LineTraceTestByChannel(Eye, Point, ECC_Visibility, SightParams)
				&& !GetWorld()->LineTraceTestByObjectType(Eye, Point, Static, SightParams))
			{
				return Character;
			}
		}
	}
	return nullptr;
}

void AHitmenSecurityCamera::RaiseAlarm(APawn* Intruder)
{
	LastAlarmTime = GetWorld()->GetTimeSeconds();
	if (UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this))
	{
		AI->CallReinforcements(this, Intruder->GetActorLocation(), Intruder);
	}
	Juice_OnAlarm(Intruder);
}

void AHitmenSecurityCamera::SetCameraDisabled(bool bInDisabled)
{
	// A broken camera stays dead (the console cannot bring it back).
	if (HasAuthority() && !(bBroken && !bInDisabled))
	{
		bDisabled = bInDisabled;
		bSeeingIntruder = false;
		DetectionProgress = 0;
		SeenTime = 0.f;
		OnRep_Visuals();
	}
}

void AHitmenSecurityCamera::OnRep_Visuals()
{
	if (ProjectionInstance)
	{
		ProjectionInstance->SetScalarParameterValue(TEXT("Progress"), DetectionProgress / 255.f);
	}
	if (bBroken)
	{
		// Hanging dead: head down, no light.
		Head->SetRelativeRotation(BaseHeadRotation + FRotator(-50.f, 0.f, 25.f));
	}
	ViewCone->SetVisibility(!bDisabled && bShowViewCone);
	FloorDecal->SetVisibility(!bDisabled && bShowFloorRing);
	ViewLight->SetVisibility(!bDisabled);
	MapMarker->SetMarkerVisible(!bDisabled);
	const FLinearColor StateColor = GetStateColor();
	ViewLight->SetLightColor(FLinearColor(StateColor.R, StateColor.G, StateColor.B, 1.f));
	ViewLight->SetIntensity(ViewLightIntensity * (bSeeingIntruder ? 1.6f : 1.f));
	if (bLastBroadcastDisabled != bDisabled)
	{
		bLastBroadcastDisabled = bDisabled;
		// Monitors swap to "no signal" and stop requesting captures; CaptureTo refuses meanwhile.
		OnDisabledChanged.Broadcast(bDisabled);
	}
	for (UMaterialInstanceDynamic* Material : ColorMaterials)
	{
		if (Material)
		{
			Material->SetVectorParameterValue(TEXT("Color"), StateColor);
			Material->SetScalarParameterValue(TEXT("Progress"), DetectionProgress / 255.f);
		}
	}
	Juice_OnSeeingChanged(bSeeingIntruder);
}

void AHitmenSecurityCamera::CaptureTo(UTextureRenderTarget2D* Target)
{
	if (!Target || bDisabled)
	{
		return;
	}
	Capture->TextureTarget = Target;
	Capture->CaptureScene();
}

// ---------------------------------------------------------------------------------------------
// Monitor
// ---------------------------------------------------------------------------------------------

AHitmenSecurityMonitor::AHitmenSecurityMonitor()
{
	Screen = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Screen"));
	SetRootComponent(Screen);
	NoSignalText = CreateDefaultSubobject<UTextRenderComponent>(TEXT("NoSignalText"));
	NoSignalText->SetupAttachment(Screen);
	NoSignalText->SetText(NSLOCTEXT("Hitmen", "NoSignal", "NO SIGNAL"));
	NoSignalText->SetHorizontalAlignment(EHTA_Center);
	NoSignalText->SetVerticalAlignment(EVRTA_TextCenter);
	NoSignalText->SetTextRenderColor(FColor(230, 230, 230));
	NoSignalText->SetWorldSize(12.f);
	NoSignalText->SetRelativeLocation(FVector(0.f, -6.f, 0.f));
	NoSignalText->SetRelativeRotation(FRotator(0.f, -90.f, 0.f));
	NoSignalText->SetVisibility(false);
}

void AHitmenSecurityMonitor::BeginPlay()
{
	Super::BeginPlay();
	if (GetNetMode() == NM_DedicatedServer || !LinkedCamera)
	{
		return;
	}
	RenderTarget = UKismetRenderingLibrary::CreateRenderTarget2D(this, 1024, 576, RTF_RGBA8_SRGB);
	FeedMaterial = Screen->CreateDynamicMaterialInstance(ScreenMaterialIndex);
	if (FeedMaterial)
	{
		FeedMaterial->SetTextureParameterValue(ScreenTextureParameter, RenderTarget);
	}
	LinkedCamera->OnDisabledChanged.AddDynamic(this, &AHitmenSecurityMonitor::HandleCameraDisabledChanged);
	HandleCameraDisabledChanged(LinkedCamera->IsCameraDisabled());
}

void AHitmenSecurityMonitor::HandleCameraDisabledChanged(bool bCameraDisabled)
{
	if (bCameraDisabled)
	{
		// Stop refreshing: the last frame stays on the screen, dimmed and noisy, with NO SIGNAL over it.
		GetWorldTimerManager().ClearTimer(RefreshTimer);
		if (FeedMaterial)
		{
			FeedMaterial->SetScalarParameterValue(TEXT("Signal"), 0.f);
		}
		else if (UMaterialInterface* NoSignal = NoSignalMaterial.LoadSynchronous())
		{
			Screen->SetMaterial(ScreenMaterialIndex, NoSignal);
		}
		NoSignalText->SetVisibility(true);
	}
	else
	{
		if (FeedMaterial)
		{
			FeedMaterial->SetScalarParameterValue(TEXT("Signal"), 1.f);
			Screen->SetMaterial(ScreenMaterialIndex, FeedMaterial);
		}
		NoSignalText->SetVisibility(false);
		GetWorldTimerManager().SetTimer(RefreshTimer, this, &AHitmenSecurityMonitor::Refresh, RefreshInterval, true);
		Refresh();
	}
	Juice_OnSignalChanged(!bCameraDisabled);
}

void AHitmenSecurityMonitor::Refresh()
{
	const APawn* LocalPawn = UGameplayStatics::GetPlayerPawn(this, 0);
	if (LinkedCamera && LocalPawn && FVector::Dist(LocalPawn->GetActorLocation(), GetActorLocation()) <= RefreshDistance)
	{
		LinkedCamera->CaptureTo(RenderTarget);
	}
}

// ---------------------------------------------------------------------------------------------
// Console
// ---------------------------------------------------------------------------------------------

AHitmenControlConsole::AHitmenControlConsole()
{
	InteractionText = NSLOCTEXT("Hitmen", "DisableCameras", "Disable cameras");
	Screen = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Screen"));
	Screen->SetupAttachment(Root);
	// Above the back of the desk, facing the operator (+X), parallel to the desk front.
	Screen->SetRelativeLocation(FVector(-18.f, 0.f, 145.f));
	Screen->SetRelativeScale3D(FVector(0.04f, 0.95f, 0.55f));
	Screen->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
}

void AHitmenControlConsole::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenControlConsole, bCamerasDisabled);
}

void AHitmenControlConsole::BeginPlay()
{
	Super::BeginPlay();
	if (GetNetMode() == NM_DedicatedServer || LinkedCameras.IsEmpty())
	{
		return;
	}
	RenderTarget = UKismetRenderingLibrary::CreateRenderTarget2D(this, 1024, 576, RTF_RGBA8_SRGB);
	FeedMaterial = Screen->CreateDynamicMaterialInstance(0);
	if (FeedMaterial)
	{
		FeedMaterial->SetTextureParameterValue(ScreenTextureParameter, RenderTarget);
	}
	ApplyScreenState();
}

void AHitmenControlConsole::OnRep_CamerasDisabled()
{
	ApplyScreenState();
}

void AHitmenControlConsole::ApplyScreenState()
{
	if (GetNetMode() == NM_DedicatedServer || !RenderTarget)
	{
		return;
	}
	if (bCamerasDisabled)
	{
		GetWorldTimerManager().ClearTimer(RefreshTimer);
		if (UMaterialInterface* NoSignal = NoSignalMaterial.LoadSynchronous())
		{
			Screen->SetMaterial(0, NoSignal);
		}
		return;
	}
	if (FeedMaterial)
	{
		Screen->SetMaterial(0, FeedMaterial);
	}
	GetWorldTimerManager().SetTimer(RefreshTimer, this, &AHitmenControlConsole::RefreshScreen, RefreshInterval, true);
	RefreshScreen();
}

void AHitmenControlConsole::RefreshScreen()
{
	FeedTime += RefreshInterval;
	if (FeedTime >= FeedCycleTime)
	{
		FeedTime = 0.f;
		FeedIndex = (FeedIndex + 1) % FMath::Max(1, LinkedCameras.Num());
	}
	const APawn* LocalPawn = UGameplayStatics::GetPlayerPawn(this, 0);
	AHitmenSecurityCamera* Camera = LinkedCameras.IsValidIndex(FeedIndex) ? LinkedCameras[FeedIndex].Get() : nullptr;
	if (Camera && LocalPawn && FVector::Dist(LocalPawn->GetActorLocation(), GetActorLocation()) <= RefreshDistance)
	{
		Camera->CaptureTo(RenderTarget);
	}
}

FText AHitmenControlConsole::GetInteractionText_Implementation(AActor* Interactor) const
{
	return bCamerasDisabled ? NSLOCTEXT("Hitmen", "EnableCameras", "Enable cameras") : InteractionText;
}

void AHitmenControlConsole::OnInteracted(AActor* Interactor)
{
	bCamerasDisabled = !bCamerasDisabled;
	OnRep_CamerasDisabled();
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::Hack, GetActorLocation());
	for (AHitmenSecurityCamera* Camera : LinkedCameras)
	{
		if (Camera)
		{
			Camera->SetCameraDisabled(bCamerasDisabled);
		}
	}
}

// ---------------------------------------------------------------------------------------------
// Turret
// ---------------------------------------------------------------------------------------------

AHitmenTurret::AHitmenTurret()
{
	// Fixtures are never vaulted, climbed or hung from.
	Tags.AddUnique(HitmenTags::NoTraversal);
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.03f;
	bReplicates = true;

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);
	Base = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Base"));
	Base->SetupAttachment(Root);
	Head = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Head"));
	Head->SetupAttachment(Root);
	Head->SetRelativeLocation(FVector(0.f, 0.f, 100.f));
	Head->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	Base->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cylinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	Barrel = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Barrel"));
	Barrel->SetupAttachment(Head);
	Barrel->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Laser = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Laser"));
	Laser->SetupAttachment(Head);
	Laser->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Laser->SetCastShadow(false);
	Laser->SetVisibility(false);
	if (Cylinder.Succeeded())
	{
		Barrel->SetStaticMesh(Cylinder.Object);
		Laser->SetStaticMesh(Cylinder.Object);
	}
	Barrel->SetRelativeLocationAndRotation(FVector(45.f, 0.f, 0.f), FRotator(-90.f, 0.f, 0.f));
	Barrel->SetRelativeScale3D(FVector(0.08f, 0.08f, 0.7f));
}

FText AHitmenTurret::GetInteractionText_Implementation(AActor* Interactor) const
{
	return NSLOCTEXT("Hitmen", "HackTurret", "Hack turret");
}

void AHitmenTurret::UpdateLaser()
{
	AActor* Target = CurrentTarget;
	if (!Target || GetNetMode() == NM_DedicatedServer)
	{
		Laser->SetVisibility(false);
		return;
	}
	const FVector Start = Head->GetComponentLocation();
	const FVector End = Target->GetActorLocation();
	const float Length = FVector::Dist(Start, End);
	// The laser follows the head's aim (it sweeps onto the target while turning).
	Laser->SetWorldLocationAndRotation(Start + Head->GetForwardVector() * Length * 0.5f, FRotationMatrix::MakeFromZ(Head->GetForwardVector()).Rotator());
	Laser->SetWorldScale3D(FVector(0.012f, 0.012f, Length / 100.f));
	Laser->SetVisibility(true);
}

void AHitmenTurret::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenTurret, TeamId);
	DOREPLIFETIME(AHitmenTurret, bHacked);
	DOREPLIFETIME(AHitmenTurret, CurrentTarget);
}

void AHitmenTurret::Hack(AActor* Hacker)
{
	if (!HasAuthority() || bHacked)
	{
		return;
	}
	bHacked = true;
	HackedBy = Hacker;
	TeamId = UHitmenTeamLibrary::GetTeamId(Hacker);
	CurrentTarget = nullptr;
	Juice_OnHacked();
}

AActor* AHitmenTurret::FindTarget() const
{
	const FVector Eye = Head->GetComponentLocation();
	const UHitmenAISubsystem* AI = UHitmenAISubsystem::Get(this);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenTurret), false, this);
	AActor* Best = nullptr;
	float BestDistance = Range;

	TArray<AActor*> Characters;
	UGameplayStatics::GetAllActorsOfClass(this, AHitmenCharacterBase::StaticClass(), Characters);
	for (AActor* Actor : Characters)
	{
		const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(Actor);
		if (!Character || Character->IsDead() || !UHitmenTeamLibrary::AreHostile(this, Character) || Character->GetStatusEffects()->IsHiddenFromSight())
		{
			continue;
		}
		const float Distance = FVector::Dist(Eye, Character->GetActorLocation());
		if (Distance > BestDistance || (AI && AI->IsSightBlocked(Eye, Character->GetActorLocation())))
		{
			continue;
		}
		FHitResult Hit;
		if (!GetWorld()->LineTraceSingleByChannel(Hit, Eye, Character->GetActorLocation(), ECC_Visibility, Params) || Hit.GetActor() == Character)
		{
			Best = Actor;
			BestDistance = Distance;
		}
	}
	return Best;
}

void AHitmenTurret::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (CurrentTarget)
	{
		const FRotator Desired = (CurrentTarget->GetActorLocation() - Head->GetComponentLocation()).Rotation();
		Head->SetWorldRotation(FMath::RInterpConstantTo(Head->GetComponentRotation(), Desired, DeltaSeconds, TurnSpeed));
	}
	if (BarrelRestLocation.IsZero())
	{
		BarrelRestLocation = Barrel->GetRelativeLocation();
	}
	BarrelOffset = FMath::FInterpTo(BarrelOffset, 0.f, DeltaSeconds, 14.f);
	Barrel->SetRelativeLocation(BarrelRestLocation - FVector(BarrelOffset, 0.f, 0.f));
	if (LastTarget.Get() != CurrentTarget)
	{
		LastTarget = CurrentTarget;
		TargetAcquiredTime = GetWorld()->GetTimeSeconds();
		if (CurrentTarget)
		{
			UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::TurretLockOn, Head->GetComponentLocation());
		}
	}
	UpdateLaser();
	if (!HasAuthority())
	{
		return;
	}
	RetargetAccumulator += DeltaSeconds;
	if (RetargetAccumulator > 0.25f)
	{
		RetargetAccumulator = 0.f;
		CurrentTarget = FindTarget();
	}
	if (!CurrentTarget || GetWorld()->GetTimeSeconds() - LastFireTime < FireInterval
		|| GetWorld()->GetTimeSeconds() - TargetAcquiredTime < LockOnTime)
	{
		return;
	}
	const FVector ToTarget = (CurrentTarget->GetActorLocation() - Head->GetComponentLocation()).GetSafeNormal();
	if (FVector::DotProduct(ToTarget, Head->GetForwardVector()) < 0.95f)
	{
		return;
	}
	LastFireTime = GetWorld()->GetTimeSeconds();
	const FVector Start = Head->GetComponentLocation() + Head->GetForwardVector() * 60.f;
	const FVector Direction = FMath::VRandCone(Head->GetForwardVector(), FMath::DegreesToRadians(AimErrorDegrees));
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenTurretShot), false, this);
	FVector Impact = Start + Direction * Range;
	if (GetWorld()->LineTraceSingleByChannel(Hit, Start, Impact, HITMEN_TRACE_WEAPON, Params))
	{
		Impact = Hit.ImpactPoint;
		AController* InstigatorController = HackedBy ? HackedBy->GetInstigatorController() : nullptr;
		UGameplayStatics::ApplyPointDamage(Hit.GetActor(), Damage, Direction, Hit, InstigatorController, this, UDamageType::StaticClass());
	}
	UHitmenAISubsystem::ReportNoise(this, Start, 900.f, this, EHitmenNoiseType::Gunshot);
	Multicast_Fire(Impact);
}

void AHitmenTurret::Multicast_Fire_Implementation(FVector_NetQuantize Impact)
{
	if (GetNetMode() != NM_DedicatedServer)
	{
		BarrelOffset = BarrelKick;
		const FVector Muzzle = Barrel->GetComponentLocation() + Head->GetForwardVector() * 40.f;
		if (MuzzleFX)
		{
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, MuzzleFX, Muzzle, Head->GetComponentRotation());
		}
		// Muzzle glow with a burst of light, and a red tracer to the impact.
		const UHitmenGameSettings* Visuals = UHitmenGameSettings::Get();
		AHitmenFlashBurst::SpawnBurst(GetWorld(), Muzzle, Visuals->FlashMaterial.LoadSynchronous(), FLinearColor(1.f, 0.45f, 0.2f), 6.f, 22.f, 0.06f, 6000.f, 500.f);
		AHitmenTracer::SpawnTracer(GetWorld(), Muzzle, Impact, Visuals->TracerMaterial.LoadSynchronous(), TracerColor);
		if (ImpactFX)
		{
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, ImpactFX, Impact);
		}
		if (FireSound)
		{
			UHitmenSoundLibrary::PlaySoundInSpace(this, FireSound, Muzzle, EHitmenSoundSpace::Gunshot);
		}
	}
	Juice_OnFire(Impact);
}

// ---------------------------------------------------------------------------------------------
// Hack panel
// ---------------------------------------------------------------------------------------------

AHitmenHackPanel::AHitmenHackPanel()
{
	// Fixtures are never vaulted, climbed or hung from.
	Tags.AddUnique(HitmenTags::NoTraversal);
	InteractionText = NSLOCTEXT("Hitmen", "HackTurret", "Hack turret");
}

bool AHitmenHackPanel::CanInteract_Implementation(AActor* Interactor) const
{
	return Super::CanInteract_Implementation(Interactor) && LinkedTurret && !LinkedTurret->IsHacked();
}

bool AHitmenHackPanel::CanRemoteActivate_Implementation() const
{
	return LinkedTurret && !LinkedTurret->IsHacked();
}

void AHitmenHackPanel::OnInteracted(AActor* Interactor)
{
	if (LinkedTurret)
	{
		LinkedTurret->Hack(Interactor);
	}
}

// ---------------------------------------------------------------------------------------------
// Lights / blackout / breaker
// ---------------------------------------------------------------------------------------------

AHitmenLightFixture::AHitmenLightFixture()
{
	bReplicates = true;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	SetRootComponent(Mesh);
	Light = CreateDefaultSubobject<UPointLightComponent>(TEXT("Light"));
	Light->SetupAttachment(Mesh);
	Light->SetIntensity(5000.f);
	Light->SetAttenuationRadius(1200.f);
	// Room lamps never glow in the volumetric fog (only camera beams do).
	Light->SetVolumetricScatteringIntensity(0.f);
	// No shadow unless a level asks for it (one lamp per room): every shadowed light costs a virtual shadow map pass.
	Light->SetCastShadows(false);
}

void AHitmenLightFixture::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenLightFixture, bPowered);
}

void AHitmenLightFixture::SetPowered(bool bInPowered)
{
	if (HasAuthority())
	{
		bPowered = bInPowered;
		OnRep_Powered();
	}
}

void AHitmenLightFixture::OnRep_Powered()
{
	Light->SetVisibility(bPowered);
	// The bulb goes dark with the power.
	if (!OnMaterial)
	{
		OnMaterial = Mesh->GetMaterial(0);
	}
	if (OffMaterial)
	{
		Mesh->SetMaterial(0, bPowered ? OnMaterial.Get() : OffMaterial.Get());
	}
	Juice_OnPowerChanged(bPowered);
}

AHitmenBlackoutVolume::AHitmenBlackoutVolume()
{
	bReplicates = true;
	Bounds = CreateDefaultSubobject<UBoxComponent>(TEXT("Bounds"));
	Bounds->SetBoxExtent(FVector(800.f, 800.f, 300.f));
	Bounds->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	SetRootComponent(Bounds);
}

void AHitmenBlackoutVolume::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenBlackoutVolume, bDark);
}

void AHitmenBlackoutVolume::BeginPlay()
{
	Super::BeginPlay();
	UHitmenAISubsystem::RegisterDarkZone(this);
}

void AHitmenBlackoutVolume::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UHitmenAISubsystem::UnregisterDarkZone(this);
	Super::EndPlay(EndPlayReason);
}

bool AHitmenBlackoutVolume::IsLocationDark(const FVector& Location) const
{
	if (!bDark)
	{
		return false;
	}
	const FVector Local = GetActorTransform().InverseTransformPosition(Location);
	const FVector Extent = Bounds->GetUnscaledBoxExtent();
	return FMath::Abs(Local.X) <= Extent.X && FMath::Abs(Local.Y) <= Extent.Y && FMath::Abs(Local.Z) <= Extent.Z;
}

AHitmenCircuitBreaker::AHitmenCircuitBreaker()
{
	// Fixtures are never vaulted, climbed or hung from.
	Tags.AddUnique(HitmenTags::NoTraversal);
	InteractionText = NSLOCTEXT("Hitmen", "CutPower", "Cut the power");
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	Lever = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Lever"));
	Lever->SetupAttachment(Root);
	Lever->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void AHitmenCircuitBreaker::OnRep_BreakerPowered()
{
	ApplyLever();
	if (GetNetMode() != NM_DedicatedServer && SwitchFX)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, SwitchFX, Lever->GetComponentLocation(), FRotator::ZeroRotator, FVector(0.4f));
	}
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::BreakerSwitch, GetActorLocation());
}

void AHitmenCircuitBreaker::ApplyLever()
{
	// Up when powered, thrown down when cut.
	Lever->SetRelativeRotation(FRotator(bPowered ? 35.f : -35.f, 0.f, 0.f));
}

void AHitmenCircuitBreaker::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenCircuitBreaker, bPowered);
}

FText AHitmenCircuitBreaker::GetInteractionText_Implementation(AActor* Interactor) const
{
	return bPowered ? InteractionText : NSLOCTEXT("Hitmen", "RestorePower", "Restore power");
}

void AHitmenCircuitBreaker::OnInteracted(AActor* Interactor)
{
	bPowered = !bPowered;
	OnRep_BreakerPowered();
	for (AHitmenLightFixture* Light : LinkedLights)
	{
		if (Light)
		{
			Light->SetPowered(bPowered);
		}
	}
	for (AHitmenBlackoutVolume* Volume : LinkedVolumes)
	{
		if (Volume)
		{
			Volume->SetDark(!bPowered);
		}
	}
	// The sudden darkness is suspicious: nearby NPCs come to check the breaker.
	UHitmenAISubsystem::ReportNoise(this, GetActorLocation(), 800.f, this, EHitmenNoiseType::Distraction);
}

float AHitmenSecurityCamera::TakeDamage(float DamageAmount, FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser)
{
	const float Taken = Super::TakeDamage(DamageAmount, DamageEvent, EventInstigator, DamageCauser);
	if (!HasAuthority() || bBroken || DamageAmount <= 0.f)
	{
		return Taken;
	}
	DamageTaken += DamageAmount;
	if (DamageTaken >= CameraHealth)
	{
		// Shot to pieces: sparks, the head drops, the feed is lost, a noise draws a guard.
		bBroken = true;
		SetCameraDisabled(true);
		Multicast_Broken();
		UHitmenAISubsystem::ReportNoise(this, GetActorLocation(), 800.f, EventInstigator ? EventInstigator->GetPawn() : nullptr, EHitmenNoiseType::Impact);
	}
	return DamageAmount;
}

void AHitmenSecurityCamera::Multicast_Broken_Implementation()
{
	if (BrokenFX && GetNetMode() != NM_DedicatedServer)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, BrokenFX, Body->GetComponentLocation(), FRotator::ZeroRotator, FVector(0.5f));
	}
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::BreakerSwitch, Body->GetComponentLocation());
}
