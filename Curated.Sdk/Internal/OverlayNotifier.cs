using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

/// <summary>
/// Fire-and-forget notify to the Curated launcher overlay over loopback TCP.
/// Never throws to callers; never logs the overlay token.
/// </summary>
internal static class OverlayNotifier
{
    private const string EndpointVariable = "CURATED_OVERLAY_ENDPOINT";
    private const string TokenVariable = "CURATED_OVERLAY_TOKEN";
    private const int ConnectTimeoutMs = 750;

    public static void NotifyAchievementUnlocked(
        long appId,
        string apiName,
        string displayName,
        string description,
        string? iconUrl,
        double? globalUnlockPercent)
    {
        if (appId <= 0 || string.IsNullOrWhiteSpace(apiName))
        {
            return;
        }

        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        var token = Environment.GetEnvironmentVariable(TokenVariable);
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (!TryParseEndpoint(endpoint.Trim(), out var host, out var port))
        {
            return;
        }

        var payload = BuildPayload(
            token.Trim(),
            appId,
            apiName.Trim(),
            displayName ?? "",
            description ?? "",
            iconUrl,
            globalUnlockPercent);

        // Detach: unlock path must not wait on the overlay.
        _ = Task.Run(() => SendQuiet(host, port, payload));
    }

    public static void NotifyAchievementToasts(
        long appId,
        IReadOnlyList<AchievementToastWire>? toasts,
        IReadOnlyList<string>? apiNamesFallback)
    {
        if (toasts is { Count: > 0 })
        {
            foreach (var toast in toasts)
            {
                if (toast is null || string.IsNullOrWhiteSpace(toast.ApiName))
                {
                    continue;
                }

                NotifyAchievementUnlocked(
                    appId,
                    toast.ApiName,
                    string.IsNullOrEmpty(toast.DisplayName) ? toast.ApiName : toast.DisplayName,
                    toast.Description ?? "",
                    toast.IconAchievedUri,
                    toast.GlobalUnlockPercent);
            }

            return;
        }

        if (apiNamesFallback is null)
        {
            return;
        }

        foreach (var name in apiNamesFallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var trimmed = name.Trim();
            var display = trimmed;
            var description = "";
            foreach (var known in Achievements.All)
            {
                if (string.Equals(known.Name, trimmed, StringComparison.Ordinal))
                {
                    display = known.DisplayName;
                    description = known.Description;
                    break;
                }
            }

            NotifyAchievementUnlocked(appId, trimmed, display, description, null, null);
        }
    }

    internal static string BuildPayload(
        string token,
        long appId,
        string apiName,
        string displayName,
        string description,
        string? iconUrl,
        double? globalUnlockPercent)
    {
        var sb = new StringBuilder(256);
        sb.Append("{\"v\":1,\"token\":");
        AppendJsonString(sb, token);
        sb.Append(",\"type\":\"achievement_unlocked\",\"app_id\":");
        sb.Append(appId);
        sb.Append(",\"api_name\":");
        AppendJsonString(sb, apiName);
        sb.Append(",\"display_name\":");
        AppendJsonString(sb, displayName);
        sb.Append(",\"description\":");
        AppendJsonString(sb, description);
        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            sb.Append(",\"icon_url\":");
            AppendJsonString(sb, iconUrl.Trim());
        }

        if (globalUnlockPercent is double pct)
        {
            sb.Append(",\"global_unlock_percent\":");
            sb.Append(pct.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        sb.Append('}');
        return sb.ToString();
    }

    internal static bool TryParseEndpoint(string endpoint, out string host, out int port)
    {
        host = "127.0.0.1";
        port = 0;
        var trimmed = endpoint.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("http://".Length);
        }

        var colon = trimmed.LastIndexOf(':');
        if (colon <= 0 || colon == trimmed.Length - 1)
        {
            return false;
        }

        host = trimmed.Substring(0, colon).Trim();
        if (string.IsNullOrEmpty(host))
        {
            host = "127.0.0.1";
        }

        return int.TryParse(
                   trimmed.Substring(colon + 1).Trim(),
                   System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out port)
               && port > 0
               && port <= 65535;
    }

    private static void SendQuiet(string host, int port, string payload)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            if (!connect.Wait(ConnectTimeoutMs))
            {
                return;
            }

            connect.GetAwaiter().GetResult();
            var bytes = Encoding.UTF8.GetBytes(payload + "\n");
            using var stream = client.GetStream();
            stream.WriteTimeout = ConnectTimeoutMs;
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
        catch
        {
            // Overlay down or filtered: unlock already succeeded.
        }
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
                        sb.Append(((int)ch).ToString("x4"));
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
