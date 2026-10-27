using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>Local player and friends presence.</summary>
public static class Presence
{
    /// <summary>
    /// Set your presence. Status is online, away, offline, or in_game.
    /// When status is in_game, the current app id from Init is sent automatically.
    /// </summary>
    public static Task SetAsync(
        string status,
        string? richText = null,
        CancellationToken cancellationToken = default) =>
        FriendsService.SetPresenceAsync(CuratedClient.GameState, status, richText, cancellationToken);

    /// <summary>Keep presence fresh while the game is running.</summary>
    public static Task HeartbeatAsync(CancellationToken cancellationToken = default) =>
        FriendsService.HeartbeatAsync(CuratedClient.GameState, cancellationToken);

    /// <summary>Presence rows for accepted friends.</summary>
    public static Task<IReadOnlyList<Friend>> GetFriendsAsync(
        CancellationToken cancellationToken = default) =>
        FriendsService.GetFriendsPresenceAsync(CuratedClient.GameState, cancellationToken);
}
