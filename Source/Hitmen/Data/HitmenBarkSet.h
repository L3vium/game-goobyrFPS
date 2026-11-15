// Hitmen — NPC barks: what a guard says when its awareness changes (Hitman-style subtitles, optional voices)

#pragma once

#include "CoreMinimal.h"
#include "Core/HitmenText.h"
#include "Engine/DataAsset.h"
#include "Core/HitmenTypes.h"
#include "HitmenBarkSet.generated.h"

class USoundBase;

/** The lines for one reason; one is picked at random. */
USTRUCT(BlueprintType)
struct FHitmenBarkLines
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks")
	TArray<FText> Lines;

	/** Optional voice per line (same index as Lines); none = subtitle only. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks")
	TArray<TObjectPtr<USoundBase>> Voices;
};

/**
 * DA_Barks (Project Settings -> Hitmen -> AI -> Bark Set). Every machine derives the bark from the replicated alert
 * state and reason, so barks cost no network traffic. Only local players within HearingDistance get the subtitle.
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenBarkSet : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks")
	TMap<EHitmenAlertReason, FHitmenBarkLines> Lines;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks")
	FText SpeakerName = HITMEN_TEXT("Hitmen.BarkSpeakerGuard", "Guard");

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks")
	FText VIPSpeakerName = HITMEN_TEXT("Hitmen.BarkSpeakerVIP", "Target");

	/** Speaker colour when the NPC is calm again (all clear); otherwise its alert colour. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks")
	FLinearColor CalmColor = FLinearColor(0.75f, 0.85f, 0.9f);

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks", meta=(ClampMin="0", Units="cm"))
	float HearingDistance = 1500.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Barks", meta=(ClampMin="0.5", Units="s"))
	float SubtitleDuration = 3.f;
};
