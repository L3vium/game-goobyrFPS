// Hitmen — name tags

#include "UI/HitmenNameTagsWidget.h"
#include "Core/HitmenText.h"
#include "UI/HitmenUIEventSubsystem.h"
#include "UI/HitmenUIFactory.h"
#include "Framework/HitmenPlayerState.h"
#include "Core/HitmenGameSettings.h"
#include "Blueprint/WidgetLayoutLibrary.h"
#include "GameFramework/GameStateBase.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/SpectatorPawn.h"
#include "Engine/World.h"
#include "CollisionQueryParams.h"

namespace
{
	bool IsLayerShown(EHitmenWatcherVisibility Visibility, bool bToggle)
	{
		return Visibility == EHitmenWatcherVisibility::Shown || (Visibility == EHitmenWatcherVisibility::ShownToggleable && bToggle);
	}
}

int32 UHitmenNameTagsWidget::NativePaint(const FPaintArgs& Args, const FGeometry& AllottedGeometry, const FSlateRect& MyCullingRect, FSlateWindowElementList& OutDrawElements, int32 LayerId, const FWidgetStyle& InWidgetStyle, bool bParentEnabled) const
{
	APlayerController* PC = GetOwningPlayer();
	const UWorld* World = GetWorld();
	const AGameStateBase* GS = World ? World->GetGameState() : nullptr;
	const UHitmenUIEventSubsystem* UI = GetUIEvents();
	// Nothing over the end cinematic or the split screen pictures.
	if (!PC || !GS || (UI && (UI->IsCinematic() || UI->GetSpectatorViews().Num() > 0)))
	{
		return LayerId;
	}
	const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
	const APawn* MyPawn = PC->GetPawn();
	// Watching = no gameplay pawn (watchers and dead players).
	const bool bWatching = !MyPawn || MyPawn->IsA<ASpectatorPawn>();
	const AHitmenPlayerState* Self = PC->GetPlayerState<AHitmenPlayerState>();
	const bool bPlayers = bWatching ? IsLayerShown(Settings->WatcherSeePlayers, !UI || UI->AreWatcherPlayersShown()) : Settings->bPlayersSeeNameTags;
	const bool bWatchers = bWatching ? IsLayerShown(Settings->WatcherSeeWatchers, !UI || UI->AreWatcherWatchersShown()) : Settings->bPlayersSeeWatchers;
	FVector CameraLocation;
	FRotator CameraRotation;
	PC->GetPlayerViewPoint(CameraLocation, CameraRotation);
	const AActor* ViewTarget = PC->GetViewTarget();

	auto PaintTag = [&](const FVector& World, const FString& Name, const FString& Sub, const FLinearColor& Color)
	{
		FVector2D Screen;
		if (!UWidgetLayoutLibrary::ProjectWorldLocationToWidgetPosition(PC, World, Screen, false))
		{
			return;
		}
		const float Distance = FVector::Dist(CameraLocation, World);
		const int32 FontSize = FMath::RoundToInt(FMath::GetMappedRangeValueClamped(FVector2D(500.f, 5000.f), FVector2D(17.f, 12.f), Distance));
		const FSlateFontInfo Font = HitmenUI::SpacedFont(FontSize, TEXT("SemiBold"), 80);
		const FString Upper = Name.ToUpper();
		const FVector2D Size = HitmenUI::MeasureText(Upper, Font);
		const FVector2D TopLeft = Screen - FVector2D(Size.X * 0.5f, Size.Y + 6.f);
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, TopLeft - FVector2D(7.f, 3.f), Size + FVector2D(14.f, 6.f), FLinearColor(0.f, 0.f, 0.f, 0.45f));
		HitmenUI::PaintBox(OutDrawElements, LayerId + 1, AllottedGeometry, FVector2D(TopLeft.X - 7.f, TopLeft.Y + Size.Y + 2.f), FVector2D(Size.X + 14.f, 1.5f), Color);
		HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Upper, TopLeft, Font, Color);
		if (!Sub.IsEmpty())
		{
			const FSlateFontInfo SubFont = HitmenUI::SpacedFont(FMath::Max(10, FontSize - 4), TEXT("Medium"), 60);
			const FVector2D SubSize = HitmenUI::MeasureText(Sub, SubFont);
			HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Sub, FVector2D(Screen.X - SubSize.X * 0.5f, TopLeft.Y - SubSize.Y - 4.f), SubFont, Color * FLinearColor(1.f, 1.f, 1.f, 0.75f));
		}
	};

	int32 WatchedBy = 0;
	for (const APlayerState* Base : GS->PlayerArray)
	{
		const AHitmenPlayerState* PS = Cast<AHitmenPlayerState>(Base);
		if (!PS || PS == Self)
		{
			continue;
		}
		const FHitmenWatcherView& View = PS->GetWatcherView();
		if (View.bWatching && Self && View.Watched == Self)
		{
			++WatchedBy;
		}
		// Players: watchers see every living player; players only those in view, near enough.
		const APawn* Player = PS->GetPawn();
		if (bPlayers && Player && PS->IsAlive() && !Player->IsA<ASpectatorPawn>() && Player != ViewTarget)
		{
			const FVector Head = Player->GetActorLocation() + FVector(0.f, 0.f, Player->GetSimpleCollisionHalfHeight() + PlayerTagOffset);
			bool bShow = true;
			if (!bWatching)
			{
				FHitResult Hit;
				FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenNameTag), false, MyPawn);
				Params.AddIgnoredActor(Player);
				bShow = FVector::Dist(CameraLocation, Head) <= Settings->PlayerNameTagRange
					&& !World->LineTraceSingleByChannel(Hit, CameraLocation, Head, ECC_Visibility, Params);
			}
			if (bShow)
			{
				PaintTag(Head, PS->GetPlayerName(), FString(), PlayerTagColor);
			}
		}
		// Watchers flying free (one following a player in player view is with that player).
		if (bWatchers && View.bWatching && !View.Watched)
		{
			const FVector At = (PS->GetSmoothedWatcherLocation().IsZero() ? FVector(View.Location) : PS->GetSmoothedWatcherLocation()) + FVector(0.f, 0.f, WatcherTagOffset);
			PaintTag(At, PS->GetPlayerName(), HITMEN_TEXT("Tags.Watching", "WATCHING").ToString(), WatcherTagColor);
		}
	}
	// Being watched: top right, under the compass line.
	if (!bWatching && Settings->bPlayersSeeWatchedIndicator && WatchedBy > 0)
	{
		const FString Text = WatchedBy == 1 ? FString(TEXT("WATCHED BY 1 PLAYER")) : FString::Printf(TEXT("WATCHED BY %d PLAYERS"), WatchedBy);
		const FSlateFontInfo Font = HitmenUI::SpacedFont(14, TEXT("SemiBold"), 160);
		const FVector2D Size = HitmenUI::MeasureText(Text, Font);
		const FVector2D At(AllottedGeometry.GetLocalSize().X - Size.X - 36.f, 64.f);
		HitmenUI::PaintBox(OutDrawElements, LayerId, AllottedGeometry, At - FVector2D(26.f, 5.f), Size + FVector2D(36.f, 10.f), FLinearColor(0.f, 0.f, 0.f, 0.5f));
		HitmenUI::PaintDiamond(OutDrawElements, LayerId + 1, AllottedGeometry, At + FVector2D(-14.f, Size.Y * 0.5f), 5.f, WatcherTagColor);
		HitmenUI::PaintText(OutDrawElements, LayerId + 1, AllottedGeometry, Text, At, Font, WatcherTagColor);
	}
	return Super::NativePaint(Args, AllottedGeometry, MyCullingRect, OutDrawElements, LayerId + 2, InWidgetStyle, bParentEnabled);
}
