# Curated SDK for Unity (`com.curatedgames.sdk`)

Unity package wrapping the official `Curated.Sdk.dll` (netstandard2.1, no third-party
dependencies) plus a small `Curated.Unity` helper assembly.

## Install from GitHub (recommended)

Add the package from this public repository with Unity Package Manager. In
`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.curatedgames.sdk": "https://github.com/CuratedGames/curated-sdk.git?path=/engines/unity/com.curatedgames.sdk#v1.5.0"
  }
}
```

Replace `v1.5.0` with a release tag, branch name, or commit SHA. Examples:

```text
https://github.com/CuratedGames/curated-sdk.git?path=/engines/unity/com.curatedgames.sdk#v1.5.0
https://github.com/CuratedGames/curated-sdk.git?path=/engines/unity/com.curatedgames.sdk#main
```

Use a tag from **v1.3.1** onward (or `main`). Tags before that (including `v1.3.0`) still use the
old folder name `games.curated.sdk` and fail with a pathspec error.

Or in the Editor: **Window > Package Manager > + > Add package from git URL…** and paste the
same URL (including `?path=/engines/unity/com.curatedgames.sdk#…`).

Unity resolves the package from the `engines/unity/com.curatedgames.sdk` folder in this repo.
You do **not** need a tarball for normal installs.

### Optional: Releases tarball

If you prefer an offline install, download `com.curatedgames.sdk-<version>.tgz` from
[Releases](https://github.com/CuratedGames/curated-sdk/releases), then
**Window > Package Manager > + > Add package from tarball…**, or:

```json
"com.curatedgames.sdk": "file:../Packages-local/com.curatedgames.sdk-1.5.0.tgz"
```

## Use

Either add **Curated Launcher Bootstrap** to a GameObject in your first scene and set the app id,
or call it from code:

```csharp
#if !UNITY_SERVER && !UNITY_WEBGL
using Curated.Sdk;
using Curated.Unity;

try
{
    CuratedSession session = await CuratedUnity.InitFromLauncherAsync(1002);
    Debug.Log($"Signed in as {session.Username} ({session.CuratedId}), owned via {session.Entitlement.Source}");
}
catch (CuratedLaunchException e)
{
    // e.Code: not_launched_by_curated, not_entitled, session_invalid, ...
    Application.Quit();
}
#endif
```

### Game language

```csharp
string language = CuratedUnity.GetCurrentGameLanguage(); // "en", "es", "it", "pt-br", ...
```

It is the language the player selected in the Curated launcher (the language the launcher
downloads depots for), or `en` outside the launcher and in the Editor. It works before
`InitFromLauncherAsync`. With Unity Localization, pick the `Locale` whose code matches it.

If you are porting from Steam and need Steam language API names (`english`, `italian`, …), see
[Migrating from Steam](#migrating-from-steam).

In the Unity Editor, `InitFromLauncherAsync` uses the account signed in to a running Curated
launcher (`Config/session.json` beside `Curated.exe`) when this process was not started by
Curated. Player builds still return `not_launched_by_curated` in that case. The session token
is sent only to the Curated API.

`CuratedUnity.InitFromLauncherAsync` shuts Curated down on `Application.quitting`. Achievements,
stats, Cloud Save, friends, and presence are on the `Curated.Sdk` API (`Achievements`, `Stats`,
`Cloud`, `Friends`, `Presence`). Example:

```csharp
await Stats.RefreshAsync();
Stats.SetStat("CardsCreated", 10);
await Stats.StoreStatsAsync();
```

For Unity Authentication OpenID Connect, call `CuratedUnity.GetOpenIdIdTokenAsync()` after init
and pass the string to `SignInWithOpenIdConnectAsync` / `LinkWithOpenIdConnectAsync` with provider
`oidc-curated`. Never log the ID token; never send `CURATED_SESSION_TOKEN` to Unity.

## Dedicated server and WebGL builds

`Curated.Sdk.dll` and `Curated.Unity` carry the define constraint `!UNITY_SERVER` and exclude
WebGL, so they are left out of dedicated server and WebGL builds automatically. Wrap your own
Curated calls in `#if !UNITY_SERVER && !UNITY_WEBGL` (or reference `Curated.Unity` only from an
assembly with the same constraints) so server builds still compile.

## Editor and development builds

The bootstrap component skips the check in the Editor by default. To test the real flow, set the
launch variables from `contracts/launch-contract.md` in the environment of a development build.

## Migrating from Steam

```csharp
string steamName = CuratedUnity.GetCurrentGameLanguageSteamApiName(); // "english", "italian", ...
```

Use this when existing code expects Steam client language names. Prefer
`GetCurrentGameLanguage()` for new Curated-first code.
