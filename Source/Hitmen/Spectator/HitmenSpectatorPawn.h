// Hitmen — free-fly spectator pawn for watchers (and dead players): through walls and terrain inside the map's box
// (Project Settings -> Hitmen -> Watchers), wheel speed, Tab scan, the info line of what it looks at.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/SpectatorPawn.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenSpectatorPawn.generated.h"

class UCameraComponent;
class UHitmenSightroomComponent;
class UMaterialInterface;

UCLASS()
class HITMEN_API AHitmenSpectatorPawn : public ASpectatorPawn, public IHitmenInputReceiver
{
	GENERATED_BODY()

public:
	AHitmenSpectatorPawn();

	virtual void Tick(float DeltaSeconds) override;

	// IHitmenInputReceiver
	virtual void Input_Move(const FVector2D& Axis) override;
	virtual void Input_Look(const FVector2D& Axis) override;
	virtual void Input_JumpPressed() override { SetVerticalInput(1.f); }
	virtual void Input_JumpReleased() override { SetVerticalInput(0.f); }
	virtual void Input_CrouchChanged(bool bWantsCrouch) override { SetVerticalInput(bWantsCrouch ? -1.f : 0.f); }
	virtual void Input_SprintChanged(bool bWantsSprint) override;
	virtual void Input_SightroomChanged(bool bActive) override;

	/** -1 down .. +1 up (free-fly up / down actions). */
	void SetVerticalInput(float InVertical) { VerticalInput = bFreeFly ? FMath::Clamp(InVertical, -1.f, 1.f) : 0.f; }
	void SetFast(bool bInFast) { Input_SprintChanged(bInFast); }

	/** Mouse wheel: fly speed x SpeedStep per notch, between SpeedScaleMin and SpeedScaleMax. Returns the new scale. */
	float AdjustSpeed(float Notches);
	float GetSpeedScale() const { return SpeedScale; }

	/** Free fly takes the move keys; in player view / split screen the controller places the pawn. */
	void SetFreeFly(bool bInFreeFly);

	/** Jumps to a view (third-person player view, entering free fly where the camera was). */
	void TeleportToView(const FVector& Location, const FRotator& Rotation);

protected:
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	/** The view (and where the Sightroom post process is blended). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCameraComponent> Camera;

	/** Tab scan for watchers (Project Settings -> Hitmen -> Watchers -> bWatchersCanScan); PostProcessMaterial in BP. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenSightroomComponent> Sightroom;

	/** Overlay on the interactable a watcher looks at (the player's focus overlay). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator")
	TObjectPtr<UMaterialInterface> FocusOverlayMaterial;

	/** Free fly stays inside the map's world bounds (DA_Map), shrunk by this margin. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0", Units="cm"))
	float BoundsMargin = 500.f;

	/** Without terrain pass-through: never lower than this above the surface under the camera. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0", Units="cm"))
	float MinHeightAboveGround = 60.f;

	/** Ceiling: this high above the highest ground seen under the camera (and the start height). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0", Units="cm"))
	float MaxHeightAboveGround = 4000.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0"))
	float FlySpeed = 1200.f;

	/** Shift: this much faster. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="1"))
	float FastMultiplier = 2.f;

	/** Mouse wheel speed range and step per notch. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0.05"))
	float SpeedScaleMin = 0.25f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="1"))
	float SpeedScaleMax = 8.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="1.01"))
	float SpeedStep = 1.25f;

	/** Seconds between looks at what is in front (info line, glow). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0.02", Units="s"))
	float FocusInterval = 0.1f;

	void ClampToBounds();
	/** Top of the terrain (actors tagged HitmenTerrain, landscapes) under XY, else the lowest surface there. */
	float FindGroundBelow(const FVector& Location) const;
	void ApplySpeed();
	void UpdateFocus();
	void ClearFocus();

	FBox2D WorldBounds = FBox2D(ForceInit);
	float CeilingZ = 0.f;
	float VerticalInput = 0.f;
	float SpeedScale = 1.f;
	bool bFast = false;
	bool bFreeFly = true;
	FTimerHandle FocusTimer;
	TWeakObjectPtr<AActor> FocusedActor;
	FString FocusedInfo;
};
