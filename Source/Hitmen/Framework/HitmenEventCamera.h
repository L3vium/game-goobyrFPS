// Hitmen — local picture-in-picture camera: films something for a few seconds (the VIP's body, a guard coming to look
// at your distraction), shown top left by the match events widget

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenEventCamera.generated.h"

class USceneCaptureComponent2D;
class UTextureRenderTarget2D;

UCLASS(NotBlueprintable)
class HITMEN_API AHitmenEventCamera : public AActor
{
	GENERATED_BODY()

public:
	AHitmenEventCamera();

	virtual void Tick(float DeltaSeconds) override;

	/** Films Subject for Duration seconds and publishes the picture to the local player's UI. */
	void Show(AActor* Subject, const FText& Title, float Duration, APlayerController* Viewer);

	/** Shows what View sees (a security camera's feed) with its field of view, until Stop or Duration (0 = until Stop). */
	void ShowFrom(USceneComponent* View, float FOV, const FText& Title, float Duration, APlayerController* Viewer);

	/** Stops now (end cinematic, level change). */
	void Stop();

	/** Filming a fixed view right now (ShowFrom). */
	bool IsShowingView() const { return FixedView.IsValid() && EndTime != -1.f; }

protected:
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<USceneCaptureComponent2D> Capture;

	UPROPERTY(Transient)
	TObjectPtr<UTextureRenderTarget2D> Target;

	TWeakObjectPtr<AActor> Subject;
	TWeakObjectPtr<USceneComponent> FixedView;
	TWeakObjectPtr<APlayerController> Viewer;
	float EndTime = -1.f;
	float OrbitYaw = 0.f;

	/** Where the camera sits around the subject: never behind a wall. */
	void Frame(float DeltaSeconds);
};
