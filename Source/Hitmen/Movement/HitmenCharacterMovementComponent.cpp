// Hitmen — predicted movement

#include "Movement/HitmenCharacterMovementComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Core/HitmenInterfaces.h"
#include "Hitmen.h"
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
	MaxWalkSpeedCrouched = 240.f;
	NavAgentProps.bCanCrouch = true;
	bCanWalkOffLedgesWhenCrouching = true;
	JumpZVelocity = 450.f;
	AirControl = 0.35f;
	BrakingDecelerationFalling = 1200.f;
	// Letting go of the keys stops you crisply (no ice-skating after a sprint).
	BrakingDecelerationWalking = 2600.f;
	GravityScale = 1.4f;
	// Steps and ledges up to 55 cm are walked up without pressing anything.
	MaxStepHeight = 55.f;
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
		case EHitmenCustomMovement::Slide: return FMath::Max(SlideEntrySpeed, SprintSpeed);
		case EHitmenCustomMovement::Hang: return HangShimmyFastSpeed;
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
	if (IsClimbing() || IsMantling() || IsHanging())
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
	// Slide starts on the crouch press while moving (before Super crouches us). After a slide, Ctrl must be
	// released before the next one: holding it never chains slides.
	if (!bWantsToCrouch)
	{
		bSlideNeedsCrouchRelease = false;
	}
	if (bWantsToCrouch && !IsCrouching() && !bSlideNeedsCrouchRelease && !IsHanging() && CanStartSlide())
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
	if (bJumpPressedNow)
	{
		LastJumpPressTime = Now;
	}
	// Space keeps counting for a short moment (a jump started just before the ledge / ladder).
	const bool bJumpBuffered = bJumpHeld || Now - LastJumpPressTime <= JumpInputBuffer;

	if (IsHanging())
	{
		// Space climbs (into the window above, or onto the ledge); Ctrl lets go.
		if (bWantsToCrouch)
		{
			StopHang(true);
		}
		else if (bJumpPressedNow)
		{
			FHitmenMantlePlan Plan;
			if (PlanHangClimb(Plan))
			{
				StartMantlePlan(Plan);
			}
			LastJumpPressTime = -100.f;
		}
	}
	else if (IsClimbing())
	{
		// A press right before or after the grab is part of getting on, not a jump off.
		const bool bInGrace = Now - ClimbStartTime < LadderJumpGraceTime;
		if (bJumpPressedNow && !bInGrace)
		{
			StopClimbing(true);
			LastJumpPressTime = -100.f;
		}
	}
	else if (!IsMantling())
	{
		// Ledge grab / vault: space in front of a wall, on the ground or in the air.
		const bool bCanTryMantle = IsMovingOnGround() || (bAllowAirMantle && IsFalling());
		if (bJumpBuffered && bCanTryMantle && Now - LastMantleTime > MantleCooldown)
		{
			if (TryStartMantle())
			{
				LastJumpPressTime = -100.f;
			}
			// No ledge to climb in the air: a thin ledge at hand height (under a window) is hung from.
			else if (IsFalling() && Now - LastHangReleaseTime > HangRegrabCooldown && TryStartHang())
			{
				LastJumpPressTime = -100.f;
			}
		}

		// Ladders / ziplines are automatic: walk or jump into them. Only the ladder just left waits.
		if (!IsMantling() && (IsFalling() || (Super::IsMovingOnGround() && !IsSliding())))
		{
			if (AActor* ClimbableActor = FindClimbableActor())
			{
				const bool bCoolingDown = ClimbableActor == LastClimbExitActor.Get() && Now - LastClimbExitTime <= ClimbReattachCooldown;
				const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(ClimbableActor);
				bool bFromTop = false;
				if (!bCoolingDown && Climbable && ShouldGrabClimbable(*Climbable, bFromTop))
				{
					StartClimb(ClimbableActor, bFromTop);
				}
				else if (!bCoolingDown && Climbable && IsFalling() && bJumpBuffered && Climbable->GetClimbType() != EHitmenClimbType::Zipline)
				{
					// Jumped at it: grab at once, whatever the exact facing.
					StartClimb(ClimbableActor, false);
					LastJumpPressTime = -100.f;
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
	case EHitmenCustomMovement::Hang:   PhysHang(DeltaTime, Iterations); break;
	default: Super::PhysCustom(DeltaTime, Iterations); break;
	}
}

// ---------------------------------------------------------------------------------------------
// Slide
// ---------------------------------------------------------------------------------------------

bool UHitmenCharacterMovementComponent::CanStartSlide() const
{
	return Super::IsMovingOnGround()
		&& (!bSlideRequiresSprint || bWantsToSprint)
		&& Velocity.Size2D() >= SlideMinStartSpeed
		&& GetTimeSeconds() - LastSlideEndTime > SlideCooldown;
}

void UHitmenCharacterMovementComponent::StartSlide()
{
	const float Impulse = IsSprinting() ? SprintSlideImpulse : SlideImpulse;
	Velocity += Velocity.GetSafeNormal2D() * Impulse;
	SlideStartTime = GetTimeSeconds();
	SlideElapsed = 0.f;
	SlideEntrySpeed = Velocity.Size2D();
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Slide));
}

void UHitmenCharacterMovementComponent::EndSlide()
{
	LastSlideEndTime = GetTimeSeconds();
	bSlideNeedsCrouchRelease = bWantsToCrouch;
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
	SlideElapsed += DeltaTime;
	if (!bWantsToCrouch || SlideElapsed > SlideMaxDuration)
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

	// Gravity pulls along the slope, so slides speed up downhill (never past the entry speed).
	const FVector FloorNormal = Floor.HitResult.ImpactNormal;
	Velocity += FVector::VectorPlaneProject(FVector(0.f, 0.f, GetGravityZ()), FloorNormal) * DeltaTime;

	// Left / right only bend the slide (never push it), at SlideSteerRate.
	const FVector Flat = Velocity.GetSafeNormal2D();
	const FVector Input = Acceleration.GetSafeNormal2D();
	if (!Flat.IsNearlyZero() && !Input.IsNearlyZero())
	{
		const float Turn = FMath::Clamp(FVector::CrossProduct(Flat, Input).Z, -1.f, 1.f);
		Velocity = Velocity.RotateAngleAxis(Turn * SlideSteerRate * DeltaTime, FVector::UpVector);
	}
	Acceleration = FVector::ZeroVector;

	// Always brakes, whatever keys are held: friction (proportional) + a constant deceleration.
	const float Speed = Velocity.Size();
	const float Loss = (SlideFriction * Speed + SlideBrakingDeceleration) * DeltaTime;
	Velocity = Speed > Loss ? Velocity * ((Speed - Loss) / Speed) : FVector::ZeroVector;
	Velocity = FVector::VectorPlaneProject(Velocity, FloorNormal).GetClampedToMaxSize(FMath::Max(SlideEntrySpeed, SlideExitSpeed));

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
	// Needs forward intent: jumping in place next to a wall is a normal jump. In the air any intent toward
	// the wall counts (a jump at the corner of a ledge, sideways input).
	const FVector Facing = CharacterOwner->GetActorForwardVector().GetSafeNormal2D();
	const FVector Input = Acceleration.GetSafeNormal2D();
	const float MinDot = IsFalling() ? 0.f : 0.3f;
	if (Input.IsNearlyZero() || FVector::DotProduct(Input, Facing) < MinDot)
	{
		return false;
	}
	FHitmenMantlePlan Plan;
	if (!PlanMantle(Input, Plan))
	{
		return false;
	}
	StartMantlePlan(Plan);
	return true;
}

EHitmenTraversalAction UHitmenCharacterMovementComponent::QueryTraversalAction() const
{
	// The hint only shows on the ground: a jump must not reveal climbs out of reach.
	if (!CharacterOwner || !UpdatedComponent || IsMantling() || IsClimbing() || !IsMovingOnGround())
	{
		return EHitmenTraversalAction::None;
	}
	FHitmenMantlePlan Plan;
	return PlanMantle(CharacterOwner->GetActorForwardVector().GetSafeNormal2D(), Plan) ? Plan.Action : EHitmenTraversalAction::None;
}

float UHitmenCharacterMovementComponent::GetMantleAlpha() const
{
	return IsMantling() ? FMath::Clamp(MantleElapsed / FMath::Max(MantleTotal, KINDA_SMALL_NUMBER), 0.f, 1.f) : 0.f;
}

bool UHitmenCharacterMovementComponent::PlanMantle(const FVector& Forward, FHitmenMantlePlan& OutPlan) const
{
	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();
	const float Radius = Capsule->GetScaledCapsuleRadius();
	const float HalfHeight = Capsule->GetScaledCapsuleHalfHeight();
	const FVector Location = UpdatedComponent->GetComponentLocation();
	const FVector Feet = Location - FVector::UpVector * HalfHeight;
	const UWorld* World = GetWorld();

	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenMantle), false, CharacterOwner);
	const ECollisionChannel Channel = UpdatedComponent->GetCollisionObjectType();

	// 1. A wall in front, from knee height (in the air: from below the feet) to the highest grabbable
	//    ledge; centre first, then a little left and right so a ledge corner is caught too.
	const bool bInAir = IsFalling();
	const float MinLedgeHeight = bInAir ? AirMantleMinHeight : MantleMinHeight;
	FHitResult WallHit;
	bool bFoundWall = false;
	const float ProbeDistance = Radius + MantleReach;
	const FVector Side = FVector::CrossProduct(FVector::UpVector, Forward).GetSafeNormal();
	const float SideOffsets[] = { 0.f, -MantleSideProbeOffset, MantleSideProbeOffset };
	for (const float Offset : SideOffsets)
	{
		for (float Height = bInAir ? MinLedgeHeight - 30.f : 30.f; Height <= MantleMaxHeight && !bFoundWall; Height += 30.f)
		{
			const FVector Start = Feet + FVector::UpVector * Height + Side * Offset;
			if (World->LineTraceSingleByChannel(WallHit, Start, Start + Forward * ProbeDistance, ECC_Visibility, Params)
				&& FMath::Abs(WallHit.ImpactNormal.Z) < 0.3f)
			{
				bFoundWall = true;
			}
		}
		if (bFoundWall)
		{
			break;
		}
	}
	// Fixtures (barrels, hack panels, turrets, cameras, mounted guns...) are never climbed.
	if (!bFoundWall || (WallHit.GetActor() && WallHit.GetActor()->ActorHasTag(HitmenTags::NoTraversal)))
	{
		return false;
	}
	const FVector Into = -WallHit.ImpactNormal.GetSafeNormal2D();

	// 2. The top surface, at several depths past the face (thin walls, sills). The downward probe
	//    starts under any ceiling (window lintel) so openings in a wall are found too.
	FHitResult TopHit;
	bool bFoundTop = false;
	for (const float Depth : MantleProbeDepths)
	{
		const FVector ProbeXY = WallHit.ImpactPoint + Into * Depth;
		const float EndZ = bInAir ? Feet.Z + MinLedgeHeight - 20.f : Feet.Z + MantleMinHeight * 0.5f;
		float StartZ = Feet.Z + MantleMaxHeight + 25.f;
		// Start in open air: a probe column through a wall with an opening (window) starts inside the
		// lintel; step down until out of solid geometry, then sweep down onto the sill.
		const FCollisionShape ProbeShape = FCollisionShape::MakeSphere(5.f);
		while (StartZ > EndZ && World->OverlapBlockingTestByChannel(FVector(ProbeXY.X, ProbeXY.Y, StartZ), FQuat::Identity, ECC_Visibility, ProbeShape, Params))
		{
			StartZ -= 12.f;
		}
		const FVector Start(ProbeXY.X, ProbeXY.Y, StartZ);
		const FVector End(ProbeXY.X, ProbeXY.Y, EndZ);
		if (Start.Z > End.Z
			&& World->SweepSingleByChannel(TopHit, Start, End, FQuat::Identity, ECC_Visibility, FCollisionShape::MakeSphere(4.f), Params)
			&& !TopHit.bStartPenetrating && IsWalkable(TopHit))
		{
			bFoundTop = true;
			break;
		}
	}
	if (!bFoundTop || (TopHit.GetActor() && TopHit.GetActor()->ActorHasTag(HitmenTags::NoTraversal)))
	{
		return false;
	}

	const float LedgeHeight = TopHit.ImpactPoint.Z - Feet.Z;
	if (LedgeHeight < MinLedgeHeight || LedgeHeight > MantleMaxHeight)
	{
		return false;
	}
	const FCollisionShape StandShape = FCollisionShape::MakeCapsule(Radius * 0.95f, HalfHeight * 0.95f);
	const FVector OverTop = TopHit.ImpactPoint + FVector::UpVector * (HalfHeight + MantleClearance);

	// Is there floor right past the wall (thin obstacle) and room to land there?
	auto FindFarSide = [&](FVector& OutDest) -> bool
	{
		const FVector FarProbe = FVector(WallHit.ImpactPoint.X, WallHit.ImpactPoint.Y, TopHit.ImpactPoint.Z + 15.f) + Into * (VaultMaxDepth + Radius + 10.f);
		FHitResult Blocked;
		// Pawn channel too: closed shutters block bodies without blocking sight.
		const FCollisionShape PassShape = FCollisionShape::MakeSphere(10.f);
		if (World->SweepSingleByChannel(Blocked, TopHit.ImpactPoint + FVector::UpVector * 15.f, FarProbe, FQuat::Identity, ECC_Visibility, PassShape, Params)
			|| World->SweepTestByChannel(TopHit.ImpactPoint + FVector::UpVector * 15.f, FarProbe, FQuat::Identity, Channel, PassShape, Params))
		{
			return false;
		}
		FHitResult FloorHit;
		if (World->LineTraceSingleByChannel(FloorHit, FarProbe, FarProbe - FVector::UpVector * (LedgeHeight + 300.f), ECC_Visibility, Params)
			&& IsWalkable(FloorHit) && FloorHit.ImpactPoint.Z < TopHit.ImpactPoint.Z - 25.f)
		{
			OutDest = FloorHit.ImpactPoint + FVector::UpVector * (HalfHeight + 3.f);
			return !World->OverlapBlockingTestByChannel(OutDest, FQuat::Identity, Channel, StandShape, Params);
		}
		return false;
	};

	// How deep is the top? A block (deep enough) is climbed onto; a thin wall is climbed over.
	float TopDepth = 0.f;
	for (float Depth = 6.f; Depth <= 2.f * (Radius + 8.f) + 6.f; Depth += 6.f)
	{
		const FVector Probe = FVector(WallHit.ImpactPoint.X, WallHit.ImpactPoint.Y, TopHit.ImpactPoint.Z) + Into * Depth;
		FHitResult TopProbe;
		if (!World->LineTraceSingleByChannel(TopProbe, Probe + FVector::UpVector * 20.f, Probe - FVector::UpVector * 20.f, ECC_Visibility, Params)
			|| FMath::Abs(TopProbe.ImpactPoint.Z - TopHit.ImpactPoint.Z) > 15.f)
		{
			break;
		}
		TopDepth = Depth;
	}
	const bool bDeepTop = TopDepth >= 2.f * (Radius + 8.f);
	const float StandDepth = bDeepTop ? Radius + 8.f : TopDepth * 0.5f;

	// Room to stand on top (a sill, a crate, a roof edge, the top of a block wall)?
	const FVector StandDest = FVector(WallHit.ImpactPoint.X, WallHit.ImpactPoint.Y, TopHit.ImpactPoint.Z) + Into * StandDepth + FVector::UpVector * (HalfHeight + 3.f);
	FHitResult StandFloor;
	const bool bTopUnderStand = World->LineTraceSingleByChannel(StandFloor, StandDest, StandDest - FVector::UpVector * (HalfHeight + 20.f), ECC_Visibility, Params)
		&& FMath::Abs(StandFloor.ImpactPoint.Z - TopHit.ImpactPoint.Z) < 30.f;
	const bool bCanStand = TopDepth >= MinStandDepth && bTopUnderStand && !World->OverlapBlockingTestByChannel(StandDest, FQuat::Identity, Channel, StandShape, Params);

	FVector FarDest;
	const bool bFarSide = FindFarSide(FarDest);
	const float HeightAlpha = FMath::GetMappedRangeValueClamped(FVector2D(MantleMinHeight, MantleMaxHeight), FVector2D(0.8f, 1.25f), LedgeHeight);

	OutPlan = FHitmenMantlePlan();
	OutPlan.Start = Location;
	OutPlan.LedgeHeight = LedgeHeight;
	if (LedgeHeight <= VaultMaxHeight && bFarSide)
	{
		// Vault: one fluid arc over the top.
		OutPlan.Action = EHitmenTraversalAction::Vault;
		OutPlan.ControlA = FVector(Location.X, Location.Y, OverTop.Z) + Into * 15.f;
		OutPlan.ControlB = OverTop + Into * 40.f;
		OutPlan.Target = FarDest;
		OutPlan.Duration = VaultDuration;
		return true;
	}
	if (bCanStand)
	{
		// Ledge grab: hands up the wall first, then over onto the top.
		OutPlan.Action = EHitmenTraversalAction::Climb;
		OutPlan.ControlA = FVector(Location.X, Location.Y, StandDest.Z + MantleClearance);
		OutPlan.ControlB = FVector(TopHit.ImpactPoint.X, TopHit.ImpactPoint.Y, StandDest.Z + MantleClearance * 0.5f);
		OutPlan.Target = StandDest;
		OutPlan.Duration = MantleDuration * HeightAlpha;
		return true;
	}
	if (bFarSide)
	{
		// Thin wall: up, over the top, down the other side.
		OutPlan.Action = EHitmenTraversalAction::ClimbOver;
		OutPlan.ControlA = FVector(Location.X, Location.Y, OverTop.Z + 10.f);
		OutPlan.ControlB = OverTop + Into * 45.f + FVector::UpVector * 10.f;
		OutPlan.Target = FarDest;
		OutPlan.Duration = ClimbOverDuration * HeightAlpha;
		return true;
	}
	// Out of an upper-floor window (a sill over a drop): over the sill and hang from the ledge under it.
	if (!bInAir && LedgeHeight <= VaultMaxHeight && TopDepth < MinStandDepth)
	{
		const FVector OuterProbe = FVector(WallHit.ImpactPoint.X, WallHit.ImpactPoint.Y, TopHit.ImpactPoint.Z) + Into * (TopDepth + 6.f);
		// The way over the sill must be open (closed shutters block bodies, not sight).
		const FVector OverStart = TopHit.ImpactPoint + FVector::UpVector * 15.f;
		const FVector OverEnd = OuterProbe + Into * 40.f + FVector::UpVector * 15.f;
		const FCollisionShape OverShape = FCollisionShape::MakeSphere(10.f);
		const bool bOpen = !World->SweepTestByChannel(OverStart, OverEnd, FQuat::Identity, ECC_Visibility, OverShape, Params)
			&& !World->SweepTestByChannel(OverStart, OverEnd, FQuat::Identity, Channel, OverShape, Params);
		FVector Face, Normal;
		float LedgeZ = 0.f;
		float Edge = 0.f;
		// The outer wall face below the sill (seen from outside: its normal points away from us, along Into).
		if (bOpen && FindHangWall(OuterProbe + Into * 80.f, -Into, TopHit.ImpactPoint.Z - 10.f, Face, Normal)
			&& FindHangLedge(Face, Normal, TopHit.ImpactPoint.Z - 15.f, LedgeZ, Edge)
			&& LedgeZ < TopHit.ImpactPoint.Z - 4.f)
		{
			const FVector HangAt = GetHangLocation(Face, Normal, LedgeZ, Edge);
			if (!World->OverlapBlockingTestByChannel(HangAt, FQuat::Identity, Channel, StandShape, Params))
			{
				OutPlan.Action = EHitmenTraversalAction::Hang;
				OutPlan.ControlA = FVector(Location.X, Location.Y, OverTop.Z) + Into * 10.f;
				OutPlan.ControlB = OverTop + Into * (TopDepth + 30.f);
				OutPlan.Target = HangAt;
				OutPlan.Duration = HangClimbDuration;
				OutPlan.bEndsInHang = true;
				OutPlan.HangFace = Face;
				OutPlan.HangNormal = Normal;
				OutPlan.HangLedgeZ = LedgeZ;
				OutPlan.HangEdgeDistance = Edge;
				return true;
			}
		}
	}
	return false;
}

void UHitmenCharacterMovementComponent::StartMantlePlan(const FHitmenMantlePlan& Plan)
{
	MantleAction = Plan.Action;
	bMantleIsVault = Plan.Action == EHitmenTraversalAction::Vault;
	bMantleFromClimb = false;
	bMantleEndsInHang = Plan.bEndsInHang;
	MantleHangFace = Plan.HangFace;
	MantleHangNormal = Plan.HangNormal;
	MantleHangLedgeZ = Plan.HangLedgeZ;
	MantleHangEdge = Plan.HangEdgeDistance;
	MantleStart = Plan.Start;
	MantleControlA = Plan.ControlA;
	MantleControlB = Plan.ControlB;
	MantleTarget = Plan.Target;
	MantleElapsed = 0.f;
	MantleTotal = Plan.Duration;
	LastMantleTime = GetTimeSeconds();
	Velocity = FVector::ZeroVector;
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Mantle));
	OnMantleStarted.Broadcast(bMantleIsVault);
}

void UHitmenCharacterMovementComponent::StartLadderExit(const IHitmenClimbable& Climbable)
{
	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();
	const float HalfHeight = Capsule->GetScaledCapsuleHalfHeight();
	const FVector Normal = Climbable.GetClimbNormal().GetSafeNormal2D();
	const FVector Location = UpdatedComponent->GetComponentLocation();
	const FVector Top = Climbable.GetClimbEnd() - Normal * (Capsule->GetScaledCapsuleRadius() + 30.f) + FVector::UpVector * (HalfHeight + 5.f);

	// Keep climbing a little past the top, then roll over onto the ledge.
	MantleAction = EHitmenTraversalAction::Climb;
	bMantleIsVault = false;
	bMantleFromClimb = true;
	MantleStart = Location;
	MantleControlA = FVector(Location.X, Location.Y, Top.Z + MantleClearance + 20.f);
	MantleControlB = Top + Normal * 10.f + FVector::UpVector * (MantleClearance + 10.f);
	MantleTarget = Top;
	MantleElapsed = 0.f;
	MantleTotal = LadderExitDuration;
	LastMantleTime = GetTimeSeconds();
	LastClimbExitTime = GetTimeSeconds();
	LastClimbExitActor = ClimbTarget;
	Velocity = FVector::ZeroVector;
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Mantle));
	OnTraversalMontage.Broadcast(EHitmenAnimMontage::ClimbExitTop);
}

void UHitmenCharacterMovementComponent::PhysMantle(float DeltaTime, int32 Iterations)
{
	if (DeltaTime < MIN_TICK_TIME)
	{
		return;
	}
	MantleElapsed += DeltaTime;
	const float Raw = FMath::Clamp(MantleElapsed / FMath::Max(MantleTotal, KINDA_SMALL_NUMBER), 0.f, 1.f);
	// Ease in and out: the pull-up starts and lands softly.
	const float T = FMath::InterpEaseInOut(0.f, 1.f, Raw, 1.7f);
	const float U = 1.f - T;
	const FVector Point = U * U * U * MantleStart + 3.f * U * U * T * MantleControlA + 3.f * U * T * T * MantleControlB + T * T * T * MantleTarget;
	const FVector OldLocation = UpdatedComponent->GetComponentLocation();
	UpdatedComponent->SetWorldLocation(Point, false, nullptr, ETeleportType::None);
	Velocity = (Point - OldLocation) / DeltaTime;
	if (Raw >= 1.f)
	{
		FinishMantle();
	}
}

void UHitmenCharacterMovementComponent::FinishMantle()
{
	if (bMantleEndsInHang)
	{
		bMantleEndsInHang = false;
		MantleAction = EHitmenTraversalAction::None;
		LastMantleTime = GetTimeSeconds();
		StartHang(MantleHangFace, MantleHangNormal, MantleHangLedgeZ, MantleHangEdge);
		return;
	}
	const FVector Forward = CharacterOwner ? CharacterOwner->GetActorForwardVector().GetSafeNormal2D() : FVector::ForwardVector;
	const bool bKeepMomentum = MantleAction == EHitmenTraversalAction::Vault || MantleAction == EHitmenTraversalAction::ClimbOver;
	Velocity = Forward * (bKeepMomentum ? WalkSpeed * 0.8f : WalkSpeed * 0.25f);
	LastMantleTime = GetTimeSeconds();
	MantleAction = EHitmenTraversalAction::None;
	bMantleFromClimb = false;
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
		// Ziplines are used with F (StartZiplineRide), never grabbed by walking into them.
		return false;
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
	ClimbStartTime = GetTimeSeconds();
	ClimbAlpha = bFromTop ? 0.97f : ProjectOnClimbAxis(UpdatedComponent->GetComponentLocation(), *Climbable);
	Velocity = FVector::ZeroVector;
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Climb));
	OnTraversalMontage.Broadcast(EHitmenAnimMontage::ClimbEnter);
}

void UHitmenCharacterMovementComponent::StartZiplineRide(AActor* ZiplineActor, float Direction)
{
	const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(ZiplineActor);
	if (!Climbable || Climbable->GetClimbType() != EHitmenClimbType::Zipline || IsClimbing() || IsMantling() || !UpdatedComponent)
	{
		return;
	}
	if (IsSliding())
	{
		LastSlideEndTime = GetTimeSeconds();
	}
	ClimbTarget = ZiplineActor;
	ClimbAlpha = ProjectOnClimbAxis(UpdatedComponent->GetComponentLocation(), *Climbable);
	ZiplineDirection = Direction >= 0.f ? 1.f : -1.f;
	ZiplineRideTime = 0.f;
	Velocity = FVector::ZeroVector;
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Climb));
	OnTraversalMontage.Broadcast(EHitmenAnimMontage::ZiplineEnter);
}

bool UHitmenCharacterMovementComponent::IsZiplining() const
{
	const IHitmenClimbable* Climbable = IsClimbing() ? Cast<IHitmenClimbable>(ClimbTarget.Get()) : nullptr;
	return Climbable && Climbable->GetClimbType() == EHitmenClimbType::Zipline;
}

void UHitmenCharacterMovementComponent::StopClimbing(bool bJumpOff)
{
	if (!IsClimbing())
	{
		return;
	}
	const IHitmenClimbable* Climbable = Cast<IHitmenClimbable>(ClimbTarget.Get());
	LastClimbExitTime = GetTimeSeconds();
	LastClimbExitActor = ClimbTarget;
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
	float SpeedScale = 1.f;
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
		// Zipline: rides on its own toward the chosen end, easing up to speed.
		Forward = ZiplineDirection;
		ZiplineRideTime += DeltaTime;
		SpeedScale = ZiplineAccelerationTime > 0.f ? FMath::Clamp(ZiplineRideTime / ZiplineAccelerationTime, 0.2f, 1.f) : 1.f;
	}
	ClimbAxisInput = FMath::Clamp(Forward, -1.f, 1.f);

	const float NewAlpha = ClimbAlpha + ClimbAxisInput * SpeedScale * Climbable->GetClimbSpeed() * DeltaTime / Length;
	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();

	if (Climbable->GetClimbType() == EHitmenClimbType::Ladder)
	{
		if (NewAlpha >= 1.f && ClimbAxisInput > 0.f)
		{
			// Pull up over the top onto the ledge behind the ladder (smooth path, no snap).
			ClimbTarget.Reset();
			StartLadderExit(*Climbable);
			return;
		}
		if (NewAlpha <= 0.f && ClimbAxisInput < 0.f)
		{
			StopClimbing(false);
			StartNewPhysics(DeltaTime, Iterations);
			return;
		}
	}
	else
	{
		// Leave the cable before the anchor, keeping some momentum so the landing carries forward.
		const float Clearance = Climbable->GetClimbOffset() / Length;
		if ((NewAlpha >= 1.f - Clearance && ClimbAxisInput > 0.f) || (NewAlpha <= Clearance && ClimbAxisInput < 0.f))
		{
			const FVector RideDirection = (Axis * ClimbAxisInput).GetSafeNormal2D();
			StopClimbing(false);
			Velocity = RideDirection * Climbable->GetClimbSpeed() * 0.45f;
			StartNewPhysics(DeltaTime, Iterations);
			return;
		}
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

// ---------------------------------------------------------------------------------------------
// Jumps, hook mantle
// ---------------------------------------------------------------------------------------------

bool UHitmenCharacterMovementComponent::DoJump(bool bReplayingMoves, float DeltaTime)
{
	// Extra jumps (double jump) go higher than the first one.
	const bool bExtraJump = CharacterOwner && CharacterOwner->JumpCurrentCount > 0 && IsFalling();
	const float SavedJumpZ = JumpZVelocity;
	if (bExtraJump)
	{
		JumpZVelocity *= ExtraJumpMultiplier;
	}
	const bool bJumped = Super::DoJump(bReplayingMoves, DeltaTime);
	JumpZVelocity = SavedJumpZ;
	return bJumped;
}

bool UHitmenCharacterMovementComponent::TryAutoMantle(const FVector& Forward)
{
	if (!CharacterOwner || !UpdatedComponent || IsMantling() || IsClimbing() || IsHanging())
	{
		return false;
	}
	FHitmenMantlePlan Plan;
	if (PlanMantle(Forward.GetSafeNormal2D(), Plan))
	{
		StartMantlePlan(Plan);
		return true;
	}
	return TryStartHang();
}

// ---------------------------------------------------------------------------------------------
// Hang (thin ledges under windows): shimmy, climb into the window, drop
// ---------------------------------------------------------------------------------------------

bool UHitmenCharacterMovementComponent::FindHangWall(const FVector& Location, const FVector& TowardWall, float LedgeZ, FVector& OutFace, FVector& OutNormal) const
{
	// Under the ledge (below a 15 cm band), straight at the wall.
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenHangWall), false, CharacterOwner);
	const FVector Start(Location.X, Location.Y, LedgeZ - 45.f);
	FHitResult Hit;
	if (!GetWorld()->LineTraceSingleByChannel(Hit, Start, Start + TowardWall.GetSafeNormal2D() * 160.f, ECC_Visibility, Params)
		|| FMath::Abs(Hit.ImpactNormal.Z) > 0.3f || (Hit.GetActor() && Hit.GetActor()->ActorHasTag(HitmenTags::NoTraversal)))
	{
		return false;
	}
	OutFace = Hit.ImpactPoint;
	OutNormal = Hit.ImpactNormal.GetSafeNormal2D();
	return true;
}

bool UHitmenCharacterMovementComponent::FindHangLedge(const FVector& WallFace, const FVector& WallNormal, float NearZ, float& OutLedgeZ, float& OutEdgeDistance) const
{
	// Walk out from the wall face: the ledge is a walkable top near NearZ that sticks out a little.
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenHangLedge), false, CharacterOwner);
	const FCollisionShape Probe = FCollisionShape::MakeSphere(3.f);
	float Found = 0.f;
	float LedgeZ = 0.f;
	int32 Hits = 0;
	for (float Distance = 3.f; Distance <= 55.f; Distance += 4.f)
	{
		const FVector XY = WallFace + WallNormal * Distance;
		FHitResult Top;
		const bool bHit = GetWorld()->SweepSingleByChannel(Top, FVector(XY.X, XY.Y, NearZ + 30.f), FVector(XY.X, XY.Y, NearZ - 30.f), FQuat::Identity, ECC_Visibility, Probe, Params)
			&& !Top.bStartPenetrating && IsWalkable(Top) && (Hits == 0 || FMath::Abs(Top.ImpactPoint.Z - LedgeZ) < 12.f);
		if (!bHit)
		{
			break;
		}
		LedgeZ = Hits == 0 ? Top.ImpactPoint.Z : LedgeZ;
		Found = Distance;
		++Hits;
	}
	// A ledge you hang from sticks out at least a few centimetres, and is thinner than a block you stand on.
	if (Hits < 2 || Found > 50.f)
	{
		return false;
	}
	OutLedgeZ = LedgeZ;
	OutEdgeDistance = Found;
	return true;
}

FVector UHitmenCharacterMovementComponent::GetHangLocation(const FVector& WallFace, const FVector& WallNormal, float LedgeZ, float EdgeDistance) const
{
	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();
	const float Radius = Capsule->GetScaledCapsuleRadius();
	const float HalfHeight = Capsule->GetScaledCapsuleHalfHeight();
	const FVector XY = WallFace + WallNormal * (EdgeDistance + Radius + 2.f);
	return FVector(XY.X, XY.Y, LedgeZ - HangHeadBelowLedge - HalfHeight);
}

bool UHitmenCharacterMovementComponent::TryStartHang()
{
	if (!CharacterOwner || !UpdatedComponent)
	{
		return false;
	}
	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();
	const float HalfHeight = Capsule->GetScaledCapsuleHalfHeight();
	const FVector Location = UpdatedComponent->GetComponentLocation();
	FVector Forward = Acceleration.GetSafeNormal2D();
	if (Forward.IsNearlyZero())
	{
		Forward = CharacterOwner->GetActorForwardVector().GetSafeNormal2D();
	}
	// The hands reach the top of the capsule and a little above.
	const float HandsZ = Location.Z + HalfHeight;
	FVector Face, Normal;
	for (const float Probe : { HandsZ - 10.f, HandsZ + 15.f, HandsZ + 40.f })
	{
		if (!FindHangWall(Location, Forward, Probe, Face, Normal) || FVector::DotProduct(Normal, Forward) > -0.3f)
		{
			continue;
		}
		float LedgeZ = 0.f;
		float Edge = 0.f;
		if (!FindHangLedge(Face, Normal, Probe, LedgeZ, Edge) || LedgeZ < HandsZ - 30.f || LedgeZ > HandsZ + 55.f)
		{
			continue;
		}
		// Not a ledge over open ground you could just stand under: something must drop away below it.
		const FVector HangAt = GetHangLocation(Face, Normal, LedgeZ, Edge);
		FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenHangFit), false, CharacterOwner);
		const FCollisionShape Shape = FCollisionShape::MakeCapsule(Capsule->GetScaledCapsuleRadius() * 0.95f, HalfHeight * 0.95f);
		if (GetWorld()->OverlapBlockingTestByChannel(HangAt, FQuat::Identity, UpdatedComponent->GetCollisionObjectType(), Shape, Params))
		{
			continue;
		}
		StartHang(Face, Normal, LedgeZ, Edge);
		return true;
	}
	return false;
}

void UHitmenCharacterMovementComponent::StartHang(const FVector& WallFace, const FVector& WallNormal, float LedgeZ, float EdgeDistance)
{
	HangNormal = WallNormal;
	HangLedgeZ = LedgeZ;
	HangEdgeDistance = EdgeDistance;
	Velocity = FVector::ZeroVector;
	UpdatedComponent->SetWorldLocation(GetHangLocation(WallFace, WallNormal, LedgeZ, EdgeDistance), false, nullptr, ETeleportType::None);
	SetMovementMode(MOVE_Custom, ToCustom(EHitmenCustomMovement::Hang));
	OnTraversalMontage.Broadcast(EHitmenAnimMontage::ClimbEnter);
}

void UHitmenCharacterMovementComponent::StopHang(bool bDrop)
{
	LastHangReleaseTime = GetTimeSeconds();
	SetMovementMode(MOVE_Falling);
	Velocity = bDrop ? HangNormal * 60.f : FVector::ZeroVector;
}

void UHitmenCharacterMovementComponent::PhysHang(float DeltaTime, int32 Iterations)
{
	if (DeltaTime < MIN_TICK_TIME || !CharacterOwner)
	{
		return;
	}
	// Left / right along the wall (whatever the camera does): the input projected on the wall tangent.
	const FVector Tangent = FVector::CrossProduct(FVector::UpVector, HangNormal).GetSafeNormal();
	const float Lateral = FVector::DotProduct(Acceleration.GetSafeNormal2D(), Tangent);
	const FVector OldLocation = UpdatedComponent->GetComponentLocation();
	Velocity = FVector::ZeroVector;
	if (FMath::Abs(Lateral) > 0.2f)
	{
		const float Speed = (bWantsToSprint ? HangShimmyFastSpeed : HangShimmySpeed) * FMath::Sign(Lateral);
		const FVector Candidate = OldLocation + Tangent * Speed * DeltaTime;
		FVector Face, Normal;
		float LedgeZ = 0.f;
		float Edge = 0.f;
		// Only move where the ledge (and the wall under it) goes on, at about the same height.
		if (FindHangWall(Candidate, -HangNormal, HangLedgeZ, Face, Normal)
			&& FVector::DotProduct(Normal, HangNormal) > 0.8f
			&& FindHangLedge(Face, Normal, HangLedgeZ, LedgeZ, Edge)
			&& FMath::Abs(LedgeZ - HangLedgeZ) < 15.f)
		{
			const FVector Target = GetHangLocation(Face, Normal, LedgeZ, Edge);
			FHitResult Hit;
			SafeMoveUpdatedComponent(Target - OldLocation, UpdatedComponent->GetComponentQuat(), true, Hit);
			if (!Hit.bBlockingHit)
			{
				HangNormal = Normal;
				HangLedgeZ = LedgeZ;
				HangEdgeDistance = Edge;
			}
			Velocity = (UpdatedComponent->GetComponentLocation() - OldLocation) / DeltaTime;
		}
	}
}

bool UHitmenCharacterMovementComponent::PlanHangClimb(FHitmenMantlePlan& OutPlan) const
{
	const UCapsuleComponent* Capsule = CharacterOwner->GetCapsuleComponent();
	const float Radius = Capsule->GetScaledCapsuleRadius();
	const float HalfHeight = Capsule->GetScaledCapsuleHalfHeight();
	const FVector Location = UpdatedComponent->GetComponentLocation();
	const FVector Into = -HangNormal;
	const UWorld* World = GetWorld();
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenHangClimb), false, CharacterOwner);
	const ECollisionChannel Channel = UpdatedComponent->GetCollisionObjectType();
	const FCollisionShape StandShape = FCollisionShape::MakeCapsule(Radius * 0.95f, HalfHeight * 0.95f);
	const float Reach = HangEdgeDistance + Radius + 40.f;

	OutPlan = FHitmenMantlePlan();
	OutPlan.Start = Location;
	OutPlan.Action = EHitmenTraversalAction::Climb;
	OutPlan.Duration = HangClimbDuration;

	// 1. An opening in the wall above the ledge (a window): over its sill, onto the floor inside.
	for (float Height = HangLedgeZ + 10.f; Height <= HangLedgeZ + 150.f; Height += 10.f)
	{
		const FVector Start(Location.X, Location.Y, Height);
		if (World->LineTraceTestByChannel(Start, Start + Into * Reach, ECC_Visibility, Params))
		{
			continue;
		}
		// The sill: the first solid surface below the opening, just inside the wall line.
		const FVector SillXY = Start + Into * (HangEdgeDistance + Radius + 12.f);
		FHitResult Sill;
		if (!World->SweepSingleByChannel(Sill, FVector(SillXY.X, SillXY.Y, Height), FVector(SillXY.X, SillXY.Y, HangLedgeZ - 20.f), FQuat::Identity, ECC_Visibility, FCollisionShape::MakeSphere(4.f), Params))
		{
			break;
		}
		const FVector InsideXY = SillXY + Into * (Radius + 45.f);
		FHitResult Floor;
		if (World->LineTraceSingleByChannel(Floor, FVector(InsideXY.X, InsideXY.Y, Sill.ImpactPoint.Z + 40.f), FVector(InsideXY.X, InsideXY.Y, Sill.ImpactPoint.Z - 160.f), ECC_Visibility, Params)
			&& IsWalkable(Floor))
		{
			const FVector Dest = Floor.ImpactPoint + FVector::UpVector * (HalfHeight + 3.f);
			if (!World->OverlapBlockingTestByChannel(Dest, FQuat::Identity, Channel, StandShape, Params))
			{
				const FVector Over(SillXY.X, SillXY.Y, Sill.ImpactPoint.Z + HalfHeight + MantleClearance);
				OutPlan.ControlA = FVector(Location.X, Location.Y, Over.Z);
				OutPlan.ControlB = Over + Into * 20.f;
				OutPlan.Target = Dest;
				return true;
			}
		}
		break;
	}

	// 2. Room to stand on the ledge top itself (a roof edge, a wall top).
	const FVector TopXY = Location + Into * (Radius + HangEdgeDistance * 0.5f);
	const FVector OnTop(TopXY.X, TopXY.Y, HangLedgeZ + HalfHeight + 3.f);
	if (!World->OverlapBlockingTestByChannel(OnTop, FQuat::Identity, Channel, StandShape, Params))
	{
		FHitResult Under;
		if (World->LineTraceSingleByChannel(Under, OnTop, OnTop - FVector::UpVector * (HalfHeight + 20.f), ECC_Visibility, Params) && IsWalkable(Under))
		{
			OutPlan.ControlA = FVector(Location.X, Location.Y, OnTop.Z + MantleClearance);
			OutPlan.ControlB = OnTop + FVector::UpVector * (MantleClearance * 0.5f);
			OutPlan.Target = OnTop;
			return true;
		}
	}
	return false;
}

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
	SavedSlideElapsed = 0.f;
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
		SavedSlideElapsed = Movement->SlideElapsed;
	}
}

void FSavedMove_Hitmen::PrepMoveFor(ACharacter* C)
{
	Super::PrepMoveFor(C);
	if (UHitmenCharacterMovementComponent* Movement = Cast<UHitmenCharacterMovementComponent>(C->GetCharacterMovement()))
	{
		Movement->bWantsToSprint = bSavedWantsToSprint;
		Movement->bWantsJetpackThrust = bSavedWantsJetpackThrust;
		// Replays start from the fuel (and the slide time) this move started with.
		Movement->JetpackFuel = SavedJetpackFuel;
		Movement->SlideElapsed = SavedSlideElapsed;
	}
}

FSavedMovePtr FNetworkPredictionData_Client_Hitmen::AllocateNewMove()
{
	return FSavedMovePtr(new FSavedMove_Hitmen());
}
