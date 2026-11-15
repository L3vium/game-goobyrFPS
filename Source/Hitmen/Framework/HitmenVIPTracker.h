// Hitmen — what the local player knows about the dead VIP: the death (toast, sound, event camera), the body in yellow
// through walls (live while in sight, else a ghost at the newest known place: your last sighting or the last sonar
// ping), retrieval points in gold, and the end cinematic

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenVIPTracker.generated.h"

class AHitmenCharacterBase;
class AHitmenGameState;
class APostProcessVolume;
class UPoseableMeshComponent;
struct FHitmenSonarPing;

/** A still copy of a body's pose, drawn only as a marker (custom depth, PP_Markers), never in the main pass. */
UCLASS(NotBlueprintable)
class HITMEN_API AHitmenBodyGhost : public AActor
{
	GENERATED_BODY()

public:
	AHitmenBodyGhost();

	/** Copies the body's mesh, place and pose. */
	void CopyFrom(const AHitmenCharacterBase* Body);

	void SetShown(bool bShown);

protected:
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Components")
	TObjectPtr<UPoseableMeshComponent> Pose;
};

/** On the local player controller (every player, watchers too). */
UCLASS(ClassGroup=(Hitmen), meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenVIPTrackerComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenVIPTrackerComponent();

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	/** The end cinematic is over (the player became a watcher). */
	void EndCinematic();

protected:
	/** Seconds between line-of-sight checks on the body. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|VIP", meta=(ClampMin="0.02", Units="s"))
	float CheckInterval = 0.1f;

	/** The view blends to the extraction camera over this time. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|VIP", meta=(ClampMin="0", Units="s"))
	float CinematicBlendTime = 0.8f;

	UPROPERTY(Transient)
	TObjectPtr<AHitmenBodyGhost> Ghost;

	UPROPERTY(Transient)
	TObjectPtr<APostProcessVolume> MarkerVolume;

	TWeakObjectPtr<AHitmenGameState> BoundState;
	bool bDeathSeen = false;
	bool bBodyInSight = false;
	bool bCinematic = false;
	float GhostTime = -1.f;
	float LastCheck = 0.f;

	UFUNCTION() void HandleSonar(const FHitmenSonarPing& Ping);
	UFUNCTION() void HandleExtraction();

	void BindGameState();
	void EnsureMarkerVolume();
	void OnVIPDied(AHitmenCharacterBase* Body);
	void UpdateBody(AHitmenCharacterBase* Body);
	void UpdateRetrievalPoints(bool bShow) const;
	bool CanSeeBody(const AHitmenCharacterBase* Body) const;
	void SetBodyMarker(AHitmenCharacterBase* Body, bool bMarked) const;
	APlayerController* GetPC() const;
};
