using System;

namespace Curated.Sdk;

/// <summary>Authenticated, entitled player session returned by <see cref="CuratedClient.InitFromLauncherAsync(long, System.Threading.CancellationToken)"/>.</summary>
public sealed class CuratedSession
{
    internal CuratedSession(long appId, Guid userId, string username, string? language, Entitlement entitlement)
    {
        AppId = appId;
        UserId = userId;
        Username = username;
        Language = language;
        Entitlement = entitlement;
    }

    public long AppId { get; }

    /// <summary>Curated player id.</summary>
    public Guid UserId { get; }

    /// <summary>Player display name.</summary>
    public string Username { get; }

    /// <summary>Player's Curated main language (<c>en</c>, <c>es</c>, <c>it</c>), or null when the launcher did not send one.</summary>
    public string? Language { get; }

    public Entitlement Entitlement { get; }
}
