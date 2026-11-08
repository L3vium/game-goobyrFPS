// Hitmen — every animation of a character in one asset, so ABPs swap animations without code

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HitmenAnimSet.generated.h"

class UAnimSequenceBase;
class UAnimMontage;
class UBlendSpace;
class UAimOffsetBlendSpace;

/** Montages of the set that gameplay code plays by name (replicated as this enum). */
UENUM(BlueprintType)
enum class EHitmenAnimMontage : uint8
{
	Mantle,
	Vault,
	Throw,
	HitReact,
	/** Variant = index in DeathMontages. */
	Death,
	Interact,
	Punch,
	CallReinforcements,
	SearchLookAround,
	ClimbEnter,
	ClimbExitTop,
	LockerEnter,
	LockerExit,
	ZiplineEnter,
	Pickpocket,
	ParachuteLand
};

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
 * Animation set read by UHitmenAnimInstance. ABP_ graphs reference the anim instance variables
 * resolved from this asset (never hard-coded sequences), so changing an animation = changing
 * this asset. Empty optional entries fall back (crouch -> stand, loops -> idle) in the instance.
 */
UCLASS(BlueprintType)
class HITMEN_API UHitmenAnimSet : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	/** Montage for a gameplay event (Variant picks among DeathMontages). Null when not set. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Anim")
	UAnimMontage* GetMontage(EHitmenAnimMontage Montage, int32 Variant = 0) const;

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

	/** Grabbing a ladder / gutter / pipe. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> ClimbEnterMontage;

	/** Pulling up over the top of a ladder / gutter / pipe. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> ClimbExitTopMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> ZiplineEnterMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> LockerEnterMontage;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> LockerExitMontage;

	/** Looping pose under the canopy during the match intro. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimSequenceBase> ParachuteLoop;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Traversal")
	TObjectPtr<UAnimMontage> ParachuteLandMontage;

	// ---- First person (arms mesh, ABP_FirstPersonArms) ----
	/** Arms pose with no weapon in hand (shown only while punching / acting). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|FirstPerson")
	TObjectPtr<UAnimSequenceBase> FirstPersonUnarmedPose;

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

	/**
	 * Death montages. With 4 entries the order is by where the killing hit came from:
	 * [Front, Back, Left, Right]; otherwise one is picked at random. Ragdoll follows.
	 */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TArray<TObjectPtr<UAnimMontage>> DeathMontages;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimSequenceBase> CarryBodyPose;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> InteractMontage;

	/** Lifting a keycard from an unaware NPC. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Combat")
	TObjectPtr<UAnimMontage> PickpocketMontage;

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
