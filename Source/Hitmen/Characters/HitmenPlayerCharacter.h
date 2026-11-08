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

	// ---- Camera ----
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Camera", meta=(ClampMin="1"))
	float AimInterpSpeed = 14.f;

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

	float BaseFOV = 90.f;
	bool bWantsCrouchInput = false;
	bool bWantsSprintInput = false;
};
