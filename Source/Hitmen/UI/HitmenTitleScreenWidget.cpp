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
#include "Components/TextBlock.h"
#include "Components/ScrollBox.h"
#include "Components/SizeBox.h"
#include "Components/Spacer.h"
#include "Components/Border.h"
#include "Kismet/KismetSystemLibrary.h"
#include "Engine/World.h"

#define LOCTEXT_NAMESPACE "HitmenTitle"

namespace
{
	enum EPage : int32 { PageTitle = 0, PageJoin = 1, PageLobby = 2 };
}

UHitmenMenuButton* UHitmenTitleScreenWidget::AddMenuButton(UVerticalBox* Box, const FText& Label, int32 Kind, FName Handler)
{
	UHitmenMenuButton* Button = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
	Button->Setup(Label, static_cast<EHitmenMenuButtonKind>(Kind));
	FScriptDelegate Delegate;
	Delegate.BindUFunction(this, Handler);
	Button->OnClicked.Add(Delegate);
	if (UVerticalBoxSlot* ButtonSlot = Box->AddChildToVerticalBox(Button))
	{
		ButtonSlot->SetPadding(FMargin(0.f, 2.f));
		ButtonSlot->SetHorizontalAlignment(HAlign_Left);
	}
	return Button;
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

	// ---------------- Title page: logo and menu, left side, vertically centred ----------------
	UCanvasPanel* TitlePage = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	Pages->AddChild(TitlePage);
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToCanvas(TitlePage, Column, FAnchors(0.f, 0.5f), FVector2D(118.f, 0.f), FVector2D(0.f, 0.5f));
	UTextBlock* Overline = HitmenUI::MakeText(WidgetTree, LOCTEXT("Overline", "A MULTIPLAYER ASSASSINATION SANDBOX"), 18, TEXT("Medium"), HitmenUI::TextDim());
	Overline->SetFont(HitmenUI::SpacedFont(18, TEXT("Medium"), 260));
	HitmenUI::AddToVBox(Column, Overline, FMargin(4.f, 0.f, 0.f, 0.f));
	UTextBlock* Logo = HitmenUI::MakeText(WidgetTree, LOCTEXT("Logo", "HITMEN"), 168, TEXT("Bold"));
	Logo->SetFont(HitmenUI::SpacedFont(168, TEXT("Bold"), 30));
	HitmenUI::AddToVBox(Column, Logo, FMargin(0.f, -18.f, 0.f, 0.f));
	UBorder* Underline = HitmenUI::MakePanel(WidgetTree, HitmenUI::Accent(), FMargin(0.f));
	USizeBox* UnderlineSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	UnderlineSize->SetWidthOverride(150.f);
	UnderlineSize->SetHeightOverride(6.f);
	UnderlineSize->SetContent(Underline);
	HitmenUI::AddToVBox(Column, UnderlineSize, FMargin(8.f, -6.f, 0.f, 46.f), false, HAlign_Left);
	AddMenuButton(Column, LOCTEXT("CreateRoom", "Create room"), static_cast<int32>(EHitmenMenuButtonKind::Primary), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, CreateRoom));
	AddMenuButton(Column, LOCTEXT("Join", "Join room"), static_cast<int32>(EHitmenMenuButtonKind::Primary), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, OpenJoin));
	AddMenuButton(Column, LOCTEXT("Options", "Options"), static_cast<int32>(EHitmenMenuButtonKind::Primary), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, OpenOptions));
	AddMenuButton(Column, LOCTEXT("Quit", "Quit"), static_cast<int32>(EHitmenMenuButtonKind::Primary), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, QuitGame));
	TitleStatusText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 18, TEXT("Italic"), HitmenUI::Accent());
	HitmenUI::AddToVBox(Column, TitleStatusText, FMargin(22.f, 18.f, 0.f, 0.f));

	if (UHitmenGameSettings::Get()->bShowDebugButton)
	{
		UVerticalBox* DebugBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
		AddMenuButton(DebugBox, LOCTEXT("DebugGym", "Debug gym"), static_cast<int32>(EHitmenMenuButtonKind::Back), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, OpenDebugGym));
		HitmenUI::AddToCanvas(TitlePage, DebugBox, FAnchors(1.f, 1.f), FVector2D(-40.f, -32.f), FVector2D(1.f, 1.f));
	}
	UTextBlock* Version = HitmenUI::MakeText(WidgetTree, LOCTEXT("Version", "HITMEN  0.3.0  -  UNREAL ENGINE 5.7"), 14, TEXT("Medium"), HitmenUI::TextDim());
	Version->SetFont(HitmenUI::SpacedFont(14, TEXT("Medium"), 200));
	HitmenUI::AddToCanvas(TitlePage, Version, FAnchors(0.f, 1.f), FVector2D(124.f, -40.f), FVector2D(0.f, 1.f));

	// ---------------- Join page ----------------
	UCanvasPanel* JoinPage = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	Pages->AddChild(JoinPage);
	UVerticalBox* JoinColumn = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UCanvasPanelSlot* JoinSlot = HitmenUI::AddToCanvas(JoinPage, JoinColumn, FAnchors(0.f, 0.f, 0.f, 1.f), FVector2D(118.f, 0.f), FVector2D(0.f, 0.f));
	JoinSlot->SetOffsets(FMargin(118.f, 110.f, 760.f, 140.f));
	UTextBlock* JoinTitle = HitmenUI::MakeText(WidgetTree, LOCTEXT("JoinTitle", "JOIN A ROOM"), 72, TEXT("Bold"));
	HitmenUI::AddToVBox(JoinColumn, JoinTitle);
	JoinStatusText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 20, TEXT("Italic"), HitmenUI::TextDim());
	HitmenUI::AddToVBox(JoinColumn, JoinStatusText, FMargin(4.f, 0.f, 0.f, 26.f));
	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	if (UVerticalBoxSlot* ScrollSlot = JoinColumn->AddChildToVerticalBox(Scroll))
	{
		ScrollSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	}
	UTextBlock* RoomsHeader = HitmenUI::MakeText(WidgetTree, LOCTEXT("Rooms", "ROOMS"), 16, TEXT("Medium"), HitmenUI::TextDim());
	RoomsHeader->SetFont(HitmenUI::SpacedFont(16, TEXT("Medium"), 250));
	Scroll->AddChild(RoomsHeader);
	RoomList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(RoomList);
	UTextBlock* FriendsHeader = HitmenUI::MakeText(WidgetTree, LOCTEXT("Friends", "STEAM FRIENDS"), 16, TEXT("Medium"), HitmenUI::TextDim());
	FriendsHeader->SetFont(HitmenUI::SpacedFont(16, TEXT("Medium"), 250));
	Scroll->AddChild(FriendsHeader);
	FriendRoomList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(FriendRoomList);
	UHorizontalBox* JoinActions = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UVerticalBox* BackBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	AddMenuButton(BackBox, LOCTEXT("Back", "Back"), static_cast<int32>(EHitmenMenuButtonKind::Back), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, CloseJoin));
	UVerticalBox* RefreshBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	AddMenuButton(RefreshBox, LOCTEXT("Refresh", "Refresh"), static_cast<int32>(EHitmenMenuButtonKind::Secondary), GET_FUNCTION_NAME_CHECKED(UHitmenTitleScreenWidget, RefreshRooms));
	HitmenUI::AddToHBox(JoinActions, BackBox, FMargin(0.f, 0.f, 30.f, 0.f));
	HitmenUI::AddToHBox(JoinActions, RefreshBox);
	HitmenUI::AddToCanvas(JoinPage, JoinActions, FAnchors(0.f, 1.f), FVector2D(118.f, -40.f), FVector2D(0.f, 1.f));

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
		Sessions->OnCreateSessionComplete.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleCreateComplete);
		Sessions->OnJoinSessionComplete.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleJoinComplete);
	}
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnMatchPhaseChanged.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandlePhaseChanged);
	}
	RefreshPageFromPhase();
}

void UHitmenTitleScreenWidget::UnbindEvents()
{
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		Sessions->OnFindSessionsComplete.RemoveAll(this);
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

void UHitmenTitleScreenWidget::ShowPage(int32 Page)
{
	if (Pages && Pages->GetActiveWidgetIndex() != Page)
	{
		Pages->SetActiveWidgetIndex(Page);
		Juice_OnPageChanged(Page);
	}
	if (Backdrop)
	{
		// The room has columns on both sides; the agent screen keeps the model (left) lit; the title darkens the left.
		const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
		const bool bAgent = Page == PageLobby && GS && GS->GetMatchPhase() == EHitmenMatchPhase::Customization;
		Backdrop->LeftCoverage = bAgent ? 0.18f : (Page == PageLobby ? 0.5f : 0.62f);
		Backdrop->RightCoverage = bAgent ? 0.5f : (Page == PageLobby ? 0.6f : 0.f);
	}
}

void UHitmenTitleScreenWidget::RefreshPageFromPhase()
{
	const AHitmenGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHitmenGameState>() : nullptr;
	const EHitmenMatchPhase Phase = GS ? GS->GetMatchPhase() : EHitmenMatchPhase::Title;
	if (Phase == EHitmenMatchPhase::Title)
	{
		if (Pages && Pages->GetActiveWidgetIndex() == PageLobby)
		{
			ShowPage(PageTitle);
		}
		else if (Pages && Pages->GetActiveWidgetIndex() != PageJoin)
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
	RefreshRooms();
}

void UHitmenTitleScreenWidget::CloseJoin()
{
	ShowPage(PageTitle);
	Juice_OnJoinPanelToggled(false);
}

void UHitmenTitleScreenWidget::RefreshRooms()
{
	if (JoinStatusText)
	{
		JoinStatusText->SetText(LOCTEXT("Searching", "Searching for rooms..."));
	}
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		Sessions->FindRooms(bUseLAN);
	}
}

void UHitmenTitleScreenWidget::HandleSessionsFound(const TArray<FHitmenSessionInfo>& Sessions, bool bSuccess)
{
	if (RoomList) { RoomList->ClearChildren(); }
	if (FriendRoomList) { FriendRoomList->ClearChildren(); }
	int32 FriendCount = 0;
	for (const FHitmenSessionInfo& Info : Sessions)
	{
		UHitmenMenuButton* Entry = WidgetTree->ConstructWidget<UHitmenMenuButton>(UHitmenMenuButton::StaticClass());
		Entry->Setup(FText::FromString(Info.RoomName), EHitmenMenuButtonKind::Secondary,
			FText::Format(LOCTEXT("RoomInfo", "{0} / {1} players  -  {2} ms"), FText::AsNumber(Info.CurrentPlayers), FText::AsNumber(Info.MaxPlayers), FText::AsNumber(Info.PingMs)));
		Entry->Index = Info.ResultIndex;
		Entry->OnIndexClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleJoinClicked);
		if (UVerticalBox* Target = Info.bIsFriend ? FriendRoomList.Get() : RoomList.Get())
		{
			HitmenUI::AddToVBox(Target, Entry, FMargin(0.f, 2.f), false, HAlign_Fill);
		}
		FriendCount += Info.bIsFriend ? 1 : 0;
	}
	if (JoinStatusText)
	{
		JoinStatusText->SetText(Sessions.IsEmpty()
			? (bSuccess ? LOCTEXT("NoRooms", "No room found.") : LOCTEXT("SearchFailed", "Search failed."))
			: FText::Format(LOCTEXT("Found", "{0} room(s), {1} from friends."), FText::AsNumber(Sessions.Num()), FText::AsNumber(FriendCount)));
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
		Sessions->JoinRoom(ResultIndex);
	}
}

void UHitmenTitleScreenWidget::HandleJoinComplete(bool bSuccess)
{
	if (!bSuccess && JoinStatusText)
	{
		JoinStatusText->SetText(LOCTEXT("JoinFailed", "Could not join the room."));
	}
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
