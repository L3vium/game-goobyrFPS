// Hitmen — animation set

#include "Data/HitmenAnimSet.h"
#include "Animation/AnimMontage.h"

UAnimMontage* UHitmenAnimSet::GetMontage(EHitmenAnimMontage Montage, int32 Variant) const
{
	switch (Montage)
	{
	case EHitmenAnimMontage::Mantle:             return MantleMontage;
	case EHitmenAnimMontage::Vault:              return VaultMontage ? VaultMontage.Get() : MantleMontage.Get();
	case EHitmenAnimMontage::Throw:              return ThrowMontage;
	case EHitmenAnimMontage::HitReact:           return HitReactMontage;
	case EHitmenAnimMontage::Death:              return DeathMontages.IsValidIndex(Variant) ? DeathMontages[Variant].Get() : nullptr;
	case EHitmenAnimMontage::Interact:           return InteractMontage;
	case EHitmenAnimMontage::Punch:              return PunchMontage;
	case EHitmenAnimMontage::CallReinforcements: return CallReinforcementsMontage;
	case EHitmenAnimMontage::SearchLookAround:   return SearchLookAroundMontage;
	case EHitmenAnimMontage::ClimbEnter:         return ClimbEnterMontage;
	case EHitmenAnimMontage::ClimbExitTop:       return ClimbExitTopMontage;
	case EHitmenAnimMontage::LockerEnter:        return LockerEnterMontage;
	case EHitmenAnimMontage::LockerExit:         return LockerExitMontage;
	case EHitmenAnimMontage::ZiplineEnter:       return ZiplineEnterMontage;
	case EHitmenAnimMontage::Pickpocket:         return PickpocketMontage ? PickpocketMontage.Get() : InteractMontage.Get();
	case EHitmenAnimMontage::ParachuteLand:      return ParachuteLandMontage;
	default:                                     return nullptr;
	}
}
