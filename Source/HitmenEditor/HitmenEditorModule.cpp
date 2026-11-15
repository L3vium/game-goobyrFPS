// Hitmen — editor module: the "Hitmen LD" tab (Window menu and level toolbar): place LD structures and builder pieces
// where the viewport looks, run PCG, bake, and check a map.

#include "Modules/ModuleManager.h"
#include "Framework/Docking/TabManager.h"
#include "HitmenLDTools.h"
#include "Styling/AppStyle.h"
#include "ToolMenus.h"
#include "Widgets/Docking/SDockTab.h"
#include "Widgets/Input/SButton.h"
#include "Widgets/Input/SSpinBox.h"
#include "Widgets/Layout/SScrollBox.h"
#include "Widgets/Layout/SWrapBox.h"
#include "Widgets/SBoxPanel.h"
#include "Widgets/Text/STextBlock.h"
#include "WorkspaceMenuStructure.h"
#include "WorkspaceMenuStructureModule.h"
#include "World/LevelDesign/HitmenLDStructure.h"

#define LOCTEXT_NAMESPACE "HitmenEditor"

namespace HitmenLDPanel
{
	const FName TabName(TEXT("HitmenLD"));

	/** Grid snap shared by every button of the tab. */
	float Grid = 50.f;

	struct FEntry
	{
		FText Label;
		FText Tip;
		TFunction<void()> Action;
	};

	AActor* StampBlock(EHitmenLDMetric Metric)
	{
		AHitmenLDBlock* Block = Cast<AHitmenLDBlock>(UHitmenLDTools::StampActor(AHitmenLDBlock::StaticClass(), Grid));
		if (Block)
		{
			Block->Height = Metric;
			Block->RerunConstructionScripts();
		}
		return Block;
	}

	AActor* StampWall(EHitmenLDOpeningType Type)
	{
		AHitmenLDWall* Wall = Cast<AHitmenLDWall>(UHitmenLDTools::StampActor(AHitmenLDWall::StaticClass(), Grid));
		if (Wall)
		{
			FHitmenLDOpening Opening;
			Opening.Type = Type;
			Opening.Offset = Wall->Length * 0.5f;
			Wall->Openings = { Opening };
			Wall->RerunConstructionScripts();
			Wall->RebuildFixtures();
		}
		return Wall;
	}

	TSharedRef<SWidget> Section(const FText& Title, const TArray<FEntry>& Entries)
	{
		TSharedRef<SWrapBox> Buttons = SNew(SWrapBox).UseAllottedSize(true).InnerSlotPadding(FVector2D(4.f, 4.f));
		for (const FEntry& Entry : Entries)
		{
			TFunction<void()> Action = Entry.Action;
			Buttons->AddSlot()
			[
				SNew(SButton)
				.Text(Entry.Label)
				.ToolTipText(Entry.Tip)
				.OnClicked_Lambda([Action]() { Action(); return FReply::Handled(); })
			];
		}
		return SNew(SVerticalBox)
			+ SVerticalBox::Slot().AutoHeight().Padding(0.f, 10.f, 0.f, 4.f)
			[
				SNew(STextBlock).Text(Title).Font(FAppStyle::GetFontStyle("DetailsView.CategoryFontStyle"))
			]
			+ SVerticalBox::Slot().AutoHeight()
			[
				Buttons
			];
	}

	TSharedRef<SDockTab> Spawn(const FSpawnTabArgs&)
	{
		const TArray<FEntry> Structures = {
			{ LOCTEXT("Step", "Step 50"), LOCTEXT("StepTip", "A block you walk or jump onto (50 cm)."), [] { StampBlock(EHitmenLDMetric::Step); } },
			{ LOCTEXT("Vault", "Vault 110"), LOCTEXT("VaultTip", "A block you vault (110 cm)."), [] { StampBlock(EHitmenLDMetric::Vault); } },
			{ LOCTEXT("Climb", "Climb 240"), LOCTEXT("ClimbTip", "A block you grab and climb onto (240 cm)."), [] { StampBlock(EHitmenLDMetric::Climb); } },
			{ LOCTEXT("Reach", "Out of reach 300"), LOCTEXT("ReachTip", "A wall out of reach (300 cm): needs a ladder, a crate or an ability."), [] { StampBlock(EHitmenLDMetric::OutOfReach); } },
			{ LOCTEXT("Wall", "Wall"), LOCTEXT("WallTip", "A 6 m wall; add openings in Details."), [] { UHitmenLDTools::StampActor(AHitmenLDWall::StaticClass(), Grid); } },
			{ LOCTEXT("WallDoor", "Wall + door"), LOCTEXT("WallDoorTip", "A wall with a door in its middle."), [] { StampWall(EHitmenLDOpeningType::Door); } },
			{ LOCTEXT("WallWindow", "Wall + window"), LOCTEXT("WallWindowTip", "A wall with a vaultable window."), [] { StampWall(EHitmenLDOpeningType::Window); } },
			{ LOCTEXT("WallVent", "Wall + vent"), LOCTEXT("WallVentTip", "A wall with a crawl hatch at floor level."), [] { StampWall(EHitmenLDOpeningType::Vent); } },
			{ LOCTEXT("Stairs", "Stairs"), LOCTEXT("StairsTip", "A straight flight (3.5 m up over 6 m)."), [] { UHitmenLDTools::StampActor(AHitmenLDStairs::StaticClass(), Grid); } },
			{ LOCTEXT("Building", "Building"), LOCTEXT("BuildingTip", "A hollow building: floors, stairs, door, windows, ledges, gutter, lamps."), [] { UHitmenLDTools::StampActor(AHitmenLDBuilding::StaticClass(), Grid); } },
			{ LOCTEXT("Duct", "Vent duct"), LOCTEXT("DuctTip", "A crawl duct along a spline, a hatch at each end."), [] { UHitmenLDTools::StampActor(AHitmenLDVentDuct::StaticClass(), Grid); } },
		};
		const TArray<FEntry> Pcg = {
			{ LOCTEXT("Cover", "Cover line"), LOCTEXT("CoverTip", "A spline of vault-height cover with climb blocks (PCG_Hitmen_CoverLine). Edit the spline, then Generate."),
				[] { UHitmenLDTools::StampActor(AHitmenLDPCGLine::StaticClass(), Grid, TEXT("/Game/Hitmen/PCG/PCG_Hitmen_CoverLine.PCG_Hitmen_CoverLine")); } },
			{ LOCTEXT("Rocks", "Rock line"), LOCTEXT("RocksTip", "A parkour line of rocks at the gym metrics (PCG_Hitmen_Rocks). Edit the spline, then Generate."),
				[] { UHitmenLDTools::StampActor(AHitmenLDPCGLine::StaticClass(), Grid, TEXT("/Game/Hitmen/PCG/PCG_Hitmen_Rocks.PCG_Hitmen_Rocks")); } },
			{ LOCTEXT("Generate", "Generate selected"), LOCTEXT("GenerateTip", "Runs the PCG of the selected PCG lines and buildings (Dress Roof on)."), [] { UHitmenLDTools::GenerateSelectedPCG(); } },
			{ LOCTEXT("Graphs", "Rebuild graphs"), LOCTEXT("GraphsTip", "Writes the three Hitmen PCG graphs again (/Game/Hitmen/PCG)."), [] { UHitmenLDTools::BuildPCGGraphs(); } },
		};
		const TArray<FEntry> Pieces = {
			{ LOCTEXT("PBuilding", "Building"), LOCTEXT("PBuildingTip", "hitmen_levels.building: a 2-floor building as plain actors."),
				[] { UHitmenLDTools::StampBuilderPiece(TEXT("L.building({x}, {y}, 1200, 1000, 2, z0={z}, folder='LD/Builder/Building')"), Grid); } },
			{ LOCTEXT("PStairs", "Stairs"), LOCTEXT("PStairsTip", "hitmen_levels.stairs"),
				[] { UHitmenLDTools::StampBuilderPiece(TEXT("L.stairs(({x}, {y}, {z}), 0, 350, 600, 260, folder='LD/Builder/Stairs')"), Grid); } },
			{ LOCTEXT("PLadder", "Ladder 4 m"), LOCTEXT("PLadderTip", "hitmen_levels.ladder"),
				[] { UHitmenLDTools::StampBuilderPiece(TEXT("L.ladder(({x}, {y}, {z}), 0, 400, folder='LD/Builder/Ladders')"), Grid); } },
			{ LOCTEXT("PGutter", "Gutter 7 m"), LOCTEXT("PGutterTip", "hitmen_levels.ladder(gutter=True)"),
				[] { UHitmenLDTools::StampBuilderPiece(TEXT("L.ladder(({x}, {y}, {z}), 0, 700, gutter=True, folder='LD/Builder/Gutters')"), Grid); } },
			{ LOCTEXT("PVent", "Vent duct"), LOCTEXT("PVentTip", "hitmen_levels.vent_duct with a hatch at each end."),
				[] { UHitmenLDTools::StampBuilderPiece(TEXT("L.vent_duct(({x}, {y}), ({x} + 600, {y}), {z}, folder='LD/Builder/Vents')\nL.vent_hatch({x}, {y}, {z}, False, 'LD/Builder/Vents')\nL.vent_hatch({x} + 600, {y}, {z}, False, 'LD/Builder/Vents')"), Grid); } },
			{ LOCTEXT("PTunnel", "Tunnel"), LOCTEXT("PTunnelTip", "hitmen_levels.tunnel: an L-shaped corridor."),
				[] { UHitmenLDTools::StampBuilderPiece(TEXT("L.tunnel([({x}, {y}), ({x} + 2000, {y}), ({x} + 2000, {y} + 1500)], {z}, folder='LD/Builder/Tunnel')"), Grid); } },
			{ LOCTEXT("PMaze", "Hedge maze"), LOCTEXT("PMazeTip", "hitmen_levels.hedge_maze: 30 m, 9 x 9 cells."),
				[] { UHitmenLDTools::StampBuilderPiece(TEXT("L.hedge_maze({x}, {y}, 3000, 9, 'LD/Builder/Maze')"), Grid); } },
		};
		const TArray<FEntry> Checks = {
			{ LOCTEXT("Bake", "Bake selected"), LOCTEXT("BakeTip", "Turns the selected LD structures into plain static mesh actors (fixtures stay)."), [] { UHitmenLDTools::BakeSelected(); } },
			{ LOCTEXT("Nav", "Rebuild navmesh"), LOCTEXT("NavTip", "Rebuilds the navmesh of the open map (save the map when it is done)."), [] { UHitmenLDTools::RebuildNavigation(); } },
			{ LOCTEXT("CheckReach", "Check reach"), LOCTEXT("CheckTip", "hitmen_check_reach.py: every door walkable, every loot / NPC / retrieval point reachable (Output Log)."), [] { UHitmenLDTools::RunToolScript(TEXT("hitmen_check_reach.py")); } },
			{ LOCTEXT("Doors", "Check doors"), LOCTEXT("DoorsTip", "hitmen_check_doors.py: every door fills its opening (Output Log)."), [] { UHitmenLDTools::RunToolScript(TEXT("hitmen_check_doors.py")); } },
		};

		return SNew(SDockTab).TabRole(ETabRole::NomadTab)
		[
			SNew(SScrollBox)
			+ SScrollBox::Slot().Padding(10.f)
			[
				SNew(SVerticalBox)
				+ SVerticalBox::Slot().AutoHeight()
				[
					SNew(STextBlock).AutoWrapText(true)
					.Text(LOCTEXT("Intro", "Places at the point the viewport looks at, snapped to the grid. Structures stay editable in Details (sizes, openings, floors, vents) and keep their doors, windows, hatches, ladders and lamps as real actors."))
				]
				+ SVerticalBox::Slot().AutoHeight().Padding(0.f, 8.f, 0.f, 0.f)
				[
					SNew(SHorizontalBox)
					+ SHorizontalBox::Slot().AutoWidth().VAlign(VAlign_Center).Padding(0.f, 0.f, 8.f, 0.f)
					[
						SNew(STextBlock).Text(LOCTEXT("Grid", "Grid (cm)"))
					]
					+ SHorizontalBox::Slot().MaxWidth(120.f)
					[
						SNew(SSpinBox<float>).MinValue(0.f).MaxValue(1000.f).Delta(10.f).Value_Lambda([] { return Grid; }).OnValueChanged_Lambda([](float Value) { Grid = Value; })
					]
				]
				+ SVerticalBox::Slot().AutoHeight()[ Section(LOCTEXT("Structures", "Structures (gym metrics)"), Structures) ]
				+ SVerticalBox::Slot().AutoHeight()[ Section(LOCTEXT("PCG", "PCG"), Pcg) ]
				+ SVerticalBox::Slot().AutoHeight()[ Section(LOCTEXT("Pieces", "Builder pieces (hitmen_levels.py, plain actors)"), Pieces) ]
				+ SVerticalBox::Slot().AutoHeight()[ Section(LOCTEXT("Checks", "Bake and check"), Checks) ]
			]
		];
	}
}

class FHitmenEditorModule : public IModuleInterface
{
public:
	virtual void StartupModule() override
	{
		FGlobalTabmanager::Get()->RegisterNomadTabSpawner(HitmenLDPanel::TabName, FOnSpawnTab::CreateStatic(&HitmenLDPanel::Spawn))
			.SetDisplayName(LOCTEXT("TabTitle", "Hitmen LD"))
			.SetTooltipText(LOCTEXT("TabTip", "Level design structures, PCG lines, builder pieces and map checks."))
			.SetGroup(WorkspaceMenu::GetMenuStructure().GetLevelEditorCategory())
			.SetIcon(FSlateIcon(FAppStyle::GetAppStyleSetName(), "LevelEditor.Tabs.PlacementBrowser"));
		UToolMenus::RegisterStartupCallback(FSimpleMulticastDelegate::FDelegate::CreateRaw(this, &FHitmenEditorModule::RegisterMenus));
	}

	virtual void ShutdownModule() override
	{
		UToolMenus::UnRegisterStartupCallback(this);
		UToolMenus::UnregisterOwner(this);
		if (FSlateApplication::IsInitialized())
		{
			FGlobalTabmanager::Get()->UnregisterNomadTabSpawner(HitmenLDPanel::TabName);
		}
	}

private:
	void RegisterMenus()
	{
		FToolMenuOwnerScoped Owner(this);
		if (UToolMenu* Toolbar = UToolMenus::Get()->ExtendMenu("LevelEditor.LevelEditorToolBar.User"))
		{
			FToolMenuSection& Section = Toolbar->FindOrAddSection("Hitmen");
			Section.AddEntry(FToolMenuEntry::InitToolBarButton(
				"HitmenLD",
				FUIAction(FExecuteAction::CreateLambda([] { FGlobalTabmanager::Get()->TryInvokeTab(HitmenLDPanel::TabName); })),
				LOCTEXT("ToolbarLabel", "Hitmen LD"),
				LOCTEXT("ToolbarTip", "Opens the Hitmen LD tab: structures, PCG lines, builder pieces, checks."),
				FSlateIcon(FAppStyle::GetAppStyleSetName(), "LevelEditor.Tabs.PlacementBrowser")));
		}
	}
};

IMPLEMENT_MODULE(FHitmenEditorModule, HitmenEditor);

#undef LOCTEXT_NAMESPACE
