# .NET (C#)

`Curated.Sdk.dll` targets netstandard2.1 and net8.0, with no third-party dependencies. Use it in
Godot C#, MonoGame, Stride, custom engines, and tools. Unity projects get it through the
[Unity package](./unity).

## Install

Download one of these from [Releases](https://github.com/CuratedGames/curated-sdk/releases/tag/%SDK_TAG%):

- `Curated.Sdk.%SDK_VERSION%.nupkg`: add a local NuGet source pointing at its folder, then
  reference package `Curated.Sdk` version `%SDK_VERSION%`.
- `Curated.Sdk.dll`: reference it directly.

## Sign in and check ownership

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#init-from-launcher{csharp}

On failure, `InitFromLauncherAsync` throws `CuratedLaunchException`:

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#launch-error{csharp}

If the game already holds a session token, `CuratedClient.InitAsync(appId, token)` runs the same
checks. Every init path verifies ownership. Game-scoped APIs refuse to run until it succeeds
(`CuratedClient.IsEntitled`).

`CuratedSession` exposes `UserId`, `CuratedId` (public 17-digit player id), `Username`,
`CurrentGameLanguage`, and `Entitlement`.

## Features

### Achievements

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#achievements{csharp}

See [Achievements and stats](./achievements-stats).

### Stats

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#stats{csharp}

### Re-check ownership

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#entitlement{csharp}

### More

- [Cloud Save, friends, presence](./cloud-friends-presence)
- [In-app purchases / Microtransactions](./purchases)
- [OpenID Connect](./openid)
- [Game language](./game-language)

## Shut down

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#shutdown{csharp}

## Reference

[.NET API reference](/reference/dotnet).
