// Hitmen — map markers: a component on any actor, a world subsystem the maps read

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Subsystems/WorldSubsystem.h"
#include "Styling/SlateBrush.h"
#include "HitmenMapMarkerComponent.generated.h"

class UHitmenMapMarkerType;

/**
 * Shows its actor on the minimap and the pause map, drawn and named by its Type (a UHitmenMapMarkerType data asset).
 * Add it to a Blueprint, pick the Type: nothing else to do. Gameplay code hides it with SetMarkerVisible (a disabled
 * camera, an unused retrieval point). Not replicated: every machine runs the same visibility logic.
 */
UCLASS(ClassGroup=(Hitmen), meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenMapMarkerComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenMapMarkerComponent();

	UFUNCTION(BlueprintCallable, Category="Hitmen|Map Marker")
	void SetMarkerVisible(bool bVisible) { bMarkerVisible = bVisible; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Map Marker")
	bool IsMarkerVisible() const { return bMarkerVisible && Type != nullptr; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Map Marker")
	UHitmenMapMarkerType* GetType() const { return Type; }

	const FText& GetLabel() const { return Label; }

	FVector GetMarkerLocation() const;

protected:
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Map Marker")
	TObjectPtr<UHitmenMapMarkerType> Type;

	/** Optional text next to the marker on the pause map. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Map Marker")
	FText Label;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Hitmen|Map Marker")
	bool bMarkerVisible = true;
};

/** Registry of the map markers in a world: the minimap, the pause map and its legend read it. */
UCLASS()
class HITMEN_API UHitmenMapMarkerSubsystem : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	static UHitmenMapMarkerSubsystem* Get(const UObject* WorldContext);

	void Register(UHitmenMapMarkerComponent* Marker);
	void Unregister(UHitmenMapMarkerComponent* Marker);

	/** Every registered marker (hidden ones included; check IsMarkerVisible). */
	const TArray<TWeakObjectPtr<UHitmenMapMarkerComponent>>& GetMarkers() const { return Markers; }

	/** Marker kinds present in this world that want a legend row, in legend order. */
	TArray<UHitmenMapMarkerType*> GetLegendTypes() const;

	/** Brush of a kind's icon, or null when it draws a shape. */
	const FSlateBrush* GetIconBrush(const UHitmenMapMarkerType* Type) const;

protected:
	TArray<TWeakObjectPtr<UHitmenMapMarkerComponent>> Markers;

	/** UPROPERTY so GC keeps the icons while a brush points at them. */
	UPROPERTY(Transient)
	TMap<TObjectPtr<UHitmenMapMarkerType>, FSlateBrush> IconBrushes;
};
