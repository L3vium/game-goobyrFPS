// Hitmen — places you hide in (locker, body container) and where carried bodies are stowed

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "HitmenHidingSpot.generated.h"

class AHitmenCharacterBase;

/**
 * A hiding spot opens (door, lid), lets someone step in facing out and closes: guards cannot see whoever hides inside;
 * the view turns within ViewArcDegrees around the way out. Carrying a body, F stows it in the next free slot instead
 * (up to BodyCapacity, never the VIP); stowed bodies stay visible inside (open it and you see them) and guards never
 * find them. With bBlockHidingWhenBodyInside, a spot holding a body has no room for you.
 *
 * Subclasses build the shape and animate their opening (ApplyOpenAlpha); everything else is here.
 */
UCLASS(Abstract, Blueprintable)
class HITMEN_API AHitmenHidingSpot : public AHitmenInteractableBase
{
	GENERATED_BODY()

public:
	AHitmenHidingSpot();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void Tick(float DeltaSeconds) override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionInfo_Implementation(AActor* Interactor) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|HidingSpot")
	int32 GetBodyCount() const { return Bodies.Num(); }

	UFUNCTION(BlueprintPure, Category="Hitmen|HidingSpot")
	int32 GetBodyCapacity() const { return BodyCapacity; }

	UFUNCTION(BlueprintPure, Category="Hitmen|HidingSpot")
	bool HasRoomForBody() const { return Bodies.Num() < BodyCapacity; }

	UFUNCTION(BlueprintPure, Category="Hitmen|HidingSpot")
	bool IsOccupied() const { return Occupant != nullptr; }

	/** A body that can go in here now (not the VIP, room left). */
	bool CanStowBody(const AHitmenCharacterBase* Body) const;

	/** Someone could get in now (empty, hiding allowed, no body in the way). */
	bool CanHide(const AActor* Who) const;

	/** Server: stows the body Carrier holds (a player or a guard). False when it cannot. */
	bool StowCarriedBody(AActor* Carrier);

	/** Where stowed body Index lies, relative to the spot. */
	FTransform GetBodySlotTransform(int32 Index) const;

protected:
	// ---- Hiding ----
	/** Off: only for bodies (no room for a person). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot")
	bool bCanHideInside = true;

	/** Whole view arc while hidden, around the way out (180 = left to right; 360 = free to turn around). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(ClampMin="10", ClampMax="360", Units="deg"))
	float ViewArcDegrees = 180.f;

	/** Up / down limits of the view while hidden. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(ClampMin="-89", ClampMax="0", Units="deg"))
	float ViewPitchMin = -40.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(ClampMin="0", ClampMax="89", Units="deg"))
	float ViewPitchMax = 35.f;

	/** Inside point (capsule centre), relative to the spot: its height sets the eye height inside. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(MakeEditWidget=true))
	FVector InsideOffset = FVector(-4.f, 0.f, 96.f);

	/** Exit point, relative to the spot. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(MakeEditWidget=true))
	FVector ExitOffset = FVector(95.f, 0.f, 96.f);

	/** The door / lid opens and closes again over this time when someone gets in or out or a body goes in. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(ClampMin="0.1", Units="s"))
	float OpenCycleTime = 0.8f;

	/** The occupant steps in (or out) over this time while it is open, turning to face out. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(ClampMin="0.05", Units="s"))
	float StepTime = 0.5f;

	/** Delay after it starts opening before the occupant moves. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot", meta=(ClampMin="0", Units="s"))
	float StepDelay = 0.12f;

	// ---- Bodies ----
	/** Bodies it holds (0 = none). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot|Bodies", meta=(ClampMin="0"))
	int32 BodyCapacity = 1;

	/** A spot holding at least one body has no room left to hide in. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot|Bodies")
	bool bBlockHidingWhenBodyInside = true;

	/** Where stowed bodies lie, relative to the spot (body N takes slot N; extra bodies stack on the last one). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HidingSpot|Bodies", meta=(MakeEditWidget=true))
	TArray<FTransform> BodySlots;

	UPROPERTY(ReplicatedUsing=OnRep_Occupant, BlueprintReadOnly, Category="Hitmen|HidingSpot")
	TObjectPtr<AHitmenCharacterBase> Occupant;

	UPROPERTY(Replicated, BlueprintReadOnly, Category="Hitmen|HidingSpot")
	TArray<TObjectPtr<AHitmenCharacterBase>> Bodies;

	/** Subclasses: the door / lid at Alpha 0 (closed) .. 1 (open), on every machine. */
	virtual void ApplyOpenAlpha(float Alpha) {}

	virtual void OnInteracted(AActor* Interactor) override;

	UFUNCTION()
	void OnRep_Occupant();

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_OpenCycle();

	/** Every machine: collision, movement and view for whoever gets in / out. */
	void ApplyOccupant(AHitmenCharacterBase* Character, bool bInside);
	void StartOpenCycle();
	void UpdateStep();

	/** The occupant's local view: limited to the arc while inside, free again outside. */
	void SetViewLimits(AHitmenCharacterBase* Character, bool bInside) const;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnOccupantChanged(bool bOccupied);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnBodyStowed(int32 Count);

	TWeakObjectPtr<AHitmenCharacterBase> AppliedOccupant;
	float OpenCycleStart = -100.f;

	/** The character stepping in or out (every machine moves it the same way). */
	TWeakObjectPtr<AHitmenCharacterBase> SteppingCharacter;
	bool bSteppingIn = false;
	FVector StepFrom = FVector::ZeroVector;
	FVector StepTo = FVector::ZeroVector;
	FRotator StepFromRotation = FRotator::ZeroRotator;
	FRotator StepToRotation = FRotator::ZeroRotator;
};
