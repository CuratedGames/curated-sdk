# In-game purchases

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

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#microtxn{csharp}

RPG Maker:

<<< @/../engines/rpgmaker/CuratedSDK.test.mjs#purchase{js}
