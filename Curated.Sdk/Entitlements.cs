using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>Ownership checks for the initialized app.</summary>
public static class Entitlements
{
    /// <summary>
    /// Ask Curated whether the signed-in player still owns this app. Refunds, resales, and
    /// revocations deactivate entitlements, so long sessions may re-check periodically.
    /// </summary>
    public static Task<Entitlement> CheckAsync(CancellationToken cancellationToken = default) =>
        SessionService.CheckEntitlementAsync(CuratedClient.State, cancellationToken);
}
