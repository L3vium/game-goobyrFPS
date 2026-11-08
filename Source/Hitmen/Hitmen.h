// Hitmen — module header and log category

#pragma once

#include "CoreMinimal.h"

HITMEN_API DECLARE_LOG_CATEGORY_EXTERN(LogHitmen, Log, All);

/** Custom collision channels (see DefaultEngine.ini). */
#define HITMEN_TRACE_WEAPON     ECC_GameTraceChannel2
#define HITMEN_TRACE_INTERACT   ECC_GameTraceChannel3
#define HITMEN_OBJECT_PROJECTILE ECC_GameTraceChannel1

/** Custom depth stencil values used by the Sightroom post-process. */
namespace HitmenStencil
{
	constexpr int32 Enemy = 1;
	constexpr int32 Interactable = 2;
	constexpr int32 Target = 3;
	constexpr int32 TargetDead = 4;
}
