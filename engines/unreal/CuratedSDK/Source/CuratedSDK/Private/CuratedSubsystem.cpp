#include "CuratedSubsystem.h"

#include "Dom/JsonObject.h"
#include "GenericPlatform/GenericPlatformHttp.h"
#include "HttpModule.h"
#include "Interfaces/IHttpRequest.h"
#include "Interfaces/IHttpResponse.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

void UCuratedSubsystem::InitFromLauncher(int64 AppId)
{
	bReady = false;
	Session = FCuratedSession();
	Context = FCuratedLaunchContext();
	const uint32 Generation = ++LaunchGeneration;

	FCuratedLaunchContext Parsed;
	const ECuratedLaunchError ParseError = CuratedLaunchContract::Parse(CuratedLaunchContract::ReadProcessEnvironment(), AppId, Parsed);
	if (ParseError != ECuratedLaunchError::None)
	{
		FinishLaunch(ParseError);
		return;
	}
	PendingContext = Parsed;

	SendRequest(PendingContext, TEXT("GET"), TEXT("/auth/me"), [this, Generation](ECuratedLaunchError MeError, const FString& MeBody)
	{
		if (Generation != LaunchGeneration)
		{
			return;
		}
		if (MeError != ECuratedLaunchError::None)
		{
			FinishLaunch(MeError);
			return;
		}

		TSharedPtr<FJsonObject> Me;
		FString UserId;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(MeBody);
		if (!FJsonSerializer::Deserialize(Reader, Me) || !Me.IsValid() || !Me->TryGetStringField(TEXT("user_id"), UserId))
		{
			FinishLaunch(ECuratedLaunchError::HubError);
			return;
		}
		Session.UserId = UserId;
		Me->TryGetStringField(TEXT("username"), Session.Username);

		SendRequest(PendingContext, TEXT("GET"), TEXT("/library"), [this, Generation](ECuratedLaunchError LibraryError, const FString& LibraryBody)
		{
			if (Generation != LaunchGeneration)
			{
				return;
			}
			if (LibraryError != ECuratedLaunchError::None)
			{
				FinishLaunch(LibraryError);
				return;
			}
			FCuratedEntitlement Entitlement;
			if (!CuratedLaunchContract::FindEntitlement(LibraryBody, PendingContext.AppId, Entitlement))
			{
				FinishLaunch(ECuratedLaunchError::HubError);
				return;
			}
			if (!Entitlement.bIsEntitled)
			{
				FinishLaunch(ECuratedLaunchError::NotEntitled);
				return;
			}
			Session.Entitlement = Entitlement;
			FinishLaunch(ECuratedLaunchError::None);
		});
	});
}

void UCuratedSubsystem::FinishLaunch(ECuratedLaunchError Error)
{
	if (Error == ECuratedLaunchError::None)
	{
		Context = PendingContext;
		Session.AppId = Context.AppId;
		Session.Language = Context.Language;
		bReady = true;
	}
	else
	{
		Session = FCuratedSession();
	}
	PendingContext = FCuratedLaunchContext();
	OnLaunchCompleted.Broadcast(Error, Session);
}

void UCuratedSubsystem::CheckEntitlement(FCuratedEntitlementChecked OnChecked)
{
	if (!bReady)
	{
		OnChecked.ExecuteIfBound(ECuratedLaunchError::NotLaunchedByCurated, FCuratedEntitlement());
		return;
	}
	const int64 AppId = Context.AppId;
	SendRequest(Context, TEXT("GET"), TEXT("/library"), [OnChecked, AppId](ECuratedLaunchError Error, const FString& Body)
	{
		FCuratedEntitlement Entitlement;
		if (Error == ECuratedLaunchError::None && !CuratedLaunchContract::FindEntitlement(Body, AppId, Entitlement))
		{
			Error = ECuratedLaunchError::HubError;
		}
		OnChecked.ExecuteIfBound(Error, Entitlement);
	});
}

void UCuratedSubsystem::UnlockAchievement(const FString& ApiName, FCuratedAchievementUnlocked OnUnlocked)
{
	const FString Trimmed = ApiName.TrimStartAndEnd();
	if (!bReady || Trimmed.IsEmpty())
	{
		OnUnlocked.ExecuteIfBound(bReady ? ECuratedLaunchError::HubError : ECuratedLaunchError::NotLaunchedByCurated, false);
		return;
	}
	const FString Path = FString::Printf(TEXT("/games/app/%lld/achievements/%s/unlock"), Context.AppId, *FGenericPlatformHttp::UrlEncode(Trimmed));
	SendRequest(Context, TEXT("POST"), Path, [OnUnlocked](ECuratedLaunchError Error, const FString& Body)
	{
		bool bNewlyUnlocked = false;
		TSharedPtr<FJsonObject> Result;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Body);
		if (Error == ECuratedLaunchError::None && FJsonSerializer::Deserialize(Reader, Result) && Result.IsValid())
		{
			Result->TryGetBoolField(TEXT("newly_unlocked"), bNewlyUnlocked);
		}
		OnUnlocked.ExecuteIfBound(Error, bNewlyUnlocked);
	});
}

void UCuratedSubsystem::SendRequest(const FCuratedLaunchContext& RequestContext, const FString& Verb, const FString& Path, FResponseHandler OnDone)
{
	const TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->SetURL(RequestContext.ApiBase + Path);
	Request->SetVerb(Verb);
	Request->SetHeader(TEXT("Authorization"), FString::Printf(TEXT("Bearer %s"), *RequestContext.SessionToken));
	Request->SetHeader(TEXT("Accept"), TEXT("application/json"));
	if (Verb == TEXT("POST"))
	{
		Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));
		Request->SetContentAsString(TEXT("{}"));
	}
	TWeakObjectPtr<UCuratedSubsystem> WeakThis(this);
	Request->OnProcessRequestComplete().BindLambda([WeakThis, OnDone](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnected)
	{
		if (!WeakThis.IsValid())
		{
			return;
		}
		if (!bConnected || !Response.IsValid())
		{
			OnDone(ECuratedLaunchError::NetworkError, FString());
			return;
		}
		OnDone(CuratedLaunchContract::ClassifyStatus(Response->GetResponseCode()), Response->GetContentAsString());
	});
	Request->ProcessRequest();
}

void UCuratedSubsystem::Deinitialize()
{
	++LaunchGeneration;
	bReady = false;
	Context = FCuratedLaunchContext();
	PendingContext = FCuratedLaunchContext();
	Session = FCuratedSession();
	Super::Deinitialize();
}
