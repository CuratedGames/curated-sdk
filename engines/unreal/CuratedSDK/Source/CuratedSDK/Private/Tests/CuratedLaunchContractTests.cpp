#include "CuratedLaunchContract.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace
{
	TMap<FString, FString> ValidEnvironment()
	{
		return {
			{ TEXT("CURATED_LAUNCHER"), TEXT("1") },
			{ TEXT("CURATED_CONTRACT_VERSION"), TEXT("1") },
			{ TEXT("CURATED_APP_ID"), TEXT("1002") },
			{ TEXT("CURATED_SESSION_TOKEN"), TEXT("tok_fixture") },
			{ TEXT("CURATED_API_BASE"), TEXT("https://api.example.test/") },
			{ TEXT("CURATED_LANGUAGE"), TEXT("it") },
		};
	}
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FCuratedLaunchParseTest, "Curated.LaunchContract.Parse",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FCuratedLaunchParseTest::RunTest(const FString& Parameters)
{
	FCuratedLaunchContext Context;
	TestTrue(TEXT("valid"), CuratedLaunchContract::Parse(ValidEnvironment(), 1002, Context) == ECuratedLaunchError::None);
	TestEqual(TEXT("api base normalized"), Context.ApiBase, FString(TEXT("https://api.example.test")));
	TestEqual(TEXT("language"), Context.Language, FString(TEXT("it")));

	TMap<FString, FString> Env = ValidEnvironment();
	Env.Remove(TEXT("CURATED_API_BASE"));
	CuratedLaunchContract::Parse(Env, 1002, Context);
	TestEqual(TEXT("default api base"), Context.ApiBase, CuratedLaunchContract::DefaultApiBase());

	Env = ValidEnvironment();
	Env.Remove(TEXT("CURATED_LAUNCHER"));
	TestTrue(TEXT("missing marker"), CuratedLaunchContract::Parse(Env, 1002, Context) == ECuratedLaunchError::NotLaunchedByCurated);

	Env = ValidEnvironment();
	Env[TEXT("CURATED_SESSION_TOKEN")] = TEXT("   ");
	TestTrue(TEXT("blank token"), CuratedLaunchContract::Parse(Env, 1002, Context) == ECuratedLaunchError::NotLaunchedByCurated);

	Env = ValidEnvironment();
	Env[TEXT("CURATED_CONTRACT_VERSION")] = TEXT("2");
	TestTrue(TEXT("newer version still accepted"), CuratedLaunchContract::Parse(Env, 1002, Context) == ECuratedLaunchError::None);
	TestEqual(TEXT("reported newer version"), Context.ContractVersion, 2);

	Env = ValidEnvironment();
	Env[TEXT("CURATED_CONTRACT_VERSION")] = TEXT("v1");
	TestTrue(TEXT("invalid version"), CuratedLaunchContract::Parse(Env, 1002, Context) == ECuratedLaunchError::UnsupportedContractVersion);

	TestTrue(TEXT("app mismatch"), CuratedLaunchContract::Parse(ValidEnvironment(), 9999, Context) == ECuratedLaunchError::AppMismatch);

	Env = ValidEnvironment();
	Env[TEXT("CURATED_API_BASE")] = TEXT("ftp://nope");
	TestTrue(TEXT("bad api base"), CuratedLaunchContract::Parse(Env, 1002, Context) == ECuratedLaunchError::NotLaunchedByCurated);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FCuratedEntitlementTest, "Curated.LaunchContract.Entitlement",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FCuratedEntitlementTest::RunTest(const FString& Parameters)
{
	FCuratedEntitlement Entitlement;
	TestTrue(TEXT("parses list"), CuratedLaunchContract::FindEntitlement(
		TEXT("[{\"app_id\":7,\"source\":\"grant\"},{\"app_id\":1002,\"source\":\"primary\",\"acquired_at\":\"2026-01-01T00:00:00Z\"}]"), 1002, Entitlement));
	TestTrue(TEXT("entitled"), Entitlement.bIsEntitled);
	TestEqual(TEXT("source"), Entitlement.Source, FString(TEXT("primary")));

	TestTrue(TEXT("empty list"), CuratedLaunchContract::FindEntitlement(TEXT("[]"), 1002, Entitlement));
	TestFalse(TEXT("not entitled"), Entitlement.bIsEntitled);
	TestFalse(TEXT("not a list"), CuratedLaunchContract::FindEntitlement(TEXT("{\"error\":\"x\"}"), 1002, Entitlement));

	TestTrue(TEXT("401"), CuratedLaunchContract::ClassifyStatus(401) == ECuratedLaunchError::SessionInvalid);
	TestTrue(TEXT("500"), CuratedLaunchContract::ClassifyStatus(500) == ECuratedLaunchError::HubError);
	TestEqual(TEXT("code"), CuratedLaunchContract::ErrorCode(ECuratedLaunchError::NotEntitled), FString(TEXT("not_entitled")));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FCuratedGameLanguageTest, "Curated.LaunchContract.GameLanguage",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FCuratedGameLanguageTest::RunTest(const FString& Parameters)
{
	// Mirrors the cases in contracts/fixtures/game-language.v1.json.
	struct FCase
	{
		const TCHAR* Raw;
		bool bSet;
		const TCHAR* Code;
		const TCHAR* Steam;
	};
	const FCase Cases[] = {
		{ TEXT(""), false, TEXT("en"), TEXT("english") },
		{ TEXT("   "), true, TEXT("en"), TEXT("english") },
		{ TEXT("en"), true, TEXT("en"), TEXT("english") },
		{ TEXT("es"), true, TEXT("es"), TEXT("spanish") },
		{ TEXT("it"), true, TEXT("it"), TEXT("italian") },
		{ TEXT(" IT "), true, TEXT("it"), TEXT("italian") },
		{ TEXT("pt-BR"), true, TEXT("pt-br"), TEXT("brazilian") },
		{ TEXT("pt_br"), true, TEXT("pt-br"), TEXT("brazilian") },
		{ TEXT("es-419"), true, TEXT("es-419"), TEXT("latam") },
		{ TEXT("it-IT"), true, TEXT("it"), TEXT("italian") },
		{ TEXT("es-ES"), true, TEXT("es"), TEXT("spanish") },
		{ TEXT("zh"), true, TEXT("zh-cn"), TEXT("schinese") },
		{ TEXT("zh-Hant"), true, TEXT("zh-tw"), TEXT("tchinese") },
		{ TEXT("nb"), true, TEXT("no"), TEXT("norwegian") },
		{ TEXT("ko"), true, TEXT("ko"), TEXT("koreana") },
		{ TEXT("italian"), true, TEXT("it"), TEXT("italian") },
		{ TEXT("brazilian"), true, TEXT("pt-br"), TEXT("brazilian") },
		{ TEXT("klingon"), true, TEXT("en"), TEXT("english") },
		{ TEXT("xx-yy"), true, TEXT("en"), TEXT("english") },
	};
	for (const FCase& Case : Cases)
	{
		TMap<FString, FString> Env;
		if (Case.bSet)
		{
			Env.Add(TEXT("CURATED_LANGUAGE"), Case.Raw);
		}
		const FString Code = CuratedLaunchContract::GameLanguageFromEnvironment(Env);
		TestEqual(FString::Printf(TEXT("code for '%s'"), Case.Raw), Code, FString(Case.Code));
		TestEqual(FString::Printf(TEXT("steam for '%s'"), Case.Raw), CuratedLaunchContract::SteamApiLanguageName(Code), FString(Case.Steam));
	}
	TestEqual(TEXT("table size"), CuratedLaunchContract::GameLanguageCount(), 31);
	TestTrue(TEXT("unknown normalizes to empty"), CuratedLaunchContract::NormalizeGameLanguage(TEXT("klingon")).IsEmpty());
	return true;
}

#endif
