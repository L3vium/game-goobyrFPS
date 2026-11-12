// Hitmen — traversal fixtures: ladders / gutters, ziplines, jump pads, teleporters

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenTraversal.generated.h"

class UBoxComponent;
class UCapsuleComponent;

/** Automatic ladder (also used for gutters): walk or jump into it. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenLadder : public AHitmenInteractableBase, public IHitmenClimbable
{
	GENERATED_BODY()

public:
	AHitmenLadder();
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return false; }

	// IHitmenClimbable
	virtual EHitmenClimbType GetClimbType() const override { return EHitmenClimbType::Ladder; }
	virtual FVector GetClimbStart() const override;
	virtual FVector GetClimbEnd() const override;
	virtual FVector GetClimbNormal() const override { return GetActorForwardVector(); }
	virtual float GetClimbOffset() const override { return StandOffDistance; }
	virtual float GetClimbSpeed() const override { return ClimbSpeed; }
	virtual float GetMinFacingDot() const override { return MinFacingDot; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> ClimbVolume;

	/** Height from the actor origin (floor) to the top ledge. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Ladder", meta=(ClampMin="50", Units="cm"))
	float Height = 400.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Ladder", meta=(ClampMin="0"))
	float ClimbSpeed = 320.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Ladder", meta=(ClampMin="0", Units="cm"))
	float StandOffDistance = 45.f;

	/** Permissive: you can look left/right a lot and still grab it. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Ladder", meta=(ClampMin="-1", ClampMax="1"))
	float MinFacingDot = 0.15f;
};

/** Which way a zipline can be ridden. */
UENUM(BlueprintType)
enum class EHitmenZiplineDirection : uint8
{
	/** From either end to the other. */
	Both,
	/** Only from the actor (start) to EndPoint. */
	StartToEnd,
	/** Only from EndPoint back to the actor. */
	EndToStart
};

/** Cable between the actor and EndPoint. F at an end rides it to the other end on its own. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenZipline : public AHitmenInteractableBase, public IHitmenClimbable
{
	GENERATED_BODY()

public:
	AHitmenZipline();
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual float GetClimbOffset() const override { return LandingClearance; }

	/** +1 = ride toward EndPoint, -1 = toward the start, 0 = not allowed from there. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Zipline")
	float GetRideDirectionFrom(const FVector& Location) const;

	virtual EHitmenClimbType GetClimbType() const override { return EHitmenClimbType::Zipline; }
	virtual FVector GetClimbStart() const override { return GetActorLocation(); }
	virtual FVector GetClimbEnd() const override { return GetActorTransform().TransformPosition(EndPoint); }
	virtual float GetClimbSpeed() const override { return Speed; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCapsuleComponent> GrabVolume;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Zipline", meta=(MakeEditWidget=true))
	FVector EndPoint = FVector(1500.f, 0.f, -300.f);

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Zipline", meta=(ClampMin="0"))
	float Speed = 1600.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Zipline")
	EHitmenZiplineDirection Direction = EHitmenZiplineDirection::Both;

	/** The rider lets go this far before the anchor (lands on the platform, not in the post). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Zipline", meta=(ClampMin="0", Units="cm"))
	float LandingClearance = 160.f;

	/** Anchor posts at both ends (what F is used on). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> StartPost;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> EndPost;

	/** Height of the posts below the cable anchors. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Zipline", meta=(ClampMin="0", Units="cm"))
	float PostHeight = 260.f;

	virtual void OnInteracted(AActor* Interactor) override;
};

/** Launches characters high. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenJumpPad : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenJumpPad();
	virtual void BeginPlay() override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return false; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> Trigger;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|JumpPad", meta=(ClampMin="0"))
	float LaunchSpeed = 1600.f;

	/** Forward push along the pad's forward vector. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|JumpPad", meta=(ClampMin="0"))
	float ForwardSpeed = 0.f;

	UFUNCTION()
	void HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnLaunch(AActor* Launched);
};

/** Fixed pair: entering one sends you out of the linked one. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenTeleporter : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenTeleporter();
	virtual void BeginPlay() override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return false; }

	/** Where arrivals appear (in front of the pad). */
	FVector GetExitLocation() const;
	void MarkArrival(AActor* Actor);

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> Trigger;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Teleporter")
	TObjectPtr<AHitmenTeleporter> Destination;

	/** Seconds an arrival is ignored so it does not bounce back. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Teleporter", meta=(ClampMin="0"))
	float ArrivalCooldown = 1.5f;

	UFUNCTION()
	void HandleOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Teleported(AActor* Actor);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnTeleported(AActor* Actor);

	TMap<TWeakObjectPtr<AActor>, float> RecentArrivals;
};
