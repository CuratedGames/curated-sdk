using System;

namespace Curated.Sdk;

/// <summary>Whether the signed-in player owns the initialized app on Curated.</summary>
public sealed class Entitlement
{
    internal Entitlement(long appId, bool isEntitled, string? source, DateTimeOffset? acquiredAt)
    {
        AppId = appId;
        IsEntitled = isEntitled;
        Source = source;
        AcquiredAt = acquiredAt;
    }

    public long AppId { get; }

    /// <summary>True when the player has an active entitlement for <see cref="AppId"/>.</summary>
    public bool IsEntitled { get; }

    /// <summary>How the copy was obtained: <c>primary</c>, <c>resale</c>, <c>grant</c>, or <c>bonus</c>.</summary>
    public string? Source { get; }

    public DateTimeOffset? AcquiredAt { get; }
}
