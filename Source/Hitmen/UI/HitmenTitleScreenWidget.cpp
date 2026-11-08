// Hitmen — title screen

#include "UI/HitmenTitleScreenWidget.h"
#include "UI/HitmenUIFactory.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "Framework/HitmenGameInstance.h"
#include "Core/HitmenGameSettings.h"
#include "Core/HitmenUserSettings.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Button.h"
#include "Components/TextBlock.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/VerticalBox.h"
#include "Components/HorizontalBox.h"
#include "Components/SizeBox.h"
#include "Components/Border.h"
#include "Components/ScrollBox.h"
#include "Kismet/KismetSystemLibrary.h"

#define LOCTEXT_NAMESPACE "HitmenTitle"

void UHitmenTitleScreenWidget::BuildDefaultLayout()
{
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;

	// Title, top-left, huge and bold.
	UTextBlock* Title = HitmenUI::MakeText(WidgetTree, LOCTEXT("Title", "HITMEN"), 150, TEXT("Black"));
	HitmenUI::AddToCanvas(Canvas, Title, FAnchors(0.f, 0.f), FVector2D(80.f, 50.f), FVector2D(0.f, 0.f));

	// Square buttons, left-aligned, labels left-aligned.
	UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	auto AddMenuButton = [this, Column](const FText& Label, EHitmenButtonStyle Style) -> UButton*
	{
		USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		Size->SetWidthOverride(380.f);
		Size->SetHeightOverride(64.f);
		UButton* Button = HitmenUI::MakeButton(WidgetTree, Label, 26, Style);
		Size->SetContent(Button);
		HitmenUI::AddToVBox(Column, Size, FMargin(0.f, 6.f), false, HAlign_Left);
		return Button;
	};
	JoinButton = AddMenuButton(LOCTEXT("Join", "JOIN"), EHitmenButtonStyle::Dashed);
	CreateRoomButton = AddMenuButton(LOCTEXT("CreateRoom", "CREATE ROOM"), EHitmenButtonStyle::Solid);
	OptionsButton = AddMenuButton(LOCTEXT("Options", "OPTIONS"), EHitmenButtonStyle::Solid);
	QuitButton = AddMenuButton(LOCTEXT("Quit", "QUIT"), EHitmenButtonStyle::Solid);
	HitmenUI::AddToCanvas(Canvas, Column, FAnchors(0.f, 0.5f), FVector2D(80.f, -40.f), FVector2D(0.f, 0.5f));

	// Small debug button, bottom-right.
	if (UHitmenGameSettings::Get()->bShowDebugButton)
	{
		DebugButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Debug", "DEBUG GYM"), 12, EHitmenButtonStyle::Ghost);
		HitmenUI::AddToCanvas(Canvas, DebugButton, FAnchors(1.f, 1.f), FVector2D(-24.f, -24.f), FVector2D(1.f, 1.f));
	}

	// Join overlay: darkened title screen, rooms then Steam friends' rooms.
	UBorder* Dim = HitmenUI::MakePanel(WidgetTree, HitmenUI::Background(), FMargin(80.f, 80.f));
	UVerticalBox* JoinColumn = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(JoinColumn, HitmenUI::MakeText(WidgetTree, LOCTEXT("JoinTitle", "JOIN A ROOM"), 54, TEXT("Black")));
	JoinStatusText = HitmenUI::MakeText(WidgetTree, FText::GetEmpty(), 16, TEXT("Regular"), HitmenUI::TextDim());
	HitmenUI::AddToVBox(JoinColumn, JoinStatusText, FMargin(0.f, 4.f, 0.f, 20.f));

	HitmenUI::AddToVBox(JoinColumn, HitmenUI::MakeText(WidgetTree, LOCTEXT("Rooms", "ROOMS"), 20, TEXT("Bold"), HitmenUI::TextDim()));
	RoomList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(JoinColumn, RoomList, FMargin(0.f, 4.f, 0.f, 24.f));

	HitmenUI::AddToVBox(JoinColumn, HitmenUI::MakeText(WidgetTree, LOCTEXT("Friends", "STEAM FRIENDS"), 20, TEXT("Bold"), HitmenUI::TextDim()));
	FriendRoomList = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	HitmenUI::AddToVBox(JoinColumn, FriendRoomList, FMargin(0.f, 4.f, 0.f, 24.f));

	UHorizontalBox* JoinButtons = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	BackButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Back", "BACK"), 20, EHitmenButtonStyle::Solid);
	RefreshButton = HitmenUI::MakeButton(WidgetTree, LOCTEXT("Refresh", "REFRESH"), 20, EHitmenButtonStyle::Dashed);
	HitmenUI::AddToHBox(JoinButtons, BackButton, FMargin(0.f, 0.f, 12.f, 0.f));
	HitmenUI::AddToHBox(JoinButtons, RefreshButton);
	HitmenUI::AddToVBox(JoinColumn, JoinButtons, FMargin(0.f, 12.f), false, HAlign_Left);

	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	Scroll->AddChild(JoinColumn);
	Dim->SetContent(Scroll);
	JoinPanel = Dim;
	HitmenUI::AddToCanvas(Canvas, Dim, FAnchors(0.f, 0.f, 1.f, 1.f), FVector2D::ZeroVector, FVector2D::ZeroVector)->SetOffsets(FMargin(0.f));
	JoinPanel->SetVisibility(ESlateVisibility::Collapsed);
}

void UHitmenTitleScreenWidget::BindEvents()
{
	if (JoinButton) { JoinButton->OnClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::OpenJoin); }
	if (CreateRoomButton) { CreateRoomButton->OnClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::CreateRoom); }
	if (OptionsButton) { OptionsButton->OnClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::OpenOptions); }
	if (QuitButton) { QuitButton->OnClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::QuitGame); }
	if (DebugButton) { DebugButton->OnClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::OpenDebugGym); }
	if (BackButton) { BackButton->OnClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::CloseJoin); }
	if (RefreshButton) { RefreshButton->OnClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::RefreshRooms); }
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		Sessions->OnFindSessionsComplete.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleSessionsFound);
		Sessions->OnCreateSessionComplete.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleCreateComplete);
	}
}

void UHitmenTitleScreenWidget::UnbindEvents()
{
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		Sessions->OnFindSessionsComplete.RemoveAll(this);
		Sessions->OnCreateSessionComplete.RemoveAll(this);
	}
}

UHitmenSessionSubsystem* UHitmenTitleScreenWidget::GetSessions() const
{
	return GetGameInstance() ? GetGameInstance()->GetSubsystem<UHitmenSessionSubsystem>() : nullptr;
}

void UHitmenTitleScreenWidget::CreateRoom()
{
	if (UHitmenSessionSubsystem* Sessions = GetSessions())
	{
		const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
		FString Name = Settings && !Settings->PreferredName.IsEmpty() ? Settings->PreferredName : Sessions->GetLocalOnlineName();
		Name = Name.IsEmpty() ? TEXT("Hitmen room") : FString::Printf(TEXT("%s's room"), *Name);
		Sessions->CreateRoom(Name, UHitmenGameSettings::Get()->MaxPlayers, bUseLAN);
	}
}

void UHitmenTitleScreenWidget::HandleCreateComplete(bool bSuccess)
{
	if (!bSuccess && JoinStatusText)
	{
		JoinStatusText->SetText(LOCTEXT("CreateFailed", "Could not create the room."));
	}
}

void UHitmenTitleScreenWidget::OpenJoin()
{
	if (JoinPanel)
	{
		JoinPanel->SetVisibility(ESlateVisibility::Visible);
	}
	Juice_OnJoinPanelToggled(true);
	RefreshRooms();
}

void UHitmenTitleScreenWidget::CloseJoin()
{
	if (JoinPanel)
	{
		JoinPanel->SetVisibility(ESlateVisibility::Collapsed);
	}
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
		UHitmenListButton* Entry = WidgetTree->ConstructWidget<UHitmenListButton>(UHitmenListButton::StaticClass());
		const FText Label = FText::Format(LOCTEXT("RoomEntry", "{0}   {1}/{2}   {3} ms"), FText::FromString(Info.RoomName), FText::AsNumber(Info.CurrentPlayers), FText::AsNumber(Info.MaxPlayers), FText::AsNumber(Info.PingMs));
		Entry->Setup(Label, Info.ResultIndex, EHitmenButtonStyle::Solid, 18);
		Entry->OnItemClicked.AddUniqueDynamic(this, &UHitmenTitleScreenWidget::HandleJoinClicked);
		UVerticalBox* Target = Info.bIsFriend ? FriendRoomList.Get() : RoomList.Get();
		if (Target)
		{
			HitmenUI::AddToVBox(Target, Entry, FMargin(0.f, 3.f), false, HAlign_Left);
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
