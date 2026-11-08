// Hitmen — spectator pawn

#include "Spectator/HitmenSpectatorPawn.h"
#include "GameFramework/FloatingPawnMovement.h"

AHitmenSpectatorPawn::AHitmenSpectatorPawn()
{
	PrimaryActorTick.bCanEverTick = true;
	bAddDefaultMovementBindings = false;
	if (UFloatingPawnMovement* Movement = Cast<UFloatingPawnMovement>(GetMovementComponent()))
	{
		Movement->MaxSpeed = FlySpeed;
	}
}

void AHitmenSpectatorPawn::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!FMath::IsNearlyZero(VerticalInput))
	{
		AddMovementInput(FVector::UpVector, VerticalInput);
	}
}

void AHitmenSpectatorPawn::Input_Move(const FVector2D& Axis)
{
	const FRotator View = GetControlRotation();
	AddMovementInput(FRotationMatrix(View).GetUnitAxis(EAxis::X), Axis.Y);
	AddMovementInput(FRotationMatrix(View).GetUnitAxis(EAxis::Y), Axis.X);
}

void AHitmenSpectatorPawn::Input_Look(const FVector2D& Axis)
{
	AddControllerYawInput(Axis.X);
	AddControllerPitchInput(-Axis.Y);
}

void AHitmenSpectatorPawn::Input_SprintChanged(bool bWantsSprint)
{
	if (UFloatingPawnMovement* Movement = Cast<UFloatingPawnMovement>(GetMovementComponent()))
	{
		Movement->MaxSpeed = FlySpeed * (bWantsSprint ? FastMultiplier : 1.f);
	}
}
