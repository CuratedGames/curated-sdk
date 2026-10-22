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
	TestTrue(TEXT("newer version"), CuratedLaunchContract::Parse(Env, 1002, Context) == ECuratedLaunchError::UnsupportedContractVersion);

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

#endif
