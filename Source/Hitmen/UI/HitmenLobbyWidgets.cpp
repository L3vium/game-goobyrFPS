// Hitmen — lobby screens

#include "UI/HitmenLobbyWidgets.h"
#include "UI/HitmenUIFactory.h"
#include "UI/HitmenMenuKit.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenGameInstance.h"
#include "Framework/HitmenAgentPreview.h"
#include "Core/HitmenGameSettings.h"
#include "Core/HitmenUserSettings.h"
#include "Data/HitmenCatalog.h"
#include "Data/HitmenMapDefinition.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenAbilityData.h"
#include "Data/HitmenCosmeticItem.h"
#include "Data/HitmenSoundSet.h"
#include "Blueprint/WidgetTree.h"
#include "Blueprint/WidgetBlueprintLibrary.h"
#include "Blueprint/WidgetLayoutLibrary.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/WidgetSwitcher.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/ScrollBox.h"
#include "Components/SizeBox.h"
#include "Components/TextBlock.h"
#include "Components/EditableTextBox.h"
#include "Components/SkeletalMeshComponent.h"
#include "Framework/Application/SlateApplication.h"
#include "Engine/Texture2D.h"
#include "EngineUtils.h"
#include "Engine/World.h"

#define LOCTEXT_NAMESPACE "HitmenLobby"

namespace HitmenLobby
{
	/** Sends the choice to the server and keeps it on the game instance for later travels. */
	void SendLoadout(AHitmenPlayerController* PC, const FHitmenLoadout& Loadout)
	{
		if (UHitmenGameInstance* GI = PC->GetGameInstance<UHitmenGameInstance>())
		{
			GI->LocalLoadout = Loadout;
			GI->bHasLocalLoadout = true;
		}
		PC->Server_SetLoadout(Loadout);
	}

	void SendCosmetics(AHitmenPlayerController* PC, const FHitmenCosmeticLoadout& Cosmetics)
	{
		if (UHitmenGameInstance* GI = PC->GetGameInstance<UHitmenGameInstance>())
		{
			GI->LocalCosmetics = Cosmetics;
			GI->bHasLocalCosmetics = true;
		}
		PC->Server_SetCosmetics(Cosmetics);
	}
}

namespace
{
	template <typename T>
	T* CycleIn(const TArray<T*>& Items, T* Current, int32 Direction, bool bAllowNone)
	{
		const int32 Count = Items.Num() + (bAllowNone ? 1 : 0);
		if (Count == 0)
		{
			return nullptr;
		}
		int32 Index = Items.IndexOfByKey(Current);
		if (Index == INDEX_NONE)
		{
			Index = bAllowNone ? Items.Num() : 0;
		}
		Index = (Index + Direction + Count) % Count;
		return Items.IsValidIndex(Index) ? Items[Index] : nullptr;
	}

	FText DifficultyName(EHitmenDifficulty Difficulty)
	{
		switch (Difficulty)
		{
		case EHitmenDifficulty::Easy: return LOCTEXT("Easy", "EASY");
		case EHitmenDifficulty::Hard: return LOCTEXT("Hard", "HARD");
		default: return LOCTEXT("Normal", "NORMAL");
		}
	}

	UTextBlock* MakeSectionLabel(UWidgetTree* Tree, const FText& Text)
	{
		UTextBlock* Label = HitmenUI::MakeText(Tree, Text, 15, TEXT("Medium"), HitmenUI::TextDim());
		Label->SetFont(HitmenUI::SpacedFont(15, TEXT("Medium"), 260));
		return Label;
	}

	const EHitmenCosmeticSlot LookSlots[3] = { EHitmenCosmeticSlot::Head, EHitmenCosmeticSlot::Body, EHitmenCosmeticSlot::Legs };
	const TCHAR* LookBones[3] = { TEXT("head"), TEXT("spine_03"), TEXT("calf_l") };
}

// =============================================================================================
// Player entry
// =============================================================================================

void UHitmenPlayerEntryWidget::BuildDefaultLayout()
{
	USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	Size->SetHeightOverride(52.f);
	Size->SetMinDesiredWidth(420.f);
	WidgetTree->RootWidget = Size;
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenPlayerEntryWidget::SetPlayer(AHitmenPlayerState* InPlayerState)
{
	PlayerState = InPlayerState;
}

bool UHitmenPlayerEntryWidget::IsLocalPlayer() const
{
	return PlayerState && GetOwningPlayer() && GetOwningPlayer()->PlayerState == PlayerState;
}

bool UHitmenPlayerEntryWidget::CanToggleWatcher() const
{
	const AHitmenPlayerController* PC = GetHitmenPC();
	return PlayerState && !bShowReady && PlayerState->GetLobbyTeamId() == INDEX_NONE && PC && (PC->IsHost() || IsLocalPlayer());
}

FBox2D UHitmenPlayerEntryWidget::GetWatcherBox(const FGeometry& Geometry) const
{
	const FVector2D Size = Geometry.GetLocalSize();
	return FBox2D(FVector2D(Size.X - 150.f, 0.f), Size);
}

void UHitmenPlayerEntryWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HoverAlpha = FMath::FInterpTo(HoverAlpha, bHovered ? 1.f : 0.f, InDeltaTime, 14.f);
}

void UHitmenPlayerEntryWidget::NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseEnter(InGeometry, InMouseEvent);
	bHovered = !bDragVisual;
}

void UHitmenPlayerEntryWidget::NativeOnMouseLeave(const FPointerEvent& InMouseEvent)
{
	Super::NativeOnMouseLeave(InMouseEvent);
	bHovered = false;
}

FReply UHitmenPlayerEntryWidget::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	if (bDragVisual || InMouseEvent.GetEffectingButton() != EKeys::LeftMouseButton)
	{
		return FReply::Unhandled();
	}
	const FVector2D Local = InGeometry.AbsoluteToLocal(InMouseEvent.GetScreenSpacePosition());
	if (CanToggleWatcher() && GetWatcherBox(InGeometry).IsInside(Local))
	{
		if (AHitmenPlayerController* PC = GetHitmenPC())
		{
			PC->Server_SetWatcher(PlayerState, !PlayerState->IsWatcher());
			UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIClick);
		}
		return FReply::Handled();
	}
	// The host drags the whole row onto a team.
	if (const AHitmenPlayerController* PC = GetHitmenPC(); PC && PC->IsHost() && !bShowReady)
	{
		return UWidgetBlueprintLibrary::DetectDragIfPressed(InMouseEvent, this, EKeys::LeftMouseButton).NativeReply;
	}
	return FReply::Unhandled();
}

void UHitmenPlayerEntryWidget::NativeOnDragDetected(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent, UDragDropOperation*& OutOperation)
{
	UHitmenPlayerDragOperation* Operation = NewObject<UHitmenPlayerDragOperation>(this);
	Operation->PlayerState = PlayerState;
	UHitmenPlayerEntryWidget* Visual = CreateWidget<UHitmenPlayerEntryWidget>(GetOwningPlayer(), GetClass());
	Visual->SetPlayer(PlayerState);
	Visual->SetAccent(Accent);
	Visual->SetDragVisual(true);
	Visual->SetRenderOpacity(0.85f);
	Visual->SetDesiredSizeInViewport(InGeometry.GetLocalSize());
	Operation->DefaultDragVisual = Visual;
	Operation->Pivot = EDragPivot::MouseDown;
	OutOperation = Operation;
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIHover);
}

int32 UHitmenPlayerEntryWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	if (!PlayerState)
	{
		return LayerId;
	}
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const FLinearColor Back = bDragVisual ? FLinearColor(0.06f, 0.06f, 0.07f, 0.95f) : FLinearColor(0.f, 0.f, 0.f, 0.45f + 0.15f * HoverAlpha);
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, Back);
	HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, FVector2D(5.f, Size.Y), Accent);
	if (bDragVisual)
	{
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, Size, HitmenUI::Accent(), 2.f);
	}
	const FSlateFontInfo NameFont = HitmenUI::SpacedFont(22, TEXT("SemiBold"), 30);
	// Long names are shortened so the tags and the right-hand box never overlap them.
	FString Name = PlayerState->GetPlayerName();
	const AHitmenGameState* NameGS = GetHitmenGameState();
	const bool bHostRow = NameGS && NameGS->PlayerArray.Num() > 0 && NameGS->PlayerArray[0] == PlayerState;
	const float NameRoom = Size.X - 20.f - 150.f - (IsLocalPlayer() ? 52.f : 0.f) - (bHostRow ? 64.f : 0.f);
	if (HitmenUI::MeasureText(Name, NameFont).X > NameRoom)
	{
		while (Name.Len() > 3 && HitmenUI::MeasureText(Name + TEXT("..."), NameFont).X > NameRoom)
		{
			Name.LeftChopInline(1);
		}
		Name += TEXT("...");
	}
	const FVector2D NameSize = HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Name, FVector2D(20.f, (Size.Y - 28.f) * 0.5f), NameFont, HitmenUI::Text());
	// Tags: YOU / HOST.
	float TagX = 20.f + NameSize.X + 12.f;
	const FSlateFontInfo TagFont = HitmenUI::SpacedFont(12, TEXT("Medium"), 160);
	auto Tag = [&](const FString& Text, const FLinearColor& Color)
	{
		const FVector2D TagSize = HitmenUI::MeasureText(Text, TagFont);
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 2, AllottedGeometry, FVector2D(TagX, (Size.Y - 20.f) * 0.5f), FVector2D(TagSize.X + 12.f, 20.f), Color, 1.f);
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Text, FVector2D(TagX + 6.f, (Size.Y - TagSize.Y) * 0.5f), TagFont, Color);
		TagX += TagSize.X + 20.f;
	};
	if (IsLocalPlayer())
	{
		Tag(TEXT("YOU"), HitmenUI::Text());
	}
	if (const AHitmenGameState* GS = GetHitmenGameState(); GS && GS->PlayerArray.Num() > 0 && GS->PlayerArray[0] == PlayerState)
	{
		Tag(TEXT("HOST"), HitmenUI::TextDim());
	}
	// Right side: watcher box (no team) or ready state (agent screen).
	const FSlateFontInfo SideFont = HitmenUI::SpacedFont(15, TEXT("SemiBold"), 140);
	if (bShowReady)
	{
		const bool bReady = PlayerState->IsReady();
		const FString Text = PlayerState->IsWatcher() ? FString(TEXT("WATCHER")) : (bReady ? FString(TEXT("READY")) : FString(TEXT("CHOOSING")));
		const FVector2D TextSize = HitmenUI::MeasureText(Text, SideFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Text, FVector2D(Size.X - TextSize.X - 18.f, (Size.Y - TextSize.Y) * 0.5f), SideFont,
			bReady ? FLinearColor(0.35f, 0.9f, 0.45f, 1.f) : HitmenUI::TextDim());
	}
	else if (PlayerState->GetLobbyTeamId() == INDEX_NONE)
	{
		const bool bEditable = CanToggleWatcher();
		const float Box = 18.f;
		const FVector2D BoxPos(Size.X - 140.f, (Size.Y - Box) * 0.5f);
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 2, AllottedGeometry, BoxPos, FVector2D(Box), FLinearColor(1.f, 1.f, 1.f, bEditable ? 0.7f : 0.3f), 1.5f);
		if (PlayerState->IsWatcher())
		{
			HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, BoxPos + FVector2D(4.f), FVector2D(Box - 8.f), HitmenUI::Accent());
		}
		const FVector2D TextSize = HitmenUI::MeasureText(TEXT("WATCHER"), SideFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, TEXT("WATCHER"), BoxPos + FVector2D(Box + 10.f, (Box - TextSize.Y) * 0.5f), SideFont,
			bEditable ? HitmenUI::Text() : HitmenUI::TextDim());
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Team panel
// =============================================================================================

void UHitmenTeamPanelWidget::BuildDefaultLayout()
{
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	WidgetTree->RootWidget = Column;
	UHorizontalBox* Header = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	TitleText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 20, TEXT("SemiBold"));
	TitleText->SetFont(HitmenUI::SpacedFont(20, TEXT("SemiBold"), 160));
	if (UHorizontalBoxSlot* TitleSlot = Header->AddChildToHorizontalBox(TitleText))
	{
		TitleSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
		TitleSlot->SetVerticalAlignment(VAlign_Center);
		TitleSlot->SetPadding(FMargin(14.f, 0.f, 0.f, 0.f));
	}
	RoleButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	RoleButton->Setup(FText::GetEmpty(), EHitmenMenuButtonKind::Back);
	RoleButton->OnClicked.AddDynamic(this, &UHitmenTeamPanelWidget::HandleRoleClicked);
	HitmenUI::AddToHBox(Header, RoleButton);
	RemoveButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	RemoveButton->Setup(LOCTEXT("RemoveTeam", "Remove"), EHitmenMenuButtonKind::Back);
	RemoveButton->OnClicked.AddDynamic(this, &UHitmenTeamPanelWidget::HandleRemoveClicked);
	HitmenUI::AddToHBox(Header, RemoveButton);
	HitmenUI::AddToVBox(Column, Header, FMargin(0.f, 6.f, 0.f, 4.f));
	MemberList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Column, MemberList, FMargin(10.f, 0.f, 10.f, 10.f));
	SetVisibility(ESlateVisibility::Visible);
}

void UHitmenTeamPanelWidget::SetTeam(int32 InTeamId)
{
	TeamId = InTeamId;
}

void UHitmenTeamPanelWidget::Refresh(bool bShowReady)
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const AHitmenPlayerController* PC = GetHitmenPC();
	const bool bHost = PC && PC->IsHost();
	if (!GS || !MemberList)
	{
		return;
	}
	TeamColor = FLinearColor::White;
	if (TeamId == INDEX_NONE)
	{
		TitleText->SetText(LOCTEXT("Unassigned", "NO TEAM"));
		RoleButton->SetVisibility(ESlateVisibility::Collapsed);
		RemoveButton->SetVisibility(ESlateVisibility::Collapsed);
	}
	else if (const FHitmenTeam* Team = GS->FindTeam(TeamId))
	{
		TeamColor = Team->Color;
		TitleText->SetText(FText::FromString(Team->DisplayName.ToString().ToUpper()));
		TitleText->SetColorAndOpacity(FSlateColor(TeamColor));
		RoleButton->SetLabel(Team->Role == EHitmenTeamRole::Protect ? LOCTEXT("Protect", "Protect") : LOCTEXT("Attack", "Attack"));
		RoleButton->SetButtonEnabled(bHost && !bShowReady);
		RoleButton->SetVisibility(ESlateVisibility::Visible);
		RemoveButton->SetVisibility(bHost && !bShowReady ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	}
	MemberList->ClearChildren();
	for (AHitmenPlayerState* PS : GS->GetPlayersInTeam(TeamId))
	{
		UHitmenPlayerEntryWidget* Entry = CreateWidget<UHitmenPlayerEntryWidget>(GetOwningPlayer(), UHitmenPlayerEntryWidget::StaticClass());
		Entry->SetPlayer(PS);
		Entry->SetAccent(TeamColor);
		Entry->SetShowReady(bShowReady);
		HitmenUI::AddToVBox(MemberList, Entry, FMargin(0.f, 2.f));
	}
	if (MemberList->GetChildrenCount() == 0)
	{
		UTextBlock* Hint = HitmenUI::MakeText(WidgetTree, bHost ? LOCTEXT("DropHere", "Drag players here") : LOCTEXT("Empty", "Empty"), 17, TEXT("Italic"), HitmenUI::TextDim());
		HitmenUI::AddToVBox(MemberList, Hint, FMargin(10.f, 8.f));
	}
}

int32 UHitmenTeamPanelWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, FLinearColor(1.f, 1.f, 1.f, bDropHover ? 0.08f : 0.03f));
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, FVector2D(Size.X, 2.f), TeamColor * FLinearColor(1, 1, 1, 0.8f));
	if (bDropHover)
	{
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, Size, HitmenUI::Accent(), 2.f);
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}

void UHitmenTeamPanelWidget::NativeOnDragEnter(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation)
{
	Super::NativeOnDragEnter(InGeometry, InDragDropEvent, InOperation);
	bDropHover = Cast<UHitmenPlayerDragOperation>(InOperation) != nullptr;
}

void UHitmenTeamPanelWidget::NativeOnDragLeave(const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation)
{
	Super::NativeOnDragLeave(InDragDropEvent, InOperation);
	bDropHover = false;
}

bool UHitmenTeamPanelWidget::NativeOnDrop(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation)
{
	bDropHover = false;
	const UHitmenPlayerDragOperation* Operation = Cast<UHitmenPlayerDragOperation>(InOperation);
	AHitmenPlayerController* PC = GetHitmenPC();
	if (!Operation || !Operation->PlayerState || !PC)
	{
		return false;
	}
	PC->Server_AssignTeam(Operation->PlayerState, TeamId);
	UHitmenSoundLibrary::PlayHitmenSound2D(this, EHitmenSound::UIConfirm);
	return true;
}

void UHitmenTeamPanelWidget::HandleRoleClicked()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const FHitmenTeam* Team = GS ? GS->FindTeam(TeamId) : nullptr;
	if (AHitmenPlayerController* PC = GetHitmenPC(); PC && Team)
	{
		PC->Server_SetTeamRole(TeamId, Team->Role == EHitmenTeamRole::Protect ? EHitmenTeamRole::Attack : EHitmenTeamRole::Protect);
	}
}

void UHitmenTeamPanelWidget::HandleRemoveClicked()
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_RemoveTeam(TeamId);
	}
}

// =============================================================================================
// Map tile
// =============================================================================================

void UHitmenMapTileWidget::SetMap(const UHitmenMapDefinition* Map, int32 Position, int32 Count)
{
	MapPosition = Position;
	MapCount = Count;
	bHasImage = false;
	MapName = Map ? Map->DisplayName : FText::GetEmpty();
	Description = Map ? Map->Description : FText::GetEmpty();
	UTexture2D* Texture = Map ? Map->MapTexture.LoadSynchronous() : nullptr;
	if (!Texture && Map)
	{
		Texture = Map->Thumbnail.LoadSynchronous();
	}
	if (Texture)
	{
		MapBrush.SetResourceObject(Texture);
		MapBrush.ImageSize = FVector2D(Texture->GetSizeX(), Texture->GetSizeY());
		bHasImage = true;
	}
}

int32 UHitmenMapTileWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, FLinearColor(0.07f, 0.075f, 0.09f, 0.95f));
	if (bHasImage)
	{
		// Square map, centred and filling the height; the tile's sides stay dark.
		const float Side = Size.Y;
		HitmenUI::PaintImage(OutDrawElements, LayerId + 1, AllottedGeometry, &MapBrush, FVector2D((Size.X - Side) * 0.5f, 0.f), FVector2D(Side));
	}
	// Caption over a bottom gradient.
	HitmenUI::PaintGradient(OutDrawElements, LayerId + 2, AllottedGeometry, FVector2D(0.f, Size.Y * 0.55f), FVector2D(Size.X, Size.Y * 0.45f), FLinearColor(0, 0, 0, 0), FLinearColor(0, 0, 0, 0.9f), false);
	const FSlateFontInfo NameFont = HitmenUI::SpacedFont(40, TEXT("Bold"), 20);
	HitmenUI::PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, MapName.ToString().ToUpper(), FVector2D(24.f, Size.Y - 92.f), NameFont, HitmenUI::Text());
	HitmenUI::PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, Description.ToString(), FVector2D(26.f, Size.Y - 42.f), HitmenUI::Font(18, TEXT("Italic")), HitmenUI::TextDim());
	if (MapCount > 1)
	{
		const FString Counter = FString::Printf(TEXT("%02d / %02d"), MapPosition + 1, MapCount);
		const FSlateFontInfo CounterFont = HitmenUI::SpacedFont(15, TEXT("Medium"), 200);
		const FVector2D CounterSize = HitmenUI::MeasureText(Counter, CounterFont);
		HitmenUI::PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, Counter, FVector2D(Size.X - CounterSize.X - 22.f, 18.f), CounterFont, HitmenUI::Text());
	}
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 3, AllottedGeometry, FVector2D::ZeroVector, Size, HitmenUI::Hairline(), 1.f);
	HitmenUI::PaintBox(OutDrawElements, LayerId + 3, AllottedGeometry, FVector2D(0.f, Size.Y - 4.f), FVector2D(Size.X * 0.25f, 4.f), HitmenUI::Accent());
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Lobby widget: layout
// =============================================================================================

void UHitmenLobbyWidget::BuildDefaultLayout()
{
	ScreenSwitcher = WidgetTree->ConstructWidget<UWidgetSwitcher>(UWidgetSwitcher::StaticClass());
	WidgetTree->RootWidget = ScreenSwitcher;

	// ================= Create room =================
	UCanvasPanel* Room = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	ScreenSwitcher->AddChild(Room);
	UVerticalBox* Left = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UCanvasPanelSlot* LeftSlot = HitmenUI::AddToCanvas(Room, Left, FAnchors(0.f, 0.f, 0.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	LeftSlot->SetOffsets(FMargin(110.f, 80.f, 860.f, 130.f));
	HitmenUI::AddToVBox(Left, MakeSectionLabel(WidgetTree, LOCTEXT("RoomOverline", "MULTIPLAYER ROOM")));
	UTextBlock* RoomTitle = HitmenUI::MakeText(WidgetTree, LOCTEXT("CreateRoom", "CREATE ROOM"), 76, TEXT("Bold"));
	HitmenUI::AddToVBox(Left, RoomTitle, FMargin(0.f, -10.f, 0.f, 16.f));

	// Map tile with arrows on its sides.
	UCanvasPanel* MapArea = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	USizeBox* MapSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	MapSize->SetWidthOverride(840.f);
	MapSize->SetHeightOverride(470.f);
	MapSize->SetContent(MapArea);
	HitmenUI::AddToVBox(Left, MapSize, FMargin(0.f, 0.f, 0.f, 22.f), false, HAlign_Left);
	MapTile = WidgetTree->ConstructWidget<UHitmenMapTileWidget>(UHitmenMapTileWidget::StaticClass());
	UCanvasPanelSlot* TileSlot = HitmenUI::AddToCanvas(MapArea, MapTile, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	TileSlot->SetOffsets(FMargin(0.f));
	PrevMapButton = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
	PrevMapButton->Setup(false, 56.f);
	PrevMapButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::PreviousMap);
	HitmenUI::AddToCanvas(MapArea, PrevMapButton, FAnchors(0.f, 0.5f), FVector2D(16.f, 0.f), FVector2D(0.f, 0.5f), FVector2D(56.f, 56.f));
	NextMapButton = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
	NextMapButton->Setup(true, 56.f);
	NextMapButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::NextMap);
	HitmenUI::AddToCanvas(MapArea, NextMapButton, FAnchors(1.f, 0.5f), FVector2D(-16.f, 0.f), FVector2D(1.f, 0.5f), FVector2D(56.f, 56.f));

	// Options: proximity chat and difficulty only (the rest is set by designers).
	HitmenUI::AddToVBox(Left, MakeSectionLabel(WidgetTree, LOCTEXT("OptionsHeader", "OPTIONS")), FMargin(0.f, 0.f, 0.f, 10.f));
	ProximityToggle = WidgetTree->ConstructWidget<UHitmenToggle>(UHitmenToggle::StaticClass());
	ProximityToggle->Setup(LOCTEXT("ProximityChat", "Proximity voice chat"), true, 20);
	ProximityToggle->OnToggled.AddDynamic(this, &UHitmenLobbyWidget::SetProximityChat);
	HitmenUI::AddToVBox(Left, ProximityToggle, FMargin(0.f, 0.f, 0.f, 10.f), false, HAlign_Left);
	UHorizontalBox* OptionsRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UTextBlock* DifficultyLabel = HitmenUI::MakeText(WidgetTree, LOCTEXT("Difficulty", "DIFFICULTY"), 20, TEXT("SemiBold"));
	DifficultyLabel->SetFont(HitmenUI::SpacedFont(20, TEXT("SemiBold"), 60));
	HitmenUI::AddToHBox(OptionsRow, DifficultyLabel, FMargin(0.f, 0.f, 16.f, 0.f));
	PrevDifficultyButton = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
	PrevDifficultyButton->Setup(false, 38.f);
	PrevDifficultyButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::PreviousDifficulty);
	USizeBox* PrevSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	PrevSize->SetWidthOverride(38.f); PrevSize->SetHeightOverride(38.f); PrevSize->SetContent(PrevDifficultyButton);
	HitmenUI::AddToHBox(OptionsRow, PrevSize);
	DifficultyText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 22, TEXT("Bold"));
	USizeBox* DiffSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	DiffSize->SetWidthOverride(120.f);
	DifficultyText->SetJustification(ETextJustify::Center);
	DiffSize->SetContent(DifficultyText);
	HitmenUI::AddToHBox(OptionsRow, DiffSize);
	NextDifficultyButton = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
	NextDifficultyButton->Setup(true, 38.f);
	NextDifficultyButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::NextDifficulty);
	USizeBox* NextSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	NextSize->SetWidthOverride(38.f); NextSize->SetHeightOverride(38.f); NextSize->SetContent(NextDifficultyButton);
	HitmenUI::AddToHBox(OptionsRow, NextSize);
	HitmenUI::AddToVBox(Left, OptionsRow);

	// Players and teams, right column.
	UVerticalBox* Right = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UCanvasPanelSlot* RightSlot = HitmenUI::AddToCanvas(Room, Right, FAnchors(1.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	RightSlot->SetOffsets(FMargin(-640.f, 150.f, 90.f, 140.f));
	UHorizontalBox* PlayersHeader = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	HitmenUI::AddToHBox(PlayersHeader, MakeSectionLabel(WidgetTree, LOCTEXT("Players", "PLAYERS")), FMargin(0.f, 0.f, 12.f, 0.f));
	PlayerCountText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 15, TEXT("Medium"), HitmenUI::Text());
	HitmenUI::AddToHBox(PlayersHeader, PlayerCountText);
	HitmenUI::AddToVBox(Right, PlayersHeader, FMargin(0.f, 0.f, 0.f, 10.f));
	UScrollBox* TeamsScroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	if (UVerticalBoxSlot* ScrollSlot = Right->AddChildToVerticalBox(TeamsScroll))
	{
		ScrollSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	}
	TeamsBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	TeamsScroll->AddChild(TeamsBox);
	CreateTeamButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	CreateTeamButton->Setup(LOCTEXT("CreateTeam", "+  Create team"), EHitmenMenuButtonKind::Secondary);
	CreateTeamButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::CreateTeam);
	HitmenUI::AddToVBox(Right, CreateTeamButton, FMargin(0.f, 8.f, 0.f, 0.f), false, HAlign_Left);

	// Bottom bar.
	UHitmenMenuButton* BackButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	BackButton->Setup(LOCTEXT("LeaveRoom", "Leave room"), EHitmenMenuButtonKind::Back);
	BackButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::Back);
	HitmenUI::AddToCanvas(Room, BackButton, FAnchors(0.f, 1.f), FVector2D(96.f, -42.f), FVector2D(0.f, 1.f));
	StartButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	StartButton->Setup(LOCTEXT("ChooseAgent", "Choose your agent"), EHitmenMenuButtonKind::Accent);
	StartButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::Start);
	HitmenUI::AddToCanvas(Room, StartButton, FAnchors(1.f, 1.f), FVector2D(-90.f, -40.f), FVector2D(1.f, 1.f));
	HostHintText = HitmenUI::MakeText(WidgetTree, LOCTEXT("WaitingHost", "Waiting for the host..."), 18, TEXT("Italic"), HitmenUI::TextDim());
	HitmenUI::AddToCanvas(Room, HostHintText, FAnchors(1.f, 1.f), FVector2D(-96.f, -52.f), FVector2D(1.f, 1.f));

	// ================= Your agent =================
	AgentCanvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	ScreenSwitcher->AddChild(AgentCanvas);
	UVerticalBox* AgentHeader = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToCanvas(AgentCanvas, AgentHeader, FAnchors(0.f, 0.f), FVector2D(110.f, 80.f), FVector2D::ZeroVector);
	HitmenUI::AddToVBox(AgentHeader, MakeSectionLabel(WidgetTree, LOCTEXT("AgentOverline", "AGENT")));
	HitmenUI::AddToVBox(AgentHeader, HitmenUI::MakeText(WidgetTree, LOCTEXT("YourAgent", "YOUR AGENT"), 76, TEXT("Bold")), FMargin(0.f, -10.f, 0.f, 8.f));
	// Identity (name, randomize) sits in the right column so the left stays clear for the model.
	UVerticalBox* Identity = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToCanvas(AgentCanvas, Identity, FAnchors(1.f, 0.f), FVector2D(-90.f, 150.f), FVector2D(1.f, 0.f));
	HitmenUI::AddToVBox(Identity, MakeSectionLabel(WidgetTree, LOCTEXT("Codename", "CODENAME")), FMargin(0.f, 0.f, 0.f, 8.f));
	AgentNameEdit = WidgetTree->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass());
	{
		FEditableTextBoxStyle Style = AgentNameEdit->WidgetStyle;
		FSlateBrush Field;
		Field.TintColor = FSlateColor(FLinearColor(0.f, 0.f, 0.f, 0.55f));
		Style.SetBackgroundImageNormal(Field);
		Style.SetBackgroundImageHovered(Field);
		Field.TintColor = FSlateColor(FLinearColor(0.f, 0.f, 0.f, 0.75f));
		Style.SetBackgroundImageFocused(Field);
		Style.SetPadding(FMargin(14.f, 8.f));
		Style.TextStyle.SetFont(HitmenUI::SpacedFont(26, TEXT("SemiBold"), 40));
		Style.TextStyle.SetColorAndOpacity(FSlateColor(HitmenUI::Text()));
		Style.SetForegroundColor(FSlateColor(HitmenUI::Text()));
		AgentNameEdit->WidgetStyle = Style;
	}
	AgentNameEdit->SetHintText(LOCTEXT("NameHint", "Agent name"));
	AgentNameEdit->OnTextCommitted.AddDynamic(this, &UHitmenLobbyWidget::HandleAgentNameCommitted);
	USizeBox* NameSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	NameSize->SetWidthOverride(420.f);
	NameSize->SetContent(AgentNameEdit);
	HitmenUI::AddToVBox(Identity, NameSize, FMargin(0.f, 0.f, 0.f, 8.f), false, HAlign_Left);
	UHitmenMenuButton* RandomButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	RandomButton->Setup(LOCTEXT("Randomize", "Randomize look"), EHitmenMenuButtonKind::Back);
	RandomButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::RandomizeCosmetics);
	HitmenUI::AddToVBox(Identity, RandomButton, FMargin(-20.f, 0.f, 0.f, 0.f), false, HAlign_Left);

	// Look arrows: two per slot (head, body, legs), placed beside the 3D model every frame.
	const FName ArrowHandlers[6] = {
		GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow0), GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow1),
		GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow2), GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow3),
		GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow4), GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow5) };
	for (int32 Arrow = 0; Arrow < 6; ++Arrow)
	{
		UHitmenArrowButton* Button = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
		Button->Setup(Arrow % 2 == 1, 46.f);
		FScriptDelegate Delegate;
		Delegate.BindUFunction(this, ArrowHandlers[Arrow]);
		Button->OnClicked.Add(Delegate);
		HitmenUI::AddToCanvas(AgentCanvas, Button, FAnchors(0.f, 0.f), FVector2D(-200.f, -200.f), FVector2D(0.5f, 0.5f), FVector2D(46.f, 46.f));
		LookArrows.Add(Button);
	}
	for (int32 Label = 0; Label < 3; ++Label)
	{
		UTextBlock* Text = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 18, TEXT("SemiBold"));
		Text->SetVisibility(ESlateVisibility::HitTestInvisible);
		HitmenUI::AddToCanvas(AgentCanvas, Text, FAnchors(0.f, 0.f), FVector2D(-400.f, -400.f), FVector2D(0.f, 0.5f));
		LookLabels.Add(Text);
	}

	// Loadout cards, bottom right, with the carousel above.
	UHorizontalBox* Cards = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	const FText Categories[3] = { LOCTEXT("Weapon", "Weapon"), LOCTEXT("Move", "Move ability"), LOCTEXT("Manipulation", "Manipulation") };
	const FText Keys[3] = { FText::GetEmpty(), FText::FromString(TEXT("E")), FText::FromString(TEXT("A")) };
	for (int32 Card = 0; Card < 3; ++Card)
	{
		UHitmenCardTile* Tile = WidgetTree->ConstructWidget<UHitmenCardTile>(UHitmenCardTile::StaticClass());
		Tile->Setup(Categories[Card], FText::GetEmpty(), Keys[Card], 190.f);
		Tile->Index = Card;
		Tile->OnHovered.AddDynamic(this, &UHitmenLobbyWidget::HandleCardHovered);
		Tile->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::HandleCardClicked);
		Tile->OnWheel.AddDynamic(this, &UHitmenLobbyWidget::HandleCardWheel);
		USizeBox* TileSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		TileSize->SetWidthOverride(190.f);
		TileSize->SetHeightOverride(190.f);
		TileSize->SetContent(Tile);
		HitmenUI::AddToHBox(Cards, TileSize, FMargin(0.f, 0.f, 10.f, 0.f));
		LoadoutCards.Add(Tile);
	}
	HitmenUI::AddToCanvas(AgentCanvas, Cards, FAnchors(1.f, 1.f), FVector2D(-80.f, -150.f), FVector2D(1.f, 1.f));
	UTextBlock* LoadoutLabel = MakeSectionLabel(WidgetTree, LOCTEXT("Loadout", "LOADOUT  -  HOVER OR CLICK A CARD, WHEEL TO BROWSE"));
	HitmenUI::AddToCanvas(AgentCanvas, LoadoutLabel, FAnchors(1.f, 1.f), FVector2D(-90.f, -360.f), FVector2D(1.f, 1.f));
	Carousel = WidgetTree->ConstructWidget<UHitmenChoiceCarousel>(UHitmenChoiceCarousel::StaticClass());
	Carousel->OnChosen.AddDynamic(this, &UHitmenLobbyWidget::HandleCarouselChosen);
	Carousel->SetVisibility(ESlateVisibility::Collapsed);
	HitmenUI::AddToCanvas(AgentCanvas, Carousel, FAnchors(1.f, 1.f), FVector2D(-80.f, -360.f), FVector2D(1.f, 1.f), FVector2D(600.f, 90.f));

	// Ready (no launch button: the match starts when everyone is ready).
	ReadyButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	ReadyButton->Setup(LOCTEXT("Ready", "Ready"), EHitmenMenuButtonKind::Accent);
	ReadyButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::ToggleReady);
	HitmenUI::AddToCanvas(AgentCanvas, ReadyButton, FAnchors(1.f, 1.f), FVector2D(-90.f, -40.f), FVector2D(1.f, 1.f));
	ReadyStatusText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 20, TEXT("Italic"), HitmenUI::TextDim());
	HitmenUI::AddToCanvas(AgentCanvas, ReadyStatusText, FAnchors(1.f, 1.f), FVector2D(-330.f, -52.f), FVector2D(1.f, 1.f));
	UHitmenMenuButton* AgentBack = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	AgentBack->Setup(LOCTEXT("BackToRoom", "Back"), EHitmenMenuButtonKind::Back);
	AgentBack->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::Back);
	HitmenUI::AddToCanvas(AgentCanvas, AgentBack, FAnchors(0.f, 1.f), FVector2D(96.f, -42.f), FVector2D(0.f, 1.f));
}

// =============================================================================================
// Lobby widget: events and refresh
// =============================================================================================

void UHitmenLobbyWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnLobbyChanged.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleLobbyChanged);
		UI->OnMatchPhaseChanged.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandlePhaseChanged);
	}
	HandleLobbyChanged();
}

void UHitmenLobbyWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnLobbyChanged.RemoveAll(this);
		UI->OnMatchPhaseChanged.RemoveAll(this);
	}
}

void UHitmenLobbyWidget::HandlePhaseChanged(EHitmenMatchPhase Phase)
{
	HandleLobbyChanged();
}

void UHitmenLobbyWidget::HandleLobbyChanged()
{
	if (IsEditingText())
	{
		bLobbyRefreshPending = true;
		return;
	}
	const AHitmenGameState* GS = GetHitmenGameState();
	const bool bCustomization = GS && GS->GetMatchPhase() == EHitmenMatchPhase::Customization;
	if (ScreenSwitcher && ScreenSwitcher->GetActiveWidgetIndex() != (bCustomization ? 1 : 0))
	{
		ScreenSwitcher->SetActiveWidgetIndex(bCustomization ? 1 : 0);
		Juice_OnScreenChanged(bCustomization);
		if (Carousel)
		{
			Carousel->SetVisibility(ESlateVisibility::Collapsed);
		}
	}
	RefreshLobby();
	if (bCustomization)
	{
		RefreshCustomization();
	}
}

bool UHitmenLobbyWidget::IsEditingText() const
{
	return AgentNameEdit && AgentNameEdit->HasKeyboardFocus();
}

void UHitmenLobbyWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	if (bLobbyRefreshPending && !IsEditingText())
	{
		bLobbyRefreshPending = false;
		HandleLobbyChanged();
	}
	if (ScreenSwitcher && ScreenSwitcher->GetActiveWidgetIndex() == 1)
	{
		UpdateAgentAnchors();
		// The carousel closes once the pointer has left the cards and the strip for a moment.
		if (Carousel && Carousel->IsVisible() && !bPointerOnCards && !Carousel->IsHovered())
		{
			CarouselCloseTimer += InDeltaTime;
			if (CarouselCloseTimer > 0.6f)
			{
				Carousel->SetVisibility(ESlateVisibility::Collapsed);
				CarouselCard = INDEX_NONE;
				for (UHitmenCardTile* Card : LoadoutCards)
				{
					Card->SetSelected(false);
				}
			}
		}
		else
		{
			CarouselCloseTimer = 0.f;
		}
	}
	// Launch countdown text (everyone ready).
	if (const AHitmenGameState* GS = GetHitmenGameState(); GS && ReadyStatusText)
	{
		const float Remaining = GS->GetPhaseCountdownRemaining();
		if (Remaining > 0.f)
		{
			ReadyStatusText->SetText(FText::Format(LOCTEXT("Launching", "Everyone is ready - starting in {0}"), FText::AsNumber(FMath::CeilToInt(Remaining))));
			ReadyStatusText->SetColorAndOpacity(FSlateColor(HitmenUI::Text()));
		}
	}
}

void UHitmenLobbyWidget::RefreshLobby()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	if (!GS)
	{
		return;
	}
	const bool bHost = IsHost();
	const FHitmenLobbySettings& Settings = GS->GetLobbySettings();
	const TArray<int32> Maps = GetCarouselMaps();
	const int32 Position = FMath::Max(0, Maps.IndexOfByKey(Settings.MapIndex));
	if (MapTile)
	{
		MapTile->SetMap(UHitmenGameSettings::Get()->LoadMap(Settings.MapIndex), Position, Maps.Num());
	}
	if (PrevMapButton) { PrevMapButton->SetVisibility(bHost && Position > 0 ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
	if (NextMapButton) { NextMapButton->SetVisibility(bHost && Position < Maps.Num() - 1 ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
	if (ProximityToggle)
	{
		ProximityToggle->SetChecked(Settings.bProximityChat);
		ProximityToggle->SetInteractable(bHost);
	}
	if (DifficultyText) { DifficultyText->SetText(DifficultyName(Settings.Difficulty)); }
	if (PrevDifficultyButton) { PrevDifficultyButton->SetVisibility(bHost ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
	if (NextDifficultyButton) { NextDifficultyButton->SetVisibility(bHost ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
	if (CreateTeamButton) { CreateTeamButton->SetVisibility(bHost ? ESlateVisibility::Visible : ESlateVisibility::Collapsed); }
	if (StartButton) { StartButton->SetVisibility(bHost ? ESlateVisibility::Visible : ESlateVisibility::Collapsed); }
	if (HostHintText) { HostHintText->SetVisibility(bHost ? ESlateVisibility::Collapsed : ESlateVisibility::HitTestInvisible); }
	if (PlayerCountText)
	{
		PlayerCountText->SetText(FText::Format(LOCTEXT("PlayerCount", "{0} / {1}"), FText::AsNumber(GS->GetHitmenPlayers().Num()), FText::AsNumber(UHitmenGameSettings::Get()->MaxPlayers)));
	}
	RebuildTeams();
}

void UHitmenLobbyWidget::RebuildTeams()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	if (!GS || !TeamsBox)
	{
		return;
	}
	const bool bShowReady = GS->GetMatchPhase() == EHitmenMatchPhase::Customization;
	TArray<int32> TeamIds = { INDEX_NONE };
	for (const FHitmenTeam& Team : GS->GetTeams())
	{
		TeamIds.Add(Team.TeamId);
	}
	// Reuse panels when the team list did not change (keeps drag state stable).
	if (TeamPanels.Num() != TeamIds.Num())
	{
		TeamsBox->ClearChildren();
		TeamPanels.Reset();
		for (int32 Index = 0; Index < TeamIds.Num(); ++Index)
		{
			UHitmenTeamPanelWidget* Panel = CreateWidget<UHitmenTeamPanelWidget>(GetOwningPlayer(), UHitmenTeamPanelWidget::StaticClass());
			HitmenUI::AddToVBox(TeamsBox, Panel, FMargin(0.f, 0.f, 0.f, 10.f));
			TeamPanels.Add(Panel);
		}
	}
	for (int32 Index = 0; Index < TeamIds.Num(); ++Index)
	{
		TeamPanels[Index]->SetTeam(TeamIds[Index]);
		TeamPanels[Index]->Refresh(bShowReady);
	}
}

// ---- Customization ----

AHitmenAgentPreview* UHitmenLobbyWidget::FindAgentPreview() const
{
	for (TActorIterator<AHitmenAgentPreview> It(GetWorld()); It; ++It)
	{
		return *It;
	}
	return nullptr;
}

void UHitmenLobbyWidget::RefreshCustomization()
{
	const AHitmenPlayerState* PS = GetLocalPlayerState();
	const UHitmenCatalog* Catalog = GetCatalog();
	if (!PS)
	{
		return;
	}
	if (!bLocalInitialized)
	{
		bLocalInitialized = true;
		LocalCosmetics = PS->GetCosmetics();
		LocalLoadout = PS->GetLoadout();
		if (Catalog)
		{
			if (!LocalLoadout.Weapon && Catalog->Weapons.Num() > 0) { LocalLoadout.Weapon = Catalog->Weapons[0]; }
			if (!LocalLoadout.MoveAbility && Catalog->MoveAbilities.Num() > 0) { LocalLoadout.MoveAbility = Catalog->MoveAbilities[0]; }
			if (!LocalLoadout.ManipulationAbility && Catalog->ManipulationAbilities.Num() > 0) { LocalLoadout.ManipulationAbility = Catalog->ManipulationAbilities[0]; }
		}
		if (AHitmenPlayerController* PC = GetHitmenPC())
		{
			HitmenLobby::SendLoadout(PC, LocalLoadout);
		}
	}
	if (AgentNameEdit && !AgentNameEdit->HasKeyboardFocus())
	{
		AgentNameEdit->SetText(FText::FromString(PS->GetPlayerName()));
	}
	if (AHitmenAgentPreview* Preview = FindAgentPreview())
	{
		Preview->ShowCosmetics(LocalCosmetics);
	}
	for (int32 LookIndex = 0; LookIndex < 3 && LookLabels.IsValidIndex(LookIndex); ++LookIndex)
	{
		const UHitmenCosmeticItem* Item = LocalCosmetics.GetSlot(LookSlots[LookIndex]);
		static const TCHAR* SlotNames[3] = { TEXT("HEAD"), TEXT("BODY"), TEXT("LEGS") };
		LookLabels[LookIndex]->SetText(FText::FromString(FString::Printf(TEXT("%s  -  %s"), SlotNames[LookIndex], Item ? *Item->DisplayName.ToString().ToUpper() : TEXT("NONE"))));
	}
	auto NameOf = [](const UObject* Asset, const FText& Fallback) -> FText
	{
		if (const UHitmenWeaponData* Weapon = Cast<UHitmenWeaponData>(Asset)) { return Weapon->DisplayName; }
		if (const UHitmenAbilityData* Ability = Cast<UHitmenAbilityData>(Asset)) { return Ability->DisplayName; }
		return Fallback;
	};
	if (LoadoutCards.Num() == 3)
	{
		LoadoutCards[0]->SetTitle(NameOf(LocalLoadout.Weapon, LOCTEXT("Fists", "Fists")));
		LoadoutCards[1]->SetTitle(NameOf(LocalLoadout.MoveAbility, LOCTEXT("None", "None")));
		LoadoutCards[2]->SetTitle(NameOf(LocalLoadout.ManipulationAbility, LOCTEXT("None", "None")));
	}
	const AHitmenGameState* GS = GetHitmenGameState();
	int32 ReadyCount = 0;
	int32 Needed = 0;
	for (const AHitmenPlayerState* Player : GS ? GS->GetHitmenPlayers() : TArray<AHitmenPlayerState*>())
	{
		if (!Player->IsWatcher())
		{
			++Needed;
			ReadyCount += Player->IsReady() ? 1 : 0;
		}
	}
	if (ReadyButton)
	{
		ReadyButton->SetLabel(PS->IsReady() ? LOCTEXT("NotReady", "Not ready") : LOCTEXT("Ready", "Ready"));
		ReadyButton->SetSelected(PS->IsReady());
	}
	if (ReadyStatusText && (!GS || GS->GetPhaseCountdownRemaining() <= 0.f))
	{
		ReadyStatusText->SetText(FText::Format(LOCTEXT("ReadyCount", "{0} / {1} ready"), FText::AsNumber(ReadyCount), FText::AsNumber(Needed)));
		ReadyStatusText->SetColorAndOpacity(FSlateColor(HitmenUI::TextDim()));
	}
}

void UHitmenLobbyWidget::UpdateAgentAnchors()
{
	APlayerController* PC = GetOwningPlayer();
	AHitmenAgentPreview* Preview = FindAgentPreview();
	USkeletalMeshComponent* Body = Preview ? Preview->GetBody() : nullptr;
	if (!PC || !Body)
	{
		return;
	}
	// Arrows sit on each side of the head, the torso and the legs, in two aligned columns at a
	// fixed distance from the body's centre line.
	FVector2D Center;
	if (!UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, Body->GetBoneLocation(TEXT("pelvis")), Center, true))
	{
		return;
	}
	const float Spread = 175.f;
	for (int32 LookIndex = 0; LookIndex < 3; ++LookIndex)
	{
		FVector2D Anchor;
		if (!UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, Body->GetBoneLocation(LookBones[LookIndex]), Anchor, true))
		{
			continue;
		}
		const float Y = Anchor.Y;
		for (int32 Side = 0; Side < 2; ++Side)
		{
			if (UCanvasPanelSlot* ArrowSlot = Cast<UCanvasPanelSlot>(LookArrows[LookIndex * 2 + Side]->Slot))
			{
				ArrowSlot->SetPosition(FVector2D(Center.X + (Side == 0 ? -Spread : Spread), Y));
			}
		}
		if (UCanvasPanelSlot* LabelSlot = Cast<UCanvasPanelSlot>(LookLabels[LookIndex]->Slot))
		{
			LabelSlot->SetPosition(FVector2D(Center.X + Spread + 40.f, Y));
		}
	}
}

void UHitmenLobbyWidget::LookArrow(int32 ArrowIndex)
{
	CycleCosmetic(LookSlots[ArrowIndex / 2], ArrowIndex % 2 == 0 ? -1 : 1);
}

void UHitmenLobbyWidget::HandleLookArrow0() { LookArrow(0); }
void UHitmenLobbyWidget::HandleLookArrow1() { LookArrow(1); }
void UHitmenLobbyWidget::HandleLookArrow2() { LookArrow(2); }
void UHitmenLobbyWidget::HandleLookArrow3() { LookArrow(3); }
void UHitmenLobbyWidget::HandleLookArrow4() { LookArrow(4); }
void UHitmenLobbyWidget::HandleLookArrow5() { LookArrow(5); }

TArray<FText> UHitmenLobbyWidget::GetLoadoutChoices(int32 CardIndex, int32& OutSelected) const
{
	TArray<FText> Choices;
	OutSelected = 0;
	const UHitmenCatalog* Catalog = GetCatalog();
	if (!Catalog)
	{
		return Choices;
	}
	if (CardIndex == 0)
	{
		Choices.Add(LOCTEXT("FistsChoice", "Fists"));
		for (int32 Index = 0; Index < Catalog->Weapons.Num(); ++Index)
		{
			Choices.Add(Catalog->Weapons[Index] ? Catalog->Weapons[Index]->DisplayName : FText::GetEmpty());
			OutSelected = Catalog->Weapons[Index] == LocalLoadout.Weapon ? Index + 1 : OutSelected;
		}
		return Choices;
	}
	const EHitmenAbilitySlot AbilitySlot = CardIndex == 1 ? EHitmenAbilitySlot::Move : EHitmenAbilitySlot::Manipulation;
	const UHitmenAbilityData* Current = CardIndex == 1 ? LocalLoadout.MoveAbility.Get() : LocalLoadout.ManipulationAbility.Get();
	Choices.Add(LOCTEXT("NoneChoice", "None"));
	const TArray<UHitmenAbilityData*> Abilities = Catalog->GetAbilities(AbilitySlot);
	for (int32 Index = 0; Index < Abilities.Num(); ++Index)
	{
		Choices.Add(Abilities[Index] ? Abilities[Index]->DisplayName : FText::GetEmpty());
		OutSelected = Abilities[Index] == Current ? Index + 1 : OutSelected;
	}
	return Choices;
}

void UHitmenLobbyWidget::OpenCarousel(int32 CardIndex)
{
	if (!Carousel || !LoadoutCards.IsValidIndex(CardIndex))
	{
		return;
	}
	CarouselCard = CardIndex;
	int32 Selected = 0;
	const TArray<FText> Choices = GetLoadoutChoices(CardIndex, Selected);
	Carousel->SetChoices(Choices, Selected);
	if (UCanvasPanelSlot* CarouselSlot = Cast<UCanvasPanelSlot>(Carousel->Slot))
	{
		CarouselSlot->SetSize(Carousel->GetStripSize());
	}
	for (int32 Card = 0; Card < LoadoutCards.Num(); ++Card)
	{
		LoadoutCards[Card]->SetSelected(Card == CardIndex);
	}
}

void UHitmenLobbyWidget::HandleCardHovered(bool bHovered)
{
	bPointerOnCards = false;
	for (int32 Card = 0; Card < LoadoutCards.Num(); ++Card)
	{
		if (LoadoutCards[Card]->IsHovered())
		{
			bPointerOnCards = true;
			if (Card != CarouselCard)
			{
				OpenCarousel(Card);
			}
		}
	}
}

void UHitmenLobbyWidget::HandleCardClicked()
{
	for (int32 Card = 0; Card < LoadoutCards.Num(); ++Card)
	{
		if (LoadoutCards[Card]->IsHovered())
		{
			OpenCarousel(Card);
		}
	}
}

void UHitmenLobbyWidget::HandleCardWheel(int32 Direction)
{
	if (CarouselCard == INDEX_NONE)
	{
		return;
	}
	int32 Selected = 0;
	const TArray<FText> Choices = GetLoadoutChoices(CarouselCard, Selected);
	if (Choices.Num() > 0)
	{
		ApplyLoadoutChoice(CarouselCard, (Selected + Direction + Choices.Num()) % Choices.Num());
		OpenCarousel(CarouselCard);
	}
}

void UHitmenLobbyWidget::HandleCarouselChosen(int32 Index)
{
	ApplyLoadoutChoice(CarouselCard, Index);
}

void UHitmenLobbyWidget::ApplyLoadoutChoice(int32 CardIndex, int32 ChoiceIndex)
{
	const UHitmenCatalog* Catalog = GetCatalog();
	if (!Catalog || CardIndex == INDEX_NONE)
	{
		return;
	}
	if (CardIndex == 0)
	{
		LocalLoadout.Weapon = ChoiceIndex > 0 && Catalog->Weapons.IsValidIndex(ChoiceIndex - 1) ? Catalog->Weapons[ChoiceIndex - 1] : nullptr;
	}
	else
	{
		const TArray<UHitmenAbilityData*> Abilities = Catalog->GetAbilities(CardIndex == 1 ? EHitmenAbilitySlot::Move : EHitmenAbilitySlot::Manipulation);
		UHitmenAbilityData* Choice = ChoiceIndex > 0 && Abilities.IsValidIndex(ChoiceIndex - 1) ? Abilities[ChoiceIndex - 1] : nullptr;
		(CardIndex == 1 ? LocalLoadout.MoveAbility : LocalLoadout.ManipulationAbility) = Choice;
	}
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		HitmenLobby::SendLoadout(PC, LocalLoadout);
	}
	RefreshCustomization();
}

// =============================================================================================
// Actions
// =============================================================================================

TArray<int32> UHitmenLobbyWidget::GetCarouselMaps() const
{
	TArray<int32> Result;
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	for (int32 Index = 0; Index < Settings->GetMapCount(); ++Index)
	{
		if (const UHitmenMapDefinition* Map = Settings->LoadMap(Index); Map && Map->bShowInCarousel)
		{
			Result.Add(Index);
		}
	}
	return Result;
}

bool UHitmenLobbyWidget::IsHost() const
{
	const AHitmenPlayerController* PC = GetHitmenPC();
	return PC && PC->IsHost();
}

UHitmenCatalog* UHitmenLobbyWidget::GetCatalog() const
{
	return UHitmenGameSettings::Get()->LoadCatalog();
}

AHitmenPlayerState* UHitmenLobbyWidget::GetLocalPlayerState() const
{
	return GetOwningPlayer() ? GetOwningPlayer()->GetPlayerState<AHitmenPlayerState>() : nullptr;
}

void UHitmenLobbyWidget::SendSettings(const FHitmenLobbySettings& Settings)
{
	if (AHitmenPlayerController* PC = GetHitmenPC(); PC && IsHost())
	{
		PC->Server_SetLobbySettings(Settings);
	}
}

void UHitmenLobbyWidget::PreviousMap()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const TArray<int32> Maps = GetCarouselMaps();
	if (!GS || Maps.IsEmpty())
	{
		return;
	}
	const int32 Current = FMath::Max(0, Maps.IndexOfByKey(GS->GetLobbySettings().MapIndex));
	FHitmenLobbySettings Settings = GS->GetLobbySettings();
	Settings.MapIndex = Maps[FMath::Max(0, Current - 1)];
	SendSettings(Settings);
}

void UHitmenLobbyWidget::NextMap()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const TArray<int32> Maps = GetCarouselMaps();
	if (!GS || Maps.IsEmpty())
	{
		return;
	}
	const int32 Current = FMath::Max(0, Maps.IndexOfByKey(GS->GetLobbySettings().MapIndex));
	FHitmenLobbySettings Settings = GS->GetLobbySettings();
	Settings.MapIndex = Maps[FMath::Min(Maps.Num() - 1, Current + 1)];
	SendSettings(Settings);
}

void UHitmenLobbyWidget::SetProximityChat(bool bEnabled)
{
	if (const AHitmenGameState* GS = GetHitmenGameState())
	{
		FHitmenLobbySettings Settings = GS->GetLobbySettings();
		Settings.bProximityChat = bEnabled;
		SendSettings(Settings);
	}
}

void UHitmenLobbyWidget::PreviousDifficulty()
{
	if (const AHitmenGameState* GS = GetHitmenGameState())
	{
		FHitmenLobbySettings Settings = GS->GetLobbySettings();
		Settings.Difficulty = static_cast<EHitmenDifficulty>((static_cast<uint8>(Settings.Difficulty) + 2) % 3);
		SendSettings(Settings);
	}
}

void UHitmenLobbyWidget::NextDifficulty()
{
	if (const AHitmenGameState* GS = GetHitmenGameState())
	{
		FHitmenLobbySettings Settings = GS->GetLobbySettings();
		Settings.Difficulty = static_cast<EHitmenDifficulty>((static_cast<uint8>(Settings.Difficulty) + 1) % 3);
		SendSettings(Settings);
	}
}

void UHitmenLobbyWidget::CreateTeam()
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_CreateTeam();
	}
}

void UHitmenLobbyWidget::Back()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	if (GS && GS->GetMatchPhase() == EHitmenMatchPhase::Customization && IsHost())
	{
		if (AHitmenPlayerController* PC = GetHitmenPC())
		{
			PC->Server_BackToLobby();
		}
		return;
	}
	if (UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>())
	{
		GI->ReturnToTitle();
	}
}

void UHitmenLobbyWidget::Start()
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_StartCustomization();
	}
}

void UHitmenLobbyWidget::CycleCosmetic(EHitmenCosmeticSlot CosmeticSlot, int32 Direction)
{
	const UHitmenCatalog* Catalog = GetCatalog();
	if (!Catalog)
	{
		return;
	}
	LocalCosmetics.SetSlot(CosmeticSlot, CycleIn(Catalog->GetCosmeticsForSlot(CosmeticSlot), LocalCosmetics.GetSlot(CosmeticSlot), Direction, true));
	if (AHitmenPlayerController* PC = GetHitmenPC()) { HitmenLobby::SendCosmetics(PC, LocalCosmetics); }
	RefreshCustomization();
}

void UHitmenLobbyWidget::RandomizeCosmetics()
{
	if (const UHitmenCatalog* Catalog = GetCatalog())
	{
		LocalCosmetics = Catalog->MakeRandomCosmetics();
		if (AHitmenPlayerController* PC = GetHitmenPC()) { HitmenLobby::SendCosmetics(PC, LocalCosmetics); }
		RefreshCustomization();
	}
}

void UHitmenLobbyWidget::ToggleReady()
{
	const AHitmenPlayerState* PS = GetLocalPlayerState();
	if (AHitmenPlayerController* PC = GetHitmenPC(); PC && PS)
	{
		PC->Server_SetReady(!PS->IsReady());
	}
}

void UHitmenLobbyWidget::HandleAgentNameCommitted(const FText& Text, ETextCommit::Type CommitType)
{
	if (Text.IsEmpty())
	{
		return;
	}
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_SetPlayerName(Text.ToString());
	}
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->PreferredName = Text.ToString();
		Settings->ApplyHitmenSettings();
	}
}

#undef LOCTEXT_NAMESPACE
