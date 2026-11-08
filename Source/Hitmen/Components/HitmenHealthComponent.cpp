// Hitmen — health

#include "Components/HitmenHealthComponent.h"
#include "Net/UnrealNetwork.h"

UHitmenHealthComponent::UHitmenHealthComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}

void UHitmenHealthComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHitmenHealthComponent, Health);
	DOREPLIFETIME(UHitmenHealthComponent, Armor);
	DOREPLIFETIME(UHitmenHealthComponent, bIsDead);
	DOREPLIFETIME(UHitmenHealthComponent, bHeadshotDeath);
}

void UHitmenHealthComponent::BeginPlay()
{
	Super::BeginPlay();
	if (GetOwner()->HasAuthority())
	{
		Health = MaxHealth;
	}
	LastReplicatedHealth = Health;
}

float UHitmenHealthComponent::ApplyDamage(float Amount, AActor* Instigator, AActor* DamageCauser, bool bHeadshot)
{
	if (!GetOwner()->HasAuthority() || bIsDead || bGodMode || Amount <= 0.f)
	{
		return 0.f;
	}
	const float Absorbed = FMath::Min(Armor, Amount);
	Armor -= Absorbed;
	const float ToHealth = Amount - Absorbed;
	const float Old = Health;
	Health = FMath::Clamp(Health - ToHealth, 0.f, MaxHealth);
	OnHealthChanged.Broadcast(this, Health, Health - Old, Instigator);
	LastReplicatedHealth = Health;
	if (Health <= 0.f)
	{
		Die(Instigator, DamageCauser, bHeadshot);
	}
	return Amount;
}

void UHitmenHealthComponent::Heal(float Amount)
{
	if (!GetOwner()->HasAuthority() || bIsDead || Amount <= 0.f)
	{
		return;
	}
	const float Old = Health;
	Health = FMath::Clamp(Health + Amount, 0.f, MaxHealth);
	OnHealthChanged.Broadcast(this, Health, Health - Old, nullptr);
	LastReplicatedHealth = Health;
}

void UHitmenHealthComponent::AddArmor(float Amount)
{
	if (GetOwner()->HasAuthority())
	{
		Armor = FMath::Clamp(Armor + Amount, 0.f, MaxArmor);
	}
}

void UHitmenHealthComponent::Kill(AActor* Instigator, AActor* DamageCauser, bool bHeadshot)
{
	if (!GetOwner()->HasAuthority() || bIsDead)
	{
		return;
	}
	const float Old = Health;
	Health = 0.f;
	OnHealthChanged.Broadcast(this, Health, -Old, Instigator);
	Die(Instigator, DamageCauser, bHeadshot);
}

void UHitmenHealthComponent::Die(AActor* Instigator, AActor* DamageCauser, bool bHeadshot)
{
	bIsDead = true;
	bHeadshotDeath = bHeadshot;
	OnDeath.Broadcast(Instigator, DamageCauser, bHeadshot);
}

void UHitmenHealthComponent::OnRep_Health()
{
	OnHealthChanged.Broadcast(this, Health, Health - LastReplicatedHealth, nullptr);
	LastReplicatedHealth = Health;
}

void UHitmenHealthComponent::OnRep_Dead()
{
	if (bIsDead)
	{
		OnDeath.Broadcast(nullptr, nullptr, bHeadshotDeath);
	}
}
