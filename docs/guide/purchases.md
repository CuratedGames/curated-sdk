# In-app purchases / Microtransactions

Your **game server** creates and finalizes the order with the [publisher Web API key](/admin/web-api). The **game client** asks the Curated launcher overlay to confirm the purchase. 

Rememeber: the publisher key never goes into the game client.

The samples under [Game client](#game-client) are that overlay handoff. Which Curated base the order uses is chosen in the [game server](#game-server).

## Order flow

The player, the game, your server, and Curated each do a different job.

    The game client asks the server to start an order, the server calls `InitTxn` with the publisher key on either the live or sandbox base, the client asks the Curated launcher overlay to confirm the purchase, and the launcher calls AuthorizeTxn on that same live or sandbox path. Then the server calls FinalizeTxn on the same base it used to start the order. Live orders move wallet funds. Sandbox orders do not.Game clientGame serverCuratedPlayer decides herePublisher key stays hereLive or sandbox base1. Start orderNo publisher key2. InitTxnKey + chosen base3. orderId and transId4. Overlay purchase handoff.5. Authorized, denied, or error6. FinalizeTxnSame base as InitTxnLive: wallet reserved at InitTxn, captured at FinalizeTxn.Sandbox: same statuses, no wallet movement.

GetUserInfo, QueryTxn, and RefundTxn are further server calls on that same base. They are not
part of the client samples.

## Game client

The client asks the Curated launcher overlay to confirm an order the server already created.  
Purchases only run inside the Curated launcher.

When `CURATED_OVERLAY_ENDPOINT` and `CURATED_OVERLAY_TOKEN` are set, every binding opens a TCP
connection and sends one line before any HTTP call:

```json
{"v":1,"token":"<CURATED_OVERLAY_TOKEN>","type":"microtxn_purchase","app_id":1002,"order_id":938473,"trans_id":374839}
```

It reads one line back:

```json
{"v":1,"type":"microtxn_purchase_result","authorized":true,"status":"Authorized","order_id":938473,"trans_id":374839,"error_code":null,"error_description":null}
```

`status` is `Authorized`, `Denied`, or `Error`. The binding maps that status onto its result.
The game does not send a decision. The launcher calls AuthorizeTxn, with
`x-curated-overlay-decision`, on the live or sandbox path the order is already using.

If those overlay variables are missing, the call returns error.

Unity Editor play can still reach the overlay. On init, the Editor sends `editor_attach` to a
running launcher with the signed-in account, without the launcher having started Unity. The
launcher then draws its purchase window on the Game view. Leaving play mode, or quitting a
player build, sends `game_closed` and the launcher cancels that game's microtransaction.
The launcher still calls AuthorizeTxn. A player build without the launcher returns error, as  
above.

1. The game server calls InitTxn and sends `orderId` and `transId` to the client.
2. The client calls the binding's authorize call. The launcher overlay collects the decision.
3. On authorized, the game server calls FinalizeTxn.

| Binding | Call |
| --- | --- |
| .NET / Unity | `MicroTxn.AuthorizeAsync(orderId, transId)` |
| Godot | `Curated.authorize_txn(order_id, trans_id)` |
| Unreal | `AuthorizeTxn(OrderId, TransId, OnDone)` |
| RPG Maker | `Curated.authorizeTxn(orderId, transId)` |

The result is authorized, denied, or error. Error means the overlay was missing, the purchase
could not be completed, or Curated rejected it. It is not a player denial.

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

### Currency

List each item in one currency. Send that ISO code as `currency` and the price as `amount[0]` in that currency's minor units: cents for USD and EUR, whole units for COP and JPY. Curated charges that amount when it is already the player's wallet currency. When it is not, InitTxn converts it with the same automatic regional rates as store prices and reserves the converted amount in the wallet currency. The InitTxn response includes that charge as `currency` and `amount`.

The catalogue below prices the 475-coin pack at 599 US cents (`currency=USD`, `amount[0]=599`). That stays 599 US cents for a US wallet, becomes the euro equivalent for a euro wallet, and becomes the peso equivalent for a Colombian wallet. Do not send a second price table. The client sends the product id. The server looks up `qty` and `amount`.

### Catalogue

This is a live shop example. `itemid` is the product id the client sends. `qty` is how many coins or gems the player receives. `amount` is the price in USD cents. `itemcount` on InitTxn is the number of lines, which is 1 here, not the pack size.

| itemid | description | qty   | amount (USD cents) |
| ------ | ----------- | ----- | ------------------ |
| 1      | Coins       | 475   | 599                |
| 2      | Coins       | 1300  | 1499               |
| 3      | Coins       | 2000  | 1899               |
| 4      | Coins       | 4500  | 3999               |
| 5      | Coins       | 13000 | 9999               |
| 6      | Coins       | 20000 | 12499              |
| 100    | Gems        | 3     | 499                |
| 101    | Gems        | 6     | 899                |
| 102    | Gems        | 10    | 999                |

Remember: keep this table on the server. A client that can choose `amount` can choose its price.

### InitTxn

InitTxn is a form-urlencoded POST. The publisher key is a query parameter, so, to avoid leaking, never log the URL in your game's logs.

`POST https://api.curated.games/{base}/InitTxn/v1/?key=<publisher key>`

`{base}` is `ICuratedMicroTxn` or `ICuratedMicroTxnSandbox`. For the 475-coin pack the body is:

```
orderid=1000000000000000001&curatedid=<player curated id>&appid=1002&itemcount=1&currency=USD&itemid[0]=1&qty[0]=475&amount[0]=599&description[0]=Coins
```

`orderid` is a positive 64-bit integer you mint on the server. One game starts that counter at `1000000000000000000` so it does not collide with another storefront's sequence. `curatedid` is the player's Curated id, the same id the launcher session returned at sign-in. `description[0]` is the line the overlay shows. The client in this shop sends `Coins` or `Gems`.

Omit `usersession`, or send `client`, and the launcher overlay asks the player to confirm. `usersession=web` is accepted and stored for a browser checkout. That checkout is unused: InitTxn does not return a redirect URL, and nothing opens a browser. When it is added, a web order will also send the buyer's public `ipaddress`, and the response will include the URL to open.

`category[0]` is optional. It is a label stored with that line and returned by QueryTxn. The overlay does not show it. Maximum 64 characters.

A success body looks like this. `currency` and `amount` are the reserved charge in the player's wallet currency, which can differ from the USD cents you listed:

```json
{"response":{"result":"OK","params":{"orderid":"1000000000000000001","transid":"1000000001","currency":"USD","amount":"599"}}}
```

A failure body looks like this. Grant nothing when `result` is not `OK`:

```json
{"response":{"result":"Failure","error":{"errorcode":3,"errordesc":"Invalid parameter: curatedid"}}}
```

Store `orderid`, `transid`, `itemid`, the listed currency, the listed amount, and status `pending` before you return the ids to the client. Return only the ids.

FinalizeTxn is the same kind of POST, on the same base, after the overlay reports authorized:

```
orderid=1000000000000000001&transid=1000000001&appid=1002
```

Grant the catalogue `qty` only after that response is `OK`. Add 475 coins for item id 1, or 3 gems for item id 100. The client does not grant.

QueryTxn is a GET on that same base, for orders you still remember when the player signs in:

`GET https://api.curated.games/{base}/QueryTxn/v1/?key=<publisher key>&appid=1002&orderid=1000000000000000001`

`params.status` tells you what to do:

| status | server action |
| --- | --- |
| `authorized`, `approved` | FinalizeTxn, then grant if you have not |
| `captured`, `succeeded` | Grant if you have not |
| `refunded`, `partialrefund`, `chargedback`, `refundedsuspectedfraud`, `refundedfriendlyfraud` | Revoke the catalogue qty if you already granted it |

### Sandbox

Use the sandbox for integration tests. The sandbox base is a server setting: point InitTxn,
FinalizeTxn, QueryTxn, and RefundTxn at `/ICuratedMicroTxnSandbox`. Switch that same code to
`/ICuratedMicroTxn` for live orders. 

A sandbox order still moves through pending, authorized, captured, and refunded. No wallet funds
are reserved, captured, or refunded. On the live base, InitTxn reserves funds and FinalizeTxn
captures them. RefundTxn returns them.

### Unity Cloud Code

[Unity Cloud Code](https://docs.unity.com/en-us/cloud-code/modules/how-to-guides/module-structure)
C# modules are one way to host that server. The publisher key stays in
[Unity Secret Manager](https://docs.unity.com/en-us/services/secret-manager/tutorials/integrations/cloud-code/modules).
The game client calls the module, then calls the Curated SDK only for the player decision.

1. Create a [publisher Web API key](/admin/web-api) and allow **Microtransactions**. Do not put

   the key in the Unity project, and do not log the request URL. The key is a query parameter.
2. In the Unity Dashboard, store the key in Secret Manager under a name only this module reads.

   The sample uses `CURATED_PUBLISHER_KEY`. Allow Cloud Code to read it.
3. Create a Cloud Code C# module. Reference `Com.Unity.Services.CloudCode.Apis` 0.0.21 or newer

   so `SecretManager` is available. Register `GameApiClient` once.
4. Put the [catalogue](#catalogue) in the module. `StartCuratedIAP` takes a product id and a

   description. It looks up `qty` and the USD cent price. The client does not send a price.
5. Mint the next order id on the server, read the signed-in player's Curated id, and POST InitTxn.

   Save the pending order, then return `orderid` and `transid` as strings.
6. Deploy the module.
7. From the game, after Unity Authentication has signed the player in, call `StartCuratedIAP`.
8. Parse the ids and call `MicroTxn.AuthorizeAsync` as in [Game client](#game-client). That call

   is the overlay handoff. It is not the sandbox switch.
9. When the status is authorized, call `FinalizeCuratedIAP` with the order id string. The module

   calls FinalizeTxn on the same base it used for InitTxn, then grants the catalogue `qty`.
10. On later sign-ins, run QueryTxn for orders you still remember, as in the status table above.

The helpers under `Post` are your player record, order counter, and inventory. This module is an
example. It is not part of the Curated SDK and CI does not compile it.

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
    }
}

public class CuratedIAPRequest
{
    public uint ProductID { get; set; }
    public string ProductDescription { get; set; }
}

public class FinalizeCuratedIAPRequest
{
    public string OrderID { get; set; }
}

public class StartCuratedIAPResponse
{
    public string OrderID { get; set; }
    public string TransID { get; set; }
}

public class OrderRecord
{
    public string OrderID { get; set; }
    public string TransID { get; set; }
    public uint ProductID { get; set; }
    public string Status { get; set; }
}

public class CuratedPurchases
{
    const string AppId = "1002";
    const string PublisherKeySecret = "CURATED_PUBLISHER_KEY";
    readonly HttpClient http = new HttpClient();

    // qty is how many coins or gems the player receives. Price is USD cents.
    static readonly Dictionary<uint, (ushort Qty, ushort UsdCents, string Description)> Catalog = new()
    {
        { 1, (475, 599, "Coins") },
        { 2, (1300, 1499, "Coins") },
        { 3, (2000, 1899, "Coins") },
        { 4, (4500, 3999, "Coins") },
        { 5, (13000, 9999, "Coins") },
        { 6, (20000, 12499, "Coins") },
        { 100, (3, 499, "Gems") },
        { 101, (6, 899, "Gems") },
        { 102, (10, 999, "Gems") },
    };

    static string BasePath(IExecutionContext context)
    {
        //we heavily recommend you to automatically set an automatic "developer environment" detection like this to avoid spending while testing.
        if (context.EnvironmentName == "dev")
        {
            return "ICuratedMicroTxnSandbox";
        }
        return "ICuratedMicroTxn";
    }

    [CloudCodeFunction("StartCuratedIAP")]
    public async Task<StartCuratedIAPResponse> StartCuratedIAP(
        IExecutionContext context, IGameApiClient gameApiClient, CuratedIAPRequest request)
    {
        if (!Catalog.TryGetValue(request.ProductID, out var item))
        {
            throw new Exception("Unknown product");
        }

        string curatedId = await CuratedIdForSignedInPlayer(context, gameApiClient);
        string orderId = await NextOrderId(context, gameApiClient);
        string description = request.ProductDescription;
        if (string.IsNullOrEmpty(description))
        {
            description = item.Description;
        }
        var form = new List<KeyValuePair<string, string>>
        {
            new("orderid", orderId),
            new("curatedid", curatedId),
            new("appid", AppId),
            new("itemcount", "1"),
            new("currency", "USD"),
            new("itemid[0]", request.ProductID.ToString(CultureInfo.InvariantCulture)),
            new("qty[0]", item.Qty.ToString(CultureInfo.InvariantCulture)),
            new("amount[0]", item.UsdCents.ToString(CultureInfo.InvariantCulture)),
            new("description[0]", description),
        };
        JObject response = await Post(context, gameApiClient, "InitTxn", form);
        string transId = response["params"]["transid"].ToString();
        await RememberPending(context, gameApiClient, orderId, transId, request.ProductID, item.UsdCents);
        return new StartCuratedIAPResponse { OrderID = orderId, TransID = transId };
    }

    [CloudCodeFunction("FinalizeCuratedIAP")]
    public async Task FinalizeCuratedIAP(
        IExecutionContext context, IGameApiClient gameApiClient, FinalizeCuratedIAPRequest request)
    {
        OrderRecord record = await FindOrderOwnedByCaller(context, gameApiClient, request.OrderID);
        if (record.Status == "granted")
        {
            return;
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("orderid", record.OrderID),
            new("transid", record.TransID),
            new("appid", AppId),
        };
        await Post(context, gameApiClient, "FinalizeTxn", form);
        await Grant(context, gameApiClient, record.ProductID);
        await MarkGranted(context, gameApiClient, record.OrderID);
    }

    async Task<JObject> Post(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string method,
        List<KeyValuePair<string, string>> form)
    {
        Secret key = await gameApiClient.SecretManager.GetSecret(context, PublisherKeySecret);
        // The key is in this URL. Do not log it, and do not return it to the client.
        string url = "https://api.curated.games/" + BasePath(context) + "/" + method
            + "/v1/?key=" + Uri.EscapeDataString(key.Value);
        HttpResponseMessage response = await http.PostAsync(url, new FormUrlEncodedContent(form));
        string body = await response.Content.ReadAsStringAsync();
        JObject root = JObject.Parse(body);
        JToken envelope = root["response"];
        if (envelope == null || !string.Equals(envelope["result"].ToString(), "OK", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Curated " + method + " failed");
        }
        return (JObject)envelope;
    }
}
```

`CuratedIdForSignedInPlayer` reads the Curated id you stored for `context.PlayerId` at sign-in.
`NextOrderId` is the server counter. `RememberPending` stores the order id, trans id, product id,
listed USD cents, and status `pending`, and it rejects nothing until that write succeeds.
`FindOrderOwnedByCaller` refuses an order whose payer is not `context.PlayerId`. `Grant` adds the
catalogue `qty` for that product id to the player's coins or gems. Do that only after  
FinalizeTxn returns OK.

The game client asks the module for the ids, asks the overlay, then asks the module to finalize.
The first argument of `CallModuleEndpointAsync` is the deployed module name. Sign in to Unity
Authentication before calling it. Product id 1 is the 475-coin pack. The client does not send 599.

```csharp
using System.Collections.Generic;
using Curated.Sdk;
using Unity.Services.CloudCode;

var request = new CuratedIAPRequest
{
    ProductID = 1,
    ProductDescription = "Coins",
};
StartCuratedIAPResponse started = await CloudCodeService.Instance.CallModuleEndpointAsync<StartCuratedIAPResponse>(
    "CuratedPurchases",
    "StartCuratedIAP",
    new Dictionary<string, object> { ["request"] = request });

long orderId = long.Parse(started.OrderID);
long transId = long.Parse(started.TransID);
// Overlay handoff from the game client section. This call is not the sandbox switch.
MicroTxnAuthorization purchase = await MicroTxn.AuthorizeAsync(orderId, transId);

if (purchase.Status == MicroTxnAuthorizationStatus.Authorized)
{
    await CloudCodeService.Instance.CallModuleEndpointAsync(
        "CuratedPurchases",
        "FinalizeCuratedIAP",
        new Dictionary<string, object>
        {
            ["request"] = new FinalizeCuratedIAPRequest { OrderID = started.OrderID },
        });
}
```
