// Hitmen — Blueprint graph layout tools

#include "HitmenBlueprintGraphTools.h"
#include "Engine/Blueprint.h"
#include "EdGraph/EdGraph.h"
#include "EdGraphNode_Comment.h"
#include "EdGraphSchema_K2.h"
#include "K2Node_Event.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "GameFramework/Actor.h"

const FLinearColor UHitmenBlueprintGraphTools::InitColor(0.15f, 0.55f, 0.20f);
const FLinearColor UHitmenBlueprintGraphTools::JuiceColor(0.55f, 0.20f, 0.70f);

namespace HitmenGraphLayout
{
	const FName JuiceGraph(TEXT("Juice"));
	const FName InitGraph(TEXT("Init"));
	const TCHAR* JuicePrefix = TEXT("Juice_On");
	constexpr int32 EventSpacingY = 180;
	constexpr int32 CommentPadding = 60;
	constexpr int32 CommentWidth = 520;

	UK2Node_Event* AddOverrideEvent(UEdGraph* Graph, UFunction* Function, int32 PosX, int32 PosY)
	{
		UClass* OwnerClass = Function->GetOwnerClass();
		UK2Node_Event* EventNode = NewObject<UK2Node_Event>(Graph);
		EventNode->EventReference.SetExternalMember(Function->GetFName(), OwnerClass);
		EventNode->bOverrideFunction = true;
		EventNode->CreateNewGuid();
		EventNode->PostPlacedNewNode();
		EventNode->NodePosX = PosX;
		EventNode->NodePosY = PosY;
		EventNode->AllocateDefaultPins();
		Graph->AddNode(EventNode, /*bFromUI*/ false, /*bSelectNewNode*/ false);
		return EventNode;
	}

	/** Greyed default nodes Unreal places in a new EventGraph (BeginPlay, ActorBeginOverlap, Tick). */
	void RemoveGhostDefaultEvents(UBlueprint* Blueprint)
	{
		for (UEdGraph* Graph : Blueprint->UbergraphPages)
		{
			TArray<UEdGraphNode*> Ghosts;
			for (UEdGraphNode* Node : Graph->Nodes)
			{
				if (UK2Node_Event* Event = Cast<UK2Node_Event>(Node); Event && Event->IsAutomaticallyPlacedGhostNode())
				{
					Ghosts.Add(Event);
				}
			}
			for (UEdGraphNode* Ghost : Ghosts)
			{
				FBlueprintEditorUtils::RemoveNode(Blueprint, Ghost, /*bDontRecompile*/ true);
			}
		}
	}
}

UEdGraph* UHitmenBlueprintGraphTools::FindOrAddGraph(UBlueprint* Blueprint, FName GraphName)
{
	if (!Blueprint)
	{
		return nullptr;
	}
	for (UEdGraph* Graph : Blueprint->UbergraphPages)
	{
		if (Graph && Graph->GetFName() == GraphName)
		{
			return Graph;
		}
	}
	UEdGraph* Graph = FBlueprintEditorUtils::CreateNewGraph(Blueprint, GraphName, UEdGraph::StaticClass(), UEdGraphSchema_K2::StaticClass());
	FBlueprintEditorUtils::AddUbergraphPage(Blueprint, Graph);
	return Graph;
}

UEdGraphNode_Comment* UHitmenBlueprintGraphTools::AddCommentBox(UBlueprint* Blueprint, FName GraphName, const FString& Text, FLinearColor Color, FVector2D Position, FVector2D Size)
{
	UEdGraph* Graph = FindOrAddGraph(Blueprint, GraphName);
	if (!Graph)
	{
		return nullptr;
	}
	UEdGraphNode_Comment* Comment = NewObject<UEdGraphNode_Comment>(Graph);
	Comment->NodeComment = Text;
	Comment->CommentColor = Color;
	Comment->NodePosX = FMath::RoundToInt(Position.X);
	Comment->NodePosY = FMath::RoundToInt(Position.Y);
	Comment->NodeWidth = FMath::RoundToInt(Size.X);
	Comment->NodeHeight = FMath::RoundToInt(Size.Y);
	Comment->bCommentBubbleVisible = false;
	Comment->CreateNewGuid();
	Comment->PostPlacedNewNode();
	Graph->AddNode(Comment, false, false);
	FBlueprintEditorUtils::MarkBlueprintAsModified(Blueprint);
	return Comment;
}

int32 UHitmenBlueprintGraphTools::LayOutConventionGraphs(UBlueprint* Blueprint, bool bCompile)
{
	using namespace HitmenGraphLayout;
	if (!Blueprint || !Blueprint->ParentClass)
	{
		return -1;
	}

	// A Blueprint child of another Blueprint inherits its parent's Juice / Init graphs: stubs here
	// would silently replace the parent's juice (and an empty BeginPlay would skip the parent's).
	if (!Blueprint->ParentClass->HasAnyClassFlags(CLASS_Native))
	{
		bool bRemoved = false;
		for (const FName GraphName : { JuiceGraph, InitGraph })
		{
			for (UEdGraph* Graph : TArray<UEdGraph*>(Blueprint->UbergraphPages))
			{
				if (Graph && Graph->GetFName() == GraphName)
				{
					FBlueprintEditorUtils::RemoveGraph(Blueprint, Graph, EGraphRemoveFlags::Recompile);
					bRemoved = true;
				}
			}
		}
		if (bRemoved && bCompile)
		{
			FKismetEditorUtilities::CompileBlueprint(Blueprint);
		}
		return 0;
	}

	// Juice hooks declared by the native parents, in declaration order (most derived last).
	TArray<UFunction*> JuiceEvents;
	TSet<FName> Seen;
	for (TFieldIterator<UFunction> It(Blueprint->ParentClass, EFieldIteratorFlags::IncludeSuper); It; ++It)
	{
		UFunction* Function = *It;
		bool bAlreadySeen = false;
		Seen.Add(Function->GetFName(), &bAlreadySeen);
		if (!bAlreadySeen && Function->GetOwnerClass()->HasAnyClassFlags(CLASS_Native)
			&& Function->HasAnyFunctionFlags(FUNC_BlueprintEvent) && !Function->HasAnyFunctionFlags(FUNC_Native)
			&& Function->GetName().StartsWith(JuicePrefix)
			&& !FBlueprintEditorUtils::FindOverrideForFunction(Blueprint, Function->GetOwnerClass(), Function->GetFName()))
		{
			JuiceEvents.AddUnique(Function);
		}
	}

	int32 Added = 0;
	if (JuiceEvents.Num() > 0)
	{
		UEdGraph* Juice = FindOrAddGraph(Blueprint, JuiceGraph);
		// New stubs go below whatever the graph already holds.
		int32 StartY = 0;
		for (const UEdGraphNode* Node : Juice->Nodes)
		{
			StartY = FMath::Max(StartY, Node->NodePosY + EventSpacingY);
		}
		for (int32 Index = 0; Index < JuiceEvents.Num(); ++Index)
		{
			AddOverrideEvent(Juice, JuiceEvents[Index], 0, StartY + Index * EventSpacingY);
			++Added;
		}
		AddCommentBox(Blueprint, JuiceGraph,
			TEXT("JUICE - cosmetic only (VFX, SFX, camera shake, UI feedback). Runs on every machine that sees the event; never put gameplay here."),
			JuiceColor, FVector2D(-CommentPadding, StartY - CommentPadding - 40),
			FVector2D(CommentWidth, JuiceEvents.Num() * EventSpacingY + CommentPadding * 2));
	}

	// Actors: an Init graph with BeginPlay instead of the greyed defaults in the EventGraph.
	if (Blueprint->ParentClass->IsChildOf(AActor::StaticClass()))
	{
		RemoveGhostDefaultEvents(Blueprint);
		UFunction* BeginPlay = AActor::StaticClass()->FindFunctionByName(FName(TEXT("ReceiveBeginPlay")));
		if (BeginPlay && !FBlueprintEditorUtils::FindOverrideForFunction(Blueprint, AActor::StaticClass(), BeginPlay->GetFName()))
		{
			UEdGraph* Init = FindOrAddGraph(Blueprint, InitGraph);
			AddOverrideEvent(Init, BeginPlay, 0, 0);
			AddCommentBox(Blueprint, InitGraph,
				TEXT("INIT - BeginPlay: bind delegates here with Create Event (C++ already binds its own at BeginPlay)."),
				InitColor, FVector2D(-CommentPadding, -CommentPadding - 40), FVector2D(CommentWidth, EventSpacingY + CommentPadding * 2));
		}
	}

	FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(Blueprint);
	if (bCompile)
	{
		FKismetEditorUtilities::CompileBlueprint(Blueprint);
	}
	return Added;
}
