// Hitmen — predicted movement

#include "Movement/HitmenCharacterMovementComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Core/HitmenInterfaces.h"
#include "Components/CapsuleComponent.h"
#include "GameFramework/Character.h"
#include "Engine/World.h"

namespace
{
	uint8 ToCustom(EHitmenCustomMovement Mode) { return static_cast<uint8>(Mode); }
}

UHitmenCharacterMovementComponent::UHitmenCharacterMovementComponent()
{
	MaxWalkSpeed = WalkSpeed;
	MaxWalkSpeedCrouched = 200.f;
	NavAgentProps.bCanCrouch = true;
	bCanWalkOffLedgesWhenCrouching = true;
	JumpZVelocity = 450.f;
	AirControl = 0.35f;
	BrakingDecelerationFalling = 1200.f;
	GravityScale = 1.4f;
	SetCrouchedHalfHeight(55.f);
}

// ---------------------------------------------------------------------------------------------
// State queries
// ---------------------------------------------------------------------------------------------

bool UHitmenCharacterMovementComponent::IsCustomMode(EHitmenCustomMovement Mode) const
{
	return MovementMode == MOVE_Custom && CustomMovementMode == ToCustom(Mode);
}

EHitmenCustomMovement UHitmenCharacterMovementComponent::GetCustomMovement() const
{
	return MovementMode == MOVE_Custom ? static_cast<EHitmenCustomMovement>(CustomMovementMode) : EHitmenCustomMovement::None;
}

bool UHitmenCharacterMovementComponent::IsSprinting() const
{
	if (!bWantsToSprint || IsCrouching() || !CharacterOwner)
	{
		return false;
	}
	if (!(MovementMode == MOVE_Walking || MovementMode == MOVE_NavWalking || MovementMode == MOVE_Falling))
	{
		return false;
	}
	const FVector Input = Acceleration.GetSafeNormal2D();
	return !Input.IsNearlyZero() && FVector::DotProduct(Input, CharacterOwner->GetActorForwardVector()) >= SprintMinForwardDot;
}

float UHitmenCharacterMovementComponent::GetTimeSeconds() const
{
	return GetWorld() ? GetWorld()->GetTimeSeconds() : 0.f;
}

float UHitmenCharacterMovementComponent::GetOwnerSpeedMultiplier() const
{
	const AHitmenCharacterBase* Owner = Cast<AHitmenCharacterBase>(CharacterOwner);
	if (!Owner)
	{
		return 1.f;
	}
	float Multiplier = Owner->GetMovementSpeedMultiplier();
	if (Owner->IsAiming())
	{
		Multiplier *= AimSpeedMultiplier;
	}
	return Multiplier;
}

float UHitmenCharacterMovementComponent::GetMaxSpeed() const
{
	switch (MovementMode)
	{
	case MOVE_Walking:
	case MOVE_NavWalking:
	case MOVE_Falling:
	{
		const float Base = IsCrouching() ? MaxWalkSpeedCrouched : (IsSprinting() ? SprintSpeed : WalkSpeed);
		return Base * GetOwnerSpeedMultiplier();
	}
	case MOVE_Custom:
		switch (static_cast<EHitmenCustomMovement>(CustomMovementMode))
		{
		case EHitmenCustomMovement::Slide: return SprintSpeed + SprintSlideImpulse;
		case EHitmenCustomMovement::Climb:
			if (const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(ClimbTarget.Get()))
			{
				return Climbable->GetClimbSpeed();
			}
			return 300.f;
		default: return 0.f;
		}
	default:
		return Super::GetMaxSpeed();
	}
}

float UHitmenCharacterMovementComponent::GetMaxBrakingDeceleration() const
{
	if (IsSliding())
	{
		return SlideBrakingDeceleration;
	}
	return Super::GetMaxBrakingDeceleration();
}

bool UHitmenCharacterMovementComponent::IsMovingOnGround() const
{
	return Super::IsMovingOnGround() || IsSliding();
}

bool UHitmenCharacterMovementComponent::CanAttemptJump() const
{
	if (IsClimbing() || IsMantling())
	{
		return false;
	}
	if (IsSliding())
	{
		return IsJumpAllowed();
	}
	return Super::CanAttemptJump();
}

// ---------------------------------------------------------------------------------------------
// Per-move state update (runs on owning client and server)
// ---------------------------------------------------------------------------------------------

void UHitmenCharacterMovementComponent::UpdateCharacterStateBeforeMovement(float DeltaSeconds)
{
	// Slide starts on the crouch press while moving (before Super crouches us).
	if (bWantsToCrouch && !IsCrouching() && CanStartSlide())
	{
		StartSlide();
	}

	Super::UpdateCharacterStateBeforeMovement(DeltaSeconds);

	if (!CharacterOwner)
	{
		return;
	}
	const bool bJumpHeld = CharacterOwner->bPressedJump;
	const bool bJumpPressedNow = bJumpHeld && !bPrevPressedJump;
	const float Now = GetTimeSeconds();

	if (IsClimbing())
	{
		if (bJumpPressedNow)
		{
			StopClimbing(true);
		}
	}
	else if (!IsMantling())
	{
		// Ledge grab / vault: space in front of a wall, on the ground or in the air (held).
		const bool bCanTryMantle = IsMovingOnGround() || (bAllowAirMantle && IsFalling());
		if (bJumpHeld && bCanTryMantle && Now - LastMantleTime > MantleCooldown)
		{
			TryStartMantle();
		}

		// Ladders / ziplines are automatic: walk or jump into them.
		if (!IsMantling() && (IsFalling() || (Super::IsMovingOnGround() && !IsSliding())) && Now - LastClimbExitTime > ClimbReattachCooldown)
		{
			if (AActor* ClimbableActor = FindClimbableActor())
			{
				bool bFromTop = false;
				if (ShouldGrabClimbable(*Cast<IHitmenClimbable>(ClimbableActor), bFromTop))
				{
					StartClimb(ClimbableActor, bFromTop);
				}
			}
		}
	}
	bPrevPressedJump = bJumpHeld;

	UpdateJetpack(DeltaSeconds);
}

void UHitmenCharacterMovementComponent::UpdateFromCompressedFlags(uint8 Flags)
{
	Super::UpdateFromCompressedFlags(Flags);
	bWantsToSprint = (Flags & FSavedMove_Character::FLAG_Custom_0) != 0;
	bWantsJetpackThrust = (Flags & FSavedMove_Character::FLAG_Custom_1) != 0;
}

// ---------------------------------------------------------------------------------------------
// Jetpack
// ---------------------------------------------------------------------------------------------

void UHitmenCharacterMovementComponent::ConfigureJetpack(bool bEnabled, float InMaxFuel, float InFuelRechargePerSecond, float InLiftSpeed, float InLiftInterpSpeed)
{
	bJetpackEnabled = bEnabled;
	JetpackMaxFuel = FMath::Max(0.f, InMaxFuel);
	JetpackFuelRechargePerSecond = FMath::Max(0.f, InFuelRechargePerSecond);
	JetpackLiftSpeed = InLiftSpeed;
	JetpackLiftInterpSpeed = FMath::Max(0.f, InLiftInterpSpeed);
	JetpackFuel = bEnabled ? JetpackMaxFuel : 0.f;
	if (!bEnabled)
	{
		bWantsJetpackThrust = false;
	}
}

bool UHitmenCharacterMovementComponent::IsJetpackThrusting() const
{
	if (!bJetpackEnabled || !bWantsJetpackThrust || JetpackFuel <= 0.f || IsClimbing() || IsMantling())
	{
		return false;
	}
	const AHitmenCharacterBase* Owner = Cast<AHitmenCharacterBase>(CharacterOwner);
	return !Owner || (!Owner->IsDead() && Owner->GetMovementSpeedMultiplier() > 0.f);
}

void UHitmenCharacterMovementComponent::UpdateJetpack(float DeltaSeconds)
{
	if (!bJetpackEnabled || DeltaSeconds <= 0.f)
	{
		return;
	}
	if (IsJetpackThrusting())
	{
		JetpackFuel = FMath::Max(0.f, JetpackFuel - DeltaSeconds);
		if (!IsFalling())
		{
			SetMovementMode(MOVE_Falling);
		}
		Velocity.Z = FMath::FInterpTo(Velocity.Z, JetpackLiftSpeed, DeltaSeconds, JetpackLiftInterpSpeed);
	}
	else if (IsMovingOnGround())
	{
		JetpackFuel = FMath::Min(JetpackMaxFuel, JetpackFuel + JetpackFuelRechargePerSecond * DeltaSeconds);
	}
}

void UHitmenCharacterMovementComponent::ReconcileJetpackFuel(float ServerFuel, float Tolerance)
{
	if (bJetpackEnabled && !bWantsJetpackThrust && FMath::Abs(JetpackFuel - ServerFuel) > Tolerance)
	{
		JetpackFuel = FMath::Clamp(ServerFuel, 0.f, JetpackMaxFuel);
	}
}

void UHitmenCharacterMovementComponent::OnMovementModeChanged(EMovementMode PreviousMovementMode, uint8 PreviousCustomMode)
{
	Super::OnMovementModeChanged(PreviousMovementMode, PreviousCustomMode);

	const EHitmenCustomMovement Previous = PreviousMovementMode == MOVE_Custom ? static_cast<EHitmenCustomMovement>(PreviousCustomMode) : EHitmenCustomMovement::None;
	const EHitmenCustomMovement Current = GetCustomMovement();
	if (Previous == EHitmenCustomMovement::Climb && Current != EHitmenCustomMovement::Climb)
	{
		ClimbTarget.Reset();
		ClimbAxisInput = 0.f;
	}
	if (Previous != Current)
	{
		OnCustomMovementChanged.Broadcast(Current);
	}
}

void UHitmenCharacterMovementComponent::PhysCustom(float DeltaTime, int32 Iterations)
{
	switch (static_cast<EHitmenCustomMovement>(CustomMovementMode))
	{
	case EHitmenCustomMovement::Slide:  PhysSlide(DeltaTime, Iterations); break;
	case EHitmenCustomMovement::Climb:  PhysClimb(DeltaTime, Iterations); break;
	case EHitmenCustomMovement::Mantle: PhysMantle(DeltaTime, Iterations); break;
	default: Super::PhysCustom(DeltaTime, Iterations); break;
	}
}

// ---------------------------------------------------------------------------------------------
// Slide
// ---------------------------------------------------------------------------------------------

bool UHitmenCharacterMovementComponent::CanStartSlide() const
{
	return Super::IsMovingOnGround()
		&& Velocity.Size2D() >= SlideMinStartSpeed
		&& GetTimeSeconds() - LastSlideEndTime > SlideCooldown;
}

void UHitmenCharacterMovementComponent::StartSlide()
{
	const float Impulse = IsSprinting() ? SprintSlideImpulse : SlideImpulse;
	Velocity += Velocity.GetSafeNormal2D() * Impulse;
	SlideStartTime = GetTimeSeconds();
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Slide));
}

void UHitmenCharacterMovementComponent::EndSlide()
{
	LastSlideEndTime = GetTimeSeconds();
	SetMovementMode(MOVE_Walking);
}

bool UHitmenCharacterMovementComponent::GetSlideSurface(FHitResult& OutHit) const
{
	FFindFloorResult Floor;
	FindFloor(UpdatedComponent->GetComponentLocation(), Floor, false);
	OutHit = Floor.HitResult;
	return Floor.IsWalkableFloor();
}

void UHitmenCharacterMovementComponent::PhysSlide(float DeltaTime, int32 Iterations)
{
	if (DeltaTime < MIN_TICK_TIME)
	{
		return;
	}
	if (!bWantsToCrouch || GetTimeSeconds() - SlideStartTime > SlideMaxDuration)
	{
		EndSlide();
		StartNewPhysics(DeltaTime, Iterations);
		return;
	}

	FFindFloorResult Floor;
	FindFloor(UpdatedComponent->GetComponentLocation(), Floor, false);
	if (!Floor.IsWalkableFloor())
	{
		LastSlideEndTime = GetTimeSeconds();
		SetMovementMode(MOVE_Falling);
		StartNewPhysics(DeltaTime, Iterations);
		return;
	}

	// Gravity pulls along the slope, so slides accelerate downhill.
	const FVector FloorNormal = Floor.HitResult.ImpactNormal;
	Velocity += FVector::VectorPlaneProject(FVector(0.f, 0.f, GetGravityZ()), FloorNormal) * DeltaTime;

	// Only lateral steering while sliding.
	const FVector Right = UpdatedComponent->GetRightVector();
	Acceleration = Acceleration.ProjectOnTo(Right) * 0.5f;

	CalcVelocity(DeltaTime, SlideFriction, false, SlideBrakingDeceleration);
	Velocity = FVector::VectorPlaneProject(Velocity, FloorNormal);

	Iterations++;
	bJustTeleported = false;
	const FVector OldLocation = UpdatedComponent->GetComponentLocation();
	const FVector Delta = Velocity * DeltaTime;
	FHitResult Hit(1.f);
	SafeMoveUpdatedComponent(Delta, UpdatedComponent->GetComponentQuat(), true, Hit);
	if (Hit.Time < 1.f)
	{
		HandleImpact(Hit, DeltaTime, Delta);
		SlideAlongSurface(Delta, 1.f - Hit.Time, Hit.Normal, Hit, true);
	}

	FindFloor(UpdatedComponent->GetComponentLocation(), CurrentFloor, false);
	if (CurrentFloor.IsWalkableFloor())
	{
		AdjustFloorHeight();
	}

	if (!bJustTeleported)
	{
		Velocity = (UpdatedComponent->GetComponentLocation() - OldLocation) / DeltaTime;
	}
	if (Velocity.Size2D() < SlideExitSpeed)
	{
		EndSlide();
	}
}

// ---------------------------------------------------------------------------------------------
// Ledge grab / vault
// ---------------------------------------------------------------------------------------------

bool UHitmenCharacterMovementComponent::TryStartMantle()
{
	if (!CharacterOwner || !UpdatedComponent)
	{
		return false;
	}

	// Needs forward intent: jumping in place next to a wall is a normal jump.
	FVector Forward = CharacterOwner->GetActorForwardVector().GetSafeNormal2D();
	const FVector Input = Acceleration.GetSafeNormal2D();
	if (Input.IsNearlyZero() || FVector::DotProduct(Input, Forward) < 0.3f)
	{
		return false;
	}
	Forward = Input;

	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();
	const float Radius = Capsule->GetScaledCapsuleRadius();
	const float HalfHeight = Capsule->GetScaledCapsuleHalfHeight();
	const FVector Location = UpdatedComponent->GetComponentLocation();
	const FVector Feet = Location - FVector::UpVector * HalfHeight;
	const UWorld* World = GetWorld();

	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenMantle), false, CharacterOwner);
	const ECollisionChannel Channel = UpdatedComponent->GetCollisionObjectType();

	// 1. Find a wall in front, from knee to max ledge height.
	FHitResult WallHit;
	bool bFoundWall = false;
	const float ProbeDistance = Radius + MantleReach;
	for (float Height = 30.f; Height <= MantleMaxHeight; Height += 30.f)
	{
		const FVector Start = Feet + FVector::UpVector * Height;
		if (World->LineTraceSingleByChannel(WallHit, Start, Start + Forward * ProbeDistance, ECC_Visibility, Params)
			&& FMath::Abs(WallHit.ImpactNormal.Z) < 0.3f)
		{
			bFoundWall = true;
			break;
		}
	}
	if (!bFoundWall)
	{
		return false;
	}
	const FVector Into = -WallHit.ImpactNormal.GetSafeNormal2D();

	// 2. Find the top surface at several depths past the face (thin walls, window sills).
	FHitResult TopHit;
	bool bFoundTop = false;
	for (const float Depth : MantleProbeDepths)
	{
		FVector ProbeXY = WallHit.ImpactPoint + Into * Depth;
		const FVector Start(ProbeXY.X, ProbeXY.Y, Feet.Z + MantleMaxHeight + 25.f);
		const FVector End(ProbeXY.X, ProbeXY.Y, Feet.Z + MantleMinHeight * 0.5f);
		if (World->SweepSingleByChannel(TopHit, Start, End, FQuat::Identity, ECC_Visibility, FCollisionShape::MakeSphere(4.f), Params)
			&& !TopHit.bStartPenetrating && IsWalkable(TopHit))
		{
			bFoundTop = true;
			break;
		}
	}
	if (!bFoundTop)
	{
		return false;
	}

	const float LedgeHeight = TopHit.ImpactPoint.Z - Feet.Z;
	if (LedgeHeight < MantleMinHeight || LedgeHeight > MantleMaxHeight)
	{
		return false;
	}

	const FCollisionShape StandShape = FCollisionShape::MakeCapsule(Radius * 0.95f, HalfHeight * 0.95f);
	const FVector StandDest = TopHit.ImpactPoint + Into * (Radius + 5.f) + FVector::UpVector * (HalfHeight + 3.f);
	const bool bCanStand = !World->OverlapBlockingTestByChannel(StandDest, FQuat::Identity, Channel, StandShape, Params);

	// 3. Vault over thin, low obstacles when there is floor on the other side.
	bool bVault = false;
	FVector VaultDest = FVector::ZeroVector;
	if (LedgeHeight <= VaultMaxHeight)
	{
		const FVector OverTop = TopHit.ImpactPoint + FVector::UpVector * 15.f;
		const FVector FarTop = FVector(WallHit.ImpactPoint.X, WallHit.ImpactPoint.Y, OverTop.Z) + Into * (VaultMaxDepth + Radius + 10.f);
		FHitResult Blocked;
		if (!World->LineTraceSingleByChannel(Blocked, OverTop, FarTop, ECC_Visibility, Params))
		{
			FHitResult FloorHit;
			if (World->LineTraceSingleByChannel(FloorHit, FarTop, FarTop - FVector::UpVector * (LedgeHeight + 200.f), ECC_Visibility, Params)
				&& IsWalkable(FloorHit) && FloorHit.ImpactPoint.Z < TopHit.ImpactPoint.Z - 25.f)
			{
				VaultDest = FloorHit.ImpactPoint + FVector::UpVector * (HalfHeight + 3.f);
				bVault = !World->OverlapBlockingTestByChannel(VaultDest, FQuat::Identity, Channel, StandShape, Params);
			}
		}
	}

	if (!bVault && !bCanStand)
	{
		return false;
	}

	bMantleIsVault = bVault;
	MantleStart = Location;
	MantleTarget = bVault ? VaultDest : StandDest;
	MantlePeak = bVault
		? TopHit.ImpactPoint + FVector::UpVector * (HalfHeight + 25.f)
		: FVector(Location.X, Location.Y, StandDest.Z + 10.f);
	MantleElapsed = 0.f;
	const float HeightAlpha = FMath::GetMappedRangeValueClamped(FVector2D(MantleMinHeight, MantleMaxHeight), FVector2D(0.75f, 1.2f), LedgeHeight);
	MantleTotal = bVault ? VaultDuration : MantleDuration * HeightAlpha;
	LastMantleTime = GetTimeSeconds();
	Velocity = FVector::ZeroVector;
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Mantle));
	OnMantleStarted.Broadcast(bVault);
	return true;
}

void UHitmenCharacterMovementComponent::PhysMantle(float DeltaTime, int32 Iterations)
{
	if (DeltaTime < MIN_TICK_TIME)
	{
		return;
	}
	MantleElapsed += DeltaTime;
	const float Alpha = FMath::Clamp(MantleElapsed / FMath::Max(MantleTotal, KINDA_SMALL_NUMBER), 0.f, 1.f);
	const float Inv = 1.f - Alpha;
	const FVector Point = Inv * Inv * MantleStart + 2.f * Inv * Alpha * MantlePeak + Alpha * Alpha * MantleTarget;
	const FVector OldLocation = UpdatedComponent->GetComponentLocation();
	UpdatedComponent->SetWorldLocation(Point, false, nullptr, ETeleportType::None);
	Velocity = (Point - OldLocation) / DeltaTime;
	if (Alpha >= 1.f)
	{
		FinishMantle();
	}
}

void UHitmenCharacterMovementComponent::FinishMantle()
{
	const FVector Forward = CharacterOwner ? CharacterOwner->GetActorForwardVector().GetSafeNormal2D() : FVector::ForwardVector;
	Velocity = Forward * (bMantleIsVault ? WalkSpeed : WalkSpeed * 0.3f);
	LastMantleTime = GetTimeSeconds();
	SetMovementMode(MOVE_Falling);
}

// ---------------------------------------------------------------------------------------------
// Ladders and ziplines
// ---------------------------------------------------------------------------------------------

AActor* UHitmenCharacterMovementComponent::FindClimbableActor() const
{
	if (!CharacterOwner)
	{
		return nullptr;
	}
	TArray<AActor*> Overlapping;
	CharacterOwner->GetOverlappingActors(Overlapping);
	for (AActor* Actor : Overlapping)
	{
		if (Cast<IHitmenClimbable>(Actor))
		{
			return Actor;
		}
	}
	return nullptr;
}

float UHitmenCharacterMovementComponent::ProjectOnClimbAxis(const FVector& Location, const IHitmenClimbable& Climbable) const
{
	const FVector Start = Climbable.GetClimbStart();
	const FVector Axis = Climbable.GetClimbEnd() - Start;
	const float LengthSq = FMath::Max(Axis.SizeSquared(), 1.f);
	const float HalfHeight = CharacterOwner->GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
	const FVector Reference = Climbable.GetClimbType() == EHitmenClimbType::Ladder
		? Location - FVector::UpVector * HalfHeight
		: Location + FVector::UpVector * ZiplineHangOffset;
	return FMath::Clamp(FVector::DotProduct(Reference - Start, Axis) / LengthSq, 0.f, 1.f);
}

FVector UHitmenCharacterMovementComponent::GetClimbLocation(float Alpha, const IHitmenClimbable& Climbable) const
{
	const FVector Start = Climbable.GetClimbStart();
	const FVector Point = Start + (Climbable.GetClimbEnd() - Start) * Alpha;
	if (Climbable.GetClimbType() == EHitmenClimbType::Ladder)
	{
		const float HalfHeight = CharacterOwner->GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
		return Point + Climbable.GetClimbNormal().GetSafeNormal2D() * Climbable.GetClimbOffset() + FVector::UpVector * HalfHeight;
	}
	return Point - FVector::UpVector * ZiplineHangOffset;
}

bool UHitmenCharacterMovementComponent::ShouldGrabClimbable(const IHitmenClimbable& Climbable, bool& bOutFromTop) const
{
	bOutFromTop = false;
	const FVector Input = Acceleration.GetSafeNormal2D();
	const FVector View = CharacterOwner->GetActorForwardVector().GetSafeNormal2D();

	if (Climbable.GetClimbType() == EHitmenClimbType::Zipline)
	{
		// Jump or walk into the cable while pushing forward.
		return !Input.IsNearlyZero() || IsFalling();
	}

	const FVector Normal = Climbable.GetClimbNormal().GetSafeNormal2D();
	const float Facing = FVector::DotProduct(View, -Normal);
	const float Alpha = ProjectOnClimbAxis(UpdatedComponent->GetComponentLocation(), Climbable);

	// From the top: walking off the edge over the ladder.
	if (Alpha > 0.9f && !IsFalling() && FVector::DotProduct(Input, Normal) > 0.3f)
	{
		bOutFromTop = true;
		return true;
	}
	if (Facing < Climbable.GetMinFacingDot())
	{
		return false;
	}
	// Jumping onto it, or walking into it.
	return IsFalling() || FVector::DotProduct(Input, -Normal) > 0.1f;
}

void UHitmenCharacterMovementComponent::StartClimb(AActor* ClimbableActor, bool bFromTop)
{
	const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(ClimbableActor);
	if (!Climbable)
	{
		return;
	}
	if (IsSliding())
	{
		LastSlideEndTime = GetTimeSeconds();
	}
	ClimbTarget = ClimbableActor;
	ClimbAlpha = bFromTop ? 0.97f : ProjectOnClimbAxis(UpdatedComponent->GetComponentLocation(), *Climbable);
	Velocity = FVector::ZeroVector;
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Climb));
}

void UHitmenCharacterMovementComponent::StopClimbing(bool bJumpOff)
{
	if (!IsClimbing())
	{
		return;
	}
	const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(ClimbTarget.Get());
	LastClimbExitTime = GetTimeSeconds();
	if (bJumpOff && Climbable)
	{
		if (Climbable->GetClimbType() == EHitmenClimbType::Ladder)
		{
			Velocity = Climbable->GetClimbNormal().GetSafeNormal2D() * ClimbJumpOffSpeed + FVector::UpVector * ClimbJumpOffSpeed * 0.8f;
		}
		else
		{
			Velocity += FVector::UpVector * JumpZVelocity * 0.6f;
		}
	}
	SetMovementMode(MOVE_Falling);
}

void UHitmenCharacterMovementComponent::PhysClimb(float DeltaTime, int32 Iterations)
{
	if (DeltaTime < MIN_TICK_TIME)
	{
		return;
	}
	const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(ClimbTarget.Get());
	if (!Climbable)
	{
		StopClimbing(false);
		StartNewPhysics(DeltaTime, Iterations);
		return;
	}

	const FVector Start = Climbable->GetClimbStart();
	const FVector Axis = Climbable->GetClimbEnd() - Start;
	const float Length = FMath::Max(Axis.Size(), 1.f);
	const FVector Input = Acceleration.GetSafeNormal2D();
	const FVector ActorForward = CharacterOwner->GetActorForwardVector().GetSafeNormal2D();

	float Forward = FVector::DotProduct(Input, ActorForward);
	if (Climbable->GetClimbType() == EHitmenClimbType::Ladder)
	{
		if (bClimbRelativeToCamera)
		{
			const float Pitch = CharacterOwner->GetControlRotation().GetNormalized().Pitch;
			Forward *= Pitch < -15.f ? -1.f : 1.f;
		}
	}
	else
	{
		// Zipline: forward moves toward where you look along the cable.
		Forward *= FVector::DotProduct(ActorForward, Axis.GetSafeNormal2D()) >= 0.f ? 1.f : -1.f;
	}
	ClimbAxisInput = FMath::Clamp(Forward, -1.f, 1.f);

	const float NewAlpha = ClimbAlpha + ClimbAxisInput * Climbable->GetClimbSpeed() * DeltaTime / Length;
	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();

	if (Climbable->GetClimbType() == EHitmenClimbType::Ladder)
	{
		if (NewAlpha >= 1.f && ClimbAxisInput > 0.f)
		{
			// Step off the top onto the ledge behind the ladder.
			const FVector Top = Climbable->GetClimbEnd() - Climbable->GetClimbNormal().GetSafeNormal2D() * (Capsule->GetScaledCapsuleRadius() + 30.f)
				+ FVector::UpVector * (Capsule->GetScaledCapsuleHalfHeight() + 5.f);
			UpdatedComponent->SetWorldLocation(Top, false, nullptr, ETeleportType::TeleportPhysics);
			LastClimbExitTime = GetTimeSeconds();
			Velocity = FVector::ZeroVector;
			SetMovementMode(MOVE_Falling);
			return;
		}
		if (NewAlpha <= 0.f && ClimbAxisInput < 0.f)
		{
			StopClimbing(false);
			StartNewPhysics(DeltaTime, Iterations);
			return;
		}
	}
	else if ((NewAlpha >= 1.f && ClimbAxisInput > 0.f) || (NewAlpha <= 0.f && ClimbAxisInput < 0.f))
	{
		StopClimbing(false);
		StartNewPhysics(DeltaTime, Iterations);
		return;
	}

	ClimbAlpha = FMath::Clamp(NewAlpha, 0.f, 1.f);
	const FVector Target = GetClimbLocation(ClimbAlpha, *Climbable);
	const FVector OldLocation = UpdatedComponent->GetComponentLocation();
	// Smoothly pull onto the axis: no snap animation, just converge.
	const FVector Delta = FMath::VInterpTo(OldLocation, Target, DeltaTime, 15.f) - OldLocation;
	FHitResult Hit(1.f);
	SafeMoveUpdatedComponent(Delta, UpdatedComponent->GetComponentQuat(), true, Hit);
	if (Hit.Time < 1.f)
	{
		SlideAlongSurface(Delta, 1.f - Hit.Time, Hit.Normal, Hit, true);
	}
	Velocity = (UpdatedComponent->GetComponentLocation() - OldLocation) / DeltaTime;
}

// ---------------------------------------------------------------------------------------------
// Prediction
// ---------------------------------------------------------------------------------------------

FNetworkPredictionData_Client* UHitmenCharacterMovementComponent::GetPredictionData_Client() const
{
	if (!ClientPredictionData)
	{
		UHitmenCharacterMovementComponent* MutableThis = const_cast<UHitmenCharacterMovementComponent*>(this);
		MutableThis->ClientPredictionData = new FNetworkPredictionData_Client_Hitmen(*this);
	}
	return ClientPredictionData;
}

void FSavedMove_Hitmen::Clear()
{
	Super::Clear();
	bSavedWantsToSprint = false;
	bSavedWantsJetpackThrust = false;
	SavedJetpackFuel = 0.f;
}

uint8 FSavedMove_Hitmen::GetCompressedFlags() const
{
	uint8 Result = Super::GetCompressedFlags();
	if (bSavedWantsToSprint)
	{
		Result |= FLAG_Custom_0;
	}
	if (bSavedWantsJetpackThrust)
	{
		Result |= FLAG_Custom_1;
	}
	return Result;
}

bool FSavedMove_Hitmen::CanCombineWith(const FSavedMovePtr& NewMove, ACharacter* InCharacter, float MaxDelta) const
{
	const FSavedMove_Hitmen* Other = static_cast<FSavedMove_Hitmen*>(NewMove.Get());
	if (bSavedWantsToSprint != Other->bSavedWantsToSprint || bSavedWantsJetpackThrust != Other->bSavedWantsJetpackThrust)
	{
		return false;
	}
	return Super::CanCombineWith(NewMove, InCharacter, MaxDelta);
}

void FSavedMove_Hitmen::CombineWith(const FSavedMove_Character* OldMove, ACharacter* InCharacter, APlayerController* PC, const FVector& OldStartLocation)
{
	Super::CombineWith(OldMove, InCharacter, PC, OldStartLocation);
	// The combined move replays from the old move's start: rewind the fuel too so it is not spent twice.
	const FSavedMove_Hitmen* Old = static_cast<const FSavedMove_Hitmen*>(OldMove);
	SavedJetpackFuel = Old->SavedJetpackFuel;
	if (UHitmenCharacterMovementComponent* Movement = InCharacter ? Cast<UHitmenCharacterMovementComponent>(InCharacter->GetCharacterMovement()) : nullptr)
	{
		Movement->JetpackFuel = Old->SavedJetpackFuel;
	}
}

void FSavedMove_Hitmen::SetMoveFor(ACharacter* C, float InDeltaTime, FVector const& NewAccel, FNetworkPredictionData_Client_Character& ClientData)
{
	Super::SetMoveFor(C, InDeltaTime, NewAccel, ClientData);
	if (const UHitmenCharacterMovementComponent* Movement = Cast<UHitmenCharacterMovementComponent>(C->GetCharacterMovement()))
	{
		bSavedWantsToSprint = Movement->bWantsToSprint;
		bSavedWantsJetpackThrust = Movement->bWantsJetpackThrust;
		SavedJetpackFuel = Movement->JetpackFuel;
	}
}

void FSavedMove_Hitmen::PrepMoveFor(ACharacter* C)
{
	Super::PrepMoveFor(C);
	if (UHitmenCharacterMovementComponent* Movement = Cast<UHitmenCharacterMovementComponent>(C->GetCharacterMovement()))
	{
		Movement->bWantsToSprint = bSavedWantsToSprint;
		Movement->bWantsJetpackThrust = bSavedWantsJetpackThrust;
		// Replays start from the fuel this move started with.
		Movement->JetpackFuel = SavedJetpackFuel;
	}
}

FSavedMovePtr FNetworkPredictionData_Client_Hitmen::AllocateNewMove()
{
	return FSavedMovePtr(new FSavedMove_Hitmen());
}
