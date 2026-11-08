// Hitmen — match rules

#include "Data/HitmenMatchRules.h"
#include "Curves/CurveFloat.h"

const FHitmenDifficultyPreset& UHitmenMatchRules::GetPreset(EHitmenDifficulty InDifficulty) const
{
	static const FHitmenDifficultyPreset Fallback;
	if (const FHitmenDifficultyPreset* Found = DifficultyPresets.Find(InDifficulty))
	{
		return *Found;
	}
	return Fallback;
}

float UHitmenMatchRules::GetSonarInterval(float CarryTime) const
{
	const float Alpha = FMath::Clamp(CarryTime / FMath::Max(SonarRampDuration, KINDA_SMALL_NUMBER), 0.f, 1.f);
	const float Shaped = SonarRampCurve ? FMath::Clamp(SonarRampCurve->GetFloatValue(Alpha), 0.f, 1.f) : Alpha;
	return FMath::Lerp(SonarStartInterval, SonarEndInterval, Shaped);
}
