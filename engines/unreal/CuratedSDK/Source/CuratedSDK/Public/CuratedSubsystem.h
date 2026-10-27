#pragma once

#include "CoreMinimal.h"
#include "CuratedLaunchContract.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "CuratedSubsystem.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FCuratedLaunchCompleted, ECuratedLaunchError, Error, const FCuratedSession&, Session);
DECLARE_DYNAMIC_DELEGATE_TwoParams(FCuratedEntitlementChecked, ECuratedLaunchError, Error, const FCuratedEntitlement&, Entitlement);
DECLARE_DYNAMIC_DELEGATE_TwoParams(FCuratedAchievementUnlocked, ECuratedLaunchError, Error, bool, bNewlyUnlocked);

/**
 * Curated launcher integration (launch contract v1).
 * Call InitFromLauncher once at startup and wait for OnLaunchCompleted.
 */
UCLASS()
class CURATEDSDK_API UCuratedSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	/** Reads launch variables, verifies the player's session, and confirms they own AppId. */
	UFUNCTION(BlueprintCallable, Category = "Curated")
	void InitFromLauncher(int64 AppId);

	/** Fires once per InitFromLauncher call. Error is None on success. */
	UPROPERTY(BlueprintAssignable, Category = "Curated")
	FCuratedLaunchCompleted OnLaunchCompleted;

	/** Re-checks ownership during a long session (for example after a refund). */
	UFUNCTION(BlueprintCallable, Category = "Curated")
	void CheckEntitlement(FCuratedEntitlementChecked OnChecked);

	/** Unlocks an achievement by API name. Safe to call more than once. */
	UFUNCTION(BlueprintCallable, Category = "Curated")
	void UnlockAchievement(const FString& ApiName, FCuratedAchievementUnlocked OnUnlocked);

	UFUNCTION(BlueprintPure, Category = "Curated")
	bool IsReady() const { return bReady; }

	UFUNCTION(BlueprintPure, Category = "Curated")
	FCuratedSession GetSession() const { return Session; }

	/**
	 * Game language the player selected in the Curated launcher, as a lowercase Curated code
	 * (en, es, it, pt-br, ...). It is the launcher's client language, the same one used to pick
	 * language depots. Returns "en" when the game was not started by the launcher, no language
	 * was sent, or it is not a Curated game language. Works before InitFromLauncher.
	 */
	UFUNCTION(BlueprintPure, Category = "Curated")
	static FString GetCurrentGameLanguage();

	/** Migrating from Steam: GetCurrentGameLanguage as a Steam client language API name (english, spanish, italian, ...). */
	UFUNCTION(BlueprintPure, Category = "Curated")
	static FString GetCurrentGameLanguageSteamApiName();

	/** Shared error code string (for example "not_entitled"). */
	UFUNCTION(BlueprintPure, Category = "Curated")
	static FString ErrorToCode(ECuratedLaunchError Error) { return CuratedLaunchContract::ErrorCode(Error); }

	virtual void Deinitialize() override;

private:
	using FResponseHandler = TFunction<void(ECuratedLaunchError Error, const FString& Body)>;

	void SendRequest(const FCuratedLaunchContext& RequestContext, const FString& Verb, const FString& Path, FResponseHandler OnDone);
	void FinishLaunch(ECuratedLaunchError Error);

	FCuratedLaunchContext Context;
	FCuratedLaunchContext PendingContext;
	FCuratedSession Session;
	bool bReady = false;
	uint32 LaunchGeneration = 0;
};
