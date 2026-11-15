// Hitmen — a watcher as others see it: a small glowing orb with a lens toward where it looks. Local only (each
// machine spawns its own from the watcher's replicated view, AHitmenPlayerState::GetWatcherView).

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenWatcherAvatar.generated.h"

class UStaticMeshComponent;
class UMaterialInstanceDynamic;
class AHitmenPlayerState;

UCLASS(NotBlueprintable)
class HITMEN_API AHitmenWatcherAvatar : public AActor
{
	GENERATED_BODY()

public:
	AHitmenWatcherAvatar();

	virtual void Tick(float DeltaSeconds) override;

	/** The watcher shown; the avatar glides to its reported view (10 Hz) and writes the smoothed position back for the
	 *  name tags. */
	void SetWatcher(AHitmenPlayerState* InWatcher);
	AHitmenPlayerState* GetWatcher() const { return Watcher.Get(); }

protected:
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Watcher")
	TObjectPtr<UStaticMeshComponent> Orb;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Watcher")
	TObjectPtr<UStaticMeshComponent> Lens;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> Material;

	/** Glow colour (Project Settings -> Hitmen -> Visuals -> FlashMaterial, additive). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Watcher")
	FLinearColor Color = FLinearColor(0.35f, 0.85f, 1.f);

	/** How fast the avatar catches up with the reported view. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Watcher", meta=(ClampMin="0.1"))
	float InterpSpeed = 10.f;

	TWeakObjectPtr<AHitmenPlayerState> Watcher;
	bool bPlaced = false;
};
