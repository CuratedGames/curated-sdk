using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Curated.Sdk.Internal;

/// <summary>
/// Request/response handoff that asks the Curated launcher overlay to open a store page.
/// The launcher decides whether the app and flag are allowed and returns one result line.
/// </summary>
internal static class OverlayStore
{
    internal const string RequestType = "open_store_overlay";
    internal const string ResultType = "open_store_overlay_result";

    internal const string OverlayUnavailableMessage =
        "The Curated overlay is not enabled or is not active for this application.";

    internal const string OverlaySilentMessage = "Curated overlay did not answer.";

    internal const string UnreadableResultMessage =
        "Curated overlay returned an unreadable store result.";

    internal static TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    internal static TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    private const int MaxLineBytes = 65536;

    public static void Open(long appId, OverlayToStoreFlag flag)
    {
        if (!OverlayChannel.TryResolve(out var endpoint, out var token)
            || !OverlayNotifier.TryParseEndpoint(endpoint, out var host, out var port))
        {
            throw Fail(HttpStatusCode.ServiceUnavailable, null, OverlayUnavailableMessage, null);
        }

        var secret = token.Trim();
        var payload = BuildRequest(secret, appId, flag);
        TcpClient? client = null;
        try
        {
            client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            if (!connect.Wait(ConnectTimeout))
            {
                client.Close();
                Observe(connect);
                throw Fail(HttpStatusCode.ServiceUnavailable, null, OverlaySilentMessage, secret);
            }

            connect.GetAwaiter().GetResult();
            using var stream = client.GetStream();
            var writeTimeoutMs = (int)Math.Min(int.MaxValue, ConnectTimeout.TotalMilliseconds);
            var readTimeoutMs = (int)Math.Min(int.MaxValue, ReadTimeout.TotalMilliseconds);
            stream.WriteTimeout = writeTimeoutMs;
            stream.ReadTimeout = readTimeoutMs;
            var bytes = Encoding.UTF8.GetBytes(payload + "\n");
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            var line = ReadLine(stream);
            if (line is null)
            {
                throw Fail(HttpStatusCode.ServiceUnavailable, null, OverlaySilentMessage, secret);
            }

            if (!TryReadResult(line, out var ok, out var error))
            {
                throw Fail(HttpStatusCode.BadRequest, line, UnreadableResultMessage, secret);
            }

            if (ok)
            {
                return;
            }

            var message = string.IsNullOrWhiteSpace(error)
                ? "Curated could not open the store overlay."
                : error.Trim();
            throw Fail(HttpStatusCode.BadRequest, line, message, secret);
        }
        catch (CuratedException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Fail(HttpStatusCode.ServiceUnavailable, null, OverlaySilentMessage, secret);
        }
        finally
        {
            client?.Dispose();
        }
    }

    internal static string BuildRequest(string token, long appId, OverlayToStoreFlag flag)
    {
        var sb = new StringBuilder(128);
        sb.Append("{\"v\":1,\"token\":");
        AppendJsonString(sb, token);
        sb.Append(",\"type\":\"");
        sb.Append(RequestType);
        sb.Append("\",\"app_id\":");
        sb.Append(appId.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"flag\":");
        sb.Append(((byte)flag).ToString(CultureInfo.InvariantCulture));
        sb.Append('}');
        return sb.ToString();
    }

    internal static bool TryReadResult(string line, out bool ok, out string? error)
    {
        ok = false;
        error = null;
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

        if (Field(obj, "ok") is not bool succeeded)
        {
            return false;
        }

        ok = succeeded;
        if (!succeeded && Field(obj, "error") is string message)
        {
            error = message;
        }

        return true;
    }

    private static string? ReadLine(NetworkStream stream)
    {
        var buffer = new byte[1024];
        var pending = new List<byte>(256);
        try
        {
            while (pending.Count < MaxLineBytes)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
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
        catch (IOException)
        {
            return null;
        }

        return null;
    }

    private static void Observe(System.Threading.Tasks.Task task)
    {
        _ = task.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
            },
            System.Threading.CancellationToken.None,
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted,
            System.Threading.Tasks.TaskScheduler.Default);
    }

    private static object? Field(Dictionary<string, object?> obj, string name)
    {
        if (obj.TryGetValue(name, out var value))
        {
            return value;
        }

        return null;
    }

    private static CuratedException Fail(HttpStatusCode status, string? body, string message, string? secret)
    {
        if (body is not null && !string.IsNullOrEmpty(secret) && body.Contains(secret))
        {
            body = null;
        }

        return new CuratedException(status, body, message);
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
