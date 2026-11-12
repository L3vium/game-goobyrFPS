// Hitmen — small interfaces that decouple systems (input, climbing, sightroom, world fixtures)

#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "Core/HitmenTypes.h"
#include "HitmenInterfaces.generated.h"

class AHitmenCharacterBase;
struct FInputActionValue;

// ---------------------------------------------------------------------------------------------
// Input: the PlayerController owns every binding and forwards to the possessed pawn through this
// interface, so any 3C Blueprint / vehicle / spectator pawn can be swapped without rebinding.
// ---------------------------------------------------------------------------------------------
UINTERFACE(MinimalAPI, meta=(CannotImplementInterfaceInBlueprint))
class UHitmenInputReceiver : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenInputReceiver
{
	GENERATED_BODY()

public:
	virtual void Input_Move(const FVector2D& Axis) {}
	virtual void Input_Look(const FVector2D& Axis) {}
	virtual void Input_JumpPressed() {}
	virtual void Input_JumpReleased() {}
	virtual void Input_CrouchChanged(bool bWantsCrouch) {}
	virtual void Input_SprintChanged(bool bWantsSprint) {}
	/** Sprint pressed and released quickly (a tap): the dash. */
	virtual void Input_SprintTapped() {}
	virtual void Input_FirePressed() {}
	virtual void Input_FireReleased() {}
	virtual void Input_AimChanged(bool bWantsAim) {}
	virtual void Input_Interact() {}
	virtual void Input_MoveAbilityPressed() {}
	virtual void Input_MoveAbilityReleased() {}
	virtual void Input_ManipulationAbilityPressed() {}
	virtual void Input_ManipulationAbilityReleased() {}
	virtual void Input_SwitchSlot(float Direction) {}
	virtual void Input_SightroomChanged(bool bActive) {}
	virtual void Input_Emote(int32 EmoteIndex) {}
	/** Current crouch/sprint wish, used by the controller for toggle mode. */
	virtual bool Input_IsCrouchWanted() const { return false; }
	virtual bool Input_IsSprintWanted() const { return false; }
};

// ---------------------------------------------------------------------------------------------
// Climbing: ladders, gutters and ziplines. The movement component only knows this interface.
// ---------------------------------------------------------------------------------------------
UENUM(BlueprintType)
enum class EHitmenClimbType : uint8
{
	Ladder,
	Zipline
};

UINTERFACE(MinimalAPI, BlueprintType, meta=(CannotImplementInterfaceInBlueprint))
class UHitmenClimbable : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenClimbable
{
	GENERATED_BODY()

public:
	virtual EHitmenClimbType GetClimbType() const = 0;
	/** World start (bottom for ladders) and end (top) of the climb axis. */
	virtual FVector GetClimbStart() const = 0;
	virtual FVector GetClimbEnd() const = 0;
	/** Outward normal of the climb surface (ladders) — the side the climber stands on. */
	virtual FVector GetClimbNormal() const { return FVector::ForwardVector; }
	/** Distance from the axis the climber's capsule centre keeps. */
	virtual float GetClimbOffset() const { return 45.f; }
	virtual float GetClimbSpeed() const { return 300.f; }
	/** Permissive facing check: dot(view, -normal) must exceed this to grab a ladder. */
	virtual float GetMinFacingDot() const { return 0.2f; }
};

// ---------------------------------------------------------------------------------------------
// Sightroom: actors shown by the Tab scan.
// ---------------------------------------------------------------------------------------------
UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenScannable : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenScannable
{
	GENERATED_BODY()

public:
	/** Category for the observer (an enemy for one player may be a teammate for another). */
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|Sightroom")
	EHitmenScanCategory GetScanCategory(const AActor* Observer) const;

	/** Primitives that receive the custom-depth highlight. */
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|Sightroom")
	void SetScanHighlight(bool bEnabled, int32 StencilValue);
};

// ---------------------------------------------------------------------------------------------
// World fixture contracts.
// ---------------------------------------------------------------------------------------------

/** Doors, windows, vent hatches: a room is sealed when all its openings are sealed. */
UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenSealable : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenSealable
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	bool IsSealed() const;
};

/** Things that accept a poison vial (drinks, vent hatches). */
UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenPoisonable : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenPoisonable
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	bool CanBePoisoned() const;

	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	void ApplyPoison(AActor* Poisoner);
};

/** Distractions, panels and consoles that the Remote Distraction ability can trigger from afar. */
UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenRemoteActivatable : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenRemoteActivatable
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	bool CanRemoteActivate() const;

	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	void RemoteActivate(AActor* Activator);
};

/** Receives explosions (breakable walls, barrels, mines). */
UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenExplosionReceiver : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenExplosionReceiver
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	void ReceiveExplosion(const FVector& Origin, float Radius, AActor* InstigatorActor);
};

/** Places an NPC can use during its routine (drinks, terminals, benches). */
UINTERFACE(MinimalAPI, BlueprintType)
class UHitmenNPCUsable : public UInterface
{
	GENERATED_BODY()
};

class HITMEN_API IHitmenNPCUsable
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	float GetUseDuration() const;

	UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category="Hitmen|World")
	void NPCUse(AHitmenCharacterBase* NPC);
};
