# Achievements and stats

Define achievements and stats in the admin under
[Game Build > Achievements and Stats](/admin/game-build/achievements-stats). The **API Name** you
set there is the name you pass from the game.

## Unlock an achievement

| Binding | Call |
| --- | --- |
| .NET / Unity | `await new Achievement("win_level_1").TriggerAsync()` or `Achievements.TriggerAsync("win_level_1")` |
| Godot | `await Curated.unlock_achievement("win_level_1")` |
| Unreal | `UnlockAchievement("win_level_1", OnUnlocked)` |
| RPG Maker | `Curated.unlockAchievement("win_level_1")` or the **Unlock achievement** plugin command |

Unlocking is idempotent. Each call reports whether it newly unlocked the achievement. Newly
unlocked achievements notify the launcher overlay, so do not draw your own Curated toast.

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#achievements{csharp}

## Stats

Stats with **Set By** = Client can be written from the game. Server stats are written by your
game server only.

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#stats{csharp}

| Binding | Set | Increment | Batch |
| --- | --- | --- | --- |
| .NET / Unity | `Stats.SetStatAsync` (or `SetStat` + `StoreStatsAsync`) | `Stats.IncrementStatAsync` | `Stats.StoreStatsAsync(updates)` |
| Godot | `set_stat` | `increment_stat` | `store_stats` |
| Unreal | `SetStat` | `IncrementStat` | |
| RPG Maker | `setStat` | `incrementStat` | |

Achievements tied to a stat unlock automatically when a write crosses their threshold. The write
result lists them.
