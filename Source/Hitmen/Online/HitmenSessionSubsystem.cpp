// Hitmen — sessions

#include "Online/HitmenSessionSubsystem.h"
#include "Core/HitmenGameSettings.h"
#include "Hitmen.h"
#include "OnlineSubsystem.h"
#include "OnlineSubsystemUtils.h"
#include "Interfaces/OnlineFriendsInterface.h"
#include "Interfaces/OnlineIdentityInterface.h"
#include "Online/OnlineSessionNames.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"
#include "Kismet/GameplayStatics.h"

namespace
{
	const FName HitmenSessionName = NAME_GameSession;
	const FName RoomNameKey = TEXT("HITMEN_ROOM");
	const FName GameTagKey = TEXT("HITMEN_GAME");
	const FString GameTagValue = TEXT("Hitmen");
}

void UHitmenSessionSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
}

IOnlineSessionPtr UHitmenSessionSubsystem::GetSessionInterface() const
{
	const IOnlineSubsystem* OSS = Online::GetSubsystem(GetWorld());
	return OSS ? OSS->GetSessionInterface() : nullptr;
}

bool UHitmenSessionSubsystem::IsUsingSteam() const
{
	const IOnlineSubsystem* OSS = Online::GetSubsystem(GetWorld());
	return OSS && OSS->GetSubsystemName() == STEAM_SUBSYSTEM;
}

FString UHitmenSessionSubsystem::GetLocalOnlineName() const
{
	const IOnlineSubsystem* OSS = Online::GetSubsystem(GetWorld());
	if (!OSS || OSS->GetSubsystemName() != STEAM_SUBSYSTEM)
	{
		return FString();
	}
	const IOnlineIdentityPtr Identity = OSS->GetIdentityInterface();
	return Identity.IsValid() ? Identity->GetPlayerNickname(0) : FString();
}

void UHitmenSessionSubsystem::CreateRoom(const FString& RoomName, int32 MaxPlayers, bool bLAN)
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (!Sessions.IsValid())
	{
		OnCreateSessionComplete.Broadcast(false);
		return;
	}

	if (Sessions->GetNamedSession(HitmenSessionName))
	{
		// Destroy the old one first, then create.
		bPendingCreate = true;
		PendingRoomName = RoomName;
		PendingMaxPlayers = MaxPlayers;
		bPendingLAN = bLAN;
		DestroySession();
		return;
	}

	FOnlineSessionSettings Settings;
	Settings.NumPublicConnections = FMath::Max(1, MaxPlayers);
	Settings.bIsLANMatch = bLAN || !IsUsingSteam();
	Settings.bShouldAdvertise = true;
	Settings.bAllowJoinInProgress = true;
	Settings.bAllowJoinViaPresence = true;
	Settings.bUsesPresence = true;
	Settings.bUseLobbiesIfAvailable = true;
	Settings.Set(RoomNameKey, RoomName, EOnlineDataAdvertisementType::ViaOnlineServiceAndPing);
	Settings.Set(GameTagKey, GameTagValue, EOnlineDataAdvertisementType::ViaOnlineServiceAndPing);

	CreateHandle = Sessions->AddOnCreateSessionCompleteDelegate_Handle(
		FOnCreateSessionCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleCreateComplete));

	const ULocalPlayer* LP = GetGameInstance()->GetFirstGamePlayer();
	const FUniqueNetIdRepl NetId = LP ? LP->GetPreferredUniqueNetId() : FUniqueNetIdRepl();
	const bool bStarted = NetId.IsValid()
		? Sessions->CreateSession(*NetId, HitmenSessionName, Settings)
		: Sessions->CreateSession(0, HitmenSessionName, Settings);
	if (!bStarted)
	{
		Sessions->ClearOnCreateSessionCompleteDelegate_Handle(CreateHandle);
		OnCreateSessionComplete.Broadcast(false);
	}
}

void UHitmenSessionSubsystem::HandleCreateComplete(FName SessionName, bool bSuccess)
{
	if (const IOnlineSessionPtr Sessions = GetSessionInterface())
	{
		Sessions->ClearOnCreateSessionCompleteDelegate_Handle(CreateHandle);
	}
	OnCreateSessionComplete.Broadcast(bSuccess);

	if (bSuccess)
	{
		const TSoftObjectPtr<UWorld>& Lobby = UHitmenGameSettings::Get()->LobbyLevel;
		if (!Lobby.IsNull())
		{
			UGameplayStatics::OpenLevelBySoftObjectPtr(this, Lobby, true, TEXT("listen"));
		}
	}
}

void UHitmenSessionSubsystem::FindRooms(bool bLAN)
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (!Sessions.IsValid())
	{
		OnFindSessionsComplete.Broadcast(TArray<FHitmenSessionInfo>(), false);
		return;
	}

	ReadFriends();

	SessionSearch = MakeShared<FOnlineSessionSearch>();
	SessionSearch->MaxSearchResults = UHitmenGameSettings::Get()->MaxSearchResults;
	SessionSearch->bIsLanQuery = bLAN || !IsUsingSteam();
	SessionSearch->QuerySettings.Set(SEARCH_LOBBIES, true, EOnlineComparisonOp::Equals);

	FindHandle = Sessions->AddOnFindSessionsCompleteDelegate_Handle(
		FOnFindSessionsCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleFindComplete));

	const ULocalPlayer* LP = GetGameInstance()->GetFirstGamePlayer();
	const FUniqueNetIdRepl NetId = LP ? LP->GetPreferredUniqueNetId() : FUniqueNetIdRepl();
	const bool bStarted = NetId.IsValid()
		? Sessions->FindSessions(*NetId, SessionSearch.ToSharedRef())
		: Sessions->FindSessions(0, SessionSearch.ToSharedRef());
	if (!bStarted)
	{
		Sessions->ClearOnFindSessionsCompleteDelegate_Handle(FindHandle);
		OnFindSessionsComplete.Broadcast(TArray<FHitmenSessionInfo>(), false);
	}
}

void UHitmenSessionSubsystem::HandleFindComplete(bool bSuccess)
{
	if (const IOnlineSessionPtr Sessions = GetSessionInterface())
	{
		Sessions->ClearOnFindSessionsCompleteDelegate_Handle(FindHandle);
	}

	LastResults.Reset();
	if (bSuccess && SessionSearch.IsValid())
	{
		for (int32 Index = 0; Index < SessionSearch->SearchResults.Num(); ++Index)
		{
			const FOnlineSessionSearchResult& Result = SessionSearch->SearchResults[Index];
			FString Tag;
			Result.Session.SessionSettings.Get(GameTagKey, Tag);
			if (Tag != GameTagValue)
			{
				continue;
			}

			FHitmenSessionInfo& Info = LastResults.AddDefaulted_GetRef();
			Info.ResultIndex = Index;
			Result.Session.SessionSettings.Get(RoomNameKey, Info.RoomName);
			Info.HostName = Result.Session.OwningUserName;
			Info.MaxPlayers = Result.Session.SessionSettings.NumPublicConnections;
			Info.CurrentPlayers = Info.MaxPlayers - Result.Session.NumOpenPublicConnections;
			Info.PingMs = Result.PingInMs;
			Info.bIsFriend = Result.Session.OwningUserId.IsValid() && FriendIds.Contains(Result.Session.OwningUserId->ToString());
			if (Info.RoomName.IsEmpty())
			{
				Info.RoomName = Info.HostName;
			}
		}
	}

	// Friends first.
	LastResults.StableSort([](const FHitmenSessionInfo& A, const FHitmenSessionInfo& B) { return A.bIsFriend && !B.bIsFriend; });
	OnFindSessionsComplete.Broadcast(LastResults, bSuccess);
}

void UHitmenSessionSubsystem::JoinRoom(int32 ResultIndex)
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (!Sessions.IsValid() || !SessionSearch.IsValid() || !SessionSearch->SearchResults.IsValidIndex(ResultIndex))
	{
		OnJoinSessionComplete.Broadcast(false);
		return;
	}

	JoinHandle = Sessions->AddOnJoinSessionCompleteDelegate_Handle(
		FOnJoinSessionCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleJoinComplete));

	const ULocalPlayer* LP = GetGameInstance()->GetFirstGamePlayer();
	const FUniqueNetIdRepl NetId = LP ? LP->GetPreferredUniqueNetId() : FUniqueNetIdRepl();
	const bool bStarted = NetId.IsValid()
		? Sessions->JoinSession(*NetId, HitmenSessionName, SessionSearch->SearchResults[ResultIndex])
		: Sessions->JoinSession(0, HitmenSessionName, SessionSearch->SearchResults[ResultIndex]);
	if (!bStarted)
	{
		Sessions->ClearOnJoinSessionCompleteDelegate_Handle(JoinHandle);
		OnJoinSessionComplete.Broadcast(false);
	}
}

void UHitmenSessionSubsystem::HandleJoinComplete(FName SessionName, EOnJoinSessionCompleteResult::Type Result)
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (Sessions.IsValid())
	{
		Sessions->ClearOnJoinSessionCompleteDelegate_Handle(JoinHandle);
	}

	const bool bSuccess = Result == EOnJoinSessionCompleteResult::Success;
	OnJoinSessionComplete.Broadcast(bSuccess);
	if (!bSuccess || !Sessions.IsValid())
	{
		return;
	}

	FString ConnectString;
	if (Sessions->GetResolvedConnectString(SessionName, ConnectString))
	{
		if (APlayerController* PC = GetGameInstance()->GetFirstLocalPlayerController())
		{
			PC->ClientTravel(ConnectString, TRAVEL_Absolute);
		}
	}
}

void UHitmenSessionSubsystem::DestroySession()
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (!Sessions.IsValid() || !Sessions->GetNamedSession(HitmenSessionName))
	{
		return;
	}
	DestroyHandle = Sessions->AddOnDestroySessionCompleteDelegate_Handle(
		FOnDestroySessionCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleDestroyComplete));
	Sessions->DestroySession(HitmenSessionName);
}

void UHitmenSessionSubsystem::HandleDestroyComplete(FName SessionName, bool bSuccess)
{
	if (const IOnlineSessionPtr Sessions = GetSessionInterface())
	{
		Sessions->ClearOnDestroySessionCompleteDelegate_Handle(DestroyHandle);
	}
	if (bPendingCreate)
	{
		bPendingCreate = false;
		CreateRoom(PendingRoomName, PendingMaxPlayers, bPendingLAN);
	}
}

void UHitmenSessionSubsystem::ReadFriends()
{
	const IOnlineSubsystem* OSS = Online::GetSubsystem(GetWorld());
	const IOnlineFriendsPtr Friends = OSS ? OSS->GetFriendsInterface() : nullptr;
	if (!Friends.IsValid())
	{
		return;
	}
	Friends->ReadFriendsList(0, EFriendsLists::ToString(EFriendsLists::Default),
		FOnReadFriendsListComplete::CreateWeakLambda(this, [this, Friends](int32, bool bOk, const FString& ListName, const FString&)
		{
			if (!bOk)
			{
				return;
			}
			TArray<TSharedRef<FOnlineFriend>> List;
			Friends->GetFriendsList(0, ListName, List);
			FriendIds.Reset();
			for (const TSharedRef<FOnlineFriend>& Friend : List)
			{
				FriendIds.Add(Friend->GetUserId()->ToString());
			}
		}));
}
