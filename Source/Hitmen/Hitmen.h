// Hitmen — module header and log category

#pragma once

#include "CoreMinimal.h"

HITMEN_API DECLARE_LOG_CATEGORY_EXTERN(LogHitmen, Log, All);

/** Custom collision channels (see DefaultEngine.ini). */
#define HITMEN_TRACE_WEAPON     ECC_GameTraceChannel2
#define HITMEN_TRACE_INTERACT   ECC_GameTraceChannel3
#define HITMEN_OBJECT_PROJECTILE ECC_GameTraceChannel1

/** Custom depth stencil values used by the Sightroom post-process. */
/** Actor tags read by gameplay code. */
namespace HitmenTags
{
	/** Never vaulted, climbed or hung from (barrels, hack panels, turrets, cameras, mounted guns...). */
	inline const FName NoTraversal(TEXT("HitmenNoTraversal"));
	/** Heightfield ground tiles (one material): their surface is read from the slope, like their colour. */
	inline const FName Terrain(TEXT("HitmenTerrain"));
}

namespace HitmenStencil
{
	constexpr int32 Enemy = 1;
	constexpr int32 Interactable = 2;
	constexpr int32 Target = 3;
	constexpr int32 TargetDead = 4;
	/** Always-on markers (PP_Markers, through walls): the VIP's body or its yellow ghost, retrieval points in gold. */
	constexpr int32 VIPMarker = 5;
	constexpr int32 RetrievalMarker = 6;
}
