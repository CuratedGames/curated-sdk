using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

/// <summary>
/// Request/response purchase handoff to the Curated launcher overlay.
/// The game never posts its own boolean. The launcher calls AuthorizeTxn.
/// </summary>
internal static class OverlayPurchase
{
    internal const string RequestType = "microtxn_purchase";
    internal const string ResultType = "microtxn_purchase_result";

    internal static TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    internal static TimeSpan ReadTimeout = TimeSpan.FromMinutes(10);

    private const int MaxLineBytes = 65536;

    public static async Task<MicroTxnAuthorization> ExchangeAsync(
        long appId,
        long orderId,
        long transId,
        CancellationToken cancellationToken)
    {
        if (!OverlayChannel.TryResolve(out var endpoint, out var token)
            || !OverlayNotifier.TryParseEndpoint(endpoint, out var host, out var port))
        {
            return Error(orderId, transId, null, "Purchases require the Curated launcher overlay.");
        }

        var payload = BuildRequest(token.Trim(), appId, orderId, transId);
        TcpClient? client = null;
        try
        {
            client = new TcpClient();
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(ConnectTimeout);
            var connect = client.ConnectAsync(host, port);
            var connectDelay = Task.Delay(Timeout.InfiniteTimeSpan, connectTimeout.Token);
            var connectDone = await Task.WhenAny(connect, connectDelay).ConfigureAwait(false);
            connectTimeout.Cancel();
            Observe(connectDelay);
            if (connectDone != connect)
            {
                client.Close();
                Observe(connect);
                cancellationToken.ThrowIfCancellationRequested();
                return Error(orderId, transId, null, "Curated overlay did not answer.");
            }

            await connect.ConfigureAwait(false);
            using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(payload + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            var line = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return Error(orderId, transId, null, "Curated overlay did not answer.");
            }

            TryParseResult(line, orderId, transId, out var result);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Error(orderId, transId, null, "Curated overlay did not answer.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Error(orderId, transId, null, "Curated overlay did not answer.");
        }
        finally
        {
            client?.Dispose();
        }
    }

    internal static string BuildRequest(string token, long appId, long orderId, long transId)
    {
        var sb = new StringBuilder(128);
        sb.Append("{\"v\":1,\"token\":");
        AppendJsonString(sb, token);
        sb.Append(",\"type\":\"");
        sb.Append(RequestType);
        sb.Append("\",\"app_id\":");
        sb.Append(appId.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"order_id\":");
        sb.Append(orderId.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"trans_id\":");
        sb.Append(transId.ToString(CultureInfo.InvariantCulture));
        sb.Append('}');
        return sb.ToString();
    }

    internal static bool TryParseResult(
        string line,
        long orderId,
        long transId,
        out MicroTxnAuthorization result)
    {
        result = Error(orderId, transId, null, "Curated overlay returned an unreadable purchase result.");
        object? root;
        try
        {
            root = Json.Parse(line.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        if (root is not Dictionary<string, object?> obj)
        {
            return false;
        }

        if (Field(obj, "type") is not string type
            || !string.Equals(type, ResultType, StringComparison.Ordinal))
        {
            return false;
        }

        var echoedOrder = ReadId(Field(obj, "order_id"), orderId);
        var echoedTrans = ReadId(Field(obj, "trans_id"), transId);
        if (Field(obj, "status") is not string status)
        {
            result = Error(echoedOrder, echoedTrans, null, "Curated overlay returned an unreadable purchase result.");
            return true;
        }

        if (status.Equals("Authorized", StringComparison.OrdinalIgnoreCase))
        {
            result = new MicroTxnAuthorization(
                MicroTxnAuthorizationStatus.Authorized,
                echoedOrder,
                echoedTrans,
                null,
                null);
            return true;
        }

        if (status.Equals("Denied", StringComparison.OrdinalIgnoreCase))
        {
            result = new MicroTxnAuthorization(
                MicroTxnAuthorizationStatus.Denied,
                echoedOrder,
                echoedTrans,
                null,
                null);
            return true;
        }

        if (!status.Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            result = Error(echoedOrder, echoedTrans, null, "Curated overlay returned an unreadable purchase result.");
            return true;
        }

        int? errorCode = null;
        if (TryReadInt32(Field(obj, "error_code"), out var code))
        {
            errorCode = code;
        }

        var description = Field(obj, "error_description") as string;
        if (string.IsNullOrWhiteSpace(description))
        {
            description = "Curated could not authorize the order.";
        }

        result = Error(echoedOrder, echoedTrans, errorCode, description);
        return true;
    }

    private static async Task<string?> ReadLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);
        var buffer = new byte[1024];
        var pending = new List<byte>(256);
        try
        {
            while (pending.Count < MaxLineBytes)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }

                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        if (pending.Count > 0 && pending[pending.Count - 1] == (byte)'\r')
                        {
                            pending.RemoveAt(pending.Count - 1);
                        }

                        return Encoding.UTF8.GetString(pending.ToArray());
                    }

                    pending.Add(buffer[i]);
                    if (pending.Count >= MaxLineBytes)
                    {
                        return null;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return null;
    }

    private static void Observe(Task task)
    {
        _ = task.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static MicroTxnAuthorization Error(long orderId, long transId, int? errorCode, string description) =>
        new(MicroTxnAuthorizationStatus.Error, orderId, transId, errorCode, description);

    private static object? Field(Dictionary<string, object?> obj, string name)
    {
        if (obj.TryGetValue(name, out var value))
        {
            return value;
        }

        return null;
    }

    private static long ReadId(object? node, long fallback)
    {
        if (TryReadInt64(node, out var value) && value > 0)
        {
            return value;
        }

        return fallback;
    }

    private static bool TryReadInt64(object? node, out long value)
    {
        switch (node)
        {
            case long number:
                value = number;
                return true;
            case double number when number >= long.MinValue && number <= long.MaxValue && Math.Abs(number - (long)number) < 0.0001:
                value = (long)number;
                return true;
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
        if (node is null)
        {
            value = 0;
            return false;
        }

        if (TryReadInt64(node, out var wide) && wide >= int.MinValue && wide <= int.MaxValue)
        {
            value = (int)wide;
            return true;
        }

        value = 0;
        return false;
    }

    private static void AppendJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (ch < 0x20)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
