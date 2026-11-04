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

    /// <summary>
    /// Open a published Curated store page in the launcher overlay.
    /// <see cref="OverlayToStoreFlag.None"/> opens the page with no cart action.
    /// <see cref="OverlayToStoreFlag.AddToCart"/> and <see cref="OverlayToStoreFlag.AddToCartAndShow"/>
    /// also add the app to the cart, and only for a released or preorderable app.
    /// </summary>
    /// <remarks>
    /// Curated must already be initialized. The overlay must be enabled in the launcher and active
    /// for this application, including after the Unity Editor attach. Sends one
    /// <c>open_store_overlay</c> line to the overlay and reads one result line. On failure the
    /// exception message is the launcher's English text.
    /// </remarks>
    /// <param name="appId">Published Curated app whose store page should open. This can be a different app than the one that is running.</param>
    /// <param name="flag">Store page only, add to cart, or add to cart and show the cart.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="appId"/> is not positive.</exception>
    /// <exception cref="InvalidOperationException">Curated is not initialized.</exception>
    /// <exception cref="CuratedLaunchException">The player does not own the running game.</exception>
    /// <exception cref="CuratedException">The overlay is unavailable, or the launcher rejected the request. <see cref="Exception.Message"/> is the English reason.</exception>
    public static void OpenStoreOverlay(long appId, OverlayToStoreFlag flag)
    {
        if (appId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appId), "app id must be positive");
        }

        _ = CuratedClient.GameState;
        OverlayStore.Open(appId, flag);
    }
}
