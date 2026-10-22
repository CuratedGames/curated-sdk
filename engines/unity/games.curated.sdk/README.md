# Curated SDK for Unity (`games.curated.sdk`)

Unity package wrapping the official `Curated.Sdk.dll` (netstandard2.1, no third-party
dependencies) plus a small `Curated.Unity` helper assembly.

## Install

1. Download `games.curated.sdk-<version>.tgz` from the curated-sdk Releases.
2. Unity: **Window > Package Manager > + > Add package from tarball...** and pick the file.
   Or reference it from `Packages/manifest.json`:
   `"games.curated.sdk": "file:../Packages-local/games.curated.sdk-1.1.0.tgz"`.

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
    Debug.Log($"Signed in as {session.Username}, owned via {session.Entitlement.Source}");
}
catch (CuratedLaunchException e)
{
    // e.Code: not_launched_by_curated, not_entitled, session_invalid, ...
    Application.Quit();
}
#endif
```

`CuratedUnity.InitFromLauncherAsync` shuts Curated down on `Application.quitting`. Achievements,
Cloud Save, friends, and presence are on the `Curated.Sdk` API (`Achievements`, `Cloud`, `Friends`,
`Presence`).

## Dedicated server and WebGL builds

`Curated.Sdk.dll` and `Curated.Unity` carry the define constraint `!UNITY_SERVER` and exclude
WebGL, so they are left out of dedicated server and WebGL builds automatically. Wrap your own
Curated calls in `#if !UNITY_SERVER && !UNITY_WEBGL` (or reference `Curated.Unity` only from an
assembly with the same constraints) so server builds still compile.

## Editor and development builds

The bootstrap component skips the check in the Editor by default. To test the real flow, set the
launch variables from `contracts/launch-contract.md` in the environment of a development build.
