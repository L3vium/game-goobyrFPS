// Hitmen — retrieval point

#include "World/Spawning/HitmenRetrievalPoint.h"
#include "Core/HitmenText.h"
#include "Components/HitmenMapMarkerComponent.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Framework/HitmenMatchGameMode.h"
#include "Framework/HitmenPlayerState.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Camera/CameraComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "Hitmen.h"

AHitmenRetrievalPoint::AHitmenRetrievalPoint()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	bReplicates = true;
	DeliveryZone = CreateDefaultSubobject<UBoxComponent>(TEXT("DeliveryZone"));
	DeliveryZone->SetBoxExtent(FVector(160.f, 160.f, 120.f));
	// Only the F trace: walking in no longer delivers (the body is extracted with F).
	DeliveryZone->SetCollisionProfileName(TEXT("NoCollision"));
	DeliveryZone->SetCollisionEnabled(ECollisionEnabled::QueryOnly);
	DeliveryZone->SetCollisionResponseToAllChannels(ECR_Ignore);
	DeliveryZone->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	SetRootComponent(DeliveryZone);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(DeliveryZone);

	Model = CreateDefaultSubobject<USceneComponent>(TEXT("Model"));
	Model->SetupAttachment(DeliveryZone);

	CinematicCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("CinematicCamera"));
	CinematicCamera->SetupAttachment(DeliveryZone);
	CinematicCamera->SetRelativeLocationAndRotation(FVector(-1100.f, 750.f, 520.f), FRotator(-16.f, -34.f, 0.f));
	CinematicCamera->SetFieldOfView(70.f);

	MapMarker = CreateDefaultSubobject<UHitmenMapMarkerComponent>(TEXT("MapMarker"));
	DisplayName = HITMEN_TEXT("Hitmen.RetrievalPoint", "Retrieval point");
}

void AHitmenRetrievalPoint::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenRetrievalPoint, bActive);
}

void AHitmenRetrievalPoint::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	BuildModel();
}

void AHitmenRetrievalPoint::BuildModel()
{
	for (UStaticMeshComponent* Part : PartComponents)
	{
		if (Part)
		{
			Part->DestroyComponent();
		}
	}
	PartComponents.Reset();
	SpinningParts.Reset();
	for (int32 Index = 0; Index < ModelParts.Num(); ++Index)
	{
		const FHitmenModelPart& Desc = ModelParts[Index];
		if (!Desc.Mesh)
		{
			continue;
		}
		UStaticMeshComponent* Part = NewObject<UStaticMeshComponent>(this, MakeUniqueObjectName(this, UStaticMeshComponent::StaticClass(), *FString::Printf(TEXT("ModelPart%d"), Index)));
		Part->CreationMethod = EComponentCreationMethod::UserConstructionScript;
		Part->SetupAttachment(Model);
		Part->SetStaticMesh(Desc.Mesh);
		if (Desc.Material)
		{
			Part->SetMaterial(0, Desc.Material);
		}
		Part->SetRelativeTransform(Desc.Transform);
		Part->SetCollisionProfileName(TEXT("BlockAllDynamic"));
		Part->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
		Part->RegisterComponent();
		PartComponents.Add(Part);
		if (Desc.bSpins)
		{
			SpinningParts.Add(Part);
		}
	}
}

void AHitmenRetrievalPoint::BeginPlay()
{
	Super::BeginPlay();
	// Spawned at runtime or loaded without its parts: build the model now.
	int32 Wanted = 0;
	for (const FHitmenModelPart& Desc : ModelParts)
	{
		Wanted += Desc.Mesh ? 1 : 0;
	}
	PartComponents.RemoveAll([](const UStaticMeshComponent* Part) { return !IsValid(Part); });
	if (PartComponents.Num() != Wanted)
	{
		BuildModel();
	}
	else
	{
		SpinningParts.Reset();
		int32 PartIndex = 0;
		for (const FHitmenModelPart& Desc : ModelParts)
		{
			if (Desc.Mesh)
			{
				if (Desc.bSpins)
				{
					SpinningParts.Add(PartComponents[PartIndex]);
				}
				++PartIndex;
			}
		}
	}
	MapMarker->SetMarkerVisible(bActive);
	OnRep_Active();
}

void AHitmenRetrievalPoint::SetActive(bool bInActive)
{
	bActive = bInActive;
	OnRep_Active();
}

void AHitmenRetrievalPoint::OnRep_Active()
{
	if (bHideWhenInactive)
	{
		SetActorHiddenInGame(!bActive);
		SetActorEnableCollision(bActive);
	}
	MapMarker->SetMarkerVisible(bActive);
	Juice_OnActiveChanged(bActive);
}

bool AHitmenRetrievalPoint::CanInteract_Implementation(AActor* Interactor) const
{
	const APawn* Pawn = Cast<APawn>(Interactor);
	const UHitmenBodyCarryComponent* Carry = Pawn ? Pawn->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	const AHitmenCharacterBase* Body = Carry ? Carry->GetCarriedBody() : nullptr;
	if (!bActive || DepartureStart >= 0.f || !Body || !Body->IsVIP())
	{
		return false;
	}
	// Only an attacking team extracts (Protect teams defend the VIP). Players without a team play solo: Attack.
	const AHitmenPlayerState* PS = Pawn->GetPlayerState<AHitmenPlayerState>();
	return !PS || PS->GetTeamRole() == EHitmenTeamRole::Attack;
}

FText AHitmenRetrievalPoint::GetInteractionText_Implementation(AActor* Interactor) const
{
	return HITMEN_TEXT("Hitmen.ExtractTarget", "Extract the target");
}

FText AHitmenRetrievalPoint::GetInteractionInfo_Implementation(AActor* Interactor) const
{
	if (!bActive)
	{
		return FText::GetEmpty();
	}
	const UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	const AHitmenCharacterBase* Body = Carry ? Carry->GetCarriedBody() : nullptr;
	return Body && Body->IsVIP() ? DisplayName
		: FText::Format(HITMEN_TEXT("Hitmen.RetrievalBring", "{0}  -  bring the target's body here"), DisplayName);
}

void AHitmenRetrievalPoint::Interact_Implementation(AActor* Interactor)
{
	APawn* Pawn = Cast<APawn>(Interactor);
	if (!HasAuthority() || !Pawn || !IHitmenInteractable::Execute_CanInteract(this, Interactor) || GetWorld()->GetTimeSeconds() - LastInteractTime < 0.5f)
	{
		return;
	}
	LastInteractTime = GetWorld()->GetTimeSeconds();
	if (AHitmenMatchGameMode* GM = GetWorld()->GetAuthGameMode<AHitmenMatchGameMode>())
	{
		GM->OnVIPDelivered(Pawn, this);
	}
}

void AHitmenRetrievalPoint::PlayDeparture()
{
	if (DepartureStart >= 0.f)
	{
		return;
	}
	DepartureStart = GetWorld()->GetTimeSeconds();
	SetActorTickEnabled(true);
	// The cinematic camera never films a wall: pulled in toward the model when something is in between.
	const FVector Focus = Model->GetComponentLocation() + FVector(0.f, 0.f, 150.f);
	const FVector Wanted = CinematicCamera->GetComponentLocation();
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenExtractionCamera), false, this);
	if (GetWorld()->SweepSingleByChannel(Hit, Focus, Wanted, FQuat::Identity, ECC_Visibility, FCollisionShape::MakeSphere(20.f), Params))
	{
		CinematicCamera->SetWorldLocation(Hit.Location);
		CinematicCamera->SetWorldRotation((Focus - Hit.Location).Rotation());
	}
	Juice_OnExtraction();
}

void AHitmenRetrievalPoint::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (DepartureStart < 0.f)
	{
		return;
	}
	const float Age = GetWorld()->GetTimeSeconds() - DepartureStart;
	for (UStaticMeshComponent* Part : SpinningParts)
	{
		if (Part)
		{
			// Rotors spool up over the delay.
			Part->AddLocalRotation(FRotator(0.f, SpinSpeed * FMath::Clamp(Age / FMath::Max(DepartureDelay, 0.1f), 0.2f, 1.f) * DeltaSeconds, 0.f));
		}
	}
	const float T = FMath::Max(Age - DepartureDelay, 0.f);
	const float Travel = 0.5f * DepartureAcceleration * T * T;
	switch (DepartureStyle)
	{
	case EHitmenDepartureStyle::LiftOff:
		// Up first, then forward and a slight nose-down tilt, like a helicopter leaving.
		Model->SetRelativeLocationAndRotation(FVector(Travel * 0.6f, 0.f, Travel), FRotator(-FMath::Min(T * 6.f, 12.f), 0.f, 0.f));
		break;
	case EHitmenDepartureStyle::DriveAway:
		Model->SetRelativeLocation(FVector(Travel, 0.f, 0.f));
		break;
	default:
		break;
	}
	if (Age > CinematicDuration + 2.f)
	{
		SetActorTickEnabled(false);
	}
}

void AHitmenRetrievalPoint::SetMarkerHighlight(bool bEnabled)
{
	TArray<UPrimitiveComponent*> Primitives;
	GetComponents(Primitives);
	for (UPrimitiveComponent* Primitive : Primitives)
	{
		if (Primitive == DeliveryZone)
		{
			continue;
		}
		if (Primitive->bRenderCustomDepth != bEnabled || Primitive->CustomDepthStencilValue != (bEnabled ? HitmenStencil::RetrievalMarker : 0))
		{
			Primitive->SetRenderCustomDepth(bEnabled);
			Primitive->SetCustomDepthStencilValue(bEnabled ? HitmenStencil::RetrievalMarker : 0);
		}
	}
}
