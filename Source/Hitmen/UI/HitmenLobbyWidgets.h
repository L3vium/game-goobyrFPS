// Hitmen — lobby screens: Create Room (map carousel, options, players, teams with drag & drop,
// roles, watchers) and Customization (look + loadout), plus their row widgets

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "Blueprint/DragDropOperation.h"
#include "Core/HitmenTypes.h"
#include "HitmenLobbyWidgets.generated.h"

class AHitmenPlayerState;
class UButton;
class UTextBlock;
class UVerticalBox;
class UEditableTextBox;
class UWidgetSwitcher;
class UImage;
class UHitmenCatalog;

UCLASS()
class HITMEN_API UHitmenPlayerDragOperation : public UDragDropOperation
{
	GENERATED_BODY()

public:
	UPROPERTY(BlueprintReadWrite, Category="Hitmen|Lobby")
	TObjectPtr<AHitmenPlayerState> PlayerState;
};

/** One player row: icon, name (editable for yourself), watcher toggle, draggable by the host. */
UCLASS()
class HITMEN_API UHitmenPlayerEntryWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby")
	void SetPlayer(AHitmenPlayerState* InPlayerState);

	UFUNCTION(BlueprintPure, Category="Hitmen|Lobby")
	AHitmenPlayerState* GetPlayer() const { return PlayerState; }

protected:
	virtual void BuildDefaultLayout() override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnDragDetected(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent, UDragDropOperation*& OutOperation) override;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> NameText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UEditableTextBox> NameEdit;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> IconButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> IconText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UImage> IconImage;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> WatcherButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> WatcherText;

	UPROPERTY(Transient)
	TObjectPtr<AHitmenPlayerState> PlayerState;

	UFUNCTION() void HandleNameCommitted(const FText& Text, ETextCommit::Type CommitType);
	UFUNCTION() void HandleIconClicked();
	UFUNCTION() void HandleWatcherClicked();

	void Refresh();
	bool IsLocalPlayer() const;
};

/** A team (or the unassigned list when TeamId = -1); players are dropped on it. */
UCLASS()
class HITMEN_API UHitmenTeamPanelWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby")
	void SetTeam(int32 InTeamId);

	/** Rebuilds member rows from the game state. */
	void Refresh();

protected:
	virtual void BuildDefaultLayout() override;
	virtual bool NativeOnDrop(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation) override;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> TitleText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> RoleButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> RoleText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> RemoveButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> MemberList;

	UFUNCTION() void HandleRoleClicked();
	UFUNCTION() void HandleRemoveClicked();

	int32 TeamId = INDEX_NONE;
};

/** Create Room + Customization, switched by the match phase. */
UCLASS()
class HITMEN_API UHitmenLobbyWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	// ---- Lobby actions ----
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void PreviousMap();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void NextMap();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void ToggleProximityChat();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void DecreaseRetrievalPoints();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void IncreaseRetrievalPoints();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void CycleDifficulty();
	/** Which: 0 = start interval, 1 = end interval, 2 = ramp duration. Direction: -1 / +1. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void AdjustSonarOption(int32 Which, int32 Direction);
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void CreateTeam();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void Back();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void Start();

	// ---- Customization actions ----
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void CycleCosmetic(EHitmenCosmeticSlot CosmeticSlot, int32 Direction);
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void RandomizeCosmetics();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void CycleWeapon(int32 Direction);
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void CycleAbility(EHitmenAbilitySlot AbilitySlot, int32 Direction);
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void ToggleReady();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void Launch();

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

	/** True while the local player types in a name box: lobby refreshes wait so the draft is kept. */
	bool IsEditingText() const;
	bool bLobbyRefreshPending = false;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UWidgetSwitcher> ScreenSwitcher;
	// Lobby
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> MapNameText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UImage> MapThumbnail;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> PrevMapButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> NextMapButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> OptionsBox;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> TeamsBox;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> CreateTeamButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> BackButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> StartButton;
	// Customization
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> CosmeticsBox;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> LoadoutBox;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UEditableTextBox> AgentNameEdit;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> ReadyButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> ReadyText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> LaunchButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UButton> CustomBackButton;

	UFUNCTION() void HandleLobbyChanged();
	UFUNCTION() void HandlePhaseChanged(EHitmenMatchPhase Phase);
	UFUNCTION() void HandleOptionClicked(int32 Index);
	UFUNCTION() void HandleCustomizationClicked(int32 Index);
	UFUNCTION() void HandleAgentNameCommitted(const FText& Text, ETextCommit::Type CommitType);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnScreenChanged(bool bCustomization);

	void RefreshLobby();
	void RefreshCustomization();
	void RebuildOptions();
	void RebuildTeams();
	void SendSettings(const FHitmenLobbySettings& Settings);
	TArray<int32> GetCarouselMaps() const;
	bool IsHost() const;
	UHitmenCatalog* GetCatalog() const;
	AHitmenPlayerState* GetLocalPlayerState() const;

	UPROPERTY(Transient)
	TArray<TObjectPtr<UHitmenTeamPanelWidget>> TeamPanels;

	/** Local edit copies pushed to the server on every change. */
	FHitmenCosmeticLoadout LocalCosmetics;
	FHitmenLoadout LocalLoadout;
	bool bLocalInitialized = false;
};
