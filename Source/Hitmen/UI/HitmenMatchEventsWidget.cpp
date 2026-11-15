// Hitmen — match events widget

#include "UI/HitmenMatchEventsWidget.h"
#include "UI/HitmenUIFactory.h"
#include "Blueprint/WidgetTree.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Image.h"
#include "Components/Border.h"
#include "Engine/TextureRenderTarget2D.h"
#include "Engine/World.h"

void UHitmenMatchEventsWidget::BuildDefaultLayout()
{
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;
	// Event camera: a thin white frame around the picture, top left (under where the title is painted).
	EventFrame = HitmenUI::MakePanel(WidgetTree, FLinearColor(0.95f, 0.95f, 0.95f, 0.9f), FMargin(2.f));
	EventImage = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
	EventFrame->SetContent(EventImage);
	if (UCanvasPanelSlot* FrameSlot = Canvas->AddChildToCanvas(EventFrame))
	{
		FrameSlot->SetAnchors(FAnchors(0.f, 0.f));
		FrameSlot->SetPosition(EventCameraOffset);
		FrameSlot->SetSize(EventCameraSize + FVector2D(4.f));
	}
	EventFrame->SetVisibility(ESlateVisibility::Collapsed);
	SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UHitmenMatchEventsWidget::BindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnToast.AddUniqueDynamic(this, &UHitmenMatchEventsWidget::HandleToast);
	}
}

void UHitmenMatchEventsWidget::UnbindEvents()
{
	if (UHitmenUIEventSubsystem* UI = GetUIEvents())
	{
		UI->OnToast.RemoveAll(this);
	}
}

void UHitmenMatchEventsWidget::HandleToast(const FHitmenToast& Toast)
{
	CurrentToast = Toast;
	ToastStart = GetWorld() ? GetWorld()->GetRealTimeSeconds() : 0.f;
}

void UHitmenMatchEventsWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const UHitmenUIEventSubsystem* UI = GetUIEvents();
	const UWorld* World = GetWorld();
	if (!UI || !World || !EventImage || !EventFrame)
	{
		return;
	}
	// Event camera: shown while its time runs, faded in and out; the picture follows the render target.
	UTextureRenderTarget2D* Target = UI->GetEventCameraTarget();
	const bool bEvent = Target && World->GetTimeSeconds() < UI->GetEventCameraEnd() && !UI->IsCinematic();
	EventAlpha = FMath::FInterpConstantTo(EventAlpha, bEvent ? 1.f : 0.f, InDeltaTime, 5.f);
	if (Target && Target != ShownTarget)
	{
		ShownTarget = Target;
		FSlateBrush Brush;
		Brush.SetResourceObject(Target);
		Brush.ImageSize = EventCameraSize;
		EventImage->SetBrush(Brush);
	}
	EventTitle = UI->GetEventCameraTitle().ToString().ToUpper();
	EventFrame->SetRenderOpacity(EventAlpha);
	EventFrame->SetVisibility(EventAlpha > 0.01f ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
	LetterboxAlpha = FMath::FInterpConstantTo(LetterboxAlpha, UI->IsCinematic() ? 1.f : 0.f, InDeltaTime, 2.f);
}

int32 UHitmenMatchEventsWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	const int32 Layer = Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId, InWidgetStyle, bParentEnabled);
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	const UWorld* World = GetWorld();

	// Letterbox: black bars slide in during the end cinematic.
	if (LetterboxAlpha > 0.f)
	{
		const float Bar = Size.Y * LetterboxHeight * FMath::InterpEaseOut(0.f, 1.f, LetterboxAlpha, 2.f);
		HitmenUI::PaintBox(OutDrawElements, Layer + 1, AllottedGeometry, FVector2D::ZeroVector, FVector2D(Size.X, Bar), FLinearColor::Black);
		HitmenUI::PaintBox(OutDrawElements, Layer + 1, AllottedGeometry, FVector2D(0.f, Size.Y - Bar), FVector2D(Size.X, Bar), FLinearColor::Black);
	}

	// Event camera title above the picture.
	if (EventAlpha > 0.01f && !EventTitle.IsEmpty())
	{
		const FSlateFontInfo TitleFont = HitmenUI::SpacedFont(15, TEXT("SemiBold"), 160);
		HitmenUI::PaintBox(OutDrawElements, Layer + 1, AllottedGeometry, EventCameraOffset - FVector2D(0.f, 26.f), FVector2D(EventCameraSize.X + 4.f, 24.f), FLinearColor(0.f, 0.f, 0.f, 0.6f * EventAlpha));
		HitmenUI::PaintText(OutDrawElements, Layer + 2, AllottedGeometry, EventTitle, EventCameraOffset + FVector2D(10.f, -23.f), TitleFont, FLinearColor(1.f, 1.f, 1.f, EventAlpha));
	}

	// Toast: a big title under the compass, fading in and out.
	if (World && !CurrentToast.Title.IsEmpty())
	{
		const float Age = World->GetRealTimeSeconds() - ToastStart;
		const float Alpha = FMath::Clamp(FMath::Min(Age / ToastFade, (CurrentToast.Duration - Age) / ToastFade), 0.f, 1.f);
		if (Alpha > 0.f)
		{
			const FSlateFontInfo TitleFont = HitmenUI::SpacedFont(38, TEXT("Bold"), 220);
			const FSlateFontInfo SubFont = HitmenUI::SpacedFont(17, TEXT("Medium"), 60);
			const FString Title = CurrentToast.Title.ToString().ToUpper();
			const FString Sub = CurrentToast.Subtitle.ToString();
			const FVector2D TitleSize = HitmenUI::MeasureText(Title, TitleFont);
			const FVector2D SubSize = Sub.IsEmpty() ? FVector2D::ZeroVector : HitmenUI::MeasureText(Sub, SubFont);
			const float Y = 96.f + (1.f - Alpha) * -10.f;
			const float LineWidth = FMath::Max(TitleSize.X, SubSize.X) + 80.f;
			HitmenUI::PaintGradient(OutDrawElements, Layer + 1, AllottedGeometry, FVector2D(Size.X * 0.5f - LineWidth * 0.5f, Y - 10.f), FVector2D(LineWidth * 0.5f, TitleSize.Y + SubSize.Y + 26.f), FLinearColor(0, 0, 0, 0), FLinearColor(0, 0, 0, 0.55f * Alpha), true);
			HitmenUI::PaintGradient(OutDrawElements, Layer + 1, AllottedGeometry, FVector2D(Size.X * 0.5f, Y - 10.f), FVector2D(LineWidth * 0.5f, TitleSize.Y + SubSize.Y + 26.f), FLinearColor(0, 0, 0, 0.55f * Alpha), FLinearColor(0, 0, 0, 0), true);
			HitmenUI::PaintText(OutDrawElements, Layer + 2, AllottedGeometry, Title, FVector2D((Size.X - TitleSize.X) * 0.5f, Y), TitleFont, CurrentToast.Color * FLinearColor(1.f, 1.f, 1.f, Alpha));
			HitmenUI::PaintBox(OutDrawElements, Layer + 2, AllottedGeometry, FVector2D(Size.X * 0.5f - TitleSize.X * 0.3f, Y + TitleSize.Y + 3.f), FVector2D(TitleSize.X * 0.6f, 2.f), CurrentToast.Color * FLinearColor(1.f, 1.f, 1.f, 0.8f * Alpha));
			if (!Sub.IsEmpty())
			{
				HitmenUI::PaintText(OutDrawElements, Layer + 2, AllottedGeometry, Sub, FVector2D((Size.X - SubSize.X) * 0.5f, Y + TitleSize.Y + 10.f), SubFont, FLinearColor(0.92f, 0.92f, 0.92f, Alpha));
			}
		}
	}
	return Layer + 3;
}
