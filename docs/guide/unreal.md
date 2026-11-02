# Unreal Engine 5

Runtime plugin with a `UGameInstanceSubsystem`, usable from Blueprints and C++. It only depends
on engine modules (`HTTP`, `Json`).

## Install

1. Download `curated-unreal-%SDK_VERSION%.zip` from
   [Releases](https://github.com/CuratedGames/curated-sdk/releases/tag/%SDK_TAG%).
2. Unzip it into `<Project>/Plugins/` so you get `Plugins/CuratedSDK/CuratedSDK.uplugin`.
3. Enable **Curated SDK** in **Edit > Plugins** and rebuild.
4. Add the dependency only for non-server targets in your game module's `Build.cs`:

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

The module has `"TargetDenyList": ["Server"]`, so it is never built into server targets.

## Sign in and check ownership

Blueprint: in your Game Instance, get the **Curated Subsystem**, bind **On Launch Completed**,
then call **Init From Launcher** with your app id. `Error` is `None` on success. Convert it with
**Error To Code** (`not_entitled`, `session_invalid`, ...).

C++:

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

::: info Not compiled in CI
Unreal is not installed in CI, so C++ snippets are checked by review. CI does check the plugin's
language table and its automation test cases against the fixture, and that every public
`UFUNCTION` is listed in the [API reference](/reference/unreal).
:::

## Features

- `CheckEntitlement` re-checks ownership.
- `UnlockAchievement` unlocks by API name.
- `RefreshStats`, `SetStat`, `IncrementStat` for player stats.
- `AuthorizeTxn` for [in-game purchases](./purchases).
- `GetOpenIdIdToken` for [OpenID Connect](./openid).
- `GetCurrentGameLanguage` (static) for the [game language](./game-language).

Cloud Save, friends, and presence are C# only. Use `Curated.Sdk` from a managed layer, or call
the same HTTP paths yourself.

## Tests

Automation tests live under **Curated.LaunchContract** in **Tools > Session Frontend >
Automation**.

## Reference

[Unreal API reference](/reference/unreal).
