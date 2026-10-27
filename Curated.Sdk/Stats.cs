using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>
/// Read and write backend-defined player stats for the initialized app (Steam-like GetStat / SetStat / StoreStats).
/// </summary>
public static class Stats
{
    private static readonly object Gate = new();
    private static IReadOnlyList<Stat> _all = Array.Empty<Stat>();
    private static readonly Dictionary<string, Stat> ByName =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> Pending =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Stats for this app after <see cref="RefreshAsync"/>. Empty until refresh succeeds.
    /// </summary>
    public static IReadOnlyList<Stat> All
    {
        get
        {
            lock (Gate)
            {
                return _all;
            }
        }
    }

    /// <summary>Load stat definitions and current player values from Curated.</summary>
    public static async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var state = CuratedClient.GameState;
        var progress = await StatsService
            .ProgressAsync(state, cancellationToken)
            .ConfigureAwait(false);
        var list = new List<Stat>(progress.Stats.Count);
        lock (Gate)
        {
            ByName.Clear();
            Pending.Clear();
            foreach (var item in progress.Stats)
            {
                if (string.IsNullOrWhiteSpace(item.ApiName))
                {
                    continue;
                }

                var name = item.ApiName.Trim();
                var stat = FromWire(item);
                list.Add(stat);
                ByName[name] = stat;
            }

            _all = list;
        }
    }

    /// <summary>Steam-style GetStat for an int-backed value from the local cache.</summary>
    public static bool GetStat(string name, out int value)
    {
        EnsureEntitled();
        if (!TryGetCached(name, out var raw))
        {
            value = 0;
            return false;
        }

        value = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>Steam-style GetStat for a float-backed value from the local cache.</summary>
    public static bool GetStat(string name, out float value)
    {
        EnsureEntitled();
        if (!TryGetCached(name, out var raw))
        {
            value = 0f;
            return false;
        }

        value = (float)raw;
        return true;
    }

    /// <summary>Read the cached numeric value, or false when the name is unknown locally.</summary>
    public static bool TryGetStat(string name, out double value)
    {
        EnsureEntitled();
        return TryGetCached(name, out value);
    }

    /// <summary>
    /// Steam-style SetStat: update the local cache and mark the value dirty.
    /// Call <see cref="StoreStatsAsync(System.Threading.CancellationToken)"/> to push pending values to Curated.
    /// </summary>
    public static void SetStat(string name, int value) => SetStat(name, (double)value);

    /// <summary>
    /// Steam-style SetStat: update the local cache and mark the value dirty.
    /// Call <see cref="StoreStatsAsync(System.Threading.CancellationToken)"/> to push pending values to Curated.
    /// </summary>
    public static void SetStat(string name, float value) => SetStat(name, (double)value);

    /// <summary>
    /// Steam-style SetStat: update the local cache and mark the value dirty.
    /// Call <see cref="StoreStatsAsync(System.Threading.CancellationToken)"/> to push pending values to Curated.
    /// </summary>
    public static void SetStat(string name, double value)
    {
        EnsureEntitled();
        var trimmed = RequireName(name);
        lock (Gate)
        {
            Pending[trimmed] = value;
            if (ByName.TryGetValue(trimmed, out var stat))
            {
                stat.ApplyValue(value);
            }
            else
            {
                var created = new Stat(trimmed);
                created.ApplyValue(value);
                ByName[trimmed] = created;
                var list = new List<Stat>(_all.Count + 1);
                list.AddRange(_all);
                list.Add(created);
                _all = list;
            }
        }
    }

    /// <summary>
    /// Steam-style StoreStats: POST pending local SetStat values to
    /// <c>/games/app/{app_id}/stats/store</c>.
    /// </summary>
    public static async Task<StatStoreResult> StoreStatsAsync(
        CancellationToken cancellationToken = default)
    {
        List<StatUpdate> updates;
        lock (Gate)
        {
            if (Pending.Count == 0)
            {
                return new StatStoreResult(Array.Empty<StatWriteResult>(), Array.Empty<string>());
            }

            updates = new List<StatUpdate>(Pending.Count);
            foreach (var pair in Pending)
            {
                updates.Add(StatUpdate.Set(pair.Key, pair.Value));
            }
        }

        var result = await StoreStatsAsync(updates, cancellationToken).ConfigureAwait(false);
        lock (Gate)
        {
            Pending.Clear();
        }

        return result;
    }

    /// <summary>
    /// Set a stat immediately via <c>PUT /games/app/{app_id}/stats/{api_name}</c>.
    /// </summary>
    public static async Task<StatWriteResult> SetStatAsync(
        string name,
        double value,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireName(name);
        var response = await StatsService
            .SetAsync(CuratedClient.GameState, trimmed, value, cancellationToken)
            .ConfigureAwait(false);
        var stat = UpsertFromWire(response.Stat);
        lock (Gate)
        {
            Pending.Remove(trimmed);
        }

        OverlayNotifier.NotifyAchievementToasts(
            CuratedClient.GameState.AppId,
            response.UnlockedAchievementToasts,
            response.UnlockedAchievements);
        return new StatWriteResult(stat, response.PreviousValue, null, response.UnlockedAchievements);
    }

    /// <summary>
    /// Set a stat immediately via <c>PUT /games/app/{app_id}/stats/{api_name}</c>.
    /// </summary>
    public static Task<StatWriteResult> SetStatAsync(
        string name,
        int value,
        CancellationToken cancellationToken = default) =>
        SetStatAsync(name, (double)value, cancellationToken);

    /// <summary>
    /// Increment a stat via <c>POST /games/app/{app_id}/stats/{api_name}/increment</c>
    /// with body <c>{ "delta": ... }</c>.
    /// </summary>
    public static async Task<StatWriteResult> IncrementStatAsync(
        string name,
        double delta = 1,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireName(name);
        var response = await StatsService
            .IncrementAsync(CuratedClient.GameState, trimmed, delta, cancellationToken)
            .ConfigureAwait(false);
        var stat = UpsertFromWire(response.Stat);
        lock (Gate)
        {
            Pending.Remove(trimmed);
        }

        OverlayNotifier.NotifyAchievementToasts(
            CuratedClient.GameState.AppId,
            response.UnlockedAchievementToasts,
            response.UnlockedAchievements);
        return new StatWriteResult(
            stat,
            response.PreviousValue,
            response.Delta,
            response.UnlockedAchievements);
    }

    /// <summary>
    /// Increment a stat via <c>POST /games/app/{app_id}/stats/{api_name}/increment</c>.
    /// </summary>
    public static Task<StatWriteResult> IncrementStatAsync(
        string name,
        int delta,
        CancellationToken cancellationToken = default) =>
        IncrementStatAsync(name, (double)delta, cancellationToken);

    /// <summary>
    /// Batch write via <c>POST /games/app/{app_id}/stats/store</c>.
    /// Each update carries either <c>value</c> or <c>delta</c>.
    /// </summary>
    public static async Task<StatStoreResult> StoreStatsAsync(
        IEnumerable<StatUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        if (updates is null)
        {
            throw new ArgumentNullException(nameof(updates));
        }

        var list = new List<StatUpdate>();
        foreach (var update in updates)
        {
            if (update is null)
            {
                throw new ArgumentException("updates must not contain null entries", nameof(updates));
            }

            list.Add(update);
        }

        if (list.Count == 0)
        {
            throw new ArgumentException("stats store requires at least one update", nameof(updates));
        }

        if (list.Count > 64)
        {
            throw new ArgumentException("stats store accepts at most 64 updates", nameof(updates));
        }

        var response = await StatsService
            .StoreAsync(CuratedClient.GameState, list, cancellationToken)
            .ConfigureAwait(false);
        var updated = new List<StatWriteResult>(response.Updated.Count);
        lock (Gate)
        {
            foreach (var item in response.Updated)
            {
                if (item.Stat is null || string.IsNullOrWhiteSpace(item.Stat.ApiName))
                {
                    continue;
                }

                var stat = UpsertFromWire_NoLock(item.Stat);
                Pending.Remove(stat.Name);
                updated.Add(new StatWriteResult(stat, item.PreviousValue, null, Array.Empty<string>()));
            }
        }

        OverlayNotifier.NotifyAchievementToasts(
            CuratedClient.GameState.AppId,
            response.UnlockedAchievementToasts,
            response.UnlockedAchievements);
        return new StatStoreResult(updated, response.UnlockedAchievements);
    }

    /// <summary>Batch write via <c>POST /games/app/{app_id}/stats/store</c>.</summary>
    public static Task<StatStoreResult> StoreStatsAsync(params StatUpdate[] updates) =>
        StoreStatsAsync((IEnumerable<StatUpdate>)updates, default);

    private static bool TryGetCached(string name, out double value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            value = 0;
            return false;
        }

        var trimmed = name.Trim();
        lock (Gate)
        {
            if (Pending.TryGetValue(trimmed, out value))
            {
                return true;
            }

            if (ByName.TryGetValue(trimmed, out var stat))
            {
                value = stat.Value;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static Stat UpsertFromWire(PlayerStatProgressWire item)
    {
        lock (Gate)
        {
            return UpsertFromWire_NoLock(item);
        }
    }

    private static Stat UpsertFromWire_NoLock(PlayerStatProgressWire item)
    {
        var name = item.ApiName.Trim();
        if (ByName.TryGetValue(name, out var existing))
        {
            existing.ApplyProgress(item);
            return existing;
        }

        var created = FromWire(item);
        ByName[name] = created;
        var list = new List<Stat>(_all.Count + 1);
        list.AddRange(_all);
        list.Add(created);
        _all = list;
        return created;
    }

    private static Stat FromWire(PlayerStatProgressWire item)
    {
        var name = item.ApiName.Trim();
        return new Stat(
            name,
            string.IsNullOrEmpty(item.DisplayName) ? name : item.DisplayName,
            item.ValueType,
            item.Value,
            item.DefaultValue,
            item.MinValue,
            item.MaxValue,
            item.IncrementOnly,
            item.SetBy,
            item.Stored);
    }

    private static void EnsureEntitled() => _ = CuratedClient.GameState;

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("stat name is required", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 64)
        {
            throw new ArgumentException("stat name max length is 64", nameof(name));
        }

        return trimmed;
    }

    /// <summary>Clear cached stats (tests / shutdown).</summary>
    internal static void ResetCache()
    {
        lock (Gate)
        {
            _all = Array.Empty<Stat>();
            ByName.Clear();
            Pending.Clear();
        }
    }
}
