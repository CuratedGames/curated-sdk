using System;
using System.Collections.Generic;

namespace Curated.Sdk;

/// <summary>Result of a single-stat SetStat or IncrementStat call.</summary>
public sealed class StatWriteResult
{
    internal StatWriteResult(
        Stat stat,
        double previousValue,
        double? delta,
        IReadOnlyList<string> unlockedAchievements)
    {
        Stat = stat ?? throw new ArgumentNullException(nameof(stat));
        PreviousValue = previousValue;
        Delta = delta;
        UnlockedAchievements = unlockedAchievements ?? Array.Empty<string>();
    }

    /// <summary>Updated stat (local cache entry).</summary>
    public Stat Stat { get; }

    /// <summary>Value before this write.</summary>
    public double PreviousValue { get; }

    /// <summary>Applied delta for increment calls; null for absolute SetStat.</summary>
    public double? Delta { get; }

    /// <summary>Achievement API names newly unlocked by this write (may be empty).</summary>
    public IReadOnlyList<string> UnlockedAchievements { get; }

    /// <summary>Current value after the write.</summary>
    public double Value => Stat.Value;
}

/// <summary>Result of a batch stats store call.</summary>
public sealed class StatStoreResult
{
    internal StatStoreResult(
        IReadOnlyList<StatWriteResult> updated,
        IReadOnlyList<string> unlockedAchievements)
    {
        Updated = updated ?? Array.Empty<StatWriteResult>();
        UnlockedAchievements = unlockedAchievements ?? Array.Empty<string>();
    }

    /// <summary>Per-stat outcomes in request order when the hub returns them.</summary>
    public IReadOnlyList<StatWriteResult> Updated { get; }

    /// <summary>Deduped achievement API names newly unlocked across the batch.</summary>
    public IReadOnlyList<string> UnlockedAchievements { get; }
}
