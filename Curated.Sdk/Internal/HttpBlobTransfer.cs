using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal sealed class HttpBlobTransfer : IBlobTransfer, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _disposeHttp;
    private bool _disposed;

    public HttpBlobTransfer()
        : this(new HttpClient(), disposeHttp: true)
    {
    }

    public HttpBlobTransfer(HttpClient httpClient, bool disposeHttp = false)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _disposeHttp = disposeHttp;
    }

    public async Task UploadAsync(
        string signedUrl,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateUrl(signedUrl);
        ArgumentNullException.ThrowIfNull(content);

        using var request = new HttpRequestMessage(HttpMethod.Put, signedUrl);
        var streamContent = new StreamContent(content);
        if (contentLength >= 0)
        {
            streamContent.Headers.ContentLength = contentLength;
        }

        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content = streamContent;

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new CuratedException(response.StatusCode, body, "Cloud Save upload failed");
        }
    }

    public async Task DownloadAsync(
        string signedUrl,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateUrl(signedUrl);
        ArgumentNullException.ThrowIfNull(destination);

        using var response = await _http.GetAsync(
            signedUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new CuratedException(response.StatusCode, body, "Cloud Save download failed");
        }

        await response.Content.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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

    private static void ValidateUrl(string signedUrl)
    {
        if (string.IsNullOrWhiteSpace(signedUrl)
            || !Uri.TryCreate(signedUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("signedUrl must be an absolute URL", nameof(signedUrl));
        }
    }
}
