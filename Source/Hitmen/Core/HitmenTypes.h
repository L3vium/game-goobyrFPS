// Hitmen — shared enums and structs used across gameplay, AI and UI

#pragma once

#include "CoreMinimal.h"
#include "HitmenTypes.generated.h"

class UHitmenWeaponData;
class UHitmenAbilityData;
class UHitmenCosmeticItem;

/** Role of a team in a match. */
UENUM(BlueprintType)
enum class EHitmenTeamRole : uint8
{
	/** Kill the VIP and deliver the body to a retrieval point. */
	Attack,
	/** Kill every player who is not in a Protect team. */
	Protect
};

/** What an NPC does while calm. */
UENUM(BlueprintType)
enum class EHitmenNPCBehavior : uint8
{
	/** Walks its patrol route (stands at its post if it has none). */
	Patrol,
	/** Stands at its post, facing the way it was placed; returns there after an alert. */
	Stationary
};

/** NPC awareness, mirrored on HUD, minimap and overhead indicators. */
UENUM(BlueprintType)
enum class EHitmenAlertState : uint8
{
	Unaware,
	/** Saw something, not alerted yet. */
	Suspicious,
	/** Alerted, investigating a point. */
	Investigating,
	/** Found the intruder, attacking. */
	Combat,
	/** Lost the intruder, searching likely places (shown yellow). */
	Searching
};

/** Colour group of an alert state (white / yellow / red). */
UENUM(BlueprintType)
enum class EHitmenAlertColor : uint8
{
	None,
	White,
	Yellow,
	Red
};

UENUM(BlueprintType)
enum class EHitmenMatchPhase : uint8
{
	Lobby,
	Customization,
	Playing,
	Ended
};

UENUM(BlueprintType)
enum class EHitmenDifficulty : uint8
{
	Easy,
	Normal,
	Hard
};

UENUM(BlueprintType)
enum class EHitmenAbilitySlot : uint8
{
	/** Movement ability (E). */
	Move,
	/** AI manipulation ability (A). */
	Manipulation
};

UENUM(BlueprintType)
enum class EHitmenCosmeticSlot : uint8
{
	/** Hat, cap, moustache, glasses... */
	Head,
	/** Jacket, vest, coat... */
	Body,
	/** Trousers, boots... */
	Legs
};

UENUM(BlueprintType)
enum class EHitmenFireMode : uint8
{
	Single,
	Burst,
	Automatic
};

/** What a noise means to an NPC that hears it. */
UENUM(BlueprintType)
enum class EHitmenNoiseType : uint8
{
	Footstep,
	Gunshot,
	Explosion,
	Distraction,
	Impact,
	Combat
};

/** Category used by the Sightroom to colour a scanned actor. */
UENUM(BlueprintType)
enum class EHitmenScanCategory : uint8
{
	None,
	Enemy,
	Interactable,
	Target
};

/** Status effects applicable to NPCs and players. */
UENUM(BlueprintType)
enum class EHitmenStatusEffect : uint8
{
	None,
	Stunned,
	Asleep,
	Charmed,
	Trapped,
	Poisoned,
	Blinded,
	Boosted,
	Invisible,
	Armored,
	Hidden,
	Concealed
};

/** A team in the lobby and in the match. */
USTRUCT(BlueprintType)
struct FHitmenTeam
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Team")
	int32 TeamId = INDEX_NONE;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Team")
	FText DisplayName;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Team")
	EHitmenTeamRole Role = EHitmenTeamRole::Attack;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Team")
	FLinearColor Color = FLinearColor::White;

	bool IsValid() const { return TeamId != INDEX_NONE; }
};

/** The assassin's visual customization (one item per slot, may be null). */
USTRUCT(BlueprintType)
struct FHitmenCosmeticLoadout
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Cosmetics")
	TObjectPtr<UHitmenCosmeticItem> Head = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Cosmetics")
	TObjectPtr<UHitmenCosmeticItem> Body = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Cosmetics")
	TObjectPtr<UHitmenCosmeticItem> Legs = nullptr;

	UHitmenCosmeticItem* GetSlot(EHitmenCosmeticSlot Slot) const;
	void SetSlot(EHitmenCosmeticSlot Slot, UHitmenCosmeticItem* Item);
};

/** The gear a player starts the match with. */
USTRUCT(BlueprintType)
struct FHitmenLoadout
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Loadout")
	TObjectPtr<UHitmenWeaponData> Weapon = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Loadout")
	TObjectPtr<UHitmenAbilityData> MoveAbility = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Loadout")
	TObjectPtr<UHitmenAbilityData> ManipulationAbility = nullptr;
};

/** An integer match option with a designer flag to hide it from the lobby UI. */
USTRUCT(BlueprintType)
struct FHitmenIntOption
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	int32 Value = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	int32 Min = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	int32 Max = 10;

	/** When false the value is fixed by design and the lobby hides it. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	bool bShowInLobbyUI = true;
};

USTRUCT(BlueprintType)
struct FHitmenBoolOption
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	bool Value = false;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	bool bShowInLobbyUI = true;
};

USTRUCT(BlueprintType)
struct FHitmenFloatOption
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	float Value = 0.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	float Min = 0.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	float Max = 1.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Option")
	bool bShowInLobbyUI = true;
};

/** Settings chosen in the lobby and carried to the match through travel. */
USTRUCT(BlueprintType)
struct FHitmenLobbySettings
{
	GENERATED_BODY()

	/** Index in the map catalog (UHitmenGameSettings::Maps). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Lobby")
	int32 MapIndex = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Lobby")
	bool bProximityChat = true;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Lobby")
	int32 RetrievalPointCount = 3;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Lobby")
	EHitmenDifficulty Difficulty = EHitmenDifficulty::Normal;

	/** Seconds between sonar pings of the carried VIP body when carrying starts. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Lobby")
	float SonarStartInterval = 12.f;

	/** Seconds between pings once the ramp is over. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Lobby")
	float SonarEndInterval = 2.f;

	/** Seconds of carrying to go from the start to the end interval. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Hitmen|Lobby")
	float SonarRampDuration = 90.f;
};

/** Result of a finished match. */
USTRUCT(BlueprintType)
struct FHitmenMatchResult
{
	GENERATED_BODY()

	/** Winning team, or INDEX_NONE when a solo player won (see WinningPlayerName). */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Match")
	int32 WinningTeamId = INDEX_NONE;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Match")
	FString WinningPlayerName;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Match")
	FText Reason;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Match")
	bool bIsDraw = false;
};

/** A sonar ping of the VIP body position. */
USTRUCT(BlueprintType)
struct FHitmenSonarPing
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Sonar")
	FVector Location = FVector::ZeroVector;

	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Sonar")
	float ServerTime = -1.f;

	/** Seconds until the next ping, so the UI can animate the blink. */
	UPROPERTY(BlueprintReadOnly, Category="Hitmen|Sonar")
	float NextInterval = 0.f;

	bool IsValid() const { return ServerTime >= 0.f; }
};

namespace HitmenAlert
{
	HITMEN_API EHitmenAlertColor ToColor(EHitmenAlertState State);
	HITMEN_API FLinearColor ToLinearColor(EHitmenAlertColor Color);
}
