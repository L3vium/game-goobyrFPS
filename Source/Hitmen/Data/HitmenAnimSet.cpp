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
	default:                                     return nullptr;
	}
}
