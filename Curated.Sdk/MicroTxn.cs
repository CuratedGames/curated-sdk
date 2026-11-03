using System;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>Outcome of a player authorization for one in-game purchase order.</summary>
public enum MicroTxnAuthorizationStatus
{
    /// <summary>The player authorized the order. The wallet reserve stays until the game server finalizes it.</summary>
    Authorized,

    /// <summary>The player denied the order. The wallet reserve is released and the order is failed.</summary>
    Denied,

    /// <summary>
    /// The launcher overlay is missing, the purchase could not be completed, or Curated rejected it.
    /// Not a player denial.
    /// </summary>
    Error,
}

/// <summary>
/// Result of <see cref="MicroTxn.AuthorizeAsync"/>. Authorized and denied are player decisions.
/// <see cref="MicroTxnAuthorizationStatus.Error"/> is a Curated failure, not a denial.
/// </summary>
public sealed class MicroTxnAuthorization
{
    internal MicroTxnAuthorization(
        MicroTxnAuthorizationStatus status,
        long orderId,
        long transId,
        int? errorCode,
        string? errorDescription)
    {
        Status = status;
        OrderId = orderId;
        TransId = transId;
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
    }

    /// <summary>Authorized, denied, or error.</summary>
    public MicroTxnAuthorizationStatus Status { get; }

    /// <summary>Order id from InitTxn. Echoed from the response when Curated sends one.</summary>
    public long OrderId { get; }

    /// <summary>Transaction id from InitTxn. Echoed from the response when Curated sends one.</summary>
    public long TransId { get; }

    /// <summary>Curated <c>errorcode</c> when <see cref="Status"/> is error; otherwise null.</summary>
    public int? ErrorCode { get; }

    /// <summary>Curated <c>errordesc</c> when <see cref="Status"/> is error; otherwise null.</summary>
    public string? ErrorDescription { get; }
}

/// <summary>
/// In-game purchases for the signed-in player. The game server creates the order with InitTxn
/// and finalizes it with FinalizeTxn. This client asks the Curated launcher overlay to confirm
/// the purchase. The publisher key is never read or sent, and the client does not POST a decision.
/// </summary>
public static class MicroTxn
{
    /// <summary>
    /// Ask the Curated launcher overlay to confirm an order the game server created with InitTxn.
    /// </summary>
    /// <remarks>
    /// When <c>CURATED_OVERLAY_ENDPOINT</c> and <c>CURATED_OVERLAY_TOKEN</c> are set, opens one
    /// TCP connection, sends a <c>microtxn_purchase</c> line, and maps the overlay
    /// <c>microtxn_purchase_result</c> status to authorized, denied, or error.
    /// The launcher calls AuthorizeTxn. If the overlay variables are missing, the result is
    /// <see cref="MicroTxnAuthorizationStatus.Error"/> and this method does not POST.
    /// Purchases only proceed inside the Curated launcher. InitTxn may have converted the
    /// game's listed price into the player's wallet currency; the overlay confirms that
    /// converted reserve. The game server still calls FinalizeTxn after the player authorizes.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="orderId"/> or <paramref name="transId"/> is not positive.
    /// </exception>
    /// <exception cref="InvalidOperationException">Curated is not initialized.</exception>
    /// <exception cref="CuratedLaunchException">The player does not own the game.</exception>
    /// <exception cref="CuratedException">The session was rejected or Curated returned a non-envelope error.</exception>
    public static Task<MicroTxnAuthorization> AuthorizeAsync(
        long orderId,
        long transId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderId), "order id must be positive");
        }

        if (transId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(transId), "transaction id must be positive");
        }

        return MicroTxnService.AuthorizeAsync(
            CuratedClient.GameState,
            orderId,
            transId,
            cancellationToken);
    }
}
