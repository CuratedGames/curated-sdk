using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class StatsService
{
    public static async Task<StatsProgressResponseWire> ProgressAsync(
        CuratedClient.ClientState state,
        CancellationToken ct)
    {
        var json = await state.Transport
            .SendAsync("GET", $"/games/app/{state.AppId}/stats/progress", null, ct)
            .ConfigureAwait(false);
        var result = Deserialize<StatsProgressResponseWire>(json);
        if (result.AppId == 0)
        {
            result.AppId = state.AppId;
        }

        return result;
    }

    public static async Task<SetStatResponseWire> SetAsync(
        CuratedClient.ClientState state,
        string apiName,
        double value,
        CancellationToken ct)
    {
        var json = await state.Transport
            .SendAsync(
                "PUT",
                $"/games/app/{state.AppId}/stats/{Uri.EscapeDataString(apiName)}",
                new SetStatRequestWire { Value = value },
                ct)
            .ConfigureAwait(false);
        var result = Deserialize<SetStatResponseWire>(json);
        if (result.AppId == 0)
        {
            result.AppId = state.AppId;
        }

        EnsureStatName(result.Stat, apiName);
        return result;
    }

    public static async Task<IncrementStatResponseWire> IncrementAsync(
        CuratedClient.ClientState state,
        string apiName,
        double delta,
        CancellationToken ct)
    {
        var json = await state.Transport
            .SendAsync(
                "POST",
                $"/games/app/{state.AppId}/stats/{Uri.EscapeDataString(apiName)}/increment",
                new IncrementStatRequestWire { Delta = delta },
                ct)
            .ConfigureAwait(false);
        var result = Deserialize<IncrementStatResponseWire>(json);
        if (result.AppId == 0)
        {
            result.AppId = state.AppId;
        }

        EnsureStatName(result.Stat, apiName);
        return result;
    }

    public static async Task<StoreStatsResponseWire> StoreAsync(
        CuratedClient.ClientState state,
        IReadOnlyList<StatUpdate> updates,
        CancellationToken ct)
    {
        var wire = new List<StoreStatEntryWire>(updates.Count);
        foreach (var update in updates)
        {
            wire.Add(
                new StoreStatEntryWire
                {
                    ApiName = update.ApiName,
                    Value = update.Value,
                    Delta = update.Delta
                });
        }

        var json = await state.Transport
            .SendAsync(
                "POST",
                $"/games/app/{state.AppId}/stats/store",
                new StoreStatsRequestWire { Stats = wire },
                ct)
            .ConfigureAwait(false);
        var result = Deserialize<StoreStatsResponseWire>(json);
        if (result.AppId == 0)
        {
            result.AppId = state.AppId;
        }

        return result;
    }

    private static void EnsureStatName(PlayerStatProgressWire? stat, string apiName)
    {
        if (stat is null)
        {
            throw new InvalidOperationException("stats response missing stat");
        }

        if (string.IsNullOrEmpty(stat.ApiName))
        {
            stat.ApiName = apiName;
        }
    }

    private static T Deserialize<T>(string json)
    {
        var value = Json.Deserialize<T>(json);
        if (value is null)
        {
            throw new InvalidOperationException($"failed to deserialize {typeof(T).Name}");
        }

        return value;
    }
}
