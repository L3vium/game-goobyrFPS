// Hitmen — weapon base

#include "Weapons/HitmenWeaponBase.h"
#include "Weapons/HitmenCombatLibrary.h"
#include "Data/HitmenWeaponData.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "Characters/HitmenCharacterBase.h"
#include "Characters/HitmenPlayerCharacter.h"
#include "AI/HitmenAISubsystem.h"
#include "Components/StaticMeshComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/SkeletalMesh.h"
#include "NiagaraFunctionLibrary.h"
#include "NiagaraComponent.h"
#include "Kismet/GameplayStatics.h"
#include "GameFramework/PlayerController.h"
#include "Net/UnrealNetwork.h"
#include "TimerManager.h"
#include "Engine/World.h"

AHitmenWeaponBase::AHitmenWeaponBase()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;
	bReplicates = true;
	SetReplicatingMovement(false);

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);

	StaticMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("StaticMesh"));
	StaticMesh->SetupAttachment(Root);
	StaticMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	SkeletalMesh = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("SkeletalMesh"));
	SkeletalMesh->SetupAttachment(Root);
	SkeletalMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void AHitmenWeaponBase::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenWeaponBase, WeaponData);
	DOREPLIFETIME(AHitmenWeaponBase, OwnerPawn);
}

void AHitmenWeaponBase::InitWeapon(UHitmenWeaponData* InData, APawn* InOwner)
{
	WeaponData = InData;
	OwnerPawn = InOwner;
	ApplyVisualsFromData();
}

void AHitmenWeaponBase::OnRep_WeaponData()
{
	ApplyVisualsFromData();
	SetEquipped(bIsEquipped);
}

void AHitmenWeaponBase::ApplyVisualsFromData()
{
	if (!WeaponData)
	{
		return;
	}
	// BP children may already set meshes; data only fills empty components.
	if (!StaticMesh->GetStaticMesh() && !WeaponData->StaticMesh.IsNull())
	{
		StaticMesh->SetStaticMesh(WeaponData->StaticMesh.LoadSynchronous());
	}
	if (!SkeletalMesh->GetSkeletalMeshAsset() && !WeaponData->SkeletalMesh.IsNull())
	{
		SkeletalMesh->SetSkeletalMesh(WeaponData->SkeletalMesh.LoadSynchronous());
	}
}

bool AHitmenWeaponBase::IsLocallyOwned() const
{
	return OwnerPawn && OwnerPawn->IsLocallyControlled();
}

USceneComponent* AHitmenWeaponBase::GetAttachParent() const
{
	if (const AHitmenPlayerCharacter* Player = Cast<AHitmenPlayerCharacter>(OwnerPawn))
	{
		return Player->GetWeaponAttachComponent();
	}
	if (const ACharacter* Character = Cast<ACharacter>(OwnerPawn))
	{
		return Character->GetMesh();
	}
	return OwnerPawn ? OwnerPawn->GetRootComponent() : nullptr;
}

void AHitmenWeaponBase::SetEquipped(bool bEquipped)
{
	bIsEquipped = bEquipped;
	SetActorHiddenInGame(!bEquipped);
	if (!bEquipped)
	{
		StopFire();
	}
	if (USceneComponent* Parent = GetAttachParent())
	{
		const FName Socket = WeaponData ? WeaponData->AttachSocket : NAME_None;
		AttachToComponent(Parent, FAttachmentTransformRules::SnapToTargetNotIncludingScale, Socket);
		if (WeaponData)
		{
			SetActorRelativeTransform(WeaponData->AttachOffset);
		}
	}
	Juice_OnEquipped(bEquipped);
}

FVector AHitmenWeaponBase::GetMuzzleLocation() const
{
	const FName Socket = WeaponData ? WeaponData->MuzzleSocket : NAME_None;
	if (SkeletalMesh->GetSkeletalMeshAsset() && SkeletalMesh->DoesSocketExist(Socket))
	{
		return SkeletalMesh->GetSocketLocation(Socket);
	}
	if (StaticMesh->GetStaticMesh() && StaticMesh->DoesSocketExist(Socket))
	{
		return StaticMesh->GetSocketLocation(Socket);
	}
	return GetActorLocation() + GetActorForwardVector() * 40.f;
}

int32 AHitmenWeaponBase::GetOwnerAmmo() const
{
	if (!WeaponData || !WeaponData->bUsesAmmo)
	{
		return MAX_int32;
	}
	const UHitmenInventoryComponent* Inventory = OwnerPawn ? OwnerPawn->FindComponentByClass<UHitmenInventoryComponent>() : nullptr;
	return Inventory ? Inventory->GetActiveAmmo() : MAX_int32;
}

float AHitmenWeaponBase::GetCurrentSpread() const
{
	if (!WeaponData)
	{
		return 0.f;
	}
	const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerPawn);
	const float Base = (Character && Character->IsAiming()) ? WeaponData->AimSpread : WeaponData->HipSpread;
	return Base + CurrentBloom;
}

float AHitmenWeaponBase::GetCharge() const
{
	if (!WeaponData || !WeaponData->bChargeToFire || ChargeStartTime < 0.f)
	{
		return 0.f;
	}
	return FMath::Clamp((GetWorld()->GetTimeSeconds() - ChargeStartTime) / WeaponData->FullChargeTime, 0.f, 1.f);
}

void AHitmenWeaponBase::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (WeaponData && CurrentBloom > 0.f && !bTriggerHeld)
	{
		CurrentBloom = FMath::Max(0.f, CurrentBloom - WeaponData->BloomRecoveryPerSecond * DeltaSeconds);
	}
	if (CurrentBloom <= 0.f && !bTriggerHeld)
	{
		SetActorTickEnabled(false);
	}
}

bool AHitmenWeaponBase::CanFireNow() const
{
	if (!WeaponData || !bIsEquipped || !OwnerPawn)
	{
		return false;
	}
	if (const AHitmenCharacterBase* Character = Cast<AHitmenCharacterBase>(OwnerPawn))
	{
		if (Character->IsDead())
		{
			return false;
		}
	}
	return GetWorld()->GetTimeSeconds() - LastFireTime >= WeaponData->GetFireInterval() * 0.95f;
}

void AHitmenWeaponBase::StartFire()
{
	if (!WeaponData || !IsLocallyOwned())
	{
		return;
	}
	bTriggerHeld = true;
	SetActorTickEnabled(true);

	if (WeaponData->bChargeToFire)
	{
		ChargeStartTime = GetWorld()->GetTimeSeconds();
		return;
	}

	if (WeaponData->SpinUpTime > 0.f && !bSpunUp)
	{
		Juice_OnSpinUp(true);
		GetWorldTimerManager().SetTimer(SpinUpTimer, FTimerDelegate::CreateWeakLambda(this, [this]()
		{
			bSpunUp = true;
			if (bTriggerHeld)
			{
				HandleAutoFire();
			}
		}), WeaponData->SpinUpTime, false);
		return;
	}

	BurstRemaining = WeaponData->FireMode == EHitmenFireMode::Burst ? WeaponData->BurstCount : 0;
	HandleAutoFire();
}

void AHitmenWeaponBase::StopFire()
{
	const bool bWasHeld = bTriggerHeld;
	bTriggerHeld = false;
	if (WeaponData && WeaponData->bChargeToFire && bWasHeld && ChargeStartTime >= 0.f)
	{
		FireOnce();
		ChargeStartTime = -1.f;
	}
	if (WeaponData && WeaponData->FireMode != EHitmenFireMode::Burst)
	{
		GetWorldTimerManager().ClearTimer(FireTimer);
	}
	if (bSpunUp || GetWorldTimerManager().IsTimerActive(SpinUpTimer))
	{
		GetWorldTimerManager().ClearTimer(SpinUpTimer);
		bSpunUp = false;
		Juice_OnSpinUp(false);
	}
}

void AHitmenWeaponBase::HandleAutoFire()
{
	if (!WeaponData)
	{
		return;
	}
	const float Interval = WeaponData->GetFireInterval();
	const float Wait = FMath::Max(0.f, LastFireTime + Interval - GetWorld()->GetTimeSeconds());
	if (Wait > 0.f)
	{
		GetWorldTimerManager().SetTimer(FireTimer, this, &AHitmenWeaponBase::HandleAutoFire, Wait, false);
		return;
	}

	FireOnce();

	bool bContinue = false;
	switch (WeaponData->FireMode)
	{
	case EHitmenFireMode::Automatic: bContinue = bTriggerHeld; break;
	case EHitmenFireMode::Burst:     bContinue = --BurstRemaining > 0; break;
	default: break;
	}
	if (bContinue)
	{
		GetWorldTimerManager().SetTimer(FireTimer, this, &AHitmenWeaponBase::HandleAutoFire, Interval, false);
	}
}

void AHitmenWeaponBase::FireOnce()
{
	if (!CanFireNow())
	{
		return;
	}

	// Empty: throw the weapon where we look.
	if (GetOwnerAmmo() < WeaponData->AmmoPerShot)
	{
		Juice_OnDryFire();
		if (UHitmenInventoryComponent* Inventory = OwnerPawn->FindComponentByClass<UHitmenInventoryComponent>())
		{
			bTriggerHeld = false;
			GetWorldTimerManager().ClearTimer(FireTimer);
			Inventory->ThrowActive();
		}
		return;
	}

	LastFireTime = GetWorld()->GetTimeSeconds();
	FVector Origin, Direction;
	UHitmenCombatLibrary::GetAimPoint(OwnerPawn, Origin, Direction);

	const int32 Seed = FMath::Rand();
	const FVector SpreadDirection = UHitmenCombatLibrary::RandomConeDirection(Direction, GetCurrentSpread(), Seed, 0);
	const float Charge = WeaponData->bChargeToFire ? FMath::Max(0.2f, GetCharge()) : 1.f;

	// Local prediction of cosmetics.
	TArray<FVector> Impacts;
	SimulateImpacts(Origin, SpreadDirection, Seed, Impacts);
	PlayFireEffects(GetMuzzleLocation(), Impacts, true);

	CurrentBloom = FMath::Min(WeaponData->MaxBloom, CurrentBloom + WeaponData->SpreadPerShot);
	if (APlayerController* PC = Cast<APlayerController>(OwnerPawn->GetController()))
	{
		PC->AddPitchInput(-WeaponData->RecoilPitch);
		PC->AddYawInput(FMath::FRandRange(-WeaponData->RecoilYawRandom, WeaponData->RecoilYawRandom));
		if (WeaponData->FireCameraShake)
		{
			PC->ClientStartCameraShake(WeaponData->FireCameraShake);
		}
	}

	Server_Fire(Origin, SpreadDirection, Seed, Charge);
}

void AHitmenWeaponBase::Server_Fire_Implementation(FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction, int32 Seed, float Charge)
{
	if (!WeaponData || !OwnerPawn || !bIsEquipped)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	// Rate validation with a little tolerance for jitter.
	if (Now - LastServerFireTime < WeaponData->GetFireInterval() * 0.7f)
	{
		return;
	}
	// Origin must be near the shooter.
	if (FVector::Dist(Origin, OwnerPawn->GetActorLocation()) > 400.f)
	{
		return;
	}
	UHitmenInventoryComponent* Inventory = OwnerPawn->FindComponentByClass<UHitmenInventoryComponent>();
	if (Inventory && !Inventory->ConsumeActiveAmmo(WeaponData->AmmoPerShot))
	{
		return;
	}
	LastServerFireTime = Now;

	TArray<FVector> Impacts;
	FireServer(Origin, Direction, Seed, FMath::Clamp(Charge, 0.f, 1.f), Impacts);
	ReportShotNoise(Origin);

	TArray<FVector_NetQuantize> NetImpacts;
	for (const FVector& Impact : Impacts)
	{
		NetImpacts.Add(Impact);
	}
	Multicast_FireEffects(GetMuzzleLocation(), NetImpacts);
}

void AHitmenWeaponBase::FireAt(const FVector& TargetLocation, float AimErrorDegrees)
{
	if (!HasAuthority() || !WeaponData || !OwnerPawn)
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	if (Now - LastServerFireTime < WeaponData->GetFireInterval())
	{
		return;
	}
	LastServerFireTime = Now;

	FVector Origin, Unused;
	UHitmenCombatLibrary::GetAimPoint(OwnerPawn, Origin, Unused);
	const FVector Direction = (TargetLocation - Origin).GetSafeNormal();
	const int32 Seed = FMath::Rand();
	const FVector Spread = UHitmenCombatLibrary::RandomConeDirection(Direction, AimErrorDegrees + WeaponData->HipSpread, Seed, 0);

	TArray<FVector> Impacts;
	FireServer(Origin, Spread, Seed, 1.f, Impacts);
	ReportShotNoise(Origin);

	TArray<FVector_NetQuantize> NetImpacts;
	for (const FVector& Impact : Impacts)
	{
		NetImpacts.Add(Impact);
	}
	Multicast_FireEffects(GetMuzzleLocation(), NetImpacts);
}

void AHitmenWeaponBase::ReportShotNoise(const FVector& Origin) const
{
	if (WeaponData && !WeaponData->bSilenced && WeaponData->NoiseRadius > 0.f)
	{
		UHitmenAISubsystem::ReportNoise(this, Origin, WeaponData->NoiseRadius, OwnerPawn, EHitmenNoiseType::Gunshot);
	}
}

void AHitmenWeaponBase::Multicast_FireEffects_Implementation(FVector_NetQuantize Origin, const TArray<FVector_NetQuantize>& Impacts)
{
	// The owning client already played predicted effects.
	if (IsLocallyOwned() && OwnerPawn && OwnerPawn->IsPlayerControlled())
	{
		return;
	}
	TArray<FVector> Points;
	for (const FVector_NetQuantize& Impact : Impacts)
	{
		Points.Add(Impact);
	}
	PlayFireEffects(Origin, Points, false);
}

void AHitmenWeaponBase::PlayFireEffects(const FVector& Origin, const TArray<FVector>& Impacts, bool bLocallyPredicted)
{
	if (GetNetMode() == NM_DedicatedServer || !WeaponData)
	{
		return;
	}
	if (UNiagaraSystem* Muzzle = WeaponData->MuzzleFX.LoadSynchronous())
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Muzzle, Origin, GetActorRotation());
	}
	if (USoundBase* Sound = WeaponData->FireSound.LoadSynchronous())
	{
		UGameplayStatics::PlaySoundAtLocation(this, Sound, Origin);
	}
	UNiagaraSystem* Tracer = WeaponData->TracerFX.LoadSynchronous();
	UNiagaraSystem* ImpactFX = WeaponData->ImpactFX.LoadSynchronous();
	USoundBase* ImpactSound = WeaponData->ImpactSound.LoadSynchronous();
	for (const FVector& Impact : Impacts)
	{
		if (Tracer)
		{
			if (UNiagaraComponent* TracerComp = UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Tracer, Origin, (Impact - Origin).Rotation()))
			{
				TracerComp->SetVectorParameter(TEXT("TracerEnd"), Impact);
			}
		}
		if (ImpactFX)
		{
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, ImpactFX, Impact);
		}
		if (ImpactSound)
		{
			UGameplayStatics::PlaySoundAtLocation(this, ImpactSound, Impact);
		}
		Juice_OnImpact(Impact);
	}
	Juice_OnFired(bLocallyPredicted);
}
