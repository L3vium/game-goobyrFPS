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
	virtual void BeginPlay() override;

protected:
	/** How far the pane slides up when open. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Window", meta=(Units="cm"))
	float SlideDistance = 90.f;

	virtual void ApplyOpenAlpha(float Alpha) override;

	FVector ClosedLocation;
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

protected:
	/** Rooms served by this vent network. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Vent")
	TArray<TObjectPtr<AHitmenRoomVolume>> LinkedRooms;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Vent", meta=(Units="deg"))
	float OpenAngle = 100.f;

	virtual void ApplyOpenAlpha(float Alpha) override;

	FRotator ClosedRotation;
};
