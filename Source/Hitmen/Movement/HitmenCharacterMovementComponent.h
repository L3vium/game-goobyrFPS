// Hitmen — predicted movement: sprint, crouch-slide, ledge grab / vault, ladders, ziplines, jetpack

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Data/HitmenAnimSet.h"
#include "HitmenCharacterMovementComponent.generated.h"

class IHitmenClimbable;

UENUM(BlueprintType)
enum class EHitmenCustomMovement : uint8
{
	None,
	Slide,
	Climb,
	Mantle
};

/** What Space would do in front of the character (shown at the crosshair). */
UENUM(BlueprintType)
enum class EHitmenTraversalAction : uint8
{
	None,
	/** Low obstacle (up to VaultMaxHeight): over it in one motion. */
	Vault,
	/** Ledge (up to MantleMaxHeight) with room on top: climb and stand on it. */
	Climb,
	/** Thin wall too high to vault: climb up and drop on the far side. */
	ClimbOver
};

/** A planned ledge grab / vault: cubic path from Start to Target. */
struct FHitmenMantlePlan
{
	EHitmenTraversalAction Action = EHitmenTraversalAction::None;
	FVector Start = FVector::ZeroVector;
	FVector ControlA = FVector::ZeroVector;
	FVector ControlB = FVector::ZeroVector;
	FVector Target = FVector::ZeroVector;
	float Duration = 0.4f;
	float LedgeHeight = 0.f;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenTraversal, bool, bIsVault);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenTraversalMontage, EHitmenAnimMontage, Montage);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenMovementStateChanged, EHitmenCustomMovement, NewState);

/**
 * Custom modes run on both the owning client and the server from replicated inputs
 * (acceleration, crouch, jump, sprint flag), so they are client-predicted.
 */
UCLASS()
class HITMEN_API UHitmenCharacterMovementComponent : public UCharacterMovementComponent
{
	GENERATED_BODY()

	friend class FSavedMove_Hitmen;

public:
	UHitmenCharacterMovementComponent();

	// ---- Events (cosmetic: anim, juice) ----
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Movement")
	FOnHitmenTraversal OnMantleStarted;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Movement")
	FOnHitmenMovementStateChanged OnCustomMovementChanged;

	/** Owning client + server: climb enter, ladder top exit, zipline grab (anim-set montage). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Movement")
	FOnHitmenTraversalMontage OnTraversalMontage;

	// ---- Speeds ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Speed", meta=(ClampMin="0", Units="cm/s"))
	float WalkSpeed = 480.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Speed", meta=(ClampMin="0", Units="cm/s"))
	float SprintSpeed = 820.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Speed", meta=(ClampMin="0", ClampMax="1"))
	float AimSpeedMultiplier = 0.65f;

	/** Sprint only counts when moving mostly forward. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Speed", meta=(ClampMin="-1", ClampMax="1"))
	float SprintMinForwardDot = 0.5f;

	// ---- Slide ----
	/** Crouching while moving at least this fast starts a slide (keep above WalkSpeed). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0", Units="cm/s"))
	float SlideMinStartSpeed = 560.f;

	/** Crouch-walking never slides: only a sprint (or a fall) does. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide")
	bool bSlideRequiresSprint = true;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0", Units="cm/s"))
	float SlideImpulse = 250.f;

	/** Impulse when the slide starts from a sprint (slides further). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0", Units="cm/s"))
	float SprintSlideImpulse = 550.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0"))
	float SlideFriction = 0.4f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0"))
	float SlideBrakingDeceleration = 500.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0", Units="cm/s"))
	float SlideExitSpeed = 220.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0", Units="s"))
	float SlideMaxDuration = 1.4f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Slide", meta=(ClampMin="0", Units="s"))
	float SlideCooldown = 0.4f;

	// ---- Ledge grab / vault ----
	/** Max height above the feet of a ledge you can climb onto. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="cm"))
	float MantleMaxHeight = 250.f;

	/** Obstacles up to this height can be vaulted over instead of climbed. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="cm"))
	float VaultMaxHeight = 120.f;

	/** Max thickness of a vaultable obstacle (window sills, low walls, crates). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="cm"))
	float VaultMaxDepth = 90.f;

	/** Ignore steps lower than this (normal step-up handles them: MaxStepHeight is 55 cm). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="cm"))
	float MantleMinHeight = 56.f;

	/** A wall top at least this deep is a block you climb onto (and walk along); thinner walls are climbed over. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="cm"))
	float MinStandDepth = 40.f;

	/** Space counts this long after the press (grab a ledge or a ladder slightly late). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="s"))
	float JumpInputBuffer = 0.25f;

	/** How far in front of the capsule a wall is searched. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="cm"))
	float MantleReach = 60.f;

	/** Probe depths past the wall face; several so thin walls (30 cm) and sills are found. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle")
	TArray<float> MantleProbeDepths = { 8.f, 20.f, 35.f, 55.f };

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0.05", Units="s"))
	float MantleDuration = 0.45f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0.05", Units="s"))
	float VaultDuration = 0.4f;

	/** Climbing over a thin wall (up, over, down on the far side). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0.05", Units="s"))
	float ClimbOverDuration = 0.75f;

	/** Clearance kept above a ledge / wall top while passing over it. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="cm"))
	float MantleClearance = 18.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle")
	bool bAllowAirMantle = true;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Mantle", meta=(ClampMin="0", Units="s"))
	float MantleCooldown = 0.35f;

	// ---- Climb ----
	/** Push away from a ladder when jumping off. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Climb", meta=(ClampMin="0"))
	float ClimbJumpOffSpeed = 350.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Climb", meta=(ClampMin="0", Units="s"))
	float ClimbReattachCooldown = 0.5f;

	/** Zipline: capsule hangs this far below the cable. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Climb", meta=(ClampMin="0", Units="cm"))
	float ZiplineHangOffset = 110.f;

	/** Pull-up over the top of a ladder / gutter / pipe (no snap). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Climb", meta=(ClampMin="0.05", Units="s"))
	float LadderExitDuration = 0.55f;

	/** Zipline rides start slow and accelerate to the cable speed over this time. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Movement|Climb", meta=(ClampMin="0", Units="s"))
	float ZiplineAccelerationTime = 0.5f;

	// ---- State API ----
	UFUNCTION(BlueprintCallable, Category="Hitmen|Movement")
	void SetSprinting(bool bSprint) { bWantsToSprint = bSprint; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	bool IsSprinting() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	bool IsCustomMode(EHitmenCustomMovement Mode) const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	EHitmenCustomMovement GetCustomMovement() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	bool IsSliding() const { return IsCustomMode(EHitmenCustomMovement::Slide); }

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	bool IsClimbing() const { return IsCustomMode(EHitmenCustomMovement::Climb); }

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	bool IsMantling() const { return IsCustomMode(EHitmenCustomMovement::Mantle); }

	/** Climb axis speed for the animation (-1 down .. 1 up). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	float GetClimbAxisInput() const { return ClimbAxisInput; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	AActor* GetClimbTarget() const { return ClimbTarget.Get(); }

	/** Server-side flag mirroring the owning player's "climb relative to camera" option. */
	void SetClimbRelativeToCamera(bool bRelative) { bClimbRelativeToCamera = bRelative; }

	/** Forces leaving a climb (teleports, deaths, abilities). */
	void StopClimbing(bool bJumpOff);

	/** What Space would do right now in front of the character (crosshair hint). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	EHitmenTraversalAction QueryTraversalAction() const;

	/** 0..1 progress of the current ledge grab / vault (0 when not mantling). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	float GetMantleAlpha() const;

	/**
	 * Owning client and server (F on a zipline): rides the cable to its end on its own.
	 * Direction +1 = toward the cable end, -1 = toward its start.
	 */
	void StartZiplineRide(AActor* ZiplineActor, float Direction);

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement")
	bool IsZiplining() const;

	// ---- Jetpack (client-predicted: the thrust wish travels in the saved move flags) ----
	/**
	 * Enables the jetpack with the equipping ability's tunables (server and every client).
	 * Disabled: thrust is ignored and fuel stops simulating.
	 */
	void ConfigureJetpack(bool bEnabled, float InMaxFuel, float InFuelRechargePerSecond, float InLiftSpeed, float InLiftInterpSpeed);

	/** Owning client (and listen-server host): hold / release the thrust. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Movement|Jetpack")
	void SetJetpackThrust(bool bThrust) { bWantsJetpackThrust = bThrust; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement|Jetpack")
	bool IsJetpackEnabled() const { return bJetpackEnabled; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement|Jetpack")
	bool WantsJetpackThrust() const { return bWantsJetpackThrust; }

	/** Thrust actually applied this move (held, enabled, fuel left, not climbing / mantling). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Movement|Jetpack")
	bool IsJetpackThrusting() const;

	/** Predicted fuel in seconds of thrust (authoritative on the server). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Movement|Jetpack")
	float GetJetpackFuel() const { return JetpackFuel; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Movement|Jetpack")
	float GetJetpackFuelNormalized() const { return JetpackMaxFuel > 0.f ? JetpackFuel / JetpackMaxFuel : 0.f; }

	/** Owning client: adopt the server's fuel when the prediction drifted. */
	void ReconcileJetpackFuel(float ServerFuel, float Tolerance);

	// UCharacterMovementComponent
	virtual float GetMaxSpeed() const override;
	virtual float GetMaxBrakingDeceleration() const override;
	virtual bool IsMovingOnGround() const override;
	virtual bool CanAttemptJump() const override;
	virtual void UpdateCharacterStateBeforeMovement(float DeltaSeconds) override;
	virtual void UpdateFromCompressedFlags(uint8 Flags) override;
	virtual class FNetworkPredictionData_Client* GetPredictionData_Client() const override;
	virtual void OnMovementModeChanged(EMovementMode PreviousMovementMode, uint8 PreviousCustomMode) override;

protected:
	virtual void PhysCustom(float DeltaTime, int32 Iterations) override;

	void PhysSlide(float DeltaTime, int32 Iterations);
	void PhysClimb(float DeltaTime, int32 Iterations);
	void PhysMantle(float DeltaTime, int32 Iterations);

	bool CanStartSlide() const;
	void StartSlide();
	void EndSlide();
	bool GetSlideSurface(FHitResult& OutHit) const;

	bool TryStartMantle();
	/** Computes the ledge grab / vault in front (Forward = move intent). Const: also used for the hint. */
	bool PlanMantle(const FVector& Forward, FHitmenMantlePlan& OutPlan) const;
	void StartMantlePlan(const FHitmenMantlePlan& Plan);
	void StartLadderExit(const IHitmenClimbable& Climbable);
	void FinishMantle();

	/** Thrust and fuel step, run per move on the owning client and the server. */
	void UpdateJetpack(float DeltaSeconds);

	AActor* FindClimbableActor() const;
	bool ShouldGrabClimbable(const IHitmenClimbable& Climbable, bool& bOutFromTop) const;
	void StartClimb(AActor* ClimbableActor, bool bFromTop);
	float ProjectOnClimbAxis(const FVector& Location, const IHitmenClimbable& Climbable) const;
	FVector GetClimbLocation(float Alpha, const IHitmenClimbable& Climbable) const;

	/** Owner-provided multiplier (carry, status effects, aim). */
	float GetOwnerSpeedMultiplier() const;
	float GetTimeSeconds() const;

	bool bWantsToSprint = false;
	bool bClimbRelativeToCamera = false;

	// Jetpack state (tunables come from the equipped ability)
	bool bWantsJetpackThrust = false;
	bool bJetpackEnabled = false;
	float JetpackFuel = 0.f;
	float JetpackMaxFuel = 0.f;
	float JetpackFuelRechargePerSecond = 0.f;
	float JetpackLiftSpeed = 0.f;
	float JetpackLiftInterpSpeed = 0.f;

	// Slide state
	float SlideStartTime = -100.f;
	float LastSlideEndTime = -100.f;

	// Mantle state
	FVector MantleStart = FVector::ZeroVector;
	FVector MantleControlA = FVector::ZeroVector;
	FVector MantleControlB = FVector::ZeroVector;
	FVector MantleTarget = FVector::ZeroVector;
	EHitmenTraversalAction MantleAction = EHitmenTraversalAction::None;
	bool bMantleFromClimb = false;
	float MantleElapsed = 0.f;
	float MantleTotal = 0.f;
	bool bMantleIsVault = false;
	float LastMantleTime = -100.f;

	// Climb state
	TWeakObjectPtr<AActor> ClimbTarget;
	float ClimbAlpha = 0.f;
	float ClimbAxisInput = 0.f;
	float LastClimbExitTime = -100.f;
	/** The ladder just left: only it waits for the reattach cooldown. */
	TWeakObjectPtr<AActor> LastClimbExitActor;
	float LastJumpPressTime = -100.f;
	bool bPrevPressedJump = false;

	// Zipline ride state (auto ride toward ZiplineDirection)
	float ZiplineDirection = 1.f;
	float ZiplineRideTime = 0.f;
};

/** Saved move carrying the sprint and jetpack flags (and the fuel at move start) for prediction / replay. */
class FSavedMove_Hitmen : public FSavedMove_Character
{
public:
	typedef FSavedMove_Character Super;

	uint8 bSavedWantsToSprint : 1;
	uint8 bSavedWantsJetpackThrust : 1;
	float SavedJetpackFuel = 0.f;

	virtual void Clear() override;
	virtual uint8 GetCompressedFlags() const override;
	virtual bool CanCombineWith(const FSavedMovePtr& NewMove, ACharacter* InCharacter, float MaxDelta) const override;
	virtual void CombineWith(const FSavedMove_Character* OldMove, ACharacter* InCharacter, APlayerController* PC, const FVector& OldStartLocation) override;
	virtual void SetMoveFor(ACharacter* C, float InDeltaTime, FVector const& NewAccel, FNetworkPredictionData_Client_Character& ClientData) override;
	virtual void PrepMoveFor(ACharacter* C) override;
};

class FNetworkPredictionData_Client_Hitmen : public FNetworkPredictionData_Client_Character
{
public:
	typedef FNetworkPredictionData_Client_Character Super;

	explicit FNetworkPredictionData_Client_Hitmen(const UCharacterMovementComponent& ClientMovement)
		: Super(ClientMovement)
	{
	}

	virtual FSavedMovePtr AllocateNewMove() override;
};
