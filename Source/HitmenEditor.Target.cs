// Hitmen — editor target

using UnrealBuildTool;
using System.Collections.Generic;

public class HitmenEditorTarget : TargetRules
{
	public HitmenEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.V6;
		IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_7;
		ExtraModuleNames.Add("Hitmen");
		ExtraModuleNames.Add("HitmenEditor");
	}
}
