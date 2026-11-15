// Hitmen — every placeholder sound of the game in one data asset (event -> sound)

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "Chaos/ChaosEngineInterface.h"
#include "HitmenSoundSet.generated.h"

class USoundBase;
class USoundAttenuation;
class UNiagaraSystem;

/** What a physical surface sounds like under the feet (steps, landings) and how far NPCs hear it. */
USTRUCT(BlueprintType)
struct FHitmenSurfaceFeedback
{
	GENERATED_BODY()

	/** Step variations: one is picked at random each step (never the same twice in a row). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	TArray<TObjectPtr<USoundBase>> Sounds;

	/** Single step sound, used when Sounds is empty. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	TObjectPtr<USoundBase> Sound = nullptr;

	/** Volume and pitch of this surface's steps (a new surface can reuse another's takes, pitched). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep", meta=(ClampMin="0"))
	float Volume = 1.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep", meta=(ClampMin="0.1"))
	float Pitch = 1.f;

	/** Puff under the foot on each step and landing; only when Project Settings -> Hitmen -> Juice -> bFootstepFX. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	TObjectPtr<UNiagaraSystem> Effect = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep", meta=(ClampMin="0"))
	float EffectScale = 1.f;

	/** Multiplies the noise radius heard by NPCs (sand quieter, metal louder). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep", meta=(ClampMin="0"))
	float NoiseMultiplier = 1.f;

	bool HasSound() const { return Sounds.Num() > 0 || Sound != nullptr; }
};

/** One row of the surface table: a project physical surface (Project Settings -> Physics) and its feedback. */
USTRUCT(BlueprintType)
struct FHitmenSurfaceEntry
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	TEnumAsByte<EPhysicalSurface> Surface = SurfaceType_Default;

	/** For designers only (the table row label). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	FName Name;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Footstep")
	FHitmenSurfaceFeedback Feedback;
};

/** How a sound carries in the world: each space has its own attenuation (falloff, occlusion). */
UENUM(BlueprintType)
enum class EHitmenSoundSpace : uint8
{
	/** Fixtures, pickups, voices: a room or two. */
	World,
	/** Steps: a few metres. */
	Footstep,
	/** Gunshots, explosions, cannons: far, muffled with distance. */
	Gunshot,
	/** Bullet and projectile impacts. */
	Impact
};

/** Gameplay / UI moments that make a sound. The team swaps the sounds in DA_SoundSet. */
UENUM(BlueprintType)
enum class EHitmenSound : uint8
{
	UIHover,
	UIClick,
	UIBack,
	UIConfirm,
	Countdown,
	MatchStart,
	Heartbeat,
	PunchWhoosh,
	DoorOpen,
	DoorClose,
	WindowOpen,
	ContainerOpen,
	LockerEnter,
	LockerExit,
	ZiplineRide,
	ClimbGrab,
	KeycardPickup,
	WeaponPickup,
	ItemPickup,
	Explosion,
	Spotted,
	CameraAlarm,
	BreakerSwitch,
	Hack,
	Pickpocket,
	SightroomOn,
	SightroomOff,
	Landing,
	Parachute,
	Death,
	TurretLockOn,
	HeadshotHit,
	HackLoop,
	HackComplete,
	RadioLoop,
	MountedGunFire,
	TankFire,
	LandingHard,
	/** Loop while sliding (stopped when the slide ends). */
	Slide,
	/** The VIP died (everyone, 2D). */
	TargetEliminated
};

UCLASS(BlueprintType)
class HITMEN_API UHitmenSoundSet : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sound")
	TMap<EHitmenSound, TSoftObjectPtr<USoundBase>> Sounds;

	/** Per-event volume (1 when missing). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sound")
	TMap<EHitmenSound, float> Volumes;

	/** Attenuation per sound space; every 3D sound of the game goes through one of these. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sound|Attenuation")
	TMap<EHitmenSoundSpace, TObjectPtr<USoundAttenuation>> Attenuations;

	/** Space of each sound-set entry played in the world (World when missing). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Sound|Attenuation")
	TMap<EHitmenSound, EHitmenSoundSpace> Spaces;

	/**
	 * Footsteps and landings per physical surface. Adding a surface: a SurfaceType in Project Settings -> Physics, a
	 * PM_ physical material on the materials that look like it, and one row here (docs/Pipelines.md).
	 */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Surfaces")
	TArray<FHitmenSurfaceEntry> Surfaces;

	/** Surfaces without a row (and materials without a physical material): concrete. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Surfaces")
	FHitmenSurfaceFeedback DefaultSurface;

	/** The row of a surface, or DefaultSurface. */
	const FHitmenSurfaceFeedback& GetSurface(EPhysicalSurface Surface) const;

	/** The project sound set (Project Settings -> Hitmen -> Data). */
	static const UHitmenSoundSet* GetProjectSet();

	USoundBase* GetSound(EHitmenSound Sound) const;
	float GetVolume(EHitmenSound Sound) const;
	USoundAttenuation* GetAttenuation(EHitmenSoundSpace Space) const;
	EHitmenSoundSpace GetSpace(EHitmenSound Sound) const;
};

/** One-liners for call sites: play a sound-set entry in 2D (UI) or at a location. */
UCLASS()
class HITMEN_API UHitmenSoundLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Sound", meta=(WorldContext="WorldContextObject"))
	static void PlayHitmenSound2D(const UObject* WorldContextObject, EHitmenSound Sound);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Sound", meta=(WorldContext="WorldContextObject"))
	static void PlayHitmenSoundAt(const UObject* WorldContextObject, EHitmenSound Sound, FVector Location);

	UFUNCTION(BlueprintPure, Category="Hitmen|Sound")
	static USoundBase* GetHitmenSound(EHitmenSound Sound);

	/** The sound-set volume of an entry (1 when missing). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Sound")
	static float GetHitmenSoundVolume(EHitmenSound Sound);

	/** Plays any sound at a location with the attenuation of its space (no attenuation = heard everywhere). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Sound", meta=(WorldContext="WorldContextObject"))
	static void PlaySoundInSpace(const UObject* WorldContextObject, USoundBase* Sound, FVector Location, EHitmenSoundSpace Space, float Volume = 1.f, float Pitch = 1.f);

	UFUNCTION(BlueprintPure, Category="Hitmen|Sound")
	static USoundAttenuation* GetSpaceAttenuation(EHitmenSoundSpace Space);
};
