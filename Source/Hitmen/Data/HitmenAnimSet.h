// Hitmen — every animation of a character in one asset, so ABPs swap animations without code

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HitmenAnimSet.generated.h"

class UAnimSequenceBase;
class UAnimMontage;
class UBlendSpace;
class UAimOffsetBlendSpace;

USTRUCT(BlueprintType)
struct FHitmenEmote
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Emote")
	FText DisplayName;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Emote")
	TSoftObjectPtr<UTexture2D> Icon;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Emote")
	TObjectPtr<UAnimMontage> Montage;
};

/**
 * Animation set read by UHitmenAnimInstance. ABP_ graphs reference these variables
 * (never hard-coded sequences), so changing an animation = changing this asset.
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenAnimSet : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	// ---- Locomotion ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UAnimSequenceBase> Idle;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UBlendSpace> Locomotion;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UAnimSequenceBase> CrouchIdle;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UBlendSpace> CrouchLocomotion;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UAnimSequenceBase> Slide;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UAnimSequenceBase> JumpStart;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UAnimSequenceBase> FallLoop;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UAnimSequenceBase> Land;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Locomotion")
	TObjectPtr<UAimOffsetBlendSpace> AimOffset;

	// ---- Traversal ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimSequenceBase> LadderClimb;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimSequenceBase> ZiplineHang;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> MantleMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> VaultMontage;

	// ---- Combat ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> PunchMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> ThrowMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> StealthKillAttackerMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> StealthKillVictimMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> HitReactMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TArray<TObjectPtr<UAnimMontage>> DeathMontages;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimSequenceBase> CarryBodyPose;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> InteractMontage;

	// ---- NPC ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|NPC")
	TObjectPtr<UAnimMontage> CallReinforcementsMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|NPC")
	TObjectPtr<UAnimMontage> SearchLookAroundMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|NPC")
	TObjectPtr<UAnimSequenceBase> StunnedLoop;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|NPC")
	TObjectPtr<UAnimSequenceBase> SleepLoop;

	// ---- Social ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Emotes")
	TArray<FHitmenEmote> Emotes;

	/** Idle variations for menus (title screen assassin). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Menu")
	TArray<TObjectPtr<UAnimSequenceBase>> MenuIdles;
};
