// Hitmen — every text of the game in every language, from one spreadsheet the team edits in Excel:
// Content/Hitmen/Localization/HitmenText.csv (Key, en, fr, ...; ';' or ',' separated, UTF-8). A new column is a new
// language. Code uses HITMEN_TEXT(<key>, <English text>) (the English is the fallback when the sheet has no entry).

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "HitmenText.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnHitmenLanguageChanged);

UCLASS()
class HITMEN_API UHitmenTextSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	/** The text of Key in the current language; Default (English) when the sheet has none. Works anywhere (before the
	 *  game instance exists it returns Default). */
	static FText Get(const TCHAR* Key, const TCHAR* Default);

	/** A text authored in English elsewhere (data assets: weapon, ability, NPC names) in the current language, when the
	 *  sheet has a row whose English is exactly that text; otherwise the text itself. */
	static FText Translate(const FText& English);

	UFUNCTION(BlueprintPure, Category="Hitmen|Text", meta=(DisplayName="Hitmen Text"))
	static FText GetText(const FString& Key, const FString& Default);

	/** "" = follow the system language (English when the sheet has no column for it). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Text")
	void SetLanguage(const FString& Code);

	/** The language in use ("en", "fr"...). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Text")
	FString GetActiveLanguage() const;

	/** Language codes the sheet has (its columns after Key). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Text")
	TArray<FString> GetLanguages() const { return Columns; }

	/** "English", "Français"... for a code (the sheet's "_LanguageName" row, else the code). */
	UFUNCTION(BlueprintPure, Category="Hitmen|Text")
	FText GetLanguageName(const FString& Code) const;

	/** Reads the sheet again (console: HitmenText.Reload). */
	UFUNCTION(BlueprintCallable, Category="Hitmen|Text")
	void Reload();

	UPROPERTY(BlueprintAssignable, Category="Hitmen|Text")
	FOnHitmenLanguageChanged OnLanguageChanged;

	static UHitmenTextSubsystem* GetInstance() { return Instance; }

	/** Where the sheet is read from. */
	static FString GetSheetPath();

private:
	static UHitmenTextSubsystem* Instance;

	/** Key -> one text per column. */
	TMap<FString, TArray<FString>> Rows;
	/** English text -> key (Translate). */
	TMap<FString, FString> KeyByEnglish;
	TArray<FString> Columns;
	int32 ActiveColumn = INDEX_NONE;
	int32 EnglishColumn = INDEX_NONE;
	FString RequestedLanguage;

	void LoadSheet();
	void ResolveColumn();
	const FString* Find(const FString& Key) const;
};

/** A text from the translation sheet: HITMEN_TEXT("Hud.Throw", "Throw"). */
#define HITMEN_TEXT(Key, Default) UHitmenTextSubsystem::Get(TEXT(Key), TEXT(Default))
