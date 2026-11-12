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
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenFootstep), false, Owner);
	Params.bReturnPhysicalMaterial = true;
	if (!GetWorld()->LineTraceSingleByChannel(Hit, Origin, Origin - FVector::UpVector * TraceLength, ECC_Visibility, Params))
	{
		return;
	}

	const EPhysicalSurface Surface = UPhysicalMaterial::DetermineSurfaceType(Hit.PhysMaterial.Get());
	const FHitmenSurfaceFeedback* Found = SurfaceFeedback.Find(Surface);
	const FHitmenSurfaceFeedback& Feedback = Found ? *Found : DefaultFeedback;

	if (Feedback.Sound && GetNetMode() != NM_DedicatedServer)
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
		const float Pitch = 1.f + FMath::FRandRange(-PitchJitter, PitchJitter);
		const APawn* Pawn = Cast<APawn>(Owner);
		if (Pawn && Pawn->IsLocallyControlled() && Pawn->IsPlayerControlled())
		{
			// Your own steps: close and centred, quieter than everyone else's.
			UGameplayStatics::PlaySound2D(this, Feedback.Sound, Volume * LocalPlayerVolume, Pitch);
		}
		else
		{
			// Everyone else's steps carry a few metres only (attenuation + occlusion).
			UHitmenSoundLibrary::PlaySoundInSpace(this, Feedback.Sound, Hit.ImpactPoint, EHitmenSoundSpace::Footstep, Volume, Pitch);
		}
	}
	if (Feedback.Effect)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Feedback.Effect, Hit.ImpactPoint);
	}

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
