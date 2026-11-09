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

	virtual void Landed(const FHitResult& Hit) override;

	/** Local: shows the first-person arms for a moment (punch, throw, interact). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Camera")
	void ShowFirstPersonArms(float Duration);

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
	float ParachuteFallSpeed = 520.f;

	/** Gentle drift of the canopy while falling (cm/s). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Intro", meta=(ClampMin="0", Units="cm/s"))
	float ParachuteDrift = 60.f;

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
	FVector ArmsHipOffset = FVector(-6.f, 9.f, -158.f);

	/** Arms mesh offset from the camera while aiming (sights near the screen centre). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera|Arms")
	FVector ArmsAimOffset = FVector(-4.f, 0.f, -153.f);

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
};
