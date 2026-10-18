using System;

namespace Curated.Sdk;

/// <summary>One accepted friend on Curated.</summary>
public sealed class Friend
{
    public Friend(Guid id, string name, string presenceStatus, string richText, long? appId)
    {
        Id = id;
        Name = name ?? "";
        PresenceStatus = presenceStatus ?? "offline";
        RichText = richText ?? "";
        AppId = appId;
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>online, away, offline, or in_game.</summary>
    public string PresenceStatus { get; }

    public string RichText { get; }

    public long? AppId { get; }

    public bool IsOnline =>
        string.Equals(PresenceStatus, "online", StringComparison.OrdinalIgnoreCase)
        || string.Equals(PresenceStatus, "in_game", StringComparison.OrdinalIgnoreCase)
        || string.Equals(PresenceStatus, "away", StringComparison.OrdinalIgnoreCase);
}
