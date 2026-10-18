using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal sealed class MemoryBlobTransfer : IBlobTransfer
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, byte[]> Store => _store;

    public Task UploadAsync(
        string signedUrl,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            throw new ArgumentException("signedUrl is required", nameof(signedUrl));
        }

        ArgumentNullException.ThrowIfNull(content);
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _store[signedUrl] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task DownloadAsync(
        string signedUrl,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(signedUrl, out var bytes))
        {
            throw new CuratedException(HttpStatusCode.NotFound, null, "Cloud Save download failed");
        }

        destination.Write(bytes, 0, bytes.Length);
        return Task.CompletedTask;
    }

    public void Seed(string signedUrl, byte[] content) =>
        _store[signedUrl] = content ?? throw new ArgumentNullException(nameof(content));
}
