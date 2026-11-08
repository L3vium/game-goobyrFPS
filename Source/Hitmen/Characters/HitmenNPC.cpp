// Hitmen — NPC

#include "Characters/HitmenNPC.h"
#include "AI/HitmenAlertComponent.h"
#include "AI/HitmenAIController.h"
#include "Weapons/HitmenWeaponBase.h"
#include "Weapons/HitmenWeaponPickup.h"
#include "World/Interactables/HitmenKeycardPickup.h"
#include "Data/HitmenWeaponData.h"
#include "Data/HitmenMatchRules.h"
#include "Framework/HitmenGameModeBase.h"
#include "Framework/HitmenGameState.h"
#include "StatusEffects/HitmenStatusEffectComponent.h"
#include "Core/HitmenTeamLibrary.h"
#include "Components/WidgetComponent.h"
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
}

void AHitmenNPC::BeginPlay()
{
	Super::BeginPlay();
	AlertComponent->OnAlertStateChanged.AddDynamic(this, &AHitmenNPC::HandleAlertStateChanged);
	HandleAlertStateChanged(EHitmenAlertState::Unaware, AlertComponent->GetAlertState());
	if (GetNetMode() != NM_DedicatedServer)
	{
		AlertWidget->InitWidget();
		if (UHitmenAlertIndicatorWidget* Indicator = Cast<UHitmenAlertIndicatorWidget>(AlertWidget->GetUserWidgetObject()))
		{
			Indicator->SetAlertSource(AlertComponent);
		}
	}
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

void AHitmenNPC::HandleAlertStateChanged(EHitmenAlertState OldState, EHitmenAlertState NewState)
{
	if (UHitmenCharacterMovementComponent* Movement = GetHitmenMovement())
	{
		switch (NewState)
		{
		case EHitmenAlertState::Combat:        Movement->WalkSpeed = CombatWalkSpeed; break;
		case EHitmenAlertState::Investigating:
		case EHitmenAlertState::Searching:     Movement->WalkSpeed = AlertWalkSpeed; break;
		default:                               Movement->WalkSpeed = CalmWalkSpeed; break;
		}
	}
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
	Weapon->FireAt(Location, AimError);
}

int32 AHitmenNPC::GetHitmenTeamId_Implementation() const
{
	if (StatusEffectComponent && StatusEffectComponent->HasStatus(EHitmenStatusEffect::Charmed))
	{
		return UHitmenTeamLibrary::GetTeamId(StatusEffectComponent->GetStatusInstigator(EHitmenStatusEffect::Charmed));
	}
	return HitmenTeams::NPC;
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
			if (AHitmenKeycardPickup* Keycard = GetWorld()->SpawnActor<AHitmenKeycardPickup>(KeycardPickupClass, GetActorLocation(), FRotator::ZeroRotator, Params))
			{
				Keycard->SetKeycardId(CarriedKeycard);
			}
		}
		if (Weapon && DefaultWeapon)
		{
			FActorSpawnParameters Params;
			Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
			const TSubclassOf<AHitmenWeaponPickup> PickupClass = DefaultWeapon->PickupClass ? DefaultWeapon->PickupClass : TSubclassOf<AHitmenWeaponPickup>(AHitmenWeaponPickup::StaticClass());
			if (AHitmenWeaponPickup* Pickup = GetWorld()->SpawnActor<AHitmenWeaponPickup>(PickupClass, Weapon->GetActorLocation(), Weapon->GetActorRotation(), Params))
			{
				Pickup->InitPickup(DefaultWeapon, DroppedWeaponAmmo);
				Pickup->Throw(FVector::UpVector * 150.f, nullptr);
			}
			Weapon->Destroy();
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
	if (!HasAuthority())
	{
		return;
	}
	if (Status == EHitmenStatusEffect::Charmed)
	{
		// New side: forget the old target, look for hostiles of the new team.
		AlertComponent->SetAlertState(EHitmenAlertState::Searching);
	}
	RefreshBrainPause();
}

void AHitmenNPC::HandleStatusRemoved(EHitmenStatusEffect Status, AActor* StatusInstigator)
{
	Super::HandleStatusRemoved(Status, StatusInstigator);
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
