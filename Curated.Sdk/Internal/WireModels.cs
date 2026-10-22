using System;
using System.Collections.Generic;

namespace Curated.Sdk.Internal;

internal sealed class SessionUserWire
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = "";
}

internal sealed class EntitlementWire
{
    public long AppId { get; set; }

    public string? Source { get; set; }

    public DateTimeOffset? AcquiredAt { get; set; }
}

internal sealed class UnlockResult
{
    public long AppId { get; set; }

    public string ApiName { get; set; } = "";

    public Guid? AchievementId { get; set; }

    public bool NewlyUnlocked { get; set; }

    public string Source { get; set; } = "native";

    public bool Unlocked => NewlyUnlocked || !string.IsNullOrEmpty(ApiName);
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
