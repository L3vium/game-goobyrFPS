// Hitmen — spectator pawn

#include "Spectator/HitmenSpectatorPawn.h"
#include "GameFramework/FloatingPawnMovement.h"
#include "Core/HitmenGameSettings.h"
#include "Data/HitmenMapDefinition.h"
#include "Engine/World.h"
#include "CollisionQueryParams.h"

AHitmenSpectatorPawn::AHitmenSpectatorPawn()
{
	PrimaryActorTick.bCanEverTick = true;
	bAddDefaultMovementBindings = false;
	if (UFloatingPawnMovement* Movement = Cast<UFloatingPawnMovement>(GetMovementComponent()))
	{
		Movement->MaxSpeed = FlySpeed;
	}
}

void AHitmenSpectatorPawn::BeginPlay()
{
	Super::BeginPlay();
	// The map's world bounds (DA_Map), shrunk by the margin; no map definition = no horizontal limit.
	const FString Current = UWorld::RemovePIEPrefix(GetWorld()->GetOutermost()->GetName());
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	for (int32 Index = 0; Index < Settings->Maps.Num(); ++Index)
	{
		const UHitmenMapDefinition* Map = Settings->LoadMap(Index);
		if (Map && Map->Level.ToSoftObjectPath().GetLongPackageName() == Current)
		{
			WorldBounds = FBox2D(Map->WorldBounds.Min + FVector2D(BoundsMargin), Map->WorldBounds.Max - FVector2D(BoundsMargin));
			break;
		}
	}
	CeilingZ = GetActorLocation().Z + MaxHeightAboveGround;
}

void AHitmenSpectatorPawn::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!FMath::IsNearlyZero(VerticalInput))
	{
		AddMovementInput(FVector::UpVector, VerticalInput);
	}
	ClampToBounds();
}

void AHitmenSpectatorPawn::ClampToBounds()
{
	FVector Location = GetActorLocation();
	const FVector Before = Location;
	if (WorldBounds.bIsValid && WorldBounds.Max.X > WorldBounds.Min.X && WorldBounds.Max.Y > WorldBounds.Min.Y)
	{
		Location.X = FMath::Clamp(Location.X, WorldBounds.Min.X, WorldBounds.Max.X);
		Location.Y = FMath::Clamp(Location.Y, WorldBounds.Min.Y, WorldBounds.Max.Y);
	}
	// Above the ground under the camera, below a ceiling that follows the highest ground flown over.
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSpectatorGround), false, this);
	if (GetWorld()->LineTraceSingleByChannel(Hit, Location + FVector(0.f, 0.f, 50.f), Location - FVector(0.f, 0.f, 100000.f), ECC_Visibility, Params))
	{
		CeilingZ = FMath::Max(CeilingZ, Hit.ImpactPoint.Z + MaxHeightAboveGround);
		Location.Z = FMath::Max(Location.Z, Hit.ImpactPoint.Z + MinHeightAboveGround);
	}
	Location.Z = FMath::Min(Location.Z, CeilingZ);
	if (!Location.Equals(Before, 0.1f))
	{
		SetActorLocation(Location);
		if (UPawnMovementComponent* Movement = GetMovementComponent())
		{
			// Kill the velocity pushing into the limit so the camera does not stick to it.
			FVector Velocity = Movement->Velocity;
			if (!FMath::IsNearlyEqual(Location.X, Before.X)) { Velocity.X = 0.f; }
			if (!FMath::IsNearlyEqual(Location.Y, Before.Y)) { Velocity.Y = 0.f; }
			if (!FMath::IsNearlyEqual(Location.Z, Before.Z)) { Velocity.Z = 0.f; }
			Movement->Velocity = Velocity;
		}
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
