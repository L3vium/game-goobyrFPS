// Hitmen — editor tools: bind anim graph asset pins to anim instance variables

#include "HitmenAnimGraphTools.h"
#include "Data/HitmenAnimSet.h"

#include "Animation/AnimBlueprint.h"
#include "Animation/AnimationAsset.h"
#include "Animation/AnimSequenceBase.h"
#include "Animation/BlendSpace.h"
#include "Animation/AimOffsetBlendSpace.h"
#include "AnimGraphNode_Base.h"
#include "AnimGraphNode_Root.h"
#include "AnimGraphNode_Slot.h"
#include "Animation/Skeleton.h"
#include "EdGraphSchema_K2.h"
#include "K2Node_VariableGet.h"
#include "EdGraph/EdGraph.h"
#include "EdGraph/EdGraphNode.h"
#include "EdGraph/EdGraphPin.h"
#include "EdGraph/EdGraphSchema.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "UObject/UnrealType.h"
#include "UObject/Package.h"

DEFINE_LOG_CATEGORY_STATIC(LogHitmenAnimGraph, Log, All);

namespace HitmenAnimGraph
{
	/** Asset properties of the supported nodes: sequence player/evaluator, blend space player/evaluator, aim offset. */
	const FName AssetPinNames[] = { FName(TEXT("Sequence")), FName(TEXT("BlendSpace")) };

	/** Placement of the created variable node, left of the anim node. */
	constexpr int32 GetNodeOffsetX = 320;
	constexpr int32 GetNodeOffsetY = 48;

	const FName* FindVariableFor(const UObject* Asset, const TMap<FString, FName>& Map)
	{
		if (!Asset)
		{
			return nullptr;
		}
		if (const FName* Found = Map.Find(Asset->GetPathName()))
		{
			return Found;
		}
		return Map.Find(Asset->GetPackage()->GetName());
	}

	FOptionalPinFromProperty* FindAssetPinOption(UAnimGraphNode_Base* Node)
	{
		for (const FName& PinName : AssetPinNames)
		{
			for (FOptionalPinFromProperty& Optional : Node->ShowPinForProperties)
			{
				if (Optional.PropertyName == PinName)
				{
					return &Optional;
				}
			}
		}
		return nullptr;
	}

	void AddAsset(TMap<FString, FName>& Map, const UObject* Asset, const TCHAR* Variable)
	{
		// First field wins when one asset fills several fields (e.g. idle reused as a fallback).
		if (Asset && !Map.Contains(Asset->GetPathName()))
		{
			Map.Add(Asset->GetPathName(), FName(Variable));
		}
	}
}

int32 UHitmenAnimGraphTools::BindAssetPinsToVariables(UAnimBlueprint* AnimBlueprint, const TMap<FString, FName>& AssetToVariable, bool bCompile)
{
	using namespace HitmenAnimGraph;

	if (!AnimBlueprint || AssetToVariable.IsEmpty())
	{
		UE_LOG(LogHitmenAnimGraph, Warning, TEXT("BindAssetPinsToVariables: no AnimBlueprint or empty map."));
		return -1;
	}

	// Variables are looked up on the compiled class (inherited UHitmenAnimInstance properties).
	UClass* SearchClass = AnimBlueprint->SkeletonGeneratedClass.Get();
	if (!SearchClass)
	{
		SearchClass = AnimBlueprint->GeneratedClass.Get();
	}
	if (!SearchClass)
	{
		SearchClass = AnimBlueprint->ParentClass.Get();
	}
	if (!SearchClass)
	{
		UE_LOG(LogHitmenAnimGraph, Warning, TEXT("BindAssetPinsToVariables: %s has no class to read variables from."), *AnimBlueprint->GetName());
		return -1;
	}

	TArray<UEdGraph*> Graphs;
	AnimBlueprint->GetAllGraphs(Graphs);

	int32 Wired = 0;
	for (UEdGraph* Graph : Graphs)
	{
		if (!Graph)
		{
			continue;
		}
		const UEdGraphSchema* Schema = Graph->GetSchema();
		// Copy: wiring adds variable nodes to the graph.
		const TArray<UEdGraphNode*> Nodes = Graph->Nodes;
		for (UEdGraphNode* GraphNode : Nodes)
		{
			UAnimGraphNode_Base* Node = Cast<UAnimGraphNode_Base>(GraphNode);
			if (!Node || !Schema)
			{
				continue;
			}
			const FName* Variable = FindVariableFor(Node->GetAnimationAsset(), AssetToVariable);
			FOptionalPinFromProperty* Option = Variable ? FindAssetPinOption(Node) : nullptr;
			if (!Option)
			{
				continue;
			}
			const FName PinName = Option->PropertyName;

			const FObjectPropertyBase* Property = FindFProperty<FObjectPropertyBase>(SearchClass, *Variable);
			if (!Property)
			{
				UE_LOG(LogHitmenAnimGraph, Warning, TEXT("%s: variable '%s' not found on %s (is the parent class UHitmenAnimInstance?)."),
					*AnimBlueprint->GetName(), *Variable->ToString(), *SearchClass->GetName());
				continue;
			}

			// Expose the asset pin.
			if (!Option->bShowPin)
			{
				Node->Modify();
				Option->bShowPin = true;
				Node->ReconstructNode();
			}
			UEdGraphPin* AssetPin = Node->FindPin(PinName, EGPD_Input);
			if (!AssetPin || AssetPin->LinkedTo.Num() > 0)
			{
				continue;
			}
			const UClass* PinClass = Cast<UClass>(AssetPin->PinType.PinSubCategoryObject.Get());
			if (PinClass && Property->PropertyClass && !Property->PropertyClass->IsChildOf(PinClass))
			{
				UE_LOG(LogHitmenAnimGraph, Warning, TEXT("%s: variable '%s' (%s) cannot feed pin %s (%s)."),
					*AnimBlueprint->GetName(), *Variable->ToString(), *Property->PropertyClass->GetName(), *PinName.ToString(), *PinClass->GetName());
				continue;
			}

			// Variable get node, left of the anim node.
			Graph->Modify();
			FGraphNodeCreator<UK2Node_VariableGet> Creator(*Graph);
			UK2Node_VariableGet* GetNode = Creator.CreateNode(false);
			GetNode->VariableReference.SetSelfMember(*Variable);
			GetNode->NodePosX = Node->NodePosX - GetNodeOffsetX;
			GetNode->NodePosY = Node->NodePosY + GetNodeOffsetY;
			Creator.Finalize();

			UEdGraphPin* ValuePin = GetNode->GetValuePin();
			if (!ValuePin || !Schema->TryCreateConnection(ValuePin, AssetPin))
			{
				UE_LOG(LogHitmenAnimGraph, Warning, TEXT("%s: could not connect '%s' to %s.%s."),
					*AnimBlueprint->GetName(), *Variable->ToString(), *Node->GetName(), *PinName.ToString());
				Graph->RemoveNode(GetNode);
				continue;
			}
			++Wired;
			UE_LOG(LogHitmenAnimGraph, Log, TEXT("%s: %s.%s <- %s"), *AnimBlueprint->GetName(), *Graph->GetName(), *PinName.ToString(), *Variable->ToString());
		}
	}

	if (Wired > 0)
	{
		FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(AnimBlueprint);
	}
	if (bCompile)
	{
		FKismetEditorUtilities::CompileBlueprint(AnimBlueprint);
	}
	AnimBlueprint->MarkPackageDirty();
	return Wired;
}

int32 UHitmenAnimGraphTools::BindAssetPinsToAnimSet(UAnimBlueprint* AnimBlueprint, UHitmenAnimSet* AnimSet, bool bCompile)
{
	using namespace HitmenAnimGraph;

	if (!AnimSet)
	{
		UE_LOG(LogHitmenAnimGraph, Warning, TEXT("BindAssetPinsToAnimSet: no anim set."));
		return -1;
	}
	// Field -> UHitmenAnimInstance variable (see UHitmenAnimInstance::ResolveAnimSet).
	TMap<FString, FName> Map;
	AddAsset(Map, AnimSet->Locomotion, TEXT("LocomotionBlendSpace"));
	AddAsset(Map, AnimSet->Idle, TEXT("IdleAnim"));
	AddAsset(Map, AnimSet->JumpStart, TEXT("JumpStartAnim"));
	AddAsset(Map, AnimSet->FallLoop, TEXT("JumpLoopAnim"));
	AddAsset(Map, AnimSet->Land, TEXT("LandAnim"));
	AddAsset(Map, AnimSet->CrouchIdle, TEXT("CrouchIdleAnim"));
	AddAsset(Map, AnimSet->CrouchLocomotion, TEXT("CrouchLocomotionBlendSpace"));
	AddAsset(Map, AnimSet->Slide, TEXT("SlideAnim"));
	AddAsset(Map, AnimSet->LadderClimb, TEXT("LadderClimbAnim"));
	AddAsset(Map, AnimSet->ZiplineHang, TEXT("ZiplineAnim"));
	AddAsset(Map, AnimSet->StunnedLoop, TEXT("StunnedLoopAnim"));
	AddAsset(Map, AnimSet->SleepLoop, TEXT("AsleepLoopAnim"));
	AddAsset(Map, AnimSet->CarryBodyPose, TEXT("CarryBodyAnim"));
	AddAsset(Map, AnimSet->AimOffset, TEXT("AimOffsetBlendSpace"));
	return BindAssetPinsToVariables(AnimBlueprint, Map, bCompile);
}

bool UHitmenAnimGraphTools::EnsureSlotBeforeOutput(UAnimBlueprint* AnimBlueprint, FName SlotName, bool bCompile)
{
	using namespace HitmenAnimGraph;

	if (!AnimBlueprint)
	{
		return false;
	}
	UEdGraph* AnimGraph = nullptr;
	for (UEdGraph* Graph : AnimBlueprint->FunctionGraphs)
	{
		if (Graph && Graph->GetFName() == UEdGraphSchema_K2::GN_AnimGraph)
		{
			AnimGraph = Graph;
			break;
		}
	}
	if (!AnimGraph || !AnimGraph->GetSchema())
	{
		UE_LOG(LogHitmenAnimGraph, Warning, TEXT("EnsureSlotBeforeOutput: %s has no AnimGraph."), *AnimBlueprint->GetName());
		return false;
	}

	UAnimGraphNode_Root* Root = nullptr;
	for (UEdGraphNode* Node : AnimGraph->Nodes)
	{
		if (Cast<UAnimGraphNode_Slot>(Node))
		{
			return true; // Already has a slot: nothing to do.
		}
		Root = Root ? Root : Cast<UAnimGraphNode_Root>(Node);
	}
	if (!Root)
	{
		UE_LOG(LogHitmenAnimGraph, Warning, TEXT("EnsureSlotBeforeOutput: %s has no Output Pose node."), *AnimBlueprint->GetName());
		return false;
	}

	auto FirstPin = [](UEdGraphNode* Node, EEdGraphPinDirection Direction) -> UEdGraphPin*
	{
		for (UEdGraphPin* Pin : Node->Pins)
		{
			if (Pin && Pin->Direction == Direction && !Pin->bHidden)
			{
				return Pin;
			}
		}
		return nullptr;
	};

	UEdGraphPin* RootInput = FirstPin(Root, EGPD_Input);
	if (!RootInput)
	{
		return false;
	}
	UEdGraphPin* Previous = RootInput->LinkedTo.Num() > 0 ? RootInput->LinkedTo[0] : nullptr;

	AnimGraph->Modify();
	FGraphNodeCreator<UAnimGraphNode_Slot> Creator(*AnimGraph);
	UAnimGraphNode_Slot* Slot = Creator.CreateNode(false);
	Slot->Node.SlotName = SlotName.IsNone() ? FAnimSlotGroup::DefaultSlotName : SlotName;
	Slot->NodePosX = Root->NodePosX - GetNodeOffsetX;
	Slot->NodePosY = Root->NodePosY;
	Creator.Finalize();

	UEdGraphPin* SlotInput = Slot->FindPin(TEXT("Source"), EGPD_Input);
	SlotInput = SlotInput ? SlotInput : FirstPin(Slot, EGPD_Input);
	UEdGraphPin* SlotOutput = FirstPin(Slot, EGPD_Output);
	const UEdGraphSchema* Schema = AnimGraph->GetSchema();
	if (!SlotInput || !SlotOutput)
	{
		AnimGraph->RemoveNode(Slot);
		return false;
	}
	if (Previous)
	{
		Schema->BreakSinglePinLink(Previous, RootInput);
		Schema->TryCreateConnection(Previous, SlotInput);
	}
	Schema->TryCreateConnection(SlotOutput, RootInput);
	UE_LOG(LogHitmenAnimGraph, Log, TEXT("%s: inserted Slot '%s' before the Output Pose."), *AnimBlueprint->GetName(), *Slot->Node.SlotName.ToString());

	FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(AnimBlueprint);
	if (bCompile)
	{
		FKismetEditorUtilities::CompileBlueprint(AnimBlueprint);
	}
	AnimBlueprint->MarkPackageDirty();
	return true;
}
