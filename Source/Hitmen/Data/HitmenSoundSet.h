// Hitmen — every placeholder sound of the game in one data asset (event -> sound)

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenSoundSet.generated.h"

class USoundBase;
class USoundAttenuation;

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
	LandingHard
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

	/** Plays any sound at a location with the attenuation of its space (no attenuation = heard everywhere). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Sound", meta=(WorldContext="WorldContextObject"))
	static void PlaySoundInSpace(const UObject* WorldContextObject, USoundBase* Sound, FVector Location, EHitmenSoundSpace Space, float Volume = 1.f, float Pitch = 1.f);

	UFUNCTION(BlueprintPure, Category="Hitmen|Sound")
	static USoundAttenuation* GetSpaceAttenuation(EHitmenSoundSpace Space);
};
