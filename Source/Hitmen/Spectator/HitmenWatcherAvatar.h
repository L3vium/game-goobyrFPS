// Hitmen — a watcher (or a dead player flying around) as others see it. Local only (each machine spawns its own from
// the watcher's replicated view, AHitmenPlayerState::GetWatcherView). Other watchers see a small glowing orb with a
// lens toward where it looks; players (when Project Settings -> Hitmen -> Watchers -> bPlayersSeeWatchers allows it)
// see a ghost. With proximity chat on, the watcher's voice comes from here.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HitmenWatcherAvatar.generated.h"

class UStaticMeshComponent;
class USkeletalMeshComponent;
class UMaterialInstanceDynamic;
class UVOIPTalker;
class USoundAttenuation;
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

	/** bShown: drawn at all; bGhost: the ghost body (players' view) instead of the orb (watchers' view). */
	void SetPresentation(bool bShown, bool bGhost);

	/** Proximity voice: this watcher's voice comes from the avatar (heard around it, Attenuation) or not at all from
	 *  here (bProximity off: the voice is not positioned). */
	void SetVoice(bool bProximity, USoundAttenuation* Attenuation);

protected:
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Watcher")
	TObjectPtr<USceneComponent> Root;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Watcher")
	TObjectPtr<UStaticMeshComponent> Orb;

	UPROPERTY(VisibleAnywhere, Category="Hitmen|Watcher")
	TObjectPtr<UStaticMeshComponent> Lens;

	/** The ghost players see (Project Settings -> Hitmen -> Watchers -> WatcherGhost*). */
	UPROPERTY(VisibleAnywhere, Category="Hitmen|Watcher")
	TObjectPtr<USkeletalMeshComponent> Ghost;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> Material;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> GhostMaterial;

	UPROPERTY(Transient)
	TObjectPtr<UVOIPTalker> VoiceTalker;

	/** Glow colour (Project Settings -> Hitmen -> Visuals -> FlashMaterial, additive). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Watcher")
	FLinearColor Color = FLinearColor(0.35f, 0.85f, 1.f);

	/** How fast the avatar catches up with the reported view. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Watcher", meta=(ClampMin="0.1"))
	float InterpSpeed = 10.f;

	/** The ghost's eyes sit at the watcher's view: its feet are this far below. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Watcher", meta=(Units="cm"))
	float GhostEyeHeight = 165.f;

	void BuildGhost();

	TWeakObjectPtr<AHitmenPlayerState> Watcher;
	bool bPlaced = false;
	bool bGhostBuilt = false;
	bool bShowGhost = false;
	bool bShown = false;
	bool bVoiceProximity = false;
};
