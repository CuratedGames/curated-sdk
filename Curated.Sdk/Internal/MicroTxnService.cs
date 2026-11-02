using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class MicroTxnService
{
    public static Task<MicroTxnAuthorization> AuthorizeAsync(
        CuratedClient.ClientState state,
        long orderId,
        long transId,
        bool authorized,
        CancellationToken ct)
    {
        // The game's boolean is ignored. The launcher overlay collects the decision
        // and calls AuthorizeTxn itself. Never POST that boolean from the client.
        _ = authorized;
        return OverlayPurchase.ExchangeAsync(state.AppId, orderId, transId, ct);
    }
}
