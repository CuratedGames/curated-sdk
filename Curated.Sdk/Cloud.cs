using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>Cloud Save: sync local files on launch and quit.</summary>
public static class Cloud
{
    /// <summary>
    /// Call on game launch. Downloads newer Cloud Save files into <paramref name="localFolder"/>.
    /// </summary>
    public static Task<CloudSyncResult> SyncOnLaunchAsync(
        string localFolder,
        CancellationToken cancellationToken = default)
    {
        var state = CuratedClient.State;
        return CloudSaveService.SyncOnLaunchAsync(state, localFolder, state.Blobs, cancellationToken);
    }

    /// <summary>
    /// Call on game quit. Uploads the listed relative paths under <paramref name="localFolder"/>.
    /// </summary>
    public static Task<CloudSyncResult> SyncOnQuitAsync(
        string localFolder,
        IEnumerable<string> relativePaths,
        CancellationToken cancellationToken = default)
    {
        var state = CuratedClient.State;
        return CloudSaveService.SyncOnQuitAsync(
            state,
            localFolder,
            relativePaths,
            state.Blobs,
            cancellationToken);
    }

    /// <summary>Call on game quit with a params list of relative paths.</summary>
    public static Task<CloudSyncResult> SyncOnQuitAsync(
        string localFolder,
        params string[] relativePaths) =>
        SyncOnQuitAsync(localFolder, (IEnumerable<string>)relativePaths);
}
