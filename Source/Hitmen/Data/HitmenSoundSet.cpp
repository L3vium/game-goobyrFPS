// Hitmen — sound set

#include "Data/HitmenSoundSet.h"
#include "Core/HitmenGameSettings.h"
#include "Kismet/GameplayStatics.h"
#include "Sound/SoundBase.h"
#include "Engine/World.h"

USoundBase* UHitmenSoundSet::GetSound(EHitmenSound Sound) const
{
	const TSoftObjectPtr<USoundBase>* Found = Sounds.Find(Sound);
	return Found ? Found->LoadSynchronous() : nullptr;
}

float UHitmenSoundSet::GetVolume(EHitmenSound Sound) const
{
	const float* Found = Volumes.Find(Sound);
	return Found ? *Found : 1.f;
}

USoundBase* UHitmenSoundLibrary::GetHitmenSound(EHitmenSound Sound)
{
	const UHitmenSoundSet* Set = UHitmenGameSettings::Get()->SoundSet.LoadSynchronous();
	return Set ? Set->GetSound(Sound) : nullptr;
}

void UHitmenSoundLibrary::PlayHitmenSound2D(const UObject* WorldContextObject, EHitmenSound Sound)
{
	const UWorld* World = WorldContextObject ? WorldContextObject->GetWorld() : nullptr;
	const UHitmenSoundSet* Set = UHitmenGameSettings::Get()->SoundSet.LoadSynchronous();
	if (!World || World->GetNetMode() == NM_DedicatedServer || !Set)
	{
		return;
	}
	if (USoundBase* Asset = Set->GetSound(Sound))
	{
		UGameplayStatics::PlaySound2D(WorldContextObject, Asset, Set->GetVolume(Sound));
	}
}

void UHitmenSoundLibrary::PlayHitmenSoundAt(const UObject* WorldContextObject, EHitmenSound Sound, FVector Location)
{
	const UWorld* World = WorldContextObject ? WorldContextObject->GetWorld() : nullptr;
	const UHitmenSoundSet* Set = UHitmenGameSettings::Get()->SoundSet.LoadSynchronous();
	if (!World || World->GetNetMode() == NM_DedicatedServer || !Set)
	{
		return;
	}
	if (USoundBase* Asset = Set->GetSound(Sound))
	{
		UGameplayStatics::PlaySoundAtLocation(WorldContextObject, Asset, Location, Set->GetVolume(Sound));
	}
}
