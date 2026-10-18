using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal sealed class HttpTransport : ITransport, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _disposeHttp;
    private bool _disposed;

    public HttpTransport(Uri hubUrl, string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(hubUrl);
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("session token is required", nameof(sessionToken));
        }

        _http = new HttpClient { BaseAddress = EnsureTrailingSlash(hubUrl) };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sessionToken.Trim());
        _disposeHttp = true;
    }

    public HttpTransport(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _disposeHttp = false;
    }

    public async Task<string> SendAsync(
        string method,
        string relativePath,
        object? jsonBody,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("method is required", nameof(method));
        }

        if (string.IsNullOrWhiteSpace(relativePath) || relativePath[0] != '/')
        {
            throw new ArgumentException("relativePath must start with /", nameof(relativePath));
        }

        using var request = new HttpRequestMessage(
            new HttpMethod(method.Trim().ToUpperInvariant()),
            relativePath);
        if (jsonBody is not null)
        {
            var payload = JsonSerializer.Serialize(jsonBody, JsonOptions.Default);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = TryReadError(body) ?? response.ReasonPhrase ?? "request failed";
            throw new CuratedException(response.StatusCode, body, detail);
        }

        return body;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_disposeHttp)
        {
            _http.Dispose();
        }
    }

    private static Uri EnsureTrailingSlash(Uri baseUrl)
    {
        var s = baseUrl.AbsoluteUri;
        if (!s.EndsWith('/'))
        {
            s += "/";
        }

        return new Uri(s, UriKind.Absolute);
    }

    private static string? TryReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.ValueKind == JsonValueKind.String)
            {
                return err.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore non-JSON error bodies
        }

        return null;
    }
}
