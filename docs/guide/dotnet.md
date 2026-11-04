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
- [Store overlay](#store-overlay)
- [OpenID Connect](./openid)
- [Game language](./game-language)

### Store overlay

`Friends.OpenStoreOverlay` opens a published Curated store page in the launcher overlay. Curated
must already be initialized, and the overlay must be enabled in the player's launcher settings
and active for the running game. In the Unity Editor that is the overlay from a successful
launcher attach. The page can be for this game or for another Curated app.

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#store-overlay{csharp}

| Flag | What the overlay does |
| --- | --- |
| `OverlayToStoreFlag.None` | Opens the store page. Does not change the cart. Allowed for a published app, including one whose game is not released yet. |
| `OverlayToStoreFlag.AddToCart` | Opens the store page and adds the app to the cart. Released or preorderable apps only. |
| `OverlayToStoreFlag.AddToCartAndShow` | Adds the app to the cart and shows the cart. Released or preorderable apps only. |

The call waits for the launcher's reply. On failure it throws `CuratedException`. `Message` is
the launcher's English reason.

If `AddToCart` or `AddToCartAndShow` is used for a store page that is published but not released
and not preorderable, `Message` says that add to cart is only available for released or
preorderable apps, that this store page is published but not released, and that the caller
should use `OverlayToStoreFlag.None`.

Before init the call throws `InvalidOperationException`. Without a verified ownership check it
throws `CuratedLaunchException` (`not_entitled`). An app id that is not positive throws
`ArgumentOutOfRangeException`. If the overlay is not enabled or not active, and the launcher
cannot be reached, `CuratedException.Message` says the overlay is not enabled or is not active
for this application.

## Shut down

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#shutdown{csharp}

## Reference

[.NET API reference](/reference/dotnet).
