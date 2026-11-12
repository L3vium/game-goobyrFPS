// Hitmen — move abilities

#include "Abilities/Move/HitmenMoveAbilities.h"
#include "Abilities/HitmenAbilityActors.h"
#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenHealthComponent.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Weapons/HitmenProjectile.h"
#include "Components/CapsuleComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "NiagaraFunctionLibrary.h"
#include "NiagaraComponent.h"

// ---------------------------------------------------------------------------------------------
// Blink
// ---------------------------------------------------------------------------------------------

UHitmenAbility_Blink::UHitmenAbility_Blink()
{
	Cooldown = 4.f;
	Range = 1600.f;
	bActivateOnRelease = true;
}

bool UHitmenAbility_Blink::ComputeDestination(const FVector& AimOrigin, const FVector& AimDirection, FVector& OutDestination) const
{
	const AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character)
	{
		return false;
	}
	FHitResult Hit;
	FVector Target = AimOrigin + AimDirection.GetSafeNormal() * Range;
	if (AimTrace(AimOrigin, AimDirection, Hit))
	{
		Target = Hit.ImpactPoint + Hit.ImpactNormal * WallOffset;
	}
	// Stand on the ground under the target if possible.
	const float HalfHeight = Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
	FHitResult Ground;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenBlink), false, Character);
	if (GetWorld()->LineTraceSingleByChannel(Ground, Target + FVector::UpVector * 50.f, Target - FVector::UpVector * 300.f, ECC_Visibility, Params))
	{
		Target = Ground.ImpactPoint + FVector::UpVector * (HalfHeight + 5.f);
	}
	FVector Adjusted = Target;
	if (!GetWorld()->FindTeleportSpot(Character, Adjusted, Character->GetActorRotation()))
	{
		return false;
	}
	OutDestination = Adjusted;
	return true;
}

bool UHitmenAbility_Blink::GetAimMarker(const FVector& Origin, const FVector& Direction, FVector& OutPoint) const
{
	// Only while E is held: the exact landing spot, on the ground under the capsule.
	FVector Destination;
	if (!bAiming || !ComputeDestination(Origin, Direction, Destination))
	{
		return false;
	}
	const AHitmenCharacterBase* Character = GetOwnerCharacter();
	const float HalfHeight = Character ? Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight() : 90.f;
	OutPoint = Destination - FVector::UpVector * HalfHeight;
	return true;
}

void UHitmenAbility_Blink::LocalPress(const FVector& AimOrigin, const FVector& AimDirection)
{
	bAiming = true;
}

void UHitmenAbility_Blink::LocalRelease()
{
	bAiming = false;
	Juice_OnAimPreview(false, FVector::ZeroVector);
}

void UHitmenAbility_Blink::LocalTick(float DeltaTime)
{
	const APawn* Pawn = GetOwnerPawn();
	if (!Pawn || !Pawn->GetController())
	{
		return;
	}
	FVector Origin;
	FRotator Rotation;
	Pawn->GetController()->GetPlayerViewPoint(Origin, Rotation);
	FVector Destination = FVector::ZeroVector;
	Juice_OnAimPreview(ComputeDestination(Origin, Rotation.Vector(), Destination), Destination);
}

bool UHitmenAbility_Blink::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Character = GetOwnerCharacter();
	FVector Destination;
	if (!Character || !ComputeDestination(AimOrigin, AimDirection, Destination))
	{
		return false;
	}
	if (UHitmenCharacterMovementComponent* Movement = Character->GetHitmenMovement())
	{
		Movement->StopClimbing(false);
	}
	PlayActivatedJuice(Character->GetActorLocation());
	return Character->TeleportTo(Destination, Character->GetActorRotation());
}

// ---------------------------------------------------------------------------------------------
// Double jump
// ---------------------------------------------------------------------------------------------

void UHitmenAbility_DoubleJump::OnEquipped()
{
	if (AHitmenCharacterBase* Character = GetOwnerCharacter())
	{
		Character->JumpMaxCount = JumpCount;
		if (UHitmenCharacterMovementComponent* Movement = Character->GetHitmenMovement())
		{
			Movement->ExtraJumpMultiplier = ExtraJumpMultiplier;
		}
	}
}

void UHitmenAbility_DoubleJump::OnUnequipped()
{
	if (AHitmenCharacterBase* Character = GetOwnerCharacter())
	{
		Character->JumpMaxCount = 1;
		if (UHitmenCharacterMovementComponent* Movement = Character->GetHitmenMovement())
		{
			Movement->ExtraJumpMultiplier = 1.f;
		}
	}
}

// ---------------------------------------------------------------------------------------------
// Jetpack
// ---------------------------------------------------------------------------------------------

UHitmenAbility_Jetpack::UHitmenAbility_Jetpack()
{
	Cooldown = 0.f;
}

void UHitmenAbility_Jetpack::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAbility_Jetpack, Fuel);
	DOREPLIFETIME(UHitmenAbility_Jetpack, bThrusting);
}

UHitmenCharacterMovementComponent* UHitmenAbility_Jetpack::GetMovement() const
{
	const AHitmenCharacterBase* Character = GetOwnerCharacter();
	return Character ? Character->GetHitmenMovement() : nullptr;
}

void UHitmenAbility_Jetpack::OnEquipped()
{
	Fuel = MaxFuel;
	if (UHitmenCharacterMovementComponent* Movement = GetMovement())
	{
		Movement->ConfigureJetpack(true, MaxFuel, FuelRechargePerSecond, LiftSpeed, LiftInterpSpeed);
	}
}

void UHitmenAbility_Jetpack::OnUnequipped()
{
	if (UHitmenCharacterMovementComponent* Movement = GetMovement())
	{
		Movement->ConfigureJetpack(false, 0.f, 0.f, 0.f, 0.f);
	}
}

void UHitmenAbility_Jetpack::LocalPress(const FVector& AimOrigin, const FVector& AimDirection)
{
	// Owning client and listen-server host: the flag travels to the server in the saved moves.
	if (UHitmenCharacterMovementComponent* Movement = GetMovement())
	{
		Movement->SetJetpackThrust(true);
	}
}

void UHitmenAbility_Jetpack::LocalRelease()
{
	if (UHitmenCharacterMovementComponent* Movement = GetMovement())
	{
		Movement->SetJetpackThrust(false);
	}
}

bool UHitmenAbility_Jetpack::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	if (Fuel > 0.f)
	{
		PlayActivatedJuice(GetOwnerCharacter()->GetActorLocation());
	}
	return false; // Fuel gates it, not a cooldown.
}

void UHitmenAbility_Jetpack::Release(const FVector& AimOrigin, const FVector& AimDirection)
{
	Juice_OnEnded();
}

void UHitmenAbility_Jetpack::TickAbility(float DeltaTime)
{
	// Server: the movement component owns the simulation; mirror its fuel for the UI.
	const UHitmenCharacterMovementComponent* Movement = GetMovement();
	if (!Movement)
	{
		return;
	}
	Fuel = Movement->GetJetpackFuel();
	const bool bNowThrusting = Movement->IsJetpackThrusting();
	if (bWasThrusting && !bNowThrusting && Movement->WantsJetpackThrust() && Fuel <= 0.f)
	{
		Juice_OnEnded(); // Ran dry while held.
	}
	bWasThrusting = bNowThrusting;
	// Replicated to everyone for the smoke; the listen-server host shows it for remote players itself.
	bThrusting = bNowThrusting;
	if (const APawn* Pawn = GetOwnerPawn(); Pawn && !Pawn->IsLocallyControlled())
	{
		SetThrustEffect(bNowThrusting);
	}
}

void UHitmenAbility_Jetpack::LocalTick(float DeltaTime)
{
	// Owning client: correct a drifted prediction while idle.
	if (UHitmenCharacterMovementComponent* Movement = GetMovement())
	{
		Movement->ReconcileJetpackFuel(Fuel, FuelReconcileTolerance);
		// Your own smoke follows your prediction (no wait for the server).
		SetThrustEffect(Movement->IsJetpackThrusting());
	}
}

float UHitmenAbility_Jetpack::GetFuelNormalized() const
{
	const APawn* Pawn = GetOwnerPawn();
	const UHitmenCharacterMovementComponent* Movement = GetMovement();
	if (Pawn && Pawn->IsLocallyControlled() && Movement && Movement->IsJetpackEnabled())
	{
		return Movement->GetJetpackFuelNormalized();
	}
	return MaxFuel > 0.f ? Fuel / MaxFuel : 0.f;
}

// ---------------------------------------------------------------------------------------------
// Dash
// ---------------------------------------------------------------------------------------------

UHitmenAbility_Dash::UHitmenAbility_Dash()
{
	Cooldown = 1.5f;
}

FVector UHitmenAbility_Dash::GetDashVelocity() const
{
	const AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character)
	{
		return FVector::ZeroVector;
	}
	FVector Direction = Character->GetLastMovementInputVector().GetSafeNormal2D();
	if (Direction.IsNearlyZero())
	{
		Direction = Character->GetActorForwardVector().GetSafeNormal2D();
	}
	return Direction * DashSpeed + FVector::UpVector * UpwardBoost;
}

void UHitmenAbility_Dash::LocalPress(const FVector& AimOrigin, const FVector& AimDirection)
{
	if (AHitmenCharacterBase* Character = GetOwnerCharacter())
	{
		Character->LaunchCharacter(GetDashVelocity(), true, true);
	}
}

bool UHitmenAbility_Dash::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character)
	{
		return false;
	}
	Character->LaunchCharacter(GetDashVelocity(), true, true);
	PlayActivatedJuice(Character->GetActorLocation());
	return true;
}

// ---------------------------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------------------------

UHitmenAbility_Hook::UHitmenAbility_Hook()
{
	Cooldown = 5.f;
	Range = 2500.f;
}

void UHitmenAbility_Hook::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenAbility_Hook, bHooked);
	DOREPLIFETIME(UHitmenAbility_Hook, HookPoint);
}

bool UHitmenAbility_Hook::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	FHitResult Hit;
	if (!AimTrace(AimOrigin, AimDirection, Hit))
	{
		return false;
	}
	HookPoint = Hit.ImpactPoint;
	bHooked = true;
	PullTime = 0.f;
	if (UHitmenCharacterMovementComponent* Movement = GetOwnerCharacter()->GetHitmenMovement())
	{
		Movement->StopClimbing(false);
		Movement->SetMovementMode(MOVE_Falling);
	}
	PlayActivatedJuice(HookPoint);
	return true;
}

bool UHitmenAbility_Hook::GetAimMarker(const FVector& Origin, const FVector& Direction, FVector& OutPoint) const
{
	// The same trace as Activate: the marker shows exactly where the hook would grab, only within range.
	FHitResult Hit;
	if (bHooked || !AimTrace(Origin, Direction, Hit))
	{
		return false;
	}
	OutPoint = Hit.ImpactPoint;
	return true;
}

void UHitmenAbility_Hook::OnRep_Hooked()
{
	if (bHooked)
	{
		PullTime = 0.f;
	}
}

void UHitmenAbility_Hook::Pull(float DeltaTime)
{
	AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!bHooked || !Character)
	{
		return;
	}
	PullTime += DeltaTime;
	const FVector ToPoint = HookPoint - Character->GetActorLocation();
	if (ToPoint.Size() < StopDistance || PullTime > MaxPullTime || Character->IsDead())
	{
		// At the anchor: climb the ledge there (or hang from it) instead of dropping off the wall.
		if (!Character->IsDead() && ToPoint.Size() < StopDistance * 1.5f)
		{
			if (UHitmenCharacterMovementComponent* Movement = Character->GetHitmenMovement(); Movement && Movement->IsFalling())
			{
				Movement->TryAutoMantle(ToPoint.GetSafeNormal2D().IsNearlyZero() ? Character->GetActorForwardVector() : ToPoint.GetSafeNormal2D());
			}
		}
		if (Character->HasAuthority())
		{
			bHooked = false;
			Juice_OnEnded();
		}
		return;
	}
	Character->LaunchCharacter(ToPoint.GetSafeNormal() * PullSpeed, true, true);
}

// ---------------------------------------------------------------------------------------------
// Transposition
// ---------------------------------------------------------------------------------------------

AActor* UHitmenAbility_Transposition::FindHintTarget(const FVector& Origin, const FVector& Direction, FText& OutAction) const
{
	if (AHitmenCharacterBase* Target = AimCharacterInCone(Origin, Direction, true))
	{
		OutAction = NSLOCTEXT("Hitmen", "HintTransposition", "Swap places");
		return Target;
	}
	return nullptr;
}

bool UHitmenAbility_Transposition::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Self = GetOwnerCharacter();
	AHitmenCharacterBase* Target = AimCharacterInCone(AimOrigin, AimDirection, true);
	if (!Self || !Target)
	{
		return false;
	}
	const FVector SelfLocation = Self->GetActorLocation();
	if (Target->IsDead())
	{
		// A body: you take its place, the ragdoll is moved where you stood.
		USkeletalMeshComponent* BodyMesh = Target->GetMesh();
		const FVector BodyLocation = BodyMesh ? BodyMesh->GetBoneLocation(TEXT("pelvis")) : Target->GetActorLocation();
		const FVector Offset = SelfLocation - BodyLocation;
		Self->TeleportTo(BodyLocation + FVector::UpVector * Self->GetCapsuleComponent()->GetScaledCapsuleHalfHeight(), Self->GetActorRotation());
		Target->SetActorLocation(Target->GetActorLocation() + Offset, false, nullptr, ETeleportType::TeleportPhysics);
		if (BodyMesh && BodyMesh->IsSimulatingPhysics())
		{
			BodyMesh->SetWorldLocation(BodyMesh->GetComponentLocation() + Offset, false, nullptr, ETeleportType::TeleportPhysics);
		}
		PlayActivatedJuice(BodyLocation);
		return true;
	}
	const FVector TargetLocation = Target->GetActorLocation();
	// Move the target out of the way first so the swap never overlaps.
	Target->SetActorLocation(TargetLocation + FVector::UpVector * 2000.f, false, nullptr, ETeleportType::TeleportPhysics);
	Self->TeleportTo(TargetLocation, Self->GetActorRotation());
	Target->TeleportTo(SelfLocation, Target->GetActorRotation());
	PlayActivatedJuice(TargetLocation);
	return true;
}

// ---------------------------------------------------------------------------------------------
// Self status (pill, invisibility)
// ---------------------------------------------------------------------------------------------

bool UHitmenAbility_SelfStatus::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character)
	{
		return false;
	}
	Character->GetStatusEffects()->ApplyStatus(Status, Duration, Magnitude, Character);
	PlayActivatedJuice(Character->GetActorLocation());
	return true;
}

UHitmenAbility_Pill::UHitmenAbility_Pill()
{
	Status = EHitmenStatusEffect::Boosted;
	Duration = 10.f;
	Cooldown = 25.f;
	MaxCharges = 3;
}

UHitmenAbility_Invisibility::UHitmenAbility_Invisibility()
{
	Status = EHitmenStatusEffect::Invisible;
	Duration = 7.f;
	Cooldown = 30.f;
}

// ---------------------------------------------------------------------------------------------
// Armor
// ---------------------------------------------------------------------------------------------

UHitmenAbility_Armor::UHitmenAbility_Armor()
{
	Duration = 12.f;
	Cooldown = 30.f;
}

bool UHitmenAbility_Armor::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character)
	{
		return false;
	}
	Character->GetHealth()->AddArmor(ArmorAmount);
	Character->GetStatusEffects()->ApplyStatus(EHitmenStatusEffect::Armored, Duration, 1.f, Character);
	PlayActivatedJuice(Character->GetActorLocation());
	return true;
}

// ---------------------------------------------------------------------------------------------
// C4
// ---------------------------------------------------------------------------------------------

UHitmenAbility_C4::UHitmenAbility_C4()
{
	Cooldown = 1.f;
	MaxCharges = 3;
	ChargeClass = AHitmenC4Charge::StaticClass();
}

bool UHitmenAbility_C4::CanActivate() const
{
	// Detonating a placed charge is always allowed.
	return PlacedCharge.IsValid() || Super::CanActivate();
}

bool UHitmenAbility_C4::Activate(const FVector& AimOrigin, const FVector& AimDirection)
{
	if (AHitmenProjectile* Charge = PlacedCharge.Get())
	{
		Charge->Detonate();
		PlacedCharge.Reset();
		return false;
	}
	AHitmenProjectile* Charge = Cast<AHitmenProjectile>(SpawnOwnedActor(ChargeClass, FTransform(AimDirection.Rotation(), AimOrigin + AimDirection * 60.f)));
	if (!Charge)
	{
		return false;
	}
	Charge->Launch(AimDirection * ThrowSpeed, 0.f);
	PlacedCharge = Charge;
	PlayActivatedJuice(AimOrigin);
	return true;
}

void UHitmenAbility_Jetpack::OnRep_Thrusting()
{
	// The owner shows its own (predicted) smoke; everyone else follows the server.
	const APawn* Pawn = GetOwnerPawn();
	if (!Pawn || !Pawn->IsLocallyControlled())
	{
		SetThrustEffect(bThrusting);
	}
}

void UHitmenAbility_Jetpack::SetThrustEffect(bool bOn)
{
	AHitmenCharacterBase* Character = GetOwnerCharacter();
	if (!Character || !GetWorld() || GetWorld()->GetNetMode() == NM_DedicatedServer || !ThrustFX)
	{
		return;
	}
	if (bOn && !ThrustEffect)
	{
		// Under the feet, blowing down.
		const float HalfHeight = Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
		ThrustEffect = UNiagaraFunctionLibrary::SpawnSystemAttached(ThrustFX, Character->GetRootComponent(), NAME_None, FVector(0.f, 0.f, -HalfHeight + 10.f),
			FRotator(180.f, 0.f, 0.f), FVector(ThrustFXScale), EAttachLocation::KeepRelativeOffset, false, ENCPoolMethod::None);
	}
	else if (!bOn && ThrustEffect)
	{
		ThrustEffect->Deactivate();
		ThrustEffect->SetAutoDestroy(true);
		ThrustEffect = nullptr;
	}
}
