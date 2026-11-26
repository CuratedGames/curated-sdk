# Cloud Save, friends, presence

Friends and presence are part of the C# API (`Curated.Sdk.dll`): .NET, Unity, and Godot C#.
Godot GDScript, Unreal, and RPG Maker do not wrap them yet. Call the same HTTP paths from the
[OpenAPI file](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/openapi/game-sdk.v1.yaml)
if you need friends or presence there. Cloud Save is synced by the Curated launcher. A game
does not call it.

## Cloud Save

Turn Cloud Save on and list the save paths in the admin first, under
[Game Build > Cloud Save](/admin/game-build/cloud-save). The Curated launcher scans
those paths and syncs them when the player starts the game and when the game exits.
That includes a crash, and a game that never calls the SDK. Writing the save files
to disk is enough.

A pattern is relative to the install folder (`saves/**/*.sav`) or starts with a known
user folder (`%AppData%/Game/*`, `%Documents%`, `%SavedGames%`). `%AppData%` is the
player's AppData folder, not a directory with that name.

For each matching file:

- only on disk: the launcher uploads it
- only in the cloud: the launcher downloads it
- on neither side: nothing happens
- on both sides: the newer file is kept

## Friends and presence

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#friends-presence{csharp}

Presence status is `online`, `away`, `offline`, or `in_game`. With `in_game`, the SDK sends your
app id automatically. Call `HeartbeatAsync` periodically while the game runs.

Opening a store page is `CuratedClient.OpenStoreOverlay` (Unity: `CuratedUnity.OpenStoreOverlay`): see [Store overlay](./dotnet#store-overlay).
