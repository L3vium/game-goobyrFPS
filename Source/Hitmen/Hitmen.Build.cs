// Hitmen — module rules

using UnrealBuildTool;

public class Hitmen : ModuleRules
{
	public Hitmen(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] {
			"Core",
			"CoreUObject",
			"Engine",
			"InputCore",
			"EnhancedInput",
			"AIModule",
			"NavigationSystem",
			"GameplayTasks",
			"GameplayTags",
			"UMG",
			"Slate",
			"SlateCore",
			"Niagara",
			"PhysicsCore",
			"NetCore",
			"DeveloperSettings",
			"OnlineSubsystem",
			"OnlineSubsystemUtils",
			"AnimGraphRuntime"
		});

		PublicIncludePaths.Add(ModuleDirectory);
	}
}
