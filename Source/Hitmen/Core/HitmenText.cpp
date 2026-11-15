// Hitmen — translation sheet

#include "Core/HitmenText.h"
#include "Core/HitmenUserSettings.h"
#include "Hitmen.h"
#include "HAL/IConsoleManager.h"
#include "Internationalization/Culture.h"
#include "Internationalization/Internationalization.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"

UHitmenTextSubsystem* UHitmenTextSubsystem::Instance = nullptr;

namespace
{
	/** One CSV line into cells (quotes, doubled quotes, the separator inside quotes). */
	TArray<FString> SplitCsvLine(const FString& Line, TCHAR Separator)
	{
		TArray<FString> Cells;
		FString Cell;
		bool bQuoted = false;
		for (int32 Index = 0; Index < Line.Len(); ++Index)
		{
			const TCHAR C = Line[Index];
			if (bQuoted)
			{
				if (C == TEXT('"'))
				{
					if (Index + 1 < Line.Len() && Line[Index + 1] == TEXT('"'))
					{
						Cell.AppendChar(TEXT('"'));
						++Index;
					}
					else
					{
						bQuoted = false;
					}
				}
				else
				{
					Cell.AppendChar(C);
				}
			}
			else if (C == TEXT('"'))
			{
				bQuoted = true;
			}
			else if (C == Separator)
			{
				Cells.Add(Cell);
				Cell.Reset();
			}
			else
			{
				Cell.AppendChar(C);
			}
		}
		Cells.Add(Cell);
		return Cells;
	}

	/** Splits the file into records (a quoted cell may span lines). */
	TArray<FString> SplitCsvRecords(const FString& Text)
	{
		TArray<FString> Records;
		FString Current;
		bool bQuoted = false;
		for (const TCHAR C : Text)
		{
			if (C == TEXT('"'))
			{
				bQuoted = !bQuoted;
			}
			if (!bQuoted && (C == TEXT('\n') || C == TEXT('\r')))
			{
				if (!Current.IsEmpty())
				{
					Records.Add(Current);
					Current.Reset();
				}
				continue;
			}
			Current.AppendChar(C);
		}
		if (!Current.IsEmpty())
		{
			Records.Add(Current);
		}
		return Records;
	}

	FAutoConsoleCommand LanguageCommand(TEXT("HitmenText.Language"), TEXT("HitmenText.Language <code>: switches the game language (empty = system)."),
		FConsoleCommandWithArgsDelegate::CreateLambda([](const TArray<FString>& Args)
		{
			if (UHitmenTextSubsystem* Text = UHitmenTextSubsystem::GetInstance())
			{
				Text->SetLanguage(Args.Num() > 0 ? Args[0] : FString());
			}
		}));

	FAutoConsoleCommand ReloadCommand(TEXT("HitmenText.Reload"), TEXT("Reads Content/Hitmen/Localization/HitmenText.csv again."),
		FConsoleCommandDelegate::CreateLambda([]()
		{
			if (UHitmenTextSubsystem* Text = UHitmenTextSubsystem::GetInstance())
			{
				Text->Reload();
			}
		}));
}

FString UHitmenTextSubsystem::GetSheetPath()
{
	return FPaths::ProjectContentDir() / TEXT("Hitmen/Localization/HitmenText.csv");
}

void UHitmenTextSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	Instance = this;
	const UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings();
	RequestedLanguage = Settings ? Settings->Language : FString();
	LoadSheet();
}

void UHitmenTextSubsystem::Deinitialize()
{
	if (Instance == this)
	{
		Instance = nullptr;
	}
	Super::Deinitialize();
}

void UHitmenTextSubsystem::LoadSheet()
{
	Rows.Reset();
	KeyByEnglish.Reset();
	Columns.Reset();
	FString Text;
	if (!FFileHelper::LoadFileToString(Text, *GetSheetPath()))
	{
		UE_LOG(LogHitmen, Warning, TEXT("Text sheet not found: %s (English everywhere)."), *GetSheetPath());
		ResolveColumn();
		return;
	}
	TArray<FString> Records = SplitCsvRecords(Text);
	if (Records.Num() == 0)
	{
		ResolveColumn();
		return;
	}
	// Excel in French saves with ';', elsewhere with ',': the header row says which.
	const FString& Header = Records[0];
	int32 Semicolons = 0;
	int32 Commas = 0;
	for (const TCHAR C : Header)
	{
		Semicolons += C == TEXT(';');
		Commas += C == TEXT(',');
	}
	const TCHAR Separator = Semicolons > Commas ? TEXT(';') : TEXT(',');
	TArray<FString> HeaderCells = SplitCsvLine(Header, Separator);
	for (int32 Index = 1; Index < HeaderCells.Num(); ++Index)
	{
		Columns.Add(HeaderCells[Index].TrimStartAndEnd().ToLower());
	}
	EnglishColumn = Columns.IndexOfByKey(TEXT("en"));
	for (int32 Record = 1; Record < Records.Num(); ++Record)
	{
		TArray<FString> Cells = SplitCsvLine(Records[Record], Separator);
		if (Cells.Num() < 2 || Cells[0].TrimStartAndEnd().IsEmpty())
		{
			continue;
		}
		const FString Key = Cells[0].TrimStartAndEnd();
		Cells.RemoveAt(0);
		Cells.SetNum(Columns.Num());
		if (EnglishColumn != INDEX_NONE && !Cells[EnglishColumn].IsEmpty())
		{
			KeyByEnglish.Add(Cells[EnglishColumn], Key);
		}
		Rows.Add(Key, MoveTemp(Cells));
	}
	ResolveColumn();
	UE_LOG(LogHitmen, Log, TEXT("Text sheet: %d texts, languages %s, using %s."), Rows.Num(), *FString::Join(Columns, TEXT(" ")), *GetActiveLanguage());
}

void UHitmenTextSubsystem::ResolveColumn()
{
	FString Code = RequestedLanguage.ToLower();
	if (Code.IsEmpty())
	{
		// Auto: the system language when the sheet has it.
		Code = FInternationalization::Get().GetCurrentCulture()->GetTwoLetterISOLanguageName().ToLower();
	}
	ActiveColumn = Columns.IndexOfByKey(Code);
	if (ActiveColumn == INDEX_NONE)
	{
		ActiveColumn = EnglishColumn;
	}
}

void UHitmenTextSubsystem::Reload()
{
	LoadSheet();
	OnLanguageChanged.Broadcast();
}

void UHitmenTextSubsystem::SetLanguage(const FString& Code)
{
	RequestedLanguage = Code;
	if (UHitmenUserSettings* Settings = UHitmenUserSettings::GetHitmenUserSettings())
	{
		Settings->Language = Code;
		Settings->SaveSettings();
	}
	ResolveColumn();
	OnLanguageChanged.Broadcast();
}

FString UHitmenTextSubsystem::GetActiveLanguage() const
{
	return Columns.IsValidIndex(ActiveColumn) ? Columns[ActiveColumn] : FString(TEXT("en"));
}

FText UHitmenTextSubsystem::GetLanguageName(const FString& Code) const
{
	const TArray<FString>* Row = Rows.Find(TEXT("_LanguageName"));
	const int32 Column = Columns.IndexOfByKey(Code.ToLower());
	if (Row && Row->IsValidIndex(Column) && !(*Row)[Column].IsEmpty())
	{
		return FText::FromString((*Row)[Column]);
	}
	return FText::FromString(Code.ToUpper());
}

const FString* UHitmenTextSubsystem::Find(const FString& Key) const
{
	const TArray<FString>* Row = Rows.Find(Key);
	if (!Row)
	{
		return nullptr;
	}
	if (Row->IsValidIndex(ActiveColumn) && !(*Row)[ActiveColumn].IsEmpty())
	{
		return &(*Row)[ActiveColumn];
	}
	if (Row->IsValidIndex(EnglishColumn) && !(*Row)[EnglishColumn].IsEmpty())
	{
		return &(*Row)[EnglishColumn];
	}
	return nullptr;
}

FText UHitmenTextSubsystem::Get(const TCHAR* Key, const TCHAR* Default)
{
	if (Instance)
	{
		if (const FString* Found = Instance->Find(Key))
		{
			return FText::FromString(*Found);
		}
	}
	return FText::FromString(Default);
}

FText UHitmenTextSubsystem::Translate(const FText& English)
{
	if (Instance && !English.IsEmpty())
	{
		if (const FString* Key = Instance->KeyByEnglish.Find(English.ToString()))
		{
			if (const FString* Found = Instance->Find(*Key))
			{
				return FText::FromString(*Found);
			}
		}
	}
	return English;
}

FText UHitmenTextSubsystem::GetText(const FString& Key, const FString& Default)
{
	return Get(*Key, *Default);
}
