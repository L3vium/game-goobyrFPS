// Hitmen — sessions

#include "Online/HitmenSessionSubsystem.h"
#include "Framework/HitmenLobbyGameMode.h"
#include "Framework/HitmenGameState.h"
#include "Core/HitmenGameSettings.h"
#include "Hitmen.h"
#include "OnlineSubsystem.h"
#include "OnlineSubsystemUtils.h"
#include "Interfaces/OnlineFriendsInterface.h"
#include "Interfaces/OnlineIdentityInterface.h"
#include "Interfaces/OnlinePresenceInterface.h"
#include "Online/OnlineSessionNames.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"
#include "Kismet/GameplayStatics.h"

namespace
{
	const FName HitmenSessionName = NAME_GameSession;
	const FName RoomNameKey = TEXT("HITMEN_ROOM");
	const FName RoomCodeKey = TEXT("HITMEN_CODE");
	const FName GameTagKey = TEXT("HITMEN_GAME");
	const FString GameTagValue = TEXT("Hitmen");
	/** No 0/O, 1/I/L: codes are read aloud and typed. */
	const TCHAR* CodeAlphabet = TEXT("ABCDEFGHJKMNPQRSTUVWXYZ23456789");
	constexpr int32 CodeLength = 6;

	FString MakeRoomCode()
	{
		const int32 AlphabetLength = FCString::Strlen(CodeAlphabet);
		FString Code;
		for (int32 Index = 0; Index < CodeLength; ++Index)
		{
			Code.AppendChar(CodeAlphabet[FMath::RandRange(0, AlphabetLength - 1)]);
		}
		return Code;
	}

	bool IsHitmenResult(const FOnlineSessionSearchResult& Result)
	{
		FString Tag;
		Result.Session.SessionSettings.Get(GameTagKey, Tag);
		return Tag == GameTagValue;
	}
}

void UHitmenSessionSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
}

void UHitmenSessionSubsystem::Deinitialize()
{
	// Never leave a delegate bound to a dead subsystem.
	if (const IOnlineSessionPtr Sessions = GetSessionInterface())
	{
		Sessions->ClearOnCreateSessionCompleteDelegate_Handle(CreateHandle);
		Sessions->ClearOnFindSessionsCompleteDelegate_Handle(FindHandle);
		Sessions->ClearOnFindFriendSessionCompleteDelegate_Handle(0, FindFriendHandle);
		Sessions->ClearOnJoinSessionCompleteDelegate_Handle(JoinHandle);
		Sessions->ClearOnDestroySessionCompleteDelegate_Handle(DestroyHandle);
	}
	Super::Deinitialize();
}

IOnlineSessionPtr UHitmenSessionSubsystem::GetSessionInterface() const
{
	const IOnlineSubsystem* OSS = Online::GetSubsystem(GetWorld());
	return OSS ? OSS->GetSessionInterface() : nullptr;
}

FUniqueNetIdRepl UHitmenSessionSubsystem::GetLocalNetId() const
{
	const ULocalPlayer* LP = GetGameInstance() ? GetGameInstance()->GetFirstGamePlayer() : nullptr;
	return LP ? LP->GetPreferredUniqueNetId() : FUniqueNetIdRepl();
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

FString UHitmenSessionSubsystem::NormalizeCode(const FString& Code)
{
	FString Result;
	for (const TCHAR Char : Code)
	{
		if (FChar::IsAlnum(Char))
		{
			Result.AppendChar(FChar::ToUpper(Char));
		}
	}
	return Result;
}

// ---------------------------------------------------------------------------------------------
// Create / destroy
// ---------------------------------------------------------------------------------------------

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

	RoomCode = MakeRoomCode();
	FOnlineSessionSettings Settings;
	Settings.NumPublicConnections = FMath::Max(1, MaxPlayers);
	Settings.bIsLANMatch = bLAN || !IsUsingSteam();
	Settings.bShouldAdvertise = true;
	Settings.bAllowJoinInProgress = true;
	Settings.bAllowJoinViaPresence = true;
	Settings.bUsesPresence = true;
	Settings.bUseLobbiesIfAvailable = true;
	Settings.Set(RoomNameKey, RoomName, EOnlineDataAdvertisementType::ViaOnlineServiceAndPing);
	Settings.Set(RoomCodeKey, RoomCode, EOnlineDataAdvertisementType::ViaOnlineServiceAndPing);
	Settings.Set(GameTagKey, GameTagValue, EOnlineDataAdvertisementType::ViaOnlineServiceAndPing);

	Sessions->ClearOnCreateSessionCompleteDelegate_Handle(CreateHandle);
	CreateHandle = Sessions->AddOnCreateSessionCompleteDelegate_Handle(
		FOnCreateSessionCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleCreateComplete));

	const FUniqueNetIdRepl NetId = GetLocalNetId();
	const bool bStarted = NetId.IsValid()
		? Sessions->CreateSession(*NetId, HitmenSessionName, Settings)
		: Sessions->CreateSession(0, HitmenSessionName, Settings);
	if (!bStarted)
	{
		Sessions->ClearOnCreateSessionCompleteDelegate_Handle(CreateHandle);
		RoomCode.Reset();
		OnCreateSessionComplete.Broadcast(false);
	}
}

void UHitmenSessionSubsystem::HandleCreateComplete(FName SessionName, bool bSuccess)
{
	if (const IOnlineSessionPtr Sessions = GetSessionInterface())
	{
		Sessions->ClearOnCreateSessionCompleteDelegate_Handle(CreateHandle);
	}
	if (!bSuccess)
	{
		RoomCode.Reset();
	}
	OnCreateSessionComplete.Broadcast(bSuccess);

	if (bSuccess)
	{
		// Already in the front end: start listening in this world and open the lobby (no reload).
		UWorld* World = GetGameInstance() ? GetGameInstance()->GetWorld() : nullptr;
		AHitmenLobbyGameMode* LobbyMode = World ? World->GetAuthGameMode<AHitmenLobbyGameMode>() : nullptr;
		if (LobbyMode && World->GetNetMode() == NM_Standalone)
		{
			FURL ListenURL;
			ListenURL.AddOption(TEXT("listen"));
			if (World->Listen(ListenURL))
			{
				if (AHitmenGameState* GS = World->GetGameState<AHitmenGameState>())
				{
					GS->SetRoomCode(RoomCode);
				}
				LobbyMode->OpenLobby();
				return;
			}
		}
		const TSoftObjectPtr<UWorld>& Lobby = UHitmenGameSettings::Get()->LobbyLevel;
		if (!Lobby.IsNull())
		{
			UGameplayStatics::OpenLevelBySoftObjectPtr(this, Lobby, true, TEXT("listen"));
		}
	}
}

void UHitmenSessionSubsystem::DestroySession()
{
	RoomCode.Reset();
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (!Sessions.IsValid() || !Sessions->GetNamedSession(HitmenSessionName))
	{
		return;
	}
	Sessions->ClearOnDestroySessionCompleteDelegate_Handle(DestroyHandle);
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

// ---------------------------------------------------------------------------------------------
// Searches (one at a time)
// ---------------------------------------------------------------------------------------------

bool UHitmenSessionSubsystem::StartSessionSearch(TSharedRef<FOnlineSessionSearch> Search)
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (!Sessions.IsValid())
	{
		return false;
	}
	SessionSearch = Search;
	Sessions->ClearOnFindSessionsCompleteDelegate_Handle(FindHandle);
	FindHandle = Sessions->AddOnFindSessionsCompleteDelegate_Handle(
		FOnFindSessionsCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleFindComplete));
	const FUniqueNetIdRepl NetId = GetLocalNetId();
	const bool bStarted = NetId.IsValid() ? Sessions->FindSessions(*NetId, Search) : Sessions->FindSessions(0, Search);
	if (!bStarted)
	{
		Sessions->ClearOnFindSessionsCompleteDelegate_Handle(FindHandle);
	}
	return bStarted;
}

void UHitmenSessionSubsystem::FindRooms(bool bLAN)
{
	if (IsSearching())
	{
		// A stuck search (lost callback) must not lock the Join screen forever.
		const UWorld* World = GetWorld();
		if (!World || World->GetRealTimeSeconds() - SearchStartTime < 20.f)
		{
			return;
		}
		SearchStage = ESearchStage::Idle;
	}
	bSearchLAN = bLAN;
	bPublicSearchSucceeded = false;
	PublicResults.Reset();
	FriendSessionResults.Reset();
	FriendsToQuery.Reset();
	SearchStartTime = GetWorld() ? GetWorld()->GetRealTimeSeconds() : 0.f;

	TSharedRef<FOnlineSessionSearch> Search = MakeShared<FOnlineSessionSearch>();
	Search->MaxSearchResults = UHitmenGameSettings::Get()->MaxSearchResults;
	Search->bIsLanQuery = bLAN || !IsUsingSteam();
	Search->QuerySettings.Set(SEARCH_LOBBIES, true, EOnlineComparisonOp::Equals);
	SearchStage = ESearchStage::Public;
	if (!StartSessionSearch(Search))
	{
		HandleFindComplete(false);
	}
}

void UHitmenSessionSubsystem::JoinByCode(const FString& InCode, bool bLAN)
{
	const FString Code = NormalizeCode(InCode);
	if (Code.Len() != CodeLength || IsSearching() || bJoining)
	{
		OnJoinSessionComplete.Broadcast(false);
		return;
	}
	PendingCode = Code;
	bSearchLAN = bLAN;
	SearchStartTime = GetWorld() ? GetWorld()->GetRealTimeSeconds() : 0.f;
	TSharedRef<FOnlineSessionSearch> Search = MakeShared<FOnlineSessionSearch>();
	Search->MaxSearchResults = UHitmenGameSettings::Get()->MaxSearchResults;
	Search->bIsLanQuery = bLAN || !IsUsingSteam();
	Search->QuerySettings.Set(SEARCH_LOBBIES, true, EOnlineComparisonOp::Equals);
	Search->QuerySettings.Set(RoomCodeKey, Code, EOnlineComparisonOp::Equals);
	SearchStage = ESearchStage::Code;
	if (!StartSessionSearch(Search))
	{
		HandleFindComplete(false);
	}
}

void UHitmenSessionSubsystem::HandleFindComplete(bool bSuccess)
{
	if (const IOnlineSessionPtr Sessions = GetSessionInterface())
	{
		Sessions->ClearOnFindSessionsCompleteDelegate_Handle(FindHandle);
	}
	if (SearchStage == ESearchStage::Code)
	{
		SearchStage = ESearchStage::Idle;
		// The service may ignore the code filter (LAN): check it here.
		if (bSuccess && SessionSearch.IsValid())
		{
			for (const FOnlineSessionSearchResult& Result : SessionSearch->SearchResults)
			{
				FString Code;
				Result.Session.SessionSettings.Get(RoomCodeKey, Code);
				if (IsHitmenResult(Result) && Code == PendingCode)
				{
					JoinResult(Result);
					return;
				}
			}
		}
		OnJoinSessionComplete.Broadcast(false);
		return;
	}
	if (SearchStage != ESearchStage::Public)
	{
		return;
	}
	bPublicSearchSucceeded = bSuccess;
	if (bSuccess && SessionSearch.IsValid())
	{
		PublicResults = SessionSearch->SearchResults;
	}
	PublishResults();
	// Then the friends list, then friends' rooms one by one (Steam: one search at a time).
	SearchStage = ESearchStage::FriendSessions;
	ReadFriends();
}

void UHitmenSessionSubsystem::ReadFriends()
{
	const IOnlineSubsystem* OSS = Online::GetSubsystem(GetWorld());
	const IOnlineFriendsPtr FriendsInterface = OSS ? OSS->GetFriendsInterface() : nullptr;
	if (!FriendsInterface.IsValid() || !IsUsingSteam())
	{
		Friends.Reset();
		FriendIndexById.Reset();
		PublishFriends();
		FinishSearch();
		return;
	}
	const bool bStarted = FriendsInterface->ReadFriendsList(0, EFriendsLists::ToString(EFriendsLists::Default),
		FOnReadFriendsListComplete::CreateWeakLambda(this, [this](int32, bool bOk, const FString& ListName, const FString&)
		{
			if (SearchStage != ESearchStage::FriendSessions)
			{
				return;
			}
			const IOnlineSubsystem* OSS = Online::GetSubsystem(GetWorld());
			const IOnlineFriendsPtr FriendsInterface = OSS ? OSS->GetFriendsInterface() : nullptr;
			Friends.Reset();
			FriendIndexById.Reset();
			FriendsToQuery.Reset();
			if (bOk && FriendsInterface.IsValid())
			{
				TArray<TSharedRef<FOnlineFriend>> List;
				FriendsInterface->GetFriendsList(0, ListName, List);
				for (const TSharedRef<FOnlineFriend>& Friend : List)
				{
					const FOnlineUserPresence& Presence = Friend->GetPresence();
					FHitmenFriendInfo& Info = Friends.AddDefaulted_GetRef();
					Info.DisplayName = Friend->GetDisplayName();
					Info.bOnline = Presence.bIsOnline;
					Info.bPlayingHitmen = Presence.bIsOnline && Presence.bIsPlayingThisGame;
					FriendIndexById.Add(Friend->GetUserId()->ToString(), Friends.Num() - 1);
					if (Info.bPlayingHitmen)
					{
						FriendsToQuery.Add(Friend->GetUserId());
					}
				}
				// Playing Hitmen first, then online, then the rest; names alphabetical.
				Friends.Sort([](const FHitmenFriendInfo& A, const FHitmenFriendInfo& B)
				{
					const int32 RankA = A.bPlayingHitmen ? 0 : (A.bOnline ? 1 : 2);
					const int32 RankB = B.bPlayingHitmen ? 0 : (B.bOnline ? 1 : 2);
					return RankA != RankB ? RankA < RankB : A.DisplayName < B.DisplayName;
				});
				FriendIndexById.Reset();
				for (const TSharedRef<FOnlineFriend>& Friend : List)
				{
					const FString Name = Friend->GetDisplayName();
					const int32 Index = Friends.IndexOfByPredicate([&Name](const FHitmenFriendInfo& Info) { return Info.DisplayName == Name; });
					FriendIndexById.Add(Friend->GetUserId()->ToString(), Index);
				}
			}
			PublishResults();
			PublishFriends();
			QueryNextFriendSession();
		}));
	if (!bStarted)
	{
		PublishFriends();
		FinishSearch();
	}
}

void UHitmenSessionSubsystem::QueryNextFriendSession()
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (SearchStage != ESearchStage::FriendSessions || !Sessions.IsValid() || FriendsToQuery.IsEmpty())
	{
		FinishSearch();
		return;
	}
	const FUniqueNetIdRef FriendId = FriendsToQuery.Pop();
	Sessions->ClearOnFindFriendSessionCompleteDelegate_Handle(0, FindFriendHandle);
	FindFriendHandle = Sessions->AddOnFindFriendSessionCompleteDelegate_Handle(0,
		FOnFindFriendSessionCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleFindFriendSessionComplete));
	// A synchronous failure calls the handler right away, which moves to the next friend.
	Sessions->FindFriendSession(0, *FriendId);
}

void UHitmenSessionSubsystem::HandleFindFriendSessionComplete(int32 LocalUserNum, bool bWasSuccessful, const TArray<FOnlineSessionSearchResult>& Results)
{
	if (const IOnlineSessionPtr Sessions = GetSessionInterface())
	{
		Sessions->ClearOnFindFriendSessionCompleteDelegate_Handle(0, FindFriendHandle);
	}
	if (SearchStage != ESearchStage::FriendSessions)
	{
		return;
	}
	if (bWasSuccessful)
	{
		FriendSessionResults.Append(Results);
		PublishResults();
		PublishFriends();
	}
	// Next query on the next tick: never re-enter the Steam session interface from its own callback.
	if (UWorld* World = GetWorld())
	{
		World->GetTimerManager().SetTimerForNextTick(FTimerDelegate::CreateWeakLambda(this, [this]() { QueryNextFriendSession(); }));
	}
	else
	{
		FinishSearch();
	}
}

void UHitmenSessionSubsystem::FinishSearch()
{
	if (SearchStage == ESearchStage::FriendSessions || SearchStage == ESearchStage::Public)
	{
		SearchStage = ESearchStage::Idle;
		PublishResults();
	}
}

void UHitmenSessionSubsystem::PublishResults()
{
	JoinableResults.Reset();
	LastResults.Reset();
	TSet<FString> SeenSessions;
	for (FHitmenFriendInfo& Friend : Friends)
	{
		Friend.RoomResultIndex = INDEX_NONE;
	}
	auto AddResult = [this, &SeenSessions](const FOnlineSessionSearchResult& Result, bool bForceFriend)
	{
		const FString SessionId = Result.GetSessionIdStr();
		if (!IsHitmenResult(Result) || SeenSessions.Contains(SessionId))
		{
			return;
		}
		SeenSessions.Add(SessionId);

		FHitmenSessionInfo& Info = LastResults.AddDefaulted_GetRef();
		Info.ResultIndex = JoinableResults.Add(Result);
		Result.Session.SessionSettings.Get(RoomNameKey, Info.RoomName);
		Result.Session.SessionSettings.Get(RoomCodeKey, Info.Code);
		Info.HostName = Result.Session.OwningUserName;
		Info.MaxPlayers = Result.Session.SessionSettings.NumPublicConnections;
		Info.CurrentPlayers = Info.MaxPlayers - Result.Session.NumOpenPublicConnections;
		Info.PingMs = Result.PingInMs;
		const FString OwnerId = Result.Session.OwningUserId.IsValid() ? Result.Session.OwningUserId->ToString() : FString();
		const int32* FriendIndex = OwnerId.IsEmpty() ? nullptr : FriendIndexById.Find(OwnerId);
		Info.bIsFriend = bForceFriend || FriendIndex != nullptr;
		if (FriendIndex && Friends.IsValidIndex(*FriendIndex))
		{
			Friends[*FriendIndex].RoomResultIndex = Info.ResultIndex;
		}
		if (Info.RoomName.IsEmpty())
		{
			Info.RoomName = Info.HostName;
		}
	};

	for (const FOnlineSessionSearchResult& Result : FriendSessionResults)
	{
		AddResult(Result, true);
	}
	for (const FOnlineSessionSearchResult& Result : PublicResults)
	{
		AddResult(Result, false);
	}

	// Friends first.
	LastResults.StableSort([](const FHitmenSessionInfo& A, const FHitmenSessionInfo& B) { return A.bIsFriend && !B.bIsFriend; });
	OnFindSessionsComplete.Broadcast(LastResults, bPublicSearchSucceeded || !FriendSessionResults.IsEmpty());
}

void UHitmenSessionSubsystem::PublishFriends()
{
	OnFriendsUpdated.Broadcast(Friends);
}

// ---------------------------------------------------------------------------------------------
// Join
// ---------------------------------------------------------------------------------------------

void UHitmenSessionSubsystem::JoinRoom(int32 ResultIndex)
{
	if (!JoinableResults.IsValidIndex(ResultIndex) || bJoining)
	{
		OnJoinSessionComplete.Broadcast(false);
		return;
	}
	// Copy: a late search result must not invalidate the session we are joining.
	const FOnlineSessionSearchResult Result = JoinableResults[ResultIndex];
	JoinResult(Result);
}

void UHitmenSessionSubsystem::JoinResult(const FOnlineSessionSearchResult& Result)
{
	const IOnlineSessionPtr Sessions = GetSessionInterface();
	if (!Sessions.IsValid() || !Result.IsValid())
	{
		OnJoinSessionComplete.Broadcast(false);
		return;
	}
	// Stop the friend queries: the join owns the session interface now.
	if (SearchStage == ESearchStage::FriendSessions)
	{
		FriendsToQuery.Reset();
	}
	bJoining = true;
	Sessions->ClearOnJoinSessionCompleteDelegate_Handle(JoinHandle);
	JoinHandle = Sessions->AddOnJoinSessionCompleteDelegate_Handle(
		FOnJoinSessionCompleteDelegate::CreateUObject(this, &UHitmenSessionSubsystem::HandleJoinComplete));

	const FUniqueNetIdRepl NetId = GetLocalNetId();
	const bool bStarted = NetId.IsValid()
		? Sessions->JoinSession(*NetId, HitmenSessionName, Result)
		: Sessions->JoinSession(0, HitmenSessionName, Result);
	if (!bStarted)
	{
		bJoining = false;
		Sessions->ClearOnJoinSessionCompleteDelegate_Handle(JoinHandle);
		OnJoinSessionComplete.Broadcast(false);
	}
}

void UHitmenSessionSubsystem::HandleJoinComplete(FName SessionName, EOnJoinSessionCompleteResult::Type Result)
{
	bJoining = false;
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
