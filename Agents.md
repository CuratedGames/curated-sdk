# Agents.md: Curated SDK and Content Builder

Guide for coding agents and developers integrating a game with Curated.

| Piece | Path / download |
|-------|-----------------|
| Launch contract v1 (all engines) | [`contracts/`](contracts): spec, OpenAPI subset, conformance fixture |
| Unity package | [`engines/unity/games.curated.sdk`](engines/unity/games.curated.sdk): Releases ship `games.curated.sdk-<version>.tgz` |
| Godot 4 addon | [`engines/godot`](engines/godot): Releases ship `curated-godot-<version>.zip` |
| Unreal Engine 5 plugin | [`engines/unreal`](engines/unreal): Releases ship `curated-unreal-<version>.zip` |
| RPG Maker MZ / MV plugin | [`engines/rpgmaker`](engines/rpgmaker): Releases ship `CuratedSDK.js` |
| C# SDK (.NET, Unity, Godot C#) | [`Curated.Sdk/`](Curated.Sdk): Releases ship DLL, Unity zip, and NuGet |
| Content Builder CLI | [`tooling/content-builder/`](tooling/content-builder): Windows-first depot upload tool |
| License | [LICENSE.md](LICENSE.md) (Curated SDK License) |

---

## 1. Launch contract (read first)

The Curated launcher starts games with environment variables defined in
[contracts/launch-contract.md](contracts/launch-contract.md):

| Variable | Example | Notes |
|----------|---------|-------|
| `CURATED_LAUNCHER` | `1` | Present only when started by Curated |
| `CURATED_CONTRACT_VERSION` | `1` | Reject versions newer than the SDK supports |
| `CURATED_APP_ID` | `1002` | Must equal the game's own app id |
| `CURATED_SESSION_TOKEN` | (secret) | Bearer token scoped to this game (12 h max). Never log, persist, or forward it |
| `CURATED_API_BASE` | `https://api.curated.games` | Only destination for the token |
| `CURATED_USER_ID`, `CURATED_USERNAME` | | Display hints; trust `/auth/me` instead |
| `CURATED_LANGUAGE` | `en`, `es`, `it` | Launcher client language; read it with `GetCurrentGameLanguage` |

Startup: `GET /auth/me` (session valid?) then `GET /library` (owns `app_id`?). Each binding
returns a session or one error code: `not_launched_by_curated`, `unsupported_contract_version`,
`app_mismatch`, `session_invalid`, `not_entitled`, `network_error`, `hub_error`.

Game language (Steamworks `GetCurrentGameLanguage`): call the binding's `GetCurrentGameLanguage`
(C# `CuratedClient.GetCurrentGameLanguage()`, Godot `Curated.get_current_game_language()`, Unreal
`UCuratedSubsystem::GetCurrentGameLanguage()`, RPG Maker `Curated.getCurrentGameLanguage()`). It
returns a lowercase Curated code (`en`, `es`, `it`, `pt-br`, ...) that matches depot languages, and
`en` outside the launcher. Where a Steamworks port expects Steam API names (`english`, `italian`),
use the `...SteamApiName` variant instead of keeping a private mapping table.

Player launch options from the launcher (Properties > General > Launch options) arrive as
ordinary command-line arguments. Credentials are never on the command line.

### Rules for agents

- Use the official binding for the game's engine. Only implement the contract by hand for
  engines without one, and run every case in `contracts/fixtures/conformance.v1.json`.
- Never print, log, persist, or send `CURATED_SESSION_TOKEN` anywhere except `CURATED_API_BASE`.
- Keep Curated out of dedicated server builds (see each engine below).
- If the game also ships on other storefronts, keep their code behind existing compile flags
  and add Curated in parallel.

---

## 2. Unity

1. Download `games.curated.sdk-<version>.tgz` from Releases.
2. **Window > Package Manager > + > Add package from tarball...**, or add
   `"games.curated.sdk": "file:<path>.tgz"` to `Packages/manifest.json`.
3. Add **Curated Launcher Bootstrap** to the first scene (set app id), or call:

```csharp
#if !UNITY_SERVER && !UNITY_WEBGL
using Curated.Sdk;
using Curated.Unity;

try
{
    CuratedSession session = await CuratedUnity.InitFromLauncherAsync(1002);
}
catch (CuratedLaunchException e)
{
    Debug.LogWarning($"Curated: {e.Code}");
    Application.Quit();
}
#endif
```

Dedicated server and WebGL: `Curated.Sdk.dll` and the `Curated.Unity` assembly carry the define
constraint `!UNITY_SERVER` and exclude WebGL. Reference `Curated.Unity` only from assemblies with
the same constraints, or wrap calls in `#if !UNITY_SERVER && !UNITY_WEBGL`.

Older projects can still drop `Curated.Sdk.dll` from `Curated.Sdk.unity.zip` into
`Assets/Plugins/Curated.Sdk/`; set its define constraint to `!UNITY_SERVER` in the importer.

## 3. Godot 4

1. Unzip `curated-godot-<version>.zip` into the project root (`res://addons/curated/`).
2. Enable **Curated SDK** in **Project Settings > Plugins** (adds the `Curated` autoload).

```gdscript
var result: Dictionary = await Curated.init_from_launcher(1002)
if not result.ok:
	get_tree().quit()
```

`Curated.unlock_achievement("BOSS_1")` and `Curated.check_entitlement()` are also available.
The addon is inert in exports with the `dedicated_server` feature. Godot C# projects may
reference `Curated.Sdk.dll` for Cloud Save, friends, and presence.

## 4. Unreal Engine 5

1. Unzip `curated-unreal-<version>.zip` into `<Project>/Plugins/`.
2. In the game module's `Build.cs`, depend on `CuratedSDK` only when
   `Target.Type != TargetType.Server` and define `WITH_CURATED` accordingly (the plugin module
   denies `Server` targets).
3. `GetGameInstance()->GetSubsystem<UCuratedSubsystem>()`: bind `OnLaunchCompleted`, call
   `InitFromLauncher(1002)`. Blueprint nodes have the same names.

## 5. RPG Maker MZ / MV

Copy `CuratedSDK.js` into `js/plugins/`, add it in the Plugin Manager, and set **Curated app id**.
The title screen waits for the ownership check. Unlock achievements with the
**Unlock achievement** plugin command or `Curated.unlockAchievement("BOSS_1")`.

## 6. C# features (Achievements, Cloud Save, friends, presence)

After `InitFromLauncherAsync` (or `CuratedClient.Init(appId, token)` when you already have a
session):

```csharp
await Achievements.RefreshAsync();
await new Achievement("win_level_1").TriggerAsync();

await Cloud.SyncOnLaunchAsync(saveFolder);
await Cloud.SyncOnQuitAsync(saveFolder, "saves/slot1.sav");

foreach (var friend in await Friends.GetFriendsAsync()) { /* friend.Name, friend.IsOnline */ }
await Presence.SetAsync("in_game", "Level 3");
await Presence.HeartbeatAsync();

string language = CuratedClient.GetCurrentGameLanguage(); // "en", "es", "it", ...
string steamName = CuratedClient.GetCurrentGameLanguageSteamApiName(); // "english", ...

CuratedClient.Shutdown(); // on quit
```

Define achievement names in the admin Achievements tool. Configure Cloud Save path patterns in
the Cloud Save tool before players sync (last write wins).

### Smoke checklist

- [ ] In the launcher, set Library > game > Properties > General > Game executable to your build
- [ ] Start from the Curated launcher: session returned with the right username
- [ ] Start the exe directly: `not_launched_by_curated`
- [ ] Account without the game: `not_entitled`
- [ ] Launch options from Properties > General reach the game's arguments
- [ ] Switch the launcher language (account main language) and relaunch: `GetCurrentGameLanguage` follows it; started directly it returns `en`
- [ ] Unlock one test achievement
- [ ] Dedicated server build contains no Curated code
- [ ] Shutdown on quit without hanging

---

## 7. Content Builder CLI (depot upload)

Canonical folder: [`tooling/content-builder`](tooling/content-builder). Binary name: `curated-content-builder`.

### Build or download

```bat
cd tooling\content-builder
cargo build --release
```

Or take the Windows artifact from this repository's Releases.

### Login (email or username + password)

Caches a session token under `%USERPROFILE%\.curated\credentials.json` (Unix: `~/.curated/credentials.json`). Later commands auto-authenticate from that file.

```bat
curated-content-builder.exe login --login you@studio.com --password "..."
curated-content-builder.exe login --username studio_dev --password "..."
curated-content-builder.exe whoami
```

`--email` works as an alias of `--login`. Override API origin with `--api-base` / `CURATED_API_BASE`.

### List depots

Only apps owned by the signed-in developer are allowed.

```bat
curated-content-builder.exe list-depots --app-id 1000
```

### Upload

```bat
curated-content-builder.exe upload ^
  --app-id 1000 ^
  --depot-key content ^
  --content C:\game\content ^
  --version 1.2.3
```

Or use a script (see `tooling/content-builder/examples/simple_app_build.toml`):

```bat
curated-content-builder.exe upload --script scripts\simple_app_build.toml
```

Ownership is checked via the API before upload. Non-owned apps are rejected. After upload, use admin **Game Build** to mark clean and set Default/Beta live.

### Logout

```bat
curated-content-builder.exe logout
```
