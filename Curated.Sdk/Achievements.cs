using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>List and unlock achievements for the initialized app.</summary>
public static class Achievements
{
    private static IReadOnlyList<Achievement> _all = System.Array.Empty<Achievement>();

    /// <summary>
    /// Achievements for this app after <see cref="RefreshAsync"/>. Empty until refresh succeeds.
    /// </summary>
    public static IReadOnlyList<Achievement> All => _all;

    /// <summary>Load achievement defs and unlock state from Curated.</summary>
    public static async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var state = CuratedClient.GameState;
        var progress = await AchievementsService
            .ProgressAsync(state, cancellationToken)
            .ConfigureAwait(false);
        var list = new List<Achievement>(progress.Achievements.Count);
        foreach (var item in progress.Achievements)
        {
            list.Add(new Achievement(
                item.ApiName,
                item.DisplayName,
                item.Description,
                item.Unlocked,
                item.UnlockedAt));
        }

        _all = list;
    }

    /// <summary>Unlock by name. Equivalent to <c>new Achievement(name).TriggerAsync()</c>.</summary>
    public static Task<bool> TriggerAsync(string name, CancellationToken cancellationToken = default) =>
        new Achievement(name).TriggerAsync(cancellationToken);
}
