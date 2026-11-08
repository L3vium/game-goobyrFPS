// Hitmen — character base

#include "Characters/HitmenCharacterBase.h"
#include "Components/HitmenHealthComponent.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Components/HitmenFootstepComponent.h"
#include "Components/HitmenCosmeticComponent.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Framework/HitmenGameModeBase.h"
#include "Framework/HitmenPlayerController.h"
#include "Framework/HitmenGameState.h"
#include "Data/HitmenMatchRules.h"
#include "AI/HitmenAISubsystem.h"
#include "Interaction/HitmenScanRegistry.h"
#include "Components/HitmenBodyCarryComponent.h"
#include "Hitmen.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/DamageEvents.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

AHitmenCharacterBase::AHitmenCharacterBase(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer.SetDefaultSubobjectClass<UHitmenCharacterMovementComponent>(ACharacter::CharacterMovementComponentName))
{
	HealthComponent = CreateDefaultSubobject<UHitmenHealthComponent>(TEXT("Health"));
	StatusEffectComponent = CreateDefaultSubobject<UHitmenStatusEffectComponent>(TEXT("StatusEffects"));
	FootstepComponent = CreateDefaultSubobject<UHitmenFootstepComponent>(TEXT("Footsteps"));
	CosmeticComponent = CreateDefaultSubobject<UHitmenCosmeticComponent>(TEXT("Cosmetics"));

	GetCapsuleComponent()->InitCapsuleSize(34.f, 92.f);
	GetMesh()->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -92.f), FRotator(0.f, -90.f, 0.f));
	GetMesh()->SetCollisionProfileName(TEXT("CharacterMesh"));
	GetMesh()->SetGenerateOverlapEvents(false);
	// Weapon traces must hit the mesh for headshots.
	GetMesh()->SetCollisionResponseToChannel(HITMEN_TRACE_WEAPON, ECR_Block);
	GetCapsuleComponent()->SetCollisionResponseToChannel(HITMEN_TRACE_WEAPON, ECR_Ignore);
}

void AHitmenCharacterBase::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenCharacterBase, bIsVIP);
	DOREPLIFETIME(AHitmenCharacterBase, bVanished);
	DOREPLIFETIME(AHitmenCharacterBase, Carrier);
	DOREPLIFETIME(AHitmenCharacterBase, CarrySocket);
}

void AHitmenCharacterBase::BeginPlay()
{
	Super::BeginPlay();
	HealthComponent->OnDeath.AddDynamic(this, &AHitmenCharacterBase::HandleDeath);
	StatusEffectComponent->OnStatusAdded.AddDynamic(this, &AHitmenCharacterBase::HandleStatusAdded);
	StatusEffectComponent->OnStatusRemoved.AddDynamic(this, &AHitmenCharacterBase::HandleStatusRemoved);
	UHitmenScanRegistry::Register(this);
}

void AHitmenCharacterBase::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UHitmenScanRegistry::Unregister(this);
	Super::EndPlay(EndPlayReason);
}

bool AHitmenCharacterBase::CanInteract_Implementation(AActor* Interactor) const
{
	const UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr;
	return Carry && Carry->CanCarry(this);
}

FText AHitmenCharacterBase::GetInteractionText_Implementation(AActor* Interactor) const
{
	return NSLOCTEXT("Hitmen", "CarryBody", "Carry body");
}

void AHitmenCharacterBase::Interact_Implementation(AActor* Interactor)
{
	if (UHitmenBodyCarryComponent* Carry = Interactor ? Interactor->FindComponentByClass<UHitmenBodyCarryComponent>() : nullptr)
	{
		Carry->PickUp(this);
	}
}

UHitmenCharacterMovementComponent* AHitmenCharacterBase::GetHitmenMovement() const
{
	return Cast<UHitmenCharacterMovementComponent>(GetCharacterMovement());
}

bool AHitmenCharacterBase::IsDead() const
{
	return HealthComponent && HealthComponent->IsDead();
}

float AHitmenCharacterBase::GetMovementSpeedMultiplier() const
{
	return StatusEffectComponent ? StatusEffectComponent->GetSpeedMultiplier() : 1.f;
}

bool AHitmenCharacterBase::IsUnawareOf(const AActor* Other) const
{
	if (!Other)
	{
		return false;
	}
	// Default: unaware when the other is behind us.
	const FVector ToOther = (Other->GetActorLocation() - GetActorLocation()).GetSafeNormal2D();
	return FVector::DotProduct(GetActorForwardVector(), ToOther) < -0.3f;
}

bool AHitmenCharacterBase::CanJumpInternal_Implementation() const
{
	// Allow slide-jumps even though sliding is crouched.
	if (const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement(); Movement && Movement->IsSliding())
	{
		return JumpIsAllowedInternal();
	}
	return Super::CanJumpInternal_Implementation();
}

float AHitmenCharacterBase::TakeDamage(float DamageAmount, FDamageEvent const& DamageEvent, AController* EventInstigator, AActor* DamageCauser)
{
	if (!HasAuthority() || IsDead() || DamageAmount <= 0.f)
	{
		return 0.f;
	}

	AActor* InstigatorActor = EventInstigator ? EventInstigator->GetPawn() : DamageCauser;
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	if (GM && !GM->CanDamage(InstigatorActor, this))
	{
		return 0.f;
	}

	bool bHeadshot = false;
	FVector HitLocation = GetActorLocation();
	if (DamageEvent.IsOfType(FPointDamageEvent::ClassID))
	{
		const FPointDamageEvent& Point = static_cast<const FPointDamageEvent&>(DamageEvent);
		bHeadshot = HeadBones.Contains(Point.HitInfo.BoneName);
		LastHitDirection = Point.ShotDirection;
		LastHitBone = Point.HitInfo.BoneName;
		HitLocation = Point.HitInfo.ImpactPoint;
	}
	else if (DamageEvent.IsOfType(FRadialDamageEvent::ClassID))
	{
		const FRadialDamageEvent& Radial = static_cast<const FRadialDamageEvent&>(DamageEvent);
		LastHitDirection = (GetActorLocation() - Radial.Origin).GetSafeNormal();
		LastHitBone = NAME_None;
	}

	float Damage = Super::TakeDamage(DamageAmount, DamageEvent, EventInstigator, DamageCauser);
	Damage *= StatusEffectComponent->GetIncomingDamageMultiplier();
	if (const AHitmenCharacterBase* InstigatorCharacter = Cast<AHitmenCharacterBase>(InstigatorActor))
	{
		Damage *= InstigatorCharacter->GetStatusEffects()->GetOutgoingDamageMultiplier();
		// NPC shots scale with the difficulty chosen in the lobby.
		if (!InstigatorCharacter->IsPlayerControlled() && GM)
		{
			if (const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>())
			{
				Damage *= GM->GetRules()->GetPreset(GS->GetLobbySettings().Difficulty).NPCDamageMultiplier;
			}
		}
	}

	const float Dealt = HealthComponent->ApplyDamage(Damage, InstigatorActor, DamageCauser, bHeadshot);
	if (Dealt <= 0.f)
	{
		return 0.f;
	}

	OnDamaged(Dealt, InstigatorActor, bHeadshot);
	Multicast_DamageFeedback(Dealt, bHeadshot, HitLocation);

	if (AHitmenPlayerController* InstigatorPC = Cast<AHitmenPlayerController>(EventInstigator))
	{
		InstigatorPC->Client_HitConfirmed(IsDead(), bHeadshot);
	}
	if (AHitmenPlayerController* VictimPC = Cast<AHitmenPlayerController>(GetController()))
	{
		VictimPC->Client_DamageTaken(InstigatorActor ? InstigatorActor->GetActorLocation() : HitLocation);
	}
	return Dealt;
}

void AHitmenCharacterBase::OnDamaged(float Damage, AActor* InstigatorActor, bool bHeadshot)
{
	// Any damage wakes a sleeper.
	StatusEffectComponent->RemoveStatus(EHitmenStatusEffect::Asleep);
}

void AHitmenCharacterBase::Multicast_DamageFeedback_Implementation(float Damage, bool bHeadshot, FVector_NetQuantize HitLocation)
{
	Juice_OnDamaged(Damage, bHeadshot, HitLocation);
}

void AHitmenCharacterBase::HandleDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot)
{
	GetCharacterMovement()->DisableMovement();
	GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore);
	GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	if (!Carrier)
	{
		EnableRagdoll();
	}

	if (HasAuthority())
	{
		StatusEffectComponent->ClearAll();
		AController* KillerController = nullptr;
		if (const APawn* KillerPawn = Cast<APawn>(Killer))
		{
			KillerController = KillerPawn->GetController();
		}
		if (AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>())
		{
			GM->OnCharacterKilled(this, KillerController, DamageCauser, bHeadshot);
		}
		UHitmenAISubsystem::ReportKill(this, this, Killer);
	}
	Juice_OnDeath(bHeadshot);
	OnBodyStateChanged.Broadcast(this);
}

void AHitmenCharacterBase::EnableRagdoll()
{
	USkeletalMeshComponent* MeshComp = GetMesh();
	MeshComp->SetCollisionProfileName(TEXT("Ragdoll"));
	MeshComp->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	MeshComp->SetSimulatePhysics(true);
	MeshComp->WakeAllRigidBodies();
	if (!LastHitDirection.IsNearlyZero())
	{
		MeshComp->AddImpulse(LastHitDirection * DeathImpulse, LastHitBone, true);
	}
}

void AHitmenCharacterBase::HandleStatusAdded(EHitmenStatusEffect Status, AActor* StatusInstigator)
{
	Juice_OnStatusAdded(Status);
}

void AHitmenCharacterBase::HandleStatusRemoved(EHitmenStatusEffect Status, AActor* StatusInstigator)
{
	Juice_OnStatusRemoved(Status);
}

bool AHitmenCharacterBase::IsDiscoverableBody() const
{
	return IsDead() && !bVanished && !Carrier;
}

void AHitmenCharacterBase::VanishBody()
{
	if (HasAuthority() && IsDead() && !bVanished)
	{
		bVanished = true;
		OnRep_Body();
	}
}

void AHitmenCharacterBase::SetCarriedBy(APawn* NewCarrier, FName AttachSocket)
{
	if (!HasAuthority() || !IsDead())
	{
		return;
	}
	Carrier = NewCarrier;
	CarrySocket = AttachSocket;
	OnRep_Body();
}

void AHitmenCharacterBase::OnRep_Body()
{
	ApplyBodyState();
	Juice_OnBodyStateChanged(bVanished, Carrier != nullptr);
	OnBodyStateChanged.Broadcast(this);
}

void AHitmenCharacterBase::ApplyBodyState()
{
	USkeletalMeshComponent* MeshComp = GetMesh();
	if (bVanished)
	{
		SetActorHiddenInGame(true);
		SetActorEnableCollision(false);
		MeshComp->SetSimulatePhysics(false);
		return;
	}
	if (Carrier)
	{
		MeshComp->SetSimulatePhysics(false);
		MeshComp->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		USceneComponent* AttachParent = Carrier->FindComponentByClass<USkeletalMeshComponent>();
		if (const ACharacter* CarrierCharacter = Cast<ACharacter>(Carrier))
		{
			AttachParent = CarrierCharacter->GetMesh();
		}
		if (AttachParent)
		{
			AttachToComponent(AttachParent, FAttachmentTransformRules::SnapToTargetNotIncludingScale, CarrySocket);
		}
		MeshComp->AttachToComponent(GetCapsuleComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
		MeshComp->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -92.f), FRotator(0.f, -90.f, 0.f));
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		return;
	}
	// Dropped: back to a ragdoll where the carrier stands.
	if (GetAttachParentActor())
	{
		DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
	}
	GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::QueryOnly);
	MeshComp->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
	if (IsDead())
	{
		EnableRagdoll();
	}
}

EHitmenScanCategory AHitmenCharacterBase::GetScanCategory_Implementation(const AActor* Observer) const
{
	if (bVanished)
	{
		return EHitmenScanCategory::None;
	}
	if (bIsVIP)
	{
		return EHitmenScanCategory::Target;
	}
	if (IsDead())
	{
		return EHitmenScanCategory::None;
	}
	return UHitmenTeamLibrary::AreHostile(Observer, this) ? EHitmenScanCategory::Enemy : EHitmenScanCategory::None;
}

void AHitmenCharacterBase::SetScanHighlight_Implementation(bool bEnabled, int32 StencilValue)
{
	TArray<UPrimitiveComponent*> Primitives = CosmeticComponent->GetCosmeticPrimitives();
	Primitives.Add(GetMesh());
	for (UPrimitiveComponent* Primitive : Primitives)
	{
		if (Primitive)
		{
			Primitive->SetRenderCustomDepth(bEnabled);
			Primitive->SetCustomDepthStencilValue(bEnabled ? StencilValue : 0);
		}
	}
}
