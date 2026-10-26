using System;

namespace Curated.Sdk;

/// <summary>Authenticated, entitled player session returned by <see cref="CuratedClient.InitFromLauncherAsync(long, System.Threading.CancellationToken)"/>.</summary>
public sealed class CuratedSession
{
    internal CuratedSession(long appId, Guid userId, string curatedId, string username, string? language, Entitlement entitlement)
    {
        AppId = appId;
        UserId = userId;
        CuratedId = curatedId;
        Username = username;
        Language = language;
        Entitlement = entitlement;
    }

    public long AppId { get; }

    /// <summary>Private account id (<c>user_id</c> uuid from <c>/auth/me</c>).</summary>
    public Guid UserId { get; }

    /// <summary>Public Curated player id (<c>curated_id</c>, 17-digit decimal string).</summary>
    public string CuratedId { get; }

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
