// Hitmen — level design tools

#include "HitmenLDTools.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "Editor.h"
#include "Engine/Selection.h"
#include "IPythonScriptPlugin.h"
#include "LevelEditorViewport.h"
#include "Misc/Paths.h"
#include "MeshSelectors/PCGMeshSelectorWeighted.h"
#include "PCGGraph.h"
#include "PCGNode.h"
#include "PCGSettings.h"
#include "ScopedTransaction.h"
#include "UObject/Package.h"
#include "UObject/SavePackage.h"
#include "World/LevelDesign/HitmenLDStructure.h"

#define LOCTEXT_NAMESPACE "HitmenLDTools"

namespace HitmenLDGraphs
{
	const TCHAR* Folder = TEXT("/Game/Hitmen/PCG");
	const TCHAR* Cube = TEXT("/Engine/BasicShapes/Cube.Cube");
	const TCHAR* Cylinder = TEXT("/Engine/BasicShapes/Cylinder.Cylinder");
	const TCHAR* Stone = TEXT("/Game/Hitmen/Materials/Instances/MI_Grid_Stone.MI_Grid_Stone");
	const TCHAR* Wood = TEXT("/Game/Hitmen/Materials/Instances/MI_Grid_Wood.MI_Grid_Wood");
	const TCHAR* Metal = TEXT("/Game/Hitmen/Materials/Instances/MI_Grid_Metal.MI_Grid_Metal");
	const TCHAR* Dark = TEXT("/Game/LevelPrototyping/Materials/MI_PrototypeGrid_TopDark.MI_PrototypeGrid_TopDark");

	/** Sets a property (dotted path into structs) from its text form, the way the Details panel would. */
	bool SetText(UObject* Object, const FString& Path, const FString& Value)
	{
		TArray<FString> Parts;
		Path.ParseIntoArray(Parts, TEXT("."));
		const UStruct* Struct = Object->GetClass();
		void* Container = Object;
		for (int32 Index = 0; Index < Parts.Num(); ++Index)
		{
			FProperty* Property = FindFProperty<FProperty>(Struct, *Parts[Index]);
			if (!Property)
			{
				UE_LOG(LogTemp, Warning, TEXT("HitmenLD: no property %s on %s"), *Path, *Object->GetClass()->GetName());
				return false;
			}
			void* Address = Property->ContainerPtrToValuePtr<void>(Container);
			if (Index == Parts.Num() - 1)
			{
				if (!Property->ImportText_Direct(*Value, Address, Object, PPF_None))
				{
					UE_LOG(LogTemp, Warning, TEXT("HitmenLD: could not set %s = %s"), *Path, *Value);
					return false;
				}
				return true;
			}
			const FStructProperty* StructProperty = CastField<FStructProperty>(Property);
			if (!StructProperty)
			{
				return false;
			}
			Struct = StructProperty->Struct;
			Container = Address;
		}
		return false;
	}

	/** A node of a PCG settings class found by name (several settings classes are not exported). */
	UPCGNode* AddNode(UPCGGraph* Graph, const TCHAR* ClassName, int32 X, int32 Y, std::initializer_list<TPair<const TCHAR*, FString>> Properties, UPCGSettings** OutSettings = nullptr)
	{
		UClass* Class = FindObject<UClass>(nullptr, *FString::Printf(TEXT("/Script/PCG.%s"), ClassName));
		if (!Class)
		{
			UE_LOG(LogTemp, Error, TEXT("HitmenLD: PCG class %s not found"), ClassName);
			return nullptr;
		}
		UPCGSettings* Settings = nullptr;
		UPCGNode* Node = Graph->AddNodeOfType(Class, Settings);
		if (!Node || !Settings)
		{
			return nullptr;
		}
		for (const TPair<const TCHAR*, FString>& Property : Properties)
		{
			SetText(Settings, Property.Key, Property.Value);
		}
#if WITH_EDITOR
		Node->SetNodePosition(X, Y);
#endif
		if (OutSettings)
		{
			*OutSettings = Settings;
		}
		return Node;
	}

	struct FMeshChoice
	{
		const TCHAR* Mesh;
		const TCHAR* Material;
		int32 Weight;
	};

	UPCGNode* AddSpawner(UPCGGraph* Graph, int32 X, int32 Y, std::initializer_list<FMeshChoice> Meshes)
	{
		UPCGSettings* Settings = nullptr;
		UPCGNode* Node = AddNode(Graph, TEXT("PCGStaticMeshSpawnerSettings"), X, Y, {}, &Settings);
		if (!Node)
		{
			return nullptr;
		}
		UObject* Selector = nullptr;
		if (FObjectProperty* SelectorProperty = FindFProperty<FObjectProperty>(Settings->GetClass(), TEXT("MeshSelectorParameters")))
		{
			Selector = SelectorProperty->GetObjectPropertyValue_InContainer(Settings);
		}
		if (!Selector || Selector->GetClass()->GetName() != TEXT("PCGMeshSelectorWeighted"))
		{
			UE_LOG(LogTemp, Error, TEXT("HitmenLD: the spawner has no weighted mesh selector"));
			return Node;
		}
		UPCGMeshSelectorWeighted* Weighted = static_cast<UPCGMeshSelectorWeighted*>(Selector);
		Weighted->MeshEntries.Reset();
		for (const FMeshChoice& Choice : Meshes)
		{
			FPCGMeshSelectorWeightedEntry Entry(TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(Choice.Mesh)), Choice.Weight);
			Entry.Descriptor.OverrideMaterials = { TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(Choice.Material)) };
			Entry.Descriptor.ComputeHash();
			Weighted->MeshEntries.Add(Entry);
		}
		return Node;
	}

	void Link(UPCGGraph* Graph, UPCGNode* From, UPCGNode* To, const TCHAR* ToPin = TEXT("In"))
	{
		if (From && To)
		{
			Graph->AddEdge(From, TEXT("Out"), To, ToPin);
		}
	}

	/** The graph asset, emptied (kept in place so actors using it keep their reference). */
	UPCGGraph* OpenGraph(const FString& Name)
	{
		const FString PackageName = FString(Folder) / Name;
		UPackage* Package = CreatePackage(*PackageName);
		Package->FullyLoad();
		UPCGGraph* Graph = FindObject<UPCGGraph>(Package, *Name);
		if (!Graph)
		{
			Graph = NewObject<UPCGGraph>(Package, *Name, RF_Public | RF_Standalone | RF_Transactional);
			FAssetRegistryModule::AssetCreated(Graph);
		}
		TArray<UPCGNode*> Nodes = Graph->GetNodes();
		Graph->RemoveNodes(Nodes);
		return Graph;
	}

	bool SaveGraph(UPCGGraph* Graph)
	{
		UPackage* Package = Graph->GetOutermost();
		Package->MarkPackageDirty();
		const FString File = FPackageName::LongPackageNameToFilename(Package->GetName(), FPackageName::GetAssetPackageExtension());
		FSavePackageArgs Args;
		Args.TopLevelFlags = RF_Public | RF_Standalone;
		return UPackage::SavePackage(Package, Graph, *File, Args);
	}

	/** One line of blocks along the spline: every Increment (from Start), Size x (min..max) and height H (cm, buried
	 *  Sink below the line so slopes never show a gap), yaw jitter. */
	UPCGNode* BlockLine(UPCGGraph* Graph, UPCGNode* Spline, int32 Row, float Increment, float Start, FVector2D LengthRange, FVector2D DepthRange, float Height, float Sink, float YawJitter, const TCHAR* Material)
	{
		const int32 Y = Row * 260;
		UPCGNode* Sampler = AddNode(Graph, TEXT("PCGSplineSamplerSettings"), 300, Y, {
			{ TEXT("SamplerParams.Mode"), TEXT("Distance") },
			{ TEXT("SamplerParams.DistanceIncrement"), FString::SanitizeFloat(Increment) },
			{ TEXT("SamplerParams.StartOffset"), FString::SanitizeFloat(Start) } });
		const float SizeZ = (Height + Sink) / 100.f;
		const float CenterZ = (Height - Sink) * 0.5f;
		UPCGNode* Transform = AddNode(Graph, TEXT("PCGTransformPointsSettings"), 600, Y, {
			{ TEXT("OffsetMin"), FString::Printf(TEXT("(X=0,Y=0,Z=%f)"), CenterZ) },
			{ TEXT("OffsetMax"), FString::Printf(TEXT("(X=0,Y=0,Z=%f)"), CenterZ) },
			{ TEXT("RotationMin"), FString::Printf(TEXT("(Pitch=0,Yaw=%f,Roll=0)"), -YawJitter) },
			{ TEXT("RotationMax"), FString::Printf(TEXT("(Pitch=0,Yaw=%f,Roll=0)"), YawJitter) },
			{ TEXT("bUniformScale"), TEXT("False") },
			{ TEXT("bAbsoluteScale"), TEXT("True") },
			{ TEXT("ScaleMin"), FString::Printf(TEXT("(X=%f,Y=%f,Z=%f)"), LengthRange.X / 100.f, DepthRange.X / 100.f, SizeZ) },
			{ TEXT("ScaleMax"), FString::Printf(TEXT("(X=%f,Y=%f,Z=%f)"), LengthRange.Y / 100.f, DepthRange.Y / 100.f, SizeZ) } });
		UPCGNode* Spawner = AddSpawner(Graph, 900, Y, { { Cube, Material, 1 } });
		Link(Graph, Spline, Sampler, TEXT("Spline"));
		Link(Graph, Sampler, Transform);
		Link(Graph, Transform, Spawner);
		return Spawner;
	}
}

int32 UHitmenLDTools::BuildPCGGraphs()
{
	using namespace HitmenLDGraphs;
	int32 Built = 0;

	// Cover along a line: low blocks to vault (110 cm) every 4 m, a tall one to climb (240 cm) every 16 m between.
	{
		UPCGGraph* Graph = OpenGraph(TEXT("PCG_Hitmen_CoverLine"));
		UPCGNode* Spline = AddNode(Graph, TEXT("PCGGetSplineSettings"), 0, 0, {});
		BlockLine(Graph, Spline, 0, 400.f, 0.f, FVector2D(160.f, 180.f), FVector2D(60.f, 60.f), 110.f, 20.f, 6.f, Dark);
		BlockLine(Graph, Spline, 1, 1600.f, 200.f, FVector2D(150.f, 150.f), FVector2D(150.f, 150.f), 240.f, 20.f, 0.f, Dark);
		Built += SaveGraph(Graph);
	}
	// A rock line on terrain at the gym metrics: a 45 cm step, a 100 cm vault block (80 cm deep at most), a 220 cm
	// climb block (top 100 cm deep or more), repeating every 9 m, sunk 40 cm into the ground, turned a little.
	{
		UPCGGraph* Graph = OpenGraph(TEXT("PCG_Hitmen_Rocks"));
		UPCGNode* Spline = AddNode(Graph, TEXT("PCGGetSplineSettings"), 0, 0, {});
		BlockLine(Graph, Spline, 0, 900.f, 0.f, FVector2D(140.f, 200.f), FVector2D(120.f, 160.f), 45.f, 40.f, 25.f, Stone);
		BlockLine(Graph, Spline, 1, 900.f, 300.f, FVector2D(120.f, 160.f), FVector2D(60.f, 80.f), 100.f, 40.f, 25.f, Stone);
		BlockLine(Graph, Spline, 2, 900.f, 600.f, FVector2D(150.f, 200.f), FVector2D(100.f, 140.f), 220.f, 40.f, 25.f, Stone);
		Built += SaveGraph(Graph);
	}
	// Roof dressing: inside the roof outline of an LD building (tag HitmenLD_Roof), about one spot in three gets a crate
	// or a vent.
	{
		UPCGGraph* Graph = OpenGraph(TEXT("PCG_Hitmen_RoofDressing"));
		UPCGNode* Spline = AddNode(Graph, TEXT("PCGGetSplineSettings"), 0, 0, {});
		UPCGNode* Sampler = AddNode(Graph, TEXT("PCGSplineSamplerSettings"), 300, 0, {
			{ TEXT("SamplerParams.Dimension"), TEXT("OnInterior") },
			{ TEXT("SamplerParams.InteriorSampleSpacing"), TEXT("260") } });
		UPCGNode* Noise = AddNode(Graph, TEXT("PCGAttributeNoiseSettings"), 600, 0, {
			{ TEXT("Mode"), TEXT("Set") }, { TEXT("NoiseMin"), TEXT("0") }, { TEXT("NoiseMax"), TEXT("1") } });
		UPCGNode* Filter = AddNode(Graph, TEXT("PCGDensityFilterSettings"), 900, 0, {
			{ TEXT("LowerBound"), TEXT("0.66") }, { TEXT("UpperBound"), TEXT("1") } });
		UPCGNode* Transform = AddNode(Graph, TEXT("PCGTransformPointsSettings"), 1200, 0, {
			{ TEXT("OffsetMin"), TEXT("(X=0,Y=0,Z=45)") }, { TEXT("OffsetMax"), TEXT("(X=0,Y=0,Z=45)") },
			{ TEXT("RotationMin"), TEXT("(Pitch=0,Yaw=0,Roll=0)") }, { TEXT("RotationMax"), TEXT("(Pitch=0,Yaw=90,Roll=0)") },
			{ TEXT("bUniformScale"), TEXT("False") }, { TEXT("bAbsoluteScale"), TEXT("True") },
			{ TEXT("ScaleMin"), TEXT("(X=0.8,Y=0.8,Z=0.9)") }, { TEXT("ScaleMax"), TEXT("(X=1.3,Y=1.1,Z=0.9)") } });
		UPCGNode* Spawner = AddSpawner(Graph, 1500, 0, { { Cube, Wood, 3 }, { Cylinder, Metal, 1 } });
		Link(Graph, Spline, Sampler, TEXT("Spline"));
		Link(Graph, Sampler, Noise);
		Link(Graph, Noise, Filter);
		Link(Graph, Filter, Transform);
		Link(Graph, Transform, Spawner);
		Built += SaveGraph(Graph);
	}
	return Built;
}

FVector UHitmenLDTools::GetViewportFocus(float Grid)
{
	UWorld* World = GEditor ? GEditor->GetEditorWorldContext().World() : nullptr;
	FVector Focus = FVector::ZeroVector;
	if (World && GCurrentLevelEditingViewportClient)
	{
		const FVector Eye = GCurrentLevelEditingViewportClient->GetViewLocation();
		const FVector Forward = GCurrentLevelEditingViewportClient->GetViewRotation().Vector();
		FHitResult Hit;
		if (World->LineTraceSingleByChannel(Hit, Eye, Eye + Forward * 10000.f, ECC_Visibility))
		{
			Focus = Hit.ImpactPoint;
		}
		else
		{
			const FVector Ahead = Eye + Forward.GetSafeNormal2D() * 1500.f;
			Focus = World->LineTraceSingleByChannel(Hit, Ahead + FVector(0.f, 0.f, 5000.f), Ahead - FVector(0.f, 0.f, 20000.f), ECC_Visibility)
				? Hit.ImpactPoint : FVector(Ahead.X, Ahead.Y, 0.f);
		}
	}
	if (Grid > 0.f)
	{
		Focus.X = FMath::GridSnap(Focus.X, Grid);
		Focus.Y = FMath::GridSnap(Focus.Y, Grid);
	}
	return Focus;
}

AActor* UHitmenLDTools::StampActor(UClass* Class, float Grid, const FString& GraphPath)
{
	UWorld* World = GEditor ? GEditor->GetEditorWorldContext().World() : nullptr;
	if (!World || !Class)
	{
		return nullptr;
	}
	const FScopedTransaction Transaction(LOCTEXT("Stamp", "Hitmen LD: place"));
	const FVector At = GetViewportFocus(Grid);
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	AActor* Actor = World->SpawnActor(Class, &At, &FRotator::ZeroRotator, Params);
	if (!Actor)
	{
		return nullptr;
	}
	Actor->SetFolderPath(TEXT("LD"));
	if (AHitmenLDPCGLine* Line = Cast<AHitmenLDPCGLine>(Actor))
	{
		if (!GraphPath.IsEmpty())
		{
			Line->Graph = TSoftObjectPtr<UPCGGraphInterface>(FSoftObjectPath(GraphPath));
			Line->RerunConstructionScripts();
		}
	}
	GEditor->SelectNone(false, true);
	GEditor->SelectActor(Actor, true, true);
	return Actor;
}

bool UHitmenLDTools::StampBuilderPiece(const FString& PythonTemplate, float Grid)
{
	IPythonScriptPlugin* Python = IPythonScriptPlugin::Get();
	if (!Python || !Python->IsPythonAvailable())
	{
		return false;
	}
	const FVector At = GetViewportFocus(Grid);
	const FString ToolsDir = FPaths::ConvertRelativePathToFull(FPaths::ProjectDir() / TEXT("Tools/Editor"));
	FString Body = PythonTemplate.Replace(TEXT("{x}"), *FString::SanitizeFloat(At.X)).Replace(TEXT("{y}"), *FString::SanitizeFloat(At.Y)).Replace(TEXT("{z}"), *FString::SanitizeFloat(At.Z));
	const FString Command = FString::Printf(TEXT("import sys\nif r'%s' not in sys.path:\n    sys.path.insert(0, r'%s')\nimport hitmen_levels as L\n%s\n"), *ToolsDir, *ToolsDir, *Body);
	FPythonCommandEx Ex;
	Ex.ExecutionMode = EPythonCommandExecutionMode::ExecuteFile;
	Ex.Command = Command;
	const FScopedTransaction Transaction(LOCTEXT("Piece", "Hitmen LD: builder piece"));
	return Python->ExecPythonCommandEx(Ex);
}

int32 UHitmenLDTools::GenerateSelectedPCG()
{
	int32 Count = 0;
	for (FSelectionIterator It(GEditor->GetSelectedActorIterator()); It; ++It)
	{
		if (AHitmenLDBuilding* Building = Cast<AHitmenLDBuilding>(*It))
		{
			Building->GenerateRoofDressing();
			++Count;
		}
		else if (AHitmenLDPCGLine* Line = Cast<AHitmenLDPCGLine>(*It))
		{
			Line->Generate();
			++Count;
		}
	}
	return Count;
}

int32 UHitmenLDTools::BakeSelected()
{
	TArray<AHitmenLDStructure*> Structures;
	for (FSelectionIterator It(GEditor->GetSelectedActorIterator()); It; ++It)
	{
		if (AHitmenLDStructure* Structure = Cast<AHitmenLDStructure>(*It))
		{
			Structures.Add(Structure);
		}
	}
	const FScopedTransaction Transaction(LOCTEXT("Bake", "Hitmen LD: bake"));
	GEditor->SelectNone(false, true);
	for (AHitmenLDStructure* Structure : Structures)
	{
		Structure->BakeToStaticMeshes();
	}
	return Structures.Num();
}

bool UHitmenLDTools::RunToolScript(const FString& FileName)
{
	IPythonScriptPlugin* Python = IPythonScriptPlugin::Get();
	if (!Python || !Python->IsPythonAvailable())
	{
		return false;
	}
	FPythonCommandEx Ex;
	Ex.ExecutionMode = EPythonCommandExecutionMode::ExecuteFile;
	Ex.Command = FPaths::ConvertRelativePathToFull(FPaths::ProjectDir() / TEXT("Tools/Editor") / FileName);
	return Python->ExecPythonCommandEx(Ex);
}

void UHitmenLDTools::RebuildNavigation()
{
	if (UWorld* World = GEditor ? GEditor->GetEditorWorldContext().World() : nullptr)
	{
		GEditor->Exec(World, TEXT("RebuildNavigation"));
	}
}

#undef LOCTEXT_NAMESPACE
