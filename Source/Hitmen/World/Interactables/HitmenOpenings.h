// Hitmen — doors (optional keycard, NPCs open them), windows (open = vaultable), vent hatches

#pragma once

#include "CoreMinimal.h"
#include "Interaction/HitmenInteractableBase.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenOpenings.generated.h"

class UBoxComponent;
class AHitmenRoomVolume;

/** Base for anything that opens and closes and seals a room when closed. */
UCLASS(Abstract)
class HITMEN_API AHitmenOpeningBase : public AHitmenInteractableBase, public IHitmenSealable
{
	GENERATED_BODY()

public:
	AHitmenOpeningBase();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void Tick(float DeltaSeconds) override;

	virtual bool IsSealed_Implementation() const override { return !bOpen; }
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Opening")
	void SetOpen(bool bNewOpen, const AActor* Opener = nullptr);

	UFUNCTION(BlueprintPure, Category="Hitmen|Opening")
	bool IsOpen() const { return bOpen; }

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Opening")
	FText OpenText;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Opening")
	FText CloseText;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Opening", meta=(ClampMin="0.1"))
	float MoveSpeed = 4.f;

	UPROPERTY(ReplicatedUsing=OnRep_Open, EditAnywhere, BlueprintReadOnly, Category="Hitmen|Opening")
	bool bOpen = false;

	/** -1 or 1: which way the door swings (chosen away from the opener). */
	UPROPERTY(Replicated)
	float OpenDirection = 1.f;

	/** 0 = closed, 1 = open. */
	float OpenAlpha = 0.f;

	virtual void OnInteracted(AActor* Interactor) override;
	virtual void ApplyOpenAlpha(float Alpha) {}

	UFUNCTION() void OnRep_Open();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnOpenChanged(bool bIsOpen);

	/** Remembers the closed pose of Mesh and its hinge: the edge at -X (doors) or +Z (hatches). */
	void CacheHinge(const FVector& EdgeDirection);
	/** Turns Mesh by Delta around the hinge (not around its own centre). */
	void RotateAroundHinge(const FRotator& Delta);

	FRotator HingeClosedRotation;
	FVector HingeClosedLocation = FVector::ZeroVector;
	/** Hinge -> mesh origin, in the mesh's closed local frame. */
	FVector HingeToCenter = FVector::ZeroVector;
};

UCLASS(Blueprintable)
class HITMEN_API AHitmenDoor : public AHitmenOpeningBase
{
	GENERATED_BODY()

public:
	AHitmenDoor();

	virtual void BeginPlay() override;
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;

protected:
	/** Keycard needed to open (-1 = none). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Door")
	int32 RequiredKeycard = INDEX_NONE;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Door", meta=(Units="deg"))
	float OpenAngle = 95.f;

	/** NPCs open doors automatically and close them behind after this delay. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Door", meta=(ClampMin="0"))
	float NPCAutoCloseDelay = 3.f;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> NPCTrigger;

	virtual void OnInteracted(AActor* Interactor) override;
	virtual void ApplyOpenAlpha(float Alpha) override;

	UFUNCTION()
	void HandleNPCOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	bool HasKeycard(const AActor* Interactor) const;

	FRotator ClosedRotation;
	FTimerHandle AutoCloseTimer;
};

/** Sliding pane: closed blocks vaulting, open lets you vault through. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenWindow : public AHitmenOpeningBase
{
	GENERATED_BODY()

public:
	AHitmenWindow();
	virtual void OnConstruction(const FTransform& Transform) override;

protected:
	/**
	 * A thin square frame (Mesh is the sill) with two shutters that swing open sideways, away from
	 * whoever opens it. Open = vaultable (Space while moving into it). Actor origin = floor.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(ClampMin="0", Units="cm"))
	float SillHeight = 100.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(ClampMin="40", Units="cm"))
	float Width = 120.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(ClampMin="40", Units="cm"))
	float Height = 120.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(ClampMin="1", Units="cm"))
	float FrameThickness = 6.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(ClampMin="1", Units="cm"))
	float FrameDepth = 12.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(ClampMin="0.5", Units="cm"))
	float ShutterThickness = 3.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(Units="deg"))
	float ShutterOpenAngle = 105.f;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> FrameTop;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> FrameLeft;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> FrameRight;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> LeftHinge;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> RightHinge;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> LeftShutter;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> RightShutter;

	virtual void ApplyOpenAlpha(float Alpha) override;
};

/** Small hatch into a vent duct. Accepts poison: sealed rooms on the network get gassed. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenVentHatch : public AHitmenOpeningBase, public IHitmenPoisonable
{
	GENERATED_BODY()

public:
	AHitmenVentHatch();
	virtual void BeginPlay() override;

	virtual bool CanBePoisoned_Implementation() const override { return LinkedRooms.Num() > 0; }
	virtual void ApplyPoison_Implementation(AActor* Poisoner) override;

	/** Rooms served by this vent network (poison only reaches the sealed ones). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Vent")
	const TArray<AHitmenRoomVolume*>& GetLinkedRooms() const { return ObjectPtrDecay(LinkedRooms); }

protected:
	/** Rooms served by this vent network. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Vent")
	TArray<TObjectPtr<AHitmenRoomVolume>> LinkedRooms;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Vent", meta=(Units="deg"))
	float OpenAngle = 100.f;

	virtual void ApplyOpenAlpha(float Alpha) override;

	FRotator ClosedRotation;
};
