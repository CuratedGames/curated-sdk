using System;

namespace Curated.Sdk.Internal;

/// <summary>
/// Overlay endpoint and token for this process. An editor attach adopts them in memory.
/// Process environment is used only when nothing has been adopted, so clearing the
/// environment after a successful attach still reaches the launcher overlay.
/// </summary>
internal static class OverlayChannel
{
    private static readonly object Gate = new();
    private static string? _endpoint;
    private static string? _token;
    private static string? _overlayUrl;

    internal static void Adopt(string endpoint, string token, string? overlayUrl)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        lock (Gate)
        {
            _endpoint = endpoint.Trim();
            _token = token.Trim();
            _overlayUrl = string.IsNullOrWhiteSpace(overlayUrl) ? null : overlayUrl.Trim();
        }
    }

    internal static void Clear()
    {
        lock (Gate)
        {
            _endpoint = null;
            _token = null;
            _overlayUrl = null;
        }
    }

    internal static string? OverlayUrl
    {
        get
        {
            lock (Gate)
            {
                return _overlayUrl;
            }
        }
    }

    /// <summary>
    /// Adopted credentials win. Otherwise read <c>CURATED_OVERLAY_ENDPOINT</c> and
    /// <c>CURATED_OVERLAY_TOKEN</c>.
    /// </summary>
    internal static bool TryResolve(out string endpoint, out string token)
    {
        lock (Gate)
        {
            if (!string.IsNullOrWhiteSpace(_endpoint) && !string.IsNullOrWhiteSpace(_token))
            {
                endpoint = _endpoint;
                token = _token;
                return true;
            }
        }

        var fromEnvEndpoint = Environment.GetEnvironmentVariable(LaunchContext.OverlayEndpointVariable);
        var fromEnvToken = Environment.GetEnvironmentVariable(LaunchContext.OverlayTokenVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvEndpoint) && !string.IsNullOrWhiteSpace(fromEnvToken))
        {
            endpoint = fromEnvEndpoint.Trim();
            token = fromEnvToken.Trim();
            return true;
        }

        endpoint = "";
        token = "";
        return false;
    }
}
