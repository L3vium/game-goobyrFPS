// Hitmen — VIP tracker (local)

#include "Framework/HitmenVIPTracker.h"
#include "Core/HitmenText.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenPlayerController.h"
#include "Characters/HitmenCharacterBase.h"
#include "World/Spawning/HitmenRetrievalPoint.h"
#include "Core/HitmenGameSettings.h"
#include "Data/HitmenSoundSet.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Components/PoseableMeshComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/PostProcessVolume.h"
#include "Camera/PlayerCameraManager.h"
#include "Engine/World.h"
#include "Hitmen.h"

// ---------------------------------------------------------------------------------------------
// Ghost
// ---------------------------------------------------------------------------------------------

AHitmenBodyGhost::AHitmenBodyGhost()
{
	PrimaryActorTick.bCanEverTick = false;
	bReplicates = false;
	SetCanBeDamaged(false);
	Pose = CreateDefaultSubobject<UPoseableMeshComponent>(TEXT("Pose"));
	SetRootComponent(Pose);
	Pose->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Pose->SetCastShadow(false);
	// Only the marker pass sees it: a yellow silhouette through walls, nothing in the normal image.
	Pose->SetRenderInMainPass(false);
	Pose->SetRenderInDepthPass(false);
	Pose->SetRenderCustomDepth(true);
	Pose->SetCustomDepthStencilValue(HitmenStencil::VIPMarker);
	Pose->SetVisibility(false);
}

void AHitmenBodyGhost::CopyFrom(const AHitmenCharacterBase* Body)
{
	const USkeletalMeshComponent* Source = Body ? Body->GetMesh() : nullptr;
	if (!Source || !Source->GetSkeletalMeshAsset())
	{
		return;
	}
	if (Pose->GetSkinnedAsset() != Source->GetSkeletalMeshAsset())
	{
		Pose->SetSkinnedAssetAndUpdate(Source->GetSkeletalMeshAsset());
	}
	Pose->SetWorldTransform(Source->GetComponentTransform());
	Pose->CopyPoseFromSkeletalComponent(const_cast<USkeletalMeshComponent*>(Source));
	Pose->SetVisibility(true);
}

void AHitmenBodyGhost::SetShown(bool bShown)
{
	Pose->SetVisibility(bShown);
}

// ---------------------------------------------------------------------------------------------
// Tracker
// ---------------------------------------------------------------------------------------------

UHitmenVIPTrackerComponent::UHitmenVIPTrackerComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.TickInterval = 0.f;
}

APlayerController* UHitmenVIPTrackerComponent::GetPC() const
{
	return Cast<APlayerController>(GetOwner());
}

void UHitmenVIPTrackerComponent::BeginPlay()
{
	Super::BeginPlay();
	const APlayerController* PC = GetPC();
	SetComponentTickEnabled(PC && PC->IsLocalController() && GetNetMode() != NM_DedicatedServer);
}

void UHitmenVIPTrackerComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (AHitmenGameState* GS = BoundState.Get())
	{
		GS->OnSonarPing.RemoveAll(this);
		GS->OnExtractionStarted.RemoveAll(this);
	}
	if (Ghost)
	{
		Ghost->Destroy();
	}
	if (MarkerVolume)
	{
		MarkerVolume->Destroy();
	}
	Super::EndPlay(EndPlayReason);
}

void UHitmenVIPTrackerComponent::BindGameState()
{
	AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	if (!GS || BoundState.Get() == GS)
	{
		return;
	}
	BoundState = GS;
	GS->OnSonarPing.AddUniqueDynamic(this, &UHitmenVIPTrackerComponent::HandleSonar);
	GS->OnExtractionStarted.AddUniqueDynamic(this, &UHitmenVIPTrackerComponent::HandleExtraction);
	// Joined late, or the VIP died before this machine could see it: no toast, just the markers.
	bDeathSeen = GS->IsVIPDead() && GS->GetMatchPhase() == EHitmenMatchPhase::Ended;
}

void UHitmenVIPTrackerComponent::EnsureMarkerVolume()
{
	if (MarkerVolume)
	{
		return;
	}
	UMaterialInterface* Material = UHitmenGameSettings::Get()->MarkerPostProcessMaterial.LoadSynchronous();
	if (!Material)
	{
		return;
	}
	// An unbound local volume: the markers show in whatever view this machine uses (player, spectator, cinematic).
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	Params.ObjectFlags |= RF_Transient;
	MarkerVolume = GetWorld()->SpawnActor<APostProcessVolume>(Params);
	if (MarkerVolume)
	{
		MarkerVolume->bUnbound = true;
		MarkerVolume->Priority = 100.f;
		MarkerVolume->BlendWeight = 1.f;
		MarkerVolume->Settings.AddBlendable(Material, 1.f);
	}
}

void UHitmenVIPTrackerComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	BindGameState();
	AHitmenGameState* GS = BoundState.Get();
	if (!GS)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	if (Now - LastCheck < CheckInterval)
	{
		return;
	}
	LastCheck = Now;
	EnsureMarkerVolume();
	AHitmenCharacterBase* Body = GS->GetVIP();
	const bool bDead = Body && GS->IsVIPDead();
	if (bDead && !bDeathSeen)
	{
		bDeathSeen = true;
		OnVIPDied(Body);
	}
	const bool bOver = GS->GetMatchPhase() == EHitmenMatchPhase::Ended;
	UpdateRetrievalPoints(bDead && UHitmenGameSettings::Get()->bShowRetrievalPointsAfterVIPDeath && !bCinematic && !bOver);
	if (bDead && !bOver)
	{
		UpdateBody(Body);
	}
}

void UHitmenVIPTrackerComponent::OnVIPDied(AHitmenCharacterBase* Body)
{
	APlayerController* PC = GetPC();
	UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(PC);
	if (UI)
	{
		FHitmenToast Toast;
		Toast.Title = HITMEN_TEXT("Hitmen.TargetEliminated", "Target eliminated");
		Toast.Subtitle = HITMEN_TEXT("Hitmen.TargetEliminatedSub", "Its body shows in yellow; the retrieval points in gold");
		Toast.Duration = 4.5f;
		UI->PushToast(Toast);
	}
	UHitmenSoundLibrary::PlayHitmenSound2D(PC, EHitmenSound::TargetEliminated);
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	if (AHitmenPlayerController* HitmenPC = Cast<AHitmenPlayerController>(PC); HitmenPC && Settings->bEventCameraOnTargetKill)
	{
		HitmenPC->ShowEventCamera(Body, HITMEN_TEXT("Hitmen.EventTargetDown", "Target down"));
	}
}

bool UHitmenVIPTrackerComponent::CanSeeBody(const AHitmenCharacterBase* Body) const
{
	const APlayerController* PC = GetPC();
	const APlayerCameraManager* Camera = PC ? PC->PlayerCameraManager.Get() : nullptr;
	if (!Camera || !Body)
	{
		return false;
	}
	const FVector Eye = Camera->GetCameraLocation();
	const FVector Forward = Camera->GetCameraRotation().Vector();
	const float HalfFOV = FMath::DegreesToRadians(Camera->GetFOVAngle() * 0.5f + 5.f);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenVIPSight), false, Body);
	Params.AddIgnoredActor(PC->GetPawn());
	if (const AActor* Carrier = Body->GetAttachParentActor())
	{
		Params.AddIgnoredActor(Carrier);
	}
	const USkeletalMeshComponent* Mesh = Body->GetMesh();
	for (const FName Bone : { FName(TEXT("pelvis")), FName(TEXT("head")) })
	{
		const FVector Point = Mesh && Mesh->DoesSocketExist(Bone) ? Mesh->GetSocketLocation(Bone) : Body->GetActorLocation();
		const FVector To = Point - Eye;
		// In the view (with a margin) and nothing in between.
		if (FVector::DotProduct(To.GetSafeNormal(), Forward) < FMath::Cos(HalfFOV))
		{
			continue;
		}
		FHitResult Hit;
		if (!GetWorld()->LineTraceSingleByChannel(Hit, Eye, Point, ECC_Visibility, Params))
		{
			return true;
		}
	}
	return false;
}

void UHitmenVIPTrackerComponent::SetBodyMarker(AHitmenCharacterBase* Body, bool bMarked) const
{
	USkeletalMeshComponent* Mesh = Body ? Body->GetMesh() : nullptr;
	if (!Mesh)
	{
		return;
	}
	// The Sightroom may have set its own stencil: only the marker value is touched here.
	if (bMarked && (!Mesh->bRenderCustomDepth || Mesh->CustomDepthStencilValue != HitmenStencil::VIPMarker))
	{
		Mesh->SetRenderCustomDepth(true);
		Mesh->SetCustomDepthStencilValue(HitmenStencil::VIPMarker);
	}
	else if (!bMarked && Mesh->CustomDepthStencilValue == HitmenStencil::VIPMarker)
	{
		Mesh->SetRenderCustomDepth(false);
		Mesh->SetCustomDepthStencilValue(0);
	}
}

void UHitmenVIPTrackerComponent::UpdateBody(AHitmenCharacterBase* Body)
{
	if (!Ghost)
	{
		FActorSpawnParameters Params;
		Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		Params.ObjectFlags |= RF_Transient;
		Ghost = GetWorld()->SpawnActor<AHitmenBodyGhost>(Params);
	}
	const APlayerController* PC = GetPC();
	// Extracted, or in your own arms (you know where it is): no marker.
	const bool bGone = Body->IsVanished() || bCinematic;
	const bool bMine = PC && Body->GetCarrier() && Body->GetCarrier() == PC->GetPawn();
	if (bGone || bMine)
	{
		SetBodyMarker(Body, false);
		if (Ghost)
		{
			Ghost->SetShown(false);
		}
		bBodyInSight = false;
		return;
	}
	const bool bSeen = CanSeeBody(Body);
	if (bSeen)
	{
		// In sight: the real body, outlined live.
		SetBodyMarker(Body, true);
		if (Ghost)
		{
			Ghost->SetShown(false);
		}
	}
	else
	{
		SetBodyMarker(Body, false);
		if (bBodyInSight && Ghost)
		{
			// Just lost from sight: the ghost stays where you last saw it.
			Ghost->CopyFrom(Body);
			GhostTime = GetWorld()->GetTimeSeconds();
		}
		if (Ghost && GhostTime >= 0.f)
		{
			Ghost->SetShown(true);
		}
	}
	bBodyInSight = bSeen;
}

void UHitmenVIPTrackerComponent::HandleSonar(const FHitmenSonarPing& Ping)
{
	const AHitmenGameState* GS = BoundState.Get();
	AHitmenCharacterBase* Body = GS ? GS->GetVIP() : nullptr;
	if (!Body || !GS->IsVIPDead() || bBodyInSight || bCinematic)
	{
		return;
	}
	// A ping is newer than anything you saw yourself: the ghost jumps to where the body is now.
	if (!Ghost)
	{
		UpdateBody(Body);
	}
	if (Ghost && !Body->IsVanished())
	{
		Ghost->CopyFrom(Body);
		GhostTime = GetWorld()->GetTimeSeconds();
	}
}

void UHitmenVIPTrackerComponent::UpdateRetrievalPoints(bool bShow) const
{
	const AHitmenGameState* GS = BoundState.Get();
	if (!GS)
	{
		return;
	}
	for (AActor* Actor : GS->GetActiveRetrievalPoints())
	{
		if (AHitmenRetrievalPoint* Point = Cast<AHitmenRetrievalPoint>(Actor))
		{
			Point->SetMarkerHighlight(bShow);
		}
	}
}

void UHitmenVIPTrackerComponent::HandleExtraction()
{
	const AHitmenGameState* GS = BoundState.Get();
	AActor* Point = GS ? GS->GetExtractionPoint() : nullptr;
	APlayerController* PC = GetPC();
	if (!Point || !PC)
	{
		return;
	}
	// Everyone watches the extraction leave: the view blends to its camera, the HUD hides, letterbox bars.
	bCinematic = true;
	if (Ghost)
	{
		Ghost->SetShown(false);
	}
	if (AHitmenPlayerController* HitmenPC = Cast<AHitmenPlayerController>(PC))
	{
		HitmenPC->StopEventCamera();
	}
	PC->SetViewTargetWithBlend(Point, CinematicBlendTime, VTBlend_EaseInOut, 2.f);
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(PC))
	{
		UI->SetCinematic(true);
	}
}

void UHitmenVIPTrackerComponent::EndCinematic()
{
	if (!bCinematic)
	{
		return;
	}
	bCinematic = false;
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(GetPC()))
	{
		UI->SetCinematic(false);
	}
}
