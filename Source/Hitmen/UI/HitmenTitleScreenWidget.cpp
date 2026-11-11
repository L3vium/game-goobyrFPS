// Hitmen — front-end root

#include "UI/HitmenTitleScreenWidget.h"
#include "UI/HitmenUIFactory.h"
#include "UI/HitmenMenuKit.h"
#include "UI/HitmenLobbyWidgets.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Framework/HitmenGameInstance.h"
#include "Framework/HitmenGameState.h"
#include "Core/HitmenGameSettings.h"
#include "Core/HitmenUserSettings.h"
#include "Blueprint/WidgetTree.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/WidgetSwitcher.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/TextBlock.h"
#include "Components/ScrollBox.h"
#include "Components/SizeBox.h"
#include "Components/Border.h"
#include "Components/EditableTextBox.h"
#include "Kismet/KismetSystemLibrary.h"
#include "Engine/World.h"

#define LOCTEXT_NAMESPACE "HitmenTitle"

namespace
{
	enum EPage : int32 { PageTitle = 0, PageJoin = 1, PageLobby = 2 };

	constexpr float MenuWidth = 440.f;

	UTextBlock* MakeHeader(UWidgetTree* Tree, const FText& Text)
	{
		UTextBlock* Header = HitmenUI::MakeText(Tree, Text, 16, TEXT("Medium"), HitmenUI::TextDim());
		Header->SetFont(HitmenUI::SpacedFont(16, TEXT("Medium"), 250));
		return Header;
	}

	/** A dark block that holds a list (friends, rooms). */
	UBorder* MakeBlock(UWidgetTree* Tree, UWidget* Content)
	{
		UBorder* Block = HitmenUI::MakePanel(Tree, FLinearColor(0.02f, 0.022f, 0.028f, 0.82f), FMargin(10.f));
		Block->SetContent(Content);
		return Block;
	}
}

UHitmenMenuButton* UHitmenTitleScreenWidget::AddMenuButton(UVerticalBox* Box, const FText& Label, int32 Kind, FName Handler)
{
	UHitmenMenuButton* Button = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	Button->Setup(Label, static_cast<EHitmenMenuButtonKind>(Kind));
	FScriptDelegate Delegate;
	Delegate.BindUFunction(this, Handler);
	Button->OnClicked.Add(Delegate);
	const bool bBlock = Kind == static_cast<int32>(EHitmenMenuButtonKind::Block) || Kind == static_cast<int32>(EHitmenMenuButtonKind::Dashed);
	if (UVerticalBoxSlot* ButtonSlot = Box->AddChildToVerticalBox(Button))
	{
		ButtonSlot->SetPadding(bBlock ? FMargin(0.f, 5.f) : FMargin(0.f, 2.f));
		ButtonSlot->SetHorizontalAlignment(bBlock ? HAlign_Fill : HAlign_Left);
	}
	return Button;
}

UTextBlock* UHitmenTitleScreenWidget::MakeListMessage(const FText& Text) const
{
	UTextBlock* Message = HitmenUI::MakeText(WidgetTree, Text, 18, TEXT("Italic"), HitmenUI::TextDim());
	return Message;
}

void UHitmenTitleScreenWidget::BuildDefaultLayout()
{
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;
	const FAnchors Full(0.f, 0.f, 1.f, 1.f);

	Backdrop = WidgetTree->ConstructWidget<UHitmenBackdrop>(UHitmenBackdrop::StaticClass());
	Backdrop->SetVisibility(ESlateVisibility::HitTestInvisible);
	if (UCanvasPanelSlot* BackSlot = Canvas->AddChildToCanvas(Backdrop)) { BackSlot->SetAnchors(Full); BackSlot->SetOffsets(FMargin(0.f)); }

	Pages = WidgetTree->ConstructWidget<UWidgetSwitcher>(UWidgetSwitcher::StaticClass());
	if (UCanvasPanelSlot* PagesSlot = Canvas->AddChildToCanvas(Pages)) { PagesSlot->SetAnchors(Full); PagesSlot->SetOffsets(FMargin(0.f)); }

	// ---------------- Title page: the logo and square menu blocks, left side, centred ----------------
	UCanvasPanel* TitlePage = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	Pages->AddChild(TitlePage);
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToCanvas(TitlePage, Column, FAnchors(0.f, 0.5f), FVector2D(118.f, 0.f), FVector2D(0.f, 0.5f));
	// Wordmark: one heavy, tightly set word, white on the dark scene. No tagline, no underline.
	UTextBlock* Logo = HitmenUI::MakeText(WidgetTree, LOCTEXT("Logo", "HITMEN"), 190, TEXT("Bold"));
	Logo->SetFont(HitmenUI::SpacedFont(190, TEXT("Bold"), -10));
	Logo->SetShadowOffset(FVector2D(0.f, 4.f));
	Logo->SetShadowColorAndOpacity(FLinearColor(0.f, 0.f, 0.f, 0.6f));
	HitmenUI::AddToVBox(Column, Logo, FMargin(-6.f, 0.f, 0.f, 26.f));
	USizeBox* MenuSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	MenuSize->SetWidthOverride(MenuWidth);
	UVerticalBox* Menu = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	MenuSize->SetContent(Menu);
	HitmenUI::AddToVBox(Column, MenuSize, FMargin(0.f), false, HAlign_Left);
	AddMenuButton(Menu, LOCTEXT("Join", "Join room"), static_cast<int32>(EHitmenMenuButtonKind::Dashed), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, OpenJoin));
	AddMenuButton(Menu, LOCTEXT("CreateRoom", "Create room"), static_cast<int32>(EHitmenMenuButtonKind::Block), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, CreateRoom));
	AddMenuButton(Menu, LOCTEXT("Options", "Options"), static_cast<int32>(EHitmenMenuButtonKind::Block), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, OpenOptions));
	AddMenuButton(Menu, LOCTEXT("Quit", "Quit"), static_cast<int32>(EHitmenMenuButtonKind::Block), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, QuitGame));
	TitleStatusText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 18, TEXT("Italic"), HitmenUI::Accent());
	HitmenUI::AddToVBox(Column, TitleStatusText, FMargin(4.f, 14.f, 0.f, 0.f));

	if (UHitmenGameSettings::Get()->bShowDebugButton)
	{
		UVerticalBox* DebugBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
		AddMenuButton(DebugBox, LOCTEXT("DebugGym", "Debug gym"), static_cast<int32>(EHitmenMenuButtonKind::Back), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, OpenDebugGym));
		HitmenUI::AddToCanvas(TitlePage, DebugBox, FAnchors(1.f, 1.f), FVector2D(-40.f, -32.f), FVector2D(1.f, 1.f));
	}

	// ---------------- Join page: friends and rooms on the left, the code on the right ----------------
	UCanvasPanel* JoinPage = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	Pages->AddChild(JoinPage);
	UVerticalBox* JoinColumn = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UCanvasPanelSlot* JoinSlot = HitmenUI::AddToCanvas(JoinPage, JoinColumn, FAnchors(0.f, 0.f, 0.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector);
	JoinSlot->SetOffsets(FMargin(118.f, 90.f, 820.f, 130.f));
	HitmenUI::AddToVBox(JoinColumn, HitmenUI::MakeText(WidgetTree, LOCTEXT("JoinTitle", "JOIN A ROOM"), 72, TEXT("Bold")), FMargin(0.f, 0.f, 0.f, 6.f));
	JoinStatusText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 18, TEXT("Italic"), HitmenUI::TextDim());
	HitmenUI::AddToVBox(JoinColumn, JoinStatusText, FMargin(2.f, 0.f, 0.f, 18.f));
	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	if (UVerticalBoxSlot* ScrollSlot = JoinColumn->AddChildToVerticalBox(Scroll))
	{
		ScrollSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	}
	UVerticalBox* Lists = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(Lists);
	// Steam friends first.
	HitmenUI::AddToVBox(Lists, MakeHeader(WidgetTree, LOCTEXT("Friends", "STEAM FRIENDS")), FMargin(0.f, 0.f, 0.f, 8.f));
	FriendList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Lists, MakeBlock(WidgetTree, FriendList), FMargin(0.f, 0.f, 0.f, 26.f));
	// Rooms: the header with a refresh button beside it, then the list (messages go inside it).
	UHorizontalBox* RoomsHeader = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	HitmenUI::AddToHBox(RoomsHeader, MakeHeader(WidgetTree, LOCTEXT("Rooms", "ROOMS")), FMargin(0.f, 0.f, 14.f, 0.f));
	RefreshButton = WidgetTree->ConstructWidget<UHitmenIconButton>(UHitmenIconButton::StaticClass());
	RefreshButton->Setup(EHitmenIconGlyph::Refresh, 34.f, true);
	RefreshButton->OnClicked.AddDynamic(this, &UHitmenTitleScreenWidget::RefreshRooms);
	HitmenUI::AddToHBox(RoomsHeader, RefreshButton, FMargin(0.f));
	HitmenUI::AddToVBox(Lists, RoomsHeader, FMargin(0.f, 0.f, 0.f, 8.f));
	RoomList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(Lists, MakeBlock(WidgetTree, RoomList));
	FriendRoomList = nullptr;

	// Room code, right column.
	UVerticalBox* CodeColumn = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToCanvas(JoinPage, CodeColumn, FAnchors(1.f, 0.f), FVector2D(-110.f, 250.f), FVector2D(1.f, 0.f));
	HitmenUI::AddToVBox(CodeColumn, MakeHeader(WidgetTree, LOCTEXT("CodeHeader", "ROOM CODE")), FMargin(0.f, 0.f, 0.f, 8.f));
	CodeEdit = WidgetTree->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass());
	{
		FEditableTextBoxStyle Style = CodeEdit->WidgetStyle;
		FSlateBrush Field;
		Field.TintColor = FSlateColor(FLinearColor(0.f, 0.f, 0.f, 0.6f));
		Style.SetBackgroundImageNormal(Field);
		Style.SetBackgroundImageHovered(Field);
		Field.TintColor = FSlateColor(FLinearColor(0.f, 0.f, 0.f, 0.8f));
		Style.SetBackgroundImageFocused(Field);
		Style.SetPadding(FMargin(18.f, 12.f));
		Style.TextStyle.SetFont(HitmenUI::SpacedFont(38, TEXT("Bold"), 300));
		Style.TextStyle.SetColorAndOpacity(FSlateColor(HitmenUI::Text()));
		Style.SetForegroundColor(FSlateColor(HitmenUI::Text()));
		CodeEdit->WidgetStyle = Style;
	}
	CodeEdit->SetHintText(LOCTEXT("CodeHint", "ABC123"));
	CodeEdit->OnTextCommitted.AddDynamic(this, &UHitmenTitleScreenWidget::HandleCodeCommitted);
	USizeBox* CodeSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	CodeSize->SetWidthOverride(380.f);
	CodeSize->SetContent(CodeEdit);
	HitmenUI::AddToVBox(CodeColumn, CodeSize, FMargin(0.f, 0.f, 0.f, 10.f), false, HAlign_Left);
	USizeBox* JoinCodeSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	JoinCodeSize->SetWidthOverride(380.f);
	UVerticalBox* JoinCodeBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	JoinCodeSize->SetContent(JoinCodeBox);
	AddMenuButton(JoinCodeBox, LOCTEXT("JoinCode", "Join"), static_cast<int32>(EHitmenMenuButtonKind::Block), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, JoinWithCode));
	HitmenUI::AddToVBox(CodeColumn, JoinCodeSize, FMargin(0.f), false, HAlign_Left);
	CodeStatusText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 17, TEXT("Italic"), HitmenUI::TextDim());
	HitmenUI::AddToVBox(CodeColumn, CodeStatusText, FMargin(2.f, 8.f, 0.f, 0.f));

	UVerticalBox* BackBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	AddMenuButton(BackBox, LOCTEXT("Back", "Back"), static_cast<int32>(EHitmenMenuButtonKind::Back), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, CloseJoin));
	HitmenUI::AddToCanvas(JoinPage, BackBox, FAnchors(0.f, 1.f), FVector2D(96.f, -40.f), FVector2D(0.f, 1.f));

	// ---------------- Lobby (create room + customization) ----------------
	Lobby = WidgetTree->ConstructWidget<UHitmenLobbyWidget>(UHitmenLobbyWidget::StaticClass());
	Pages->AddChild(Lobby);
	Pages->SetActiveWidgetIndex(PageTitle);
}

void UHitmenTitleScreenWidget::BindEvents()
{
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		Sessions->OnFindSessionsComplete.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleSessionsFound);
		Sessions->OnFriendsUpdated.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleFriendsUpdated);
		Sessions->OnCreateSessionComplete.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleCreateComplete);
		Sessions->OnJoinSessionComplete.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleJoinComplete);
	}
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnMatchPhaseChanged.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandlePhaseChanged);
	}
	SetIsFocusable(true);
	RefreshPageFromPhase();
}

void UHitmenTitleScreenWidget::UnbindEvents()
{
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		Sessions->OnFindSessionsComplete.RemoveAll(this);
		Sessions->OnFriendsUpdated.RemoveAll(this);
		Sessions->OnCreateSessionComplete.RemoveAll(this);
		Sessions->OnJoinSessionComplete.RemoveAll(this);
	}
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnMatchPhaseChanged.RemoveAll(this);
	}
}

UHitmenSessionSubsystem* UHitmenTitleScreenWidget::GetSessions() const
{
	return GetGameInstance() ? GetGameInstance()->GetSubsystem<UHitmenSessionSubsystem>() : nullptr;
}

bool UHitmenTitleScreenWidget::IsTyping() const
{
	return (CodeEdit && CodeEdit->HasKeyboardFocus()) || (Lobby && Lobby->IsEditingText());
}

void UHitmenTitleScreenWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	// Keep keyboard focus on the menu (Space / Escape = Back) unless a text box is being typed in
	// or an overlay (options) is open on top.
	if (IsVisible() && !IsTyping() && !HasAnyUserFocus() && !HasFocusedDescendants())
	{
		const UHitmenUIEventSubsystem* UI = GetUIEvents();
		if (!UI || !UI->IsOptionsOpen())
		{
			SetKeyboardFocus();
		}
	}
}

FReply UHitmenTitleScreenWidget::NativeOnKeyDown(const FGeometry& InGeometry, const FKeyEvent& InKeyEvent)
{
	const FKey Key = InKeyEvent.GetKey();
	if ((Key == EKeys::SpaceBar || Key == EKeys::Escape) && !IsTyping() && !InKeyEvent.IsRepeat())
	{
		GoBack();
		return FReply::Handled();
	}
	return Super::NativeOnKeyDown(InGeometry, InKeyEvent);
}

void UHitmenTitleScreenWidget::GoBack()
{
	const int32 Page = Pages ? Pages->GetActiveWidgetIndex() : PageTitle;
	if (Page == PageJoin)
	{
		CloseJoin();
	}
	else if (Page == PageLobby && Lobby)
	{
		Lobby->Back();
	}
}

void UHitmenTitleScreenWidget::ShowPage(int32 Page)
{
	if (Pages && Pages->GetActiveWidgetIndex() != Page)
	{
		Pages->SetActiveWidgetIndex(Page);
		Juice_OnPageChanged(Page);
	}
	if (Backdrop)
	{
		// The room has columns on both sides; the agent screen keeps the model lit; the title darkens the left.
		const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
		const bool bAgent = Page == PageLobby && GS && GS->GetMatchPhase() == EHitmenMatchPhase::Customization;
		Backdrop->LeftCoverage = bAgent ? 0.f : (Page == PageLobby ? 0.55f : 0.62f);
		Backdrop->RightCoverage = bAgent ? 0.55f : (Page == PageLobby ? 0.6f : (Page == PageJoin ? 0.45f : 0.f));
	}
}

void UHitmenTitleScreenWidget::RefreshPageFromPhase()
{
	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	const EHitmenMatchPhase Phase = GS ? GS->GetMatchPhase() : EHitmenMatchPhase::Title;
	if (Phase == EHitmenMatchPhase::Title)
	{
		if (Pages && Pages->GetActiveWidgetIndex() != PageJoin)
		{
			ShowPage(PageTitle);
		}
		return;
	}
	ShowPage(PageLobby);
}

void UHitmenTitleScreenWidget::HandlePhaseChanged(EHitmenMatchPhase Phase)
{
	RefreshPageFromPhase();
}

void UHitmenTitleScreenWidget::CreateRoom()
{
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		if (TitleStatusText)
		{
			TitleStatusText->SetText(LOCTEXT("Creating", "Opening the room..."));
		}
		const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
		FString Name = Settings && !Settings->PreferredName.IsEmpty() ? Settings->PreferredName : Sessions->GetLocalOnlineName();
		Name = Name.IsEmpty() ? TEXT("Hitmen room") : FString::Printf(TEXT("%s's room"), *Name);
		Sessions->CreateRoom(Name, UHitmenGameSettings::Get()->MaxPlayers, bUseLAN);
	}
}

void UHitmenTitleScreenWidget::HandleCreateComplete(bool bSuccess)
{
	if (TitleStatusText)
	{
		TitleStatusText->SetText(bSuccess ? FText::GetEmpty() : LOCTEXT("CreateFailed", "Could not create the room."));
	}
}

void UHitmenTitleScreenWidget::OpenJoin()
{
	ShowPage(PageJoin);
	Juice_OnJoinPanelToggled(true);
	if (CodeStatusText)
	{
		CodeStatusText->SetText(FText::GetEmpty());
	}
	if (const UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		HandleFriendsUpdated(Sessions->GetFriends());
	}
	RefreshRooms();
}

void UHitmenTitleScreenWidget::CloseJoin()
{
	ShowPage(PageTitle);
	Juice_OnJoinPanelToggled(false);
}

void UHitmenTitleScreenWidget::RefreshRooms()
{
	UHitmenSessionSubsystem* Sessions = GetSessions();
	if (!Sessions)
	{
		return;
	}
	if (JoinStatusText)
	{
		JoinStatusText->SetText(LOCTEXT("Searching", "Searching..."));
	}
	if (RoomList && RoomList->GetChildrenCount() == 0)
	{
		HitmenUI::AddToVBox(RoomList, MakeListMessage(LOCTEXT("SearchingRooms", "Searching for rooms...")), FMargin(8.f, 6.f));
	}
	// Spamming refresh while a search runs is ignored by the subsystem (one search at a time).
	Sessions->FindRooms(bUseLAN);
}

void UHitmenTitleScreenWidget::HandleSessionsFound(const TArray<FHitmenSessionInfo>& Sessions, bool bSuccess)
{
	if (!RoomList)
	{
		return;
	}
	RoomList->ClearChildren();
	for (const FHitmenSessionInfo& Info : Sessions)
	{
		UHitmenMenuButton* Entry = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
		const FText Subtitle = FText::Format(LOCTEXT("RoomInfo", "{0} / {1} players  -  {2} ms{3}"), FText::AsNumber(Info.CurrentPlayers), FText::AsNumber(Info.MaxPlayers),
			FText::AsNumber(Info.PingMs), Info.bIsFriend ? LOCTEXT("FriendTag", "  -  friend") : FText::GetEmpty());
		Entry->Setup(FText::FromString(Info.RoomName), EHitmenMenuButtonKind::Block, Subtitle);
		Entry->Index = Info.ResultIndex;
		Entry->OnIndexClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleJoinClicked);
		HitmenUI::AddToVBox(RoomList, Entry, FMargin(0.f, 3.f), false, HAlign_Fill);
	}
	const UHitmenSessionSubsystem* Subsystem = GetSessions();
	const bool bStillSearching = Subsystem && Subsystem->IsSearching();
	if (Sessions.IsEmpty())
	{
		HitmenUI::AddToVBox(RoomList, MakeListMessage(bStillSearching ? LOCTEXT("SearchingRooms", "Searching for rooms...")
			: (bSuccess ? LOCTEXT("NoRooms", "No room found.") : LOCTEXT("SearchFailed", "Search failed."))), FMargin(8.f, 6.f));
	}
	if (JoinStatusText)
	{
		JoinStatusText->SetText(bStillSearching ? LOCTEXT("Searching", "Searching...") : FText::GetEmpty());
	}
}

void UHitmenTitleScreenWidget::HandleFriendsUpdated(const TArray<FHitmenFriendInfo>& Friends)
{
	if (!FriendList)
	{
		return;
	}
	FriendList->ClearChildren();
	const UHitmenSessionSubsystem* Sessions = GetSessions();
	if (Friends.IsEmpty())
	{
		HitmenUI::AddToVBox(FriendList, MakeListMessage(Sessions && !Sessions->IsUsingSteam()
			? LOCTEXT("NoSteam", "Steam is not running.")
			: LOCTEXT("NoFriends", "No friends to show.")), FMargin(8.f, 6.f));
		return;
	}
	for (const FHitmenFriendInfo& Friend : Friends)
	{
		UHitmenMenuButton* Entry = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
		const bool bHasRoom = Friend.RoomResultIndex != INDEX_NONE;
		const FText State = bHasRoom ? LOCTEXT("FriendInRoom", "In a room  -  click to join")
			: (Friend.bPlayingHitmen ? LOCTEXT("FriendPlaying", "Playing Hitmen") : (Friend.bOnline ? LOCTEXT("FriendOnline", "Online") : LOCTEXT("FriendOffline", "Offline")));
		Entry->Setup(FText::FromString(Friend.DisplayName), EHitmenMenuButtonKind::Block, State);
		Entry->Index = Friend.RoomResultIndex;
		Entry->SetButtonEnabled(bHasRoom);
		if (bHasRoom)
		{
			Entry->OnIndexClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleJoinClicked);
		}
		HitmenUI::AddToVBox(FriendList, Entry, FMargin(0.f, 3.f), false, HAlign_Fill);
	}
}

void UHitmenTitleScreenWidget::HandleJoinClicked(int32 ResultIndex)
{
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		if (JoinStatusText)
		{
			JoinStatusText->SetText(LOCTEXT("Joining", "Joining..."));
		}
		bJoiningByCode = false;
		Sessions->JoinRoom(ResultIndex);
	}
}

void UHitmenTitleScreenWidget::HandleCodeCommitted(const FText& Text, ETextCommit::Type CommitType)
{
	if (CommitType == ETextCommit::OnEnter)
	{
		JoinWithCode();
	}
}

void UHitmenTitleScreenWidget::JoinWithCode()
{
	UHitmenSessionSubsystem* Sessions = GetSessions();
	if (!Sessions || !CodeEdit)
	{
		return;
	}
	const FString Code = UHitmenSessionSubsystem::NormalizeCode(CodeEdit->GetText().ToString());
	CodeEdit->SetText(FText::FromString(Code));
	if (Code.Len() != 6)
	{
		if (CodeStatusText)
		{
			CodeStatusText->SetText(LOCTEXT("CodeLength", "A room code has 6 characters."));
		}
		return;
	}
	if (CodeStatusText)
	{
		CodeStatusText->SetText(LOCTEXT("CodeSearching", "Looking for the room..."));
	}
	bJoiningByCode = true;
	Sessions->JoinByCode(Code, bUseLAN);
}

void UHitmenTitleScreenWidget::HandleJoinComplete(bool bSuccess)
{
	if (bSuccess)
	{
		return;
	}
	if (bJoiningByCode && CodeStatusText)
	{
		CodeStatusText->SetText(LOCTEXT("CodeNotFound", "No room with this code."));
	}
	else if (JoinStatusText)
	{
		JoinStatusText->SetText(LOCTEXT("JoinFailed", "Could not join the room."));
	}
	bJoiningByCode = false;
}

void UHitmenTitleScreenWidget::OpenOptions()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->SetOptionsOpen(true);
	}
}

void UHitmenTitleScreenWidget::QuitGame()
{
	UKismetSystemLibrary::QuitGame(this, GetOwningPlayer(), EQuitPreference::Quit, false);
}

void UHitmenTitleScreenWidget::OpenDebugGym()
{
	if (UHitmenGameInstance* GI = GetGameInstance<UHitmenGameInstance>())
	{
		GI->OpenGym();
	}
}

#undef LOCTEXT_NAMESPACE
