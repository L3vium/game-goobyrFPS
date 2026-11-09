// Hitmen — first-person arms anim instance

#include "Characters/HitmenFirstPersonAnimInstance.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenAnimSet.h"
#include "Animation/AnimSequenceBase.h"

void UHitmenFirstPersonAnimInstance::NativeUpdateAnimation(float DeltaSeconds)
{
	Super::NativeUpdateAnimation(DeltaSeconds);

	UAnimSequenceBase* Pose = nullptr;
	if (const AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(TryGetPawnOwner()))
	{
		if (const UHitmenWeaponData* Data = Player->GetInventory() ? Player->GetInventory()->GetActiveWeaponData() : nullptr)
		{
			Pose = Data->FirstPersonPose.LoadSynchronous();
		}
		if (!Pose && Player->GetAnimSet())
		{
			Pose = Player->GetAnimSet()->FirstPersonUnarmedPose;
		}
	}
	HoldPose = Pose ? Pose : DefaultHoldPose.Get();
}
