// Hitmen — Steam (or Null in PIE) sessions: create a room (with a short join code), list rooms,
// list Steam friends (and the room each one is in), join a room, a friend or a code.
// Steam allows one search at a time (FindSessions and FindFriendSession share a slot), so every
// query goes through one queue and runs alone.

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Interfaces/OnlineSessionInterface.h"
#include "OnlineSessionSettings.h"
#include "HitmenSessionSubsystem.generated.h"

USTRUCT(BlueprintType)
struct FHitmenSessionInfo
{
	GENERATED_BODY()

	/** Index into the last search results (pass to JoinRoom). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	int32 ResultIndex = INDEX_NONE;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	FString RoomName;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	FString HostName;

	/** Short join code of the room. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	FString Code;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	int32 CurrentPlayers = 0;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	int32 MaxPlayers = 0;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	int32 PingMs = 0;

	/** Host is on the local user's Steam friends list. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	bool bIsFriend = false;
};

/** One Steam friend for the Join screen. */
USTRUCT(BlueprintType)
struct FHitmenFriendInfo
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	FString DisplayName;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	bool bOnline = false;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	bool bPlayingHitmen = false;

	/** Index of the friend's room in the last results (INDEX_NONE: not in a room we found). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	int32 RoomResultIndex = INDEX_NONE;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSessionResult, bool, bSuccess);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenSessionsFound, const TArray<FHitmenSessionInfo>&, Sessions, bool, bSuccess);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenFriendsUpdated, const TArray<FHitmenFriendInfo>&, Friends);

UCLASS()
class HITMEN_API UHitmenSessionSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Session")
	FOnHitmenSessionResult OnCreateSessionComplete;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Session")
	FOnHitmenSessionsFound OnFindSessionsComplete;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Session")
	FOnHitmenFriendsUpdated OnFriendsUpdated;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Session")
	FOnHitmenSessionResult OnJoinSessionComplete;

	/** Creates a room (with a fresh join code) and listens in place. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void CreateRoom(const FString& RoomName, int32 MaxPlayers, bool bLAN);

	/** Searches public rooms, then friends' rooms (one query at a time). Ignored while a search runs. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void FindRooms(bool bLAN);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void JoinRoom(int32 ResultIndex);

	/** Searches the room with this code and joins it (OnJoinSessionComplete false when not found). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void JoinByCode(const FString& Code, bool bLAN);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void DestroySession();

	/** Join code of the room this game hosts (empty when not hosting). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	const FString& GetRoomCode() const { return RoomCode; }

	/** True while a search (public, friends or code) is running. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	bool IsSearching() const { return SearchStage != ESearchStage::Idle; }

	/** Steam persona name of the local user, or empty. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	FString GetLocalOnlineName() const;

	/** True when the Steam subsystem is active (false in PIE / Null). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	bool IsUsingSteam() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	const TArray<FHitmenSessionInfo>& GetLastResults() const { return LastResults; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	const TArray<FHitmenFriendInfo>& GetFriends() const { return Friends; }

	/** Normalizes user input to a code (upper case, no spaces). */
	static FString NormalizeCode(const FString& Code);

private:
	enum class ESearchStage : uint8 { Idle, Public, FriendSessions, Code };

	IOnlineSessionPtr GetSessionInterface() const;
	FUniqueNetIdRepl GetLocalNetId() const;
	void ReadFriends();
	bool StartSessionSearch(TSharedRef<FOnlineSessionSearch> Search);
	void FinishSearch();

	void HandleCreateComplete(FName SessionName, bool bSuccess);
	void HandleFindComplete(bool bSuccess);
	void HandleJoinComplete(FName SessionName, EOnJoinSessionCompleteResult::Type Result);
	void HandleDestroyComplete(FName SessionName, bool bSuccess);
	void HandleFindFriendSessionComplete(int32 LocalUserNum, bool bWasSuccessful, const TArray<FOnlineSessionSearchResult>& Results);

	/** Queries the next friend playing Hitmen (one at a time); finishes the search when done. */
	void QueryNextFriendSession();
	/** Merges public results and friend sessions into LastResults / JoinableResults and broadcasts. */
	void PublishResults();
	void PublishFriends();
	void JoinResult(const FOnlineSessionSearchResult& Result);

	TSharedPtr<FOnlineSessionSearch> SessionSearch;
	TArray<FHitmenSessionInfo> LastResults;
	/** FHitmenSessionInfo::ResultIndex indexes this. */
	TArray<FOnlineSessionSearchResult> JoinableResults;
	TArray<FOnlineSessionSearchResult> PublicResults;
	TArray<FOnlineSessionSearchResult> FriendSessionResults;
	TArray<FHitmenFriendInfo> Friends;
	/** Friend id string -> index in Friends. */
	TMap<FString, int32> FriendIndexById;
	TArray<FUniqueNetIdRef> FriendsToQuery;
	FString PendingCode;

	ESearchStage SearchStage = ESearchStage::Idle;
	bool bSearchLAN = false;
	bool bPublicSearchSucceeded = false;
	bool bJoining = false;
	float SearchStartTime = 0.f;

	FString RoomCode;

	FDelegateHandle CreateHandle;
	FDelegateHandle FindHandle;
	FDelegateHandle FindFriendHandle;
	FDelegateHandle JoinHandle;
	FDelegateHandle DestroyHandle;

	/** Create request waiting for an old session to be destroyed first. */
	bool bPendingCreate = false;
	FString PendingRoomName;
	int32 PendingMaxPlayers = 8;
	bool bPendingLAN = false;
};
