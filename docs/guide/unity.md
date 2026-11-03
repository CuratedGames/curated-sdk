# Unity

Unity 2021.3 or newer. The package wraps `Curated.Sdk.dll` (netstandard2.1, no third-party
dependencies) and adds a small `Curated.Unity` helper assembly.

## Install

Add the package from GitHub with Package Manager. In `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.curatedgames.sdk": "https://github.com/CuratedGames/curated-sdk.git?path=/engines/unity/com.curatedgames.sdk#%SDK_TAG%"
  }
}
```

Or **Window > Package Manager > + > Add package from git URL** and paste the same URL. The `#`
fragment can be a release tag, `main`, or a commit SHA. Very old tags used a different package
folder and fail with a pathspec error; use a current tag.

Offline install: download `com.curatedgames.sdk-%SDK_VERSION%.tgz` from
[Releases](https://github.com/CuratedGames/curated-sdk/releases/tag/%SDK_TAG%) and use
**Add package from tarball**.

## Sign in and check ownership

Option 1: add **Curated Launcher Bootstrap** to a GameObject in your first scene and set
**App Id**. It runs the check on `Start`, invokes **On Ready** or **On Failed** (with the error
code), and quits on failure unless you turn off **Quit On Failure**. **Skip In Editor** is on by
default.

Option 2: call it from code.

```csharp
#if !UNITY_SERVER && !UNITY_WEBGL
using Curated.Sdk;
using Curated.Unity;

try
{
    CuratedSession session = await CuratedUnity.InitFromLauncherAsync(1002);
    Debug.Log($"Signed in as {session.Username} ({session.CuratedId})");
}
catch (CuratedLaunchException e)
{
    Debug.LogWarning($"Curated: {e.Code}");
    Application.Quit();
}
#endif
```

::: info Not compiled in CI
Unity projects are not built in CI, so the snippet above is checked by review only. Everything
after `InitFromLauncherAsync` uses the `Curated.Sdk` API, whose examples on the
[.NET page](./dotnet) are compiled and run by `dotnet test`.
:::

`CuratedUnity.InitFromLauncherAsync` shuts Curated down when the application quits.

In the Unity Editor, `InitFromLauncherAsync` sends `editor_attach` to a running Curated launcher
with the account signed in there. The launcher does not have to start Unity, and the Editor
does not need `CURATED_LAUNCHER`. On success the Game view loads the launcher overlay page.
That page is the purchase and achievement UI. A successful attach initializes the session, so
editor play does not fail with `not_launched_by_curated`. If the launcher is not signed in, the
reply is `not_signed_in` and that session is not started. Player builds still return
`not_launched_by_curated` when the launcher did not start them. Do not reimplement that
connection in the game.

**Skip In Editor** on the bootstrap still lets play continue when the launcher is not running.
It does try the connection first, so a signed-in launcher is enough for the Game view overlay.

## Features

Achievements, stats, Cloud Save, friends, presence, purchases, and OpenID use the shared C# API.
See [.NET](./dotnet#features) for tested examples, and:

- [Achievements and stats](./achievements-stats)
- [Game language](./game-language): `CuratedUnity.GetCurrentGameLanguage()`
- [In-app purchases / Microtransactions](./purchases)
- [OpenID Connect](./openid): `CuratedUnity.GetOpenIdIdTokenAsync()` for Unity Authentication

## Dedicated server and WebGL

`Curated.Sdk.dll` and `Curated.Unity` carry the define constraint `!UNITY_SERVER` and exclude
WebGL. Wrap your own calls in `#if !UNITY_SERVER && !UNITY_WEBGL`, or reference `Curated.Unity`
only from an assembly with the same constraints.

Older projects can drop `Curated.Sdk.dll` from `Curated.Sdk.unity.zip` into
`Assets/Plugins/Curated.Sdk/` and set its define constraint to `!UNITY_SERVER`.

## Reference

[Unity API reference](/reference/unity) and [.NET API reference](/reference/dotnet).
