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
#include "Components/Border.h"
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

	UTextBlock* MakeSectionLabel(UWidgetTree* Tree, const FText& Text, int32 Size = 15)
	{
		UTextBlock* Label = HitmenUI::MakeText(Tree, Text, Size, TEXT("Medium"), HitmenUI::TextDim());
		Label->SetFont(HitmenUI::SpacedFont(Size, TEXT("Medium"), 260));
		return Label;
	}

	USizeBox* Sized(UWidgetTree* Tree, UWidget* Content, float Width, float Height)
	{
		USizeBox* Box = Tree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		if (Width > 0.f) { Box->SetWidthOverride(Width); }
		if (Height > 0.f) { Box->SetHeightOverride(Height); }
		Box->SetContent(Content);
		return Box;
	}

	const EHitmenCosmeticSlot LookSlots[3] = { EHitmenCosmeticSlot::Head, EHitmenCosmeticSlot::Body, EHitmenCosmeticSlot::Legs };
	const TCHAR* LookBones[3] = { TEXT("head"), TEXT("spine_03"), TEXT("calf_l") };

	/** Half the distance between the two look arrows of a slot (they hug the body). */
	constexpr float LookArrowSpread = 118.f;
	/** Seconds without a click before an edited look is sent to the server. */
	constexpr float CosmeticSendPause = 0.15f;
	constexpr float LoadoutTileSize = 84.f;
	constexpr int32 LoadoutColumns = 7;

	const FLinearColor PanelOuter(0.018f, 0.02f, 0.026f, 0.9f);
	const FLinearColor PanelTeam(0.07f, 0.074f, 0.086f, 0.97f);
	const FLinearColor PanelPlayer(0.135f, 0.14f, 0.155f, 0.98f);
}

// =============================================================================================
// Player entry
// =============================================================================================

void UHitmenPlayerEntryWidget::BuildDefaultLayout()
{
	USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	Size->SetHeightOverride(54.f);
	Size->SetMinDesiredWidth(300.f);
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
	return FBox2D(FVector2D(Size.X - 54.f, 0.f), Size);
}

void UHitmenPlayerEntryWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	HoverAlpha = FMath::FInterpTo(HoverAlpha, bHovered ? 1.f : 0.f, InDeltaTime, 18.f);
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
	bEyeHovered = false;
}

FReply UHitmenPlayerEntryWidget::NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	bEyeHovered = CanToggleWatcher() && GetWatcherBox(InGeometry).IsInside(InGeometry.AbsoluteToLocal(InMouseEvent.GetScreenSpacePosition()));
	return Super::NativeOnMouseMove(InGeometry, InMouseEvent);
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
	// The host drags the whole block onto a team.
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
	if (!IsValid(PlayerState))
	{
		return LayerId;
	}
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	// A solid block per player, lighter on hover.
	const FLinearColor Back = bDragVisual ? FLinearColor(0.2f, 0.205f, 0.22f, 0.98f) : FMath::Lerp(PanelPlayer, FLinearColor(0.2f, 0.205f, 0.225f, 1.f), HoverAlpha);
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, Back);
	HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, FVector2D(6.f, Size.Y), Accent);
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, Size, bDragVisual ? HitmenUI::Accent() : FLinearColor(1.f, 1.f, 1.f, 0.08f + 0.2f * HoverAlpha), bDragVisual ? 2.f : 1.f);
	const bool bWatcher = PlayerState->IsWatcher();
	const FSlateFontInfo NameFont = HitmenUI::SpacedFont(22, TEXT("SemiBold"), 30);
	// Long names are shortened so the tags and the right-hand state never overlap them.
	FString Name = PlayerState->GetPlayerName();
	const AHitmenGameState* NameGS = GetHitmenGameState();
	const bool bHostRow = NameGS && NameGS->PlayerArray.Num() > 0 && NameGS->PlayerArray[0] == PlayerState;
	const float NameRoom = Size.X - 24.f - 120.f - (IsLocalPlayer() ? 52.f : 0.f) - (bHostRow ? 64.f : 0.f);
	if (HitmenUI::MeasureText(Name, NameFont).X > NameRoom)
	{
		while (Name.Len() > 3 && HitmenUI::MeasureText(Name + TEXT("..."), NameFont).X > NameRoom)
		{
			Name.LeftChopInline(1);
		}
		Name += TEXT("...");
	}
	const FVector2D NameSize = HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Name, FVector2D(22.f, (Size.Y - 28.f) * 0.5f), NameFont,
		bWatcher && !bShowReady ? HitmenUI::TextDim() : HitmenUI::Text());
	// Tags: YOU / HOST.
	float TagX = 22.f + NameSize.X + 12.f;
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
	if (bHostRow)
	{
		Tag(TEXT("HOST"), HitmenUI::TextDim());
	}
	if (bShowReady)
	{
		const bool bReady = PlayerState->IsReady();
		if (bWatcher)
		{
			UHitmenIconButton::PaintGlyph(OutDrawElements, LayerId + 2, AllottedGeometry, EHitmenIconGlyph::Eye, FVector2D(Size.X - 30.f, Size.Y * 0.5f), 11.f, HitmenUI::TextDim(), 2.f);
		}
		else
		{
			const FSlateFontInfo SideFont = HitmenUI::SpacedFont(15, TEXT("SemiBold"), 140);
			const FString Text = bReady ? FString(TEXT("READY")) : FString(TEXT("CHOOSING"));
			const FVector2D TextSize = HitmenUI::MeasureText(Text, SideFont);
			HitmenUI::PaintText(OutDrawElements, LayerId + 2, AllottedGeometry, Text, FVector2D(Size.X - TextSize.X - 18.f, (Size.Y - TextSize.Y) * 0.5f), SideFont,
				bReady ? FLinearColor(0.35f, 0.9f, 0.45f, 1.f) : HitmenUI::TextDim());
		}
	}
	else if (PlayerState->GetLobbyTeamId() == INDEX_NONE && (bHovered || bWatcher))
	{
		// Watcher eye: only on hover (faint while watching); open = watching, struck = playing.
		const bool bEditable = CanToggleWatcher();
		const float Alpha = bHovered ? (bEyeHovered && bEditable ? 1.f : 0.7f) : 0.45f;
		const FBox2D Box = GetWatcherBox(AllottedGeometry);
		if (bEyeHovered && bEditable)
		{
			HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, Box.Min + FVector2D(6.f, 8.f), Box.GetSize() - FVector2D(12.f, 16.f), FLinearColor(1.f, 1.f, 1.f, 0.1f));
		}
		UHitmenIconButton::PaintGlyph(OutDrawElements, LayerId + 3, AllottedGeometry, bWatcher ? EHitmenIconGlyph::Eye : EHitmenIconGlyph::EyeOff,
			Box.GetCenter(), 11.f, FLinearColor(1.f, 1.f, 1.f, Alpha), 2.f);
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 4, InWidgetStyle, bParentEnabled);
}

// =============================================================================================
// Team panel
// =============================================================================================

void UHitmenTeamPanelWidget::BuildDefaultLayout()
{
	// Opaque block (Border, drawn under the rows), a tinted header strip, then the player blocks.
	PanelFill = HitmenUI::MakePanel(WidgetTree, PanelTeam, FMargin(0.f));
	WidgetTree->RootWidget = PanelFill;
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	PanelFill->SetContent(Column);
	HeaderFill = HitmenUI::MakePanel(WidgetTree, FLinearColor(0.12f, 0.125f, 0.14f, 1.f), FMargin(0.f));
	UHorizontalBox* Header = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	HeaderFill->SetContent(Sized(WidgetTree, Header, 0.f, 50.f));
	TitleText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 20, TEXT("SemiBold"));
	TitleText->SetFont(HitmenUI::SpacedFont(20, TEXT("SemiBold"), 160));
	if (UHorizontalBoxSlot* TitleSlot = Header->AddChildToHorizontalBox(TitleText))
	{
		TitleSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
		TitleSlot->SetVerticalAlignment(VAlign_Center);
		TitleSlot->SetPadding(FMargin(18.f, 0.f, 0.f, 0.f));
	}
	RoleButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	RoleButton->Setup(FText::GetEmpty(), EHitmenMenuButtonKind::Chip);
	RoleButton->OnClicked.AddDynamic(this, &UHitmenTeamPanelWidget::HandleRoleClicked);
	HitmenUI::AddToHBox(Header, Sized(WidgetTree, RoleButton, 132.f, 0.f), FMargin(0.f, 0.f, 8.f, 0.f));
	RemoveButton = WidgetTree->ConstructWidget<UHitmenIconButton>(UHitmenIconButton::StaticClass());
	RemoveButton->Setup(EHitmenIconGlyph::Close, 34.f, true);
	RemoveButton->OnClicked.AddDynamic(this, &UHitmenTeamPanelWidget::HandleRemoveClicked);
	HitmenUI::AddToHBox(Header, RemoveButton, FMargin(0.f, 0.f, 10.f, 0.f));
	HitmenUI::AddToVBox(Column, HeaderFill, FMargin(0.f, 0.f, 0.f, 8.f));
	MemberList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Column, MemberList, FMargin(14.f, 0.f, 12.f, 12.f));
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
	TeamColor = FLinearColor(0.75f, 0.76f, 0.8f, 1.f);
	if (TeamId == INDEX_NONE)
	{
		TitleText->SetText(LOCTEXT("Unassigned", "NO TEAM"));
		TitleText->SetColorAndOpacity(FSlateColor(HitmenUI::TextDim()));
		RoleButton->SetVisibility(ESlateVisibility::Collapsed);
		RemoveButton->SetVisibility(ESlateVisibility::Collapsed);
	}
	else if (const FHitmenTeam* Team = GS->FindTeam(TeamId))
	{
		TeamColor = Team->Color;
		TitleText->SetText(FText::FromString(Team->DisplayName.ToString().ToUpper()));
		TitleText->SetColorAndOpacity(FSlateColor(FMath::Lerp(TeamColor, FLinearColor::White, 0.35f)));
		RoleButton->SetLabel(Team->Role == EHitmenTeamRole::Protect ? LOCTEXT("Protect", "Protect") : LOCTEXT("Attack", "Attack"));
		RoleButton->SetButtonEnabled(bHost && !bShowReady);
		RoleButton->SetVisibility(ESlateVisibility::Visible);
		RemoveButton->SetVisibility(bHost && !bShowReady ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	}
	if (HeaderFill)
	{
		const FLinearColor Tint = TeamId == INDEX_NONE ? FLinearColor(0.11f, 0.115f, 0.13f, 1.f) : TeamColor * FLinearColor(0.3f, 0.3f, 0.3f, 1.f);
		HeaderFill->SetBrushColor(FLinearColor(Tint.R, Tint.G, Tint.B, 1.f));
	}
	MemberList->ClearChildren();
	for (AHitmenPlayerState* PS : GS->GetPlayersInTeam(TeamId))
	{
		UHitmenPlayerEntryWidget* Entry = CreateWidget<UHitmenPlayerEntryWidget>(GetOwningPlayer(), UHitmenPlayerEntryWidget::StaticClass());
		Entry->SetPlayer(PS);
		Entry->SetAccent(TeamId == INDEX_NONE ? FLinearColor(1.f, 1.f, 1.f, 0.85f) : TeamColor);
		Entry->SetShowReady(bShowReady);
		HitmenUI::AddToVBox(MemberList, Entry, FMargin(0.f, 3.f));
	}
	if (MemberList->GetChildrenCount() == 0)
	{
		UTextBlock* Hint = HitmenUI::MakeText(WidgetTree, bHost ? LOCTEXT("DropHere", "Drag players here") : LOCTEXT("Empty", "Empty"), 17, TEXT("Italic"), HitmenUI::TextDim());
		HitmenUI::AddToVBox(MemberList, Hint, FMargin(8.f, 10.f));
	}
}

int32 UHitmenTeamPanelWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	// Drawn over the children (Slate paints a user widget after them): only the colour bar and frame.
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, FVector2D(5.f, Size.Y), TeamColor);
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D::ZeroVector, Size, bDropHover ? HitmenUI::Accent() : FLinearColor(1.f, 1.f, 1.f, 0.1f), bDropHover ? 2.f : 1.f);
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}

void UHitmenTeamPanelWidget::NativeOnDragEnter(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation)
{
	Super::NativeOnDragEnter(InGeometry, InDragDropEvent, InOperation);
	bDropHover = Cast<UHitmenPlayerDragOperation>(InOperation) != nullptr;
	if (PanelFill)
	{
		PanelFill->SetBrushColor(bDropHover ? FLinearColor(0.11f, 0.115f, 0.13f, 0.98f) : PanelTeam);
	}
}

void UHitmenTeamPanelWidget::NativeOnDragLeave(const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation)
{
	Super::NativeOnDragLeave(InDragDropEvent, InOperation);
	bDropHover = false;
	if (PanelFill)
	{
		PanelFill->SetBrushColor(PanelTeam);
	}
}

bool UHitmenTeamPanelWidget::NativeOnDrop(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation)
{
	bDropHover = false;
	const UHitmenPlayerDragOperation* Operation = Cast<UHitmenPlayerDragOperation>(InOperation);
	AHitmenPlayerController* PC = GetHitmenPC();
	if (!Operation || !IsValid(Operation->PlayerState) || !PC)
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
	if (ShownMap.Get() == Map && Map)
	{
		return;
	}
	ShownMap = Map;
	bHasShot = false;
	bHasMinimap = false;
	MapName = Map ? Map->DisplayName : FText::GetEmpty();
	Description = Map ? Map->Description : FText::GetEmpty();
	auto SetBrush = [](FSlateBrush& Brush, UTexture2D* Texture)
	{
		Brush = FSlateBrush();
		if (!Texture)
		{
			return false;
		}
		Brush.SetResourceObject(Texture);
		Brush.ImageSize = FVector2D(Texture->GetSizeX(), Texture->GetSizeY());
		return true;
	};
	bHasShot = Map && SetBrush(ShotBrush, Map->Thumbnail.LoadSynchronous());
	bHasMinimap = Map && SetBrush(MinimapBrush, Map->MapTexture.LoadSynchronous());
}

int32 UHitmenMapTileWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, FVector2D::ZeroVector, Size, FLinearColor(0.05f, 0.052f, 0.06f, 0.98f));
	// The screenshot fills the whole tile: cropped (never letterboxed) to the tile's shape.
	const FSlateBrush* Fill = bHasShot ? &ShotBrush : (bHasMinimap ? &MinimapBrush : nullptr);
	if (Fill && Fill->ImageSize.X > 0.f && Fill->ImageSize.Y > 0.f && Size.Y > 0.f)
	{
		FSlateBrush Cropped = *Fill;
		const float TileAspect = Size.X / Size.Y;
		const float ImageAspect = Fill->ImageSize.X / Fill->ImageSize.Y;
		FBox2f UV(FVector2f(0.f, 0.f), FVector2f(1.f, 1.f));
		if (ImageAspect > TileAspect)
		{
			const float Width = TileAspect / ImageAspect;
			UV = FBox2f(FVector2f((1.f - Width) * 0.5f, 0.f), FVector2f((1.f + Width) * 0.5f, 1.f));
		}
		else
		{
			const float Height = ImageAspect / TileAspect;
			UV = FBox2f(FVector2f(0.f, (1.f - Height) * 0.5f), FVector2f(1.f, (1.f + Height) * 0.5f));
		}
		Cropped.SetUVRegion(UV);
		HitmenUI::PaintImage(OutDrawElements, LayerId + 1, AllottedGeometry, &Cropped, FVector2D::ZeroVector, Size);
	}
	// The whole map, small, top-left.
	if (bHasShot && bHasMinimap)
	{
		const float Mini = FMath::Min(170.f, Size.Y * 0.36f);
		const FVector2D MiniPos(18.f, 18.f);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, MiniPos - FVector2D(4.f), FVector2D(Mini + 8.f), FLinearColor(0.f, 0.f, 0.f, 0.75f));
		HitmenUI::PaintImage(OutDrawElements, LayerId + 3, AllottedGeometry, &MinimapBrush, MiniPos, FVector2D(Mini));
		HitmenUI::PaintFrame(OutDrawElements, LayerId + 3, AllottedGeometry, MiniPos - FVector2D(4.f), FVector2D(Mini + 8.f), FLinearColor(1.f, 1.f, 1.f, 0.3f), 1.f);
	}
	// Caption over a bottom gradient.
	HitmenUI::PaintGradient(OutDrawElements, LayerId + 2, AllottedGeometry, FVector2D(0.f, Size.Y * 0.5f), FVector2D(Size.X, Size.Y * 0.5f), FLinearColor(0, 0, 0, 0), FLinearColor(0, 0, 0, 0.92f), false);
	const FSlateFontInfo NameFont = HitmenUI::SpacedFont(44, TEXT("Bold"), 20);
	HitmenUI::PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, MapName.ToString().ToUpper(), FVector2D(26.f, Size.Y - 96.f), NameFont, HitmenUI::Text());
	HitmenUI::PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, Description.ToString(), FVector2D(28.f, Size.Y - 42.f), HitmenUI::Font(18, TEXT("Italic")), FLinearColor(0.78f, 0.79f, 0.82f, 1.f));
	if (MapCount > 1)
	{
		const FString Counter = FString::Printf(TEXT("%02d / %02d"), MapPosition + 1, MapCount);
		const FSlateFontInfo CounterFont = HitmenUI::SpacedFont(15, TEXT("Medium"), 200);
		const FVector2D CounterSize = HitmenUI::MeasureText(Counter, CounterFont);
		HitmenUI::PaintBox(OutDrawElements, LayerId + 2, AllottedGeometry, FVector2D(Size.X - CounterSize.X - 34.f, 14.f), FVector2D(CounterSize.X + 20.f, CounterSize.Y + 10.f), FLinearColor(0.f, 0.f, 0.f, 0.7f));
		HitmenUI::PaintText(OutDrawElements, LayerId + 3, AllottedGeometry, Counter, FVector2D(Size.X - CounterSize.X - 24.f, 19.f), CounterFont, HitmenUI::Text());
	}
	HitmenUI::PaintFrame(OutDrawElements, LayerId + 3, AllottedGeometry, FVector2D::ZeroVector, Size, HitmenUI::Hairline(), 1.f);
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
	HitmenUI::AddToCanvas(Room, Left, FAnchors(0.f, 0.f), FVector2D(110.f, 70.f), FVector2D::ZeroVector);
	HitmenUI::AddToVBox(Left, HitmenUI::MakeText(WidgetTree, LOCTEXT("CreateRoom", "CREATE ROOM"), 72, TEXT("Bold")), FMargin(0.f, 0.f, 0.f, 14.f));

	// Map tile with arrows on its sides.
	UCanvasPanel* MapArea = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	HitmenUI::AddToVBox(Left, Sized(WidgetTree, MapArea, 840.f, 470.f), FMargin(0.f, 0.f, 0.f, 22.f), false, HAlign_Left);
	MapTile = WidgetTree->ConstructWidget<UHitmenMapTileWidget>(UHitmenMapTileWidget::StaticClass());
	UCanvasPanelSlot* TileSlot = HitmenUI::AddToCanvas(MapArea, MapTile, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	TileSlot->SetAutoSize(false);
	TileSlot->SetOffsets(FMargin(0.f));
	PrevMapButton = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
	PrevMapButton->Setup(false, 60.f);
	PrevMapButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::PreviousMap);
	HitmenUI::AddToCanvas(MapArea, PrevMapButton, FAnchors(0.f, 0.5f), FVector2D(16.f, 0.f), FVector2D(0.f, 0.5f), FVector2D(60.f, 60.f));
	NextMapButton = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
	NextMapButton->Setup(true, 60.f);
	NextMapButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::NextMap);
	HitmenUI::AddToCanvas(MapArea, NextMapButton, FAnchors(1.f, 0.5f), FVector2D(-16.f, 0.f), FVector2D(1.f, 0.5f), FVector2D(60.f, 60.f));

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
	PrevDifficultyButton->Setup(false, 40.f);
	PrevDifficultyButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::PreviousDifficulty);
	HitmenUI::AddToHBox(OptionsRow, Sized(WidgetTree, PrevDifficultyButton, 40.f, 40.f));
	DifficultyText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 22, TEXT("Bold"));
	DifficultyText->SetJustification(ETextJustify::Center);
	HitmenUI::AddToHBox(OptionsRow, Sized(WidgetTree, DifficultyText, 120.f, 0.f));
	NextDifficultyButton = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
	NextDifficultyButton->Setup(true, 40.f);
	NextDifficultyButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::NextDifficulty);
	HitmenUI::AddToHBox(OptionsRow, Sized(WidgetTree, NextDifficultyButton, 40.f, 40.f));
	HitmenUI::AddToVBox(Left, OptionsRow);

	// Players: one big block holding the team blocks, which hold the player blocks. It runs from
	// the map's right edge to the screen margin.
	UBorder* PlayersPanel = HitmenUI::MakePanel(WidgetTree, PanelOuter, FMargin(16.f));
	UCanvasPanelSlot* PlayersSlot = HitmenUI::AddToCanvas(Room, PlayersPanel, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	PlayersSlot->SetAutoSize(false);
	PlayersSlot->SetOffsets(FMargin(980.f, 160.f, 90.f, 130.f));
	UVerticalBox* Right = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	PlayersPanel->SetContent(Right);
	UHorizontalBox* PlayersHeader = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	HitmenUI::AddToHBox(PlayersHeader, MakeSectionLabel(WidgetTree, LOCTEXT("Players", "PLAYERS")), FMargin(4.f, 0.f, 12.f, 0.f));
	PlayerCountText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 15, TEXT("Medium"), HitmenUI::Text());
	if (UHorizontalBoxSlot* CountSlot = PlayersHeader->AddChildToHorizontalBox(PlayerCountText))
	{
		CountSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
		CountSlot->SetVerticalAlignment(VAlign_Center);
	}
	HitmenUI::AddToHBox(PlayersHeader, MakeSectionLabel(WidgetTree, LOCTEXT("CodeLabel", "ROOM CODE")), FMargin(0.f, 0.f, 12.f, 0.f));
	RoomCodeText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 26, TEXT("Bold"));
	RoomCodeText->SetFont(HitmenUI::SpacedFont(26, TEXT("Bold"), 220));
	HitmenUI::AddToHBox(PlayersHeader, RoomCodeText, FMargin(0.f, 0.f, 4.f, 0.f));
	HitmenUI::AddToVBox(Right, Sized(WidgetTree, PlayersHeader, 0.f, 40.f), FMargin(0.f, 0.f, 0.f, 12.f));
	UScrollBox* TeamsScroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	if (UVerticalBoxSlot* ScrollSlot = Right->AddChildToVerticalBox(TeamsScroll))
	{
		ScrollSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	}
	TeamsBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	TeamsScroll->AddChild(TeamsBox);
	CreateTeamButton = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	CreateTeamButton->Setup(LOCTEXT("CreateTeam", "+  Create team"), EHitmenMenuButtonKind::Dashed);
	CreateTeamButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::CreateTeam);
	HitmenUI::AddToVBox(Right, CreateTeamButton, FMargin(0.f, 10.f, 0.f, 0.f), false, HAlign_Fill);

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
	HitmenUI::AddToCanvas(AgentCanvas, MakeSectionLabel(WidgetTree, LOCTEXT("YourAgent", "YOUR AGENT"), 18), FAnchors(0.f, 0.f), FVector2D(110.f, 70.f), FVector2D::ZeroVector);

	// Codename above the agent's head (placed every frame).
	AgentNameEdit = WidgetTree->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass());
	{
		FEditableTextBoxStyle Style = AgentNameEdit->WidgetStyle;
		FSlateBrush Field;
		Field.TintColor = FSlateColor(FLinearColor(0.f, 0.f, 0.f, 0.55f));
		Style.SetBackgroundImageNormal(Field);
		Style.SetBackgroundImageHovered(Field);
		Field.TintColor = FSlateColor(FLinearColor(0.f, 0.f, 0.f, 0.8f));
		Style.SetBackgroundImageFocused(Field);
		Style.SetPadding(FMargin(14.f, 8.f));
		Style.TextStyle.SetFont(HitmenUI::SpacedFont(26, TEXT("SemiBold"), 60));
		Style.TextStyle.SetColorAndOpacity(FSlateColor(HitmenUI::Text()));
		Style.SetForegroundColor(FSlateColor(HitmenUI::Text()));
		AgentNameEdit->WidgetStyle = Style;
	}
	AgentNameEdit->SetHintText(LOCTEXT("NameHint", "Codename"));
	AgentNameEdit->SetJustification(ETextJustify::Center);
	AgentNameEdit->OnTextCommitted.AddDynamic(this, &UHitmenLobbyWidget::HandleAgentNameCommitted);
	AgentNameBox = Sized(WidgetTree, AgentNameEdit, 340.f, 0.f);
	HitmenUI::AddToCanvas(AgentCanvas, AgentNameBox, FAnchors(0.f, 0.f), FVector2D(-1000.f, -1000.f), FVector2D(0.5f, 1.f));

	// Look arrows: two per slot (head, body, legs), hugging the 3D model, placed every frame.
	const FName ArrowHandlers[6] = {
		GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow0), GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow1),
		GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow2), GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow3),
		GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow4), GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleLookArrow5) };
	for (int32 Arrow = 0; Arrow < 6; ++Arrow)
	{
		UHitmenArrowButton* Button = WidgetTree->ConstructWidget<UHitmenArrowButton>(UHitmenArrowButton::StaticClass());
		Button->Setup(Arrow % 2 == 1, 50.f);
		FScriptDelegate Delegate;
		Delegate.BindUFunction(this, ArrowHandlers[Arrow]);
		Button->OnClicked.Add(Delegate);
		HitmenUI::AddToCanvas(AgentCanvas, Button, FAnchors(0.f, 0.f), FVector2D(-1000.f, -1000.f), FVector2D(0.5f, 0.5f), FVector2D(50.f, 50.f));
		LookArrows.Add(Button);
	}
	// Randomize look, under the agent's feet.
	RandomizeButton = WidgetTree->ConstructWidget<UHitmenIconButton>(UHitmenIconButton::StaticClass());
	RandomizeButton->Setup(EHitmenIconGlyph::Dice, 40.f, true);
	RandomizeButton->SetLabel(LOCTEXT("Randomize", "Randomize"));
	RandomizeButton->OnClicked.AddDynamic(this, &UHitmenLobbyWidget::RandomizeCosmetics);
	HitmenUI::AddToCanvas(AgentCanvas, RandomizeButton, FAnchors(0.f, 0.f), FVector2D(-1000.f, -1000.f), FVector2D(0.5f, 0.f));

	// Loadout: three rows of icon tiles, always visible on the right. A click picks.
	UBorder* LoadoutPanel = HitmenUI::MakePanel(WidgetTree, PanelOuter, FMargin(20.f, 16.f));
	HitmenUI::AddToCanvas(AgentCanvas, LoadoutPanel, FAnchors(1.f, 0.f), FVector2D(-90.f, 120.f), FVector2D(1.f, 0.f));
	UVerticalBox* Loadout = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	LoadoutPanel->SetContent(Loadout);
	const FText RowNames[3] = { LOCTEXT("WeaponRow", "WEAPON"), LOCTEXT("MoveRow", "MOVE ABILITY   [E]"), LOCTEXT("ManipulationRow", "MANIPULATION   [A]") };
	const FName StripHandlers[3] = { GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleWeaponChosen),
		GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleMoveChosen), GET_FUNCTION_NAME_CHECKED(UHitmenLobbyWidget, HandleManipulationChosen) };
	for (int32 Row = 0; Row < 3; ++Row)
	{
		HitmenUI::AddToVBox(Loadout, MakeSectionLabel(WidgetTree, RowNames[Row]), FMargin(0.f, Row == 0 ? 0.f : 16.f, 0.f, 8.f));
		UHitmenChoiceStrip* Strip = WidgetTree->ConstructWidget<UHitmenChoiceStrip>(UHitmenChoiceStrip::StaticClass());
		Strip->StripIndex = Row;
		FScriptDelegate Delegate;
		Delegate.BindUFunction(this, StripHandlers[Row]);
		Strip->OnChosen.Add(Delegate);
		HitmenUI::AddToVBox(Loadout, Strip, FMargin(0.f), false, HAlign_Left);
		LoadoutStrips.Add(Strip);
	}

	// Launch countdown: one big number in a square block, centred at the top.
	UBorder* Countdown = HitmenUI::MakePanel(WidgetTree, FLinearColor(0.f, 0.f, 0.f, 0.82f), FMargin(0.f));
	CountdownText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 96, TEXT("Bold"));
	CountdownText->SetJustification(ETextJustify::Center);
	Countdown->SetContent(CountdownText);
	Countdown->SetHorizontalAlignment(HAlign_Center);
	Countdown->SetVerticalAlignment(VAlign_Center);
	CountdownBlock = Sized(WidgetTree, Countdown, 150.f, 150.f);
	CountdownBlock->SetVisibility(ESlateVisibility::Collapsed);
	HitmenUI::AddToCanvas(AgentCanvas, CountdownBlock, FAnchors(0.5f, 0.f), FVector2D(0.f, 60.f), FVector2D(0.5f, 0.f));

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
	// Edited look: sent once the clicks pause (the stage already shows it).
	if (CosmeticSendDelay >= 0.f)
	{
		CosmeticSendDelay -= InDeltaTime;
		if (CosmeticSendDelay < 0.f)
		{
			if (AHitmenPlayerController* PC = GetHitmenPC())
			{
				HitmenLobby::SendCosmetics(PC, LocalCosmetics);
			}
		}
	}
	if (ScreenSwitcher && ScreenSwitcher->GetActiveWidgetIndex() == 1)
	{
		UpdateAgentAnchors();
	}
	// Everyone ready: the match starts after a short beat, no number on screen.
	if (CountdownBlock)
	{
		CountdownBlock->SetVisibility(ESlateVisibility::Collapsed);
	}
}

TArray<int32> UHitmenLobbyWidget::GetCarouselMaps() const
{
	if (!bCarouselMapsCached)
	{
		bCarouselMapsCached = true;
		CachedCarouselMaps.Reset();
		const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
		for (int32 Index = 0; Index < Settings->GetMapCount(); ++Index)
		{
			if (const UHitmenMapDefinition* Map = Settings->LoadMap(Index); Map && Map->bShowInCarousel)
			{
				CachedCarouselMaps.Add(Index);
			}
		}
	}
	return CachedCarouselMaps;
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
	if (MapTile && ShownMapIndex != Settings.MapIndex)
	{
		ShownMapIndex = Settings.MapIndex;
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
	if (RoomCodeText)
	{
		const FString& Code = GS->GetRoomCode();
		RoomCodeText->SetText(Code.IsEmpty() ? FText::FromString(TEXT("-")) : FText::FromString(Code));
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
			HitmenUI::AddToVBox(TeamsBox, Panel, FMargin(0.f, 0.f, 0.f, 12.f));
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

void UHitmenLobbyWidget::PreviewLocalCosmetics()
{
	if (AHitmenAgentPreview* Preview = FindAgentPreview())
	{
		Preview->ShowCosmetics(LocalCosmetics);
	}
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
		// Default loadout: the first weapon (silenced pistol), no abilities.
		if (Catalog && !LocalLoadout.Weapon && !LocalLoadout.MoveAbility && !LocalLoadout.ManipulationAbility && Catalog->Weapons.Num() > 0)
		{
			LocalLoadout.Weapon = Catalog->Weapons[0];
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
	// While the local look is being edited the stage keeps the local copy.
	PreviewLocalCosmetics();
	RefreshLoadoutStrips();
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
	if (ReadyStatusText)
	{
		ReadyStatusText->SetText(FText::Format(LOCTEXT("ReadyCount", "{0} / {1} ready"), FText::AsNumber(ReadyCount), FText::AsNumber(Needed)));
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
	// Arrows sit right beside the head, the torso and the legs, in two columns hugging the body.
	FVector2D Center;
	if (!UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, Body->GetBoneLocation(TEXT("pelvis")), Center, true))
	{
		return;
	}
	for (int32 LookIndex = 0; LookIndex < 3; ++LookIndex)
	{
		FVector2D Anchor;
		if (!UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, Body->GetBoneLocation(LookBones[LookIndex]), Anchor, true))
		{
			continue;
		}
		for (int32 Side = 0; Side < 2; ++Side)
		{
			if (UCanvasPanelSlot* ArrowSlot = Cast<UCanvasPanelSlot>(LookArrows[LookIndex * 2 + Side]->Slot))
			{
				ArrowSlot->SetPosition(FVector2D(Center.X + (Side == 0 ? -LookArrowSpread : LookArrowSpread), Anchor.Y));
			}
		}
	}
	// Codename above the head, Randomize under the feet.
	FVector2D Head;
	if (AgentNameBox && UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, Body->GetBoneLocation(TEXT("head")) + FVector(0.f, 0.f, 32.f), Head, true))
	{
		if (UCanvasPanelSlot* NameSlot = Cast<UCanvasPanelSlot>(AgentNameBox->Slot))
		{
			NameSlot->SetPosition(FVector2D(Center.X, Head.Y - 36.f));
		}
	}
	FVector2D Feet;
	if (RandomizeButton && UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, Body->GetComponentLocation(), Feet, true))
	{
		if (UCanvasPanelSlot* RandomSlot = Cast<UCanvasPanelSlot>(RandomizeButton->Slot))
		{
			RandomSlot->SetPosition(FVector2D(Center.X, Feet.Y + 18.f));
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

TArray<FHitmenChoiceItem> UHitmenLobbyWidget::GetLoadoutChoices(int32 CardIndex, int32& OutSelected) const
{
	TArray<FHitmenChoiceItem> Choices;
	auto Item = [](const FText& Name, UTexture2D* Icon)
	{
		FHitmenChoiceItem Choice;
		Choice.Name = Name;
		Choice.Icon = Icon;
		return Choice;
	};
	OutSelected = 0;
	const UHitmenCatalog* Catalog = GetCatalog();
	if (!Catalog)
	{
		return Choices;
	}
	// Every row starts with None (fists for the weapon).
	Choices.Add(Item(CardIndex == 0 ? LOCTEXT("FistsChoice", "None") : LOCTEXT("NoneChoice", "None"), nullptr));
	if (CardIndex == 0)
	{
		for (int32 Index = 0; Index < Catalog->Weapons.Num(); ++Index)
		{
			const UHitmenWeaponData* Weapon = Catalog->Weapons[Index];
			Choices.Add(Item(Weapon ? Weapon->DisplayName : FText::GetEmpty(), Weapon ? Weapon->Icon.LoadSynchronous() : nullptr));
			OutSelected = Weapon && Weapon == LocalLoadout.Weapon ? Index + 1 : OutSelected;
		}
		return Choices;
	}
	const EHitmenAbilitySlot AbilitySlot = CardIndex == 1 ? EHitmenAbilitySlot::Move : EHitmenAbilitySlot::Manipulation;
	const UHitmenAbilityData* Current = CardIndex == 1 ? LocalLoadout.MoveAbility.Get() : LocalLoadout.ManipulationAbility.Get();
	const TArray<UHitmenAbilityData*> Abilities = Catalog->GetAbilities(AbilitySlot);
	for (int32 Index = 0; Index < Abilities.Num(); ++Index)
	{
		const UHitmenAbilityData* Ability = Abilities[Index];
		Choices.Add(Item(Ability ? Ability->DisplayName : FText::GetEmpty(), Ability ? Ability->Icon.LoadSynchronous() : nullptr));
		OutSelected = Ability && Ability == Current ? Index + 1 : OutSelected;
	}
	return Choices;
}

void UHitmenLobbyWidget::RefreshLoadoutStrips()
{
	for (int32 Row = 0; Row < LoadoutStrips.Num(); ++Row)
	{
		int32 Selected = 0;
		const TArray<FHitmenChoiceItem> Choices = GetLoadoutChoices(Row, Selected);
		LoadoutStrips[Row]->SetChoices(Choices, Selected, LoadoutTileSize, LoadoutColumns);
	}
}

void UHitmenLobbyWidget::HandleWeaponChosen(int32 Index) { ApplyLoadoutChoice(0, Index); }
void UHitmenLobbyWidget::HandleMoveChosen(int32 Index) { ApplyLoadoutChoice(1, Index); }
void UHitmenLobbyWidget::HandleManipulationChosen(int32 Index) { ApplyLoadoutChoice(2, Index); }

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
	if (LoadoutStrips.IsValidIndex(CardIndex))
	{
		LoadoutStrips[CardIndex]->SetSelected(ChoiceIndex);
	}
}

// =============================================================================================
// Actions
// =============================================================================================

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
	// Instant on the stage; the server gets the result once the clicks pause.
	LocalCosmetics.SetSlot(CosmeticSlot, CycleIn(Catalog->GetCosmeticsForSlot(CosmeticSlot), LocalCosmetics.GetSlot(CosmeticSlot), Direction, true));
	PreviewLocalCosmetics();
	CosmeticSendDelay = CosmeticSendPause;
}

void UHitmenLobbyWidget::RandomizeCosmetics()
{
	if (const UHitmenCatalog* Catalog = GetCatalog())
	{
		LocalCosmetics = Catalog->MakeRandomCosmetics();
		PreviewLocalCosmetics();
		CosmeticSendDelay = CosmeticSendPause;
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
