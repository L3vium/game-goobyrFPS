// Hitmen — title screen: big HITMEN title, square left-aligned buttons (Join dashed above
// Create Room, Options, Quit), debug button bottom-right, Join overlay with rooms and friends

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "Online/HitmenSessionSubsystem.h"
#include "HitmenTitleScreenWidget.generated.h"

class UButton;
class UTextBlock;
class UVerticalBox;
class UWidget;

UCLASS()
class HITMEN_API UHitmenTitleScreenWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Title") void CreateRoom();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Title") void OpenJoin();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Title") void CloseJoin();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Title") void RefreshRooms();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Title") void OpenOptions();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Title") void QuitGame();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Title") void OpenDebugGym();

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;

	/** Search LAN instead of Steam (local testing). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Title")
	bool bUseLAN = false;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> JoinButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> CreateRoomButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> OptionsButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> QuitButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> DebugButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UWidget> JoinPanel;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> RoomList;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> FriendRoomList;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> JoinStatusText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> RefreshButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> BackButton;

	UFUNCTION() void HandleSessionsFound(const TArray<FHitmenSessionInfo>& Sessions, bool bSuccess);
	UFUNCTION() void HandleJoinClicked(int32 ResultIndex);
	UFUNCTION() void HandleCreateComplete(bool bSuccess);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnJoinPanelToggled(bool bOpen);

	UHitmenSessionSubsystem* GetSessions() const;
};
