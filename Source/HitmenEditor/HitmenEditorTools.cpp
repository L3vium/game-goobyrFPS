// Hitmen — editor tools: builds BB_NPC and the NPC behavior trees as editable graphs

#include "HitmenEditorTools.h"
#include "Core/HitmenTypes.h"
#include "AI/HitmenAIController.h"
#include "AI/Tasks/HitmenBTTasks.h"
#include "AI/Decorators/HitmenBTDecorators.h"

#include "BehaviorTree/BehaviorTree.h"
#include "BehaviorTree/BlackboardData.h"
#include "BehaviorTree/Blackboard/BlackboardKeyType_Enum.h"
#include "BehaviorTree/Blackboard/BlackboardKeyType_Object.h"
#include "BehaviorTree/Blackboard/BlackboardKeyType_Vector.h"
#include "BehaviorTree/Blackboard/BlackboardKeyType_Float.h"
#include "BehaviorTree/Blackboard/BlackboardKeyType_Bool.h"
#include "BehaviorTree/Composites/BTComposite_Selector.h"
#include "BehaviorTree/Composites/BTComposite_Sequence.h"
#include "BehaviorTree/Composites/BTComposite_SimpleParallel.h"
#include "BehaviorTree/Tasks/BTTask_MoveTo.h"
#include "BehaviorTree/Tasks/BTTask_Wait.h"
#include "BehaviorTree/Tasks/BTTask_WaitBlackboardTime.h"
#include "BehaviorTree/Tasks/BTTask_RunBehavior.h"
#include "BehaviorTree/Decorators/BTDecorator_Blackboard.h"
#include "BehaviorTree/Decorators/BTDecorator_Cooldown.h"
#include "BehaviorTree/Services/BTService_DefaultFocus.h"
#include "BehaviorTree/Tasks/BTTask_RunEQSQuery.h"
#include "EnvironmentQuery/EnvQuery.h"
#include "EnvironmentQuery/EnvQueryOption.h"
#include "EnvironmentQuery/Tests/EnvQueryTest_Distance.h"
#include "EnvironmentQuery/Tests/EnvQueryTest_Trace.h"
#include "EnvironmentQuery/Tests/EnvQueryTest_Pathfinding.h"
#include "EnvironmentQuery/Contexts/EnvQueryContext_Querier.h"
#include "AI/HitmenEQS.h"

#include "BehaviorTreeGraph.h"
#include "BehaviorTreeGraphNode.h"
#include "BehaviorTreeGraphNode_Root.h"
#include "BehaviorTreeGraphNode_Composite.h"
#include "BehaviorTreeGraphNode_Task.h"
#include "BehaviorTreeGraphNode_SubtreeTask.h"
#include "BehaviorTreeGraphNode_SimpleParallel.h"
#include "BehaviorTreeGraphNode_Decorator.h"
#include "BehaviorTreeGraphNode_Service.h"
#include "EdGraphSchema_BehaviorTree.h"
#include "EdGraph/EdGraph.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "GameFramework/Actor.h"
#include "Editor/EditorPerformanceSettings.h"
#include "Settings/LevelEditorPlaySettings.h"
#include "UObject/Package.h"

namespace
{
	template <typename T>
	T* CreateOrLoadAsset(const FString& Folder, const FString& Name)
	{
		const FString PackageName = Folder / Name;
		UPackage* Package = CreatePackage(*PackageName);
		Package->FullyLoad();
		T* Asset = FindObject<T>(Package, *Name);
		if (!Asset)
		{
			Asset = NewObject<T>(Package, *Name, RF_Public | RF_Standalone | RF_Transactional);
			FAssetRegistryModule::AssetCreated(Asset);
		}
		Package->MarkPackageDirty();
		return Asset;
	}

	// ---- Reflection setters (node properties are often protected) ----
	void SetKey(UObject* Node, FName PropertyName, FName KeyName)
	{
		if (const FStructProperty* Prop = FindFProperty<FStructProperty>(Node->GetClass(), PropertyName))
		{
			if (Prop->Struct == FBlackboardKeySelector::StaticStruct())
			{
				Prop->ContainerPtrToValuePtr<FBlackboardKeySelector>(Node)->SelectedKeyName = KeyName;
			}
		}
	}

	void SetFloat(UObject* Node, FName PropertyName, float Value)
	{
		FProperty* Prop = FindFProperty<FProperty>(Node->GetClass(), PropertyName);
		if (FFloatProperty* FloatProp = CastField<FFloatProperty>(Prop))
		{
			FloatProp->SetPropertyValue_InContainer(Node, Value);
		}
		else if (FStructProperty* StructProp = CastField<FStructProperty>(Prop))
		{
			void* StructPtr = StructProp->ContainerPtrToValuePtr<void>(Node);
			if (FFloatProperty* Inner = FindFProperty<FFloatProperty>(StructProp->Struct, TEXT("DefaultValue")))
			{
				Inner->SetPropertyValue_InContainer(StructPtr, Value);
			}
		}
	}

	void SetBool(UObject* Node, FName PropertyName, bool bValue)
	{
		if (FBoolProperty* Prop = FindFProperty<FBoolProperty>(Node->GetClass(), PropertyName))
		{
			Prop->SetPropertyValue_InContainer(Node, bValue);
		}
	}

	void SetObject(UObject* Node, FName PropertyName, UObject* Value)
	{
		if (FObjectPropertyBase* Prop = FindFProperty<FObjectPropertyBase>(Node->GetClass(), PropertyName))
		{
			Prop->SetObjectPropertyValue_InContainer(Node, Value);
		}
	}

	void SetEnumByte(UObject* Node, FName PropertyName, uint8 Value)
	{
		FProperty* Prop = FindFProperty<FProperty>(Node->GetClass(), PropertyName);
		if (FByteProperty* ByteProp = CastField<FByteProperty>(Prop))
		{
			ByteProp->SetPropertyValue_InContainer(Node, Value);
		}
		else if (FEnumProperty* EnumProp = CastField<FEnumProperty>(Prop))
		{
			EnumProp->GetUnderlyingProperty()->SetIntPropertyValue(EnumProp->ContainerPtrToValuePtr<void>(Node), static_cast<int64>(Value));
		}
	}

	/** Small helper around the BT editor graph API. */
	struct FTreeBuilder
	{
		UBehaviorTree* Tree = nullptr;
		UBehaviorTreeGraph* Graph = nullptr;
		UBehaviorTreeGraphNode* Root = nullptr;

		FTreeBuilder(UBehaviorTree* InTree, UBlackboardData* Blackboard)
			: Tree(InTree)
		{
			Tree->BlackboardAsset = Blackboard;
			// Rebuild from scratch: purge the old graph, its nodes and every old node instance.
			// Reusing them loses decorators of the new tree (one per decorator of the old tree).
			Tree->BTGraph = nullptr;
			Tree->RootNode = nullptr;
			Tree->RootDecorators.Reset();
			Tree->RootDecoratorOps.Reset();
			TArray<UObject*> OldInners;
			GetObjectsWithOuter(Tree, OldInners, /*bIncludeNestedObjects*/ false);
			for (UObject* Old : OldInners)
			{
				Old->SetFlags(RF_Transient);
				Old->Rename(nullptr, GetTransientPackage(), REN_DontCreateRedirectors | REN_NonTransactional);
				Old->MarkAsGarbage();
			}
			Graph = CastChecked<UBehaviorTreeGraph>(FBlueprintEditorUtils::CreateNewGraph(Tree, NAME_None, UBehaviorTreeGraph::StaticClass(), UEdGraphSchema_BehaviorTree::StaticClass()));
			Tree->BTGraph = Graph;
			Graph->GetSchema()->CreateDefaultNodesForGraph(*Graph);
			Graph->OnCreated();
			for (UEdGraphNode* Node : Graph->Nodes)
			{
				if (UBehaviorTreeGraphNode_Root* RootNode = Cast<UBehaviorTreeGraphNode_Root>(Node))
				{
					Root = RootNode;
				}
			}
		}

		template <typename TGraphNode>
		TGraphNode* Add(UClass* NodeClass, UBehaviorTreeGraphNode* Parent, int32 X, int32 Y, int32 ParentPin = 0)
		{
			FGraphNodeCreator<TGraphNode> Creator(*Graph);
			TGraphNode* Node = Creator.CreateNode();
			Node->ClassData = FGraphNodeClassData(NodeClass, FString());
			Node->NodePosX = X;
			Node->NodePosY = Y;
			Creator.Finalize();
			if (Parent)
			{
				Parent->GetOutputPin(ParentPin)->MakeLinkTo(Node->GetInputPin());
			}
			return Node;
		}

		UBehaviorTreeGraphNode* Composite(UClass* NodeClass, UBehaviorTreeGraphNode* Parent, int32 X, int32 Y)
		{
			return Add<UBehaviorTreeGraphNode_Composite>(NodeClass, Parent, X, Y);
		}

		UBehaviorTreeGraphNode* Task(UClass* NodeClass, UBehaviorTreeGraphNode* Parent, int32 X, int32 Y, int32 ParentPin = 0)
		{
			return Add<UBehaviorTreeGraphNode_Task>(NodeClass, Parent, X, Y, ParentPin);
		}

		UBehaviorTreeGraphNode* Subtree(UBehaviorTree* Asset, UBehaviorTreeGraphNode* Parent, int32 X, int32 Y)
		{
			UBehaviorTreeGraphNode* Node = Add<UBehaviorTreeGraphNode_SubtreeTask>(UBTTask_RunBehavior::StaticClass(), Parent, X, Y);
			SetObject(Node->NodeInstance, TEXT("BehaviorAsset"), Asset);
			return Node;
		}

		UObject* Decorator(UBehaviorTreeGraphNode* Target, UClass* NodeClass)
		{
			UBehaviorTreeGraphNode_Decorator* Node = NewObject<UBehaviorTreeGraphNode_Decorator>(Graph);
			Node->ClassData = FGraphNodeClassData(NodeClass, FString());
			Target->AddSubNode(Node, Graph);
			return Node->NodeInstance;
		}

		UObject* Service(UBehaviorTreeGraphNode* Target, UClass* NodeClass)
		{
			UBehaviorTreeGraphNode_Service* Node = NewObject<UBehaviorTreeGraphNode_Service>(Graph);
			Node->ClassData = FGraphNodeClassData(NodeClass, FString());
			Target->AddSubNode(Node, Graph);
			return Node->NodeInstance;
		}

		UBehaviorTreeGraphNode* MoveTo(UBehaviorTreeGraphNode* Parent, FName Key, float Radius, int32 X, int32 Y, int32 ParentPin = 0)
		{
			UBehaviorTreeGraphNode* Node = Task(UBTTask_MoveTo::StaticClass(), Parent, X, Y, ParentPin);
			SetKey(Node->NodeInstance, TEXT("BlackboardKey"), Key);
			SetFloat(Node->NodeInstance, TEXT("AcceptableRadius"), Radius);
			return Node;
		}

		void AlertIs(UBehaviorTreeGraphNode* Target, std::initializer_list<EHitmenAlertState> States)
		{
			if (UBTDecorator_HitmenAlertState* Decorator = Cast<UBTDecorator_HitmenAlertState>(this->Decorator(Target, UBTDecorator_HitmenAlertState::StaticClass())))
			{
				Decorator->SetAllowedStates(TArray<EHitmenAlertState>(States));
			}
		}

		/** Blackboard "is set" condition. Observer aborts: None for static keys (sniper, stationary),
		 *  Both for live keys so a change preempts the running branch (seeing the target, calling). */
		void KeyIsSet(UBehaviorTreeGraphNode* Target, FName Key, EBTFlowAbortMode::Type AbortMode = EBTFlowAbortMode::None)
		{
			if (UObject* Decorator = this->Decorator(Target, UBTDecorator_Blackboard::StaticClass()))
			{
				SetKey(Decorator, TEXT("BlackboardKey"), Key);
				SetEnumByte(Decorator, TEXT("BasicOperation"), 0 /* Set */);
				SetEnumByte(Decorator, TEXT("FlowAbortMode"), static_cast<uint8>(AbortMode));
			}
		}

		/** Move that re-paths when its blackboard location changes (new noise, new sighting). */
		UBehaviorTreeGraphNode* MoveToObserved(UBehaviorTreeGraphNode* Parent, FName Key, float Radius, int32 X, int32 Y, int32 ParentPin = 0)
		{
			UBehaviorTreeGraphNode* Node = MoveTo(Parent, Key, Radius, X, Y, ParentPin);
			SetBool(Node->NodeInstance, TEXT("bObserveBlackboardValue"), true);
			return Node;
		}

		void FocusOn(UBehaviorTreeGraphNode* Target, FName Key)
		{
			if (UObject* Service = this->Service(Target, UBTService_DefaultFocus::StaticClass()))
			{
				SetKey(Service, TEXT("BlackboardKey"), Key);
			}
		}

		void Finish()
		{
			Graph->UpdateAsset();
			Tree->MarkPackageDirty();
		}
	};

	UBlackboardData* BuildBlackboard(const FString& Folder)
	{
		UBlackboardData* Blackboard = CreateOrLoadAsset<UBlackboardData>(Folder, TEXT("BB_NPC"));
		Blackboard->Keys.Reset();
		auto AddKey = [Blackboard](FName Name, UBlackboardKeyType* Type)
		{
			FBlackboardEntry Entry;
			Entry.EntryName = Name;
			Entry.KeyType = Type;
			Blackboard->Keys.Add(Entry);
		};
		auto ObjectKey = [Blackboard](UClass* BaseClass)
		{
			UBlackboardKeyType_Object* Type = NewObject<UBlackboardKeyType_Object>(Blackboard);
			Type->BaseClass = BaseClass;
			return Type;
		};
		UBlackboardKeyType_Enum* EnumType = NewObject<UBlackboardKeyType_Enum>(Blackboard);
		EnumType->EnumType = StaticEnum<EHitmenAlertState>();
		EnumType->EnumName = TEXT("EHitmenAlertState");

		AddKey(TEXT("SelfActor"), ObjectKey(AActor::StaticClass()));
		AddKey(HitmenBBKeys::AlertState, EnumType);
		AddKey(HitmenBBKeys::TargetActor, ObjectKey(AActor::StaticClass()));
		AddKey(HitmenBBKeys::LastKnownLocation, NewObject<UBlackboardKeyType_Vector>(Blackboard));
		AddKey(HitmenBBKeys::InvestigateLocation, NewObject<UBlackboardKeyType_Vector>(Blackboard));
		AddKey(HitmenBBKeys::MoveLocation, NewObject<UBlackboardKeyType_Vector>(Blackboard));
		AddKey(HitmenBBKeys::HomeLocation, NewObject<UBlackboardKeyType_Vector>(Blackboard));
		AddKey(HitmenBBKeys::WaitTime, NewObject<UBlackboardKeyType_Float>(Blackboard));
		AddKey(HitmenBBKeys::TargetVisible, NewObject<UBlackboardKeyType_Bool>(Blackboard));
		AddKey(HitmenBBKeys::IsSniper, NewObject<UBlackboardKeyType_Bool>(Blackboard));
		AddKey(HitmenBBKeys::CanCallReinforcements, NewObject<UBlackboardKeyType_Bool>(Blackboard));
		AddKey(HitmenBBKeys::IsStationary, NewObject<UBlackboardKeyType_Bool>(Blackboard));
		AddKey(HitmenBBKeys::HoldPosition, NewObject<UBlackboardKeyType_Bool>(Blackboard));
		AddKey(HitmenBBKeys::UseActor, ObjectKey(AActor::StaticClass()));
		AddKey(HitmenBBKeys::IsOnEdge, NewObject<UBlackboardKeyType_Bool>(Blackboard));
		AddKey(HitmenBBKeys::Evacuate, NewObject<UBlackboardKeyType_Bool>(Blackboard));
		AddKey(HitmenBBKeys::DisposeBody, ObjectKey(AActor::StaticClass()));
		AddKey(HitmenBBKeys::DisposeContainer, ObjectKey(AActor::StaticClass()));
		AddKey(HitmenBBKeys::DisposeLocation, NewObject<UBlackboardKeyType_Vector>(Blackboard));
		Blackboard->MarkPackageDirty();
		return Blackboard;
	}

	/**
	 * EQ_HitmenSearch: where a searching guard looks next. Candidates around the search center (hiding spots, the center,
	 * random reachable points); reachable only; scored by: not claimed by a colleague, in front of a hiding spot, hidden
	 * from the center, hidden from the guard, near the center. Generated here (edit this builder, not the asset).
	 */
	UEnvQuery* BuildSearchQuery(const FString& Folder)
	{
		UEnvQuery* Query = CreateOrLoadAsset<UEnvQuery>(Folder, TEXT("EQ_HitmenSearch"));
		auto& Options = Query->GetOptionsMutable();
		Options.Reset();
		UEnvQueryOption* Option = NewObject<UEnvQueryOption>(Query, NAME_None, RF_Transactional);
		Option->Generator = NewObject<UEnvQueryGenerator_HitmenSearchSpots>(Option, NAME_None, RF_Transactional);

		UEnvQueryTest_Pathfinding* Reachable = NewObject<UEnvQueryTest_Pathfinding>(Option, NAME_None, RF_Transactional);
		Reachable->TestMode = EEnvTestPathfinding::PathExist;
		Reachable->Context = UEnvQueryContext_Querier::StaticClass();
		Reachable->TestPurpose = EEnvTestPurpose::Filter;
		Reachable->BoolValue.DefaultValue = true;

		UEnvQueryTest_HitmenSearchFree* Free = NewObject<UEnvQueryTest_HitmenSearchFree>(Option, NAME_None, RF_Transactional);
		Free->TestPurpose = EEnvTestPurpose::Score;
		Free->BoolValue.DefaultValue = true;
		Free->ScoringFactor.DefaultValue = 2.f;

		UEnvQueryTest_HitmenNearHidingSpot* Hiding = NewObject<UEnvQueryTest_HitmenNearHidingSpot>(Option, NAME_None, RF_Transactional);
		Hiding->TestPurpose = EEnvTestPurpose::Score;
		Hiding->BoolValue.DefaultValue = true;
		Hiding->ScoringFactor.DefaultValue = 1.5f;

		// A trace from the center that is blocked = a spot hidden from where the target was (where it could hide).
		UEnvQueryTest_Trace* HiddenFromCenter = NewObject<UEnvQueryTest_Trace>(Option, NAME_None, RF_Transactional);
		HiddenFromCenter->Context = UEnvQueryContext_HitmenSearchCenter::StaticClass();
		HiddenFromCenter->ItemHeightOffset.DefaultValue = 150.f;
		HiddenFromCenter->ContextHeightOffset.DefaultValue = 150.f;
		HiddenFromCenter->TestPurpose = EEnvTestPurpose::Score;
		HiddenFromCenter->BoolValue.DefaultValue = true;
		HiddenFromCenter->ScoringFactor.DefaultValue = 1.f;

		UEnvQueryTest_Trace* HiddenFromSelf = NewObject<UEnvQueryTest_Trace>(Option, NAME_None, RF_Transactional);
		HiddenFromSelf->Context = UEnvQueryContext_Querier::StaticClass();
		HiddenFromSelf->ItemHeightOffset.DefaultValue = 150.f;
		HiddenFromSelf->ContextHeightOffset.DefaultValue = 150.f;
		HiddenFromSelf->TestPurpose = EEnvTestPurpose::Score;
		HiddenFromSelf->BoolValue.DefaultValue = true;
		HiddenFromSelf->ScoringFactor.DefaultValue = 0.8f;

		UEnvQueryTest_Distance* Near = NewObject<UEnvQueryTest_Distance>(Option, NAME_None, RF_Transactional);
		Near->DistanceTo = UEnvQueryContext_HitmenSearchCenter::StaticClass();
		Near->TestPurpose = EEnvTestPurpose::Score;
		Near->ScoringEquation = EEnvTestScoreEquation::InverseLinear;
		Near->ScoringFactor.DefaultValue = 1.f;

		Option->Tests = { Reachable, Free, Hiding, HiddenFromCenter, HiddenFromSelf, Near };
		Options.Add(Option);
		Query->MarkPackageDirty();
		return Query;
	}
}

bool UHitmenEditorTools::BuildNPCBehaviorTrees(const FString& Folder)
{
	UBlackboardData* Blackboard = BuildBlackboard(Folder);
	UEnvQuery* SearchQuery = BuildSearchQuery(Folder);

	// ---- BT_Patrol: next point, walk, use the point's actor, wait ----
	UBehaviorTree* Patrol = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_Patrol"));
	{
		FTreeBuilder B(Patrol, Blackboard);
		UBehaviorTreeGraphNode* Seq = B.Composite(UBTComposite_Sequence::StaticClass(), B.Root, 0, 150);
		B.Task(UBTTask_HitmenFindPatrolPoint::StaticClass(), Seq, -450, 320);
		B.MoveTo(Seq, HitmenBBKeys::MoveLocation, 80.f, -150, 320);
		B.Task(UBTTask_HitmenUseActor::StaticClass(), Seq, 150, 320);
		// On edge after an alert (Hitman): a look around at each point instead of the idle wait.
		UBehaviorTreeGraphNode* Pause = B.Composite(UBTComposite_Selector::StaticClass(), Seq, 500, 320);
		UBehaviorTreeGraphNode* EdgeLook = B.Task(UBTTask_HitmenLookAround::StaticClass(), Pause, 400, 520);
		B.KeyIsSet(EdgeLook, HitmenBBKeys::IsOnEdge);
		SetFloat(EdgeLook->NodeInstance, TEXT("Duration"), 2.5f);
		UBehaviorTreeGraphNode* Wait = B.Task(UBTTask_WaitBlackboardTime::StaticClass(), Pause, 650, 520);
		SetKey(Wait->NodeInstance, TEXT("BlackboardKey"), HitmenBBKeys::WaitTime);
		B.Finish();
	}

	// ---- BT_Guard: stationary NPC, back to its post, face its placement direction, wait ----
	UBehaviorTree* Guard = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_Guard"));
	{
		FTreeBuilder B(Guard, Blackboard);
		UBehaviorTreeGraphNode* Seq = B.Composite(UBTComposite_Sequence::StaticClass(), B.Root, 0, 150);
		B.Task(UBTTask_HitmenFindPatrolPoint::StaticClass(), Seq, -450, 320);
		B.MoveTo(Seq, HitmenBBKeys::MoveLocation, 60.f, -150, 320);
		B.Task(UBTTask_HitmenFacePost::StaticClass(), Seq, 150, 320);
		UBehaviorTreeGraphNode* Wait = B.Task(UBTTask_WaitBlackboardTime::StaticClass(), Seq, 450, 320);
		SetKey(Wait->NodeInstance, TEXT("BlackboardKey"), HitmenBBKeys::WaitTime);
		B.Finish();
	}

	// ---- BT_Investigate: go to the noise / body, look around ----
	UBehaviorTree* Investigate = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_Investigate"));
	{
		FTreeBuilder B(Investigate, Blackboard);
		UBehaviorTreeGraphNode* Seq = B.Composite(UBTComposite_Sequence::StaticClass(), B.Root, 0, 150);
		// "Hm?": face the sound / the sight first (a shot fired near it: toward the shooter), then walk over.
		B.Task(UBTTask_HitmenTurnToNoise::StaticClass(), Seq, -600, 320);
		B.MoveToObserved(Seq, HitmenBBKeys::InvestigateLocation, 150.f, -300, 320);
		B.Task(UBTTask_HitmenLookAround::StaticClass(), Seq, 0, 320);
		UBehaviorTreeGraphNode* Wait = B.Task(UBTTask_Wait::StaticClass(), Seq, 300, 320);
		SetFloat(Wait->NodeInstance, TEXT("WaitTime"), 1.f);
		B.Finish();
	}

	// ---- BT_Search: EQS (EQ_HitmenSearch) picks the next place to look around the last known position (or the body
	//      found), it is claimed, the guard walks there and looks around (the root loops it until the search ends) ----
	UBehaviorTree* Search = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_Search"));
	{
		FTreeBuilder B(Search, Blackboard);
		UBehaviorTreeGraphNode* Seq = B.Composite(UBTComposite_Sequence::StaticClass(), B.Root, 0, 150);
		UBehaviorTreeGraphNode* Query = B.Task(UBTTask_RunEQSQuery::StaticClass(), Seq, -450, 320);
		if (UBTTask_RunEQSQuery* EQSTask = Cast<UBTTask_RunEQSQuery>(Query->NodeInstance))
		{
			EQSTask->EQSRequest.QueryTemplate = SearchQuery;
			EQSTask->EQSRequest.RunMode = EEnvQueryRunMode::RandomBest25Pct;
		}
		SetKey(Query->NodeInstance, TEXT("BlackboardKey"), HitmenBBKeys::MoveLocation);
		B.Task(UBTTask_HitmenClaimSearchSpot::StaticClass(), Seq, -200, 320);
		B.MoveTo(Seq, HitmenBBKeys::MoveLocation, 100.f, 50, 320);
		UBehaviorTreeGraphNode* Look = B.Task(UBTTask_HitmenLookAround::StaticClass(), Seq, 300, 320);
		SetFloat(Look->NodeInstance, TEXT("Duration"), 2.5f);
		B.Finish();
	}

	// ---- BT_DisposeBody: after a body search, carry the body to the nearest container with room and stow it ----
	UBehaviorTree* Dispose = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_DisposeBody"));
	{
		FTreeBuilder B(Dispose, Blackboard);
		UBehaviorTreeGraphNode* Seq = B.Composite(UBTComposite_Sequence::StaticClass(), B.Root, 0, 150);
		B.MoveTo(Seq, HitmenBBKeys::DisposeBody, 110.f, -450, 320);
		B.Task(UBTTask_HitmenPickUpBody::StaticClass(), Seq, -150, 320);
		B.MoveTo(Seq, HitmenBBKeys::DisposeLocation, 60.f, 150, 320);
		B.Task(UBTTask_HitmenStowBody::StaticClass(), Seq, 450, 320);
		B.Finish();
	}

	// ---- BT_CallReinforcements: interruptible radio call ----
	UBehaviorTree* Call = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_CallReinforcements"));
	{
		FTreeBuilder B(Call, Blackboard);
		UBehaviorTreeGraphNode* Seq = B.Composite(UBTComposite_Sequence::StaticClass(), B.Root, 0, 150);
		B.Task(UBTTask_HitmenCallReinforcements::StaticClass(), Seq, 0, 320);
		B.Finish();
	}

	// ---- BT_Sniper: keep the long-range slot; shoot while visible (moving to the slot in parallel),
	//      otherwise reposition and keep watching. Never falls through to the close-range chase. ----
	UBehaviorTree* Sniper = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_Sniper"));
	{
		FTreeBuilder B(Sniper, Blackboard);
		UBehaviorTreeGraphNode* Sel = B.Composite(UBTComposite_Selector::StaticClass(), B.Root, 0, 150);
		B.FocusOn(Sel, HitmenBBKeys::TargetActor);

		UBehaviorTreeGraphNode* Parallel = B.Add<UBehaviorTreeGraphNode_SimpleParallel>(UBTComposite_SimpleParallel::StaticClass(), Sel, -300, 320);
		B.KeyIsSet(Parallel, HitmenBBKeys::TargetVisible, EBTFlowAbortMode::Both);
		UBehaviorTreeGraphNode* Shoot = B.Task(UBTTask_HitmenShoot::StaticClass(), Parallel, -450, 520, 0);
		SetFloat(Shoot->NodeInstance, TEXT("BurstDuration"), 2.5f);
		UBehaviorTreeGraphNode* Reposition = B.Add<UBehaviorTreeGraphNode_Composite>(UBTComposite_Sequence::StaticClass(), Parallel, -150, 520, 1);
		B.Task(UBTTask_HitmenFindFlankPosition::StaticClass(), Reposition, -250, 720);
		B.MoveTo(Reposition, HitmenBBKeys::MoveLocation, 150.f, -50, 720);

		UBehaviorTreeGraphNode* Watch = B.Composite(UBTComposite_Sequence::StaticClass(), Sel, 300, 320);
		B.Task(UBTTask_HitmenFindFlankPosition::StaticClass(), Watch, 150, 520);
		B.MoveTo(Watch, HitmenBBKeys::MoveLocation, 150.f, 350, 520);
		UBehaviorTreeGraphNode* WatchWait = B.Task(UBTTask_Wait::StaticClass(), Watch, 550, 520);
		SetFloat(WatchWait->NodeInstance, TEXT("WaitTime"), 1.f);
		B.Finish();
	}

	// ---- BT_Combat: hold position, snipers, flank + shoot, chase. Reinforcement calls live at the
	//      root (any alerted NPC that is not fighting); the cooldown is in C++ (match rules). ----
	UBehaviorTree* Combat = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_Combat"));
	{
		FTreeBuilder B(Combat, Blackboard);
		UBehaviorTreeGraphNode* Sel = B.Composite(UBTComposite_Selector::StaticClass(), B.Root, 0, 150);
		B.FocusOn(Sel, HitmenBBKeys::TargetActor);

		// Holds position: shoot while visible, otherwise keep facing the target. Never moves.
		UBehaviorTreeGraphNode* Hold = B.Composite(UBTComposite_Selector::StaticClass(), Sel, -675, 360);
		B.KeyIsSet(Hold, HitmenBBKeys::HoldPosition);
		UBehaviorTreeGraphNode* HoldShoot = B.Task(UBTTask_HitmenShoot::StaticClass(), Hold, -800, 560);
		B.KeyIsSet(HoldShoot, HitmenBBKeys::TargetVisible, EBTFlowAbortMode::Both);
		UBehaviorTreeGraphNode* HoldWait = B.Task(UBTTask_Wait::StaticClass(), Hold, -600, 560);
		SetFloat(HoldWait->NodeInstance, TEXT("WaitTime"), 0.3f);

		UBehaviorTreeGraphNode* SniperNode = B.Subtree(Sniper, Sel, -450, 360);
		B.KeyIsSet(SniperNode, HitmenBBKeys::IsSniper);

		// Seeing the target aborts the chase / look-around at once (lower priority), losing it
		// ends the shooting branch (self).
		UBehaviorTreeGraphNode* Parallel = B.Add<UBehaviorTreeGraphNode_SimpleParallel>(UBTComposite_SimpleParallel::StaticClass(), Sel, 0, 360);
		B.KeyIsSet(Parallel, HitmenBBKeys::TargetVisible, EBTFlowAbortMode::Both);
		B.Task(UBTTask_HitmenShoot::StaticClass(), Parallel, -150, 560, 0);
		// Background: close in only when out of range / out of sight (the task fails when the guard can
		// shoot from where it stands); otherwise wait a moment and check again. Never backs away.
		// Cover first (Hitman): a spot nearby that hides a crouched guard and still lets it shoot standing.
		UBehaviorTreeGraphNode* Reposition = B.Add<UBehaviorTreeGraphNode_Composite>(UBTComposite_Selector::StaticClass(), Parallel, 150, 560, 1);
		UBehaviorTreeGraphNode* Cover = B.Composite(UBTComposite_Sequence::StaticClass(), Reposition, -200, 760);
		B.Task(UBTTask_HitmenFindCover::StaticClass(), Cover, -300, 960);
		B.MoveTo(Cover, HitmenBBKeys::MoveLocation, 40.f, -100, 960);
		UBehaviorTreeGraphNode* Flank = B.Composite(UBTComposite_Sequence::StaticClass(), Reposition, 100, 760);
		B.Task(UBTTask_HitmenFindFlankPosition::StaticClass(), Flank, 50, 960);
		B.MoveTo(Flank, HitmenBBKeys::MoveLocation, 100.f, 250, 960);
		UBehaviorTreeGraphNode* HoldGround = B.Task(UBTTask_Wait::StaticClass(), Reposition, 400, 760);
		SetFloat(HoldGround->NodeInstance, TEXT("WaitTime"), 0.3f);

		UBehaviorTreeGraphNode* Chase = B.Composite(UBTComposite_Sequence::StaticClass(), Sel, 600, 360);
		B.MoveToObserved(Chase, HitmenBBKeys::LastKnownLocation, 120.f, 450, 560);
		B.Task(UBTTask_HitmenLookAround::StaticClass(), Chase, 750, 560);
		B.Finish();
	}

	// ---- BT_NPC: root, one subtree per alert state ----
	UBehaviorTree* Root = CreateOrLoadAsset<UBehaviorTree>(Folder, TEXT("BT_NPC"));
	{
		FTreeBuilder B(Root, Blackboard);
		UBehaviorTreeGraphNode* Sel = B.Composite(UBTComposite_Selector::StaticClass(), B.Root, 0, 150);

		// VIP evacuation (Hitman): an unarmed VIP in danger runs to a safe spot and stays there, looking around.
		UBehaviorTreeGraphNode* Evacuate = B.Composite(UBTComposite_Sequence::StaticClass(), Sel, -2300, 360);
		B.KeyIsSet(Evacuate, HitmenBBKeys::Evacuate, EBTFlowAbortMode::Both);
		B.Task(UBTTask_HitmenFindSafeSpot::StaticClass(), Evacuate, -2500, 560);
		B.MoveTo(Evacuate, HitmenBBKeys::MoveLocation, 120.f, -2300, 560);
		UBehaviorTreeGraphNode* Hide = B.Task(UBTTask_HitmenLookAround::StaticClass(), Evacuate, -2100, 560);
		SetFloat(Hide->NodeInstance, TEXT("Duration"), 4.f);

		// Radio call: any alerted NPC that is not fighting (C++ computes CanCallReinforcements:
		// cooldown, min alert time, no relays). Both: starts as soon as allowed, aborts the moment
		// the NPC must fight (target visible) or is suppressed; damage interrupts it in the task.
		UBehaviorTreeGraphNode* CallNode = B.Subtree(Call, Sel, -1850, 360);
		B.KeyIsSet(CallNode, HitmenBBKeys::CanCallReinforcements, EBTFlowAbortMode::Both);

		// Alert-state decorators observe AlertState with aborts Both (seeing the target preempts
		// search / investigate / patrol).
		UBehaviorTreeGraphNode* CombatNode = B.Subtree(Combat, Sel, -1400, 360);
		B.AlertIs(CombatNode, { EHitmenAlertState::Combat });

		// Holding NPCs search and investigate by looking around from their post.
		UBehaviorTreeGraphNode* HoldLook = B.Composite(UBTComposite_Sequence::StaticClass(), Sel, -950, 360);
		B.KeyIsSet(HoldLook, HitmenBBKeys::HoldPosition);
		B.AlertIs(HoldLook, { EHitmenAlertState::Searching, EHitmenAlertState::Investigating });
		B.Task(UBTTask_HitmenLookAround::StaticClass(), HoldLook, -950, 560);

		UBehaviorTreeGraphNode* SearchNode = B.Subtree(Search, Sel, -550, 360);
		B.AlertIs(SearchNode, { EHitmenAlertState::Searching });

		UBehaviorTreeGraphNode* InvestigateNode = B.Subtree(Investigate, Sel, -100, 360);
		B.AlertIs(InvestigateNode, { EHitmenAlertState::Investigating });

		UBehaviorTreeGraphNode* Suspicious = B.Composite(UBTComposite_Sequence::StaticClass(), Sel, 350, 360);
		B.AlertIs(Suspicious, { EHitmenAlertState::Suspicious });
		B.FocusOn(Suspicious, HitmenBBKeys::TargetActor);
		UBehaviorTreeGraphNode* Stare = B.Task(UBTTask_Wait::StaticClass(), Suspicious, 350, 560);
		SetFloat(Stare->NodeInstance, TEXT("WaitTime"), 0.4f);

		// Calm again after a body search: carry the body away (aborts as soon as anything else happens).
		UBehaviorTreeGraphNode* DisposeNode = B.Subtree(Dispose, Sel, 600, 360);
		B.KeyIsSet(DisposeNode, HitmenBBKeys::DisposeBody, EBTFlowAbortMode::Both);
		B.AlertIs(DisposeNode, { EHitmenAlertState::Unaware });

		UBehaviorTreeGraphNode* GuardNode = B.Subtree(Guard, Sel, 800, 360);
		B.KeyIsSet(GuardNode, HitmenBBKeys::IsStationary);

		B.Subtree(Patrol, Sel, 1200, 360);
		B.Finish();
	}
	return true;
}

void UHitmenEditorTools::SetBackgroundThrottling(bool bThrottle)
{
	GetMutableDefault<UEditorPerformanceSettings>()->bThrottleCPUWhenNotForeground = bThrottle;
}

void UHitmenEditorTools::SetPIENetMode(int32 NumPlayers)
{
	ULevelEditorPlaySettings* Settings = GetMutableDefault<ULevelEditorPlaySettings>();
	Settings->SetPlayNetMode(NumPlayers > 1 ? EPlayNetMode::PIE_ListenServer : EPlayNetMode::PIE_Standalone);
	Settings->SetPlayNumberOfClients(FMath::Max(1, NumPlayers));
	Settings->SetRunUnderOneProcess(true);
}
