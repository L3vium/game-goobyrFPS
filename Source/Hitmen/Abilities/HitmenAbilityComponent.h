// Hitmen — holds the move (E) and manipulation (A) abilities and routes input/RPCs to them

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Core/HitmenTypes.h"
#include "HitmenAbilityComponent.generated.h"

class UMaterialInterface;
class UStaticMesh;
class UStaticMeshComponent;

class UHitmenAbility;
class UHitmenAbilityData;

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenAbilitiesChanged);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnHitmenAbilityFailed, UHitmenAbility*, Ability, const FText&, Reason);

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenAbilityComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenAbilityComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Abilities")
	FOnHitmenAbilitiesChanged OnAbilitiesChanged;

	/** Owning client: a use was refused by the server, with the reason to show. */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Abilities")
	FOnHitmenAbilityFailed OnAbilityFailed;

	/** Server: equips an ability in its slot (replaces the previous one). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Hitmen|Abilities")
	void SetAbility(UHitmenAbilityData* AbilityData);

	UFUNCTION(BlueprintPure, Category="Hitmen|Abilities")
	UHitmenAbility* GetAbility(EHitmenAbilitySlot Slot) const;

	/** Owning client input. */
	void PressSlot(EHitmenAbilitySlot Slot);
	void ReleaseSlot(EHitmenAbilitySlot Slot);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_AbilityActivated(UHitmenAbility* Ability, FVector_NetQuantize Location);

	/** Server -> owning client: the ability refused to activate (no charge spent). */
	UFUNCTION(Client, Reliable)
	void Client_AbilityFailed(UHitmenAbility* Ability, const FText& Reason);

protected:
	/** Also show failure reasons in the centre prompt (until the UI has a dedicated toast). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities")
	bool bShowFailureAsPrompt = true;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities", meta=(ClampMin="0.1", Units="s"))
	float FailurePromptDuration = 1.5f;

	/** Glow on remote-ability targets under the crosshair (overlay material). Empty = no glow. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities")
	TObjectPtr<UMaterialInterface> HintOverlayMaterial;

	/** Seconds between two target scans for the hint (local player only). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities", meta=(ClampMin="0.02", Units="s"))
	float HintScanInterval = 0.1f;

	/** Cable of pulling abilities (hook): a thin cylinder from the hand to the anchor, seen by everyone. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	TObjectPtr<UStaticMesh> CableMesh;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	TObjectPtr<UMaterialInterface> CableMaterial;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	FLinearColor CableColor = FLinearColor(0.08f, 0.08f, 0.09f);

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals", meta=(ClampMin="0.1", Units="cm"))
	float CableThickness = 1.2f;

	/** Where the cable starts for the local player, in camera space (forward, right, up): low right, in view. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	FVector FirstPersonCableOffset = FVector(45.f, 20.f, -24.f);

	/** Third person: the hand socket the cable leaves from (the actor centre when missing). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	FName CableSocket = TEXT("hand_r");

	/** Local player: a small marker where the aimed ability will land (hook in range). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	TObjectPtr<UStaticMesh> AimMarkerMesh;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	TObjectPtr<UMaterialInterface> AimMarkerMaterial;

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	FLinearColor AimMarkerColor = FLinearColor(1.f, 1.f, 1.f);

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals", meta=(ClampMin="1", Units="cm"))
	float AimMarkerSize = 12.f;

	UPROPERTY(Transient)
	TObjectPtr<UStaticMeshComponent> CableComponent;

	UPROPERTY(Transient)
	TObjectPtr<UStaticMeshComponent> AimMarkerComponent;

	/** Multi-kill: red links between the marked targets and a red glow on them (owner only). */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals")
	FLinearColor MarkColor = FLinearColor(1.f, 0.05f, 0.05f);

	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Abilities|Visuals", meta=(ClampMin="0.1", Units="cm"))
	float MarkLinkThickness = 4.f;

	UPROPERTY(Transient)
	TArray<TObjectPtr<UStaticMeshComponent>> MarkLinks;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> MarkHighlightMaterial;

	TArray<TWeakObjectPtr<AActor>> MarkHighlighted;
	void UpdateMarkLinks();

	/** Cable (every machine) and aim marker (local player), each frame. */
	void UpdateAbilityVisuals();
	UStaticMeshComponent* MakeVisual(UStaticMesh* Mesh, UMaterialInterface* Material, const FLinearColor& Color);

	/** Local: highlights the target the manipulation (then move) ability would act on. */
	void UpdateTargetHint();
	void ClearTargetHint();

	FTimerHandle HintTimer;
	TWeakObjectPtr<AActor> HintedActor;

	FTimerHandle FailurePromptTimer;

	void ClearFailurePrompt();

	UPROPERTY(ReplicatedUsing=OnRep_Abilities)
	TObjectPtr<UHitmenAbility> MoveAbility;

	UPROPERTY(ReplicatedUsing=OnRep_Abilities)
	TObjectPtr<UHitmenAbility> ManipulationAbility;

	UFUNCTION(Server, Reliable)
	void Server_Press(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction);

	UFUNCTION(Server, Reliable)
	void Server_Release(EHitmenAbilitySlot Slot, FVector_NetQuantize Origin, FVector_NetQuantizeNormal Direction);

	UFUNCTION() void OnRep_Abilities();

	void GetAim(FVector& OutOrigin, FVector& OutDirection) const;

	/** Remembers previously equipped abilities on clients to call OnUnequipped. */
	UPROPERTY(Transient)
	TArray<TObjectPtr<UHitmenAbility>> KnownAbilities;
};
