// Hitmen — security fixtures

#include "World/Interactables/HitmenSecurity.h"
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
}

void AHitmenSecurityCamera::BeginPlay()
{
	Super::BeginPlay();
	BaseHeadRotation = Head->GetRelativeRotation();
	Capture->FOVAngle = ViewHalfAngle * 2.f;

	// Cone length / width follow the view settings (default mesh: engine cone pointing up).
	const float Radius = FMath::Tan(FMath::DegreesToRadians(ViewHalfAngle)) * ViewDistance;
	ViewCone->SetRelativeLocation(FVector(ViewDistance * 0.5f, 0.f, 0.f));
	ViewCone->SetRelativeRotation(FRotator(90.f, 0.f, 0.f));
	ViewCone->SetRelativeScale3D(FVector(Radius / 50.f, Radius / 50.f, ViewDistance / 100.f));

	// Footprint on the floor: project straight down where the cone hits the ground.
	FHitResult Ground;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenCameraFloor), false, this);
	const FVector Eye = Head->GetComponentLocation();
	const FVector Along = Eye + Head->GetForwardVector() * ViewDistance;
	FVector FloorPoint = Along;
	if (GetWorld()->LineTraceSingleByChannel(Ground, Eye, Along, ECC_Visibility, Params))
	{
		FloorPoint = Ground.ImpactPoint;
	}
	const float FootRadius = FMath::Tan(FMath::DegreesToRadians(ViewHalfAngle)) * FVector::Dist(Eye, FloorPoint);
	FloorDecal->SetUsingAbsoluteRotation(true);
	FloorDecal->SetWorldLocation(FloorPoint);
	FloorDecal->SetWorldRotation(FRotator(-90.f, Head->GetComponentRotation().Yaw, 0.f));
	FloorDecal->DecalSize = FVector(400.f, FootRadius, FootRadius);

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
	OnRep_Visuals();
	SetActorTickEnabled(HasAuthority() || SweepAngle > 0.f);
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
		FHitResult Hit;
		if (!GetWorld()->LineTraceSingleByChannel(Hit, Eye, Target, ECC_Visibility, Params) || Hit.GetActor() == Character)
		{
			return Character;
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
	if (HasAuthority())
	{
		bDisabled = bInDisabled;
		bSeeingIntruder = false;
		OnRep_Visuals();
	}
}

void AHitmenSecurityCamera::OnRep_Visuals()
{
	ViewCone->SetVisibility(!bDisabled);
	FloorDecal->SetVisibility(!bDisabled);
	if (bLastBroadcastDisabled != bDisabled)
	{
		bLastBroadcastDisabled = bDisabled;
		// Monitors swap to "no signal" and stop requesting captures; CaptureTo refuses meanwhile.
		OnDisabledChanged.Broadcast(bDisabled);
	}
	const FLinearColor Color = bSeeingIntruder ? AlertColor : IdleColor;
	for (UMaterialInstanceDynamic* Material : ColorMaterials)
	{
		if (Material)
		{
			Material->SetVectorParameterValue(TEXT("Color"), Color);
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
}

void AHitmenSecurityMonitor::BeginPlay()
{
	Super::BeginPlay();
	if (GetNetMode() == NM_DedicatedServer || !LinkedCamera)
	{
		return;
	}
	RenderTarget = UKismetRenderingLibrary::CreateRenderTarget2D(this, 512, 288);
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
		// Stop refreshing (no capture cost) and show the "no signal" screen instead of a frozen frame.
		GetWorldTimerManager().ClearTimer(RefreshTimer);
		if (UMaterialInterface* NoSignal = NoSignalMaterial.LoadSynchronous())
		{
			Screen->SetMaterial(ScreenMaterialIndex, NoSignal);
		}
	}
	else
	{
		if (FeedMaterial)
		{
			Screen->SetMaterial(ScreenMaterialIndex, FeedMaterial);
		}
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
}

void AHitmenControlConsole::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenControlConsole, bCamerasDisabled);
}

FText AHitmenControlConsole::GetInteractionText_Implementation(AActor* Interactor) const
{
	return bCamerasDisabled ? NSLOCTEXT("Hitmen", "EnableCameras", "Enable cameras") : InteractionText;
}

void AHitmenControlConsole::OnInteracted(AActor* Interactor)
{
	bCamerasDisabled = !bCamerasDisabled;
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
	if (!CurrentTarget || GetWorld()->GetTimeSeconds() - LastFireTime < FireInterval)
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
	UHitmenAISubsystem::ReportNoise(this, Start, 2000.f, this, EHitmenNoiseType::Gunshot);
	Multicast_Fire(Impact);
}

void AHitmenTurret::Multicast_Fire_Implementation(FVector_NetQuantize Impact)
{
	Juice_OnFire(Impact);
}

// ---------------------------------------------------------------------------------------------
// Hack panel
// ---------------------------------------------------------------------------------------------

AHitmenHackPanel::AHitmenHackPanel()
{
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
	InteractionText = NSLOCTEXT("Hitmen", "CutPower", "Cut the power");
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
	UHitmenAISubsystem::ReportNoise(this, GetActorLocation(), 1200.f, this, EHitmenNoiseType::Distraction);
}
