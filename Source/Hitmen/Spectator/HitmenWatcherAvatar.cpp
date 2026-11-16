// Hitmen — watcher avatar

#include "Spectator/HitmenWatcherAvatar.h"
#include "Framework/HitmenPlayerState.h"
#include "Core/HitmenGameSettings.h"
#include "Components/StaticMeshComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/SkeletalMesh.h"
#include "Animation/AnimationAsset.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Net/VoiceConfig.h"
#include "Sound/SoundAttenuation.h"
#include "UObject/ConstructorHelpers.h"

AHitmenWatcherAvatar::AHitmenWatcherAvatar()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = false;
	SetActorEnableCollision(false);

	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> ConeMesh(TEXT("/Engine/BasicShapes/Cone.Cone"));

	// The actor turns with the view's yaw only (the ghost stands upright); the orb also takes the pitch.
	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);
	Orb = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Orb"));
	Orb->SetupAttachment(Root);
	Orb->SetStaticMesh(SphereMesh.Object);
	Orb->SetRelativeScale3D(FVector(0.22f));
	// A cone lying along +X: the lens shows where the watcher looks.
	Lens = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Lens"));
	Lens->SetupAttachment(Orb);
	Lens->SetStaticMesh(ConeMesh.Object);
	Lens->SetRelativeLocation(FVector(55.f, 0.f, 0.f));
	Lens->SetRelativeRotation(FRotator(-90.f, 0.f, 0.f));
	Lens->SetRelativeScale3D(FVector(0.6f, 0.6f, 0.7f));
	for (UStaticMeshComponent* Mesh : { Orb.Get(), Lens.Get() })
	{
		Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Mesh->SetCastShadow(false);
		Mesh->SetGenerateOverlapEvents(false);
	}
	Ghost = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("Ghost"));
	Ghost->SetupAttachment(Root);
	Ghost->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Ghost->SetCastShadow(false);
	Ghost->SetGenerateOverlapEvents(false);
	Ghost->SetVisibility(false);
	SetActorHiddenInGame(true);
}

void AHitmenWatcherAvatar::BeginPlay()
{
	Super::BeginPlay();
	if (UMaterialInterface* Flash = UHitmenGameSettings::Get()->FlashMaterial.LoadSynchronous())
	{
		Material = UMaterialInstanceDynamic::Create(Flash, this);
		Material->SetVectorParameterValue(TEXT("Color"), Color);
		Material->SetScalarParameterValue(TEXT("Opacity"), 0.85f);
		Orb->SetMaterial(0, Material);
		Lens->SetMaterial(0, Material);
	}
}

void AHitmenWatcherAvatar::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	// The voice must not stay attached to a destroyed avatar.
	if (VoiceTalker && Watcher.IsValid())
	{
		UVOIPStatics::ResetPlayerVoiceTalker(Watcher.Get());
	}
	VoiceTalker = nullptr;
	Super::EndPlay(EndPlayReason);
}

void AHitmenWatcherAvatar::BuildGhost()
{
	if (bGhostBuilt)
	{
		return;
	}
	bGhostBuilt = true;
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	if (USkeletalMesh* Mesh = Settings->WatcherGhostMesh.LoadSynchronous())
	{
		Ghost->SetSkeletalMeshAsset(Mesh);
		// The mannequin faces +Y; feet under the view, eyes at it.
		Ghost->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -GhostEyeHeight), FRotator(0.f, -90.f, 0.f));
		UMaterialInterface* Base = Settings->WatcherGhostMaterial.LoadSynchronous();
		if (!Base)
		{
			Base = Settings->FlashMaterial.LoadSynchronous();
		}
		if (Base)
		{
			GhostMaterial = UMaterialInstanceDynamic::Create(Base, this);
			GhostMaterial->SetVectorParameterValue(TEXT("Color"), Settings->WatcherGhostColor);
			GhostMaterial->SetScalarParameterValue(TEXT("Opacity"), Settings->WatcherGhostColor.A);
			for (int32 Index = 0; Index < Ghost->GetNumMaterials(); ++Index)
			{
				Ghost->SetMaterial(Index, GhostMaterial);
			}
		}
		if (UAnimationAsset* Idle = Settings->WatcherGhostAnimation.LoadSynchronous())
		{
			Ghost->PlayAnimation(Idle, true);
		}
	}
}

void AHitmenWatcherAvatar::SetPresentation(bool bInShown, bool bGhost)
{
	if (bGhost)
	{
		BuildGhost();
	}
	const bool bHasGhost = Ghost->GetSkeletalMeshAsset() != nullptr;
	bShown = bInShown;
	bShowGhost = bGhost && bHasGhost;
	SetActorHiddenInGame(!bShown);
	Orb->SetVisibility(bShown && !bShowGhost, true);
	Ghost->SetVisibility(bShown && bShowGhost);
}

void AHitmenWatcherAvatar::SetVoice(bool bProximity, USoundAttenuation* Attenuation)
{
	AHitmenPlayerState* PS = Watcher.Get();
	if (!PS || bProximity == bVoiceProximity || GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	bVoiceProximity = bProximity;
	UVOIPStatics::ResetPlayerVoiceTalker(PS);
	VoiceTalker = nullptr;
	if (bProximity)
	{
		VoiceTalker = UVOIPTalker::CreateTalkerForPlayer(PS);
		if (VoiceTalker)
		{
			VoiceTalker->Settings.ComponentToAttachTo = GetRootComponent();
			VoiceTalker->Settings.AttenuationSettings = Attenuation;
		}
	}
}

void AHitmenWatcherAvatar::SetWatcher(AHitmenPlayerState* InWatcher)
{
	Watcher = InWatcher;
	bPlaced = false;
}

void AHitmenWatcherAvatar::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	AHitmenPlayerState* PS = Watcher.Get();
	if (!PS)
	{
		return;
	}
	const FHitmenWatcherView& View = PS->GetWatcherView();
	const FVector TargetLocation = View.Location;
	const FRotator TargetRotation(0.f, View.Yaw, 0.f);
	if (!bPlaced)
	{
		SetActorLocationAndRotation(TargetLocation, TargetRotation);
		Orb->SetRelativeRotation(FRotator(View.Pitch, 0.f, 0.f));
		bPlaced = true;
	}
	else
	{
		SetActorLocationAndRotation(FMath::VInterpTo(GetActorLocation(), TargetLocation, DeltaSeconds, InterpSpeed),
			FMath::RInterpTo(GetActorRotation(), TargetRotation, DeltaSeconds, InterpSpeed));
		Orb->SetRelativeRotation(FMath::RInterpTo(Orb->GetRelativeRotation(), FRotator(View.Pitch, 0.f, 0.f), DeltaSeconds, InterpSpeed));
	}
	PS->SetSmoothedWatcherLocation(GetActorLocation());
}
