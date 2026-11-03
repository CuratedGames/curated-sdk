# Curated SDK for Unity (`com.curatedgames.sdk`)

Unity package wrapping the official `Curated.Sdk.dll` (netstandard2.1, no third-party
dependencies) plus a small `Curated.Unity` helper assembly.

## Install from GitHub (recommended)

Add the package from this public repository with Unity Package Manager. In
`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.curatedgames.sdk": "https://github.com/CuratedGames/curated-sdk.git?path=/engines/unity/com.curatedgames.sdk#v1.5.1"
  }
}
```

Replace `v1.5.1` with a release tag, branch name, or commit SHA. Examples:

```text
https://github.com/CuratedGames/curated-sdk.git?path=/engines/unity/com.curatedgames.sdk#v1.5.1
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
"com.curatedgames.sdk": "file:../Packages-local/com.curatedgames.sdk-1.5.1.tgz"
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

In the Unity Editor, `InitFromLauncherAsync` sends `editor_attach` to a running Curated launcher
with the account signed in there. The launcher does not have to start Unity. On success the
A panel over the Game view loads the launcher overlay page, which is the purchase and achievement
UI, and init does not return `not_launched_by_curated`. The launcher still authorizes the purchase.
If the launcher is not signed in, the reply is `not_signed_in`. Player builds do not send
`editor_attach` and return `not_launched_by_curated` when the launcher did not start them. The
session token is sent only to the Curated API.

`CuratedUnity.InitFromLauncherAsync` shuts Curated down on `Application.quitting`. Achievements,
stats, Cloud Save, friends, presence, and in-game purchase authorization are on the
`Curated.Sdk` API (`Achievements`, `Stats`, `Cloud`, `Friends`, `Presence`, `MicroTxn`).
`MicroTxn.AuthorizeAsync` asks the Curated launcher overlay to confirm an order the game
server created. The `authorized` argument is ignored. Outside the launcher the result is
error, and the client does not POST. Example:

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

The bootstrap component keeps playing in the Editor when the launcher is not running. It still
connects when a signed-in launcher is open, and a panel over the Game view loads the launcher overlay page.
To test a player build, set the launch variables from `contracts/launch-contract.md`.

## Migrating from Steam

```csharp
string steamName = CuratedUnity.GetCurrentGameLanguageSteamApiName(); // "english", "italian", ...
```

Use this when existing code expects Steam client language names. Prefer
`GetCurrentGameLanguage()` for new Curated-first code.
