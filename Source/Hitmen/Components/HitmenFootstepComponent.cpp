// Hitmen — footsteps

#include "Components/HitmenFootstepComponent.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Framework/HitmenGameModeBase.h"
#include "Data/HitmenMatchRules.h"
#include "AI/HitmenAISubsystem.h"
#include "Data/HitmenSoundSet.h"
#include "GameFramework/Character.h"
#include "Kismet/GameplayStatics.h"
#include "NiagaraFunctionLibrary.h"
#include "PhysicalMaterials/PhysicalMaterial.h"
#include "Engine/World.h"
#include "Core/HitmenGameSettings.h"
#include "Hitmen.h"

UHitmenFootstepComponent::UHitmenFootstepComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.TickInterval = 0.05f;

	// One entry per project surface (DefaultEngine.ini) so the team only drops sounds/VFX in.
	// Noise multipliers: sand muffles, metal rings out.
	auto AddSurface = [this](EPhysicalSurface Surface, float NoiseMultiplier)
	{
		FHitmenSurfaceFeedback Feedback;
		Feedback.NoiseMultiplier = NoiseMultiplier;
		SurfaceFeedback.Add(Surface, Feedback);
	};
	AddSurface(SurfaceType1, 0.8f);  // Grass
	AddSurface(SurfaceType2, 1.f);   // Stone
	AddSurface(SurfaceType3, 0.6f);  // Sand
	AddSurface(SurfaceType4, 1.1f);  // Wood
	AddSurface(SurfaceType5, 1.4f);  // Metal
}

void UHitmenFootstepComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);

	const ACharacter* Character = Cast<ACharacter>(GetOwner());
	const UHitmenCharacterMovementComponent* Movement = Character ? Cast<UHitmenCharacterMovementComponent>(Character->GetCharacterMovement()) : nullptr;
	const FVector Location = GetOwner()->GetActorLocation();
	const float Travelled = FVector::Dist2D(Location, LastLocation);
	LastLocation = Location;

	if (bExternalStepTiming || !bUseDistanceFallback || !Movement || !Movement->IsMovingOnGround() || Movement->IsSliding() || GetWorld()->GetTimeSeconds() - LastNotifyTime < NotifySuppressesFallbackTime)
	{
		DistanceAccumulator = 0.f;
		return;
	}
	DistanceAccumulator += Travelled;
	const float Stride = Movement->IsCrouching() ? CrouchStrideLength : (Movement->IsSprinting() ? SprintStrideLength : WalkStrideLength);
	if (DistanceAccumulator >= Stride * 0.5f)
	{
		DistanceAccumulator = 0.f;
		HandleFootstepInternal(NAME_None);
		// HandleFootstep marks LastNotifyTime; fallback steps must not suppress themselves.
		LastNotifyTime = -100.f;
	}
}

float UHitmenFootstepComponent::ComputeNoiseRadius(float SurfaceMultiplier) const
{
	const ACharacter* Character = Cast<ACharacter>(GetOwner());
	const UHitmenCharacterMovementComponent* Movement = Character ? Cast<UHitmenCharacterMovementComponent>(Character->GetCharacterMovement()) : nullptr;
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	const UHitmenMatchRules* Rules = GM ? GM->GetRules() : nullptr;
	if (!Movement || !Rules)
	{
		return 0.f;
	}
	float Radius = Rules->WalkFootstepRadius;
	if (Movement->IsCrouching())
	{
		Radius = Rules->CrouchFootstepRadius;
	}
	else if (Movement->IsSprinting())
	{
		Radius = Rules->SprintFootstepRadius;
	}
	return Radius * SurfaceMultiplier;
}

void UHitmenFootstepComponent::HandleFootstep(FName FootBone)
{
	// Anim notifies: ignored while the owner times its own steps (first-person head bob).
	if (bExternalStepTiming)
	{
		return;
	}
	HandleFootstepInternal(FootBone);
}

const FHitmenSurfaceFeedback& UHitmenFootstepComponent::GetFeedback(EPhysicalSurface Surface) const
{
	// A character override with a sound wins; otherwise the project surface table (DA_SoundSet).
	const FHitmenSurfaceFeedback* Own = SurfaceFeedback.Find(Surface);
	if (Own && Own->HasSound())
	{
		return *Own;
	}
	if (const UHitmenSoundSet* Set = UHitmenSoundSet::GetProjectSet(); Set && (Set->Surfaces.Num() > 0 || Set->DefaultSurface.HasSound()))
	{
		return Set->GetSurface(Surface);
	}
	return Own ? *Own : DefaultFeedback;
}

void UHitmenFootstepComponent::SpawnStepEffect(const FHitmenSurfaceFeedback& Feedback, const FVector& Location) const
{
	if (Feedback.Effect && UHitmenGameSettings::Get()->bFootstepFX && GetNetMode() != NM_DedicatedServer)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Feedback.Effect, Location, FRotator::ZeroRotator, FVector(Feedback.EffectScale));
	}
}

bool UHitmenFootstepComponent::QuerySurface(const FVector& From, FHitResult& OutHit, EPhysicalSurface& OutSurface) const
{
	OutSurface = SurfaceType_Default;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenFootstep), true, GetOwner());
	Params.bReturnPhysicalMaterial = true;
	if (!GetWorld()->LineTraceSingleByChannel(OutHit, From, From - FVector::UpVector * TraceLength, ECC_Visibility, Params))
	{
		return false;
	}
	OutSurface = UPhysicalMaterial::DetermineSurfaceType(OutHit.PhysMaterial.Get());
	if (const AActor* Ground = OutHit.GetActor(); Ground && Ground->ActorHasTag(HitmenTags::Terrain))
	{
		const float Up = OutHit.ImpactNormal.Z;
		OutSurface = Up >= TerrainGrassMinNormalZ ? TerrainGrassSurface.GetValue() : (Up <= TerrainRockMaxNormalZ ? TerrainRockSurface.GetValue() : TerrainEarthSurface.GetValue());
	}
	return true;
}

USoundBase* UHitmenFootstepComponent::PickStepSound(const FHitmenSurfaceFeedback& Feedback)
{
	const int32 Count = Feedback.Sounds.Num();
	if (Count == 0)
	{
		return Feedback.Sound;
	}
	int32 Index = FMath::RandRange(0, Count - 1);
	if (Count > 1 && Index == LastVariation)
	{
		Index = (Index + 1 + FMath::RandRange(0, Count - 2)) % Count;
	}
	LastVariation = Index;
	return Feedback.Sounds[Index];
}

void UHitmenFootstepComponent::PlayStepSound(USoundBase* Sound, const FVector& Location, float Volume, float Pitch) const
{
	if (!Sound || GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	const APawn* Pawn = Cast<APawn>(GetOwner());
	if (Pawn && Pawn->IsLocallyControlled() && Pawn->IsPlayerControlled())
	{
		// Your own steps: close and centred, quieter than everyone else's.
		UGameplayStatics::PlaySound2D(this, Sound, Volume * LocalPlayerVolume, Pitch);
	}
	else
	{
		// Everyone else's steps carry a few metres only (attenuation + occlusion).
		UHitmenSoundLibrary::PlaySoundInSpace(this, Sound, Location, EHitmenSoundSpace::Footstep, Volume, Pitch);
	}
}

void UHitmenFootstepComponent::PlayLanding(float FallSpeed, bool bHard)
{
	FHitResult Hit;
	EPhysicalSurface Surface;
	if (!QuerySurface(GetOwner()->GetActorLocation(), Hit, Surface))
	{
		return;
	}
	const FHitmenSurfaceFeedback& Feedback = GetFeedback(Surface);
	const float Pitch = 1.f + FMath::FRandRange(-PitchJitter, PitchJitter);
	PlayStepSound(PickStepSound(Feedback), Hit.ImpactPoint, LandStepVolume * Feedback.Volume, Pitch * 0.94f * Feedback.Pitch);
	const TArray<TObjectPtr<USoundBase>>& Thumps = bHard && HardLandThumps.Num() > 0 ? HardLandThumps : LandThumps;
	if (Thumps.Num() > 0)
	{
		PlayStepSound(Thumps[FMath::RandRange(0, Thumps.Num() - 1)], Hit.ImpactPoint, bHard ? HardLandThumpVolume : LandThumpVolume, Pitch);
	}
	SpawnStepEffect(Feedback, Hit.ImpactPoint);
}

void UHitmenFootstepComponent::HandleFootstepInternal(FName FootBone)
{
	LastNotifyTime = GetWorld()->GetTimeSeconds();

	const AActor* Owner = GetOwner();
	FVector Origin = Owner->GetActorLocation();
	if (const ACharacter* Character = Cast<ACharacter>(Owner); Character && FootBone != NAME_None && Character->GetMesh())
	{
		Origin = Character->GetMesh()->GetSocketLocation(FootBone) + FVector::UpVector * TraceStartHeight;
	}

	FHitResult Hit;
	EPhysicalSurface Surface;
	if (!QuerySurface(Origin, Hit, Surface))
	{
		return;
	}
	const FHitmenSurfaceFeedback& Feedback = GetFeedback(Surface);
	if (GetNetMode() != NM_DedicatedServer)
	{
		const ACharacter* Character = Cast<ACharacter>(Owner);
		const UHitmenCharacterMovementComponent* Movement = Character ? Cast<UHitmenCharacterMovementComponent>(Character->GetCharacterMovement()) : nullptr;
		float Volume = WalkVolume;
		if (Movement && Movement->IsCrouching())
		{
			Volume = CrouchVolume;
		}
		else if (Movement && Movement->IsSprinting())
		{
			Volume = SprintVolume;
		}
		PlayStepSound(PickStepSound(Feedback), Hit.ImpactPoint, Volume * Feedback.Volume, (1.f + FMath::FRandRange(-PitchJitter, PitchJitter)) * Feedback.Pitch);
	}
	SpawnStepEffect(Feedback, Hit.ImpactPoint);

	float NoiseRadius = 0.f;
	if (Owner->HasAuthority() && bReportNoiseToAI)
	{
		NoiseRadius = ComputeNoiseRadius(Feedback.NoiseMultiplier);
		if (NoiseRadius > 0.f)
		{
			UHitmenAISubsystem::ReportNoise(this, Hit.ImpactPoint, NoiseRadius, const_cast<AActor*>(Owner), EHitmenNoiseType::Footstep);
		}
	}
	OnFootstep.Broadcast(Surface, Hit.ImpactPoint, NoiseRadius);
}
