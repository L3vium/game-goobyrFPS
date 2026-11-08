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

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="0"))
	float FlySpeed = 1200.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Spectator", meta=(ClampMin="1"))
	float FastMultiplier = 3.f;

	float VerticalInput = 0.f;
};
