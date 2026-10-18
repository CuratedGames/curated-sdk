# Agents.md — Curated SDK and Content Builder

Guide for coding agents and developers integrating a game with Curated.

**Source of truth:** [github.com/RikuTheFuffs/curated-sdk](https://github.com/RikuTheFuffs/curated-sdk)

| Piece | Path / download |
|-------|-----------------|
| Game SDK (C# DLL) | `Curated.Sdk/` — Releases ship Unity zip + NuGet + DLL |
| Content Builder CLI | `tooling/content-builder/` — Windows-first depot upload tool |
| License | [LICENSE.md](LICENSE.md) (Curated SDK License) |

---

## 1. Integrate the SDK (Unity DLL)

### Download

1. Open [curated-sdk Releases](https://github.com/RikuTheFuffs/curated-sdk/releases).
2. Download `Curated.Sdk.*.unity.zip` (or the stable `Curated.Sdk.unity.zip`).
3. Extract `Curated.Sdk.dll` into `Assets/Plugins/Curated.Sdk/`.

Optional NuGet: `Curated.Sdk` from the same Release (`*.nupkg`).

### Initialize

Use the Curated **app id** and a **player session token** from the Curated launcher (or a signed-in player session).

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

// On quit:
CuratedClient.Shutdown();
```

Default hub: `https://api.curated.games`. Achievements and Cloud Save need an active entitlement for that app.

### Achievements

```csharp
await Achievements.RefreshAsync();
var ach = new Achievement("win_level_1");
await ach.TriggerAsync();
```

Define achievement names in the admin Achievements tool. Prefer one shared name string if you also unlock on other storefronts.

### Cloud Save

```csharp
await Cloud.SyncOnLaunchAsync(saveFolder);
await Cloud.SyncOnQuitAsync(saveFolder, "saves/slot1.sav");
```

Configure path patterns on the Cloud Save tool before players sync. Conflict policy: last write wins.

### Friends and presence

```csharp
foreach (var friend in await Friends.GetFriendsAsync())
{
    // friend.Name, friend.PresenceStatus, friend.IsOnline
}
await Presence.SetAsync("in_game", "Level 3");
await Presence.HeartbeatAsync();
```

### Public API surface

Game-facing types only: `CuratedClient`, `Achievement`, `Achievements`, `Cloud`, `CloudSyncResult`, `Friends`, `Friend`, `Presence`, `CuratedException`. Transport and wire models stay internal.

### Smoke checklist

- [ ] Init with real app id + launcher session
- [ ] Unlock one test achievement
- [ ] Sync Cloud Save on launch and quit
- [ ] Friends list + presence heartbeat
- [ ] Shutdown on quit without hanging

---

## 2. Content Builder CLI (depot upload)

Canonical folder: [`tooling/content-builder`](tooling/content-builder). Binary name: `curated-content-builder`.

### Build or download

```bat
cd tooling\content-builder
cargo build --release
```

Or take the Windows artifact from [Releases](https://github.com/RikuTheFuffs/curated-sdk/releases) when published.

### Login (email or username + password)

Caches a session token under `%USERPROFILE%\.curated\credentials.json` (Unix: `~/.curated/credentials.json`). Later commands auto-authenticate from that file.

```bat
curated-content-builder.exe login --login you@studio.com --password "…"
curated-content-builder.exe login --username studio_dev --password "…"
curated-content-builder.exe whoami
```

`--email` works as an alias of `--login`. Override API origin with `--api-base` / `CURATED_API_BASE`.

### List depots

Only apps owned by the signed-in developer are allowed.

```bat
curated-content-builder.exe list-depots --app-id 1000
```

### Upload

```bat
curated-content-builder.exe upload ^
  --app-id 1000 ^
  --depot-key content ^
  --content C:\game\content ^
  --version 1.2.3
```

Or use a script (see `tooling/content-builder/examples/simple_app_build.toml`):

```bat
curated-content-builder.exe upload --script scripts\simple_app_build.toml
```

Ownership is checked via the API before upload. Non-owned apps are rejected. After upload, use admin **Game Build** to mark clean and set Default/Beta live.

### Logout

```bat
curated-content-builder.exe logout
```

---

## 3. Dual-storefront tip

If the game already unlocks achievements or cloud-saves on another storefront, keep those calls behind your existing compile flags and add parallel Curated calls when the player is entitled on Curated.
