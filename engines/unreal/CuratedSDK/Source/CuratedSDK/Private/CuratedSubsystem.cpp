#include "CuratedSubsystem.h"

#include "Async/Async.h"
#include "Dom/JsonObject.h"
#include "GenericPlatform/GenericPlatformHttp.h"
#include "HttpModule.h"
#include "Interfaces/IHttpRequest.h"
#include "Interfaces/IHttpResponse.h"
#include "IPAddress.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"
#include "SocketSubsystem.h"
#include "Sockets.h"

namespace
{
	void NotifyOverlayAchievement(const TSharedPtr<FJsonObject>& Body, int64 FallbackAppId)
	{
		const FString Endpoint = FPlatformMisc::GetEnvironmentVariable(TEXT("CURATED_OVERLAY_ENDPOINT")).TrimStartAndEnd();
		const FString Token = FPlatformMisc::GetEnvironmentVariable(TEXT("CURATED_OVERLAY_TOKEN")).TrimStartAndEnd();
		if (Endpoint.IsEmpty() || Token.IsEmpty() || !Body.IsValid())
		{
			return;
		}
		int32 Colon = INDEX_NONE;
		if (!Endpoint.FindLastChar(TEXT(':'), Colon) || Colon <= 0)
		{
			return;
		}
		const FString Host = Endpoint.Left(Colon).TrimStartAndEnd();
		const int32 Port = FCString::Atoi(*Endpoint.Mid(Colon + 1).TrimStartAndEnd());
		if (Host.IsEmpty() || Port <= 0)
		{
			return;
		}

		TSharedRef<FJsonObject> Payload = MakeShared<FJsonObject>();
		Payload->SetNumberField(TEXT("v"), 1);
		Payload->SetStringField(TEXT("token"), Token);
		Payload->SetStringField(TEXT("type"), TEXT("achievement_unlocked"));
		double AppId = static_cast<double>(FallbackAppId);
		Body->TryGetNumberField(TEXT("app_id"), AppId);
		Payload->SetNumberField(TEXT("app_id"), AppId);
		FString ApiName;
		Body->TryGetStringField(TEXT("api_name"), ApiName);
		Payload->SetStringField(TEXT("api_name"), ApiName);
		FString DisplayName = ApiName;
		Body->TryGetStringField(TEXT("display_name"), DisplayName);
		Payload->SetStringField(TEXT("display_name"), DisplayName);
		FString Description;
		Body->TryGetStringField(TEXT("description"), Description);
		Payload->SetStringField(TEXT("description"), Description);
		FString IconUrl;
		if (Body->TryGetStringField(TEXT("icon_achieved_uri"), IconUrl) && !IconUrl.IsEmpty())
		{
			Payload->SetStringField(TEXT("icon_url"), IconUrl);
		}
		double GlobalPct = 0;
		if (Body->TryGetNumberField(TEXT("global_unlock_percent"), GlobalPct))
		{
			Payload->SetNumberField(TEXT("global_unlock_percent"), GlobalPct);
		}

		FString Json;
		const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Json);
		FJsonSerializer::Serialize(Payload, Writer);
		Json += TEXT("\n");

		ISocketSubsystem* Sockets = ISocketSubsystem::Get(PLATFORM_SOCKETSUBSYSTEM);
		if (!Sockets)
		{
			return;
		}
		TSharedRef<FInternetAddr> Addr = Sockets->CreateInternetAddr();
		bool bValid = false;
		Addr->SetIp(*Host, bValid);
		Addr->SetPort(Port);
		if (!bValid)
		{
			return;
		}
		FSocket* Socket = Sockets->CreateSocket(NAME_Stream, TEXT("CuratedOverlay"), false);
		if (!Socket)
		{
			return;
		}
		Socket->SetNonBlocking(false);
		if (Socket->Connect(*Addr))
		{
			int32 Sent = 0;
			const FTCHARToUTF8 Utf8(*Json);
			Socket->Send(reinterpret_cast<const uint8*>(Utf8.Get()), Utf8.Length(), Sent);
		}
		Sockets->DestroySocket(Socket);
	}
}

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
		FString CuratedId;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(MeBody);
		if (!FJsonSerializer::Deserialize(Reader, Me) || !Me.IsValid()
			|| !Me->TryGetStringField(TEXT("user_id"), UserId)
			|| !Me->TryGetStringField(TEXT("curated_id"), CuratedId)
			|| CuratedId.TrimStartAndEnd().IsEmpty())
		{
			FinishLaunch(ECuratedLaunchError::HubError);
			return;
		}
		Session.UserId = UserId;
		Session.CuratedId = CuratedId.TrimStartAndEnd();
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
		const FString Normalized = CuratedLaunchContract::NormalizeGameLanguage(Context.Language);
		Session.CurrentGameLanguage = Normalized.IsEmpty() ? CuratedLaunchContract::DefaultGameLanguage() : Normalized;
		bReady = true;
	}
	else
	{
		Session = FCuratedSession();
	}
	PendingContext = FCuratedLaunchContext();
	OnLaunchCompleted.Broadcast(Error, Session);
}

FString UCuratedSubsystem::GetCurrentGameLanguage()
{
	return CuratedLaunchContract::GameLanguageFromEnvironment(CuratedLaunchContract::ReadProcessEnvironment());
}

FString UCuratedSubsystem::GetCurrentGameLanguageSteamApiName()
{
	return CuratedLaunchContract::SteamApiLanguageName(GetCurrentGameLanguage());
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

bool UCuratedSubsystem::RequireReady(ECuratedLaunchError& OutError) const
{
	if (!bReady)
	{
		OutError = ECuratedLaunchError::NotLaunchedByCurated;
		return false;
	}
	OutError = ECuratedLaunchError::None;
	return true;
}

void UCuratedSubsystem::UnlockAchievement(const FString& ApiName, FCuratedAchievementUnlocked OnUnlocked)
{
	const FString Trimmed = ApiName.TrimStartAndEnd();
	ECuratedLaunchError ReadyError;
	if (!RequireReady(ReadyError) || Trimmed.IsEmpty())
	{
		OnUnlocked.ExecuteIfBound(Trimmed.IsEmpty() && bReady ? ECuratedLaunchError::HubError : ReadyError, false);
		return;
	}
	const FString Path = FString::Printf(TEXT("/games/app/%lld/achievements/%s/unlock"), Context.AppId, *FGenericPlatformHttp::UrlEncode(Trimmed));
	const int64 AppId = Context.AppId;
	SendRequest(Context, TEXT("POST"), Path, [OnUnlocked, AppId](ECuratedLaunchError Error, const FString& Body)
	{
		bool bNewlyUnlocked = false;
		TSharedPtr<FJsonObject> Result;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Body);
		if (Error == ECuratedLaunchError::None && FJsonSerializer::Deserialize(Reader, Result) && Result.IsValid())
		{
			Result->TryGetBoolField(TEXT("newly_unlocked"), bNewlyUnlocked);
			if (bNewlyUnlocked)
			{
				NotifyOverlayAchievement(Result, AppId);
			}
		}
		OnUnlocked.ExecuteIfBound(Error, bNewlyUnlocked);
	});
}

void UCuratedSubsystem::RefreshStats(FCuratedStatsRefreshed OnRefreshed)
{
	ECuratedLaunchError ReadyError;
	if (!RequireReady(ReadyError))
	{
		OnRefreshed.ExecuteIfBound(ReadyError, FString());
		return;
	}
	const FString Path = FString::Printf(TEXT("/games/app/%lld/stats/progress"), Context.AppId);
	SendRequest(Context, TEXT("GET"), Path, [OnRefreshed](ECuratedLaunchError Error, const FString& Body)
	{
		OnRefreshed.ExecuteIfBound(Error, Body);
	});
}

void UCuratedSubsystem::SetStat(const FString& ApiName, float Value, FCuratedStatWritten OnWritten)
{
	const FString Trimmed = ApiName.TrimStartAndEnd();
	ECuratedLaunchError ReadyError;
	if (!RequireReady(ReadyError) || Trimmed.IsEmpty())
	{
		OnWritten.ExecuteIfBound(Trimmed.IsEmpty() && bReady ? ECuratedLaunchError::HubError : ReadyError, FString());
		return;
	}
	const FString Path = FString::Printf(TEXT("/games/app/%lld/stats/%s"), Context.AppId, *FGenericPlatformHttp::UrlEncode(Trimmed));
	const FString Body = FString::Printf(TEXT("{\"value\":%g}"), Value);
	SendRequest(Context, TEXT("PUT"), Path, [OnWritten](ECuratedLaunchError Error, const FString& ResponseBody)
	{
		OnWritten.ExecuteIfBound(Error, ResponseBody);
	}, Body);
}

namespace
{
	FString EscapeOverlayJson(const FString& Value)
	{
		FString Out;
		Out.Reserve(Value.Len() + 2);
		for (const TCHAR Ch : Value)
		{
			switch (Ch)
			{
			case TEXT('\\'): Out += TEXT("\\\\"); break;
			case TEXT('"'): Out += TEXT("\\\""); break;
			case TEXT('\n'): Out += TEXT("\\n"); break;
			case TEXT('\r'): Out += TEXT("\\r"); break;
			case TEXT('\t'): Out += TEXT("\\t"); break;
			default:
				if (Ch < 0x20)
				{
					Out += FString::Printf(TEXT("\\u%04x"), static_cast<int32>(Ch));
				}
				else
				{
					Out.AppendChar(Ch);
				}
				break;
			}
		}
		return Out;
	}

	bool ParseOverlayEndpoint(FString Endpoint, FString& Host, int32& Port)
	{
		Endpoint = Endpoint.TrimStartAndEnd();
		if (Endpoint.StartsWith(TEXT("http://")))
		{
			Endpoint.RightChopInline(7);
		}
		int32 Colon = INDEX_NONE;
		if (!Endpoint.FindLastChar(TEXT(':'), Colon) || Colon <= 0)
		{
			return false;
		}
		Host = Endpoint.Left(Colon).TrimStartAndEnd();
		Port = FCString::Atoi(*Endpoint.Mid(Colon + 1).TrimStartAndEnd());
		if (Host.IsEmpty())
		{
			Host = TEXT("127.0.0.1");
		}
		return Port > 0 && Port <= 65535;
	}

	int64 ReadOverlayId(const TSharedPtr<FJsonObject>& Object, const TCHAR* Field, int64 Fallback)
	{
		if (!Object.IsValid() || !Object->HasField(Field))
		{
			return Fallback;
		}
		FString Text;
		if (Object->TryGetStringField(Field, Text))
		{
			const int64 Parsed = FCString::Atoi64(*Text);
			return Parsed > 0 ? Parsed : Fallback;
		}
		double Number = 0;
		if (Object->TryGetNumberField(Field, Number) && Number > 0)
		{
			return static_cast<int64>(Number);
		}
		return Fallback;
	}

	FCuratedMicroTxnAuthorization OverlayError(int64 OrderId, int64 TransId, const FString& Description)
	{
		FCuratedMicroTxnAuthorization Result;
		Result.Status = ECuratedMicroTxnStatus::Error;
		Result.OrderId = OrderId;
		Result.TransId = TransId;
		Result.TransportError = ECuratedLaunchError::HubError;
		Result.ErrorDescription = Description;
		return Result;
	}

	FCuratedMicroTxnAuthorization ParseOverlayPurchaseResult(const FString& Line, int64 OrderId, int64 TransId)
	{
		TSharedPtr<FJsonObject> Root;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Line);
		if (!FJsonSerializer::Deserialize(Reader, Root) || !Root.IsValid())
		{
			return OverlayError(OrderId, TransId, TEXT("Curated overlay returned an unreadable purchase result."));
		}
		FString Type;
		Root->TryGetStringField(TEXT("type"), Type);
		if (Type != TEXT("microtxn_purchase_result"))
		{
			return OverlayError(OrderId, TransId, TEXT("Curated overlay returned an unreadable purchase result."));
		}
		const int64 EchoedOrder = ReadOverlayId(Root, TEXT("order_id"), OrderId);
		const int64 EchoedTrans = ReadOverlayId(Root, TEXT("trans_id"), TransId);
		FString Status;
		if (!Root->TryGetStringField(TEXT("status"), Status))
		{
			return OverlayError(EchoedOrder, EchoedTrans, TEXT("Curated overlay returned an unreadable purchase result."));
		}
		FCuratedMicroTxnAuthorization Result;
		Result.OrderId = EchoedOrder;
		Result.TransId = EchoedTrans;
		Result.TransportError = ECuratedLaunchError::None;
		if (Status.Equals(TEXT("Authorized"), ESearchCase::IgnoreCase))
		{
			Result.Status = ECuratedMicroTxnStatus::Authorized;
			return Result;
		}
		if (Status.Equals(TEXT("Denied"), ESearchCase::IgnoreCase))
		{
			Result.Status = ECuratedMicroTxnStatus::Denied;
			return Result;
		}
		Result.Status = ECuratedMicroTxnStatus::Error;
		Result.ErrorDescription = TEXT("Curated could not authorize the order.");
		if (!Status.Equals(TEXT("Error"), ESearchCase::IgnoreCase))
		{
			Result.ErrorDescription = TEXT("Curated overlay returned an unreadable purchase result.");
			Result.TransportError = ECuratedLaunchError::HubError;
			return Result;
		}
		double Code = 0;
		if (Root->TryGetNumberField(TEXT("error_code"), Code))
		{
			Result.ErrorCode = static_cast<int32>(Code);
		}
		FString Description;
		if (Root->TryGetStringField(TEXT("error_description"), Description) && !Description.IsEmpty())
		{
			Result.ErrorDescription = Description;
		}
		return Result;
	}

	bool ReadOverlayLine(FSocket* Socket, FString& OutLine)
	{
		TArray<uint8> Pending;
		const double Deadline = FPlatformTime::Seconds() + 600.0;
		while (FPlatformTime::Seconds() < Deadline)
		{
			if (!Socket->Wait(ESocketWaitConditions::WaitForRead, FTimespan::FromMilliseconds(200)))
			{
				continue;
			}
			uint8 Buffer[1024];
			int32 Read = 0;
			if (!Socket->Recv(Buffer, sizeof(Buffer), Read))
			{
				return false;
			}
			if (Read <= 0)
			{
				return false;
			}
			Pending.Append(Buffer, Read);
			for (int32 Index = 0; Index < Pending.Num(); ++Index)
			{
				if (Pending[Index] != '\n')
				{
					continue;
				}
				int32 Length = Index;
				if (Length > 0 && Pending[Length - 1] == '\r')
				{
					Length--;
				}
				const FUTF8ToTCHAR Convert(reinterpret_cast<const ANSICHAR*>(Pending.GetData()), Length);
				OutLine = FString(Convert.Length(), Convert.Get());
				return true;
			}
			if (Pending.Num() > 65536)
			{
				return false;
			}
		}
		return false;
	}

	FCuratedMicroTxnAuthorization ExchangeOverlayPurchase(int64 AppId, int64 OrderId, int64 TransId)
	{
		const FString Endpoint = FPlatformMisc::GetEnvironmentVariable(TEXT("CURATED_OVERLAY_ENDPOINT")).TrimStartAndEnd();
		const FString Token = FPlatformMisc::GetEnvironmentVariable(TEXT("CURATED_OVERLAY_TOKEN")).TrimStartAndEnd();
		FString Host;
		int32 Port = 0;
		if (Endpoint.IsEmpty() || Token.IsEmpty() || !ParseOverlayEndpoint(Endpoint, Host, Port))
		{
			return OverlayError(OrderId, TransId, TEXT("Purchases require the Curated launcher overlay."));
		}
		ISocketSubsystem* Sockets = ISocketSubsystem::Get(PLATFORM_SOCKETSUBSYSTEM);
		if (!Sockets)
		{
			return OverlayError(OrderId, TransId, TEXT("Curated overlay did not answer."));
		}
		TSharedRef<FInternetAddr> Addr = Sockets->CreateInternetAddr();
		bool bValid = false;
		Addr->SetIp(*Host, bValid);
		Addr->SetPort(Port);
		if (!bValid)
		{
			return OverlayError(OrderId, TransId, TEXT("Curated overlay did not answer."));
		}
		FSocket* Socket = Sockets->CreateSocket(NAME_Stream, TEXT("CuratedOverlayPurchase"), false);
		if (!Socket)
		{
			return OverlayError(OrderId, TransId, TEXT("Curated overlay did not answer."));
		}
		Socket->SetNonBlocking(false);
		if (!Socket->Connect(*Addr))
		{
			Sockets->DestroySocket(Socket);
			return OverlayError(OrderId, TransId, TEXT("Curated overlay did not answer."));
		}
		const FString Payload = FString::Printf(
			TEXT("{\"v\":1,\"token\":\"%s\",\"type\":\"microtxn_purchase\",\"app_id\":%lld,\"order_id\":%lld,\"trans_id\":%lld}\n"),
			*EscapeOverlayJson(Token),
			AppId,
			OrderId,
			TransId);
		const FTCHARToUTF8 Utf8(*Payload);
		int32 Offset = 0;
		while (Offset < Utf8.Length())
		{
			int32 Sent = 0;
			if (!Socket->Send(reinterpret_cast<const uint8*>(Utf8.Get()) + Offset, Utf8.Length() - Offset, Sent) || Sent <= 0)
			{
				Sockets->DestroySocket(Socket);
				return OverlayError(OrderId, TransId, TEXT("Curated overlay did not answer."));
			}
			Offset += Sent;
		}
		FString Line;
		const bool bRead = ReadOverlayLine(Socket, Line);
		Sockets->DestroySocket(Socket);
		if (!bRead)
		{
			return OverlayError(OrderId, TransId, TEXT("Curated overlay did not answer."));
		}
		return ParseOverlayPurchaseResult(Line, OrderId, TransId);
	}
}

void UCuratedSubsystem::AuthorizeTxn(int64 OrderId, int64 TransId, FCuratedMicroTxnAuthorized OnDone)
{
	ECuratedLaunchError ReadyError;
	if (!RequireReady(ReadyError) || OrderId <= 0 || TransId <= 0)
	{
		FCuratedMicroTxnAuthorization Result;
		Result.Status = ECuratedMicroTxnStatus::Error;
		Result.OrderId = OrderId;
		Result.TransId = TransId;
		Result.TransportError = !bReady ? ReadyError : ECuratedLaunchError::HubError;
		OnDone.ExecuteIfBound(Result);
		return;
	}
	const int64 AppId = Context.AppId;
	Async(EAsyncExecution::ThreadPool, [OnDone, OrderId, TransId, AppId]()
	{
		const FCuratedMicroTxnAuthorization Result = ExchangeOverlayPurchase(AppId, OrderId, TransId);
		AsyncTask(ENamedThreads::GameThread, [OnDone, Result]()
		{
			OnDone.ExecuteIfBound(Result);
		});
	});
}

void UCuratedSubsystem::IncrementStat(const FString& ApiName, float Delta, FCuratedStatWritten OnWritten)
{
	const FString Trimmed = ApiName.TrimStartAndEnd();
	ECuratedLaunchError ReadyError;
	if (!RequireReady(ReadyError) || Trimmed.IsEmpty())
	{
		OnWritten.ExecuteIfBound(Trimmed.IsEmpty() && bReady ? ECuratedLaunchError::HubError : ReadyError, FString());
		return;
	}
	const FString Path = FString::Printf(TEXT("/games/app/%lld/stats/%s/increment"), Context.AppId, *FGenericPlatformHttp::UrlEncode(Trimmed));
	const FString Body = FString::Printf(TEXT("{\"delta\":%g}"), Delta);
	SendRequest(Context, TEXT("POST"), Path, [OnWritten](ECuratedLaunchError Error, const FString& ResponseBody)
	{
		OnWritten.ExecuteIfBound(Error, ResponseBody);
	}, Body);
}

void UCuratedSubsystem::GetOpenIdIdToken(FCuratedOpenIdIdToken OnDone)
{
	ECuratedLaunchError ReadyError;
	if (!RequireReady(ReadyError))
	{
		OnDone.ExecuteIfBound(ReadyError, FString());
		return;
	}
	const FString Body = FString::Printf(TEXT("{\"app_id\":%lld}"), Context.AppId);
	SendRequest(Context, TEXT("POST"), TEXT("/auth/openid/id-token"), [OnDone](ECuratedLaunchError Error, const FString& ResponseBody)
	{
		FString IdToken;
		TSharedPtr<FJsonObject> Result;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(ResponseBody);
		if (Error == ECuratedLaunchError::None && FJsonSerializer::Deserialize(Reader, Result) && Result.IsValid())
		{
			Result->TryGetStringField(TEXT("id_token"), IdToken);
			IdToken = IdToken.TrimStartAndEnd();
		}
		if (Error == ECuratedLaunchError::None && IdToken.IsEmpty())
		{
			Error = ECuratedLaunchError::HubError;
		}
		OnDone.ExecuteIfBound(Error, IdToken);
	}, Body);
}

void UCuratedSubsystem::SendRequest(
	const FCuratedLaunchContext& RequestContext,
	const FString& Verb,
	const FString& Path,
	FResponseHandler OnDone,
	const FString& JsonBody)
{
	const TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->SetURL(RequestContext.ApiBase + Path);
	Request->SetVerb(Verb);
	Request->SetHeader(TEXT("Authorization"), FString::Printf(TEXT("Bearer %s"), *RequestContext.SessionToken));
	Request->SetHeader(TEXT("Accept"), TEXT("application/json"));
	if (Verb == TEXT("POST") || Verb == TEXT("PUT"))
	{
		Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));
		Request->SetContentAsString(JsonBody.IsEmpty() ? TEXT("{}") : JsonBody);
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
