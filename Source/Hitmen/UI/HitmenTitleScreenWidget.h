// Hitmen — front-end root: title page (logo + menu), join page, and the lobby screens, all over
// the same 3D scene (the camera moves between shots; no level is loaded between them)

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "Online/HitmenSessionSubsystem.h"
#include "Core/HitmenTypes.h"
#include "HitmenTitleScreenWidget.generated.h"

class UTextBlock;
class UVerticalBox;
class UWidget;
class UWidgetSwitcher;
class UHitmenMenuButton;
class UHitmenBackdrop;
class UHitmenLobbyWidget;

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

	/** Pages: 0 title, 1 join, 2 lobby / customization. */
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UWidgetSwitcher> Pages;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenBackdrop> Backdrop;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenLobbyWidget> Lobby;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> RoomList;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> FriendRoomList;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> JoinStatusText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> TitleStatusText;

	UFUNCTION() void HandleSessionsFound(const TArray<FHitmenSessionInfo>& Sessions, bool bSuccess);
	UFUNCTION() void HandleJoinClicked(int32 ResultIndex);
	UFUNCTION() void HandleCreateComplete(bool bSuccess);
	UFUNCTION() void HandleJoinComplete(bool bSuccess);
	UFUNCTION() void HandlePhaseChanged(EHitmenMatchPhase Phase);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnJoinPanelToggled(bool bOpen);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnPageChanged(int32 Page);

	void ShowPage(int32 Page);
	void RefreshPageFromPhase();
	UHitmenSessionSubsystem* GetSessions() const;
	UHitmenMenuButton* AddMenuButton(UVerticalBox* Box, const FText& Label, int32 Kind, FName Handler);
};
