// Hitmen — fixtures built from engine cubes (lockers, windows, containers): placement helpers

#pragma once

#include "CoreMinimal.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"

namespace HitmenGeometry
{
	/** Engine cube (100 cm, centred) used by every box-built fixture until the team swaps meshes. */
	inline const TCHAR* CubePath = TEXT("/Engine/BasicShapes/Cube.Cube");

	/** Places a 100 cm cube component as a box: centre and full size in its parent's space (cm). */
	inline void SetBox(UStaticMeshComponent* Box, const FVector& Center, const FVector& Size)
	{
		if (Box)
		{
			Box->SetRelativeLocationAndRotation(Center, FRotator::ZeroRotator);
			Box->SetRelativeScale3D(Size / 100.f);
		}
	}

	/** Gives Target the mesh / material of Source when Target has none (one material drives a whole fixture). */
	inline void InheritLook(UStaticMeshComponent* Target, const UStaticMeshComponent* Source)
	{
		if (!Target || !Source)
		{
			return;
		}
		if (!Target->GetStaticMesh() && Source->GetStaticMesh())
		{
			Target->SetStaticMesh(Source->GetStaticMesh());
		}
		if (Target->OverrideMaterials.IsEmpty() && Source->GetMaterial(0))
		{
			Target->SetMaterial(0, Source->GetMaterial(0));
		}
	}
}
