#include "CuratedLaunchContract.h"

#include "Dom/JsonObject.h"
#include "HAL/PlatformMisc.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

namespace
{
	const TCHAR* const LaunchVariables[] = {
		TEXT("CURATED_LAUNCHER"),
		TEXT("CURATED_CONTRACT_VERSION"),
		TEXT("CURATED_APP_ID"),
		TEXT("CURATED_SESSION_TOKEN"),
		TEXT("CURATED_API_BASE"),
		TEXT("CURATED_USER_ID"),
		TEXT("CURATED_USERNAME"),
		TEXT("CURATED_LANGUAGE"),
	};

	FString ReadVar(const TMap<FString, FString>& Environment, const TCHAR* Name)
	{
		const FString* Value = Environment.Find(Name);
		return Value ? Value->TrimStartAndEnd() : FString();
	}

	bool ParsePositiveInteger(const FString& Text, int64& OutValue)
	{
		if (Text.IsEmpty() || Text.Len() > 18)
		{
			return false;
		}
		for (const TCHAR C : Text)
		{
			if (!FChar::IsDigit(C))
			{
				return false;
			}
		}
		OutValue = FCString::Atoi64(*Text);
		return OutValue > 0;
	}
}

namespace CuratedLaunchContract
{
	FString DefaultApiBase()
	{
		return TEXT("https://api.curated.games");
	}

	FString ErrorCode(ECuratedLaunchError Error)
	{
		switch (Error)
		{
		case ECuratedLaunchError::None: return FString();
		case ECuratedLaunchError::NotLaunchedByCurated: return TEXT("not_launched_by_curated");
		case ECuratedLaunchError::UnsupportedContractVersion: return TEXT("unsupported_contract_version");
		case ECuratedLaunchError::AppMismatch: return TEXT("app_mismatch");
		case ECuratedLaunchError::SessionInvalid: return TEXT("session_invalid");
		case ECuratedLaunchError::NotEntitled: return TEXT("not_entitled");
		case ECuratedLaunchError::NetworkError: return TEXT("network_error");
		default: return TEXT("hub_error");
		}
	}

	TMap<FString, FString> ReadProcessEnvironment()
	{
		TMap<FString, FString> Environment;
		for (const TCHAR* Name : LaunchVariables)
		{
			const FString Value = FPlatformMisc::GetEnvironmentVariable(Name);
			if (!Value.IsEmpty())
			{
				Environment.Add(Name, Value);
			}
		}
		return Environment;
	}

	ECuratedLaunchError Parse(const TMap<FString, FString>& Environment, int64 ExpectedAppId, FCuratedLaunchContext& OutContext)
	{
		const FString Token = ReadVar(Environment, TEXT("CURATED_SESSION_TOKEN"));
		if (ReadVar(Environment, TEXT("CURATED_LAUNCHER")) != TEXT("1") || Token.IsEmpty())
		{
			return ECuratedLaunchError::NotLaunchedByCurated;
		}

		int64 Version = 0;
		if (!ParsePositiveInteger(ReadVar(Environment, TEXT("CURATED_CONTRACT_VERSION")), Version) || Version > SupportedContractVersion)
		{
			return ECuratedLaunchError::UnsupportedContractVersion;
		}

		int64 AppId = 0;
		if (!ParsePositiveInteger(ReadVar(Environment, TEXT("CURATED_APP_ID")), AppId) || AppId != ExpectedAppId)
		{
			return ECuratedLaunchError::AppMismatch;
		}

		FString ApiBase = DefaultApiBase();
		FString ApiRaw = ReadVar(Environment, TEXT("CURATED_API_BASE"));
		if (!ApiRaw.IsEmpty())
		{
			while (ApiRaw.EndsWith(TEXT("/")))
			{
				ApiRaw.LeftChopInline(1);
			}
			const bool bHasScheme = ApiRaw.StartsWith(TEXT("https://"), ESearchCase::IgnoreCase)
				|| ApiRaw.StartsWith(TEXT("http://"), ESearchCase::IgnoreCase);
			int32 SchemeEnd = INDEX_NONE;
			ApiRaw.FindChar(TEXT(':'), SchemeEnd);
			const FString Host = bHasScheme ? ApiRaw.Mid(SchemeEnd + 3) : FString();
			if (!bHasScheme || Host.IsEmpty() || Host.Contains(TEXT(" ")))
			{
				return ECuratedLaunchError::NotLaunchedByCurated;
			}
			ApiBase = ApiRaw;
		}

		OutContext.ContractVersion = static_cast<int32>(Version);
		OutContext.AppId = AppId;
		OutContext.SessionToken = Token;
		OutContext.ApiBase = ApiBase;
		OutContext.UserIdHint = ReadVar(Environment, TEXT("CURATED_USER_ID"));
		OutContext.UsernameHint = ReadVar(Environment, TEXT("CURATED_USERNAME"));
		OutContext.Language = ReadVar(Environment, TEXT("CURATED_LANGUAGE"));
		return ECuratedLaunchError::None;
	}

	ECuratedLaunchError ClassifyStatus(int32 StatusCode)
	{
		if (StatusCode == 401)
		{
			return ECuratedLaunchError::SessionInvalid;
		}
		return StatusCode >= 200 && StatusCode < 300 ? ECuratedLaunchError::None : ECuratedLaunchError::HubError;
	}

	bool FindEntitlement(const FString& LibraryJson, int64 AppId, FCuratedEntitlement& OutEntitlement)
	{
		TArray<TSharedPtr<FJsonValue>> Items;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(LibraryJson);
		if (!FJsonSerializer::Deserialize(Reader, Items))
		{
			return false;
		}

		OutEntitlement = FCuratedEntitlement();
		OutEntitlement.AppId = AppId;
		for (const TSharedPtr<FJsonValue>& Item : Items)
		{
			const TSharedPtr<FJsonObject>* Object = nullptr;
			double ItemAppId = 0;
			if (!Item.IsValid() || !Item->TryGetObject(Object) || !(*Object)->TryGetNumberField(TEXT("app_id"), ItemAppId))
			{
				continue;
			}
			if (static_cast<int64>(ItemAppId) == AppId)
			{
				OutEntitlement.bIsEntitled = true;
				(*Object)->TryGetStringField(TEXT("source"), OutEntitlement.Source);
				(*Object)->TryGetStringField(TEXT("acquired_at"), OutEntitlement.AcquiredAt);
				break;
			}
		}
		return true;
	}
}
