// Hitmen — a container (dumpster, chest, freezer) to hide carried bodies in, and to hide in yourself

#pragma once

#include "CoreMinimal.h"
#include "World/Interactables/HitmenHidingSpot.h"
#include "HitmenBodyContainer.generated.h"

/**
 * A hollow bin with a lid hinged on its back edge. Carrying a body, F: the lid opens, the body goes in (it stays there,
 * visible when the lid opens; guards never find it) and the lid closes; up to BodyCapacity bodies, never the VIP. Empty
 * handed, F: you climb in and crouch, looking out through a slit under the rim (the view turns within the arc).
 * Looking at it always shows its name and how many bodies it holds.
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenBodyContainer : public AHitmenHidingSpot
{
	GENERATED_BODY()

public:
	AHitmenBodyContainer();
	virtual void OnConstruction(const FTransform& Transform) override;

	UFUNCTION(BlueprintPure, Category="Hitmen|BodyContainer")
	int32 GetHiddenCount() const { return GetBodyCount(); }

	UFUNCTION(BlueprintPure, Category="Hitmen|BodyContainer")
	bool IsFull() const { return !HasRoomForBody(); }

protected:
	/** Outer size (cm): Width along Y (bodies lie along it), Depth along X (front at +X), Height of the walls. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(ClampMin="60", Units="cm"))
	float Width = 210.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(ClampMin="60", Units="cm"))
	float Depth = 110.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(ClampMin="60", Units="cm"))
	float Height = 118.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(ClampMin="1", Units="cm"))
	float WallThickness = 5.f;

	/** Centre height of the peek slit in the front wall (the crouched eye inside) and its opening. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(Units="cm"))
	float SlitHeight = 88.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(ClampMin="1", Units="cm"))
	float SlitGap = 8.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|BodyContainer", meta=(Units="deg"))
	float LidOpenAngle = 105.f;

	/** The lid (hinged on its back edge); the team swaps the mesh in the Blueprint. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> LidPivot;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Lid;

	/** Walls and floor (Mesh is the back wall). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> WallLeft;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> WallRight;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> WallFrontLower;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> WallFrontUpper;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Floor;

	virtual void ApplyOpenAlpha(float Alpha) override;
};
