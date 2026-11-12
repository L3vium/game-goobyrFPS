// Hitmen — NPC

#include "Characters/HitmenNPC.h"
#include "AI/HitmenAISubsystem.h"
#include "Animation/AnimInstance.h"
#include "Animation/AnimMontage.h"
#include "Data/HitmenWeaponData.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "UObject/ConstructorHelpers.h"
#include "Data/HitmenAnimSet.h"
#include "Data/HitmenSoundSet.h"
#include "Inventory/HitmenInventoryComponent.h"
#include "AI/HitmenGuardGroup.h"
#include "AI/HitmenAlertComponent.h"
#include "AI/HitmenAIController.h"
#include "Weapons/HitmenWeaponBase.h"
#include "World/Interactables/HitmenKeycardPickup.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenMatchRules.h"
#include "Framework/HitmenGameModeBase.h"
#include "Framework/HitmenGameState.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Core/HitmenTeamLibrary.h"
#include "Components/WidgetComponent.h"
#include "Components/CapsuleComponent.h"
#include "UI/HitmenHUDWidgets.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Movement/HitmenCharacterMovementComponent.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"

AHitmenNPC::AHitmenNPC(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	AIControllerClass = AHitmenAIController::StaticClass();
	AutoPossessAI = EAutoPossessAI::PlacedInWorldOrSpawned;
	bUseControllerRotationYaw = false;
	GetCharacterMovement()->bOrientRotationToMovement = true;
	GetCharacterMovement()->bUseControllerDesiredRotation = true;
	// Path following drives acceleration (not just velocity): the locomotion graph walks / runs.
	GetCharacterMovement()->GetNavMovementProperties()->bUseAccelerationForPaths = true;
	GetCharacterMovement()->bRequestedMoveUseAcceleration = true;
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cylinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	KeycardMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("KeycardMesh"));
	KeycardMesh->SetupAttachment(GetMesh(), KeycardBone);
	KeycardMesh->SetRelativeLocationAndRotation(FVector(0.f, -14.f, 18.f), FRotator(0.f, 0.f, 90.f));
	KeycardMesh->SetRelativeScale3D(FVector(0.09f, 0.055f, 0.01f));
	KeycardMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	KeycardMesh->SetVisibility(false);
	AimLaser = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("AimLaser"));
	AimLaser->SetupAttachment(GetRootComponent());
	AimLaser->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	AimLaser->SetCastShadow(false);
	AimLaser->SetVisibility(false);
	AimLaser->SetUsingAbsoluteLocation(true);
	AimLaser->SetUsingAbsoluteRotation(true);
	AimLaser->SetUsingAbsoluteScale(true);
	if (Cube.Succeeded())
	{
		KeycardMesh->SetStaticMesh(Cube.Object);
	}
	if (Cylinder.Succeeded())
	{
		AimLaser->SetStaticMesh(Cylinder.Object);
	}

	AlertComponent = CreateDefaultSubobject<UHitmenAlertComponent>(TEXT("Alert"));

	AlertWidget = CreateDefaultSubobject<UWidgetComponent>(TEXT("AlertWidget"));
	AlertWidget->SetupAttachment(GetRootComponent());
	AlertWidget->SetRelativeLocation(FVector(0.f, 0.f, 120.f));
	AlertWidget->SetWidgetSpace(EWidgetSpace::Screen);
	AlertWidget->SetDrawAtDesiredSize(true);
	AlertWidget->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	AlertWidget->SetWidgetClass(UHitmenAlertIndicatorWidget::StaticClass());

	KeycardPickupClass = AHitmenKeycardPickup::StaticClass();
}

void AHitmenNPC::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHitmenNPC, Weapon);
	DOREPLIFETIME(AHitmenNPC, CarriedKeycard);
}

void AHitmenNPC::OnRep_CarriedKeycard()
{
	KeycardMesh->SetVisibility(CarriedKeycard >= 0 && !IsDead());
}

void AHitmenNPC::UpdateAimPose()
{
	UAnimInstance* AnimInstance = GetMesh() ? GetMesh()->GetAnimInstance() : nullptr;
	if (!AnimInstance)
	{
		return;
	}
	const AActor* Target = AlertComponent ? AlertComponent->GetTarget() : nullptr;
	const EHitmenAlertState State = AlertComponent ? AlertComponent->GetAlertState() : EHitmenAlertState::Unaware;
	const bool bArmed = !IsDead() && Weapon && Weapon->GetWeaponData();
	const bool bFighting = bArmed && Target && State == EHitmenAlertState::Combat;
	// Searching: the gun stays ready while the guard stops to look around (alert state replicates: every machine).
	const bool bReady = bFighting || (bArmed && bReadyGunWhileSearching && State == EHitmenAlertState::Searching);
	if (bReady && !bGunReady && !AnimInstance->IsAnyMontagePlaying())
	{
		// Draw first: the weapon's equip animation, then the aim pose takes over.
		if (UAnimMontage* Equip = Weapon->GetWeaponData()->EquipMontage.LoadSynchronous())
		{
			AnimInstance->Montage_Play(Equip, 1.f);
		}
	}
	bGunReady = bReady;
	UAnimMontage* Wanted = bReady && GetVelocity().Size2D() <= AimPoseMaxSpeed ? Weapon->GetWeaponData()->AimMontage.LoadSynchronous() : nullptr;
	if (ActiveAimMontage && ActiveAimMontage != Wanted)
	{
		AnimInstance->Montage_Stop(0.25f, ActiveAimMontage);
		ActiveAimMontage = nullptr;
	}
	// Re-raise after a shot (the fire montage replaces it in the slot) or when it was stopped.
	if (Wanted && !AnimInstance->IsAnyMontagePlaying())
	{
		if (AnimInstance->Montage_Play(Wanted, 1.f) > 0.f)
		{
			const FName Section = Wanted->GetSectionName(0);
			AnimInstance->Montage_SetNextSection(Section, Section, Wanted);
			ActiveAimMontage = Wanted;
		}
	}
}

void AHitmenNPC::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (GetNetMode() != NM_DedicatedServer)
	{
		UpdateAimPose();
	}
	// Snipers: a thin red line from the weapon to the target while aiming (every machine).
	if (!bIsSniper || GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	const AActor* Target = AlertComponent ? AlertComponent->GetTarget() : nullptr;
	const bool bAiming = Target && !IsDead() && AlertComponent->GetAlertState() == EHitmenAlertState::Combat && Weapon;
	AimLaser->SetVisibility(bAiming);
	if (bAiming)
	{
		const FVector Start = Weapon->GetMuzzleLocation();
		FVector End = Target->GetActorLocation();
		FHitResult Hit;
		FCollisionQueryParams Params(SCENE_QUERY_STAT(HitmenSniperLaser), false, this);
		Params.AddIgnoredActor(Weapon);
		if (GetWorld()->LineTraceSingleByChannel(Hit, Start, End, ECC_Visibility, Params))
		{
			End = Hit.ImpactPoint;
		}
		const float Length = FVector::Dist(Start, End);
		AimLaser->SetWorldLocationAndRotation((Start + End) * 0.5f, FRotationMatrix::MakeFromZ(End - Start).Rotator());
		AimLaser->SetWorldScale3D(FVector(0.01f, 0.01f, Length / 100.f));
	}
}

void AHitmenNPC::SetGuardGroup(AHitmenGuardGroup* Group)
{
	GuardGroup = Group;
}

AHitmenGuardGroup* AHitmenNPC::GetGuardGroup() const
{
	return GuardGroup.Get();
}

bool AHitmenNPC::CanBePickpocketedBy(const AActor* Thief) const
{
	if (!Thief || IsDead() || CarriedKeycard < 0 || !IsUnawareOf(Thief))
	{
		return false;
	}
	const FVector ToThief = (Thief->GetActorLocation() - GetActorLocation()).GetSafeNormal2D();
	return FVector::DotProduct(GetActorForwardVector().GetSafeNormal2D(), ToThief) <= PickpocketMaxDot;
}

bool AHitmenNPC::CanInteract_Implementation(AActor* Interactor) const
{
	return CanBePickpocketedBy(Interactor) || Super::CanInteract_Implementation(Interactor);
}

FText AHitmenNPC::GetInteractionText_Implementation(AActor* Interactor) const
{
	if (CanBePickpocketedBy(Interactor))
	{
		return FText::Format(NSLOCTEXT("Hitmen", "Pickpocket", "Pickpocket keycard {0}"), FText::AsNumber(CarriedKeycard));
	}
	return Super::GetInteractionText_Implementation(Interactor);
}

void AHitmenNPC::Interact_Implementation(AActor* Interactor)
{
	if (!HasAuthority())
	{
		return;
	}
	if (CanBePickpocketedBy(Interactor))
	{
		if (UHitmenInventoryComponent* Inventory = Interactor->FindComponentByClass<UHitmenInventoryComponent>())
		{
			Inventory->AddKeycard(CarriedKeycard);
			CarriedKeycard = INDEX_NONE;
			OnRep_CarriedKeycard();
			if (AHitmenCharacterBase* Thief = Cast<AHitmenCharacterBase>(Interactor))
			{
				Thief->PlayAnimSetMontage(EHitmenAnimMontage::Pickpocket);
			}
			UHitmenSoundLibrary::PlayHitmenSoundAt(this, EHitmenSound::Pickpocket, GetActorLocation());
			Juice_OnPickpocketed(Interactor);
		}
		return;
	}
	Super::Interact_Implementation(Interactor);
}

void AHitmenNPC::PostInitializeComponents()
{
	// Before Super: a placed NPC is auto-possessed there, and its controller reads the post.
	PostLocation = GetActorLocation();
	PostRotation = FRotator(0.f, GetActorRotation().Yaw, 0.f);
	Super::PostInitializeComponents();
}

void AHitmenNPC::BeginPlay()
{
	Super::BeginPlay();
	KeycardMesh->AttachToComponent(GetMesh(), FAttachmentTransformRules::KeepRelativeTransform, KeycardBone);
	OnRep_CarriedKeycard();
	// Every guard ticks for its combat aim pose (raised weapon); only snipers need every frame (aim laser).
	SetActorTickEnabled(GetNetMode() != NM_DedicatedServer || bIsSniper);
	SetActorTickInterval(bIsSniper ? 0.f : 0.1f);
	AlertComponent->SetReactsToNoises(bReactsToNoises);
	// Bodies are a visual cue: only sentries (Hold Position + deaf) ignore them, because a sentry's
	// job is to never be lured off its gate. A deaf patrol / stationary NPC still investigates bodies.
	AlertComponent->SetReactsToBodies(!(HoldsPosition() && !bReactsToNoises));
	if (Behavior == EHitmenNPCBehavior::Patrol && !PatrolRoute && HasAuthority())
	{
		UE_LOG(LogTemp, Verbose, TEXT("%s patrols without a route: it stands at its post."), *GetName());
	}
	AlertComponent->OnAlertStateChanged.AddDynamic(this, &AHitmenNPC::HandleAlertStateChanged);
	if (GetNetMode() != NM_DedicatedServer)
	{
		// The overhead widget holds no reference to the alert component: the NPC pushes the view.
		AlertWidget->InitWidget();
		AlertComponent->OnCallingChanged.AddDynamic(this, &AHitmenNPC::HandleAlertCallingChanged);
		AlertComponent->OnSuspicionChanged.AddDynamic(this, &AHitmenNPC::HandleAlertSuspicionChanged);
	}
	HandleAlertStateChanged(EHitmenAlertState::Unaware, AlertComponent->GetAlertState());
	if (HasAuthority() && DefaultWeapon && DefaultWeapon->WeaponClass)
	{
		FActorSpawnParameters Params;
		Params.Owner = this;
		Params.Instigator = this;
		Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		Weapon = GetWorld()->SpawnActor<AHitmenWeaponBase>(DefaultWeapon->WeaponClass, GetActorTransform(), Params);
		if (Weapon)
		{
			Weapon->InitWeapon(DefaultWeapon, this);
			Weapon->SetEquipped(true);
		}
	}
}

void AHitmenNPC::HandleAlertCallingChanged(bool bCalling)
{
	PushAlertView();
}

void AHitmenNPC::HandleAlertSuspicionChanged(float Suspicion)
{
	PushAlertView();
}

void AHitmenNPC::PushAlertView()
{
	if (GetNetMode() == NM_DedicatedServer || !AlertComponent || !AlertWidget)
	{
		return;
	}
	if (UHitmenAlertIndicatorWidget* Indicator = Cast<UHitmenAlertIndicatorWidget>(AlertWidget->GetUserWidgetObject()))
	{
		Indicator->SetAlertView(AlertComponent->GetAlertColor(), AlertComponent->GetSuspicion(), AlertComponent->IsCallingReinforcements());
		Indicator->SetAsleep(bSleepRagdoll && !IsDead());
	}
}

void AHitmenNPC::SetSleepRagdoll(bool bAsleep)
{
	USkeletalMeshComponent* Body = GetMesh();
	if (!Body || IsDead() || bAsleep == bSleepRagdoll)
	{
		return;
	}
	bSleepRagdoll = bAsleep;
	if (bAsleep)
	{
		// Out cold: the body goes limp (every machine simulates it; the capsule stays where it fell asleep).
		SleepMeshRelative = Body->GetRelativeTransform();
		Body->SetCollisionProfileName(TEXT("Ragdoll"));
		Body->SetSimulatePhysics(true);
		Body->SetAllBodiesSimulatePhysics(true);
		Body->WakeAllRigidBodies();
	}
	else
	{
		// Wakes up: back in the capsule, a heavy hit reaction hides the snap.
		Body->SetSimulatePhysics(false);
		Body->SetAllBodiesSimulatePhysics(false);
		Body->SetCollisionProfileName(TEXT("CharacterMesh"));
		Body->AttachToComponent(GetCapsuleComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
		Body->SetRelativeTransform(SleepMeshRelative);
		PlayAnimSetMontageLocal(EHitmenAnimMontage::HitReact);
	}
	PushAlertView();
}

void AHitmenNPC::HandleAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState)
{
	PushAlertView();
	if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		switch (NewState)
		{
		case EHitmenAlertState::Combat:        Movement->WalkSpeed = CombatWalkSpeed; break;
		case EHitmenAlertState::Investigating:
		case EHitmenAlertState::Searching:     Movement->WalkSpeed = AlertWalkSpeed; break;
		default:                               Movement->WalkSpeed = CalmWalkSpeed; break;
		}
		// Fighting: face the target (controller focus), strafing while moving; otherwise face where it walks.
		// Orient-to-movement wins over the controller rotation in the movement code, so it must be off.
		const bool bFighting = NewState == EHitmenAlertState::Combat;
		Movement->bOrientRotationToMovement = !bFighting;
		Movement->bUseControllerDesiredRotation = true;
		if (bFighting)
		{
			Movement->RotationRate = FRotator(0.f, CombatTurnRate, 0.f);
		}
		else
		{
			Movement->RotationRate = GetClass()->GetDefaultObject<AHitmenNPC>()->GetCharacterMovement()->RotationRate;
		}
	}
}

float AHitmenNPC::GetEffectiveShootingRange() const
{
	return IsSniper() || ShootingRange <= 0.f ? TNumericLimits<float>::Max() : ShootingRange;
}

int32 AHitmenNPC::AdvancePatrolIndex(int32 PointCount)
{
	if (PointCount <= 0)
	{
		return INDEX_NONE;
	}
	PatrolIndex = (PatrolIndex + 1) % PointCount;
	return PatrolIndex;
}

void AHitmenNPC::FireAt(const FVector& Location)
{
	if (!HasAuthority() || !Weapon || IsDead())
	{
		return;
	}
	float AimError = 3.f;
	const AHitmenGameModeBase* GM = GetWorld()->GetAuthGameMode<AHitmenGameModeBase>();
	const AHitmenGameState* GS = GetWorld()->GetGameState<AHitmenGameState>();
	if (GM && GS)
	{
		AimError = GM->GetRules()->GetPreset(GS->GetLobbySettings().Difficulty).NPCAimErrorDegrees;
	}
	// Pacing (Hitman-like): raise the weapon, a short burst at a slow rate, a pause, again.
	const UHitmenMatchRules& Rules = UHitmenAISubsystem::GetRules(this);
	const float Now = GetWorld()->GetTimeSeconds();
	if (Now - LastShotRequestTime > Rules.NPCBurstPause + 1.f)
	{
		NextShotTime = Now + Rules.NPCFirstShotDelay;
		ShotsLeftInBurst = Rules.NPCBurstShots;
	}
	LastShotRequestTime = Now;
	if (Now < NextShotTime)
	{
		return;
	}
	Weapon->FireAt(Location, AimError);
	const float Interval = Weapon->GetWeaponData() ? Weapon->GetWeaponData()->GetFireInterval() : 0.3f;
	NextShotTime = Now + Interval * Rules.NPCFireIntervalMultiplier;
	if (--ShotsLeftInBurst <= 0)
	{
		ShotsLeftInBurst = Rules.NPCBurstShots;
		NextShotTime = Now + Rules.NPCBurstPause;
	}
}

int32 AHitmenNPC::GetHitmenTeamId_Implementation() const
{
	if (StatusEffectComponent && StatusEffectComponent->HasStatus(EHitmenStatusEffect::Charmed))
	{
		return UHitmenTeamLibrary::GetTeamId(StatusEffectComponent->GetStatusInstigator(EHitmenStatusEffect::Charmed));
	}
	return HitmenTeams::NPC;
}

bool AHitmenNPC::CanBeTakenDownBy(const AActor* Attacker) const
{
	if (!Attacker)
	{
		return false;
	}
	if (StatusEffectComponent && StatusEffectComponent->IsIncapacitated())
	{
		return true;
	}
	if (!AlertComponent)
	{
		return Super::CanBeTakenDownBy(Attacker);
	}
	// Unaware of this attacker = not looking at it right now (any state, any angle, standing or crouched).
	// An NPC that sees you coming detects you before you reach it (instant detection range) and fights.
	return !(AlertComponent->GetTarget() == Attacker && AlertComponent->IsTargetVisible());
}

bool AHitmenNPC::IsUnawareOf(const AActor* Other) const
{
	if (!AlertComponent)
	{
		return Super::IsUnawareOf(Other);
	}
	const EHitmenAlertState State = AlertComponent->GetAlertState();
	if (State == EHitmenAlertState::Combat && AlertComponent->GetTarget() == Other && AlertComponent->IsTargetVisible())
	{
		return false;
	}
	// Investigating / searching NPCs can still be surprised from behind.
	return Super::IsUnawareOf(Other) || StatusEffectComponent->IsIncapacitated();
}

void AHitmenNPC::OnDamaged(float Damage, AActor* InstigatorActor, bool bHeadshot)
{
	Super::OnDamaged(Damage, InstigatorActor, bHeadshot);
	AlertComponent->OnDamagedBy(InstigatorActor);
}

void AHitmenNPC::HandleDeath(AActor* Killer, AActor* DamageCauser, bool bHeadshot)
{
	AlertComponent->SetSuppressed(true);
	AlertComponent->SetAlertState(EHitmenAlertState::Unaware);
	AlertWidget->SetVisibility(false);

	if (HasAuthority())
	{
		if (CarriedKeycard >= 0 && KeycardPickupClass)
		{
			FActorSpawnParameters Params;
			Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
			// On the floor under the body, not at the capsule centre.
			FVector DropLocation = GetActorLocation();
			FHitResult Ground;
			FCollisionQueryParams GroundParams(SCENE_QUERY_STAT(HitmenKeycardDrop), false, this);
			if (GetWorld()->LineTraceSingleByChannel(Ground, DropLocation, DropLocation - FVector(0.f, 0.f, 300.f), ECC_Visibility, GroundParams))
			{
				DropLocation = Ground.Location;
			}
			if (AHitmenKeycardPickup* Keycard = GetWorld()->SpawnActor<AHitmenKeycardPickup>(KeycardPickupClass, DropLocation, FRotator(0.f, FMath::FRand() * 360.f, 0.f), Params))
			{
				Keycard->SetKeycardId(CarriedKeycard);
				KeycardMesh->SetVisibility(false);
			}
		}
		if (Weapon)
		{
			// The weapon actor itself becomes the world pickup (spawned once, never respawned).
			Weapon->Drop(DroppedWeaponAmmo, Weapon->GetActorLocation(), Weapon->GetActorRotation(), FVector::UpVector * 150.f, nullptr);
			Weapon = nullptr;
		}
		if (AController* AI = GetController())
		{
			AI->UnPossess();
			AI->Destroy();
		}
	}
	Super::HandleDeath(Killer, DamageCauser, bHeadshot);
}

void AHitmenNPC::HandleStatusAdded(EHitmenStatusEffect Status, AActor* StatusInstigator)
{
	Super::HandleStatusAdded(Status, StatusInstigator);
	if (Status == EHitmenStatusEffect::Asleep)
	{
		SetSleepRagdoll(true);
	}
	if (!HasAuthority())
	{
		return;
	}
	if (Status == EHitmenStatusEffect::Charmed)
	{
		// New side: forget the old target, look for hostiles of the new team from here.
		AlertComponent->StartSearchAround(GetActorLocation());
	}
	RefreshBrainPause();
}

void AHitmenNPC::HandleStatusRemoved(EHitmenStatusEffect Status, AActor* StatusInstigator)
{
	Super::HandleStatusRemoved(Status, StatusInstigator);
	if (Status == EHitmenStatusEffect::Asleep)
	{
		SetSleepRagdoll(false);
	}
	if (!HasAuthority())
	{
		return;
	}
	if (Status == EHitmenStatusEffect::Charmed)
	{
		AlertComponent->SetAlertState(EHitmenAlertState::Unaware);
	}
	RefreshBrainPause();
}

void AHitmenNPC::RefreshBrainPause()
{
	if (AHitmenAIController* AI = Cast<AHitmenAIController>(GetController()))
	{
		const bool bPaused = StatusEffectComponent->IsIncapacitated() || StatusEffectComponent->HasStatus(EHitmenStatusEffect::Trapped);
		AI->SetBrainPaused(bPaused);
	}
}
