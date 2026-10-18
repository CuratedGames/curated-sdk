using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class CloudSaveService
{
    public static async Task<CloudSyncResult> SyncOnLaunchAsync(
        CuratedClient.ClientState state,
        string localRoot,
        IBlobTransfer blobs,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(localRoot))
        {
            throw new ArgumentException("local folder is required", nameof(localRoot));
        }

        ArgumentNullException.ThrowIfNull(blobs);

        Directory.CreateDirectory(localRoot);
        var sync = await SyncAsync(state, "download", Array.Empty<CloudSaveFile>(), ct)
            .ConfigureAwait(false);
        var transferred = new List<string>();

        foreach (var item in sync.Urls)
        {
            ct.ThrowIfCancellationRequested();
            if (!NeedsTransfer(item, "get") || string.IsNullOrWhiteSpace(item.RelativePath))
            {
                continue;
            }

            var relative = NormalizeRelative(item.RelativePath);
            var destPath = SafeCombine(localRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            await using (var fs = new FileStream(
                destPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await blobs.DownloadAsync(item.Url!, fs, ct).ConfigureAwait(false);
            }

            transferred.Add(relative);
        }

        return new CloudSyncResult(transferred);
    }

    public static async Task<CloudSyncResult> SyncOnQuitAsync(
        CuratedClient.ClientState state,
        string localRoot,
        IEnumerable<string> relativePaths,
        IBlobTransfer blobs,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(localRoot))
        {
            throw new ArgumentException("local folder is required", nameof(localRoot));
        }

        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(blobs);

        var files = new List<CloudSaveFile>();
        foreach (var raw in relativePaths)
        {
            ct.ThrowIfCancellationRequested();
            var relative = NormalizeRelative(raw);
            var path = SafeCombine(localRoot, relative);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Cloud Save file missing", path);
            }

            var info = new FileInfo(path);
            var hash = await CloudSaveHash.OfFileAsync(path, ct).ConfigureAwait(false);
            files.Add(new CloudSaveFile
            {
                RelativePath = relative,
                ContentHash = hash,
                SizeBytes = info.Length
            });
        }

        var sync = await SyncAsync(state, "upload", files, ct).ConfigureAwait(false);
        var transferred = new List<string>();
        var byPath = sync.Urls.ToDictionary(
            u => NormalizeRelative(u.RelativePath),
            u => u,
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            if (!byPath.TryGetValue(file.RelativePath, out var item) || !NeedsTransfer(item, "put"))
            {
                continue;
            }

            var path = SafeCombine(localRoot, file.RelativePath);
            await using var fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            await blobs.UploadAsync(item.Url!, fs, file.SizeBytes, ct).ConfigureAwait(false);
            transferred.Add(file.RelativePath);
        }

        return new CloudSyncResult(transferred);
    }

    internal static bool NeedsTransfer(CloudSaveSignedUrl item, string expectedAction)
    {
        if (string.IsNullOrWhiteSpace(item.Url))
        {
            return false;
        }

        if (string.Equals(item.Action, "unchanged", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Action))
        {
            return true;
        }

        return string.Equals(item.Action, expectedAction, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<CloudSaveSyncResultWire> SyncAsync(
        CuratedClient.ClientState state,
        string direction,
        IReadOnlyList<CloudSaveFile> files,
        CancellationToken ct)
    {
        var json = await state.Transport
            .SendAsync(
                "POST",
                $"/games/app/{state.AppId}/cloud-save/sync",
                new CloudSaveSyncRequest { Direction = direction, Files = files },
                ct)
            .ConfigureAwait(false);
        return Deserialize<CloudSaveSyncResultWire>(json);
    }

    private static string NormalizeRelative(string relativePath)
    {
        var t = relativePath.Trim().Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrEmpty(t) || t.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("invalid relative path", nameof(relativePath));
        }

        return t;
    }

    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root);
        var combined = Path.GetFullPath(
            Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(
                fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
            && !string.Equals(combined, fullRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("path escapes local folder");
        }

        return combined;
    }

    private static T Deserialize<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions.Default);
        if (value is null)
        {
            throw new InvalidOperationException($"failed to deserialize {typeof(T).Name}");
        }

        return value;
    }
}
