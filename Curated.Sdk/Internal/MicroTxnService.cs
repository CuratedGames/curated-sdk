using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class MicroTxnService
{
    internal const string AuthorizePath = "/ICuratedMicroTxn/AuthorizeTxn/v1/";

    public static async Task<MicroTxnAuthorization> AuthorizeAsync(
        CuratedClient.ClientState state,
        long orderId,
        long transId,
        bool authorized,
        CancellationToken ct)
    {
        // Player session only. Never attach a publisher key (query or body).
        var body = new Dictionary<string, object>
        {
            ["orderid"] = orderId,
            ["transid"] = transId,
            ["appid"] = state.AppId,
            ["authorized"] = authorized,
        };

        string json;
        try
        {
            json = await state.Transport
                .SendAsync("POST", AuthorizePath, body, ct)
                .ConfigureAwait(false);
        }
        catch (CuratedException ex)
        {
            if (TryParseEnvelope(ex.ResponseBody, orderId, transId, authorized, out var parsed)
                && parsed.Status == MicroTxnAuthorizationStatus.Error)
            {
                return parsed;
            }

            throw;
        }

        if (!TryParseEnvelope(json, orderId, transId, authorized, out var result))
        {
            return Error(orderId, transId, null, "Curated returned an unreadable authorization response.");
        }

        return result;
    }

    internal static bool TryParseEnvelope(
        string? json,
        long orderId,
        long transId,
        bool submittedAuthorized,
        out MicroTxnAuthorization result)
    {
        result = Error(orderId, transId, null, "Curated returned an unreadable authorization response.");
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        object? root;
        try
        {
            root = Json.Parse(json);
        }
        catch (FormatException)
        {
            return false;
        }

        if (root is not Dictionary<string, object?> rootObj
            || !TryGet(rootObj, "response", out var responseNode)
            || responseNode is not Dictionary<string, object?> response)
        {
            return false;
        }

        if (!TryGet(response, "result", out var resultNode) || resultNode is not string resultText)
        {
            return false;
        }

        if (string.Equals(resultText, "OK", StringComparison.OrdinalIgnoreCase))
        {
            var echoedOrder = orderId;
            var echoedTrans = transId;
            var decision = submittedAuthorized;
            if (TryGet(response, "params", out var paramsNode)
                && paramsNode is Dictionary<string, object?> parms)
            {
                if (TryReadInt64(Field(parms, "orderid"), out var parsedOrder) && parsedOrder > 0)
                {
                    echoedOrder = parsedOrder;
                }

                if (TryReadInt64(Field(parms, "transid"), out var parsedTrans) && parsedTrans > 0)
                {
                    echoedTrans = parsedTrans;
                }

                if (parms.ContainsKeyNormalized("authorized"))
                {
                    if (!TryReadAuthorized(Field(parms, "authorized"), out decision))
                    {
                        result = Error(echoedOrder, echoedTrans, null, "Curated returned an unreadable authorization response.");
                        return true;
                    }
                }
            }

            result = new MicroTxnAuthorization(
                decision ? MicroTxnAuthorizationStatus.Authorized : MicroTxnAuthorizationStatus.Denied,
                echoedOrder,
                echoedTrans,
                null,
                null);
            return true;
        }

        if (!string.Equals(resultText, "Failure", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int? errorCode = null;
        string? errorDescription = null;
        if (TryGet(response, "error", out var errorNode) && errorNode is Dictionary<string, object?> error)
        {
            if (TryReadInt32(Field(error, "errorcode"), out var code))
            {
                errorCode = code;
            }

            if (Field(error, "errordesc") is string description && !string.IsNullOrWhiteSpace(description))
            {
                errorDescription = description;
            }
        }

        result = Error(
            orderId,
            transId,
            errorCode,
            errorDescription ?? "Curated could not authorize the order.");
        return true;
    }

    private static MicroTxnAuthorization Error(long orderId, long transId, int? errorCode, string description) =>
        new(MicroTxnAuthorizationStatus.Error, orderId, transId, errorCode, description);

    private static bool TryGet(Dictionary<string, object?> obj, string name, out object? value)
    {
        var want = Normalize(name);
        foreach (var pair in obj)
        {
            if (Normalize(pair.Key) == want)
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static object? Field(Dictionary<string, object?> obj, string name) =>
        TryGet(obj, name, out var value) ? value : null;

    private static bool ContainsKeyNormalized(this Dictionary<string, object?> obj, string name)
    {
        var want = Normalize(name);
        foreach (var key in obj.Keys)
        {
            if (Normalize(key) == want)
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string name) => name.Replace("_", string.Empty).ToLowerInvariant();

    private static bool TryReadAuthorized(object? node, out bool authorized)
    {
        switch (node)
        {
            case bool flag:
                authorized = flag;
                return true;
            case long number when number == 0 || number == 1:
                authorized = number == 1;
                return true;
            case double number when number == 0d || number == 1d:
                authorized = number == 1d;
                return true;
            case string text when text == "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase):
                authorized = false;
                return true;
            case string text when text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase):
                authorized = true;
                return true;
            default:
                authorized = false;
                return false;
        }
    }

    private static bool TryReadInt64(object? node, out long value)
    {
        switch (node)
        {
            case long number:
                value = number;
                return true;
            case double number when number >= long.MinValue && number <= long.MaxValue:
                value = (long)number;
                return Math.Abs(number - value) < 0.0001;
            case string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static bool TryReadInt32(object? node, out int value)
    {
        if (TryReadInt64(node, out var wide) && wide >= int.MinValue && wide <= int.MaxValue)
        {
            value = (int)wide;
            return true;
        }

        value = 0;
        return false;
    }
}
