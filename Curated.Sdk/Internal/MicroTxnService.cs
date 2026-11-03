using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class MicroTxnService
{
    public static Task<MicroTxnAuthorization> AuthorizeAsync(
        CuratedClient.ClientState state,
        long orderId,
        long transId,
        CancellationToken ct)
    {
        return OverlayPurchase.ExchangeAsync(state.AppId, orderId, transId, ct);
    }
}
