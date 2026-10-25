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

    /// <summary>
    /// Raw <c>CURATED_LANGUAGE</c> value from the launcher (<c>en</c>, <c>es</c>, <c>it</c>), or null when the
    /// launcher did not send one. Prefer <see cref="CurrentGameLanguage"/>.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Current game language as a Curated code, with the <c>en</c> fallback. Same value as
    /// <see cref="CuratedClient.GetCurrentGameLanguage"/>.
    /// </summary>
    public string CurrentGameLanguage => CuratedClient.ResolveGameLanguage(Language);

    public Entitlement Entitlement { get; }
}
