// Hitmen — lobby screens over the front-end scene: Create Room (map, options, players and teams
// with drag & drop) and Your Agent (the mannequin in the scene with look arrows beside it, name,
// loadout cards with a choice carousel, Ready)

#pragma once

#include "CoreMinimal.h"
#include "UI/HitmenUserWidget.h"
#include "Blueprint/DragDropOperation.h"
#include "Core/HitmenTypes.h"
#include "HitmenLobbyWidgets.generated.h"

class AHitmenPlayerState;
class AHitmenAgentPreview;
class UTextBlock;
class UVerticalBox;
class UEditableTextBox;
class UWidgetSwitcher;
class UCanvasPanel;
class UCanvasPanelSlot;
class UHitmenCatalog;
class UHitmenMenuButton;
class UHitmenArrowButton;
class UHitmenToggle;
class UHitmenCardTile;
class UHitmenChoiceCarousel;

UCLASS()
class HITMEN_API UHitmenPlayerDragOperation : public UDragDropOperation
{
	GENERATED_BODY()

public:
	UPROPERTY(BlueprintReadWrite, Category="Hitmen|Lobby")
	TObjectPtr<AHitmenPlayerState> PlayerState;
};

/**
 * One player: a full-width row (colour bar, name, YOU / HOST tags, Watcher check box or Ready
 * state). The host drags the whole row onto a team.
 */
UCLASS()
class HITMEN_API UHitmenPlayerEntryWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby")
	void SetPlayer(AHitmenPlayerState* InPlayerState);

	UFUNCTION(BlueprintPure, Category="Hitmen|Lobby")
	AHitmenPlayerState* GetPlayer() const { return PlayerState; }

	/** The drag visual (semi-transparent, no interaction). */
	void SetDragVisual(bool bInDragVisual) { bDragVisual = bInDragVisual; }

	/** Row colour (team colour, white without team). */
	void SetAccent(const FLinearColor& InAccent) { Accent = InAccent; }

	/** Customization phase: shows Ready instead of the Watcher box. */
	void SetShowReady(bool bInShowReady) { bShowReady = bInShowReady; }

protected:
	virtual void BuildDefaultLayout() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnDragDetected(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent, UDragDropOperation*& OutOperation) override;
	virtual void NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;

	UPROPERTY(Transient)
	TObjectPtr<AHitmenPlayerState> PlayerState;

	bool IsLocalPlayer() const;
	bool CanToggleWatcher() const;
	FBox2D GetWatcherBox(const FGeometry& Geometry) const;

	FLinearColor Accent = FLinearColor::White;
	bool bDragVisual = false;
	bool bShowReady = false;
	bool bHovered = false;
	float HoverAlpha = 0.f;
};

/** A team (or the unassigned list when TeamId = -1): header and member rows; drop players here. */
UCLASS()
class HITMEN_API UHitmenTeamPanelWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby")
	void SetTeam(int32 InTeamId);

	/** Rebuilds member rows from the game state. */
	void Refresh(bool bShowReady);

protected:
	virtual void BuildDefaultLayout() override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual bool NativeOnDrop(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation) override;
	virtual void NativeOnDragEnter(const FGeometry& InGeometry, const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation) override;
	virtual void NativeOnDragLeave(const FDragDropEvent& InDragDropEvent, UDragDropOperation* InOperation) override;

	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UTextBlock> TitleText;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenMenuButton> RoleButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UHitmenMenuButton> RemoveButton;
	UPROPERTY(meta=(BindWidgetOptional)) TObjectPtr<UVerticalBox> MemberList;

	UFUNCTION() void HandleRoleClicked();
	UFUNCTION() void HandleRemoveClicked();

	int32 TeamId = INDEX_NONE;
	FLinearColor TeamColor = FLinearColor::White;
	bool bDropHover = false;
};

/** Map preview of the room: the baked schematic (or thumbnail), name and description. */
UCLASS()
class HITMEN_API UHitmenMapTileWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	void SetMap(const class UHitmenMapDefinition* Map, int32 Position, int32 Count);

protected:
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;

	FSlateBrush MapBrush;
	bool bHasImage = false;
	FText MapName;
	FText Description;
	int32 MapPosition = 0;
	int32 MapCount = 0;
};

/** Create Room + Your Agent, switched by the match phase. */
UCLASS()
class HITMEN_API UHitmenLobbyWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	// ---- Lobby actions ----
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void PreviousMap();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void NextMap();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void SetProximityChat(bool bEnabled);
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void PreviousDifficulty();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void NextDifficulty();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void CreateTeam();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void Back();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void Start();

	// ---- Customization actions ----
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void CycleCosmetic(EHitmenCosmeticSlot CosmeticSlot, int32 Direction);
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void RandomizeCosmetics();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Lobby") void ToggleReady();

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void UnbindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

	/** True while the local player types in a name box: lobby refreshes wait so the draft is kept. */
	bool IsEditingText() const;
	bool bLobbyRefreshPending = false;

	UPROPERTY(Transient) TObjectPtr<UWidgetSwitcher> ScreenSwitcher;

	// Create Room
	UPROPERTY(Transient) TObjectPtr<UHitmenMapTileWidget> MapTile;
	UPROPERTY(Transient) TObjectPtr<UHitmenArrowButton> PrevMapButton;
	UPROPERTY(Transient) TObjectPtr<UHitmenArrowButton> NextMapButton;
	UPROPERTY(Transient) TObjectPtr<UHitmenToggle> ProximityToggle;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> DifficultyText;
	UPROPERTY(Transient) TObjectPtr<UHitmenArrowButton> PrevDifficultyButton;
	UPROPERTY(Transient) TObjectPtr<UHitmenArrowButton> NextDifficultyButton;
	UPROPERTY(Transient) TObjectPtr<UVerticalBox> TeamsBox;
	UPROPERTY(Transient) TObjectPtr<UHitmenMenuButton> CreateTeamButton;
	UPROPERTY(Transient) TObjectPtr<UHitmenMenuButton> StartButton;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> HostHintText;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> PlayerCountText;

	// Your Agent
	UPROPERTY(Transient) TObjectPtr<UCanvasPanel> AgentCanvas;
	UPROPERTY(Transient) TObjectPtr<UEditableTextBox> AgentNameEdit;
	UPROPERTY(Transient) TArray<TObjectPtr<UHitmenArrowButton>> LookArrows;
	UPROPERTY(Transient) TArray<TObjectPtr<UTextBlock>> LookLabels;
	UPROPERTY(Transient) TArray<TObjectPtr<UHitmenCardTile>> LoadoutCards;
	UPROPERTY(Transient) TObjectPtr<UHitmenChoiceCarousel> Carousel;
	UPROPERTY(Transient) TObjectPtr<UHitmenMenuButton> ReadyButton;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> ReadyStatusText;

	UFUNCTION() void HandleLobbyChanged();
	UFUNCTION() void HandlePhaseChanged(EHitmenMatchPhase Phase);
	UFUNCTION() void HandleAgentNameCommitted(const FText& Text, ETextCommit::Type CommitType);
	UFUNCTION() void HandleCardHovered(bool bHovered);
	UFUNCTION() void HandleCardClicked();
	UFUNCTION() void HandleCardWheel(int32 Direction);
	UFUNCTION() void HandleCarouselChosen(int32 Index);
	UFUNCTION() void HandleLookArrow0();
	UFUNCTION() void HandleLookArrow1();
	UFUNCTION() void HandleLookArrow2();
	UFUNCTION() void HandleLookArrow3();
	UFUNCTION() void HandleLookArrow4();
	UFUNCTION() void HandleLookArrow5();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnScreenChanged(bool bCustomization);

	void RefreshLobby();
	void RefreshCustomization();
	void RebuildTeams();
	void SendSettings(const FHitmenLobbySettings& Settings);
	void LookArrow(int32 ArrowIndex);
	void OpenCarousel(int32 CardIndex);
	void ApplyLoadoutChoice(int32 CardIndex, int32 ChoiceIndex);
	void UpdateAgentAnchors();
	TArray<FText> GetLoadoutChoices(int32 CardIndex, int32& OutSelected) const;
	AHitmenAgentPreview* FindAgentPreview() const;
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
	int32 CarouselCard = INDEX_NONE;
	float CarouselCloseTimer = 0.f;
	bool bPointerOnCards = false;
};
