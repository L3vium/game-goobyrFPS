// Hitmen — lobby screens

#include "UI/HitmenLobbyWidgets.h"
#include "UI/HitmenUIFactory.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenPlayerState.h"
#include "Framework/HitmenGameState.h"
#include "Framework/HitmenGameInstance.h"
#include "Core/HitmenGameSettings.h"
#include "Core/HitmenUserSettings.h"
#include "Data/HitmenCatalog.h"
#include "Data/HitmenMatchRules.h"
#include "Data/HitmenMapDefinition.h"
#include "Data/HitmenCosmeticItem.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenAbilityData.h"
#include "Blueprint/WidgetTree.h"
#include "Blueprint/WidgetBlueprintLibrary.h"
#include "Components/Button.h"
#include "Components/TextBlock.h"
#include "Components/EditableTextBox.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/VerticalBox.h"
#include "Components/HorizontalBox.h"
#include "Components/SizeBox.h"
#include "Components/Border.h"
#include "Components/Image.h"
#include "Components/ScrollBox.h"
#include "Components/WidgetSwitcher.h"
#include "Engine/Texture2D.h"
#include "Framework/Application/SlateApplication.h"

#define LOCTEXT_NAMESPACE "HitmenLobby"

namespace
{
	const EHitmenCosmeticSlot CosmeticSlots[] = { EHitmenCosmeticSlot::Head, EHitmenCosmeticSlot::Body, EHitmenCosmeticSlot::Legs };

	FText CosmeticSlotName(EHitmenCosmeticSlot Slot)
	{
		switch (Slot)
		{
		case EHitmenCosmeticSlot::Head: return LOCTEXT("Head", "HEAD");
		case EHitmenCosmeticSlot::Body: return LOCTEXT("Body", "BODY");
		default:                        return LOCTEXT("Legs", "LEGS");
		}
	}

	FText DifficultyName(EHitmenDifficulty Difficulty)
	{
		switch (Difficulty)
		{
		case EHitmenDifficulty::Easy: return LOCTEXT("Easy", "EASY");
		case EHitmenDifficulty::Hard: return LOCTEXT("Hard", "HARD");
		default:                      return LOCTEXT("Normal", "NORMAL");
		}
	}

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
}

// =============================================================================================
// Player entry
// =============================================================================================

void UHitmenPlayerEntryWidget::BuildDefaultLayout()
{
	UBorder* Row = HitmenUI::MakePanel(WidgetTree, HitmenUI::PanelLight(), FMargin(10.f, 6.f));
	UHorizontalBox* Box = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());

	UTextBlock* IconLabel = nullptr;
	IconButton = HitmenUI::MakeButton(WidgetTree, FText::GetEmpty(), 16, EHitmenButtonStyle::Ghost, &IconLabel);
	IconText = IconLabel;
	USizeBox* IconSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	IconSize->SetWidthOverride(56.f);
	IconSize->SetHeightOverride(40.f);
	IconSize->SetContent(IconButton);
	HitmenUI::AddToHBox(Box, IconSize);

	NameText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 18);
	HitmenUI::AddToHBox(Box, NameText, FMargin(8.f, 0.f), true);

	NameEdit = WidgetTree->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass());
	NameEdit->SetVisibility(ESlateVisibility::Collapsed);
	HitmenUI::AddToHBox(Box, NameEdit, FMargin(8.f, 0.f), true);

	UTextBlock* WatcherLabel = nullptr;
	WatcherButton = HitmenUI::MakeButton(WidgetTree, FText::GetEmpty(), 13, EHitmenButtonStyle::Dashed, &WatcherLabel);
	WatcherText = WatcherLabel;
	HitmenUI::AddToHBox(Box, WatcherButton);

	Row->SetContent(Box);
	WidgetTree->RootWidget = Row;

	IconButton->OnClicked.AddDynamic(this, &UHitmenPlayerEntryWidget::HandleIconClicked);
	WatcherButton->OnClicked.AddDynamic(this, &UHitmenPlayerEntryWidget::HandleWatcherClicked);
	NameEdit->OnTextCommitted.AddDynamic(this, &UHitmenPlayerEntryWidget::HandleNameCommitted);
}

void UHitmenPlayerEntryWidget::SetPlayer(AHitmenPlayerState* InPlayerState)
{
	PlayerState = InPlayerState;
	Refresh();
}

bool UHitmenPlayerEntryWidget::IsLocalPlayer() const
{
	return PlayerState && GetOwningPlayer() && GetOwningPlayer()->PlayerState == PlayerState;
}

void UHitmenPlayerEntryWidget::Refresh()
{
	if (!PlayerState)
	{
		return;
	}
	const bool bLocal = IsLocalPlayer();
	const FText Name = FText::FromString(PlayerState->GetPlayerName());
	if (NameText)
	{
		NameText->SetText(Name);
		NameText->SetVisibility(bLocal ? ESlateVisibility::Collapsed : ESlateVisibility::HitTestInvisible);
	}
	if (NameEdit)
	{
		NameEdit->SetText(Name);
		NameEdit->SetVisibility(bLocal ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	}
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
	const int32 Icon = PlayerState->GetIconIndex();
	UTexture2D* IconTexture = Catalog && Catalog->PlayerIcons.IsValidIndex(Icon) ? Catalog->PlayerIcons[Icon].LoadSynchronous() : nullptr;
	if (IconImage)
	{
		IconImage->SetVisibility(IconTexture ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
		if (IconTexture)
		{
			IconImage->SetBrushFromTexture(IconTexture);
		}
	}
	if (IconText)
	{
		const bool bGlyph = !IconTexture && Catalog && Catalog->FallbackIconGlyphs.Num() > 0;
		IconText->SetText(bGlyph ? FText::FromString(Catalog->FallbackIconGlyphs[Icon % Catalog->FallbackIconGlyphs.Num()])
			: IconTexture ? FText::GetEmpty() : FText::Format(LOCTEXT("Icon", "#{0}"), FText::AsNumber(Icon + 1)));
	}
	if (IconButton && Catalog && !IconTexture && Catalog->FallbackIconColors.Num() > 0)
	{
		IconButton->SetBackgroundColor(Catalog->FallbackIconColors[Icon % Catalog->FallbackIconColors.Num()]);
	}
	if (WatcherButton)
	{
		WatcherButton->SetVisibility(PlayerState->IsInTeam() ? ESlateVisibility::Collapsed : ESlateVisibility::Visible);
	}
	if (WatcherText)
	{
		WatcherText->SetText(PlayerState->IsWatcher() ? LOCTEXT("Watcher", "WATCHER") : LOCTEXT("Playing", "PLAYING"));
	}
}

void UHitmenPlayerEntryWidget::HandleNameCommitted(const FText& Text, ETextCommit::Type CommitType)
{
	if (!IsLocalPlayer() || Text.IsEmpty())
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

void UHitmenPlayerEntryWidget::HandleIconClicked()
{
	if (!IsLocalPlayer())
	{
		return;
	}
	const UHitmenCatalog* Catalog = UHitmenGameSettings::Get()->LoadCatalog();
	const int32 Count = Catalog ? Catalog->GetIconCount() : 1;
	const int32 Next = (PlayerState->GetIconIndex() + 1) % Count;
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_SetIcon(Next);
	}
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->PreferredIconIndex = Next;
		Settings->ApplyHitmenSettings();
	}
}

void UHitmenPlayerEntryWidget::HandleWatcherClicked()
{
	if (AHitmenPlayerController* PC = GetHitmenPC(); PC && PlayerState)
	{
		PC->Server_SetWatcher(PlayerState, !PlayerState->IsWatcher());
	}
}

FReply UHitmenPlayerEntryWidget::NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent)
{
	const AHitmenPlayerController* PC = GetHitmenPC();
	if (PC && PC->IsHost() && InMouseEvent.GetEffectingButton() == EKeys::LeftMouseButton)
	{
		return UWidgetBlueprintLibrary::DetectDragIfPressed(InMouseEvent, this, EKeys::LeftMouseButton).NativeReply;
	}
	return Super::NativeOnMouseButtonDown(InGeometry, InMouseEvent);
}

void UHitmenPlayerEntryWidget::NativeOnDragDetected(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent, UDragDropOperation*& OutOperation)
{
	UHitmenPlayerDragOperation* Operation = NewObject<UHitmenPlayerDragOperation>(this);
	Operation->PlayerState = PlayerState;
	Operation->Pivot = EDragPivot::MouseDown;
	UTextBlock* Visual = HitmenUI::MakeText(WidgetTree, FText::FromString(PlayerState ? PlayerState->GetPlayerName() : FString()), 18);
	Operation->DefaultDragVisual = Visual;
	OutOperation = Operation;
}

// =============================================================================================
// Team panel
// =============================================================================================

void UHitmenTeamPanelWidget::BuildDefaultLayout()
{
	UBorder* Panel = HitmenUI::MakePanel(WidgetTree, HitmenUI::Panel(), FMargin(12.f));
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UHorizontalBox* Header = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	TitleText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 20, TEXT("Black"));
	HitmenUI::AddToHBox(Header, TitleText, FMargin(0.f, 0.f, 12.f, 0.f), true);
	UTextBlock* RoleLabel = nullptr;
	RoleButton = HitmenUI::MakeButton(WidgetTree, FText::GetEmpty(), 13, EHitmenButtonStyle::Solid, &RoleLabel);
	RoleText = RoleLabel;
	HitmenUI::AddToHBox(Header, RoleButton);
	RemoveButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Remove", "X"), 13, EHitmenButtonStyle::Ghost);
	HitmenUI::AddToHBox(Header, RemoveButton);
	HitmenUI::AddToVBox(Column, Header, FMargin(0.f, 0.f, 0.f, 8.f));
	MemberList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Column, MemberList);
	Panel->SetContent(Column);
	WidgetTree->RootWidget = Panel;

	RoleButton->OnClicked.AddDynamic(this, &UHitmenTeamPanelWidget::HandleRoleClicked);
	RemoveButton->OnClicked.AddDynamic(this, &UHitmenTeamPanelWidget::HandleRemoveClicked);
}

void UHitmenTeamPanelWidget::SetTeam(int32 InTeamId)
{
	TeamId = InTeamId;
	Refresh();
}

void UHitmenTeamPanelWidget::Refresh()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const AHitmenPlayerController* PC = GetHitmenPC();
	if (!GS)
	{
		return;
	}
	const bool bHost = PC && PC->IsHost();
	const FHitmenTeam* Team = GS->FindTeam(TeamId);
	if (TitleText)
	{
		TitleText->SetText(Team ? Team->DisplayName : LOCTEXT("Players", "PLAYERS"));
		TitleText->SetColorAndOpacity(FSlateColor(Team ? Team->Color : HitmenUI::Text()));
	}
	if (RoleButton)
	{
		RoleButton->SetVisibility(Team ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
		RoleButton->SetIsEnabled(bHost);
	}
	if (RoleText && Team)
	{
		RoleText->SetText(Team->Role == EHitmenTeamRole::Protect ? LOCTEXT("Protect", "PROTECT") : LOCTEXT("Attack", "ATTACK"));
	}
	if (RemoveButton)
	{
		RemoveButton->SetVisibility(Team && bHost ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	}
	if (MemberList)
	{
		MemberList->ClearChildren();
		for (AHitmenPlayerState* PS : GS->GetPlayersInTeam(TeamId))
		{
			UHitmenPlayerEntryWidget* Entry = WidgetTree->ConstructWidget<UHitmenPlayerEntryWidget>(UHitmenPlayerEntryWidget::StaticClass());
			Entry->SetPlayer(PS);
			HitmenUI::AddToVBox(MemberList, Entry, FMargin(0.f, 2.f));
		}
		if (GS->GetPlayersInTeam(TeamId).IsEmpty())
		{
			HitmenUI::AddToVBox(MemberList, HitmenUI::MakeText(WidgetTree, LOCTEXT("DropHere", "Drag players here"), 14, TEXT("Regular"), HitmenUI::TextDim()));
		}
	}
}

bool UHitmenTeamPanelWidget::NativeOnDrop(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation)
{
	const UHitmenPlayerDragOperation* Operation = Cast<UHitmenPlayerDragOperation>(InOperation);
	AHitmenPlayerController* PC = GetHitmenPC();
	if (!Operation || !Operation->PlayerState || !PC)
	{
		return false;
	}
	PC->Server_AssignTeam(Operation->PlayerState, TeamId);
	return true;
}

void UHitmenTeamPanelWidget::HandleRoleClicked()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const FHitmenTeam* Team = GS ? GS->FindTeam(TeamId) : nullptr;
	if (AHitmenPlayerController* PC = GetHitmenPC(); PC && Team)
	{
		PC->Server_SetTeamRole(TeamId, Team->Role == EHitmenTeamRole::Attack ? EHitmenTeamRole::Protect : EHitmenTeamRole::Attack);
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
// Lobby
// =============================================================================================

void UHitmenLobbyWidget::BuildDefaultLayout()
{
	UBorder* Background = HitmenUI::MakePanel(WidgetTree, HitmenUI::Background(), FMargin(60.f, 50.f));
	ScreenSwitcher = WidgetTree->ConstructWidget<UWidgetSwitcher>(UWidgetSwitcher::StaticClass());
	Background->SetContent(ScreenSwitcher);
	WidgetTree->RootWidget = Background;

	// ---------------- Create Room screen ----------------
	UCanvasPanel* LobbyCanvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	ScreenSwitcher->AddChild(LobbyCanvas);

	HitmenUI::AddToCanvas(LobbyCanvas, HitmenUI::MakeText(WidgetTree, LOCTEXT("CreateRoom", "CREATE ROOM"), 54, TEXT("Black")), FAnchors(0.f, 0.f), FVector2D::ZeroVector, FVector2D::ZeroVector);

	// Left: map carousel + options.
	UVerticalBox* Left = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Left, HitmenUI::MakeText(WidgetTree, LOCTEXT("Map", "MAP"), 18, TEXT("Bold"), HitmenUI::TextDim()));
	UHorizontalBox* Carousel = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	PrevMapButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Prev", "<"), 28, EHitmenButtonStyle::Ghost);
	NextMapButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Next", ">"), 28, EHitmenButtonStyle::Ghost);
	UVerticalBox* MapCard = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	USizeBox* ThumbSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	ThumbSize->SetWidthOverride(420.f);
	ThumbSize->SetHeightOverride(236.f);
	MapThumbnail = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
	MapThumbnail->SetColorAndOpacity(HitmenUI::PanelLight());
	ThumbSize->SetContent(MapThumbnail);
	HitmenUI::AddToVBox(MapCard, ThumbSize);
	MapNameText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 28, TEXT("Black"));
	HitmenUI::AddToVBox(MapCard, MapNameText, FMargin(0.f, 8.f));
	HitmenUI::AddToHBox(Carousel, PrevMapButton);
	HitmenUI::AddToHBox(Carousel, MapCard);
	HitmenUI::AddToHBox(Carousel, NextMapButton);
	HitmenUI::AddToVBox(Left, Carousel, FMargin(0.f, 6.f, 0.f, 20.f));
	HitmenUI::AddToVBox(Left, HitmenUI::MakeText(WidgetTree, LOCTEXT("Options", "OPTIONS"), 18, TEXT("Bold"), HitmenUI::TextDim()));
	OptionsBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Left, OptionsBox);
	HitmenUI::AddToCanvas(LobbyCanvas, Left, FAnchors(0.f, 0.f), FVector2D(0.f, 110.f), FVector2D::ZeroVector);

	// Right: players and teams, vertical.
	UScrollBox* RightScroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	UVerticalBox* Right = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	TeamsBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Right, TeamsBox);
	CreateTeamButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("CreateTeam", "+  CREATE TEAM"), 18, EHitmenButtonStyle::Dashed);
	HitmenUI::AddToVBox(Right, CreateTeamButton, FMargin(0.f, 10.f));
	RightScroll->AddChild(Right);
	UCanvasPanelSlot* RightSlot = HitmenUI::AddToCanvas(LobbyCanvas, RightScroll, FAnchors(0.55f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	RightSlot->SetOffsets(FMargin(0.f, 110.f, 0.f, 90.f));

	BackButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Back", "BACK"), 22, EHitmenButtonStyle::Solid);
	HitmenUI::AddToCanvas(LobbyCanvas, BackButton, FAnchors(0.f, 1.f), FVector2D::ZeroVector, FVector2D(0.f, 1.f));
	StartButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Start", "START"), 22, EHitmenButtonStyle::Accent);
	HitmenUI::AddToCanvas(LobbyCanvas, StartButton, FAnchors(1.f, 1.f), FVector2D::ZeroVector, FVector2D(1.f, 1.f));

	// ---------------- Customization screen ----------------
	UCanvasPanel* CustomCanvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	ScreenSwitcher->AddChild(CustomCanvas);
	HitmenUI::AddToCanvas(CustomCanvas, HitmenUI::MakeText(WidgetTree, LOCTEXT("Customize", "YOUR AGENT"), 54, TEXT("Black")), FAnchors(0.f, 0.f), FVector2D::ZeroVector, FVector2D::ZeroVector);

	UVerticalBox* LookColumn = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(LookColumn, HitmenUI::MakeText(WidgetTree, LOCTEXT("Name", "NAME"), 18, TEXT("Bold"), HitmenUI::TextDim()));
	AgentNameEdit = WidgetTree->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass());
	HitmenUI::AddToVBox(LookColumn, AgentNameEdit, FMargin(0.f, 4.f, 0.f, 16.f));
	HitmenUI::AddToVBox(LookColumn, HitmenUI::MakeText(WidgetTree, LOCTEXT("Look", "LOOK"), 18, TEXT("Bold"), HitmenUI::TextDim()));
	CosmeticsBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(LookColumn, CosmeticsBox);
	HitmenUI::AddToCanvas(CustomCanvas, LookColumn, FAnchors(0.f, 0.f), FVector2D(0.f, 110.f), FVector2D::ZeroVector);

	UVerticalBox* GearColumn = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(GearColumn, HitmenUI::MakeText(WidgetTree, LOCTEXT("Loadout", "LOADOUT"), 18, TEXT("Bold"), HitmenUI::TextDim()));
	LoadoutBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(GearColumn, LoadoutBox);
	HitmenUI::AddToCanvas(CustomCanvas, GearColumn, FAnchors(1.f, 0.f), FVector2D(0.f, 110.f), FVector2D(1.f, 0.f));

	CustomBackButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("BackToLobby", "BACK"), 22, EHitmenButtonStyle::Solid);
	HitmenUI::AddToCanvas(CustomCanvas, CustomBackButton, FAnchors(0.f, 1.f), FVector2D::ZeroVector, FVector2D(0.f, 1.f));
	UHorizontalBox* RightButtons = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UTextBlock* ReadyLabel = nullptr;
	ReadyButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Ready", "READY"), 22, EHitmenButtonStyle::Dashed, &ReadyLabel);
	ReadyText = ReadyLabel;
	LaunchButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Launch", "LAUNCH"), 22, EHitmenButtonStyle::Accent);
	HitmenUI::AddToHBox(RightButtons, ReadyButton, FMargin(0.f, 0.f, 12.f, 0.f));
	HitmenUI::AddToHBox(RightButtons, LaunchButton);
	HitmenUI::AddToCanvas(CustomCanvas, RightButtons, FAnchors(1.f, 1.f), FVector2D::ZeroVector, FVector2D(1.f, 1.f));
}

void UHitmenLobbyWidget::BindEvents()
{
	if (PrevMapButton) { PrevMapButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::PreviousMap); }
	if (NextMapButton) { NextMapButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::NextMap); }
	if (CreateTeamButton) { CreateTeamButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::CreateTeam); }
	if (BackButton) { BackButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::Back); }
	if (StartButton) { StartButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::Start); }
	if (ReadyButton) { ReadyButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::ToggleReady); }
	if (LaunchButton) { LaunchButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::Launch); }
	if (CustomBackButton) { CustomBackButton->OnClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::Back); }
	if (AgentNameEdit) { AgentNameEdit->OnTextCommitted.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleAgentNameCommitted); }
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

void UHitmenLobbyWidget::HandlePhaseChanged(EHitmenMatchPhase Phase)
{
	HandleLobbyChanged();
}

void UHitmenLobbyWidget::HandleLobbyChanged()
{
	if (IsEditingText())
	{
		// Rebuilding now would wipe the name being typed; NativeTick refreshes once editing ends.
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
	if (bCustomization)
	{
		RefreshCustomization();
	}
	else
	{
		RefreshLobby();
	}
}

// ---- Create Room ----

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

void UHitmenLobbyWidget::RefreshLobby()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	if (!GS)
	{
		return;
	}
	const bool bHost = IsHost();
	const TArray<int32> Maps = GetCarouselMaps();
	const int32 MapIndex = GS->GetLobbySettings().MapIndex;
	const int32 CarouselIndex = Maps.IndexOfByKey(MapIndex);
	if (const UHitmenMapDefinition* Map = UHitmenGameSettings::Get()->LoadMap(MapIndex))
	{
		if (MapNameText) { MapNameText->SetText(Map->DisplayName); }
		if (MapThumbnail)
		{
			if (UTexture2D* Thumb = Map->Thumbnail.LoadSynchronous())
			{
				MapThumbnail->SetBrushFromTexture(Thumb);
				MapThumbnail->SetColorAndOpacity(FLinearColor::White);
			}
		}
	}
	// Arrows disappear at the ends of the carousel.
	if (PrevMapButton) { PrevMapButton->SetVisibility(bHost && CarouselIndex > 0 ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
	if (NextMapButton) { NextMapButton->SetVisibility(bHost && CarouselIndex != INDEX_NONE && CarouselIndex < Maps.Num() - 1 ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
	if (CreateTeamButton) { CreateTeamButton->SetVisibility(bHost ? ESlateVisibility::Visible : ESlateVisibility::Collapsed); }
	if (StartButton) { StartButton->SetVisibility(bHost ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
	RebuildOptions();
	RebuildTeams();
}

void UHitmenLobbyWidget::RebuildOptions()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const UHitmenMatchRules* Rules = UHitmenGameSettings::Get()->LoadDefaultRules();
	if (!OptionsBox || !GS)
	{
		return;
	}
	OptionsBox->ClearChildren();
	const FHitmenLobbySettings& Settings = GS->GetLobbySettings();
	const bool bHost = IsHost();
	auto AddOption = [this, bHost](const FText& Label, int32 Index)
	{
		UHitmenListButton* Button = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Button->Setup(Label, Index, EHitmenButtonStyle::Solid, 16);
		Button->SetIsEnabled(bHost);
		Button->OnItemClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleOptionClicked);
		HitmenUI::AddToVBox(OptionsBox, Button, FMargin(0.f, 3.f), false, HAlign_Left);
	};
	if (!Rules || Rules->ProximityChat.bShowInLobbyUI)
	{
		AddOption(FText::Format(LOCTEXT("Proximity", "PROXIMITY CHAT: {0}"), Settings.bProximityChat ? LOCTEXT("On", "ON") : LOCTEXT("Off", "OFF")), 0);
	}
	// "-  LABEL  +" row; the buttons send MinusIndex / MinusIndex + 1 to HandleOptionClicked.
	auto AddStepper = [this, bHost](const FText& Label, int32 MinusIndex)
	{
		UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		UHitmenListButton* Minus = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Minus->Setup(LOCTEXT("Minus", "-"), MinusIndex, EHitmenButtonStyle::Solid, 16);
		Minus->SetIsEnabled(bHost);
		Minus->OnItemClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleOptionClicked);
		UHitmenListButton* Plus = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Plus->Setup(LOCTEXT("Plus", "+"), MinusIndex + 1, EHitmenButtonStyle::Solid, 16);
		Plus->SetIsEnabled(bHost);
		Plus->OnItemClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleOptionClicked);
		HitmenUI::AddToHBox(Row, Minus);
		HitmenUI::AddToHBox(Row, HitmenUI::MakeText(WidgetTree, Label, 16), FMargin(12.f, 0.f));
		HitmenUI::AddToHBox(Row, Plus);
		HitmenUI::AddToVBox(OptionsBox, Row, FMargin(0.f, 3.f), false, HAlign_Left);
	};
	auto Seconds = [](float Value) { return FText::AsNumber(FMath::RoundToInt(Value)); };
	if (!Rules || Rules->RetrievalPointCount.bShowInLobbyUI)
	{
		AddStepper(FText::Format(LOCTEXT("Retrieval", "RETRIEVAL POINTS: {0}"), FText::AsNumber(Settings.RetrievalPointCount)), 1);
	}
	if (!Rules || Rules->Difficulty.bShowInLobbyUI)
	{
		AddOption(FText::Format(LOCTEXT("Difficulty", "DIFFICULTY: {0}"), DifficultyName(Settings.Difficulty)), 3);
	}
	if (!Rules || Rules->SonarStartInterval.bShowInLobbyUI)
	{
		AddStepper(FText::Format(LOCTEXT("SonarStart", "SONAR FIRST INTERVAL: {0}s"), Seconds(Settings.SonarStartInterval)), 4);
	}
	if (!Rules || Rules->SonarEndInterval.bShowInLobbyUI)
	{
		AddStepper(FText::Format(LOCTEXT("SonarEnd", "SONAR FASTEST INTERVAL: {0}s"), Seconds(Settings.SonarEndInterval)), 6);
	}
	if (!Rules || Rules->SonarRampDuration.bShowInLobbyUI)
	{
		AddStepper(FText::Format(LOCTEXT("SonarRamp", "SONAR SPEED-UP TIME: {0}s"), Seconds(Settings.SonarRampDuration)), 8);
	}
}

void UHitmenLobbyWidget::RebuildTeams()
{
	const AHitmenGameState* GS = GetHitmenGameState();
	if (!TeamsBox || !GS)
	{
		return;
	}
	TeamsBox->ClearChildren();
	TeamPanels.Reset();
	auto AddPanel = [this](int32 TeamId)
	{
		UHitmenTeamPanelWidget* Panel = WidgetTree->ConstructWidget<UHitmenTeamPanelWidget>(UHitmenTeamPanelWidget::StaticClass());
		Panel->SetTeam(TeamId);
		HitmenUI::AddToVBox(TeamsBox, Panel, FMargin(0.f, 6.f));
		TeamPanels.Add(Panel);
	};
	AddPanel(INDEX_NONE);
	for (const FHitmenTeam& Team : GS->GetTeams())
	{
		AddPanel(Team.TeamId);
	}
}

void UHitmenLobbyWidget::SendSettings(const FHitmenLobbySettings& Settings)
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
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

void UHitmenLobbyWidget::ToggleProximityChat()
{
	if (const AHitmenGameState* GS = GetHitmenGameState())
	{
		FHitmenLobbySettings Settings = GS->GetLobbySettings();
		Settings.bProximityChat = !Settings.bProximityChat;
		SendSettings(Settings);
	}
}

void UHitmenLobbyWidget::DecreaseRetrievalPoints()
{
	if (const AHitmenGameState* GS = GetHitmenGameState())
	{
		FHitmenLobbySettings Settings = GS->GetLobbySettings();
		Settings.RetrievalPointCount -= 1;
		SendSettings(Settings);
	}
}

void UHitmenLobbyWidget::IncreaseRetrievalPoints()
{
	if (const AHitmenGameState* GS = GetHitmenGameState())
	{
		FHitmenLobbySettings Settings = GS->GetLobbySettings();
		Settings.RetrievalPointCount += 1;
		SendSettings(Settings);
	}
}

void UHitmenLobbyWidget::CycleDifficulty()
{
	if (const AHitmenGameState* GS = GetHitmenGameState())
	{
		FHitmenLobbySettings Settings = GS->GetLobbySettings();
		Settings.Difficulty = static_cast<EHitmenDifficulty>((static_cast<uint8>(Settings.Difficulty) + 1) % 3);
		SendSettings(Settings);
	}
}

void UHitmenLobbyWidget::HandleOptionClicked(int32 Index)
{
	switch (Index)
	{
	case 0: ToggleProximityChat(); break;
	case 1: DecreaseRetrievalPoints(); break;
	case 2: IncreaseRetrievalPoints(); break;
	case 3: CycleDifficulty(); break;
	case 4: AdjustSonarOption(0, -1); break;
	case 5: AdjustSonarOption(0, 1); break;
	case 6: AdjustSonarOption(1, -1); break;
	case 7: AdjustSonarOption(1, 1); break;
	case 8: AdjustSonarOption(2, -1); break;
	case 9: AdjustSonarOption(2, 1); break;
	default: break;
	}
}

void UHitmenLobbyWidget::AdjustSonarOption(int32 Which, int32 Direction)
{
	const AHitmenGameState* GS = GetHitmenGameState();
	const UHitmenMatchRules* Rules = UHitmenGameSettings::Get()->LoadDefaultRules();
	if (!GS)
	{
		return;
	}
	FHitmenLobbySettings Settings = GS->GetLobbySettings();
	const float Step = (Rules ? Rules->SonarLobbyStep : 1.f) * FMath::Sign(Direction);
	// The ramp is long: step it ten times faster.
	switch (Which)
	{
	case 0: Settings.SonarStartInterval += Step; break;
	case 1: Settings.SonarEndInterval += Step * 0.5f; break;
	case 2: Settings.SonarRampDuration += Step * 10.f; break;
	default: return;
	}
	// The server clamps to each option's range.
	SendSettings(Settings);
}

void UHitmenLobbyWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	if (bLobbyRefreshPending && !IsEditingText())
	{
		bLobbyRefreshPending = false;
		HandleLobbyChanged();
	}
}

bool UHitmenLobbyWidget::IsEditingText() const
{
	if (!FSlateApplication::IsInitialized())
	{
		return false;
	}
	const TSharedPtr<SWidget> Focused = FSlateApplication::Get().GetKeyboardFocusedWidget();
	return Focused.IsValid() && Focused->GetType() == FName(TEXT("SEditableText")) && HasFocusedDescendants();
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

// ---- Customization ----

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
		// Sensible defaults: first entry of each loadout list.
		if (Catalog)
		{
			if (!LocalLoadout.Weapon && Catalog->Weapons.Num() > 0) { LocalLoadout.Weapon = Catalog->Weapons[0]; }
			if (!LocalLoadout.MoveAbility && Catalog->MoveAbilities.Num() > 0) { LocalLoadout.MoveAbility = Catalog->MoveAbilities[0]; }
			if (!LocalLoadout.ManipulationAbility && Catalog->ManipulationAbilities.Num() > 0) { LocalLoadout.ManipulationAbility = Catalog->ManipulationAbilities[0]; }
		}
		if (AHitmenPlayerController* PC = GetHitmenPC())
		{
			PC->Server_SetLoadout(LocalLoadout);
		}
	}
	if (AgentNameEdit && !AgentNameEdit->HasKeyboardFocus())
	{
		AgentNameEdit->SetText(FText::FromString(PS->GetPlayerName()));
	}

	auto AddRow = [this](UVerticalBox* Box, const FText& Label, const FText& Value, int32 RowIndex)
	{
		UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
		USizeBox* LabelSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		LabelSize->SetWidthOverride(170.f);
		LabelSize->SetContent(HitmenUI::MakeText(WidgetTree, Label, 16, TEXT("Bold"), HitmenUI::TextDim()));
		HitmenUI::AddToHBox(Row, LabelSize);
		UHitmenListButton* Prev = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Prev->Setup(LOCTEXT("PrevItem", "<"), RowIndex * 10, EHitmenButtonStyle::Ghost, 18);
		Prev->OnItemClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleCustomizationClicked);
		HitmenUI::AddToHBox(Row, Prev);
		USizeBox* ValueSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		ValueSize->SetWidthOverride(240.f);
		ValueSize->SetContent(HitmenUI::MakeText(WidgetTree, Value, 18));
		HitmenUI::AddToHBox(Row, ValueSize);
		UHitmenListButton* Next = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Next->Setup(LOCTEXT("NextItem", ">"), RowIndex * 10 + 1, EHitmenButtonStyle::Ghost, 18);
		Next->OnItemClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleCustomizationClicked);
		HitmenUI::AddToHBox(Row, Next);
		HitmenUI::AddToVBox(Box, Row, FMargin(0.f, 3.f), false, HAlign_Left);
	};

	if (CosmeticsBox)
	{
		CosmeticsBox->ClearChildren();
		for (int32 Index = 0; Index < UE_ARRAY_COUNT(CosmeticSlots); ++Index)
		{
			const UHitmenCosmeticItem* Item = LocalCosmetics.GetSlot(CosmeticSlots[Index]);
			AddRow(CosmeticsBox, CosmeticSlotName(CosmeticSlots[Index]), Item ? Item->DisplayName : LOCTEXT("None", "NONE"), Index);
		}
		UHitmenListButton* Randomize = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		Randomize->Setup(LOCTEXT("Randomize", "RANDOMIZE"), 70, EHitmenButtonStyle::Dashed, 16);
		Randomize->OnItemClicked.AddUniqueDynamic(this, &UHitmenLobbyWidget::HandleCustomizationClicked);
		HitmenUI::AddToVBox(CosmeticsBox, Randomize, FMargin(0.f, 10.f), false, HAlign_Left);
	}
	if (LoadoutBox)
	{
		LoadoutBox->ClearChildren();
		AddRow(LoadoutBox, LOCTEXT("Weapon", "WEAPON"), LocalLoadout.Weapon ? LocalLoadout.Weapon->DisplayName : LOCTEXT("Fists", "FISTS"), 4);
		AddRow(LoadoutBox, LOCTEXT("MoveItem", "MOVE (E)"), LocalLoadout.MoveAbility ? LocalLoadout.MoveAbility->DisplayName : LOCTEXT("None", "NONE"), 5);
		AddRow(LoadoutBox, LOCTEXT("ManipItem", "MANIPULATION (A)"), LocalLoadout.ManipulationAbility ? LocalLoadout.ManipulationAbility->DisplayName : LOCTEXT("None", "NONE"), 6);
	}
	if (ReadyText)
	{
		ReadyText->SetText(PS->IsReady() ? LOCTEXT("NotReady", "NOT READY") : LOCTEXT("ReadyUp", "READY"));
	}
	if (LaunchButton) { LaunchButton->SetVisibility(IsHost() ? ESlateVisibility::Visible : ESlateVisibility::Collapsed); }
	if (CustomBackButton) { CustomBackButton->SetVisibility(IsHost() ? ESlateVisibility::Visible : ESlateVisibility::Hidden); }
}

void UHitmenLobbyWidget::HandleCustomizationClicked(int32 Index)
{
	const int32 Row = Index / 10;
	const int32 Direction = (Index % 10) == 1 ? 1 : -1;
	if (Row >= 0 && Row < 4)
	{
		CycleCosmetic(CosmeticSlots[Row], Direction);
	}
	else if (Row == 4)
	{
		CycleWeapon(Direction);
	}
	else if (Row == 5)
	{
		CycleAbility(EHitmenAbilitySlot::Move, Direction);
	}
	else if (Row == 6)
	{
		CycleAbility(EHitmenAbilitySlot::Manipulation, Direction);
	}
	else if (Row == 7)
	{
		RandomizeCosmetics();
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
	if (AHitmenPlayerController* PC = GetHitmenPC()) { PC->Server_SetCosmetics(LocalCosmetics); }
	RefreshCustomization();
}

void UHitmenLobbyWidget::RandomizeCosmetics()
{
	if (const UHitmenCatalog* Catalog = GetCatalog())
	{
		LocalCosmetics = Catalog->MakeRandomCosmetics();
		if (AHitmenPlayerController* PC = GetHitmenPC()) { PC->Server_SetCosmetics(LocalCosmetics); }
		RefreshCustomization();
	}
}

void UHitmenLobbyWidget::CycleWeapon(int32 Direction)
{
	const UHitmenCatalog* Catalog = GetCatalog();
	if (!Catalog)
	{
		return;
	}
	TArray<UHitmenWeaponData*> Weapons;
	for (UHitmenWeaponData* Weapon : Catalog->Weapons) { Weapons.Add(Weapon); }
	LocalLoadout.Weapon = CycleIn(Weapons, LocalLoadout.Weapon.Get(), Direction, true);
	if (AHitmenPlayerController* PC = GetHitmenPC()) { PC->Server_SetLoadout(LocalLoadout); }
	RefreshCustomization();
}

void UHitmenLobbyWidget::CycleAbility(EHitmenAbilitySlot AbilitySlot, int32 Direction)
{
	const UHitmenCatalog* Catalog = GetCatalog();
	if (!Catalog)
	{
		return;
	}
	TObjectPtr<UHitmenAbilityData>& Current = AbilitySlot == EHitmenAbilitySlot::Move ? LocalLoadout.MoveAbility : LocalLoadout.ManipulationAbility;
	Current = CycleIn(Catalog->GetAbilities(AbilitySlot), Current.Get(), Direction, true);
	if (AHitmenPlayerController* PC = GetHitmenPC()) { PC->Server_SetLoadout(LocalLoadout); }
	RefreshCustomization();
}

void UHitmenLobbyWidget::ToggleReady()
{
	const AHitmenPlayerState* PS = GetLocalPlayerState();
	if (AHitmenPlayerController* PC = GetHitmenPC(); PC && PS)
	{
		PC->Server_SetReady(!PS->IsReady());
	}
}

void UHitmenLobbyWidget::Launch()
{
	if (AHitmenPlayerController* PC = GetHitmenPC())
	{
		PC->Server_LaunchMatch();
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
