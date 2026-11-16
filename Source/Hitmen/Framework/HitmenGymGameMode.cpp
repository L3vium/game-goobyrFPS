// Hitmen — debug gym game mode

#include "Framework/HitmenGymGameMode.h"

AHitmenGymGameMode::AHitmenGymGameMode()
{
	bWinConditionsEnabled = false;
	bActivateAllCandidates = true;
	RespawnDelay = 2.f;
	bParachuteIntro = false;
	// The gym always has its cheats, Shipping builds included.
	CheatAccess = EHitmenCheatAccess::Always;
}
