// Hitmen — the assassin: first-person body, gameplay components, input receiver

#pragma once

#include "CoreMinimal.h"
#include "Characters/HitmenCharacterBase.h"
#include "Core/HitmenInterfaces.h"
#include "HitmenPlayerCharacter.generated.h"

class UCameraComponent;
class USceneCaptureComponent2D;
class UHitmenInventoryComponent;
class UHitmenAbilityComponent;
class UHitmenInteractionComponent;
class UHitmenMeleeComponent;
class UHitmenBodyCarryComponent;
class UHitmenSightroomComponent;
class UHitmenEmoteComponent;
class AAIController;
class UVOIPTalker;
class USoundAttenuation;

/** Procedural first-person actions of the arms (no body montage plays on the first-person arms). */
UENUM(BlueprintType)
enum class EHitmenFPAction : uint8
{
	None,
	Punch,
	KnifeSlash,
	Throw,
	Takedown
};

UCLASS(Blueprintable)
class HITMEN_API AHitmenPlayerCharacter : public AHitmenCharacterBase, public IHitmenInputReceiver
{
	GENERATED_BODY()

public:
	AHitmenPlayerCharacter(const FObjectInitializer& ObjectInitializer);

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void Tick(float DeltaSeconds) override;
	virtual void PossessedBy(AController* NewController) override;
	/** Local settings (FOV, ladder direction) bind here: a respawned pawn is possessed after BeginPlay. */
	virtual void NotifyControllerChanged() override;
	virtual void OnStartCrouch(float HalfHeightAdjust, float ScaledHalfHeightAdjust) override;
	virtual void OnEndCrouch(float HalfHeightAdjust, float ScaledHalfHeightAdjust) override;

	// ---- Loadout ----
	/** Server: starting weapon + abilities. */
	void ApplyLoadout(const struct FHitmenLoadout& Loadout);

	/** Server: cosmetics replicate through the cosmetic component. */
	void ApplyCosmetics(const struct FHitmenCosmeticLoadout& Cosmetics);

	// ---- State ----
	virtual bool IsAiming() const override { return bIsAiming; }
	virtual float GetMovementSpeedMultiplier() const override;
	virtual int32 GetHitmenTeamId_Implementation() const override;

	/** Where held weapons attach: first-person arms for the local player, body for others. */
	USceneComponent* GetWeaponAttachComponent() const;

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	UCameraComponent* GetFirstPersonCamera() const { return FirstPersonCamera; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	USceneCaptureComponent2D* GetSpectatorCapture() const { return SpectatorCapture; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	USkeletalMeshComponent* GetFirstPersonMesh() const { return FirstPersonMesh; }

	/**
	 * Local: camera kick that recovers smoothly (degrees; positive pitch = up). The view really
	 * moves (aim follows it) and comes back down unless the player compensates.
	 */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Camera")
	void AddRecoil(float PitchDegrees, float YawDegrees);

	/** Local: short view shake (explosions, landings). Intensity in degrees, fades over Duration. */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Camera")
	void AddCameraShake(float Intensity, float Duration);

	/** Server: starts the intro drop (no control, slow fall under a canopy until landing). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Intro")
	void StartParachute();

	UFUNCTION(BlueprintPure, Category="Hitmen|Intro")
	bool IsParachuting() const { return bParachuting; }

	/** Parachuting, or just landed: guards and cameras cannot see you yet. */
	bool IsIntroProtected() const;

	virtual void Landed(const FHitResult& Hit) override;

	/** Local: shows the first-person arms for a moment (punch, throw, interact). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Camera")
	void ShowFirstPersonArms(float Duration);

	/** Local: plays a procedural first-person arms action (punch, knife slash, throw, takedown). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Camera")
	void PlayFirstPersonAction(EHitmenFPAction Action, float Duration);

	/** First-person rendering scale (cosmetics drawn next to the first-person weapon use it). */
	float GetFirstPersonViewScale() const { return FirstPersonViewScale; }

	/** Local: the held weapon fired (kick, or the knife slash for melee weapons). */
	void OnFirstPersonFired(const class UHitmenWeaponData* Weapon);

	virtual void HandleWeaponThrown(UHitmenWeaponData* Weapon) override;

	/** True while the first-person arms are shown (weapon in hand or a recent action). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Camera")
	bool AreFirstPersonArmsVisible() const { return bArmsVisible; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	UHitmenInventoryComponent* GetInventory() const { return InventoryComponent; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Player")
	UHitmenAbilityComponent* GetAbilities() const { return AbilityComponent; }

	// ---- IHitmenInputReceiver ----
	virtual void Input_Move(const FVector2D& Axis) override;
	virtual void Input_Look(const FVector2D& Axis) override;
	virtual void Input_JumpPressed() override;
	virtual void Input_JumpReleased() override;
	virtual void Input_CrouchChanged(bool bWantsCrouch) override;
	virtual void Input_SprintChanged(bool bWantsSprint) override;
	virtual void Input_SprintTapped() override;
	virtual void Input_FirePressed() override;
	virtual void Input_FireReleased() override;
	virtual void Input_AimChanged(bool bWantsAim) override;
	virtual void Input_Interact() override;
	virtual void Input_MoveAbilityPressed() override;
	virtual void Input_MoveAbilityReleased() override;
	virtual void Input_ManipulationAbilityPressed() override;
	virtual void Input_ManipulationAbilityReleased() override;
	virtual void Input_SwitchSlot(float Direction) override;
	virtual void Input_SightroomChanged(bool bActive) override;
	virtual void Input_Emote(int32 EmoteIndex) override;
	virtual bool Input_IsCrouchWanted() const override;
	virtual bool Input_IsSprintWanted() const override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USkeletalMeshComponent> FirstPersonMesh;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCameraComponent> FirstPersonCamera;

	/** Renders this player's view for watchers' grid mode (inactive by default). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneCaptureComponent2D> SpectatorCapture;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenInventoryComponent> InventoryComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenAbilityComponent> AbilityComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenInteractionComponent> InteractionComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenMeleeComponent> MeleeComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenBodyCarryComponent> BodyCarryComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenSightroomComponent> SightroomComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenEmoteComponent> EmoteComponent;

	/** Canopy shown during the intro drop (engine shapes until the team assigns a mesh). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> ParachuteCanopy;

	/** Fall speed under the canopy. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Intro", meta=(ClampMin="50", Units="cm/s"))
	float ParachuteFallSpeed = 1500.f;

	/** Gentle drift of the canopy while falling (cm/s). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Intro", meta=(ClampMin="0", Units="cm/s"))
	float ParachuteDrift = 60.f;

	/** Horizontal speed when steering the canopy with the move keys. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Intro", meta=(ClampMin="0", Units="cm/s"))
	float ParachuteSteerSpeed = 420.f;

	/** How fast the canopy answers the keys (higher = snappier). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Intro", meta=(ClampMin="0.1"))
	float ParachuteSteerResponse = 2.5f;

	/** Seconds after landing during which guards still cannot see you. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Intro", meta=(ClampMin="0", Units="s"))
	float IntroSightGraceTime = 2.f;

	float ParachuteLandTime = -100.f;

	UPROPERTY(ReplicatedUsing=OnRep_Parachuting)
	bool bParachuting = false;

	UFUNCTION()
	void OnRep_Parachuting();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnParachute(bool bDeployed);

	// ---- Camera ----
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera", meta=(ClampMin="1"))
	float AimInterpSpeed = 14.f;

	/** Eye height above the capsule centre while standing. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera", meta=(Units="cm"))
	float StandingViewHeight = 68.f;

	/** Eye height above the (shrunk) capsule centre while crouched. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera", meta=(Units="cm"))
	float CrouchedViewHeight = 32.f;

	/** Eye height above the capsule centre while sliding. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera", meta=(Units="cm"))
	float SlideViewHeight = 18.f;

	/** How fast the eye reaches its stance height (crouch, slide, stand up). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera", meta=(ClampMin="1"))
	float EyeHeightInterpSpeed = 11.f;

	/** Recoil: how fast the view reaches the kick. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Recoil", meta=(ClampMin="1"))
	float RecoilKickInterpSpeed = 28.f;

	/** Recoil: how fast the kick target returns to rest (the view follows it back down). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Recoil", meta=(ClampMin="0.1"))
	float RecoilRecoveryInterpSpeed = 7.f;

	/** Share of the kick that comes back (1 = the view returns exactly where it was). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Recoil", meta=(ClampMin="0", ClampMax="1"))
	float RecoilRecoveryRatio = 0.9f;

	// ---- First-person arms (shown only with a weapon in hand or while acting) ----
	/** Arms mesh offset from the camera when firing from the hip. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	FVector ArmsHipOffset = FVector(8.f, 17.f, -165.f);

	/** Arms mesh offset while aiming: the gun comes to the centre, still low (the view stays clear). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	FVector ArmsAimOffset = FVector(7.f, 10.f, -161.f);

	/** Sprinting: the weapon is lowered and tilted (it stays in view). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	FVector ArmsSprintOffset = FVector(4.f, 22.f, -171.f);

	/** Camera-space pitch / yaw / roll while sprinting (muzzle down, turned in, canted). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	FRotator ArmsSprintRotation = FRotator(-16.f, 10.f, 14.f);

	/** Arms mesh offset with no weapon (punch, takedown): higher, so the fists swing through the view. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	FVector ArmsUnarmedOffset = FVector(20.f, 6.f, -150.f);

	/** Yaw of the arms mesh relative to the camera (mannequin forward is +Y). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	float ArmsYawOffset = -90.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="1"))
	float ArmsOffsetInterpSpeed = 14.f;

	/** Seconds the arms stay visible after a punch / throw with no weapon in hand. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0", Units="s"))
	float ArmsActionLinger = 0.6f;

	/** Bones hidden on the first-person mesh (head and legs: only the arms are drawn). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	TArray<FName> FirstPersonHiddenBones = { TEXT("neck_01"), TEXT("thigh_l"), TEXT("thigh_r") };

	/** Arms sway: degrees of lag per degree/s of view rotation. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0"))
	float ArmsSwayAmount = 0.012f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0"))
	float ArmsSwayMax = 3.5f;

	/** Walking bob of the arms (cm at full walk speed). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0"))
	float ArmsBobAmount = 0.9f;

	/** Arms rotations (sway, kick, actions) turn around this point in camera space (about the hands), not the feet. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	FVector ArmsPivot = FVector(35.f, 10.f, -22.f);

	/** Hip <-> aim travel: a spring that overshoots a little and settles (the raise reads as a motion). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="1"))
	float ArmsAimStiffness = 260.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0", ClampMax="2"))
	float ArmsAimDamping = 0.72f;

	/** Moving sideways tilts the arms (deg at walk speed); moving forward / back pushes them (cm). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0"))
	float ArmsStrafeTilt = 4.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0"))
	float ArmsMoveLag = 1.6f;

	/** Near a wall the weapon is pulled back and lowered instead of going through it. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0", Units="cm"))
	float ArmsWallCheckDistance = 75.f;

	/** First-person rendering scale (UE first-person view): lower pulls the arms toward the eye, so they never
	 *  reach into walls, without changing their size on screen. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms", meta=(ClampMin="0.01", ClampMax="1"))
	float FirstPersonViewScale = 0.35f;

	// ---- Damage feel (the camera's own post process: fringe and red tint on hits, colour drains with health) ----
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Damage", meta=(ClampMin="0"))
	float HitFringeIntensity = 5.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Damage", meta=(ClampMin="0", ClampMax="1"))
	float LowHealthSaturation = 0.35f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Damage", meta=(ClampMin="0"))
	float HitShake = 0.35f;

	float HitPulse = 0.f;
	float LastHealthSeen = -1.f;
	float LastArmorSeen = -1.f;
	float ArmorPulse = 0.f;

	// ---- Trap: mash the move keys to break free ----
	/** Distinct move-key presses needed to break out of a trap (each press counts once; progress drains). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Movement|Trap", meta=(ClampMin="1"))
	int32 TrapMashPresses = 8;

	/** Share of the escape lost per second without a press. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Movement|Trap", meta=(ClampMin="0"))
	float TrapEscapeDrain = 0.35f;

	UFUNCTION(Server, Reliable)
	void Server_TrapMash();

	float TrapEscapeProgress = 0.f;
	FIntPoint LastMoveKeys = FIntPoint::ZeroValue;
	void UpdateDamageFeel(float DeltaSeconds);

	// ---- Head bob (the camera; footsteps fall on the low point of each bob) ----
	/** Bob height and sideways sway (cm) at walk; sprint and crouch scale it. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|HeadBob", meta=(ClampMin="0", Units="cm"))
	float HeadBobHeight = 1.4f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|HeadBob", meta=(ClampMin="0", Units="cm"))
	float HeadBobSide = 0.8f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|HeadBob", meta=(ClampMin="0"))
	float HeadBobSprintScale = 1.7f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|HeadBob", meta=(ClampMin="0"))
	float HeadBobCrouchScale = 0.6f;

	/** Distance per step (cm) for walk, sprint and crouch: one bob (and one footstep) per step. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|HeadBob", meta=(ClampMin="10", Units="cm"))
	float WalkStepLength = 75.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|HeadBob", meta=(ClampMin="10", Units="cm"))
	float SprintStepLength = 105.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|HeadBob", meta=(ClampMin="10", Units="cm"))
	float CrouchStepLength = 50.f;

	// ---- Landing ----
	/** Falls faster than this make a landing sound; faster than HardLandingSpeed a hard landing (sound + view dip). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Movement|Landing", meta=(ClampMin="0", Units="cm/s"))
	float LandingSoundSpeed = 350.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Movement|Landing", meta=(ClampMin="0", Units="cm/s"))
	float HardLandingSpeed = 950.f;

	/** 0..1 blend toward the sprint arms pose. */
	float ArmsSprintAlpha = 0.f;

	// ---- Death camera ----
	/** After death the view leaves the head and orbits the body from behind and above. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Death", meta=(ClampMin="0", Units="cm"))
	float DeathCameraDistance = 280.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Death", meta=(Units="cm"))
	float DeathCameraHeight = 170.f;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Death", meta=(ClampMin="0.1"))
	float DeathCameraInterpSpeed = 2.5f;

	/** Slow orbit around the body (deg/s). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Death")
	float DeathCameraOrbitSpeed = 8.f;

	// ---- Charm ----
	/** AI controller that drives this player while charmed. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Charm")
	TSubclassOf<AAIController> CharmedControllerClass;

	// ---- Proximity voice ----
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Voice")
	TObjectPtr<USoundAttenuation> VoiceAttenuation;

	UPROPERTY(Transient)
	TObjectPtr<UVOIPTalker> VoiceTalker;

	UPROPERTY(Replicated)
	bool bIsAiming = false;

	UPROPERTY(Transient)
	TObjectPtr<AController> CharmedPreviousController;

	UFUNCTION(Server, Reliable)
	void Server_SetAiming(bool bAim);

	UFUNCTION(Server, Reliable)
	void Server_SetClimbRelative(bool bRelative);

	virtual void HandleStatusAdded(EHitmenStatusEffect Status, AActor* StatusInstigator) override;
	virtual void HandleStatusRemoved(EHitmenStatusEffect Status, AActor* StatusInstigator) override;
	virtual void HandleDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot) override;

	/** The local player's first-person arms play montages too. */
	virtual void GetMontageMeshes(TArray<USkeletalMeshComponent*>& OutMeshes) const override;

	/** Local player: FOV and ladder direction from Options; re-applied whenever Options are saved. */
	UFUNCTION()
	void ApplyUserSettings();

	void BeginCharm(AActor* Charmer);
	void EndCharm();
	void SetupProximityVoice();
	void ReleaseProximityVoice();
	void ApplyInvisibilityVisuals(bool bInvisible);
	bool CanAct() const;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnAimChanged(bool bAiming);

	/** The local view left the body after death (death camera). */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDeathCamera();

	void BindLocalSettings();
	void UpdateEyeHeight(float DeltaSeconds);
	void UpdateRecoil(float DeltaSeconds);
	void UpdateFirstPersonArms(float DeltaSeconds);
	void UpdateDeathCamera(float DeltaSeconds);
	float GetTargetEyeHeight() const;
	/** Local: camera bob from the step phase; plays your footstep at the low point of each step. */
	void UpdateHeadBob(float DeltaSeconds);
	/** Offset (cm) and rotation (deg, camera space) of the running procedural action. */
	void EvaluateFirstPersonAction(FVector& OutOffset, FRotator& OutRotation) const;

	float BaseFOV = 90.f;
	float CurrentEyeHeight = 68.f;
	FVector2D RecoilTarget = FVector2D::ZeroVector;
	FVector2D RecoilCurrent = FVector2D::ZeroVector;
	/** Part of the kick that stays (1 - RecoilRecoveryRatio); the target settles there. */
	FVector2D RecoilSettled = FVector2D::ZeroVector;
	float ShakeIntensity = 0.f;
	float ShakeDuration = 0.f;
	float ShakeTimeLeft = 0.f;
	FVector CurrentArmsOffset = FVector::ZeroVector;
	FVector ArmsOffsetVelocity = FVector::ZeroVector;
	// Fire kick spring (camera-space offset and pitch / yaw / roll), equip, actions, scope, wall.
	FVector KickOffset = FVector::ZeroVector;
	FVector KickOffsetVelocity = FVector::ZeroVector;
	FVector KickRotation = FVector::ZeroVector;
	FVector KickRotationVelocity = FVector::ZeroVector;
	float KickStiffness = 220.f;
	TWeakObjectPtr<class AHitmenWeaponBase> LastArmsWeapon;
	float EquipStartTime = -100.f;
	EHitmenFPAction FPAction = EHitmenFPAction::None;
	float FPActionStart = -100.f;
	float FPActionDuration = 0.f;
	float FPActionSide = 1.f;
	float AimHeldTime = 0.f;
	float WallPullAlpha = 0.f;
	FVector2D ScopeSwayApplied = FVector2D::ZeroVector;
	float HeadBobPhase = 0.f;
	float HeadBobAlpha = 0.f;
	int32 HeadBobStep = 0;
	FVector HeadBobOffset = FVector::ZeroVector;
	/** Smoothed walk lag (X, cm) and strafe tilt (roll, deg) of the arms. */
	FVector2D ArmsMoveState = FVector2D::ZeroVector;
	FRotator ArmsSway = FRotator::ZeroRotator;
	FRotator LastControlRotation = FRotator::ZeroRotator;
	float ArmsBobPhase = 0.f;
	float ArmsVisibleUntil = -1.f;
	bool bArmsVisible = false;
	bool bArmsInitialised = false;
	bool bDeathCameraActive = false;
	float DeathCameraYaw = 0.f;
	bool bLocalSettingsBound = false;
	float HintAccumulator = 0.f;

	/** Local: pushes the traversal hint ("[SPACE] Vault") and the scope state to the HUD. */
	void UpdateHUDHints(float DeltaSeconds);
	bool bWantsCrouchInput = false;
	bool bWantsSprintInput = false;

	// Jetpack on Space: holding Space in the air thrusts (E still works).
	bool bJumpInputHeld = false;
	float JumpInputPressTime = -100.f;
	bool bJetpackFromSpace = false;

	/** Space held this long in the air starts the jetpack (a plain jump stays a jump). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Movement", meta=(ClampMin="0", Units="s"))
	float JetpackSpaceHoldDelay = 0.2f;

	/** Move ability of this class equipped? */
	bool HasMoveAbility(TSubclassOf<class UHitmenAbility> AbilityClass) const;
	void UpdateJetpackFromSpace();
};
