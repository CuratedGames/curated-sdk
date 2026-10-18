# Curated.Sdk

Unity-friendly C# client for Curated. One DLL, Curated-style naming, no hub transport types in the public surface.

**Canonical public repo:** [CuratedGames/curated-sdk](https://github.com/CuratedGames/curated-sdk)  
This folder (`packages/sdk-csharp` in the Curated monorepo) is a development mirror. GitHub Release assets (Unity zip, DLL, NuGet) publish from **curated-sdk**. Sync with `scripts/push-to-curated-sdk.sh` when you have write access.

| Feature | Supported |
|---------|-----------|
| Unity / .NET | ✔ |
| Single managed DLL | ✔ |
| Achievements unlock | ✔ |
| Cloud Save (launch + quit) | ✔ |
| Friends + presence | ✔ |
| Curated SDK License | ✔ (see LICENSE.md) |

## Getting started

Download the Unity zip from [curated-sdk Releases](https://github.com/CuratedGames/curated-sdk/releases) and drop `Curated.Sdk.dll` into `Assets/Plugins/Curated.Sdk/`. Initialize once with your Curated app id and the session token from the Curated launcher:

```csharp
using Curated.Sdk;

try
{
    CuratedClient.Init(1001, sessionToken);
}
catch (System.Exception e)
{
    // Could not start Curated (missing session, etc.)
}
```

When the game is closing:

```csharp
CuratedClient.Shutdown();
```

## Achievements

```csharp
await Achievements.RefreshAsync();

foreach (var a in Achievements.All)
{
    Console.WriteLine($"{a.Name} ({a.State})");
}

var ach = new Achievement("win_level_1");
await ach.TriggerAsync();
```

## Cloud Save

```csharp
await Cloud.SyncOnLaunchAsync(saveFolder);
await Cloud.SyncOnQuitAsync(saveFolder, "saves/slot1.sav");
```

## Friends and presence

```csharp
foreach (var friend in await Friends.GetFriendsAsync())
{
    Console.WriteLine($"{friend.Name}: {friend.PresenceStatus} / {friend.IsOnline}");
}

await Presence.SetAsync("in_game", "Level 3");
await Presence.HeartbeatAsync();
```

## Build, test, pack

```bash
dotnet test Curated.Sdk.sln --configuration Release
dotnet pack Curated.Sdk/Curated.Sdk.csproj --configuration Release -o artifacts/nuget
bash scripts/pack-unity-zip.sh
```

## Public API surface

Only game-facing types are public: `CuratedClient`, `Achievement`, `Achievements`, `Cloud`, `CloudSyncResult`, `Friends`, `Friend`, `Presence`, `CuratedException`. HTTP transport and wire models stay internal (`InternalsVisibleTo` for unit tests).

## License

See [LICENSE.md](LICENSE.md). Redistribute only the official object-code packages (DLL / NuGet / Unity zip) with your game.
