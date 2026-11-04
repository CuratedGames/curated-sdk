# Migrating from Steam

Keep Steam (and other store) SDKs behind your existing compile flags and add Curated in parallel.

## Language names

Curated codes are short tags (`en`, `es`, `pt-br`). Steam's client API uses names (`english`,
`spanish`, `brazilian`). If your code expects Steam names, use the Steam-name helper instead of a
private mapping table:

| Binding | Call |
| --- | --- |
| .NET / Unity | `CuratedClient.GetCurrentGameLanguageSteamApiName()` |
| Godot | `Curated.get_current_game_language_steam_api_name()` |
| Unreal | `UCuratedSubsystem::GetCurrentGameLanguageSteamApiName()` |
| RPG Maker | `Curated.getCurrentGameLanguageSteamApiName()` |

In C# you can also convert in both directions:

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#steam-names{csharp}

The mapping for every language is on [Game languages](/reference/languages).

## Store overlay

`Friends.OpenStoreOverlay` is Curated's `OpenStoreOverlay`. It opens a published Curated store
page in the launcher overlay. Call it after Curated is initialized.

| Flag | Effect |
| --- | --- |
| `OverlayToStoreFlag.None` | Open the store page. No cart action. The page can be published before the game is released. |
| `OverlayToStoreFlag.AddToCart` | Open the store page and add the app to the cart. Released or preorderable apps only. |
| `OverlayToStoreFlag.AddToCartAndShow` | Add the app to the cart and show the cart. Released or preorderable apps only. |

On failure the call throws `CuratedException`. `Message` is the launcher's English text. When
`AddToCart` or `AddToCartAndShow` is used for a page that is published but not released or
preorderable, that message says that add to cart is only available for released or preorderable
apps, that this store page is published but not released, and to use `OverlayToStoreFlag.None`.

See [Store overlay](./dotnet#store-overlay).

## Stats

`Stats.SetStat` + `Stats.StoreStatsAsync` mirror Steam's SetStat / StoreStats. See
[Achievements and stats](./achievements-stats).

## Import from Steam in the admin

If your game is on Steam, the admin can import store data, achievements, stats, install settings,
and reviews. See [Basic Info](/admin/store-presence/basic-info#import-from-steam).
