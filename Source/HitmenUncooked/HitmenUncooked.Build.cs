// Hitmen — uncooked-only module: anim graph nodes (editor graphs of runtime anim blueprints)

using UnrealBuildTool;

public class HitmenUncooked : ModuleRules
{
	public HitmenUncooked(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] {
			"Core",
			"CoreUObject",
			"Engine",
			"Hitmen",
			"AnimGraph",
			"AnimGraphRuntime"
		});

		PrivateDependencyModuleNames.AddRange(new string[] {
			"BlueprintGraph",
			"UnrealEd"
		});

		PublicIncludePaths.Add(ModuleDirectory);
	}
}
