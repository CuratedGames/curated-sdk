using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>Friends list and requests (helpers over Curated social).</summary>
public static class Friends
{
    /// <summary>Accepted friends, with presence when available.</summary>
    public static Task<IReadOnlyList<Friend>> GetFriendsAsync(
        CancellationToken cancellationToken = default) =>
        FriendsService.GetFriendsAsync(CuratedClient.GameState, cancellationToken);

    /// <summary>Send a friend request by username.</summary>
    public static Task RequestAsync(string username, CancellationToken cancellationToken = default) =>
        FriendsService.RequestAsync(CuratedClient.GameState, username, cancellationToken);

    /// <summary>Accept a pending request from <paramref name="userId"/>.</summary>
    public static Task AcceptAsync(Guid userId, CancellationToken cancellationToken = default) =>
        FriendsService.AcceptAsync(CuratedClient.GameState, userId, cancellationToken);

    /// <summary>Incoming pending friend requests.</summary>
    public static Task<IReadOnlyList<Friend>> GetRequestsAsync(
        CancellationToken cancellationToken = default) =>
        FriendsService.GetRequestsAsync(CuratedClient.GameState, cancellationToken);
}
