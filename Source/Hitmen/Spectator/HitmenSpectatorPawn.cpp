// Hitmen — spectator pawn

#include "Spectator/HitmenSpectatorPawn.h"
#include "GameFramework/FloatingPawnMovement.h"
#include "GameFramework/PlayerController.h"
#include "Camera/CameraComponent.h"
#include "Components/SphereComponent.h"
#include "Components/HitmenSightroomComponent.h"
#include "Interaction/HitmenInteractable.h"
#include "Interaction/HitmenInteractionComponent.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Core/HitmenGameSettings.h"
#include "Data/HitmenMapDefinition.h"
#include "Hitmen.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "CollisionQueryParams.h"

AHitmenSpectatorPawn::AHitmenSpectatorPawn()
{
	PrimaryActorTick.bCanEverTick = true;
	bAddDefaultMovementBindings = false;
	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(GetRootComponent());
	Camera->bUsePawnControlRotation = true;
	Sightroom = CreateDefaultSubobject<UHitmenSightroomComponent>(TEXT("Sightroom"));
	ApplySpeed();
}

void AHitmenSpectatorPawn::BeginPlay()
{
	Super::BeginPlay();
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	// The map's world bounds (DA_Map), shrunk by the margin; no map definition = no horizontal limit.
	const FString Current = UWorld::RemovePIEPrefix(GetWorld()->GetOutermost()->GetName());
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
	// Through walls: no collision at all (the box and the ground rule still hold).
	if (UPrimitiveComponent* Collision = GetCollisionComponent())
	{
		Collision->SetCollisionEnabled(Settings->bWatchersFlyThroughWalls ? ECollisionEnabled::NoCollision : ECollisionEnabled::QueryOnly);
	}
	ApplySpeed();
	if (Settings->bWatchersSeeInteractableInfo || Settings->bWatchersHighlightInteractables)
	{
		GetWorldTimerManager().SetTimer(FocusTimer, this, &AHitmenSpectatorPawn::UpdateFocus, FocusInterval, true);
	}
}

void AHitmenSpectatorPawn::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	GetWorldTimerManager().ClearTimer(FocusTimer);
	ClearFocus();
	Super::EndPlay(EndPlayReason);
}

void AHitmenSpectatorPawn::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (bFreeFly && !FMath::IsNearlyZero(VerticalInput))
	{
		AddMovementInput(FVector::UpVector, VerticalInput);
	}
	ClampToBounds();
}

float AHitmenSpectatorPawn::FindGroundBelow(const FVector& Location) const
{
	// Every static surface on a vertical line from above the ceiling: the first terrain tile from the top, else the
	// lowest surface (the floor of the world under buildings and bridges).
	const FVector Top(Location.X, Location.Y, FMath::Max(CeilingZ, Location.Z) + 2000.f);
	TArray<FHitResult> Hits;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSpectatorGround), false, this);
	GetWorld()->LineTraceMultiByObjectType(Hits, Top, Top - FVector(0.f, 0.f, 200000.f), FCollisionObjectQueryParams(ECC_WorldStatic), Params);
	float Lowest = -UE_BIG_NUMBER;
	for (const FHitResult& Hit : Hits)
	{
		const AActor* Actor = Hit.GetActor();
		if (Actor && Actor->ActorHasTag(TEXT("HitmenTerrain")))
		{
			return Hit.ImpactPoint.Z;
		}
		Lowest = Hit.ImpactPoint.Z;
	}
	return Lowest;
}

void AHitmenSpectatorPawn::ClampToBounds()
{
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	FVector Location = GetActorLocation();
	const FVector Before = Location;
	if (WorldBounds.bIsValid && WorldBounds.Max.X > WorldBounds.Min.X && WorldBounds.Max.Y > WorldBounds.Min.Y)
	{
		Location.X = FMath::Clamp(Location.X, WorldBounds.Min.X, WorldBounds.Max.X);
		Location.Y = FMath::Clamp(Location.Y, WorldBounds.Min.Y, WorldBounds.Max.Y);
	}
	if (Settings->bWatchersFlyThroughTerrain)
	{
		// Under the ground too, down to a floor below the terrain (the box's bottom).
		const float Ground = FindGroundBelow(Location);
		if (Ground > -UE_BIG_NUMBER)
		{
			CeilingZ = FMath::Max(CeilingZ, Ground + MaxHeightAboveGround);
			Location.Z = FMath::Max(Location.Z, Ground - Settings->WatcherDepthBelowGround);
		}
	}
	else
	{
		// Above the surface under the camera (inside a cave or a building too).
		FHitResult Hit;
		FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSpectatorGround), false, this);
		if (GetWorld()->LineTraceSingleByChannel(Hit, Location + FVector(0.f, 0.f, 50.f), Location - FVector(0.f, 0.f, 100000.f), ECC_Visibility, Params))
		{
			CeilingZ = FMath::Max(CeilingZ, Hit.ImpactPoint.Z + MaxHeightAboveGround);
			Location.Z = FMath::Max(Location.Z, Hit.ImpactPoint.Z + MinHeightAboveGround);
		}
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
	if (!bFreeFly)
	{
		return;
	}
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
	bFast = bWantsSprint;
	ApplySpeed();
}

void AHitmenSpectatorPawn::Input_SightroomChanged(bool bActive)
{
	if (Sightroom && (!bActive || UHitmenGameSettings::Get()->bWatchersCanScan))
	{
		Sightroom->SetSightroomActive(bActive);
	}
}

float AHitmenSpectatorPawn::AdjustSpeed(float Notches)
{
	SpeedScale = FMath::Clamp(SpeedScale * FMath::Pow(SpeedStep, Notches), SpeedScaleMin, SpeedScaleMax);
	ApplySpeed();
	return SpeedScale;
}

void AHitmenSpectatorPawn::ApplySpeed()
{
	if (UFloatingPawnMovement* Movement = Cast<UFloatingPawnMovement>(GetMovementComponent()))
	{
		Movement->MaxSpeed = FlySpeed * SpeedScale * (bFast ? FastMultiplier : 1.f);
		// Quick to start and stop at any speed.
		Movement->Acceleration = Movement->MaxSpeed * 4.f;
		Movement->Deceleration = Movement->MaxSpeed * 8.f;
	}
}

void AHitmenSpectatorPawn::SetFreeFly(bool bInFreeFly)
{
	bFreeFly = bInFreeFly;
	if (!bFreeFly)
	{
		VerticalInput = 0.f;
		if (UPawnMovementComponent* Movement = GetMovementComponent())
		{
			Movement->StopMovementImmediately();
		}
	}
}

void AHitmenSpectatorPawn::TeleportToView(const FVector& Location, const FRotator& Rotation)
{
	SetActorLocationAndRotation(Location, Rotation, false, nullptr, ETeleportType::TeleportPhysics);
	if (UPawnMovementComponent* Movement = GetMovementComponent())
	{
		Movement->StopMovementImmediately();
	}
}

void AHitmenSpectatorPawn::UpdateFocus()
{
	APlayerController* PC = Cast<APlayerController>(GetController());
	if (!PC || !PC->IsLocalController())
	{
		return;
	}
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	// From the watcher's view (a followed player's eyes in player view).
	FVector Eye;
	FRotator View;
	PC->GetPlayerViewPoint(Eye, View);
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenWatcherFocus), false, this);
	if (AActor* ViewTarget = PC->GetViewTarget(); ViewTarget && ViewTarget != this)
	{
		Params.AddIgnoredActor(ViewTarget);
	}
	AActor* Target = nullptr;
	if (GetWorld()->LineTraceSingleByChannel(Hit, Eye, Eye + View.Vector() * Settings->WatcherFocusRange, HITMEN_TRACE_INTERACT, Params))
	{
		Target = Hit.GetActor();
	}
	FString Info;
	if (Target && Target->GetClass()->ImplementsInterface(UHitmenInteractable::StaticClass()))
	{
		Info = IHitmenInteractable::Execute_GetInteractionInfo(Target, this).ToString();
	}
	if (Info.IsEmpty())
	{
		Target = nullptr;
	}
	if (Target == FocusedActor.Get() && Info == FocusedInfo)
	{
		return;
	}
	if (FocusedActor.Get() != Target)
	{
		if (Settings->bWatchersHighlightInteractables)
		{
			UHitmenInteractionComponent::SetActorHighlight(FocusedActor.Get(), FocusOverlayMaterial, false);
			UHitmenInteractionComponent::SetActorHighlight(Target, FocusOverlayMaterial, true);
		}
		FocusedActor = Target;
	}
	FocusedInfo = Info;
	if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(PC))
	{
		// The info line alone (no key: watchers do not interact).
		UI->SetInteractionInfo(Settings->bWatchersSeeInteractableInfo ? FText::FromString(Info) : FText::GetEmpty());
		UI->BroadcastPrompt(FText::GetEmpty(), false);
	}
}

void AHitmenSpectatorPawn::ClearFocus()
{
	UHitmenInteractionComponent::SetActorHighlight(FocusedActor.Get(), FocusOverlayMaterial, false);
	FocusedActor.Reset();
	FocusedInfo.Reset();
	if (APlayerController* PC = Cast<APlayerController>(GetController()))
	{
		if (UHitmenUIEventSubsystem* UI = UHitmenUIEventSubsystem::Get(PC))
		{
			UI->SetInteractionInfo(FText::GetEmpty());
		}
	}
}
