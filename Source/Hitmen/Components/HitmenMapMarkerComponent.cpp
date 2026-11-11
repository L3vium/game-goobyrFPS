// Hitmen — map markers

#include "Components/HitmenMapMarkerComponent.h"
#include "Data/HitmenMapMarkerType.h"
#include "Engine/Texture2D.h"
#include "Engine/World.h"
#include "GameFramework/Actor.h"

UHitmenMapMarkerComponent::UHitmenMapMarkerComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
}

FVector UHitmenMapMarkerComponent::GetMarkerLocation() const
{
	return GetOwner() ? GetOwner()->GetActorLocation() : FVector::ZeroVector;
}

void UHitmenMapMarkerComponent::BeginPlay()
{
	Super::BeginPlay();
	if (UHitmenMapMarkerSubsystem* Markers = UHitmenMapMarkerSubsystem::Get(this))
	{
		Markers->Register(this);
	}
}

void UHitmenMapMarkerComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (UHitmenMapMarkerSubsystem* Markers = UHitmenMapMarkerSubsystem::Get(this))
	{
		Markers->Unregister(this);
	}
	Super::EndPlay(EndPlayReason);
}

// ---------------------------------------------------------------------------------------------

UHitmenMapMarkerSubsystem* UHitmenMapMarkerSubsystem::Get(const UObject* WorldContext)
{
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	return World ? World->GetSubsystem<UHitmenMapMarkerSubsystem>() : nullptr;
}

void UHitmenMapMarkerSubsystem::Register(UHitmenMapMarkerComponent* Marker)
{
	if (!Marker)
	{
		return;
	}
	Markers.AddUnique(Marker);
	UHitmenMapMarkerType* Type = Marker->GetType();
	if (Type && Type->Icon && !IconBrushes.Contains(Type))
	{
		FSlateBrush Brush;
		Brush.SetResourceObject(Type->Icon);
		Brush.ImageSize = FVector2D(Type->Icon->GetSizeX(), Type->Icon->GetSizeY());
		IconBrushes.Add(Type, Brush);
	}
}

void UHitmenMapMarkerSubsystem::Unregister(UHitmenMapMarkerComponent* Marker)
{
	Markers.Remove(Marker);
}

TArray<UHitmenMapMarkerType*> UHitmenMapMarkerSubsystem::GetLegendTypes() const
{
	TArray<UHitmenMapMarkerType*> Types;
	for (const TWeakObjectPtr<UHitmenMapMarkerComponent>& Marker : Markers)
	{
		UHitmenMapMarkerType* Type = Marker.IsValid() ? Marker->GetType() : nullptr;
		if (Type && Type->bShowInLegend && Type->bShowOnPauseMap)
		{
			Types.AddUnique(Type);
		}
	}
	Types.Sort([](const UHitmenMapMarkerType& A, const UHitmenMapMarkerType& B)
	{
		return A.LegendOrder != B.LegendOrder ? A.LegendOrder < B.LegendOrder : A.DisplayName.CompareTo(B.DisplayName) < 0;
	});
	return Types;
}

const FSlateBrush* UHitmenMapMarkerSubsystem::GetIconBrush(const UHitmenMapMarkerType* Type) const
{
	return Type ? IconBrushes.Find(Type) : nullptr;
}
