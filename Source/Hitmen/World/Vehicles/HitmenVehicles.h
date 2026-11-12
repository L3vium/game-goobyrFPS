// Hitmen — possessable fixtures: base vehicle, mounted machine gun (180°), tank (TPS + cannon)

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Pawn.h"
#include "Core/HitmenInterfaces.h"
#include "Core/HitmenTeamLibrary.h"
#include "Interaction/HitmenInteractable.h"
#include "HitmenVehicles.generated.h"

class UCameraComponent;
class USpringArmComponent;
class UBoxComponent;
class UStaticMeshComponent;
class UFloatingPawnMovement;
class UHitmenHealthComponent;
class AHitmenCharacterBase;
class AHitmenProjectile;

/**
 * Enter with F, exit with F. The driver is hidden and the controller possesses the vehicle.
 * Vehicles have health: a share of the damage they take reaches the hidden driver, and a
 * destroyed vehicle ejects its driver and can no longer be used.
 */
UCLASS(Abstract, Blueprintable)
class HITMEN_API AHitmenVehiclePawn : public APawn, public IHitmenInputReceiver, public IHitmenInteractable, public IHitmenTeamMember
{
	GENERATED_BODY()

public:
	AHitmenVehiclePawn();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual float TakeDamage(float DamageAmount, struct FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser) override;

	/** Shown on the vehicle HUD (health bar title). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Vehicle")
	FText DisplayName = NSLOCTEXT("Hitmen", "Vehicle", "Vehicle");

	/** Health 0..1 for the HUD. */
	float GetHealthNormalized() const;

	/** Reload progress 0..1 for the HUD ring (negative: no reload, e.g. machine guns). */
	virtual float GetReloadAlpha() const { return -1.f; }
	/** Server: the controller left (disconnect) — put the driver back in the world. */
	virtual void UnPossessed() override;

	// IHitmenInteractable
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

	// IHitmenTeamMember
	virtual int32 GetHitmenTeamId_Implementation() const override;

	// IHitmenInputReceiver
	virtual void Input_Look(const FVector2D& Axis) override;
	virtual void Input_Interact() override;
	virtual void Input_FirePressed() override;
	virtual void Input_FireReleased() override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Vehicle")
	AHitmenCharacterBase* GetDriver() const { return Driver; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Vehicle")
	UHitmenHealthComponent* GetHealth() const { return Health; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Vehicle")
	bool IsVehicleDestroyed() const { return bVehicleDestroyed; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> Collision;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	/** Vehicle health (set MaxHealth on the component in the BP child). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UHitmenHealthComponent> Health;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Vehicle", meta=(MakeEditWidget=true))
	FVector ExitOffset = FVector(-200.f, 0.f, 100.f);

	/** Share of the incoming damage (after explosion falloff, before the vehicle multipliers) forwarded to the driver. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Vehicle|Damage", meta=(ClampMin="0", ClampMax="1"))
	float DriverDamageShare = 0.25f;

	/** Multiplier on bullets / melee (point damage). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Vehicle|Damage", meta=(ClampMin="0"))
	float PointDamageMultiplier = 1.f;

	/** Multiplier on explosions (radial damage). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Vehicle|Damage", meta=(ClampMin="0"))
	float RadialDamageMultiplier = 1.f;

	/** Empty vehicles are neutral (not hostile to anyone): allow damaging them anyway. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Vehicle|Damage")
	bool bEmptyVehicleTakesDamage = true;

	UPROPERTY(ReplicatedUsing=OnRep_Driver, BlueprintReadOnly, Category="Hitmen|Vehicle")
	TObjectPtr<AHitmenCharacterBase> Driver;

	UPROPERTY(ReplicatedUsing=OnRep_VehicleDestroyed, BlueprintReadOnly, Category="Hitmen|Vehicle")
	bool bVehicleDestroyed = false;

	UFUNCTION(Server, Reliable)
	void Server_Exit();

	UFUNCTION(Server, Reliable)
	void Server_SetFiring(bool bNewFiring);

	void Enter(AHitmenCharacterBase* Character);
	void Exit();

	/** Server: tries ExitOffset, its mirror and a spot above until the driver fits. */
	void PlaceExitingDriver(AHitmenCharacterBase* Character) const;

	/** Hides / shows the driver locally (collision is not replicated, so clients do it too). */
	static void SetDriverStowed(AHitmenCharacterBase* Character, bool bStowed);

	/** Server: called while the trigger is held (auto weapons) or once (cannons). */
	virtual void FireServer() {}

	/** Server: the vehicle's health reached zero. */
	virtual void OnVehicleDestroyed();

	UFUNCTION() void OnRep_Driver();
	UFUNCTION() void OnRep_VehicleDestroyed();

	UFUNCTION()
	void HandleHealthDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot);

	UFUNCTION()
	void HandleHealthChanged(UHitmenHealthComponent* HealthComponent, float NewHealth, float Delta, AActor* DamageInstigator);

	/** Ejects a driver about to die (health hits zero before OnDeath), so the death flow finds its controller. */
	UFUNCTION()
	void HandleDriverHealthChanged(UHitmenHealthComponent* HealthComponent, float NewHealth, float Delta, AActor* DamageInstigator);

	bool bFiring = false;

	/** Clients: last driver seen, to restore it when the replicated driver changes. */
	TWeakObjectPtr<AHitmenCharacterBase> StowedDriver;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEntered(AHitmenCharacterBase* NewDriver);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnExited();

	/** Every machine, when the vehicle loses health. */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDamaged(float DamageTaken, float HealthNormalized);

	/** Every machine, once, when the vehicle is destroyed. */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnDestroyed();
};

/** Fixed machine gun: stand behind it and shoot within a 180° arc. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenMountedGun : public AHitmenVehiclePawn
{
	GENERATED_BODY()

public:
	AHitmenMountedGun();
	virtual void Tick(float DeltaSeconds) override;
	virtual void Input_Look(const FVector2D& Axis) override;

	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override;

	/** True when the user stands behind the gun. */
	UFUNCTION(BlueprintPure, Category="Hitmen|MountedGun")
	bool IsUserBehind(const AActor* User) const;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> GunPivot;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCameraComponent> Camera;

	/** Tripod and shield plate (engine shapes until the team assigns meshes). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Tripod;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Shield;

	/** Flashes at the muzzle on each shot (a light, not a particle). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UPointLightComponent> MuzzleLight;

	/** Look speed multiplier (the player's look goes through the controller's x2.5 input scale; this matches it). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(ClampMin="0.1"))
	float LookSensitivity = 2.5f;

	/** View shake per shot for the gunner. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun|Juice", meta=(ClampMin="0"))
	float FireShake = 0.35f;

	float MuzzleFlashUntil = 0.f;

	/** Off: the gun can only be used by someone standing behind it. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun")
	bool bUsableFromAnySide = true;

	/** Half of the firing arc. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(ClampMin="0", ClampMax="180", Units="deg"))
	float MaxYaw = 90.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(Units="deg"))
	float MinPitch = -20.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(Units="deg"))
	float MaxPitch = 30.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(ClampMin="0"))
	float Damage = 18.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(ClampMin="0.02"))
	float FireInterval = 0.09f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(ClampMin="0"))
	float Range = 8000.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(ClampMin="0"))
	float SpreadDegrees = 1.5f;

	/**
	 * Use-from-behind check: dot(gun forward, direction gun -> user) must be below this
	 * (-1 = exactly behind, 0 = anywhere in the rear half).
	 */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|MountedGun", meta=(ClampMin="-1", ClampMax="1"))
	float UseFromBehindMaxDot = -0.2f;

	/** Prompt shown when the user is not behind the gun. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|MountedGun")
	FText UseFromBehindText;

	UPROPERTY(Replicated)
	FRotator AimRotation = FRotator::ZeroRotator;

	UFUNCTION(Server, Unreliable)
	void Server_SetAim(FRotator NewAim);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Fire(FVector_NetQuantize Impact);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnFire(FVector Impact);

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void FireServer() override;

	float LastFireTime = 0.f;
};

/**
 * Drivable tank: third-person camera, turret follows the view, explosive cannon.
 * Server-authoritative: the driver sends its drive axis and aim, the server moves the hull
 * (replicated movement for everyone else, a smoothed server transform for the driver).
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenTank : public AHitmenVehiclePawn
{
	GENERATED_BODY()

public:
	AHitmenTank();
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
	virtual void Input_Move(const FVector2D& Axis) override;
	virtual void Input_Look(const FVector2D& Axis) override;
	virtual void Input_FirePressed() override;
	virtual float GetReloadAlpha() const override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Turret;

	/** The cannon: follows the aim pitch, kicks back on each shot. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Barrel;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UPointLightComponent> MuzzleLight;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank|Juice", meta=(ClampMin="0"))
	float FireShake = 2.5f;

	/** How far the barrel slides back on a shot (cm) and how much the hull rocks back (deg). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank|Juice", meta=(ClampMin="0", Units="cm"))
	float BarrelKickDistance = 120.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank|Juice", meta=(ClampMin="0", Units="deg"))
	float HullKickPitch = 4.f;

	/** Blast at the muzzle and dust around the hull on a shot. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank|Juice")
	TObjectPtr<class UNiagaraSystem> MuzzleBlastFX;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank|Juice")
	TObjectPtr<class UNiagaraSystem> HullDustFX;

	float HullKick = 0.f;
	FRotator HullRestRotation = FRotator::ZeroRotator;
	bool bHullRestSet = false;

	float MuzzleFlashUntil = 0.f;
	float BarrelKick = 0.f;

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_TankFired();

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USpringArmComponent> SpringArm;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCameraComponent> Camera;

	/** Holds the drive tunables (MaxSpeed, Acceleration, Deceleration) and the velocity; the tank moves itself on the server. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UFloatingPawnMovement> Movement;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float TurnRate = 60.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank")
	TSubclassOf<AHitmenProjectile> ShellClass;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float ShellSpeed = 20000.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float ReloadTime = 1.25f;

	/** Gap kept between the hull box and the ground (keeps the drive sweep off the floor). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float HoverHeight = 5.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0", Units="cm"))
	float GroundTraceDistance = 2000.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float GroundFollowSpeed = 10.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank", meta=(Units="deg"))
	float MinAimPitch = -15.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank", meta=(Units="deg"))
	float MaxAimPitch = 35.f;

	/** Server drops the drive input if the driver has not refreshed it for this long (lost release packet). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank|Network", meta=(ClampMin="0.05", Units="s"))
	float DriveInputTimeout = 0.3f;

	/** Driving client: minimum delay between two unchanged drive-input sends (must stay below DriveInputTimeout). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank|Network", meta=(ClampMin="0.01", Units="s"))
	float DriveInputResendInterval = 0.1f;

	/** Driver's view: how fast it converges to the server transform. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank|Network", meta=(ClampMin="0"))
	float DriverSmoothingSpeed = 15.f;

	/** Driver's view: snap instead of smoothing past this error. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank|Network", meta=(ClampMin="0", Units="cm"))
	float DriverSnapDistance = 500.f;

	/** Barrel muzzle distance from the turret pivot. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0", Units="cm"))
	float MuzzleOffset = 350.f;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0", Units="cm"))
	float CannonNoiseRadius = 6000.f;

	/** Aim used by the server (cannon) and by other clients (turret). The driver uses its own view. */
	UPROPERTY(Replicated)
	FRotator AimRotation = FRotator::ZeroRotator;

	/** Server transform sent to the driving client only (it does not receive replicated movement). */
	UPROPERTY(ReplicatedUsing=OnRep_DriverState)
	FVector_NetQuantize10 DriverStateLocation = FVector::ZeroVector;

	UPROPERTY(ReplicatedUsing=OnRep_DriverState)
	float DriverStateYaw = 0.f;

	UFUNCTION(Server, Unreliable)
	void Server_SetDriveInput(FVector2D Axis);

	UFUNCTION(Server, Unreliable)
	void Server_SetAim(FRotator NewAim);

	UFUNCTION() void OnRep_DriverState();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void FireServer() override;
	virtual void OnVehicleDestroyed() override;

	/** Server: integrates DriveInput into hull yaw / speed and moves with a sweep. */
	void SimulateDrive(float DeltaSeconds);
	void StickToGround(float DeltaSeconds);
	FRotator ClampAim(const FRotator& Aim) const;

	FVector2D DriveInput = FVector2D::ZeroVector;
	float LastDriveInputTime = 0.f;
	FVector2D LastSentDriveInput = FVector2D::ZeroVector;
	float LastDriveSendTime = -100.f;
	float CurrentSpeed = 0.f;
	float LastShotTime = -100.f;
	bool bHasDriverState = false;
};
