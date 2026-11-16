// Hitmen — NPC

#include "Characters/HitmenNPC.h"
#include "Core/HitmenText.h"
#include "Core/HitmenGameSettings.h"
#include "Engine/SkeletalMesh.h"
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
#include "Components/HitmenBodyCarryComponent.h"
#include "Components/MaterialBillboardComponent.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Kismet/GameplayStatics.h"
#include "Camera/PlayerCameraManager.h"

AHitmenNPC::AHitmenNPC(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	AIControllerClass = AHitmenAIController::StaticClass();
	AutoPossessAI = EAutoPossessAI::PlacedInWorldOrSpawned;
	bUseControllerRotationYaw = false;
	// The body always turns toward the controller's rotation; the AI controller faces where the NPC walks unless it
	// looks at something within its strafe angle (UpdateControlRotation), so it never walks backward.
	GetCharacterMovement()->bOrientRotationToMovement = false;
	GetCharacterMovement()->bUseControllerDesiredRotation = true;
	// Path following drives acceleration (not just velocity): the locomotion graph walks / runs.
	GetCharacterMovement()->GetNavMovementProperties()->bUseAccelerationForPaths = true;
	GetCharacterMovement()->bRequestedMoveUseAcceleration = true;
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cylinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	KeycardMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("KeycardMesh"));
	KeycardMesh->SetupAttachment(GetMesh(), KeycardBone);
	KeycardMesh->SetRelativeScale3D(FVector(0.055f, 0.01f, 0.09f));
	BodyCarryComponent = CreateDefaultSubobject<UHitmenBodyCarryComponent>(TEXT("BodyCarryComponent"));
	DisplayName = HITMEN_TEXT("Hitmen.NPCGuard", "Guard");
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
	ScopeGlint = CreateDefaultSubobject<UMaterialBillboardComponent>(TEXT("ScopeGlint"));
	ScopeGlint->SetupAttachment(GetRootComponent());
	ScopeGlint->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	ScopeGlint->SetUsingAbsoluteLocation(true);
	ScopeGlint->SetVisibility(false);
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
	DOREPLIFETIME(AHitmenNPC, bSniperAiming);
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
	const bool bOnEdge = AlertComponent && AlertComponent->IsOnEdge() && State == EHitmenAlertState::Unaware;
	const bool bReady = bFighting || (bArmed && bReadyGunWhileSearching && (State == EHitmenAlertState::Searching || bOnEdge));
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
	// On edge ends without a state change: back to the calm stroll.
	if (HasAuthority() && AlertComponent && AlertComponent->IsOnEdge() != bWasOnEdge)
	{
		bWasOnEdge = AlertComponent->IsOnEdge();
		RefreshWalkSpeed();
	}
	// Holster or draw (a threat investigation can start without a state change).
	if (HasAuthority())
	{
		RefreshWeaponCarry();
	}
	// Snipers aim only at a target they see: the server decides, every machine shows it.
	const AActor* Target = AlertComponent ? AlertComponent->GetTarget() : nullptr;
	if (bIsSniper && HasAuthority())
	{
		bSniperAiming = Target && !IsDead() && Weapon && AlertComponent->GetAlertState() == EHitmenAlertState::Combat && AlertComponent->IsTargetVisible();
	}
	// Snipers: a thin red line from the weapon to the target while aiming (every machine).
	if (!bIsSniper || GetNetMode() == NM_DedicatedServer)
	{
		return;
	}
	const bool bAiming = bSniperAiming && Target && !IsDead() && Weapon;
	AimLaser->SetVisibility(bAiming);
	// The glint: the red star at the scope, only while it aims at you by default (bGlintOnlyWhileAiming), never once it
	// is dead or asleep.
	const APlayerCameraManager* ViewCamera = UGameplayStatics::GetPlayerCameraManager(this, 0);
	const bool bGlint = GlintInstance && Weapon && !IsDead() && !GetStatusEffects()->HasStatus(EHitmenStatusEffect::Asleep)
		&& (bAiming || !bGlintOnlyWhileAiming);
	ScopeGlint->SetVisibility(bGlint);
	if (bGlint)
	{
		// At the eye, where the scope is when it aims (the gun hangs low while it idles).
		const FVector Scope = GetMesh()->DoesSocketExist(TEXT("head"))
			? GetMesh()->GetSocketLocation(TEXT("head")) + GetActorForwardVector() * 18.f : GetPawnViewLocation();
		const FVector ToViewer = ViewCamera ? (ViewCamera->GetCameraLocation() - Scope).GetSafeNormal() : FVector::ZeroVector;
		ScopeGlint->SetWorldLocation(Scope + ToViewer * 25.f);
		GlintInstance->SetScalarParameterValue(TEXT("Aiming"), bAiming ? 1.f : 0.f);
	}
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
		// About two pixels wide at any distance: the width grows with the distance from your eye to the line.
		float Width = 1.f;
		if (ViewCamera)
		{
			const FVector Eye = ViewCamera->GetCameraLocation();
			Width = FMath::Max(1.f, FMath::PointDistToSegment(Eye, Start, End) * AimLaserScreenWidth);
		}
		AimLaser->SetWorldLocationAndRotation((Start + End) * 0.5f, FRotationMatrix::MakeFromZ(End - Start).Rotator());
		AimLaser->SetWorldScale3D(FVector(Width / 100.f, Width / 100.f, Length / 100.f));
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
	return GetPickpocketItem(Thief) != EHitmenPickpocketItem::None;
}

EHitmenPickpocketItem AHitmenNPC::GetPickpocketItem(const AActor* Thief) const
{
	const bool bKeycard = CarriedKeycard >= 0;
	const bool bHolstered = Weapon && Weapon->IsOnHip();
	if (!Thief || IsDead() || (!bKeycard && !bHolstered))
	{
		return EHitmenPickpocketItem::None;
	}
	if (FVector::DistSquared2D(Thief->GetActorLocation(), GetActorLocation()) > FMath::Square(PickpocketReach))
	{
		return EHitmenPickpocketItem::None;
	}
	const ACharacter* ThiefCharacter = Cast<ACharacter>(Thief);
	const bool bCrouched = ThiefCharacter && ThiefCharacter->bIsCrouched;
	if (bPickpocketRequiresCrouch && !bCrouched)
	{
		return EHitmenPickpocketItem::None;
	}
	const bool bIncapacitated = StatusEffectComponent && StatusEffectComponent->IsIncapacitated();
	if (!bIncapacitated)
	{
		// Never from someone looking at you; from behind, or beside it when you are crouched.
		if (AlertComponent && AlertComponent->GetTarget() == Thief && AlertComponent->IsTargetVisible())
		{
			return EHitmenPickpocketItem::None;
		}
		const FVector ToThief = (Thief->GetActorLocation() - GetActorLocation()).GetSafeNormal2D();
		const float Dot = FVector::DotProduct(GetActorForwardVector().GetSafeNormal2D(), ToThief);
		if (Dot > (bCrouched ? FMath::Max(CrouchedPickpocketMaxDot, PickpocketMaxDot) : PickpocketMaxDot))
		{
			return EHitmenPickpocketItem::None;
		}
	}
	if (bKeycard != bHolstered)
	{
		return bKeycard ? EHitmenPickpocketItem::Keycard : EHitmenPickpocketItem::Weapon;
	}
	// Both hips carry something: the one nearest to where the thief looks.
	const APawn* ThiefPawn = Cast<APawn>(Thief);
	const FVector Eye = ThiefPawn ? ThiefPawn->GetPawnViewLocation() : Thief->GetActorLocation();
	const FVector Look = ThiefPawn ? ThiefPawn->GetBaseAimRotation().Vector() : Thief->GetActorForwardVector();
	auto RayDistance = [&Eye, &Look](const FVector& Point)
	{
		return FVector::DistSquared(FMath::ClosestPointOnInfiniteLine(Eye, Eye + Look, Point), Point);
	};
	return RayDistance(KeycardMesh->GetComponentLocation()) <= RayDistance(Weapon->GetActorLocation()) ? EHitmenPickpocketItem::Keycard : EHitmenPickpocketItem::Weapon;
}

FTransform AHitmenNPC::ActorSpaceToHipBone(const FTransform& ActorSpace) const
{
	const USkeletalMeshComponent* Body = GetMesh();
	const USkeletalMesh* Asset = Body ? Body->GetSkeletalMeshAsset() : nullptr;
	if (!Asset)
	{
		return ActorSpace;
	}
	const FReferenceSkeleton& Ref = Asset->GetRefSkeleton();
	const int32 Bone = Ref.FindBoneIndex(KeycardBone);
	if (Bone == INDEX_NONE)
	{
		return ActorSpace;
	}
	// Reference pose, component space: the same on every machine whatever the animation is doing.
	FTransform BoneComponentSpace = FTransform::Identity;
	for (int32 Index = Bone; Index != INDEX_NONE; Index = Ref.GetParentIndex(Index))
	{
		BoneComponentSpace = BoneComponentSpace * Ref.GetRefBonePose()[Index];
	}
	const FTransform ComponentSpace = ActorSpace.GetRelativeTransform(Body->GetRelativeTransform());
	return ComponentSpace.GetRelativeTransform(BoneComponentSpace);
}

bool AHitmenNPC::GetHolsterAttachment(FName& OutBone, FTransform& OutRelative) const
{
	FTransform Slot = HolsterTransform;
	if (HolsterSide == EHitmenHipSlot::Left)
	{
		// Mirror the right hip.
		FVector Location = Slot.GetLocation();
		Location.Y *= -1.f;
		FRotator Rotation = Slot.Rotator();
		Rotation.Yaw *= -1.f;
		Rotation.Roll *= -1.f;
		Slot = FTransform(Rotation, Location);
	}
	OutBone = KeycardBone;
	OutRelative = ActorSpaceToHipBone(Slot);
	return true;
}

void AHitmenNPC::SpawnWeapon(UHitmenWeaponData* Data)
{
	if (!HasAuthority() || !Data || !Data->WeaponClass)
	{
		return;
	}
	FActorSpawnParameters Params;
	Params.Owner = this;
	Params.Instigator = this;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	Weapon = GetWorld()->SpawnActor<AHitmenWeaponBase>(Data->WeaponClass, GetActorTransform(), Params);
	if (Weapon)
	{
		Weapon->InitWeapon(Data, this);
		Weapon->SetEquipped(true);
		RefreshWeaponCarry();
	}
}

void AHitmenNPC::RefreshWeaponCarry()
{
	if (!HasAuthority() || !Weapon || Weapon->IsDropped() || IsDead())
	{
		return;
	}
	const UHitmenWeaponData* Data = Weapon->GetWeaponData();
	const bool bHolsterable = WeaponCarry == EHitmenNPCWeaponCarry::Holstered && Data && Data->bCanHolster
		&& !(StatusEffectComponent && StatusEffectComponent->HasStatus(EHitmenStatusEffect::Charmed));
	bool bDrawn = !bHolsterable;
	if (!bDrawn && AlertComponent)
	{
		// Drawn for real threats (a fight, a search, gunfire or a body being checked, on edge after an alert); a glimpse
		// or a footstep leaves it in the holster.
		const EHitmenAlertState State = AlertComponent->GetAlertState();
		bDrawn = State == EHitmenAlertState::Combat || State == EHitmenAlertState::Searching || AlertComponent->IsOnEdge()
			|| (State == EHitmenAlertState::Investigating && AlertComponent->IsThreatInvestigation());
	}
	if (bDrawn)
	{
		if (!Weapon->IsEquipped())
		{
			Weapon->SetEquipped(true);
		}
	}
	else if (!Weapon->IsOnHip())
	{
		Weapon->SetOnHip();
	}
}

bool AHitmenNPC::CanInteract_Implementation(AActor* Interactor) const
{
	return CanBePickpocketedBy(Interactor) || Super::CanInteract_Implementation(Interactor);
}

FText AHitmenNPC::GetInteractionInfo_Implementation(AActor* Interactor) const
{
	if (IsDead())
	{
		return FText::GetEmpty();
	}
	// VIP candidates: the one picked is the target; the others became guards at match start.
	FText Info = IsVIP() ? HITMEN_TEXT("Hitmen.NPCTarget", "Target") : (bVIPCandidate ? HITMEN_TEXT("Hitmen.NPCGuard", "Guard") : UHitmenTextSubsystem::Translate(DisplayName));
	if (Weapon && Weapon->GetWeaponData())
	{
		Info = FText::Format(HITMEN_TEXT("Hitmen.NPCInfoWeapon", "{0}  -  {1}"), Info, UHitmenTextSubsystem::Translate(Weapon->GetWeaponData()->DisplayName));
	}
	if (CarriedKeycard >= 0)
	{
		Info = FText::Format(HITMEN_TEXT("Hitmen.NPCInfoKeycard", "{0}  -  Keycard {1}"), Info, FText::AsNumber(CarriedKeycard));
	}
	if (Weapon && Weapon->IsOnHip())
	{
		Info = FText::Format(HITMEN_TEXT("Hitmen.NPCInfoHolstered", "{0}  -  holstered"), Info);
	}
	return Info;
}

void AHitmenNPC::HandleCarryChanged(AHitmenCharacterBase* Body)
{
	// A guard carrying a body away has its hands full: the gun is put away until it is done.
	if (Weapon)
	{
		Weapon->SetActorHiddenInGame(Body != nullptr || IsDead());
		if (!Body && !IsDead())
		{
			Weapon->RefreshAttachment();
		}
	}
}

FText AHitmenNPC::GetInteractionText_Implementation(AActor* Interactor) const
{
	switch (GetPickpocketItem(Interactor))
	{
	case EHitmenPickpocketItem::Keycard:
		return FText::Format(HITMEN_TEXT("Hitmen.Pickpocket", "Pickpocket keycard {0}"), FText::AsNumber(CarriedKeycard));
	case EHitmenPickpocketItem::Weapon:
		return FText::Format(HITMEN_TEXT("Hitmen.PickpocketWeapon", "Pickpocket {0}"),
			Weapon && Weapon->GetWeaponData() ? UHitmenTextSubsystem::Translate(Weapon->GetWeaponData()->DisplayName) : FText::GetEmpty());
	default:
		break;
	}
	return Super::GetInteractionText_Implementation(Interactor);
}

void AHitmenNPC::Interact_Implementation(AActor* Interactor)
{
	if (!HasAuthority())
	{
		return;
	}
	const EHitmenPickpocketItem Item = GetPickpocketItem(Interactor);
	if (Item != EHitmenPickpocketItem::None)
	{
		if (UHitmenInventoryComponent* Inventory = Interactor->FindComponentByClass<UHitmenInventoryComponent>())
		{
			if (Item == EHitmenPickpocketItem::Keycard)
			{
				Inventory->AddKeycard(CarriedKeycard);
				CarriedKeycard = INDEX_NONE;
				OnRep_CarriedKeycard();
			}
			else
			{
				// The holstered pistol: out of the holster and into the thief's hands (or a free slot).
				AHitmenWeaponBase* Stolen = Weapon;
				Weapon = nullptr;
				Stolen->Drop(DroppedWeaponAmmo, Stolen->GetActorLocation(), Stolen->GetActorRotation(), FVector::ZeroVector, nullptr);
				Inventory->PickUpWeapon(Stolen, true);
			}
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
	// The card sits on the hip opposite the holster and follows the hips (placed from the reference pose: the same
	// on every machine).
	const FVector CardOffset(KeycardHipOffset.X, GetKeycardSide() == EHitmenHipSlot::Right ? KeycardHipOffset.Y : -KeycardHipOffset.Y, KeycardHipOffset.Z);
	FTransform CardRelative = ActorSpaceToHipBone(FTransform(FRotator::ZeroRotator, CardOffset));
	CardRelative.SetScale3D(FVector(0.055f, 0.01f, 0.09f));
	KeycardMesh->AttachToComponent(GetMesh(), FAttachmentTransformRules::SnapToTargetNotIncludingScale, KeycardBone);
	KeycardMesh->SetRelativeTransform(CardRelative);
	BodyCarryComponent->OnCarryChanged.AddUniqueDynamic(this, &AHitmenNPC::HandleCarryChanged);
	OnRep_CarriedKeycard();
	if (bIsSniper && GlintMaterial && GetNetMode() != NM_DedicatedServer)
	{
		GlintInstance = UMaterialInstanceDynamic::Create(GlintMaterial, this);
		ScopeGlint->AddElement(GlintInstance, nullptr, true, GlintScreenSize, GlintScreenSize, nullptr);
	}
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
	if (HasAuthority() && DefaultWeapon)
	{
		SpawnWeapon(DefaultWeapon);
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
		Indicator->SetAlertView(AlertComponent->GetDisplayAlertColor(), AlertComponent->GetSuspicion(), AlertComponent->IsCallingReinforcements());
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
	RefreshWalkSpeed();
	RefreshWeaponCarry();
	if (NewState != EHitmenAlertState::Combat)
	{
		ClearCover();
		if (bIsCrouched)
		{
			UnCrouch();
		}
	}
	if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		// The body follows the controller's rotation in every state (the controller faces the way it walks, or its focus
		// within the strafe angle); fighting turns faster.
		const bool bFighting = NewState == EHitmenAlertState::Combat;
		Movement->bOrientRotationToMovement = false;
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

void AHitmenNPC::AssignPatrolRoute(AHitmenPatrolRoute* Route)
{
	if (!HasAuthority() || !Route)
	{
		return;
	}
	PatrolRoute = Route;
	Behavior = EHitmenNPCBehavior::Patrol;
	if (AHitmenAIController* AI = Cast<AHitmenAIController>(GetController()))
	{
		AI->RefreshNPCKeys();
	}
}

void AHitmenNPC::SetPaceMultiplier(float Multiplier)
{
	if (!FMath::IsNearlyEqual(PaceMultiplier, Multiplier))
	{
		PaceMultiplier = Multiplier;
		RefreshWalkSpeed();
	}
}

void AHitmenNPC::RefreshWalkSpeed()
{
	UHitmenCharacterMovementComponent* Movement = GetHitmenMovement();
	if (!Movement || !AlertComponent)
	{
		return;
	}
	const EHitmenAlertState State = AlertComponent->GetAlertState();
	// An unarmed VIP in danger runs to safety.
	if (IsVIP() && !Weapon && (State == EHitmenAlertState::Combat || State == EHitmenAlertState::Searching
		|| (State == EHitmenAlertState::Investigating && AlertComponent->IsThreatInvestigation())))
	{
		Movement->WalkSpeed = CombatWalkSpeed;
		return;
	}
	switch (State)
	{
	case EHitmenAlertState::Combat:        Movement->WalkSpeed = CombatWalkSpeed; break;
	case EHitmenAlertState::Investigating:
	case EHitmenAlertState::Searching:     Movement->WalkSpeed = AlertWalkSpeed; break;
	default:                               Movement->WalkSpeed = (AlertComponent->IsOnEdge() ? OnEdgeWalkSpeed : CalmWalkSpeed) * PaceMultiplier; break;
	}
}

float AHitmenNPC::GetEffectiveShootingRange() const
{
	if (!Weapon)
	{
		return UnarmedReach;
	}
	const float Own = IsSniper() || ShootingRange <= 0.f ? TNumericLimits<float>::Max() : ShootingRange;
	// A shotgun or an SMG closes in to its own range (the weapon's NPCEngageRange).
	const UHitmenWeaponData* Data = Weapon->GetWeaponData();
	return Data && Data->NPCEngageRange > 0.f ? FMath::Min(Own, Data->NPCEngageRange) : Own;
}

bool AHitmenNPC::IsInCover() const
{
	return bHasCover && FVector::Dist2D(GetActorLocation(), CoverLocation) <= CoverArrivalRadius;
}

FVector AHitmenNPC::GetPawnViewLocation() const
{
	if (bIsCrouched && IsInCover())
	{
		const ACharacter* Default = GetClass()->GetDefaultObject<ACharacter>();
		const float StandOffset = Default->GetCapsuleComponent()->GetUnscaledCapsuleHalfHeight() - GetCapsuleComponent()->GetUnscaledCapsuleHalfHeight();
		return GetActorLocation() + FVector(0.f, 0.f, StandOffset + Default->BaseEyeHeight);
	}
	return Super::GetPawnViewLocation();
}

int32 AHitmenNPC::AdvancePatrolIndex(int32 PointCount, bool bClosedLoop)
{
	PatrolResumeCount = 0;
	if (PointCount <= 0)
	{
		return INDEX_NONE;
	}
	if (bClosedLoop || PointCount == 1 || PatrolIndex == INDEX_NONE)
	{
		PatrolDirection = 1;
		PatrolIndex = (PatrolIndex + 1) % PointCount;
		return PatrolIndex;
	}
	// Open route: back and forth.
	if (PatrolIndex + PatrolDirection < 0 || PatrolIndex + PatrolDirection >= PointCount)
	{
		PatrolDirection = -PatrolDirection;
	}
	PatrolIndex = FMath::Clamp(PatrolIndex + PatrolDirection, 0, PointCount - 1);
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
	// In cover (Hitman): duck behind it during the pause between bursts, stand a moment before the next one.
	if (bInBurstPause && IsInCover() && Now < NextShotTime - CoverRiseLead)
	{
		if (!bIsCrouched)
		{
			Crouch();
		}
		return;
	}
	if (bIsCrouched)
	{
		UnCrouch();
	}
	if (Now < NextShotTime)
	{
		return;
	}
	bInBurstPause = false;
	Weapon->FireAt(Location, AimError);
	const float Interval = Weapon->GetWeaponData() ? Weapon->GetWeaponData()->GetFireInterval() : 0.3f;
	NextShotTime = Now + Interval * Rules.NPCFireIntervalMultiplier;
	if (--ShotsLeftInBurst <= 0)
	{
		ShotsLeftInBurst = Rules.NPCBurstShots;
		NextShotTime = Now + Rules.NPCBurstPause;
		bInBurstPause = true;
	}
}

int32 AHitmenNPC::GetHitmenTeamId_Implementation() const
{
	if (StatusEffectComponent && StatusEffectComponent->HasStatus(EHitmenStatusEffect::Charmed))
	{
		return UHitmenTeamLibrary::GetTeamId(StatusEffectComponent->GetStatusInstigator(EHitmenStatusEffect::Charmed));
	}
	// Guests are nobody's enemy (off in every map: the whole map is a hostile area).
	return bGuest ? HitmenTeams::Neutral : HitmenTeams::NPC;
}

void AHitmenNPC::BecomeGuard()
{
	if (!HasAuthority() || IsVIP())
	{
		return;
	}
	// Its own guard weapon, else its default one, else the project's guard weapon (Project Settings -> Hitmen -> AI):
	// a candidate that was not picked is a plain armed guard, never an unarmed VIP look-alike.
	UHitmenWeaponData* GuardWeapon = CandidateGuardWeapon ? CandidateGuardWeapon.Get() : DefaultWeapon.Get();
	if (!GuardWeapon)
	{
		GuardWeapon = UHitmenGameSettings::Get()->DefaultGuardWeapon.LoadSynchronous();
	}
	bGuest = false;
	RefreshWalkSpeed();
	if (!GuardWeapon || !GuardWeapon->WeaponClass || (Weapon && Weapon->GetWeaponData() == GuardWeapon))
	{
		return;
	}
	if (Weapon)
	{
		Weapon->Destroy();
		Weapon = nullptr;
	}
	DefaultWeapon = GuardWeapon;
	SpawnWeapon(GuardWeapon);
	if (AHitmenAIController* NPCAI = Cast<AHitmenAIController>(GetController()))
	{
		NPCAI->RefreshNPCKeys();
	}
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
