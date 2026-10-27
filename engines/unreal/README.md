# Curated SDK for Unreal Engine 5

Runtime plugin implementing launch contract v1 (`contracts/launch-contract.md`) as a
`UGameInstanceSubsystem`, usable from Blueprints and C++. It only depends on engine modules
(`HTTP`, `Json`).

## Install

1. Download `curated-unreal-<version>.zip` from this repository's
   [Releases](https://github.com/CuratedGames/curated-sdk/releases).
2. Unzip it into `<Project>/Plugins/` so you get `Plugins/CuratedSDK/CuratedSDK.uplugin`.
3. Enable **Curated SDK** in **Edit > Plugins** and rebuild.

## Use (Blueprint)

In your Game Instance (or first level), get the **Curated Subsystem**, bind
**On Launch Completed**, then call **Init From Launcher** with your app id. `Error` is `None`
on success; otherwise convert it with **Error To Code** (`not_entitled`, `session_invalid`, ...).

## Use (C++)

```cpp
#if WITH_CURATED
#include "CuratedSubsystem.h"

void UMyGameInstance::Init()
{
	Super::Init();
	UCuratedSubsystem* Curated = GetSubsystem<UCuratedSubsystem>();
	Curated->OnLaunchCompleted.AddDynamic(this, &UMyGameInstance::HandleCuratedLaunch);
	Curated->InitFromLauncher(1002);
}
#endif
```

`CheckEntitlement` re-checks ownership; `UnlockAchievement` unlocks by API name.

## Game language

`UCuratedSubsystem::GetCurrentGameLanguage()` (Blueprint: **Get Current Game Language**) returns
the language the player selected in the Curated launcher as a Curated code (`en`, `es`, `it`,
`pt-br`, ...), the language the launcher downloads depots for, or `en` outside the launcher. It
is static and works before `InitFromLauncher`. After a successful launch,
`FCuratedSession::CurrentGameLanguage` holds the same value.

```cpp
FInternationalization::Get().SetCurrentCulture(UCuratedSubsystem::GetCurrentGameLanguage());
```

If you are porting from Steam, see [Migrating from Steam](#migrating-from-steam).

## Dedicated servers

The module has `"TargetDenyList": ["Server"]`, so it is never built into server targets. Add
the dependency only for non-server targets in your game module's `Build.cs`:

```csharp
if (Target.Type != TargetType.Server)
{
	PrivateDependencyModuleNames.Add("CuratedSDK");
	PublicDefinitions.Add("WITH_CURATED=1");
}
else
{
	PublicDefinitions.Add("WITH_CURATED=0");
}
```

## Migrating from Steam

**Get Current Game Language Steam Api Name** /
`UCuratedSubsystem::GetCurrentGameLanguageSteamApiName()` returns Steam client language names
(`english`, `italian`, ...) for code that already expects those strings.

## Tests

Automation tests live under **Curated.LaunchContract** in the Session Frontend
(**Tools > Session Frontend > Automation**).
