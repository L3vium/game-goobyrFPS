// Hitmen — project settings

#include "Core/HitmenGameSettings.h"
#include "Data/HitmenMatchRules.h"
#include "Data/HitmenCatalog.h"
#include "Data/HitmenMapDefinition.h"

UHitmenGameSettings::UHitmenGameSettings()
{
	SectionName = TEXT("Hitmen");
}

UHitmenMatchRules* UHitmenGameSettings::LoadDefaultRules() const
{
	return DefaultRules.LoadSynchronous();
}

UHitmenCatalog* UHitmenGameSettings::LoadCatalog() const
{
	return Catalog.LoadSynchronous();
}

UHitmenMapDefinition* UHitmenGameSettings::LoadMap(int32 Index) const
{
	return Maps.IsValidIndex(Index) ? Maps[Index].LoadSynchronous() : nullptr;
}
