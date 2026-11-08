// Hitmen — editor-only module: content builders callable from editor Python

using UnrealBuildTool;

public class HitmenEditor : ModuleRules
{
	public HitmenEditor(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] {
			"Core",
			"CoreUObject",
			"Engine",
			"Hitmen"
		});

		PrivateDependencyModuleNames.AddRange(new string[] {
			"UnrealEd",
			"AIModule",
			"AIGraph",
			"BehaviorTreeEditor",
			"AssetTools",
			"AssetRegistry",
			"GameplayTasks"
		});
	}
}
