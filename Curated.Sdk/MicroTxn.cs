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

    /// <summary>Curated rejected the authorization or returned an unreadable result.</summary>
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

    /// <summary>Steam-style <c>errorcode</c> when <see cref="Status"/> is error; otherwise null.</summary>
    public int? ErrorCode { get; }

    /// <summary>Steam-style <c>errordesc</c> when <see cref="Status"/> is error; otherwise null.</summary>
    public string? ErrorDescription { get; }
}

/// <summary>
/// In-game purchases for the signed-in player. The game server creates the order with InitTxn
/// and finalizes it with FinalizeTxn. This client only records the player's authorization.
/// The publisher key is never read or sent.
/// </summary>
public static class MicroTxn
{
    /// <summary>
    /// Begin authorization for an order the game server created with InitTxn.
    /// <paramref name="authorized"/> is the signed-in player's decision: true keeps the wallet
    /// reserve, false releases it and fails the order.
    /// </summary>
    /// <remarks>
    /// Calls <c>POST /ICuratedMicroTxn/AuthorizeTxn/v1/</c> with the player session bearer token.
    /// There is no publisher <c>key</c> parameter. The game server still calls FinalizeTxn after
    /// the player authorizes. InitTxn may have converted the game's listed price into the
    /// player's wallet currency; this call confirms that converted reserve.
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
        bool authorized,
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
            authorized,
            cancellationToken);
    }
}
