# Curated SDK

Integrate games with [Curated](https://curated.games) on any engine. When the Curated launcher
starts a game, the game can sign the player in, confirm they own it, and use achievements,
Cloud Save, friends, and presence. The SDK also includes the Content Builder CLI for depot uploads.

| Engine | Package | Path |
|--------|---------|------|
| Unity 2021.3+ | `games.curated.sdk-<version>.tgz` (UPM) | [engines/unity](engines/unity/games.curated.sdk) |
| Godot 4 | `curated-godot-<version>.zip` (addon) | [engines/godot](engines/godot) |
| Unreal Engine 5 | `curated-unreal-<version>.zip` (plugin) | [engines/unreal](engines/unreal) |
| RPG Maker MZ / MV | `CuratedSDK.js` (plugin) | [engines/rpgmaker](engines/rpgmaker) |
| .NET (Godot C#, MonoGame, Stride, tools) | `Curated.Sdk.dll` / NuGet | [Curated.Sdk](Curated.Sdk) |
| Any other engine | Launch contract v1 | [contracts](contracts) |
| Depot uploads | Content Builder CLI | [tooling/content-builder](tooling/content-builder) |

Every package downloads from this repository's Releases.

## How it works

The Curated launcher starts the game with a few environment variables
([launch contract v1](contracts/launch-contract.md)): app id, the player's session, the API
address, and the player's language. The SDK reads them, calls `GET /auth/me` to verify the
session and `GET /library` to confirm ownership, and returns a session or a stable error code:

| Code | Meaning | Typical handling |
|------|---------|------------------|
| `not_launched_by_curated` | Started outside the launcher | Show "Start from Curated" and quit |
| `unsupported_contract_version` | Launcher is newer than the SDK | Ask the player to update the game |
| `app_mismatch` | Launched for a different app id | Quit |
| `session_invalid` | Session expired or signed out | Ask the player to sign in again in Curated |
| `not_entitled` | Player does not own the game | Quit |
| `network_error` | Curated unreachable | Retry or allow offline play |
| `hub_error` | Unexpected server response | Retry later |

Every binding is checked against the same fixtures,
[contracts/fixtures/conformance.v1.json](contracts/fixtures/conformance.v1.json) and
[contracts/fixtures/game-language.v1.json](contracts/fixtures/game-language.v1.json).

### Game language

Like Steamworks `ISteamApps::GetCurrentGameLanguage`, every binding returns the game language the
player selected in the Curated launcher. It is the launcher's client language, the same language
the launcher uses to download language depots, as a lowercase Curated code (`en`, `es`, `it`, and
any other catalog code such as `pt-br`). It returns `en` when the game runs outside the launcher.
A second call returns the Steam API name (`english`, `spanish`, `italian`, ...) to ease porting.

| Binding | Curated code | Steam API name |
|---------|--------------|----------------|
| .NET / Unity | `CuratedClient.GetCurrentGameLanguage()` | `CuratedClient.GetCurrentGameLanguageSteamApiName()` |
| Godot | `Curated.get_current_game_language()` | `Curated.get_current_game_language_steam_api_name()` |
| Unreal | `UCuratedSubsystem::GetCurrentGameLanguage()` | `UCuratedSubsystem::GetCurrentGameLanguageSteamApiName()` |
| RPG Maker | `Curated.getCurrentGameLanguage()` | `Curated.getCurrentGameLanguageSteamApiName()` |

`GameLanguages` (C#) converts between Curated codes and Steam API names
(`GameLanguages.ToSteamApiName("pt-br")` is `brazilian`, `GameLanguages.FromSteamApiName("latam")`
is `es-419`).

## Getting started

Unity:

```csharp
#if !UNITY_SERVER && !UNITY_WEBGL
try
{
    CuratedSession session = await CuratedUnity.InitFromLauncherAsync(1002);
}
catch (CuratedLaunchException e)
{
    Debug.LogWarning(e.Code);
    Application.Quit();
}
#endif
```

Godot:

```gdscript
var result: Dictionary = await Curated.init_from_launcher(1002)
```

Unreal (C++ or Blueprint): `GetSubsystem<UCuratedSubsystem>()->InitFromLauncher(1002)`, then
handle `OnLaunchCompleted`.

RPG Maker: add `CuratedSDK.js` in the Plugin Manager and set the app id.

.NET:

```csharp
CuratedSession session = await CuratedClient.InitFromLauncherAsync(1002);
// ...
CuratedClient.Shutdown();
```

Each engine folder has a README with details. To test outside the launcher, set the variables
from the [launch contract](contracts/launch-contract.md#development-without-the-launcher) yourself.

### Dedicated servers

Curated code is never built into dedicated servers: the Unity DLL and helper assembly carry
`!UNITY_SERVER`, the Unreal module denies `Server` targets, and the Godot addon does nothing
in `dedicated_server` exports.

## C# API (.NET and Unity)

```csharp
await Achievements.RefreshAsync();
await new Achievement("win_level_1").TriggerAsync();

await Cloud.SyncOnLaunchAsync(saveFolder);
await Cloud.SyncOnQuitAsync(saveFolder, "saves/slot1.sav");

foreach (var friend in await Friends.GetFriendsAsync())
{
    Console.WriteLine($"{friend.Name}: {friend.PresenceStatus}");
}
await Presence.SetAsync("in_game", "Level 3");

Entitlement current = await Entitlements.CheckAsync();

string language = CuratedClient.GetCurrentGameLanguage(); // "en", "es", "it", ...
```

Public types: `CuratedClient`, `CuratedSession`, `Entitlement`, `Entitlements`,
`LaunchContext`, `GameLanguages`, `GameLanguage`, `CuratedLaunchException`, `CuratedLaunchError`, `Achievement`, `Achievements`,
`Cloud`, `CloudSyncResult`, `Friends`, `Friend`, `Presence`, `CuratedException`. Transport and
wire models stay internal. `Curated.Sdk.dll` targets netstandard2.1 and net8.0 and has no
third-party dependencies.

`CuratedClient.Init(appId, sessionToken)` is still available for games that receive the
session another way; it does not check ownership.

## Content Builder CLI

Windows-first tool to upload depot builds for apps you own:

```bat
cd tooling\content-builder
cargo build --release
curated-content-builder.exe login --login you@studio.com --password "..."
curated-content-builder.exe list-depots --app-id 1000
curated-content-builder.exe upload --app-id 1000 --depot-key content --content C:\game\content --version 1.2.3
```

See [tooling/content-builder/README.md](tooling/content-builder/README.md) and [Agents.md](Agents.md).

## Build, test, pack

```bash
dotnet test Curated.Sdk.sln --configuration Release
node --test engines/rpgmaker/CuratedSDK.test.mjs
python3 scripts/check-game-languages.py
bash scripts/pack-unity-zip.sh artifacts/sdk-unity
bash scripts/pack-engines.sh artifacts/sdk-unity/publish/Curated.Sdk.dll artifacts/engines
cd tooling/content-builder && cargo test && cargo build --release
```

Godot and Unreal tests run inside the engine (see their READMEs).

## License

See [LICENSE.md](LICENSE.md). Ship the official packages (DLL, NuGet, and the engine bindings in
`engines/`) only as part of your game. Never log or store the player's session token, and send
it only to the Curated API address the launcher provides.

Copyright (c) Margherita Roncone, licensed to Fatum Games SL (Business ID: B21832985), Calle
Mediodia 7, 35015, Las Palmas De Gran Canaria, Las Palmas, Spain. Curated and Curated Games are
trademarks of Margherita Roncone, licensed to Fatum Games SL. Support: support@curated.games.
