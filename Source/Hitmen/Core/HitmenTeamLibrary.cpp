// Hitmen — hostility rules

#include "Core/HitmenTeamLibrary.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/Controller.h"

int32 UHitmenTeamLibrary::GetTeamId(const AActor* Actor)
{
	// Walk owner/instigator chain: projectile -> weapon -> character.
	const AActor* Current = Actor;
	for (int32 Depth = 0; Current && Depth < 4; ++Depth)
	{
		if (Current->GetClass()->ImplementsInterface(UHitmenTeamMember::StaticClass()))
		{
			return IHitmenTeamMember::Execute_GetHitmenTeamId(Current);
		}
		if (const AController* Controller = Cast<AController>(Current))
		{
			Current = Controller->GetPawn();
			continue;
		}
		const AActor* Next = Current->GetInstigator();
		Current = (Next && Next != Current) ? Next : Current->GetOwner();
	}
	return HitmenTeams::Neutral;
}

bool UHitmenTeamLibrary::AreTeamIdsHostile(int32 A, int32 B)
{
	if (A == HitmenTeams::Neutral || B == HitmenTeams::Neutral)
	{
		return false;
	}
	return A != B;
}

bool UHitmenTeamLibrary::AreHostile(const AActor* A, const AActor* B)
{
	if (!A || !B || A == B)
	{
		return false;
	}
	return AreTeamIdsHostile(GetTeamId(A), GetTeamId(B));
}
