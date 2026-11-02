<!-- Keeps /guide/purchases#in-game-purchases working after the heading rename. -->
<div id="in-game-purchases"></div>

# In-app purchases / Microtransactions

Curated in-app purchases follow the Steam microtransaction flow. Your **game server** creates
and finalizes the order with the [publisher Web API key](/admin/web-api). The **game client** only
reports the player's decision, using the player session. The publisher key never goes into the
game client.

The samples under [Game client](#game-client) are that player decision. They are not the sandbox.
Which Curated base the order uses is chosen in the [game server](#game-server).

## Order flow

The player, the game, your server, and Curated each do a different job.

<div class="purchase-flow">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 760 560" role="img" aria-labelledby="purchase-flow-title purchase-flow-desc">
  <title id="purchase-flow-title">In-app purchase flow across the game client, the game server, and Curated</title>
  <desc id="purchase-flow-desc">The game client asks the server to start an order, the server calls InitTxn with the publisher key on either the live or sandbox base, the client authorizes with the player session on the live path, then the server calls FinalizeTxn on the same base it used to start the order. Live orders move wallet funds. Sandbox orders do not.</desc>
  <style>
    .pf-box { fill: var(--curated-bg-deep); stroke: var(--curated-line-soft); }
    .pf-title { fill: var(--curated-fg-bright); font: 600 15px "Source Sans 3", sans-serif; }
    .pf-line { stroke: var(--curated-faint); stroke-width: 1.5; }
    .pf-arrow { stroke: var(--curated-accent); stroke-width: 1.6; fill: none; }
    .pf-label { fill: var(--curated-fg); font: 13px "Source Sans 3", sans-serif; }
    .pf-note { fill: var(--curated-muted); font: 12px "Source Sans 3", sans-serif; }
  </style>
  <rect class="pf-box" x="16" y="16" width="200" height="40" rx="8"/>
  <rect class="pf-box" x="280" y="16" width="200" height="40" rx="8"/>
  <rect class="pf-box" x="544" y="16" width="200" height="40" rx="8"/>
  <text class="pf-title" x="116" y="42" text-anchor="middle">Game client</text>
  <text class="pf-title" x="380" y="42" text-anchor="middle">Game server</text>
  <text class="pf-title" x="644" y="42" text-anchor="middle">Curated</text>
  <line class="pf-line" x1="116" y1="64" x2="116" y2="500"/>
  <line class="pf-line" x1="380" y1="64" x2="380" y2="500"/>
  <line class="pf-line" x1="644" y1="64" x2="644" y2="500"/>
  <text class="pf-note" x="116" y="84" text-anchor="middle">Player decides here</text>
  <text class="pf-note" x="380" y="84" text-anchor="middle">Publisher key stays here</text>
  <text class="pf-note" x="644" y="84" text-anchor="middle">Live or sandbox base</text>
  <polyline class="pf-arrow" points="124,118 368,118" marker-end="url(#pf-head)"/>
  <text class="pf-label" x="246" y="110" text-anchor="middle">1. Start order</text>
  <text class="pf-note" x="246" y="136" text-anchor="middle">No publisher key</text>
  <polyline class="pf-arrow" points="388,176 632,176" marker-end="url(#pf-head)"/>
  <text class="pf-label" x="510" y="168" text-anchor="middle">2. InitTxn</text>
  <text class="pf-note" x="510" y="194" text-anchor="middle">Key + chosen base</text>
  <polyline class="pf-arrow" points="632,236 388,236" marker-end="url(#pf-head)"/>
  <polyline class="pf-arrow" points="368,268 124,268" marker-end="url(#pf-head)"/>
  <text class="pf-label" x="380" y="258" text-anchor="middle">3. orderId and transId</text>
  <polyline class="pf-arrow" points="124,330 632,330" marker-end="url(#pf-head)"/>
  <text class="pf-label" x="378" y="322" text-anchor="middle">4. AuthorizeTxn, player session</text>
  <text class="pf-note" x="378" y="348" text-anchor="middle">Live path only. Not the sandbox base.</text>
  <polyline class="pf-arrow" points="124,392 368,392" marker-end="url(#pf-head)"/>
  <text class="pf-label" x="246" y="384" text-anchor="middle">5. Authorized, denied, or error</text>
  <polyline class="pf-arrow" points="388,444 632,444" marker-end="url(#pf-head)"/>
  <text class="pf-label" x="510" y="436" text-anchor="middle">6. FinalizeTxn</text>
  <text class="pf-note" x="510" y="462" text-anchor="middle">Same base as InitTxn</text>
  <text class="pf-note" x="380" y="520" text-anchor="middle">Live: wallet reserved at InitTxn, captured at FinalizeTxn.</text>
  <text class="pf-note" x="380" y="538" text-anchor="middle">Sandbox: same statuses, no wallet movement.</text>
  <defs>
    <marker id="pf-head" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
      <path d="M 0 1.2 L 9 5 L 0 8.8 z" fill="var(--curated-accent)"/>
    </marker>
  </defs>
</svg>
</div>

GetUserInfo, QueryTxn, and RefundTxn are further server calls on that same base. They are not
part of the client samples.

## Game client

The client reports the player's decision for an order the server already created. Every binding
calls `POST /ICuratedMicroTxn/AuthorizeTxn/v1/` with the player session. There is no sandbox
argument on these calls.

1. The game server calls InitTxn and sends `orderId` and `transId` to the client.
2. The client calls the binding's authorize call with the player's decision.
3. On authorized, the game server calls FinalizeTxn.

| Binding | Call |
| --- | --- |
| .NET / Unity | `MicroTxn.AuthorizeAsync(orderId, transId, authorized)` |
| Godot | `Curated.authorize_txn(order_id, trans_id, authorized)` |
| Unreal | `AuthorizeTxn(OrderId, TransId, bAuthorized, OnDone)` |
| RPG Maker | `Curated.authorizeTxn(orderId, transId, authorized)` |

The result is authorized, denied, or error. Error means Curated rejected the call. It is not a
player denial.

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#microtxn{csharp}

RPG Maker:

<<< @/../engines/rpgmaker/CuratedSDK.test.mjs#purchase{js}

## Game server

The server holds the publisher key and chooses the API base.

| Orders | Base |
| --- | --- |
| Live | `/ICuratedMicroTxn` |
| Sandbox | `/ICuratedMicroTxnSandbox` |

Both bases have the same methods: GetUserInfo, InitTxn, AuthorizeTxn, FinalizeTxn, QueryTxn, and
RefundTxn. Call them from the server. The in-game samples above do not call the sandbox base.

### Sandbox

Use the sandbox for integration tests. The sandbox base is a server setting: point InitTxn,
FinalizeTxn, and the other server methods at `/ICuratedMicroTxnSandbox`. Switch that same code
to `/ICuratedMicroTxn` for live orders.

A sandbox order still moves through pending, authorized, captured, and refunded. No wallet funds
are reserved, captured, or refunded. On the live base, InitTxn reserves funds and FinalizeTxn
captures them. RefundTxn returns them.

### Unity Cloud Code

[Unity Cloud Code](https://docs.unity.com/en-us/cloud-code/modules/how-to-guides/module-structure)
C# modules are one way to host that server. The publisher key stays in
[Unity Secret Manager](https://docs.unity.com/en-us/services/secret-manager/tutorials/integrations/cloud-code/modules).
The game client calls the module, then calls the Curated SDK only for the player decision.

1. Create a [publisher Web API key](/admin/web-api) and allow **Microtransactions**. Do not put
   the key in the Unity project.
2. In the Unity Dashboard, store the key in Secret Manager as `CURATED_PUBLISHER_KEY` and allow
   Cloud Code to read it.
3. Create a Cloud Code C# module. Reference `Com.Unity.Services.CloudCode.Apis` 0.0.21 or newer
   so `SecretManager` is available.
4. Register `GameApiClient` once, then add `StartOrder` and `FinalizeOrder`. Read the secret
   inside the module and call `https://api.curated.games` plus the base you want.
5. Deploy the module.
6. From the game, after Unity Authentication has signed the player in, call `StartOrder`. It
   returns `orderId` and `transId`.
7. Call `MicroTxn.AuthorizeAsync` as in [Game client](#game-client). That call is not the sandbox.
8. When the status is authorized, call `FinalizeOrder`. The module calls FinalizeTxn on the same
   base it used for InitTxn.
9. Grant the item only after FinalizeTxn succeeds. QueryTxn and RefundTxn stay on the module.

`UseSandbox` below is the server-side switch. This module is an example. It is not part of the
Curated SDK and CI does not compile it.

```csharp
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
    }
}

public class CuratedPurchases
{
    // Integration tests. Set this to false for live purchases.
    const bool UseSandbox = true;

    static string BasePath =>
        UseSandbox ? "/ICuratedMicroTxnSandbox" : "/ICuratedMicroTxn";

    [CloudCodeFunction("StartOrder")]
    public async Task<OrderIds> StartOrder(
        IExecutionContext context, IGameApiClient gameApiClient, string sku)
    {
        Secret key = await gameApiClient.SecretManager.GetSecret(context, "CURATED_PUBLISHER_KEY");
        string url = "https://api.curated.games" + BasePath + "/InitTxn/v1/";
        // POST url. Authenticate with key.Value and send the item for sku.
        // Read orderid and transid from the response. Do not log or return the key.
        return await InitTxn(url, key.Value, sku);
    }

    [CloudCodeFunction("FinalizeOrder")]
    public async Task FinalizeOrder(
        IExecutionContext context, IGameApiClient gameApiClient, long orderId, long transId)
    {
        Secret key = await gameApiClient.SecretManager.GetSecret(context, "CURATED_PUBLISHER_KEY");
        string url = "https://api.curated.games" + BasePath + "/FinalizeTxn/v1/";
        // POST url with key.Value, orderId, and transId. Same base as StartOrder.
        await FinalizeTxn(url, key.Value, orderId, transId);
    }
}

public class OrderIds
{
    public long OrderId { get; set; }
    public long TransId { get; set; }
}
```

`InitTxn` and `FinalizeTxn` in that module are your HTTP calls to the URL above. Parse `orderid`
and `transid` from the InitTxn response into `OrderIds`. The game client asks the module for those
ids, records the player's decision with the Curated SDK, then asks the module to finalize. The
first argument of `CallModuleEndpointAsync` is the deployed module name. Sign in to Unity
Authentication before calling it.

```csharp
using System.Collections.Generic;
using Curated.Sdk;
using Unity.Services.CloudCode;

OrderIds started = await CloudCodeService.Instance.CallModuleEndpointAsync<OrderIds>(
    "CuratedPurchases",
    "StartOrder",
    new Dictionary<string, object> { ["sku"] = "gems_small" });

MicroTxnAuthorization purchase = await MicroTxn.AuthorizeAsync(
    started.OrderId, started.TransId, authorized: true);

if (purchase.Status == MicroTxnAuthorizationStatus.Authorized)
{
    await CloudCodeService.Instance.CallModuleEndpointAsync(
        "CuratedPurchases",
        "FinalizeOrder",
        new Dictionary<string, object>
        {
            ["orderId"] = started.OrderId,
            ["transId"] = started.TransId,
        });
}
```
