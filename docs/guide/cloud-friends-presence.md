# Cloud Save, friends, presence

These are part of the C# API (`Curated.Sdk.dll`): .NET, Unity, and Godot C#. Godot GDScript,
Unreal, and RPG Maker do not wrap them yet. Call the same HTTP paths from the
[OpenAPI file](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/openapi/game-sdk.v1.yaml)
if you need them there.

## Cloud Save

Turn Cloud Save on and list the save paths in the admin first, under
[More tools > Cloud Save](/admin/more-tools#cloud-save). Conflicts use last write wins.

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#cloud-save{csharp}

## Friends and presence

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#friends-presence{csharp}

Presence status is `online`, `away`, `offline`, or `in_game`. With `in_game`, the SDK sends your
app id automatically. Call `HeartbeatAsync` periodically while the game runs.

Opening a store page is also on `Friends`: see [Store overlay](./dotnet#store-overlay).
