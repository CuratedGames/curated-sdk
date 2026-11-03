using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

/// <summary>
/// Connects the Unity Editor to a running Curated launcher that did not start this process.
/// Sends <c>editor_attach</c> with the signed-in account token. On success, adopts the
/// reply's endpoint, overlay token, and overlay page URL. Does not require
/// <c>CURATED_LAUNCHER</c>.
/// </summary>
internal static class EditorLauncherBridge
{
    internal const string BridgeVariable = "CURATED_EDITOR_BRIDGE";
    internal const string BridgeFileName = "editor-bridge.json";
    internal const string AttachType = "editor_attach";
    internal const string AttachResultType = "editor_attach_result";
    internal const string NotSignedInError = "not_signed_in";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    internal sealed class EditorAttachResult
    {
        private EditorAttachResult(
            bool succeeded,
            bool notSignedIn,
            string endpoint,
            string overlayToken,
            string overlayUrl,
            string accountToken)
        {
            Succeeded = succeeded;
            NotSignedIn = notSignedIn;
            Endpoint = endpoint;
            OverlayToken = overlayToken;
            OverlayUrl = overlayUrl;
            AccountToken = accountToken;
        }

        internal static EditorAttachResult SignedOut() =>
            new(false, true, "", "", "", "");

        internal static EditorAttachResult Ok(
            string endpoint,
            string overlayToken,
            string overlayUrl,
            string accountToken) =>
            new(true, false, endpoint, overlayToken, overlayUrl, accountToken);

        public bool Succeeded { get; }

        public bool NotSignedIn { get; }

        public string Endpoint { get; }

        public string OverlayToken { get; }

        public string OverlayUrl { get; }

        public string AccountToken { get; }
    }

    /// <summary>
    /// Ask the running launcher to attach this editor. Returns null when no loopback
    /// listener answers. Returns <see cref="EditorAttachResult.NotSignedIn"/> when the
    /// launcher replies <c>not_signed_in</c>. Never throws except when
    /// <paramref name="cancellationToken"/> is canceled.
    /// </summary>
    public static async Task<EditorAttachResult?> TryAttachAsync(
        long appId,
        string? accountToken,
        CancellationToken cancellationToken)
    {
        if (appId <= 0 || string.IsNullOrWhiteSpace(accountToken))
        {
            return null;
        }

        var token = accountToken.Trim();
        var endpoint = ResolveEndpoint();
        if (endpoint is null || !TryParseLoopback(endpoint, out var host, out var port))
        {
            return null;
        }

        TcpClient? client = null;
        try
        {
            client = new TcpClient();
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(ConnectTimeout);
            var connect = client.ConnectAsync(host, port);
            var delay = Task.Delay(Timeout.InfiniteTimeSpan, connectTimeout.Token);
            var done = await Task.WhenAny(connect, delay).ConfigureAwait(false);
            connectTimeout.Cancel();
            Observe(delay);
            if (done != connect)
            {
                client.Close();
                Observe(connect);
                return null;
            }

            await connect.ConfigureAwait(false);
            using var stream = client.GetStream();
            var request = Encoding.UTF8.GetBytes(BuildAttachRequest(appId, token) + "\n");
            await stream.WriteAsync(request, 0, request.Length, cancellationToken).ConfigureAwait(false);
            var line = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return null;
            }

            var parsed = ParseAttachResponse(line);
            if (parsed is null || !parsed.Succeeded)
            {
                return parsed;
            }

            return EditorAttachResult.Ok(parsed.Endpoint, parsed.OverlayToken, parsed.OverlayUrl, token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            client?.Dispose();
        }
    }

    /// <summary>
    /// Remember the launcher overlay for this process. Does not log the overlay token or URL.
    /// </summary>
    public static void Apply(EditorAttachResult result)
    {
        if (!result.Succeeded)
        {
            return;
        }

        OverlayChannel.Adopt(result.Endpoint, result.OverlayToken, result.OverlayUrl);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, result.Endpoint);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, result.OverlayToken);
    }

    internal static string BuildAttachRequest(long appId, string token)
    {
        var sb = new StringBuilder(128);
        sb.Append("{\"v\":1,\"type\":\"");
        sb.Append(AttachType);
        sb.Append("\",\"app_id\":");
        sb.Append(appId.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"token\":");
        AppendJsonString(sb, token);
        sb.Append('}');
        return sb.ToString();
    }

    internal static EditorAttachResult? ParseAttachResponse(string line)
    {
        object? root;
        try
        {
            root = Json.Parse(line.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        if (root is not Dictionary<string, object?> obj)
        {
            return null;
        }

        if (!string.Equals(Text(Field(obj, "type")), AttachResultType, StringComparison.Ordinal))
        {
            return null;
        }

        var error = Text(Field(obj, "error"));
        if (string.Equals(error, NotSignedInError, StringComparison.Ordinal))
        {
            return EditorAttachResult.SignedOut();
        }

        if (!string.IsNullOrEmpty(error))
        {
            return null;
        }

        if (Field(obj, "ok") is bool ok && !ok)
        {
            return null;
        }

        var endpoint = Text(Field(obj, "endpoint"));
        var overlayToken = Text(Field(obj, "overlay_token"));
        var overlayUrl = Text(Field(obj, "overlay_url"));
        if (string.IsNullOrEmpty(endpoint)
            || string.IsNullOrEmpty(overlayToken)
            || string.IsNullOrEmpty(overlayUrl)
            || !TryParseLoopback(endpoint, out _, out _)
            || !IsLoopbackOverlayUrl(overlayUrl, overlayToken))
        {
            return null;
        }

        return EditorAttachResult.Ok(endpoint, overlayToken, overlayUrl, "");
    }

    internal static bool IsLoopbackOverlayUrl(string url, string overlayToken)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrEmpty(overlayToken))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!IsLoopbackHost(uri.Host))
        {
            return false;
        }

        var fragment = uri.Fragment.Length > 0 ? uri.Fragment.Substring(1) : "";
        fragment = Uri.UnescapeDataString(fragment);
        return fragment.Length > 0 && string.Equals(fragment, overlayToken, StringComparison.Ordinal);
    }

    internal static string? ReadBridgeEndpoint(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        object? root;
        try
        {
            root = Json.Parse(json);
        }
        catch (FormatException)
        {
            return null;
        }

        if (root is not Dictionary<string, object?> obj)
        {
            return null;
        }

        var editorEndpoint = Text(Field(obj, "editor_endpoint"));
        if (!string.IsNullOrEmpty(editorEndpoint))
        {
            return editorEndpoint;
        }

        return Text(Field(obj, "endpoint"));
    }

    internal static bool TryParseLoopback(string endpoint, out string host, out int port)
    {
        host = "";
        port = 0;
        if (!OverlayNotifier.TryParseEndpoint(endpoint, out host, out port))
        {
            return false;
        }

        host = host.Trim().TrimStart('[').TrimEnd(']');
        return IsLoopbackHost(host);
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private static string? ResolveEndpoint()
    {
        var fromEnv = Environment.GetEnvironmentVariable(BridgeVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        // The Unity Editor often cannot read another process's path, so a file in the
        // user profile is checked before Config beside the launcher executable.
        var fromProfile = ReadBridgeEndpoint(ReadUserBridgeFile());
        if (!string.IsNullOrEmpty(fromProfile))
        {
            return fromProfile;
        }

        var fromConfig = ReadBridgeEndpoint(LocalAccountSession.TryReadRunningLauncherConfig(BridgeFileName));
        if (!string.IsNullOrEmpty(fromConfig))
        {
            return fromConfig;
        }

        var fromSession = ReadBridgeEndpoint(LocalAccountSession.TryReadRunningLauncherConfig("session.json"));
        if (!string.IsNullOrEmpty(fromSession))
        {
            return fromSession;
        }

        return ReadBridgeEndpoint(LocalAccountSession.TryReadCredentialsJson());
    }

    private static string? ReadUserBridgeFile()
    {
        var home = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetEnvironmentVariable("HOME");
        }

        if (string.IsNullOrEmpty(home))
        {
            return null;
        }

        var path = Path.Combine(home, ".curated", BridgeFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<string?> ReadLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);
        var buffer = new byte[1024];
        var pending = new List<byte>(128);
        try
        {
            while (pending.Count < 65536)
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

    private static object? Field(Dictionary<string, object?> obj, string name)
    {
        if (obj.TryGetValue(name, out var value))
        {
            return value;
        }

        return null;
    }

    private static string? Text(object? value)
    {
        if (value is not string text)
        {
            return null;
        }

        text = text.Trim();
        return text.Length == 0 ? null : text;
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
