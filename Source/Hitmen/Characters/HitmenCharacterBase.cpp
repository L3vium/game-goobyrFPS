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
#include "Inventory/HitmenInventoryComponent.h"
#include "Data/HitmenAnimSet.h"
#include "Hitmen.h"
#include "Animation/AnimInstance.h"
#include "Animation/AnimMontage.h"
#include "TimerManager.h"
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
	DOREPLIFETIME(AHitmenCharacterBase, DeathMontageIndex);
}

void AHitmenCharacterBase::BeginPlay()
{
	Super::BeginPlay();
	HealthComponent->OnDeath.AddDynamic(this, &AHitmenCharacterBase::HandleDeath);
	StatusEffectComponent->OnStatusAdded.AddDynamic(this, &AHitmenCharacterBase::HandleStatusAdded);
	StatusEffectComponent->OnStatusRemoved.AddDynamic(this, &AHitmenCharacterBase::HandleStatusRemoved);
	// Cosmetic montages from gameplay events (anim set).
	if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		Movement->OnMantleStarted.AddDynamic(this, &AHitmenCharacterBase::HandleMantleStarted);
	}
	if (UHitmenInventoryComponent* Inventory = FindComponentByClass<UHitmenInventoryComponent>())
	{
		Inventory->OnWeaponThrown.AddDynamic(this, &AHitmenCharacterBase::HandleWeaponThrown);
	}
	UHitmenScanRegistry::Register(this);
}

void AHitmenCharacterBase::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	GetWorldTimerManager().ClearTimer(DeathRagdollTimer);
	UHitmenScanRegistry::Unregister(this);
	Super::EndPlay(EndPlayReason);
}

void AHitmenCharacterBase::NotifyControllerChanged()
{
	Super::NotifyControllerChanged();
	if (UHitmenInventoryComponent* Inventory = FindComponentByClass<UHitmenInventoryComponent>())
	{
		Inventory->RefreshWeaponAttachments();
	}
}

// ---- Montages ----

void AHitmenCharacterBase::GetMontageMeshes(TArray<USkeletalMeshComponent*>& OutMeshes) const
{
	OutMeshes.Add(GetMesh());
}

float AHitmenCharacterBase::PlayCosmeticMontage(UAnimMontage* Montage, float PlayRate)
{
	if (!Montage || GetNetMode() == NM_DedicatedServer)
	{
		return 0.f;
	}
	TArray<USkeletalMeshComponent*> Meshes;
	GetMontageMeshes(Meshes);
	float Length = 0.f;
	for (USkeletalMeshComponent* MeshComp : Meshes)
	{
		// A montage whose skeleton does not match a mesh simply does not play there.
		if (UAnimInstance* AnimInstance = MeshComp ? MeshComp->GetAnimInstance() : nullptr)
		{
			const float Played = AnimInstance->Montage_Play(Montage, PlayRate);
			Length = Length > 0.f ? Length : Played;
		}
	}
	return Length;
}

float AHitmenCharacterBase::PlayAnimSetMontageLocal(EHitmenAnimMontage Montage, int32 Variant)
{
	return AnimSet ? PlayCosmeticMontage(AnimSet->GetMontage(Montage, Variant)) : 0.f;
}

void AHitmenCharacterBase::PlayAnimSetMontage(EHitmenAnimMontage Montage, int32 Variant)
{
	if (HasAuthority())
	{
		Multicast_PlayAnimSetMontage(Montage, static_cast<uint8>(FMath::Clamp(Variant, 0, static_cast<int32>(MAX_uint8))), false);
	}
}

void AHitmenCharacterBase::Multicast_PlayAnimSetMontage_Implementation(EHitmenAnimMontage Montage, uint8 Variant, bool bOwnerPredicted)
{
	if (bOwnerPredicted && (HasAuthority() || IsLocallyControlled()))
	{
		return;
	}
	PlayAnimSetMontageLocal(Montage, Variant);
}

void AHitmenCharacterBase::HandleMantleStarted(bool bIsVault)
{
	const EHitmenAnimMontage Montage = bIsVault ? EHitmenAnimMontage::Vault : EHitmenAnimMontage::Mantle;
	// Client move replays can fire the event again: never restart a running traversal montage.
	const UAnimInstance* AnimInstance = GetMesh() ? GetMesh()->GetAnimInstance() : nullptr;
	UAnimMontage* Asset = AnimSet ? AnimSet->GetMontage(Montage) : nullptr;
	if (!Asset || (AnimInstance && AnimInstance->Montage_IsPlaying(Asset)))
	{
		return;
	}
	PlayCosmeticMontage(Asset);
	// The event runs on the owning client and the server; simulated proxies get it from here.
	if (HasAuthority())
	{
		Multicast_PlayAnimSetMontage(Montage, 0, true);
	}
}

void AHitmenCharacterBase::HandleWeaponThrown(UHitmenWeaponData* Weapon)
{
	PlayAnimSetMontageLocal(EHitmenAnimMontage::Throw);
}

int32 AHitmenCharacterBase::ChooseDeathMontageIndex() const
{
	const int32 Count = AnimSet ? AnimSet->DeathMontages.Num() : 0;
	if (Count <= 0)
	{
		return INDEX_NONE;
	}
	constexpr int32 DirectionalCount = 4; // [Front, Back, Left, Right], see UHitmenAnimSet::DeathMontages
	const FVector From = -LastHitDirection.GetSafeNormal2D();
	if (Count != DirectionalCount || From.IsNearlyZero())
	{
		return FMath::RandRange(0, Count - 1);
	}
	const float Front = FVector::DotProduct(GetActorForwardVector(), From);
	const float Right = FVector::DotProduct(GetActorRightVector(), From);
	if (FMath::Abs(Front) >= FMath::Abs(Right))
	{
		return Front >= 0.f ? 0 : 1;
	}
	return Right < 0.f ? 2 : 3;
}

float AHitmenCharacterBase::PlayDeathMontage()
{
	UAnimInstance* AnimInstance = GetMesh() ? GetMesh()->GetAnimInstance() : nullptr;
	if (!bPlayDeathMontage || !AnimSet || !AnimInstance || GetNetMode() == NM_DedicatedServer)
	{
		return 0.f;
	}
	// A stealth kill victim montage is the death animation: let it finish, then ragdoll.
	if (UAnimMontage* Victim = AnimSet->StealthKillVictimMontage; Victim && AnimInstance->Montage_IsPlaying(Victim))
	{
		const float Remaining = Victim->GetPlayLength() - AnimInstance->Montage_GetPosition(Victim);
		return FMath::Clamp(Remaining, 0.f, DeathRagdollMaxDelay);
	}
	const int32 Index = AnimSet->DeathMontages.IsValidIndex(DeathMontageIndex) ? DeathMontageIndex : 0;
	UAnimMontage* Montage = AnimSet->GetMontage(EHitmenAnimMontage::Death, Index);
	if (!Montage || PlayCosmeticMontage(Montage) <= 0.f)
	{
		return 0.f;
	}
	// Ragdoll before the montage blends back to the living pose.
	return FMath::Clamp(Montage->GetPlayLength() - Montage->GetDefaultBlendOutTime(), 0.f, DeathRagdollMaxDelay);
}

void AHitmenCharacterBase::HandleDeathRagdollTimer()
{
	if (IsDead() && !Carrier && !bVanished)
	{
		EnableRagdoll();
	}
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
	// Existing cosmetic multicast: the hit react rides on it.
	if (!IsDead())
	{
		const UAnimInstance* AnimInstance = GetMesh() ? GetMesh()->GetAnimInstance() : nullptr;
		const UAnimMontage* HitReact = AnimSet ? AnimSet->GetMontage(EHitmenAnimMontage::HitReact) : nullptr;
		const bool bBusy = AnimInstance && AnimInstance->IsAnyMontagePlaying() && !(HitReact && AnimInstance->Montage_IsPlaying(HitReact));
		if (bHitReactInterruptsMontages || !bBusy)
		{
			PlayAnimSetMontageLocal(EHitmenAnimMontage::HitReact);
		}
	}
	Juice_OnDamaged(Damage, bHeadshot, HitLocation);
}

void AHitmenCharacterBase::HandleDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot)
{
	GetCharacterMovement()->DisableMovement();
	GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore);
	GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	if (HasAuthority())
	{
		DeathMontageIndex = ChooseDeathMontageIndex();
	}
	if (!Carrier)
	{
		// Death montage first (every machine), then the ragdoll.
		const float RagdollDelay = PlayDeathMontage();
		if (RagdollDelay > 0.f)
		{
			GetWorldTimerManager().SetTimer(DeathRagdollTimer, this, &AHitmenCharacterBase::HandleDeathRagdollTimer, RagdollDelay, false);
		}
		else
		{
			EnableRagdoll();
		}
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
	if (bVanished || Carrier)
	{
		GetWorldTimerManager().ClearTimer(DeathRagdollTimer);
	}
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
