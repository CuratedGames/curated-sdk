# Curated.Sdk

Unity-friendly C# client and developer tooling for [Curated](https://curated.games). One managed DLL for games, plus the Content Builder CLI for depot uploads.

**Repository:** [RikuTheFuffs/curated-sdk](https://github.com/RikuTheFuffs/curated-sdk) — source of truth for the SDK and CLI.

| Feature | Supported |
|---------|-----------|
| Unity / .NET | ✔ |
| Single managed DLL | ✔ |
| Achievements unlock | ✔ |
| Cloud Save (launch + quit) | ✔ |
| Friends + presence | ✔ |
| Content Builder CLI | ✔ (`tooling/content-builder`) |
| Curated SDK License | ✔ (see LICENSE.md) |

## Getting started (SDK)

Download the Unity zip from [Releases](https://github.com/RikuTheFuffs/curated-sdk/releases) and drop `Curated.Sdk.dll` into `Assets/Plugins/Curated.Sdk/`. Initialize once with your Curated app id and the session token from the Curated launcher:

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

## Content Builder CLI

Windows-first tool to upload depot builds for apps you own:

```bat
cd tooling\content-builder
cargo build --release
curated-content-builder.exe login --login you@studio.com --password "…"
curated-content-builder.exe list-depots --app-id 1000
curated-content-builder.exe upload --app-id 1000 --depot-key content --content C:\game\content --version 1.2.3
```

See [tooling/content-builder/README.md](tooling/content-builder/README.md) and [Agents.md](Agents.md).

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

cd tooling/content-builder && cargo test && cargo build --release
```

## Public API surface

Only game-facing types are public: `CuratedClient`, `Achievement`, `Achievements`, `Cloud`, `CloudSyncResult`, `Friends`, `Friend`, `Presence`, `CuratedException`. HTTP transport and wire models stay internal (`InternalsVisibleTo` for unit tests).

## License

See [LICENSE.md](LICENSE.md). Redistribute only the official object-code packages (DLL / NuGet / Unity zip) with your game.
