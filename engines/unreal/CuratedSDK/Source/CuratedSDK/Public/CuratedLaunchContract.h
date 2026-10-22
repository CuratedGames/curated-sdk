#pragma once

#include "CoreMinimal.h"
#include "CuratedLaunchContract.generated.h"

/** Launch contract error. Use UCuratedSubsystem::ErrorToCode for the shared string code. */
UENUM(BlueprintType)
enum class ECuratedLaunchError : uint8
{
	None,
	NotLaunchedByCurated,
	UnsupportedContractVersion,
	AppMismatch,
	SessionInvalid,
	NotEntitled,
	NetworkError,
	HubError
};

USTRUCT(BlueprintType)
struct CURATEDSDK_API FCuratedEntitlement
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	int64 AppId = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	bool bIsEntitled = false;

	/** primary, resale, grant, or bonus. */
	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	FString Source;

	/** RFC 3339 timestamp. */
	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	FString AcquiredAt;
};

USTRUCT(BlueprintType)
struct CURATEDSDK_API FCuratedSession
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	int64 AppId = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	FString UserId;

	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	FString Username;

	/** Player's Curated main language (en, es, it), or empty. */
	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	FString Language;

	UPROPERTY(BlueprintReadOnly, Category = "Curated")
	FCuratedEntitlement Entitlement;
};

/** Validated launch variables. The session token never leaves this struct and the HTTP layer. */
struct CURATEDSDK_API FCuratedLaunchContext
{
	int32 ContractVersion = 0;
	int64 AppId = 0;
	FString SessionToken;
	FString ApiBase;
	FString UserIdHint;
	FString UsernameHint;
	FString Language;
};

namespace CuratedLaunchContract
{
	constexpr int32 SupportedContractVersion = 1;

	CURATEDSDK_API FString DefaultApiBase();

	/** Shared error code string, identical across every Curated engine binding. */
	CURATEDSDK_API FString ErrorCode(ECuratedLaunchError Error);

	/** Launch variables of the current process. */
	CURATEDSDK_API TMap<FString, FString> ReadProcessEnvironment();

	/** Validate launch variables for a game whose app id is ExpectedAppId. */
	CURATEDSDK_API ECuratedLaunchError Parse(const TMap<FString, FString>& Environment, int64 ExpectedAppId, FCuratedLaunchContext& OutContext);

	/** None for 2xx, SessionInvalid for 401, HubError otherwise. */
	CURATEDSDK_API ECuratedLaunchError ClassifyStatus(int32 StatusCode);

	/** Reads a /library response. Returns false when the body is not a JSON array. */
	CURATEDSDK_API bool FindEntitlement(const FString& LibraryJson, int64 AppId, FCuratedEntitlement& OutEntitlement);
}
