// Hitmen — game target

using UnrealBuildTool;
using System.Collections.Generic;

public class HitmenTarget : TargetRules
{
	public HitmenTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.V6;
		IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_7;
		ExtraModuleNames.Add("Hitmen");
	}
}
