// Hitmen — every placeholder sound of the game in one data asset (event -> sound)

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HitmenSoundSet.generated.h"

class USoundBase;

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
	TurretLockOn
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

	USoundBase* GetSound(EHitmenSound Sound) const;
	float GetVolume(EHitmenSound Sound) const;
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
};
