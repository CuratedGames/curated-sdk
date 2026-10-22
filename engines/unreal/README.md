# Curated SDK for Unreal Engine 5

Runtime plugin implementing launch contract v1 (`contracts/launch-contract.md`) as a
`UGameInstanceSubsystem`, usable from Blueprints and C++. It only depends on engine modules
(`HTTP`, `Json`).

## Install

1. Download `curated-unreal-<version>.zip` from the curated-sdk Releases.
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

## Tests

Automation tests live under **Curated.LaunchContract** in the Session Frontend
(**Tools > Session Frontend > Automation**).
