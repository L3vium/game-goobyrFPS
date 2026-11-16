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

	/** 0..1 detection meter (all machines): the alarm goes off at 1. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Camera")
	float GetDetectionMeter() const { return DetectionProgress / 255.f; }

	/** Raised the alarm within the last AlarmCooldown (all machines): the HUD shows it as a search, not a fight. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Camera")
	bool HasRecentlyAlarmed() const { return bRecentlyAlarmed; }

	/** Shot to pieces: it goes dark for good (sparks, the head drops, monitors lose it, a noise). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Camera")
	bool IsBroken() const { return bBroken; }

	virtual float TakeDamage(float DamageAmount, struct FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser) override;

	/** Every machine: the camera was disabled / re-enabled (monitors show "no signal"). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Camera")
	FOnHitmenCameraDisabledChanged OnDisabledChanged;

	/** Renders the camera view into a target (monitors). Does nothing while disabled. */
	void CaptureTo(UTextureRenderTarget2D* Target);

	/** Who the camera is looking at right now (replicated; the camera-feed picture of the one seen). */
	APawn* GetSeenIntruder() const { return SeenIntruder; }

	/** The feed's view (position, direction) and field of view, for pictures of what the camera sees. */
	USceneComponent* GetFeedView() const;
	float GetFeedFOV() const { return FeedFOV; }

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

	/** The electronic ring where the light meets the floor and walls (decal; material parameters Color, Progress,
	 *  Eye, Forward, CosHalfAngle: the material tests the camera's own cone per pixel, so it matches the light). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UDecalComponent> FloorDecal;

	/** Shows the camera on the minimap and the pause map (hidden while disabled). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UHitmenMapMarkerComponent> MapMarker;

	/** What the camera watches, shown as real light: walls cut it, it spreads on the floor. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class USpotLightComponent> ViewLight;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice", meta=(ClampMin="0", Units="cd"))
	float ViewLightIntensity = 1500.f;

	/** The old translucent cone (and its hard floor disc); off by default, the light shows the view. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	bool bShowViewCone = false;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<USceneCaptureComponent2D> Capture;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0", Units="cm"))
	float ViewDistance = 1200.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="1", ClampMax="89", Units="deg"))
	float ViewHalfAngle = 17.f;

	/** The head looks down at the floor by this much (the cone draws a round footprint there). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0", ClampMax="89", Units="deg"))
	float LookDownAngle = 38.f;

	/** Security footage: field of view of the feed (wider than the detection cone, to see the room). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Feed", meta=(ClampMin="20", ClampMax="150", Units="deg"))
	float FeedFOV = 90.f;

	/** The feed looks down only this much (the head looks at its own light pool on the floor). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Feed", meta=(ClampMin="0", ClampMax="89", Units="deg"))
	float FeedLookDownAngle = 15.f;

	/** Fixed exposure of the feed (EV100, the match maps' MatchExposure). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Feed")
	float FeedExposureEV100 = -0.3f;

	/** Total sweep arc (0 = static). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0", ClampMax="350", Units="deg"))
	float SweepAngle = 90.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float SweepSpeed = 0.4f;

	/** Seconds of continuous sight at mid range before the alarm (the meter fills gradually, so you can brush the
	 *  edge of the cone and get out before it goes off). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0", Units="s"))
	float DetectionTime = 3.f;

	/** Meter speed right under the camera (x) and at the end of its view (x): closer fills faster. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float NearDetectionMultiplier = 1.6f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float FarDetectionMultiplier = 0.6f;

	/** Meter lost per second once nobody is in view (it drains, it does not reset at once). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float DetectionDecayRate = 0.3f;

	/** Seconds between two alarms. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="0"))
	float AlarmCooldown = 10.f;

	/** Colour of the cone (its light) while nobody is in view. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	FLinearColor IdleColor = FLinearColor(1.f, 1.f, 1.f, 0.25f);

	/** Colour while it sees someone (the detection meter fills). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	FLinearColor DetectingColor = FLinearColor(1.f, 0.06f, 0.04f, 0.4f);

	/** Colour once it raised the alarm, while it still sees the intruder. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	FLinearColor AlertColor = FLinearColor(1.f, 0.05f, 0.05f, 0.4f);

	/** Seeing someone stops the sweep where the camera looks (it does not follow them); it sweeps again once they
	 *  leave the cone. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera")
	bool bFreezeSweepWhileSeeing = true;

	/** The electronic floor ring (Hitman-like): on by default; the decal material draws it. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	bool bShowFloorRing = false;

	/** The camera's zone is projected by its light (a light function: edge ring, turning dashes, scanlines), so walls
	 *  and obstacles cut it exactly like they cut the light. Scalar parameter "Progress" = detection meter. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	TObjectPtr<UMaterialInterface> ProjectionMaterial;

	/** Visible beam from the lens (volumetric fog scattering of its light, shadowed by walls). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice", meta=(ClampMin="0"))
	float BeamScattering = 6.f;

	/** Health: a few bullets break it. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera", meta=(ClampMin="1"))
	float CameraHealth = 30.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Camera|Juice")
	TObjectPtr<class UNiagaraSystem> BrokenFX;

	UPROPERTY(ReplicatedUsing=OnRep_Visuals)
	bool bBroken = false;

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_Broken();

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> ProjectionInstance;

	float DamageTaken = 0.f;

	/** Detection meter, 0..255 for 0..DetectionTime of continuous sight (replicated: ring fill and colour). */
	UPROPERTY(ReplicatedUsing=OnRep_Visuals)
	uint8 DetectionProgress = 0;

	UPROPERTY(ReplicatedUsing=OnRep_Visuals)
	bool bDisabled = false;

	UPROPERTY(ReplicatedUsing=OnRep_Visuals)
	bool bSeeingIntruder = false;

	UPROPERTY(Replicated)
	bool bRecentlyAlarmed = false;

	/** Server: 0..1 meter (DetectionProgress is its replicated byte). */
	float DetectionMeter = 0.f;

	UPROPERTY(Replicated)
	TObjectPtr<APawn> SeenIntruder;

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
	/** Places the ring decal over the cone's footprint and feeds the material the cone (follows the sweep). */
	void UpdateFootprint();
	/** IdleColor with nobody in view, DetectingColor while it sees someone, AlertColor once it raised the alarm. */
	FLinearColor GetStateColor() const;
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

	/** "NO SIGNAL" over the frozen last frame while the camera is off or broken. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<class UTextRenderComponent> NoSignalText;

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
	TSoftObjectPtr<UMaterialInterface> NoSignalMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Hitmen/Materials/Master/M_Monitor_NoSignal.M_Monitor_NoSignal")));

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
	virtual float GetInteractHoldTime_Implementation(AActor* Interactor) const override { return HoldTime; }

	/** Seconds F must be held to toggle the cameras. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Security", meta=(ClampMin="0", Units="s"))
	float HoldTime = 2.f;

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console")
	TArray<TObjectPtr<AHitmenSecurityCamera>> LinkedCameras;

	/** Screen above the desk, parallel to its front (material slot 0 shows the feed). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Screen;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console")
	FName ScreenTextureParameter = TEXT("ScreenTexture");

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Console")
	TSoftObjectPtr<UMaterialInterface> NoSignalMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Hitmen/Materials/Master/M_Monitor_NoSignal.M_Monitor_NoSignal")));

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
	/** Only its hack panel hacks it (with a hold): the tower itself is not interactable. */
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return false; }
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual void Interact_Implementation(AActor* Interactor) override { Hack(Interactor); }
	virtual bool CanRemoteActivate_Implementation() const override { return !bHacked; }
	virtual void RemoteActivate_Implementation(AActor* Activator) override { Hack(Activator); }

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Turret")
	void Hack(AActor* Hacker);

	UFUNCTION(BlueprintPure, Category="Hitmen|Turret")
	bool IsHacked() const { return bHacked; }

	/** Shot (or blown) to pieces: it sparks, its gun droops and it never fires again. */
	UFUNCTION(BlueprintPure, Category="Hitmen|Turret")
	bool IsBroken() const { return bBroken; }

	virtual float TakeDamage(float DamageAmount, struct FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser) override;

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

	/** How far the turret sees and shoots (cm, in line of sight). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0", Units="cm"))
	float Range = 2500.f;

	/** Whole field of view around the way the turret was placed (180, 270, 360 = all around). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="10", ClampMax="360", Units="deg"))
	float VisionArcDegrees = 180.f;

	/** Damage per bullet. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="0"))
	float Damage = 6.f;

	/** Shots per minute once locked on. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="1"))
	float RoundsPerMinute = 200.f;

	float GetFireInterval() const { return 60.f / FMath::Max(RoundsPerMinute, 1.f); }

	/** Inside the field of view (placement direction, horizontal angle). */
	bool IsInVisionArc(const FVector& Location) const;

	/** Barrels spin up while the turret fires; a tracer per shot; a light flash at the muzzle. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Turret|Juice")
	FLinearColor TracerColor = FLinearColor(1.f, 0.3f, 0.2f);

	float BarrelSpin = 0.f;
	float BarrelSpinSpeed = 0.f;

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

	/** Health: bullets and explosions break it for good (like a camera). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret", meta=(ClampMin="1"))
	float TurretHealth = 80.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Turret|Juice")
	TObjectPtr<UNiagaraSystem> BrokenFX;

	UPROPERTY(ReplicatedUsing=OnRep_Broken)
	bool bBroken = false;

	UFUNCTION() void OnRep_Broken();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnBroken();

	float TurretDamageTaken = 0.f;

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
	virtual float GetInteractHoldTime_Implementation(AActor* Interactor) const override { return HoldTime; }

	/** Hacking takes time: F held this long (a bar fills under the crosshair). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Security", meta=(ClampMin="0", Units="s"))
	float HoldTime = 3.f;

protected:
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|HackPanel")
	TObjectPtr<AHitmenTurret> LinkedTurret;

	virtual void OnInteracted(AActor* Interactor) override;
};

/** A lamp; turned off by its circuit breaker or light switch. Looking at it shows "Light - On / Off" (no key). */
UCLASS(Blueprintable)
class HITMEN_API AHitmenLightFixture : public AActor, public IHitmenInteractable
{
	GENERATED_BODY()

public:
	AHitmenLightFixture();
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	// IHitmenInteractable: info only.
	virtual bool CanInteract_Implementation(AActor* Interactor) const override { return false; }
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override { return FText::GetEmpty(); }
	virtual void Interact_Implementation(AActor* Interactor) override {}
	virtual FText GetInteractionInfo_Implementation(AActor* Interactor) const override;

	UFUNCTION(BlueprintPure, Category="Hitmen|Light")
	bool IsPowered() const { return bPowered; }

	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Light")
	void SetPowered(bool bPowered);

protected:
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UPointLightComponent> Light;

	/** The lamp's look when the power is cut (the bulb goes dark). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Light")
	TObjectPtr<UMaterialInterface> OffMaterial;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInterface> OnMaterial;

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

	UPROPERTY(ReplicatedUsing=OnRep_BreakerPowered)
	bool bPowered = true;

	/** The lever (flips down when the power is cut); the team swaps the mesh. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Hitmen|Components")
	TObjectPtr<UStaticMeshComponent> Lever;

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Hitmen|Breaker|Juice")
	TObjectPtr<class UNiagaraSystem> SwitchFX;

	UFUNCTION()
	void OnRep_BreakerPowered();
	void ApplyLever();

	virtual void OnInteracted(AActor* Interactor) override;
};

/** A wall light switch: a breaker for a room's lamps ("Light switch - On / Off"; F switches them). */
UCLASS(Blueprintable)
class HITMEN_API AHitmenLightSwitch : public AHitmenCircuitBreaker
{
	GENERATED_BODY()

public:
	AHitmenLightSwitch();
	virtual FText GetInteractionText_Implementation(AActor* Interactor) const override;
	virtual FText GetInteractionInfo_Implementation(AActor* Interactor) const override;
};
