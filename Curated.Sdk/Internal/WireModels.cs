using System;
using System.Collections.Generic;

namespace Curated.Sdk.Internal;

internal sealed class SessionUserWire
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = "";

    /// <summary>Public 17-digit Curated player id. Empty when the hub omits it.</summary>
    public string CuratedId { get; set; } = "";
}

internal sealed class EntitlementWire
{
    public long AppId { get; set; }

    public string? Source { get; set; }

    public DateTimeOffset? AcquiredAt { get; set; }
}

internal sealed class OpenIdIdTokenRequest
{
    public long AppId { get; set; }
}

internal sealed class OpenIdIdTokenWire
{
    public string IdToken { get; set; } = "";

    public int ExpiresIn { get; set; }
}

internal sealed class UnlockResult
{
    public long AppId { get; set; }

    public string ApiName { get; set; } = "";

    public Guid? AchievementId { get; set; }

    public bool NewlyUnlocked { get; set; }

    public string Source { get; set; } = "native";

    public string DisplayName { get; set; } = "";

    public string Description { get; set; } = "";

    public string? IconAchievedUri { get; set; }

    public double? GlobalUnlockPercent { get; set; }

    public bool Unlocked => NewlyUnlocked || !string.IsNullOrEmpty(ApiName);
}

/// Toast payload for an achievement unlocked via stats or explicit unlock.
internal sealed class AchievementToastWire
{
    public string ApiName { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Description { get; set; } = "";

    public string? IconAchievedUri { get; set; }

    public double? GlobalUnlockPercent { get; set; }

    public bool NewlyUnlocked { get; set; } = true;
}

internal sealed class AchievementProgressItem
{
    public string ApiName { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Description { get; set; } = "";

    public bool Unlocked { get; set; }

    public DateTimeOffset? UnlockedAt { get; set; }

    public string? Source { get; set; }
}

internal sealed class AchievementProgressResult
{
    public long AppId { get; set; }

    public int Unlocked { get; set; }

    public int Total { get; set; }

    public IReadOnlyList<AchievementProgressItem> Achievements { get; set; } =
        Array.Empty<AchievementProgressItem>();
}

internal sealed class AchievementListResult
{
    public IReadOnlyList<AchievementProgressItem> Achievements { get; set; } =
        Array.Empty<AchievementProgressItem>();
}

internal sealed class CloudSaveFile
{
    public string RelativePath { get; set; } = "";

    public string ContentHash { get; set; } = "";

    public long SizeBytes { get; set; }
}

internal sealed class CloudSaveSyncRequest
{
    public string Direction { get; set; } = "download";

    public IReadOnlyList<CloudSaveFile> Files { get; set; } = Array.Empty<CloudSaveFile>();
}

internal sealed class CloudSaveSignedUrl
{
    public string RelativePath { get; set; } = "";

    public string Action { get; set; } = "";

    public string? Url { get; set; }

    public string ContentHash { get; set; } = "";

    public long SizeBytes { get; set; }
}

internal sealed class CloudSaveSyncResultWire
{
    public string Direction { get; set; } = "";

    public string ConflictPolicy { get; set; } = "last_write_wins";

    public IReadOnlyList<CloudSaveSignedUrl> Urls { get; set; } = Array.Empty<CloudSaveSignedUrl>();
}

internal sealed class FriendRow
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = "";

    public string Status { get; set; } = "";
}

internal sealed class PresenceRow
{
    public Guid UserId { get; set; }

    public string? Username { get; set; }

    public string Status { get; set; } = "offline";

    public string RichText { get; set; } = "";

    public long? AppId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class SetPresenceRequest
{
    public string Status { get; set; } = "online";

    public string RichText { get; set; } = "";

    public long? AppId { get; set; }
}

internal sealed class PlayerStatProgressWire
{
    public string ApiName { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string ValueType { get; set; } = "int";

    public double Value { get; set; }

    public double DefaultValue { get; set; }

    public double? MinValue { get; set; }

    public double? MaxValue { get; set; }

    public bool IncrementOnly { get; set; }

    public string SetBy { get; set; } = "client";

    public bool Stored { get; set; }
}

internal sealed class StatsProgressResponseWire
{
    public long AppId { get; set; }

    public IReadOnlyList<PlayerStatProgressWire> Stats { get; set; } =
        Array.Empty<PlayerStatProgressWire>();
}

internal sealed class SetStatRequestWire
{
    public double Value { get; set; }
}

internal sealed class SetStatResponseWire
{
    public long AppId { get; set; }

    public PlayerStatProgressWire Stat { get; set; } = new();

    public double PreviousValue { get; set; }

    public IReadOnlyList<string> UnlockedAchievements { get; set; } = Array.Empty<string>();

    public IReadOnlyList<AchievementToastWire> UnlockedAchievementToasts { get; set; } =
        Array.Empty<AchievementToastWire>();
}

internal sealed class IncrementStatRequestWire
{
    public double? Delta { get; set; }
}

internal sealed class IncrementStatResponseWire
{
    public long AppId { get; set; }

    public PlayerStatProgressWire Stat { get; set; } = new();

    public double PreviousValue { get; set; }

    public double Delta { get; set; }

    public IReadOnlyList<string> UnlockedAchievements { get; set; } = Array.Empty<string>();

    public IReadOnlyList<AchievementToastWire> UnlockedAchievementToasts { get; set; } =
        Array.Empty<AchievementToastWire>();
}

internal sealed class StoreStatEntryWire
{
    public string ApiName { get; set; } = "";

    public double? Value { get; set; }

    public double? Delta { get; set; }
}

internal sealed class StoreStatsRequestWire
{
    public IReadOnlyList<StoreStatEntryWire> Stats { get; set; } =
        Array.Empty<StoreStatEntryWire>();
}

internal sealed class StoreStatsUpdatedItemWire
{
    public PlayerStatProgressWire Stat { get; set; } = new();

    public double PreviousValue { get; set; }
}

internal sealed class StoreStatsResponseWire
{
    public long AppId { get; set; }

    public IReadOnlyList<StoreStatsUpdatedItemWire> Updated { get; set; } =
        Array.Empty<StoreStatsUpdatedItemWire>();

    public IReadOnlyList<string> UnlockedAchievements { get; set; } = Array.Empty<string>();

    public IReadOnlyList<AchievementToastWire> UnlockedAchievementToasts { get; set; } =
        Array.Empty<AchievementToastWire>();
}
