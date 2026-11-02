# SDK overview

The Curated SDK connects your game to the Curated launcher. When a player presses **Play**, the
launcher starts your game with a few environment variables. The SDK reads them, signs the player
in, and confirms they own the game. Then you can use achievements, stats, Cloud Save, friends,
presence, and in-game purchases.

Current release: **%SDK_VERSION%** ([changelog](/changelog)).

## Pick your engine

| Engine | Package | Guide |
| --- | --- | --- |
| Unity 2021.3+ | `com.curatedgames.sdk` (Git URL or tarball) | [Unity](./unity) |
| .NET (Godot C#, MonoGame, Stride, tools) | `Curated.Sdk.dll` or `.nupkg` | [.NET](./dotnet) |
| Godot 4 (GDScript) | `curated-godot-%SDK_VERSION%.zip` | [Godot 4](./godot) |
| Unreal Engine 5 | `curated-unreal-%SDK_VERSION%.zip` | [Unreal Engine 5](./unreal) |
| RPG Maker MZ / MV | `CuratedSDK.js` | [RPG Maker](./rpgmaker) |
| Anything else | Implement the contract yourself | [Launch contract](./launch-contract) |

Every package is on [GitHub Releases](https://github.com/CuratedGames/curated-sdk/releases/tag/%SDK_TAG%).
There is no nuget.org package and no private feed.

## What every binding does

1. Reads the launch variables (`CURATED_LAUNCHER`, `CURATED_APP_ID`, `CURATED_SESSION_TOKEN`, ...).
2. Calls `GET /auth/me` to verify the session.
3. Calls `GET /library` to confirm the player owns your app.
4. Returns a session, or one error code:

| Code | Meaning | Typical handling |
| --- | --- | --- |
| `not_launched_by_curated` | Started outside the launcher | Show "Start from Curated" and quit |
| `unsupported_contract_version` | Launcher contract below the game minimum, or invalid | Update the launcher |
| `app_mismatch` | Launched for a different app id | Quit |
| `session_invalid` | Session expired or signed out | Ask the player to sign in again in Curated |
| `not_entitled` | Player does not own the game | Quit |
| `network_error` | Curated unreachable | Retry or allow offline play |
| `hub_error` | Unexpected server response | Retry later |

All bindings are tested against the same fixtures:
[`conformance.v1.json`](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/fixtures/conformance.v1.json)
and [`game-language.v1.json`](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/fixtures/game-language.v1.json).

## Rules

- Never log, store, or forward `CURATED_SESSION_TOKEN`. Send it only to `CURATED_API_BASE`.
- Keep Curated out of dedicated server builds. Each engine guide shows how.
- If the game also ships on other stores, keep their SDKs behind your existing compile flags and
  add Curated in parallel.

## Before you submit

- [ ] In the launcher, set **Library > game > Properties > General > Game executable** to your build
- [ ] Start from the Curated launcher: a session with the right username
- [ ] Start the exe directly: `not_launched_by_curated`
- [ ] An account without the game: `not_entitled`
- [ ] Launch options from **Properties > General** reach the game's arguments
- [ ] Switch the launcher language and relaunch: `GetCurrentGameLanguage` follows it
- [ ] Unlock one test achievement and write one client-writable stat
- [ ] The dedicated server build contains no Curated code
- [ ] Shut down on quit without hanging
