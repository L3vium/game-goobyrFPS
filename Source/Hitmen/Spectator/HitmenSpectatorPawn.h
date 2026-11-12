// Hitmen — free-fly spectator pawn for watchers

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/SpectatorPawn.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenSpectatorPawn.generated.h"

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
	virtual void Input_JumpPressed() override { VerticalInput = 1.f; }
	virtual void Input_JumpReleased() override { VerticalInput = 0.f; }
	virtual void Input_CrouchChanged(bool bWantsCrouch) override { VerticalInput = bWantsCrouch ? -1.f : 0.f; }
	virtual void Input_SprintChanged(bool bWantsSprint) override;

	/** -1 down .. +1 up (free-fly up / down actions). */
	void SetVerticalInput(float InVertical) { VerticalInput = FMath::Clamp(InVertical, -1.f, 1.f); }
	void SetFast(bool bFast) { Input_SprintChanged(bFast); }

protected:
	virtual void BeginPlay() override;

	/** Free fly stays inside the map's world bounds (DA_Map), shrunk by this margin. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0", Units="cm"))
	float BoundsMargin = 500.f;

	/** Never lower than this above the ground under the camera. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0", Units="cm"))
	float MinHeightAboveGround = 60.f;

	/** Ceiling: this high above the highest ground seen under the camera (and the start height). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0", Units="cm"))
	float MaxHeightAboveGround = 4000.f;

	void ClampToBounds();
	FBox2D WorldBounds = FBox2D(ForceInit);
	float CeilingZ = 0.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0"))
	float FlySpeed = 1200.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="1"))
	float FastMultiplier = 3.f;

	float VerticalInput = 0.f;
};
