// Hitmen — emotes picked from the hold-R wheel, replicated to everyone

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HitmenEmoteComponent.generated.h"

UCLASS(ClassGroup=(Hitmen), Blueprintable, meta=(BlueprintSpawnableComponent))
class HITMEN_API UHitmenEmoteComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHitmenEmoteComponent();

	UFUNCTION(BlueprintCallable, Category="Hitmen|Emote")
	void PlayEmote(int32 EmoteIndex);

protected:
	UPROPERTY(EditDefaultsOnly, Category="Hitmen|Emote", meta=(ClampMin="0"))
	float EmoteCooldown = 1.f;

	UFUNCTION(Server, Reliable)
	void Server_PlayEmote(int32 EmoteIndex);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_PlayEmote(int32 EmoteIndex);

	UFUNCTION(BlueprintImplementableEvent, Category="Hitmen|Juice")
	void Juice_OnEmote(int32 EmoteIndex);

	float LastEmoteTime = -100.f;
};
