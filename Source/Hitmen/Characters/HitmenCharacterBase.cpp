// Hitmen — character base

#include "Characters/HitmenCharacterBase.h"
#include "Core/HitmenText.h"
#include "GameFramework/SpectatorPawn.h"
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
#include "Core/HitmenGameSettings.h"
#include "Data/HitmenSoundSet.h"
#include "NiagaraFunctionLibrary.h"
#include "NiagaraComponent.h"
#include "Components/AudioComponent.h"
#include "Kismet/GameplayStatics.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Weapons/HitmenProjectile.h"
#include "Data/HitmenWeaponData.h"
#include "World/Interactables/HitmenHidingSpot.h"
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
	DOREPLIFETIME(AHitmenCharacterBase, StowedIn);
	DOREPLIFETIME(AHitmenCharacterBase, StowSlot);
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
		Movement->OnTraversalMontage.AddDynamic(this, &AHitmenCharacterBase::HandleTraversalMontage);
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

void AHitmenCharacterBase::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (GetNetMode() != NM_DedicatedServer)
	{
		UpdateTraversalJuice(DeltaSeconds);
	}
}

void AHitmenCharacterBase::UpdateTraversalJuice(float DeltaSeconds)
{
	const UHitmenCharacterMovementComponent* Movement = GetHitmenMovement();
	const bool bZipline = Movement && Movement->IsZiplining();
	const bool bClimbing = Movement && Movement->IsClimbing() && !bZipline;
	const float Z = GetActorLocation().Z;

	// Hand-level dust stays out of your own first-person view (it would fill the screen); everyone else sees it.
	const bool bFirstPersonViewer = IsLocallyControlled() && IsPlayerControlled();

	// Ladders and gutters: a grab sound every step of the climb, and a puff when switched on (up / down).
	const UHitmenGameSettings* Juice = UHitmenGameSettings::Get();
	if (bClimbing)
	{
		const float Step = Z - LastTraversalZ;
		ClimbStepAccumulator += FMath::Abs(Step);
		if (ClimbStepAccumulator >= ClimbStepDistance)
		{
			ClimbStepAccumulator = 0.f;
			const bool bDown = Step < 0.f;
			const FVector Hands = GetActorLocation() + GetActorForwardVector() * 30.f + FVector::UpVector * (bDown ? -20.f : 60.f);
			UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::ClimbGrab, Hands);
			UNiagaraSystem* Puff = bDown ? (Juice->bLadderDescendFX ? LadderDescendFX.Get() : nullptr) : (Juice->bLadderClimbFX ? ClimbFX.Get() : nullptr);
			if (Puff && !bFirstPersonViewer)
			{
				UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, Puff, Hands, FRotator::ZeroRotator, FVector(0.35f));
			}
		}
	}
	else if (!bZipline)
	{
		ClimbStepAccumulator = 0.f;
	}
	LastTraversalZ = Z;

	// Ledge climbs: dust and a grab sound where the hands land; a vault or climb ends with dust under the feet.
	const bool bMantling = Movement && Movement->IsMantling();
	if (bMantling && !bWasMantlingJuice)
	{
		FVector Ledge, Normal;
		if (FindLedgeInFront(Ledge, Normal))
		{
			const FVector Along = FVector::CrossProduct(FVector::UpVector, Normal).GetSafeNormal();
			if (!bFirstPersonViewer && Juice->bMantleHandsFX)
			{
				SpawnTraversalDust(Ledge - Normal * 8.f + Along * 22.f, TraversalDustScale);
				SpawnTraversalDust(Ledge - Normal * 8.f - Along * 22.f, TraversalDustScale);
			}
			UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::ClimbGrab, Ledge);
		}
	}
	else if (!bMantling && bWasMantlingJuice && Juice->bMantleLandFX)
	{
		SpawnTraversalDust(GetActorLocation() - FVector::UpVector * GetCapsuleComponent()->GetScaledCapsuleHalfHeight(), TraversalDustScale);
	}
	bWasMantlingJuice = bMantling;

	// Slides: a scrape sound and dust along the way.
	const bool bSliding = Movement && Movement->IsSliding();
	const FVector Feet = GetActorLocation() - FVector::UpVector * GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
	if (bSliding && !bWasSlidingJuice)
	{
		SlideDustAccumulator = 0.f;
		USoundBase* Scrape = Juice->bSlideSound ? UHitmenSoundLibrary::GetHitmenSound(EHitmenSound::Slide) : nullptr;
		if (Scrape)
		{
			SlideAudio = UGameplayStatics::SpawnSoundAttached(Scrape, GetRootComponent(), NAME_None, FVector(0.f, 0.f, -60.f), EAttachLocation::KeepRelativeOffset,
				true, UHitmenSoundLibrary::GetHitmenSoundVolume(EHitmenSound::Slide), 1.f, 0.f, UHitmenSoundLibrary::GetSpaceAttenuation(EHitmenSoundSpace::Footstep));
		}
	}
	if (bSliding && Juice->bSlideFX)
	{
		SlideDustAccumulator -= DeltaSeconds;
		if (SlideDustAccumulator <= 0.f)
		{
			SlideDustAccumulator = SlideDustInterval;
			SpawnTraversalDust(Feet, TraversalDustScale * 0.7f);
		}
	}
	else if (bWasSlidingJuice && SlideAudio)
	{
		SlideAudio->FadeOut(0.15f, 0.f);
		SlideAudio = nullptr;
	}
	bWasSlidingJuice = bSliding;

	// Ziplines: bursts of sparks where the hands hold the cable, and the cable sound.
	if (bZipline && ZiplineFX)
	{
		ClimbStepAccumulator -= DeltaSeconds;
		if (ClimbStepAccumulator <= 0.f)
		{
			ClimbStepAccumulator = 0.12f;
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, ZiplineFX, GetActorLocation() + FVector::UpVector * 110.f, GetActorRotation(), FVector(0.3f));
		}
	}
	// The trolley clamps on as the ride starts (one sound), then the cable sings steadily until the end.
	if (bZipline && !bWasZiplineJuice)
	{
		if (USoundBase* Enter = UHitmenSoundLibrary::GetHitmenSound(EHitmenSound::ZiplineEnter))
		{
			UGameplayStatics::SpawnSoundAttached(Enter, GetRootComponent(), NAME_None, FVector::UpVector * 110.f, EAttachLocation::KeepRelativeOffset, true,
				UHitmenSoundLibrary::GetHitmenSoundVolume(EHitmenSound::ZiplineEnter), 1.f, 0.f, UHitmenSoundLibrary::GetSpaceAttenuation(EHitmenSoundSpace::World));
		}
	}
	bWasZiplineJuice = bZipline;
	if (bZipline && !ZiplineAudio)
	{
		const bool bHasLoop = UHitmenSoundLibrary::GetHitmenSound(EHitmenSound::ZiplineLoop) != nullptr;
		const EHitmenSound Cable = bHasLoop ? EHitmenSound::ZiplineLoop : EHitmenSound::ZiplineRide;
		if (USoundBase* Ride = UHitmenSoundLibrary::GetHitmenSound(Cable))
		{
			// Kept alive for the whole ride (restarted if the clip ends), destroyed after its fade-out.
			ZiplineAudio = UGameplayStatics::SpawnSoundAttached(Ride, GetRootComponent(), NAME_None, FVector::UpVector * 110.f, EAttachLocation::KeepRelativeOffset, false,
				UHitmenSoundLibrary::GetHitmenSoundVolume(Cable), 1.f, 0.f, UHitmenSoundLibrary::GetSpaceAttenuation(EHitmenSoundSpace::World), nullptr, false);
		}
	}
	if (bZipline && ZiplineAudio)
	{
		// Constant for the whole ride: a clip that ends is started again; the pitch follows the speed a little.
		if (!ZiplineAudio->IsPlaying() && UHitmenSoundLibrary::GetHitmenSound(EHitmenSound::ZiplineLoop))
		{
			ZiplineAudio->Play();
		}
		const float Speed = GetVelocity().Size();
		ZiplineAudio->SetPitchMultiplier(FMath::GetMappedRangeValueClamped(FVector2D(200.f, 1600.f), FVector2D(0.85f, 1.1f), Speed));
	}
	if (!bZipline)
	{
		if (ZiplineEffect)
		{
			ZiplineEffect->DestroyComponent();
			ZiplineEffect = nullptr;
		}
		if (ZiplineAudio)
		{
			if (ZiplineAudio->IsPlaying())
			{
				ZiplineAudio->bAutoDestroy = true;
				ZiplineAudio->FadeOut(0.25f, 0.f);
			}
			else
			{
				ZiplineAudio->DestroyComponent();
			}
			ZiplineAudio = nullptr;
		}
	}
}

void AHitmenCharacterBase::SpawnTraversalDust(const FVector& Location, float Scale) const
{
	if (TraversalDustFX && GetNetMode() != NM_DedicatedServer)
	{
		UNiagaraFunctionLibrary::SpawnSystemAtLocation(this, TraversalDustFX, Location, FRotator::ZeroRotator, FVector(Scale));
	}
}

bool AHitmenCharacterBase::FindLedgeInFront(FVector& OutPoint, FVector& OutNormal) const
{
	const UWorld* World = GetWorld();
	const float HalfHeight = GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
	const FVector Feet = GetActorLocation() - FVector::UpVector * HalfHeight;
	const FVector Forward = GetActorForwardVector().GetSafeNormal2D();
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenLedge), false, this);
	// The wall face: from knee to above the head, the first hit straight ahead.
	FHitResult Wall;
	bool bWall = false;
	for (float Height = 40.f; Height <= 260.f && !bWall; Height += 40.f)
	{
		const FVector Start = Feet + FVector::UpVector * Height;
		bWall = World->LineTraceSingleByChannel(Wall, Start, Start + Forward * 130.f, ECC_Visibility, Params) && FMath::Abs(Wall.ImpactNormal.Z) < 0.3f;
	}
	if (!bWall)
	{
		return false;
	}
	// The top just past the face.
	const FVector Into = -Wall.ImpactNormal.GetSafeNormal2D();
	const FVector Column = Wall.ImpactPoint + Into * 12.f;
	FHitResult Top;
	if (!World->LineTraceSingleByChannel(Top, FVector(Column.X, Column.Y, Feet.Z + 300.f), FVector(Column.X, Column.Y, Wall.ImpactPoint.Z - 10.f), ECC_Visibility, Params)
		|| Top.bStartPenetrating || Top.ImpactNormal.Z < 0.6f)
	{
		return false;
	}
	OutPoint = FVector(Wall.ImpactPoint.X, Wall.ImpactPoint.Y, Top.ImpactPoint.Z);
	OutNormal = Wall.ImpactNormal.GetSafeNormal2D();
	return true;
}

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

void AHitmenCharacterBase::HandleTraversalMontage(EHitmenAnimMontage Montage)
{
	PlayAnimSetMontageLocal(Montage);
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
	if (IsDead() && !Carrier && !bVanished && !StowedIn)
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
	return HITMEN_TEXT("Hitmen.CarryBody", "Carry body");
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

	// Headshot = death for bullets and arrows (explosions and fists are radial / not point hits on bones).
	if (bHeadshot && Damage > 0.f)
	{
		const UHitmenGameSettings* Settings = UHitmenGameSettings::Get();
		const AHitmenCharacterBase* Shooter = Cast<AHitmenCharacterBase>(InstigatorActor);
		const bool bFromNPC = Shooter && !Shooter->IsPlayerControlled();
		// The firing weapon decides too (its bHeadshotKills): a projectile is owned by the weapon that fired it.
		const AHitmenWeaponBase* Weapon = Cast<AHitmenWeaponBase>(DamageCauser);
		const AHitmenProjectile* Projectile = Cast<AHitmenProjectile>(DamageCauser);
		if (!Weapon && Projectile)
		{
			Weapon = Cast<AHitmenWeaponBase>(Projectile->GetOwner());
		}
		const bool bWeaponKills = !Weapon || !Weapon->GetWeaponData() || Weapon->GetWeaponData()->bHeadshotKills;
		// Fists are not weapons: a punch in the face is no headshot (no red cross, no headshot sound).
		if (!Weapon && !Projectile)
		{
			bHeadshot = false;
		}
		const bool bLethalCauser = (Weapon || Projectile) && bWeaponKills;
		if (Settings->bHeadshotKills && bLethalCauser && (!bFromNPC || Settings->bHeadshotKillsFromNPCs))
		{
			Damage = FMath::Max(Damage, HealthComponent->GetHealth() + HealthComponent->GetArmor() + 1.f);
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
	// The standing capsule would stay where the body fell from and block traces (F on the dropped
	// gun, the body, sight lines): the ragdoll mesh is the body from now on.
	GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision);
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

void AHitmenCharacterBase::EnableRagdoll(bool bDeathImpulse)
{
	USkeletalMeshComponent* MeshComp = GetMesh();
	MeshComp->SetCollisionProfileName(TEXT("Ragdoll"));
	MeshComp->SetCollisionResponseToChannel(HITMEN_TRACE_INTERACT, ECR_Block);
	// Continuous collision: a limb never tunnels into a wall it starts next to.
	MeshComp->SetAllUseCCD(true);
	MeshComp->SetSimulatePhysics(true);
	MeshComp->WakeAllRigidBodies();
	if (bDeathImpulse && !LastHitDirection.IsNearlyZero())
	{
		MeshComp->AddImpulse(LastHitDirection * DeathImpulse, LastHitBone, true);
	}
}

FVector AHitmenCharacterBase::FindSafeDropLocation(const FVector& From, const FVector& Wanted) const
{
	// Sweep a ball the size of the body from the carrier's chest to where the body was held: a wall in between stops
	// it short; then make sure there is room above the floor.
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenBodyDrop), false, this);
	if (LastCarrier.IsValid())
	{
		Params.AddIgnoredActor(LastCarrier.Get());
	}
	FCollisionObjectQueryParams Objects;
	Objects.AddObjectTypesToQuery(ECC_WorldStatic);
	Objects.AddObjectTypesToQuery(ECC_WorldDynamic);
	const FCollisionShape Ball = FCollisionShape::MakeSphere(DropClearance);
	FVector Location = Wanted;
	FHitResult Hit;
	if (GetWorld()->SweepSingleByObjectType(Hit, From, Wanted, FQuat::Identity, Objects, Ball, Params))
	{
		Location = Hit.bStartPenetrating ? From : Hit.Location;
	}
	FHitResult Floor;
	if (GetWorld()->SweepSingleByObjectType(Floor, Location + FVector(0.f, 0.f, 60.f), Location - FVector(0.f, 0.f, 200.f), FQuat::Identity, Objects, Ball, Params)
		&& !Floor.bStartPenetrating)
	{
		Location.Z = FMath::Max(Location.Z, Floor.Location.Z + 5.f);
	}
	return Location;
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
	return IsDead() && !bVanished && !Carrier && !StowedIn;
}

void AHitmenCharacterBase::SetStowedIn(AHitmenHidingSpot* Spot, int32 Slot)
{
	if (!HasAuthority() || !IsDead())
	{
		return;
	}
	Carrier = nullptr;
	StowedIn = Spot;
	StowSlot = Slot;
	OnRep_Body();
}

void AHitmenCharacterBase::VanishBody()
{
	if (HasAuthority() && IsDead() && !bVanished)
	{
		bVanished = true;
		OnRep_Body();
	}
}

void AHitmenCharacterBase::Multicast_Burn_Implementation(UNiagaraSystem* FireFX, float FireScale, UNiagaraSystem* AshFX, float Duration)
{
	if (GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	// Fire along the body (pelvis and chest), attached to the ragdoll so it burns where the body lies.
	TArray<TWeakObjectPtr<UNiagaraComponent>> Fires;
	if (FireFX && GetMesh())
	{
		for (const FName Bone : { FName(TEXT("pelvis")), FName(TEXT("spine_03")) })
		{
			const FName Socket = GetMesh()->DoesSocketExist(Bone) ? Bone : NAME_None;
			if (UNiagaraComponent* Fire = UNiagaraFunctionLibrary::SpawnSystemAttached(FireFX, GetMesh(), Socket, FVector::ZeroVector, FRotator::ZeroRotator,
				FVector(FireScale), EAttachLocation::SnapToTarget, true, ENCPoolMethod::None, true, true))
			{
				Fires.Add(Fire);
			}
		}
	}
	UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::BodyBurn, GetMesh() ? GetMesh()->GetComponentLocation() : GetActorLocation());
	// When it is gone: the fire dies down and ash puffs where it lay.
	TWeakObjectPtr<AHitmenCharacterBase> WeakThis(this);
	FTimerHandle Handle;
	GetWorldTimerManager().SetTimer(Handle, FTimerDelegate::CreateWeakLambda(this, [WeakThis, Fires, AshFX]()
	{
		for (const TWeakObjectPtr<UNiagaraComponent>& Fire : Fires)
		{
			if (Fire.IsValid())
			{
				Fire->Deactivate();
			}
		}
		if (WeakThis.IsValid() && AshFX)
		{
			const FVector Where = WeakThis->GetMesh() ? WeakThis->GetMesh()->GetBoneLocation(TEXT("pelvis")) : WeakThis->GetActorLocation();
			UNiagaraFunctionLibrary::SpawnSystemAtLocation(WeakThis.Get(), AshFX, Where, FRotator::ZeroRotator, FVector(0.8f));
		}
	}), FMath::Max(0.05f, Duration), false);
}

void AHitmenCharacterBase::SetCarriedBy(APawn* NewCarrier, FName AttachSocket)
{
	if (!HasAuthority() || !IsDead())
	{
		return;
	}
	if (NewCarrier)
	{
		StowedIn = nullptr;
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
	if (bVanished || Carrier || StowedIn)
	{
		GetWorldTimerManager().ClearTimer(DeathRagdollTimer);
	}
	// Your own carried body is drawn like your gun (first-person render: never inside a wall); everyone else, and
	// every body that is not in your arms, is a normal world mesh.
	const bool bInMyArms = Carrier && Carrier->IsLocallyControlled() && Carrier->IsPlayerControlled();
	if (MeshComp->FirstPersonPrimitiveType != (bInMyArms ? EFirstPersonPrimitiveType::FirstPerson : EFirstPersonPrimitiveType::None))
	{
		MeshComp->FirstPersonPrimitiveType = bInMyArms ? EFirstPersonPrimitiveType::FirstPerson : EFirstPersonPrimitiveType::None;
		MeshComp->SetCastShadow(!bInMyArms);
		MeshComp->MarkRenderStateDirty();
	}
	if (bVanished)
	{
		SetActorHiddenInGame(true);
		SetActorEnableCollision(false);
		MeshComp->SetSimulatePhysics(false);
		return;
	}
	if (StowedIn)
	{
		// In its slot inside the hiding spot: still, no collision, seen when the door / lid opens.
		MeshComp->SetSimulatePhysics(false);
		MeshComp->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		const AHitmenHidingSpot* Spot = Cast<AHitmenHidingSpot>(StowedIn);
		AttachToComponent(StowedIn->GetRootComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
		SetActorRelativeTransform(Spot ? Spot->GetBodySlotTransform(StowSlot) : FTransform(FVector(0.f, 0.f, 40.f)));
		MeshComp->AttachToComponent(GetCapsuleComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
		MeshComp->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -92.f), FRotator(0.f, -90.f, 0.f));
		return;
	}
	if (Carrier)
	{
		LastCarrier = Carrier;
		MeshComp->SetSimulatePhysics(false);
		MeshComp->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		USceneComponent* AttachParent = Carrier->FindComponentByClass<USkeletalMeshComponent>();
		if (const ACharacter* CarrierCharacter = Cast<ACharacter>(Carrier))
		{
			AttachParent = CarrierCharacter->GetMesh();
		}
		if (CarrySocket.IsNone())
		{
			// In the arms: on the carrier's root, where its carry component says (lying across, in front).
			const UHitmenBodyCarryComponent* CarryComponent = Carrier->FindComponentByClass<UHitmenBodyCarryComponent>();
			AttachToComponent(Carrier->GetRootComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
			SetActorRelativeTransform(CarryComponent ? CarryComponent->GetCarryTransform() : FTransform(FVector(55.f, 0.f, 0.f)));
		}
		else if (AttachParent)
		{
			AttachToComponent(AttachParent, FAttachmentTransformRules::SnapToTargetNotIncludingScale, CarrySocket);
		}
		MeshComp->AttachToComponent(GetCapsuleComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
		MeshComp->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -92.f), FRotator(0.f, -90.f, 0.f));
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		return;
	}
	// Dropped: back to a ragdoll in front of the carrier, never inside a wall or the floor (it was carried with no
	// collision), and without the killing hit's push.
	const bool bWasHeld = GetAttachParentActor() != nullptr;
	if (bWasHeld)
	{
		DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
		if (const APawn* From = LastCarrier.Get())
		{
			const FVector Safe = FindSafeDropLocation(From->GetActorLocation() + FVector(0.f, 0.f, 20.f), GetActorLocation());
			SetActorLocation(Safe, false, nullptr, ETeleportType::TeleportPhysics);
		}
	}
	GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::QueryOnly);
	MeshComp->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
	if (IsDead())
	{
		EnableRagdoll(!bWasHeld);
	}
}

EHitmenScanCategory AHitmenCharacterBase::GetScanCategory_Implementation(const AActor* Observer) const
{
	if (bVanished || StowedIn)
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
	// Watchers have no team: every guard is lit for them (players get the watcher outline instead).
	if (Observer && Observer->IsA<ASpectatorPawn>())
	{
		return GetPlayerState() ? EHitmenScanCategory::None : EHitmenScanCategory::Enemy;
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
