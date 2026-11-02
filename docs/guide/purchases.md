<!-- Keeps /guide/purchases#in-game-purchases working after the heading rename. -->
<div id="in-game-purchases"></div>

# In-app purchases / Microtransactions

Curated in-game purchases follow the Steam microtransaction flow. Your **game server** creates
and finalizes the order with the [publisher Web API key](/admin/web-api). The **game client** only
reports the player's decision, using the player session. The publisher key never goes into the
game client.

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

## Sandbox

Use the sandbox for integration tests. The live API base is `/ICuratedMicroTxn`. The sandbox
base is `/ICuratedMicroTxnSandbox`. Both bases have the same methods: GetUserInfo, InitTxn,
AuthorizeTxn, FinalizeTxn, QueryTxn, and RefundTxn.

A sandbox order still moves through pending, authorized, captured, and refunded. No wallet funds
are reserved, captured, or refunded.

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#microtxn{csharp}

RPG Maker:

<<< @/../engines/rpgmaker/CuratedSDK.test.mjs#purchase{js}
