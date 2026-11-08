// Hitmen — combat helpers

#include "Weapons/HitmenCombatLibrary.h"
#include "Core/HitmenInterfaces.h"
#include "AI/HitmenAISubsystem.h"
#include "Hitmen.h"
#include "Kismet/GameplayStatics.h"
#include "Engine/OverlapResult.h"
#include "Engine/World.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/Controller.h"
#include "Components/PrimitiveComponent.h"

void UHitmenCombatLibrary::Explode(const UObject* WorldContextObject, FVector Origin, float Radius, float Damage, AActor* DamageCauser, AController* InstigatorController, float NoiseRadius, float Impulse)
{
	UWorld* World = WorldContextObject ? WorldContextObject->GetWorld() : nullptr;
	if (!World || World->GetNetMode() == NM_Client)
	{
		return;
	}

	TArray<AActor*> Ignore;
	UGameplayStatics::ApplyRadialDamageWithFalloff(World, Damage, Damage * 0.2f, Origin, Radius * 0.3f, Radius, 1.f,
		UDamageType::StaticClass(), Ignore, DamageCauser, InstigatorController, ECC_Visibility);

	TArray<FOverlapResult> Overlaps;
	FCollisionObjectQueryParams ObjectParams(FCollisionObjectQueryParams::AllObjects);
	World->OverlapMultiByObjectType(Overlaps, Origin, FQuat::Identity, ObjectParams, FCollisionShape::MakeSphere(Radius));

	TSet<AActor*> Notified;
	AActor* InstigatorActor = InstigatorController ? InstigatorController->GetPawn() : DamageCauser;
	for (const FOverlapResult& Overlap : Overlaps)
	{
		AActor* Actor = Overlap.GetActor();
		if (Actor && !Notified.Contains(Actor) && Actor->GetClass()->ImplementsInterface(UHitmenExplosionReceiver::StaticClass()))
		{
			Notified.Add(Actor);
			IHitmenExplosionReceiver::Execute_ReceiveExplosion(Actor, Origin, Radius, InstigatorActor);
		}
		if (UPrimitiveComponent* Primitive = Overlap.GetComponent(); Primitive && Primitive->IsSimulatingPhysics())
		{
			Primitive->AddRadialImpulse(Origin, Radius, Impulse, ERadialImpulseFalloff::RIF_Linear);
		}
	}

	if (NoiseRadius > 0.f)
	{
		UHitmenAISubsystem::ReportNoise(World, Origin, NoiseRadius, InstigatorActor, EHitmenNoiseType::Explosion);
	}
}

void UHitmenCombatLibrary::GetAimPoint(const APawn* Pawn, FVector& OutLocation, FVector& OutDirection)
{
	OutLocation = FVector::ZeroVector;
	OutDirection = FVector::ForwardVector;
	if (!Pawn)
	{
		return;
	}
	FRotator Rotation;
	if (const AController* Controller = Pawn->GetController())
	{
		Controller->GetPlayerViewPoint(OutLocation, Rotation);
	}
	else
	{
		Pawn->GetActorEyesViewPoint(OutLocation, Rotation);
	}
	OutDirection = Rotation.Vector();
}

FVector UHitmenCombatLibrary::RandomConeDirection(const FVector& Direction, float HalfAngleDegrees, int32 Seed, int32 Index)
{
	FRandomStream Stream(Seed * 7919 + Index * 104729);
	return Stream.VRandCone(Direction, FMath::DegreesToRadians(FMath::Max(0.f, HalfAngleDegrees)));
}

void UHitmenCombatLibrary::ReportNoise(const UObject* WorldContextObject, FVector Location, float Radius, AActor* NoiseInstigator, EHitmenNoiseType Type)
{
	UHitmenAISubsystem::ReportNoise(WorldContextObject, Location, Radius, NoiseInstigator, Type);
}
