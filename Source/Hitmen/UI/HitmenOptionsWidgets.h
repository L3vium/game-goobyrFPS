// Hitmen — Options (title and pause): a centred panel with tabs (Gameplay, Controls, Graphics, Audio), one row per
// setting (label left, "< value >" or a slider right), a description column for the hovered row, and key hints
// at the bottom ([Esc] Back, [R] Defaults). Everything is painted so the look lives in one place.

#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "UI/HitmenUserWidget.h"
#include "HitmenOptionsWidgets.generated.h"

class UVerticalBox;
class UScrollBox;
class UTextBlock;
class USizeBox;

UENUM()
enum class EHitmenOptionRowKind : uint8
{
	/** Section title inside a tab (Controls categories). */
	Header,
	/** "< value >": click the left / right half (or the wheel) to step. */
	Choice,
	/** Track with a knob and the value as text: click or drag. */
	Slider,
	/** A rebindable key: click, then press a key or a mouse button (Escape cancels). */
	Key,
	/** A button on the right ("Run"). */
	Action
};

/** One options row, painted: hover lights it (red bar on the left), the right part edits the value. */
UCLASS()
class HITMEN_API UHitmenOptionRow : public UUserWidget
{
	GENERATED_BODY()

public:
	void SetupHeader(const FText& InLabel);
	void SetupChoice(const FText& InLabel, const FText& InDescription, const TArray<FText>& InChoices, int32 InIndex, TFunction<void(int32)> InOnChoice);
	void SetupSlider(const FText& InLabel, const FText& InDescription, float InMin, float InMax, float InStep, float InValue,
		TFunction<FText(float)> InFormat, TFunction<void(float)> InOnValue);
	void SetupKey(const FText& InLabel, const FText& InDescription, FName InMappingName, const FKey& InKey);
	void SetupAction(const FText& InLabel, const FText& InDescription, const FText& InButtonText, TFunction<void()> InOnAction);

	void SetChoiceIndex(int32 InIndex) { ChoiceIndex = InIndex; }
	void SetSliderValue(float InValue) { SliderValue = InValue; }
	void SetKey(const FKey& InKey) { CurrentKey = InKey; bListening = false; }
	/** Another row uses the same key: drawn red, the name of the other action in the description. */
	void SetConflict(const FText& InOtherAction) { ConflictWith = InOtherAction; }
	void SetActionText(const FText& InText) { ActionText = InText; }

	EHitmenOptionRowKind GetKind() const { return Kind; }
	const FText& GetLabel() const { return Label; }
	FText GetDescription() const;
	FName GetMappingName() const { return MappingName; }
	const FKey& GetKey() const { return CurrentKey; }
	bool IsListening() const { return bListening; }

	/** Hovered row (description column). */
	TFunction<void(UHitmenOptionRow*)> OnHovered;
	/** A key was pressed while listening (the options screen maps it and re-checks conflicts). */
	TFunction<void(UHitmenOptionRow*, const FKey&)> OnKeyChosen;

	static constexpr float RowHeight = 50.f;

protected:
	virtual void NativeOnInitialized() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnMouseButtonUp(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnMouseWheel(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnKeyDown(const FGeometry& InGeometry, const FKeyEvent& InKeyEvent) override;
	virtual void NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnFocusLost(const FFocusEvent& InFocusEvent) override;

	/** Width of the value area on the right. */
	float GetValueWidth() const { return 380.f; }
	void StepChoice(int32 Direction);
	void SetSliderFromLocalX(float LocalX, float RowWidth);
	void ChooseKey(const FKey& Key);

	EHitmenOptionRowKind Kind = EHitmenOptionRowKind::Choice;
	FText Label;
	FText Description;
	TArray<FText> Choices;
	int32 ChoiceIndex = 0;
	float SliderMin = 0.f;
	float SliderMax = 1.f;
	float SliderStep = 0.f;
	float SliderValue = 0.f;
	FName MappingName;
	FKey CurrentKey;
	FText ConflictWith;
	FText ActionText;
	TFunction<void(int32)> OnChoice;
	TFunction<void(float)> OnValue;
	TFunction<FText(float)> FormatValue;
	TFunction<void()> OnAction;

	bool bHovered = false;
	bool bDragging = false;
	bool bListening = false;
	float HoverAlpha = 0.f;
	/** -1 left arrow, +1 right arrow, 0 none (choice rows). */
	int32 HoveredArrow = 0;
};

/** Tabs across the top of the options panel; the selected one is underlined in red. */
UCLASS()
class HITMEN_API UHitmenTabBar : public UUserWidget
{
	GENERATED_BODY()

public:
	void SetTabs(const TArray<FText>& InTabs, int32 InSelected);
	void SetSelected(int32 InSelected) { Selected = InSelected; }
	TFunction<void(int32)> OnTabChosen;

protected:
	virtual void NativeOnInitialized() override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual FReply NativeOnMouseMove(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;

	int32 TabAt(const FGeometry& Geometry, const FVector2D& ScreenPosition) const;
	float GetTabWidth(int32 Tab) const;

	TArray<FText> Tabs;
	int32 Selected = 0;
	int32 Hovered = INDEX_NONE;
};

/** "[Esc] Back": a key cap and a label, clickable. */
UCLASS()
class HITMEN_API UHitmenKeyHint : public UUserWidget
{
	GENERATED_BODY()

public:
	void Setup(const FString& InKey, const FText& InLabel);
	TFunction<void()> OnClicked;

protected:
	virtual void NativeOnInitialized() override;
	virtual int32 NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const override;
	virtual FReply NativeOnMouseButtonDown(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseEnter(const FGeometry& InGeometry, const FPointerEvent& InMouseEvent) override;
	virtual void NativeOnMouseLeave(const FPointerEvent& InMouseEvent) override;

	FString Key;
	FText Label;
	bool bHovered = false;
};

UENUM()
enum class EHitmenOptionsTab : uint8
{
	Gameplay,
	Controls,
	Graphics,
	Audio
};

UCLASS()
class HITMEN_API UHitmenOptionsWidget : public UHitmenUserWidget
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category="Hitmen|Options") void Close();
	UFUNCTION(BlueprintCallable, Category="Hitmen|Options") void ResetToDefaults();
	/** 0 Gameplay, 1 Controls, 2 Graphics, 3 Audio. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Options") void SelectTab(int32 TabIndex);

	/** Width of the centred panel (it shrinks on small screens). */
	UPROPERTY(EditAnywhere, Category="Hitmen|Options")
	float PanelWidth = 1100.f;

	UPROPERTY(EditAnywhere, Category="Hitmen|Options")
	float PanelHeight = 780.f;

protected:
	virtual void BuildDefaultLayout() override;
	virtual void BindEvents() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	virtual FReply NativeOnKeyDown(const FGeometry& InGeometry, const FKeyEvent& InKeyEvent) override;

	void ShowTab(EHitmenOptionsTab Tab);
	void BuildGameplay();
	void BuildControls();
	void BuildGraphics();
	void BuildAudio();
	/** Graphics rows read back from the settings (after a preset or the benchmark changed several at once). */
	void RefreshCurrentTab() { ShowTab(CurrentTab); }
	void RefreshKeyConflicts();
	void HandleRowHovered(UHitmenOptionRow* Row);
	void HandleKeyChosen(UHitmenOptionRow* Row, const FKey& Key);

	UHitmenOptionRow* AddRow();
	void AddHeader(const FText& Text);

	UPROPERTY(Transient) TObjectPtr<UHitmenTabBar> TabBar;
	UPROPERTY(Transient) TObjectPtr<UScrollBox> RowScroll;
	UPROPERTY(Transient) TObjectPtr<UVerticalBox> RowBox;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> DescriptionTitle;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> DescriptionText;
	UPROPERTY(Transient) TObjectPtr<USizeBox> PanelBox;
	UPROPERTY(Transient) TArray<TObjectPtr<UHitmenOptionRow>> Rows;

	EHitmenOptionsTab CurrentTab = EHitmenOptionsTab::Gameplay;
	bool bWasVisible = false;
};
