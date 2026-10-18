using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class CloudSaveHash
{
    public static string OfBytes(ReadOnlySpan<byte> data)
    {
        var hash = SHA256.HashData(data);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<string> OfStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<string> OfFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return await OfStreamAsync(fs, cancellationToken).ConfigureAwait(false);
    }
}
