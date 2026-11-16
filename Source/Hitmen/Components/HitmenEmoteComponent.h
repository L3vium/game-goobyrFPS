// Hitmen — emotes picked from the hold-R wheel, replicated to everyone

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenEmoteComponent.generated.h"

class UAnimMontage;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnHitmenEmoteChanged, int32, EmoteIndex);

/**
 * The running emote is replicated state (ActiveEmote, INDEX_NONE when none): every machine plays its montage, loops
 * it while it runs and stops it when it ends; the owner's view goes to third person meanwhile (the character binds
 * OnEmoteChanged). R again, moving, jumping, firing or any action ends it.
 */
UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenEmoteComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenEmoteComponent();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	/** Local request: plays this emote (index in the anim set's Emotes). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Emote")
	void PlayEmote(int32 EmoteIndex);

	/** Local request: ends the running emote (no-op when none runs). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Emote")
	void StopEmote();

	/** Server: ends the running emote at once (death, damage, a cancel from the owner). */
	void ServerStopEmote();

	UFUNCTION(BlueprintPure, Category="Hitmen|Emote")
	bool IsEmoting() const { return ActiveEmote != INDEX_NONE; }

	UFUNCTION(BlueprintPure, Category="Hitmen|Emote")
	int32 GetActiveEmote() const { return ActiveEmote; }

	/** Fires on every machine when an emote starts (index) or ends (INDEX_NONE). */
	UPROPERTY(BlueprintAssignable, Category="Hitmen|Emote")
	FOnHitmenEmoteChanged OnEmoteChanged;

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Emote", meta=(ClampMin="0", Units="s"))
	float EmoteCooldown = 0.5f;

	/** Emotes loop until cancelled (R again, moving, an action). Off: each one plays once and ends. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Emote")
	bool bLoopEmotes = true;

	/** Blend out time of the montage when the emote is cancelled. */
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Emote", meta=(ClampMin="0", Units="s"))
	float StopBlendOutTime = 0.2f;

	UFUNCTION(Server, Reliable)
	void Server_PlayEmote(int32 EmoteIndex);

	UFUNCTION(Server, Reliable)
	void Server_StopEmote();

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEmote(int32 EmoteIndex);

	/** Fires when an emote ends (cancelled or played out). */
	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEmoteEnded();

	UFUNCTION()
	void OnRep_ActiveEmote();

	UFUNCTION()
	void HandleMontageEnded(UAnimMontage* Montage, bool bInterrupted);

	UAnimMontage* GetEmoteMontage(int32 EmoteIndex) const;
	void ApplyEmote();
	void PlayMontageNow();

	UPROPERTY(ReplicatedUsing=OnRep_ActiveEmote)
	int32 ActiveEmote = INDEX_NONE;

	/** Emote applied on this machine (the montage playing). */
	int32 AppliedEmote = INDEX_NONE;

	UPROPERTY(Transient)
	TObjectPtr<UAnimMontage> PlayingMontage;

	float LastEmoteTime = -100.f;
	FTimerHandle EndTimer;
};
