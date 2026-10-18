using System;
using System.Collections.Generic;

namespace Curated.Sdk;

/// <summary>Outcome of a Cloud Save sync (paths that were transferred).</summary>
public sealed class CloudSyncResult
{
    public CloudSyncResult(IReadOnlyList<string> transferredPaths)
    {
        TransferredPaths = transferredPaths ?? Array.Empty<string>();
    }

    /// <summary>Relative paths that were uploaded or downloaded during this sync.</summary>
    public IReadOnlyList<string> TransferredPaths { get; }
}
