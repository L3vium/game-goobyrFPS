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

float UHitmenMatchRules::GetSonarInterval(float CarryTime, const FHitmenLobbySettings& Settings) const
{
	const float Alpha = FMath::Clamp(CarryTime / FMath::Max(Settings.SonarRampDuration, KINDA_SMALL_NUMBER), 0.f, 1.f);
	const float Shaped = SonarRampCurve ? FMath::Clamp(SonarRampCurve->GetFloatValue(Alpha), 0.f, 1.f) : Alpha;
	return FMath::Lerp(Settings.SonarStartInterval, Settings.SonarEndInterval, Shaped);
}

FHitmenLobbySettings UHitmenMatchRules::MakeDefaultLobbySettings() const
{
	FHitmenLobbySettings Settings;
	Settings.bProximityChat = ProximityChat.Value;
	Settings.RetrievalPointCount = RetrievalPointCount.Value;
	Settings.Difficulty = static_cast<EHitmenDifficulty>(FMath::Clamp(Difficulty.Value, 0, static_cast<int32>(EHitmenDifficulty::Hard)));
	Settings.SonarStartInterval = SonarStartInterval.Value;
	Settings.SonarEndInterval = SonarEndInterval.Value;
	Settings.SonarRampDuration = SonarRampDuration.Value;
	return Settings;
}

FHitmenLobbySettings UHitmenMatchRules::SanitizeLobbySettings(const FHitmenLobbySettings& Requested) const
{
	FHitmenLobbySettings Out = Requested;
	const FHitmenLobbySettings Defaults = MakeDefaultLobbySettings();
	Out.RetrievalPointCount = FMath::Clamp(Out.RetrievalPointCount, RetrievalPointCount.Min, RetrievalPointCount.Max);
	Out.Difficulty = static_cast<EHitmenDifficulty>(FMath::Clamp(static_cast<int32>(Out.Difficulty), Difficulty.Min, Difficulty.Max));
	Out.SonarStartInterval = FMath::Clamp(Out.SonarStartInterval, SonarStartInterval.Min, SonarStartInterval.Max);
	Out.SonarEndInterval = FMath::Clamp(Out.SonarEndInterval, SonarEndInterval.Min, SonarEndInterval.Max);
	Out.SonarRampDuration = FMath::Clamp(Out.SonarRampDuration, SonarRampDuration.Min, SonarRampDuration.Max);
	if (!ProximityChat.bShowInLobbyUI)       { Out.bProximityChat = Defaults.bProximityChat; }
	if (!RetrievalPointCount.bShowInLobbyUI) { Out.RetrievalPointCount = Defaults.RetrievalPointCount; }
	if (!Difficulty.bShowInLobbyUI)          { Out.Difficulty = Defaults.Difficulty; }
	if (!SonarStartInterval.bShowInLobbyUI)  { Out.SonarStartInterval = Defaults.SonarStartInterval; }
	if (!SonarEndInterval.bShowInLobbyUI)    { Out.SonarEndInterval = Defaults.SonarEndInterval; }
	if (!SonarRampDuration.bShowInLobbyUI)   { Out.SonarRampDuration = Defaults.SonarRampDuration; }
	return Out;
}
