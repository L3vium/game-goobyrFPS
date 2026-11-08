// Hitmen — Steam (or Null in PIE) sessions: create a room, list rooms and friends' rooms, join

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

	/** Index into the last search results (pass to JoinSession). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	int32 ResultIndex = INDEX_NONE;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	FString RoomName;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Session")
	FString HostName;

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

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenSessionResult, bool, bSuccess);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenSessionsFound, const TArray<FHitmenSessionInfo>&, Sessions, bool, bSuccess);

UCLASS()
class HITMEN_API UHitmenSessionSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Session")
	FOnHitmenSessionResult OnCreateSessionComplete;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Session")
	FOnHitmenSessionsFound OnFindSessionsComplete;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Session")
	FOnHitmenSessionResult OnJoinSessionComplete;

	/** Creates a room and travels to the lobby map as listen server. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void CreateRoom(const FString& RoomName, int32 MaxPlayers, bool bLAN);

	/** Searches public rooms; results flag rooms hosted by friends. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void FindRooms(bool bLAN);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void JoinRoom(int32 ResultIndex);

	UFUNCTION(BlueprintCallable, Category="Hitmen|Session")
	void DestroySession();

	/** Steam persona name of the local user, or empty. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	FString GetLocalOnlineName() const;

	/** True when the Steam subsystem is active (false in PIE / Null). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	bool IsUsingSteam() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Session")
	const TArray<FHitmenSessionInfo>& GetLastResults() const { return LastResults; }

private:
	IOnlineSessionPtr GetSessionInterface() const;
	void ReadFriends();

	void HandleCreateComplete(FName SessionName, bool bSuccess);
	void HandleFindComplete(bool bSuccess);
	void HandleJoinComplete(FName SessionName, EOnJoinSessionCompleteResult::Type Result);
	void HandleDestroyComplete(FName SessionName, bool bSuccess);
	void HandleFindFriendSessionComplete(int32 LocalUserNum, bool bWasSuccessful, const TArray<FOnlineSessionSearchResult>& Results);

	/** Asks Steam for the session of every online friend (rooms that a public search may miss). */
	void QueryFriendSessions();
	/** Merges public results and friend sessions into LastResults / JoinableResults and broadcasts. */
	void PublishResults();

	TSharedPtr<FOnlineSessionSearch> SessionSearch;
	TArray<FHitmenSessionInfo> LastResults;
	/** Public search results then friend sessions; FHitmenSessionInfo::ResultIndex indexes this. */
	TArray<FOnlineSessionSearchResult> JoinableResults;
	TArray<FOnlineSessionSearchResult> FriendSessionResults;
	TSet<FString> FriendIds;
	TArray<FUniqueNetIdRef> OnlineFriendIds;
	FDelegateHandle FindFriendHandle;
	int32 PendingFriendQueries = 0;
	bool bPublicSearchDone = false;
	bool bPublicSearchSucceeded = false;
	bool bFriendsRead = false;

	FDelegateHandle CreateHandle;
	FDelegateHandle FindHandle;
	FDelegateHandle JoinHandle;
	FDelegateHandle DestroyHandle;

	/** Create request waiting for an old session to be destroyed first. */
	bool bPendingCreate = false;
	FString PendingRoomName;
	int32 PendingMaxPlayers = 8;
	bool bPendingLAN = false;
};
