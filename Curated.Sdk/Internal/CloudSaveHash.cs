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
        var hash = Compat.Sha256(data.ToArray());
        return "sha256:" + Compat.ToLowerHex(hash);
    }

    public static async Task<string> OfStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream, nameof(stream));
        using var sha = SHA256.Create();
#if NET5_0_OR_GREATER
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
#else
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hash = sha.Hash!;
#endif
        return "sha256:" + Compat.ToLowerHex(hash);
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
