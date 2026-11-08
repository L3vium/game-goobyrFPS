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
class AHitmenCharacterBase;
class AHitmenProjectile;

/** Enter with F, exit with F. The driver is hidden and the controller possesses the vehicle. */
UCLASS(Abstract, Blueprintable)
class HITMEN_API AHitmenVehiclePawn : public APawn, public IHitmenInputReceiver, public IHitmenInteractable, public IHitmenTeamMember
{
	GENERATED_BODY()

public:
	AHitmenVehiclePawn();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

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

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> Collision;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Vehicle", meta=(MakeEditWidget=true))
	FVector ExitOffset = FVector(-200.f, 0.f, 100.f);

	UPROPERTY(Replicated, BlueprintReadOnly, Category="Hitmen|Vehicle")
	TObjectPtr<AHitmenCharacterBase> Driver;

	UFUNCTION(Server, Reliable)
	void Server_Exit();

	UFUNCTION(Server, Reliable)
	void Server_SetFiring(bool bNewFiring);

	void Enter(AHitmenCharacterBase* Character);
	void Exit();

	/** Server: called while the trigger is held (auto weapons) or once (cannons). */
	virtual void FireServer() {}

	bool bFiring = false;

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEntered(AHitmenCharacterBase* NewDriver);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnExited();
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

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> GunPivot;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCameraComponent> Camera;

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

/** Drivable tank: third-person camera, turret follows the view, explosive cannon. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenTank : public AHitmenVehiclePawn
{
	GENERATED_BODY()

public:
	AHitmenTank();
	virtual void Tick(float DeltaSeconds) override;
	virtual void Input_Move(const FVector2D& Axis) override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Turret;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USpringArmComponent> SpringArm;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UCameraComponent> Camera;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UFloatingPawnMovement> Movement;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float TurnRate = 60.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank")
	TSubclassOf<AHitmenProjectile> ShellClass;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float ShellSpeed = 6000.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float ReloadTime = 2.5f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Tank", meta=(ClampMin="0"))
	float HoverHeight = 60.f;

	virtual void FireServer() override;

	float LastShotTime = -100.f;
};
