// Hitmen — security: cameras (visible cone, alarms), monitors, control consoles, turrets and
// their hack panels, circuit breakers, light fixtures, blackout volumes

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Interaction/HitmenInteractableBase.h"
#include "Core/HitmenInterfaces.h"
#include "Core/HitmenTeamLibrary.h"
#include "AI/HitmenAISubsystem.h"
#include "HitmenSecurity.generated.h"

class USceneCaptureComponent2D;
class UTextureRenderTarget2D;
class UMaterialInstanceDynamic;
class UDecalComponent;
class UBoxComponent;
class ULightComponent;
class UPointLightComponent;
class UMaterialInterface;
class UNiagaraSystem;
class USoundBase;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenCameraDisabledChanged, bool, bDisabled);

/** Sweeping camera. Its cone is visible (white, red when it sees you) and it calls reinforcements. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenSecurityCamera : public AActor
{
	GENERATED_BODY()

public:
	AHitmenSecurityCamera();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Camera")
	void SetCameraDisabled(bool bDisabled);

	UFUNCTION(BlueprintPure, Category="Hitmen|Camera")
	bool IsCameraDisabled() const { return bDisabled; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Camera")
	bool IsSeeingIntruder() const { return bSeeingIntruder; }

	/** Every machine: the camera was disabled / re-enabled (monitors show "no signal"). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Camera")
	FOnHitmenCameraDisabledChanged OnDisabledChanged;

	/** Renders the camera view into a target (monitors). Does nothing while disabled. */
	void CaptureTo(UTextureRenderTarget2D* Target);

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Root;

	/** Rotating head (yaw sweep). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Head;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Body;

	/** Visible view cone (translucent mesh, color parameter "Color"). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> ViewCone;

	/** Footprint of the cone on the floor (decal, color parameter "Color"). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UDecalComponent> FloorDecal;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneCaptureComponent2D> Capture;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0", Units="cm"))
	float ViewDistance = 1200.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="1", ClampMax="89", Units="deg"))
	float ViewHalfAngle = 17.f;

	/** The head looks down at the floor by this much (the cone draws a round footprint there). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0", ClampMax="89", Units="deg"))
	float LookDownAngle = 38.f;

	/** Total sweep arc (0 = static). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0", ClampMax="350", Units="deg"))
	float SweepAngle = 90.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float SweepSpeed = 0.4f;

	/** Seconds of continuous sight before the alarm. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float DetectionTime = 1.2f;

	/** Seconds between two alarms. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float AlarmCooldown = 10.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	FLinearColor IdleColor = FLinearColor(1.f, 1.f, 1.f, 0.25f);

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	FLinearColor AlertColor = FLinearColor(1.f, 0.05f, 0.05f, 0.4f);

	UPROPERTY(ReplicatedUsing=OnRep_Visuals)
	bool bDisabled = false;

	UPROPERTY(ReplicatedUsing=OnRep_Visuals)
	bool bSeeingIntruder = false;

	UPROPERTY(Transient)
	TArray<TObjectPtr<UMaterialInstanceDynamic>> ColorMaterials;

	/** Last disabled state broadcast on this machine (OnRep_Visuals also fires for bSeeingIntruder). */
	bool bLastBroadcastDisabled = false;

	UFUNCTION() void OnRep_Visuals();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnAlarm(AActor* Intruder);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSeeingChanged(bool bSeeing);

	APawn* FindIntruder() const;
	/** Floor disc under the cone (follows the sweep). */
	void UpdateFootprint();
	void RaiseAlarm(APawn* Intruder);

	float SweepTime = 0.f;
	float SeenTime = 0.f;
	float LastAlarmTime = -1000.f;
	FRotator BaseHeadRotation;
};

/** Screen showing one camera's view. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenSecurityMonitor : public AActor
{
	GENERATED_BODY()

public:
	AHitmenSecurityMonitor();
	virtual void BeginPlay() override;

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Screen;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Monitor")
	TObjectPtr<AHitmenSecurityCamera> LinkedCamera;

	/** Material slot index of the screen and its texture parameter name. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Monitor")
	int32 ScreenMaterialIndex = 0;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Monitor")
	FName ScreenTextureParameter = TEXT("ScreenTexture");

	/** Only refresh when a local player is this close (performance). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Monitor", meta=(ClampMin="0"))
	float RefreshDistance = 1500.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Monitor", meta=(ClampMin="0.02"))
	float RefreshInterval = 0.2f;

	/** Shown on the screen slot while the linked camera is disabled (no capture runs meanwhile). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Monitor")
	TSoftObjectPtr<UMaterialInterface> NoSignalMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Hitmen/Materials/M_Monitor_NoSignal.M_Monitor_NoSignal")));

	UPROPERTY(Transient)
	TObjectPtr<UTextureRenderTarget2D> RenderTarget;

	/** Live feed material (render target), restored when the camera comes back. */
	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> FeedMaterial;

	void Refresh();

	UFUNCTION()
	void HandleCameraDisabledChanged(bool bCameraDisabled);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnSignalChanged(bool bHasSignal);

	FTimerHandle RefreshTimer;
};

/**
 * Security station: a desk with a screen cycling through its cameras' feeds. F (or a remote
 * hack) disables / enables every linked camera; the screen then shows "no signal".
 */
UCLASS(Blueprintable)
class HITMEN_API AHitmenControlConsole : public AHitmenInteractableBase, public IHitmenRemoteActivatable
{
	GENERATED_BODY()

public:
	AHitmenControlConsole();
	virtual void BeginPlay() override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual bool CanRemoteActivate_Implementation() const override { return true; }
	virtual void RemoteActivate_Implementation(AActor* Activator) override { OnInteracted(Activator); }

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console")
	TArray<TObjectPtr<AHitmenSecurityCamera>> LinkedCameras;

	/** Screen above the desk, parallel to its front (material slot 0 shows the feed). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Screen;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console")
	FName ScreenTextureParameter = TEXT("ScreenTexture");

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console")
	TSoftObjectPtr<UMaterialInterface> NoSignalMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Hitmen/Materials/M_Monitor_NoSignal.M_Monitor_NoSignal")));

	/** Seconds each camera stays on screen. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console", meta=(ClampMin="0.5", Units="s"))
	float FeedCycleTime = 3.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console", meta=(ClampMin="0.02", Units="s"))
	float RefreshInterval = 0.2f;

	/** Only refresh when a local player is this close (performance). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console", meta=(ClampMin="0", Units="cm"))
	float RefreshDistance = 1500.f;

	UPROPERTY(ReplicatedUsing=OnRep_CamerasDisabled)
	bool bCamerasDisabled = false;

	UPROPERTY(Transient)
	TObjectPtr<UTextureRenderTarget2D> RenderTarget;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> FeedMaterial;

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void OnInteracted(AActor* Interactor) override;

	UFUNCTION()
	void OnRep_CamerasDisabled();

	void RefreshScreen();
	void ApplyScreenState();

	FTimerHandle RefreshTimer;
	int32 FeedIndex = 0;
	float FeedTime = 0.f;
};

/** Auto turret. Shoots players; once hacked it shoots for the hacker's side. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenTurret : public AActor, public IHitmenTeamMember, public IHitmenInteractable, public IHitmenRemoteActivatable
{
	GENERATED_BODY()

public:
	AHitmenTurret();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void Tick(float DeltaSeconds) override;
	virtual int32 GetHitmenTeamId_Implementation() const override { return TeamId; }

	// F on the turret itself hacks it (like its panel).
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return !bHacked; }
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override { Hack(Interactor); }
	virtual bool CanRemoteActivate_Implementation() const override { return !bHacked; }
	virtual void RemoteActivate_Implementation(AActor* Activator) override { Hack(Activator); }

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Turret")
	void Hack(AActor* Hacker);

	UFUNCTION(BlueprintPure, Category="Hitmen|Turret")
	bool IsHacked() const { return bHacked; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneComponent> Root;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Base;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Head;

	/** Barrel (kicks back on each shot). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Barrel;

	/** Red aiming laser to the current target (thin cylinder scaled to the distance). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Laser;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0", Units="cm"))
	float Range = 2500.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0"))
	float Damage = 6.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0.02"))
	float FireInterval = 0.3f;

	/** Seconds the laser is on a new target before the first shot (time to react). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0", Units="s"))
	float LockOnTime = 0.8f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret|Juice")
	TObjectPtr<UNiagaraSystem> MuzzleFX;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret|Juice")
	TObjectPtr<UNiagaraSystem> ImpactFX;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret|Juice")
	TObjectPtr<USoundBase> FireSound;

	/** Barrel kick per shot (cm, recovers). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret|Juice", meta=(ClampMin="0", Units="cm"))
	float BarrelKick = 9.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0"))
	float TurnSpeed = 120.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0"))
	float AimErrorDegrees = 2.f;

	UPROPERTY(Replicated)
	int32 TeamId = HitmenTeams::NPC;

	UPROPERTY(Replicated)
	bool bHacked = false;

	UPROPERTY(Replicated)
	TObjectPtr<AActor> CurrentTarget;

	UPROPERTY(Transient)
	TObjectPtr<AActor> HackedBy;

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Fire(FVector_NetQuantize Impact);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnFire(FVector Impact);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnHacked();

	AActor* FindTarget() const;
	void UpdateLaser();
	float LastFireTime = 0.f;
	float RetargetAccumulator = 0.f;
	float TargetAcquiredTime = 0.f;
	float BarrelOffset = 0.f;
	FVector BarrelRestLocation = FVector::ZeroVector;
	TWeakObjectPtr<AActor> LastTarget;
};

/** Wired panel that hacks its turret. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenHackPanel : public AHitmenInteractableBase, public IHitmenRemoteActivatable
{
	GENERATED_BODY()

public:
	AHitmenHackPanel();
	virtual bool CanInteract_Implementation(AActor* Interactor) const override;
	virtual bool CanRemoteActivate_Implementation() const override;
	virtual void RemoteActivate_Implementation(AActor* Activator) override { OnInteracted(Activator); }

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HackPanel")
	TObjectPtr<AHitmenTurret> LinkedTurret;

	virtual void OnInteracted(AActor* Interactor) override;
};

/** A lamp; turned off by its circuit breaker. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenLightFixture : public AActor
{
	GENERATED_BODY()

public:
	AHitmenLightFixture();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Light")
	void SetPowered(bool bPowered);

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UPointLightComponent> Light;

	UPROPERTY(ReplicatedUsing=OnRep_Powered)
	bool bPowered = true;

	UFUNCTION() void OnRep_Powered();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnPowerChanged(bool bOn);
};

/** Area that becomes dark when its breaker is off: NPCs cannot see inside it. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenBlackoutVolume : public AActor, public IHitmenDarkZone
{
	GENERATED_BODY()

public:
	AHitmenBlackoutVolume();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	virtual bool IsLocationDark(const FVector& Location) const override;

	void SetDark(bool bInDark) { bDark = bInDark; }

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UBoxComponent> Bounds;

	UPROPERTY(Replicated, BlueprintReadOnly, Category="Hitmen|Blackout")
	bool bDark = false;
};

/** Circuit breaker: cuts power to its lights and darkens its volumes. */
UCLASS(Blueprintable)
class HITMEN_API AHitmenCircuitBreaker : public AHitmenInteractableBase, public IHitmenRemoteActivatable
{
	GENERATED_BODY()

public:
	AHitmenCircuitBreaker();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual bool CanRemoteActivate_Implementation() const override { return true; }
	virtual void RemoteActivate_Implementation(AActor* Activator) override { OnInteracted(Activator); }

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Breaker")
	TArray<TObjectPtr<AHitmenLightFixture>> LinkedLights;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Breaker")
	TArray<TObjectPtr<AHitmenBlackoutVolume>> LinkedVolumes;

	UPROPERTY(Replicated)
	bool bPowered = true;

	virtual void OnInteracted(AActor* Interactor) override;
};
